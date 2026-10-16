param(
    [ValidateSet("all", "win-x64", "linux-x64")]
    [string]$Target = "all",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "       BAT DAU DONG GOI UNG DUNG SHOP HOA             " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "Muc tieu: $Target" -ForegroundColor Yellow
Write-Host "Phien ban: $Version" -ForegroundColor Yellow

# 0. Chuan bi logo icon tu Assets/LOGO_APP.png
$logoSource = if (Test-Path "Assets/LOGO_APP.png") { "Assets/LOGO_APP.png" } else { "Assets/logo.png" }
if (Test-Path $logoSource) {
    Write-Host "`n[Chuan bi] Tao file Assets/logo.ico tu $logoSource..." -ForegroundColor Yellow
    Add-Type -AssemblyName System.Drawing
    $srcImg = [System.Drawing.Image]::FromFile((Resolve-Path $logoSource))
    $bmp = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($srcImg, 0, 0, 256, 256)
    $g.Dispose()
    $srcImg.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngBytes = $ms.ToArray()
    $ms.Dispose()

    $icoPath = Join-Path (Get-Location) "Assets/logo.ico"
    $icoFs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter($icoFs)
    # ICONDIR
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]1)
    # ICONDIRENTRY
    $bw.Write([byte]0)   # 256 width
    $bw.Write([byte]0)   # 256 height
    $bw.Write([byte]0)   # color count
    $bw.Write([byte]0)   # reserved
    $bw.Write([uint16]1) # planes
    $bw.Write([uint16]32)# bpp
    $bw.Write([uint32]$pngBytes.Length)
    $bw.Write([uint32]22) # offset
    # PNG data
    $bw.Write($pngBytes)
    $bw.Flush()
    $bw.Close()
    $icoFs.Close()
    Write-Host "Da tao thanh cong Assets/logo.ico tu $logoSource!" -ForegroundColor Green
}

# 1. Kiem tra cong cu vpk (Velopack CLI)
Write-Host "`n[1/3] Kiem tra cong cu Velopack CLI (vpk)..." -ForegroundColor Green
$vpkInstalled = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpkInstalled) {
    Write-Host "Dang tu dong cai dat cong cu vpk (Velopack CLI)..." -ForegroundColor Yellow
    dotnet tool install -g vpk
} else {
    Write-Host "Velopack CLI da san sang." -ForegroundColor Green
}

# Tat tien trinh ShopHoa neu dang chay ngam de tranh khoa file DLL
Get-Process -Name "ShopHoa", "ShopHoa-win-Setup", "vpk" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$targetsToBuild = if ($Target -eq "all") { @("win-x64", "linux-x64") } else { @($Target) }

foreach ($t in $targetsToBuild) {
    Write-Host "`n------------------------------------------------------" -ForegroundColor Cyan
    Write-Host ">>> DANG DONG GOI CHO: $t (Phien ban: $Version) <<<" -ForegroundColor Cyan
    Write-Host "------------------------------------------------------" -ForegroundColor Cyan

    $publishDir = "publish/$t"
    if (Test-Path $publishDir) {
        Get-ChildItem -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.IsReadOnly) { $_.IsReadOnly = $false }
        }
        try {
            Remove-Item $publishDir -Recurse -Force -ErrorAction Stop
        } catch {
            Start-Sleep -Seconds 1
            Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host "`n[2/3] Dang xuat ban ung dung (dotnet publish $t)..." -ForegroundColor Green
    dotnet publish ShopHoa.csproj -c Release /p:PublishProfile="Properties/PublishProfiles/$t.pubxml" /p:Version=$Version

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Xuat ban (dotnet publish) cho $t that bai!"
        exit 1
    }

    Write-Host "`n[3/3] Dang tao bo cai dat cho $t..." -ForegroundColor Green
    $releaseDir = "Releases/$t"
    if (-not (Test-Path $releaseDir)) {
        New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    } else {
        # Bo thuoc tinh ReadOnly tren tat ca file trong Releases
        Get-ChildItem -Path $releaseDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                if ($_.IsReadOnly) { $_.IsReadOnly = $false }
                [System.IO.File]::SetAttributes($_.FullName, [System.IO.FileAttributes]::Normal)
            } catch { }
        }

        # Xoa file RELEASES cu de vpk khong bi loi UnauthorizedAccessException khi xoa
        $releasesLegacy = Join-Path $releaseDir "RELEASES"
        if (Test-Path $releasesLegacy) {
            try {
                [System.IO.File]::SetAttributes($releasesLegacy, [System.IO.FileAttributes]::Normal)
                [System.IO.File]::Delete($releasesLegacy)
            } catch {
                Start-Sleep -Milliseconds 500
                try {
                    [System.IO.File]::SetAttributes($releasesLegacy, [System.IO.FileAttributes]::Normal)
                    [System.IO.File]::Delete($releasesLegacy)
                } catch {
                    Remove-Item $releasesLegacy -Force -ErrorAction SilentlyContinue
                }
            }
        }

        Get-ChildItem -Path $releaseDir -Filter "*$Version*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }

    if ($t -eq "win-x64") {
        $iconArgs = @()
        if (Test-Path "Assets/logo.ico") {
            $iconArgs = @("-i", "Assets/logo.ico")
        }

        vpk pack -u ShopHoa -v $Version -p $publishDir -e "ShopHoa.exe" -o $releaseDir --packTitle "ShopHoa" --runtime $t @iconArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Dong goi Velopack Windows that bai!"
            exit 1
        }
        Write-Host "-> Thanh cong bo cai Windows: $releaseDir/ShopHoa-win-Setup.exe" -ForegroundColor Green
    } elseif ($t -eq "linux-x64") {
        Write-Host "Dang tao goi ShopHoa-linux.tar.gz phuc vu cai dat 1 dong..." -ForegroundColor Yellow
        tar -czf "$releaseDir/ShopHoa-linux.tar.gz" -C $publishDir .
        Copy-Item "install.sh" "$releaseDir/install.sh" -Force -ErrorAction SilentlyContinue
        Write-Host "-> Thanh cong bo cai Linux: $releaseDir/ShopHoa-linux.tar.gz va install.sh" -ForegroundColor Green

        $linuxIcon = if (Test-Path "Assets/LOGO_APP.png") { "Assets/LOGO_APP.png" } elseif (Test-Path "Assets/logo.png") { "Assets/logo.png" } else { "" }
        $linuxIconArgs = if ($linuxIcon) { @("-i", $linuxIcon) } else { @() }

        Write-Host "Dang thu tao AppImage bang Velopack qua chi thi [linux]..." -ForegroundColor Yellow
        & vpk "[linux]" pack -u ShopHoa -v $Version -p $publishDir -e "ShopHoa" -o $releaseDir --packTitle "ShopHoa" --runtime $t @linuxIconArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Chu y: Velopack AppImage khong the build tren Windows (can squashfs). Goi ShopHoa-linux.tar.gz da du de cai dat may khach!" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n======================================================" -ForegroundColor Green
Write-Host "       TAT CA DONG GOI DA HOAN TAT THANH CONG!        " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
if ($targetsToBuild -contains "win-x64") {
    Write-Host "- Windows installer: Releases/win-x64/ShopHoa-win-Setup.exe" -ForegroundColor Cyan
}
if ($targetsToBuild -contains "linux-x64") {
    Write-Host "- Linux package:     Releases/linux-x64/ShopHoa-linux.tar.gz" -ForegroundColor Cyan
}
Write-Host ""
