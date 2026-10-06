#!/bin/bash
# Script khởi chạy dự án DeliveryManagement bằng Docker Compose

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_DIR"

echo "========================================================"
echo "🚀 Đang khởi động DeliveryManagement bằng Docker Compose..."
echo "========================================================"

docker compose up -d

echo ""
echo "✅ Hệ thống đã sẵn sàng!"
echo "🌐 Truy cập website tại: http://localhost:5055"
echo "========================================================"
