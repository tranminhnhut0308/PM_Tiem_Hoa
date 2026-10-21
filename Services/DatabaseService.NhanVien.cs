using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Lớp mở rộng của <see cref="DatabaseService"/> phục vụ màn "Quản lý nhân viên".
///
/// Về dữ liệu: WINWON không có bảng "nhan_vien" riêng. Hồ sơ nhân viên và tài khoản
/// đăng nhập đều nằm trong bảng <c>nguoi_dung</c> (xem database/hoa_shop.sql).
/// Để tách bạch hai khái niệm mà vẫn không tạo thêm một hệ thống song song, phần
/// mở rộng dưới đây chỉ BỔ SUNG các cột rỗng (nullable) và một cờ đánh dấu:
///
/// - <c>chuc_vu</c>              : chức vụ nhân sự (khác với vai trò phần mềm).
/// - <c>ngay_vao_lam</c>         : ngày vào làm.
/// - <c>co_tai_khoan_dang_nhap</c>: 1 = hồ sơ này được cấp tài khoản đăng nhập,
///                                0 = chỉ là hồ sơ nhân viên (chưa có tài khoản).
///
/// Cột mới có DEFAULT nên mọi dòng dữ liệu cũ vẫn đọc được bình thường,
/// không có bước migration phá huỷ dữ liệu.
/// </summary>
public sealed partial class DatabaseService
{
    private static bool _daKiemTraCotNhanVien;
    private static bool _cotChucVu;
    private static bool _cotNgayVaoLam;
    private static bool _cotCoTaiKhoanDangNhap;

