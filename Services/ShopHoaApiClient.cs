using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Client kết nối tới ASP.NET Core Web API của ShopHoa.
/// Hỗ trợ gửi JWT Bearer Token tự động và xử lý serialization chuẩn .NET 8.
/// </summary>
public sealed class ShopHoaApiClient
{
    private static readonly Lazy<ShopHoaApiClient> _instance = new(() => new ShopHoaApiClient());
    public static ShopHoaApiClient Instance => _instance.Value;

    private readonly HttpClient _http;
    private string? _token;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string BaseUrl { get; private set; } = "http://localhost:5000";
    public bool IsConnected => !string.IsNullOrEmpty(_token);

    public ShopHoaApiClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        try
        {
            var config = new DatabaseConfigService().Load();
            if (!string.IsNullOrWhiteSpace(config.ApiUrl))
            {
                BaseUrl = config.ApiUrl;
            }
        }
        catch { }

        CapNhatBaseUrl(BaseUrl);
    }

    public void CapNhatBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var clean = url.Trim().TrimEnd('/');
        BaseUrl = clean;
        _http.BaseAddress = new Uri(clean + "/");
    }

    public void SetToken(string? token)
    {
        _token = token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public void ClearToken()
    {
        SetToken(null);
    }

    // =========================================================
    // XÁC THỰC (AUTH)
    // =========================================================

    public async Task<ApiLoginResult> LoginAsync(string tenDangNhap, string matKhau)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/login", new
            {
                TenDangNhap = tenDangNhap,
                MatKhau = matKhau
            });

            if (!res.IsSuccessStatusCode)
            {
                var errContent = await res.Content.ReadAsStringAsync();
                var msg = "Đăng nhập thất bại.";
                try
                {
                    using var doc = JsonDocument.Parse(errContent);
                    if (doc.RootElement.TryGetProperty("message", out var m))
                        msg = m.GetString() ?? msg;
                }
                catch { }

                return new ApiLoginResult { Success = false, ErrorMessage = msg };
            }

            var loginData = await res.Content.ReadFromJsonAsync<ApiLoginResponse>(_jsonOptions);
            if (loginData is null || string.IsNullOrEmpty(loginData.Token))
            {
                return new ApiLoginResult { Success = false, ErrorMessage = "Dữ liệu trả về không hợp lệ." };
            }

            SetToken(loginData.Token);

            return new ApiLoginResult
            {
                Success = true,
                Token = loginData.Token,
                User = loginData.User,
                Permissions = loginData.Permissions ?? new()
            };
        }
        catch (Exception ex)
        {
            return new ApiLoginResult
            {
                Success = false,
                ErrorMessage = "Không thể kết nối đến máy chủ API: " + ex.Message
            };
        }
    }

    public async Task<bool> ChangePasswordAsync(string? oldPassword, string newPassword)
    {
        var res = await _http.PostAsJsonAsync("api/auth/change-password", new
        {
            OldPassword = oldPassword,
            NewPassword = newPassword
        });

        return res.IsSuccessStatusCode;
    }

    // =========================================================
    // QUẢN LÝ TÀI KHOẢN (USERS)
    // =========================================================

    public async Task<List<NguoiDung>> GetUsersAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "api/users" : $"api/users?search={Uri.EscapeDataString(search)}";
        var res = await _http.GetFromJsonAsync<List<NguoiDung>>(url, _jsonOptions);
        return res ?? new List<NguoiDung>();
    }

    public async Task<NguoiDung?> GetUserByIdAsync(uint id)
    {
        return await _http.GetFromJsonAsync<NguoiDung>($"api/users/{id}", _jsonOptions);
    }

    // =========================================================
    // SẢN PHẨM & HÀNG HÓA (SAN PHAM)
    // =========================================================

    public async Task<ApiPagedResult<SanPham>?> GetSanPhamsAsync(
        string? search = null,
        uint? nhomId = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = $"api/sanphams?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        if (nhomId.HasValue && nhomId.Value > 0)
            query += $"&nhomId={nhomId.Value}";

        return await _http.GetFromJsonAsync<ApiPagedResult<SanPham>>(query, _jsonOptions);
    }

    public async Task<SanPham?> GetSanPhamByIdAsync(uint id)
    {
        return await _http.GetFromJsonAsync<SanPham>($"api/sanphams/{id}", _jsonOptions);
    }

    public async Task<bool> UpdateGiaBanAsync(uint id, decimal giaBan)
    {
        var res = await _http.PutAsJsonAsync($"api/sanphams/{id}/price", new { GiaBan = giaBan });
        return res.IsSuccessStatusCode;
    }
}

public sealed class ApiLoginResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public NguoiDung? User { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public sealed class ApiLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public NguoiDung? User { get; set; }
    public List<string>? Permissions { get; set; }
}

public sealed class ApiPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
