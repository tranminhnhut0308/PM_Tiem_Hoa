using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MySqlConnector;
using ShopHoa.Helpers;
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

    /// <summary>
    /// Các SSL Mode hợp lệ để hiển thị trong combobox. Bind theo chuỗi (không
    /// dùng ComboBoxItem) nên giá trị đưa vào cấu hình luôn là tên enum hợp lệ.
    /// </summary>
    public IReadOnlyList<string> SslModeOptions { get; } = DatabaseConfigService.SslModeHopLe;

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
        SslMode = DatabaseConfigService.ChuanHoaSslMode(config.SslMode);
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
            SslMode = DatabaseConfigService.ChuanHoaSslMode(SslMode),
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

            // Kiểm tra bằng đúng thông số đang hiển thị trên form (chưa lưu).
            // Chỉ lưu cấu hình khi kết nối thành công để giá trị sai không ghi đè
            // lên cấu hình đang chạy được.
            var databaseService = new DatabaseService();

            await databaseService.TestConnectionAsync(config);

            _configService.Save(config);

            ThongBao =
                "✓ Kết nối database thành công! Đã lưu cấu hình.";
        }
        catch (MySqlException ex)
        {
            ThongBao =
                "✕ " + MoTaLoiKetNoi(ex);
        }
        catch (Exception ex)
        {
            ThongBao =
                "✕ Kết nối thất bại: " + SafeLog.Redact(ex.Message);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    /// <summary>
    /// Dịch lỗi MySqlException thành thông báo người dùng hiểu được, phân biệt
    /// sai tài khoản/mật khẩu, thiếu quyền, sai database, lỗi mạng và lỗi SSL.
    /// Không đưa mật khẩu vào thông báo.
    /// </summary>
    private string MoTaLoiKetNoi(MySqlException ex)
    {
        var message = ex.Message ?? "";

        if (message.Contains("SSL", StringComparison.OrdinalIgnoreCase))
        {
            return "Lỗi SSL: máy chủ database không hỗ trợ SSL theo cấu hình đang chọn. "
                 + "Hãy thử chọn SSL Mode = None hoặc Preferred.";
        }

        return ex.ErrorCode switch
        {
            MySqlErrorCode.AccessDenied =>
                "Sai tài khoản hoặc mật khẩu database.",
            MySqlErrorCode.DatabaseAccessDenied =>
                $"Tài khoản '{User.Trim()}' không có quyền truy cập database '{Database.Trim()}'.",
            MySqlErrorCode.UnknownDatabase =>
                $"Không tìm thấy database '{Database.Trim()}' trên máy chủ.",
            MySqlErrorCode.UnableToConnectToHost =>
                $"Không kết nối được tới máy chủ '{Server.Trim()}:{Port}'. "
                + "Kiểm tra địa chỉ, cổng và mạng.",
            _ =>
                "Kết nối thất bại: " + SafeLog.Redact(message)
        };
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