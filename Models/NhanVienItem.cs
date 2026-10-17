using System;
using ShopHoa.Services;

namespace ShopHoa.Models;

/// <summary>
/// Model đại diện cho một dòng nhân viên trong danh sách "Danh mục → Đối tác → Nhân viên".
/// Kết hợp dữ liệu từ nguoi_dung, chi_nhanh và vai_tro.
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

    public string TenDangNhap { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Chức vụ / bộ phận đảm nhiệm (chuỗi vai trò: Quản lý, Thu ngân, Kế toán, Thủ kho...).
    /// </summary>
    public string? ChucVu { get; set; }

    public string? MaVaiTro { get; set; }
    public uint? ChiNhanhId { get; set; }
    public string? TenChiNhanh { get; set; }

    /// <summary>
    /// Trạng thái làm việc: true = Đang làm việc / Hoạt động; false = Bị khóa / Đã nghỉ.
    /// </summary>
    public bool TrangThai { get; set; } = true;

    public string TrangThaiText => TrangThai ? "Đang làm việc" : "Đã khóa";

    public DateTime? LanDangNhapCuoi { get; set; }
    public DateTime? NgayTao { get; set; }

    /// <summary>
    /// Kiểm tra xem nhân viên này có phải là tài khoản Quản trị viên hay không.
    /// </summary>
    public bool IsAdmin =>
        string.Equals(MaVaiTro, "QUAN_TRI", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(MaVaiTro, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
        (ChucVu?.IndexOf("Quản trị", StringComparison.OrdinalIgnoreCase) >= 0);

    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersEdit) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersDelete) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanLock => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersLock) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanUnlock => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersUnlock) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanAssignPermission => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission) || Quyen.IsAdmin(Session.CurrentUser));
}
