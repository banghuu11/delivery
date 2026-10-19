#!/bin/bash
echo "========================================================"
echo "⚙️ Đang cấu hình Git Hooks (Branch & Commit Validation)..."
echo "========================================================"
chmod +x .githooks/*
git config core.hooksPath .githooks
echo "✅ Git Hooks (Branch Name + Commit Message) đã được kích hoạt!"
