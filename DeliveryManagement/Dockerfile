# Multi-stage build for ASP.NET Core 8.0
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Sao chép file csproj và restore packages
COPY ["DeliveryManagement.csproj", "./"]
RUN dotnet restore "./DeliveryManagement.csproj"

# Sao chép toàn bộ mã nguồn và build
COPY . .
RUN dotnet publish "./DeliveryManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DeliveryManagement.dll"]