    /// <summary>
    /// Bổ sung các cột phục vụ hồ sơ nhân viên vào bảng <c>nguoi_dung</c>.
    /// Thao tác idempotent, an toàn với dữ liệu cũ.
    /// </summary>
    public async Task EnsureCotNhanVienAsync()
    {
        if (_daKiemTraCotNhanVien)
            return;

        try
        {
            await using var connection = await OpenAsync();

            var existing = (await connection.QueryAsync<string>(
                "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nguoi_dung';"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existing.Contains("chuc_vu"))
            {
                try
                {
                    await connection.ExecuteAsync(
                        "ALTER TABLE nguoi_dung ADD COLUMN chuc_vu VARCHAR(100) COLLATE utf8mb4_unicode_ci NULL AFTER ho_ten;");
                    existing.Add("chuc_vu");
                }
                catch
                {
                    // Không có quyền ALTER: vẫn dùng được nếu cột đã tồn tại từ trước.
                }
            }

            if (!existing.Contains("ngay_vao_lam"))
            {
                try
                {
                    await connection.ExecuteAsync(
                        "ALTER TABLE nguoi_dung ADD COLUMN ngay_vao_lam DATE NULL AFTER email;");
                    existing.Add("ngay_vao_lam");
                }
                catch
                {
                }
            }

            if (!existing.Contains("co_tai_khoan_dang_nhap"))
            {
                try
                {
                    await connection.ExecuteAsync(
                        "ALTER TABLE nguoi_dung ADD COLUMN co_tai_khoan_dang_nhap TINYINT(1) NOT NULL DEFAULT 1 AFTER trang_thai;");
                    existing.Add("co_tai_khoan_dang_nhap");
                }
                catch
                {
                }
            }

            _cotChucVu = existing.Contains("chuc_vu");
            _cotNgayVaoLam = existing.Contains("ngay_vao_lam");
            _cotCoTaiKhoanDangNhap = existing.Contains("co_tai_khoan_dang_nhap");
            _daKiemTraCotNhanVien = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureCotNhanVienAsync", ex);
        }
    }

    /// <summary>
    /// Lấy danh sách hồ sơ nhân viên có phân trang, tìm kiếm và bộ lọc.
    /// </summary>
    /// <param name="search">Từ khóa: mã nhân viên, họ tên, số điện thoại, email, chức vụ hoặc tên bộ phận.</param>
    /// <param name="chiNhanhId">Lọc theo bộ phận / đơn vị trực thuộc (null = tất cả).</param>
    /// <param name="chiCoTaiKhoan">
    /// true = chỉ lấy hồ sơ đã có tài khoản đăng nhập (dùng cho màn Quản lý tài khoản);
    /// false = lấy tất cả hồ sơ (dùng cho màn Quản lý nhân viên).
    /// </param>
    public async Task<(IReadOnlyList<NhanVienItem> Items, int TongSoBanGhi)> GetNhanVienPagedAsync(
        string? search,
        int page,
        int pageSize,
        string? trangThaiLoc = null,
        uint? vaiTroId = null,
        uint? chiNhanhId = null,
        bool? chiCoTaiKhoan = null)
    {
        await EnsureCotNhanVienAsync();
        await using var connection = await OpenAsync();
        await EnsureNguoiDungContactColumnsAsync(connection);

        var keyword = (search ?? string.Empty).Trim();
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        var offset = (safePage - 1) * safePageSize;

        var parameters = new DynamicParameters();
        parameters.Add("PageSize", safePageSize);
        parameters.Add("Offset", offset);

        var whereConditions = new List<string>();

        bool coSdt = _coCotSoDienThoaiNguoiDung == true;
        bool coEmail = _coCotEmailNguoiDung == true;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            parameters.Add("Search", $"%{keyword}%");
            var searchConds = new List<string>
            {
                "nd.ten_dang_nhap LIKE @Search",
                "nd.ho_ten LIKE @Search",
                "nd.ma_nguoi_dung LIKE @Search",
                "cn.ten_chi_nhanh LIKE @Search"
            };

            if (coSdt)
                searchConds.Add("nd.so_dien_thoai LIKE @Search");
            if (coEmail)
                searchConds.Add("nd.email LIKE @Search");
            if (_cotChucVu)
                searchConds.Add("nd.chuc_vu LIKE @Search");

            whereConditions.Add($"({string.Join(" OR ", searchConds)})");
        }

        if (!string.IsNullOrWhiteSpace(trangThaiLoc) && !string.Equals(trangThaiLoc, "TAT_CA", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(trangThaiLoc, "DANG_LAM_VIEC", StringComparison.OrdinalIgnoreCase) || string.Equals(trangThaiLoc, "1"))
            {
                whereConditions.Add("nd.trang_thai = 1");
            }
            else if (string.Equals(trangThaiLoc, "NGUNG_HOAT_DONG", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(trangThaiLoc, "KHOA", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(trangThaiLoc, "0", StringComparison.OrdinalIgnoreCase))
            {
                whereConditions.Add("nd.trang_thai = 0");
            }
        }

        if (vaiTroId.HasValue && vaiTroId.Value > 0)
        {
            parameters.Add("VaiTroId", vaiTroId.Value);
            whereConditions.Add("EXISTS (SELECT 1 FROM nguoi_dung_vai_tro r_vt WHERE r_vt.nguoi_dung_id = nd.id AND r_vt.vai_tro_id = @VaiTroId)");
        }

        if (chiNhanhId.HasValue && chiNhanhId.Value > 0)
        {
            parameters.Add("ChiNhanhId", chiNhanhId.Value);
            whereConditions.Add("nd.chi_nhanh_id = @ChiNhanhId");
        }

        if (chiCoTaiKhoan.HasValue && _cotCoTaiKhoanDangNhap)
        {
            whereConditions.Add(chiCoTaiKhoan.Value
                ? "nd.co_tai_khoan_dang_nhap = 1"
                : "nd.co_tai_khoan_dang_nhap = 0");
        }

        var whereClause = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        var countSql = $"""
        SELECT COUNT(DISTINCT nd.id)
        FROM nguoi_dung nd
        LEFT JOIN chi_nhanh cn ON cn.id = nd.chi_nhanh_id
        {whereClause}
        """;

        var totalRecords = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var selectCols = BuildCotNhanVienSelect("nd", "cn");
        var groupByCols = BuildGroupByNhanVien("nd", "cn");

        var dataSql = $"""
        SELECT
            nd.id AS Id,
            nd.ma_nguoi_dung AS MaNguoiDung,
            nd.ten_dang_nhap AS TenDangNhap,
            nd.ho_ten AS HoTen,
            {selectCols.SoDienThoai} AS SoDienThoai,
            {selectCols.Email} AS Email,
            {selectCols.ChucVu} AS ChucVu,
            {selectCols.NgayVaoLam} AS NgayVaoLam,
            {selectCols.CoTaiKhoan} AS CoTaiKhoan,
            nd.chi_nhanh_id AS ChiNhanhId,
            cn.ten_chi_nhanh AS TenChiNhanh,
            nd.trang_thai AS TrangThai,
            nd.lan_dang_nhap_cuoi AS LanDangNhapCuoi,
            nd.ngay_tao AS NgayTao,
            GROUP_CONCAT(DISTINCT vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro,
            GROUP_CONCAT(DISTINCT vt.ma_vai_tro ORDER BY vt.id SEPARATOR ',') AS MaVaiTro
        FROM nguoi_dung nd
        LEFT JOIN chi_nhanh cn ON cn.id = nd.chi_nhanh_id
        LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
        LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
        {whereClause}
        GROUP BY {groupByCols}
        ORDER BY nd.trang_thai DESC, nd.ho_ten ASC
        LIMIT @PageSize OFFSET @Offset
        """;

        var items = (await connection.QueryAsync<NhanVienItem>(dataSql, parameters)).AsList();
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Stt = offset + i + 1;
        }

        return (items, totalRecords);
    }

    /// <summary>Lấy một hồ sơ nhân viên theo id (dùng khi mở form sửa).</summary>
    public async Task<NhanVienItem?> GetNhanVienByIdAsync(uint id)
    {
        await EnsureCotNhanVienAsync();
        await using var connection = await OpenAsync();
        await EnsureNguoiDungContactColumnsAsync(connection);

        var selectCols = BuildCotNhanVienSelect("nd", "cn");
        var groupByCols = BuildGroupByNhanVien("nd", "cn");

        var sql = $"""
        SELECT
            nd.id AS Id,
            nd.ma_nguoi_dung AS MaNguoiDung,
            nd.ten_dang_nhap AS TenDangNhap,
            nd.ho_ten AS HoTen,
            {selectCols.SoDienThoai} AS SoDienThoai,
            {selectCols.Email} AS Email,
            {selectCols.ChucVu} AS ChucVu,
            {selectCols.NgayVaoLam} AS NgayVaoLam,
            {selectCols.CoTaiKhoan} AS CoTaiKhoan,
            nd.chi_nhanh_id AS ChiNhanhId,
            cn.ten_chi_nhanh AS TenChiNhanh,
            nd.trang_thai AS TrangThai,
            nd.lan_dang_nhap_cuoi AS LanDangNhapCuoi,
            nd.ngay_tao AS NgayTao,
            GROUP_CONCAT(DISTINCT vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro,
            GROUP_CONCAT(DISTINCT vt.ma_vai_tro ORDER BY vt.id SEPARATOR ',') AS MaVaiTro
        FROM nguoi_dung nd
        LEFT JOIN chi_nhanh cn ON cn.id = nd.chi_nhanh_id
        LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
        LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
        WHERE nd.id = @Id
        GROUP BY {groupByCols}
        """;

        return await connection.QuerySingleOrDefaultAsync<NhanVienItem>(sql, new { Id = id });
    }

    /// <summary>
    /// Thêm mới hồ sơ nhân viên.
    /// Tài khoản đăng nhập là TÙY CHỌN: nếu không cấp thì hồ sơ vẫn tồn tại nhưng
    /// nhân viên chưa có tài khoản sử dụng phần mềm.
    /// </summary>
    public async Task<uint> ThemNhanVienAsync(
        NhanVienItem item,
        string? tenDangNhap = null,
        string? matKhau = null,
        IEnumerable<uint>? vaiTroIds = null)
    {
        await EnsureCotNhanVienAsync();

        await using var connection = await OpenAsync();
        await EnsureNguoiDungContactColumnsAsync(connection);
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var maNguoiDung = await GenerateMaNguoiDungAsync(connection, tx);
            var coTaiKhoan = !string.IsNullOrWhiteSpace(tenDangNhap);

            // Hồ sơ chưa có tài khoản vẫn cần một "tên đăng nhập" duy nhất vì cột này
            // có ràng buộc UNIQUE NOT NULL. Dùng mã nội bộ dạng nv_noi_bo để không
            // đụng với tên đăng nhập thật của người dùng.
            var tenDangNhapLuu = coTaiKhoan
                ? tenDangNhap!.Trim()
                : await TaoTenDangNhapNoiBoAsync(connection, tx);

            // Mật khẩu của hồ sơ chưa có tài khoản là chuỗi ngẫu nhiên không ai biết,
            // kèm trang_thai = 0 nên tài khoản này không thể đăng nhập.
            var matKhauLuu = coTaiKhoan
                ? BCrypt.Net.BCrypt.HashPassword(matKhau ?? string.Empty)
                : BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

            bool coSdt = _coCotSoDienThoaiNguoiDung == true;
            bool coEmail = _coCotEmailNguoiDung == true;

            var sql = (coSdt && coEmail)
                ? """
                INSERT INTO nguoi_dung (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten, so_dien_thoai, email,
                                        chuc_vu, ngay_vao_lam, chi_nhanh_id, trang_thai, co_tai_khoan_dang_nhap, ngay_tao, ngay_cap_nhat)
                VALUES (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen, @SoDienThoai, @Email,
                        @ChucVu, @NgayVaoLam, @ChiNhanhId, @TrangThai, @CoTaiKhoan, NOW(), NOW())
                """
                : """
                INSERT INTO nguoi_dung (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten,
                                        chuc_vu, ngay_vao_lam, chi_nhanh_id, trang_thai, co_tai_khoan_dang_nhap, ngay_tao, ngay_cap_nhat)
                VALUES (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen,
                        @ChucVu, @NgayVaoLam, @ChiNhanhId, @TrangThai, @CoTaiKhoan, NOW(), NOW())
                """;

            await connection.ExecuteAsync(sql, new
            {
                MaNguoiDung = maNguoiDung,
                TenDangNhap = tenDangNhapLuu,
                MatKhau = matKhauLuu,
                HoTen = item.HoTen,
                SoDienThoai = item.SoDienThoai,
                Email = item.Email,
                ChucVu = string.IsNullOrWhiteSpace(item.ChucVu) ? null : item.ChucVu.Trim(),
                NgayVaoLam = item.NgayVaoLam,
                ChiNhanhId = item.ChiNhanhId,
                TrangThai = item.TrangThai ? 1 : 0,
                CoTaiKhoan = coTaiKhoan ? 1 : 0
            }, tx);

            var userId = await connection.ExecuteScalarAsync<uint>("SELECT LAST_INSERT_ID();", null, tx);

            if (coTaiKhoan && vaiTroIds != null)
            {
                foreach (var rid in vaiTroIds.Where(r => r > 0).Distinct())
                {
                    await connection.ExecuteAsync(
                        "INSERT IGNORE INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId)",
                        new { UserId = userId, RoleId = rid }, tx);
                }
            }

            await tx.CommitAsync();
            return userId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Cập nhật hồ sơ nhân viên (thông tin liên hệ, bộ phận, chức vụ, trạng thái).
    /// Không đụng tới tài khoản đăng nhập, mật khẩu và quyền.
    /// </summary>
    public async Task CapNhatNhanVienAsync(NhanVienItem item)
    {
        await EnsureCotNhanVienAsync();

        await using var connection = await OpenAsync();
        await EnsureNguoiDungContactColumnsAsync(connection);

        bool coSdt = _coCotSoDienThoaiNguoiDung == true;
        bool coEmail = _coCotEmailNguoiDung == true;
        var contactCols = (coSdt && coEmail) ? ", so_dien_thoai = @SoDienThoai, email = @Email" : "";

        var chucVuCol = _cotChucVu ? ", chuc_vu = @ChucVu" : "";
        var ngayVaoLamCol = _cotNgayVaoLam ? ", ngay_vao_lam = @NgayVaoLam" : "";

        await connection.ExecuteAsync(
            $"""
            UPDATE nguoi_dung
            SET ho_ten = @HoTen {contactCols}{chucVuCol}{ngayVaoLamCol},
                chi_nhanh_id = @ChiNhanhId,
                trang_thai = @TrangThai,
                ngay_cap_nhat = NOW()
            WHERE id = @Id
            """,
            new
            {
                Id = item.Id,
                HoTen = item.HoTen,
                SoDienThoai = item.SoDienThoai,
                Email = item.Email,
                ChucVu = string.IsNullOrWhiteSpace(item.ChucVu) ? null : item.ChucVu.Trim(),
                NgayVaoLam = item.NgayVaoLam,
                ChiNhanhId = item.ChiNhanhId,
                TrangThai = item.TrangThai ? 1 : 0
            });
    }

    /// <summary>
    /// Cấp tài khoản đăng nhập cho một hồ sơ nhân viên đang chưa có tài khoản.
    /// </summary>
    public async Task CapTaiKhoanChoNhanVienAsync(
        uint nhanVienId,
        string tenDangNhap,
        string matKhau,
        IEnumerable<uint>? vaiTroIds = null)
    {
        if (string.IsNullOrWhiteSpace(tenDangNhap))
            throw new ArgumentException("Tên đăng nhập không được để trống.");
        if (string.IsNullOrWhiteSpace(matKhau))
            throw new ArgumentException("Mật khẩu không được để trống.");

        await EnsureCotNhanVienAsync();

        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var daCoTaiKhoan = await connection.ExecuteScalarAsync<int?>(
                "SELECT co_tai_khoan_dang_nhap FROM nguoi_dung WHERE id = @Id", new { Id = nhanVienId }, tx) ?? 0;
            if (daCoTaiKhoan == 1)
                throw new InvalidOperationException("Hồ sơ nhân viên này đã có tài khoản đăng nhập.");

            var hashed = BCrypt.Net.BCrypt.HashPassword(matKhau);
            await connection.ExecuteAsync(
                "UPDATE nguoi_dung SET ten_dang_nhap = @TenDangNhap, mat_khau = @MatKhau, co_tai_khoan_dang_nhap = 1, ngay_cap_nhat = NOW() WHERE id = @Id",
                new { Id = nhanVienId, TenDangNhap = tenDangNhap.Trim(), MatKhau = hashed }, tx);

            if (vaiTroIds != null)
            {
                foreach (var rid in vaiTroIds.Where(r => r > 0).Distinct())
                {
                    await connection.ExecuteAsync(
                        "INSERT IGNORE INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId)",
                        new { UserId = nhanVienId, RoleId = rid }, tx);
                }
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Sinh tên đăng nhập nội bộ cho hồ sơ nhân viên chưa có tài khoản.
    /// Dùng tiền tố "nv_noi_bo" để không trùng với tên đăng nhập thật.
    /// </summary>
    private static async Task<string> TaoTenDangNhapNoiBoAsync(
        MySqlConnection conn,
        MySqlTransaction? tx = null)
    {
        const string baseName = "nv_noi_bo";
        var candidate = baseName;
        var suffix = 1;

        while (await conn.ExecuteScalarAsync<int>(
                   "SELECT COUNT(1) FROM nguoi_dung WHERE ten_dang_nhap = @Code",
                   new { Code = candidate },
                   tx) > 0)
        {
            suffix++;
            candidate = $"{baseName}_{suffix}";
        }

        return candidate;
    }

    /// <summary>Dựng biểu thức SELECT cho các cột hồ sơ nhân viên (xử lý cột chưa tồn tại).</summary>
    private (string SoDienThoai, string Email, string ChucVu, string NgayVaoLam, string CoTaiKhoan) BuildCotNhanVienSelect(string n, string cn)
    {
        var sdt = _coCotSoDienThoaiNguoiDung == true ? $"{n}.so_dien_thoai" : "NULL";
        var email = _coCotEmailNguoiDung == true ? $"{n}.email" : "NULL";
        var chucVu = _cotChucVu ? $"{n}.chuc_vu" : "NULL";
        var ngayVaoLam = _cotNgayVaoLam ? $"{n}.ngay_vao_lam" : "NULL";
        var coTaiKhoan = _cotCoTaiKhoanDangNhap ? $"{n}.co_tai_khoan_dang_nhap" : "1";
        return (sdt, email, chucVu, ngayVaoLam, coTaiKhoan);
    }

    /// <summary>Dựng danh sách cột cho GROUP BY của truy vấn hồ sơ nhân viên.</summary>
    private string BuildGroupByNhanVien(string n, string cn)
    {
        var cols = new List<string>
        {
            $"{n}.id",
            $"{n}.ma_nguoi_dung",
            $"{n}.ten_dang_nhap",
            $"{n}.ho_ten",
            $"{n}.chi_nhanh_id",
            $"{cn}.ten_chi_nhanh",
            $"{n}.trang_thai",
            $"{n}.lan_dang_nhap_cuoi",
            $"{n}.ngay_tao"
        };

        if (_coCotSoDienThoaiNguoiDung == true)
            cols.Add($"{n}.so_dien_thoai");
        if (_coCotEmailNguoiDung == true)
            cols.Add($"{n}.email");
        if (_cotChucVu)
            cols.Add($"{n}.chuc_vu");
        if (_cotNgayVaoLam)
            cols.Add($"{n}.ngay_vao_lam");
        if (_cotCoTaiKhoanDangNhap)
            cols.Add($"{n}.co_tai_khoan_dang_nhap");

        return string.Join(", ", cols);
    }

    // ---------------------------------------------------------------------
    // BỘ PHẬN / ĐƠN VỊ TRỰC THUỘC (dùng bảng chi_nhanh hiện có)
    // ---------------------------------------------------------------------

    /// <summary>Lấy danh sách bộ phận / chi nhánh để hiển thị trong ComboBox.</summary>
    public async Task<List<ProductOption>> GetChiNhanhOptionsAsync(bool chiTrangThai = true)
    {
        try
        {
            await using var connection = await OpenAsync();
            var sql = chiTrangThai
                ? "SELECT id AS Id, ten_chi_nhanh AS Name FROM chi_nhanh WHERE trang_thai = 1 ORDER BY ten_chi_nhanh;"
                : "SELECT id AS Id, ten_chi_nhanh AS Name FROM chi_nhanh ORDER BY ten_chi_nhanh;";

            var list = (await connection.QueryAsync<ProductOption>(sql)).AsList();
            if (list.Count > 0)
                return list;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("GetChiNhanhOptionsAsync", ex);
        }

        return new List<ProductOption>();
    }

    /// <summary>Thêm mới một bộ phận / chi nhánh.</summary>
    public async Task<uint> ThemChiNhanhAsync(string tenChiNhanh, string? maChiNhanh = null)
    {
        if (string.IsNullOrWhiteSpace(tenChiNhanh))
            throw new ArgumentException("Tên bộ phận không được để trống.");

        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var ma = string.IsNullOrWhiteSpace(maChiNhanh)
                ? await TaoMaChiNhanhAsync(connection, tx)
                : maChiNhanh!.Trim();

            await connection.ExecuteAsync(
                "INSERT INTO chi_nhanh (ma_chi_nhanh, ten_chi_nhanh, trang_thai, ngay_tao, ngay_cap_nhat) VALUES (@Ma, @Ten, 1, NOW(), NOW())",
                new { Ma = ma, Ten = tenChiNhanh.Trim() }, tx);

            var id = await connection.ExecuteScalarAsync<uint>("SELECT LAST_INSERT_ID();", null, tx);
            await tx.CommitAsync();
            return id;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>Cập nhật tên / trạng thái của một bộ phận / chi nhánh.</summary>
    public async Task CapNhatChiNhanhAsync(uint id, string tenChiNhanh, bool trangThai)
    {
        if (string.IsNullOrWhiteSpace(tenChiNhanh))
            throw new ArgumentException("Tên bộ phận không được để trống.");

        await using var connection = await OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE chi_nhanh SET ten_chi_nhanh = @Ten, trang_thai = @TrangThai, ngay_cap_nhat = NOW() WHERE id = @Id",
            new { Id = id, Ten = tenChiNhanh.Trim(), TrangThai = trangThai ? 1 : 0 });
    }

    private static async Task<string> TaoMaChiNhanhAsync(
        MySqlConnection conn,
        MySqlTransaction? tx = null)
    {
        var nextNum = await conn.ExecuteScalarAsync<long>(
            """
            SELECT COALESCE(MAX(CAST(SUBSTRING(ma_chi_nhanh, 3) AS UNSIGNED)), 0) + 1
            FROM chi_nhanh
            WHERE ma_chi_nhanh REGEXP '^CN[0-9]{1,8}$';
            """,
            transaction: tx);

        if (nextNum <= 1)
            nextNum = 1;

        var code = $"CN{nextNum:D3}";
        while (await conn.ExecuteScalarAsync<int>(
                   "SELECT COUNT(1) FROM chi_nhanh WHERE ma_chi_nhanh = @Code",
                   new { Code = code },
                   tx) > 0)
        {
            nextNum++;
            code = $"CN{nextNum:D3}";
        }

        return code;
    }
}
