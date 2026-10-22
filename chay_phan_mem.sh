#!/bin/bash
# Script khởi chạy nhanh Phần mềm Tiệm Hoa (Giao diện Desktop)
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
cd "$DIR"

echo "================================================="
echo "   ĐANG KHỞI CHẠY PHẦN MỀM TIỆM HOA WINWON...   "
echo "================================================="
dotnet run --project ShopHoa.csproj
