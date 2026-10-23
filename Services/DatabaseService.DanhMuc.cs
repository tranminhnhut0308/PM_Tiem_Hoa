using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Quản lý danh mục <b>Nhóm hàng</b> (bảng <c>nhom_san_pham</c>) và
/// <b>Đơn vị tính</b> (bảng <c>don_vi_tinh</c>).
/// <para>
/// Lưu ý về lịch sử: hai bảng này là bảng "lớp tương thích" của ứng dụng,
/// được định nghĩa ở <c>database/shophoa_Huy_tiemhoatest_fixed.sql</c> mục 7.6.3.
/// Database ERP gốc chỉ có <c>nhom_hang_hoa</c> và <c>don_vi_tinh_hang_hoa</c>,
/// nên khi kết nối vào database chưa có migration, MySQL báo lỗi 1146
/// ("Table ... doesn't exist"). Vì vậy mọi truy vấn danh mục ở đây đều gọi
/// <see cref="EnsureBangDanhMucAsync"/> trước để bảo đảm bảng/cột tồn tại,
/// và nếu bảng vừa được tạo thì nội dung danh mục cũ được sao chép sang từ
/// bảng ERP để không mất dữ liệu.
/// </para>
/// </summary>
public sealed partial class DatabaseService
{
    /// <summary>
    /// Khóa của kết nối đã được bảo đảm có đủ bảng danh mục. Khóa gồm
    /// server + port + database + user (không phải chỉ tên database) để khi
    /// người dùng đổi sang server khác trùng tên database trong "Cài đặt kết
    /// nối Database" thì bảng danh mục của server mới vẫn được bảo đảm.
    /// </summary>
    private string? _daDamBaoBangDanhMuc;

