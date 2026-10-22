#!/bin/bash
# Script khởi chạy Backend API & Web Báo cáo
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
cd "$DIR"

echo "================================================="
echo "   ĐANG KHỞI CHẠY SERVER API & WEB BÁO CÁO...    "
echo "   Địa chỉ Web: http://localhost:5000           "
echo "================================================="
dotnet run --project ShopHoa.Api/ShopHoa.Api.csproj
