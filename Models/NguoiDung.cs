namespace ShopHoa.Models;

public sealed class NguoiDung
{
    public uint Id { get; set; }
    public string MaNguoiDung { get; set; } = "";
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public uint? ChiNhanhId { get; set; }
    public string? TenChiNhanh { get; set; }

    /// <summary>Tên vai trò, dùng để hiển thị (có thể nhiều vai trò, ngăn cách bởi ", ").</summary>
    public string? VaiTro { get; set; }

    /// <summary>
    /// Mã vai trò lấy từ bảng <c>vai_tro.ma_vai_tro</c> (dùng để kiểm tra quyền).
    /// Không gắn cứng tên vai trò trong code, đổi dữ liệu vai trò trong database
    /// thì quyền tự đổi theo.
    /// </summary>
    public string? MaVaiTro { get; set; }

    /// <summary>Tách chuỗi mã vai trò thành danh sách để kiểm tra quyền.</summary>
    public IEnumerable<string> DanhSachMaVaiTro()
    {
        if (string.IsNullOrWhiteSpace(MaVaiTro))
            return [];

        return MaVaiTro
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ma => ma.Trim())
            .Where(ma => ma.Length > 0);
    }
}
