/*
 * Bản đồ vị trí shipper trực tiếp.
 *  - [data-shipper-map]      : bản đồ của một đơn (khách, admin, kho, shipper).
 *  - [data-shipper-overview] : tất cả shipper đang giao (admin, kho).
 * Vị trí do shipper gửi lên server (auto-checkin.js); trang này chỉ đọc
 * lại mỗi vài giây và di chuyển marker mượt trên bản đồ.
 */
(function () {
    'use strict';

    var LEAFLET_CSS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
    var LEAFLET_CSS_SRI = 'sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=';
    var LEAFLET_JS = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
    var LEAFLET_JS_SRI = 'sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=';

    var POLL_ACTIVE_MS = 5000;    // đang giao: hỏi lại mỗi 5 giây
    var POLL_IDLE_MS = 15000;     // chưa giao / chưa có vị trí: 15 giây
    var STALE_AFTER_S = 60;       // quá 60 giây không có điểm mới thì cảnh báo
    var DEFAULT_CENTER = [10.7769, 106.7009]; // TP.HCM
    var ANIMATION_MS = 1500;

    function ensureLeaflet(callback) {
        if (window.L) { callback(); return; }

        if (!document.querySelector('link[data-leaflet-css]')) {
            var link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = LEAFLET_CSS;
            link.integrity = LEAFLET_CSS_SRI;
            link.crossOrigin = '';
            link.setAttribute('data-leaflet-css', '1');
            document.head.appendChild(link);
        }

        var script = document.createElement('script');
        script.src = LEAFLET_JS;
        script.integrity = LEAFLET_JS_SRI;
        script.crossOrigin = '';
        script.onload = callback;
        script.onerror = function () {
            document.querySelectorAll('[data-sm-status]').forEach(function (e) {
                e.textContent = 'Không tải được thư viện bản đồ. Kiểm tra kết nối mạng.';
            });
        };
        document.head.appendChild(script);
    }

    function createMap(canvas) {
        var map = L.map(canvas).setView(DEFAULT_CENTER, 12);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(map);
        return map;
    }

    function pinIcon(cssClass, iconClass) {
        return L.divIcon({
            className: 'sm-marker-wrap',
            html: '<div class="sm-pin ' + cssClass + '"><i class="bi ' + iconClass + '"></i></div>',
            iconSize: [38, 38],
            iconAnchor: [19, 19],
            popupAnchor: [0, -18]
        });
    }

    function shipperIcon() {
        return L.divIcon({
            className: 'sm-marker-wrap',
            html: '<div class="sm-shipper"><div class="sm-pulse"></div>'
                + '<div class="sm-pin sm-pin-shipper"><i class="bi bi-bicycle"></i></div></div>',
            iconSize: [44, 44],
            iconAnchor: [22, 22],
            popupAnchor: [0, -20]
        });
    }

    // Nội dung popup dựng bằng textContent để tránh chèn HTML từ dữ liệu người dùng.
    function popupContent(title, text) {
        var box = document.createElement('div');
        var strong = document.createElement('strong');
        strong.textContent = title;
        box.appendChild(strong);
        if (text) {
            var p = document.createElement('div');
            p.className = 'small';
            p.textContent = text;
            box.appendChild(p);
        }
        return box;
    }

    // Di chuyển marker mượt từ vị trí cũ sang vị trí mới.
    function animateMarker(marker, to) {
        var from = marker.getLatLng();
        if (marker._smAnim) cancelAnimationFrame(marker._smAnim);

        if (Math.abs(from.lat - to[0]) < 1e-7 && Math.abs(from.lng - to[1]) < 1e-7) return;

        var start = performance.now();
        function step(now) {
            var t = Math.min(1, (now - start) / ANIMATION_MS);
            var e = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2; // ease in-out
            marker.setLatLng([
                from.lat + (to[0] - from.lat) * e,
                from.lng + (to[1] - from.lng) * e
            ]);
            if (t < 1) marker._smAnim = requestAnimationFrame(step);
        }
        marker._smAnim = requestAnimationFrame(step);
    }

    function agoText(seconds) {
        if (seconds < 5) return 'vừa xong';
        if (seconds < 60) return seconds + ' giây trước';
        var m = Math.floor(seconds / 60);
        if (m < 60) return m + ' phút trước';
        return Math.floor(m / 60) + ' giờ trước';
    }

    async function getJson(url) {
        var res = await fetch(url, {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        });
        if (res.status === 403) {
            var forbidden = new Error('forbidden');
            forbidden.code = 403;
            throw forbidden;
        }
        // Chưa đăng nhập: server chuyển sang trang đăng nhập (HTML).
        var type = res.headers.get('content-type') || '';
        if (!res.ok || type.indexOf('json') === -1) {
            var bad = new Error('bad-response');
            bad.code = res.status;
            throw bad;
        }
        return res.json();
    }

    // ======================= BẢN ĐỒ MỘT ĐƠN =======================
    function initOrderMap(root) {
        var code = root.dataset.orderCode;
        var locationUrl = root.dataset.locationUrl;
        var pointsUrl = root.dataset.pointsUrl;
        var canvas = root.querySelector('[data-sm-canvas]');
        var statusEl = root.querySelector('[data-sm-status]');
        var ageEl = root.querySelector('[data-sm-age]');
        var recenterBtn = root.querySelector('[data-sm-recenter]');
        var destBtn = root.querySelector('[data-sm-dest]');
        var routeUrl = root.dataset.routeUrl;
        var routeEl = root.querySelector('[data-sm-route]');
        var routeOuter = null;
        var routeInner = null;
        var routeFitted = false;
        var routeBusy = false;
        var lastRouteAt = 0;
        var destMarker = null;
        var following = true; // tự theo shipper cho đến khi người xem tự điều khiển bản đồ
        if (!code || !canvas || !statusEl) return;

        var map = createMap(canvas);
        var shipperMarker = null;
        var fitted = false;
        var lastSecondsAgo = null;
        var lastReceivedAt = 0;
        var timer = null;
        var stopped = false;
        var markersForBounds = [];

        function setStatus(text, cls) {
            statusEl.textContent = text;
            statusEl.className = 'small ' + (cls || 'text-muted');
        }

        function renderAge() {
            if (!ageEl || lastSecondsAgo === null) return;
            var elapsed = lastSecondsAgo + Math.round((Date.now() - lastReceivedAt) / 1000);
            ageEl.textContent = 'Cập nhật ' + agoText(elapsed);
            ageEl.className = 'small ' + (elapsed > STALE_AFTER_S ? 'text-warning fw-semibold' : 'text-success');
        }
        setInterval(renderAge, 1000);

        function drawRoute(points) {
            if (!routeOuter) {
                // Viền trắng bên dưới để đường nổi trên nền bản đồ.
                routeOuter = L.polyline(points, { color: '#ffffff', weight: 9, opacity: 0.9, lineJoin: 'round' }).addTo(map);
                routeInner = L.polyline(points, { color: '#1a73e8', weight: 5, opacity: 0.95, lineJoin: 'round' }).addTo(map);
            } else {
                routeOuter.setLatLngs(points);
                routeInner.setLatLngs(points);
            }
        }

        function clearRoute() {
            if (routeOuter) { map.removeLayer(routeOuter); routeOuter = null; }
            if (routeInner) { map.removeLayer(routeInner); routeInner = null; }
            if (routeEl) routeEl.textContent = '';
        }

        function distanceText(km) {
            if (km < 1) return Math.round(km * 1000) + ' m';
            return km.toFixed(1).replace('.', ',') + ' km';
        }

        // Tính lại đường đi tối đa 15 giây một lần.
        async function updateRoute() {
            if (!routeUrl || !destMarker || !shipperMarker || routeBusy) return;
            if (Date.now() - lastRouteAt < 15000) return;

            routeBusy = true;
            lastRouteAt = Date.now();
            try {
                var data = await getJson(routeUrl + '?orderCode=' + encodeURIComponent(code));
                if (data.success && data.points && data.points.length > 1) {
                    drawRoute(data.points);
                    if (routeEl) {
                        routeEl.textContent = 'Còn ' + distanceText(data.distanceKm)
                            + ' · khoảng ' + data.durationMin + ' phút';
                    }
                    if (!routeFitted && following) {
                        map.fitBounds(routeInner.getBounds(), { padding: [40, 40] });
                        routeFitted = true;
                    }
                } else {
                    clearRoute();
                }
            } catch (e) {
                /* giữ đường cũ, lần sau thử lại */
            } finally {
                routeBusy = false;
            }
        }

        function fitAll() {
            var pts = markersForBounds
                .map(function (m) { return m.getLatLng(); });
            if (!pts.length) return;
            if (pts.length === 1) { map.setView(pts[0], 16); return; }
            map.fitBounds(L.latLngBounds(pts), { padding: [40, 40], maxZoom: 16 });
        }

        // Điểm lấy hàng / giao hàng: tải một lần.
        getJson(pointsUrl + '?orderCode=' + encodeURIComponent(code))
            .then(function (data) {
                if (data.pickup) {
                    var a = L.marker([data.pickup.latitude, data.pickup.longitude],
                        { icon: pinIcon('sm-pin-pickup', 'bi-box-seam-fill') }).addTo(map);
                    a.bindPopup(popupContent('Điểm lấy hàng', data.pickup.address));
                    markersForBounds.push(a);
                }
                if (data.destination) {
                    var b = L.marker([data.destination.latitude, data.destination.longitude],
                        { icon: pinIcon('sm-pin-dest', 'bi-geo-alt-fill') }).addTo(map);
                    b.bindPopup(popupContent('Điểm giao hàng', data.destination.address));
                    markersForBounds.push(b);
                    destMarker = b;
                    if (destBtn) destBtn.disabled = false;
                    updateRoute();
                }
                if (!fitted) fitAll();
            })
            .catch(function () { /* không có điểm thì vẫn hiện shipper */ });

        // Người xem tự kéo hoặc zoom bản đồ thì thôi tự kéo về shipper.
        map.on('dragstart', function () { following = false; });
        canvas.addEventListener('wheel', function () { following = false; }, { passive: true });
        canvas.addEventListener('dblclick', function () { following = false; });

        if (recenterBtn) {
            recenterBtn.addEventListener('click', function () {
                following = true;
                if (shipperMarker) map.panTo(shipperMarker.getLatLng());
            });
        }

        if (destBtn) {
            destBtn.addEventListener('click', function () {
                if (!destMarker) return;
                following = false;
                map.setView(destMarker.getLatLng(), 16);
                destMarker.openPopup();
            });
        }

        function schedule(ms) {
            clearTimeout(timer);
            if (!stopped) timer = setTimeout(poll, ms);
        }

        async function poll() {
            if (document.hidden) { schedule(POLL_IDLE_MS); return; }

            try {
                var data = await getJson(locationUrl + '?orderCode=' + encodeURIComponent(code));

                if (!data.active) {
                    clearRoute();
                    if (ageEl) ageEl.textContent = '';
                    setStatus('Đơn hàng không ở trạng thái đang giao nên chưa có vị trí trực tiếp.');
                    schedule(POLL_IDLE_MS);
                    return;
                }

                if (!data.hasLocation) {
                    if (ageEl) ageEl.textContent = '';
                    setStatus('Shipper đang giao đơn, đang chờ tín hiệu vị trí đầu tiên...', 'text-warning');
                    schedule(POLL_IDLE_MS);
                    return;
                }

                var pos = [data.latitude, data.longitude];
                lastSecondsAgo = data.secondsAgo;
                lastReceivedAt = Date.now();

                if (!shipperMarker) {
                    shipperMarker = L.marker(pos, { icon: shipperIcon(), zIndexOffset: 1000 }).addTo(map);
                    markersForBounds.push(shipperMarker);
                    fitAll();
                    fitted = true;
                } else {
                    animateMarker(shipperMarker, pos);
                    if (following && !map.getBounds().contains(pos)) map.panTo(pos);
                }
                shipperMarker.bindPopup(popupContent(data.shipperName || 'Shipper',
                    data.accuracy ? 'Sai số GPS khoảng ' + Math.round(data.accuracy) + ' m' : ''));

                setStatus(data.secondsAgo > STALE_AFTER_S
                    ? 'Mất tín hiệu GPS của shipper, đang hiển thị vị trí gần nhất.'
                    : (data.shipperName || 'Shipper') + ' đang trên đường giao hàng.',
                    data.secondsAgo > STALE_AFTER_S ? 'text-warning' : 'text-success');
                renderAge();
                updateRoute();
                schedule(POLL_ACTIVE_MS);
            } catch (err) {
                if (err.code === 403) {
                    stopped = true;
                    setStatus('Bạn không có quyền xem vị trí của đơn này.', 'text-danger');
                } else if (err.code === 401 || err.message === 'bad-response') {
                    stopped = true;
                    setStatus('Hãy đăng nhập để xem vị trí shipper.', 'text-danger');
                } else {
                    setStatus('Mất kết nối, đang thử lại...', 'text-danger');
                    schedule(POLL_IDLE_MS);
                }
            }
        }

        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && !stopped) { clearTimeout(timer); poll(); }
        });

        poll();
    }

    // ======================= TỔNG QUAN NHIỀU SHIPPER =======================
    function initOverview(root) {
        var activeUrl = root.dataset.activeUrl;
        var mapUrl = root.dataset.mapUrl;
        var canvas = root.querySelector('[data-sm-canvas]');
        var listEl = root.querySelector('[data-sm-list]');
        var statusEl = root.querySelector('[data-sm-status]');
        if (!activeUrl || !canvas || !listEl || !statusEl) return;

        var map = createMap(canvas);
        var markers = {};
        var fitted = false;
        var timer = null;

        function renderList(items) {
            listEl.textContent = '';

            if (!items.length) {
                var empty = document.createElement('div');
                empty.className = 'text-muted small p-3';
                empty.textContent = 'Hiện không có đơn nào đang giao.';
                listEl.appendChild(empty);
                return;
            }

            items.forEach(function (it) {
                var row = document.createElement('a');
                row.className = 'list-group-item list-group-item-action';
                row.href = mapUrl + '?orderCode=' + encodeURIComponent(it.orderCode);

                var top = document.createElement('div');
                top.className = 'd-flex justify-content-between align-items-center';
                var code = document.createElement('strong');
                code.textContent = it.orderCode;
                var badge = document.createElement('span');
                badge.className = 'badge ' + (it.hasLocation
                    ? (it.secondsAgo > STALE_AFTER_S ? 'bg-warning text-dark' : 'bg-success')
                    : 'bg-secondary');
                badge.textContent = it.hasLocation ? agoText(it.secondsAgo) : 'Chưa có GPS';
                top.appendChild(code);
                top.appendChild(badge);

                var who = document.createElement('div');
                who.className = 'small text-muted';
                who.textContent = it.shipperName + ' · ' + (it.receiverAddress || '');

                row.appendChild(top);
                row.appendChild(who);
                listEl.appendChild(row);
            });
        }

        async function poll() {
            if (document.hidden) { timer = setTimeout(poll, POLL_IDLE_MS); return; }

            try {
                var data = await getJson(activeUrl);
                var items = data.items || [];
                var seen = {};

                items.forEach(function (it) {
                    if (!it.hasLocation) return;
                    seen[it.orderCode] = true;
                    var pos = [it.latitude, it.longitude];
                    var label = it.orderCode + ' · ' + it.shipperName;

                    if (!markers[it.orderCode]) {
                        var m = L.marker(pos, { icon: shipperIcon() }).addTo(map);
                        m.bindPopup(popupContent(label, it.receiverAddress));
                        markers[it.orderCode] = m;
                    } else {
                        animateMarker(markers[it.orderCode], pos);
                    }
                });

                Object.keys(markers).forEach(function (k) {
                    if (!seen[k]) { map.removeLayer(markers[k]); delete markers[k]; }
                });

                if (!fitted) {
                    var pts = Object.keys(markers).map(function (k) { return markers[k].getLatLng(); });
                    if (pts.length) {
                        map.fitBounds(L.latLngBounds(pts), { padding: [40, 40], maxZoom: 15 });
                        fitted = true;
                    }
                }

                renderList(items);
                statusEl.textContent = items.length + ' đơn đang giao, '
                    + Object.keys(markers).length + ' shipper có vị trí.';
                statusEl.className = 'small text-success';
                timer = setTimeout(poll, POLL_ACTIVE_MS);
            } catch (err) {
                statusEl.textContent = err.code === 403
                    ? 'Bạn không có quyền xem trang này.'
                    : 'Mất kết nối, đang thử lại...';
                statusEl.className = 'small text-danger';
                if (err.code !== 403) timer = setTimeout(poll, POLL_IDLE_MS);
            }
        }

        poll();
    }

    function boot() {
        var singles = document.querySelectorAll('[data-shipper-map]');
        var overviews = document.querySelectorAll('[data-shipper-overview]');
        if (!singles.length && !overviews.length) return;

        ensureLeaflet(function () {
            singles.forEach(initOrderMap);
            overviews.forEach(initOverview);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
