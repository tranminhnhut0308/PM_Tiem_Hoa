using System.Collections.Generic;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Phiên làm việc của người dùng đang chạy ứng dụng.
/// Chỉ giữ trong RAM, đóng ứng dụng là mất.
/// </summary>
public static class Session
{
    public static NguoiDung? CurrentUser { get; private set; }
    public static HashSet<string> Permissions { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static List<string> PermissionList { get; private set; } = new();

    public static bool IsLoggedIn => CurrentUser is not null;

    public static void SetCurrentUser(NguoiDung user)
    {
        SetCurrentUser(user, Array.Empty<string>());
    }

    public static void SetCurrentUser(NguoiDung user, IEnumerable<string> permissions)
    {
        CurrentUser = user;
        Permissions.Clear();
        PermissionList.Clear();

        foreach (var p in permissions)
        {
            if (string.IsNullOrWhiteSpace(p))
                continue;
            var key = p.Trim();
            if (Permissions.Add(key))
            {
                PermissionList.Add(key);
            }
        }
    }

    public static bool HasPermission(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        if (CurrentUser == null)
            return false;

        // Admin bypass
        foreach (var ma in CurrentUser.DanhSachMaVaiTro())
        {
            if (string.Equals(ma, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return Permissions.Contains(key.Trim());
    }

    public static bool HasAnyPermission(params string[] keys)
    {
        if (keys == null || keys.Length == 0)
            return false;
        if (CurrentUser == null)
            return false;

        foreach (var ma in CurrentUser.DanhSachMaVaiTro())
        {
            if (string.Equals(ma, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var k in keys)
        {
            if (!string.IsNullOrWhiteSpace(k) && Permissions.Contains(k.Trim()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Kết thúc phiên hiện tại.
    /// </summary>
    public static void Logout()
    {
        CurrentUser = null;
        Permissions.Clear();
        PermissionList.Clear();
    }

    public static void Clear()
    {
        Logout();
    }
}
