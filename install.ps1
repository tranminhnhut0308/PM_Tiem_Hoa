# ==============================================================================
# Script cài đặt tự động 1 lệnh duy nhất cho ShopHoa trên Windows
# Sử dụng: irm https://raw.githubusercontent.com/tranminhnhut0308/PM_Shop_Hoa/main/install.ps1 | iex
# ==============================================================================

$ErrorActionPreference = "Stop"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "   BAT DAU CAI DAT PHAN MEM QUAN LY SHOP HOA WINDOWS  " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan

$repo = "tranminhnhut0308/PM_Shop_Hoa"
$releaseUrl = "https://github.com/$repo/releases/latest/download/ShopHoa-win-Setup.exe"
$rawFallbackUrl = "https://raw.githubusercontent.com/$repo/main/Releases/win-x64/ShopHoa-win-Setup.exe"

$tempExe = Join-Path $env:TEMP "ShopHoa-win-Setup.exe"
if (Test-Path $tempExe) {
    Remove-Item $tempExe -Force -ErrorAction SilentlyContinue
}

Write-Host "`n[1/2] Dang tai bo cai dat ShopHoa tu GitHub..." -ForegroundColor Yellow

$downloaded = $false
try {
    # Thu tai tu GitHub Releases (luon la phien ban moi nhat)
    Invoke-WebRequest -Uri $releaseUrl -OutFile $tempExe -UseBasicParsing
    $downloaded = $true
} catch {
    try {
        # Fallback tai tu branch main neu chua tao Release
        Invoke-WebRequest -Uri $rawFallbackUrl -OutFile $tempExe -UseBasicParsing
        $downloaded = $true
    } catch {
        Write-Host "Khong the tai file cai dat tu GitHub. Vui long kiem tra lai link repository!" -ForegroundColor Red
        exit 1
    }
}

if ($downloaded -and (Test-Path $tempExe)) {
    Write-Host "`n[2/2] Dang khoi chay trinh cai dat ShopHoa..." -ForegroundColor Green
    Start-Process -FilePath $tempExe
    Write-Host "`n======================================================" -ForegroundColor Green
    Write-Host "   DA KHOI CHAY TRINH CAI DAT THANH CONG!            " -ForegroundColor Green
    Write-Host "   Phan mem se tu dong tao bieu tuong ngoai Desktop   " -ForegroundColor Cyan
    Write-Host "   va mo ung dung ngay sau khi cai dat hoan tat.      " -ForegroundColor Cyan
    Write-Host "======================================================" -ForegroundColor Green
}
