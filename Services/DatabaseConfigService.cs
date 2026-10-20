using System.Text.Json;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class DatabaseConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Danh sách SSL Mode hợp lệ với MySqlConnector. Dùng chung cho combobox ở
    /// màn "Cài đặt kết nối Database" và cho việc chuẩn hóa cấu hình đã lưu.
    /// </summary>
    public static readonly IReadOnlyList<string> SslModeHopLe =
        ["None", "Preferred", "Required", "VerifyCA", "VerifyFull"];

    private readonly string _filePath;

    public DatabaseConfigService()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShopHoa",
            "database_config.json");
    }

    public DatabaseConfig Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return LoadFromAppSettings();
            }

            var json = File.ReadAllText(_filePath);

            var config = JsonSerializer.Deserialize<DatabaseConfig>(
                json,
                JsonOptions);

            return config is null
                ? LoadFromAppSettings()
                : ChuanHoa(config);
        }
        catch
        {
            return LoadFromAppSettings();
        }
    }

    private DatabaseConfig LoadFromAppSettings()
    {
        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(appSettingsPath))
            {
                appSettingsPath = "appsettings.json";
            }

            if (File.Exists(appSettingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
                if (doc.RootElement.TryGetProperty("Database", out var dbProp))
                {
                    var config = new DatabaseConfig();
                    if (dbProp.TryGetProperty("Server", out var s) && s.GetString() is { } server) config.Server = server;
                    if (dbProp.TryGetProperty("Port", out var p) && p.TryGetInt32(out var port)) config.Port = port;
                    if (dbProp.TryGetProperty("Database", out var d) && d.GetString() is { } db) config.Database = db;
                    if (dbProp.TryGetProperty("User", out var u) && u.GetString() is { } user) config.User = user;
                    if (dbProp.TryGetProperty("Password", out var pwd) && pwd.GetString() is { } pass) config.Password = pass;
                    if (dbProp.TryGetProperty("SslMode", out var ssl) && ssl.GetString() is { } sm) config.SslMode = sm;
                    return ChuanHoa(config);
                }
            }
        }
        catch { }

        return new DatabaseConfig();
    }

    public void Save(DatabaseConfig config)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(ChuanHoa(config), JsonOptions);

        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Chuẩn hóa SSL Mode về đúng tên enum của MySqlConnector. Cấu hình cũ có thể
    /// chứa giá trị rỗng, "Disabled" hoặc giá trị rác (ví dụ
    /// "Avalonia.Controls.ComboBoxItem" sinh ra do lỗi ComboBox trước đây) nên mọi
    /// giá trị không hợp lệ đều quy về "None" thay vì để MySqlConnector ném
    /// ArgumentException khi mở kết nối.
    /// </summary>
    public static string ChuanHoaSslMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "None";
        }

        var giaTri = value.Trim();

        if (giaTri.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "None";
        }

        foreach (var hopLe in SslModeHopLe)
        {
            if (hopLe.Equals(giaTri, StringComparison.OrdinalIgnoreCase))
            {
                return hopLe;
            }
        }

        return "None";
    }

    /// <summary>
    /// Chuẩn hóa cấu hình trước khi dùng hoặc lưu: bỏ khoảng trắng thừa, chặn
    /// cổng và SSL Mode không hợp lệ để việc kết nối không bao giờ lỗi vì dữ liệu
    /// cấu hình cũ.
    /// </summary>
    public static DatabaseConfig ChuanHoa(DatabaseConfig config)
    {
        config.Server = config.Server?.Trim() ?? "";
        config.Database = config.Database?.Trim() ?? "";
        config.User = config.User?.Trim() ?? "";
        config.Password ??= "";
        config.SslMode = ChuanHoaSslMode(config.SslMode);
        config.ApiUrl = string.IsNullOrWhiteSpace(config.ApiUrl)
            ? "http://localhost:5000"
            : config.ApiUrl.Trim();

        if (config.Port <= 0 || config.Port > 65535)
        {
            config.Port = 3306;
        }

        return config;
    }

    public string GetConfigPath()
    {
        return _filePath;
    }
}