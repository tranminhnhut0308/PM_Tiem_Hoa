using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ quản lý kiểm tra và cập nhật phiên bản ứng dụng từ xa bằng Velopack.
/// </summary>
public sealed class UpdateService
{
    private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
    public static UpdateService Instance => _instance.Value;

    private UpdateManager? _updateManager;
    private UpdateInfo? _lastUpdateInfo;

    public string CurrentVersionText { get; private set; } = "1.0.0";
    public string UpdateUrl { get; set; } = "https://github.com/tranminhnhut0308/PM_Shop_Hoa";
    public bool IsInstalled => _updateManager?.IsInstalled ?? false;

    public UpdateService()
    {
        try
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version != null)
            {
                CurrentVersionText = $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }
        catch { }

        KhoiTaoUpdateManager();
    }

    public void KhoiTaoUpdateManager()
    {
        try
        {
            IUpdateSource source;
            if (UpdateUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                source = new GithubSource(UpdateUrl, accessToken: null, prerelease: false);
            }
            else
            {
                source = new SimpleWebSource(UpdateUrl);
            }

            _updateManager = new UpdateManager(source);
            if (_updateManager.IsInstalled && _updateManager.CurrentVersion != null)
            {
                CurrentVersionText = _updateManager.CurrentVersion.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Không thể khởi tạo UpdateManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Kiểm tra xem có phiên bản mới từ xa hay không.
    /// Trả về UpdateCheckResult chứa thông tin phiên bản mới nếu có.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        if (_updateManager is null || !_updateManager.IsInstalled)
        {
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = CurrentVersionText,
                Message = "Ứng dụng đang chạy ở chế độ phát triển (chưa được đóng gói cài đặt qua Velopack)."
            };
        }

        try
        {
            _lastUpdateInfo = await _updateManager.CheckForUpdatesAsync();

            // Nếu GithubSource không tìm thấy bản mới trên GitHub Releases,
            // tự động fallback sang thư mục Releases/{channel} trên nhánh main của Git
            if (_lastUpdateInfo is null && UpdateUrl.Contains("github.com/tranminhnhut0308/PM_Shop_Hoa"))
            {
                try
                {
                    var channel = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
                    var rawUrl = $"https://raw.githubusercontent.com/tranminhnhut0308/PM_Shop_Hoa/main/Releases/{channel}";
                    var rawSource = new SimpleWebSource(rawUrl);
                    var rawManager = new UpdateManager(rawSource);
                    var rawInfo = await rawManager.CheckForUpdatesAsync();
                    if (rawInfo != null)
                    {
                        _updateManager = rawManager;
                        _lastUpdateInfo = rawInfo;
                    }
                }
                catch { }
            }

            if (_lastUpdateInfo is null)
            {
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = CurrentVersionText,
                    Message = "Bạn đang sử dụng phiên bản mới nhất."
                };
            }

            var newVer = _lastUpdateInfo.TargetFullRelease.Version.ToString();
            return new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = CurrentVersionText,
                NewVersion = newVer,
                Message = $"Đã có phiên bản mới {newVer} sẵn sàng để cập nhật!"
            };
        }
        catch (Exception ex)
        {
            // Nếu kiểm tra GitHub Releases gặp lỗi, thử kiểm tra qua raw Git
            if (UpdateUrl.Contains("github.com/tranminhnhut0308/PM_Shop_Hoa"))
            {
                try
                {
                    var channel = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
                    var rawUrl = $"https://raw.githubusercontent.com/tranminhnhut0308/PM_Shop_Hoa/main/Releases/{channel}";
                    var rawSource = new SimpleWebSource(rawUrl);
                    var rawManager = new UpdateManager(rawSource);
                    var rawInfo = await rawManager.CheckForUpdatesAsync();
                    if (rawInfo != null)
                    {
                        _updateManager = rawManager;
                        _lastUpdateInfo = rawInfo;
                        var newVer = rawInfo.TargetFullRelease.Version.ToString();
                        return new UpdateCheckResult
                        {
                            IsUpdateAvailable = true,
                            CurrentVersion = CurrentVersionText,
                            NewVersion = newVer,
                            Message = $"Đã có phiên bản mới {newVer} sẵn sàng để cập nhật!"
                        };
                    }
                }
                catch { }
            }

            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = CurrentVersionText,
                ErrorMessage = "Không thể kiểm tra cập nhật (vui lòng kiểm tra lại kết nối Internet): " + ex.Message
            };
        }
    }

    /// <summary>
    /// Tải gói cập nhật với tiến trình báo về từ 0% đến 100%.
    /// </summary>
    public async Task<bool> DownloadUpdatesAsync(Action<int>? progressCallback = null)
    {
        if (_updateManager is null || _lastUpdateInfo is null)
            return false;

        try
        {
            await _updateManager.DownloadUpdatesAsync(_lastUpdateInfo, progress =>
            {
                progressCallback?.Invoke(progress);
            });

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi khi tải bản cập nhật: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Khởi động lại ứng dụng và áp dụng bản cập nhật mới.
    /// </summary>
    public void RestartAndApplyUpdates()
    {
        if (_updateManager is null || _lastUpdateInfo is null)
            return;

        _updateManager.ApplyUpdatesAndRestart(_lastUpdateInfo);
    }
}

public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = "1.0.0";
    public string? NewVersion { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}
