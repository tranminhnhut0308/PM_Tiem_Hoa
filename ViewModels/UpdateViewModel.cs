using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class UpdateViewModel : ObservableObject
{
    private readonly UpdateService _updateService = UpdateService.Instance;

    [ObservableProperty]
    private string currentVersion = "1.0.0";

    [ObservableProperty]
    private string newVersion = "---";

    [ObservableProperty]
    private string statusMessage = "Bấm 'Kiểm tra cập nhật' để tìm phiên bản mới.";

    [ObservableProperty]
    private int downloadProgress = 0;

    [ObservableProperty]
    private bool isChecking;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private bool hasNewVersion;

    [ObservableProperty]
    private bool isUpdateReady;

    [ObservableProperty]
    private string updateUrl = "";

    public System.Action? CloseAction { get; set; }

    public UpdateViewModel()
    {
        CurrentVersion = _updateService.CurrentVersionText;
        UpdateUrl = _updateService.UpdateUrl;
    }

    [RelayCommand]
    public async Task CheckUpdateAsync()
    {
        if (IsChecking || IsDownloading)
            return;

        IsChecking = true;
        StatusMessage = "Đang kiểm tra phiên bản mới từ máy chủ...";
        HasNewVersion = false;
        IsUpdateReady = false;
        DownloadProgress = 0;

        try
        {
            if (!string.IsNullOrWhiteSpace(UpdateUrl) && UpdateUrl != _updateService.UpdateUrl)
            {
                _updateService.UpdateUrl = UpdateUrl.Trim();
                _updateService.KhoiTaoUpdateManager();
            }

            var result = await _updateService.CheckForUpdatesAsync();

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                StatusMessage = "✕ " + result.ErrorMessage;
                return;
            }

            if (result.IsUpdateAvailable && !string.IsNullOrEmpty(result.NewVersion))
            {
                HasNewVersion = true;
                NewVersion = result.NewVersion;
                StatusMessage = $"✓ Phát hiện phiên bản mới: {result.NewVersion}. Bấm 'Tải và Cập nhật' để tiếp tục.";
            }
            else
            {
                NewVersion = CurrentVersion;
                StatusMessage = "✓ " + (result.Message ?? "Bạn đang sử dụng phiên bản mới nhất.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "✕ Lỗi khi kiểm tra cập nhật: " + ex.Message;
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    public async Task DownloadAndInstallAsync()
    {
        if (IsDownloading || !HasNewVersion)
            return;

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = "Đang tải bản cập nhật... 0%";

        try
        {
            var success = await _updateService.DownloadUpdatesAsync(progress =>
            {
                DownloadProgress = progress;
                StatusMessage = $"Đang tải bản cập nhật... {progress}%";
            });

            if (success)
            {
                DownloadProgress = 100;
                IsUpdateReady = true;
                HasNewVersion = false;
                StatusMessage = "✓ Tải bản cập nhật hoàn tất! Bấm 'Khởi động lại ngay' để áp dụng phiên bản mới.";
            }
            else
            {
                StatusMessage = "✕ Tải bản cập nhật thất bại. Vui lòng kiểm tra lại kết nối mạng.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "✕ Lỗi trong quá trình tải: " + ex.Message;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    public void RestartToApply()
    {
        _updateService.RestartAndApplyUpdates();
    }

    [RelayCommand]
    public void Close()
    {
        CloseAction?.Invoke();
    }
}
