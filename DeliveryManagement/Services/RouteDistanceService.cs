
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace DeliveryManagement.Services;

public sealed class RouteDistanceResult
{
    public bool Success { get; init; }
    public decimal DistanceKm { get; init; }
    public string Message { get; init; } = string.Empty;

    // Tọa độ được xác định từ địa chỉ người gửi, không lấy GPS thay thế.
    public double? SenderLatitude { get; init; }
    public double? SenderLongitude { get; init; }
}

public interface IRouteDistanceService
{
    Task<RouteDistanceResult> CalculateDistanceKmAsync(
        string senderAddress,
        string receiverAddress,
        double? senderLat = null,
        double? senderLon = null);
}

public sealed class RouteDistanceService : IRouteDistanceService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RouteDistanceService> _logger;

    private static readonly SemaphoreSlim GeocodeLock = new(1, 1);
    private static DateTime _lastGeocodeRequestUtc = DateTime.MinValue;

    public RouteDistanceService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<RouteDistanceService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(25);

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "DeliveryManagement/1.0");
        }
    }

    public async Task<RouteDistanceResult> CalculateDistanceKmAsync(
        string senderAddress,
        string receiverAddress,
        double? senderLat = null,
        double? senderLon = null)
    {
        if (string.IsNullOrWhiteSpace(senderAddress) ||
            string.IsNullOrWhiteSpace(receiverAddress))
        {
            return Failure(
                "Vui lòng nhập đầy đủ địa chỉ gửi và địa chỉ nhận.");
        }

        // Không sử dụng senderLat/senderLon để tính tuyến đường.
        // Luôn định vị theo địa chỉ văn bản người dùng đã nhập.
        var currentStage = "tìm tọa độ địa chỉ người gửi";

        try
        {
            var senderPoint = await GeocodeAsync(senderAddress);

            if (senderPoint == null)
            {
                return Failure(
                    "Không tìm thấy địa chỉ người gửi. " +
                    "Hãy nhập địa chỉ cụ thể hơn, gồm đường, phường, quận và thành phố.");
            }

            currentStage = "tìm tọa độ địa chỉ người nhận";

            var receiverPoint = await GeocodeAsync(receiverAddress);

            if (receiverPoint == null)
            {
                return Failure(
                    "Không tìm thấy địa chỉ người nhận. " +
                    "Hãy kiểm tra địa chỉ và bổ sung quận, thành phố.");
            }

            currentStage = "tính tuyến đường bộ OSRM";

            var url =
                "https://router.project-osrm.org/route/v1/driving/" +
                $"{senderPoint.Value.Longitude.ToString(CultureInfo.InvariantCulture)}," +
                $"{senderPoint.Value.Latitude.ToString(CultureInfo.InvariantCulture)};" +
                $"{receiverPoint.Value.Longitude.ToString(CultureInfo.InvariantCulture)}," +
                $"{receiverPoint.Value.Latitude.ToString(CultureInfo.InvariantCulture)}" +
                "?overview=false&alternatives=false&steps=false";

            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OSRM trả về HTTP {StatusCode}.",
                    (int)response.StatusCode);

                return Failure(
                    $"Dịch vụ tính tuyến đường trả về lỗi HTTP {(int)response.StatusCode}. " +
                    "Vui lòng thử lại sau.");
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync();

            using var document =
                await JsonDocument.ParseAsync(stream);

            var root = document.RootElement;

            if (!root.TryGetProperty("code", out var code) ||
                code.GetString() != "Ok" ||
                !root.TryGetProperty("routes", out var routes) ||
                routes.ValueKind != JsonValueKind.Array ||
                routes.GetArrayLength() == 0)
            {
                return Failure(
                    "Không tìm được tuyến đường bộ giữa hai địa chỉ. " +
                    "Hãy kiểm tra lại địa chỉ gửi và nhận.");
            }

            var meters = routes[0]
                .GetProperty("distance")
                .GetDouble();

            var distanceKm = Math.Round(
                (decimal)meters / 1000m,
                2,
                MidpointRounding.AwayFromZero);

            if (distanceKm <= 0)
            {
                return Failure(
                    "Khoảng cách tuyến đường không hợp lệ.");
            }

            return new RouteDistanceResult
            {
                Success = true,
                DistanceKm = distanceKm,
                Message = "Đã tính quãng đường theo tuyến đường bộ.",

                SenderLatitude = senderPoint.Value.Latitude,
                SenderLongitude = senderPoint.Value.Longitude
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Hết thời gian chờ tại bước {Stage}.",
                currentStage);

            return Failure(
                $"Bước {currentStage} bị hết thời gian chờ. " +
                "Vui lòng thử lại.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Lỗi kết nối tại bước {Stage}.",
                currentStage);

            return Failure(
                $"Không kết nối được dịch vụ ở bước: {currentStage}. " +
                "Hãy kiểm tra kết nối Internet của máy chủ rồi thử lại.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Dữ liệu JSON không hợp lệ tại bước {Stage}.",
                currentStage);

            return Failure(
                $"Dữ liệu trả về không hợp lệ tại bước: {currentStage}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi tính khoảng cách tại bước {Stage}.",
                currentStage);

            return Failure(
                "Có lỗi xảy ra khi tính khoảng cách. " +
                "Kiểm tra log ứng dụng để biết chi tiết.");
        }
    }


    private async Task<GeoPoint?> GeocodeAsync(string address)
    {
        var normalizedAddress = address.Trim();

        if (!normalizedAddress.Contains(
                "Vietnam", StringComparison.OrdinalIgnoreCase) &&
            !normalizedAddress.Contains(
                "Việt Nam", StringComparison.OrdinalIgnoreCase))
        {
            normalizedAddress += ", Việt Nam";
        }

        var cacheKey =
            "geocode_photon_" + normalizedAddress.ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out GeoPoint cachedPoint))
        {
            return cachedPoint;
        }

        await GeocodeLock.WaitAsync();

        try
        {
            // Kiểm tra cache lần nữa sau khi chờ khóa.
            if (_cache.TryGetValue(cacheKey, out cachedPoint))
            {
                return cachedPoint;
            }

            // Giới hạn tốc độ truy vấn dịch vụ geocoding công cộng.
            var elapsed = DateTime.UtcNow - _lastGeocodeRequestUtc;
            var remaining = TimeSpan.FromSeconds(1.1) - elapsed;

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }

            // Dùng Photon thay cho Nominatim.
            // Photon hỗ trợ lang=en; không dùng lang=vi.
            var url =
                "https://photon.komoot.io/api/" +
                "?q=" + Uri.EscapeDataString(normalizedAddress) +
                "&limit=1&lang=en";

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.GetAsync(url);
            }
            finally
            {
                _lastGeocodeRequestUtc = DateTime.UtcNow;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Photon trả về HTTP {StatusCode} khi tìm địa chỉ.",
                        (int)response.StatusCode);

                    return null;
                }

                await using var stream =
                    await response.Content.ReadAsStreamAsync();

                using var document =
                    await JsonDocument.ParseAsync(stream);

                var root = document.RootElement;

                if (!root.TryGetProperty("features", out var features) ||
                    features.ValueKind != JsonValueKind.Array ||
                    features.GetArrayLength() == 0)
                {
                    _logger.LogInformation(
                        "Photon không tìm thấy địa chỉ: {Address}",
                        normalizedAddress);

                    return null;
                }

                var first = features[0];

                if (!first.TryGetProperty("geometry", out var geometry) ||
                    !geometry.TryGetProperty("coordinates", out var coordinates) ||
                    coordinates.ValueKind != JsonValueKind.Array ||
                    coordinates.GetArrayLength() < 2)
                {
                    _logger.LogWarning(
                        "Photon trả về tọa độ không hợp lệ cho địa chỉ: {Address}",
                        normalizedAddress);

                    return null;
                }

                // GeoJSON luôn lưu thứ tự [longitude, latitude].
                var lon = coordinates[0].GetDouble();
                var lat = coordinates[1].GetDouble();

                if (!double.IsFinite(lat) ||
                    !double.IsFinite(lon) ||
                    lat < -90 || lat > 90 ||
                    lon < -180 || lon > 180)
                {
                    return null;
                }

                var point = new GeoPoint(lat, lon);

                _cache.Set(
                    cacheKey,
                    point,
                    TimeSpan.FromHours(24));

                _logger.LogInformation(
                    "Đã định vị địa chỉ {Address} tại {Latitude}, {Longitude}",
                    normalizedAddress,
                    lat,
                    lon);

                return point;
            }
        }
        finally
        {
            GeocodeLock.Release();
        }
    }

    private static RouteDistanceResult Failure(string message)
    {
        return new RouteDistanceResult
        {
            Success = false,
            DistanceKm = 0,
            Message = message
        };
    }

    private readonly record struct GeoPoint(
        double Latitude,
        double Longitude);
}
