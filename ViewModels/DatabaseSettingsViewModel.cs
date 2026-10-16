using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class DatabaseSettingsViewModel : ObservableObject
{
    private readonly DatabaseConfigService _configService;

    [ObservableProperty]
    private string server = "";

    [ObservableProperty]
    private string port = "3306";

    [ObservableProperty]
    private string database = "";

    [ObservableProperty]
    private string user = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string sslMode = "None";

    [ObservableProperty]
    private string apiUrl = "http://localhost:5000";

    [ObservableProperty]
    private bool useApi;

    [ObservableProperty]
    private string thongBao = "";

    [ObservableProperty]
    private bool dangXuLy;

    public DatabaseSettingsViewModel()
    {
        _configService = new DatabaseConfigService();

        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configService.Load();

        Server = config.Server;
        Port = config.Port.ToString();
        Database = config.Database;
        User = config.User;
        Password = config.Password;
        SslMode = config.SslMode;
        ApiUrl = config.ApiUrl;
        UseApi = config.UseApi;
    }

    private DatabaseConfig GetConfig()
    {
        if (!int.TryParse(Port, out var port))
        {
            port = 3306;
        }

        return new DatabaseConfig
        {
            Server = Server.Trim(),
            Port = port,
            Database = Database.Trim(),
            User = User.Trim(),
            Password = Password,
            SslMode = string.IsNullOrWhiteSpace(SslMode)
                ? "None"
                : SslMode,
            ApiUrl = string.IsNullOrWhiteSpace(ApiUrl) ? "http://localhost:5000" : ApiUrl.Trim(),
            UseApi = UseApi
        };
    }

    [RelayCommand]
    private async Task KiemTraKetNoiAsync()
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            ThongBao = "Vui lòng nhập máy chủ.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            ThongBao = "Vui lòng nhập tên database.";
            return;
        }

        DangXuLy = true;
        ThongBao = "Đang kiểm tra kết nối...";

        try
        {
            var config = GetConfig();

            _configService.Save(config);

            var databaseService = new DatabaseService();

            var success =
                await databaseService.TestConnectionAsync();

            if (success)
            {
                ThongBao =
                    "✓ Kết nối database thành công!";
            }
        }
        catch (Exception ex)
        {
            ThongBao =
                "✕ Kết nối thất bại: " + ex.Message;
        }
        finally
        {
            DangXuLy = false;
        }
    }

    [RelayCommand]
    private void Luu(Window? window)
    {
        try
        {
            var config = GetConfig();

            _configService.Save(config);

            ThongBao =
                "✓ Đã lưu cấu hình kết nối.";

            // Đóng cửa sổ sau một khoảng ngắn không cần thiết,
            // nên để người dùng tự bấm Đóng.
        }
        catch (Exception ex)
        {
            ThongBao =
                "Không thể lưu cấu hình: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Dong(Window? window)
    {
        window?.Close();
    }
}