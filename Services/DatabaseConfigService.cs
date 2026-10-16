using System.Text.Json;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class DatabaseConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

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

            return config ?? LoadFromAppSettings();
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
                    return config;
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

        var json = JsonSerializer.Serialize(config, JsonOptions);

        File.WriteAllText(_filePath, json);
    }

    public string GetConfigPath()
    {
        return _filePath;
    }
}