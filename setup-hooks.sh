#!/bin/bash
echo "========================================================"
echo "⚙️ Đang cấu hình Git Hooks (Commit Message Validation)..."
echo "========================================================"
chmod +x .githooks/commit-msg
git config core.hooksPath .githooks
echo "✅ Git Hooks đã được kích hoạt thành công!"
