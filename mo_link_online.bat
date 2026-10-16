@echo off
title Vua Hoa Nha Huy - Tao Link Web 4G Cho Dien Thoai
color 0B
cd /d "%~dp0"

echo ====================================================================
echo        VUA HOA NHA HUY - TAO LINK XEM BAO CAO CHO DIEN THOAI
echo ====================================================================
echo.

if not exist "cloudflared.exe" (
    echo [1/3] Dang tai cong cu Cloudflare ve may...
    echo       (Dung luong khoang 25MB, xin cho trong giay lat...)
    curl.exe -L -A "Mozilla/5.0" --progress-bar -o "cloudflared.exe" "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
)

echo [1/2] Dang khoi dong Web Server ShopHoa tai cong 5000...
start "ShopHoa Web Server" cmd /k "cd /d "%~dp0ShopHoa.Api" && dotnet run"

echo Cho 6 giay de Web Server khoi dong...
ping 127.0.0.1 -n 7 >nul

echo.
echo [2/2] Dang ket noi Cloudflare tao duong link HTTPS cho dien thoai...
echo.
echo ====================================================================
echo  HUONG DAN LAY LINK XEM DIEN THOAI:
echo  1. Xem cac dong ben duoi, tim dong co chu:
echo     https://xxxx.trycloudflare.com
echo  2. Copy link do gui qua Zalo / tin nhan mo tren DIEN THOAI (4G hoac Wifi)!
echo  3. GIU CUA SO NAY LUON MO trong suot luc xem tren dien thoai.
echo ====================================================================
echo.

cloudflared.exe tunnel --url http://localhost:5000

echo.
echo ====================================================================
echo Ket noi da ket thuc.
echo ====================================================================
pause
