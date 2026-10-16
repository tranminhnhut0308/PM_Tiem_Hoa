using System;
using System.Linq;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Kiểm tra quyền người dùng dựa trên vai trò và quyền chi tiết.
/// </summary>
public static class Quyen
{
    public const string QuanTri = "QUAN_TRI";
    public const string Admin = "ADMIN";
    public const string QuanLy = "QUAN_LY";
    public const string Kho = "KHO";
    public const string ThuKho = "THU_KHO";
    public const string ThuNgan = "THU_NGAN";
    public const string KeToan = "KE_TOAN";
    public const string NhanVien = "NHAN_VIEN";
    public const string BaoCao = "BAO_CAO";

    public static bool HasAnyRole(NguoiDung? nguoiDung, params string[] maVaiTro)
    {
        if (nguoiDung is null || maVaiTro.Length == 0)
            return false;

        foreach (var ma in nguoiDung.DanhSachMaVaiTro())
        {
            foreach (var canCo in maVaiTro)
            {
                if (string.Equals(ma, canCo, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Ánh xạ tương đương giữa hai quy ước vai trò (hoa_shop vs shophoa_Huy_tiemhoatest)
                if ((string.Equals(ma, "ADMIN", StringComparison.OrdinalIgnoreCase) || string.Equals(ma, "QUAN_TRI", StringComparison.OrdinalIgnoreCase))
                    && (string.Equals(canCo, "ADMIN", StringComparison.OrdinalIgnoreCase) || string.Equals(canCo, "QUAN_TRI", StringComparison.OrdinalIgnoreCase)))
                    return true;

                if ((string.Equals(ma, "THU_KHO", StringComparison.OrdinalIgnoreCase) || string.Equals(ma, "KHO", StringComparison.OrdinalIgnoreCase))
                    && (string.Equals(canCo, "THU_KHO", StringComparison.OrdinalIgnoreCase) || string.Equals(canCo, "KHO", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        return false;
    }

    public static bool IsAdmin(NguoiDung? nguoiDung)
    {
        return HasAnyRole(nguoiDung, QuanTri, Admin);
    }

    public static bool HasPermission(NguoiDung? nguoiDung, string? permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return false;
        if (nguoiDung is null)
            return false;

        if (IsAdmin(nguoiDung))
            return true;

        return Session.HasPermission(permissionKey);
    }

    public static bool HasAnyPermission(NguoiDung? nguoiDung, params string[] keys)
    {
        if (keys == null || keys.Length == 0)
            return false;
        if (nguoiDung is null)
            return false;

        if (IsAdmin(nguoiDung))
            return true;

        return Session.HasAnyPermission(keys);
    }

    public static void RequirePermission(NguoiDung? nguoiDung, string permissionKey, string? message = null)
    {
        if (!HasPermission(nguoiDung, permissionKey))
        {
            throw new UnauthorizedAccessException(message ?? "Bạn không có quyền thực hiện chức năng này.");
        }
    }
}
