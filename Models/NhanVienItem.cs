using System;
using ShopHoa.Services;

namespace ShopHoa.Models;

/// <summary>
/// Model đại diện cho một hồ sơ nhân viên trong màn "Quản lý nhân viên".
/// Dữ liệu lấy từ nguoi_dung (hồ sơ nhân sự), chi_nhanh (bộ phận / đơn vị trực thuộc)
/// và vai_tro (vai trò sử dụng phần mềm).
/// </summary>
public sealed class NhanVienItem
{
    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaNguoiDung { get; set; } = string.Empty;

    /// <summary>
    /// Hiển thị mã nhân viên chuẩn NVxxxx nếu mã người dùng trống hoặc tự sinh.
    /// </summary>
    public string MaNhanVien
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MaNguoiDung))
                return MaNguoiDung;

            return $"NV{Id:D4}";
        }
    }

    /// <summary>Tên đăng nhập. Rỗng khi hồ sơ nhân viên chưa được cấp tài khoản.</summary>
    public string TenDangNhap { get; set; } = string.Empty;

    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }

    /// <summary>Chức vụ nhân sự (do quản trị khai báo, tách khỏi vai trò phần mềm).</summary>
    public string? ChucVu { get; set; }

    /// <summary>Vai trò sử dụng phần mềm, gộp nhiều vai trò bằng dấu phẩy.</summary>
    public string? VaiTro { get; set; }

    public string? MaVaiTro { get; set; }
    public uint? ChiNhanhId { get; set; }

    /// <summary>Bộ phận / đơn vị trực thuộc (chi nhánh).</summary>
    public string? TenChiNhanh { get; set; }

    /// <summary>
    /// Trạng thái làm việc: true = Đang làm việc; false = Ngừng hoạt động.
    /// </summary>
    public bool TrangThai { get; set; } = true;

    public string TrangThaiText => TrangThai ? "Đang làm việc" : "Ngừng hoạt động";

    /// <summary>
    /// true khi hồ sơ nhân viên này đã được cấp tài khoản đăng nhập phần mềm.
    /// Hồ sơ nhân viên không bắt buộc phải có tài khoản.
    /// </summary>
    public bool CoTaiKhoan { get; set; } = true;

    public string TaiKhoanText => CoTaiKhoan ? "Đã có tài khoản" : "Chưa có tài khoản";

    /// <summary>Ngày vào làm của nhân viên.</summary>
    public DateTime? NgayVaoLam { get; set; }

    public DateTime? LanDangNhapCuoi { get; set; }
    public DateTime? NgayTao { get; set; }

    /// <summary>
    /// Kiểm tra xem tài khoản này có phải là tài khoản Quản trị viên hay không.
    /// </summary>
    public bool IsAdmin =>
        string.Equals(MaVaiTro, "QUAN_TRI", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(MaVaiTro, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
        (VaiTro?.IndexOf("Quản trị", StringComparison.OrdinalIgnoreCase) >= 0);

    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersEdit) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersDelete) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanLock => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersLock) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanUnlock => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersUnlock) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanAssignPermission => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission) || Quyen.IsAdmin(Session.CurrentUser));
}
