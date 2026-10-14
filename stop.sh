#!/bin/bash
# Script dừng dự án DeliveryManagement

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_DIR"

echo "🛑 Đang dừng toàn bộ containers của DeliveryManagement..."
docker compose down
echo "✅ Đã dừng thành công!"