    private const string TaoBangNhomSanPhamSql = """
        CREATE TABLE IF NOT EXISTS `nhom_san_pham` (
          `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
          `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
          `ten_nhom` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
          `nhom_cha_id` int(10) UNSIGNED DEFAULT NULL,
          `ghi_chu` text COLLATE utf8mb4_unicode_ci,
          `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
          PRIMARY KEY (`id`),
          KEY `idx_nhom_san_pham_ten` (`ten_nhom`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

    private const string TaoBangDonViTinhSql = """
        CREATE TABLE IF NOT EXISTS `don_vi_tinh` (
          `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
          `ma_don_vi` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
          `ten_don_vi` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
          `ghi_chu` text COLLATE utf8mb4_unicode_ci,
          `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
          PRIMARY KEY (`id`),
          KEY `idx_don_vi_tinh_ten` (`ten_don_vi`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

    /// <summary>
    /// Bảo đảm bảng <c>nhom_san_pham</c> và <c>don_vi_tinh</c> tồn tại với đúng
    /// cấu trúc mà ứng dụng cần, kèm theo bước bổ sung các cột còn thiếu cho
    /// database đã có bảng nhưng shape cũ.
    /// <para>
    /// - Nếu bảng chưa tồn tại: tạo mới rồi sao chép danh mục cũ từ bảng ERP
    ///   (<c>nhom_hang_hoa</c> / <c>don_vi_tinh_hang_hoa</c>) sang để không mất dữ liệu.
    /// - Nếu bảng đã tồn tại: chỉ bổ sung cột còn thiếu, không đụng tới dữ liệu.
    /// - Không bao giờ DROP/TRUNCATE/DELETE.
    /// </para>
    /// </summary>
    private async Task EnsureBangDanhMucAsync(MySqlConnection connection)
    {
        var khoa = TaoKhoaBangDanhMuc(connection);

        // Cache theo đúng server/port/database/user. Vẫn kiểm tra lại sự tồn tại
        // của cả hai bảng để trường hợp bảng bị xóa/đổi database trong lúc ứng
        // dụng đang chạy thì lần truy vấn kế tiếp tự tạo lại thay vì trả lỗi 1146.
        if (string.Equals(_daDamBaoBangDanhMuc, khoa, StringComparison.OrdinalIgnoreCase)
            && await HaiBangDanhMucTonTaiAsync(connection))
        {
            return;
        }

        try
        {
            if (!await BangDanhMucTonTaiAsync(connection, "nhom_san_pham"))
            {
                await connection.ExecuteAsync(TaoBangNhomSanPhamSql);
                await SaoChepNhomSanPhamAnToanAsync(connection);
            }
            else
            {
                await EnsureCotBangDanhMucAsync(
                    connection,
                    "nhom_san_pham",
                    ("ma_nhom", "varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''"),
                    ("ten_nhom", "varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''"),
                    ("nhom_cha_id", "int(10) UNSIGNED DEFAULT NULL"),
                    ("ghi_chu", "text COLLATE utf8mb4_unicode_ci"),
                    ("trang_thai", "tinyint(1) NOT NULL DEFAULT 1"));

                await BackfillMaDanhMucAsync(connection, "nhom_san_pham", "ma_nhom", "NH");
            }

            if (!await BangDanhMucTonTaiAsync(connection, "don_vi_tinh"))
            {
                await connection.ExecuteAsync(TaoBangDonViTinhSql);
                await SaoChepDonViTinhAnToanAsync(connection);
            }
            else
            {
                await EnsureCotBangDanhMucAsync(
                    connection,
                    "don_vi_tinh",
                    ("ma_don_vi", "varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''"),
                    ("ten_don_vi", "varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''"),
                    ("ghi_chu", "text COLLATE utf8mb4_unicode_ci"),
                    ("trang_thai", "tinyint(1) NOT NULL DEFAULT 1"));

                await BackfillMaDanhMucAsync(connection, "don_vi_tinh", "ma_don_vi", "DVT");
            }
        }
        catch (MySqlException ex) when (ex.Number is 1044 or 1045 or 1142)
        {
            // Tài khoản kết nối không có quyền tạo bảng -> báo rõ để người dùng
            // chạy file migration thủ công thay vì báo lỗi chung chung.
            SafeLog.WriteError("Không tạo được bảng danh mục nhom_san_pham / don_vi_tinh", ex);

            throw new InvalidOperationException(
                "Database chưa có bảng 'nhom_san_pham' / 'don_vi_tinh' và tài khoản kết nối không có quyền tạo bảng. " +
                "Vui lòng chạy file 'database/migration_danh_muc_nhom_hang_don_vi_tinh.sql' trên database đang dùng.",
                ex);
        }
        catch (Exception ex)
        {
            // Không nuốt lỗi nữa: nếu không tạo/bổ sung được schema thì phải báo
            // đúng nguyên nhân thay vì để câu SELECT kế tiếp ném lỗi 1146 khó hiểu.
            SafeLog.WriteError("Lỗi bảo đảm bảng danh mục", ex);

            throw new InvalidOperationException(
                "Không thể khởi tạo/bổ sung bảng danh mục trên database đang dùng: " +
                SafeLog.Redact(ex.Message),
                ex);
        }

        _daDamBaoBangDanhMuc = khoa;
    }

    /// <summary>
    /// Khóa nhận diện kết nối đang dùng (server + port + database + user).
    /// Không chứa mật khẩu.
    /// </summary>
    private static string TaoKhoaBangDanhMuc(MySqlConnection connection)
    {
        var builder = new MySqlConnectionStringBuilder(connection.ConnectionString);

        return $"{builder.Server}|{builder.Port}|{builder.Database}|{builder.UserID}";
    }

    /// <summary>
    /// Kiểm tra nhanh cả hai bảng danh mục còn tồn tại trong database hiện tại.
    /// </summary>
    private static async Task<bool> HaiBangDanhMucTonTaiAsync(MySqlConnection connection)
    {
        var soBang = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT TABLE_NAME)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('nhom_san_pham', 'don_vi_tinh');
            """);

        return soBang == 2;
    }

    /// <summary>
    /// Sao chép dữ liệu cũ nhưng không làm hỏng chức năng thêm mới nếu bảng ERP
    /// nguồn có cấu trúc lạ: lỗi sao chép chỉ được ghi log.
    /// </summary>
    private static async Task SaoChepNhomSanPhamAnToanAsync(MySqlConnection connection)
    {
        try
        {
            await SaoChepNhomSanPhamAsync(connection);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không sao chép được nhóm hàng cũ từ nhom_hang_hoa", ex);
        }
    }

    /// <summary>
    /// Sao chép đơn vị tính cũ nhưng không làm hỏng chức năng thêm mới nếu bảng
    /// ERP nguồn có cấu trúc lạ: lỗi sao chép chỉ được ghi log.
    /// </summary>
    private static async Task SaoChepDonViTinhAnToanAsync(MySqlConnection connection)
    {
        try
        {
            await SaoChepDonViTinhAsync(connection);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không sao chép được đơn vị tính cũ từ don_vi_tinh_hang_hoa", ex);
        }
    }

    /// <summary>
    /// Kiểm tra bảng đã tồn tại trong database hiện tại hay chưa.
    /// </summary>
    private static async Task<bool> BangDanhMucTonTaiAsync(MySqlConnection connection, string tenBang)
    {
        var soBang = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TenBang;",
            new { TenBang = tenBang });

        return soBang > 0;
    }

    /// <summary>
    /// Bổ sung các cột còn thiếu cho bảng danh mục đã tồn tại (không xóa gì).
    /// </summary>
    private static async Task EnsureCotBangDanhMucAsync(
        MySqlConnection connection,
        string tenBang,
        params (string TenCot, string DinhNghia)[] cotCanThieu)
    {
        var cotHienCo = (await connection.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TenBang;",
            new { TenBang = tenBang })).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (tenCot, dinhNghia) in cotCanThieu)
        {
            if (cotHienCo.Contains(tenCot))
                continue;

            try
            {
                await connection.ExecuteAsync(
                    $"ALTER TABLE `{tenBang}` ADD COLUMN `{tenCot}` {dinhNghia};");
            }
            catch (MySqlException ex) when (ex.Number == 1060)
            {
                // Một tiến trình khác vừa thêm cùng cột -> không còn thiếu nữa.
            }
        }
    }

    /// <summary>
    /// Điền mã cho các bản ghi bị trống mã sau khi cột mã vừa được thêm vào
    /// bảng cũ (tránh đụng ràng buộc UNIQUE khi thêm mục mới).
    /// </summary>
    private static async Task BackfillMaDanhMucAsync(
        MySqlConnection connection,
        string tenBang,
        string tenCot,
        string tienTo)
    {
        await connection.ExecuteAsync(
            $"""
             UPDATE `{tenBang}`
             SET `{tenCot}` = CONCAT(@TienTo, LPAD(`id`, 8, '0'))
             WHERE `{tenCot}` IS NULL OR `{tenCot}` = '';
             """,
            new { TienTo = tienTo });
    }

    /// <summary>
    /// Sao chép danh mục nhóm hàng cũ từ bảng ERP <c>nhom_hang_hoa</c> sang bảng
    /// tương thích <c>nhom_san_pham</c>. Tự bỏ qua nếu bảng ERP không tồn tại.
    /// </summary>
    private static async Task SaoChepNhomSanPhamAsync(MySqlConnection connection)
    {
        if (!await BangDanhMucTonTaiAsync(connection, "nhom_hang_hoa"))
            return;

        var cot = await CotBangDanhMucTonTaiAsync(connection, "nhom_hang_hoa", "ma_nhom_hang_hoa", "ten_nhom_hang_hoa");
        if (!cot)
            return;

        var coCotGhiChu = await CotBangDanhMucTonTaiAsync(connection, "nhom_hang_hoa", "ghi_chu");

        var sql = coCotGhiChu
            ? """
              INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`)
              SELECT nhh.`id`, nhh.`ma_nhom_hang_hoa`, nhh.`ten_nhom_hang_hoa`, nhh.`ghi_chu`, 1
              FROM `nhom_hang_hoa` nhh
              ON DUPLICATE KEY UPDATE
                `ten_nhom` = VALUES(`ten_nhom`),
                `ghi_chu`  = VALUES(`ghi_chu`);
              """
            : """
              INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `trang_thai`)
              SELECT nhh.`id`, nhh.`ma_nhom_hang_hoa`, nhh.`ten_nhom_hang_hoa`, 1
              FROM `nhom_hang_hoa` nhh
              ON DUPLICATE KEY UPDATE
                `ten_nhom` = VALUES(`ten_nhom`);
              """;

        await connection.ExecuteAsync(sql);
    }

    /// <summary>
    /// Sao chép danh mục đơn vị tính cũ từ bảng ERP <c>don_vi_tinh_hang_hoa</c>
    /// sang bảng tương thích <c>don_vi_tinh</c>.
    /// </summary>
    private static async Task SaoChepDonViTinhAsync(MySqlConnection connection)
    {
        if (!await BangDanhMucTonTaiAsync(connection, "don_vi_tinh_hang_hoa"))
            return;

        var cot = await CotBangDanhMucTonTaiAsync(connection, "don_vi_tinh_hang_hoa", "ma_don_vi_tinh", "ten_don_vi_tinh");
        if (!cot)
            return;

        await connection.ExecuteAsync("""
            INSERT INTO `don_vi_tinh` (`id`, `ma_don_vi`, `ten_don_vi`, `trang_thai`)
            SELECT dvt.`id`, dvt.`ma_don_vi_tinh`, dvt.`ten_don_vi_tinh`, 1
            FROM `don_vi_tinh_hang_hoa` dvt
            ON DUPLICATE KEY UPDATE
              `ten_don_vi` = VALUES(`ten_don_vi`);
            """);
    }

    /// <summary>
    /// Kiểm tra các cột nguồn có tồn tại trong bảng ERP hay không.
    /// </summary>
    private static async Task<bool> CotBangDanhMucTonTaiAsync(MySqlConnection connection, string tenBang, params string[] tenCot)
    {
        var danhSachCot = string.Join(", ", tenCot.Select(c => $"'{c}'"));

        var soCot = await connection.ExecuteScalarAsync<int>(
            $"""
             SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TenBang
               AND COLUMN_NAME IN ({danhSachCot});
             """,
            new { TenBang = tenBang });

        return soCot == tenCot.Length;
    }

    /// <summary>
    /// Sinh mã danh mục duy nhất (tối đa 30 ký tự theo schema) để tránh lỗi
    /// "Duplicate entry" khi thêm nhiều mục trong cùng một giây.
    /// </summary>
    private static async Task<string> SinhMaDanhMucAsync(MySqlConnection connection, string tenBang, string tenCot, string tienTo)
    {
        for (var i = 0; i < 20; i++)
        {
            var ma = $"{tienTo}{DateTime.Now:yyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(100, 1000)}";

            var soTrung = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM `{tenBang}` WHERE `{tenCot}` = @Ma;",
                new { Ma = ma });

            if (soTrung == 0)
                return ma;
        }

        // Cực hiếm: bị trùng liên tục -> sinh theo số thứ tự id lớn nhất.
        var maxId = await connection.ExecuteScalarAsync<uint?>(
            $"SELECT MAX(`id`) FROM `{tenBang}`;") ?? 0;

        return $"{tienTo}{(maxId + 1):D6}";
    }

    // ==========================================
    // ĐỌC DANH MỤC
    // ==========================================

    /// <summary>
    /// Lấy danh sách nhóm hàng đang hoạt động. Có thể truyền từ khóa để tìm kiếm
    /// theo tên hoặc mã nhóm.
    /// </summary>
    public async Task<IReadOnlyList<ProductOption>> GetNhomSanPhamAsync(string? tuKhoa = null)
    {
        await using var connection = await OpenAsync();
        await EnsureBangDanhMucAsync(connection);

        var sql = new StringBuilder()
            .AppendLine("SELECT id AS Id, ten_nhom AS Name")
            .AppendLine("FROM nhom_san_pham")
            .AppendLine("WHERE trang_thai = 1");

        var thamSo = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            sql.AppendLine("AND (ten_nhom LIKE @TuKhoa OR ma_nhom LIKE @TuKhoa)");
            thamSo["TuKhoa"] = $"%{tuKhoa.Trim()}%";
        }

        sql.AppendLine("ORDER BY ten_nhom;");

        return (await connection.QueryAsync<ProductOption>(sql.ToString(), thamSo)).AsList();
    }

    /// <summary>
    /// Lấy danh sách đơn vị tính đang hoạt động. Có thể truyền từ khóa để tìm
    /// kiếm theo tên hoặc mã đơn vị tính.
    /// </summary>
    public async Task<IReadOnlyList<ProductOption>> GetDonViTinhAsync(string? tuKhoa = null)
    {
        await using var connection = await OpenAsync();
        await EnsureBangDanhMucAsync(connection);

        var sql = new StringBuilder()
            .AppendLine("SELECT id AS Id, ten_don_vi AS Name")
            .AppendLine("FROM don_vi_tinh")
            .AppendLine("WHERE trang_thai = 1");

        var thamSo = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            sql.AppendLine("AND (ten_don_vi LIKE @TuKhoa OR ma_don_vi LIKE @TuKhoa)");
            thamSo["TuKhoa"] = $"%{tuKhoa.Trim()}%";
        }

        sql.AppendLine("ORDER BY ten_don_vi;");

        return (await connection.QueryAsync<ProductOption>(sql.ToString(), thamSo)).AsList();
    }

    // ==========================================
    // THÊM NHÓM HÀNG / ĐƠN VỊ TÍNH
    // ==========================================

    /// <summary>
    /// Thêm mới (hoặc kích hoạt lại nếu đã tồn tại) một nhóm hàng.
    /// <para>
    /// - Kiểm tra tên không được để trống / quá dài.
    /// - Kiểm tra trùng tên (không phân biệt hoa thường và bỏ khoảng trắng thừa).
    /// - Sinh mã nhóm duy nhất để không đụng ràng buộc UNIQUE.
    /// - Trả về đúng bản ghi vừa lưu để ứng dụng tải lại danh sách.
    /// </para>
    /// </summary>
    public async Task<ProductOption> ThemNhomSanPhamAsync(string tenNhom, string? maNhom = null)
    {
        var ten = (tenNhom ?? "").Trim();

        if (string.IsNullOrWhiteSpace(ten))
            throw new ArgumentException("Tên nhóm hàng không được để trống.", nameof(tenNhom));

        if (ten.Length > 150)
            throw new ArgumentException("Tên nhóm hàng không được vượt quá 150 ký tự.", nameof(tenNhom));

        await using var connection = await OpenAsync();
        await EnsureBangDanhMucAsync(connection);

        // Nhóm đã có (kể cả đã bị ẩn) -> bật lại thay vì tạo bản ghi trùng.
        var existing = await connection.QueryFirstOrDefaultAsync<ProductOption>(
            """
            SELECT id AS Id, ten_nhom AS Name
            FROM nhom_san_pham
            WHERE LOWER(TRIM(ten_nhom)) = LOWER(@Ten)
            ORDER BY id
            LIMIT 1;
            """,
            new { Ten = ten });

        if (existing != null)
        {
            await connection.ExecuteAsync(
                "UPDATE nhom_san_pham SET trang_thai = 1 WHERE id = @Id;",
                new { Id = existing.Id });

            return new ProductOption { Id = existing.Id, Name = existing.Name };
        }

        var ma = string.IsNullOrWhiteSpace(maNhom)
            ? await SinhMaDanhMucAsync(connection, "nhom_san_pham", "ma_nhom", "NH")
            : maNhom.Trim();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connection.ExecuteAsync(
                    "INSERT INTO nhom_san_pham (ma_nhom, ten_nhom, trang_thai) VALUES (@Ma, @Ten, 1);",
                    new { Ma = ma, Ten = ten });
            }
            catch (MySqlException ex) when (ex.Number == 1062 && attempt <= 5)
            {
                // Trùng mã (hiếm) -> sinh mã khác rồi thử lại.
                ma = await SinhMaDanhMucAsync(connection, "nhom_san_pham", "ma_nhom", "NH");
                continue;
            }

            var idMoi = await connection.ExecuteScalarAsync<uint>(
                "SELECT id FROM nhom_san_pham WHERE ma_nhom = @Ma LIMIT 1;",
                new { Ma = ma });

            // Chống tạo bản ghi trùng: nếu có tiến trình khác vừa thêm cùng tên
            // thì giữ lại bản ghi cũ (id nhỏ hơn) và xóa bản ghi vừa tạo.
            var idNhomCungTen = await connection.ExecuteScalarAsync<uint>(
                """
                SELECT id FROM nhom_san_pham
                WHERE LOWER(TRIM(ten_nhom)) = LOWER(@Ten)
                ORDER BY id
                LIMIT 1;
                """,
                new { Ten = ten });

            if (idNhomCungTen != 0 && idNhomCungTen != idMoi)
            {
                await connection.ExecuteAsync("DELETE FROM nhom_san_pham WHERE id = @Id;", new { Id = idMoi });
                await connection.ExecuteAsync(
                    "UPDATE nhom_san_pham SET trang_thai = 1 WHERE id = @Id;",
                    new { Id = idNhomCungTen });

                var tenDaCo = await connection.ExecuteScalarAsync<string>(
                    "SELECT ten_nhom FROM nhom_san_pham WHERE id = @Id LIMIT 1;",
                    new { Id = idNhomCungTen });

                return new ProductOption { Id = idNhomCungTen, Name = tenDaCo ?? ten };
            }

            return new ProductOption { Id = idMoi, Name = ten };
        }
    }

    /// <summary>
    /// Thêm mới (hoặc kích hoạt lại nếu đã tồn tại) một đơn vị tính.
    /// <para>
    /// - Kiểm tra tên không được để trống / quá dài.
    /// - Kiểm tra trùng tên (không phân biệt hoa thường và bỏ khoảng trắng thừa).
    /// - Sinh mã đơn vị tính duy nhất để không đụng ràng buộc UNIQUE.
    /// </para>
    /// </summary>
    public async Task<ProductOption> ThemDonViTinhAsync(string tenDonVi, string? maDonVi = null)
    {
        var ten = (tenDonVi ?? "").Trim();

        if (string.IsNullOrWhiteSpace(ten))
            throw new ArgumentException("Tên đơn vị tính không được để trống.", nameof(tenDonVi));

        if (ten.Length > 100)
            throw new ArgumentException("Tên đơn vị tính không được vượt quá 100 ký tự.", nameof(tenDonVi));

        await using var connection = await OpenAsync();
        await EnsureBangDanhMucAsync(connection);

        // Đơn vị tính đã có (kể cả đã bị ẩn) -> bật lại thay vì tạo bản ghi trùng.
        var existing = await connection.QueryFirstOrDefaultAsync<ProductOption>(
            """
            SELECT id AS Id, ten_don_vi AS Name
            FROM don_vi_tinh
            WHERE LOWER(TRIM(ten_don_vi)) = LOWER(@Ten)
            ORDER BY id
            LIMIT 1;
            """,
            new { Ten = ten });

        if (existing != null)
        {
            await connection.ExecuteAsync(
                "UPDATE don_vi_tinh SET trang_thai = 1 WHERE id = @Id;",
                new { Id = existing.Id });

            return new ProductOption { Id = existing.Id, Name = existing.Name };
        }

        var ma = string.IsNullOrWhiteSpace(maDonVi)
            ? await SinhMaDanhMucAsync(connection, "don_vi_tinh", "ma_don_vi", "DVT")
            : maDonVi.Trim();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connection.ExecuteAsync(
                    "INSERT INTO don_vi_tinh (ma_don_vi, ten_don_vi, trang_thai) VALUES (@Ma, @Ten, 1);",
                    new { Ma = ma, Ten = ten });
            }
            catch (MySqlException ex) when (ex.Number == 1062 && attempt <= 5)
            {
                // Trùng mã (hiếm) -> sinh mã khác rồi thử lại.
                ma = await SinhMaDanhMucAsync(connection, "don_vi_tinh", "ma_don_vi", "DVT");
                continue;
            }

            var idMoi = await connection.ExecuteScalarAsync<uint>(
                "SELECT id FROM don_vi_tinh WHERE ma_don_vi = @Ma LIMIT 1;",
                new { Ma = ma });

            // Chống tạo bản ghi trùng: nếu có tiến trình khác vừa thêm cùng tên
            // thì giữ lại bản ghi cũ (id nhỏ hơn) và xóa bản ghi vừa tạo.
            var idDvtCungTen = await connection.ExecuteScalarAsync<uint>(
                """
                SELECT id FROM don_vi_tinh
                WHERE LOWER(TRIM(ten_don_vi)) = LOWER(@Ten)
                ORDER BY id
                LIMIT 1;
                """,
                new { Ten = ten });

            if (idDvtCungTen != 0 && idDvtCungTen != idMoi)
            {
                await connection.ExecuteAsync("DELETE FROM don_vi_tinh WHERE id = @Id;", new { Id = idMoi });
                await connection.ExecuteAsync(
                    "UPDATE don_vi_tinh SET trang_thai = 1 WHERE id = @Id;",
                    new { Id = idDvtCungTen });

                var tenDaCo = await connection.ExecuteScalarAsync<string>(
                    "SELECT ten_don_vi FROM don_vi_tinh WHERE id = @Id LIMIT 1;",
                    new { Id = idDvtCungTen });

                return new ProductOption { Id = idDvtCungTen, Name = tenDaCo ?? ten };
            }

            return new ProductOption { Id = idMoi, Name = ten };
        }
    }

    // ==========================================
    // XÓA NHÓM HÀNG / ĐƠN VỊ TÍNH
    // ==========================================

    /// <summary>
    /// Ẩn nhóm hàng (soft delete) để không phá vỡ các hàng hóa đang tham chiếu.
    /// </summary>
    public async Task XoaNhomSanPhamAsync(uint id)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SettingsEdit, "Bạn không có quyền xóa nhóm hàng hóa.");

        await using var connection = await OpenAsync();
        await EnsureBangDanhMucAsync(connection);

        const string sql = "UPDATE nhom_san_pham SET trang_thai = 0 WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Ẩn đơn vị tính (soft delete) để không phá vỡ các hàng hóa đang tham chiếu.
    /// </summary>
    public async Task XoaDonViTinhAsync(uint id)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SettingsEdit, "Bạn không có quyền xóa đơn vị tính.");

        await using var connection = await OpenAsync();
        await EnsureBangDanhMucAsync(connection);

        const string sql = "UPDATE don_vi_tinh SET trang_thai = 0 WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}
