using System.Data;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private readonly DatabaseConfigService _configService;
    private bool? _hasPhieuNhapExtraColumns;
    private bool? _hasPhieuNhapPhuongThucColumn;
    private bool? _hasPhieuQuyExtraColumns;
    private bool? _hasPhieuQuyHoaDonColumns;
    private bool? _hasPhieuQuyPhieuNhapColumns;
    private bool? _hasBangTraHang;
    private bool? _coCotDuLieuKiotViet;
    private bool? _cotNgayTaoSanPhamChoPhepNull;
    private bool? _coCotDuLieuKiotVietNhaCungCap;
    private bool? _hasHoaDonContactColumns;
    private bool? _coCotSoDienThoaiNguoiDung;
    private bool? _coCotEmailNguoiDung;
    private bool? _coCotHangTangHoaDon;
    private bool? _hasBangLichSuNhapXuatKho;
    private bool _daBackfillNhomSoQuy;

    public DatabaseService()
    {
        _configService = new DatabaseConfigService();
    }

    /// <summary>
    /// Má»Ÿ káº¿t ná»‘i má»›i tá»›i database. CÃ´ng khai Ä‘á»ƒ cÃ¡c service khÃ¡c (vÃ­ dá»¥
    /// <see cref="DashboardService"/>) dÃ¹ng chung Ä‘Ãºng má»™t cÃ¡ch cáº¥u hÃ¬nh káº¿t ná»‘i
    /// thay vÃ¬ tá»± dá»±ng connection string riÃªng.
    /// </summary>
    public async Task<MySqlConnection> TaoKetNoiAsync()
    {
        var connection = new MySqlConnection(GetConnectionString());

        await connection.OpenAsync();

        return connection;
    }

    private string GetConnectionString()
    {
        return GetConnectionString(_configService.Load());
    }

    private static string GetConnectionString(DatabaseConfig config)
    {
        // SslMode Ä‘Æ°á»£c chuáº©n hÃ³a láº¡i má»™t láº§n ná»¯a á»Ÿ Ä‘Ã¢y Ä‘á»ƒ má»i Ä‘Æ°á»ng vÃ o (cáº¥u hÃ¬nh
        // Ä‘Ã£ lÆ°u, cáº¥u hÃ¬nh nháº­p trÃªn form) Ä‘á»u khÃ´ng thá»ƒ Ä‘áº©y giÃ¡ trá»‹ khÃ´ng há»£p lá»‡
        // xuá»‘ng MySqlConnector.
        var sslMode = DatabaseConfigService.ChuanHoaSslMode(config.SslMode);

        return
            $"Server={config.Server};" +
            $"Port={config.Port};" +
            $"Database={config.Database};" +
            $"User ID={config.User};" +
            $"Password={config.Password};" +
            $"SslMode={sslMode};" +
            $"Allow User Variables=True;";
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var connection = new MySqlConnection(GetConnectionString());

        await connection.OpenAsync();

        return connection;
    }

    public async Task<bool> TestConnectionAsync()
    {
        return await TestConnectionAsync(_configService.Load());
    }

    /// <summary>
    /// Kiá»ƒm tra káº¿t ná»‘i báº±ng Ä‘Ãºng cáº¥u hÃ¬nh Ä‘ang nháº­p trÃªn form (chÆ°a lÆ°u), nhá» Ä‘Ã³
    /// mÃ n "CÃ i Ä‘áº·t káº¿t ná»‘i Database" khÃ´ng pháº£i ghi cáº¥u hÃ¬nh xuá»‘ng Ä‘Ä©a trÆ°á»›c khi
    /// biáº¿t káº¿t ná»‘i cÃ³ thÃ nh cÃ´ng hay khÃ´ng.
    /// </summary>
    public async Task<bool> TestConnectionAsync(DatabaseConfig config)
    {
        try
        {
            await using var connection = new MySqlConnection(GetConnectionString(config));

            await connection.OpenAsync();

            await connection.ExecuteScalarAsync<int>("SELECT 1;");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== DATABASE ERROR ==========");
            Console.WriteLine(ex.GetType().Name + ": " + ex.Message);
            Console.WriteLine("====================================");

            throw;
        }
    }

    // Má»Ÿ sáºµn má»™t káº¿t ná»‘i Ä‘á»ƒ MySqlConnector Ä‘Æ°a vÃ o connection pool.
    // Láº§n Ä‘Äƒng nháº­p sau Ä‘Ã³ cÃ³ thá»ƒ dÃ¹ng láº¡i káº¿t ná»‘i nÃ y thay vÃ¬ báº¯t tay láº¡i vá»›i server.
    public async Task WarmUpAsync()
    {
        await using var connection = await OpenAsync();

        // Báº£o Ä‘áº£m schema danh má»¥c (nhÃ³m hÃ ng / Ä‘Æ¡n vá»‹ tÃ­nh) sáºµn sÃ ng ngay khi
        // kiá»ƒm tra káº¿t ná»‘i. Lá»—i á»Ÿ bÆ°á»›c nÃ y khÃ´ng Ä‘Æ°á»£c lÃ m há»ng viá»‡c Ä‘Äƒng nháº­p;
        // cÃ¡c mÃ n hÃ¬nh danh má»¥c sáº½ bÃ¡o lá»—i cá»¥ thá»ƒ náº¿u váº«n chÆ°a bá»• sung Ä‘Æ°á»£c.
        try
        {
            await EnsureBangDanhMucAsync(connection);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhÃ´ng chuáº©n bá»‹ Ä‘Æ°á»£c schema danh má»¥c nhÃ³m hÃ ng / Ä‘Æ¡n vá»‹ tÃ­nh", ex);
        }

        _ = EnsureAuditTableAsync();
    }

    public async Task<LoginResultExtended> DangNhapAsync(
        string tenDangNhap,
        string matKhau)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            nd.id AS Id,
            nd.ma_nguoi_dung AS MaNguoiDung,
            nd.ten_dang_nhap AS TenDangNhap,
            nd.mat_khau AS MatKhau,
            nd.ho_ten AS HoTen,
            nd.so_dien_thoai AS SoDienThoai,
            nd.email AS Email,
            nd.chi_nhanh_id AS ChiNhanhId,
            cn.ten_chi_nhanh AS TenChiNhanh,
            nd.trang_thai AS TrangThai,
            GROUP_CONCAT(
                vt.ten_vai_tro
                ORDER BY vt.id
                SEPARATOR ', '
            ) AS VaiTro,
            GROUP_CONCAT(
                vt.ma_vai_tro
                ORDER BY vt.id
                SEPARATOR ','
            ) AS MaVaiTro

        FROM nguoi_dung nd

        LEFT JOIN chi_nhanh cn
            ON cn.id = nd.chi_nhanh_id

        LEFT JOIN nguoi_dung_vai_tro ndvt
            ON ndvt.nguoi_dung_id = nd.id

        LEFT JOIN vai_tro vt
            ON vt.id = ndvt.vai_tro_id

        WHERE nd.ten_dang_nhap = @TenDangNhap

        GROUP BY
            nd.id,
            nd.ma_nguoi_dung,
            nd.ten_dang_nhap,
            nd.mat_khau,
            nd.ho_ten,
            nd.so_dien_thoai,
            nd.email,
            nd.chi_nhanh_id,
            nd.trang_thai,
            cn.ten_chi_nhanh

        LIMIT 1;
        """;

        dynamic? row = null;
        try
        {
            row = await connection.QuerySingleOrDefaultAsync<dynamic>(
                sql,
                new { TenDangNhap = tenDangNhap });
        }
        catch
        {
            // Fallback khi cháº¡y trÃªn schema shophoa_Huy_tiemhoatest (nguoi_dung cÃ³ cá»™t vai_tro trá»±c tiáº¿p, khÃ´ng cÃ³ báº£ng vai_tro)
            const string fallbackSql = """
            SELECT
                nd.id AS Id,
                CONCAT('ND', LPAD(nd.id, 4, '0')) AS MaNguoiDung,
                nd.ten_dang_nhap AS TenDangNhap,
                nd.mat_khau AS MatKhau,
                nd.ho_ten AS HoTen,
                '' AS SoDienThoai,
                '' AS Email,
                1 AS ChiNhanhId,
                COALESCE((SELECT ten_don_vi FROM don_vi LIMIT 1), 'Shop Hoa') AS TenChiNhanh,
                nd.trang_thai AS TrangThai,
                nd.vai_tro AS VaiTro,
                nd.vai_tro AS MaVaiTro
            FROM nguoi_dung nd
            WHERE nd.ten_dang_nhap = @TenDangNhap
            LIMIT 1;
            """;
            try
            {
                row = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    fallbackSql,
                    new { TenDangNhap = tenDangNhap });
            }
            catch { }
        }

        // Tá»± Ä‘á»™ng khá»Ÿi táº¡o tÃ i khoáº£n admin máº·c Ä‘á»‹nh náº¿u báº£ng nguoi_dung hoÃ n toÃ n trá»‘ng
        if (row is null)
        {
            try
            {
                var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM nguoi_dung;");
                if (userCount == 0)
                {
                    var defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");
                    bool hasVaiTroCol = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nguoi_dung' AND COLUMN_NAME = 'vai_tro';") > 0;
                    if (hasVaiTroCol)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO nguoi_dung (ten_dang_nhap, mat_khau, ho_ten, vai_tro, trang_thai) VALUES ('admin', @Pwd, 'Quáº£n trá»‹ viÃªn', 'ADMIN', 1);",
                            new { Pwd = defaultHash });
                    }
                    else
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO nguoi_dung (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten, trang_thai) VALUES ('ND0001', 'admin', @Pwd, 'Quáº£n trá»‹ viÃªn', 1);",
                            new { Pwd = defaultHash });
                    }

                    if (string.Equals(tenDangNhap, "admin", StringComparison.OrdinalIgnoreCase))
                    {
                        row = await connection.QuerySingleOrDefaultAsync<dynamic>(
                            "SELECT id AS Id, 'ND0001' AS MaNguoiDung, ten_dang_nhap AS TenDangNhap, mat_khau AS MatKhau, ho_ten AS HoTen, 'ADMIN' AS VaiTro, 'ADMIN' AS MaVaiTro, 1 AS TrangThai FROM nguoi_dung WHERE ten_dang_nhap = 'admin' LIMIT 1;");
                    }
                }
            }
            catch { }
        }

        if (row is null)
        {
            return new LoginResultExtended { IsValid = false, IsLocked = false };
        }

        var user = new NguoiDung
        {
            Id = (uint)row.Id,
            MaNguoiDung = row.MaNguoiDung ?? "",
            TenDangNhap = row.TenDangNhap ?? "",
            MatKhau = row.MatKhau ?? "",
            HoTen = row.HoTen ?? "",
            SoDienThoai = row.SoDienThoai,
            Email = row.Email,
            ChiNhanhId = row.ChiNhanhId != null ? (uint)row.ChiNhanhId : 1,
            TenChiNhanh = row.TenChiNhanh ?? "Shop Hoa",
            VaiTro = row.VaiTro ?? "ADMIN",
            MaVaiTro = row.MaVaiTro ?? "ADMIN"
        };

        bool trangThai = true;
        if (row.TrangThai != null)
        {
            var t = row.TrangThai;
            if (t is bool b) trangThai = b;
            else if (int.TryParse(t.ToString(), out int iv)) trangThai = iv == 1;
            else if (bool.TryParse(t.ToString(), out bool bv)) trangThai = bv;
            else trangThai = t.ToString() == "1";
        }
        if (!trangThai)
        {
            return new LoginResultExtended { IsValid = false, IsLocked = true, ErrorMessage = "TÃ i khoáº£n Ä‘Ã£ bá»‹ khÃ³a. Vui lÃ²ng liÃªn há»‡ quáº£n trá»‹ viÃªn." };
        }

        bool validPassword;
        var pwd = user.MatKhau ?? "";
        if (pwd.StartsWith("$2a$") || pwd.StartsWith("$2b$") || pwd.StartsWith("$2y$"))
        {
            validPassword = BCrypt.Net.BCrypt.Verify(matKhau, pwd);
        }
        else
        {
            validPassword = string.Equals(matKhau, pwd, StringComparison.Ordinal);
        }

        if (!validPassword)
        {
            return new LoginResultExtended { IsValid = false, IsLocked = false };
        }

        return new LoginResultExtended { IsValid = true, IsLocked = false, User = user };
    }

    public async Task CapNhatLanDangNhapCuoiAsync(uint userId)
    {
        try
        {
            await using var connection = await OpenAsync();

            bool hasLanDangNhap = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nguoi_dung' AND COLUMN_NAME = 'lan_dang_nhap_cuoi';") > 0;

            if (hasLanDangNhap)
            {
                await connection.ExecuteAsync(
                """
                UPDATE nguoi_dung
                SET
                    lan_dang_nhap_cuoi = NOW(),
                    ngay_cap_nhat = NOW()
                WHERE id = @Id;
                """,
                new
                {
                    Id = userId
                });
            }

            // Ghi nháº­t kÃ½ vÃ o nhat_ky_he_thong náº¿u cÃ³ (schema shophoa_Huy_tiemhoatest)
            try
            {
                bool hasNhatKyHeThong = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nhat_ky_he_thong';") > 0;
                if (hasNhatKyHeThong)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO nhat_ky_he_thong (nguoi_dung_id, hanh_dong, thoi_gian) VALUES (@Id, 'ÄÄƒng nháº­p há»‡ thá»‘ng', NOW());",
                        new { Id = userId });
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            // KhÃ´ng Ä‘á»ƒ lá»—i ghi nháº­t kÃ½ lÃ m áº£nh hÆ°á»Ÿng Ä‘áº¿n phiÃªn Ä‘Äƒng nháº­p há»£p lá»‡.
            Console.WriteLine($"KhÃ´ng thá»ƒ cáº­p nháº­t láº§n Ä‘Äƒng nháº­p cuá»‘i: {ex.Message}");
        }
    }

    /// <summary>
    /// Sá»‘ hÃ ng hÃ³a tá»‘i Ä‘a Ä‘Æ°á»£c Ä‘á»c trong má»™t láº§n táº£i danh sÃ¡ch.
    /// MÃ n hÃ¬nh "HÃ ng hÃ³a" hiá»‡n chÆ°a cÃ³ phÃ¢n trang nÃªn láº¥y Ä‘á»§ danh sÃ¡ch Ä‘á»ƒ ngÆ°á»i dÃ¹ng xem.
    /// </summary>
    public const int SoDongSanPhamToiDa = 1000;

    /// <summary>
    /// Láº¥y danh sÃ¡ch hÃ ng hÃ³a. Tá»« khÃ³a Ä‘Æ°á»£c MySQL Ä‘á»‘i chiáº¿u trá»±c tiáº¿p trÃªn TOÃ€N Bá»˜ báº£ng
    /// <c>san_pham</c>, chá»‰ sau Ä‘Ã³ má»›i Ã¡p dá»¥ng LIMIT, nÃªn sáº£n pháº©m náº±m ngoÃ i pháº§n Ä‘ang
    /// hiá»ƒn thá»‹ váº«n tÃ¬m ra Ä‘Æ°á»£c (khÃ´ng láº¥y sáºµn má»™t pháº§n rá»“i má»›i lá»c).
    /// </summary>
    public async Task<IReadOnlyList<SanPham>> GetSanPhamAsync(string? search = null)
        => (await GetSanPhamPagedAsync(search, 1, SoDongSanPhamToiDa)).Items;

    /// <summary>
    /// Láº¥y má»™t trang hÃ ng hÃ³a kÃ¨m tá»•ng sá»‘ báº£n ghi khá»›p tá»« khÃ³a.
    /// Tá»•ng sá»‘ báº£n ghi Ä‘Æ°á»£c Ä‘áº¿m trÃªn toÃ n database nÃªn khÃ´ng phá»¥ thuá»™c sá»‘ dÃ²ng cá»§a trang,
    /// dÃ¹ng cho phÃ¢n trang vÃ  cho thÃ´ng Ä‘iá»‡p "TÃ¬m tháº¥y N sáº£n pháº©m".
    /// </summary>
    public async Task<(IReadOnlyList<SanPham> Items, int TongSoBanGhi)> GetSanPhamPagedAsync(
        string? search,
        int page,
        int pageSize)
    {
        var keyword = (search ?? "").Trim();
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? SoDongSanPhamToiDa : pageSize;
        var offset = (safePage - 1) * safePageSize;

        await using var connection = await OpenAsync();

        // Tá»« khÃ³a Ä‘Æ°á»£c tÃ¡ch thÃ nh tá»«ng tá»«, má»—i tá»« pháº£i xuáº¥t hiá»‡n trong má»™t trong cÃ¡c
        // cá»™t tÃ¬m kiáº¿m (AND giá»¯a cÃ¡c tá»«). Nhá» váº­y gÃµ "Há»“ng mua bÃªn" váº«n ra sáº£n pháº©m
        // "há»“ng ( mua bÃªn VL )" dÃ¹ hai tá»« khÃ´ng ná»‘i liá»n nhau.
        // KhÃ´ng cÃ³ tá»« khÃ³a thÃ¬ khÃ´ng thÃªm Ä‘iá»u kiá»‡n nÃ o Ä‘á»ƒ MySQL Ä‘á»c báº£ng nhÆ° bÃ¬nh thÆ°á»ng.
        var parameters = new DynamicParameters();

        var dieuKien = TaoDieuKienTimKiemSanPham(
            parameters,
            keyword,
            "sp.ma_san_pham",
            "sp.ma_vach",
            "sp.ten_san_pham");

        var whereSql = dieuKien.Length == 0 ? "" : "WHERE " + dieuKien;

        // Khá»›p chÃ­nh xÃ¡c mÃ£ hÃ ng / mÃ£ váº¡ch thÃ¬ Ä‘Æ°a lÃªn Ä‘áº§u, tÃªn hÃ ng báº¯t Ä‘áº§u báº±ng
        // tá»« khÃ³a thÃ¬ káº¿ sau, cÃ²n láº¡i sáº¯p theo tÃªn hÃ ng. Äiá»u kiá»‡n nÃ y so sÃ¡nh trÃªn
        // biá»ƒu thá»©c Ä‘Ã£ chuáº©n hÃ³a y há»‡t Ä‘iá»u kiá»‡n lá»c á»Ÿ trÃªn.
        var orderSql = dieuKien.Length == 0
            ? "ORDER BY sp.ten_san_pham, sp.id"
            : TaoDieuKienSapXepTimKiemSanPham(
                keyword,
                "sp.ma_san_pham",
                "sp.ma_vach",
                "sp.ten_san_pham");

        parameters.Add("PageSize", safePageSize);
        parameters.Add("Offset", offset);

        var countSql = $"""
            SELECT COUNT(*)
            FROM san_pham sp
            {whereSql}
            """;

        var pageSql = $"""
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
                sp.nhom_san_pham_id AS NhomSanPhamId,
                sp.don_vi_tinh_id AS DonViTinhId,
                sp.thuong_hieu_id AS ThuongHieuId,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                COALESCE(tk.so_luong_ton, 0) AS TonKho,
                COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                COALESCE(nsp.ten_nhom, NULLIF(TRIM(sp.nhom_hang_3_cap_excel), '')) AS NhomSanPham,
                COALESCE(NULLIF(TRIM(sp.loai_hang_excel), ''), 'HÃ ng hÃ³a') AS LoaiHang,
                COALESCE(NULLIF(TRIM(sp.nhom_hang_3_cap_excel), ''), nsp.ten_nhom) AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id AND tk.chi_nhanh_id = @ChiNhanhId
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            {whereSql}
            {orderSql}
            LIMIT @PageSize OFFSET @Offset
            """;

        var tongSoBanGhi =
            await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var products = (
            await connection.QueryAsync<SanPham>(pageSql, parameters)
        ).AsList();

        if (products.Count > 0 || tongSoBanGhi > 0)
            return (products, tongSoBanGhi);

        // Chá»‰ dÃ¹ng báº£ng import cÅ© khi báº£ng san_pham khÃ´ng cÃ³ dá»¯ liá»‡u.
        var fromImport = await GetSanPhamTuBangImportAsync(
            connection,
            keyword,
            offset,
            safePageSize);

        return fromImport.Items.Count > 0
            ? fromImport
            : (products, 0);
    }

    /// <summary>
    /// Äá»c láº¡i Má»˜T hÃ ng hÃ³a kÃ¨m tÃªn ÄVT / nhÃ³m hÃ ng (qua JOIN) sau khi sá»­a, Ä‘á»ƒ
    /// mÃ n hÃ¬nh chá»‰ cáº­p nháº­t Ä‘Ãºng dÃ²ng vá»«a sá»­a mÃ  khÃ´ng pháº£i náº¡p láº¡i cáº£ danh sÃ¡ch.
    /// Tráº£ vá» null náº¿u khÃ´ng cÃ²n tÃ¬m tháº¥y sáº£n pháº©m.
    /// </summary>
    public async Task<SanPham?> GetSanPhamByIdAsync(uint id)
    {
        if (id == 0)
            return null;

        await using var connection = await OpenAsync();

        const string sql = """
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
                sp.nhom_san_pham_id AS NhomSanPhamId,
                sp.don_vi_tinh_id AS DonViTinhId,
                sp.thuong_hieu_id AS ThuongHieuId,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                COALESCE(tk.so_luong_ton, 0) AS TonKho,
                COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                COALESCE(nsp.ten_nhom, NULLIF(TRIM(sp.nhom_hang_3_cap_excel), '')) AS NhomSanPham,
                COALESCE(NULLIF(TRIM(sp.loai_hang_excel), ''), 'HÃ ng hÃ³a') AS LoaiHang,
                COALESCE(NULLIF(TRIM(sp.nhom_hang_3_cap_excel), ''), nsp.ten_nhom) AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id AND tk.chi_nhanh_id = @ChiNhanhId
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            WHERE sp.id = @Id
            LIMIT 1
            """;

        return await connection.QueryFirstOrDefaultAsync<SanPham>(sql, new { Id = id });
    }

    /// <summary>
    /// Tra cá»©u sáº£n pháº©m theo mÃ£ váº¡ch (hoáº·c mÃ£ hÃ ng khi mÃ£ váº¡ch trÃ¹ng rá»—ng).
    /// <para>
    /// Chá»‰ khá»›p CHÃNH XÃC (sau khi cáº¯t khoáº£ng tráº¯ng) chá»© khÃ´ng dÃ¹ng LIKE, vÃ¬
    /// mÃ£ váº¡ch lÃ  chuá»—i kÃ½ tá»± ngÆ°á»i bÃ¡n quÃ©t vÃ o: "12" khÃ´ng Ä‘Æ°á»£c khá»›p mÃ£
    /// "1234567890". Tráº£ vá» danh sÃ¡ch vÃ¬ má»™t mÃ£ váº¡ch cÃ³ thá»ƒ dÃ­nh nhiá»u quy cÃ¡ch
    /// (Ä‘Æ¡n vá»‹ tÃ­nh khÃ¡c nhau) â€” khi Ä‘Ã³ mÃ n bÃ¡n hÃ ng sáº½ yÃªu cáº§u ngÆ°á»i bÃ¡n chá»n.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<SanPham>> GetSanPhamTheoBarcodeAsync(string barcode)
    {
        var code = (barcode ?? "").Trim();

        if (code.Length == 0)
            return Array.Empty<SanPham>();

        await using var connection = await OpenAsync();

        const string sql = """
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
                sp.nhom_san_pham_id AS NhomSanPhamId,
                sp.don_vi_tinh_id AS DonViTinhId,
                sp.thuong_hieu_id AS ThuongHieuId,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                COALESCE(tk.so_luong_ton, 0) AS TonKho,
                COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                COALESCE(nsp.ten_nhom, NULLIF(TRIM(sp.nhom_hang_3_cap_excel), '')) AS NhomSanPham,
                COALESCE(NULLIF(TRIM(sp.loai_hang_excel), ''), 'HÃ ng hÃ³a') AS LoaiHang,
                COALESCE(NULLIF(TRIM(sp.nhom_hang_3_cap_excel), ''), nsp.ten_nhom) AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id AND tk.chi_nhanh_id = @ChiNhanhId
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            WHERE TRIM(COALESCE(sp.ma_vach, '')) = @Code
               OR TRIM(sp.ma_san_pham) = @Code
            ORDER BY sp.id;
            """;

        return (await connection.QueryAsync<SanPham>(sql, new { Code = code })).AsList();
    }

    /// <summary>
    /// Náº¡p nhiá»u sáº£n pháº©m theo danh sÃ¡ch id trong Má»˜T láº§n truy váº¥n (dÃ¹ng Ä‘á»ƒ
    /// láº¥y sáºµn sáº£n pháº©m táº·ng cá»§a cÃ¡c chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i).
    /// </summary>
    public async Task<IReadOnlyList<SanPham>> GetSanPhamTheoDanhSachIdAsync(IEnumerable<uint> ids)
    {
        var danhSachId = ids.Where(x => x > 0).Distinct().ToList();

        if (danhSachId.Count == 0)
            return Array.Empty<SanPham>();

        await using var connection = await OpenAsync();

        const string sql = """
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
                sp.nhom_san_pham_id AS NhomSanPhamId,
                sp.don_vi_tinh_id AS DonViTinhId,
                sp.thuong_hieu_id AS ThuongHieuId,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                COALESCE(tk.so_luong_ton, 0) AS TonKho,
                COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                COALESCE(nsp.ten_nhom, NULLIF(TRIM(sp.nhom_hang_3_cap_excel), '')) AS NhomSanPham,
                COALESCE(NULLIF(TRIM(sp.loai_hang_excel), ''), 'HÃ ng hÃ³a') AS LoaiHang,
                COALESCE(NULLIF(TRIM(sp.nhom_hang_3_cap_excel), ''), nsp.ten_nhom) AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id AND tk.chi_nhanh_id = @ChiNhanhId
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            WHERE sp.id IN @Ids;
            """;

        return (await connection.QueryAsync<SanPham>(sql, new { Ids = danhSachId })).AsList();
    }

    /// <summary>
    /// Lấy tất cả chương trình khuyến mãi đang có hiệu lực để màn bán hàng áp dụng.
    /// </summary>
    public async Task<IReadOnlyList<ChuongTrinhKhuyenMai>> GetChuongTrinhKhuyenMaiHieuLucAsync()
    {
        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);

        const string sql = """
        SELECT 
            km.id AS Id,
            km.ma_chuong_trinh AS MaChuongTrinh,
            km.ten_chuong_trinh AS TenChuongTrinh,
            km.ngay_bat_dau AS NgayBatDau,
            km.ngay_ket_thuc AS NgayKetThuc,
            km.trang_thai AS TrangThai,
            km.loai_khuyen_mai AS LoaiKhuyenMai,
            COALESCE(km.gia_tri_don_toi_thieu, 0) AS GiaTriDonToiThieu,
            COALESCE(km.loai_giam_gia, 'PHAN_TRAM') AS LoaiGiamGia,
            COALESCE(km.gia_tri_giam, 0) AS GiaTriGiam,
            km.ghi_chu AS GhiChu
        FROM chuong_trinh_khuyen_mai km
        WHERE km.trang_thai = 'DANG_HOAT_DONG'
          AND km.ngay_bat_dau <= NOW()
          AND km.ngay_ket_thuc >= CURDATE()
        ORDER BY km.id ASC;
        """;

        var list = (await connection.QueryAsync<ChuongTrinhKhuyenMai>(sql)).AsList();
        foreach (var item in list)
            item.LoaiKhuyenMai = ShopHoa.Models.KhuyenMaiLoai.ChuanHoa(item.LoaiKhuyenMai);
        return list;
    }

    /// <summary>
    /// Lấy các quy tắc "tặng kèm" đang có hiệu lực để màn bán hàng áp dụng tự động.
    /// </summary>
    public async Task<IReadOnlyList<KhuyenMaiTangKem>> GetQuyTacTangKemAsync()
    {
        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);

        const string sql = """
            SELECT
                km.id                    AS ChuongTrinhId,
                km.ma_chuong_trinh       AS MaChuongTrinh,
                km.ten_chuong_trinh      AS TenChuongTrinh,
                NULL                     AS NhomKhachHangId,
                ct.san_pham_id           AS SanPhamId,
                COALESCE(NULLIF(TRIM(ct.ten_san_pham), ''), ct.ma_san_pham, '') AS TenSanPhamMua,
                ct.don_vi_tinh           AS DonViTinh,
                ct.so_luong_mua          AS SoLuongMua,
                ct.san_pham_tang_id      AS SanPhamTangId,
                COALESCE(NULLIF(TRIM(ct.ten_san_pham_tang), ''), '')  AS TenSanPhamTang,
                ct.so_luong_tang         AS SoLuongTang
            FROM chuong_trinh_khuyen_mai km
            JOIN chi_tiet_khuyen_mai ct ON ct.chuong_trinh_id = km.id
            WHERE km.trang_thai = 'DANG_HOAT_DONG'
              AND km.ngay_bat_dau <= NOW()
              AND km.ngay_ket_thuc >= CURDATE()
              AND (km.loai_khuyen_mai IN ('BUY_X_GET_Y','TANG_KEM') OR ct.so_luong_tang > 0)
              AND ct.san_pham_tang_id IS NOT NULL
              AND ct.san_pham_tang_id > 0
              AND ct.so_luong_tang > 0
              AND ct.so_luong_mua > 0
            ORDER BY km.id, ct.id;
            """;

        var quyTac = (await connection.QueryAsync<KhuyenMaiTangKem>(sql)).AsList();

        if (quyTac.Count == 0)
            return quyTac;

        var sanPhamTang = await GetSanPhamTheoDanhSachIdAsync(quyTac.Select(q => q.SanPhamTangId));
        var dicSanPham = sanPhamTang.ToDictionary(p => p.Id);

        foreach (var quyTacItem in quyTac)
        {
            if (dicSanPham.TryGetValue(quyTacItem.SanPhamTangId, out var sanPham))
                quyTacItem.SanPhamTang = sanPham;
        }

        return quyTac;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng <c>chi_tiet_hoa_don_ban_hang</c> cÃ³ cÃ¡c cá»™t ghi nháº­n hÃ ng
    /// táº·ng kÃ¨m. Náº¿u database chÆ°a cÃ³ thÃ¬ tá»± Ä‘á»™ng ALTER TABLE (cháº¡y ngoÃ i
    /// transaction vÃ¬ MySQL khÃ´ng cho ALTER trong transaction).
    /// </summary>
    private async Task<bool> EnsureChiTietHoaDonHangTangColumnsAsync()
    {
        if (_coCotHangTangHoaDon.HasValue)
            return _coCotHangTangHoaDon.Value;

        await using var connection = await OpenAsync();

        try
        {
            var coCot = (await connection.QueryAsync<string>(
                """
                SELECT COLUMN_NAME FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
                  AND COLUMN_NAME IN (
                      'is_hang_tang', 'khuyen_mai_id', 'khuyen_mai_ten',
                      'san_pham_id_mua', 'so_luong_mua');
                """)).AsList();

            var daCo = coCot.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!daCo.Contains("is_hang_tang"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN is_hang_tang tinyint(1) NOT NULL DEFAULT 0 AFTER thanh_tien;");
            }

            if (!daCo.Contains("khuyen_mai_id"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN khuyen_mai_id int unsigned DEFAULT NULL AFTER is_hang_tang;");
            }

            if (!daCo.Contains("khuyen_mai_ten"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN khuyen_mai_ten varchar(150) DEFAULT NULL AFTER khuyen_mai_id;");
            }

            if (!daCo.Contains("san_pham_id_mua"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN san_pham_id_mua int unsigned DEFAULT NULL AFTER khuyen_mai_ten;");
            }

            if (!daCo.Contains("so_luong_mua"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN so_luong_mua decimal(15,3) DEFAULT NULL AFTER san_pham_id_mua;");
            }

            _coCotHangTangHoaDon = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Khong them duoc cot hang tang vao chi tiet hoa don ban hang", ex);
            _coCotHangTangHoaDon = false;
        }

        return _coCotHangTangHoaDon.Value;
    }

    /// <summary>
    /// Báº£ng lá»‹ch sá»­ nháº­p xuáº¥t kho lÃ  báº£ng báº¯t buá»™c cho luá»“ng bÃ¡n hÃ ng: má»—i
    /// láº§n xuáº¥t hÃ ng Ä‘á»u ghi 1 dÃ²ng váº¿t xuáº¥t. Nhiá»u database ERP cÅ© chÆ°a cÃ³
    /// báº£ng nÃ y nÃªn lá»‡nh INSERT sáº½ nÃ©m lá»—i "Table doesn't exist" vÃ  há»§y
    /// cáº£ giao dá»‹ch táº¡o hÃ³a Ä‘Æ¡n. HÃ m nÃ y tá»± táº¡o báº£ng náº¿u thiáº¿u (idempotent,
    /// cháº¡y ngoÃ i transaction vÃ¬ DDL tá»± commit trong MySQL/MariaDB).
    /// </summary>
    private async Task<bool> EnsureBangLichSuNhapXuatKhoAsync()
    {
        if (_hasBangLichSuNhapXuatKho.HasValue)
            return _hasBangLichSuNhapXuatKho.Value;

        await using var connection = await OpenAsync();

        try
        {
            var coBang = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'lich_su_nhap_xuat_kho';
                """) > 0;

            if (!coBang)
            {
                await connection.ExecuteAsync(
                    """
                    CREATE TABLE IF NOT EXISTS lich_su_nhap_xuat_kho (
                        id int unsigned NOT NULL AUTO_INCREMENT,
                        chi_nhanh_id int unsigned NOT NULL DEFAULT 1,
                        san_pham_id int unsigned NOT NULL,
                        loai_giao_dich varchar(30) NOT NULL,
                        loai_chung_tu varchar(30) DEFAULT NULL,
                        ma_chung_tu varchar(50) DEFAULT NULL,
                        so_luong decimal(15,3) NOT NULL DEFAULT 0.000,
                        ton_truoc decimal(15,3) NOT NULL DEFAULT 0.000,
                        ton_sau decimal(15,3) NOT NULL DEFAULT 0.000,
                        gia_von decimal(15,2) NOT NULL DEFAULT 0.00,
                        nguoi_thuc_hien_id int unsigned DEFAULT NULL,
                        thoi_gian datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ghi_chu text,
                        PRIMARY KEY (id),
                        KEY ix_lsnxk_sp (san_pham_id),
                        KEY ix_lsnxk_thoi_gian (thoi_gian)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    """);
            }

            _hasBangLichSuNhapXuatKho = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Khong tao duoc bang lich su nhap xuat kho", ex);
            _hasBangLichSuNhapXuatKho = false;
        }

        return _hasBangLichSuNhapXuatKho.Value;
    }

    /// <summary>
    /// CÃ¡c cáº·p kÃ½ tá»± tiáº¿ng Viá»‡t cáº§n bá» dáº¥u khi so sÃ¡nh tÃ¬m kiáº¿m.
    /// Äáº·t "Ä‘"/"Ä" lÃªn Ä‘áº§u vÃ¬ Ä‘Ã¢y lÃ  kÃ½ tá»± duy nháº¥t mÃ  chÃ­nh collation
    /// utf8mb4_unicode_ci cá»§a MySQL/MariaDB KHÃ”NG coi lÃ  báº±ng "d"/"D"
    /// (Ä‘Ã£ kiá»ƒm tra trÃªn MariaDB 10.1: <c>'Ä‘' = 'd'</c> tráº£ vá» FALSE).
    /// </summary>
    private static readonly (string CoDau, string KhongDau)[] BangChuanHoaTiengViet =
    [
        ("Ä‘", "d"), ("Ä", "D"),
        ("Ã¡", "a"), ("Ã ", "a"), ("áº£", "a"), ("Ã£", "a"), ("áº¡", "a"),
        ("Ã¢", "a"), ("áº¥", "a"), ("áº§", "a"), ("áº©", "a"), ("áº«", "a"), ("áº­", "a"),
        ("Äƒ", "a"), ("áº¯", "a"), ("áº±", "a"), ("áº³", "a"), ("áºµ", "a"), ("áº·", "a"),
        ("Ã©", "e"), ("Ã¨", "e"), ("áº»", "e"), ("áº½", "e"), ("áº¹", "e"),
        ("Ãª", "e"), ("áº¿", "e"), ("á»", "e"), ("á»ƒ", "e"), ("á»…", "e"), ("á»‡", "e"),
        ("Ã­", "i"), ("Ã¬", "i"), ("á»‰", "i"), ("Ä©", "i"), ("á»‹", "i"),
        ("Ã³", "o"), ("Ã²", "o"), ("á»", "o"), ("Ãµ", "o"), ("á»", "o"),
        ("Ã´", "o"), ("á»‘", "o"), ("á»“", "o"), ("á»•", "o"), ("á»—", "o"), ("á»™", "o"),
        ("Æ¡", "o"), ("á»›", "o"), ("á»", "o"), ("á»Ÿ", "o"), ("á»¡", "o"), ("á»£", "o"),
        ("Ãº", "u"), ("Ã¹", "u"), ("á»§", "u"), ("Å©", "u"), ("á»¥", "u"),
        ("Æ°", "u"), ("á»©", "u"), ("á»«", "u"), ("á»­", "u"), ("á»¯", "u"), ("á»±", "u"),
        ("Ã½", "y"), ("á»³", "y"), ("á»·", "y"), ("á»¹", "y"), ("á»µ", "y")
    ];

    /// <summary>
    /// Biá»ƒu thá»©c SQL chuáº©n hÃ³a má»™t cá»™t vá» dáº¡ng "chá»¯ thÆ°á»ng khÃ´ng dáº¥u" Ä‘á»ƒ so sÃ¡nh.
    /// <para>
    /// MySQL/MariaDB khÃ´ng cÃ³ hÃ m bá» dáº¥u sáºµn cÃ³, vÃ  collation utf8mb4_unicode_ci dÃ¹
    /// bá» Ä‘Æ°á»£c háº§u háº¿t dáº¥u thÃ¬ váº«n KHÃ”NG bá» Ä‘Æ°á»£c "Ä‘" â€” nÃªn gÃµ "cuc mau don trang"
    /// sáº½ khÃ´ng tÃ¬m ra "CÃºc máº«u Ä‘Æ¡n tráº¯ng". Chuá»—i REPLACE á»Ÿ Ä‘Ã¢y bá» dáº¥u cáº£ "Ä‘"
    /// láº«n cÃ¡c dáº¥u cÃ²n láº¡i, Ä‘á»ƒ viá»‡c tÃ¬m kiáº¿m khÃ´ng phá»¥ thuá»™c collation cá»§a server.
    /// </para>
    /// <para>
    /// Cá»™t rá»—ng/NULL tráº£ vá» chuá»—i rá»—ng nÃªn khÃ´ng bao giá» loáº¡i máº¥t sáº£n pháº©m.
    /// Biá»ƒu thá»©c Ä‘Æ°á»£c dá»±ng sáºµn má»™t láº§n cho má»—i tÃªn cá»™t vÃ¬ nÃ³ lÃ  háº±ng sá»‘.
    /// </para>
    /// </summary>
    private static string BieuThucChuanHoaSql(string cot)
        => "COALESCE(" + ChuoiReplaceBoDau($"LOWER({cot})") + ", '')";

    /// <summary>Quy Ä‘á»•i kÃ½ tá»± cÃ³ dáº¥u thÃ nh kÃ½ tá»± gá»‘c, bá»c trong COALESCE Ä‘á»ƒ NULL thÃ nh chuá»—i rá»—ng.</summary>
    private static string ChuoiReplaceBoDau(string bieuThuc)
    {
        foreach (var (coDau, khongDau) in BangChuanHoaTiengViet)
            bieuThuc = $"REPLACE({bieuThuc}, '{coDau}', '{khongDau}')";

        return bieuThuc;
    }

    /// <summary>
    /// Chuáº©n hÃ³a tá»« khÃ³a á»Ÿ C# theo ÄÃšNG quy táº¯c mÃ  biá»ƒu thá»©c SQL Ã¡p dá»¥ng cho cá»™t:
    /// háº¡ chá»¯ thÆ°á»ng, bá» dáº¥u tiáº¿ng Viá»‡t (ká»ƒ cáº£ Ä‘/Ä), gá»™p má»i kiá»ƒu khoáº£ng tráº¯ng
    /// (space, tab, xuá»‘ng dÃ²ng, khoáº£ng tráº¯ng khÃ´ng ngáº¯t dÃ²ng NBSP, ...) thÃ nh má»™t
    /// dáº¥u cÃ¡ch rá»“i cáº¯t bá» khoáº£ng tráº¯ng Ä‘áº§u/cuá»‘i.
    /// <para>
    /// Hai váº¿ dÃ¹ng chung má»™t quy táº¯c nÃªn káº¿t quáº£ khÃ´ng cÃ²n phá»¥ thuá»™c collation,
    /// ngÆ°á»i dÃ¹ng gÃµ "CÃºc máº«u Ä‘Æ¡n tráº¯ng hÃ ng A" hay "cuc mau don trang hang a"
    /// Ä‘á»u ra cÃ¹ng má»™t danh sÃ¡ch.
    /// </para>
    /// </summary>
    private static string ChuauHoaTuKhoa(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return string.Empty;

        // FormD tÃ¡ch chá»¯ cÃ³ dáº¥u thÃ nh chá»¯ gá»‘c + dáº¥u phá»¥, bá» dáº¥u phá»¥ Ä‘i lÃ  xong.
        var khongDau = keyword.Trim().Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(khongDau.Length);

        foreach (var character in khongDau)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(character))
                builder.Append(' ');
            else if (character == (char)273) builder.Append('d'); else if (character == (char)272) builder.Append('D');
            else
                builder.Append(char.ToLowerInvariant(character));
        }

        return string.Join(' ', builder.ToString().Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// TÃ¡ch tá»« khÃ³a Ä‘Ã£ chuáº©n hÃ³a thÃ nh cÃ¡c tá»« cáº§n cÃ³ trong sáº£n pháº©m.
    /// Má»i kÃ½ tá»± khÃ´ng pháº£i chá»¯/sá»‘ (dáº¥u pháº©y, gáº¡ch ná»‘i, ngoáº·c, dáº¥u cháº¥m cÃ¢u)
    /// Ä‘á»u bá»‹ coi lÃ  ranh giá»›i Ä‘á»ƒ "CÃºc máº«u, Ä‘Æ¡n tráº¯ng" váº«n ra káº¿t quáº£ Ä‘Ãºng.
    /// </summary>
    private static string[] TachTuKhoa(string? keyword)
    {
        var chuanHoa = ChuauHoaTuKhoa(keyword);

        if (chuanHoa.Length == 0)
            return [];

        var tokens = new List<string>();
        var builder = new StringBuilder();

        foreach (var character in chuanHoa)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
            tokens.Add(builder.ToString());

        return [.. tokens.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Dá»±ng Ä‘iá»u kiá»‡n tÃ¬m kiáº¿m hÃ ng hÃ³a vÃ  gáº¯n cÃ¡c tham sá»‘ vÃ o
    /// <paramref name="parameters"/>.
    /// Tá»« khÃ³a Ä‘Æ°á»£c tÃ¡ch thÃ nh tá»«ng tá»«, má»—i tá»« pháº£i xuáº¥t hiá»‡n trong má»™t trong cÃ¡c
    /// cá»™t Ä‘Æ°á»£c tÃ¬m, cÃ¡c tá»« ná»‘i vá»›i nhau báº±ng AND (giá»¯ Ä‘Æ°á»£c Ã½ cÅ©: gÃµ "Há»“ng mua bÃªn"
    /// váº«n ra sáº£n pháº©m "há»“ng ( mua bÃªn VL )" dÃ¹ hai tá»« khÃ´ng ná»‘i liá»n nhau).
    /// <para>
    /// Cáº£ tá»« khÃ³a láº«n cá»™t Ä‘á»u Ä‘Æ°á»£c chuáº©n hÃ³a vá» chá»¯ thÆ°á»ng khÃ´ng dáº¥u trÆ°á»›c khi
    /// so sÃ¡nh, nÃªn khÃ´ng phÃ¢n biá»‡t hoa/thÆ°á»ng, khÃ´ng phÃ¢n biá»‡t dáº¥u tiáº¿ng Viá»‡t
    /// (ká»ƒ cáº£ Ä‘/Ä) vÃ  khÃ´ng phá»¥ thuá»™c collation cá»§a server.
    /// </para>
    /// <para>
    /// MÃ£ váº¡ch vÃ  cÃ¡c cá»™t khÃ¡c cÃ³ thá»ƒ NULL váº«n Ä‘Æ°á»£c giá»¯ láº¡i: COALESCE Ä‘Æ°a NULL
    /// thÃ nh chuá»—i rá»—ng nÃªn sáº£n pháº©m thiáº¿u mÃ£ váº¡ch khÃ´ng bá»‹ loáº¡i vÃ¬ lÃ½ do Ä‘Ã³.
    /// </para>
    /// Tráº£ vá» chuá»—i rá»—ng khi khÃ´ng cÃ³ tá»« khÃ³a Ä‘á»ƒ ngÆ°á»i gá»i tá»± ghÃ©p vÃ o WHERE.
    /// </summary>
    private static string TaoDieuKienTimKiemSanPham(
        DynamicParameters parameters,
        string keyword,
        string cotMaHang,
        string cotMaVach,
        string cotTenHang)
    {
        parameters.Add("Search", ChuauHoaTuKhoa(keyword));

        var tokens = TachTuKhoa(keyword);

        if (tokens.Length == 0)
            return "";

        var maHang = BieuThucChuanHoaSql(cotMaHang);
        var maVach = BieuThucChuanHoaSql(cotMaVach);
        var tenHang = BieuThucChuanHoaSql(cotTenHang);

        var dieuKien = new List<string>(tokens.Length);

        for (var i = 0; i < tokens.Length; i++)
        {
            parameters.Add($"S{i}", tokens[i]);

            dieuKien.Add($"({maHang} LIKE CONCAT('%', @S{i}, '%')"
                         + $" OR {maVach} LIKE CONCAT('%', @S{i}, '%')"
                         + $" OR {tenHang} LIKE CONCAT('%', @S{i}, '%'))");
        }

        return string.Join(" AND ", dieuKien);
    }

    /// <summary>
    /// Äiá»u kiá»‡n sáº¯p xáº¿p khi Ä‘ang tÃ¬m kiáº¿m: mÃ£ hÃ ng/mÃ£ váº¡ch khá»›p chÃ­nh xÃ¡c Ä‘Æ°a lÃªn
    /// Ä‘áº§u, tÃªn hÃ ng báº¯t Ä‘áº§u báº±ng tá»« khÃ³a káº¿ sau, cÃ²n láº¡i sáº¯p theo tÃªn hÃ ng.
    /// So sÃ¡nh cÅ©ng dÃ¹ng biá»ƒu thá»©c chuáº©n hÃ³a Ä‘á»ƒ khÃ´ng lá»‡ch vá»›i Ä‘iá»u kiá»‡n lá»c.
    /// </summary>
    private static string TaoDieuKienSapXepTimKiemSanPham(
        string keyword,
        string cotMaHang,
        string cotMaVach,
        string cotTenHang)
    {
        var tokens = TachTuKhoa(keyword);

        return $"""
            ORDER BY
                CASE
                    WHEN {BieuThucChuanHoaSql(cotMaHang)} = @Search THEN 0
                    WHEN {BieuThucChuanHoaSql(cotMaVach)} = @Search THEN 1
                    WHEN {BieuThucChuanHoaSql(cotTenHang)} LIKE CONCAT(@S0, '%') THEN 2
                    ELSE 3
                END,
                sp.ten_san_pham,
                sp.id
            """;
    }

    /// <summary>
    /// Báº£ng import Excel cÅ©: chá»‰ Ä‘á»c khi báº£ng san_pham chÆ°a cÃ³ dá»¯ liá»‡u.
    /// DÃ¹ng chung cÃ¡ch tÃ¬m kiáº¿m vá»›i báº£ng san_pham Ä‘á»ƒ hÃ nh vi nháº¥t quÃ¡n.
    /// </summary>
    private static async Task<(IReadOnlyList<SanPham> Items, int TongSoBanGhi)> GetSanPhamTuBangImportAsync(
        MySqlConnection connection,
        string keyword,
        int offset,
        int pageSize)
    {
        var parameters = new DynamicParameters();

        var dieuKien = TaoDieuKienTimKiemSanPham(
            parameters,
            keyword,
            "`MÃ£ hÃ ng`",
            "`MÃ£ váº¡ch`",
            "`TÃªn hÃ ng`");

        var whereSql = "WHERE COALESCE(`Äang kinh doanh`, 1) = 1"
                       + (dieuKien.Length == 0 ? "" : " AND " + dieuKien);

        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var countSql = $"""
            SELECT COUNT(*)
            FROM import_san_pham_excel
            {whereSql}
            """;

        var pageSql = $"""
            SELECT
                `MÃ£ hÃ ng` AS MaSanPham,
                `MÃ£ váº¡ch` AS MaVach,
                COALESCE(`TÃªn hÃ ng`, `MÃ£ hÃ ng`) AS TenSanPham,
                COALESCE(`GiÃ¡ bÃ¡n`, 0) AS GiaBan,
                COALESCE(`GiÃ¡ vá»‘n`, 0) AS GiaVon,
                COALESCE(`Tá»“n kho`, 0) AS TonKho,
                COALESCE(`Tá»“n kho`, 0) AS TonKhoExcel,
                COALESCE(`Tá»“n nhá» nháº¥t`, 0) AS TonToiThieu,
                COALESCE(`Tá»“n lá»›n nháº¥t`, 0) AS TonToiDa,
                COALESCE(`KH Ä‘áº·t`, 0) AS KhachDat,
                `Thá»i gian táº¡o` AS NgayTao,
                `Dá»± kiáº¿n háº¿t hÃ ng` AS DuKienHetHang,
                `ÄVT` AS DonViTinh,
                `NhÃ³m hÃ ng(3 Cáº¥p)` AS NhomSanPham,
                `Loáº¡i hÃ ng` AS LoaiHang,
                `NhÃ³m hÃ ng(3 Cáº¥p)` AS NhomHangBaCap,
                `Vá»‹ trÃ­` AS ViTriKho,
                COALESCE(`Äang kinh doanh`, 1) AS DangKinhDoanh,
                COALESCE(`ÄÆ°á»£c bÃ¡n trá»±c tiáº¿p`, 1) AS DuocBanTrucTiep
            FROM import_san_pham_excel
            {whereSql}
            ORDER BY `TÃªn hÃ ng`
            LIMIT @PageSize OFFSET @Offset
            """;

        try
        {
            var tongSoBanGhi =
                await connection.ExecuteScalarAsync<int>(countSql, parameters);

            var items = (
                await connection.QueryAsync<SanPham>(pageSql, parameters)
            ).AsList();

            return (items, tongSoBanGhi);
        }
        catch (MySqlException ex) when (ex.Number == 1146)
        {
            // Báº£ng import cÅ© khÃ´ng tá»“n táº¡i -> coi nhÆ° khÃ´ng cÃ³ dá»¯ liá»‡u.
            return (Array.Empty<SanPham>(), 0);
        }
    }

    // LÆ°u Ã½: cÃ¡c hÃ m Ä‘á»c/thÃªm/xÃ³a NhÃ³m hÃ ng & ÄÆ¡n vá»‹ tÃ­nh náº±m á»Ÿ file
    // Services/DatabaseService.DanhMuc.cs (partial class DatabaseService).

    public async Task CapNhatHinhAnhSanPhamAsync(uint productId, string imagePath)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsEdit, "Báº¡n khÃ´ng cÃ³ quyá»n cáº­p nháº­t hÃ ng hÃ³a.");
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            """
            UPDATE san_pham
            SET hinh_anh = @ImagePath,
                ngay_cap_nhat = NOW()
            WHERE id = @ProductId;
            """,
            new
            {
                ProductId = productId,
                ImagePath = imagePath
            });
    }

    public async Task<uint> ThemSanPhamAsync(SanPham product)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm hÃ ng hÃ³a.");
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(product.LoaiHang))
        {
            product.LoaiHang = "HÃ ng hÃ³a";
        }

        if (string.IsNullOrWhiteSpace(product.NhomHangBaCap))
        {
            if (!string.IsNullOrWhiteSpace(product.NhomSanPham))
            {
                product.NhomHangBaCap = product.NhomSanPham;
            }
            else if (product.NhomSanPhamId > 0)
            {
                var tenNhom = await connection.ExecuteScalarAsync<string>(
                    "SELECT ten_nhom FROM nhom_san_pham WHERE id = @Id LIMIT 1;",
                    new { Id = product.NhomSanPhamId });
                if (!string.IsNullOrWhiteSpace(tenNhom))
                {
                    product.NhomSanPham = tenNhom;
                    product.NhomHangBaCap = tenNhom;
                }
            }
        }

        const string sql = """
            INSERT INTO san_pham
            (
                ma_san_pham,
                ma_vach,
                ten_san_pham,
                nhom_san_pham_id,
                don_vi_tinh_id,
                thuong_hieu_id,
                gia_ban,
                gia_von,
                ton_kho_excel,
                ton_toi_thieu,
                ton_toi_da,
                khach_dat,
                du_kien_het_hang,
                loai_hang_excel,
                nhom_hang_3_cap_excel,
                vi_tri_kho,
                mo_ta,
                ghi_chu,
                co_ban_truc_tiep,
                trang_thai
            )
            VALUES
            (
                @MaSanPham,
                @MaVach,
                @TenSanPham,
                @NhomSanPhamId,
                @DonViTinhId,
                @ThuongHieuId,
                @GiaBan,
                @GiaVon,
                @TonKhoExcel,
                @TonToiThieu,
                @TonToiDa,
                @KhachDat,
                @DuKienHetHang,
                @LoaiHang,
                @NhomHangBaCap,
                @ViTriKho,
                @MoTa,
                @GhiChu,
                @DuocBanTrucTiep,
                @DangKinhDoanh
            );
            SELECT LAST_INSERT_ID();
            """;

        var newId = await connection.ExecuteScalarAsync<uint>(sql, product);
        _ = LogSubAccountActivityAsync("ThÃªm hÃ ng hÃ³a", "SAN_PHAM", newId, product.MaSanPham, $"ThÃªm sáº£n pháº©m má»›i: {product.TenSanPham} ({product.MaSanPham})", product.GiaBan);
        return newId;
    }

    public async Task CapNhatSanPhamAsync(SanPham product)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a hÃ ng hÃ³a.");
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(product.LoaiHang))
        {
            product.LoaiHang = "HÃ ng hÃ³a";
        }

        if (string.IsNullOrWhiteSpace(product.NhomHangBaCap))
        {
            if (!string.IsNullOrWhiteSpace(product.NhomSanPham))
            {
                product.NhomHangBaCap = product.NhomSanPham;
            }
            else if (product.NhomSanPhamId > 0)
            {
                var tenNhom = await connection.ExecuteScalarAsync<string>(
                    "SELECT ten_nhom FROM nhom_san_pham WHERE id = @Id LIMIT 1;",
                    new { Id = product.NhomSanPhamId });
                if (!string.IsNullOrWhiteSpace(tenNhom))
                {
                    product.NhomSanPham = tenNhom;
                    product.NhomHangBaCap = tenNhom;
                }
            }
        }

        // TrÆ°á»ng nÃ o ngÆ°á»i dÃ¹ng thiáº¿u quyá»n chi tiáº¿t tÆ°Æ¡ng á»©ng thÃ¬ bá» háº³n khá»i
        // cÃ¢u UPDATE (giÃ¡ bÃ¡n -> sua_gia_ban, giÃ¡ vá»‘n -> sua_gia_von, tá»“n kho ->
        // dieu_chinh_ton_kho). Nhá» váº­y giÃ¡ trá»‹ Ä‘ang cÃ³ trong database Ä‘Æ°á»£c giá»¯
        // nguyÃªn, ká»ƒ cáº£ khi hÃ m nÃ y bá»‹ gá»i tháº³ng thay vÃ¬ qua giao diá»‡n Ä‘Ã£ khÃ³a Ã´.
        var danhSachCot = new List<string>
        {
            "ma_san_pham = @MaSanPham",
            "ma_vach = @MaVach",
            "ten_san_pham = @TenSanPham",
            "nhom_san_pham_id = @NhomSanPhamId",
            "don_vi_tinh_id = @DonViTinhId",
            "thuong_hieu_id = @ThuongHieuId",
            "ton_toi_thieu = @TonToiThieu",
            "ton_toi_da = @TonToiDa",
            "khach_dat = @KhachDat",
            "du_kien_het_hang = @DuKienHetHang",
            "loai_hang_excel = @LoaiHang",
            "nhom_hang_3_cap_excel = @NhomHangBaCap",
            "vi_tri_kho = @ViTriKho",
            "mo_ta = @MoTa",
            "ghi_chu = @GhiChu",
            "co_ban_truc_tiep = @DuocBanTrucTiep",
            "trang_thai = @DangKinhDoanh",
            "ngay_cap_nhat = NOW()"
        };

        if (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsUpdatePrice))
            danhSachCot.Add("gia_ban = @GiaBan");

        if (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsUpdateCost))
            danhSachCot.Add("gia_von = @GiaVon");

        if (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsAdjustStock))
            danhSachCot.Add("ton_kho_excel = @TonKhoExcel");

        var sql = $"UPDATE san_pham SET {string.Join(", ", danhSachCot)} WHERE id = @Id;";

        await connection.ExecuteAsync(sql, product);

        _ = LogSubAccountActivityAsync("Cáº­p nháº­t hÃ ng hÃ³a", "SAN_PHAM", product.Id, product.MaSanPham, $"Cáº­p nháº­t sáº£n pháº©m: {product.TenSanPham} ({product.MaSanPham})", product.GiaBan);
    }
    /// <summary>
    /// Chá»‰ cáº­p nháº­t Ä‘Ãºng cá»™t <c>san_pham.gia_ban</c> cá»§a má»™t sáº£n pháº©m.
    /// KhÃ´ng Ä‘á»¥ng tá»›i mÃ£ hÃ ng, tá»“n kho, giÃ¡ vá»‘n hay cÃ¡c cá»™t khÃ¡c.
    /// GiÃ¡ bÃ¡n báº±ng 0 váº«n há»£p lá»‡ (sáº£n pháº©m chÆ°a Ä‘á»‹nh giÃ¡).
    /// <para>
    /// Tráº£ vá» false khi sáº£n pháº©m khÃ´ng cÃ²n tá»“n táº¡i (bá»‹ ngÆ°á»i khÃ¡c xÃ³a)
    /// Ä‘á»ƒ lá»›p giao diá»‡n bÃ¡o lá»—i vÃ  táº£i láº¡i danh sÃ¡ch thay vÃ¬ crash.
    /// </para>
    /// </summary>
    public async Task<bool> CapNhatGiaBanSanPhamAsync(
        uint productId,
        decimal giaBan)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsUpdatePrice, "Báº¡n khÃ´ng cÃ³ quyá»n cáº­p nháº­t giÃ¡ bÃ¡n hÃ ng hÃ³a.");
        if (productId == 0)
            throw new ArgumentException(
                "Sáº£n pháº©m chÆ°a cÃ³ mÃ£ dá»¯ liá»‡u Ä‘á»ƒ cáº­p nháº­t giÃ¡ bÃ¡n.",
                nameof(productId));

        if (giaBan < 0)
            throw new ArgumentException(
                "GiÃ¡ bÃ¡n khÃ´ng Ä‘Æ°á»£c Ã¢m.",
                nameof(giaBan));

        if (giaBan > Helpers.MoneyInput.GiaTienToiDa)
            throw new ArgumentOutOfRangeException(
                nameof(giaBan),
                "GiÃ¡ bÃ¡n vÆ°á»£t quÃ¡ giá»›i háº¡n cá»§a cá»™t DECIMAL(15,2).");

        await using var connection = await OpenAsync();

        // Parameterized query: giÃ¡ bÃ¡n do ngÆ°á»i dÃ¹ng nháº­p khÃ´ng bao giá» ná»‘i vÃ o SQL.
        const string sql = """
        UPDATE san_pham
        SET gia_ban = @GiaBan,
            ngay_cap_nhat = NOW()
        WHERE id = @ProductId;
        """;

        var affectedRows = await connection.ExecuteAsync(
            sql,
            new
            {
                ProductId = productId,
                GiaBan = Math.Round(giaBan, 2, MidpointRounding.AwayFromZero)
            });

        // MySQL tráº£ 0 dÃ²ng bá»‹ áº£nh hÆ°á»Ÿng khi sáº£n pháº©m khÃ´ng tá»“n táº¡i.
        return affectedRows > 0;
    }

    /// <summary>
    /// Cáº­p nháº­t nhanh sá»‘ lÆ°á»£ng tá»“n kho cá»§a má»™t sáº£n pháº©m (dÃ¹ng trong mÃ n hÃ¬nh bÃ¡n hÃ ng
    /// khi sáº£n pháº©m háº¿t hÃ ng vÃ  ngÆ°á»i dÃ¹ng muá»‘n bá»• sung tá»“n Ä‘á»ƒ bÃ¡n ngay).
    /// Cáº­p nháº­t báº£ng ton_kho, san_pham.ton_kho_excel vÃ  ghi nháº­t kÃ½ lá»‹ch sá»­ kho.
    /// </summary>
    public async Task<bool> CapNhatTonKhoSanPhamAsync(
        uint productId,
        decimal soLuongTonMoi,
        uint? chiNhanhId = null)
    {
        if (productId == 0)
            throw new ArgumentException(
                "Sáº£n pháº©m chÆ°a cÃ³ mÃ£ dá»¯ liá»‡u Ä‘á»ƒ cáº­p nháº­t tá»“n kho.",
                nameof(productId));

        if (soLuongTonMoi < 0)
            throw new ArgumentException(
                "Sá»‘ lÆ°á»£ng tá»“n kho khÃ´ng Ä‘Æ°á»£c Ã¢m.",
                nameof(soLuongTonMoi));

        var branchId = chiNhanhId ?? Session.CurrentUser?.ChiNhanhId ?? 1;
        var userId = Session.CurrentUser?.Id;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string selectTonSql = """
            SELECT so_luong_ton, gia_von_binh_quan
            FROM ton_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId
            FOR UPDATE;
            """;

            var banGhi = (await connection.QueryAsync(
                selectTonSql,
                new
                {
                    ChiNhanhId = branchId,
                    SanPhamId = productId
                },
                transaction)).ToList();

            decimal tonTruoc = 0;
            decimal giaVon = 0;

            if (banGhi.FirstOrDefault() is IDictionary<string, object> row)
            {
                tonTruoc = ToDecimal(row.TryGetValue("so_luong_ton", out var s) ? s : null);
                giaVon = ToDecimal(row.TryGetValue("gia_von_binh_quan", out var g) ? g : null);
            }
            else
            {
                giaVon = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(gia_von, 0) FROM san_pham WHERE id = @Id;",
                    new { Id = productId },
                    transaction);
            }

            const string upsertTonSql = """
            INSERT INTO ton_kho
            (
                chi_nhanh_id,
                san_pham_id,
                so_luong_ton,
                gia_von_binh_quan,
                ngay_cap_nhat
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                @SoLuongTon,
                @GiaVonBinhQuan,
                NOW()
            )
            ON DUPLICATE KEY UPDATE
                so_luong_ton  = @SoLuongTon,
                ngay_cap_nhat = NOW();
            """;

            await connection.ExecuteAsync(
                upsertTonSql,
                new
                {
                    ChiNhanhId = branchId,
                    SanPhamId = productId,
                    SoLuongTon = soLuongTonMoi,
                    GiaVonBinhQuan = giaVon
                },
                transaction);

            await connection.ExecuteAsync(
                """
                UPDATE san_pham
                SET ton_kho_excel = @SoLuongTon,
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
                """,
                new
                {
                    SanPhamId = productId,
                    SoLuongTon = soLuongTonMoi
                },
                transaction);

            try
            {
                const string insertLichSuSql = """
                INSERT INTO lich_su_nhap_xuat_kho
                (
                    chi_nhanh_id,
                    san_pham_id,
                    loai_giao_dich,
                    loai_chung_tu,
                    ma_chung_tu,
                    so_luong,
                    ton_truoc,
                    ton_sau,
                    gia_von,
                    nguoi_thuc_hien_id,
                    thoi_gian,
                    ghi_chu
                )
                VALUES
                (
                    @ChiNhanhId,
                    @SanPhamId,
                    'KIEM_KHO',
                    'CHINH_SUA_NHANH',
                    NULL,
                    @SoLuong,
                    @TonTruoc,
                    @TonSau,
                    @GiaVon,
                    @NguoiThucHienId,
                    NOW(),
                    @GhiChu
                );
                """;

                var chenhLech = soLuongTonMoi - tonTruoc;
                await connection.ExecuteAsync(
                    insertLichSuSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = productId,
                        SoLuong = Math.Abs(chenhLech),
                        TonTruoc = tonTruoc,
                        TonSau = soLuongTonMoi,
                        GiaVon = giaVon,
                        NguoiThucHienId = userId,
                        GhiChu = "Chá»‰nh sá»­a tá»“n kho nhanh táº¡i giao diá»‡n bÃ¡n hÃ ng"
                    },
                    transaction);
            }
            catch (Exception exLichSu)
            {
                SafeLog.WriteError("Ghi lich su ton kho nhanh", exLichSu);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Chá»‰nh tá»“n kho nhanh",
                "SAN_PHAM",
                productId,
                null,
                $"Cáº­p nháº­t tá»“n kho sáº£n pháº©m ID {productId} tá»« {tonTruoc:N0} thÃ nh {soLuongTonMoi:N0}",
                soLuongTonMoi);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Cap nhat ton kho nhanh", ex);
            throw;
        }
    }

    public async Task XoaSanPhamAsync(uint productId)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a hÃ ng hÃ³a.");
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM san_pham WHERE id = @Id;",
            new { Id = productId });
    }

    /// <summary>
    /// ÄÆ°a kÃ½ tá»± xuá»‘ng dÃ²ng trong SQL vá» dáº¡ng LF.
    /// <para>
    /// Cáº§n thiáº¿t vÃ¬ cÃ¡c háº±ng SQL viáº¿t dáº¡ng raw string literal giá»¯ nguyÃªn CRLF cá»§a file
    /// nguá»“n. MariaDB cháº¥p nháº­n CR trong danh sÃ¡ch cá»™t cá»§a INSERT (náº±m trong ngoáº·c
    /// trÃ²n) nhÆ°ng láº¡i bÃ¡o lá»—i cÃº phÃ¡p khi CR xuáº¥t hiá»‡n trong má»‡nh Ä‘á» SET â€” má»—i láº§n
    /// thay tháº¿ sáº£n pháº©m trÃ¹ng Ä‘á»u há»ng vÃ¬ lÃ½ do nÃ y.
    /// </para>
    /// </summary>
    private static string ChuanHoaDongMoiSql(string sql)
    {
        return sql.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>Cá»™t cá»§a báº£ng san_pham Ä‘Æ°á»£c ghi khi nháº­p tá»« file, dÃ¹ng chung cho INSERT vÃ  UPDATE.</summary>
    private const string CotNhapSanPham = """
        ma_san_pham,
        ma_vach,
        ten_san_pham,
        nhom_san_pham_id,
        don_vi_tinh_id,
        thuong_hieu_id,
        gia_ban,
        gia_von,
        ton_toi_thieu,
        ton_toi_da,
        vi_tri_kho,
        mo_ta,
        ghi_chu,
        hinh_anh,
        co_ban_truc_tiep,
        trang_thai,
        loai_hang_excel,
        nhom_hang_3_cap_excel,
        ton_kho_excel,
        khach_dat,
        du_kien_het_hang,
        ma_dvt_co_ban,
        quy_doi,
        thuoc_tinh,
        ma_hh_lien_quan,
        trong_luong,
        dang_kinh_doanh,
        hang_thanh_phan,
        mau_ghi_chu
        """;

    private const string GiaTriNhapSanPham = """
        @MaSanPham,
        @MaVach,
        @TenSanPham,
        @NhomSanPhamId,
        @DonViTinhId,
        @ThuongHieuId,
        @GiaBan,
        @GiaVon,
        @TonToiThieu,
        @TonToiDa,
        @ViTriKho,
        @MoTa,
        @GhiChu,
        @HinhAnh,
        @DuocBanTrucTiep,
        @DangKinhDoanh,
        @LoaiHang,
        @NhomHangBaCap,
        @TonKhoExcel,
        @KhachDat,
        @DuKienHetHang,
        @MaDvtCoBan,
        @QuyDoi,
        @ThuocTinh,
        @MaHhLienQuan,
        @TrongLuong,
        @DangKinhDoanhExcel,
        @HangThanhPhan,
        @MauGhiChu
        """;

    /// <summary>
    /// Cáº·p "cá»™t = tham sá»‘" cho má»‡nh Ä‘á» SET cá»§a UPDATE, sinh ra tá»« chÃ­nh
    /// <see cref="CotNhapSanPham"/> vÃ  <see cref="GiaTriNhapSanPham"/>.
    /// <para>
    /// UPDATE khÃ´ng dÃ¹ng láº¡i Ä‘Æ°á»£c danh sÃ¡ch cá»™t tráº§n cá»§a INSERT: "SET ma_san_pham,
    /// ma_vach, ..." lÃ  cÃº phÃ¡p sai, pháº£i lÃ  "SET ma_san_pham = @MaSanPham,
    /// ma_vach = @MaVach, ...". Sinh tá»± Ä‘á»™ng tá»« hai danh sÃ¡ch trÃªn Ä‘á»ƒ hai bÃªn
    /// khÃ´ng thá»ƒ lá»‡ch nhau â€” lá»‡ch chá»— nÃ y lÃ  má»i láº§n thay tháº¿ sáº£n pháº©m trÃ¹ng Ä‘á»u há»ng.
    /// </para>
    /// </summary>
    private static readonly string CapNhatNhapSanPham = TaoCapNhatNhapSanPham();

    private static string TaoCapNhatNhapSanPham()
    {
        var cot = CotNhapSanPham.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var giaTri = GiaTriNhapSanPham.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (cot.Length != giaTri.Length)
            throw new InvalidOperationException(
                $"Danh sÃ¡ch cá»™t ({cot.Length}) vÃ  danh sÃ¡ch giÃ¡ trá»‹ ({giaTri.Length}) "
                + "cá»§a phÃ©p nháº­p sáº£n pháº©m khÃ´ng khá»›p nhau.");

        return ChuanHoaDongMoiSql(
            string.Join(",\n", cot.Zip(giaTri, (c, v) => $"{c} = {v}")));
    }

    /// <summary>Äá»™ dÃ i tá»‘i Ä‘a cá»§a cÃ¡c cá»™t varchar trong báº£ng san_pham (Ä‘Æ°á»£c xÃ¡c nháº­n tá»« schema).</summary>
    private const int DoDaiToiDaTenSanPham = 255;
    private const int DoDaiToiDaMaSanPham = 50;
    private const int DoDaiToiDaMaVach = 100;
    private const int DoDaiToiDaViTriKho = 100;
    private const int DoDaiToiDaDuKienHetHang = 100;
    private const int DoDaiToiDaLoaiHang = 100;
    private const int DoDaiToiDaMaHhLienQuan = 100;
    private const int DoDaiToiDaMaDvtCoBan = 50;
    private const int DoDaiToiDaNhomHangBaCap = 255;

    /// <summary>
    /// Tra cá»©u sáº£n pháº©m trÃ¹ng trong database theo MÃƒ HÃ€NG (khÃ³a chÃ­nh) vÃ  MÃƒ Váº CH.
    /// <para>
    /// Cá»‘ tÃ¬nh KHÃ”NG so tÃªn hÃ ng: hai mÃ³n hÃ ng khÃ¡c nhau ráº¥t dá»… trÃ¹ng tÃªn, coi tÃªn lÃ 
    /// khÃ³a trÃ¹ng sáº½ Ã¢m tháº§m bá» sÃ³t sáº£n pháº©m há»£p lá»‡.
    /// </para>
    /// <para>
    /// Truy váº¥n cháº¡y trÃªn database vá»›i danh sÃ¡ch mÃ£ cáº§n tra cá»©u, khÃ´ng táº£i toÃ n bá»™
    /// báº£ng vá» bá»™ nhá»›, nÃªn váº«n nhanh khi file cÃ³ hÃ ng nghÃ¬n dÃ²ng.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<SanPham>> TimSanPhamTrungAsync(
        IReadOnlyCollection<SanPham> productsFromFile)
    {
        var maSanPhamList = productsFromFile
            .Select(p => p.MaSanPham?.Trim())
            .Where(ma => !string.IsNullOrWhiteSpace(ma))
            .Select(ma => ma!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var maVachList = productsFromFile
            .Select(p => p.MaVach?.Trim())
            .Where(ma => !string.IsNullOrWhiteSpace(ma))
            .Select(ma => ma!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (maSanPhamList.Length == 0 && maVachList.Length == 0)
            return [];

        // Tá»± dá»±ng danh sÃ¡ch tham sá»‘ cho má»‡nh Ä‘á» IN thay vÃ¬ Ä‘Æ°a cáº£ máº£ng vÃ o má»™t tham sá»‘.
        // (Dapper coi máº£ng lÃ  má»™t giÃ¡ trá»‹ duy nháº¥t nÃªn SQL sáº½ thÃ nh IN (('a','b'))
        // vÃ  MariaDB bÃ¡o "Operand should contain 1 column".)
        var parameters = new DynamicParameters();
        var dieuKien = new List<string>();

        if (maSanPhamList.Length > 0)
        {
            var ten = "MaSanPham";
            var danhSach = new List<string>(maSanPhamList.Length);

            for (var i = 0; i < maSanPhamList.Length; i++)
            {
                parameters.Add(ten + i, maSanPhamList[i]);
                danhSach.Add("@" + ten + i);
            }

            dieuKien.Add($"sp.ma_san_pham IN ({string.Join(", ", danhSach)})");
        }

        if (maVachList.Length > 0)
        {
            var ten = "MaVach";
            var danhSach = new List<string>(maVachList.Length);

            for (var i = 0; i < maVachList.Length; i++)
            {
                parameters.Add(ten + i, maVachList[i]);
                danhSach.Add("@" + ten + i);
            }

            dieuKien.Add($"sp.ma_vach IN ({string.Join(", ", danhSach)})");
        }

        const string sql = """
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                sp.ton_kho_excel AS TonKhoExcel,
                sp.ngay_tao AS NgayTao
            FROM san_pham sp
            WHERE
            """;

        await using var connection = await OpenAsync();

        return (await connection.QueryAsync<SanPham>(
                sql + " " + string.Join(" OR ", dieuKien),
                parameters))
            .AsList();
    }

    /// <summary>
    /// Ghi danh sÃ¡ch sáº£n pháº©m xuá»‘ng database trong Má»˜T transaction.
    /// <list type="bullet">
    /// <item>ChÆ°a cÃ³ trong database -> INSERT báº£n ghi má»›i.</item>
    /// <item>ÄÃ£ cÃ³ vÃ  ngÆ°á»i dÃ¹ng chá»n thay tháº¿ -> UPDATE Ä‘Ãºng báº£n ghi cÅ©, khÃ´ng táº¡o báº£n ghi má»›i.</item>
    /// <item>ÄÃ£ cÃ³ vÃ  ngÆ°á»i dÃ¹ng chá»n giá»¯ -> khÃ´ng ghi gÃ¬.</item>
    /// <item>DÃ²ng lá»—i -> ghi nháº­n lá»—i vÃ  tiáº¿p tá»¥c dÃ²ng sau, khÃ´ng lÃ m há»ng cáº£ lÆ°á»£t nháº­p.</item>
    /// </list>
    /// Náº¿u xáº£y ra lá»—i nghiÃªm trá»ng (máº¥t káº¿t ná»‘i, báº£ng khÃ´ng tá»“n táº¡i...) thÃ¬ rollback
    /// toÃ n bá»™ Ä‘á»ƒ database khÃ´ng bá»‹ Ä‘á»ƒ láº¡i tráº¡ng thÃ¡i nháº­p dá»Ÿ dang.
    /// </summary>
    /// <summary>
    /// Ghi danh sÃ¡ch sáº£n pháº©m xuá»‘ng database trong má»™t transaction.
    /// </summary>
    /// <param name="tenFile">TÃªn file, chá»‰ Ä‘á»ƒ hiá»ƒn thá»‹ trong bÃ¡o cÃ¡o.</param>
    /// <param name="danhSachHang">Danh sÃ¡ch thao tÃ¡c Ä‘Ã£ Ä‘Æ°á»£c quyáº¿t Ä‘á»‹nh cho tá»«ng dÃ²ng.</param>
    /// <param name="tongSoDongExcel">Tá»•ng sá»‘ dÃ²ng dá»¯ liá»‡u cÃ³ trong file.</param>
    /// <param name="loiDocFile">
    /// CÃ¡c dÃ²ng Ä‘Ã£ lá»—i ngay khi Ä‘á»c file (khÃ´ng Ä‘á»c Ä‘Æ°á»£c Ã´, thiáº¿u cáº£ mÃ£ láº«n tÃªn).
    /// ÄÆ°á»£c gá»™p vÃ o bÃ¡o cÃ¡o Ä‘á»ƒ khÃ´ng cÃ³ dÃ²ng nÃ o cá»§a file biáº¿n máº¥t im láº·ng.
    /// </param>
    public async Task<KetQuaImportSanPham> ImportSanPhamAsync(
        string tenFile,
        IReadOnlyList<HangNhapSanPham> danhSachHang,
        int tongSoDongExcel,
        IReadOnlyCollection<LoiDongNhap>? loiDocFile = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsImport, "Báº¡n khÃ´ng cÃ³ quyá»n import hÃ ng hÃ³a.");
        var ketQua = new KetQuaImportSanPham
        {
            TenFile = tenFile,
            TongSoDongExcel = tongSoDongExcel
        };

        if (loiDocFile is { Count: > 0 })
        {
            ketQua.LoiDong.AddRange(loiDocFile.OrderBy(l => l.SoDongExcel));
            ketQua.SoLoi += loiDocFile.Count;
        }

        var canGhi = danhSachHang
            .Where(h => h.hanDong is HanDong.ThemMoi or HanDong.ThayThe)
            .ToList();

        // NgÆ°á»i dÃ¹ng chá»n giá»¯ háº¿t sáº£n pháº©m trÃ¹ng thÃ¬ khÃ´ng má»Ÿ transaction.
        if (canGhi.Count == 0)
            return ketQua;

        // san_pham.ngay_tao máº·c Ä‘á»‹nh lÃ  NOT NULL DEFAULT CURRENT_TIMESTAMP, nÃªn náº¿u
        // khÃ´ng gá»­i cá»™t nÃ y thÃ¬ MySQL tá»± Ä‘iá»n ngÃ y LÃšC NHáº¬P, lÃ m sai lá»‹ch sá»­.
        // Cho phÃ©p cá»™t nháº­n NULL Ä‘á»ƒ ghi Ä‘Ãºng NULL khi Ã´ "Thá»i gian táº¡o" trong
        // Excel trá»‘ng (khÃ´ng cÃ³ dá»¯ liá»‡u cÅ© thÃ¬ khÃ´ng bá»‹a ra ngÃ y giáº£).
        var ghiDuLieuNgayTao = await DamBaoCotNgayTaoSanPhamAsync();

        if (!ghiDuLieuNgayTao)
            ketQua.CanhBao.Add(
                "KhÃ´ng sá»­a Ä‘Æ°á»£c cá»™t ngay_tao Ä‘á»ƒ cho phÃ©p NULL: nhá»¯ng dÃ²ng khÃ´ng cÃ³ "
                + "'Thá»i gian táº¡o' trong file sáº½ nháº­n ngÃ y lÃºc nháº­p. Cáº§n cáº¥p quyá»n ALTER "
                + "trÃªn báº£ng san_pham Ä‘á»ƒ sá»­a.");

        var insertSql = ChuanHoaDongMoiSql($"""
            INSERT INTO san_pham
            (
                {CotNhapSanPham}{(ghiDuLieuNgayTao ? ",\n                ngay_tao" : "")}
            )
            VALUES
            (
                {GiaTriNhapSanPham}{(ghiDuLieuNgayTao ? ",\n                @NgayTao" : "")}
            );
            """);

        // Khi thay tháº¿: ngÃ y táº¡o láº¥y tá»« Excel náº¿u file cÃ³, cÃ²n khÃ´ng thÃ¬ GIá»® NGUYÃŠN
        // ngÃ y Ä‘ang cÃ³ trong database â€” tuyá»‡t Ä‘á»‘i khÃ´ng dÃ¹ng DateTime.Now.
        var updateSql = ChuanHoaDongMoiSql($"""
            UPDATE san_pham
            SET {CapNhatNhapSanPham},
                ngay_cap_nhat = NOW()
                {(ghiDuLieuNgayTao ? ", ngay_tao = COALESCE(@NgayTao, ngay_tao)" : string.Empty)}
            WHERE id = @Id;
            """);

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var thuongHieuTheoTen = await LoadDanhMucThamChieuAsync(connection, transaction, "thuong_hieu", "ten_thuong_hieu");
            var donViTinhTheoTen = await LoadDanhMucThamChieuAsync(connection, transaction, "don_vi_tinh", "ten_don_vi");
            var donViTinhTheoMa = await LoadDanhMucThamChieuAsync(connection, transaction, "don_vi_tinh", "ma_don_vi");
            var nhomSanPhamTheoTen = await LoadDanhMucThamChieuAsync(connection, transaction, "nhom_san_pham", "ten_nhom");

            for (var index = 0; index < canGhi.Count; index++)
            {
                var hang = canGhi[index];

                // SAVEPOINT cho tá»«ng dÃ²ng: má»™t dÃ²ng há»ng (vÃ­ dá»¥ trÃ¹ng mÃ£ váº¡ch) chá»‰
                // bá»‹ loáº¡i riÃªng, khÃ´ng máº¥t cÃ¡c dÃ²ng Ä‘Ã£ ghi trÆ°á»›c Ä‘Ã³ vÃ  khÃ´ng phÃ¡ vá»¡
                // transaction chung.
                var savepoint = $"sp_nhap_{index}";
                await connection.ExecuteAsync($"SAVEPOINT {savepoint};", transaction: transaction);

                var canhBao = new List<string>();

                try
                {
                    var parameters = BuildParametersNhapSanPham(
                        hang.SanPham,
                        thuongHieuTheoTen,
                        donViTinhTheoTen,
                        donViTinhTheoMa,
                        nhomSanPhamTheoTen,
                        canhBao);

                    foreach (var dong in canhBao)
                    {
                        if (!ketQua.CanhBao.Contains(dong))
                            ketQua.CanhBao.Add(dong);
                    }

                    var sql = hang.hanDong == HanDong.ThemMoi ? insertSql : updateSql;

                    if (hang.hanDong == HanDong.ThayThe)
                    {
                        if (hang.IdTrongDatabase is null)
                            throw new InvalidOperationException("Thiáº¿u id sáº£n pháº©m Ä‘Ã£ tá»“n táº¡i Ä‘á»ƒ thay tháº¿.");

                        parameters.Add("Id", hang.IdTrongDatabase.Value);
                    }

                    await connection.ExecuteAsync(sql, parameters, transaction);

                    if (hang.hanDong == HanDong.ThemMoi)
                        ketQua.SoThemMoi++;
                    else
                        ketQua.SoThayThe++;
                }
                catch (Exception ex)
                {
                    await connection.ExecuteAsync($"ROLLBACK TO SAVEPOINT {savepoint};", transaction: transaction);
                    ketQua.SoLoi++;
                    ketQua.LoiDong.Add(new LoiDongNhap(
                        hang.SoDongExcel,
                        hang.SanPham.MaSanPham,
                        hang.SanPham.TenSanPham,
                        MoTaLoiNhap(ex)));
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ketQua.DaRollback = true;
            ketQua.SoThemMoi = 0;
            ketQua.SoThayThe = 0;
            ketQua.LoiDong.Add(new LoiDongNhap(0, null, null,
                "Lá»—i nghiÃªm trá»ng, Ä‘Ã£ rollback toÃ n bá»™ lÆ°á»£t nháº­p: " + MoTaLoiNhap(ex)));
        }

        return ketQua;
    }

    /// <summary>Chuyá»ƒn lá»—i MySQL thÃ nh thÃ´ng bÃ¡o dá»… hiá»ƒu cho ngÆ°á»i dÃ¹ng.</summary>
    internal static string MoTaLoiNhap(Exception ex)
    {
        if (ex is MySqlException mySql)
        {
            return mySql.Number switch
            {
                1062 => $"trÃ¹ng khÃ³a duy nháº¥t ({mySql.Message}). Kiá»ƒm tra láº¡i mÃ£ hÃ ng/mÃ£ váº¡ch.",
                1406 => $"giÃ¡ trá»‹ quÃ¡ dÃ i so vá»›i Ä‘á»™ dÃ i cá»™t ({mySql.Message}).",
                1048 => $"thiáº¿u giÃ¡ trá»‹ báº¯t buá»™c ({mySql.Message}).",
                1452 => $"khÃ´ng tÃ¬m tháº¥y báº£n ghi tham chiáº¿u ({mySql.Message}).",
                _ => $"lá»—i cÆ¡ sá»Ÿ dá»¯ liá»‡u {mySql.Number}: {mySql.Message}"
            };
        }

        return ex.Message;
    }

    /// <summary>
    /// Náº¡p báº£ng danh má»¥c (thÆ°Æ¡ng hiá»‡u / Ä‘Æ¡n vá»‹ tÃ­nh / nhÃ³m hÃ ng) Ä‘á»ƒ tra id theo tÃªn.
    /// Báº£ng rá»—ng hoáº·c khÃ´ng tá»“n táº¡i thÃ¬ tráº£ vá» tá»« Ä‘iá»ƒn rá»—ng, lÃºc Ä‘Ã³ cá»™t id tÆ°Æ¡ng á»©ng
    /// Ä‘Æ°á»£c Ä‘á»ƒ NULL â€” dÃ²ng váº«n nháº­p bÃ¬nh thÆ°á»ng.
    /// </summary>
    private static async Task<Dictionary<string, uint>> LoadDanhMucThamChieuAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tenBang,
        string cotTen)
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var rows = await connection.QueryAsync<(uint Id, string Ten)>(
                $"SELECT id AS Id, {cotTen} AS Ten FROM {tenBang};",
                transaction: transaction);

            foreach (var row in rows)
            {
                var key = row.Ten?.Trim();

                if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                    result[key] = row.Id;
            }
        }
        catch (MySqlException)
        {
            // KhÃ´ng cÃ³ báº£ng danh má»¥c: bá» qua, khÃ´ng lÃ m há»ng lÆ°á»£t nháº­p.
        }

        return result;
    }

    /// <summary>
    /// Gom tham sá»‘ cho INSERT/UPDATE. CÃ¡c cá»™t khÃ´ng báº¯t buá»™c Ä‘Æ°á»£c Ä‘á»ƒ NULL/rá»—ng bÃ¬nh
    /// thÆ°á»ng, vÃ  giÃ¡ trá»‹ vÆ°á»£t Ä‘á»™ dÃ i cá»™t varchar Ä‘Æ°á»£c cáº¯t láº¡i kÃ¨m cáº£nh bÃ¡o thay vÃ¬
    /// lÃ m há»ng cáº£ dÃ²ng.
    /// </summary>
    private static DynamicParameters BuildParametersNhapSanPham(
        SanPham product,
        Dictionary<string, uint> thuongHieuTheoTen,
        Dictionary<string, uint> donViTinhTheoTen,
        Dictionary<string, uint> donViTinhTheoMa,
        Dictionary<string, uint> nhomSanPhamTheoTen,
        List<string> canhBao)
    {

        uint? TraTheoTen(Dictionary<string, uint> dic, string? ten)
        {
            var key = ten?.Trim();

            if (string.IsNullOrWhiteSpace(key))
                return null;

            return dic.TryGetValue(key, out var id) ? id : null;
        }

        string? Cat(string? value, int doDaiToiDa, string tenCot)
        {
            var trimmed = value?.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                return null;

            if (trimmed.Length <= doDaiToiDa)
                return trimmed;

            canhBao.Add(
                $"Cá»™t \"{tenCot}\" dÃ i {trimmed.Length} kÃ½ tá»±, Ä‘Ã£ cáº¯t cÃ²n {doDaiToiDa} kÃ½ tá»± cho vá»«a Ä‘á»™ dÃ i cá»™t.");

            return trimmed[..doDaiToiDa];
        }

        var maSanPham = Cat(product.MaSanPham, DoDaiToiDaMaSanPham, "MÃ£ hÃ ng") ?? string.Empty;
        var maVach = Cat(product.MaVach, DoDaiToiDaMaVach, "MÃ£ váº¡ch");

        // ma_vach cÃ³ UNIQUE index. Nhiá»u dÃ²ng khÃ´ng cÃ³ mÃ£ váº¡ch thÃ¬ ghi NULL (MySQL cho
        // phÃ©p nhiá»u NULL trong UNIQUE); Ä‘á»ƒ chuá»—i rá»—ng sáº½ khiáº¿n chá»‰ má»™t dÃ²ng Ä‘Æ°á»£c nháº­p
        // rá»“i cÃ¡c dÃ²ng sau bÃ¡o trÃ¹ng.
        var parameters = new DynamicParameters();
        parameters.Add("MaSanPham", maSanPham, DbType.String);
        parameters.Add("MaVach", maVach, DbType.String);
        parameters.Add("TenSanPham", Cat(product.TenSanPham, DoDaiToiDaTenSanPham, "TÃªn hÃ ng") ?? maSanPham, DbType.String);
        parameters.Add("NhomSanPhamId", TraTheoTen(nhomSanPhamTheoTen, product.NhomHangBaCap), DbType.Int32);
        parameters.Add("DonViTinhId",
            TraTheoTen(donViTinhTheoMa, product.MaDvtCoBan)
            ?? TraTheoTen(donViTinhTheoTen, product.TenDonViTinh),
            DbType.Int32);
        parameters.Add("ThuongHieuId", TraTheoTen(thuongHieuTheoTen, product.ThuongHieu), DbType.Int32);
        parameters.Add("GiaBan", product.GiaBan);
        parameters.Add("GiaVon", product.GiaVon);
        parameters.Add("TonToiThieu", product.TonToiThieu);
        parameters.Add("TonToiDa", product.TonToiDa);
        parameters.Add("ViTriKho", Cat(product.ViTriKho, DoDaiToiDaViTriKho, "Vá»‹ trÃ­"), DbType.String);
        parameters.Add("MoTa", BlankToNull(product.MoTa), DbType.String);
        parameters.Add("GhiChu", BlankToNull(product.GhiChu), DbType.String);
        parameters.Add("HinhAnh", BlankToNull(product.HinhAnh), DbType.String);
        parameters.Add("DuocBanTrucTiep", product.DuocBanTrucTiep);
        parameters.Add("DangKinhDoanh", product.DangKinhDoanh);
        parameters.Add("LoaiHang", Cat(product.LoaiHang, DoDaiToiDaLoaiHang, "Loáº¡i hÃ ng"), DbType.String);
        parameters.Add("NhomHangBaCap", Cat(product.NhomHangBaCap, DoDaiToiDaNhomHangBaCap, "NhÃ³m hÃ ng(3 Cáº¥p)"), DbType.String);
        parameters.Add("TonKhoExcel", product.TonKhoExcel);
        parameters.Add("KhachDat", product.KhachDat);
        parameters.Add("DuKienHetHang", Cat(product.DuKienHetHang, DoDaiToiDaDuKienHetHang, "Dá»± kiáº¿n háº¿t hÃ ng"), DbType.String);
        parameters.Add("MaDvtCoBan", Cat(product.MaDvtCoBan, DoDaiToiDaMaDvtCoBan, "MÃ£ ÄVT CÆ¡ báº£n"), DbType.String);
        parameters.Add("QuyDoi", product.QuyDoi <= 0 ? 1m : product.QuyDoi);
        parameters.Add("ThuocTinh", BlankToNull(product.ThuocTinh), DbType.String);
        parameters.Add("MaHhLienQuan", Cat(product.MaHhLienQuan, DoDaiToiDaMaHhLienQuan, "MÃ£ HH LiÃªn quan"), DbType.String);
        parameters.Add("TrongLuong", product.TrongLuong, DbType.Decimal);
        parameters.Add("DangKinhDoanhExcel", product.DangKinhDoanh);
        parameters.Add("HangThanhPhan", BlankToNull(product.HangThanhPhan), DbType.String);
        parameters.Add("MauGhiChu", BlankToNull(product.MauGhiChu), DbType.String);
        parameters.Add("NgayTao", product.NgayTao, DbType.DateTime);

        return parameters;
    }

    private static string? BlankToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m <c>san_pham.ngay_tao</c> cho phÃ©p giÃ¡ trá»‹ NULL.
    /// <para>
    /// Cá»™t nÃ y Ä‘Æ°á»£c khai bÃ¡o <c>NOT NULL DEFAULT CURRENT_TIMESTAMP</c>, nÃªn khi
    /// nháº­p hÃ ng tá»« file mÃ  Ã´ "Thá»i gian táº¡o" bá»‹ trá»‘ng, MySQL sáº½ tá»± Ä‘iá»n ngÃ y
    /// lÃºc nháº­p â€” Ä‘Ãºng cÃ¡i lá»—i lÃ m sai dá»¯ liá»‡u lá»‹ch sá»­. Sá»­a thÃ nh
    /// <c>DATETIME NULL DEFAULT CURRENT_TIMESTAMP</c>:
    /// nháº­p tay sáº£n pháº©m má»›i (bá» trá»‘ng cá»™t) váº«n nháº­n ngÃ y táº¡o = thá»i Ä‘iá»ƒm thÃªm,
    /// cÃ²n nháº­p tá»« file thÃ¬ ghi Ä‘Ãºng giÃ¡ trá»‹ trong file hoáº·c NULL.
    /// </para>
    /// <para>
    /// Cháº¡y má»™t láº§n duy nháº¥t rá»“i ghi nhá»› káº¿t quáº£, khÃ´ng Ä‘á»¥ng tá»›i dá»¯ liá»‡u sáºµn cÃ³.
    /// Náº¿u database khÃ´ng cho sá»­a (thiáº¿u quyá»n) thÃ¬ pháº§n má»m váº«n cháº¡y, chá»‰ khÃ´ng
    /// ghi cá»™t ngay_tao khi nháº­p tá»« file.
    /// </para>
    /// </summary>
    private async Task<bool> DamBaoCotNgayTaoSanPhamAsync()
    {
        if (_cotNgayTaoSanPhamChoPhepNull.HasValue)
            return _cotNgayTaoSanPhamChoPhepNull.Value;

        try
        {
            await using var connection = await OpenAsync();

            const string kiemTraSql = """
                SELECT IS_NULLABLE
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'san_pham'
                  AND COLUMN_NAME = 'ngay_tao';
                """;

            var isNullable = await connection.ExecuteScalarAsync<string>(kiemTraSql);

            if (string.IsNullOrWhiteSpace(isNullable))
            {
                // KhÃ´ng Ä‘á»c Ä‘Æ°á»£c cá»™t (báº£ng thiáº¿u / khÃ´ng Ä‘á»§ quyá»n): giá»¯ nguyÃªn
                // Ä‘á»‹nh nghÄ©a cÅ© thay vÃ¬ Ä‘oÃ¡n.
                _cotNgayTaoSanPhamChoPhepNull = false;
            }
            else if (!isNullable.Equals("YES", StringComparison.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE san_pham MODIFY COLUMN ngay_tao DATETIME NULL DEFAULT CURRENT_TIMESTAMP;");

                _cotNgayTaoSanPhamChoPhepNull = true;
            }
            else
            {
                _cotNgayTaoSanPhamChoPhepNull = true;
            }
        }
        catch
        {
            _cotNgayTaoSanPhamChoPhepNull = false;
        }

        return _cotNgayTaoSanPhamChoPhepNull.Value;
    }

    public List<SanPham> ParseProductsFromFile(string filePath)
    {
        return ParseProductsFromFile(filePath, out _);
    }

    /// <summary>
    /// Äá»c danh sÃ¡ch hÃ ng hÃ³a tá»« file vÃ  tráº£ vá» kÃ¨m cÃ¡c dÃ²ng khÃ´ng Ä‘á»c Ä‘Æ°á»£c.
    /// <para>
    /// DÃ²ng thiáº¿u mÃ£ hÃ ng/tÃªn khÃ´ng Ä‘Æ°á»£c Ã¢m tháº§m loáº¡i bá» â€” náº¿u khÃ´ng, ngÆ°á»i dÃ¹ng
    /// tháº¥y bÃ¡o cÃ¡o "736 dÃ²ng" trong khi file cÃ³ 740 dÃ²ng mÃ  khÃ´ng biáº¿t 4 dÃ²ng Ä‘Ã³
    /// Ä‘Ã£ máº¥t á»Ÿ Ä‘Ã¢u. DÃ²ng khÃ´ng Ä‘á»c Ä‘Æ°á»£c Ä‘Æ°á»£c tráº£ vá» qua <paramref name="loiDocFile"/>
    /// Ä‘á»ƒ Ä‘Æ°a vÃ o bÃ¡o cÃ¡o lá»—i cá»§a lÆ°á»£t nháº­p.
    /// </para>
    /// </summary>
    public List<SanPham> ParseProductsFromFile(
        string filePath,
        out List<LoiDongNhap> loiDocFile)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("ÄÆ°á»ng dáº«n file khÃ´ng há»£p lá»‡.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("KhÃ´ng tÃ¬m tháº¥y file danh sÃ¡ch hÃ ng hÃ³a.", filePath);

        loiDocFile = [];
        var rows = ReadProductRowsFromFile(filePath);

        // Äá»c Háº¾T má»i dÃ²ng trong file. Chá»‰ dÃ²ng khÃ´ng cÃ³ báº¥t ká»³ dá»¯ liá»‡u nÃ o má»›i bá»‹
        // loáº¡i; má»™t dÃ²ng thiáº¿u mÃ£ váº¡ch / thÆ°Æ¡ng hiá»‡u / mÃ´ táº£ / hÃ¬nh áº£nh / nhÃ  cung
        // cáº¥p váº«n lÃ  dÃ²ng há»£p lá»‡ vÃ  pháº£i Ä‘Æ°á»£c giá»¯ láº¡i.
        var products = new List<SanPham>(rows.Count);

        for (var index = 0; index < rows.Count; index++)
        {
            var dong = rows[index];

            SanPham product;

            try
            {
                product = MapRowToProduct(dong.GiaTri, dong.SoDongExcel);
            }
            catch (Exception ex)
            {
                // Lá»—i Ä‘á»c má»™t dÃ²ng khÃ´ng Ä‘Æ°á»£c lÃ m há»ng cáº£ lÆ°á»£t nháº­p: ghi nháº­n
                // dÃ²ng lá»—i rá»“i tiáº¿p tá»¥c dÃ²ng tiáº¿p theo.
                loiDocFile.Add(new LoiDongNhap(
                    dong.SoDongExcel,
                    GetFirstString(ChuanHoaTieuDe(dong.GiaTri), "mahang", "masanpham", "ma_san_pham"),
                    null,
                    "KhÃ´ng Ä‘á»c Ä‘Æ°á»£c dá»¯ liá»‡u cá»§a dÃ²ng: " + MoTaLoiNhap(ex)));

                continue;
            }

            if (string.IsNullOrWhiteSpace(product.MaSanPham)
                && string.IsNullOrWhiteSpace(product.TenSanPham))
            {
                loiDocFile.Add(new LoiDongNhap(
                    dong.SoDongExcel,
                    null,
                    null,
                    "DÃ²ng khÃ´ng cÃ³ mÃ£ hÃ ng láº«n tÃªn sáº£n pháº©m nÃªn khÃ´ng táº¡o Ä‘Æ°á»£c sáº£n pháº©m."));

                continue;
            }

            products.Add(product);
        }

        return products;
    }

    /// <summary>Má»™t dÃ²ng Ä‘á»c tá»« file kÃ¨m sá»‘ thá»© tá»± dÃ²ng trong file, Ä‘á»ƒ bÃ¡o lá»—i chÃ­nh xÃ¡c.</summary>
    private sealed record DongFile(int SoDongExcel, Dictionary<string, string> GiaTri);

    private static List<DongFile> ReadProductRowsFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvProductRows(filePath);

        if (extension is ".xlsx" or ".xls")
            return ReadExcelProductRows(filePath);

        throw new NotSupportedException("File khÃ´ng Ä‘Æ°á»£c há»— trá»£. Vui lÃ²ng chá»n file Excel (.xlsx, .xls) hoáº·c CSV.");
    }

    private static List<DongFile> ReadExcelProductRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
            throw new InvalidOperationException("File Excel khÃ´ng chá»©a sheet dá»¯ liá»‡u há»£p lá»‡.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return [];

        var headers = new List<string>();
        var firstRow = worksheet.Row(1);
        foreach (var cell in firstRow.Cells())
            headers.Add(cell.GetString().Trim());

        var result = new List<DongFile>();

        // Duyá»‡t tá»« dÃ²ng 2 Ä‘áº¿n dÃ²ng cuá»‘i cÃ¹ng cá»§a sheet: khÃ´ng giá»›i háº¡n sá»‘ dÃ²ng,
        // nÃªn file 737 dÃ²ng thÃ¬ Ä‘á»c Ä‘á»§ 736 dÃ²ng dá»¯ liá»‡u.
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index];
                var value = DocGiaTriOTheoKieuSanPham(row.Cell(index + 1));

                if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                if (!string.IsNullOrWhiteSpace(header))
                    values[header] = value;
            }

            if (hasAnyValue)
                result.Add(new DongFile(rowNumber, values));
        }

        return result;
    }

    /// <summary>
    /// Äá»c má»™t Ã´ cá»§a file danh sÃ¡ch hÃ ng hÃ³a.
    /// <para>
    /// Ã” ngÃ y giá» PHáº¢I láº¥y theo kiá»ƒu dá»¯ liá»‡u (XLDataType.DateTime) chá»© khÃ´ng láº¥y
    /// chuá»—i Ä‘á»‹nh dáº¡ng cá»§a Ã´: "20/09/2026 21:00" trong Excel lÃ  má»™t DateTime tháº­t
    /// (Ä‘á»‹nh dáº¡ng chá»‰ lÃ  cÃ¡ch hiá»ƒn thá»‹), Ä‘á»c theo chuá»—i sáº½ phá»¥ thuá»™c vÃ o format Ã´
    /// vÃ  cÃ³ thá»ƒ máº¥t giá»/phÃºt. DateTime cá»§a Excel lÃ  giÃ¡ trá»‹ khÃ´ng mÃºi giá» nÃªn
    /// chuyá»ƒn tháº³ng ra chuá»—i "yyyy-MM-dd HH:mm:ss" â€” khÃ´ng cÃ³ bÆ°á»›c Ä‘á»•i mÃºi giá»
    /// nÃ o vÃ  khÃ´ng tá»± Ä‘á»•i sang ngÃ y hiá»‡n táº¡i.
    /// </para>
    /// <para>
    /// Sá»‘ / chá»¯ / boolean / Ã´ trá»‘ng váº«n giá»¯ Ä‘Ãºng cÃ¡ch Ä‘á»c cÅ© (GetString) Ä‘á»ƒ khÃ´ng
    /// Ä‘á»•i hÃ nh vi cá»§a cÃ¡c cá»™t khÃ¡c trong file.
    /// </para>
    /// </summary>
    private static string DocGiaTriOTheoKieuSanPham(IXLCell cell)
    {
        return cell.Value.Type switch
        {
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            XLDataType.Blank => string.Empty,

            // Ã” lá»—i cÃ´ng thá»©c (#DIV/0!, #VALUE!...) láº¥y ná»™i dung lá»—i lÃ m chuá»—i
            // thay vÃ¬ Ä‘á»ƒ GetString() nÃ©m exception, vÃ¬ má»™t Ã´ há»ng trong file
            // khÃ´ng Ä‘Æ°á»£c lÃ m há»ng cáº£ 736 dÃ²ng cÃ²n láº¡i.
            XLDataType.Error => cell.Value.GetError().ToString(),

            _ => cell.GetString().Trim()
        };
    }

    private static List<DongFile> ReadCsvProductRows(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
            return [];

        var headers = ParseCsvLine(lines[0]);
        var result = new List<DongFile>();

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                var value = i < cells.Count ? cells[i].Trim() : string.Empty;

                if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                if (!string.IsNullOrWhiteSpace(header))
                    row[header] = value;
            }

            if (hasAnyValue)
                result.Add(new DongFile(index + 1, row));
        }

        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    /// <summary>
    /// ÄÆ°a cÃ¡c tÃªn cá»™t trong file vá» dáº¡ng Ä‘Ã£ chuáº©n hÃ³a Ä‘á»ƒ tra cá»©u theo tÃªn cá»™t
    /// mÃ  code dÃ¹ng (sau <see cref="NormalizeHeader"/>). Cá»™t trÃ¹ng nhau sau khi
    /// chuáº©n hÃ³a thÃ¬ giá»¯ giÃ¡ trá»‹ Ä‘áº§u tiÃªn, trÃ¡nh lá»—i "key Ä‘Ã£ tá»“n táº¡i".
    /// </summary>
    private static Dictionary<string, string> ChuanHoaTieuDe(Dictionary<string, string> rawRow)
    {
        return rawRow
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .GroupBy(entry => NormalizeHeader(entry.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static SanPham MapRowToProduct(Dictionary<string, string> rawRow, int soDongExcel = 0)
    {
        var normalizedRow = ChuanHoaTieuDe(rawRow);

        var maSanPham = GetFirstString(normalizedRow, "mahang", "masanpham", "mach", "macode");
        var tenSanPham = GetFirstString(normalizedRow, "tenhang", "tensanpham", "ten", "name", "sanpham", "san_pham")
            ?? GetAnyNonEmptyValue(normalizedRow, "mahang", "masanpham", "mavach", "giaban", "giavon");

        // KhÃ´ng cÃ³ mÃ£ hÃ ng thÃ¬ Ä‘á»ƒ trá»‘ng thay vÃ¬ tá»± sinh mÃ£ theo giá» nháº­p: mÃ£ sinh tá»± Ä‘á»™ng
        // (SP-yyyyMMdd-HHmmssfff) má»—i láº§n nháº­p lÃ  má»™t sáº£n pháº©m khÃ¡c, khiáº¿n cÃ¹ng má»™t
        // mÃ³n hÃ ng bá»‹ nhÃ¢n báº£n má»—i láº§n nháº­p láº¡i file. Viá»‡c sinh mÃ£ Ä‘Æ°á»£c Ä‘Æ°a sang
        // thao tÃ¡c táº¡o sáº£n pháº©m thá»§ cÃ´ng, nÆ¡i ngÆ°á»i dÃ¹ng thá»±c sá»± cáº§n mÃ£ má»›i.
        var product = new SanPham
        {
            SoDongExcel = soDongExcel,
            MaSanPham = maSanPham ?? string.Empty,
            MaVach = GetFirstString(normalizedRow, "mavach", "barcode", "ma_vach"),
            TenSanPham = tenSanPham ?? maSanPham ?? string.Empty,
            GiaBan = ParseDecimal(GetFirstString(normalizedRow, "giaban", "giabanhang", "price", "dongia"), 0m),
            GiaVon = ParseDecimal(GetFirstString(normalizedRow, "giavon", "giavonhang", "cost"), 0m),
            TonKhoExcel = ParseDecimal(GetFirstString(normalizedRow, "tonkho", "tonkhoexcel", "soluongton"), 0m),
            TonToiThieu = ParseDecimal(GetFirstString(normalizedRow, "tontoithieu", "tonthieunhat", "tonthieu"), 0m),
            TonToiDa = ParseDecimal(GetFirstString(normalizedRow, "tontoida", "tonlonnhat", "tonmax"), 0m),
            KhachDat = ParseDecimal(GetFirstString(normalizedRow, "khdat", "khachdat", "soluongdat"), 0m),
            // "Thá»i gian táº¡o"/"NgÃ y táº¡o" pháº£i láº¥y Ä‘Ãºng tá»« file. KhÃ´ng cÃ³ giÃ¡ trá»‹
            // há»£p lá»‡ thÃ¬ Ä‘á»ƒ NULL (xem DamBaoCotNgayTaoSanPhamAsync) chá»© khÃ´ng
            // gÃ¡n DateTime.Now, náº¿u khÃ´ng sáº£n pháº©m khÃ´i phá»¥c tá»« dá»¯ liá»‡u cÅ© sáº½ bá»‹
            // Ä‘á»•i thÃ nh ngÃ y lÃºc nháº­p vÃ  sai lá»‹ch sá»­.
            NgayTao = ParseNgayTaiFileSanPham(GetFirstString(
                normalizedRow,
                "thoigiantao",
                "thoigiantaohang",
                "ngaytao",
                "ngaytaohang",
                "createdat",
                "createddate",
                "datecreated",
                "timecreated")),
            DuKienHetHang = GetFirstString(normalizedRow, "dukienhethang", "du_kien_het_hang", "het_hang"),
            DonViTinh = GetFirstString(normalizedRow, "dvt", "donvitinh", "don_vi_tinh"),
            TenDonViTinh = GetFirstString(normalizedRow, "dvt", "donvitinh", "don_vi_tinh"),
            LoaiHang = GetFirstString(normalizedRow, "loaihang", "loaihangexcel", "loai"),
            NhomHangBaCap = GetFirstString(normalizedRow, "nhomhang3cap", "nhomhang3capexcel", "nhomhang3cap"),
            ViTriKho = GetFirstString(normalizedRow, "vitri", "vitrikho", "vitri_kho"),
            DangKinhDoanh = ParseBoolean(GetFirstString(normalizedRow, "dangkinhdoanh", "trangthai", "status"), true),
            DuocBanTrucTiep = ParseBoolean(GetFirstString(normalizedRow, "duocbanchirectiep", "duocbantructiep", "bantructiep"), true),
            MoTa = GetFirstString(normalizedRow, "mota", "mo_ta"),
            GhiChu = GetFirstString(normalizedRow, "ghichu", "ghi_chu"),
            // Header thá»±c táº¿ lÃ  "HÃ¬nh áº£nh (url1,url2...)"; sau khi chuáº©n hÃ³a
            // ngoáº·c bá»‹ bá» nÃªn cáº§n thá»­ cáº£ dáº¡ng cÃ³ vÃ  khÃ´ng cÃ³ pháº§n trong ngoáº·c.
            HinhAnh = GetFirstString(normalizedRow, "hinhanh", "hinh", "hinh_anh", "hinh_anh_url", "hinhanhurl1url2"),
            ThuongHieu = GetFirstString(normalizedRow, "thuonghieu", "thuong_hieu", "brand"),
            MaDvtCoBan = GetFirstString(normalizedRow, "madvtcoban", "ma_dvt_co_ban"),
            QuyDoi = ParseDecimal(GetFirstString(normalizedRow, "quydoi", "quy_doi"), 1m),
            ThuocTinh = GetFirstString(normalizedRow, "thuocTinh", "thuoc_tinh"),
            MaHhLienQuan = GetFirstString(normalizedRow, "mahhlienquan", "ma_hh_lien_quan"),
            TrongLuong = ParseNullableDecimal(GetFirstString(normalizedRow, "trongluong", "trong_luong")),
            HangThanhPhan = GetFirstString(normalizedRow, "hangthanhphan", "hang_thanh_phan"),
            MauGhiChu = GetFirstString(normalizedRow, "maughichu", "mau_ghi_chu")
        };

        return product;
    }

    private static string? GetFirstString(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(NormalizeHeader(key), out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string? GetAnyNonEmptyValue(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var normalizedKey = NormalizeHeader(key);
            if (row.TryGetValue(normalizedKey, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        foreach (var value in row.Values)
        {
            var trimmed = value.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Chuyá»ƒn Ä‘á»•i an toÃ n giÃ¡ trá»‹ báº¥t ká»³ tá»« cÆ¡ sá»Ÿ dá»¯ liá»‡u sang kiá»ƒu decimal.
    /// Há»— trá»£ null, DBNull, sá»‘ nguyÃªn, sá»‘ thá»±c hoáº·c chuá»—i sá»‘.
    /// </summary>
    private static decimal ToDecimal(object? value)
    {
        if (value is null or DBNull)
            return 0m;

        if (value is decimal d)
            return d;

        if (value is double dbl)
            return (decimal)dbl;

        if (value is float f)
            return (decimal)f;

        if (value is int i)
            return i;

        if (value is long l)
            return l;

        if (value is short s)
            return s;

        if (value is uint ui)
            return ui;

        if (value is ulong ul)
            return ul;

        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str))
            return 0m;

        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        try
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0m;
        }
    }

    private static decimal ParseDecimal(string? value, decimal defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Replace(".", "")
            .Replace(",", ".")
            .Replace(" ", string.Empty);

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return defaultValue;
    }

    /// <summary>
    /// Giá»‘ng <see cref="ParseDecimal"/> nhÆ°ng tráº£ vá» null khi Ã´ trá»‘ng hoáº·c khÃ´ng Ä‘á»c
    /// Ä‘Æ°á»£c sá»‘, Ä‘á»ƒ phÃ¢n biá»‡t "khÃ´ng cÃ³ dá»¯ liá»‡u" vá»›i giÃ¡ trá»‹ 0.
    /// </summary>
    private static decimal? ParseNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Replace(".", "")
            .Replace(",", ".")
            .Replace(" ", string.Empty);

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out var result))
            return result;

        return normalized is "1" or "yes" or "y" or "co" or "Ä‘Ãºng" ? true : defaultValue;
    }

    /// <summary>
    /// Äá»c Ã´ "Thá»i gian táº¡o" / "NgÃ y táº¡o" cá»§a file danh sÃ¡ch hÃ ng hÃ³a.
    /// <para>
    /// Danh sÃ¡ch Ä‘á»‹nh dáº¡ng lÃ  Cá» Äá»ŠNH, khÃ´ng nhá» vÃ o vÄƒn hÃ³a cá»§a mÃ¡y. Náº¿u Ä‘á»ƒ
    /// <c>DateTime.TryParse</c> vá»›i vÄƒn hÃ³a InvariantCulture cháº¡y trÆ°á»›c thÃ¬
    /// "05/09/2026" sáº½ bá»‹ hiá»ƒu theo thá»© tá»± thÃ¡ng/ngÃ y cá»§a vÄƒn hÃ³a Má»¹, ra
    /// 9 thÃ¡ng 5 â€” lá»‡ch so vá»›i 5 thÃ¡ng 9 mÃ  ngÆ°á»i dÃ¹ng nhÃ¬n tháº¥y trong file.
    /// </para>
    /// <para>
    /// Ã” trá»‘ng hoáº·c khÃ´ng nháº­n ra Ä‘Æ°á»£c thÃ¬ tráº£ vá» NULL â€” khÃ´ng bao giá» tráº£ vá»
    /// ngÃ y hiá»‡n táº¡i, Ä‘á»ƒ ngÆ°á»i dÃ¹ng biáº¿t dá»¯ liá»‡u cÅ© khÃ´ng cÃ³ giÃ¡ trá»‹ nÃ y.
    /// </para>
    /// </summary>
    private static DateTime? ParseNgayTaiFileSanPham(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Excel khÃ´ng lÆ°u mÃºi giá»: bá» háº­u tá»‘ Z / +07:00 náº¿u cÃ³ Ä‘á»ƒ giá»¯ Ä‘Ãºng
        // giá» ghi trong Ã´, tuyá»‡t Ä‘á»‘i khÃ´ng Ä‘á»•i theo mÃºi giá» cá»§a mÃ¡y.
        var text = BoHauToMuiGio(value.Trim());

        if (text.Length == 0)
            return null;

        string[] dinhDang =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd HH:mm:ss",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yy",
            "dd/MM/yy HH:mm",
            "dd-MM-yyyy",
            "dd-MM-yyyy HH:mm",
            "dd-MM-yyyy HH:mm:ss",
            "d/M/yyyy",
            "d/M/yyyy HH:mm",
            "d/M/yyyy HH:mm:ss"
        ];

        if (DateTime.TryParseExact(
                text,
                dinhDang,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var ngay))
        {
            return new DateTime(
                ngay.Year,
                ngay.Month,
                ngay.Day,
                ngay.Hour,
                ngay.Minute,
                ngay.Second,
                DateTimeKind.Unspecified);
        }

        // Ã” ngÃ y bá»‹ Excel/CSV lÆ°u thÃ nh sá»‘ serial (46287 = 20/09/2026).
        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var serial)
            && serial is > 1 and <= 2958465)
        {
            return DateTime.FromOADate(serial);
        }

        return ParseNgayTaiFile(text);
    }

    /// <summary>
    /// Bá» háº­u tá»‘ mÃºi giá» (Z, +07:00, -05:00) á»Ÿ cuá»‘i chuá»—i ngÃ y giá» Ä‘á»ƒ giá»¯ nguyÃªn
    /// giá» ghi trong file, khÃ´ng chuyá»ƒn Ä‘á»•i theo mÃºi giá» cá»§a mÃ¡y.
    /// </summary>
    private static string BoHauToMuiGio(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            return value[..^1].TrimEnd();

        for (var index = value.Length - 1; index > 0; index--)
        {
            var character = value[index];

            if (character is '+' or '-')
            {
                var phanCuoi = value[index..];

                if (phanCuoi.Length is 6 or 5
                    && phanCuoi[1] is >= '0' and <= '9'
                    && phanCuoi[2] is >= '0' and <= '9'
                    && (phanCuoi.Length == 5 || phanCuoi[3] == ':'))
                {
                    return value[..index].TrimEnd();
                }

                break;
            }

            if (!char.IsDigit(character) && character != ':')
                break;
        }

        return value;
    }

    /// <summary>
    /// Chuáº©n hÃ³a tÃªn cá»™t Ä‘á»ƒ so khá»›p giá»¯a tÃªn cá»™t trong file vÃ  tÃªn cá»™t trong code.
    /// Bá» dáº¥u, háº¡ chá»¯ thÆ°á»ng vÃ  loáº¡i bá» má»i kÃ½ tá»± phÃ¢n tÃ¡ch/Ä‘áº·c biá»‡t, ká»ƒ cáº£ dáº¥u
    /// ngoáº·c â€” vÃ¬ file thá»±c táº¿ cÃ³ cá»™t "NhÃ³m hÃ ng(3 Cáº¥p)" vÃ  "HÃ¬nh áº£nh (url1,url2...)"
    /// mÃ  khÃ´ng bá» ngoáº·c thÃ¬ tÃªn cá»™t khÃ´ng bao giá» khá»›p Ä‘Æ°á»£c vá»›i khoÃ¡ tra cá»©u.
    /// </summary>
    private static string NormalizeHeader(string value)
    {
        var normalized = value
            .Replace("\uFEFF", string.Empty)
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            // Chá»‰ giá»¯ láº¡i chá»¯ vÃ  sá»‘; bá» háº¿t dáº¥u cÃ¢u, ngoáº·c, khoáº£ng tráº¯ng.
            if (char.IsLetterOrDigit(character))
                builder.Append(FoldDangDe(char.ToLowerInvariant(character)));
        }

        return builder.ToString();
    }

    private static string GenerateProductCode()
    {
        return $"SP-{DateTime.Now:yyyyMMdd-HHmmssfff}";
    }

    private static string GenerateCustomerCode()
    {
        return $"KH{DateTime.Now:yyyyMMddHHmmssfff}";
    }

    /// <summary>
    /// Sinh mÃ£ khÃ¡ch hÃ ng tuáº§n tá»± theo dáº¡ng KH0000001.
    /// Chá»‰ xÃ©t cÃ¡c mÃ£ KH + sá»‘ Ä‘á»ƒ khÃ´ng bá»‹ áº£nh hÆ°á»Ÿng bá»Ÿi mÃ£ cÅ© khÃ¡c Ä‘á»‹nh dáº¡ng.
    /// </summary>
    public async Task<string> TaoMaKhachHangMoiAsync()
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_khach_hang, 3) AS UNSIGNED)),
            0
        ) + 1
        FROM khach_hang
        WHERE ma_khach_hang REGEXP '^KH[0-9]{1,7}$';
        """;

        var nextNumber = await connection.ExecuteScalarAsync<long>(sql);

        if (nextNumber > 9_999_999)
            throw new InvalidOperationException("ÄÃ£ vÆ°á»£t giá»›i háº¡n mÃ£ khÃ¡ch hÃ ng tá»± Ä‘á»™ng KH9999999.");

        return $"KH{nextNumber:D7}";
    }

    /// <summary>
    /// Sinh mÃ£ khÃ¡ch hÃ ng ná»™i bá»™ tuáº§n tá»± theo dáº¡ng NB000001 (hoáº·c KHNB0001).
    /// </summary>
    public async Task<string> TaoMaKhachHangNoiBoMoiAsync(string prefix = "NB")
    {
        await using var connection = await OpenAsync();

        var sql = $"""
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_khach_hang, {prefix.Length + 1}) AS UNSIGNED)),
            0
        ) + 1
        FROM khach_hang
        WHERE ma_khach_hang REGEXP '^{prefix}[0-9]+$';
        """;

        var nextNumber = await connection.ExecuteScalarAsync<long>(sql);
        if (nextNumber <= 0) nextNumber = 1;

        return $"{prefix}{nextNumber:D6}";
    }

    /// <summary>
    /// Äáº£m báº£o nhÃ³m "Ná»™i bá»™" tá»“n táº¡i trong báº£ng nhom_khach_hang Ä‘á»ƒ gÃ¡n hoáº·c lá»c Ä‘á»‘i tÃ¡c ná»™i bá»™.
    /// </summary>
    public async Task<NhomKhachHang> EnsureNhomKhachHangNoiBoAsync()
    {
        await using var connection = await OpenAsync();

        const string checkSql = """
        SELECT id AS Id, ten_nhom AS TenNhom, phan_tram_giam_gia AS PhanTramGiamGia, trang_thai AS TrangThai
        FROM nhom_khach_hang
        WHERE ten_nhom = 'Ná»™i bá»™'
        LIMIT 1;
        """;

        var existing = await connection.QueryFirstOrDefaultAsync<NhomKhachHang>(checkSql);
        if (existing is not null)
            return existing;

        // ma_nhom lÃ  cá»™t NOT NULL nÃªn báº¯t buá»™c pháº£i cÃ³ giÃ¡ trá»‹ khi thÃªm nhÃ³m má»›i.
        var maNhom = await SinhMaNhomKhachHangAsync(connection, "NHN");

        const string insertSql = """
        INSERT INTO nhom_khach_hang (ma_nhom, ten_nhom, phan_tram_giam_gia, ghi_chu, trang_thai)
        VALUES (@MaNhom, 'Ná»™i bá»™', 0, 'NhÃ³m khÃ¡ch hÃ ng / Ä‘á»‘i tÃ¡c ná»™i bá»™', 1);
        SELECT LAST_INSERT_ID();
        """;

        var id = await connection.ExecuteScalarAsync<uint>(insertSql, new { MaNhom = maNhom });
        return new NhomKhachHang
        {
            Id = id,
            TenNhom = "Ná»™i bá»™",
            PhanTramGiamGia = 0,
            TrangThai = true
        };
    }

    /// <summary>Sinh mÃ£ nhÃ³m khÃ¡ch hÃ ng káº¿ tiáº¿p theo dáº¡ng {prefix} + sá»‘ thá»© tá»±.</summary>
    private static async Task<string> SinhMaNhomKhachHangAsync(MySqlConnection connection, string prefix)
    {
        var nextNumber = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COALESCE(MAX(CAST(SUBSTRING(ma_nhom, 4) AS UNSIGNED)), 0) + 1
            FROM nhom_khach_hang
            WHERE ma_nhom REGEXP '^[A-Z]{3}[0-9]{1,8}$';
            """);

        if (nextNumber <= 0)
            nextNumber = 1;

        var code = $"{prefix}{nextNumber:D4}";
        while (await connection.ExecuteScalarAsync<int>(
                   "SELECT COUNT(1) FROM nhom_khach_hang WHERE ma_nhom = @Code",
                   new { Code = code }) > 0)
        {
            nextNumber++;
            code = $"{prefix}{nextNumber:D4}";
        }

        return code;
    }

    public async Task<IReadOnlyList<KhachHang>> GetKhachHangAsync(
        string? search = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT
            kh.id AS Id,
            kh.ma_khach_hang AS MaKhachHang,
            kh.ten_khach_hang AS TenKhachHang,
            kh.loai_khach_hang AS LoaiKhachHang,
            kh.so_dien_thoai AS SoDienThoai,
            kh.ngay_sinh AS NgaySinh,
            kh.gioi_tinh AS GioiTinh,
            kh.email AS Email,
            kh.dia_chi AS DiaChi,
            kh.phuong_xa AS PhuongXa,
            kh.quan_huyen AS QuanHuyen,
            kh.tinh_thanh AS TinhThanh,
            nkh.ten_nhom AS NhomKhachHang
            ,kh.ma_so_thue AS MaSoThue
            ,kh.ten_nguoi_mua_hoa_don AS TenNguoiMuaHoaDon
            ,kh.ten_cong_ty AS TenCongTy
            ,kh.dia_chi_xuat_hoa_don AS DiaChiXuatHoaDon
            ,kh.ghi_chu AS GhiChu
        FROM khach_hang kh
        LEFT JOIN nhom_khach_hang nkh
            ON nkh.id = kh.nhom_khach_hang_id
        WHERE kh.trang_thai = 1
          AND (
                @Search = ''
                OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.so_dien_thoai LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY kh.ten_khach_hang;
        """;

        return (
            await connection.QueryAsync<KhachHang>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }

    /// <summary>
    /// Kiá»ƒm tra sá»‘ Ä‘iá»‡n thoáº¡i Ä‘Ã£ tá»“n táº¡i trong khÃ¡ch hÃ ng Ä‘ang hoáº¡t Ä‘á»™ng chÆ°a.
    /// So sÃ¡nh sau khi bá» khoáº£ng tráº¯ng, dáº¥u gáº¡ch vÃ  dáº¥u cháº¥m
    /// Ä‘á»ƒ trÃ¡nh lÆ°u trÃ¹ng khi khÃ¡ch gÃµ khÃ¡c Ä‘á»‹nh dáº¡ng.
    /// </summary>
    public async Task<bool> KiemTraTrungSoDienThoaiAsync(
        string? soDienThoai,
        uint khachHangId = 0)
    {
        var sdt = (soDienThoai ?? "")
            .Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace(".", "");

        if (sdt.Length == 0)
            return false;

        await using var connection = await OpenAsync();

        const string sql = """
        SELECT COUNT(*)
        FROM khach_hang
        WHERE trang_thai = 1
          AND id <> @KhachHangId
          AND REPLACE(
                REPLACE(REPLACE(TRIM(so_dien_thoai), ' ', ''), '-', ''),
                '.', '')
              = @SoDienThoai;
        """;

        var soLuong = await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                SoDienThoai = sdt,
                KhachHangId = khachHangId
            });

        return soLuong > 0;
    }

    public async Task XoaKhachHangAsync(uint customerId)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a khÃ¡ch hÃ ng.");
        await using var connection = await OpenAsync();

        // Giá»¯ láº¡i lá»‹ch sá»­ hÃ³a Ä‘Æ¡n/cÃ´ng ná»£; danh sÃ¡ch khÃ¡ch hÃ ng chá»‰ hiá»ƒn thá»‹ báº£n ghi cÃ²n hoáº¡t Ä‘á»™ng.
        var affectedRows = await connection.ExecuteAsync(
            "UPDATE khach_hang SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id AND trang_thai = 1;",
            new { Id = customerId });

        if (affectedRows == 0)
            throw new InvalidOperationException("KhÃ¡ch hÃ ng khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ bá»‹ xÃ³a.");
    }

    /// <summary>
    /// Kiá»ƒm tra báº£ng lÆ°u phiáº¿u tráº£ hÃ ng cÃ³ tá»“n táº¡i trong database hay khÃ´ng
    /// (má»™t sá»‘ báº£n cÃ i cÅ© chÆ°a cÃ³ báº£ng nÃ y). Káº¿t quáº£ Ä‘Æ°á»£c ghi nhá»› Ä‘á»ƒ
    /// khÃ´ng pháº£i há»i láº¡i database á»Ÿ má»—i láº§n táº£i danh sÃ¡ch.
    /// </summary>
    private async Task<bool> HasBangTraHangAsync()
    {
        if (_hasBangTraHang.HasValue)
            return _hasBangTraHang.Value;

        try
        {
            await using var connection = await OpenAsync();

            const string sql = """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'phieu_tra_hang';
            """;

            var count = await connection.ExecuteScalarAsync<long>(sql);
            _hasBangTraHang = count > 0;
        }
        catch
        {
            _hasBangTraHang = false;
        }

        return _hasBangTraHang.Value;
    }

    /// <summary>
    /// Tá»•ng tiá»n tráº£ hÃ ng cá»§a má»™t khÃ¡ch hÃ ng, tÃ­nh theo biá»ƒu thá»©c Ä‘á»‹nh danh
    /// khÃ¡ch hÃ ng Ä‘ang cÃ³ trong cÃ¢u truy váº¥n gá»‘c (vÃ­ dá»¥ "kh.id" hoáº·c "@Id").
    /// Náº¿u chÆ°a cÃ³ báº£ng tráº£ hÃ ng thÃ¬ coi nhÆ° khÃ´ng cÃ³ giao dá»‹ch tráº£ hÃ ng nÃ o,
    /// khÃ´ng tá»± táº¡o sá»‘ liá»‡u.
    /// </summary>
    private static string TongTienTraHangSql(string idExpression, bool coBangTraHang)
        => coBangTraHang
            ? $"""
               COALESCE(
                   (
                       SELECT SUM(pt.tong_tien_tra)
                       FROM phieu_tra_hang pt
                       WHERE pt.khach_hang_id = {idExpression}
                         AND pt.trang_thai = 'HOAN_THANH'
                   ),
                   0
               )
               """
            : "0";

    /// <summary>
    /// Ba biá»ƒu thá»©c tiá»n cá»§a má»™t khÃ¡ch hÃ ng, dÃ¹ng chung cho báº£ng danh sÃ¡ch,
    /// thanh thá»‘ng kÃª vÃ  vÃ¹ng chi tiáº¿t Ä‘á»ƒ ba nÆ¡i luÃ´n hiá»ƒn thá»‹ cÃ¹ng má»™t sá»‘.
    /// Æ¯u tiÃªn sá»‘ tiá»n Ä‘Ã£ nháº­p tá»« file KiotViet, khÃ´ng cÃ³ thÃ¬ tÃ­nh tá»«
    /// dá»¯ liá»‡u bÃ¡n hÃ ng, cÃ´ng ná»£ vÃ  tráº£ hÃ ng cá»§a pháº§n má»m.
    /// </summary>
    private static (string NoHienTai, string TongBan, string TongBanTruTraHang) TaoBieuThucTienKhachHang(
        bool coBangTraHang,
        string idExpression,
        bool coCotKiotViet)
    {
        var tienNoiBo = $"""
            COALESCE(
                (
                    SELECT SUM(ls.ghi_no - ls.ghi_co)
                    FROM lich_su_cong_no_khach_hang ls
                    WHERE ls.khach_hang_id = {idExpression}
                ),
                (
                    SELECT SUM(hdcn.con_no)
                    FROM hoa_don_ban_hang hdcn
                    WHERE hdcn.khach_hang_id = {idExpression}
                      AND hdcn.trang_thai_hoa_don = 'HOAN_THANH'
                ),
                0
            )
            """;

        var tienBanNoiBo = $"""
            COALESCE(
                (
                    SELECT SUM(hb.khach_can_tra)
                    FROM hoa_don_ban_hang hb
                    WHERE hb.khach_hang_id = {idExpression}
                      AND hb.trang_thai_hoa_don = 'HOAN_THANH'
                ),
                0
            )
            """;

        var tienBanTruTraNoiBo =
            $"""
             {tienBanNoiBo} - {TongTienTraHangSql(idExpression, coBangTraHang)}
             """;

        if (!coCotKiotViet)
            return (tienNoiBo, tienBanNoiBo, tienBanTruTraNoiBo);

        return (
            $"CASE WHEN kh.co_du_lieu_kiotviet = 1 THEN (COALESCE(kh.no_can_thu_hien_tai, 0) + {tienNoiBo}) ELSE {tienNoiBo} END",
            $"CASE WHEN kh.co_du_lieu_kiotviet = 1 THEN (COALESCE(kh.tong_ban, 0) + {tienBanNoiBo}) ELSE {tienBanNoiBo} END",
            $"CASE WHEN kh.co_du_lieu_kiotviet = 1 THEN (COALESCE(kh.tong_ban_tru_tra_hang, 0) + {tienBanTruTraNoiBo}) ELSE {tienBanTruTraNoiBo} END");
    }

    /// <summary>
    /// Ba cá»™t tiá»n cá»§a báº£ng danh sÃ¡ch khÃ¡ch hÃ ng, dÃ¹ng subquery tÆ°Æ¡ng quan theo
    /// kh.id nÃªn chá»‰ tÃ­nh cho cÃ¡c dÃ²ng thuá»™c trang Ä‘ang xem thay vÃ¬ gom nhÃ³m toÃ n báº£ng.
    /// </summary>
    private static string TienTruongKhachHangSql(bool coBangTraHang, bool coCotKiotViet)
    {
        var tien = TaoBieuThucTienKhachHang(coBangTraHang, "kh.id", coCotKiotViet);

        return $"""
                {tien.NoHienTai} AS NoHienTai,
                {tien.TongBan} AS TongBan,
                {tien.TongBanTruTraHang} AS TongBanTruTraHang
                """;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng khach_hang cÃ³ Ä‘á»§ ba cá»™t phá»¥c vá»¥ viá»‡c nháº­p tá»« file KiotViet.
    /// CÃ¡c cá»™t nÃ y Ä‘Æ°á»£c thÃªm má»™t láº§n duy nháº¥t, khÃ´ng Ä‘á»¥ng tá»›i dá»¯ liá»‡u sáºµn cÃ³:
    /// co_du_lieu_kiotviet Ä‘Ã¡nh dáº¥u báº£n ghi cÃ³ sá»‘ tiá»n láº¥y tá»« file,
    /// nguoi_tao_ten giá»¯ tÃªn ngÆ°á»i táº¡o ghi trong file, ngay_nhap_excel Ä‘á»ƒ truy váº¿t.
    /// Náº¿u thÃªm khÃ´ng Ä‘Æ°á»£c (database cÅ©, khÃ´ng Ä‘á»§ quyá»n) thÃ¬ pháº§n má»m váº«n cháº¡y
    /// bÃ¬nh thÆ°á»ng vÃ  chá»‰ dÃ¹ng sá»‘ tiá»n tÃ­nh tá»« hÃ³a Ä‘Æ¡n, cÃ´ng ná»£, tráº£ hÃ ng.
    /// </summary>
    private async Task<bool> DamBaoCotDuLieuKiotVietAsync()
    {
        if (_coCotDuLieuKiotViet.HasValue)
            return _coCotDuLieuKiotViet.Value;

        const string kiemTraSql = """
            SELECT COLUMN_NAME
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'khach_hang'
              AND COLUMN_NAME IN ('co_du_lieu_kiotviet', 'nguoi_tao_ten', 'ngay_nhap_excel');
            """;

        // ThÃªm tá»«ng cá»™t má»™t vÃ¬ báº£ng cÅ© cÃ³ thá»ƒ Ä‘Ã£ cÃ³ sáºµn má»™t trong ba cá»™t.
        var cotCanThem = new (string Ten, string DinhNghia)[]
        {
            ("co_du_lieu_kiotviet", "TINYINT(1) NOT NULL DEFAULT 0"),
            ("nguoi_tao_ten", "VARCHAR(150) NULL"),
            ("ngay_nhap_excel", "DATETIME NULL")
        };

        try
        {
            await using var connection = await OpenAsync();

            var daCo = (await connection.QueryAsync<string>(kiemTraSql)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (ten, dinhNghia) in cotCanThem)
            {
                if (daCo.Contains(ten))
                    continue;

                await connection.ExecuteAsync($"ALTER TABLE khach_hang ADD COLUMN {ten} {dinhNghia};");
            }

            _coCotDuLieuKiotViet = true;
        }
        catch
        {
            // KhÃ´ng thÃªm Ä‘Æ°á»£c cá»™t thÃ¬ dÃ¹ng sá»‘ liá»‡u tÃ­nh tá»« hÃ³a Ä‘Æ¡n, cÃ´ng ná»£, tráº£ hÃ ng.
            _coCotDuLieuKiotViet = false;
        }

        return _coCotDuLieuKiotViet.Value;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng nha_cung_cap cÃ³ Ä‘á»§ cÃ¡c cá»™t phá»¥c vá»¥ viá»‡c nháº­p tá»« file danh sÃ¡ch
    /// nhÃ  cung cáº¥p. ÄÃ¢y lÃ  cá»™t Má»I theo Ä‘Ãºng cÃ¡ch Ä‘Ã£ lÃ m vá»›i báº£ng khach_hang:
    /// <list type="bullet">
    /// <item>co_du_lieu_kiotviet Ä‘Ã¡nh dáº¥u báº£n ghi cÃ³ sá»‘ tiá»n láº¥y tá»« file,
    /// nguoi_tao_ten giá»¯ ngÆ°á»i táº¡o ghi trong file, ngay_nhap_excel Ä‘á»ƒ truy váº¿t;</item>
    /// <item>no_can_tra_hien_tai, tong_mua, tong_mua_tru_tra_hang lÆ°u ba cá»™t tiá»n
    /// cá»§a file, khÃ´ng cÃ³ cá»™t nÃ y thÃ¬ khÃ´ng thá»ƒ giá»¯ sá»‘ liá»‡u sau khi nháº­p;</item>
    /// <item>khu_vuc, so_cccd, nhom_nha_cung_cap_ten, ten_cong_ty lÆ°u cÃ¡c cá»™t chá»¯
    /// cÃ²n láº¡i cá»§a file mÃ  báº£ng cÅ© chÆ°a cÃ³ chá»— Ä‘á»ƒ lÆ°u.</item>
    /// </list>
    /// CÃ¡c cá»™t Ä‘Æ°á»£c thÃªm má»™t láº§n duy nháº¥t, khÃ´ng Ä‘á»¥ng tá»›i dá»¯ liá»‡u sáºµn cÃ³. Náº¿u thÃªm
    /// khÃ´ng Ä‘Æ°á»£c (database cÅ©, khÃ´ng Ä‘á»§ quyá»n) thÃ¬ pháº§n má»m váº«n cháº¡y bÃ¬nh thÆ°á»ng
    /// vÃ  chá»‰ dÃ¹ng sá»‘ tiá»n tÃ­nh tá»« phiáº¿u nháº­p hÃ ng vÃ  sá»• cÃ¡i cÃ´ng ná»£.
    /// </summary>
    private async Task<bool> DamBaoCotDuLieuKiotVietNhaCungCapAsync()
    {
        if (_coCotDuLieuKiotVietNhaCungCap.HasValue)
            return _coCotDuLieuKiotVietNhaCungCap.Value;

        const string kiemTraSql = """
            SELECT COLUMN_NAME
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'nha_cung_cap'
              AND COLUMN_NAME IN (
                  'co_du_lieu_kiotviet', 'nguoi_tao_ten', 'ngay_nhap_excel',
                  'no_can_tra_hien_tai', 'tong_mua', 'tong_mua_tru_tra_hang',
                  'khu_vuc', 'so_cccd', 'nhom_nha_cung_cap_ten', 'ten_cong_ty'
              );
            """;

        // ThÃªm tá»«ng cá»™t má»™t vÃ¬ báº£ng cÅ© cÃ³ thá»ƒ Ä‘Ã£ cÃ³ sáºµn má»™t trong cÃ¡c cá»™t nÃ y.
        var cotCanThem = new (string Ten, string DinhNghia)[]
        {
            ("co_du_lieu_kiotviet", "TINYINT(1) NOT NULL DEFAULT 0"),
            ("nguoi_tao_ten", "VARCHAR(150) NULL"),
            ("ngay_nhap_excel", "DATETIME NULL"),
            ("no_can_tra_hien_tai", "DECIMAL(15,2) NULL"),
            ("tong_mua", "DECIMAL(15,2) NULL"),
            ("tong_mua_tru_tra_hang", "DECIMAL(15,2) NULL"),
            ("khu_vuc", "VARCHAR(100) NULL"),
            ("so_cccd", "VARCHAR(50) NULL"),
            ("nhom_nha_cung_cap_ten", "VARCHAR(150) NULL"),
            ("ten_cong_ty", "VARCHAR(200) NULL")
        };

        try
        {
            await using var connection = await OpenAsync();

            var daCo = (await connection.QueryAsync<string>(kiemTraSql))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (ten, dinhNghia) in cotCanThem)
            {
                if (daCo.Contains(ten))
                    continue;

                await connection.ExecuteAsync(
                    $"ALTER TABLE nha_cung_cap ADD COLUMN {ten} {dinhNghia};");
            }

            _coCotDuLieuKiotVietNhaCungCap = true;
        }
        catch
        {
            // KhÃ´ng thÃªm Ä‘Æ°á»£c cá»™t thÃ¬ dÃ¹ng sá»‘ liá»‡u tÃ­nh tá»« phiáº¿u nháº­p hÃ ng, sá»• cÃ¡i cÃ´ng ná»£.
            _coCotDuLieuKiotVietNhaCungCap = false;
        }

        return _coCotDuLieuKiotVietNhaCungCap.Value;
    }

    /// <summary>
    /// Ba biá»ƒu thá»©c tiá»n cá»§a má»™t nhÃ  cung cáº¥p, dÃ¹ng chung cho báº£ng danh sÃ¡ch,
    /// thanh thá»‘ng kÃª, bá»™ lá»c cÃ´ng ná»£ vÃ  vÃ¹ng chi tiáº¿t Ä‘á»ƒ ba nÆ¡i luÃ´n hiá»ƒn thá»‹
    /// cÃ¹ng má»™t sá»‘. Æ¯u tiÃªn sá»‘ tiá»n Ä‘Ã£ nháº­p tá»« file danh sÃ¡ch, khÃ´ng cÃ³ thÃ¬
    /// tÃ­nh tá»« dá»¯ liá»‡u nháº­p hÃ ng vÃ  cÃ´ng ná»£ cá»§a pháº§n má»m.
    /// </summary>
    private static (string NoHienTai, string TongMua, string TongMuaTruTraHang)
        TaoBieuThucTienNhaCungCap(string idExpression, bool coCotKiotViet)
    {
        // Sá»• cÃ¡i cÃ´ng ná»£ lÃ  nguá»“n cÃ´ng ná»£ chÃ­nh, giá»‘ng há»‡t cá»™t "CÃ²n ná»£" vÃ 
        // cá»™t "CÃ´ng ná»£ hiá»‡n táº¡i" cá»§a vÃ¹ng chi tiáº¿t.
        // Náº¿u chÆ°a cÃ³ dÃ²ng sá»• cÃ¡i nÃ o, tá»± Ä‘á»™ng fallback sang tá»•ng cÃ²n ná»£ cá»§a phiáº¿u nháº­p.
        var tienNoiBo = $"""
            COALESCE(
                (
                    SELECT SUM(ls.ghi_no - ls.ghi_co)
                    FROM lich_su_cong_no_nha_cung_cap ls
                    WHERE ls.nha_cung_cap_id = {idExpression}
                ),
                (
                    SELECT SUM(p_cn.con_no)
                    FROM phieu_nhap_hang p_cn
                    WHERE p_cn.nha_cung_cap_id = {idExpression}
                      AND p_cn.trang_thai NOT IN ('LUU_TAM', 'HUY')
                ),
                0
            )
            """;

        // Tá»•ng mua láº¥y tá»« cÃ¡c phiáº¿u nháº­p hÃ ng há»£p lá»‡ (DA_NHAP_HANG hoáº·c HOAN_THANH).
        var tienMuaNoiBo = $"""
            COALESCE(
                (
                    SELECT SUM(p.tong_tien_nhap)
                    FROM phieu_nhap_hang p
                    WHERE p.nha_cung_cap_id = {idExpression}
                      AND p.trang_thai NOT IN ('LUU_TAM', 'HUY')
                ),
                0
            )
            """;

        // Pháº§n má»m chÆ°a cÃ³ phiáº¿u tráº£ hÃ ng cho nhÃ  cung cáº¥p, nÃªn tá»•ng mua trá»« tráº£
        // hÃ ng tÃ­nh tá»« ná»™i bá»™ báº±ng chÃ­nh tá»•ng mua. Khi cÃ³ dá»¯ liá»‡u file thÃ¬ láº¥y
        // sá»‘ cá»§a file vÃ¬ file Ä‘Ã£ trá»« sáºµn cÃ¡c phiáº¿u tráº£ hÃ ng.
        var tienMuaTruTraNoiBo = tienMuaNoiBo;

        if (!coCotKiotViet)
            return (tienNoiBo, tienMuaNoiBo, tienMuaTruTraNoiBo);

        return (
            $"CASE WHEN ncc.co_du_lieu_kiotviet = 1 OR ncc.no_can_tra_hien_tai IS NOT NULL THEN (COALESCE(ncc.no_can_tra_hien_tai, 0) + {tienNoiBo}) ELSE {tienNoiBo} END",
            $"CASE WHEN ncc.co_du_lieu_kiotviet = 1 OR ncc.tong_mua IS NOT NULL THEN (COALESCE(ncc.tong_mua, 0) + {tienMuaNoiBo}) ELSE {tienMuaNoiBo} END",
            $"CASE WHEN ncc.co_du_lieu_kiotviet = 1 OR ncc.tong_mua_tru_tra_hang IS NOT NULL THEN (COALESCE(ncc.tong_mua_tru_tra_hang, 0) + {tienMuaTruTraNoiBo}) ELSE {tienMuaTruTraNoiBo} END");
    }

    /// <summary>Láº¥y danh sÃ¡ch khÃ¡ch hÃ ng theo trang cho mÃ n hÃ¬nh "Danh sÃ¡ch khÃ¡ch hÃ ng".
    /// Chá»‰ Ä‘á»c dá»¯ liá»‡u cá»§a trang hiá»‡n táº¡i (LIMIT/OFFSET) kÃ¨m tá»•ng sá»‘ báº£n ghi,
    /// nÃªn giao diá»‡n váº«n mÆ°á»£t khi báº£ng cÃ³ hÃ ng nghÃ¬n khÃ¡ch hÃ ng.
    /// </summary>
    /// <param name="dayDuThongTin">
    /// false: láº¥y Ä‘Ãºng cÃ¡c cá»™t cá»§a báº£ng danh sÃ¡ch (mÃ£, tÃªn, Ä‘iá»‡n thoáº¡i, email,
    /// Ä‘á»‹a chá»‰, nhÃ³m, ngÃ y táº¡o) kÃ¨m ná»£ hiá»‡n táº¡i, tá»•ng bÃ¡n vÃ  tá»•ng bÃ¡n trá»« tráº£ hÃ ng.
    /// true: láº¥y Ä‘áº§y Ä‘á»§ má»i cá»™t Ä‘á»ƒ dÃ¹ng cho form nháº­p/sá»­a.
    /// </param>
    public async Task<(IReadOnlyList<KhachHang> Items, int TongSoBanGhi)> GetKhachHangPagedAsync(
        string? search,
        int page,
        int pageSize,
        bool dayDuThongTin = false,
        uint nhomKhachHangId = 0,
        string? congNoFilter = null)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        var offset = (safePage - 1) * safePageSize;
        var keyword = (search ?? "").Trim();

        var coBangTraHang = await HasBangTraHangAsync();
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietAsync();

        // Bá»™ lá»c nhÃ³m khÃ¡ch hÃ ng vÃ  tÃ¬nh tráº¡ng cÃ´ng ná»£, dÃ¹ng cÃ¹ng biá»ƒu thá»©c
        // NoHienTai vá»›i cá»™t tiá»n trong báº£ng nÃªn sá»‘ liá»‡u lá»c luÃ´n khá»›p.
        var noHienTai = TaoBieuThucTienKhachHang(coBangTraHang, "kh.id", coCotKiotViet).NoHienTai;
        var dieuKienNhom = nhomKhachHangId == 0
            ? ""
            : "AND kh.nhom_khach_hang_id = @NhomKhachHangId ";
        var dieuKienCongNo = (congNoFilter ?? "").Trim() switch
        {
            "conno" => $"AND COALESCE({noHienTai}, 0) > 0 ",
            "khongconno" => $"AND COALESCE({noHienTai}, 0) <= 0 ",
            _ => ""
        };

        var countSql = $"""
        SELECT COUNT(*)
        FROM khach_hang kh
        WHERE kh.trang_thai = 1
          {dieuKienNhom}{dieuKienCongNo}
          AND (
                @Search = ''
                OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.so_dien_thoai LIKE CONCAT('%', @Search, '%')
              );
        """;

        var columns = dayDuThongTin
            ? $"""
                  kh.id AS Id,
                  kh.ma_khach_hang AS MaKhachHang,
                  kh.ten_khach_hang AS TenKhachHang,
                  kh.loai_khach_hang AS LoaiKhachHang,
                  kh.so_dien_thoai AS SoDienThoai,
                  kh.ngay_sinh AS NgaySinh,
                  kh.gioi_tinh AS GioiTinh,
                  kh.email AS Email,
                  kh.dia_chi AS DiaChi,
                  kh.phuong_xa AS PhuongXa,
                  kh.quan_huyen AS QuanHuyen,
                  kh.tinh_thanh AS TinhThanh,
                  kh.nhom_khach_hang_id AS NhomKhachHangId,
                  kh.ma_so_thue AS MaSoThue,
                  kh.ten_nguoi_mua_hoa_don AS TenNguoiMuaHoaDon,
                  kh.ten_cong_ty AS TenCongTy,
                  kh.dia_chi_xuat_hoa_don AS DiaChiXuatHoaDon,
                  kh.ghi_chu AS GhiChu,
                  kh.ngay_tao AS NgayTao,
                  kh.ngay_cap_nhat AS NgayCapNhat,
                  nkh.ten_nhom AS NhomKhachHang
              """
            : $"""
                  kh.id AS Id,
                  kh.ma_khach_hang AS MaKhachHang,
                  kh.ten_khach_hang AS TenKhachHang,
                  kh.so_dien_thoai AS SoDienThoai,
                  kh.email AS Email,
                  COALESCE(
                      NULLIF(TRIM(kh.dia_chi), ''),
                      NULLIF(
                          CONCAT_WS(
                              ', ',
                              NULLIF(TRIM(kh.phuong_xa), ''),
                              NULLIF(TRIM(kh.quan_huyen), ''),
                              NULLIF(TRIM(kh.tinh_thanh), '')
                          ),
                          ''
                      )
                  ) AS DiaChi,
                  kh.ngay_tao AS NgayTao,
                  nkh.ten_nhom AS NhomKhachHang,
                  {TienTruongKhachHangSql(coBangTraHang, coCotKiotViet)}
              """;

        var pageSql = $"""
        SELECT
            {columns}
        FROM khach_hang kh
        LEFT JOIN nhom_khach_hang nkh
            ON nkh.id = kh.nhom_khach_hang_id
        WHERE kh.trang_thai = 1
          {dieuKienNhom}{dieuKienCongNo}
          AND (
                @Search = ''
                OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.so_dien_thoai LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY kh.ten_khach_hang, kh.id
        LIMIT @PageSize OFFSET @Offset;
        """;

        await using var connection = await OpenAsync();

        var parameters = new
        {
            Search = keyword,
            PageSize = safePageSize,
            Offset = offset,
            NhomKhachHangId = nhomKhachHangId
        };

        var tongSoBanGhi = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        if (tongSoBanGhi == 0)
            return (Array.Empty<KhachHang>(), 0);

        var items = (
            await connection.QueryAsync<KhachHang>(pageSql, parameters)
        ).AsList();

        return (items, tongSoBanGhi);
    }

    /// <summary>Láº¥y Ä‘áº§y Ä‘á»§ thÃ´ng tin má»™t khÃ¡ch hÃ ng Ä‘á»ƒ má»Ÿ vÃ¹ng chi tiáº¿t / form sá»­a.</summary>
    public async Task<KhachHang?> GetKhachHangByIdAsync(uint customerId)
    {
        const string sql = """
        SELECT
            kh.id AS Id,
            kh.ma_khach_hang AS MaKhachHang,
            kh.ten_khach_hang AS TenKhachHang,
            kh.loai_khach_hang AS LoaiKhachHang,
            kh.so_dien_thoai AS SoDienThoai,
            kh.ngay_sinh AS NgaySinh,
            kh.gioi_tinh AS GioiTinh,
            kh.email AS Email,
            kh.dia_chi AS DiaChi,
            kh.phuong_xa AS PhuongXa,
            kh.quan_huyen AS QuanHuyen,
            kh.tinh_thanh AS TinhThanh,
            kh.nhom_khach_hang_id AS NhomKhachHangId,
            kh.ma_so_thue AS MaSoThue,
            kh.ten_nguoi_mua_hoa_don AS TenNguoiMuaHoaDon,
            kh.ten_cong_ty AS TenCongTy,
            kh.dia_chi_xuat_hoa_don AS DiaChiXuatHoaDon,
            kh.ghi_chu AS GhiChu,
            kh.ngay_tao AS NgayTao,
            kh.ngay_cap_nhat AS NgayCapNhat,
            nkh.ten_nhom AS NhomKhachHang
        FROM khach_hang kh
        LEFT JOIN nhom_khach_hang nkh
            ON nkh.id = kh.nhom_khach_hang_id
        WHERE kh.trang_thai = 1
          AND kh.id = @Id;
        """;

        await using var connection = await OpenAsync();

        return await connection.QueryFirstOrDefaultAsync<KhachHang>(
            sql,
            new { Id = customerId });
    }

    /// <summary>
    /// Sá»‘ ngÃ y dÃ¹ng Ä‘á»ƒ Ä‘áº¿m "KhÃ¡ch hÃ ng má»›i" khi giao diá»‡n khÃ´ng chá»‰ Ä‘á»‹nh.
    /// Giá»¯ á»Ÿ Ä‘Ã¢y Ä‘á»ƒ query vÃ  chá»¯ dÆ°á»›i tháº» thá»‘ng kÃª cÃ¹ng má»™t con sá»‘.
    /// </summary>
    public const int SoNgayKhachMoiMacDinh = 30;

    /// <summary>
    /// Sá»‘ liá»‡u tá»•ng quan cho thanh thá»‘ng kÃª phÃ­a trÃªn báº£ng danh sÃ¡ch khÃ¡ch hÃ ng.
    /// Cáº£ bá»‘n chá»‰ sá»‘ Ä‘á»u tÃ­nh tháº³ng tá»« database, khÃ´ng láº¥y tá»« cÃ¡c dÃ²ng Ä‘ang hiá»ƒn thá»‹:
    /// <list type="bullet">
    /// <item>Tá»•ng khÃ¡ch hÃ ng = COUNT(*) cá»§a khach_hang cÃ³ trang_thai = 1.</item>
    /// <item>KhÃ¡ch hÃ ng má»›i = sá»‘ khÃ¡ch hÃ ng Ä‘ang hoáº¡t Ä‘á»™ng cÃ³ ngay_tao náº±m trong
    /// <paramref name="soNgay"/> ngÃ y gáº§n nháº¥t (máº·c Ä‘á»‹nh 30 ngÃ y), tÃ­nh báº±ng
    /// DATE_SUB(NOW(), INTERVAL ...) chá»© khÃ´ng pháº£i COUNT toÃ n bá»™ báº£ng.</item>
    /// <item>Ná»£ hiá»‡n táº¡i = SUM cá»§a biá»ƒu thá»©c ná»£ cá»§a tá»«ng khÃ¡ch hÃ ng, Æ°u tiÃªn sá»• cÃ¡i
    /// cÃ´ng ná»£ (SUM(ghi_no - ghi_co)), khÃ´ng cÃ³ sá»• cÃ¡i thÃ¬ láº¥y SUM(con_no) cá»§a
    /// hÃ³a Ä‘Æ¡n Ä‘Ã£ hoÃ n thÃ nh.</item>
    /// <item>Tá»•ng bÃ¡n = SUM cá»§a biá»ƒu thá»©c bÃ¡n cá»§a tá»«ng khÃ¡ch hÃ ng, tá»©c tá»•ng
    /// khach_can_tra cá»§a cÃ¡c hÃ³a Ä‘Æ¡n Ä‘Ã£ hoÃ n thÃ nh (Ä‘Ã£ trá»« giáº£m giÃ¡, chÆ°a trá»« tráº£ hÃ ng).</item>
    /// </list>
    /// Nhá» váº­y tháº» thá»‘ng kÃª vÃ  tá»•ng cÃ¡c dÃ²ng trong báº£ng luÃ´n dÃ¹ng chung má»™t quy táº¯c.
    /// </summary>
    public async Task<ThongKeDanhSachKhachHang>
        GetThongKeDanhSachKhachHangAsync(int soNgay = SoNgayKhachMoiMacDinh)
    {
        var coBangTraHang = await HasBangTraHangAsync();
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietAsync();
        var tien = TaoBieuThucTienKhachHang(coBangTraHang, "kh.id", coCotKiotViet);

        var sql = $"""
        SELECT
            COUNT(*) AS TongSoKhachHang,
            COALESCE(
                SUM(
                    CASE
                        WHEN kh.ngay_tao IS NOT NULL
                             AND kh.ngay_tao >= DATE_SUB(NOW(), INTERVAL @SoNgay DAY)
                        THEN 1
                        ELSE 0
                    END
                ),
                0
            ) AS SoKhachMoi,
            COALESCE(SUM(CASE WHEN kh.loai_khach_hang = 'DOAN' OR kh.loai_khach_hang = 'DOANH_NGHIEP' THEN 1 ELSE 0 END), 0) AS SoKhachDoanhNghiep,
            COALESCE(SUM(CASE WHEN kh.loai_khach_hang = 'CA_NHAN' OR kh.loai_khach_hang IS NULL OR kh.loai_khach_hang = '' THEN 1 ELSE 0 END), 0) AS SoKhachCaNhan,
            COALESCE(SUM({tien.NoHienTai}), 0) AS TongNoHienTai,
            COALESCE(SUM({tien.TongBan}), 0) AS TongBan
        FROM khach_hang kh
        WHERE kh.trang_thai = 1;
        """;

        await using var connection = await OpenAsync();

        var row = await connection.QueryFirstOrDefaultAsync<ThongKeDanhSachKhachHang>(
            sql,
            new { SoNgay = soNgay < 1 ? SoNgayKhachMoiMacDinh : soNgay });

        return row ?? new ThongKeDanhSachKhachHang();


    }

    /// <summary>Danh sÃ¡ch nhÃ³m khÃ¡ch hÃ ng Ä‘ang hoáº¡t Ä‘á»™ng, dÃ¹ng cho ComboBox trong form nháº­p/sá»­a.</summary>
    public async Task<IReadOnlyList<NhomKhachHang>> GetNhomKhachHangAsync()
    {
        const string sql = """
        SELECT
            id AS Id,
            ten_nhom AS TenNhom,
            phan_tram_giam_gia AS PhanTramGiamGia,
            trang_thai AS TrangThai
        FROM nhom_khach_hang
        WHERE trang_thai = 1
        ORDER BY ten_nhom;
        """;

        await using var connection = await OpenAsync();

        return (
            await connection.QueryAsync<NhomKhachHang>(sql)
        ).AsList();
    }

    /// <summary>
    /// Lá»‹ch sá»­ hÃ³a Ä‘Æ¡n cá»§a má»™t khÃ¡ch hÃ ng (má»›i nháº¥t trÆ°á»›c).
    /// Chá»‰ Ä‘á»c tá»‘i Ä‘a <paramref name="gioiHan"/> dÃ²ng, khÃ´ng quÃ©t toÃ n bá»™ báº£ng hÃ³a Ä‘Æ¡n.
    /// </summary>
    public async Task<IReadOnlyList<HoaDon>> GetLichSuMuaHangCuaKhachHangAsync(
        uint customerId,
        int gioiHan = 50)
    {
        var limit = gioiHan is < 1 or > 200 ? 50 : gioiHan;

        const string sql = """
        SELECT
            hd.id AS Id,
            hd.ma_hoa_don AS MaHoaDon,
            hd.thoi_gian_ban AS ThoiGianBan,
            COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'KhÃ¡ch láº»') AS TenKhachHang,
            hd.tong_tien_hang AS TongTienHang,
            hd.giam_gia AS GiamGia,
            hd.thu_khac AS ThuKhac,
            hd.khach_can_tra AS KhachCanTra,
            hd.khach_da_thanh_toan AS KhachDaThanhToan,
            hd.con_no AS ConNo,
            hd.trang_thai_thanh_toan AS TrangThaiThanhToan
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh
            ON kh.id = hd.khach_hang_id
        WHERE hd.khach_hang_id = @Id
        ORDER BY hd.thoi_gian_ban DESC, hd.id DESC
        LIMIT @Limit;
        """;

        await using var connection = await OpenAsync();

        return (
            await connection.QueryAsync<HoaDon>(
                sql,
                new
                {
                    Id = customerId,
                    Limit = limit
                })
        ).AsList();
    }

    /// <summary>
    /// Tá»•ng há»£p tá»•ng tiá»n Ä‘Ã£ mua vÃ  cÃ´ng ná»£ hiá»‡n táº¡i cá»§a khÃ¡ch hÃ ng.
    /// CÃ¡c sá»‘ tiá»n láº¥y Ä‘Ãºng theo cÃ¹ng quy táº¯c vá»›i báº£ng danh sÃ¡ch: Æ°u tiÃªn sá»‘ tiá»n
    /// Ä‘Ã£ nháº­p tá»« file KiotViet, khÃ´ng cÃ³ thÃ¬ tÃ­nh tá»« hÃ³a Ä‘Æ¡n, sá»• cÃ¡i cÃ´ng ná»£ vÃ 
    /// phiáº¿u tráº£ hÃ ng. HÃ³a Ä‘Æ¡n chá»‰ tÃ­nh loáº¡i Ä‘Ã£ hoÃ n thÃ nh.
    /// </summary>
    public async Task<ThongKeMuaHangKhachHang> GetThongKeMuaHangCuaKhachHangAsync(uint customerId)
    {
        var coBangTraHang = await HasBangTraHangAsync();
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietAsync();
        var tien = TaoBieuThucTienKhachHang(coBangTraHang, "kh.id", coCotKiotViet);

        var sql = $"""
        SELECT
            (
                SELECT COUNT(*)
                FROM hoa_don_ban_hang
                WHERE khach_hang_id = @Id
                  AND trang_thai_hoa_don = 'HOAN_THANH'
            ) AS SoHoaDon,
            (
                SELECT COALESCE(SUM(khach_can_tra), 0)
                FROM hoa_don_ban_hang
                WHERE khach_hang_id = @Id
                  AND trang_thai_hoa_don = 'HOAN_THANH'
            ) AS TongTienDaMuaHoaDon,
            (
                SELECT COALESCE(SUM(khach_da_thanh_toan), 0)
                FROM hoa_don_ban_hang
                WHERE khach_hang_id = @Id
                  AND trang_thai_hoa_don = 'HOAN_THANH'
            ) AS TongDaThanhToan,
            (
                SELECT COALESCE(SUM(con_no), 0)
                FROM hoa_don_ban_hang
                WHERE khach_hang_id = @Id
                  AND trang_thai_hoa_don = 'HOAN_THANH'
            ) AS TongConNoHoaDon,
            (
                SELECT COUNT(*)
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @Id
            ) AS SoLanGhiCongNo,
            (
                SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @Id
            ) AS TongCongNoLichSu,
            (
                SELECT MAX(thoi_gian)
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @Id
            ) AS LanGhiCongNoGanNhat,
            -- Sá»‘ tiá»n hiá»ƒn thá»‹: khi cÃ³ dá»¯ liá»‡u file KiotViet thÃ¬ láº¥y tá»« file,
            -- ngÆ°á»£c láº¡i láº¥y tá»« hÃ³a Ä‘Æ¡n/sá»• cÃ¡i/tráº£ hÃ ng nhÆ° trÆ°á»›c.
            {tien.TongBan} AS TongTienDaMua,
            {tien.NoHienTai} AS TongConNoHienTai,
            {tien.TongBanTruTraHang} AS TongBanTruTraHang
        FROM khach_hang kh
        WHERE kh.id = @Id;
        """;

        await using var connection = await OpenAsync();

        var thongKe = await connection.QueryFirstOrDefaultAsync<ThongKeMuaHangKhachHang>(
                          sql,
                          new { Id = customerId })
                      ?? new ThongKeMuaHangKhachHang();

        return thongKe;
    }

    public async Task<uint> ThemKhachHangAsync(KhachHang customer)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm khÃ¡ch hÃ ng.");
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(customer.MaKhachHang))
            throw new ArgumentException("MÃ£ khÃ¡ch hÃ ng khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

        if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
            throw new ArgumentException("TÃªn khÃ¡ch hÃ ng khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

        // Kiá»ƒm tra trÃ¹ng mÃ£ khÃ¡ch hÃ ng
        const string checkCodeSql = """
        SELECT COUNT(*)
        FROM khach_hang
        WHERE ma_khach_hang = @MaKhachHang
          AND trang_thai = 1;
        """;

        var exists = await connection.ExecuteScalarAsync<int>(
            checkCodeSql,
            new
            {
                customer.MaKhachHang
            });

        if (exists > 0)
            throw new InvalidOperationException(
                $"MÃ£ khÃ¡ch hÃ ng {customer.MaKhachHang} Ä‘Ã£ tá»“n táº¡i.");

        const string sql = """
        INSERT INTO khach_hang
        (
            ma_khach_hang,
            ten_khach_hang,
            loai_khach_hang,
            so_dien_thoai,
            ngay_sinh,
            gioi_tinh,
            email,
            dia_chi,
            phuong_xa,
            quan_huyen,
            tinh_thanh,
            nhom_khach_hang_id,
            ma_so_thue,
            ten_nguoi_mua_hoa_don,
            ten_cong_ty,
            dia_chi_xuat_hoa_don,
            ghi_chu,
            trang_thai,
            ngay_tao,
            ngay_cap_nhat
        )
        VALUES
        (
            @MaKhachHang,
            @TenKhachHang,
            @LoaiKhachHang,
            @SoDienThoai,
            @NgaySinh,
            @GioiTinh,
            @Email,
            @DiaChi,
            @PhuongXa,
            @QuanHuyen,
            @TinhThanh,
            @NhomKhachHangId,
            @MaSoThue,
            @TenNguoiMuaHoaDon,
            @TenCongTy,
            @DiaChiXuatHoaDon,
            @GhiChu,
            1,
            NOW(),
            NOW()
        );

        SELECT LAST_INSERT_ID();
        """;

        var id = await connection.ExecuteScalarAsync<uint>(
            sql,
            new
            {
                customer.MaKhachHang,
                customer.TenKhachHang,
                customer.LoaiKhachHang,
                customer.SoDienThoai,
                customer.NgaySinh,
                customer.GioiTinh,
                customer.Email,
                customer.DiaChi,
                customer.PhuongXa,
                customer.QuanHuyen,
                customer.TinhThanh,
                customer.NhomKhachHangId,
                customer.MaSoThue,
                customer.TenNguoiMuaHoaDon,
                customer.TenCongTy,
                customer.DiaChiXuatHoaDon,
                customer.GhiChu
            });

        _ = LogSubAccountActivityAsync("ThÃªm khÃ¡ch hÃ ng", "KHACH_HANG", id, customer.MaKhachHang, $"ThÃªm khÃ¡ch hÃ ng: {customer.TenKhachHang} ({customer.SoDienThoai})");

        return id;
    }

    /// <summary>
    /// ThÃªm Ä‘á»‘i tÃ¡c / khÃ¡ch hÃ ng ná»™i bá»™ (gáº¯n LoaiKhachHang = "NOI_BO").
    /// </summary>
    public async Task<uint> ThemKhachHangNoiBoAsync(KhachHang customer)
    {
        customer.LoaiKhachHang = "NOI_BO";
        var id = await ThemKhachHangAsync(customer);
        _ = LogSubAccountActivityAsync(
            "ThÃªm khÃ¡ch ná»™i bá»™",
            "KHACH_HANG",
            id,
            customer.MaKhachHang,
            $"ThÃªm khÃ¡ch hÃ ng ná»™i bá»™: {customer.TenKhachHang} ({customer.TenCongTy ?? "Ná»™i bá»™"})");
        return id;
    }

    public async Task CapNhatKhachHangAsync(KhachHang customer)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a khÃ¡ch hÃ ng.");
        await using var connection = await OpenAsync();

        const string sql = """
        UPDATE khach_hang
        SET ma_khach_hang = @MaKhachHang,
            ten_khach_hang = @TenKhachHang,
            loai_khach_hang = @LoaiKhachHang,
            so_dien_thoai = @SoDienThoai,
            ngay_sinh = @NgaySinh,
            gioi_tinh = @GioiTinh,
            email = @Email,
            dia_chi = @DiaChi,
            phuong_xa = @PhuongXa,
            quan_huyen = @QuanHuyen,
            tinh_thanh = @TinhThanh,
            nhom_khach_hang_id = @NhomKhachHangId,
            ma_so_thue = @MaSoThue,
            ten_nguoi_mua_hoa_don = @TenNguoiMuaHoaDon,
            ten_cong_ty = @TenCongTy,
            dia_chi_xuat_hoa_don = @DiaChiXuatHoaDon,
            ghi_chu = @GhiChu,
            ngay_cap_nhat = NOW()
        WHERE id = @Id AND trang_thai = 1;
        """;

        var affectedRows = await connection.ExecuteAsync(sql, customer);
        if (affectedRows == 0)
            throw new InvalidOperationException("KhÃ¡ch hÃ ng khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ bá»‹ xÃ³a.");

        _ = LogSubAccountActivityAsync("Cáº­p nháº­t khÃ¡ch hÃ ng", "KHACH_HANG", customer.Id, customer.MaKhachHang, $"Cáº­p nháº­t khÃ¡ch hÃ ng: {customer.TenKhachHang} ({customer.SoDienThoai})");
    }

    /// <summary>TÃªn báº£ng khÃ¡ch hÃ ng, dÃ¹ng Ä‘á»ƒ sinh cÃ¢u lá»‡nh SQL.</summary>
    private const string BangKhachHang = "khach_hang";

    /// <summary>Báº£ng táº¡m dÃ¹ng Ä‘á»ƒ cáº­p nháº­t hÃ ng loáº¡t khÃ¡ch hÃ ng.</summary>
    private const string BangTamCapNhat = "tmp_nhap_khach_hang";

    /// <summary>
    /// CÃ¡c cá»™t Ä‘Æ°á»£c ghi khi nháº­p tá»« file, Ä‘Ãºng thá»© tá»± vá»›i <see cref="GiaTriDongNhap"/>.
    /// Gá»“m cáº£ nhá»¯ng cá»™t láº¥y tá»« file KiotViet: khu vá»±c giao hÃ ng, ngÆ°á»i táº¡o,
    /// ngÃ y giao dá»‹ch cuá»‘i vÃ  ba cá»™t tiá»n.
    /// </summary>
    private const string CotNhapKhachHang = """
        ma_khach_hang, ten_khach_hang, loai_khach_hang, so_dien_thoai, ngay_sinh, gioi_tinh,
        email, dia_chi, phuong_xa, quan_huyen, tinh_thanh, nhom_khach_hang_id,
        ma_so_thue, ten_nguoi_mua_hoa_don, ten_cong_ty, dia_chi_xuat_hoa_don, ghi_chu,
        chi_nhanh_tao, khu_vuc_giao_hang, nguoi_tao_ten, nguoi_tao_id, ngay_giao_dich_cuoi,
        ngay_nhap_excel, co_du_lieu_kiotviet, no_can_thu_hien_tai, tong_ban,
        tong_ban_tru_tra_hang, trang_thai, ngay_tao, ngay_cap_nhat
        """;

    /// <summary>
    /// GiÃ¡ trá»‹ tÆ°Æ¡ng á»©ng vá»›i tá»«ng cá»™t trong <see cref="CotNhapKhachHang"/>.
    /// NgÃ y táº¡o láº¥y theo file, khi file khÃ´ng cÃ³ thÃ¬ dÃ¹ng thá»i Ä‘iá»ƒm nháº­p.
    /// </summary>
    private const string GiaTriDongNhap = """
        @MaKhachHang, @TenKhachHang, @LoaiKhachHang, @SoDienThoai, @NgaySinh, @GioiTinh,
        @Email, @DiaChi, @PhuongXa, @QuanHuyen, @TinhThanh, @NhomKhachHangId,
        @MaSoThue, @TenNguoiMuaHoaDon, @TenCongTy, @DiaChiXuatHoaDon, @GhiChu,
        @ChiNhanhTao, @KhuVucGiaoHang, @NguoiTaoTen, @NguoiTaoId, @NgayGiaoDichCuoi,
        @NgayNhapExcel, @CoDuLieuKiotViet, @NoCanThuHienTai, @TongBan,
        @TongBanTruTraHang, @TrangThai, COALESCE(@NgayTao, NOW()), @NgayCapNhat
        """;

    /// <summary>
    /// Láº¥y vá» táº­p khÃ³a "ma:" + mÃ£ khÃ¡ch hÃ ng vÃ  "dt:" + sá»‘ Ä‘iá»‡n thoáº¡i Ä‘ang cÃ³,
    /// chá»‰ Ä‘á»ƒ Ä‘áº¿m khÃ¡ch hÃ ng trÃ¹ng trÆ°á»›c khi nháº­p file. KhÃ´ng táº£i danh sÃ¡ch
    /// khÃ¡ch hÃ ng lÃªn giao diá»‡n.
    /// </summary>
    public async Task<IReadOnlySet<string>> LayKhoaTrungKhopKhachHangAsync()
    {
        const string sql = """
        SELECT ma_khach_hang AS MaKhachHang, so_dien_thoai AS SoDienThoai
        FROM khach_hang;
        """;

        await using var connection = await OpenAsync();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in (await connection.QueryAsync<DongKhachHangTonTai>(sql)).AsList())
        {
            if (!string.IsNullOrWhiteSpace(row.MaKhachHang))
                keys.Add("ma:" + row.MaKhachHang.Trim());

            if (!string.IsNullOrWhiteSpace(row.SoDienThoai))
                keys.Add("dt:" + row.SoDienThoai.Trim());
        }

        return keys;
    }

    /// <summary>Nháº­p danh sÃ¡ch khÃ¡ch hÃ ng tá»« file, tá»± Ä‘á»c file vÃ  ghi log Ä‘áº§y Ä‘á»§.</summary>
    public async Task<KetQuaImportKhachHang> ImportKhachHangTuFileAsync(
        string filePath,
        bool replaceExisting = false)
        => await ImportKhachHangTuFileAsync(filePath, DocKhachHangTuFile(filePath), replaceExisting);

    /// <summary>
    /// Nháº­p danh sÃ¡ch khÃ¡ch hÃ ng Ä‘Ã£ Ä‘á»c tá»« file.
    /// KhÃ¡ch hÃ ng Ä‘Ã£ tá»“n táº¡i (trÃ¹ng mÃ£, hoáº·c trÃ¹ng sá»‘ Ä‘iá»‡n thoáº¡i) Ä‘Æ°á»£c cáº­p nháº­t
    /// náº¿u <paramref name="replaceExisting"/> = true, ngÆ°á»£c láº¡i Ä‘Æ°á»£c giá»¯ nguyÃªn.
    /// KhÃ´ng bao giá» xÃ³a khÃ¡ch hÃ ng nÃ o khÃ´ng cÃ³ trong file.
    /// </summary>
    public async Task<KetQuaImportKhachHang> ImportKhachHangTuFileAsync(
        string filePath,
        KetQuaDocKhachHang doc,
        bool replaceExisting = false)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersImport, "Báº¡n khÃ´ng cÃ³ quyá»n import khÃ¡ch hÃ ng.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("ÄÆ°á»ng dáº«n file khÃ´ng há»£p lá»‡.", nameof(filePath));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = new KetQuaImportKhachHang
        {
            TenFile = doc.TenFile,
            SoDongExcel = doc.SoDongExcel,
            SoKhachHangHopLe = doc.SoKhachHangHopLe,
            SoLoi = doc.SoDongLoi
        };

        if (doc.KhachHangs.Count == 0)
        {
            result.DanhSachLoi = doc.DanhSachLoi.Take(20).ToList();
            result.SoLoi = doc.SoDongLoi;
            result.ThoiGian = stopwatch.Elapsed;
            GhiLogNhapKhachHang(result);

            return result;
        }

        await DamBaoCotDuLieuKiotVietAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Náº¡p sáºµn khÃ¡ch hÃ ng Ä‘ang cÃ³, nhÃ³m khÃ¡ch hÃ ng vÃ  tÃ i khoáº£n Ä‘á»ƒ tra cá»©u trong
        // bá»™ nhá»›, trÃ¡nh há»i database 849 láº§n cho 849 dÃ²ng.
        var byCode = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var byPhone = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in (await connection.QueryAsync<DongKhachHangTonTai>(
            """
            SELECT
                id AS Id,
                ma_khach_hang AS MaKhachHang,
                so_dien_thoai AS SoDienThoai
            FROM khach_hang;
            """,
            transaction: transaction)).AsList())
        {
            if (!string.IsNullOrWhiteSpace(row.MaKhachHang))
                byCode[row.MaKhachHang.Trim()] = row.Id;

            if (!string.IsNullOrWhiteSpace(row.SoDienThoai))
                byPhone[row.SoDienThoai.Trim()] = row.Id;
        }

        var nhomKhachHang = await TaiDanhSachTenAsync(
            connection,
            transaction,
            "SELECT id AS Id, ten_nhom AS Ten FROM nhom_khach_hang;");
        var nguoiDung = await TaiDanhSachTenAsync(
            connection,
            transaction,
            """
            SELECT
                id AS Id,
                CONCAT_WS(' ', NULLIF(ho_ten, ''), NULLIF(ten_dang_nhap, '')) AS Ten
            FROM nguoi_dung;
            """);

        var now = DateTime.Now;
        var rowsToInsert = new List<DongNhapKhachHang>(doc.KhachHangs.Count);
        var rowsToUpdate = new List<DongNhapKhachHang>();
        var errors = new List<string>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var customer in doc.KhachHangs)
        {
            if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
            {
                result.SoLoi++;
                continue;
            }

            var ma = (customer.MaKhachHang ?? "").Trim();

            if (ma.Length == 0)
            {
                ma = GenerateCustomerCode();
                customer.MaKhachHang = ma;
            }

            // File Ä‘Ã£ cÃ³ mÃ£ láº·p thÃ¬ chá»‰ giá»¯ dÃ²ng Ä‘áº§u tiÃªn.
            if (!seenCodes.Add(ma))
            {
                result.SoBoQua++;
                errors.Add($"MÃ£ '{ma}' bá»‹ láº·p trong file, bá» qua dÃ²ng láº·p");
                continue;
            }

            var existingId = byCode.TryGetValue(ma, out var idTheoMa)
                ? idTheoMa
                : ResolveIdTheoSoDienThoai(customer.SoDienThoai, byPhone);

            var row = new DongNhapKhachHang
            {
                MaKhachHang = ma,
                TenKhachHang = customer.TenKhachHang.Trim(),
                LoaiKhachHang = string.IsNullOrWhiteSpace(customer.LoaiKhachHang)
                    ? "CA_NHAN"
                    : customer.LoaiKhachHang,
                SoDienThoai = CleanText(customer.SoDienThoai),
                NgaySinh = customer.NgaySinh,
                GioiTinh = CleanText(customer.GioiTinh),
                Email = CleanText(customer.Email),
                DiaChi = CleanText(customer.DiaChi),
                PhuongXa = CleanText(customer.PhuongXa),
                QuanHuyen = CleanText(customer.QuanHuyen),
                TinhThanh = CleanText(customer.TinhThanh),
                MaSoThue = CleanText(customer.MaSoThue),
                TenNguoiMuaHoaDon = CleanText(customer.TenNguoiMuaHoaDon),
                TenCongTy = CleanText(customer.TenCongTy),
                DiaChiXuatHoaDon = CleanText(customer.DiaChiXuatHoaDon),
                GhiChu = CleanText(customer.GhiChu),
                KhuVucGiaoHang = CleanText(customer.KhuVucGiaoHang),
                ChiNhanhTao = CleanText(customer.ChiNhanhTao),
                NhomKhachHangTen = CleanText(customer.NhomKhachHang),
                NguoiTaoTen = CleanText(customer.NguoiTaoTen),
                NgayGiaoDichCuoi = customer.NgayGiaoDichCuoi,
                NgayTao = customer.NgayTao,
                NgayCapNhat = now,
                NgayNhapExcel = now,
                TrangThai = customer.TrangThai,
                NoCanThuHienTai = customer.NoCanThuHienTaiExcel ?? 0m,
                TongBan = customer.TongBanExcel ?? 0m,
                TongBanTruTraHang = customer.TongBanTruTraHangExcel ?? 0m,
                CoDuLieuKiotViet = customer.NoCanThuHienTaiExcel.HasValue
                                    || customer.TongBanExcel.HasValue
                                    || customer.TongBanTruTraHangExcel.HasValue
            };

            if (!string.IsNullOrWhiteSpace(row.NhomKhachHangTen))
            {
                if (nhomKhachHang.TryGetValue(row.NhomKhachHangTen, out var nhomId))
                    row.NhomKhachHangId = nhomId;
                else
                    result.SoNhomKhachHangKhongKhop++;
            }

            if (!string.IsNullOrWhiteSpace(row.NguoiTaoTen))
            {
                if (nguoiDung.TryGetValue(row.NguoiTaoTen, out var nguoiId))
                    row.NguoiTaoId = nguoiId;
                else
                    result.SoNguoiTaoKhongKhop++;
            }

            if (existingId.HasValue)
            {
                if (!replaceExisting)
                {
                    result.SoBoQua++;
                    continue;
                }

                row.Id = existingId.Value;
                rowsToUpdate.Add(row);
                result.SoCapNhat++;
                continue;
            }

            rowsToInsert.Add(row);
            result.SoThemMoi++;
        }

        foreach (var batch in Chunk(rowsToInsert, 200))
            await connection.ExecuteAsync(InsertKhachHangSql("khach_hang"), batch, transaction);

        if (rowsToUpdate.Count > 0)
            await CapNhatNhieuKhachHangAsync(connection, transaction, rowsToUpdate);

        await transaction.CommitAsync();

        errors.InsertRange(0, doc.DanhSachLoi);
        result.DanhSachLoi = errors.Take(20).ToList();
        result.ThoiGian = stopwatch.Elapsed;

        GhiLogNhapKhachHang(result);

        return result;
    }

    /// <summary>
    /// CÃ¢u lá»‡nh ghi má»™t lÃ´ khÃ¡ch hÃ ng. Khi ghi vÃ o báº£ng táº¡m pháº£i kÃ¨m cá»™t id
    /// Ä‘á»ƒ cÃ²n ná»‘i Ä‘Æ°á»£c vá»›i Ä‘Ãºng khÃ¡ch hÃ ng Ä‘ang cáº§n cáº­p nháº­t.
    /// </summary>
    private static string InsertKhachHangSql(string table, bool gheKhoaChinh = false)
    {
        var cot = gheKhoaChinh ? $"id, {CotNhapKhachHang}" : CotNhapKhachHang;
        var giaTri = gheKhoaChinh ? $"@Id, {GiaTriDongNhap}" : GiaTriDongNhap;

        return $"""
            INSERT INTO {table}
            (
                {cot}
            )
            VALUES
            (
                {giaTri}
            );
            """;
    }

    /// <summary>
    /// Cáº­p nháº­t hÃ ng loáº¡t qua báº£ng táº¡m, Ä‘á»ƒ 849 dÃ²ng chá»‰ tá»‘n vÃ i cÃ¢u lá»‡nh thay vÃ¬
    /// 849 láº§n gá»i database. NgÃ y táº¡o vÃ  nhÃ³m/ngÆ°á»i táº¡o chá»‰ bá»‹ ghi Ä‘Ã¨ khi file tháº­t sá»± cÃ³ dá»¯ liá»‡u.
    /// </summary>
    private async Task CapNhatNhieuKhachHangAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyCollection<DongNhapKhachHang> rows)
    {
        await connection.ExecuteAsync(
            $"CREATE TEMPORARY TABLE IF NOT EXISTS {BangTamCapNhat} LIKE {BangKhachHang};",
            transaction: transaction);

        await connection.ExecuteAsync(
            $"DELETE FROM {BangTamCapNhat};",
            transaction: transaction);

        foreach (var batch in Chunk(rows, 200))
        {
            await connection.ExecuteAsync(
                InsertKhachHangSql(BangTamCapNhat, gheKhoaChinh: true),
                batch,
                transaction);
        }

        var cot = CotNhapKhachHang
            .Split(',')
            .Select(column => column.Trim())
            .Where(column => column.Length > 0
                             && column is not ("ngay_tao" or "ngay_cap_nhat" or "nhom_khach_hang_id" or "nguoi_tao_id"))
            .Select(column => $"kh.{column} = tmp.{column}")
            .ToList();

        cot.Add("kh.nhom_khach_hang_id = COALESCE(tmp.nhom_khach_hang_id, kh.nhom_khach_hang_id)");
        cot.Add("kh.nguoi_tao_id = COALESCE(tmp.nguoi_tao_id, kh.nguoi_tao_id)");
        cot.Add("kh.ngay_tao = COALESCE(tmp.ngay_tao, kh.ngay_tao)");
        cot.Add("kh.ngay_cap_nhat = NOW()");

        await connection.ExecuteAsync(
            $"""
             UPDATE {BangKhachHang} kh
             INNER JOIN {BangTamCapNhat} tmp ON tmp.id = kh.id
             SET {string.Join(", ", cot)};
             DROP TEMPORARY TABLE IF EXISTS {BangTamCapNhat};
             """,
            transaction: transaction);
    }

    /// <summary>
    /// Náº¡p danh sÃ¡ch tÃªn kÃ¨m khÃ³a Ä‘á»ƒ tra cá»©u khi nháº­p. Náº¿u báº£ng khÃ´ng tá»“n táº¡i
    /// thÃ¬ tráº£ vá» danh sÃ¡ch rá»—ng, khÃ´ng lÃ m há»ng viá»‡c nháº­p.
    /// </summary>
    private static async Task<Dictionary<string, uint>> TaiDanhSachTenAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sql)
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var row in (await connection.QueryAsync<DongTen>(sql, transaction: transaction)).AsList())
            {
                var ten = (row.Ten ?? "").Trim();

                if (ten.Length == 0)
                    continue;

                // TÃªn trong file KiotViet cÃ³ thá»ƒ lÃ  "PhÆ°Æ¡ng KhÃ¡nh" hoáº·c "KhÃ¡ch láº»",
                // nÃªn pháº£i giá»¯ nguyÃªn cáº£ tÃªn chá»© khÃ´ng tÃ¡ch tá»«ng tá»«.
                result.TryAdd(ten, row.Id);
                result.TryAdd(NormalizeHeaderKey(ten), row.Id);
            }
        }
        catch
        {
            // Báº£ng tra cá»©u khÃ´ng tá»“n táº¡i thÃ¬ bá» qua pháº§n nÃ y.
        }

        return result;
    }

    private static uint? ResolveIdTheoSoDienThoai(
        string? soDienThoai,
        IReadOnlyDictionary<string, uint> byPhone)
        => !string.IsNullOrWhiteSpace(soDienThoai) && byPhone.TryGetValue(soDienThoai.Trim(), out var id)
            ? id
            : null;

    private static string? CleanText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyCollection<T> source, int size)
    {
        var chunk = new List<T>(Math.Min(size, source.Count));

        foreach (var item in source)
        {
            chunk.Add(item);

            if (chunk.Count < size)
                continue;

            yield return chunk;
            chunk = [];
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    /// <summary>
    /// Ghi log káº¿t quáº£ nháº­p ra tá»‡p trong thÆ° má»¥c dá»¯ liá»‡u á»©ng dá»¥ng Ä‘á»ƒ tra cá»©u Ä‘Æ°á»£c
    /// khi cÃ³ sá»± cá»‘, khÃ´ng áº£nh hÆ°á»Ÿng tá»›i viá»‡c cháº¡y cá»§a á»©ng dá»¥ng.
    /// </summary>
    private void GhiLogNhapKhachHang(KetQuaImportKhachHang result)
    {
        var dong = $"[NHAP KHACH HANG] {result.ThongBaoTatCa}";

        if (result.SoNhomKhachHangKhongKhop > 0)
            dong += $" | nhÃ³m khÃ¡ch hÃ ng khÃ´ng khá»›p: {result.SoNhomKhachHangKhongKhop}";

        if (result.SoNguoiTaoKhongKhop > 0)
            dong += $" | ngÆ°á»i táº¡o khÃ´ng khá»›p tÃ i khoáº£n: {result.SoNguoiTaoKhongKhop}";

        GhiLog(dong);

        foreach (var loi in result.DanhSachLoi)
            GhiLog($"[NHAP KHACH HANG] Lá»–I {result.TenFile}: {loi}");
    }

    /// <param name="message">Ná»™i dung cáº§n ghi.</param>
    /// <param name="tenTep">
    /// Tiá»n tá»‘ tÃªn tá»‡p log theo ngÃ y. Máº·c Ä‘á»‹nh lÃ  log nháº­p khÃ¡ch hÃ ng;
    /// má»—i loáº¡i nháº­p dÃ¹ng má»™t tá»‡p riÃªng Ä‘á»ƒ tra cá»©u khÃ´ng láº«n lá»™n.
    /// </param>
    internal void GhiLog(string message, string tenTep = "nhap-khach-hang")
    {
        try
        {
            var thuMuc = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShopHoa",
                "logs");

            Directory.CreateDirectory(thuMuc);

            var tenTepLog = $"{tenTep}-{DateTime.Now:yyyyMMdd}.log";

            File.AppendAllText(
                Path.Combine(thuMuc, tenTepLog),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // KhÃ´ng ghi Ä‘Æ°á»£c log thÃ¬ bá» qua, khÃ´ng Ä‘Æ°á»£c lÃ m há»ng luá»“ng nháº­p.
        }
    }

    /// <summary>Má»™t dÃ²ng khÃ¡ch hÃ ng sáºµn cÃ³ trong database, dÃ¹ng Ä‘á»ƒ tÃ¬m khÃ¡ch hÃ ng trÃ¹ng.</summary>
    private sealed class DongKhachHangTonTai
    {
        public uint Id { get; set; }
        public string? MaKhachHang { get; set; }
        public string? SoDienThoai { get; set; }
    }

    /// <summary>Má»™t dÃ²ng chá»‰ cÃ³ khÃ³a vÃ  tÃªn, dÃ¹ng cho báº£ng nhÃ³m khÃ¡ch hÃ ng vÃ  ngÆ°á»i dÃ¹ng.</summary>
    private sealed class DongTen
    {
        public uint Id { get; set; }
        public string? Ten { get; set; }
    }

    /// <summary>
    /// Má»™t dÃ²ng khÃ¡ch hÃ ng sáº½ ghi xuá»‘ng database, Ä‘Ãºng thá»© tá»± cá»™t trong
    /// <see cref="CotNhapKhachHang"/>. CÃ¡c cá»™t tiá»n láº¥y tá»« file KiotViet.
    /// </summary>
    private sealed class DongNhapKhachHang
    {
        public uint Id { get; set; }
        public string MaKhachHang { get; set; } = "";
        public string TenKhachHang { get; set; } = "";
        public string LoaiKhachHang { get; set; } = "CA_NHAN";
        public string? SoDienThoai { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? Email { get; set; }
        public string? DiaChi { get; set; }
        public string? PhuongXa { get; set; }
        public string? QuanHuyen { get; set; }
        public string? TinhThanh { get; set; }
        public uint? NhomKhachHangId { get; set; }
        public string? MaSoThue { get; set; }
        public string? TenNguoiMuaHoaDon { get; set; }
        public string? TenCongTy { get; set; }
        public string? DiaChiXuatHoaDon { get; set; }
        public string? GhiChu { get; set; }
        public string? ChiNhanhTao { get; set; }
        public string? KhuVucGiaoHang { get; set; }
        public string? NguoiTaoTen { get; set; }
        public uint? NguoiTaoId { get; set; }
        public DateTime? NgayGiaoDichCuoi { get; set; }
        public DateTime? NgayNhapExcel { get; set; }
        public bool CoDuLieuKiotViet { get; set; }
        public decimal NoCanThuHienTai { get; set; }
        public decimal TongBan { get; set; }
        public decimal TongBanTruTraHang { get; set; }
        public bool TrangThai { get; set; }
        public DateTime? NgayTao { get; set; }
        public DateTime NgayCapNhat { get; set; }

        /// <summary>TÃªn nhÃ³m trong file, chá»‰ dÃ¹ng Ä‘á»ƒ tra cá»©u rá»“i bá» Ä‘i.</summary>
        public string? NhomKhachHangTen { get; set; }
    }

    public List<KhachHang> ParseKhachHangFromFile(string filePath)
        => DocKhachHangTuFile(filePath).KhachHangs.ToList();

    /// <summary>
    /// Äá»c file danh sÃ¡ch khÃ¡ch hÃ ng vÃ  tráº£ vá» kÃ¨m sá»‘ liá»‡u thá»‘ng kÃª Ä‘á»ƒ ghi log:
    /// tá»•ng sá»‘ dÃ²ng trong file, sá»‘ dÃ²ng há»£p lá»‡ vÃ  chi tiáº¿t cÃ¡c dÃ²ng bá»‹ loáº¡i.
    /// </summary>
    public KetQuaDocKhachHang DocKhachHangTuFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("ÄÆ°á»ng dáº«n file khÃ´ng há»£p lá»‡.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("KhÃ´ng tÃ¬m tháº¥y file danh sÃ¡ch khÃ¡ch hÃ ng.", filePath);

        var rows = ReadCustomerRowsFromFile(filePath);
        var customers = new List<KhachHang>(rows.Count);
        var errors = new List<string>();
        var soDongLoi = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            var customer = MapRowToKhachHang(rows[index]);

            if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
            {
                soDongLoi++;

                if (errors.Count < 20)
                    errors.Add($"DÃ²ng {index + 2}: thiáº¿u tÃªn khÃ¡ch hÃ ng");

                continue;
            }

            customers.Add(customer);
        }

        return new KetQuaDocKhachHang
        {
            TenFile = Path.GetFileName(filePath),
            SoDongExcel = rows.Count,
            SoKhachHangHopLe = customers.Count,
            SoDongLoi = soDongLoi,
            DanhSachLoi = errors,
            KhachHangs = customers
        };
    }

    private static List<Dictionary<string, string>> ReadCustomerRowsFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvRows(filePath);

        if (extension is ".xlsx" or ".xls")
            return ReadExcelRows(filePath);

        throw new NotSupportedException("File khÃ´ng Ä‘Æ°á»£c há»— trá»£. Vui lÃ²ng chá»n file Excel (.xlsx, .xls) hoáº·c CSV.");
    }

    private static List<Dictionary<string, string>> ReadExcelRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
            throw new InvalidOperationException("File Excel khÃ´ng chá»©a sheet dá»¯ liá»‡u há»£p lá»‡.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
            return [];

        var headerRowNumber = FindCustomerHeaderRow(worksheet, lastRow);
        if (headerRowNumber == 0)
            throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y dÃ²ng tiÃªu Ä‘á» khÃ¡ch hÃ ng trong file.");

        var firstHeaderColumn = worksheet.Row(headerRowNumber).FirstCellUsed()?.Address.ColumnNumber ?? 1;
        var lastHeaderColumn = worksheet.Row(headerRowNumber).LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastHeaderColumn < firstHeaderColumn)
            return [];

        var headers = new List<string>();
        for (var column = firstHeaderColumn; column <= lastHeaderColumn; column++)
            headers.Add(worksheet.Cell(headerRowNumber, column).GetString().Trim());

        var result = new List<Dictionary<string, string>>();

        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index];
                var value = DocGiaTriOTheoKieu(row.Cell(firstHeaderColumn + index));

                if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                if (!string.IsNullOrWhiteSpace(header))
                    values[header] = value;
            }

            if (hasAnyValue)
                result.Add(values);
        }

        return result;
    }

    private static int FindCustomerHeaderRow(IXLWorksheet worksheet, int lastRow)
    {
        for (var rowNumber = 1; rowNumber <= Math.Min(lastRow, 30); rowNumber++)
        {
            var values = worksheet.Row(rowNumber).CellsUsed()
                .Select(cell => NormalizeHeaderKey(cell.GetString()))
                .ToList();

            var hasCode = values.Any(value => value is "ma_khach_hang" or "ma_kh");
            var hasName = values.Any(value => value is "ten_khach_hang" or "ten_kh" or "ten");

            if (hasCode || hasName)
                return rowNumber;
        }

        return 0;
    }

    /// <summary>
    /// Láº¥y giÃ¡ trá»‹ Ã´ theo Ä‘Ãºng kiá»ƒu dá»¯ liá»‡u thay vÃ¬ láº¥y chuá»—i Ä‘Ã£ Ä‘á»‹nh dáº¡ng.
    /// Cáº§n thiáº¿t vÃ¬ file KiotViet Ä‘á»‹nh dáº¡ng tiá»n theo kiá»ƒu "195,000" vÃ  ngÃ y theo
    /// kiá»ƒu "30/09/2026": Ä‘á»c chuá»—i Ä‘á»‹nh dáº¡ng sáº½ lÃ m sai sá»‘ tiá»n (vÄƒn hÃ³a Viá»‡t
    /// dÃ¹ng dáº¥u pháº©y lÃ m dáº¥u tháº­p phÃ¢n). GiÃ¡ trá»‹ sá»‘/ngÃ y Ä‘Æ°á»£c chuyá»ƒn sang
    /// chuá»—i theo quy Æ°á»›c báº¥t biáº¿n Ä‘á»ƒ ParseSoTien/ParseNgayTaiFile Ä‘á»c Ä‘Ãºng.
    /// </summary>
    internal static string DocGiaTriOTheoKieu(IXLCell cell)
    {
        return cell.Value.Type switch
        {
            XLDataType.Number => cell.GetDouble().ToString("0.###############", CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            XLDataType.Boolean => cell.GetBoolean() ? "1" : "0",
            XLDataType.Blank => string.Empty,
            _ => cell.GetString().Trim()
        };
    }

    private static List<Dictionary<string, string>> ReadCsvRows(string filePath)
    {
        var rows = new List<Dictionary<string, string>>();
        var lines = File.ReadAllLines(filePath);

        if (lines.Length < 2)
            return rows;

        var headers = lines[0].Split(',');
        for (var i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length == 0)
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length; j++)
            {
                if (j < values.Length)
                    row[headers[j].Trim()] = values[j].Trim();
            }

            if (row.Count > 0)
                rows.Add(row);
        }

        return rows;
    }

    internal static string NormalizeHeaderKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character) || character == '_')
                builder.Append(FoldDangDe(char.ToLowerInvariant(character)));
            else if (char.IsWhiteSpace(character) || character == '-' || character == '/' || character == '.')
                builder.Append('_');
        }

        return builder.ToString().Replace("__", "_").Trim('_');
    }

    /// <summary>
    /// ÄÆ°a chá»¯ "Ä" (U+0110) vÃ  "Ä‘" vá» "d"/"d".
    /// Hai kÃ½ tá»± nÃ y khÃ´ng cÃ³ dáº¡ng phÃ¢n rÃ£ chuáº©n nÃªn FormD giá»¯ nguyÃªn chÃºng,
    /// khiáº¿n tiÃªu Ä‘á» "Äiá»‡n thoáº¡i" hoÃ¡ thÃ nh "Ä‘ien_thoai" vÃ  khÃ´ng khá»›p vá»›i
    /// danh sÃ¡ch tÃªn cá»™t viáº¿t báº±ng chá»¯ "d" thÃ´ng thÆ°á»ng. Ã” "Äiá»‡n thoáº¡i" vÃ 
    /// "Äá»‹a chá»‰" trong file Excel sáº½ bá»‹ bá» qua náº¿u thiáº¿u bÆ°á»›c gá»™p nÃ y.
    /// </summary>
    private static char FoldDangDe(char character)
        => character switch
        {
            'đ' => 'd',
            'Đ' => 'd',
            _ => character
        };

    private static string? GetCellValue(Dictionary<string, string> row, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizeHeaderKey(candidate);
            foreach (var key in row.Keys)
            {
                if (NormalizeHeaderKey(key) == normalized)
                    return row[key].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Chuyá»ƒn má»™t dÃ²ng Ä‘Ã£ Ä‘á»c tá»« file thÃ nh khÃ¡ch hÃ ng.
    /// Má»i cá»™t Ä‘á»u tÃ¬m theo TÃŠN Cá»˜T (Ä‘Ã£ bá» dáº¥u vÃ  chá»¯ thÆ°á»ng), khÃ´ng theo vá»‹ trÃ­,
    /// nÃªn thá»© tá»± cá»™t trong file khÃ´ng áº£nh hÆ°á»Ÿng káº¿t quáº£.
    /// </summary>
    private static KhachHang MapRowToKhachHang(Dictionary<string, string> row)
    {
        var customer = new KhachHang
        {
            MaKhachHang = GetCellValue(row, "ma_khach_hang", "mÃ£ khÃ¡ch hÃ ng", "ma khach hang", "mÃ£ khÃ¡ch hÃ ng", "customer code", "customer_code", "mÃ£", "mÃ£ khÃ¡ch hÃ ng") ?? string.Empty,
            TenKhachHang = GetCellValue(row, "ten_khach_hang", "tÃªn khÃ¡ch hÃ ng", "ten khach hang", "customer name", "name", "tÃªn", "tÃªn khÃ¡ch hÃ ng") ?? string.Empty,
            LoaiKhachHang = ChuanHoaLoaiKhachHang(GetCellValue(row, "loai_khach_hang", "loáº¡i khÃ¡ch hÃ ng", "loai khach hang", "customer type", "loáº¡i khÃ¡ch", "loai khach", "loáº¡i")),
            SoDienThoai = GetCellValue(row, "so_dien_thoai", "sá»‘ Ä‘iá»‡n thoáº¡i", "so dien thoai", "dien thoai", "Ä‘iá»‡n thoáº¡i", "phone", "sdt", "Ä‘t", "sá»‘ Ä‘iá»‡n thoáº¡i"),
            Email = GetCellValue(row, "email"),
            DiaChi = GetCellValue(row, "dia_chi", "Ä‘á»‹a chá»‰", "dia chi", "address", "Ä‘á»‹a chá»‰"),
            PhuongXa = GetCellValue(row, "phuong_xa", "phÆ°á»ng/xÃ£", "phuong xa", "ward", "phÆ°á»ng xÃ£"),
            QuanHuyen = GetCellValue(row, "quan_huyen", "quáº­n/huyá»‡n", "quan huyen", "district"),
            TinhThanh = GetCellValue(row, "tinh_thanh", "tá»‰nh/thÃ nh", "tinh thanh", "province"),
            NhomKhachHang = GetCellValue(row, "nhom_khach_hang", "nhÃ³m khÃ¡ch hÃ ng", "nhom khach hang", "group", "nhÃ³m", "nhom"),
            MaSoThue = GetCellValue(row, "ma_so_thue", "mÃ£ sá»‘ thuáº¿", "ma so thue", "tax code", "mst"),
            TenNguoiMuaHoaDon = GetCellValue(row, "ten_nguoi_mua_hoa_don", "tÃªn ngÆ°á»i mua hÃ³a Ä‘Æ¡n", "nguoi mua hoa don"),
            TenCongTy = GetCellValue(row, "ten_cong_ty", "tÃªn cÃ´ng ty", "cong ty"),
            DiaChiXuatHoaDon = GetCellValue(row, "dia_chi_xuat_hoa_don", "Ä‘á»‹a chá»‰ xuáº¥t hÃ³a Ä‘Æ¡n", "dia chi xuat hoa don"),
            GhiChu = GetCellValue(row, "ghi_chu", "ghi chÃº", "note", "chÃº thÃ­ch"),
            KhuVucGiaoHang = GetCellValue(row, "khu_vuc_giao_hang", "khu vuc giao hang", "khu vá»±c", "khu vuc", "khu vá»±c giao hÃ ng"),
            ChiNhanhTao = GetCellValue(row, "chi_nhanh_tao", "chi nhÃ¡nh táº¡o", "chi nhanh tao", "chi nhÃ¡nh", "chi nhanh"),
            NguoiTaoTen = GetCellValue(row, "nguoi_tao", "ngÆ°á»i táº¡o", "nguoi tao", "nguoi_tao", "ngÆ°á»i táº¡o"),
            NgayGiaoDichCuoi = ParseNgayTaiFile(GetCellValue(row, "ngay_giao_dich_cuoi", "ngÃ y giao dá»‹ch cuá»‘i", "ngay giao dich cuoi", "ngÃ y giao dá»‹ch cuá»‘i cÃ¹ng", "ngay giao dich gan nhat", "last transaction")),
            NgayTao = ParseNgayTaiFile(GetCellValue(row, "ngay_tao", "ngÃ y táº¡o", "ngay tao", "created at", "ngÃ y táº¡o")),
            NoCanThuHienTaiExcel = ParseSoTien(GetCellValue(row, "no_can_thu_hien_tai", "ná»£ cáº§n thu hiá»‡n táº¡i", "no can thu hien tai", "ná»£ hiá»‡n táº¡i", "no hien tai", "ná»£ cáº§n thu")),
            TongBanExcel = ParseSoTien(GetCellValue(row, "tong_ban", "tá»•ng bÃ¡n", "tong ban", "tá»•ng bÃ¡n")),
            TongBanTruTraHangExcel = ParseSoTien(GetCellValue(row, "tong_ban_tru_tra_hang", "tá»•ng bÃ¡n trá»« tráº£ hÃ ng", "tong ban tru tra hang", "tá»•ng bÃ¡n trá»« tráº£", "tong ban tru tra")),
            GioiTinh = GetCellValue(row, "gioi_tinh", "giá»›i tÃ­nh", "gender", "sex", "gioi tinh"),
            NgaySinh = ParseNgayTaiFile(GetCellValue(row, "ngay_sinh", "ngÃ y sinh", "ngay sinh", "birthday", "dob"))
        };

        var trangThai = ParseTrangThai(GetCellValue(row, "trang_thai", "tráº¡ng thÃ¡i", "trang thai", "status", "active", "tráº¡ng thÃ¡i"));
        if (trangThai.HasValue)
            customer.TrangThai = trangThai.Value;

        if (string.IsNullOrWhiteSpace(customer.MaKhachHang))
            customer.MaKhachHang = GenerateCustomerCode();

        if (string.IsNullOrWhiteSpace(customer.LoaiKhachHang))
            customer.LoaiKhachHang = "CA_NHAN";

        return customer;
    }

    /// <summary>
    /// Chuáº©n hÃ³a loáº¡i khÃ¡ch hÃ ng. File KiotViet ghi "CÃ¡ nhÃ¢n" (sai chÃ­nh táº£),
    /// pháº§n má»m dÃ¹ng mÃ£ "CA_NHAN"/"DOAN".
    /// </summary>
    private static string ChuanHoaLoaiKhachHang(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "CA_NHAN";

        var normalized = NormalizeHeaderKey(value);

        return normalized switch
        {
            "ca_nhan" or "ca_nhan_hang" or "khach_le" or "ca_nhan_" or "canhan" => "CA_NHAN",
            "do_an" or "doanh_nghiep" or "dn" or "khach_doanh_nghiep" or "to_chuc" => "DOAN",
            _ => value.Trim().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Äá»c sá»‘ tiá»n tá»« Ã´. GiÃ¡ trá»‹ Ä‘á»c tá»« file Excel Ä‘Ã£ lÃ  chuá»—i sá»‘ theo quy Æ°á»›c
    /// báº¥t biáº¿n, cÃ²n dá»¯ liá»‡u CSV cÃ³ thá»ƒ lÃ  "195.000", "195,000" hoáº·c "1.234,56".
    /// </summary>
    internal static decimal? ParseSoTien(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim()
            .Replace("\u00A0", "")
            .Replace(" ", "")
            .Replace("â‚«", "")
            .Replace("Ä‘", "")
            .Replace("Ä", "");

        if (text.Length == 0)
            return null;

        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
            return invariantValue;

        var hasComma = text.Contains(',');
        var hasDot = text.Contains('.');
        var lastSeparator = hasComma && hasDot
            ? (text.LastIndexOf(',') > text.LastIndexOf('.') ? ',' : '.')
            : hasComma ? ',' : '.';

        var cleaned = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (char.IsDigit(character))
            {
                cleaned.Append(character);
                continue;
            }

            // Dáº¥u trá»« á»Ÿ Ä‘áº§u lÃ  dáº¥u Ã¢m, cÃ¡c kÃ½ tá»± khÃ¡c bá»‹ bá» qua.
            if (character == '-' && cleaned.Length == 0)
                cleaned.Append(character);
            else if (character == lastSeparator)
            {
                var digitsAfter = 0;

                for (var scan = index + 1; scan < text.Length && char.IsDigit(text[scan]); scan++)
                    digitsAfter++;

                var isDecimalSeparator = digitsAfter is > 0 and < 3;

                if (isDecimalSeparator)
                    cleaned.Append('.');
            }
        }

        var normalized = cleaned.ToString();

        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Äá»c ngÃ y tá»« Ã´. Cháº¥p nháº­n cáº£ chuá»—i khÃ´ng dáº¥u phÃ¢n cÃ¡ch do Excel sinh ra
    /// ("2026-09-30 12:37:50"), dáº¡ng Viá»‡t Nam ("30/09/2026") vÃ  dáº¡ng quá»‘c táº¿.
    /// </summary>
    internal static DateTime? ParseNgayTaiFile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();

        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var invariantValue))
            return invariantValue;

        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("vi-VN"), out var vietnameseValue))
            return vietnameseValue;

        return DateTime.TryParse(text, out var fallback) ? fallback : null;
    }

    /// <summary>Äá»c tráº¡ng thÃ¡i: 1/0, CÃ³/KhÃ´ng, Active/Inactive, Äang hoáº¡t Ä‘á»™ng...</summary>
    private static bool? ParseTrangThai(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var normalized = NormalizeHeaderKey(text);

        return normalized switch
        {
            "1" or "co" or "true" or "yes" or "y" or "active" or "dang_hoat_dong" or "bat_dau" => true,
            "0" or "khong" or "false" or "no" or "n" or "inactive" or "ngung_hoat_dong" => false,
            _ => null
        };
    }

    public async Task<IReadOnlyList<HoaDon>> GetHoaDonAsync(
        string? search = null,
        string? trangThaiLoc = null)
    {
        await using var connection = await OpenAsync();

        const string sql = """
SELECT
    hd.id AS Id,
    hd.ma_hoa_don AS MaHoaDon,
    hd.thoi_gian_ban AS ThoiGianBan,

    COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
    COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'KhÃ¡ch láº»') AS TenKhachHang,

    hd.tong_tien_hang AS TongTienHang,
    hd.giam_gia AS GiamGia,
    hd.thu_khac AS ThuKhac,
    hd.khach_can_tra AS KhachCanTra,
    hd.khach_da_thanh_toan AS KhachDaThanhToan,
    hd.con_no AS ConNo,
    hd.trang_thai_thanh_toan AS TrangThaiThanhToan,
    hd.ghi_chu AS GhiChu

FROM hoa_don_ban_hang hd

LEFT JOIN khach_hang kh
    ON kh.id = hd.khach_hang_id

WHERE (
    @Search = ''
    OR hd.ma_hoa_don LIKE CONCAT('%', @Search, '%')
    OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
    OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
)
AND (
    @Loc = ''
    OR (@Loc = 'PAID' AND hd.con_no <= 0)
    OR (@Loc = 'DEBT' AND hd.con_no > 0)
)

ORDER BY hd.thoi_gian_ban DESC
LIMIT 500;
""";
        return (
            await connection.QueryAsync<HoaDon>(
                sql,
                new
                {
                    Search = search ?? "",
                    Loc = (trangThaiLoc ?? "").Trim().ToUpperInvariant()
                })
        ).AsList();
    }
    /// <summary>
    /// Sinh mÃ£ hÃ³a Ä‘Æ¡n káº¿ tiáº¿p trong ngÃ y, dáº¡ng HDyyMMdd-NNN (VD: HD261005-002).
    /// <para>
    /// PHáº¢I cáº¯t chuá»—i táº¡i <c>CHAR_LENGTH(@Prefix) + 2</c>. MÃ£ hÃ³a Ä‘Æ¡n gá»“m 8 kÃ½ tá»±
    /// tiá»n tá»‘ + 1 dáº¥u gáº¡ch ná»‘i nÃªn pháº§n sá»‘ báº¯t Ä‘áº§u á»Ÿ vá»‹ trÃ­ 10. Báº£n cÅ© dÃ¹ng cá»©ng
    /// <c>SUBSTRING(ma_hoa_don, 9)</c> nÃªn cáº¯t trÃºng dáº¥u "-" vÃ  cáº¯t dáº¥u "-" Ä‘i
    /// ("-001"); MySQL/MariaDB Ã©p "-001" sang UNSIGNED thÃ nh 18446744073709551615,
    /// rá»“i <c>MAX(...) + 1</c> trÃ n sá»‘ khiáº¿n Dapper nÃ©m
    /// <see cref="OverflowException"/>. Há»‡ quáº£: Ä‘Æ¡n Ä‘áº§u tiÃªn váº«n táº¡o Ä‘Æ°á»£c (báº£ng
    /// rá»—ng nÃªn MAX lÃ  NULL, COALESCE vá» 0) nhÆ°ng Tá»ª ÄÆ N THá»¨ HAI TRá»ž ÄI LUÃ”N Lá»–I:
    /// khÃ´ng INSERT Ä‘Æ°á»£c gÃ¬ vÃ  khÃ´ng hiá»‡n hÃ³a Ä‘Æ¡n.
    /// </para>
    /// <para>
    /// Äá»c káº¿t quáº£ báº±ng <see cref="decimal"/> Ä‘á»ƒ mÃ£ hÃ³a Ä‘Æ¡n báº©n/sai Ä‘á»‹nh dáº¡ng
    /// khÃ´ng lÃ m trÃ n sá»‘ lÃºc quy Ä‘á»•i sang <see cref="long"/>.
    /// </para>
    /// </summary>
    private static async Task<string> TaoMaHoaDonMoiTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction)
    {
        var prefix = $"HD{DateTime.Now:yyMMdd}";

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_hoa_don, CHAR_LENGTH(@Prefix) + 2) AS UNSIGNED)),
            0
        )
        FROM hoa_don_ban_hang
        WHERE ma_hoa_don LIKE CONCAT(@Prefix, '-%')
          AND ma_hoa_don REGEXP CONCAT('^', @Prefix, '-[0-9]+$');
        """;

        var maxNumber = await connection.ExecuteScalarAsync<decimal>(
            sql,
            new { Prefix = prefix },
            transaction);

        var nextNumber = Convert.ToInt64(Math.Clamp(maxNumber, 0, 999_999_999)) + 1;

        return $"{prefix}-{nextNumber:D3}";
    }

    /// <summary>Láº¥y mÃ£ hÃ³a Ä‘Æ¡n káº¿ tiáº¿p (dÃ¹ng ngoÃ i giao dá»‹ch bÃ¡n hÃ ng).</summary>
    public async Task<string> TaoMaHoaDonMoiAsync()
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesPayment, "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng.");
        await using var connection = await OpenAsync();

        return await TaoMaHoaDonMoiTrongGiaoDichAsync(connection, null);
    }

    /// <summary>
    /// Dá»±ng láº¡i hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng tá»« database (Ä‘áº§y Ä‘á»§ cáº£ chi tiáº¿t) Ä‘á»ƒ má»Ÿ láº¡i
    /// xem / in má»™t hÃ³a Ä‘Æ¡n Ä‘Ã£ cÃ³. Tráº£ vá» null náº¿u mÃ£ hÃ³a Ä‘Æ¡n khÃ´ng tá»“n táº¡i.
    /// </summary>
    public async Task<HoaDonBanHangReceipt?> GetHoaDonBanHangTheoMaAsync(
        string maHoaDon)
    {
        if (string.IsNullOrWhiteSpace(maHoaDon))
            return null;

        var maCanTim = maHoaDon.Trim();

        var coCotContact = await EnsureHoaDonContactColumnsAsync();
        var coCotHangTang = await EnsureChiTietHoaDonHangTangColumnsAsync();
        var coCotLienKet = await EnsurePhieuQuyHoaDonColumnsAsync();

        await using var connection = await OpenAsync();

        var contactCols = coCotContact
            ? """
              COALESCE(hd.so_dien_thoai, kh.so_dien_thoai, '') AS SoDienThoai,
              COALESCE(hd.dia_chi, kh.dia_chi, '')             AS DiaChi,
              COALESCE(hd.in_kem_thong_tin, 0)                 AS InKemThongTin
              """
            : """
              COALESCE(kh.so_dien_thoai, '')                   AS SoDienThoai,
              COALESCE(kh.dia_chi, '')                         AS DiaChi,
              0                                                AS InKemThongTin
              """;

        // PhÆ°Æ¡ng thá»©c thanh toÃ¡n náº±m á»Ÿ phiáº¿u thu sá»• quá»¹, liÃªn káº¿t ngÆ°á»£c vá» hÃ³a Ä‘Æ¡n.
        var phuongThucCols = coCotLienKet
            ? """
              (SELECT pc.phuong_thuc
                 FROM phieu_chi pc
                WHERE pc.hoa_don_ban_hang_id = hd.id
                ORDER BY pc.id DESC
                LIMIT 1) AS PhuongThucThanhToan
              """
            : "'' AS PhuongThucThanhToan";

        var headerSql = $"""
        SELECT
            hd.id                       AS HoaDonId,
            hd.ma_hoa_don               AS MaHoaDon,
            hd.thoi_gian_ban            AS ThoiGianBan,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'KhÃ¡ch láº»') AS TenKhachHang,
            COALESCE(nd.ho_ten, '')     AS TenNhanVien,
            hd.tong_tien_hang           AS TongTienHang,
            hd.giam_gia                 AS GiamGia,
            hd.thu_khac                 AS ThuKhac,
            hd.khach_can_tra            AS KhachCanTra,
            hd.khach_da_thanh_toan      AS KhachDaThanhToan,
            hd.con_no                   AS ConNo,
            hd.ghi_chu                  AS GhiChu,
            {contactCols},
            {phuongThucCols}
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        LEFT JOIN nguoi_dung  nd ON nd.id = hd.nguoi_ban_id
        WHERE hd.ma_hoa_don = @MaHoaDon
        LIMIT 1;
        """;

        var header = await connection.QueryFirstOrDefaultAsync(
            headerSql,
            new { MaHoaDon = maCanTim });

        if (header is null)
            return null;

        const string detailSql = """
        SELECT
            ten_san_pham AS TenSanPham,
            so_luong     AS SoLuong,
            don_gia      AS DonGia,
            thanh_tien   AS ThanhTien
        FROM chi_tiet_hoa_don_ban_hang
        WHERE hoa_don_id = @HoaDonId
        ORDER BY id;
        """;

        // CÃ³ cá»™t ghi nháº­n hÃ ng táº·ng kÃ¨m thÃ¬ Ä‘á»c thÃªm Ä‘á»ƒ hÃ³a Ä‘Æ¡n hiá»ƒn thá»‹
        // Ä‘Ãºng nhÃ£n "Táº¶NG KÃˆM" vÃ  tÃªn chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i.
        const string detailSqlCoHangTang = """
        SELECT
            ten_san_pham    AS TenSanPham,
            so_luong        AS SoLuong,
            don_gia         AS DonGia,
            thanh_tien      AS ThanhTien,
            is_hang_tang    AS IsHangTang,
            khuyen_mai_ten  AS ChuongTrinhKhuyenMaiTen
        FROM chi_tiet_hoa_don_ban_hang
        WHERE hoa_don_id = @HoaDonId
        ORDER BY id;
        """;

        var items = (await connection.QueryAsync<HoaDonBanHangReceiptItem>(
            coCotHangTang ? detailSqlCoHangTang : detailSql,
            new { HoaDonId = (uint)header.HoaDonId })).AsList();

        var thuKhac = ToDecimal(header.ThuKhac);
        var daThanhToan = ToDecimal(header.KhachDaThanhToan);
        var conNo = ToDecimal(header.ConNo);

        var inKem = Convert.ToInt32(header.InKemThongTin) == 1;
        if (conNo > 0)
        {
            inKem = true;
        }

        return new HoaDonBanHangReceipt
        {
            HoaDonId = (uint)header.HoaDonId,
            MaHoaDon = header.MaHoaDon ?? maCanTim,
            ThoiGianBan = header.ThoiGianBan,
            TenKhachHang = header.TenKhachHang ?? "KhÃ¡ch láº»",
            TenNhanVien = header.TenNhanVien ?? "",
            SoDienThoai = header.SoDienThoai?.ToString(),
            DiaChi = header.DiaChi?.ToString(),
            InKemThongTin = inKem,

            TongTienHang = ToDecimal(header.TongTienHang),
            GiamGia = ToDecimal(header.GiamGia),
            ThuKhac = thuKhac,
            KhachCanTra = ToDecimal(header.KhachCanTra),
            KhachDaThanhToan = daThanhToan,
            ConNo = conNo,

            // "Tiá»n khÃ¡ch Ä‘Æ°a" = sá»‘ tiá»n Ä‘Ã£ tráº£ + tiá»n thá»‘i
            TienKhachDua = daThanhToan + thuKhac,
            TienThoi = thuKhac,

            TrangThaiThanhToan = TrangThaiThanhToanHienThi(conNo > 0, thuKhac > 0),

            PhuongThucThanhToan = (header.PhuongThucThanhToan as string) ?? "",

            GhiChu = header.GhiChu as string,

            Items = items
        };
    }

    private static string TrangThaiThanhToanHienThi(bool conNo, bool coTienThoi)
        => conNo
            ? "Thanh toÃ¡n thiáº¿u - CÃ´ng ná»£"
            : coTienThoi
                ? "ÄÃ£ thanh toÃ¡n Ä‘á»§ - Tiá»n thá»‘i"
                : "ÄÃ£ thanh toÃ¡n Ä‘á»§";

    /// <summary>
    /// LÆ°u má»™t Ä‘Æ¡n bÃ¡n hÃ ng: hÃ³a Ä‘Æ¡n + chi tiáº¿t + trá»« tá»“n kho + ghi lá»‹ch sá»­ xuáº¥t
    /// kho + cÃ´ng ná»£ khÃ¡ch hÃ ng, táº¥t cáº£ trong Má»˜T giao dá»‹ch duy nháº¥t.
    /// Chá»‰ khi commit thÃ nh cÃ´ng má»›i tráº£ vá» hÃ³a Ä‘Æ¡n Ä‘Ã£ Ä‘á»c láº¡i tá»« database;
    /// báº¥t ká»³ bÆ°á»›c nÃ o lá»—i thÃ¬ rollback toÃ n bá»™ vÃ  nÃ©m lá»—i ra ngoÃ i.
    /// </summary>
    public async Task<HoaDonBanHangReceipt> TaoHoaDonBanHangAsync(
    IReadOnlyCollection<SaleCartItem> items,
    KhachHang? customer,
    decimal discount,
    decimal tienKhachDua,
    string? phuongThucThanhToan = null,
    string? ghiChu = null,
    string? soDienThoai = null,
    string? diaChi = null,
    bool inKemThongTin = false)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesPayment, "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng.");
        if (items.Count == 0)
            throw new InvalidOperationException("Giá» hÃ ng Ä‘ang trá»‘ng.");

        var now = DateTime.Now;

        // ============================================================
        // 0. KIá»‚M TRA Dá»® LIá»†U Äáº¦U VÃ€O (trÆ°á»›c khi má»Ÿ giao dá»‹ch)
        // ============================================================

        var cart = new List<SaleCartItem>(items.Count);

        foreach (var item in items)
        {
            if (item?.SanPham is null)
                throw new InvalidOperationException("Giá» hÃ ng cÃ³ dÃ²ng sáº£n pháº©m khÃ´ng há»£p lá»‡.");

            if (item.SanPham.Id == 0)
                throw new InvalidOperationException(
                    $"Sáº£n pháº©m \"{item.SanPham.TenSanPham}\" chÆ°a Ä‘Æ°á»£c lÆ°u vÃ o há»‡ thá»‘ng nÃªn khÃ´ng thá»ƒ bÃ¡n.");

            if (item.SoLuong <= 0)
                throw new InvalidOperationException(
                    $"Sá»‘ lÆ°á»£ng cá»§a sáº£n pháº©m \"{item.SanPham.TenSanPham}\" pháº£i lá»›n hÆ¡n 0.");

            if (item.SanPham.GiaBan <= 0)
                throw new InvalidOperationException(
                    $"Sáº£n pháº©m \"{item.SanPham.TenSanPham}\" chÆ°a cÃ³ giÃ¡ bÃ¡n.");

            cart.Add(item);
        }

        // Tá»•ng tiá»n hÃ ng
        var total = cart.Sum(item => item.ThanhTien);

        // Giáº£m giÃ¡ há»£p lá»‡
        var safeDiscount = Math.Clamp(discount, 0, total);

        // KhÃ¡ch cáº§n thanh toÃ¡n
        var amountDue = total - safeDiscount;

        // Tiá»n khÃ¡ch Ä‘Æ°a khÃ´ng Ä‘Æ°á»£c Ã¢m
        var cashReceived = Math.Max(0, tienKhachDua);

        // Sá»‘ tiá»n thá»±c táº¿ Ä‘Æ°á»£c tÃ­nh lÃ  Ä‘Ã£ thanh toÃ¡n
        // Náº¿u khÃ¡ch Ä‘Æ°a dÆ° thÃ¬ chá»‰ ghi nháº­n báº±ng sá»‘ tiá»n cáº§n tráº£.
        var paidAmount = Math.Min(cashReceived, amountDue);

        // CÃ´ng ná»£ phÃ¡t sinh
        var debtAmount = Math.Max(0, amountDue - paidAmount);

        // KhÃ¡ch láº» (khÃ´ng gáº¯n há»“ sÆ¡ khÃ¡ch hÃ ng) KHÃ”NG Ä‘Æ°á»£c phÃ©p ghi ná»£.
        // Cháº·n ngay táº¡i táº§ng nghiá»‡p vá»¥, TRÆ¯á»šC khi má»Ÿ giao dá»‹ch, nÃªn khÃ´ng táº¡o hÃ³a Ä‘Æ¡n,
        // khÃ´ng trá»« tá»“n kho, khÃ´ng ghi sá»• quá»¹ vÃ  khÃ´ng ghi cÃ´ng ná»£ khÃ¡ch hÃ ng.
        if (debtAmount > 0m && (customer is null || customer.Id == 0))
        {
            throw new InvalidOperationException(
                "KhÃ¡ch láº» khÃ´ng Ä‘Æ°á»£c phÃ©p ghi ná»£. Vui lÃ²ng nháº­p Ä‘á»§ sá»‘ tiá»n khÃ¡ch thanh toÃ¡n " +
                "hoáº·c chá»n khÃ¡ch hÃ ng cÃ³ thÃ´ng tin Ä‘á»ƒ ghi nháº­n cÃ´ng ná»£.");
        }

        // Tiá»n thá»‘i: chá»‰ cÃ³ khi thanh toÃ¡n báº±ng tiá»n máº·t. Thanh toÃ¡n báº±ng
        // chuyá»ƒn khoáº£n / tháº» / vÃ­ Ä‘iá»‡n tá»­ thÃ¬ khÃ´ng cÃ³ tiá»n máº·t Ä‘á»•i láº¡i.
        var phuongThucHienThi = ChuanHoaPhuongThuc(phuongThucThanhToan);
        var laThanhToanTienMat = string.Equals(phuongThucHienThi, "TIEN_MAT", StringComparison.Ordinal);

        var changeAmount = laThanhToanTienMat
            ? Math.Max(0, cashReceived - amountDue)
            : 0m;

        // Tráº¡ng thÃ¡i thanh toÃ¡n
        var paymentStatus = debtAmount > 0 ? "CONG_NO" : "DA_THANH_TOAN";

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
        var userId = Session.CurrentUser?.Id;

        // Äáº£m báº£o cá»™t liÃªn káº¿t phiáº¿u thu vÃ  cá»™t thÃ´ng tin liÃªn há»‡ tá»“n táº¡i TRÆ¯á»šC khi má»Ÿ giao dá»‹ch.
        // ALTER TABLE bÃªn trong transaction sáº½ lÃ m MySQL bÃ¡o
        // "Table definition has changed" vÃ  huá»· cáº£ giao dá»‹ch.
        var coCotPhieuThuLienKet = await EnsurePhieuQuyHoaDonColumnsAsync();
        var coCotContactHoaDon = await EnsureHoaDonContactColumnsAsync();
        var coCotHangTang = await EnsureChiTietHoaDonHangTangColumnsAsync();
        await EnsureBangLichSuNhapXuatKhoAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;

        // MÃ£ phiáº¿u thu sá»• quá»¹ sinh ra tá»« hÃ³a Ä‘Æ¡n nÃ y (rá»—ng náº¿u khÃ¡ch tráº£ 0 Ä‘á»“ng).
        var maPhieuThu = "";

        try
        {
            // ============================================================
            // 1. Táº O HÃ“A ÄÆ N
            //    MÃ£ hÃ³a Ä‘Æ¡n sinh NGAY TRONG giao dá»‹ch (cÃ¹ng connection) Ä‘á»ƒ
            //    khÃ´ng trÃ¹ng mÃ£, vÃ  thá»­ láº¡i náº¿u cháº¡m UNIQUE index ma_hoa_don.
            // ============================================================

            var insertInvoiceSql = coCotContactHoaDon
                ? """
                INSERT INTO hoa_don_ban_hang
                (
                    ma_hoa_don,
                    chi_nhanh_id,
                    khach_hang_id,
                    nguoi_ban_id,
                    so_dien_thoai,
                    dia_chi,
                    in_kem_thong_tin,
                    thoi_gian_ban,
                    tong_tien_hang,
                    giam_gia,
                    thu_khac,
                    khach_can_tra,
                    khach_da_thanh_toan,
                    con_no,
                    trang_thai_thanh_toan,
                    trang_thai_hoa_don,
                    ghi_chu,
                    ngay_tao,
                    ngay_cap_nhat
                )
                VALUES
                (
                    @MaHoaDon,
                    @ChiNhanhId,
                    @KhachHangId,
                    @NguoiBanId,
                    @SoDienThoai,
                    @DiaChi,
                    @InKemThongTin,
                    @ThoiGianBan,
                    @TongTienHang,
                    @GiamGia,
                    @ThuKhac,
                    @KhachCanTra,
                    @KhachDaThanhToan,
                    @ConNo,
                    @TrangThaiThanhToan,
                    'HOAN_THANH',
                    @GhiChu,
                    @ThoiGianBan,
                    @ThoiGianBan
                );
                """
                : """
                INSERT INTO hoa_don_ban_hang
                (
                    ma_hoa_don,
                    chi_nhanh_id,
                    khach_hang_id,
                    nguoi_ban_id,
                    thoi_gian_ban,
                    tong_tien_hang,
                    giam_gia,
                    thu_khac,
                    khach_can_tra,
                    khach_da_thanh_toan,
                    con_no,
                    trang_thai_thanh_toan,
                    trang_thai_hoa_don,
                    ghi_chu,
                    ngay_tao,
                    ngay_cap_nhat
                )
                VALUES
                (
                    @MaHoaDon,
                    @ChiNhanhId,
                    @KhachHangId,
                    @NguoiBanId,
                    @ThoiGianBan,
                    @TongTienHang,
                    @GiamGia,
                    @ThuKhac,
                    @KhachCanTra,
                    @KhachDaThanhToan,
                    @ConNo,
                    @TrangThaiThanhToan,
                    'HOAN_THANH',
                    @GhiChu,
                    @ThoiGianBan,
                    @ThoiGianBan
                );
                """;

            var invoiceCode = "";
            uint invoiceId = 0;

            const int soLanThuLai = 5;

            for (var lan = 1; lan <= soLanThuLai; lan++)
            {
                invoiceCode = await TaoMaHoaDonMoiTrongGiaoDichAsync(
                    connection,
                    transaction);

                try
                {
                    await connection.ExecuteAsync(
                        insertInvoiceSql,
                        new
                        {
                            MaHoaDon = invoiceCode,
                            ChiNhanhId = branchId,
                            KhachHangId = customer?.Id,
                            NguoiBanId = userId,
                            SoDienThoai = BlankToNull(string.IsNullOrWhiteSpace(soDienThoai) ? customer?.SoDienThoai : soDienThoai),
                            DiaChi = BlankToNull(string.IsNullOrWhiteSpace(diaChi) ? customer?.DiaChi : diaChi),
                            InKemThongTin = inKemThongTin ? 1 : 0,
                            ThoiGianBan = now,

                            TongTienHang = total,
                            GiamGia = safeDiscount,

                            // Tiá»n thá»‘i
                            ThuKhac = changeAmount,

                            // KhÃ¡ch cáº§n tráº£
                            KhachCanTra = amountDue,

                            // KhÃ¡ch thá»±c táº¿ Ä‘Ã£ thanh toÃ¡n
                            KhachDaThanhToan = paidAmount,

                            // CÃ´ng ná»£
                            ConNo = debtAmount,

                            TrangThaiThanhToan = paymentStatus,

                            GhiChu = BlankToNull(ghiChu)
                        },
                        transaction);

                    // Äá»c LAST_INSERT_ID() báº±ng lá»‡nh riÃªng, khÃ´ng gá»™p batch nhiá»u
                    // cÃ¢u lá»‡nh (batch phá»¥ thuá»™c cá» AllowBatch cá»§a driver).
                    invoiceId = await connection.ExecuteScalarAsync<uint>(
                        "SELECT LAST_INSERT_ID();",
                        null,
                        transaction);

                    break;
                }
                catch (MySqlException ex) when (ex.Number == 1062 && lan < soLanThuLai)
                {
                    // 1062 = Duplicate entry: mÃ£ hÃ³a Ä‘Æ¡n bá»‹ trÃ¹ng, thá»­ mÃ£ káº¿ tiáº¿p.
                }
            }

            if (invoiceId == 0)
                throw new InvalidOperationException(
                    "KhÃ´ng táº¡o Ä‘Æ°á»£c hÃ³a Ä‘Æ¡n trong database. Giao dá»‹ch Ä‘Ã£ há»§y, dá»¯ liá»‡u bÃ¡n hÃ ng khÃ´ng bá»‹ thay Ä‘á»•i.");

            // ============================================================
            // 2. CHI TIáº¾T HÃ“A ÄÆ N
            // ============================================================

            const string detailSql = """
            INSERT INTO chi_tiet_hoa_don_ban_hang
            (
                hoa_don_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                don_vi_tinh,
                so_luong,
                don_gia,
                thanh_tien,
                gia_von
            )
            VALUES
            (
                @HoaDonId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                @DonViTinh,
                @SoLuong,
                @DonGia,
                @ThanhTien,
                @GiaVon
            );
            """;

            // Báº£n chi tiáº¿t cÃ³ thÃªm cá»™t ghi nháº­n hÃ ng táº·ng kÃ¨m (is_hang_tang...).
            const string detailSqlCoHangTang = """
            INSERT INTO chi_tiet_hoa_don_ban_hang
            (
                hoa_don_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                don_vi_tinh,
                so_luong,
                don_gia,
                thanh_tien,
                gia_von,
                is_hang_tang,
                khuyen_mai_id,
                khuyen_mai_ten,
                san_pham_id_mua,
                so_luong_mua
            )
            VALUES
            (
                @HoaDonId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                @DonViTinh,
                @SoLuong,
                @DonGia,
                @ThanhTien,
                @GiaVon,
                @IsHangTang,
                @KhuyenMaiId,
                @KhuyenMaiTen,
                @SanPhamIdMua,
                @SoLuongMua
            );
            """;

            foreach (var item in cart)
            {
                var affectedRows = await connection.ExecuteAsync(
                    coCotHangTang ? detailSqlCoHangTang : detailSql,
                    new
                    {
                        HoaDonId = invoiceId,
                        SanPhamId = item.SanPham.Id,
                        MaSanPham = item.SanPham.MaSanPham,
                        TenSanPham = item.SanPham.TenSanPham,
                        DonViTinh = item.SanPham.DonViTinh,
                        item.SoLuong,
                        DonGia = item.DonGia,
                        item.ThanhTien,
                        GiaVon = item.SanPham.GiaVon,
                        IsHangTang = item.IsHangTang ? 1 : 0,
                        KhuyenMaiId = item.ChuongTrinhKhuyenMaiId,
                        KhuyenMaiTen = BlankToNull(item.ChuongTrinhKhuyenMaiTen),
                        SanPhamIdMua = item.SanPhamIdMua,
                        SoLuongMua = item.IsHangTang ? item.SoLuongMua : (decimal?)null
                    },
                    transaction);

                if (affectedRows != 1)
                    throw new InvalidOperationException(
                        $"KhÃ´ng lÆ°u Ä‘Æ°á»£c dÃ²ng sáº£n pháº©m \"{item.SanPham.TenSanPham}\" vÃ o hÃ³a Ä‘Æ¡n {invoiceCode}.");
            }

            // Báº£ng chi tiáº¿t KHÃ”NG cÃ³ khoÃ¡ ngoáº¡i nÃªn pháº£i tá»± Ä‘á»‘i chiáº¿u sá»‘ dÃ²ng,
            // trÃ¡nh trÆ°á»ng há»£p "cÃ³ hÃ³a Ä‘Æ¡n nhÆ°ng há»¥t chi tiáº¿t" váº«n commit.
            var soDongChiTiet = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM chi_tiet_hoa_don_ban_hang
                WHERE hoa_don_id = @HoaDonId;
                """,
                new { HoaDonId = invoiceId },
                transaction);

            if (soDongChiTiet != cart.Count)
                throw new InvalidOperationException(
                    $"HÃ³a Ä‘Æ¡n {invoiceCode} chá»‰ lÆ°u Ä‘Æ°á»£c {soDongChiTiet}/{cart.Count} dÃ²ng sáº£n pháº©m. Giao dá»‹ch Ä‘Ã£ há»§y.");

            // ============================================================
            // 3. TRá»ª Tá»’N KHO + GHI Lá»ŠCH Sá»¬ NHáº¬P/XUáº¤T
            // ============================================================

            var canhBao = new List<string>();

            foreach (var item in cart)
                await CapNhatTonKhoBanHangAsync(
                    connection,
                    transaction,
                    branchId,
                    userId,
                    invoiceCode,
                    item,
                    canhBao);

            // ============================================================
            // 4. Náº¾U CÃ“ CÃ”NG Ná»¢ -> LÆ¯U VÃ€O Lá»ŠCH Sá»¬ CÃ”NG Ná»¢ KHÃCH HÃ€NG
            // ============================================================

            if (customer is not null && customer.Id > 0 && debtAmount > 0)
            {
                // Láº¥y cÃ´ng ná»£ hiá»‡n táº¡i cá»§a khÃ¡ch
                const string currentDebtSql = """
                SELECT COALESCE(
                    SUM(ghi_no - ghi_co),
                    0
                )
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @KhachHangId
                FOR UPDATE;
                """;

                var currentDebt = await connection.ExecuteScalarAsync<decimal>(
                    currentDebtSql,
                    new
                    {
                        KhachHangId = customer.Id
                    },
                    transaction);

                // CÃ´ng ná»£ má»›i sau khi phÃ¡t sinh hÃ³a Ä‘Æ¡n
                var newDebtBalance = currentDebt + debtAmount;

                // Ghi lá»‹ch sá»­ cÃ´ng ná»£
                const string debtSql = """
                INSERT INTO lich_su_cong_no_khach_hang
                (
                    khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                    ma_chung_tu, ghi_no, ghi_co, so_du,
                    thoi_gian, nguoi_thuc_hien_id, ghi_chu
                )
                VALUES
                (
                    @KhachHangId, @ChiNhanhId, 'BAN_HANG', 'HOA_DON', @MaChungTu,
                    @GhiNo, 0, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu
                );
                """;

                await connection.ExecuteAsync(
                    debtSql,
                    new
                    {
                        KhachHangId = customer.Id,
                        ChiNhanhId = branchId,

                        MaChungTu = invoiceCode,

                        // Khoáº£n ná»£ phÃ¡t sinh
                        GhiNo = debtAmount,

                        // Sá»‘ dÆ° cÃ´ng ná»£ má»›i
                        SoDu = newDebtBalance,

                        ThoiGian = now,
                        NguoiThucHienId = userId,

                        GhiChu =
                            $"CÃ´ng ná»£ phÃ¡t sinh tá»« hÃ³a Ä‘Æ¡n {invoiceCode}"
                    },
                    transaction);
            }

            // ============================================================
            // 5. GHI PHIáº¾U THU Sá»” QUá»¸
            //    Chá»‰ ghi pháº§n tiá»n khÃ¡ch tráº£ tháº­t (khÃ´ng ká»ƒ tiá»n thá»‘i vÃ 
            //    khÃ´ng tÃ­nh pháº§n cÃ²n ná»£) vÃ  ghi trong CÃ™NG giao dá»‹ch vá»›i hÃ³a Ä‘Æ¡n
            //    nÃªn khÃ´ng bao giá» cÃ³ hÃ³a Ä‘Æ¡n khÃ´ng kÃ¨m phiáº¿u thu, hoáº·c phiáº¿u thu
            //    má»“ cÃ´i khi hÃ³a Ä‘Æ¡n bá»‹ rollback.
            // ============================================================

            maPhieuThu = await GhiPhieuThuBanHangAsync(
                connection,
                transaction,
                branchId,
                userId,
                invoiceId,
                invoiceCode,
                paidAmount,
                changeAmount,
                debtAmount,
                customer,
                now,
                coCotPhieuThuLienKet,
                ChuanHoaPhuongThuc(phuongThucThanhToan));

            // ============================================================
            // 6. COMMIT
            // ============================================================

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "BÃ¡n hÃ ng",
                "HOA_DON",
                invoiceId,
                invoiceCode,
                $"BÃ¡n hÃ³a Ä‘Æ¡n {invoiceCode} cho {(customer != null && !string.IsNullOrWhiteSpace(customer.TenKhachHang) ? customer.TenKhachHang : "KhÃ¡ch láº»")} (Tá»•ng thanh toÃ¡n: {amountDue:N0} Ä‘)",
                amountDue);

            // ============================================================
            // 7. Äá»ŒC Láº I HÃ“A ÄÆ N ÄÃƒ LÆ¯U Äá»‚ HIá»‚N THá»Š
            //    Láº¥y dá»¯ liá»‡u tháº­t trong database thay vÃ¬ dá»±ng láº¡i tá»« RAM, nhá»
            //    váº­y mÃ£ hÃ³a Ä‘Æ¡n, tá»•ng tiá»n, tiá»n thá»‘i, cÃ´ng ná»£ vÃ  danh sÃ¡ch hÃ ng
            //    trÃªn hÃ³a Ä‘Æ¡n luÃ´n khá»›p vá»›i dá»¯ liá»‡u Ä‘Ã£ lÆ°u.
            // ============================================================

            var receipt = await GetHoaDonBanHangTheoMaAsync(invoiceCode);

            if (receipt is null)
                throw new InvalidOperationException(
                    $"ÄÃ£ lÆ°u hÃ³a Ä‘Æ¡n {invoiceCode} nhÆ°ng khÃ´ng Ä‘á»c láº¡i Ä‘Æ°á»£c. HÃ£y kiá»ƒm tra danh sÃ¡ch hÃ³a Ä‘Æ¡n.");

            receipt.CanhBao.AddRange(canhBao);

            receipt.MaPhieuThu = maPhieuThu;

            return receipt;
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    SafeLog.WriteError(
                        "Khong rollback duoc giao dich ban hang",
                        rollbackEx);
                }
            }

            SafeLog.WriteError("Loi tao hoa don ban hang", ex);

            throw;
        }
    }

    /// <summary>
    /// Ghi nháº­n khÃ¡ch tráº£ thÃªm tiá»n cho má»™t hÃ³a Ä‘Æ¡n cÃ²n ná»£: cáº­p nháº­t sá»‘ Ä‘Ã£ tráº£ /
    /// cÃ²n ná»£ / tráº¡ng thÃ¡i cá»§a hÃ³a Ä‘Æ¡n, lÆ°u má»™t dÃ²ng vÃ o báº£ng <c>thanh_toan</c>,
    /// cáº­p nháº­t lá»‹ch sá»­ cÃ´ng ná»£ khÃ¡ch hÃ ng vÃ  táº¡o phiáº¿u thu trong sá»• quá»¹ â€” táº¥t cáº£
    /// trong Má»˜T giao dá»‹ch. Tráº£ vá» hÃ³a Ä‘Æ¡n Ä‘Ã£ Ä‘á»c láº¡i tá»« database.
    /// </summary>
    public async Task<HoaDonBanHangReceipt> ThuNoHoaDonAsync(
        string maHoaDon,
        decimal soTien,
        string? phuongThucThanhToan = null,
        string? ghiChu = null)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.CustomersDebtPay,
            "Báº¡n khÃ´ng cÃ³ quyá»n thu cÃ´ng ná»£ khÃ¡ch hÃ ng.");

        if (string.IsNullOrWhiteSpace(maHoaDon))
            throw new InvalidOperationException("Thiáº¿u mÃ£ hÃ³a Ä‘Æ¡n cáº§n thu ná»£.");

        if (soTien <= 0)
            throw new InvalidOperationException("Sá»‘ tiá»n thu pháº£i lá»›n hÆ¡n 0.");

        var maCanTim = maHoaDon.Trim();
        var now = DateTime.Now;
        var userId = Session.CurrentUser?.Id;

        var coCotLienKet = await EnsurePhieuQuyHoaDonColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;

        try
        {
            const string selectSql = """
            SELECT
                id                  AS Id,
                chi_nhanh_id        AS ChiNhanhId,
                khach_hang_id       AS KhachHangId,
                khach_can_tra       AS KhachCanTra,
                khach_da_thanh_toan AS KhachDaThanhToan,
                con_no              AS ConNo
            FROM hoa_don_ban_hang
            WHERE ma_hoa_don = @MaHoaDon
            LIMIT 1
            FOR UPDATE;
            """;

            var hd = await connection.QueryFirstOrDefaultAsync<HoaDonCongNoRow>(
                selectSql,
                new { MaHoaDon = maCanTim },
                transaction);

            if (hd is null)
                throw new InvalidOperationException($"KhÃ´ng tÃ¬m tháº¥y hÃ³a Ä‘Æ¡n {maCanTim} trong database.");

            var hoaDonId = hd.Id;
            var branchCuaHoaDon = hd.ChiNhanhId;
            var khachHangId = hd.KhachHangId;

            var khachCanTra = hd.KhachCanTra;
            var daTraCu = hd.KhachDaThanhToan;
            var conNoCu = hd.ConNo;

            if (conNoCu <= 0)
                throw new InvalidOperationException(
                    $"HÃ³a Ä‘Æ¡n {maCanTim} Ä‘Ã£ thanh toÃ¡n Ä‘á»§, khÃ´ng cÃ²n cÃ´ng ná»£.");

            var (maPhieu, thu, conNoMoi) = await GhiNhanMotLanThuNoTrongGiaoDichAsync(
                connection,
                transaction,
                hoaDonId,
                branchCuaHoaDon,
                khachHangId,
                maCanTim,
                khachCanTra,
                daTraCu,
                conNoCu,
                soTien,
                phuongThucThanhToan,
                ghiChu,
                now,
                userId,
                coCotLienKet);

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Thu ná»£",
                "HOA_DON",
                hoaDonId,
                maCanTim,
                $"Thu ná»£ hÃ³a Ä‘Æ¡n {maCanTim}: {thu:N0} Ä‘ (cÃ²n ná»£ {conNoMoi:N0} Ä‘)",
                thu);

            var receipt = await GetHoaDonBanHangTheoMaAsync(maCanTim);

            if (receipt is null)
                throw new InvalidOperationException(
                    $"ÄÃ£ thu ná»£ nhÆ°ng khÃ´ng Ä‘á»c láº¡i Ä‘Æ°á»£c hÃ³a Ä‘Æ¡n {maCanTim}.");

            receipt.MaPhieuThu = maPhieu;

            return receipt;
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    SafeLog.WriteError(
                        "Khong rollback duoc giao dich thu no",
                        rollbackEx);
                }
            }

            SafeLog.WriteError("Loi thu no hoa don", ex);

            throw;
        }
    }

    /// <summary>
    /// Ghi nháº­n má»™t láº§n thu ná»£ cho Ä‘Ãºng Má»˜T hÃ³a Ä‘Æ¡n trong giao dá»‹ch Ä‘ang má»Ÿ:
    /// cáº­p nháº­t sá»‘ Ä‘Ã£ tráº£ / cÃ²n ná»£ / tráº¡ng thÃ¡i cá»§a hÃ³a Ä‘Æ¡n, lÆ°u dÃ²ng thanh toÃ¡n,
    /// ghi cÃ³ vÃ o sá»• cÃ¡i cÃ´ng ná»£ khÃ¡ch hÃ ng vÃ  táº¡o phiáº¿u thu sá»• quá»¹. KhÃ´ng tá»± má»Ÿ
    /// giao dá»‹ch â€” dÃ¹ng chung connection/transaction cá»§a hÃ m gá»i Ä‘á»ƒ nhiá»u hÃ³a Ä‘Æ¡n
    /// hoáº·c pháº§n ná»£ khÃ´ng theo hÃ³a Ä‘Æ¡n cÃ³ thá»ƒ náº±m trong CÃ™NG má»™t giao dá»‹ch.
    /// </summary>
    private async Task<(string MaPhieu, decimal Thu, decimal ConNoMoi)>
        GhiNhanMotLanThuNoTrongGiaoDichAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            uint hoaDonId,
            uint chiNhanhId,
            uint? khachHangId,
            string maHoaDon,
            decimal khachCanTra,
            decimal daTraCu,
            decimal conNoCu,
            decimal soTien,
            string? phuongThucThanhToan,
            string? ghiChu,
            DateTime now,
            uint? userId,
            bool coCotLienKet)
    {
        // KhÃ´ng cho thu vÆ°á»£t quÃ¡ sá»‘ cÃ²n ná»£ cá»§a hÃ³a Ä‘Æ¡n.
        var thu = Math.Min(soTien, conNoCu);
        var daTraMoi = daTraCu + thu;
        var conNoMoi = Math.Max(0, khachCanTra - daTraMoi);
        var trangThai = conNoMoi > 0 ? "CONG_NO" : "DA_THANH_TOAN";

        const string updateSql = """
        UPDATE hoa_don_ban_hang
        SET khach_da_thanh_toan = @DaTra,
            con_no = @ConNo,
            trang_thai_thanh_toan = @TrangThai,
            ngay_cap_nhat = @ThoiGian
        WHERE id = @Id;
        """;

        await connection.ExecuteAsync(
            updateSql,
            new
            {
                DaTra = daTraMoi,
                ConNo = conNoMoi,
                TrangThai = trangThai,
                ThoiGian = now,
                Id = hoaDonId
            },
            transaction);

        // LÆ°u dÃ²ng thanh toÃ¡n.
        var maThanhToan = await SinhMaThanhToanTrongGiaoDichAsync(connection, transaction);

        const string insertTtSql = """
        INSERT INTO thanh_toan
        (ma_thanh_toan, hoa_don_id, khach_hang_id, so_tien, phuong_thuc,
         thoi_gian, nguoi_thuc_hien_id, ghi_chu)
        VALUES
        (@MaThanhToan, @HoaDonId, @KhachHangId, @SoTien, @PhuongThuc,
         @ThoiGian, @NguoiThucHienId, @GhiChu);
        """;

        await connection.ExecuteAsync(
            insertTtSql,
            new
            {
                MaThanhToan = maThanhToan,
                HoaDonId = hoaDonId,
                KhachHangId = khachHangId,
                SoTien = thu,
                PhuongThuc = ChuanHoaPhuongThuc(phuongThucThanhToan),
                ThoiGian = now,
                NguoiThucHienId = userId,
                GhiChu = BlankToNull(ghiChu)
            },
            transaction);

        // Cáº­p nháº­t lá»‹ch sá»­ cÃ´ng ná»£ khÃ¡ch hÃ ng (ghi cÃ³ = giáº£m ná»£).
        if (khachHangId is > 0)
        {
            const string currentDebtSql = """
            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
            FROM lich_su_cong_no_khach_hang
            WHERE khach_hang_id = @KhachHangId
            FOR UPDATE;
            """;

            var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                currentDebtSql,
                new { KhachHangId = khachHangId },
                transaction);

            const string debtSql = """
            INSERT INTO lich_su_cong_no_khach_hang
            (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
             ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
            VALUES
            (@KhachHangId, @ChiNhanhId, 'THU_TIEN', 'HOA_DON', @MaChungTu,
             0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
            """;

            await connection.ExecuteAsync(
                debtSql,
                new
                {
                    KhachHangId = khachHangId,
                    ChiNhanhId = chiNhanhId,
                    MaChungTu = maHoaDon,
                    GhiCo = thu,
                    SoDu = soDuCu - thu,
                    ThoiGian = now,
                    NguoiThucHienId = userId,
                    GhiChu = $"Thu ná»£ hÃ³a Ä‘Æ¡n {maHoaDon}"
                },
                transaction);
        }

        // Ghi phiáº¿u thu sá»• quá»¹ (mÃ£ PT). KHÃ”NG gÃ¡n hoa_don_ban_hang_id vÃ¬ chá»‰ má»¥c
        // duy nháº¥t chá»‰ cho phÃ©p 1 phiáº¿u/hÃ³a Ä‘Æ¡n â€” phiáº¿u thu Ä‘áº§u tiÃªn Ä‘Ã£ giá»¯ chá»— Ä‘Ã³.
        var maPhieu = await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction);

        // Phiáº¿u thu ná»£ hÃ³a Ä‘Æ¡n luÃ´n thuá»™c loáº¡i "Thu khÃ¡ch hÃ ng".
        var idNhomThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "THU_KHACH_HANG");

        var noiDung = $"Thu ná»£ hÃ³a Ä‘Æ¡n {maHoaDon}";
        var ghiChuPhieu = string.IsNullOrWhiteSpace(ghiChu)
            ? noiDung
            : $"{noiDung}. {ghiChu.Trim()}";

        var insertPhieuSql = coCotLienKet
            ? """
              INSERT INTO phieu_chi
              (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
               noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
               nguoi_thuc_hien_id, trang_thai, hoa_don_ban_hang_id, ma_hoa_don)
              VALUES
              (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
               @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
               @NguoiTaoId, @TrangThai, NULL, @MaHoaDon);
              """
            : """
              INSERT INTO phieu_chi
              (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
               noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
               nguoi_thuc_hien_id, trang_thai)
              VALUES
              (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
               @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
               @NguoiTaoId, @TrangThai);
              """;

        var tenKhach = khachHangId is null or 0
            ? null
            : await connection.ExecuteScalarAsync<string>(
                "SELECT NULLIF(TRIM(ten_khach_hang), '') FROM khach_hang WHERE id = @Id LIMIT 1;",
                new { Id = khachHangId },
                transaction);

        await connection.ExecuteAsync(
            insertPhieuSql,
            new
            {
                MaPhieu = maPhieu,
                ChiNhanhId = chiNhanhId,
                NhomChiPhiId = idNhomThuKhachHang,
                SoTien = thu,
                PhuongThuc = ChuanHoaPhuongThuc(phuongThucThanhToan),
                NoiDung = noiDung,
                NguoiNopNhan = Truncate150(tenKhach),
                GhiChu = ghiChuPhieu,
                ThoiGian = now,
                NguoiTaoId = userId,
                TrangThai = TrangThaiPhieuHoanThanh,
                MaHoaDon = maHoaDon
            },
            transaction);

        return (maPhieu, thu, conNoMoi);
    }

    /// <summary>
    /// Thu cÃ´ng ná»£ theo khÃ¡ch hÃ ng: trá»« dáº§n sá»‘ tiá»n khÃ¡ch tráº£ vÃ o cÃ¡c hÃ³a Ä‘Æ¡n cÃ²n ná»£
    /// (cÅ© nháº¥t trÆ°á»›c), pháº§n vÆ°á»£t quÃ¡ tá»•ng ná»£ trÃªn hÃ³a Ä‘Æ¡n (vÃ­ dá»¥ ná»£ nháº­p tá»« KiotViet
    /// khÃ´ng gáº¯n hÃ³a Ä‘Æ¡n) ghi tháº³ng vÃ o sá»• cÃ¡i cÃ´ng ná»£ khÃ¡ch hÃ ng. Má»i hÃ³a Ä‘Æ¡n Ä‘Æ°á»£c
    /// cáº¥n trá»«, dÃ²ng thanh toÃ¡n vÃ  phiáº¿u thu sá»• quá»¹ Ä‘á»u náº±m trong CÃ™NG má»™t giao dá»‹ch
    /// nÃªn khÃ´ng bao giá» lá»‡ch sá»•.
    /// </summary>
    public async Task<ThuNoKhachHangKetQua> ThuNoKhachHangAsync(
        uint khachHangId,
        decimal soTien,
        string? phuongThucThanhToan = null,
        string? ghiChu = null)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.CustomersDebtPay,
            "Báº¡n khÃ´ng cÃ³ quyá»n thu cÃ´ng ná»£ khÃ¡ch hÃ ng.");

        if (khachHangId == 0)
            throw new InvalidOperationException("Thiáº¿u khÃ¡ch hÃ ng cáº§n thu ná»£.");

        if (soTien <= 0)
            throw new InvalidOperationException("Sá»‘ tiá»n thu pháº£i lá»›n hÆ¡n 0.");

        var now = DateTime.Now;
        var userId = Session.CurrentUser?.Id;

        var coCotLienKet = await EnsurePhieuQuyHoaDonColumnsAsync();
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;

        try
        {
            const string khachSql = """
            SELECT
                id             AS Id,
                ten_khach_hang AS TenKhachHang,
                chi_nhanh_id   AS ChiNhanhId
            FROM khach_hang
            WHERE id = @Id
            LIMIT 1
            FOR UPDATE;
            """;

            var khach = await connection.QueryFirstOrDefaultAsync<KhachHangCongNoRow>(
                khachSql,
                new { Id = khachHangId },
                transaction);

            if (khach is null)
                throw new InvalidOperationException(
                    $"KhÃ´ng tÃ¬m tháº¥y khÃ¡ch hÃ ng (id {khachHangId}) trong database.");

            string? tenKhach = string.IsNullOrWhiteSpace(khach.TenKhachHang)
                ? null
                : khach.TenKhachHang.Trim();

            uint chiNhanhCuaKhach = khach.ChiNhanhId ?? (Session.CurrentUser?.ChiNhanhId ?? 1);

            // Cháº·n thu vÆ°á»£t quÃ¡ dÆ° ná»£ thá»±c táº¿ theo cÃ¹ng cÃ´ng thá»©c hiá»ƒn thá»‹ á»Ÿ
            // danh sÃ¡ch khÃ¡ch hÃ ng (sá»• cÃ¡i + sá»‘ dÆ° KiotViet náº¿u cÃ³).
            var bieuThucNo = TaoBieuThucTienKhachHang(false, "kh.id", coCotKiotViet).NoHienTai;
            var noHienTai = await connection.ExecuteScalarAsync<decimal>(
                $"SELECT {bieuThucNo} FROM khach_hang kh WHERE kh.id = @Id;",
                new { Id = khachHangId },
                transaction);

            if (noHienTai <= 0)
            {
                throw new InvalidOperationException(
                    $"KhÃ¡ch hÃ ng {(tenKhach ?? khachHangId.ToString())} hiá»‡n khÃ´ng cÃ²n ná»£ Ä‘á»ƒ thu.");
            }

            if (soTien - noHienTai > 0.009m)
            {
                throw new InvalidOperationException(
                    $"Sá»‘ tiá»n thu ({soTien:N0} Ä‘) vÆ°á»£t quÃ¡ dÆ° ná»£ hiá»‡n táº¡i ({noHienTai:N0} Ä‘).");
            }

            // CÃ¡c hÃ³a Ä‘Æ¡n cÃ²n ná»£ cá»§a khÃ¡ch, cÅ© nháº¥t trÆ°á»›c, khoÃ¡ Ä‘á»ƒ trÃ¡nh thu trÃ¹ng.
            const string hoaDonSql = """
            SELECT
                id                  AS Id,
                chi_nhanh_id        AS ChiNhanhId,
                ma_hoa_don          AS MaHoaDon,
                khach_can_tra       AS KhachCanTra,
                khach_da_thanh_toan AS KhachDaThanhToan,
                con_no              AS ConNo
            FROM hoa_don_ban_hang
            WHERE khach_hang_id = @KhachHangId
              AND con_no > 0
              AND trang_thai_hoa_don = 'HOAN_THANH'
            ORDER BY thoi_gian_ban ASC, id ASC
            FOR UPDATE;
            """;

            var hoaDons = (
                await connection.QueryAsync<HoaDonCongNoRow>(
                    hoaDonSql,
                    new { KhachHangId = khachHangId },
                    transaction)
            ).AsList();

            var conLai = soTien;
            var tongDaThu = 0m;
            var soHoaDon = 0;
            var maPhieuThus = new List<string>();

            foreach (var hd in hoaDons)
            {
                if (conLai <= 0)
                    break;

                if (hd.ConNo <= 0)
                    continue;

                var thuLan = Math.Min(conLai, hd.ConNo);

                var (maPhieu, thu, _) = await GhiNhanMotLanThuNoTrongGiaoDichAsync(
                    connection,
                    transaction,
                    hd.Id,
                    hd.ChiNhanhId,
                    khachHangId,
                    hd.MaHoaDon,
                    hd.KhachCanTra,
                    hd.KhachDaThanhToan,
                    hd.ConNo,
                    thuLan,
                    phuongThucThanhToan,
                    ghiChu,
                    now,
                    userId,
                    coCotLienKet);

                tongDaThu += thu;
                conLai -= thu;
                soHoaDon++;
                maPhieuThus.Add(maPhieu);
            }

            if (conLai > 0)
            {
                var maPhieu = await GhiNhanThuNoKhongTheoHoaDonTrongGiaoDichAsync(
                    connection,
                    transaction,
                    khachHangId,
                    chiNhanhCuaKhach,
                    tenKhach,
                    conLai,
                    phuongThucThanhToan,
                    ghiChu,
                    now,
                    userId,
                    coCotLienKet);

                tongDaThu += conLai;
                maPhieuThus.Add(maPhieu);
            }

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Thu ná»£",
                "KHACH_HANG",
                khachHangId,
                null,
                $"Thu ná»£ khÃ¡ch hÃ ng {(tenKhach ?? khachHangId.ToString())}: {tongDaThu:N0} Ä‘",
                tongDaThu);

            // Äá»c láº¡i cÃ´ng ná»£ cÃ²n láº¡i theo cÃ¹ng cÃ¡ch hiá»ƒn thá»‹ á»Ÿ danh sÃ¡ch khÃ¡ch hÃ ng.
            var thongKe = await GetThongKeMuaHangCuaKhachHangAsync(khachHangId);

            return new ThuNoKhachHangKetQua
            {
                KhachHangId = khachHangId,
                TenKhachHang = tenKhach ?? "",
                TongDaThu = tongDaThu,
                ConNoConLai = thongKe.TongConNoHienTai,
                SoHoaDonDaThu = soHoaDon,
                MaPhieuThus = maPhieuThus
            };
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    SafeLog.WriteError(
                        "Khong rollback duoc giao dich thu no khach hang",
                        rollbackEx);
                }
            }

            SafeLog.WriteError("Loi thu no khach hang", ex);

            throw;
        }
    }

    /// <summary>
    /// Ghi nháº­n pháº§n tiá»n thu ná»£ khÃ´ng gáº¯n vá»›i hÃ³a Ä‘Æ¡n nÃ o (ná»£ tá»•ng há»£p tá»« KiotViet
    /// hoáº·c ghi ná»£ tay) vÃ o sá»• cÃ¡i cÃ´ng ná»£ khÃ¡ch hÃ ng vÃ  táº¡o phiáº¿u thu sá»• quá»¹.
    /// </summary>
    private async Task<string> GhiNhanThuNoKhongTheoHoaDonTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint khachHangId,
        uint chiNhanhId,
        string? tenKhach,
        decimal soTien,
        string? phuongThucThanhToan,
        string? ghiChu,
        DateTime now,
        uint? userId,
        bool coCotLienKet)
    {
        const string currentDebtSql = """
        SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
        FROM lich_su_cong_no_khach_hang
        WHERE khach_hang_id = @KhachHangId
        FOR UPDATE;
        """;

        var soDuCu = await connection.ExecuteScalarAsync<decimal>(
            currentDebtSql,
            new { KhachHangId = khachHangId },
            transaction);

        const string debtSql = """
        INSERT INTO lich_su_cong_no_khach_hang
        (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu,
         ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
        VALUES
        (@KhachHangId, @ChiNhanhId, 'THU_TIEN', 'KHACH_HANG', NULL,
         0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu);
        """;

        await connection.ExecuteAsync(
            debtSql,
            new
            {
                KhachHangId = khachHangId,
                ChiNhanhId = chiNhanhId,
                GhiCo = soTien,
                SoDu = soDuCu - soTien,
                ThoiGian = now,
                NguoiThucHienId = userId,
                GhiChu = string.IsNullOrWhiteSpace(ghiChu)
                    ? "Thu ná»£ khÃ¡ch hÃ ng"
                    : $"Thu ná»£ khÃ¡ch hÃ ng. {ghiChu.Trim()}"
            },
            transaction);

        var maPhieu = await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction);

        // Phiáº¿u thu ná»£ khÃ¡ch hÃ ng luÃ´n thuá»™c loáº¡i "Thu khÃ¡ch hÃ ng".
        var idNhomThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "THU_KHACH_HANG");

        var noiDung = "Thu ná»£ khÃ¡ch hÃ ng";
        var ghiChuPhieu = string.IsNullOrWhiteSpace(ghiChu)
            ? noiDung
            : $"{noiDung}. {ghiChu.Trim()}";

        var insertPhieuSql = coCotLienKet
            ? """
              INSERT INTO phieu_chi
              (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
               noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
               nguoi_thuc_hien_id, trang_thai, hoa_don_ban_hang_id, ma_hoa_don)
              VALUES
              (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
               @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
               @NguoiTaoId, @TrangThai, NULL, NULL);
              """
            : """
              INSERT INTO phieu_chi
              (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
               noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
               nguoi_thuc_hien_id, trang_thai)
              VALUES
              (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
               @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
               @NguoiTaoId, @TrangThai);
              """;

        await connection.ExecuteAsync(
            insertPhieuSql,
            new
            {
                MaPhieu = maPhieu,
                ChiNhanhId = chiNhanhId,
                NhomChiPhiId = idNhomThuKhachHang,
                SoTien = soTien,
                PhuongThuc = ChuanHoaPhuongThuc(phuongThucThanhToan),
                NoiDung = noiDung,
                NguoiNopNhan = Truncate150(tenKhach),
                GhiChu = ghiChuPhieu,
                ThoiGian = now,
                NguoiTaoId = userId,
                TrangThai = TrangThaiPhieuHoanThanh
            },
            transaction);

        return maPhieu;
    }

    /// <summary>DÃ²ng khÃ¡ch hÃ ng dÃ¹ng khi thu ná»£ theo khÃ¡ch hÃ ng.</summary>
    private sealed class KhachHangCongNoRow
    {
        public uint Id { get; set; }
        public string TenKhachHang { get; set; } = "";
        public uint? ChiNhanhId { get; set; }
    }

    /// <summary>DÃ²ng hÃ³a Ä‘Æ¡n cÃ²n ná»£ dÃ¹ng khi thu ná»£ theo hÃ³a Ä‘Æ¡n/khÃ¡ch hÃ ng.</summary>
    private sealed class HoaDonCongNoRow
    {
        public uint Id { get; set; }
        public uint ChiNhanhId { get; set; }
        public uint? KhachHangId { get; set; }
        public string MaHoaDon { get; set; } = "";
        public decimal KhachCanTra { get; set; }
        public decimal KhachDaThanhToan { get; set; }
        public decimal ConNo { get; set; }
    }

    /// <summary>DÃ²ng phiáº¿u nháº­p cáº§n cho viá»‡c ghi nháº­n thanh toÃ¡n cÃ´ng ná»£.</summary>
    private sealed class PhieuNhapCongNoRow
    {
        public uint Id { get; set; }
        public uint ChiNhanhId { get; set; }
        public uint? NhaCungCapId { get; set; }
        public string MaPhieuNhap { get; set; } = "";
        public string? TenNhaCungCap { get; set; }
        public decimal TongTienNhap { get; set; }
        public decimal DaThanhToan { get; set; }
        public decimal ConNo { get; set; }
        public string TrangThai { get; set; } = "";
    }

    /// <summary>
    /// Ghi nháº­n thanh toÃ¡n cÃ´ng ná»£ cho Má»˜T phiáº¿u nháº­p hÃ ng. ToÃ n bá»™ thao tÃ¡c náº±m
    /// trong Má»˜T giao dá»‹ch:
    /// <list type="number">
    /// <item>KhoÃ¡ dÃ²ng phiáº¿u vÃ  kiá»ƒm tra cÃ²n ná»£, sá»‘ tiá»n khÃ´ng vÆ°á»£t quÃ¡ cÃ´ng ná»£.</item>
    /// <item>Cáº­p nháº­t <c>da_thanh_toan</c> / <c>con_no</c> cá»§a Ä‘áº§u phiáº¿u.</item>
    /// <item>LÆ°u má»™t dÃ²ng lá»‹ch sá»­ vÃ o <c>thanh_toan_nha_cung_cap</c> (mÃ£ TTNCC...).</item>
    /// <item>Ghi cÃ³ vÃ o sá»• cÃ¡i cÃ´ng ná»£ nhÃ  cung cáº¥p Ä‘á»ƒ cÃ´ng ná»£ giáº£m Ä‘Ãºng sá»‘ Ä‘Ã£ tráº£.</item>
    /// <item>Ghi phiáº¿u chi (mÃ£ PC...) vÃ o sá»• quá»¹, gáº¯n vá»›i chÃ­nh phiáº¿u nháº­p.</item>
    /// </list>
    /// Tráº¡ng thÃ¡i thanh toÃ¡n (chÆ°a/má»™t pháº§n/Ä‘á»§) Ä‘Æ°á»£c SUY RA tá»« sá»‘ Ä‘Ã£ tráº£ vÃ  cÃ²n ná»£,
    /// khÃ´ng lÆ°u thÃªm cá»™t riÃªng.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Phiáº¿u khÃ´ng tá»“n táº¡i, Ä‘ang lÆ°u táº¡m/há»§y, Ä‘Ã£ tráº£ Ä‘á»§, hoáº·c sá»‘ tiá»n khÃ´ng há»£p lá»‡.
    /// </exception>
    public async Task<ThanhToanPhieuNhapKetQua> ThanhToanPhieuNhapAsync(
        string maPhieuNhap,
        decimal soTien,
        string? phuongThucThanhToan = null,
        DateTime? thoiGianThanhToan = null,
        string? ghiChu = null)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.SuppliersDebtPay,
            "Báº¡n khÃ´ng cÃ³ quyá»n thanh toÃ¡n cÃ´ng ná»£ nhÃ  cung cáº¥p.");

        if (string.IsNullOrWhiteSpace(maPhieuNhap))
            throw new InvalidOperationException("Thiáº¿u mÃ£ phiáº¿u nháº­p cáº§n thanh toÃ¡n.");

        if (soTien <= 0)
            throw new InvalidOperationException("Sá»‘ tiá»n thanh toÃ¡n pháº£i lá»›n hÆ¡n 0.");

        var maCanTim = maPhieuNhap.Trim();
        var now = thoiGianThanhToan ?? DateTime.Now;
        var userId = Session.CurrentUser?.Id;

        var coCotNguoiNopNhanGhiChu = await EnsurePhieuQuyExtraColumnsAsync();
        var coCotPhieuNhap = await EnsurePhieuQuyPhieuNhapColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;

        try
        {
            const string selectSql = """
            SELECT
                p.id                 AS Id,
                p.chi_nhanh_id       AS ChiNhanhId,
                p.nha_cung_cap_id    AS NhaCungCapId,
                p.ma_phieu_nhap      AS MaPhieuNhap,
                ncc.ten_nha_cung_cap AS TenNhaCungCap,
                p.tong_tien_nhap     AS TongTienNhap,
                p.da_thanh_toan      AS DaThanhToan,
                p.con_no             AS ConNo,
                p.trang_thai         AS TrangThai
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            WHERE p.ma_phieu_nhap = @MaPhieuNhap
            LIMIT 1
            FOR UPDATE;
            """;

            var phieu = await connection.QueryFirstOrDefaultAsync<PhieuNhapCongNoRow>(
                selectSql,
                new { MaPhieuNhap = maCanTim },
                transaction);

            if (phieu is null)
                throw new InvalidOperationException($"KhÃ´ng tÃ¬m tháº¥y phiáº¿u nháº­p {maCanTim} trong database.");

            if (!TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(phieu.TrangThai))
                throw new InvalidOperationException(
                    $"Phiáº¿u nháº­p {maCanTim} Ä‘ang á»Ÿ tráº¡ng thÃ¡i khÃ´ng thá»ƒ thanh toÃ¡n.");

            if (phieu.ConNo <= 0m)
                throw new InvalidOperationException(
                    $"Phiáº¿u nháº­p {maCanTim} Ä‘Ã£ thanh toÃ¡n Ä‘á»§, khÃ´ng cÃ²n cÃ´ng ná»£.");

            if (soTien > phieu.ConNo)
                throw new InvalidOperationException(
                    $"Sá»‘ tiá»n thanh toÃ¡n ({soTien:N0} Ä‘) vÆ°á»£t quÃ¡ cÃ´ng ná»£ cÃ²n láº¡i ({phieu.ConNo:N0} Ä‘).");

            var daTraMoi = phieu.DaThanhToan + soTien;
            var conNoMoi = Math.Max(0m, phieu.TongTienNhap - daTraMoi);
            var phuongThuc = ChuanHoaPhuongThuc(phuongThucThanhToan);

            // 1. Cáº­p nháº­t Ä‘áº§u phiáº¿u: tá»•ng Ä‘Ã£ tráº£ tÄƒng, cÃ²n ná»£ giáº£m.
            await connection.ExecuteAsync(
                """
                UPDATE phieu_nhap_hang
                SET da_thanh_toan = @DaThanhToan,
                    con_no        = @ConNo,
                    ngay_cap_nhat = @ThoiGian
                WHERE id = @Id;
                """,
                new
                {
                    DaThanhToan = daTraMoi,
                    ConNo = conNoMoi,
                    ThoiGian = now,
                    Id = phieu.Id
                },
                transaction);

            // 2. LÆ°u má»™t dÃ²ng lá»‹ch sá»­ thanh toÃ¡n riÃªng cho láº§n tráº£ nÃ y.
            var maThanhToan = await SinhMaThanhToanNhaCungCapTrongGiaoDichAsync(connection, transaction);

            if (phieu.NhaCungCapId is uint nccId && nccId > 0)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO thanh_toan_nha_cung_cap
                    (
                        ma_thanh_toan, nha_cung_cap_id, phieu_nhap_id, so_tien,
                        phuong_thuc, thoi_gian, nguoi_thuc_hien_id, ghi_chu
                    )
                    VALUES
                    (
                        @MaThanhToan, @NhaCungCapId, @PhieuNhapId, @SoTien,
                        @PhuongThuc, @ThoiGian, @NguoiThucHienId, @GhiChu
                    );
                    """,
                    new
                    {
                        MaThanhToan = maThanhToan,
                        NhaCungCapId = nccId,
                        PhieuNhapId = phieu.Id,
                        SoTien = soTien,
                        PhuongThuc = phuongThuc,
                        ThoiGian = now,
                        NguoiThucHienId = userId,
                        GhiChu = BlankToNull(ghiChu)
                    },
                    transaction);

                // 3. Ghi cÃ³ sá»• cÃ¡i cÃ´ng ná»£ nhÃ  cung cáº¥p -> cÃ´ng ná»£ giáº£m Ä‘Ãºng sá»‘ Ä‘Ã£ tráº£.
                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    nccId,
                    phieu.ChiNhanhId,
                    "THANH_TOAN_NHAP",
                    maCanTim,
                    0,
                    soTien,
                    now,
                    userId,
                    string.IsNullOrWhiteSpace(ghiChu)
                        ? $"Thanh toÃ¡n {soTien:N0} Ä‘ cho phiáº¿u {maCanTim}"
                        : $"Thanh toÃ¡n {soTien:N0} Ä‘ cho phiáº¿u {maCanTim}. {ghiChu.Trim()}");
            }

            // 4. Ghi phiáº¿u chi trong sá»• quá»¹.
            var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);

            // Phiáº¿u chi thanh toÃ¡n nháº­p hÃ ng luÃ´n thuá»™c loáº¡i "Chi nhÃ  cung cáº¥p".
            var idNhomChiNhaCungCap = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");

            var tenNcc = string.IsNullOrWhiteSpace(phieu.TenNhaCungCap)
                ? phieu.NhaCungCapId?.ToString()
                : phieu.TenNhaCungCap;
            var noiDung = $"Thanh toÃ¡n cÃ´ng ná»£ nháº­p hÃ ng - phiáº¿u {maCanTim}";
            var ghiChuPhieu = $"Chi tráº£ cÃ´ng ná»£ cho nhÃ  cung cáº¥p {tenNcc} theo phiáº¿u nháº­p {maCanTim}.";

            string insertPhieuChiSql;
            if (coCotPhieuNhap && coCotNguoiNopNhanGhiChu)
            {
                insertPhieuChiSql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                    nguoi_thuc_hien_id, trang_thai, phieu_nhap_hang_id, ma_phieu_nhap
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                    @NguoiTaoId, @TrangThai, @PhieuNhapId, @MaPhieuNhap
                );
                """;
            }
            else if (coCotNguoiNopNhanGhiChu)
            {
                insertPhieuChiSql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                    nguoi_thuc_hien_id, trang_thai
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                    @NguoiTaoId, @TrangThai
                );
                """;
            }
            else
            {
                insertPhieuChiSql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @ThoiGian, @NguoiTaoId, @TrangThai
                );
                """;
            }

            await connection.ExecuteAsync(
                insertPhieuChiSql,
                new
                {
                    MaPhieu = maPhieuChi,
                    ChiNhanhId = phieu.ChiNhanhId,
                    NhomChiPhiId = idNhomChiNhaCungCap,
                    SoTien = soTien,
                    PhuongThuc = phuongThuc,
                    NoiDung = noiDung,
                    NguoiNopNhan = Truncate150(phieu.TenNhaCungCap),
                    GhiChu = ghiChuPhieu,
                    ThoiGian = now,
                    NguoiTaoId = userId,
                    TrangThai = TrangThaiPhieuHoanThanh,
                    PhieuNhapId = phieu.Id,
                    MaPhieuNhap = maCanTim
                },
                transaction);

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Thanh toÃ¡n NCC",
                "PHIEU_NHAP",
                phieu.Id,
                maCanTim,
                $"Thanh toÃ¡n cÃ´ng ná»£ phiáº¿u {maCanTim}: {soTien:N0} Ä‘ (cÃ²n ná»£ {conNoMoi:N0} Ä‘)",
                soTien);

            return new ThanhToanPhieuNhapKetQua
            {
                PhieuNhapId = phieu.Id,
                MaPhieuNhap = maCanTim,
                TenNhaCungCap = phieu.TenNhaCungCap,
                TongTienNhap = phieu.TongTienNhap,
                DaThanhToan = daTraMoi,
                ConNo = conNoMoi,
                SoTienThanhToan = soTien,
                MaThanhToan = maThanhToan,
                MaPhieuChi = maPhieuChi,
                TrangThaiThanhToan = TrangThaiThanhToanPhieuNhap.Tinh(daTraMoi, conNoMoi)
            };
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    SafeLog.WriteError("KhÃ´ng rollback Ä‘Æ°á»£c giao dá»‹ch thanh toÃ¡n phiáº¿u nháº­p", rollbackEx);
                }
            }

            SafeLog.WriteError("Lá»—i thanh toÃ¡n cÃ´ng ná»£ phiáº¿u nháº­p", ex);
            throw;
        }
    }

    /// <summary>
    /// Cáº¥p mÃ£ lá»‹ch sá»­ thanh toÃ¡n nhÃ  cung cáº¥p káº¿ tiáº¿p (TTNCC0001, TTNCC0002...)
    /// báº±ng chÃ­nh connection vÃ  transaction Ä‘ang cháº¡y, trÃ¡nh trÃ¹ng mÃ£
    /// (cá»™t <c>ma_thanh_toan</c> cÃ³ UNIQUE).
    /// </summary>
    private static async Task<string> SinhMaThanhToanNhaCungCapTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction)
    {
        const string prefix = "TTNCC";

        var maxSuffix = await connection.ExecuteScalarAsync<long?>(
            """
            SELECT COALESCE(MAX(CAST(SUBSTRING(TRIM(ma_thanh_toan), 6) AS SIGNED)), 0)
            FROM thanh_toan_nha_cung_cap
            WHERE ma_thanh_toan LIKE CONCAT(@Prefix, '%')
              AND TRIM(ma_thanh_toan) REGEXP CONCAT('^', @Prefix, '[0-9]+$');
            """,
            new { Prefix = prefix },
            transaction) ?? 0L;

        var nextNumber = maxSuffix > 0 ? maxSuffix + 1 : 1L;

        for (var i = 0; i < 100; i++)
        {
            var candidate = prefix + nextNumber.ToString("D4", CultureInfo.InvariantCulture);

            var daTonTai = await connection.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM thanh_toan_nha_cung_cap WHERE ma_thanh_toan = @Ma LIMIT 1;",
                new { Ma = candidate },
                transaction);

            if (daTonTai is null)
                return candidate;

            nextNumber++;
        }

        return prefix + nextNumber.ToString("D4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Cáº¥p mÃ£ thanh toÃ¡n káº¿ tiáº¿p (TT0001, TT0002...) báº±ng chÃ­nh connection vÃ 
    /// transaction Ä‘ang cháº¡y, trÃ¡nh trÃ¹ng mÃ£ (cá»™t <c>ma_thanh_toan</c> cÃ³ UNIQUE).
    /// </summary>
    private async Task<string> SinhMaThanhToanTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction)
    {
        const string prefix = "TT";

        var maxSuffix = await connection.ExecuteScalarAsync<long?>(
            """
            SELECT COALESCE(MAX(CAST(SUBSTRING(TRIM(ma_thanh_toan), 3) AS SIGNED)), 0)
            FROM thanh_toan
            WHERE ma_thanh_toan LIKE CONCAT(@Prefix, '%')
              AND TRIM(ma_thanh_toan) REGEXP CONCAT('^', @Prefix, '[0-9]+$');
            """,
            new { Prefix = prefix },
            transaction) ?? 0L;

        var nextNumber = maxSuffix > 0 ? maxSuffix + 1 : 1L;

        for (var i = 0; i < 100; i++)
        {
            var candidate = prefix + nextNumber.ToString("D4", CultureInfo.InvariantCulture);

            var daTonTai = await connection.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM thanh_toan WHERE ma_thanh_toan = @Ma LIMIT 1;",
                new { Ma = candidate },
                transaction);

            if (daTonTai is null)
                return candidate;

            nextNumber++;
        }

        return prefix + nextNumber.ToString("D4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Trá»« tá»“n kho khi bÃ¡n hÃ ng vÃ  ghi lá»‹ch sá»­ nháº­p/xuáº¥t kho, trong CÃ™NG giao dá»‹ch
    /// vá»›i hÃ³a Ä‘Æ¡n. TrÆ°á»›c Ä‘Ã¢y luá»“ng bÃ¡n hÃ ng khÃ´ng há» Ä‘á»¥ng tá»›i báº£ng ton_kho nÃªn
    /// tá»“n kho khÃ´ng bao giá» giáº£m vÃ  khÃ´ng cÃ³ dáº¥u váº¿t xuáº¥t hÃ ng.
    /// </summary>
    private async Task CapNhatTonKhoBanHangAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint branchId,
        uint? userId,
        string invoiceCode,
        SaleCartItem item,
        List<string> canhBao)
    {
        const string selectTonSql = """
        SELECT so_luong_ton, gia_von_binh_quan
        FROM ton_kho
        WHERE chi_nhanh_id = @ChiNhanhId
          AND san_pham_id = @SanPhamId
        FOR UPDATE;
        """;

        const string upsertTonSql = """
        INSERT INTO ton_kho
        (
            chi_nhanh_id,
            san_pham_id,
            so_luong_ton,
            gia_von_binh_quan,
            ngay_cap_nhat
        )
        VALUES
        (
            @ChiNhanhId,
            @SanPhamId,
            @SoLuongTon,
            @GiaVonBinhQuan,
            NOW()
        )
        ON DUPLICATE KEY UPDATE
            so_luong_ton  = @SoLuongTon,
            ngay_cap_nhat = NOW();
        """;

        const string insertLichSuSql = """
        INSERT INTO lich_su_nhap_xuat_kho
        (
            chi_nhanh_id,
            san_pham_id,
            loai_giao_dich,
            loai_chung_tu,
            ma_chung_tu,
            so_luong,
            ton_truoc,
            ton_sau,
            gia_von,
            nguoi_thuc_hien_id,
            thoi_gian,
            ghi_chu
        )
        VALUES
        (
            @ChiNhanhId,
            @SanPhamId,
            'XUAT',
            'HOA_DON_BAN_HANG',
            @MaChungTu,
            @SoLuong,
            @TonTruoc,
            @TonSau,
            @GiaVon,
            @NguoiThucHienId,
            NOW(),
            @GhiChu
        );
        """;

        var banGhi = (await connection.QueryAsync(
            selectTonSql,
            new
            {
                ChiNhanhId = branchId,
                SanPhamId = item.SanPham.Id
            },
            transaction)).ToList();

        var coBanGhiTon = banGhi.Count > 0;

        decimal tonTruoc = 0;
        decimal giaVonCu = 0;

        if (banGhi.FirstOrDefault() is IDictionary<string, object> row)
        {
            tonTruoc = ToDecimal(row.TryGetValue("so_luong_ton", out var soLuong) ? soLuong : null);
            giaVonCu = ToDecimal(row.TryGetValue("gia_von_binh_quan", out var giaVon) ? giaVon : null);
        }

        var tonSau = tonTruoc - item.SoLuong;

        if (!coBanGhiTon)
            canhBao.Add(
                $"\"{item.SanPham.TenSanPham}\" chÆ°a cÃ³ sá»‘ tá»“n trong kho, tá»“n sau khi bÃ¡n = {tonSau:N0}.");
        else if (tonSau < 0)
            canhBao.Add(
                $"\"{item.SanPham.TenSanPham}\" bÃ¡n vÆ°á»£t tá»“n kho (cÃ²n {tonTruoc:N0}, bÃ¡n {item.SoLuong:N0}).");

        await connection.ExecuteAsync(
            upsertTonSql,
            new
            {
                ChiNhanhId = branchId,
                SanPhamId = item.SanPham.Id,
                SoLuongTon = tonSau,
                GiaVonBinhQuan = giaVonCu > 0 ? giaVonCu : item.SanPham.GiaVon
            },
            transaction);

        await connection.ExecuteAsync(
            insertLichSuSql,
            new
            {
                ChiNhanhId = branchId,
                SanPhamId = item.SanPham.Id,
                MaChungTu = invoiceCode,
                item.SoLuong,
                TonTruoc = tonTruoc,
                TonSau = tonSau,
                GiaVon = item.SanPham.GiaVon,
                NguoiThucHienId = userId,
                GhiChu = $"Xuáº¥t kho bÃ¡n hÃ ng theo hÃ³a Ä‘Æ¡n {invoiceCode}"
            },
            transaction);
    }

    /// <summary>
    /// Ghi phiáº¿u Thu trong sá»• quá»¹ cho má»™t hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng, dÃ¹ng CHUNG connection
    /// vÃ  transaction vá»›i láº§n ghi hÃ³a Ä‘Æ¡n nÃªn hai thao tÃ¡c cÃ¹ng thÃ nh cÃ´ng hoáº·c
    /// cÃ¹ng bá»‹ rollback.
    /// Chá»‰ ghi khi khÃ¡ch tráº£ tháº­t (&gt; 0); tiá»n thá»‘i vÃ  pháº§n cÃ²n ná»£ khÃ´ng ghi vÃ o quá»¹.
    /// Tráº£ vá» mÃ£ phiáº¿u Thu vá»«a táº¡o, rá»—ng náº¿u khÃ´ng táº¡o phiáº¿u.
    /// </summary>
    private async Task<string> GhiPhieuThuBanHangAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint branchId,
        uint? userId,
        uint invoiceId,
        string invoiceCode,
        decimal paidAmount,
        decimal changeAmount,
        decimal debtAmount,
        KhachHang? customer,
        DateTime thoiGian,
        bool coCotLienKet,
        string phuongThucThanhToan)
    {
        if (paidAmount <= 0)
            return "";

        // Chá»‘ng táº¡o trÃ¹ng: náº¿u hÃ³a Ä‘Æ¡n nÃ y Ä‘Ã£ cÃ³ phiáº¿u thu thÃ¬ láº¥y láº¡i mÃ£ cÅ©,
        // khÃ´ng ghi thÃªm (an toÃ n ká»ƒ cáº£ khi giao dá»‹ch bá»‹ cháº¡y láº¡i).
        if (coCotLienKet)
        {
            var maPhieuDaCo = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT ma_phieu_chi FROM phieu_chi WHERE hoa_don_ban_hang_id = @HoaDonId LIMIT 1;",
                new { HoaDonId = invoiceId },
                transaction);

            if (!string.IsNullOrWhiteSpace(maPhieuDaCo))
                return maPhieuDaCo;
        }

        // Cáº¥p mÃ£ PT ngay trong giao dá»‹ch Ä‘á»ƒ khÃ´ng trÃ¹ng vá»›i phiáº¿u Ä‘ang má»Ÿ á»Ÿ mÃ¡y khÃ¡c.
        var maPhieu = await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction);

        // Phiáº¿u thu bÃ¡n hÃ ng luÃ´n thuá»™c loáº¡i "Thu khÃ¡ch hÃ ng".
        var idNhomThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "THU_KHACH_HANG");

        var ghiChu = $"Thu tiá»n thanh toÃ¡n hÃ³a Ä‘Æ¡n {invoiceCode}.";

        if (changeAmount > 0)
            ghiChu += $" Tiá»n thá»‘i {changeAmount:N0} Ä‘Ã£ tráº£ láº¡i khÃ¡ch, khÃ´ng tÃ­nh vÃ o quá»¹.";

        if (debtAmount > 0)
            ghiChu += $" CÃ²n ná»£ {debtAmount:N0} ghi vÃ o sá»• cÃ´ng ná»£ khÃ¡ch hÃ ng.";

        var sql = coCotLienKet
            ? """
              INSERT INTO phieu_chi
              (
                  ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                  noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                  nguoi_thuc_hien_id, trang_thai, hoa_don_ban_hang_id, ma_hoa_don
              )
              VALUES
              (
                  @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                  @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                  @NguoiTaoId, @TrangThai, @HoaDonId, @MaHoaDon
              );
              """
            : """
              INSERT INTO phieu_chi
              (
                  ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                  noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                  nguoi_thuc_hien_id, trang_thai
              )
              VALUES
              (
                  @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                  @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                  @NguoiTaoId, @TrangThai
              );
              """;

        await connection.ExecuteAsync(
            sql,
            new
            {
                MaPhieu = maPhieu,
                ChiNhanhId = branchId,
                NhomChiPhiId = idNhomThuKhachHang,
                SoTien = paidAmount,
                PhuongThuc = phuongThucThanhToan,
                NoiDung = $"Thu tiá»n bÃ¡n hÃ ng - hÃ³a Ä‘Æ¡n {invoiceCode}",
                NguoiNopNhan = Truncate150(customer?.TenKhachHang),
                GhiChu = ghiChu,
                ThoiGian = thoiGian,
                NguoiTaoId = userId,
                TrangThai = TrangThaiPhieuHoanThanh,
                HoaDonId = invoiceId,
                MaHoaDon = invoiceCode
            },
            transaction);

        return maPhieu;
    }

    /// <summary>
    /// Chuáº©n hÃ³a mÃ£ phÆ°Æ¡ng thá»©c thanh toÃ¡n cho cá»™t <c>phieu_chi.phuong_thuc</c>
    /// (varchar(30)). MÃ£ rá»—ng/khÃ´ng há»£p lá»‡ quay vá» "TIEN_MAT".
    /// </summary>
    private static string ChuanHoaPhuongThuc(string? ma)
    {
        const string macDinh = "TIEN_MAT";

        if (string.IsNullOrWhiteSpace(ma))
            return macDinh;

        var chuanHoa = ma.Trim().ToUpperInvariant();

        if (chuanHoa.Length > 30)
            chuanHoa = chuanHoa[..30];

        return chuanHoa.All(c => char.IsLetterOrDigit(c) || c == '_') ? chuanHoa : macDinh;
    }

    /// <summary>
    /// Cáº¥p mÃ£ phiáº¿u Thu káº¿ tiáº¿p (PT0001, PT0002...) báº±ng chÃ­nh connection vÃ 
    /// transaction cá»§a giao dá»‹ch Ä‘ang cháº¡y, trÃ¡nh má»Ÿ thÃªm káº¿t ná»‘i vÃ  trÃ¡nh trÃ¹ng mÃ£.
    /// </summary>
    private async Task<string> SinhMaPhieuThuTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction)
    {
        var nextNumber = await DocSoThuTuMaPhieuQuyAsync(connection, transaction, "PT");

        for (var i = 0; i < 100; i++)
        {
            var candidate = "PT" + nextNumber.ToString("D4", CultureInfo.InvariantCulture);

            var daTonTai = await connection.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM phieu_chi WHERE ma_phieu_chi = @Ma LIMIT 1;",
                new { Ma = candidate },
                transaction);

            if (daTonTai is null)
                return candidate;

            nextNumber++;
        }

        return "PT" + nextNumber.ToString("D4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Cáº¥p mÃ£ phiáº¿u Chi káº¿ tiáº¿p (PC0001, PC0002...) báº±ng chÃ­nh connection vÃ 
    /// transaction cá»§a giao dá»‹ch Ä‘ang cháº¡y, trÃ¡nh má»Ÿ thÃªm káº¿t ná»‘i vÃ  trÃ¡nh trÃ¹ng mÃ£.
    /// </summary>
    private static async Task<string> SinhMaPhieuChiTrongGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction)
    {
        var nextNumber = await DocSoThuTuMaPhieuQuyAsync(connection, transaction, "PC");

        for (var i = 0; i < 100; i++)
        {
            var candidate = "PC" + nextNumber.ToString("D4", CultureInfo.InvariantCulture);

            var daTonTai = await connection.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM phieu_chi WHERE ma_phieu_chi = @Ma LIMIT 1;",
                new { Ma = candidate },
                transaction);

            if (daTonTai is null)
                return candidate;

            nextNumber++;
        }

        return "PC" + nextNumber.ToString("D4", CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<NhaCungCap>> GetNhaCungCapAsync(
        string? search = null)
    {
        await using var connection = await OpenAsync();

        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        var sql = $"""
        SELECT
        {TaoDanhSachCotNhaCungCap(coCotKiotViet)}
        FROM nha_cung_cap ncc
        WHERE ncc.trang_thai = 1
          AND (
                @Search = ''
                OR ncc.ma_nha_cung_cap LIKE CONCAT('%', @Search, '%')
                OR ncc.ten_nha_cung_cap LIKE CONCAT('%', @Search, '%')
                OR ncc.so_dien_thoai LIKE CONCAT('%', @Search, '%')
                OR ncc.email LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY ncc.ten_nha_cung_cap;
        """;

        return (
            await connection.QueryAsync<NhaCungCap>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }

    /// <summary>
    /// Sinh mÃ£ nhÃ  cung cáº¥p tuáº§n tá»± theo dáº¡ng NCC0000001.
    /// Chá»‰ dÃ¹ng cÃ¡c mÃ£ NCC + sá»‘ tá»‘i Ä‘a 7 chá»¯ sá»‘ Ä‘á»ƒ khÃ´ng bá»‹ áº£nh hÆ°á»Ÿng
    /// bá»Ÿi cÃ¡c mÃ£ cÅ© dáº¡ng timestamp.
    /// </summary>
    // ==========================================
    // DANH SÃCH NHÃ€ CUNG Cáº¤P: Lá»ŒC + PHÃ‚N TRANG SERVER-SIDE
    // ==========================================

    /// <summary>
    /// Má»‡nh Ä‘á» FROM dÃ¹ng chung cho cáº£ truy váº¥n láº¥y trang vÃ  truy váº¥n Ä‘áº¿m/thá»‘ng kÃª,
    /// Ä‘á»ƒ danh sÃ¡ch vÃ  cÃ¡c tháº» thá»‘ng kÃª luÃ´n nÃ³i vá» cÃ¹ng má»™t táº­p nhÃ  cung cáº¥p.
    /// CÃ´ng ná»£, tá»•ng mua vÃ  tá»•ng mua trá»« tráº£ hÃ ng Ä‘á»u láº¥y tá»« cÃ¹ng má»™t biá»ƒu thá»©c
    /// trong <see cref="TaoBieuThucTienNhaCungCap"/> nÃªn khÃ´ng cáº§n JOIN báº£ng view.
    /// </summary>
    private const string NhaCungCapFrom = """
    FROM nha_cung_cap ncc
    """;

    /// <summary>
    /// Danh sÃ¡ch cá»™t cá»§a báº£ng nhÃ  cung cáº¥p, dÃ¹ng chung cho nhiá»u cÃ¢u SQL.
    /// Ba cá»™t tiá»n dÃ¹ng Ä‘Ãºng biá»ƒu thá»©c mÃ  bá»™ lá»c vÃ  tháº» thá»‘ng kÃª Ä‘ang dÃ¹ng nÃªn
    /// sá»‘ tiá»n hiá»ƒn thá»‹ á»Ÿ má»i nÆ¡i luÃ´n khá»›p nhau.
    /// </summary>
    private static string TaoDanhSachCotNhaCungCap(bool coCotKiotViet)
    {
        var tien = TaoBieuThucTienNhaCungCap("ncc.id", coCotKiotViet);

        return $"""
            ncc.id AS Id,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            ncc.so_dien_thoai AS SoDienThoai,
            ncc.email AS Email,
            ncc.dia_chi AS DiaChi,
            ncc.phuong_xa AS PhuongXa,
            ncc.quan_huyen AS QuanHuyen,
            ncc.tinh_thanh AS TinhThanh,
            ncc.ma_so_thue AS MaSoThue,
            ncc.nguoi_lien_he AS NguoiLienHe,
            ncc.ghi_chu AS GhiChu,
            ncc.trang_thai AS TrangThai,
            ncc.ngay_tao AS NgayTao,
            COALESCE({tien.NoHienTai}, 0) AS ConNo,
            COALESCE({tien.TongMua}, 0) AS TongMua,
            COALESCE({tien.TongMuaTruTraHang}, 0) AS TongMuaTruTraHang
            """;
    }

    /// <summary>
    /// CÃ¡c cá»™t láº¥y tá»« file danh sÃ¡ch (Ä‘Ã£ lÆ°u xuá»‘ng database) dÃ nh cho truy váº¥n
    /// chi tiáº¿t má»™t nhÃ  cung cáº¥p. KhÃ´ng dÃ¹ng cho báº£ng danh sÃ¡ch vÃ¬ chá»‰ cáº§n ba
    /// cá»™t tiá»n Ä‘Ã£ Ä‘Æ°á»£c tÃ­nh sáºµn trong <see cref="TaoDanhSachCotNhaCungCap"/>.
    /// </summary>
    private static string TaoDanhSachCotFileNhaCungCap() => """
            ncc.co_du_lieu_kiotviet AS CoDuLieuKiotViet,
            ncc.nguoi_tao_ten AS NguoiTaoTen,
            ncc.ngay_nhap_excel AS NgayNhapExcel,
            ncc.khu_vuc AS KhuVuc,
            ncc.so_cccd AS SoCccd,
            ncc.nhom_nha_cung_cap_ten AS NhomNhaCungCapTen,
            ncc.ten_cong_ty AS TenCongTy,
            ncc.no_can_tra_hien_tai AS NoCanTraHienTaiExcel,
            ncc.tong_mua AS TongMuaExcel,
            ncc.tong_mua_tru_tra_hang AS TongMuaTruTraHangExcel
    """;

    /// <summary>
    /// Sinh má»‡nh Ä‘á» WHERE tá»« bá»™ lá»c. Má»i Ä‘iá»u kiá»‡n Ä‘á»u lÃ  tham sá»‘ hÃ³a nÃªn an toÃ n
    /// vá»›i dáº¥u nhÃ¡y trong tá»« khÃ³a, vÃ  Ä‘á»u Ä‘Æ°á»£c Ä‘áº©y xuá»‘ng MySQL xá»­ lÃ½.
    /// Lá»c cÃ´ng ná»£ dÃ¹ng chÃ­nh biá»ƒu thá»©c cÃ´ng ná»£ cá»§a báº£ng danh sÃ¡ch Ä‘á»ƒ ngÆ°á»i
    /// dÃ¹ng khÃ´ng lá»c ra má»™t danh sÃ¡ch mÃ  cá»™t "CÃ²n ná»£" láº¡i hiá»ƒn thá»‹ khÃ¡c.
    /// </summary>
    private static string TaoDieuKienNhaCungCap(BoLocNhaCungCap loc, string bieuThucConNo)
    {
        var dieuKien = new StringBuilder();

        dieuKien.Append("ncc.trang_thai = 1");

        // Ã” tÃ¬m kiáº¿m: mÃ£ NCC, tÃªn, sá»‘ Ä‘iá»‡n thoáº¡i, email.
        dieuKien.Append(
            " AND (@TimKiem = ''"
            + " OR ncc.ma_nha_cung_cap LIKE @TimKiemLike"
            + " OR ncc.ten_nha_cung_cap LIKE @TimKiemLike"
            + " OR COALESCE(ncc.so_dien_thoai, '') LIKE @TimKiemLike"
            + " OR COALESCE(ncc.email, '') LIKE @TimKiemLike)");

        // Tá»«ng Ã´ cá»§a bá»™ lá»c chi tiáº¿t.
        dieuKien.Append(" AND (@MaNhaCungCap = '' OR ncc.ma_nha_cung_cap LIKE @MaNhaCungCapLike)");
        dieuKien.Append(" AND (@TenNhaCungCap = '' OR ncc.ten_nha_cung_cap LIKE @TenNhaCungCapLike)");
        dieuKien.Append(" AND (@SoDienThoai = '' OR COALESCE(ncc.so_dien_thoai, '') LIKE @SoDienThoaiLike)");
        dieuKien.Append(" AND (@Email = '' OR COALESCE(ncc.email, '') LIKE @EmailLike)");

        // Äá»‹a chá»‰ khá»›p cáº£ 4 cá»™t Ä‘á»‹a chá»‰ Ä‘á»ƒ lá»c Ä‘Æ°á»£c cáº£ khi ngÆ°á»i dÃ¹ng chá»‰ nhá»›
        // tÃªn phÆ°á»ng/xÃ£, quáº­n/huyá»‡n hoáº·c tá»‰nh/thÃ nh.
        dieuKien.Append(
            " AND (@DiaChi = ''"
            + " OR COALESCE(ncc.dia_chi, '') LIKE @DiaChiLike"
            + " OR COALESCE(ncc.phuong_xa, '') LIKE @DiaChiLike"
            + " OR COALESCE(ncc.quan_huyen, '') LIKE @DiaChiLike"
            + " OR COALESCE(ncc.tinh_thanh, '') LIKE @DiaChiLike)");

        // Tráº¡ng thÃ¡i cÃ´ng ná»£: táº¥t cáº£ / cÃ²n ná»£ / khÃ´ng cÃ²n ná»£.
        dieuKien.Append(
            " AND (@CongNoMode = 0"
            + $" OR (@CongNoMode = 1 AND COALESCE({bieuThucConNo}, 0) > 0)"
            + $" OR (@CongNoMode = 2 AND COALESCE({bieuThucConNo}, 0) = 0))");

        // Khoáº£ng cÃ´ng ná»£.
        dieuKien.Append($" AND (@ConNoTu IS NULL OR COALESCE({bieuThucConNo}, 0) >= @ConNoTu)");
        dieuKien.Append($" AND (@ConNoDen IS NULL OR COALESCE({bieuThucConNo}, 0) <= @ConNoDen)");

        return dieuKien.ToString();
    }

    /// <summary>Gom bá»™ lá»c thÃ nh Ä‘Ãºng bá»™ tham sá»‘ mÃ  cÃ¡c cÃ¢u SQL kia Ä‘ang mong Ä‘á»£i.</summary>
    private static object TaoThamSoNhaCungCap(BoLocNhaCungCap loc)
    {
        var timKiem = (loc.TimKiem ?? "").Trim();
        var maNhaCungCap = (loc.MaNhaCungCap ?? "").Trim();
        var tenNhaCungCap = (loc.TenNhaCungCap ?? "").Trim();
        var soDienThoai = (loc.SoDienThoai ?? "").Trim();
        var email = (loc.Email ?? "").Trim();
        var diaChi = (loc.DiaChi ?? "").Trim();

        return new
        {
            TimKiem = timKiem,
            TimKiemLike = $"%{timKiem}%",
            MaNhaCungCap = maNhaCungCap,
            MaNhaCungCapLike = $"%{maNhaCungCap}%",
            TenNhaCungCap = tenNhaCungCap,
            TenNhaCungCapLike = $"%{tenNhaCungCap}%",
            SoDienThoai = soDienThoai,
            SoDienThoaiLike = $"%{soDienThoai}%",
            Email = email,
            EmailLike = $"%{email}%",
            DiaChi = diaChi,
            DiaChiLike = $"%{diaChi}%",
            CongNoMode = loc.CongNoMode,
            ConNoTu = loc.ConNoTu,
            ConNoDen = loc.ConNoDen
        };
    }

    /// <summary>
    /// Láº¥y má»™t trang nhÃ  cung cáº¥p theo bá»™ lá»c, kÃ¨m tá»•ng sá»‘ báº£n ghi khá»›p bá»™ lá»c.
    /// Chá»‰ Ä‘á»c Ä‘Ãºng <paramref name="pageSize"/> dÃ²ng (LIMIT/OFFSET), khÃ´ng bao giá»
    /// kÃ©o toÃ n bá»™ báº£ng nha_cung_cap vá» rá»“i má»›i lá»c á»Ÿ C#.
    /// </summary>
    public async Task<(IReadOnlyList<NhaCungCap> Items, int TongSoBanGhi)> GetNhaCungCapPagedAsync(
        BoLocNhaCungCap loc,
        int page,
        int pageSize)
    {
        loc ??= new BoLocNhaCungCap();

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        var offset = (safePage - 1) * safePageSize;

        await using var connection = await OpenAsync();

        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();
        var bieuThucConNo = TaoBieuThucTienNhaCungCap("ncc.id", coCotKiotViet).NoHienTai;
        var dieuKien = TaoDieuKienNhaCungCap(loc, bieuThucConNo);
        var thamSo = TaoThamSoNhaCungCap(loc);

        var countSql = $"""
        SELECT COUNT(*)
        {NhaCungCapFrom}
        WHERE {dieuKien};
        """;

        var tongSoBanGhi = await connection.ExecuteScalarAsync<int>(countSql, thamSo);

        if (tongSoBanGhi == 0)
            return (Array.Empty<NhaCungCap>(), 0);

        var pageSql = $"""
        SELECT
        {TaoDanhSachCotNhaCungCap(coCotKiotViet)}
        {NhaCungCapFrom}
        WHERE {dieuKien}
        ORDER BY ncc.ten_nha_cung_cap, ncc.id
        LIMIT @Limit OFFSET @Offset;
        """;

        var items = (
            await connection.QueryAsync<NhaCungCap>(
                pageSql,
                GopThamSo(thamSo, safePageSize, offset))
        ).AsList();

        return (items, tongSoBanGhi);
    }

    /// <summary>
    /// Láº¥y TOÃ€N Bá»˜ nhÃ  cung cáº¥p khá»›p bá»™ lá»c Ä‘á»ƒ xuáº¥t file Excel, khÃ´ng phÃ¢n trang.
    ///
    /// Cá»‘ tÃ¬nh dÃ¹ng chung <see cref="TaoDieuKienNhaCungCap"/> vÃ  chung bá»™ cá»™t
    /// tiá»n vá»›i truy váº¥n láº¥y danh sÃ¡ch trÃªn mÃ n hÃ¬nh, nÃªn sá»‘ tiá»n xuáº¥t ra file
    /// luÃ´n khá»›p vá»›i tháº» thá»‘ng kÃª vÃ  cá»™t "Tá»•ng mua"/"CÃ²n ná»£" Ä‘ang hiá»ƒn thá»‹.
    /// KhÃ´ng cÃ³ LIMIT/OFFSET nÃªn xuáº¥t 48 dÃ²ng khi trang Ä‘ang xem 20 dÃ²ng.
    /// </summary>
    public async Task<IReadOnlyList<NhaCungCap>> GetAllNhaCungCapForExportAsync(
        BoLocNhaCungCap loc)
    {
        loc ??= new BoLocNhaCungCap();

        await using var connection = await OpenAsync();

        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();
        var bieuThucConNo = TaoBieuThucTienNhaCungCap("ncc.id", coCotKiotViet).NoHienTai;
        var dieuKien = TaoDieuKienNhaCungCap(loc, bieuThucConNo);

        var sql = $"""
        SELECT
        {TaoDanhSachCotNhaCungCap(coCotKiotViet)},
        {TaoDanhSachCotFileNhaCungCap()}
        {NhaCungCapFrom}
        WHERE {dieuKien}
        ORDER BY ncc.ten_nha_cung_cap, ncc.id;
        """;

        return (
            await connection.QueryAsync<NhaCungCap>(sql, TaoThamSoNhaCungCap(loc))
        ).AsList();
    }

    /// <summary>
    /// Thá»‘ng kÃª phÃ­a trÃªn báº£ng nhÃ  cung cáº¥p. CÃ¹ng bá»™ lá»c vá»›i truy váº¥n láº¥y danh
    /// sÃ¡ch nÃªn khi Ä‘á»•i bá»™ lá»c thÃ¬ cáº£ báº£ng láº«n thá»‘ng kÃª cÃ¹ng cáº­p nháº­t.
    /// </summary>
    public async Task<ThongKeDanhSachNhaCungCap> GetThongKeDanhSachNhaCungCapAsync(
        BoLocNhaCungCap loc)
    {
        loc ??= new BoLocNhaCungCap();

        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();
        var tien = TaoBieuThucTienNhaCungCap("ncc.id", coCotKiotViet);
        var dieuKien = TaoDieuKienNhaCungCap(loc, tien.NoHienTai);

        var sql = $"""
        SELECT
            COUNT(*) AS TongSoNhaCungCap,
            COALESCE(
                SUM(
                    CASE
                        WHEN COALESCE({tien.NoHienTai}, 0) > 0 THEN 1
                        ELSE 0
                    END
                ),
                0
            ) AS SoNhaCungCapConNo,
            COALESCE(
                SUM(
                    CASE
                        WHEN COALESCE({tien.NoHienTai}, 0) > 0 THEN COALESCE({tien.NoHienTai}, 0)
                        ELSE 0
                    END
                ),
                0
            ) AS TongCongNo,
            COALESCE(SUM(COALESCE({tien.TongMua}, 0)), 0) AS TongMua,
            COALESCE(SUM(COALESCE({tien.TongMuaTruTraHang}, 0)), 0) AS TongMuaTruTraHang
        {NhaCungCapFrom}
        WHERE {dieuKien};
        """;

        await using var connection = await OpenAsync();

        var thongKe = await connection.QueryFirstOrDefaultAsync<ThongKeDanhSachNhaCungCap>(
            sql,
            TaoThamSoNhaCungCap(loc));

        return thongKe ?? new ThongKeDanhSachNhaCungCap();
    }

    /// <summary>Láº¥y Ä‘áº§y Ä‘á»§ thÃ´ng tin má»™t nhÃ  cung cáº¥p Ä‘á»ƒ má»Ÿ vÃ¹ng chi tiáº¿t / form sá»­a.</summary>
    public async Task<NhaCungCap?> GetNhaCungCapByIdAsync(uint supplierId)
    {
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        var sql = $"""
        SELECT
        {TaoDanhSachCotNhaCungCap(coCotKiotViet)},
        {TaoDanhSachCotFileNhaCungCap()}
        {NhaCungCapFrom}
        WHERE ncc.trang_thai = 1
          AND ncc.id = @Id;
        """;

        await using var connection = await OpenAsync();

        return await connection.QueryFirstOrDefaultAsync<NhaCungCap>(
            sql,
            new { Id = supplierId });
    }

    /// <summary>
    /// Lá»‹ch sá»­ nháº­p hÃ ng cá»§a má»™t nhÃ  cung cáº¥p. Chá»‰ gá»i khi ngÆ°á»i dÃ¹ng má»Ÿ vÃ¹ng
    /// chi tiáº¿t, vÃ  chá»‰ láº¥y <paramref name="limit"/> phiáº¿u gáº§n nháº¥t.
    /// </summary>
    public async Task<IReadOnlyList<PhieuNhapHang>> GetPhieuNhapCuaNhaCungCapAsync(
        uint supplierId,
        int limit = 200)
    {
        var safeLimit = limit < 1 ? 200 : limit;
        var hasPhuongThuc = await EnsurePhieuNhapPhuongThucColumnAsync();
        var extraSelect = hasPhuongThuc
            ? ",\n            COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan"
            : string.Empty;

        var sql = $"""
        SELECT
            p.id AS Id,
            p.ma_phieu_nhap AS MaPhieuNhap,
            p.nha_cung_cap_id AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            p.nguoi_nhap_id AS NguoiNhapId,
            nd.ho_ten AS TenNhanVien,
            p.thoi_gian_nhap AS ThoiGianNhap,
            p.tong_tien_hang AS TongTienHang,
            p.chi_phi_khac AS ChiPhiKhac,
            p.tong_tien_nhap AS TongTienNhap,
            p.da_thanh_toan AS DaThanhToan,
            p.con_no AS ConNo,
            p.trang_thai AS TrangThai,
            p.ghi_chu AS GhiChu{extraSelect}
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung nd
            ON nd.id = p.nguoi_nhap_id
        WHERE p.nha_cung_cap_id = @Id
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC
        LIMIT @Limit;
        """;

        await using var connection = await OpenAsync();

        return (
            await connection.QueryAsync<PhieuNhapHang>(
                sql,
                new { Id = supplierId, Limit = safeLimit })
        ).AsList();
    }

    /// <summary>
    /// Sá»• cÃ¡i cÃ´ng ná»£ cá»§a má»™t nhÃ  cung cáº¥p (báº£ng lich_su_cong_no_nha_cung_cap).
    /// CÅ©ng chá»‰ Ä‘á»c khi ngÆ°á»i dÃ¹ng má»Ÿ chi tiáº¿t.
    /// </summary>
    public async Task<IReadOnlyList<LichSuCongNoNhaCungCap>> GetLichSuCongNoNhaCungCapAsync(
        uint supplierId,
        int limit = 200)
    {
        var safeLimit = limit < 1 ? 200 : limit;

        const string sql = """
        SELECT
            ls.id AS Id,
            ls.loai_giao_dich AS LoaiGiaoDich,
            ls.ma_chung_tu AS MaChungTu,
            ls.ghi_no AS GhiNo,
            ls.ghi_co AS GhiCo,
            ls.so_du AS SoDu,
            ls.thoi_gian AS ThoiGian,
            ls.ghi_chu AS GhiChu
        FROM lich_su_cong_no_nha_cung_cap ls
        WHERE ls.nha_cung_cap_id = @Id
        ORDER BY ls.thoi_gian DESC, ls.id DESC
        LIMIT @Limit;
        """;

        await using var connection = await OpenAsync();

        return (
            await connection.QueryAsync<LichSuCongNoNhaCungCap>(
                sql,
                new { Id = supplierId, Limit = safeLimit })
        ).AsList();
    }

    /// <summary>
    /// Tá»•ng há»£p tÃ i chÃ­nh cá»§a má»™t nhÃ  cung cáº¥p: tá»•ng tiá»n nháº­p, Ä‘Ã£ thanh toÃ¡n,
    /// cÃ²n ná»£ (theo sá»• cÃ¡i cÃ´ng ná»£ náº¿u cÃ³, khÃ´ng thÃ¬ theo phiáº¿u nháº­p hÃ ng).
    /// </summary>
    public async Task<ThongKeTaiChinhNhaCungCap> GetThongKeTaiChinhNhaCungCapAsync(uint supplierId)
    {
        const string phieuSql = """
        SELECT
            COUNT(*) AS SoPhieuNhap,
            COALESCE(SUM(p.tong_tien_nhap), 0) AS TongTienNhap,
            COALESCE(SUM(p.da_thanh_toan), 0) AS TongDaThanhToan,
            COALESCE(SUM(p.con_no), 0) AS TongConNoPhieuNhap
        FROM phieu_nhap_hang p
        WHERE p.nha_cung_cap_id = @Id
          AND p.trang_thai NOT IN ('LUU_TAM', 'HUY');
        """;

        const string congNoSql = """
        SELECT
            COUNT(ls.id) AS SoLanGhiCongNo,
            COALESCE(SUM(ls.ghi_no - ls.ghi_co), 0) AS TongCongNoLichSu,
            MAX(ls.thoi_gian) AS LanGhiCongNoGanNhat
        FROM lich_su_cong_no_nha_cung_cap ls
        WHERE ls.nha_cung_cap_id = @Id;
        """;

        await using var connection = await OpenAsync();

        var thamSo = new { Id = supplierId };

        var phieu = await connection.QueryFirstOrDefaultAsync<NhaCungCapPhieuTongHopRow>(
            phieuSql,
            thamSo) ?? new NhaCungCapPhieuTongHopRow();

        var congNo = await connection.QueryFirstOrDefaultAsync<NhaCungCapCongNoTongHopRow>(
            congNoSql,
            thamSo) ?? new NhaCungCapCongNoTongHopRow();

        var nhacCungCap = await GetNhaCungCapByIdAsync(supplierId);

        return new ThongKeTaiChinhNhaCungCap
        {
            SoPhieuNhap = phieu.SoPhieuNhap,
            TongTienNhap = phieu.TongTienNhap,
            TongDaThanhToan = phieu.TongDaThanhToan,
            TongConNoPhieuNhap = phieu.TongConNoPhieuNhap,
            SoLanGhiCongNo = congNo.SoLanGhiCongNo,
            TongCongNoLichSu = congNo.TongCongNoLichSu,
            LanGhiCongNoGanNhat = congNo.LanGhiCongNoGanNhat,
            CoDuLieuKiotViet = nhacCungCap?.CoDuLieuKiotViet == true,
            NoCanTraHienTaiExcel = nhacCungCap?.NoCanTraHienTaiExcel,
            TongMuaExcel = nhacCungCap?.TongMuaExcel,
            TongMuaTruTraHangExcel = nhacCungCap?.TongMuaTruTraHangExcel
        };
    }

    /// <summary>
    /// Trong sá»‘ mÃ£ nhÃ  cung cáº¥p Ä‘Æ°á»£c truyá»n vÃ o, tráº£ vá» táº­p mÃ£ Ä‘Ã£ tá»“n táº¡i trong
    /// database (ká»ƒ cáº£ nhÃ  cung cáº¥p Ä‘Ã£ bá»‹ áº©n). DÃ¹ng cho bÆ°á»›c kiá»ƒm tra trÃ¹ng khi
    /// import: chá»‰ há»i Ä‘Ãºng cÃ¡c mÃ£ trong file thay vÃ¬ táº£i toÃ n bá»™ danh sÃ¡ch NCC.
    /// </summary>
    public async Task<IReadOnlySet<string>> LayMaNhaCungCapDaTonTaiAsync(
        IEnumerable<string> maNhaCungCap)
    {
        var maList = maNhaCungCap
            .Select(ma => (ma ?? "").Trim())
            .Where(ma => ma.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (maList.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sql = $"""
        SELECT ma_nha_cung_cap
        FROM nha_cung_cap
        WHERE ma_nha_cung_cap IN ({string.Join(", ", maList.Select((_, i) => "@Ma" + i))});
        """;

        var thamSo = new Dictionary<string, object>();

        for (var i = 0; i < maList.Count; i++)
            thamSo["Ma" + i] = maList[i];

        await using var connection = await OpenAsync();

        var rows = (
            await connection.QueryAsync<string>(sql, thamSo)
        ).AsList();

        return new HashSet<string>(
            rows.Where(ma => !string.IsNullOrWhiteSpace(ma)),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> TaoMaNhaCungCapMoiAsync()
    {
        await using var connection = await OpenAsync();

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_nha_cung_cap, 4) AS UNSIGNED)),
            0
        ) + 1
        FROM nha_cung_cap
        WHERE ma_nha_cung_cap REGEXP '^NCC[0-9]{1,7}$';
        """;

        var nextNumber = await connection.ExecuteScalarAsync<long>(sql);

        if (nextNumber > 9_999_999)
            throw new InvalidOperationException("ÄÃ£ vÆ°á»£t giá»›i háº¡n mÃ£ nhÃ  cung cáº¥p tá»± Ä‘á»™ng NCC9999999.");

        return $"NCC{nextNumber:D7}";
    }

    private bool _daMoRongCotSoDienThoaiNcc;

    private async Task DamBaoDoDaiSoDienThoaiNhaCungCapAsync(MySqlConnection connection)
    {
        if (_daMoRongCotSoDienThoaiNcc)
            return;

        try
        {
            await connection.ExecuteAsync(
                "ALTER TABLE nha_cung_cap MODIFY COLUMN so_dien_thoai VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL;");
            _daMoRongCotSoDienThoaiNcc = true;
        }
        catch
        {
            // Bá» qua náº¿u khÃ´ng Ä‘á»§ quyá»n ALTER hoáº·c cá»™t Ä‘Ã£ thá»a mÃ£n
            _daMoRongCotSoDienThoaiNcc = true;
        }
    }

    /// <summary>
    /// Kiá»ƒm tra xem mÃ£ nhÃ  cung cáº¥p Ä‘Ã£ tá»“n táº¡i trong database chÆ°a (bá» qua id hiá»‡n táº¡i náº¿u cÃ³).
    /// </summary>
    public async Task<bool> KiemTraTrungMaNhaCungCapAsync(string maNhaCungCap, uint idHienTai = 0)
    {
        if (string.IsNullOrWhiteSpace(maNhaCungCap))
            return false;

        await using var connection = await OpenAsync();
        const string sql = """
        SELECT COUNT(*)
        FROM nha_cung_cap
        WHERE ma_nha_cung_cap = @Ma
          AND (@Id = 0 OR id != @Id)
          AND trang_thai = 1;
        """;

        var count = await connection.ExecuteScalarAsync<int>(
            sql,
            new { Ma = maNhaCungCap.Trim(), Id = idHienTai });

        return count > 0;
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch cÃ¡c nhÃ³m nhÃ  cung cáº¥p Ä‘Ã£ cÃ³ trong database Ä‘á»ƒ gá»£i Ã½ khi nháº­p liá»‡u.
    /// </summary>
    public async Task<IReadOnlyList<string>> LayDanhSachNhomNhaCungCapAsync()
    {
        try
        {
            await using var connection = await OpenAsync();
            var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();
            if (!coCotKiotViet)
                return [];

            const string sql = """
            SELECT DISTINCT nhom_nha_cung_cap_ten
            FROM nha_cung_cap
            WHERE nhom_nha_cung_cap_ten IS NOT NULL
              AND TRIM(nhom_nha_cung_cap_ten) != ''
            ORDER BY nhom_nha_cung_cap_ten;
            """;

            var rows = await connection.QueryAsync<string>(sql);
            return rows.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<uint> ThemNhaCungCapAsync(NhaCungCap supplier)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersCreate, "Báº¡n khÃ´ng cÃ³ quyá»n thÃªm nhÃ  cung cáº¥p.");
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        if (string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
            supplier.MaNhaCungCap = await TaoMaNhaCungCapMoiAsync();

        await using var connection = await OpenAsync();
        await DamBaoDoDaiSoDienThoaiNhaCungCapAsync(connection);

        // Báº£n Ä‘áº§y Ä‘á»§ ghi cáº£ cÃ¡c cá»™t láº¥y tá»« file danh sÃ¡ch nhÃ  cung cáº¥p. Náº¿u báº£ng
        // cÅ© khÃ´ng thÃªm Ä‘Æ°á»£c cá»™t thÃ¬ dÃ¹ng báº£n rÃºt gá»n nhÆ° trÆ°á»›c Ä‘á»ƒ váº«n nháº­p Ä‘Æ°á»£c.
        var sql = coCotKiotViet
            ? """
            INSERT INTO nha_cung_cap
            (
                ma_nha_cung_cap, ten_nha_cung_cap, so_dien_thoai, email, dia_chi,
                phuong_xa, quan_huyen, tinh_thanh, ma_so_thue, nguoi_lien_he, ghi_chu,
                chi_nhanh_id, trang_thai,
                ngay_tao, ngay_cap_nhat,
                co_du_lieu_kiotviet, nguoi_tao_ten, ngay_nhap_excel,
                no_can_tra_hien_tai, tong_mua, tong_mua_tru_tra_hang,
                khu_vuc, so_cccd, nhom_nha_cung_cap_ten, ten_cong_ty
            )
            VALUES
            (
                @MaNhaCungCap, @TenNhaCungCap, @SoDienThoai, @Email, @DiaChi,
                @PhuongXa, @QuanHuyen, @TinhThanh, @MaSoThue, @NguoiLienHe, @GhiChu,
                @ChiNhanhId, @TrangThai,
                COALESCE(@NgayTao, NOW()), NOW(),
                @CoDuLieuKiotViet, @NguoiTaoTen, @NgayNhapExcel,
                COALESCE(@NoCanTraHienTaiExcel, 0), COALESCE(@TongMuaExcel, 0), COALESCE(@TongMuaTruTraHangExcel, 0),
                @KhuVuc, @SoCccd, @NhomNhaCungCapTen, @TenCongTy
            );
            SELECT LAST_INSERT_ID();
            """
            : """
            INSERT INTO nha_cung_cap
            (
                ma_nha_cung_cap, ten_nha_cung_cap, so_dien_thoai, email, dia_chi,
                phuong_xa, quan_huyen, tinh_thanh, ma_so_thue, nguoi_lien_he, ghi_chu,
                chi_nhanh_id, trang_thai, ngay_tao, ngay_cap_nhat
            )
            VALUES
            (
                @MaNhaCungCap, @TenNhaCungCap, @SoDienThoai, @Email, @DiaChi,
                @PhuongXa, @QuanHuyen, @TinhThanh, @MaSoThue, @NguoiLienHe, @GhiChu,
                @ChiNhanhId, @TrangThai, COALESCE(@NgayTao, NOW()), NOW()
            );
            SELECT LAST_INSERT_ID();
            """;

        return await connection.ExecuteScalarAsync<uint>(
            sql,
            new
            {
                supplier.MaNhaCungCap,
                supplier.TenNhaCungCap,
                supplier.SoDienThoai,
                supplier.Email,
                supplier.DiaChi,
                supplier.PhuongXa,
                supplier.QuanHuyen,
                supplier.TinhThanh,
                supplier.MaSoThue,
                supplier.NguoiLienHe,
                supplier.GhiChu,
                supplier.TrangThai,
                supplier.NgayTao,
                supplier.CoDuLieuKiotViet,
                supplier.NguoiTaoTen,
                supplier.NgayNhapExcel,
                supplier.NoCanTraHienTaiExcel,
                supplier.TongMuaExcel,
                supplier.TongMuaTruTraHangExcel,
                supplier.KhuVuc,
                supplier.SoCccd,
                supplier.NhomNhaCungCapTen,
                supplier.TenCongTy,
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId
            });
    }

    public async Task CapNhatNhaCungCapAsync(NhaCungCap supplier)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a nhÃ  cung cáº¥p.");
        await using var connection = await OpenAsync();
        await DamBaoDoDaiSoDienThoaiNhaCungCapAsync(connection);
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        var sql = coCotKiotViet
            ? """
            UPDATE nha_cung_cap
            SET ma_nha_cung_cap = @MaNhaCungCap,
                ten_nha_cung_cap = @TenNhaCungCap,
                so_dien_thoai = @SoDienThoai,
                email = @Email,
                dia_chi = @DiaChi,
                phuong_xa = @PhuongXa,
                quan_huyen = @QuanHuyen,
                tinh_thanh = @TinhThanh,
                ma_so_thue = @MaSoThue,
                nguoi_lien_he = @NguoiLienHe,
                ghi_chu = @GhiChu,
                nhom_nha_cung_cap_ten = @NhomNhaCungCapTen,
                ten_cong_ty = @TenCongTy,
                trang_thai = @TrangThai,
                ngay_cap_nhat = NOW()
            WHERE id = @Id;
            """
            : """
            UPDATE nha_cung_cap
            SET ma_nha_cung_cap = @MaNhaCungCap,
                ten_nha_cung_cap = @TenNhaCungCap,
                so_dien_thoai = @SoDienThoai,
                email = @Email,
                dia_chi = @DiaChi,
                phuong_xa = @PhuongXa,
                quan_huyen = @QuanHuyen,
                tinh_thanh = @TinhThanh,
                ma_so_thue = @MaSoThue,
                nguoi_lien_he = @NguoiLienHe,
                ghi_chu = @GhiChu,
                trang_thai = @TrangThai,
                ngay_cap_nhat = NOW()
            WHERE id = @Id;
            """;

        var affectedRows = await connection.ExecuteAsync(sql, supplier);
        if (affectedRows == 0)
            throw new InvalidOperationException("NhÃ  cung cáº¥p khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ bá»‹ xÃ³a.");
    }

    /// <summary>
    /// Cáº­p nháº­t nhÃ  cung cáº¥p vá»›i Ä‘áº§y Ä‘á»§ dá»¯ liá»‡u Ä‘á»c tá»« file danh sÃ¡ch, bao gá»“m
    /// ba cá»™t tiá»n, ngÆ°á»i táº¡o, ngÃ y táº¡o vÃ  cÃ¡c cá»™t chá»¯ cÃ²n láº¡i. Chá»‰ dÃ¹ng cho
    /// bÆ°á»›c cáº­p nháº­t khi nháº­p file, khÃ´ng dÃ¹ng cho form sá»­a (form sá»­a khÃ´ng
    /// Ä‘Æ°á»£c phÃ©p xoÃ¡ sá»‘ tiá»n láº¥y tá»« file).
    /// </summary>
    public async Task CapNhatNhaCungCapTuFileAsync(NhaCungCap supplier)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a nhÃ  cung cáº¥p.");
        await using var connection = await OpenAsync();

        const string sql = """
        UPDATE nha_cung_cap
        SET ten_nha_cung_cap = @TenNhaCungCap,
            so_dien_thoai = @SoDienThoai,
            email = @Email,
            dia_chi = @DiaChi,
            phuong_xa = @PhuongXa,
            quan_huyen = @QuanHuyen,
            tinh_thanh = @TinhThanh,
            ma_so_thue = @MaSoThue,
            nguoi_lien_he = @NguoiLienHe,
            ghi_chu = @GhiChu,
            khu_vuc = @KhuVuc,
            so_cccd = @SoCccd,
            nhom_nha_cung_cap_ten = @NhomNhaCungCapTen,
            ten_cong_ty = @TenCongTy,
            trang_thai = @TrangThai,
            co_du_lieu_kiotviet = @CoDuLieuKiotViet,
            nguoi_tao_ten = @NguoiTaoTen,
            ngay_nhap_excel = @NgayNhapExcel,
            no_can_tra_hien_tai = @NoCanTraHienTaiExcel,
            tong_mua = @TongMuaExcel,
            tong_mua_tru_tra_hang = @TongMuaTruTraHangExcel,
            ngay_tao = COALESCE(@NgayTao, ngay_tao),
            ngay_cap_nhat = NOW()
        WHERE ma_nha_cung_cap = @MaNhaCungCap;
        """;

        await connection.ExecuteAsync(sql, supplier);
    }

    public async Task XoaNhaCungCapAsync(uint supplierId)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a nhÃ  cung cáº¥p.");
        await using var connection = await OpenAsync();

        var affectedRows = await connection.ExecuteAsync(
            "UPDATE nha_cung_cap SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id AND trang_thai = 1;",
            new { Id = supplierId });

        if (affectedRows == 0)
            throw new InvalidOperationException("NhÃ  cung cáº¥p khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ bá»‹ xÃ³a.");
    }

    public async Task<int> ImportNhaCungCapTuFileAsync(string filePath)
        => (await ImportNhaCungCapTuFileAsync(filePath, null, null)).SoThemMoi;

    public async Task<int> ImportNhaCungCapTuFileAsync(string filePath, IReadOnlyCollection<NhaCungCap>? suppliersOverride)
        => (await ImportNhaCungCapTuFileAsync(filePath, suppliersOverride, null)).SoThemMoi;

    /// <summary>
    /// Nháº­p danh sÃ¡ch nhÃ  cung cáº¥p tá»« file vÃ  tráº£ vá» bÃ¡o cÃ¡o chi tiáº¿t Ä‘á»ƒ
    /// ngÆ°á»i dÃ¹ng biáº¿t chÃ­nh xÃ¡c tá»•ng sá»‘ dÃ²ng, sá»‘ thÃ nh cÃ´ng, sá»‘ bá» qua vÃ  sá»‘ lá»—i.
    /// MÃ£ trÃ¹ng vá»›i database hoáº·c trÃ¹ng ngay trong file Ä‘á»u Ä‘Æ°á»£c bá» qua an toÃ n,
    /// trá»« khi <paramref name="capNhatNhaCungCapDaTonTai"/> = true: khi Ä‘Ã³ nhÃ  cung
    /// cáº¥p cÃ³ mÃ£ Ä‘Ã£ cÃ³ trong database Ä‘Æ°á»£c cáº­p nháº­t láº¡i báº±ng dá»¯ liá»‡u cá»§a file.
    /// </summary>
    public async Task<KetQuaImportNhaCungCap> ImportNhaCungCapTuFileAsync(
        string filePath,
        IReadOnlyCollection<NhaCungCap>? suppliersOverride,
        IProgress<int>? progress,
        bool capNhatNhaCungCapDaTonTai = false)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersImport, "Báº¡n khÃ´ng cÃ³ quyá»n import nhÃ  cung cáº¥p.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("ÄÆ°á»ng dáº«n file khÃ´ng há»£p lá»‡.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("KhÃ´ng tÃ¬m tháº¥y file danh sÃ¡ch nhÃ  cung cáº¥p.", filePath);

        var suppliers = suppliersOverride ?? ParseNhaCungCapFromFile(filePath);

        // Khi chá»n cáº­p nháº­t nhÃ  cung cáº¥p Ä‘Ã£ cÃ³, báº£o Ä‘áº£m cÃ¡c cá»™t lÆ°u dá»¯ liá»‡u tá»«
        // file Ä‘Ã£ tá»“n táº¡i trÆ°á»›c khi UPDATE, náº¿u khÃ´ng toÃ n bá»™ file sáº½ bá»‹ lá»—i.
        if (capNhatNhaCungCapDaTonTai)
            await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        var result = new KetQuaImportNhaCungCap
        {
            TenFile = Path.GetFileName(filePath),
            SoDongExcel = suppliers.Count
        };

        if (suppliers.Count == 0)
            return result;

        // Ã” tÃªn trá»‘ng thÃ¬ khÃ´ng táº¡o Ä‘Æ°á»£c nhÃ  cung cáº¥p: bá» qua vÃ  ghi log,
        // khÃ´ng nÃ©m lá»—i Ä‘á»ƒ pháº§n cÃ²n láº¡i cá»§a file váº«n Ä‘Æ°á»£c nháº­p.
        var hopLe = new List<NhaCungCap>(suppliers.Count);

        for (var i = 0; i < suppliers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(suppliers.ElementAt(i).TenNhaCungCap))
            {
                result.SoLoi++;
                GhiLoi(result, $"DÃ²ng {i + 2}: thiáº¿u tÃªn nhÃ  cung cáº¥p.");
            }
            else
            {
                hopLe.Add(suppliers.ElementAt(i));
            }
        }

        result.SoNhaCungCapHopLe = hopLe.Count;

        if (hopLe.Count == 0)
            return result;

        // Chá»‰ há»i database vá» nhá»¯ng mÃ£ thá»±c sá»± cÃ³ trong file, khÃ´ng táº£i toÃ n bá»™ danh sÃ¡ch.
        var maTrongDatabase = await LayMaNhaCungCapDaTonTaiAsync(
            hopLe.Select(ncc => ncc.MaNhaCungCap));

        var maDaDung = new HashSet<string>(maTrongDatabase, StringComparer.OrdinalIgnoreCase);

        // Cáº£nh bÃ¡o má»m: tÃªn + sá»‘ Ä‘iá»‡n thoáº¡i trÃ¹ng nhÃ  cung cáº¥p Ä‘Ã£ cÃ³.
        // Báº£ng khÃ´ng cÃ³ UNIQUE cho hai cá»™t nÃ y nÃªn váº«n nháº­p, chá»‰ bÃ¡o cho ngÆ°á»i dÃ¹ng.
        result.SoTrungTenVaDienThoai = await DemNhaCungCapTrungTenVaDienThoaiAsync(
            hopLe.Select(ncc => (ncc.TenNhaCungCap, ncc.SoDienThoai)));

        for (var i = 0; i < hopLe.Count; i++)
        {
            var supplier = hopLe[i];

            progress?.Report(i);

            if (string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
                supplier.MaNhaCungCap = GenerateSupplierCode();

            var ma = supplier.MaNhaCungCap.Trim();

            if (maDaDung.Contains(ma))
            {
                // MÃ£ Ä‘Ã£ cÃ³ trong database.
                if (capNhatNhaCungCapDaTonTai)
                {
                    try
                    {
                        await CapNhatNhaCungCapTuFileAsync(supplier);
                        result.SoCapNhat++;
                    }
                    catch (Exception ex)
                    {
                        result.SoLoi++;
                        GhiLoi(result, $"DÃ²ng {i + 2} ({ma}): {ex.Message}");
                    }
                }
                else
                {
                    result.SoTrungMa++;
                }
            }
            else if (!maDaDung.Add(ma))
            {
                // MÃ£ bá»‹ trÃ¹ng ngay trong chÃ­nh file nÃ y.
                result.SoTrungMaTrongFile++;
            }
            else
            {
                try
                {
                    await ThemNhaCungCapAsync(supplier);
                    result.SoThemMoi++;
                }
                catch (MySqlException ex) when (ex.Number == 1062)
                {
                    // CÃ³ tiáº¿n trÃ¬nh khÃ¡c vá»«a chÃ¨n mÃ£ nÃ y: bá» qua vÃ  tiáº¿p tá»¥c.
                    result.SoTrungMa++;
                }
                catch (Exception ex)
                {
                    result.SoLoi++;
                    GhiLoi(result, $"DÃ²ng {i + 2} ({ma}): {ex.Message}");
                }
            }
        }

        progress?.Report(hopLe.Count);

        return result;
    }

    /// <summary>
    /// Äáº¿m sá»‘ nhÃ  cung cáº¥p trong danh sÃ¡ch Ä‘ang xÃ©t trÃ¹ng (tÃªn + sá»‘ Ä‘iá»‡n thoáº¡i)
    /// vá»›i nhÃ  cung cáº¥p Ä‘Ã£ cÃ³ trong database. Chá»‰ dÃ¹ng Ä‘á»ƒ bÃ¡o ngÆ°á»i dÃ¹ng.
    /// </summary>
    public async Task<int> DemNhaCungCapTrungTenVaDienThoaiAsync(
        IEnumerable<(string Ten, string? SoDienThoai)> danhSach)
    {
        var cap = danhSach
            .Where(item => !string.IsNullOrWhiteSpace(item.Ten))
            .GroupBy(
                item => (Ten: item.Ten.Trim(), Sdt: (item.SoDienThoai ?? "").Trim()),
                new CapNhaCungCapSoQuy())
            .Select(group => group.Key)
            .ToList();

        if (cap.Count == 0)
            return 0;

        var dieuKien = new List<string>(cap.Count);
        var thamSo = new Dictionary<string, object>(cap.Count * 2);

        for (var i = 0; i < cap.Count; i++)
        {
            var ten = "Ten" + i;
            var sdt = "Sdt" + i;

            thamSo[ten] = cap[i].Ten;

            if (cap[i].Sdt.Length == 0)
            {
                dieuKien.Add($"(ten_nha_cung_cap = @{ten} AND (so_dien_thoai IS NULL OR so_dien_thoai = ''))");
            }
            else
            {
                dieuKien.Add($"(ten_nha_cung_cap = @{ten} AND so_dien_thoai = @{sdt})");
                thamSo[sdt] = cap[i].Sdt;
            }
        }

        var sql = $"""
        SELECT COUNT(*)
        FROM nha_cung_cap
        WHERE {string.Join(" OR ", dieuKien)};
        """;

        await using var connection = await OpenAsync();

        return await connection.ExecuteScalarAsync<int>(sql, thamSo);
    }

    private static void GhiLoi(KetQuaImportNhaCungCap result, string loi)
    {
        // Chá»‰ giá»¯ 20 lá»—i Ä‘áº§u Ä‘á»ƒ thÃ´ng bÃ¡o khÃ´ng bá»‹ quÃ¡ dÃ i.
        if (result.DanhSachLoi.Count < 20)
            result.DanhSachLoi.Add(loi);
    }

    private sealed class CapNhaCungCapSoQuy : IEqualityComparer<(string Ten, string Sdt)>
    {
        public bool Equals((string Ten, string Sdt) x, (string Ten, string Sdt) y)
            => string.Equals(x.Ten, y.Ten, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Sdt, y.Sdt, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Ten, string Sdt) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Ten?.ToUpperInvariant() ?? "", StringComparer.Ordinal);
            hash.Add(obj.Sdt?.ToUpperInvariant() ?? "", StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    public List<NhaCungCap> ParseNhaCungCapFromFile(string filePath)

    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "ÄÆ°á»ng dáº«n file khÃ´ng há»£p lá»‡.",
                nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "KhÃ´ng tÃ¬m tháº¥y file danh sÃ¡ch nhÃ  cung cáº¥p.",
                filePath);

        var rows = ReadSupplierRowsFromFile(filePath);

        return rows
            .Select(MapRowToNhaCungCap)
            .Where(supplier =>
                !string.IsNullOrWhiteSpace(supplier.TenNhaCungCap) ||
                !string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
            .ToList();
    }
    private static List<Dictionary<string, string>> ReadSupplierRowsFromFile(
    string filePath)
    {
        var extension = Path.GetExtension(filePath)
            .Trim()
            .ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvRows(filePath);

        if (extension == ".xlsx")
            return ReadSupplierExcelRows(filePath);

        throw new NotSupportedException(
            "File khÃ´ng Ä‘Æ°á»£c há»— trá»£. Vui lÃ²ng chá»n file Excel (.xlsx) hoáº·c CSV.");
    }
    private static List<Dictionary<string, string>> ReadSupplierExcelRows(
    string filePath)
    {
        using var workbook = new XLWorkbook(filePath);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
            throw new InvalidOperationException(
                "File Excel khÃ´ng chá»©a sheet dá»¯ liá»‡u há»£p lá»‡.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastRow == 0)
            return [];

        // TÃ¬m dÃ²ng tiÃªu Ä‘á» nhÃ  cung cáº¥p
        var headerRowNumber = FindSupplierHeaderRow(
            worksheet,
            lastRow);

        if (headerRowNumber == 0)
        {
            throw new InvalidOperationException(
                "KhÃ´ng tÃ¬m tháº¥y dÃ²ng tiÃªu Ä‘á» nhÃ  cung cáº¥p trong file.");
        }

        var firstHeaderColumn =
            worksheet.Row(headerRowNumber)
                .FirstCellUsed()
                ?.Address.ColumnNumber ?? 1;

        var lastHeaderColumn =
            worksheet.Row(headerRowNumber)
                .LastCellUsed()
                ?.Address.ColumnNumber ?? 0;

        if (lastHeaderColumn < firstHeaderColumn)
            return [];

        var headers = new List<string>();

        for (var column = firstHeaderColumn;
             column <= lastHeaderColumn;
             column++)
        {
            var header = worksheet
                .Cell(headerRowNumber, column)
                .GetString()
                .Trim();

            headers.Add(header);
        }

        var result =
            new List<Dictionary<string, string>>();

        for (var rowNumber = headerRowNumber + 1;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var row =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            var hasAnyValue = false;

            for (var index = 0;
                 index < headers.Count;
                 index++)
            {
                var header = headers[index];

                if (string.IsNullOrWhiteSpace(header))
                    continue;

                var columnNumber =
                    firstHeaderColumn + index;

                var value = worksheet
                    .Cell(rowNumber, columnNumber)
                    .GetFormattedString()
                    .Trim();

                if (!string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                row[header] = value;
            }

            if (hasAnyValue)
                result.Add(row);
        }

        return result;
    }
    private static int FindSupplierHeaderRow(
    IXLWorksheet worksheet,
    int lastRow)
    {
        for (var rowNumber = 1;
             rowNumber <= lastRow;
             rowNumber++)
        {
            var values = worksheet
                .Row(rowNumber)
                .CellsUsed()
                .Select(cell =>
                    NormalizeHeader(cell.GetString()))
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count == 0)
                continue;

            var hasSupplierCode =
                values.Any(value =>
                    value is
                        "manhacungcap" or
                        "mancc" or
                        "macungcap" or
                        "suppliercode");

            var hasSupplierName =
                values.Any(value =>
                    value is
                        "tennhacungcap" or
                        "tenncc" or
                        "suppliername");

            var hasPhone =
                values.Any(value =>
                    value is
                        "sodienthoai" or
                        "dienthoai" or
                        "sdt" or
                        "phone");

            if (hasSupplierCode ||
                hasSupplierName ||
                (hasPhone && values.Count >= 2))
            {
                return rowNumber;
            }
        }

        return 0;
    }
    private static NhaCungCap MapRowToNhaCungCap(Dictionary<string, string> row)
    {
        var noCanTra = ParseSoTien(
            GetCellValue(row, "no_can_tra_hien_tai", "ná»£ cáº§n tráº£ hiá»‡n táº¡i", "no can tra hien tai", "ná»£ hiá»‡n táº¡i", "no hien tai", "ná»£ cáº§n tráº£"));
        var tongMua = ParseSoTien(
            GetCellValue(row, "tong_mua", "tá»•ng mua", "tong mua"));
        var tongMuaTruTraHang = ParseSoTien(
            GetCellValue(row, "tong_mua_tru_tra_hang", "tá»•ng mua trá»« tráº£ hÃ ng", "tong mua tru tra hang", "tá»•ng mua trá»« tráº£", "tong mua tru tra"));

        var trangThai = ParseBoolean(
            GetCellValue(row, "trang_thai", "tráº¡ng thÃ¡i", "trang thai", "status"),
            true);

        var supplier = new NhaCungCap
        {
            MaNhaCungCap = GetCellValue(row, "ma_nha_cung_cap", "mÃ£ nhÃ  cung cáº¥p", "ma ncc", "supplier code") ?? string.Empty,
            TenNhaCungCap = GetCellValue(row, "ten_nha_cung_cap", "tÃªn nhÃ  cung cáº¥p", "ten ncc", "supplier name", "name") ?? string.Empty,
            SoDienThoai = GetCellValue(row, "so_dien_thoai", "sá»‘ Ä‘iá»‡n thoáº¡i", "dien thoai", "phone", "sdt"),
            Email = GetCellValue(row, "email"),
            DiaChi = GetCellValue(row, "dia_chi", "Ä‘á»‹a chá»‰", "dia chi", "address"),
            PhuongXa = GetCellValue(row, "phuong_xa", "phÆ°á»ng/xÃ£", "phuong xa", "ward"),
            QuanHuyen = GetCellValue(row, "quan_huyen", "quáº­n/huyá»‡n", "quan huyen", "district"),
            TinhThanh = GetCellValue(row, "tinh_thanh", "tá»‰nh/thÃ nh", "tinh thanh", "province"),
            MaSoThue = GetCellValue(row, "ma_so_thue", "mÃ£ sá»‘ thuáº¿", "ma so thue", "tax code"),
            NguoiLienHe = GetCellValue(row, "nguoi_lien_he", "ngÆ°á»i liÃªn há»‡", "lien he", "contact"),
            GhiChu = GetCellValue(row, "ghi_chu", "ghi chÃº", "note"),
            KhuVuc = GetCellValue(row, "khu_vuc", "khu vá»±c", "khu vuc", "region"),
            SoCccd = GetCellValue(row, "so_cmnd_cccd", "sá»‘ cmnd/cccd", "so cmnd cccd", "cmnd", "cccd"),
            NhomNhaCungCapTen = GetCellValue(row, "nhom_nha_cung_cap", "nhÃ³m nhÃ  cung cáº¥p", "nhom ncc", "nhÃ³m"),
            TenCongTy = GetCellValue(row, "cong_ty", "cÃ´ng ty", "ten cong ty"),
            NguoiTaoTen = GetCellValue(row, "nguoi_tao", "ngÆ°á»i táº¡o", "nguoi tao", "nguoi_tao_ten"),
            NgayTao = ParseNgayTaiFile(GetCellValue(row, "ngay_tao", "ngÃ y táº¡o", "ngay tao")),
            NgayNhapExcel = DateTime.Now,
            TrangThai = trangThai,
            NoCanTraHienTaiExcel = noCanTra,
            TongMuaExcel = tongMua,
            TongMuaTruTraHangExcel = tongMuaTruTraHang,
            CoDuLieuKiotViet = noCanTra.HasValue || tongMua.HasValue || tongMuaTruTraHang.HasValue
        };

        if (string.IsNullOrWhiteSpace(supplier.MaNhaCungCap))
            supplier.MaNhaCungCap = GenerateSupplierCode();

        return supplier;
    }
    private static string GenerateSupplierCode()
    {
        return $"NCC{DateTime.Now:yyyyMMddHHmmssfff}";
    }

    // ==========================================
    // QUáº¢N LÃ NHÃ“M Sáº¢N PHáº¨M & ÄÆ N Vá»Š TÃNH
    // Xem Services/DatabaseService.DanhMuc.cs
    // ==========================================

    // ==========================================
    // PHIáº¾U NHáº¬P HÃ€NG
    // ==========================================

    /// <summary>
    /// Sinh mÃ£ phiáº¿u nháº­p tá»± Ä‘á»™ng dáº¡ng PN000001, PN000002, PN000003...
    /// Sá»‘ thá»© tá»± láº¥y tá»« MAX cá»§a pháº§n sá»‘ trong cÃ¡c mÃ£ ÄÃšNG Äá»ŠNH Dáº NG:
    ///   - MÃ£ má»›i          : PN000001    (bá» 2 kÃ½ tá»± "PN", pháº§n cÃ²n láº¡i "000001")
    ///   - MÃ£ cÅ© cÃ³ ngÃ y  : PN260929-001 (bá» "PN", láº¥y Ä‘oáº¡n sau dáº¥u '-' cuá»‘i: "001")
    /// Má»i mÃ£ lá»‡ch Ä‘á»‹nh dáº¡ng (NULL, rá»—ng, chá»©a chá»¯) bá»‹ loáº¡i báº±ng REGEXP nÃªn dá»¯ liá»‡u
    /// rÃ¡c khÃ´ng lÃ m sai/há»ng sá»‘ thá»© tá»± vÃ  khÃ´ng lÃ m á»©ng dá»¥ng bá»‹ lá»—i.
    /// </summary>
    public async Task<string> TaoMaPhieuNhapMoiAsync()
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.PurchasesCreate, "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o phiáº¿u nháº­p hÃ ng.");
        await using var connection = await OpenAsync();

        // SUBSTRING(ma, 3) bá» Ä‘Ãºng 2 kÃ½ tá»± tiá»n tá»‘ "PN" (SQL Ä‘áº¿m tá»« 1).
        // SUBSTRING_INDEX(..., '-', -1) láº¥y Ä‘oáº¡n sau dáº¥u '-' cuá»‘i cÃ¹ng Ä‘á»ƒ váº«n láº¥y
        // Ä‘Ãºng sá»‘ thá»© tá»± vá»›i mÃ£ cÅ© dáº¡ng PNyyMMdd-NNN.
        //
        // CAST(... AS SIGNED) Ä‘á»ƒ giÃ¡ trá»‹ tráº£ vá» luÃ´n náº±m trong kiá»ƒu long.
        // Náº¿u dÃ¹ng AS UNSIGNED, chuá»—i báº¯t Ä‘áº§u báº±ng '-' sáº½ ra sá»‘ khá»•ng lá»“ vÃ  lÃ m
        // ExecuteScalarAsync<long> nÃ©m exception.
        const string maxSuffixSql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING_INDEX(SUBSTRING(TRIM(ma_phieu_nhap), 3), '-', -1) AS SIGNED)),
            0
        )
        FROM phieu_nhap_hang
        WHERE TRIM(ma_phieu_nhap) REGEXP '^PN[0-9]{6,}(-[0-9]+)?$';
        """;

        var maxSuffix = await connection.ExecuteScalarAsync<long?>(maxSuffixSql) ?? 0L;

        // Database chÆ°a cÃ³ phiáº¿u nÃ o (hoáº·c mÃ£ toÃ n sai Ä‘á»‹nh dáº¡ng) => báº¯t Ä‘áº§u tá»« PN000001.
        var nextNumber = maxSuffix > 0 ? maxSuffix + 1 : 1L;

        // ma_phieu_nhap cÃ³ UNIQUE KEY uk_pnh_ma: náº¿u mÃ£ vá»«a sinh trÃ¹ng
        // (vÃ­ dá»¥ 2 mÃ¡y cÃ¹ng má»Ÿ mÃ n hÃ¬nh) thÃ¬ tÄƒng tiáº¿p tá»›i khi ra mÃ£ cÃ²n trá»‘ng.
        for (var i = 0; i < 100; i++)
        {
            var candidate = "PN" + nextNumber.ToString("D6", CultureInfo.InvariantCulture);

            var daTonTai = await connection.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM phieu_nhap_hang WHERE ma_phieu_nhap = @Ma LIMIT 1;",
                new { Ma = candidate });

            if (daTonTai is null)
                return candidate;

            nextNumber++;
        }

        throw new InvalidOperationException(
            "KhÃ´ng sinh Ä‘Æ°á»£c mÃ£ phiáº¿u nháº­p: Ä‘Ã£ thá»­ 100 láº§n liÃªn tiáº¿p mÃ  mÃ£ váº«n bá»‹ trÃ¹ng.");
    }

    public async Task<IReadOnlyList<PhieuNhapHang>> GetPhieuNhapHangAsync(
        string? search = null)
    {
        var hasPhuongThuc = await EnsurePhieuNhapPhuongThucColumnAsync();
        var extraSelect = hasPhuongThuc
            ? ",\n            COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan"
            : string.Empty;

        await using var connection = await OpenAsync();

        var sql = $"""
        SELECT
            p.id AS Id,
            p.ma_phieu_nhap AS MaPhieuNhap,
            p.nha_cung_cap_id AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            p.nguoi_nhap_id AS NguoiNhapId,
            nd.ho_ten AS TenNhanVien,
            p.thoi_gian_nhap AS ThoiGianNhap,
            p.tong_tien_hang AS TongTienHang,
            p.chi_phi_khac AS ChiPhiKhac,
            p.tong_tien_nhap AS TongTienNhap,
            p.da_thanh_toan AS DaThanhToan,
            p.con_no AS ConNo,
            p.trang_thai AS TrangThai,
            p.ghi_chu AS GhiChu{extraSelect}
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung nd
            ON nd.id = p.nguoi_nhap_id
        WHERE (
                @Search = ''
                OR p.ma_phieu_nhap LIKE CONCAT('%', @Search, '%')
                OR ncc.ma_nha_cung_cap LIKE CONCAT('%', @Search, '%')
                OR ncc.ten_nha_cung_cap LIKE CONCAT('%', @Search, '%')
              )
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC;
        """;

        return (
            await connection.QueryAsync<PhieuNhapHang>(
                sql,
                new
                {
                    Search = search ?? ""
                })
        ).AsList();
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch phiáº¿u nháº­p theo trang cho bÃ¡o cÃ¡o vÃ  mÃ n "Danh sÃ¡ch phiáº¿u nháº­p".
    /// Há»— trá»£ tÃ¬m kiáº¿m theo mÃ£ phiáº¿u, mÃ£ NCC, tÃªn NCC; lá»c theo tÃ¬nh tráº¡ng thanh toÃ¡n,
    /// khoáº£ng thá»i gian nháº­p vÃ  tráº¡ng thÃ¡i phiáº¿u (Ä‘Ã£ nháº­p / lÆ°u táº¡m).
    /// Chá»‰ Ä‘á»c dá»¯ liá»‡u cá»§a trang hiá»‡n táº¡i (LIMIT/OFFSET) kÃ¨m tá»•ng sá»‘ báº£n ghi,
    /// khÃ´ng táº£i toÃ n bá»™ báº£ng lÃªn giao diá»‡n.
    /// CÃ¡c tham sá»‘ lá»c ngÃ y/tráº¡ng thÃ¡i lÃ  tuá»³ chá»n Ä‘á»ƒ giá»¯ tÆ°Æ¡ng thÃ­ch vá»›i cÃ¡c
    /// nÆ¡i gá»i cÅ© (chá»‰ truyá»n 4 tham sá»‘).
    /// </summary>
    public async Task<(IReadOnlyList<PhieuNhapHang> Items, int TongSoBanGhi)> GetPhieuNhapHangPagedAsync(
        string? search,
        int page,
        int pageSize,
        string? tinhTrangThanhToan = null,
        DateTime? tuNgay = null,
        DateTime? denNgay = null,
        string? trangThaiPhieu = null)
    {
        if (page < 1)
            page = 1;

        if (pageSize < 1)
            pageSize = 15;

        var offset = (page - 1) * pageSize;
        var keyword = (search ?? "").Trim();
        var hasPhuongThuc = await EnsurePhieuNhapPhuongThucColumnAsync();
        var extraSelect = hasPhuongThuc
            ? ",\n            COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan"
            : string.Empty;

        var paymentCondition = (tinhTrangThanhToan?.ToUpperInvariant()) switch
        {
            "DA_THANH_TOAN" => "AND p.con_no <= 0",
            "CHUA_THANH_TOAN" => "AND p.da_thanh_toan <= 0",
            "MOT_PHAN" => "AND p.da_thanh_toan > 0 AND p.con_no > 0",
            _ => ""
        };

        var dateCondition = "";
        if (tuNgay.HasValue)
            dateCondition += "\n          AND p.thoi_gian_nhap >= @TuNgay";
        if (denNgay.HasValue)
            dateCondition += "\n          AND p.thoi_gian_nhap < @DenNgayExclusive";

        var statusCondition = (trangThaiPhieu?.ToUpperInvariant()) switch
        {
            "DA_NHAP_HANG" => "\n          AND p.trang_thai = 'DA_NHAP_HANG'",
            "LUU_TAM" => "\n          AND p.trang_thai = 'LUU_TAM'",
            _ => ""
        };

        var searchCondition = @"(@Search = ''
            OR p.ma_phieu_nhap LIKE CONCAT('%', @Search, '%')
            OR ncc.ma_nha_cung_cap LIKE CONCAT('%', @Search, '%')
            OR ncc.ten_nha_cung_cap LIKE CONCAT('%', @Search, '%'))";

        var countSql = $"""
        SELECT COUNT(*)
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        WHERE {searchCondition}
          {paymentCondition}{dateCondition}{statusCondition};
        """;

        var pageSql = $"""
        SELECT
            p.id AS Id,
            p.ma_phieu_nhap AS MaPhieuNhap,
            p.nha_cung_cap_id AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            p.nguoi_nhap_id AS NguoiNhapId,
            nd.ho_ten AS TenNhanVien,
            p.thoi_gian_nhap AS ThoiGianNhap,
            p.tong_tien_hang AS TongTienHang,
            p.chi_phi_khac AS ChiPhiKhac,
            p.tong_tien_nhap AS TongTienNhap,
            p.da_thanh_toan AS DaThanhToan,
            p.con_no AS ConNo,
            p.trang_thai AS TrangThai,
            p.ghi_chu AS GhiChu{extraSelect}
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc
            ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung nd
            ON nd.id = p.nguoi_nhap_id
        WHERE {searchCondition}
          {paymentCondition}{dateCondition}{statusCondition}
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC
        LIMIT @PageSize OFFSET @Offset;
        """;

        await using var connection = await OpenAsync();

        var parameters = new
        {
            Search = keyword,
            PageSize = pageSize,
            Offset = offset,
            TuNgay = tuNgay,
            DenNgayExclusive = denNgay.HasValue ? denNgay.Value.Date.AddDays(1) : (DateTime?)null
        };

        var tongSoBanGhi = Convert.ToInt32(
            await connection.ExecuteScalarAsync<long>(countSql, parameters));

        var items = (
            await connection.QueryAsync<PhieuNhapHang>(pageSql, parameters)
        ).AsList();

        return (items, tongSoBanGhi);
    }

    /// <summary>
    /// Äá»c Ä‘áº§y Ä‘á»§ má»™t phiáº¿u nháº­p theo khoÃ¡ chÃ­nh phieu_nhap_hang.id Ä‘á»ƒ hiá»ƒn thá»‹
    /// cá»­a sá»• "Xem chi tiáº¿t": header + toÃ n bá»™ dÃ²ng hÃ ng trong
    /// chi_tiet_phieu_nhap_hang (ná»‘i san_pham/don_vi_tinh Ä‘á»ƒ láº¥y Ä‘Æ¡n vá»‹ tÃ­nh).
    /// Tráº£ vá» null náº¿u khÃ´ng tÃ¬m tháº¥y phiáº¿u. Chá»‰ Ä‘á»c, khÃ´ng sá»­a.
    /// </summary>
    public async Task<PhieuNhapHangDetail?> GetPhieuNhapHangDetailAsync(uint phieuNhapId)
    {
        if (phieuNhapId == 0)
            return null;

        await using var connection = await OpenAsync();

        // Hai cá»™t so_hoa_don_dau_vao / ma_dat_hang_nhap lÃ  tuá»³ chá»n (database cÅ©
        // chÆ°a cÃ³); chá»‰ thÃªm vÃ o cÃ¢u SQL khi thá»±c sá»± tá»“n táº¡i.
        var hasExtraColumns = await HasPhieuNhapExtraColumnsAsync();
        var hasPhuongThuc = await EnsurePhieuNhapPhuongThucColumnAsync();

        var extraSelect = (hasExtraColumns
            ? ",\n            p.so_hoa_don_dau_vao AS SoHoaDonDauVao,\n            p.ma_dat_hang_nhap   AS MaDatHangNhap"
            : string.Empty)
            + (hasPhuongThuc
            ? ",\n            COALESCE(p.phuong_thuc_thanh_toan, 'TIEN_MAT') AS PhuongThucThanhToan"
            : string.Empty);

        var headerSql = $"""
        SELECT
            p.id               AS PhieuNhapId,
            p.ma_phieu_nhap    AS MaPhieuNhap,
            p.nha_cung_cap_id  AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            nd.ho_ten          AS TenNhanVien,
            p.thoi_gian_nhap   AS ThoiGianNhap,
            p.giam_gia         AS GiamGia,
            p.chi_phi_khac     AS ChiPhiKhac,
            p.da_thanh_toan    AS DaThanhToan,
            p.trang_thai       AS TrangThai,
            p.ghi_chu          AS GhiChu{extraSelect}
        FROM phieu_nhap_hang p
        LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        LEFT JOIN nguoi_dung   nd  ON nd.id  = p.nguoi_nhap_id
        WHERE p.id = @PhieuNhapId
        LIMIT 1;
        """;

        var header = await connection.QueryFirstOrDefaultAsync<PhieuNhapHangDetail>(
            headerSql,
            new { PhieuNhapId = phieuNhapId });

        if (header is null)
            return null;

        const string detailSql = """
        SELECT
            ct.ma_san_pham  AS MaSanPham,
            ct.ten_san_pham AS TenSanPham,
            dvt.ten_don_vi  AS DonViTinh,
            ct.so_luong     AS SoLuong,
            ct.don_gia_nhap AS DonGiaNhap,
            ct.giam_gia     AS GiamGia,
            ct.thanh_tien   AS ThanhTien,
            ct.ghi_chu      AS GhiChu
        FROM chi_tiet_phieu_nhap_hang ct
        LEFT JOIN san_pham    sp  ON sp.id  = ct.san_pham_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        WHERE ct.phieu_nhap_id = @PhieuNhapId
        ORDER BY ct.id;
        """;

        var items = (
            await connection.QueryAsync<PhieuNhapHangDetailItem>(
                detailSql,
                new { PhieuNhapId = phieuNhapId })
        ).AsList();

        for (var i = 0; i < items.Count; i++)
            items[i].Index = i + 1;

        header.Items = items;

        return header;
    }

    /// <summary>
    /// HoÃ n thÃ nh phiáº¿u nháº­p: lÆ°u header, toÃ n bá»™ dÃ²ng chi tiáº¿t, cá»™ng tá»“n kho,
    /// ghi lá»‹ch sá»­ nháº­p xuáº¥t kho vÃ  cáº­p nháº­t láº¡i giÃ¡ vá»‘n bÃ¬nh quÃ¢n â€” táº¥t cáº£ trong
    /// má»™t transaction, hoáº·c rollback trá»n váº¹n náº¿u cÃ³ bÆ°á»›c nÃ o lá»—i.
    /// </summary>
    public async Task<uint> LuuPhieuNhapHangAsync(
        PhieuNhapHang phieu,
        IReadOnlyList<ChiTietPhieuNhapHang> items)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.PurchasesCreate, "Báº¡n khÃ´ng cÃ³ quyá»n lÆ°u phiáº¿u nháº­p hÃ ng.");
        if (phieu is null)
            throw new ArgumentNullException(nameof(phieu));

        if (items is null)
            throw new ArgumentNullException(nameof(items));

        var dongHopLe = items
            .Where(item => item.SanPham.Id != 0 && item.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
            throw new InvalidOperationException("Phiáº¿u nháº­p chÆ°a cÃ³ hÃ ng hÃ³a nÃ o há»£p lá»‡.");

        // Cháº·n á»Ÿ táº§ng service: mÃ£ phiáº¿u rá»—ng hoáº·c sai Ä‘á»‹nh dáº¡ng tuyá»‡t Ä‘á»‘i khÃ´ng Ä‘Æ°á»£c
        // ghi xuá»‘ng phieu_nhap_hang (rÃ ng buá»™c UNIQUE uk_pnh_ma chá»‰ báº¯t Ä‘Æ°á»£c mÃ£ rá»—ng).
        if (string.IsNullOrWhiteSpace(phieu.MaPhieuNhap))
            throw new InvalidOperationException("MÃ£ phiáº¿u nháº­p rá»—ng, khÃ´ng thá»ƒ lÆ°u phiáº¿u nháº­p.");

        phieu.MaPhieuNhap = phieu.MaPhieuNhap.Trim();

        var branchId = phieu.ChiNhanhId != 0
            ? phieu.ChiNhanhId
            : Session.CurrentUser?.ChiNhanhId ?? 1;

        phieu.ChiNhanhId = branchId;
        phieu.NguoiNhapId ??= Session.CurrentUser?.Id;

        phieu.TongTienHang = dongHopLe.Sum(item => item.ThanhTien);
        phieu.ChiPhiKhac = phieu.ChiPhiKhac < 0 ? 0 : phieu.ChiPhiKhac;
        phieu.TongTienNhap = phieu.TongTienHang + phieu.ChiPhiKhac;
        phieu.DaThanhToan = Math.Clamp(phieu.DaThanhToan, 0, phieu.TongTienNhap);
        phieu.ConNo = phieu.TongTienNhap - phieu.DaThanhToan;
        phieu.TrangThai = "DA_NHAP_HANG";

        var hasExtraColumns = await HasPhieuNhapExtraColumnsAsync();
        var hasPhuongThucColumn = await EnsurePhieuNhapPhuongThucColumnAsync();
        var hasPhieuQuyExtraColumns = await EnsurePhieuQuyExtraColumnsAsync();
        var hasPhieuQuyPhieuNhapColumns = await EnsurePhieuQuyPhieuNhapColumnsAsync();

        var extraColumns = (hasPhuongThucColumn ? ",\n                    phuong_thuc_thanh_toan" : string.Empty)
            + (hasExtraColumns ? ",\n                    so_hoa_don_dau_vao,\n                    ma_dat_hang_nhap" : string.Empty);

        var extraValues = (hasPhuongThucColumn ? ",\n                    @PhuongThucThanhToan" : string.Empty)
            + (hasExtraColumns ? ",\n                    @SoHoaDonDauVao,\n                    @MaDatHangNhap" : string.Empty);

        var extraAssignments = (hasPhuongThucColumn ? ",\n                    phuong_thuc_thanh_toan = @PhuongThucThanhToan" : string.Empty)
            + (hasExtraColumns ? ",\n                    so_hoa_don_dau_vao = @SoHoaDonDauVao,\n                    ma_dat_hang_nhap   = @MaDatHangNhap" : string.Empty);

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // ============================================================
            // 0. CHá»N GHI TRÃ™NG KHI Báº¤M "HOÃ€N THÃ€NH" NHIá»€U Láº¦N
            //    Äá»c tráº¡ng thÃ¡i hiá»‡n táº¡i trong transaction Ä‘á»ƒ tá»“n kho
            //    khÃ´ng bá»‹ cá»™ng hai láº§n cho cÃ¹ng má»™t phiáº¿u.
            // ============================================================

            var trangThaiHienTai = phieu.Id == 0
                ? null
                : await connection.ExecuteScalarAsync<string?>(
                    """
                    SELECT trang_thai
                    FROM phieu_nhap_hang
                    WHERE id = @Id
                    FOR UPDATE;
                    """,
                    new { phieu.Id },
                    transaction);

            // Phiáº¿u Ä‘Ã£ hoÃ n thÃ nh thÃ¬ tá»“n kho Ä‘Ã£ Ä‘Æ°á»£c cá»™ng má»™t láº§n,
            // khÃ´ng Ä‘Æ°á»£c cá»™ng thÃªm láº§n ná»¯a.
            if (trangThaiHienTai == "DA_NHAP_HANG")
                throw new InvalidOperationException(
                    "Phiáº¿u nháº­p nÃ y Ä‘Ã£ hoÃ n thÃ nh trÆ°á»›c Ä‘Ã³. Tá»“n kho Ä‘Ã£ Ä‘Æ°á»£c cáº­p nháº­t nÃªn khÃ´ng thá»ƒ hoÃ n thÃ nh láº¡i.");

            // ============================================================
            // 1. HEADER
            // ============================================================

            if (phieu.Id == 0)
            {
                var insertSql = $"""
                INSERT INTO phieu_nhap_hang
                (
                    ma_phieu_nhap,
                    chi_nhanh_id,
                    nha_cung_cap_id,
                    nguoi_nhap_id,
                    thoi_gian_nhap,
                    tong_tien_hang,
                    giam_gia,
                    chi_phi_khac,
                    tong_tien_nhap,
                    da_thanh_toan,
                    con_no,
                    trang_thai,
                    ghi_chu{extraColumns},
                    ngay_tao,
                    ngay_cap_nhat
                )
                VALUES
                (
                    @MaPhieuNhap,
                    @ChiNhanhId,
                    @NhaCungCapId,
                    @NguoiNhapId,
                    @ThoiGianNhap,
                    @TongTienHang,
                    0,
                    @ChiPhiKhac,
                    @TongTienNhap,
                    @DaThanhToan,
                    @ConNo,
                    @TrangThai,
                    @GhiChu{extraValues},
                    NOW(),
                    NOW()
                );
                SELECT LAST_INSERT_ID();
                """;

                phieu.Id = await connection.ExecuteScalarAsync<uint>(
                    insertSql,
                    BuildPhieuNhapParameters(phieu),
                    transaction);
            }
            else
            {
                var updateSql = $"""
                UPDATE phieu_nhap_hang
                SET ma_phieu_nhap      = @MaPhieuNhap,
                    nha_cung_cap_id    = @NhaCungCapId,
                    nguoi_nhap_id      = @NguoiNhapId,
                    thoi_gian_nhap     = @ThoiGianNhap,
                    tong_tien_hang     = @TongTienHang,
                    giam_gia           = 0,
                    chi_phi_khac       = @ChiPhiKhac,
                    tong_tien_nhap     = @TongTienNhap,
                    da_thanh_toan      = @DaThanhToan,
                    con_no             = @ConNo,
                    trang_thai         = @TrangThai,
                    ghi_chu            = @GhiChu{extraAssignments},
                    ngay_cap_nhat      = NOW()
                WHERE id = @Id;
                """;

                await connection.ExecuteAsync(
                    updateSql,
                    BuildPhieuNhapParameters(phieu),
                    transaction);
            }

            // ============================================================
            // 2. CHI TIáº¾T PHIáº¾U NHáº¬P
            // ============================================================

            await connection.ExecuteAsync(
                "DELETE FROM chi_tiet_phieu_nhap_hang WHERE phieu_nhap_id = @PhieuNhapId;",
                new { PhieuNhapId = phieu.Id },
                transaction);

            const string insertDetailSql = """
            INSERT INTO chi_tiet_phieu_nhap_hang
            (
                phieu_nhap_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                so_luong,
                don_gia_nhap,
                giam_gia,
                thanh_tien,
                ghi_chu
            )
            VALUES
            (
                @PhieuNhapId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                @SoLuong,
                @DonGiaNhap,
                @GiamGia,
                @ThanhTien,
                NULL
            );
            """;

            foreach (var item in dongHopLe)
            {
                await connection.ExecuteAsync(
                    insertDetailSql,
                    new
                    {
                        PhieuNhapId = phieu.Id,
                        SanPhamId = item.SanPham.Id,
                        MaSanPham = item.SanPham.MaSanPham,
                        TenSanPham = item.SanPham.TenSanPham,
                        item.SoLuong,
                        item.DonGiaNhap,
                        item.GiamGia,
                        item.ThanhTien
                    },
                    transaction);
            }

            // ============================================================
            // 3. Cáº¬P NHáº¬T Tá»’N KHO + Lá»ŠCH Sá»¬ NHáº¬P XUáº¤T KHO
            // ============================================================

            await CapNhatTonKhoPhieuNhapAsync(
                connection,
                transaction,
                phieu,
                dongHopLe);

            // ============================================================
            // 4. CÃ”NG Ná»¢ NHÃ€ CUNG Cáº¤P
            //    Ghi vÃ o lich_su_cong_no_nha_cung_cap ngay trong giao dá»‹ch nÃ y
            //    Ä‘á»ƒ sá»‘ dÆ° cÃ´ng ná»£ khÃ¡ch má»›i luÃ´n khá»›p vá»›i phiáº¿u vá»«a lÆ°u.
            // ============================================================

            await GhiCongNoNhaCungCapPhieuNhapAsync(
                connection,
                transaction,
                phieu,
                branchId);

            // ============================================================
            // 5. Sá»” QUá»¸ (PHIáº¾U CHI)
            //    Náº¿u phiáº¿u nháº­p cÃ³ thanh toÃ¡n tiá»n (DaThanhToan > 0), ghi
            //    phiáº¿u chi tÆ°Æ¡ng á»©ng vÃ o sá»• quá»¹ Ä‘á»ƒ sá»‘ dÆ° quá»¹ tá»± Ä‘á»™ng giáº£m Ä‘Ãºng nguá»“n tiá»n.
            // ============================================================

            await GhiPhieuChiNhapHangAsync(
                connection,
                transaction,
                branchId,
                phieu.NguoiNhapId,
                phieu,
                hasPhieuQuyExtraColumns,
                hasPhieuQuyPhieuNhapColumns);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Nháº­p hÃ ng",
                "PHIEU_NHAP",
                phieu.Id,
                phieu.MaPhieuNhap,
                $"Nháº­p hÃ ng hÃ³a {phieu.MaPhieuNhap} (Tá»•ng tiá»n nháº­p: {phieu.TongTienNhap:N0} Ä‘)",
                phieu.TongTienNhap);

            return phieu.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Ghi sá»• cÃ¡i cÃ´ng ná»£ nhÃ  cung cáº¥p cho má»™t phiáº¿u nháº­p, trong CÃ™NG transaction
    /// vá»›i láº§n lÆ°u phiáº¿u: má»™t dÃ²ng ghi khoáº£n pháº£i tráº£ khi nháº­p hÃ ng vÃ  má»™t dÃ²ng
    /// ghi khoáº£n Ä‘Ã£ tráº£ náº¿u phiáº¿u cÃ³ ghi "Ä‘Ã£ thanh toÃ¡n".
    /// Tá»•ng ghi ná»£ - ghi cÃ³ cá»§a má»—i phiáº¿u luÃ´n báº±ng <c>phieu.ConNo</c> nÃªn sá»‘ dÆ°
    /// cÃ´ng ná»£ khá»›p vá»›i cá»™t con_no trÃªn Ä‘áº§u phiáº¿u.
    /// </summary>
    private static async Task GhiCongNoNhaCungCapPhieuNhapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PhieuNhapHang phieu,
        uint branchId)
    {
        if (phieu.NhaCungCapId is not uint nccId || nccId == 0)
            return;

        // Chá»‘ng ghi trÃ¹ng khi má»™t phiáº¿u Ä‘Æ°á»£c sá»­a rá»“i hoÃ n thÃ nh láº¡i:
        // xoÃ¡ cÃ¡c dÃ²ng cÅ© cá»§a chÃ­nh mÃ£ phiáº¿u nÃ y trÆ°á»›c khi ghi láº¡i.
        await connection.ExecuteAsync(
            "DELETE FROM lich_su_cong_no_nha_cung_cap WHERE ma_chung_tu = @MaChungTu;",
            new { phieu.MaPhieuNhap },
            transaction);

        // Khoáº£n mua lÃ m tÄƒng ná»£ pháº£i tráº£, nÃªn ghi ná»£.
        if (phieu.TongTienNhap > 0)
        {
            await GhiMotDongCongNoNhaCungCapAsync(
                connection,
                transaction,
                nccId,
                branchId,
                "NHAP_HANG",
                phieu.MaPhieuNhap,
                phieu.TongTienNhap,
                0,
                phieu.ThoiGianNhap,
                phieu.NguoiNhapId,
                $"Nháº­p hÃ ng táº¡o khoáº£n pháº£i tráº£ {phieu.TongTienNhap:N0} cho phiáº¿u {phieu.MaPhieuNhap}");
        }

        // Khoáº£n Ä‘Ã£ tráº£ lÃ m giáº£m ná»£ pháº£i tráº£, nÃªn ghi cÃ³.
        if (phieu.DaThanhToan > 0)
        {
            await GhiMotDongCongNoNhaCungCapAsync(
                connection,
                transaction,
                nccId,
                branchId,
                "THANH_TOAN_NHAP",
                phieu.MaPhieuNhap,
                0,
                phieu.DaThanhToan,
                phieu.ThoiGianNhap,
                phieu.NguoiNhapId,
                $"ÄÃ£ thanh toÃ¡n {phieu.DaThanhToan:N0} cho phiáº¿u {phieu.MaPhieuNhap}");
        }
    }

    /// <summary>
    /// Ghi phiáº¿u Chi trong sá»• quá»¹ cho má»™t phiáº¿u nháº­p hÃ ng cÃ³ thanh toÃ¡n tiá»n (DaThanhToan > 0),
    /// dÃ¹ng CHUNG connection vÃ  transaction vá»›i láº§n ghi phiáº¿u nháº­p.
    /// GiÃºp sá»• quá»¹ vÃ  cÃ¡c quá»¹ tiá»n máº·t/ngÃ¢n hÃ ng pháº£n Ã¡nh chÃ­nh xÃ¡c nguá»“n tiá»n chi tráº£ khi nháº­p hÃ ng.
    /// </summary>
    private static async Task GhiPhieuChiNhapHangAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint branchId,
        uint? userId,
        PhieuNhapHang phieu,
        bool coCotNguoiNopNhanGhiChu,
        bool coCotPhieuNhap)
    {
        // 1. Dá»n dáº¹p phiáº¿u chi cÅ© náº¿u phiáº¿u nháº­p Ä‘Æ°á»£c cáº­p nháº­t láº¡i / hoÃ n thÃ nh láº¡i
        if (coCotPhieuNhap)
        {
            await connection.ExecuteAsync(
                """
                DELETE FROM phieu_chi
                WHERE (phieu_nhap_hang_id = @PhieuNhapId
                   OR (ma_phieu_nhap = @MaPhieuNhap AND ma_phieu_chi LIKE 'PC%'));
                """,
                new { PhieuNhapId = phieu.Id, phieu.MaPhieuNhap },
                transaction);
        }
        else if (coCotNguoiNopNhanGhiChu)
        {
            await connection.ExecuteAsync(
                """
                DELETE FROM phieu_chi
                WHERE ma_phieu_chi LIKE 'PC%'
                  AND ghi_chu LIKE CONCAT('%', @MaPhieuNhap, '%');
                """,
                new { phieu.MaPhieuNhap },
                transaction);
        }

        // 2. Náº¿u phiáº¿u cÃ³ thanh toÃ¡n tiá»n (> 0), táº¡o phiáº¿u chi má»›i
        if (phieu.DaThanhToan > 0)
        {
            var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);

            // Phiáº¿u chi thanh toÃ¡n nháº­p hÃ ng luÃ´n thuá»™c loáº¡i "Chi nhÃ  cung cáº¥p".
            var idNhomChiNhaCungCap = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");

            var phuongThuc = ChuanHoaPhuongThuc(phieu.PhuongThucThanhToan);
            var noiDung = $"Thanh toÃ¡n tiá»n nháº­p hÃ ng - phiáº¿u {phieu.MaPhieuNhap}";
            var tenNcc = string.IsNullOrWhiteSpace(phieu.TenNhaCungCap) ? phieu.NhaCungCapId?.ToString() : phieu.TenNhaCungCap;
            var ghiChu = $"Chi tráº£ tiá»n nháº­p hÃ ng cho nhÃ  cung cáº¥p {tenNcc} theo phiáº¿u nháº­p {phieu.MaPhieuNhap}.";

            string sql;
            if (coCotPhieuNhap && coCotNguoiNopNhanGhiChu)
            {
                sql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                    nguoi_thuc_hien_id, trang_thai, phieu_nhap_hang_id, ma_phieu_nhap
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                    @NguoiTaoId, @TrangThai, @PhieuNhapId, @MaPhieuNhap
                );
                """;
            }
            else if (coCotNguoiNopNhanGhiChu)
            {
                sql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                    nguoi_thuc_hien_id, trang_thai
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                    @NguoiTaoId, @TrangThai
                );
                """;
            }
            else
            {
                sql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @ThoiGian, @NguoiTaoId, @TrangThai
                );
                """;
            }

            await connection.ExecuteAsync(
                sql,
                new
                {
                    MaPhieu = maPhieuChi,
                    ChiNhanhId = branchId,
                    NhomChiPhiId = idNhomChiNhaCungCap,
                    SoTien = phieu.DaThanhToan,
                    PhuongThuc = phuongThuc,
                    NoiDung = noiDung,
                    NguoiNopNhan = Truncate150(phieu.TenNhaCungCap),
                    GhiChu = ghiChu,
                    ThoiGian = phieu.ThoiGianNhap,
                    NguoiTaoId = userId,
                    TrangThai = TrangThaiPhieuHoanThanh,
                    PhieuNhapId = phieu.Id,
                    phieu.MaPhieuNhap
                },
                transaction);
        }
    }

    /// <summary>
    /// Ghi má»™t dÃ²ng vÃ o sá»• cÃ´ng ná»£ nhÃ  cung cáº¥p, tá»± tÃ­nh láº¡i sá»‘ dÆ° cá»™ng dá»“n
    /// (Ä‘á»c khoÃ¡ dÃ²ng hiá»‡n táº¡i Ä‘á»ƒ hai phiáº¿u song song khÃ´ng ghi Ä‘Ã¨ sá»‘ dÆ° cá»§a nhau).
    /// </summary>
    private static async Task GhiMotDongCongNoNhaCungCapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint nccId,
        uint branchId,
        string loaiGiaoDich,
        string maChungTu,
        decimal ghiNo,
        decimal ghiCo,
        DateTime thoiGian,
        uint? nguoiThucHienId,
        string ghiChu)
    {
        var baselineDebt = await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(no_can_tra_hien_tai, 0)
            FROM nha_cung_cap
            WHERE id = @NhaCungCapId;
            """,
            new { NhaCungCapId = nccId },
            transaction);

        var soDuHienTai = await connection.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
            FROM lich_su_cong_no_nha_cung_cap
            WHERE nha_cung_cap_id = @NhaCungCapId
            FOR UPDATE;
            """,
            new { NhaCungCapId = nccId },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO lich_su_cong_no_nha_cung_cap
            (
                nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                ma_chung_tu, ghi_no, ghi_co, so_du,
                thoi_gian, nguoi_thuc_hien_id, ghi_chu
            )
            VALUES
            (
                @NhaCungCapId, @ChiNhanhId, @LoaiGiaoDich, 'PHIEU_NHAP',
                @MaChungTu, @GhiNo, @GhiCo, @SoDu,
                @ThoiGian, @NguoiThucHienId, @GhiChu
            );
            """,
            new
            {
                NhaCungCapId = nccId,
                ChiNhanhId = branchId,
                LoaiGiaoDich = loaiGiaoDich,
                MaChungTu = maChungTu,
                GhiNo = ghiNo,
                GhiCo = ghiCo,
                SoDu = baselineDebt + soDuHienTai + ghiNo - ghiCo,
                ThoiGian = thoiGian,
                NguoiThucHienId = nguoiThucHienId,
                GhiChu = ghiChu
            },
            transaction);
    }

    private async Task CapNhatTonKhoPhieuNhapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PhieuNhapHang phieu,
        IReadOnlyList<ChiTietPhieuNhapHang> items)
    {
        const string selectTonSql = """
        SELECT so_luong_ton, gia_von_binh_quan
        FROM ton_kho
        WHERE chi_nhanh_id = @ChiNhanhId
          AND san_pham_id = @SanPhamId
        FOR UPDATE;
        """;

        const string upsertTonSql = """
        INSERT INTO ton_kho
        (
            chi_nhanh_id,
            san_pham_id,
            so_luong_ton,
            gia_von_binh_quan,
            ngay_cap_nhat
        )
        VALUES
        (
            @ChiNhanhId,
            @SanPhamId,
            @SoLuongTon,
            @GiaVonBinhQuan,
            NOW()
        )
        ON DUPLICATE KEY UPDATE
            so_luong_ton      = @SoLuongTon,
            gia_von_binh_quan = @GiaVonBinhQuan,
            ngay_cap_nhat     = NOW();
        """;

        const string updateGiaVonSql = """
        UPDATE san_pham
        SET gia_von        = @GiaVon,
            don_vi_tinh_id = COALESCE(@DonViTinhIdMoi, don_vi_tinh_id),
            ngay_cap_nhat  = NOW()
        WHERE id = @SanPhamId;
        """;

        const string insertLichSuSql = """
        INSERT INTO lich_su_nhap_xuat_kho
        (
            chi_nhanh_id,
            san_pham_id,
            loai_giao_dich,
            loai_chung_tu,
            ma_chung_tu,
            so_luong,
            ton_truoc,
            ton_sau,
            gia_von,
            nguoi_thuc_hien_id,
            thoi_gian,
            ghi_chu
        )
        VALUES
        (
            @ChiNhanhId,
            @SanPhamId,
            'NHAP',
            'PHIEU_NHAP_HANG',
            @MaChungTu,
            @SoLuong,
            @TonTruoc,
            @TonSau,
            @GiaVon,
            @NguoiThucHienId,
            NOW(),
            NULL
        );
        """;

        foreach (var item in items)
        {
            var tonHienTai = await connection.QueryAsync(
                selectTonSql,
                new
                {
                    phieu.ChiNhanhId,
                    SanPhamId = item.SanPham.Id
                },
                transaction);

            decimal tonTruoc = 0;
            decimal giaVonCu = 0;

            if (tonHienTai.FirstOrDefault() is IDictionary<string, object> row)
            {
                tonTruoc = ToDecimal(row.TryGetValue("so_luong_ton", out var soLuong) ? soLuong : null);
                giaVonCu = ToDecimal(row.TryGetValue("gia_von_binh_quan", out var giaVon) ? giaVon : null);
            }

            var tonSau = tonTruoc + item.SoLuong;

            // GiÃ¡ vá»‘n bÃ¬nh quÃ¢n = (tá»“n cÅ© * giÃ¡ cÅ© + lÆ°á»£ng nháº­p * giÃ¡ nháº­p) / tá»“n sau
            var giaVonBinhQuan = tonSau > 0
                ? (tonTruoc * giaVonCu + item.SoLuong * item.DonGiaNhap) / tonSau
                : item.DonGiaNhap;

            await connection.ExecuteAsync(
                upsertTonSql,
                new
                {
                    phieu.ChiNhanhId,
                    SanPhamId = item.SanPham.Id,
                    SoLuongTon = tonSau,
                    GiaVonBinhQuan = Math.Round(giaVonBinhQuan, 2)
                },
                transaction);

            await connection.ExecuteAsync(
                """
                UPDATE san_pham
                SET ton_kho_excel = GREATEST(0, COALESCE(ton_kho_excel, 0) + @Delta),
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
                """,
                new
                {
                    Delta = item.SoLuong,
                    SanPhamId = item.SanPham.Id
                },
                transaction);

            await connection.ExecuteAsync(
                updateGiaVonSql,
                new
                {
                    GiaVon = item.DonGiaNhap,
                    DonViTinhIdMoi = item.DonViTinhIdMoi,
                    SanPhamId = item.SanPham.Id
                },
                transaction);

            if (item.SelectedDonViTinh is { Id: > 0 } donViMoi)
            {
                item.SanPham.DonViTinhId = donViMoi.Id;
                item.SanPham.DonViTinh = donViMoi.Name;
                item.SanPham.TenDonViTinh = donViMoi.Name;
            }

            await connection.ExecuteAsync(
                insertLichSuSql,
                new
                {
                    phieu.ChiNhanhId,
                    SanPhamId = item.SanPham.Id,
                    MaChungTu = phieu.MaPhieuNhap,
                    item.SoLuong,
                    TonTruoc = tonTruoc,
                    TonSau = tonSau,
                    GiaVon = item.DonGiaNhap,
                    phieu.NguoiNhapId
                },
                transaction);
        }
    }

    private object BuildPhieuNhapParameters(PhieuNhapHang phieu) => new
    {
        phieu.Id,
        phieu.MaPhieuNhap,
        phieu.ChiNhanhId,
        phieu.NhaCungCapId,
        phieu.NguoiNhapId,
        phieu.ThoiGianNhap,
        phieu.TongTienHang,
        phieu.ChiPhiKhac,
        phieu.TongTienNhap,
        phieu.DaThanhToan,
        PhuongThucThanhToan = ChuanHoaPhuongThuc(phieu.PhuongThucThanhToan),
        phieu.ConNo,
        phieu.TrangThai,
        GhiChu = string.IsNullOrWhiteSpace(phieu.GhiChu) ? null : phieu.GhiChu.Trim(),
        SoHoaDonDauVao = string.IsNullOrWhiteSpace(phieu.SoHoaDonDauVao) ? null : phieu.SoHoaDonDauVao.Trim(),
        MaDatHangNhap = string.IsNullOrWhiteSpace(phieu.MaDatHangNhap) ? null : phieu.MaDatHangNhap.Trim()
    };

    /// <summary>
    /// Hai cá»™t so_hoa_don_dau_vao / ma_dat_hang_nhap lÃ  tuá»³ chá»n.
    /// Kiá»ƒm tra information_schema má»™t láº§n Ä‘á»ƒ database cÅ© váº«n cháº¡y Ä‘Æ°á»£c.
    /// </summary>
    private async Task<bool> HasPhieuNhapExtraColumnsAsync()
    {
        if (_hasPhieuNhapExtraColumns.HasValue)
            return _hasPhieuNhapExtraColumns.Value;

        try
        {
            await using var connection = await OpenAsync();

            const string sql = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'phieu_nhap_hang'
              AND COLUMN_NAME IN ('so_hoa_don_dau_vao', 'ma_dat_hang_nhap');
            """;

            var count = await connection.ExecuteScalarAsync<long>(sql);
            _hasPhieuNhapExtraColumns = count >= 2;
        }
        catch
        {
            _hasPhieuNhapExtraColumns = false;
        }

        return _hasPhieuNhapExtraColumns.Value;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng phieu_nhap_hang cÃ³ cá»™t phuong_thuc_thanh_toan Ä‘á»ƒ lÆ°u nguá»“n tiá»n chi tráº£
    /// (TIEN_MAT, CHUYEN_KHOAN...). Náº¿u database cÅ© chÆ°a cÃ³ thÃ¬ tá»± Ä‘á»™ng ALTER TABLE.
    /// </summary>
    private async Task<bool> EnsurePhieuNhapPhuongThucColumnAsync()
    {
        if (_hasPhieuNhapPhuongThucColumn.HasValue)
            return _hasPhieuNhapPhuongThucColumn.Value;

        await using var connection = await OpenAsync();

        try
        {
            const string sql = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'phieu_nhap_hang'
              AND COLUMN_NAME = 'phuong_thuc_thanh_toan';
            """;

            var count = await connection.ExecuteScalarAsync<long>(sql);
            if (count > 0)
            {
                _hasPhieuNhapPhuongThucColumn = true;
                return true;
            }

            await connection.ExecuteAsync(
                "ALTER TABLE phieu_nhap_hang ADD COLUMN phuong_thuc_thanh_toan varchar(30) DEFAULT 'TIEN_MAT' AFTER da_thanh_toan;");

            _hasPhieuNhapPhuongThucColumn = true;
        }
        catch
        {
            _hasPhieuNhapPhuongThucColumn = false;
        }

        return _hasPhieuNhapPhuongThucColumn.Value;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng phieu_chi cÃ³ cá»™t liÃªn káº¿t phiáº¿u nháº­p hÃ ng:
    /// phieu_nhap_hang_id vÃ  ma_phieu_nhap.
    /// </summary>
    private async Task<bool> EnsurePhieuQuyPhieuNhapColumnsAsync()
    {
        if (_hasPhieuQuyPhieuNhapColumns.HasValue)
            return _hasPhieuQuyPhieuNhapColumns.Value;

        await using var connection = await OpenAsync();

        try
        {
            var coCot = (await connection.QueryAsync<string>(
                """
                SELECT COLUMN_NAME FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi'
                  AND COLUMN_NAME IN ('phieu_nhap_hang_id', 'ma_phieu_nhap');
                """)).AsList();

            var daCoPhieuNhapId = false;
            var daCoMaPhieuNhap = false;

            foreach (var cot in coCot)
            {
                if (string.Equals(cot, "phieu_nhap_hang_id", StringComparison.OrdinalIgnoreCase))
                    daCoPhieuNhapId = true;
                else if (string.Equals(cot, "ma_phieu_nhap", StringComparison.OrdinalIgnoreCase))
                    daCoMaPhieuNhap = true;
            }

            if (!daCoPhieuNhapId)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE phieu_chi ADD COLUMN phieu_nhap_hang_id int(10) unsigned DEFAULT NULL;");
            }

            if (!daCoMaPhieuNhap)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE phieu_chi ADD COLUMN ma_phieu_nhap varchar(50) DEFAULT NULL;");
            }

            _hasPhieuQuyPhieuNhapColumns = true;
        }
        catch
        {
            _hasPhieuQuyPhieuNhapColumns = false;
        }

        return _hasPhieuQuyPhieuNhapColumns.Value;
    }

    /// <summary>
    /// Báº£ng <c>phieu_chi</c> dÃ¹ng chung cho cáº£ phiáº¿u thu vÃ  phiáº¿u chi. NgoÃ i cá»™t
    /// <c>noi_dung</c>, sá»• quá»¹ cáº§n tÃ¡ch riÃªng <c>nguoi_nop_nhan</c> (ngÆ°á»i ná»™p vá»›i
    /// phiáº¿u thu, ngÆ°á»i nháº­n vá»›i phiáº¿u chi) vÃ  <c>ghi_chu</c>; trÆ°á»›c Ä‘Ã¢y hai thÃ´ng
    /// tin nÃ y bá»‹ dá»“n chung vÃ o <c>noi_dung</c> nÃªn khÃ´ng hiá»ƒn thá»‹ tÃ¡ch biá»‡t Ä‘Æ°á»£c.
    /// <para>
    /// Hai cá»™t lÃ  báº¯t buá»™c nÃªn hÃ m nÃ y tá»± thÃªm vÃ o database cÅ© thay vÃ¬ chá»‰ bÃ¡o lá»—i.
    /// Viá»‡c kiá»ƒm tra chá»‰ lÃ m má»™t láº§n rá»“i cache. Náº¿u tÃ i khoáº£n MySQL khÃ´ng cÃ³ quyá»n
    /// ALTER TABLE thÃ¬ hÃ m tráº£ vá» false vÃ  cÃ¡c cÃ¢u SQL sá»• quá»¹ tá»± bá» qua hai cá»™t nÃ y
    /// Ä‘á»ƒ á»©ng dá»¥ng váº«n cháº¡y Ä‘Æ°á»£c vá»›i dá»¯ liá»‡u cÅ©.
    /// </para>
    /// </summary>
    private async Task<bool> EnsurePhieuQuyExtraColumnsAsync()
    {
        if (_hasPhieuQuyExtraColumns.HasValue)
            return _hasPhieuQuyExtraColumns.Value;

        await using var connection = await OpenAsync();

        const string kiemTraSql = """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'phieu_chi'
          AND COLUMN_NAME IN ('nguoi_nop_nhan', 'ghi_chu');
        """;

        try
        {
            var count = await connection.ExecuteScalarAsync<long>(kiemTraSql);

            if (count >= 2)
            {
                _hasPhieuQuyExtraColumns = true;
                return true;
            }

            // ADD COLUMN khÃ´ng cÃ³ IF NOT EXISTS trÃªn MySQL nÃªn pháº£i kiá»ƒm tra tá»«ng cá»™t.
            if (count == 0)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE phieu_chi ADD COLUMN nguoi_nop_nhan varchar(150) DEFAULT NULL AFTER noi_dung;");
            }

            await connection.ExecuteAsync(
                "ALTER TABLE phieu_chi ADD COLUMN ghi_chu text DEFAULT NULL AFTER nguoi_nop_nhan;");

            _hasPhieuQuyExtraColumns = true;
        }
        catch
        {
            // KhÃ´ng cÃ³ quyá»n ALTER hoáº·c máº¡ng lá»—i: cháº¡y tiáº¿p vá»›i cáº¥u trÃºc cÅ©.
            _hasPhieuQuyExtraColumns = false;
        }

        return _hasPhieuQuyExtraColumns.Value;
    }

    /// <summary>
    /// Báº£o Ä‘áº£m <c>phieu_chi</c> cÃ³ 2 cá»™t liÃªn káº¿t hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng:
    /// <c>hoa_don_ban_hang_id</c> (UNIQUE) vÃ  <c>ma_hoa_don</c>.
    /// Phiáº¿u thu sinh ra khi bÃ¡n hÃ ng sáº½ gáº¯n vÃ o Ä‘Ã¢y nÃªn má»—i hÃ³a Ä‘Æ¡n chá»‰ cÃ³
    /// tá»‘i Ä‘a má»™t phiáº¿u thu â€” UNIQUE index cháº·n trÃ¹ng ngay á»Ÿ táº§ng database, khÃ´ng
    /// phá»¥ thuá»™c code kiá»ƒm tra cá»§a á»©ng dá»¥ng.
    /// </summary>
    private async Task<bool> EnsurePhieuQuyHoaDonColumnsAsync()
    {
        if (_hasPhieuQuyHoaDonColumns.HasValue)
            return _hasPhieuQuyHoaDonColumns.Value;

        await using var connection = await OpenAsync();

        try
        {
            var coCot = (await connection.QueryAsync<string>(
                """
                SELECT COLUMN_NAME FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi'
                  AND COLUMN_NAME IN ('hoa_don_ban_hang_id', 'ma_hoa_don');
                """)).AsList();

            var daCoHoaDonId = false;
            var daCoMaHoaDon = false;

            foreach (var cot in coCot)
            {
                if (string.Equals(cot, "hoa_don_ban_hang_id", StringComparison.OrdinalIgnoreCase))
                    daCoHoaDonId = true;
                else if (string.Equals(cot, "ma_hoa_don", StringComparison.OrdinalIgnoreCase))
                    daCoMaHoaDon = true;
            }

            // MySQL khÃ´ng cÃ³ ADD COLUMN IF NOT EXISTS nÃªn pháº£i kiá»ƒm tra tá»«ng cá»™t.
            if (!daCoHoaDonId)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE phieu_chi ADD COLUMN hoa_don_ban_hang_id int(10) unsigned DEFAULT NULL AFTER trang_thai;");
            }

            if (!daCoMaHoaDon)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE phieu_chi ADD COLUMN ma_hoa_don varchar(50) DEFAULT NULL AFTER hoa_don_ban_hang_id;");
            }

            var coIndex = await connection.ExecuteScalarAsync<long>(
                """
                SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi'
                  AND INDEX_NAME = 'uk_phieu_chi_hoa_don';
                """);

            if (coIndex == 0)
            {
                // Cá»™t cho phÃ©p NULL nÃªn phiáº¿u thu/chi tay váº«n insert bÃ¬nh thÆ°á»ng,
                // chá»‰ má»—i hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng má»›i bá»‹ giá»›i háº¡n 1 phiáº¿u thu.
                await connection.ExecuteAsync(
                    "CREATE UNIQUE INDEX uk_phieu_chi_hoa_don ON phieu_chi (hoa_don_ban_hang_id);");
            }

            _hasPhieuQuyHoaDonColumns = true;
        }
        catch
        {
            // Thiáº¿u quyá»n ALTER: váº«n cháº¡y tiáº¿p, code sáº½ tá»± kiá»ƒm tra trÃ¹ng
            // báº±ng SELECT trÆ°á»›c khi INSERT.
            _hasPhieuQuyHoaDonColumns = false;
        }

        return _hasPhieuQuyHoaDonColumns.Value;
    }

    /// <summary>
    /// Tráº£ vá» id cá»§a má»™t loáº¡i thu/chi theo mÃ£ (<c>nhom_chi_phi.ma_nhom</c>),
    /// <c>null</c> náº¿u danh má»¥c chÆ°a tá»“n táº¡i. DÃ¹ng Ä‘á»ƒ gáº¯n Ä‘Ãºng loáº¡i
    /// "Thu khÃ¡ch hÃ ng" / "Chi nhÃ  cung cáº¥p" cho cÃ¡c phiáº¿u quá»¹ do há»‡ thá»‘ng tá»±
    /// sinh (bÃ¡n hÃ ng, thu ná»£ khÃ¡ch hÃ ng, thanh toÃ¡n nháº­p hÃ ng).
    /// </summary>
    private static async Task<uint?> LayIdNhomChiPhiTheoMaAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string maNhom)
    {
        return await connection.ExecuteScalarAsync<uint?>(
            "SELECT id FROM nhom_chi_phi WHERE ma_nhom = @MaNhom LIMIT 1;",
            new { MaNhom = maNhom },
            transaction);
    }

    /// <summary>
    /// Gáº¯n loáº¡i thu/chi cho cÃ¡c phiáº¿u quá»¹ cÅ© do há»‡ thá»‘ng tá»± sinh nhÆ°ng cÃ²n Ä‘á»ƒ
    /// <c>nhom_chi_phi_id = NULL</c>: phiáº¿u thu bÃ¡n hÃ ng / thu ná»£ khÃ¡ch hÃ ng
    /// (mÃ£ hÃ³a Ä‘Æ¡n, liÃªn káº¿t hÃ³a Ä‘Æ¡n hoáº·c ná»™i dung "Thu ná»£...") vÃ  phiáº¿u chi
    /// thanh toÃ¡n nháº­p hÃ ng (liÃªn káº¿t phiáº¿u nháº­p). Nhá» váº­y bá»™ lá»c
    /// "Thu khÃ¡ch hÃ ng" / "Chi nhÃ  cung cáº¥p" lá»c Ä‘Ãºng cáº£ dá»¯ liá»‡u cÅ©.
    /// <para>
    /// Chá»‰ cháº¡y má»™t láº§n, chá»‰ Ä‘á»¥ng cÃ¡c dÃ²ng Ä‘ang NULL nÃªn cháº¡y láº¡i váº«n an toÃ n
    /// vÃ  khÃ´ng bao giá» ghi Ä‘Ã¨ loáº¡i mÃ  ngÆ°á»i dÃ¹ng Ä‘Ã£ chá»n.
    /// </para>
    /// </summary>
    private async Task EnsureSoQuyNhomChiPhiChoPhieuCuAsync()
    {
        if (_daBackfillNhomSoQuy)
            return;

        _daBackfillNhomSoQuy = true;

        try
        {
            var coCotHoaDon = await EnsurePhieuQuyHoaDonColumnsAsync();
            var coCotPhieuNhap = await EnsurePhieuQuyPhieuNhapColumnsAsync();

            await using var connection = await OpenAsync();

            var idThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, null, "THU_KHACH_HANG");

            if (idThuKhachHang is uint idThu && idThu > 0)
            {
                // Phiáº¿u thu bÃ¡n hÃ ng cÃ³ liÃªn káº¿t hÃ³a Ä‘Æ¡n hoáº·c mÃ£ hÃ³a Ä‘Æ¡n; phiáº¿u
                // thu ná»£ cÃ³ ná»™i dung báº¯t Ä‘áº§u báº±ng "Thu ná»£".
                var nhanDienKhachHang = coCotHoaDon
                    ? "(pc.hoa_don_ban_hang_id IS NOT NULL"
                      + " OR NULLIF(TRIM(pc.ma_hoa_don), '') IS NOT NULL"
                      + " OR COALESCE(pc.noi_dung, '') LIKE 'Thu ná»£%')"
                    : "COALESCE(pc.noi_dung, '') LIKE 'Thu ná»£%'";

                await connection.ExecuteAsync(
                    $"""
                    UPDATE phieu_chi pc
                    SET pc.nhom_chi_phi_id = @IdNhom
                    WHERE pc.nhom_chi_phi_id IS NULL
                      AND pc.ma_phieu_chi LIKE 'PT%'
                      AND {nhanDienKhachHang};
                    """,
                    new { IdNhom = idThu });
            }

            var idChiNhaCungCap = await LayIdNhomChiPhiTheoMaAsync(connection, null, "CHI_NHA_CUNG_CAP");

            if (idChiNhaCungCap is uint idChi && idChi > 0)
            {
                var nhanDienNhaCungCap = coCotPhieuNhap
                    ? "pc.phieu_nhap_hang_id IS NOT NULL"
                    : "COALESCE(pc.noi_dung, '') LIKE 'Thanh toÃ¡n%nháº­p hÃ ng%'";

                await connection.ExecuteAsync(
                    $"""
                    UPDATE phieu_chi pc
                    SET pc.nhom_chi_phi_id = @IdNhom
                    WHERE pc.nhom_chi_phi_id IS NULL
                      AND pc.ma_phieu_chi LIKE 'PC%'
                      AND {nhanDienNhaCungCap};
                    """,
                    new { IdNhom = idChi });
            }
        }
        catch
        {
            // Best-effort: lá»—i migration khÃ´ng Ä‘Æ°á»£c lÃ m há»ng viá»‡c náº¡p sá»• quá»¹.
        }
    }

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng hoa_don_ban_hang cÃ³ 3 cá»™t:
    /// so_dien_thoai, dia_chi, in_kem_thong_tin.
    /// Náº¿u database chÆ°a cÃ³ thÃ¬ tá»± Ä‘á»™ng ALTER TABLE trÆ°á»›c khi má»Ÿ transaction.
    /// </summary>
    private async Task<bool> EnsureHoaDonContactColumnsAsync()
    {
        if (_hasHoaDonContactColumns.HasValue)
            return _hasHoaDonContactColumns.Value;

        await using var connection = await OpenAsync();

        try
        {
            var coCot = (await connection.QueryAsync<string>(
                """
                SELECT COLUMN_NAME FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'hoa_don_ban_hang'
                  AND COLUMN_NAME IN ('so_dien_thoai', 'dia_chi', 'in_kem_thong_tin');
                """)).AsList();

            var daCoSdt = false;
            var daCoDiaChi = false;
            var daCoInKem = false;

            foreach (var cot in coCot)
            {
                if (string.Equals(cot, "so_dien_thoai", StringComparison.OrdinalIgnoreCase))
                    daCoSdt = true;
                else if (string.Equals(cot, "dia_chi", StringComparison.OrdinalIgnoreCase))
                    daCoDiaChi = true;
                else if (string.Equals(cot, "in_kem_thong_tin", StringComparison.OrdinalIgnoreCase))
                    daCoInKem = true;
            }

            if (!daCoSdt)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE hoa_don_ban_hang ADD COLUMN so_dien_thoai varchar(50) DEFAULT NULL AFTER khach_hang_id;");
            }

            if (!daCoDiaChi)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE hoa_don_ban_hang ADD COLUMN dia_chi text DEFAULT NULL AFTER so_dien_thoai;");
            }

            if (!daCoInKem)
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE hoa_don_ban_hang ADD COLUMN in_kem_thong_tin tinyint(1) NOT NULL DEFAULT 0 AFTER dia_chi;");
            }

            _hasHoaDonContactColumns = true;
        }
        catch
        {
            _hasHoaDonContactColumns = false;
        }

        return _hasHoaDonContactColumns.Value;
    }

    // ================================================================
    // Sá»” QUá»¸ - PHIáº¾U THU / PHIáº¾U CHI
    // Database hiá»‡n cÃ³ báº£ng `phieu_chi` (chÆ°a cÃ³ báº£ng `phieu_thu`),
    // nÃªn cáº£ dÃ£y mÃ£ PT vÃ  PC Ä‘á»u láº¥y sá»‘ thá»© tá»± lá»›n nháº¥t tá»« `phieu_chi`.
    // ================================================================

    /// <summary>
    /// Sinh mÃ£ phiáº¿u sá»• quá»¹ tá»± Ä‘á»™ng: "PT0001", "PT0002"... hoáº·c "PC0001", "PC0002"...
    /// Sá»‘ thá»© tá»± láº¥y tá»« MAX pháº§n sá»‘ cá»§a cÃ¡c mÃ£ ÄÃšNG Äá»ŠNH Dáº NG cÃ³ cÃ¹ng tiá»n tá»‘
    /// (khÃ´ng dÃ¹ng COUNT(*) Ä‘á»ƒ trÃ¡nh trÃ¹ng khi dá»¯ liá»‡u bá»‹ xÃ³a).
    /// MÃ£ NULL/rá»—ng/khÃ´ng Ä‘Ãºng Ä‘á»‹nh dáº¡ng bá»‹ loáº¡i báº±ng REGEXP nÃªn dá»¯ liá»‡u rÃ¡c khÃ´ng lÃ m há»ng sá»‘ thá»© tá»±.
    /// Má»i lá»—i káº¿t ná»‘i Ä‘á»u Ä‘Æ°á»£c nuá»‘t vÃ  tráº£ vá» mÃ£ máº·c Ä‘á»‹nh Ä‘á»ƒ á»©ng dá»¥ng khÃ´ng bá»‹ crash.
    /// </summary>
    public async Task<string> TaoMaPhieuQuyMoiAsync(string tienTo)
    {
        var prefix = string.IsNullOrWhiteSpace(tienTo)
            ? "PT"
            : tienTo.Trim().ToUpperInvariant();

        try
        {
            await using var connection = await OpenAsync();

            // REGEXP loáº¡i bá» mÃ£ rá»—ng / sai Ä‘á»‹nh dáº¡ng (VD "PCxxxx").
            // Collation utf8mb4_unicode_ci nÃªn 'pt0001' cÅ©ng khá»›p vá»›i 'PT0001'.
            var maxSuffix = await connection.ExecuteScalarAsync<long?>(
                """
                SELECT COALESCE(MAX(CAST(SUBSTRING(TRIM(ma_phieu_chi), 3) AS SIGNED)), 0)
                FROM phieu_chi
                WHERE ma_phieu_chi LIKE CONCAT(@Prefix, '%')
                  AND TRIM(ma_phieu_chi) REGEXP CONCAT('^', @Prefix, '[0-9]+$');
                """,
                new { Prefix = prefix }) ?? 0L;

            // ChÆ°a cÃ³ phiáº¿u nÃ o (hoáº·c mÃ£ toÃ n sai Ä‘á»‹nh dáº¡ng) => báº¯t Ä‘áº§u tá»« 0001.
            var nextNumber = maxSuffix > 0 ? maxSuffix + 1 : 1L;

            // ma_phieu_chi cÃ³ UNIQUE KEY: náº¿u mÃ£ vá»«a sinh trÃ¹ng (2 mÃ¡y cÃ¹ng má»Ÿ mÃ n hÃ¬nh)
            // thÃ¬ tÄƒng tiáº¿p cho tá»›i khi ra mÃ£ cÃ²n trá»‘ng.
            for (var i = 0; i < 100; i++)
            {
                var candidate = prefix + nextNumber.ToString("D4", CultureInfo.InvariantCulture);

                var daTonTai = await connection.ExecuteScalarAsync<int?>(
                    "SELECT 1 FROM phieu_chi WHERE ma_phieu_chi = @Ma LIMIT 1;",
                    new { Ma = candidate });

                if (daTonTai is null)
                    return candidate;

                nextNumber++;
            }

            return prefix + nextNumber.ToString("D4", CultureInfo.InvariantCulture);
        }
        catch
        {
            // KhÃ´ng cháº·n ngÆ°á»i dÃ¹ng vÃ¬ lá»—i máº¡ng/database.
            return prefix + "0001";
        }
    }

    /// <summary>Tráº¡ng thÃ¡i chá»©ng tá»« Ä‘Ã£ há»§y: khÃ´ng Ä‘Æ°á»£c tÃ­nh vÃ o tá»•ng thu/tá»•ng chi cá»§a quá»¹.</summary>
    public const string TrangThaiPhieuHuy = "HUY";

    /// <summary>
    /// Tráº¡ng thÃ¡i chá»©ng tÆ° Ä‘Ã£ hoÃ n táº¥t: duy nháº¥t Ä‘Æ°á»£c tÃ­nh vÃ o quá»¹.
    /// Xem <see cref="LuuPhieuQuyAsync"/> Ä‘ang ghi tráº¡ng thÃ¡i nÃ y.
    /// </summary>
    public const string TrangThaiPhieuHoanThanh = "HOAN_THANH";

    /// <summary>
    /// FROM + JOIN dÃ¹ng chung cho má»i truy váº¥n sá»• quá»¹.
    /// Phiáº¿u thu vÃ  phiáº¿u chi cÃ¹ng náº±m trong báº£ng <c>phieu_chi</c>, loáº¡i phiáº¿u
    /// Ä‘Æ°á»£c phÃ¢n biá»‡t báº±ng tiá»n tá»‘ mÃ£ (PTxxxx / PCxxxx).
    /// </summary>
    private const string SoQuyFrom = """
    FROM phieu_chi pc
    LEFT JOIN nguoi_dung nd ON nd.id = pc.nguoi_thuc_hien_id
    LEFT JOIN nhom_chi_phi ncp ON ncp.id = pc.nhom_chi_phi_id
    """;

    /// <summary>
    /// Danh sÃ¡ch cá»™t láº¥y tá»« <see cref="SoQuyFrom"/>.
    /// Báº£n Ä‘áº§y Ä‘á»§ dÃ¹ng khi database Ä‘Ã£ cÃ³ <c>nguoi_nop_nhan</c> vÃ  <c>ghi_chu</c>;
    /// báº£n rÃºt gá»n dÃ¹ng khi chÆ°a thÃªm Ä‘Æ°á»£c (xem
    /// <c>EnsurePhieuQuyExtraColumnsAsync</c>) Ä‘á»ƒ á»©ng dá»¥ng váº«n cháº¡y vá»›i dá»¯ liá»‡u cÅ©.
    /// </summary>
    private const string SoQuyCotChonDayDu = """
            pc.ma_phieu_chi AS MaPhieu,
            pc.so_tien AS SoTien,
            COALESCE(pc.noi_dung, '') AS NoiDung,
            COALESCE(pc.nguoi_nop_nhan, '') AS NguoiNopNhan,
            COALESCE(pc.ghi_chu, '') AS GhiChu,
            pc.thoi_gian_chi AS ThoiGian,
            COALESCE(nd.ho_ten, '') AS NguoiTao,
            pc.trang_thai AS TrangThai,
            COALESCE(ncp.ten_nhom, '') AS TenLoai,
            pc.nhom_chi_phi_id AS NhomChiPhiId,
            COALESCE(pc.phuong_thuc, '') AS PhuongThuc
        """;

    private const string SoQuyCotChonRutGon = """
            pc.ma_phieu_chi AS MaPhieu,
            pc.so_tien AS SoTien,
            COALESCE(pc.noi_dung, '') AS NoiDung,
            '' AS NguoiNopNhan,
            '' AS GhiChu,
            pc.thoi_gian_chi AS ThoiGian,
            COALESCE(nd.ho_ten, '') AS NguoiTao,
            pc.trang_thai AS TrangThai,
            COALESCE(ncp.ten_nhom, '') AS TenLoai,
            pc.nhom_chi_phi_id AS NhomChiPhiId,
            COALESCE(pc.phuong_thuc, '') AS PhuongThuc
        """;

    private static string LayCotChonSoQuy(bool coCotMoi) => coCotMoi ? SoQuyCotChonDayDu : SoQuyCotChonRutGon;

    /// <summary>
    /// Dá»±ng Ä‘iá»u kiá»‡n WHERE cho sá»• quá»¹. CÃ¹ng má»™t hÃ m Ä‘Æ°á»£c dÃ¹ng cho cáº£
    /// COUNT (phÃ¢n trang), SELECT (danh sÃ¡ch) vÃ  SUM (thá»‘ng kÃª) nÃªn
    /// danh sÃ¡ch vá»›i thá»‘ng kÃª khÃ´ng thá»ƒ lá»‡ch nhau.
    /// <para>
    /// Má»i Ä‘iá»u kiá»‡n Ä‘á»u gáº¯n tham sá»‘ nÃªn khÃ´ng cÃ³ chuyá»‡n dÃ¡n chuá»—i tá»« ngÆ°á»i dÃ¹ng
    /// vÃ o SQL. Khoáº£ng thá»i gian vÃ  khoáº£ng tiá»n cÃ³ thá»ƒ táº¯t riÃªng vÃ¬ quá»¹ Ä‘áº§u ká»³
    /// cáº§n tá»›i táº­n cÃ¡c phiáº¿u trÆ°á»›c má»‘c thá»i gian nhÆ°ng khÃ´ng Ä‘Æ°á»£c lá»c theo sá»‘ tiá»n.
    /// </para>
    /// <para>
    /// <paramref name="apDungBoLocHienThi"/> = false Ä‘á»ƒ bá» toÃ n bá»™ cÃ¡c bá»™ lá»c chá»‰
    /// dÃ¹ng cho viá»‡c xem danh sÃ¡ch (tÃ¬m kiáº¿m, loáº¡i chá»©ng tá»«, loáº¡i thu/chi, ngÆ°á»i
    /// táº¡o, tráº¡ng thÃ¡i, khoáº£ng tiá»n). Tháº» tá»•ng quan cá»§a sá»• quá»¹ dÃ¹ng cháº¿ Ä‘á»™ nÃ y Ä‘á»ƒ
    /// tá»“n quá»¹ luÃ´n Ä‘Ãºng sá»‘ dÆ° thá»±c táº¿, khÃ´ng bá»‹ bÃ³p mÃ©o khi ngÆ°á»i dÃ¹ng lá»c báº£ng.
    /// </para>
    /// </summary>
    private static string TaoDieuKienSoQuy(
        bool apDungKhoangThoiGian,
        bool apDungKhoangTien,
        bool coCotNguoiNopNhanGhiChu = true,
        bool apDungBoLocHienThi = true)
    {
        var dieuKien = new List<string>();

        if (apDungBoLocHienThi)
        {
            // Ã” tÃ¬m kiáº¿m: mÃ£ phiáº¿u, ná»™i dung, ghi chÃº, ngÆ°á»i ná»™p/nháº­n, ngÆ°á»i táº¡o, tÃªn loáº¡i thu/chi.
            var timKiem = new StringBuilder();
            timKiem.Append(
                "(@TimKiem = ''"
                + " OR pc.ma_phieu_chi LIKE @TimKiemLike"
                + " OR COALESCE(pc.noi_dung, '') LIKE @TimKiemLike"
                + " OR COALESCE(nd.ho_ten, '') LIKE @TimKiemLike"
                + " OR COALESCE(ncp.ten_nhom, '') LIKE @TimKiemLike");

            // Hai cá»™t nÃ y chá»‰ tá»“n táº¡i khi migration Ä‘Ã£ cháº¡y thÃ nh cÃ´ng.
            if (coCotNguoiNopNhanGhiChu)
            {
                timKiem.Append(" OR COALESCE(pc.nguoi_nop_nhan, '') LIKE @TimKiemLike");
                timKiem.Append(" OR COALESCE(pc.ghi_chu, '') LIKE @TimKiemLike");
            }

            timKiem.Append(")");
            dieuKien.Add(timKiem.ToString());

            // Loáº¡i chá»©ng tá»«: 0 = cáº£ hai, 1 = chá»‰ phiáº¿u thu, 2 = chá»‰ phiáº¿u chi.
            dieuKien.Add(
                "(@LoaiChungTu = 0"
                + " OR (@LoaiChungTu = 1 AND pc.ma_phieu_chi LIKE 'PT%')"
                + " OR (@LoaiChungTu = 2 AND pc.ma_phieu_chi LIKE 'PC%'))");
        }

        // Quá»¹: so khá»›p Ä‘Ãºng mÃ£ phÆ°Æ¡ng thá»©c Ä‘ang cÃ³ trong báº£ng phieu_chi.
        dieuKien.Add("(@PhuongThuc = '' OR pc.phuong_thuc = @PhuongThuc)");

        if (apDungKhoangThoiGian)
        {
            dieuKien.Add("(@TuNgay IS NULL OR pc.thoi_gian_chi >= @TuNgay)");
            dieuKien.Add("(@DenNgay IS NULL OR pc.thoi_gian_chi < @DenNgay)");
        }

        if (apDungBoLocHienThi)
        {
            // CÃ¡c nhÃ³m lá»±a chá»n nhiá»u má»¥c gá»­i lÃªn dáº¡ng danh sÃ¡ch id cÃ¡ch nhau báº±ng dáº¥u pháº©y.
            dieuKien.Add(
                "(@LoaiThuChiIds = ''"
                + " OR FIND_IN_SET(CAST(pc.nhom_chi_phi_id AS CHAR), @LoaiThuChiIds) > 0)");
            dieuKien.Add(
                "(@NguoiTaoIds = ''"
                + " OR FIND_IN_SET(CAST(pc.nguoi_thuc_hien_id AS CHAR), @NguoiTaoIds) > 0)");
        }

        if (apDungKhoangTien)
        {
            dieuKien.Add("(@TienTu IS NULL OR pc.so_tien >= @TienTu)");
            dieuKien.Add("(@TienDen IS NULL OR pc.so_tien <= @TienDen)");
        }

        return string.Join(" AND ", dieuKien);
    }

    /// <summary>Gom bá»™ lá»c thÃ nh Ä‘Ãºng bá»™ tham sá»‘ mÃ  cÃ¡c cÃ¢u SQL kia Ä‘ang mong Ä‘á»£i.</summary>
    private static object TaoThamSoSoQuy(SoQuyBoLoc loc, bool apDungKhoangTien)
    {
        var timKiem = (loc.TimKiem ?? "").Trim();

        return new
        {
            TimKiem = timKiem,
            TimKiemLike = $"%{timKiem}%",
            LoaiChungTu = loc.LoaiChungTu,
            PhuongThuc = loc.PhuongThuc ?? "",
            TuNgay = loc.TuNgay,
            DenNgay = loc.DenNgay,
            LoaiThuChiIds = loc.LoaiThuChiIds ?? "",
            NguoiTaoIds = loc.NguoiTaoIds ?? "",
            TienTu = apDungKhoangTien ? loc.TienTu : null,
            TienDen = apDungKhoangTien ? loc.TienDen : null
        };
    }

    /// <summary>
    /// Láº¥y má»™t trang phiáº¿u cá»§a sá»• quá»¹ theo bá»™ lá»c, kÃ¨m tá»•ng sá»‘ phiáº¿u khá»›p bá»™ lá»c
    /// Ä‘á»ƒ phÃ¢n trng. Chá»‰ Ä‘á»c Ä‘Ãºng <c>pageSize</c> dÃ²ng, khÃ´ng kÃ©o toÃ n bá»™ báº£ng vá».
    /// </summary>
    public async Task<(IReadOnlyList<SoQuyGiaoDich> Items, int TongSoBanGhi)> GetSoQuyPhieuPagedAsync(
        SoQuyBoLoc loc,
        int page,
        int pageSize)
    {
        loc ??= new SoQuyBoLoc();

        // Gáº¯n loáº¡i cho cÃ¡c phiáº¿u tá»± sinh cÃ²n NULL trÆ°á»›c khi lá»c, Ä‘á»ƒ bá»™ lá»c
        // "Thu khÃ¡ch hÃ ng" / "Chi nhÃ  cung cáº¥p" Ã¡p Ä‘Ãºng cáº£ dá»¯ liá»‡u cÅ©.
        await EnsureSoQuyNhomChiPhiChoPhieuCuAsync();

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        var dieuKien = TaoDieuKienSoQuy(apDungKhoangThoiGian: true, apDungKhoangTien: true, coCotMoi);
        var thamSo = TaoThamSoSoQuy(loc, apDungKhoangTien: true);

        var countSql = $"""
        SELECT COUNT(*)
        {SoQuyFrom}
        WHERE {dieuKien};
        """;

        var tongSoBanGhi = await connection.ExecuteScalarAsync<int>(countSql, thamSo);

        if (tongSoBanGhi == 0)
            return (Array.Empty<SoQuyGiaoDich>(), 0);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        var offset = (safePage - 1) * safePageSize;

        var listSql = $"""
        SELECT
            {LayCotChonSoQuy(coCotMoi)}
        {SoQuyFrom}
        WHERE {dieuKien}
        ORDER BY pc.thoi_gian_chi DESC, pc.id DESC
        LIMIT @Limit OFFSET @Offset;
        """;

        var rows = await connection.QueryAsync<SoQuyPhieuRow>(
            listSql,
            GopThamSo(thamSo, safePageSize, offset));

        return (ChuyenThanhGiaoDich(rows), tongSoBanGhi);
    }

    /// <summary>
    /// Chuyá»ƒn dÃ²ng káº¿t quáº£ SQL thÃ nh Ä‘á»‘i tÆ°á»£ng hiá»ƒn thá»‹.
    /// DÃ¹ng chung cho danh sÃ¡ch phÃ¢n trang vÃ  xuáº¥t Excel nÃªn hai nÆ¡i khÃ´ng thá»ƒ lá»‡ch nhau.
    /// </summary>
    private static List<SoQuyGiaoDich> ChuyenThanhGiaoDich(IEnumerable<SoQuyPhieuRow> rows)
    {
        var items = new List<SoQuyGiaoDich>();

        foreach (var row in rows)
        {
            var maPhieu = row.MaPhieu ?? "";

            items.Add(new SoQuyGiaoDich
            {
                MaPhieu = maPhieu,
                // Phiáº¿u thu vÃ  phiáº¿u chi cÃ¹ng náº±m trong báº£ng phieu_chi nÃªn loáº¡i
                // phiáº¿u Ä‘Æ°á»£c suy ra tá»« tiá»n tá»‘ mÃ£ (PTxxxx / PCxxxx).
                LaThu = maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase),
                // NgÆ°á»i ná»™p/nháº­n lÃ  thÃ´ng tin riÃªng cá»§a phiáº¿u (cá»™t nguoi_nop_nhan),
                // cÃ²n ngÆ°á»i táº¡o lÃ  nhÃ¢n viÃªn thao tÃ¡c (FK nguoi_thuc_hien_id).
                NguoiNopNhan = row.NguoiNopNhan ?? "",
                NguoiTao = row.NguoiTao ?? "",
                NoiDung = row.NoiDung ?? "",
                GhiChu = row.GhiChu ?? "",
                GiaTri = ToDecimal(row.SoTien),
                ThoiGian = row.ThoiGian,
                TenLoai = row.TenLoai ?? "",
                PhuongThuc = row.PhuongThuc ?? "",
                TrangThai = row.TrangThai ?? ""
            });
        }

        return items;
    }

    /// <summary>
    /// Láº¥y chi tiáº¿t má»™t phiáº¿u thu/chi Ä‘á»ƒ má»Ÿ mÃ n hÃ¬nh chi tiáº¿t.
    /// Chá»‰ Ä‘á»c Ä‘Ãºng má»™t dÃ²ng theo mÃ£ phiáº¿u nÃªn khÃ´ng áº£nh hÆ°á»Ÿng hiá»‡u nÄƒng.
    /// </summary>
    public async Task<SoQuyChiTiet?> GetSoQuyChiTietAsync(string maPhieu)
    {
        if (string.IsNullOrWhiteSpace(maPhieu))
            return null;

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        var sql = $"""
        SELECT
            {LayCotChonSoQuy(coCotMoi)}
        {SoQuyFrom}
        WHERE pc.ma_phieu_chi = @MaPhieu
        LIMIT 1;
        """;

        var row = await connection.QueryFirstOrDefaultAsync<SoQuyPhieuRow>(
            sql,
            new { MaPhieu = maPhieu.Trim() });

        if (row is null)
            return null;

        var ma = row.MaPhieu ?? "";

        return new SoQuyChiTiet
        {
            MaPhieu = ma,
            LaThu = ma.StartsWith("PT", StringComparison.OrdinalIgnoreCase),
            ThoiGian = row.ThoiGian,
            NguoiNopNhan = row.NguoiNopNhan ?? "",
            SoTien = ToDecimal(row.SoTien),
            NoiDung = row.NoiDung ?? "",
            GhiChu = row.GhiChu ?? "",
            NguoiTao = row.NguoiTao ?? "",
            TrangThai = row.TrangThai ?? "",
            TenLoai = row.TenLoai ?? "",
            NhomChiPhiId = row.NhomChiPhiId,
            PhuongThuc = row.PhuongThuc ?? ""
        };
    }

    /// <summary>
    /// Há»§y phiáº¿u: chá»‰ chuyá»ƒn sang tráº¡ng thÃ¡i HUY khi phiáº¿u Ä‘ang HOAN_THANH.
    /// Tráº£ vá» sá»‘ dÃ²ng Ä‘Æ°á»£c cáº­p nháº­t (0 = phiáº¿u khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ há»§y).
    /// </summary>
    public async Task<int> HuyPhieuQuyAsync(string maPhieu)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookCancel, "Báº¡n khÃ´ng cÃ³ quyá»n há»§y phiáº¿u thu/chi.");
        if (string.IsNullOrWhiteSpace(maPhieu))
            return 0;

        await using var connection = await OpenAsync();

        const string sql = """
        UPDATE phieu_chi
        SET trang_thai = @TrangThaiHuy
        WHERE ma_phieu_chi = @MaPhieu
          AND trang_thai = @TrangThaiHoanThanh
        LIMIT 1;
        """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            MaPhieu = maPhieu.Trim(),
            TrangThaiHuy = TrangThaiPhieuHuy,
            TrangThaiHoanThanh = TrangThaiPhieuHoanThanh
        });

        if (affected > 0)
        {
            _ = LogSubAccountActivityAsync("Há»§y sá»• quá»¹", "SO_QUY", null, maPhieu.Trim(), $"Há»§y phiáº¿u sá»• quá»¹ {maPhieu.Trim()}");
        }

        return affected;
    }

    /// <summary>
    /// Há»§y nhiá»u phiáº¿u cÃ¹ng lÃºc trong má»™t transaction: hoáº·c cáº£ nhÃ³m thÃ nh cÃ´ng,
    /// hoáº·c khÃ´ng phiáº¿u nÃ o bá»‹ Ä‘á»•i. DÃ¹ng cho thao tÃ¡c chá»n nhiá»u trÃªn danh sÃ¡ch.
    /// Tráº£ vá» sá»‘ phiáº¿u thá»±c sá»± chuyá»ƒn sang tráº¡ng thÃ¡i há»§y (nhá»¯ng phiáº¿u Ä‘Ã£ há»§y trÆ°á»›c Ä‘Ã³ khÃ´ng tÃ­nh).
    /// </summary>
    public async Task<int> HuyNhieuPhieuQuyAsync(IReadOnlyCollection<string> maPhieus)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookCancel, "Báº¡n khÃ´ng cÃ³ quyá»n há»§y phiáº¿u thu/chi.");
        var danhSach = maPhieus
            .Where(ma => !string.IsNullOrWhiteSpace(ma))
            .Select(ma => ma.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (danhSach.Count == 0)
            return 0;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var soDong = await connection.ExecuteAsync(
                """
                UPDATE phieu_chi
                SET trang_thai = @TrangThaiHuy
                WHERE trang_thai = @TrangThaiHoanThanh
                  AND ma_phieu_chi IN @MaPhieus;
                """,
                new
                {
                    TrangThaiHuy = TrangThaiPhieuHuy,
                    TrangThaiHoanThanh = TrangThaiPhieuHoanThanh,
                    MaPhieus = danhSach.ToArray()
                },
                transaction);

            await transaction.CommitAsync();

            return soDong;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Láº¥y toÃ n bá»™ phiáº¿u khá»›p bá»™ lá»c Ä‘á»ƒ xuáº¥t Excel (khÃ´ng phÃ¢n trang).
    /// DÃ¹ng Ä‘Ãºng Ä‘iá»u kiá»‡n WHERE vá»›i danh sÃ¡ch nÃªn file xuáº¥t ra luÃ´n khá»›p
    /// vá»›i nhá»¯ng gÃ¬ ngÆ°á»i dÃ¹ng Ä‘ang nhÃ¬n tháº¥y trÃªn mÃ n hÃ¬nh.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyGiaoDich>> GetSoQuyPhieuExportAsync(SoQuyBoLoc loc)
    {
        loc ??= new SoQuyBoLoc();

        await EnsureSoQuyNhomChiPhiChoPhieuCuAsync();

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        var dieuKien = TaoDieuKienSoQuy(apDungKhoangThoiGian: true, apDungKhoangTien: true, coCotMoi);
        var thamSo = TaoThamSoSoQuy(loc, apDungKhoangTien: true);

        var listSql = $"""
        SELECT
            {LayCotChonSoQuy(coCotMoi)}
        {SoQuyFrom}
        WHERE {dieuKien}
        ORDER BY pc.thoi_gian_chi DESC, pc.id DESC;
        """;

        var rows = await connection.QueryAsync<SoQuyPhieuRow>(listSql, thamSo);

        return ChuyenThanhGiaoDich(rows);
    }

    /// <summary>Trá»™n bá»™ tham sá»‘ lá»c vá»›i LIMIT/OFFSET thÃ nh má»™t Ä‘á»‘i tÆ°á»£ng duy nháº¥t.</summary>
    private static Dictionary<string, object?> GopThamSo(
        object thamSoLoc,
        int limit,
        int offset)
    {
        var gop = new Dictionary<string, object?>();

        foreach (var thuocTinh in thamSoLoc.GetType().GetProperties())
            gop[thuocTinh.Name] = thuocTinh.GetValue(thamSoLoc);

        gop["Limit"] = limit;
        gop["Offset"] = offset;

        return gop;
    }

    /// <summary>
    /// Thá»‘ng kÃª sá»• quá»¹: quá»¹ Ä‘áº§u ká»³, tá»•ng thu, tá»•ng chi, tá»“n quá»¹, tá»•ng sá»‘ phiáº¿u.
    /// Bá»‘n tháº» chá»‰ sá»‘ luÃ´n tÃ­nh trÃªn Cáº¢ phiáº¿u thu láº«n phiáº¿u chi, chá»‰ theo ká»³ thá»i
    /// gian vÃ  quá»¹ Ä‘ang chá»n, nÃªn tá»“n quá»¹ luÃ´n lÃ  sá»‘ dÆ° thá»±c táº¿ chá»© khÃ´ng bá»‹
    /// bÃ³p mÃ©o khi ngÆ°á»i dÃ¹ng lá»c loáº¡i chá»©ng tá»« / tÃ¬m kiáº¿m / loáº¡i thu chi / ngÆ°á»i
    /// táº¡o / khoáº£ng tiá»n (cÃ¡c bá»™ lá»c Ä‘Ã³ chá»‰ áº£nh hÆ°á»Ÿng danh sÃ¡ch bÃªn dÆ°á»›i).
    /// Tá»•ng thu/tá»•ng chi chá»‰ cá»™ng phiáº¿u <see cref="TrangThaiPhieuHoanThanh"/> nÃªn
    /// phiáº¿u Ä‘Ã£ há»§y khÃ´ng lÃ m thay Ä‘á»•i quá»¹.
    /// </summary>
    public async Task<SoQuyTongHop> GetSoQuyTongHopTheoLocAsync(SoQuyBoLoc loc)
    {
        loc ??= new SoQuyBoLoc();

        try
        {
            var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

            await using var connection = await OpenAsync();

            // Tháº» tá»•ng quan bá» qua cÃ¡c bá»™ lá»c chá»‰ dÃ¹ng Ä‘á»ƒ xem danh sÃ¡ch
            // (loáº¡i chá»©ng tá»«, tÃ¬m kiáº¿m, loáº¡i thu/chi, ngÆ°á»i táº¡o, tráº¡ng thÃ¡i,
            // khoáº£ng tiá»n) Ä‘á»ƒ quá»¹ Ä‘áº§u ká»³ vÃ  tá»“n quá»¹ luÃ´n lÃ  sá»‘ dÆ° thá»±c táº¿.
            var dieuKienDauKy = TaoDieuKienSoQuy(
                apDungKhoangThoiGian: false,
                apDungKhoangTien: false,
                coCotMoi,
                apDungBoLocHienThi: false);

            var dieuKienKy = TaoDieuKienSoQuy(
                apDungKhoangThoiGian: true,
                apDungKhoangTien: false,
                coCotMoi,
                apDungBoLocHienThi: false);

            // Quá»¹ Ä‘áº§u ká»³ chá»‰ cÃ³ nghÄ©a khi Ä‘ang giá»›i háº¡n bá»Ÿi má»‘c báº¯t Ä‘áº§u: gom táº¥t cáº£
            // phiáº¿u trÆ°á»›c má»‘c Ä‘Ã³, thu trá»« chi.
            var sql = $"""
            SELECT
                (
                    SELECT COALESCE(
                        SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN pc.so_tien ELSE -pc.so_tien END),
                        0)
                    {SoQuyFrom}
                    WHERE {dieuKienDauKy}
                      AND @TuNgay IS NOT NULL
                      AND pc.thoi_gian_chi < @TuNgay
                      AND pc.trang_thai = @TrangThaiHoanThanh
                ) AS QuyDauKy,
                COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN pc.so_tien ELSE 0 END), 0) AS TongThu,
                COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PC%' THEN pc.so_tien ELSE 0 END), 0) AS TongChi
            {SoQuyFrom}
            WHERE {dieuKienKy}
              AND pc.trang_thai = @TrangThaiHoanThanh;
            """;

            var thamSo = TaoThamSoSoQuy(loc, apDungKhoangTien: false);

            var parameters = GopThamSo(thamSo, 0, 0);
            parameters["TrangThaiHoanThanh"] = TrangThaiPhieuHoanThanh;

            var row = await connection.QueryFirstOrDefaultAsync<SoQuyTongHopRow>(sql, parameters);

            // Tá»•ng sá»‘ phiáº¿u pháº£i khá»›p danh sÃ¡ch Ä‘ang hiá»ƒn thá»‹ nÃªn Ä‘áº¿m theo Ä‘Ãºng
            // bá»™ lá»c hiá»ƒn thá»‹ (gá»“m cáº£ phiáº¿u Ä‘Ã£ há»§y).
            var dieuKienDem = TaoDieuKienSoQuy(apDungKhoangThoiGian: true, apDungKhoangTien: true, coCotMoi);
            var thamSoDem = TaoThamSoSoQuy(loc, apDungKhoangTien: true);

            // Cá»™ng sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ do ngÆ°á»i dÃ¹ng khai bÃ¡o (báº£ng so_quy_dau_ky)
            // vÃ o quá»¹ Ä‘áº§u ká»³ Ä‘á»ƒ tá»“n quá»¹ luÃ´n pháº£n Ã¡nh Ä‘Ãºng tiá»n thá»±c cÃ³.
            // Chá»‰ cá»™ng nhá»¯ng khai bÃ¡o cÃ³ ngÃ y Ä‘áº§u ká»³ khÃ´ng vÆ°á»£t quÃ¡ má»‘c báº¯t Ä‘áº§u
            // cá»§a ká»³ Ä‘ang xem, Ä‘á»ƒ khai bÃ¡o cá»§a ká»³ sau khÃ´ng lÃ m lá»‡ch sá»‘.
            var soDuQuyDauKy = await GetTongSoDuQuyDauKyAsync(loc.PhuongThuc, loc.TuNgay);

            var quyDauKy = (row?.QuyDauKy ?? 0m) + soDuQuyDauKy;
            var tongThu = row?.TongThu ?? 0m;
            var tongChi = row?.TongChi ?? 0m;
            var tonQuy = quyDauKy + tongThu - tongChi;

            // Bá»‘n tháº» tá»•ng quan luÃ´n pháº£n Ã¡nh Ä‘Ãºng sá»‘ liá»‡u cá»§a ká»³ Ä‘ang xem:
            // quá»¹ Ä‘áº§u ká»³ + tá»•ng thu - tá»•ng chi = tá»“n quá»¹. Chip "loáº¡i chá»©ng tá»«"
            // chá»‰ lá»c danh sÃ¡ch bÃªn dÆ°á»›i, khÃ´ng bÃ³p mÃ©o sá»‘ liá»‡u cá»§a ká»³.
            return new SoQuyTongHop
            {
                QuyDauKy = quyDauKy,
                TongThu = tongThu,
                TongChi = tongChi,
                TonQuy = tonQuy,
                TongSoPhieu = await connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) {SoQuyFrom} WHERE {dieuKienDem};",
                    thamSoDem)
            };
        }
        catch
        {
            // Tá»•ng quá»¹ chá»‰ Ä‘á»ƒ hiá»ƒn thá»‹: lá»—i truy váº¥n khÃ´ng Ä‘Æ°á»£c cháº·n mÃ n hÃ¬nh.
            return new SoQuyTongHop();
        }
    }

    /// <summary>
    /// CÃ¡c quá»¹ thá»±c sá»± Ä‘ang cÃ³ dá»¯ liá»‡u trong báº£ng phieu_chi.
    /// KhÃ´ng tá»± thÃªm lá»±a chá»n "ngÃ¢n hÃ ng"/"vÃ­ Ä‘iá»‡n tá»­" khi database chÆ°a cÃ³.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyLuaChon>> GetSoQuyQuyOptionsAsync()
    {
        try
        {
            await using var connection = await OpenAsync();

            const string sql = """
            SELECT DISTINCT phuong_thuc
            FROM phieu_chi
            WHERE phuong_thuc IS NOT NULL AND phuong_thuc <> ''
            ORDER BY phuong_thuc;
            """;

            var maQuy = await connection.QueryAsync<string>(sql);

            return maQuy
                .Select(ma => new SoQuyLuaChon(ma, TenHienThiPhuongThuc(ma)))
                .ToList();
        }
        catch
        {
            return Array.Empty<SoQuyLuaChon>();
        }
    }

    private static bool _daDamBaoNhomChiPhiMacDinh;

    /// <summary>
    /// Báº£o Ä‘áº£m hai danh má»¥c "Thu khÃ¡ch hÃ ng" vÃ  "Chi nhÃ  cung cáº¥p" luÃ´n cÃ³ trong
    /// báº£ng <c>nhom_chi_phi</c> Ä‘á»ƒ ngÆ°á»i dÃ¹ng chá»n khi táº¡o phiáº¿u thu/chi thá»§ cÃ´ng
    /// vÃ  dÃ¹ng lÃ m tiÃªu chÃ­ lá»c á»Ÿ sá»• quá»¹. Má»¥c nÃ o Ä‘Ã£ tá»“n táº¡i thÃ¬ bá» qua.
    /// </summary>
    public async Task EnsureNhomChiPhiMacDinhAsync()
    {
        if (_daDamBaoNhomChiPhiMacDinh)
            return;

        try
        {
            await using var connection = await OpenAsync();

            const string sql = """
            INSERT INTO nhom_chi_phi (ma_nhom, ten_nhom, ghi_chu, trang_thai)
            SELECT @MaNhom, @TenNhom, NULL, 1
            WHERE NOT EXISTS (SELECT 1 FROM nhom_chi_phi WHERE ma_nhom = @MaNhom);
            """;

            var nhomMacDinh = new[]
            {
                new { MaNhom = "THU_KHACH_HANG", TenNhom = "Thu khÃ¡ch hÃ ng" },
                new { MaNhom = "CHI_NHA_CUNG_CAP", TenNhom = "Chi nhÃ  cung cáº¥p" }
            };

            await connection.ExecuteAsync(sql, nhomMacDinh);

            _daDamBaoNhomChiPhiMacDinh = true;
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("EnsureNhomChiPhiMacDinhAsync error", ex);
        }
    }

    /// <summary>
    /// CÃ¡c loáº¡i thu/chi thá»±c táº¿ Ä‘ang cÃ³ trong báº£ng nhom_chi_phi.
    /// Báº£ng nÃ y rá»—ng thÃ¬ bá»™ lá»c hiá»ƒn thá»‹ rá»—ng, khÃ´ng bá»‹a thÃªm loáº¡i giáº£.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyLuaChonItem>> GetSoQuyLoaiThuChiOptionsAsync()
    {
        try
        {
            await EnsureNhomChiPhiMacDinhAsync();

            await using var connection = await OpenAsync();

            // Dapper khá»›p tÃªn cá»™t theo Ä‘Ãºng tÃªn thuá»™c tÃ­nh (bá» qua dáº¥u gáº¡ch dÆ°á»›i),
            // nÃªn pháº£i Ä‘áº·t alias lÃ  TenNhom chá»© khÃ´ng Ä‘á»ƒ tÃªn cá»™t lÃ  ten_nhom.
            const string sql = """
            SELECT id, ten_nhom AS TenNhom
            FROM nhom_chi_phi
            WHERE trang_thai = 1
            ORDER BY ten_nhom;
            """;

            var loai = await connection.QueryAsync<SoQuyTenNhomRow>(sql);

            return loai
                .Select(item => new SoQuyLuaChonItem(
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.TenNhom))
                .ToList();
        }
        catch
        {
            return Array.Empty<SoQuyLuaChonItem>();
        }
    }

    /// <summary>
    /// TÃªn hiá»ƒn thá»‹ cá»§a má»™t mÃ£ loáº¡i thu/chi trong báº£ng nhom_chi_phi.
    /// Tráº£ vá» rï¿½ï¿½ng náº¿u id khÃ´ng tá»“n táº¡i (phiáº¿u cÅ© chÆ°a gÃ¡n nhÃ³m).
    /// </summary>
    public async Task<string> GetTenNhomChiPhiAsync(int id)
    {
        try
        {
            await using var connection = await OpenAsync();

            return await connection.ExecuteScalarAsync<string?>(
                "SELECT ten_nhom FROM nhom_chi_phi WHERE id = @Id LIMIT 1;",
                new { Id = id }) ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>NgÆ°á»i dÃ¹ng thá»±c táº¿ trong báº£ng nguoi_dung, dÃ¹ng cho bá»™ lá»c "NgÆ°á»i táº¡o".</summary>
    public async Task<IReadOnlyList<SoQuyLuaChonItem>> GetSoQuyNguoiTaoOptionsAsync()
    {
        try
        {
            await using var connection = await OpenAsync();

            // DÃ¹ng chung kiá»ƒu dÃ²ng vá»›i nhom_chi_phi, xem chÃº thÃ­ch alias bÃªn trÃªn.
            const string sql = """
            SELECT id, ho_ten AS TenNhom
            FROM nguoi_dung
            WHERE trang_thai = 1
            ORDER BY ho_ten;
            """;

            var nguoi = await connection.QueryAsync<SoQuyTenNhomRow>(sql);

            return nguoi
                .Select(item => new SoQuyLuaChonItem(
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.TenNhom))
                .ToList();
        }
        catch
        {
            return Array.Empty<SoQuyLuaChonItem>();
        }
    }

    /// <summary>
    /// Äá»•i mÃ£ phÆ°Æ¡ng thá»©c trong database sang nhÃ£n tiáº¿ng Viá»‡t.
    /// MÃ£ láº¡ thÃ¬ hiá»‡n nguyÃªn vÄƒn, khÃ´ng bá»‹a nghÄ©a.
    /// </summary>
    private static string TenHienThiPhuongThuc(string ma) => ma.Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" => "Tiá»n máº·t",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "NgÃ¢n hÃ ng",
        "THE" or "THE_TIN_DUNG" => "Tháº»",
        "VI_DIEN_TU" or "VI" => "VÃ­ Ä‘iá»‡n tá»­",
        _ => ma
    };

    /// <summary>DÃ²ng káº¿t quáº£ truy váº¥n sá»• quá»¹ (chá»‰ dÃ¹ng ná»™i bá»™ DatabaseService).</summary>
    private sealed class SoQuyPhieuRow
    {
        public string MaPhieu { get; set; } = "";
        public decimal SoTien { get; set; }
        public string NoiDung { get; set; } = "";
        public string NguoiNopNhan { get; set; } = "";
        public string GhiChu { get; set; } = "";
        public DateTime ThoiGian { get; set; }

        /// <summary>
        /// NhÃ¢n viÃªn thao tÃ¡c (FK nguoi_thuc_hien_id), tá»©c ngÆ°á»i táº¡o chá»©ng tá»«.
        /// KhÃ´ng pháº£i ngÆ°á»i ná»™p/nháº­n â€” thÃ´ng tin Ä‘Ã³ náº±m á»Ÿ cá»™t riÃªng.
        /// </summary>
        public string NguoiTao { get; set; } = "";

        public string TrangThai { get; set; } = "";
        public string TenLoai { get; set; } = "";

        /// <summary>KhÃ³a ngoáº¡i tá»›i nhom_chi_phi, cáº§n cho form sá»­a Ä‘á»ƒ chá»n láº¡i loáº¡i.</summary>
        public uint? NhomChiPhiId { get; set; }
        public string PhuongThuc { get; set; } = "";
    }

    /// <summary>Danh má»¥c chuáº©n hÃ³a dÃ¹ng Ä‘á»ƒ tra cá»©u khi validate file nháº­p.</summary>
    private sealed class SoQuyChuanHoaRow
    {
        public uint Id { get; set; }
        public string Ten { get; set; } = "";
    }

    /// <summary>DÃ²ng káº¿t quáº£ tá»•ng há»£p sá»• quá»¹ (chá»‰ dÃ¹ng ná»™i bá»™ DatabaseService).</summary>
    private sealed class SoQuyTongHopRow
    {
        public decimal QuyDauKy { get; set; }
        public decimal TongThu { get; set; }
        public decimal TongChi { get; set; }
        public int TongSoPhieu { get; set; }
    }

    /// <summary>Cáº·p id + tÃªn dÃ¹ng chung cho nhom_chi_phi vÃ  nguoi_dung.</summary>
    private sealed class SoQuyTenNhomRow
    {
        public uint Id { get; set; }
        public string TenNhom { get; set; } = "";
    }

    /// <summary>
    /// Tra cá»©u má»™t láº§n danh má»¥c cáº§n cho bÆ°á»›c validate file nháº­p.
    /// Tráº£ vá» cáº£ chiá»u id -&gt; tÃªn vÃ  tÃªn Ä‘Ã£ bá» dáº¥u -&gt; id Ä‘á»ƒ dÃ²ng trong file chá»‰ cáº§n
    /// ghi tÃªn loáº¡i / tÃªn ngÆ°á»i táº¡o cÅ©ng tra khá»›p Ä‘Æ°á»£c, khÃ´ng query tá»«ng dÃ²ng.
    /// </summary>
    public async Task<SoQuyImportDanhMuc> GetSoQuyImportDanhMucAsync()
    {
        var danhMuc = new SoQuyImportDanhMuc();

        try
        {
            await using var connection = await OpenAsync();

            const string sqlLoai = """
            SELECT id, ten_nhom AS Ten
            FROM nhom_chi_phi
            WHERE trang_thai = 1
            ORDER BY ten_nhom;
            """;

            foreach (var row in await connection.QueryAsync<SoQuyChuanHoaRow>(sqlLoai))
            {
                danhMuc.LoaiThuChiTheoId[row.Id] = row.Ten;
                danhMuc.LoaiThuChiTheoTen[ChuanHoaChuoi(row.Ten)] = row.Id;
            }

            const string sqlNguoi = """
            SELECT id, ho_ten AS Ten
            FROM nguoi_dung
            WHERE trang_thai = 1
            ORDER BY ho_ten;
            """;

            foreach (var row in await connection.QueryAsync<SoQuyChuanHoaRow>(sqlNguoi))
            {
                danhMuc.NguoiDungTheoId[row.Id] = row.Ten;
                danhMuc.NguoiDungTheoTen[ChuanHoaChuoi(row.Ten)] = row.Id;
            }

            // Quá»¹ láº¥y tá»« chÃ­nh dá»¯ liá»‡u Ä‘ang cÃ³, khÃ´ng tá»± thÃªm mÃ£ má»›i.
            const string sqlQuy = """
            SELECT phuong_thuc AS Ten
            FROM phieu_chi
            WHERE phuong_thuc IS NOT NULL AND phuong_thuc <> ''
            GROUP BY phuong_thuc
            ORDER BY phuong_thuc;
            """;

            foreach (var ma in await connection.QueryAsync<string>(sqlQuy))
            {
                if (string.IsNullOrWhiteSpace(ma))
                    continue;

                var maChuanHoa = ma.Trim();
                danhMuc.QuyTheoMa[maChuanHoa] = TenHienThiPhuongThuc(maChuanHoa);
                danhMuc.QuyTheoTen[ChuanHoaChuoi(maChuanHoa)] = maChuanHoa;
                danhMuc.QuyTheoTen[ChuanHoaChuoi(TenHienThiPhuongThuc(maChuanHoa))] = maChuanHoa;
            }

            // Náº¿u database cÃ²n chÆ°a cÃ³ phiáº¿u nÃ o thÃ¬ váº«n cho phÃ©p dÃ¹ng giÃ¡ trá»‹ máº·c Ä‘á»‹nh
            // cá»§a cá»™t phuong_thuc, váº«n lÃ  dá»¯ liá»‡u tháº­t cá»§a database chá»© khÃ´ng pháº£i bá»‹a thÃªm.
            if (danhMuc.QuyTheoMa.Count == 0)
            {
                danhMuc.QuyTheoMa[danhMuc.PhuongThucMacDinh] = TenHienThiPhuongThuc(danhMuc.PhuongThucMacDinh);
                danhMuc.QuyTheoTen[ChuanHoaChuoi(danhMuc.PhuongThucMacDinh)] = danhMuc.PhuongThucMacDinh;
            }
        }
        catch
        {
            // Äá»ƒ danh má»¥c rá»—ng: má»i dÃ²ng cáº§n tra cá»©u sáº½ bÃ¡o lá»—i á»Ÿ bÆ°á»›c validate,
            // ngÆ°á»i dÃ¹ng tháº¥y ngay trong bÆ°á»›c xem trÆ°á»›c thay vÃ¬ ghi sai dá»¯ liá»‡u.
        }

        return danhMuc;
    }

    /// <summary>
    /// Láº¥y táº­p mÃ£ phiáº¿u Ä‘Ã£ tá»“n táº¡i trong database. DÃ¹ng cho bÆ°á»›c xem trÆ°á»›c Ä‘á»ƒ biáº¿t
    /// dÃ²ng nÃ o trÃ¹ng mÃ£, trÃ¡nh gá»i vÃ²ng láº·p "SELECT tá»«ng dÃ²ng".
    /// </summary>
    public async Task<HashSet<string>> GetSoQuyMaPhieuTonTaiAsync(IReadOnlyCollection<string> maPhieus)
    {
        var danhSach = maPhieus
            .Where(ma => !string.IsNullOrWhiteSpace(ma))
            .Select(ma => ma.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ketQua = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (danhSach.Count == 0)
            return ketQua;

        await using var connection = await OpenAsync();

        // Chia nhá» Ä‘á»ƒ cÃ¢u IN khÃ´ng vÆ°á»£t quÃ¡ giá»›i háº¡n tham sá»‘ cá»§a MySQL.
        const int khoi = 500;

        for (var i = 0; i < danhSach.Count; i += khoi)
        {
            var chunk = danhSach.Skip(i).Take(khoi).ToArray();

            var tonTai = await connection.QueryAsync<string>(
                "SELECT ma_phieu_chi FROM phieu_chi WHERE ma_phieu_chi IN @MaPhieus;",
                new { MaPhieus = chunk });

            foreach (var ma in tonTai)
            {
                if (!string.IsNullOrWhiteSpace(ma))
                    ketQua.Add(ma.Trim());
            }
        }

        return ketQua;
    }

    /// <summary>
    /// Ghi hÃ ng loáº¡t phiáº¿u sá»• quá»¹ trong má»™t transaction.
    /// Má»™t dÃ²ng lá»—i khiáº¿n toÃ n bá»™ lÆ°á»£t ghi Ä‘Æ°á»£c rollback Ä‘á»ƒ khÃ´ng Ä‘á»ƒ láº¡i dá»¯ liá»‡u ná»­a vá»i.
    /// Tiáº¿n Ä‘á»™ Ä‘Æ°á»£c bÃ¡o sau má»—i lÃ´ Ä‘á»ƒ thanh tiáº¿n trÃªn UI khÃ´ng bá»‹ Ä‘á»©ng.
    /// </summary>
    public async Task<SoQuyImportResult> NhapPhieuQuyAsync(
        IReadOnlyList<SoQuyImportRow> dongGhi,
        XuLyPhieuTrung xuLyTrung,
        Func<SoQuyImportProgress, Task>? baoTienDo = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookImport, "Báº¡n khÃ´ng cÃ³ quyá»n import sá»• quá»¹.");
        var ketQua = new SoQuyImportResult
        {
            XuLyTrung = xuLyTrung
        };

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var chiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1;
            var tongSoDong = dongGhi.Count;

            // Sá»‘ thá»© tá»± mÃ£ tá»± sinh: Ä‘á»c MAX má»™t láº§n rá»“i cáº¥p phÃ¡t liÃªn tá»¥c trong bá»™ nhá»›.
            var soThuTuThu = await DocSoThuTuMaPhieuQuyAsync(connection, transaction, "PT");
            var soThuTuChi = await DocSoThuTuMaPhieuQuyAsync(connection, transaction, "PC");

            var dongDaXuLy = 0;

            foreach (var dong in dongGhi)
            {
                var maPhieu = dong.MaPhieu.Trim();

                if (maPhieu.Length == 0)
                {
                    // File khÃ´ng ghi mÃ£ phiáº¿u thÃ¬ há»‡ thá»‘ng tá»± cáº¥p.
                    maPhieu = dong.LaThu
                        ? "PT" + (soThuTuThu++).ToString("D4", CultureInfo.InvariantCulture)
                        : "PC" + (soThuTuChi++).ToString("D4", CultureInfo.InvariantCulture);
                }

                const string sqlThem = """
                INSERT INTO phieu_chi
                    (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc, noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai)
                VALUES
                    (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc, @NoiDung, @ThoiGian, @NguoiTaoId, @TrangThai);
                """;

                const string sqlThemDayDu = """
                INSERT INTO phieu_chi
                    (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc, noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai)
                VALUES
                    (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc, @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian, @NguoiTaoId, @TrangThai);
                """;

                if (dong.MaPhieuDaTonTai && xuLyTrung == XuLyPhieuTrung.ThayThe)
                {
                    var capNhatCotMoi = coCotMoi
                        ? ", nguoi_nop_nhan = @NguoiNopNhan, ghi_chu = @GhiChu"
                        : "";

                    var sqlCapNhat = $"""
                    UPDATE phieu_chi
                    SET nhom_chi_phi_id = @NhomChiPhiId,
                        so_tien = @SoTien,
                        phuong_thuc = COALESCE(@PhuongThuc, phuong_thuc),
                        noi_dung = @NoiDung{capNhatCotMoi},
                        thoi_gian_chi = @ThoiGian,
                        nguoi_thuc_hien_id = COALESCE(@NguoiTaoId, nguoi_thuc_hien_id),
                        trang_thai = @TrangThai
                    WHERE ma_phieu_chi = @MaPhieu;
                    """;

                    var soDongCapNhat = await connection.ExecuteAsync(sqlCapNhat, TaoThamSoGhiPhieu(
                        maPhieu,
                        chiNhanhId,
                        dong,
                        phuongThuc: dong.PhuongThuc), transaction);

                    // UPDATE khÃ´ng sinh mÃ£ má»›i nÃªn váº«n cáº§n ná»›i sá»‘ thá»© tá»± vá» sau mÃ£ vá»«a dÃ¹ng.
                    if (maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
                        soThuTuThu = Math.Max(soThuTuThu, ParseSoThuTuMaPhieu(maPhieu, "PT") + 1);
                    else if (maPhieu.StartsWith("PC", StringComparison.OrdinalIgnoreCase))
                        soThuTuChi = Math.Max(soThuTuChi, ParseSoThuTuMaPhieu(maPhieu, "PC") + 1);

                    if (soDongCapNhat > 0)
                    {
                        ketQua.SoCapNhat++;
                    }
                    else
                    {
                        ketQua.SoLoi++;
                        ketQua.DanhSachLoi.Add(new SoQuyImportError(dong.SoDongExcel, dong.MaPhieu, "KhÃ´ng tÃ¬m tháº¥y phiáº¿u Ä‘á»ƒ cáº­p nháº­t."));
                    }
                }
                else if (dong.MaPhieuDaTonTai)
                {
                    // Giá»¯ dá»¯ liá»‡u cÅ© hoáº·c bá» qua dÃ²ng trÃ¹ng: hai cÃ¡ch nÃ y Ä‘á»u khÃ´ng ghi.
                    ketQua.SoBoQua++;
                }
                else
                {
                    await connection.ExecuteAsync(
                        coCotMoi ? sqlThemDayDu : sqlThem,
                        TaoThamSoGhiPhieu(maPhieu, chiNhanhId, dong, phuongThuc: dong.PhuongThuc),
                        transaction);

                    ketQua.SoThemMoi++;
                }

                dongDaXuLy++;

                if (baoTienDo is not null)
                    await baoTienDo(new SoQuyImportProgress
                    {
                        GiaiDoan = "Äang ghi phiáº¿u",
                        DaXuLy = dongDaXuLy,
                        TongSo = tongSoDong
                    });
            }

            await transaction.CommitAsync();

            return ketQua;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            // Lá»—i nÃ y cháº·n cáº£ lÆ°á»£t ghi vÃ¬ cÃ³ thá»ƒ lÃ  háº¿t quyá»n, máº¥t káº¿t ná»‘i, trÃ¹ng mÃ£...
            // Rollback sáº¡ch thÃ¬ bÃ¡o ngÆ°á»i dÃ¹ng chá»© khÃ´ng ghi tiáº¿p.
            ketQua.DaRollback = true;
            ketQua.DaHuy = true;
            ketQua.CanhBao.Add($"ÄÃ£ rollback toÃ n bá»™ lÆ°á»£t nháº­p: {ex.Message}");
            ketQua.SoThemMoi = 0;
            ketQua.SoCapNhat = 0;
            ketQua.SoLoi++;
            ketQua.DanhSachLoi.Add(new SoQuyImportError(0, null, ex.Message));

            return ketQua;
        }
    }

    /// <summary>Gom tham sá»‘ chung cho INSERT vÃ  UPDATE cá»§a má»™t dÃ²ng phiáº¿u nháº­p tá»« file.</summary>
    private static object TaoThamSoGhiPhieu(
        string maPhieu,
        uint chiNhanhId,
        SoQuyImportRow dong,
        string? phuongThuc)
    {
        return new
        {
            MaPhieu = maPhieu,
            ChiNhanhId = chiNhanhId,
            NhomChiPhiId = dong.NhomChiPhiId,
            SoTien = dong.SoTien,
            PhuongThuc = string.IsNullOrWhiteSpace(phuongThuc) ? null : phuongThuc.Trim().ToUpperInvariant(),
            NoiDung = string.IsNullOrWhiteSpace(dong.NoiDung) ? null : dong.NoiDung.Trim(),
            NguoiNopNhan = Truncate150(dong.NguoiNopNhan),
            GhiChu = string.IsNullOrWhiteSpace(dong.GhiChu) ? null : dong.GhiChu.Trim(),
            ThoiGian = dong.ThoiGian,
            NguoiTaoId = dong.NguoiTaoId,
            TrangThai = dong.TrangThai
        };
    }

    /// <summary>
    /// Äá»c sá»‘ thá»© tá»± lá»›n nháº¥t cá»§a má»™t tiá»n tá»‘ mÃ£ phiáº¿u, dÃ¹ng Ä‘á»ƒ cáº¥p mÃ£ tá»± sinh khi
    /// nháº­p hÃ ng loáº¡t (trÃ¡nh gá»i SELECT cho tá»«ng dÃ²ng).
    /// </summary>
    private static async Task<long> DocSoThuTuMaPhieuQuyAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tienTo)
    {
        var maxSuffix = await connection.ExecuteScalarAsync<long?>(
            """
            SELECT COALESCE(MAX(CAST(SUBSTRING(TRIM(ma_phieu_chi), 3) AS SIGNED)), 0)
            FROM phieu_chi
            WHERE ma_phieu_chi LIKE CONCAT(@Prefix, '%')
              AND TRIM(ma_phieu_chi) REGEXP CONCAT('^', @Prefix, '[0-9]+$');
            """,
            new { Prefix = tienTo },
            transaction) ?? 0L;

        return maxSuffix > 0 ? maxSuffix + 1 : 1L;
    }

    /// <summary>Láº¥y pháº§n sá»‘ cá»§a mÃ£ phiáº¿u, tráº£ vá» 0 náº¿u mÃ£ khÃ´ng Ä‘Ãºng Ä‘á»‹nh dáº¡ng.</summary>
    private static long ParseSoThuTuMaPhieu(string maPhieu, string tienTo)
    {
        var phanConLai = maPhieu.Trim();

        if (phanConLai.StartsWith(tienTo, StringComparison.OrdinalIgnoreCase))
            phanConLai = phanConLai[tienTo.Length..];

        return long.TryParse(phanConLai, NumberStyles.None, CultureInfo.InvariantCulture, out var giaTri)
            ? giaTri
            : 0L;
    }

    /// <summary>
    /// Bá» dáº¥u vÃ  viáº¿t thÆ°á»ng Ä‘á»ƒ so khá»›p tÃªn loáº¡i thu/chi, tÃªn ngÆ°á»i vÃ  tÃªn quá»¹
    /// giá»¯a file nháº­p vÃ  database, khÃ´ng phá»¥ thuá»™c khÃ¡c biá»‡t hoa/thÆ°á»ng hay dáº¥u tiáº¿ng Viá»‡t.
    /// </summary>
    internal static string ChuanHoaChuoi(string? giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return "";

        var chuoi = giaTri.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);

        return new string(chuoi.Where(ch =>
        {
            var loai = CharUnicodeInfo.GetUnicodeCategory(ch);

            return loai != UnicodeCategory.NonSpacingMark
                && loai != UnicodeCategory.SpacingCombiningMark
                && loai != UnicodeCategory.EnclosingMark;
        }).ToArray());
    }

    /// <summary>Tá»•ng há»£p sá»‘ tiá»n trÃªn cÃ¡c phiáº¿u nháº­p hÃ ng (chá»‰ dÃ¹ng ná»™i bá»™ DatabaseService).</summary>
    private sealed class NhaCungCapPhieuTongHopRow
    {
        public int SoPhieuNhap { get; set; }
        public decimal TongTienNhap { get; set; }
        public decimal TongDaThanhToan { get; set; }
        public decimal TongConNoPhieuNhap { get; set; }
    }

    /// <summary>Tá»•ng há»£p sá»• cÃ¡i cÃ´ng ná»£ (chá»‰ dÃ¹ng ná»™i bá»™ DatabaseService).</summary>
    private sealed class NhaCungCapCongNoTongHopRow
    {
        public int SoLanGhiCongNo { get; set; }
        public decimal TongCongNoLichSu { get; set; }
        public DateTime? LanGhiCongNoGanNhat { get; set; }
    }

    /// <summary>
    /// LÆ°u 1 phiáº¿u thu (PTxxxx) hoáº·c phiáº¿u chi (PCxxxx) cá»§a sá»• quá»¹.
    /// Cá»™t sá»‘ <c>so_tien</c> luÃ´n nháº­n giÃ¡ trá»‹ decimal thÃ´, khÃ´ng nháº­n chuá»—i Ä‘Ã£ Ä‘á»‹nh dáº¡ng.
    /// KhÃ´ng nuá»‘t exception: lá»—i UNIQUE/connection Ä‘Æ°á»£c nÃ©m ra Ä‘á»ƒ ViewModel hiá»ƒn thá»‹ cho ngÆ°á»i dÃ¹ng.
    /// <para>
    /// Ãnh xáº¡ cá»™t: <c>chi_nhanh_id</c> láº¥y chi nhÃ¡nh cá»§a ngÆ°á»i Ä‘ang Ä‘Äƒng nháº­p,
    /// <c>nhom_chi_phi_id</c> lÃ  loáº¡i thu/chi, <c>phuong_thuc</c> lÃ  mÃ£ quá»¹,
    /// <c>nguoi_thuc_hien_id</c> lÃ  ngÆ°á»i táº¡o, cÃ²n <c>nguoi_nop_nhan</c> vÃ 
    /// <c>ghi_chu</c> lÃ  hai thÃ´ng tin riÃªng cá»§a tá»«ng phiáº¿u.
    /// </para>
    /// </summary>
    public async Task LuuPhieuQuyAsync(
        string maPhieu,
        decimal soTien,
        DateTime thoiGian,
        uint? nhomChiPhiId,
        string? phuongThuc,
        uint? nguoiTaoId,
        string? nguoiNopNhan,
        string? noiDung,
        string? ghiChu)
    {
        if (string.IsNullOrWhiteSpace(maPhieu))
            throw new InvalidOperationException("Thiáº¿u mÃ£ phiáº¿u khi lÆ°u sá»• quá»¹.");

        var laPhieuThu = maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase);
        Quyen.RequirePermission(
            Session.CurrentUser,
            laPhieuThu ? PermissionKeys.CashBookCreateReceipt : PermissionKeys.CashBookCreatePayment,
            laPhieuThu ? "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o phiáº¿u thu." : "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o phiáº¿u chi.");

        if (soTien <= 0)
            throw new InvalidOperationException("Sá»‘ tiá»n pháº£i lá»›n hÆ¡n 0.");

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        // Cá»™t tÃ¹y chá»n Ä‘Æ°á»£c ghÃ©p vÃ o danh sÃ¡ch hÃ ng/cá»™t; má»i giÃ¡ trá»‹ váº«n Ä‘i qua tham sá»‘
        // nÃªn khÃ´ng cÃ³ chuyá»‡n dÃ¡n chuá»—i cá»§a ngÆ°á»i dÃ¹ng vÃ o SQL.
        var danhSachCot = "(ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc, noi_dung";
        var danhSachGiaTri = "(@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc, @NoiDung";

        if (coCotMoi)
        {
            danhSachCot += ", nguoi_nop_nhan, ghi_chu";
            danhSachGiaTri += ", @NguoiNopNhan, @GhiChu";
        }

        danhSachCot += ", thoi_gian_chi, nguoi_thuc_hien_id, trang_thai)";
        danhSachGiaTri += ", @ThoiGian, @NguoiTaoId, @TrangThai);";

        var sql = $"INSERT INTO phieu_chi {danhSachCot} VALUES {danhSachGiaTri}";

        await connection.ExecuteAsync(sql, new
        {
            MaPhieu = maPhieu.Trim(),
            ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1,
            NhomChiPhiId = nhomChiPhiId,
            SoTien = soTien,
            // Rá»—ng thÃ¬ Ä‘á»ƒ MySQL láº¥y giÃ¡ trá»‹ máº·c Ä‘á»‹nh cá»§a cá»™t (TIEN_MAT).
            PhuongThuc = string.IsNullOrWhiteSpace(phuongThuc) ? null : phuongThuc.Trim().ToUpperInvariant(),
            NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim(),
            NguoiNopNhan = Truncate150(nguoiNopNhan),
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim(),
            ThoiGian = thoiGian,
            NguoiTaoId = nguoiTaoId,
            TrangThai = TrangThaiPhieuHoanThanh
        });

        _ = LogSubAccountActivityAsync(
            laPhieuThu ? "Thu tiá»n" : "Chi tiá»n",
            "SO_QUY",
            null,
            maPhieu.Trim(),
            noiDung ?? (laPhieuThu ? $"Láº­p phiáº¿u thu {maPhieu.Trim()}" : $"Láº­p phiáº¿u chi {maPhieu.Trim()}"),
            laPhieuThu ? soTien : -soTien);
    }

    /// <summary>Cáº¯t chuá»—i cho vá»«a Ä‘á»™ dÃ i cá»§a cá»™t varchar(150) cá»§a nguoi_nop_nhan.</summary>
    private static string? Truncate150(string? giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return null;

        var chuoi = giaTri.Trim();

        return chuoi.Length <= 150 ? chuoi : chuoi[..150];
    }

    /// <summary>
    /// Sá»­a má»™t phiáº¿u Ä‘ang hiá»‡u lá»±c. Cho sá»­a Ä‘Æ°á»£c cáº£ loáº¡i thu/chi, quá»¹, ngÆ°á»i ná»™p/nháº­n,
    /// ná»™i dung vÃ  ghi chÃº vÃ¬ Ä‘á»u lÃ  thÃ´ng tin cá»§a phiáº¿u; khÃ´ng cho sá»­a mÃ£ phiáº¿u,
    /// ngÆ°á»i táº¡o vÃ  tráº¡ng thÃ¡i vÃ¬ Ä‘Ã³ lÃ  dá»¯ liá»‡u sinh ra lÃºc táº¡o chá»©ng tá»«.
    /// Tráº£ vá» sá»‘ dÃ²ng Ä‘Æ°á»£c cáº­p nháº­t (0 = phiáº¿u khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ há»§y).
    /// </summary>
    public async Task<int> SuaPhieuQuyAsync(
        string maPhieu,
        decimal soTien,
        DateTime thoiGian,
        uint? nhomChiPhiId,
        string? phuongThuc,
        string? nguoiNopNhan,
        string? noiDung,
        string? ghiChu)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a phiáº¿u thu/chi.");
        if (string.IsNullOrWhiteSpace(maPhieu))
            throw new InvalidOperationException("Thiáº¿u mÃ£ phiáº¿u khi sá»­a sá»• quá»¹.");

        if (soTien <= 0)
            throw new InvalidOperationException("Sá»‘ tiá»n pháº£i lá»›n hÆ¡n 0.");

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        // Hai cá»™t má»›i chá»‰ xuáº¥t hiá»‡n trong cÃ¢u UPDATE khi migration Ä‘Ã£ cháº¡y, trÃ¡nh lá»—i
        // "Unknown column" vá»›i database cÅ©. GiÃ¡ trá»‹ váº«n Ä‘i qua tham sá»‘.
        var capNhatCotMoi = coCotMoi ? ", nguoi_nop_nhan = @NguoiNopNhan, ghi_chu = @GhiChu" : "";

        var sql = $"""
        UPDATE phieu_chi
        SET so_tien = @SoTien,
            thoi_gian_chi = @ThoiGian,
            nhom_chi_phi_id = @NhomChiPhiId,
            phuong_thuc = @PhuongThuc,
            noi_dung = @NoiDung{capNhatCotMoi}
        WHERE ma_phieu_chi = @MaPhieu
          AND trang_thai <> @TrangThaiHuy
        LIMIT 1;
        """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            MaPhieu = maPhieu.Trim(),
            SoTien = soTien,
            ThoiGian = thoiGian,
            NhomChiPhiId = nhomChiPhiId,
            PhuongThuc = string.IsNullOrWhiteSpace(phuongThuc) ? null : phuongThuc.Trim().ToUpperInvariant(),
            NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim(),
            NguoiNopNhan = Truncate150(nguoiNopNhan),
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim(),
            TrangThaiHuy = TrangThaiPhieuHuy
        });

        if (affected > 0)
        {
            var laThu = maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase);
            _ = LogSubAccountActivityAsync(
                "Sá»­a sá»• quá»¹",
                "SO_QUY",
                null,
                maPhieu.Trim(),
                $"Cáº­p nháº­t phiáº¿u {maPhieu.Trim()}: {noiDung}",
                laThu ? soTien : -soTien);
        }

        return affected;
    }

    #region User Management & Permissions
    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(string? search = null)
    {
        await using var connection = await OpenAsync();
        var keyword = (search ?? string.Empty).Trim();
        var sql = """
        SELECT 
            nd.id AS Id,
            nd.ma_nguoi_dung AS MaNguoiDung,
            nd.ten_dang_nhap AS TenDangNhap,
            nd.ho_ten AS HoTen,
            nd.trang_thai AS TrangThai,
            nd.ngay_tao AS NgayTao,
            nd.lan_dang_nhap_cuoi AS LanDangNhapCuoi,
            GROUP_CONCAT(DISTINCT vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro,
            GROUP_CONCAT(DISTINCT vt.ma_vai_tro ORDER BY vt.id SEPARATOR ',') AS MaVaiTro
        FROM nguoi_dung nd
        LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
        LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
        WHERE (@Search = '' OR nd.ten_dang_nhap LIKE CONCAT('%', @Search, '%') OR nd.ho_ten LIKE CONCAT('%', @Search, '%') OR nd.ma_nguoi_dung LIKE CONCAT('%', @Search, '%'))
        GROUP BY nd.id, nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten, nd.trang_thai, nd.ngay_tao, nd.lan_dang_nhap_cuoi
        ORDER BY nd.id ASC
        """;
        var items = await connection.QueryAsync<UserListItem>(sql, new { Search = keyword });
        return items.ToList();
    }

    private async Task EnsureNguoiDungContactColumnsAsync(MySqlConnection connection)
    {
        if (_coCotSoDienThoaiNguoiDung.HasValue && _coCotEmailNguoiDung.HasValue)
            return;

        try
        {
            var cols = (await connection.QueryAsync<string>(
                "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nguoi_dung';"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _coCotSoDienThoaiNguoiDung = cols.Contains("so_dien_thoai");
            _coCotEmailNguoiDung = cols.Contains("email");
        }
        catch
        {
            _coCotSoDienThoaiNguoiDung ??= true;
            _coCotEmailNguoiDung ??= true;
        }
    }

    // Pháº§n truy váº¥n danh sÃ¡ch nhÃ¢n viÃªn (há»“ sÆ¡ nhÃ¢n sá»±) náº±m trong file partial
    // DatabaseService.NhanVien.cs Ä‘á»ƒ gom nhá»¯ng thay Ä‘á»•i liÃªn quan tá»›i mÃ n
    // "Quáº£n lÃ½ nhÃ¢n viÃªn" vá» má»™t chá»—.

    /// <summary>
    /// Kiá»ƒm tra rÃ ng buá»™c trÆ°á»›c khi xÃ³a nhÃ¢n viÃªn:
    /// - KhÃ´ng cho xÃ³a tÃ i khoáº£n ID = 1 hoáº·c Quáº£n trá»‹ viÃªn.
    /// - KhÃ´ng cho tá»± xÃ³a tÃ i khoáº£n cá»§a chÃ­nh ngÆ°á»i Ä‘ang thao tÃ¡c.
    /// - Kiá»ƒm tra xem nhÃ¢n viÃªn Ä‘Ã£ tá»«ng láº­p hÃ³a Ä‘Æ¡n, phiáº¿u nháº­p hay phiáº¿u thu/chi chÆ°a.
    /// </summary>
    public async Task<(bool CanDelete, string? Reason)> KiemTraXoaNhanVienAsync(uint nhanVienId)
    {
        if (nhanVienId == 1)
            return (false, "KhÃ´ng thá»ƒ xÃ³a tÃ i khoáº£n Quáº£n trá»‹ viÃªn máº·c Ä‘á»‹nh cá»§a há»‡ thá»‘ng.");

        if (Session.CurrentUser != null && nhanVienId == Session.CurrentUser.Id)
            return (false, "Báº¡n khÃ´ng thá»ƒ tá»± xÃ³a tÃ i khoáº£n cá»§a chÃ­nh mÃ¬nh Ä‘ang Ä‘Äƒng nháº­p.");

        if (await IsQuanTriAsync(nhanVienId))
            return (false, "KhÃ´ng thá»ƒ xÃ³a tÃ i khoáº£n cÃ³ chá»©c vá»¥ / vai trÃ² Quáº£n trá»‹ viÃªn.");

        await using var connection = await OpenAsync();

        // 1. Kiá»ƒm tra hÃ³a Ä‘Æ¡n bÃ¡n hÃ ng
        try
        {
            var hasHoaDonCol = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'hoa_don_ban_hang' AND COLUMN_NAME = 'nguoi_ban_id';");
            if (hasHoaDonCol > 0)
            {
                var soHoaDon = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM hoa_don_ban_hang WHERE nguoi_ban_id = @Id;",
                    new { Id = nhanVienId });
                if (soHoaDon > 0)
                {
                    return (false, $"NhÃ¢n viÃªn nÃ y Ä‘Ã£ láº­p {soHoaDon} hÃ³a Ä‘Æ¡n trong há»‡ thá»‘ng. Äá»ƒ báº£o toÃ n lá»‹ch sá»­ bÃ¡n hÃ ng vÃ  káº¿ toÃ¡n, khÃ´ng thá»ƒ xÃ³a tÃ i khoáº£n. Báº¡n cÃ³ thá»ƒ sá»­ dá»¥ng chá»©c nÄƒng KhÃ³a nhÃ¢n viÃªn.");
                }
            }
        }
        catch { }

        // 2. Kiá»ƒm tra phiáº¿u nháº­p hÃ ng
        try
        {
            var hasPhieuNhapCol = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_nhap_hang' AND COLUMN_NAME = 'nguoi_nhap_id';");
            if (hasPhieuNhapCol > 0)
            {
                var soPhieuNhap = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM phieu_nhap_hang WHERE nguoi_nhap_id = @Id;",
                    new { Id = nhanVienId });
                if (soPhieuNhap > 0)
                {
                    return (false, $"NhÃ¢n viÃªn nÃ y Ä‘Ã£ láº­p {soPhieuNhap} phiáº¿u nháº­p hÃ ng trong há»‡ thá»‘ng. Äá»ƒ báº£o toÃ n lá»‹ch sá»­ kho hÃ ng, khÃ´ng thá»ƒ xÃ³a tÃ i khoáº£n. Báº¡n cÃ³ thá»ƒ sá»­ dá»¥ng chá»©c nÄƒng KhÃ³a nhÃ¢n viÃªn.");
                }
            }
        }
        catch { }

        // 3. Kiá»ƒm tra phiáº¿u chi / thu
        try
        {
            var hasPhieuChiCol = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi' AND COLUMN_NAME = 'nguoi_thuc_hien_id';");
            if (hasPhieuChiCol > 0)
            {
                var soPhieuChi = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM phieu_chi WHERE nguoi_thuc_hien_id = @Id;",
                    new { Id = nhanVienId });
                if (soPhieuChi > 0)
                {
                    return (false, $"NhÃ¢n viÃªn nÃ y Ä‘Ã£ láº­p {soPhieuChi} phiáº¿u thu / chi trong há»‡ thá»‘ng. Äá»ƒ báº£o toÃ n chá»©ng tá»« thu chi, khÃ´ng thá»ƒ xÃ³a tÃ i khoáº£n. Báº¡n cÃ³ thá»ƒ sá»­ dá»¥ng chá»©c nÄƒng KhÃ³a nhÃ¢n viÃªn.");
                }
            }
        }
        catch { }

        return (true, null);
    }

    /// <summary>
    /// XÃ³a an toÃ n nhÃ¢n viÃªn cÃ³ kiá»ƒm tra Ä‘áº§y Ä‘á»§ quyá»n vÃ  rÃ ng buá»™c nghiá»‡p vá»¥.
    /// </summary>
    public async Task XoaNhanVienAnToanAsync(uint nhanVienId)
    {
        var (canDelete, reason) = await KiemTraXoaNhanVienAsync(nhanVienId);
        if (!canDelete)
        {
            throw new InvalidOperationException(reason ?? "KhÃ´ng thá»ƒ xÃ³a nhÃ¢n viÃªn nÃ y.");
        }

        await DeleteUserAsync(nhanVienId, Session.CurrentUser?.Id);
    }

    public async Task<UserWithRoles?> GetUserWithRolesAsync(uint userId)
    {
        await using var connection = await OpenAsync();
        await EnsureNguoiDungContactColumnsAsync(connection);
        var sdtCol = _coCotSoDienThoaiNguoiDung == true ? "nd.so_dien_thoai" : "''";
        var emailCol = _coCotEmailNguoiDung == true ? "nd.email" : "''";
        var sqlUser = $"""
        SELECT nd.id AS Id, nd.ma_nguoi_dung AS MaNguoiDung, nd.ten_dang_nhap AS TenDangNhap, nd.ho_ten AS HoTen,
               {sdtCol} AS SoDienThoai, {emailCol} AS Email, nd.trang_thai AS TrangThai,
               GROUP_CONCAT(DISTINCT vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro,
               GROUP_CONCAT(DISTINCT vt.ma_vai_tro ORDER BY vt.id SEPARATOR ',') AS MaVaiTro
        FROM nguoi_dung nd
        LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
        LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
        WHERE nd.id = @Id
        GROUP BY nd.id, nd.ma_nguoi_dung, nd.ten_dang_nhap, nd.ho_ten, nd.trang_thai
        """;
        var user = await connection.QuerySingleOrDefaultAsync<UserWithRoles>(sqlUser, new { Id = userId });
        if (user == null) return null;
        var roles = await connection.QueryAsync<uint>("SELECT vai_tro_id FROM nguoi_dung_vai_tro WHERE nguoi_dung_id = @Id", new { Id = userId });
        user.RoleIds = roles.ToList();
        return user;
    }

    public async Task<string?> GetUserPermissionsAsStringAsync(uint userId)
    {
        var list = await GetUserPermissionKeysAsync(userId);
        return list.Count == 0 ? null : string.Join(',', list);
    }

    /// <summary>
    /// TÃ i khoáº£n nÃ y cÃ³ quyá»n Ä‘Æ°á»£c gÃ¡n riÃªng hay khÃ´ng.
    /// Náº¿u cá»™t phan_quyen_rieng Ä‘Ã£ tá»“n táº¡i thÃ¬ dÃ¹ng cá»™t Ä‘Ã³ (xá»­ lÃ½ Ä‘Æ°á»£c cáº£ trÆ°á»ng há»£p
    /// quáº£n trá»‹ bá» chá»n táº¥t cáº£). Náº¿u chÆ°a cháº¡y migration thÃ¬ lÃ¹i vá» cÃ¡ch Ä‘áº¿m dÃ²ng
    /// trong nguoi_dung_quyen.
    /// </summary>
    private async Task<bool> CoQuyenGanRiengAsync(uint userId)
    {
        await using var connection = await OpenAsync();
        var hasColumn = await connection.ExecuteScalarAsync<bool>("""
            SELECT COUNT(1) > 0 FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'nguoi_dung'
              AND column_name = 'phan_quyen_rieng'
        """);

        if (hasColumn)
        {
            return await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(1) > 0 FROM nguoi_dung WHERE id = @UserId AND phan_quyen_rieng = 1",
                new { UserId = userId });
        }

        return await connection.ExecuteScalarAsync<bool>(
            "SELECT COUNT(1) > 0 FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId",
            new { UserId = userId });
    }

    public async Task<List<string>> GetUserPermissionKeysAsync(uint userId)
    {
        try
        {
            await using var connection = await OpenAsync();

            // ÄÃ£ cÃ³ quyá»n gÃ¡n riÃªng cho tÃ i khoáº£n => dÃ¹ng Ä‘Ãºng danh sÃ¡ch nÃ y,
            // khÃ´ng cá»™ng thÃªm quyá»n káº¿ thá»«a tá»« vai trÃ².
            var hasExplicit = await CoQuyenGanRiengAsync(userId);

            var sql = hasExplicit
                ? """
                SELECT DISTINCT q.ma_quyen
                FROM quyen q
                JOIN nguoi_dung_quyen ndq ON ndq.quyen_id = q.id
                WHERE ndq.nguoi_dung_id = @UserId AND ndq.cho_phep = 1
                """
                : """
                SELECT DISTINCT q.ma_quyen
                FROM quyen q
                WHERE EXISTS (
                  SELECT 1 FROM vai_tro_quyen vtq 
                  JOIN nguoi_dung_vai_tro ndvt ON ndvt.vai_tro_id = vtq.vai_tro_id 
                  WHERE vtq.quyen_id = q.id AND ndvt.nguoi_dung_id = @UserId
                )
                """;

            var res = await connection.QueryAsync<string>(sql, new { UserId = userId });
            return res.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync()
    {
        await using var connection = await OpenAsync();
        var sql = "SELECT id AS Id, ma_vai_tro AS MaVaiTro, ten_vai_tro AS TenVaiTro, mo_ta AS MoTa, trang_thai AS TrangThai FROM vai_tro WHERE trang_thai = 1 ORDER BY id ASC";
        var items = await connection.QueryAsync<Role>(sql);
        return items.ToList();
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync()
    {
        try
        {
            await using var connection = await OpenAsync();
            var sql = "SELECT id AS Id, ma_quyen AS MaQuyen, ten_quyen AS TenQuyen, nhom AS Nhom, loai AS Loai, mo_ta AS MoTa, thu_tu AS ThuTu, trang_thai AS TrangThai FROM quyen WHERE trang_thai = 1 ORDER BY nhom, thu_tu, id ASC";
            var items = await connection.QueryAsync<Permission>(sql);
            return items.ToList();
        }
        catch { return new List<Permission>(); }
    }

    private static bool _daDamBaoDanhMucQuyen;

    /// <summary>
    /// Báº£o Ä‘áº£m báº£ng <c>quyen</c> cÃ³ Ä‘á»§ má»i mÃ£ quyá»n trong <see cref="PermissionCatalog"/>.
    /// Nhá» váº­y cÃ¢y phÃ¢n quyá»n luÃ´n cÃ³ Id &gt; 0 vÃ  khÃ´ng bá»‹ máº¥t quyá»n khi lÆ°u.
    /// Cháº¡y idempotent, an toÃ n khi gá»i láº¡i nhiá»u láº§n.
    /// </summary>
    public async Task EnsurePermissionCatalogAsync()
    {
        if (_daDamBaoDanhMucQuyen)
        {
            return;
        }

        try
        {
            await using var connection = await OpenAsync();

            // Báº£o Ä‘áº£m cÃ¡c báº£ng phÃ¢n quyá»n tá»“n táº¡i (database cÅ© chÆ°a cháº¡y migration).
            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS `quyen` (
              `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
              `ma_quyen` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL,
              `ten_quyen` VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL,
              `nhom` VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL,
              `loai` VARCHAR(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `mo_ta` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `thu_tu` INT(10) UNSIGNED DEFAULT 0,
              `trang_thai` TINYINT(1) NOT NULL DEFAULT 1,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uk_quyen_ma_quyen` (`ma_quyen`),
              KEY `idx_quyen_nhom` (`nhom`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """);

            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS `vai_tro_quyen` (
              `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
              `vai_tro_id` INT(10) UNSIGNED NOT NULL,
              `quyen_id` INT(10) UNSIGNED NOT NULL,
              `nguoi_cap_nhat_id` INT(10) UNSIGNED DEFAULT NULL,
              `ngay_cap_nhat` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uk_vai_tro_quyen` (`vai_tro_id`, `quyen_id`),
              KEY `fk_vtq_quyen` (`quyen_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """);

            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS `nguoi_dung_quyen` (
              `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
              `nguoi_dung_id` INT(10) UNSIGNED NOT NULL,
              `quyen_id` INT(10) UNSIGNED NOT NULL,
              `cho_phep` TINYINT(1) NOT NULL DEFAULT 1,
              `nguoi_cap_nhat_id` INT(10) UNSIGNED DEFAULT NULL,
              `ngay_cap_nhat` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uk_nguoi_dung_quyen` (`nguoi_dung_id`, `quyen_id`),
              KEY `fk_ndq_quyen` (`quyen_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """);

            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS `nhat_ky_hoat_dong` (
              `id` BIGINT(20) UNSIGNED NOT NULL AUTO_INCREMENT,
              `nguoi_dung_id` INT(10) UNSIGNED DEFAULT NULL,
              `ten_dang_nhap` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `ho_ten` VARCHAR(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `vai_tro` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `hanh_dong` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL,
              `doi_tuong` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `doi_tuong_id` INT(10) UNSIGNED DEFAULT NULL,
              `ma_chung_tu` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `noi_dung` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `gia_tri` DECIMAL(15,2) DEFAULT NULL,
              `dia_chi_ip` VARCHAR(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `thoi_gian` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
              PRIMARY KEY (`id`),
              KEY `idx_nhat_ky_nguoi_dung` (`nguoi_dung_id`),
              KEY `idx_nhat_ky_thoi_gian` (`thoi_gian`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """);

            try
            {
                var existingCols = (await connection.QueryAsync<string>(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nhat_ky_hoat_dong';")).ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!existingCols.Contains("vai_tro"))
                    await connection.ExecuteAsync("ALTER TABLE nhat_ky_hoat_dong ADD COLUMN vai_tro VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER ho_ten;");
                if (!existingCols.Contains("ma_chung_tu"))
                    await connection.ExecuteAsync("ALTER TABLE nhat_ky_hoat_dong ADD COLUMN ma_chung_tu VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER doi_tuong_id;");
                if (!existingCols.Contains("gia_tri"))
                    await connection.ExecuteAsync("ALTER TABLE nhat_ky_hoat_dong ADD COLUMN gia_tri DECIMAL(15,2) DEFAULT NULL AFTER noi_dung;");
            }
            catch { }

            var sql = """
            INSERT INTO quyen (ma_quyen, ten_quyen, nhom, loai, thu_tu, trang_thai)
            VALUES (@MaQuyen, @TenQuyen, @Nhom, @Loai, @ThuTu, 1)
            ON DUPLICATE KEY UPDATE
                ten_quyen = VALUES(ten_quyen),
                nhom = VALUES(nhom),
                loai = VALUES(loai),
                thu_tu = VALUES(thu_tu),
                trang_thai = 1
            """;

            foreach (var group in PermissionCatalog.Groups)
            {
                for (var i = 0; i < group.Keys.Length; i++)
                {
                    var key = group.Keys[i];
                    await connection.ExecuteAsync(sql, new
                    {
                        MaQuyen = key,
                        TenQuyen = PermissionCatalog.GetTenQuyen(key),
                        Nhom = group.Nhom,
                        Loai = PermissionCatalog.GetLoai(key),
                        ThuTu = i + 1
                    });
                }
            }

            // Báº£o Ä‘áº£m cá»™t phan_quyen_rieng tá»“n táº¡i trong báº£ng nguoi_dung
            var coColPhanQuyenRieng = await connection.ExecuteScalarAsync<bool>("""
                SELECT COUNT(1) > 0 FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = 'nguoi_dung'
                  AND column_name = 'phan_quyen_rieng'
            """);
            if (!coColPhanQuyenRieng)
            {
                try
                {
                    await connection.ExecuteAsync("ALTER TABLE nguoi_dung ADD COLUMN `phan_quyen_rieng` TINYINT(1) NOT NULL DEFAULT 0");
                }
                catch { }
            }

            // Dá»n dáº¹p tÃ i khoáº£n kiá»ƒm thá»­ náº¿u cÃ³
            try
            {
                await connection.ExecuteAsync("DELETE ndvt, ndq, nd FROM nguoi_dung nd LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id LEFT JOIN nguoi_dung_quyen ndq ON ndq.nguoi_dung_id = nd.id WHERE nd.ten_dang_nhap = 'repro_test_x'");
            }
            catch { }

            // Äá»“ng bá»™ quyá»n máº·c Ä‘á»‹nh theo vai trÃ² vÃ o vai_tro_quyen cho táº¥t cáº£ vai trÃ²
            foreach (var kvp in PermissionCatalog.DefaultRolePermissions)
            {
                var roleId = await connection.ExecuteScalarAsync<uint?>(
                    "SELECT id FROM vai_tro WHERE ma_vai_tro = @Ma",
                    new { Ma = kvp.Key });

                if (roleId.HasValue)
                {
                    if (string.Equals(kvp.Key, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase))
                    {
                        // QUAN_TRI luÃ´n cÃ³ táº¥t cáº£ quyá»n
                        await connection.ExecuteAsync(
                            "INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id) SELECT @RoleId, q.id FROM quyen q",
                            new { RoleId = roleId.Value });
                    }
                    else
                    {
                        var sqlVtq = """
                            INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
                            SELECT @RoleId, q.id FROM quyen q WHERE q.ma_quyen = @MaQuyen
                        """;
                        foreach (var k in kvp.Value)
                        {
                            await connection.ExecuteAsync(sqlVtq, new { RoleId = roleId.Value, MaQuyen = k });
                        }
                    }
                }
            }

            _daDamBaoDanhMucQuyen = true;
        }
        catch (Exception ex)
        {
            // KhÃ´ng cháº·n Ä‘Äƒng nháº­p náº¿u danh má»¥c quyá»n chÆ°a Ä‘á»“ng bá»™ Ä‘Æ°á»£c.
            Helpers.SafeLog.WriteError("EnsurePermissionCatalogAsync", ex);
        }
    }

    public async Task<IReadOnlyList<uint>> GetRolePermissionIdsAsync(uint roleId)
    {
        await using var connection = await OpenAsync();
        var sql = "SELECT quyen_id FROM vai_tro_quyen WHERE vai_tro_id = @RoleId";
        var items = await connection.QueryAsync<uint>(sql, new { RoleId = roleId });
        return items.ToList();
    }

    public async Task<IReadOnlyList<UserPermissionDto>> GetUserPermissionsDetailAsync(uint userId)
    {
        await using var connection = await OpenAsync();
        var hasExplicit = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId",
            new { UserId = userId }) > 0;

        var sql = hasExplicit
            ? """
            SELECT q.id AS QuyenId, q.ma_quyen AS MaQuyen, q.ten_quyen AS TenQuyen, q.nhom AS Nhom, q.loai AS Loai, 1 AS ChoPhep
            FROM quyen q
            JOIN nguoi_dung_quyen ndq ON ndq.quyen_id = q.id
            WHERE ndq.nguoi_dung_id = @UserId AND ndq.cho_phep = 1
            ORDER BY q.nhom, q.thu_tu, q.id
            """
            : """
            SELECT q.id AS QuyenId, q.ma_quyen AS MaQuyen, q.ten_quyen AS TenQuyen, q.nhom AS Nhom, q.loai AS Loai, 1 AS ChoPhep
            FROM quyen q
            WHERE EXISTS (
              SELECT 1 FROM vai_tro_quyen vtq 
              JOIN nguoi_dung_vai_tro ndvt ON ndvt.vai_tro_id = vtq.vai_tro_id 
              WHERE vtq.quyen_id = q.id AND ndvt.nguoi_dung_id = @UserId
            )
            ORDER BY q.nhom, q.thu_tu, q.id
            """;
        var items = await connection.QueryAsync<UserPermissionDto>(sql, new { UserId = userId });
        return items.ToList();
    }

    /// <summary>TÃ i khoáº£n Ä‘ang cÃ³ Ã­t nháº¥t má»™t quyá»n Ä‘Æ°á»£c gÃ¡n riÃªng (khÃ´ng káº¿ thá»«a vai trÃ²).</summary>
    public async Task<bool> HasExplicitUserPermissionsAsync(uint userId)
    {
        await using var connection = await OpenAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId",
            new { UserId = userId }) > 0;
    }

    public async Task<bool> UserHasPermissionAsync(uint userId, string maQuyen)
    {
        await using var connection = await OpenAsync();
        var sql = """
        SELECT 1 FROM quyen q
        WHERE q.ma_quyen = @MaQuyen
        AND (EXISTS (
          SELECT 1 FROM vai_tro_quyen vtq 
          JOIN nguoi_dung_vai_tro ndvt ON ndvt.vai_tro_id = vtq.vai_tro_id 
          WHERE vtq.quyen_id = q.id AND ndvt.nguoi_dung_id = @UserId
        ) OR EXISTS (
          SELECT 1 FROM nguoi_dung_quyen ndq 
          WHERE ndq.quyen_id = q.id AND ndq.nguoi_dung_id = @UserId AND ndq.cho_phep = 1
        ))
        LIMIT 1
        """;
        var res = await connection.QueryFirstOrDefaultAsync<int?>(sql, new { UserId = userId, MaQuyen = maQuyen });
        return res.HasValue;
    }

    public async Task SaveUserRolesAsync(uint userId, IEnumerable<uint> roleIds, uint? actorId = null)
    {
        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("DELETE FROM nguoi_dung_vai_tro WHERE nguoi_dung_id = @UserId", new { UserId = userId }, tx);
            foreach (var rid in roleIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId)", new { UserId = userId, RoleId = rid }, tx);
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task SaveUserPermissionsAsync(uint userId, IEnumerable<uint> permissionIds, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission, "Báº¡n khÃ´ng cÃ³ quyá»n phÃ¢n quyá»n tÃ i khoáº£n.");
        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("DELETE FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId", new { UserId = userId }, tx);
            var safeActorId = actorId.HasValue && actorId.Value > 0 ? actorId.Value : (uint?)null;
            foreach (var pid in permissionIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO nguoi_dung_quyen (nguoi_dung_id, quyen_id, cho_phep, nguoi_cap_nhat_id) VALUES (@UserId, @QuyenId, 1, @ActorId)", new { UserId = userId, QuyenId = pid, ActorId = safeActorId }, tx);
            }
            try { await connection.ExecuteAsync("UPDATE nguoi_dung SET phan_quyen_rieng = 1 WHERE id = @UserId", new { UserId = userId }, tx); } catch { }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task SaveRolePermissionsAsync(uint roleId, IEnumerable<uint> permissionIds, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission, "Báº¡n khÃ´ng cÃ³ quyá»n phÃ¢n quyá»n vai trÃ².");
        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("DELETE FROM vai_tro_quyen WHERE vai_tro_id = @RoleId", new { RoleId = roleId }, tx);
            var safeActorId = actorId.HasValue && actorId.Value > 0 ? actorId.Value : (uint?)null;
            foreach (var pid in permissionIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id, nguoi_cap_nhat_id) VALUES (@RoleId, @QuyenId, @ActorId)", new { RoleId = roleId, QuyenId = pid, ActorId = safeActorId }, tx);
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<uint> CreateUserAsync(UserCreateDto dto, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersCreate, "Báº¡n khÃ´ng cÃ³ quyá»n táº¡o tÃ i khoáº£n.");
        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var maNguoiDung = await GenerateMaNguoiDungAsync(connection, tx);
            var hashed = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);
            var chiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1u;
            await EnsureNguoiDungContactColumnsAsync(connection);
            bool coSdt = _coCotSoDienThoaiNguoiDung == true;
            bool coEmail = _coCotEmailNguoiDung == true;
            var sql = (coSdt && coEmail)
                ? """
                INSERT INTO nguoi_dung (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten, so_dien_thoai, email, chi_nhanh_id, trang_thai, ngay_tao, ngay_cap_nhat)
                VALUES (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen, @SoDienThoai, @Email, @ChiNhanhId, @TrangThai, NOW(), NOW())
                """
                : """
                INSERT INTO nguoi_dung (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten, chi_nhanh_id, trang_thai, ngay_tao, ngay_cap_nhat)
                VALUES (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen, @ChiNhanhId, @TrangThai, NOW(), NOW())
                """;
            await connection.ExecuteAsync(sql, new
            {
                MaNguoiDung = maNguoiDung,
                TenDangNhap = dto.TenDangNhap,
                MatKhau = hashed,
                HoTen = dto.HoTen,
                SoDienThoai = dto.SoDienThoai,
                Email = dto.Email,
                ChiNhanhId = chiNhanhId,
                TrangThai = dto.TrangThai ? 1 : 0
            }, tx);
            var userId = await connection.ExecuteScalarAsync<uint>("SELECT LAST_INSERT_ID();", null, tx);
            foreach (var rid in dto.RoleIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId)", new { UserId = userId, RoleId = rid }, tx);
            }
            var safeActorId = actorId.HasValue && actorId.Value > 0 ? actorId.Value : (uint?)null;
            foreach (var pid in dto.PermissionIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO nguoi_dung_quyen (nguoi_dung_id, quyen_id, cho_phep, nguoi_cap_nhat_id) VALUES (@UserId, @QuyenId, 1, @ActorId)", new { UserId = userId, QuyenId = pid, ActorId = safeActorId }, tx);
            }
            try { await connection.ExecuteAsync("UPDATE nguoi_dung SET phan_quyen_rieng = 1 WHERE id = @UserId", new { UserId = userId }, tx); } catch { }
            await tx.CommitAsync();
            return userId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateUserAsync(UserEditDto dto, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersEdit, "Báº¡n khÃ´ng cÃ³ quyá»n sá»­a tÃ i khoáº£n.");

        // KhÃ´ng cho bá» vai trÃ² QUAN_TRI cá»§a chÃ­nh mÃ¬nh hoáº·c cá»§a tÃ i khoáº£n Admin khÃ¡c.
        if (dto.Id == Session.CurrentUser?.Id && await IsQuanTriAsync(dto.Id))
        {
            var vanConQuanTri = false;
            foreach (var rid in dto.RoleIds)
            {
                if (vanConQuanTri)
                {
                    break;
                }

                var ma = await GetRoleCodeAsync(rid);
                if (string.Equals(ma, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase))
                {
                    vanConQuanTri = true;
                }
            }

            if (!vanConQuanTri)
            {
                throw new InvalidOperationException("Báº¡n khÃ´ng thá»ƒ tá»± bá» vai trÃ² QUAN_TRI cá»§a chÃ­nh mÃ¬nh.");
            }
        }

        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await EnsureNguoiDungContactColumnsAsync(connection);
            bool coSdt = _coCotSoDienThoaiNguoiDung == true;
            bool coEmail = _coCotEmailNguoiDung == true;
            var contactCols = (coSdt && coEmail) ? ", so_dien_thoai = @SoDienThoai, email = @Email" : "";

            if (!string.IsNullOrWhiteSpace(dto.MatKhau))
            {
                var hashed = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);
                await connection.ExecuteAsync($"UPDATE nguoi_dung SET ho_ten = @HoTen {contactCols}, mat_khau = @MatKhau, trang_thai = @TrangThai, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = dto.Id, HoTen = dto.HoTen, SoDienThoai = dto.SoDienThoai, Email = dto.Email, MatKhau = hashed, TrangThai = dto.TrangThai ? 1 : 0 }, tx);
            }
            else
            {
                await connection.ExecuteAsync($"UPDATE nguoi_dung SET ho_ten = @HoTen {contactCols}, trang_thai = @TrangThai, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = dto.Id, HoTen = dto.HoTen, SoDienThoai = dto.SoDienThoai, Email = dto.Email, TrangThai = dto.TrangThai ? 1 : 0 }, tx);
            }
            await connection.ExecuteAsync("DELETE FROM nguoi_dung_vai_tro WHERE nguoi_dung_id = @UserId", new { UserId = dto.Id }, tx);
            foreach (var rid in dto.RoleIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO nguoi_dung_vai_tro (nguoi_dung_id, vai_tro_id) VALUES (@UserId, @RoleId)", new { UserId = dto.Id, RoleId = rid }, tx);
            }
            await connection.ExecuteAsync("DELETE FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId", new { UserId = dto.Id }, tx);
            var safeActorId = actorId.HasValue && actorId.Value > 0 ? actorId.Value : (uint?)null;
            foreach (var pid in dto.PermissionIds.Distinct())
            {
                await connection.ExecuteAsync("INSERT IGNORE INTO nguoi_dung_quyen (nguoi_dung_id, quyen_id, cho_phep, nguoi_cap_nhat_id) VALUES (@UserId, @QuyenId, 1, @ActorId)", new { UserId = dto.Id, QuyenId = pid, ActorId = safeActorId }, tx);
            }
            try { await connection.ExecuteAsync("UPDATE nguoi_dung SET phan_quyen_rieng = 1 WHERE id = @UserId", new { UserId = dto.Id }, tx); } catch { }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<string?> GetRoleCodeAsync(uint roleId)
    {
        await using var connection = await OpenAsync();
        return await connection.ExecuteScalarAsync<string?>(
            "SELECT ma_vai_tro FROM vai_tro WHERE id = @Id", new { Id = roleId });
    }

    /// <summary>TÃ i khoáº£n cÃ³ vai trÃ² QUAN_TRI hay khÃ´ng.</summary>
    public async Task<bool> IsQuanTriAsync(uint userId)
    {
        await using var connection = await OpenAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM nguoi_dung_vai_tro ndvt
            JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
            WHERE ndvt.nguoi_dung_id = @UserId AND vt.ma_vai_tro = @MaVaiTro
            """,
            new { UserId = userId, MaVaiTro = Quyen.QuanTri });
        return count > 0;
    }

    private async Task BaoVeQuanTriAsync(uint userId, string hanhDong)
    {
        if (userId == 0 || userId == Session.CurrentUser?.Id)
        {
            return;
        }

        if (await IsQuanTriAsync(userId))
        {
            throw new InvalidOperationException($"KhÃ´ng thá»ƒ {hanhDong} tÃ i khoáº£n cÃ³ vai trÃ² QUAN_TRI.");
        }
    }

    public async Task LockUserAsync(uint userId, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersLock, "Báº¡n khÃ´ng cÃ³ quyá»n khÃ³a tÃ i khoáº£n.");
        await BaoVeQuanTriAsync(userId, "khÃ³a");
        await using var connection = await OpenAsync();
        await connection.ExecuteAsync("UPDATE nguoi_dung SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = userId });
    }

    public async Task UnlockUserAsync(uint userId, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersUnlock, "Báº¡n khÃ´ng cÃ³ quyá»n má»Ÿ khÃ³a tÃ i khoáº£n.");
        await using var connection = await OpenAsync();
        await connection.ExecuteAsync("UPDATE nguoi_dung SET trang_thai = 1, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = userId });
    }

    public async Task DeleteUserAsync(uint userId, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersDelete, "Báº¡n khÃ´ng cÃ³ quyá»n xÃ³a tÃ i khoáº£n.");
        await using var connection = await OpenAsync();
        // KhÃ´ng xÃ³a admin (id=1 thÆ°á»ng) - phÃ²ng ngá»«a
        if (userId == 1) throw new InvalidOperationException("KhÃ´ng thá»ƒ xÃ³a tÃ i khoáº£n Admin máº·c Ä‘á»‹nh.");
        await BaoVeQuanTriAsync(userId, "xÃ³a");
        await connection.ExecuteAsync("DELETE FROM nguoi_dung_vai_tro WHERE nguoi_dung_id = @UserId", new { UserId = userId });
        await connection.ExecuteAsync("DELETE FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId", new { UserId = userId });
        await connection.ExecuteAsync("DELETE FROM nguoi_dung WHERE id = @Id", new { Id = userId });
    }

    public async Task ResetPasswordAsync(uint userId, string newPassword, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersResetPassword, "Báº¡n khÃ´ng cÃ³ quyá»n Ä‘áº·t láº¡i máº­t kháº©u.");
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ArgumentException("Máº­t kháº©u khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");
        await using var connection = await OpenAsync();
        var hashed = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await connection.ExecuteAsync("UPDATE nguoi_dung SET mat_khau = @MatKhau, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = userId, MatKhau = hashed });
    }

    private async Task<string> GenerateMaNguoiDungAsync(MySqlConnector.MySqlConnection conn, MySqlConnector.MySqlTransaction? tx = null)
    {
        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_nguoi_dung, 3) AS UNSIGNED)),
            0
        ) + 1
        FROM nguoi_dung
        WHERE ma_nguoi_dung REGEXP '^ND[0-9]{1,8}$';
        """;

        var nextNum = await conn.ExecuteScalarAsync<long>(sql, transaction: tx);
        if (nextNum <= 1)
        {
            nextNum = 2;
        }

        var code = $"ND{nextNum:D6}";
        while (await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM nguoi_dung WHERE ma_nguoi_dung = @Code",
            new { Code = code },
            tx) > 0)
        {
            nextNum++;
            code = $"ND{nextNum:D6}";
        }
        return code;
    }

    private static bool _daDamBaoBangNhatKy;

    public async Task EnsureAuditTableAsync()
    {
        if (_daDamBaoBangNhatKy) return;
        try
        {
            await using var connection = await OpenAsync();
            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS `nhat_ky_hoat_dong` (
              `id` BIGINT(20) UNSIGNED NOT NULL AUTO_INCREMENT,
              `nguoi_dung_id` INT(10) UNSIGNED DEFAULT NULL,
              `ten_dang_nhap` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `ho_ten` VARCHAR(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `vai_tro` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `hanh_dong` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL,
              `doi_tuong` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `doi_tuong_id` INT(10) UNSIGNED DEFAULT NULL,
              `ma_chung_tu` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `noi_dung` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `gia_tri` DECIMAL(15,2) DEFAULT NULL,
              `dia_chi_ip` VARCHAR(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `thoi_gian` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
              PRIMARY KEY (`id`),
              KEY `idx_nhat_ky_nguoi_dung` (`nguoi_dung_id`),
              KEY `idx_nhat_ky_thoi_gian` (`thoi_gian`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);

            try
            {
                var existingCols = (await connection.QueryAsync<string>(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'nhat_ky_hoat_dong';")).ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!existingCols.Contains("vai_tro"))
                    await connection.ExecuteAsync("ALTER TABLE nhat_ky_hoat_dong ADD COLUMN vai_tro VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER ho_ten;");
                if (!existingCols.Contains("ma_chung_tu"))
                    await connection.ExecuteAsync("ALTER TABLE nhat_ky_hoat_dong ADD COLUMN ma_chung_tu VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER doi_tuong_id;");
                if (!existingCols.Contains("gia_tri"))
                    await connection.ExecuteAsync("ALTER TABLE nhat_ky_hoat_dong ADD COLUMN gia_tri DECIMAL(15,2) DEFAULT NULL AFTER noi_dung;");
            }
            catch { }

            _daDamBaoBangNhatKy = true;
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("EnsureAuditTableAsync error", ex);
        }
    }

    public async Task LogAuditAsync(AuditLog log)
    {
        try
        {
            await EnsureAuditTableAsync();
            await using var connection = await OpenAsync();
            var sql = """
                INSERT INTO nhat_ky_hoat_dong 
                (nguoi_dung_id, ten_dang_nhap, ho_ten, vai_tro, hanh_dong, doi_tuong, doi_tuong_id, ma_chung_tu, noi_dung, gia_tri, dia_chi_ip, thoi_gian) 
                VALUES 
                (@NguoiDungId, @TenDangNhap, @HoTen, @VaiTro, @HanhDong, @DoiTuong, @DoiTuongId, @MaChungTu, @NoiDung, @GiaTri, @DiaChiIp, NOW());
            """;
            await connection.ExecuteAsync(sql, log);
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("LogAuditAsync error", ex);
        }
    }

    public async Task LogSubAccountActivityAsync(
        string hanhDong,
        string? doiTuong = null,
        uint? doiTuongId = null,
        string? maChungTu = null,
        string? noiDung = null,
        decimal? giaTri = null)
    {
        try
        {
            var user = Session.CurrentUser;
            if (user == null)
                return;

            var log = new AuditLog
            {
                NguoiDungId = user.Id,
                TenDangNhap = user.TenDangNhap,
                HoTen = user.HoTen,
                VaiTro = user.VaiTro ?? user.MaVaiTro ?? "",
                HanhDong = hanhDong,
                DoiTuong = doiTuong,
                DoiTuongId = doiTuongId,
                MaChungTu = maChungTu,
                NoiDung = noiDung,
                GiaTri = giaTri,
                ThoiGian = DateTime.Now
            };

            await LogAuditAsync(log);
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("LogSubAccountActivityAsync error", ex);
        }
    }
    #endregion

    #region Chi Tiáº¿t GiÃ¡ Äá»‘i TÃ¡c
    public async Task<IReadOnlyList<ChiTietGiaDoiTac>> GetChiTietGiaDoiTacAsync(uint doiTacId)
    {
        await using var connection = await OpenAsync();
        try
        {
            const string sql = """
            SELECT 
                ct.id AS Id,
                ct.doi_tac_id AS DoiTacId,
                ct.hang_hoa_id AS HangHoaId,
                hh.ma_hang_hoa AS MaHangHoa,
                hh.ten_hang_hoa AS TenHangHoa,
                ct.don_vi_tinh_id AS DonViTinhId,
                COALESCE(dvt.ten_don_vi_tinh, '') AS DonViTinh,
                ct.chiet_khau AS ChietKhau,
                ct.don_gia AS DonGia
            FROM chi_tiet_gia_doi_tac ct
            LEFT JOIN hang_hoa hh ON hh.id = ct.hang_hoa_id
            LEFT JOIN don_vi_tinh_hang_hoa dvt ON dvt.id = ct.don_vi_tinh_id
            WHERE ct.doi_tac_id = @DoiTacId;
            """;
            return (await connection.QueryAsync<ChiTietGiaDoiTac>(sql, new { DoiTacId = doiTacId })).AsList();
        }
        catch
        {
            return Array.Empty<ChiTietGiaDoiTac>();
        }
    }

    public async Task LuuChiTietGiaDoiTacAsync(uint doiTacId, IEnumerable<ChiTietGiaDoiTac> items)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("DELETE FROM chi_tiet_gia_doi_tac WHERE doi_tac_id = @DoiTacId;", new { DoiTacId = doiTacId }, transaction);
            const string insertSql = """
            INSERT INTO chi_tiet_gia_doi_tac (doi_tac_id, hang_hoa_id, don_vi_tinh_id, chiet_khau, don_gia)
            VALUES (@DoiTacId, @HangHoaId, @DonViTinhId, @ChietKhau, @DonGia);
            """;
            foreach (var item in items)
            {
                await connection.ExecuteAsync(insertSql, new
                {
                    DoiTacId = doiTacId,
                    item.HangHoaId,
                    item.DonViTinhId,
                    item.ChietKhau,
                    item.DonGia
                }, transaction);
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Loi luu chi tiet gia doi tac", ex);
            throw;
        }
    }
    #endregion

    #region HÃ ng Tá»“n Äáº§u Ká»³
    /// <summary>
    /// Láº¥y danh sÃ¡ch kho hÃ ng / chi nhÃ¡nh hiá»‡n cÃ³ tá»« cÆ¡ sá»Ÿ dá»¯ liá»‡u.
    /// TÆ°Æ¡ng thÃ­ch cáº£ báº£ng kho_hang (SakePro) vÃ  chi_nhanh (ShopHoa).
    /// </summary>
    public async Task<IReadOnlyList<ProductOption>> GetDanhSachKhoAsync()
    {
        await using var connection = await OpenAsync();
        try
        {
            // 1. Thá»­ láº¥y tá»« báº£ng kho_hang (SakePro schema)
            bool hasKhoHang = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'kho_hang';") > 0;
            if (hasKhoHang)
            {
                var list = (await connection.QueryAsync<ProductOption>(
                    "SELECT id AS Id, ten_kho_hang AS Name FROM kho_hang ORDER BY id;")).AsList();
                if (list.Count > 0)
                    return list;
            }
        }
        catch { }

        try
        {
            // 2. Thá»­ láº¥y tá»« báº£ng chi_nhanh
            bool hasChiNhanh = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_nhanh';") > 0;
            if (hasChiNhanh)
            {
                var list = (await connection.QueryAsync<ProductOption>(
                    "SELECT id AS Id, ten_chi_nhanh AS Name FROM chi_nhanh WHERE trang_thai = 1 ORDER BY id;")).AsList();
                if (list.Count > 0)
                    return list;
            }
        }
        catch { }

        // Máº·c Ä‘á»‹nh fallback
        return new List<ProductOption>
        {
            new() { Id = 1, Name = "Kho Tá»•ng (Máº·c Ä‘á»‹nh)" }
        };
    }

    /// <summary>
    /// Láº¥y thÃ´ng tin tá»“n Ä‘áº§u ká»³ Ä‘Ã£ lÆ°u trÆ°á»›c Ä‘Ã³ cá»§a sáº£n pháº©m theo kho vÃ  ngÃ y (náº¿u cÃ³).
    /// </summary>
    public async Task<ChiTietHangHoaDauKy?> LayHangTonDauKyCuaSanPhamAsync(uint khoHangId, uint sanPhamId, DateTime ngayDauKy)
    {
        await using var connection = await OpenAsync();
        try
        {
            bool hasChiTietDauKy = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hang_hoa_dau_ky';") > 0;

            if (hasChiTietDauKy)
            {
                const string sql = """
                SELECT 
                    ct.id AS Id,
                    ct.kho_hang_id AS KhoHangId,
                    ct.hang_hoa_id AS HangHoaId,
                    ct.ky_ke_toan_id AS KyKeToanId,
                    ct.so_luong AS SoLuong,
                    ct.gia_von AS GiaVon,
                    ct.thanh_tien AS ThanhTien,
                    ct.noi_dung AS NoiDung
                FROM chi_tiet_hang_hoa_dau_ky ct
                WHERE ct.kho_hang_id = @KhoHangId AND ct.hang_hoa_id = @HangHoaId
                ORDER BY ct.id DESC
                LIMIT 1;
                """;

                return await connection.QueryFirstOrDefaultAsync<ChiTietHangHoaDauKy>(
                    sql, new { KhoHangId = khoHangId, HangHoaId = sanPhamId });
            }
            else
            {
                // Fallback tá»« báº£ng ton_kho
                bool hasTonKho = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ton_kho';") > 0;

                if (hasTonKho)
                {
                    const string sqlTk = """
                    SELECT 
                        san_pham_id AS HangHoaId,
                        chi_nhanh_id AS KhoHangId,
                        so_luong_ton AS SoLuong,
                        gia_von_binh_quan AS GiaVon,
                        (so_luong_ton * gia_von_binh_quan) AS ThanhTien,
                        'Tá»“n Ä‘áº§u ká»³' AS NoiDung
                    FROM ton_kho
                    WHERE chi_nhanh_id = @KhoHangId AND san_pham_id = @SanPhamId
                    LIMIT 1;
                    """;

                    return await connection.QueryFirstOrDefaultAsync<ChiTietHangHoaDauKy>(
                        sqlTk, new { KhoHangId = khoHangId, SanPhamId = sanPhamId });
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i LayHangTonDauKyCuaSanPhamAsync", ex);
        }

        return null;
    }

    /// <summary>
    /// LÆ°u thÃ´ng tin tá»“n Ä‘áº§u ká»³ cá»§a sáº£n pháº©m vÃ o há»‡ thá»‘ng.
    /// Tá»± Ä‘á»™ng cáº­p nháº­t báº£ng chi_tiet_hang_hoa_dau_ky, ton_kho, san_pham
    /// vÃ  ghi nháº­t kÃ½ lá»‹ch sá»­ kho mÃ  khÃ´ng gÃ¢y cá»™ng dá»“n 2 láº§n khi thao tÃ¡c láº¡i.
    /// </summary>
    public async Task<bool> LuuHangTonDauKyAsync(
        uint khoHangId,
        uint sanPhamId,
        decimal soLuong,
        decimal giaVon,
        DateTime ngayDauKy,
        string? noiDung)
    {
        if (khoHangId == 0)
            throw new ArgumentException("Kho hÃ ng khÃ´ng há»£p lá»‡.", nameof(khoHangId));

        if (sanPhamId == 0)
            throw new ArgumentException("HÃ ng hÃ³a chÆ°a Ä‘Æ°á»£c chá»n.", nameof(sanPhamId));

        if (soLuong < 0)
            throw new ArgumentException("Sá»‘ lÆ°á»£ng khÃ´ng Ä‘Æ°á»£c Ã¢m.", nameof(soLuong));

        if (giaVon < 0)
            throw new ArgumentException("GiÃ¡ vá»‘n khÃ´ng Ä‘Æ°á»£c Ã¢m.", nameof(giaVon));

        noiDung = string.IsNullOrWhiteSpace(noiDung) ? "Tá»“n Ä‘áº§u ká»³" : noiDung.Trim();
        decimal thanhTien = Math.Round(soLuong * giaVon, 2, MidpointRounding.AwayFromZero);

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Quáº£n lÃ½ ká»³ káº¿ toÃ¡n (náº¿u database cÃ³ báº£ng ky_ke_toan)
            uint kyKeToanId = 1;
            bool hasKyKeToan = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ky_ke_toan';",
                transaction: transaction) > 0;

            if (hasKyKeToan)
            {
                var maKy = $"KY_{ngayDauKy:MM_yyyy}";
                var existingKyId = await connection.ExecuteScalarAsync<uint?>(
                    "SELECT id FROM ky_ke_toan WHERE ma_ky = @MaKy LIMIT 1;",
                    new { MaKy = maKy },
                    transaction: transaction);

                if (existingKyId.HasValue && existingKyId.Value > 0)
                {
                    kyKeToanId = existingKyId.Value;
                }
                else
                {
                    var dauThang = new DateTime(ngayDauKy.Year, ngayDauKy.Month, 1);
                    var cuoiThang = dauThang.AddMonths(1).AddDays(-1);
                    var tenKy = $"ThÃ¡ng {ngayDauKy.Month} NÄƒm {ngayDauKy.Year}";
                    const string insertKySql = """
                        INSERT INTO ky_ke_toan (ma_ky, ten_ky, ngay_bat_dau_ky, ngay_ket_thuc_ky, trang_thai_khoa_so)
                        VALUES (@MaKy, @TenKy, @BatDau, @KetThuc, 0);
                        SELECT LAST_INSERT_ID();
                        """;
                    kyKeToanId = await connection.ExecuteScalarAsync<uint>(insertKySql, new
                    {
                        MaKy = maKy,
                        TenKy = tenKy,
                        BatDau = dauThang,
                        KetThuc = cuoiThang
                    }, transaction: transaction);
                }
            }

            // 2. LÆ°u vÃ o báº£ng chi_tiet_hang_hoa_dau_ky (schema SakePro) náº¿u cÃ³
            bool hasChiTietDauKy = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hang_hoa_dau_ky';",
                transaction: transaction) > 0;

            if (hasChiTietDauKy)
            {
                // Kiá»ƒm tra trÃ¹ng theo quy táº¯c: cÃ¹ng kho, hÃ ng, ká»³ káº¿ toÃ¡n
                var existingId = await connection.ExecuteScalarAsync<uint?>(
                    """
                    SELECT id FROM chi_tiet_hang_hoa_dau_ky
                    WHERE kho_hang_id = @KhoHangId AND hang_hoa_id = @HangHoaId AND ky_ke_toan_id = @KyKeToanId
                    LIMIT 1;
                    """,
                    new { KhoHangId = khoHangId, HangHoaId = sanPhamId, KyKeToanId = kyKeToanId },
                    transaction: transaction);

                if (existingId.HasValue && existingId.Value > 0)
                {
                    // Cáº­p nháº­t láº¡i báº£n ghi cÅ©, khÃ´ng cá»™ng dá»“n hai láº§n
                    await connection.ExecuteAsync(
                        """
                        UPDATE chi_tiet_hang_hoa_dau_ky
                        SET so_luong = @SoLuong,
                            gia_von = @GiaVon,
                            thanh_tien = @ThanhTien,
                            noi_dung = @NoiDung
                        WHERE id = @Id;
                        """,
                        new
                        {
                            Id = existingId.Value,
                            SoLuong = soLuong,
                            GiaVon = giaVon,
                            ThanhTien = thanhTien,
                            NoiDung = noiDung
                        },
                        transaction: transaction);
                }
                else
                {
                    // ThÃªm báº£n ghi má»›i
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO chi_tiet_hang_hoa_dau_ky
                        (kho_hang_id, hang_hoa_id, ky_ke_toan_id, so_luong, gia_von, thanh_tien, noi_dung)
                        VALUES
                        (@KhoHangId, @HangHoaId, @KyKeToanId, @SoLuong, @GiaVon, @ThanhTien, @NoiDung);
                        """,
                        new
                        {
                            KhoHangId = khoHangId,
                            HangHoaId = sanPhamId,
                            KyKeToanId = kyKeToanId,
                            SoLuong = soLuong,
                            GiaVon = giaVon,
                            ThanhTien = thanhTien,
                            NoiDung = noiDung
                        },
                        transaction: transaction);
                }
            }

            // 3. Cáº­p nháº­t tá»“n kho vÃ o báº£ng ton_kho
            bool hasTonKho = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ton_kho';",
                transaction: transaction) > 0;

            decimal tonTruoc = 0;
            if (hasTonKho)
            {
                var tonRow = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT so_luong_ton FROM ton_kho WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId FOR UPDATE;",
                    new { ChiNhanhId = khoHangId, SanPhamId = sanPhamId },
                    transaction: transaction);

                if (tonRow != null)
                {
                    tonTruoc = ToDecimal(tonRow.so_luong_ton);
                }

                // Ghi Ä‘Ã¨ sá»‘ lÆ°á»£ng tá»“n Ä‘áº§u ká»³ (trÃ¡nh cá»™ng dá»“n 2 láº§n)
                await connection.ExecuteAsync(
                    """
                    INSERT INTO ton_kho (chi_nhanh_id, san_pham_id, so_luong_ton, gia_von_binh_quan, ngay_cap_nhat)
                    VALUES (@ChiNhanhId, @SanPhamId, @SoLuong, @GiaVon, NOW())
                    ON DUPLICATE KEY UPDATE
                        so_luong_ton = @SoLuong,
                        gia_von_binh_quan = @GiaVon,
                        ngay_cap_nhat = NOW();
                    """,
                    new
                    {
                        ChiNhanhId = khoHangId,
                        SanPhamId = sanPhamId,
                        SoLuong = soLuong,
                        GiaVon = giaVon
                    },
                    transaction: transaction);
            }

            // 4. Äá»“ng bá»™ vÃ o báº£ng san_pham
            bool hasSanPham = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'san_pham';",
                transaction: transaction) > 0;

            if (hasSanPham)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE san_pham
                    SET ton_kho_excel = @SoLuong,
                        gia_von = @GiaVon,
                        ngay_cap_nhat = NOW()
                    WHERE id = @SanPhamId;
                    """,
                    new
                    {
                        SanPhamId = sanPhamId,
                        SoLuong = soLuong,
                        GiaVon = giaVon
                    },
                    transaction: transaction);
            }

            // 5. Ghi nháº­t kÃ½ lá»‹ch sá»­ nháº­p xuáº¥t kho
            bool hasLichSu = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'lich_su_nhap_xuat_kho';",
                transaction: transaction) > 0;

            if (hasLichSu)
            {
                var maChungTu = $"TDK_{ngayDauKy:yyyyMMdd}_{sanPhamId}";
                await connection.ExecuteAsync(
                    """
                    INSERT INTO lich_su_nhap_xuat_kho
                    (
                        chi_nhanh_id,
                        san_pham_id,
                        loai_giao_dich,
                        loai_chung_tu,
                        ma_chung_tu,
                        so_luong,
                        ton_truoc,
                        ton_sau,
                        gia_von,
                        nguoi_thuc_hien_id,
                        thoi_gian,
                        ghi_chu
                    )
                    VALUES
                    (
                        @ChiNhanhId,
                        @SanPhamId,
                        'TON_DAU_KY',
                        'DAU_KY',
                        @MaChungTu,
                        @SoLuong,
                        @TonTruoc,
                        @TonSau,
                        @GiaVon,
                        @NguoiThucHienId,
                        NOW(),
                        @GhiChu
                    );
                    """,
                    new
                    {
                        ChiNhanhId = khoHangId,
                        SanPhamId = sanPhamId,
                        MaChungTu = maChungTu,
                        SoLuong = soLuong,
                        TonTruoc = tonTruoc,
                        TonSau = soLuong,
                        GiaVon = giaVon,
                        NguoiThucHienId = Session.CurrentUser?.Id,
                        GhiChu = noiDung
                    },
                    transaction: transaction);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "ThÃªm hÃ ng tá»“n Ä‘áº§u ká»³",
                "SAN_PHAM",
                sanPhamId,
                $"TDK_{ngayDauKy:yyyyMMdd}",
                $"Cáº­p nháº­t tá»“n Ä‘áº§u ká»³: SL = {soLuong:N0}, GiÃ¡ vá»‘n = {giaVon:N0}, KhoId = {khoHangId}. {noiDung}",
                thanhTien);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lá»—i LuuHangTonDauKyAsync", ex);
            throw;
        }
    }
    #endregion

    #region CÃ´ng Ná»£ Äáº§u Ká»³
    /// <summary>
    /// Äáº£m báº£o báº£ng chi_tiet_cong_no_dau_ky tá»“n táº¡i Ä‘á»ƒ lÆ°u váº¿t cáº¥u trÃºc dá»¯ liá»‡u Ä‘áº§u ká»³ Ä‘á»“ng bá»™.
    /// </summary>
    private async Task EnsureBangCongNoDauKyAsync(MySqlConnection connection, MySqlTransaction transaction)
    {
        try
        {
            const string createSql = """
            CREATE TABLE IF NOT EXISTS chi_tiet_cong_no_dau_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                loai_doi_tac VARCHAR(20) NOT NULL COMMENT 'KHACH_HANG hoáº·c NHA_CUNG_CAP',
                doi_tac_id INT UNSIGNED NOT NULL,
                ma_doi_tac VARCHAR(50) NULL,
                ten_doi_tac VARCHAR(255) NULL,
                so_dien_thoai VARCHAR(50) NULL,
                so_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                ngay_dau_ky DATE NOT NULL,
                ngay_hen_tra DATE NULL,
                noi_dung TEXT NULL,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                ky_ke_toan_id INT UNSIGNED NULL,
                nguoi_tao_id INT UNSIGNED NULL,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_doitac_dauky (loai_doi_tac, doi_tac_id, ngay_dau_ky)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createSql, transaction: transaction);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhÃ´ng táº¡o Ä‘Æ°á»£c báº£ng chi_tiet_cong_no_dau_ky", ex);
        }
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch Ä‘á»‘i tÃ¡c (KhÃ¡ch hÃ ng hoáº·c NhÃ  cung cáº¥p) dÃ¹ng cho Ã´ tÃ¬m kiáº¿m AutoCompleteBox.
    /// </summary>
    public async Task<IReadOnlyList<DoiTacOption>> GetDanhSachDoiTacCongNoAsync(string loaiDoiTac, string? search = null)
    {
        var result = new List<DoiTacOption>();
        loaiDoiTac = (loaiDoiTac ?? "KHACH_HANG").Trim().ToUpperInvariant();

        if (loaiDoiTac == "KHACH_HANG")
        {
            var khachHangs = await GetKhachHangAsync(search);
            foreach (var kh in khachHangs)
            {
                result.Add(new DoiTacOption
                {
                    Id = kh.Id,
                    MaDoiTac = kh.MaKhachHang,
                    TenDoiTac = kh.TenKhachHang,
                    SoDienThoai = kh.SoDienThoai,
                    LoaiDoiTac = "KHACH_HANG",
                    CongNoHienTai = kh.NoHienTai
                });
            }
        }
        else
        {
            var nhaCungCaps = await GetNhaCungCapAsync(search);
            foreach (var ncc in nhaCungCaps)
            {
                result.Add(new DoiTacOption
                {
                    Id = ncc.Id,
                    MaDoiTac = ncc.MaNhaCungCap,
                    TenDoiTac = ncc.TenNhaCungCap,
                    SoDienThoai = ncc.SoDienThoai,
                    LoaiDoiTac = "NHA_CUNG_CAP",
                    CongNoHienTai = ncc.ConNo
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Tra cá»©u cÃ´ng ná»£ Ä‘áº§u ká»³ Ä‘Ã£ khai bÃ¡o trÆ°á»›c Ä‘Ã³ cá»§a Ä‘á»‘i tÃ¡c (náº¿u cÃ³).
    /// </summary>
    public async Task<ChiTietCongNoDauKy?> LayCongNoDauKyCuaDoiTacAsync(string loaiDoiTac, uint doiTacId, DateTime ngayDauKy)
    {
        await using var connection = await OpenAsync();
        loaiDoiTac = (loaiDoiTac ?? "KHACH_HANG").Trim().ToUpperInvariant();

        try
        {
            // 1. Thá»­ láº¥y tá»« chi_tiet_cong_no_dau_ky
            bool hasBangDauKy = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_cong_no_dau_ky';") > 0;

            if (hasBangDauKy)
            {
                const string sql = """
                SELECT 
                    id AS Id,
                    loai_doi_tac AS LoaiDoiTac,
                    doi_tac_id AS DoiTacId,
                    ma_doi_tac AS MaDoiTac,
                    ten_doi_tac AS TenDoiTac,
                    so_dien_thoai AS SoDienThoai,
                    so_tien AS SoTien,
                    ngay_dau_ky AS NgayDauKy,
                    ngay_hen_tra AS NgayHenTra,
                    noi_dung AS NoiDung,
                    chi_nhanh_id AS ChiNhanhId,
                    ky_ke_toan_id AS KyKeToanId,
                    ngay_tao AS NgayTao
                FROM chi_tiet_cong_no_dau_ky
                WHERE loai_doi_tac = @LoaiDoiTac AND doi_tac_id = @DoiTacId
                ORDER BY id DESC
                LIMIT 1;
                """;

                var record = await connection.QueryFirstOrDefaultAsync<ChiTietCongNoDauKy>(
                    sql, new { LoaiDoiTac = loaiDoiTac, DoiTacId = doiTacId });

                if (record != null)
                    return record;
            }

            // 2. Fallback tá»« lá»‹ch sá»­ cÃ´ng ná»£
            if (loaiDoiTac == "KHACH_HANG")
            {
                const string sqlKh = """
                SELECT 
                    id AS Id,
                    'KHACH_HANG' AS LoaiDoiTac,
                    khach_hang_id AS DoiTacId,
                    ghi_no AS SoTien,
                    thoi_gian AS NgayDauKy,
                    ghi_chu AS NoiDung,
                    chi_nhanh_id AS ChiNhanhId
                FROM lich_su_cong_no_khach_hang
                WHERE khach_hang_id = @Id 
                  AND (loai_giao_dich = 'NO_DAU_KY' OR loai_chung_tu = 'DAU_KY')
                ORDER BY id DESC
                LIMIT 1;
                """;

                var row = await connection.QueryFirstOrDefaultAsync<ChiTietCongNoDauKy>(sqlKh, new { Id = doiTacId });
                if (row != null)
                {
                    var kh = await connection.QueryFirstOrDefaultAsync<KhachHang>(
                        "SELECT ma_khach_hang AS MaKhachHang, ten_khach_hang AS TenKhachHang, so_dien_thoai AS SoDienThoai FROM khach_hang WHERE id = @Id;",
                        new { Id = doiTacId });
                    if (kh != null)
                    {
                        row.MaDoiTac = kh.MaKhachHang;
                        row.TenDoiTac = kh.TenKhachHang;
                        row.SoDienThoai = kh.SoDienThoai;
                    }
                    return row;
                }
            }
            else
            {
                const string sqlNcc = """
                SELECT 
                    id AS Id,
                    'NHA_CUNG_CAP' AS LoaiDoiTac,
                    nha_cung_cap_id AS DoiTacId,
                    ghi_no AS SoTien,
                    thoi_gian AS NgayDauKy,
                    ghi_chu AS NoiDung,
                    chi_nhanh_id AS ChiNhanhId
                FROM lich_su_cong_no_nha_cung_cap
                WHERE nha_cung_cap_id = @Id 
                  AND (loai_giao_dich = 'NO_DAU_KY' OR loai_chung_tu = 'DAU_KY')
                ORDER BY id DESC
                LIMIT 1;
                """;

                var row = await connection.QueryFirstOrDefaultAsync<ChiTietCongNoDauKy>(sqlNcc, new { Id = doiTacId });
                if (row != null)
                {
                    var ncc = await connection.QueryFirstOrDefaultAsync<NhaCungCap>(
                        "SELECT ma_nha_cung_cap AS MaNhaCungCap, ten_nha_cung_cap AS TenNhaCungCap, so_dien_thoai AS SoDienThoai FROM nha_cung_cap WHERE id = @Id;",
                        new { Id = doiTacId });
                    if (ncc != null)
                    {
                        row.MaDoiTac = ncc.MaNhaCungCap;
                        row.TenDoiTac = ncc.TenNhaCungCap;
                        row.SoDienThoai = ncc.SoDienThoai;
                    }
                    return row;
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i LayCongNoDauKyCuaDoiTacAsync", ex);
        }

        return null;
    }

    /// <summary>
    /// LÆ°u thÃ´ng tin cÃ´ng ná»£ Ä‘áº§u ká»³ cá»§a Ä‘á»‘i tÃ¡c (KhÃ¡ch hÃ ng / NhÃ  cung cáº¥p).
    /// Äáº£m báº£o tÃ­nh nháº¥t quÃ¡n qua Transaction, tá»± cáº­p nháº­t sá»• cÃ¡i cÃ´ng ná»£ tÆ°Æ¡ng á»©ng,
    /// chá»‘ng cá»™ng dá»“n hai láº§n khi ngÆ°á»i dÃ¹ng thao tÃ¡c láº¡i, khÃ´ng táº¡o phiáº¿u thu chi hay hÃ³a Ä‘Æ¡n giáº£.
    /// </summary>
    public async Task<bool> LuuCongNoDauKyAsync(ChiTietCongNoDauKy item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (item.DoiTacId == 0)
            throw new ArgumentException("ChÆ°a chá»n Ä‘á»‘i tÃ¡c cÃ´ng ná»£.", nameof(item.DoiTacId));

        if (item.SoTien < 0)
            throw new ArgumentException("Sá»‘ tiá»n cÃ´ng ná»£ khÃ´ng Ä‘Æ°á»£c Ã¢m.", nameof(item.SoTien));

        var loaiDoiTac = (item.LoaiDoiTac ?? "KHACH_HANG").Trim().ToUpperInvariant();
        var branchId = item.ChiNhanhId > 0 ? item.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);
        var userId = Session.CurrentUser?.Id;
        var ngayDauKy = item.NgayDauKy.Date;

        var ghiChuDayDu = string.IsNullOrWhiteSpace(item.NoiDung) ? "Ná»£ Ä‘áº§u ká»³" : item.NoiDung.Trim();
        if (item.NgayHenTra.HasValue)
        {
            ghiChuDayDu += $" (Háº¡n tráº£: {item.NgayHenTra.Value:dd/MM/yyyy})";
        }

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await EnsureBangCongNoDauKyAsync(connection, transaction);

            if (loaiDoiTac == "KHACH_HANG")
            {
                // ==============================================================
                // 1. CÃ”NG Ná»¢ KHÃCH HÃ€NG (PHáº¢I THU KHÃCH HÃ€NG)
                // ==============================================================
                var maChungTu = $"CNDK_{item.MaDoiTac}";

                // Kiá»ƒm tra xem khÃ¡ch hÃ ng nÃ y Ä‘Ã£ cÃ³ dÃ²ng cÃ´ng ná»£ Ä‘áº§u ká»³ chÆ°a (FOR UPDATE Ä‘á»ƒ an toÃ n)
                const string checkExistingKhSql = """
                SELECT id, ghi_no, so_du 
                FROM lich_su_cong_no_khach_hang 
                WHERE khach_hang_id = @KhachHangId 
                  AND (loai_giao_dich = 'NO_DAU_KY' OR loai_chung_tu = 'DAU_KY' OR ma_chung_tu = @MaChungTu)
                LIMIT 1
                FOR UPDATE;
                """;

                var existingRow = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    checkExistingKhSql,
                    new { KhachHangId = item.DoiTacId, MaChungTu = maChungTu },
                    transaction: transaction);

                if (existingRow != null)
                {
                    uint existingId = (uint)existingRow.id;
                    decimal oldGhiNo = ToDecimal(existingRow.ghi_no);
                    decimal delta = item.SoTien - oldGhiNo;

                    // Cáº­p nháº­t láº¡i báº£n ghi Ä‘áº§u ká»³ Ä‘Ã£ cÃ³ (chá»‘ng cá»™ng dá»“n 2 láº§n)
                    await connection.ExecuteAsync(
                        """
                        UPDATE lich_su_cong_no_khach_hang
                        SET ghi_no = @SoTien,
                            so_du = so_du + @Delta,
                            thoi_gian = @ThoiGian,
                            nguoi_thuc_hien_id = @NguoiThucHienId,
                            ghi_chu = @GhiChu
                        WHERE id = @Id;
                        """,
                        new
                        {
                            Id = existingId,
                            SoTien = item.SoTien,
                            Delta = delta,
                            ThoiGian = ngayDauKy,
                            NguoiThucHienId = userId,
                            GhiChu = ghiChuDayDu
                        },
                        transaction: transaction);

                    // Náº¿u cÃ³ chÃªnh lá»‡ch delta vÃ  cÃ³ cÃ¡c dÃ²ng phÃ¡t sinh sau Ä‘Ã³, Ä‘iá»u chá»‰nh sá»‘ dÆ° an toÃ n
                    if (delta != 0)
                    {
                        await connection.ExecuteAsync(
                            """
                            UPDATE lich_su_cong_no_khach_hang
                            SET so_du = so_du + @Delta
                            WHERE khach_hang_id = @KhachHangId AND id > @ExistingId;
                            """,
                            new
                            {
                                Delta = delta,
                                KhachHangId = item.DoiTacId,
                                ExistingId = existingId
                            },
                            transaction: transaction);
                    }
                }
                else
                {
                    // Láº¥y sá»‘ dÆ° cÃ´ng ná»£ hiá»‡n cÃ³ trÆ°á»›c khi thÃªm
                    var currentDebt = await connection.ExecuteScalarAsync<decimal>(
                        """
                        SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                        FROM lich_su_cong_no_khach_hang
                        WHERE khach_hang_id = @KhachHangId
                        FOR UPDATE;
                        """,
                        new { KhachHangId = item.DoiTacId },
                        transaction: transaction);

                    var newDebtBalance = currentDebt + item.SoTien;

                    await connection.ExecuteAsync(
                        """
                        INSERT INTO lich_su_cong_no_khach_hang
                        (
                            khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                            ma_chung_tu, ghi_no, ghi_co, so_du,
                            thoi_gian, nguoi_thuc_hien_id, ghi_chu
                        )
                        VALUES
                        (
                            @KhachHangId, @ChiNhanhId, 'NO_DAU_KY', 'DAU_KY',
                            @MaChungTu, @GhiNo, 0, @SoDu,
                            @ThoiGian, @NguoiThucHienId, @GhiChu
                        );
                        """,
                        new
                        {
                            KhachHangId = item.DoiTacId,
                            ChiNhanhId = branchId,
                            MaChungTu = maChungTu,
                            GhiNo = item.SoTien,
                            SoDu = newDebtBalance,
                            ThoiGian = ngayDauKy,
                            NguoiThucHienId = userId,
                            GhiChu = ghiChuDayDu
                        },
                        transaction: transaction);
                }

                // LÆ°u Ã½: KHÃ”NG Ä‘á»“ng bá»™ cá»™t no_can_thu_hien_tai á»Ÿ Ä‘Ã¢y vÃ¬ cÃ´ng ná»£ Ä‘áº§u ká»³
                // Ä‘Ã£ Ä‘Æ°á»£c ghi nháº­n vÃ o sá»• cÃ¡i lich_su_cong_no_khach_hang. Náº¿u vá»«a ghi sá»• cÃ¡i
                // vá»«a gÃ¡n cá»™t ná»n táº£ng sáº½ khiáº¿n sá»‘ dÆ° bá»‹ cá»™ng hai láº§n á»Ÿ mÃ n hÃ¬nh cÃ´ng ná»£.
            }
            else
            {
                // ==============================================================
                // 2. CÃ”NG Ná»¢ NHÃ€ CUNG Cáº¤P (PHáº¢I TRáº¢ NHÃ€ CUNG Cáº¤P)
                // ==============================================================
                var maChungTu = $"CNDK_{item.MaDoiTac}";

                const string checkExistingNccSql = """
                SELECT id, ghi_no, so_du 
                FROM lich_su_cong_no_nha_cung_cap 
                WHERE nha_cung_cap_id = @NhaCungCapId 
                  AND (loai_giao_dich = 'NO_DAU_KY' OR loai_chung_tu = 'DAU_KY' OR ma_chung_tu = @MaChungTu)
                LIMIT 1
                FOR UPDATE;
                """;

                var existingRow = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    checkExistingNccSql,
                    new { NhaCungCapId = item.DoiTacId, MaChungTu = maChungTu },
                    transaction: transaction);

                if (existingRow != null)
                {
                    uint existingId = (uint)existingRow.id;
                    decimal oldGhiNo = ToDecimal(existingRow.ghi_no);
                    decimal delta = item.SoTien - oldGhiNo;

                    await connection.ExecuteAsync(
                        """
                        UPDATE lich_su_cong_no_nha_cung_cap
                        SET ghi_no = @SoTien,
                            so_du = so_du + @Delta,
                            thoi_gian = @ThoiGian,
                            nguoi_thuc_hien_id = @NguoiThucHienId,
                            ghi_chu = @GhiChu
                        WHERE id = @Id;
                        """,
                        new
                        {
                            Id = existingId,
                            SoTien = item.SoTien,
                            Delta = delta,
                            ThoiGian = ngayDauKy,
                            NguoiThucHienId = userId,
                            GhiChu = ghiChuDayDu
                        },
                        transaction: transaction);

                    if (delta != 0)
                    {
                        await connection.ExecuteAsync(
                            """
                            UPDATE lich_su_cong_no_nha_cung_cap
                            SET so_du = so_du + @Delta
                            WHERE nha_cung_cap_id = @NhaCungCapId AND id > @ExistingId;
                            """,
                            new
                            {
                                Delta = delta,
                                NhaCungCapId = item.DoiTacId,
                                ExistingId = existingId
                            },
                            transaction: transaction);
                    }
                }
                else
                {
                    var baselineDebt = await connection.ExecuteScalarAsync<decimal>(
                        """
                        SELECT COALESCE(no_can_tra_hien_tai, 0)
                        FROM nha_cung_cap
                        WHERE id = @NhaCungCapId;
                        """,
                        new { NhaCungCapId = item.DoiTacId },
                        transaction: transaction);

                    var currentDebt = await connection.ExecuteScalarAsync<decimal>(
                        """
                        SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                        FROM lich_su_cong_no_nha_cung_cap
                        WHERE nha_cung_cap_id = @NhaCungCapId
                        FOR UPDATE;
                        """,
                        new { NhaCungCapId = item.DoiTacId },
                        transaction: transaction);

                    var newDebtBalance = baselineDebt + currentDebt + item.SoTien;

                    await connection.ExecuteAsync(
                        """
                        INSERT INTO lich_su_cong_no_nha_cung_cap
                        (
                            nha_cung_cap_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                            ma_chung_tu, ghi_no, ghi_co, so_du,
                            thoi_gian, nguoi_thuc_hien_id, ghi_chu
                        )
                        VALUES
                        (
                            @NhaCungCapId, @ChiNhanhId, 'NO_DAU_KY', 'DAU_KY',
                            @MaChungTu, @GhiNo, 0, @SoDu,
                            @ThoiGian, @NguoiThucHienId, @GhiChu
                        );
                        """,
                        new
                        {
                            NhaCungCapId = item.DoiTacId,
                            ChiNhanhId = branchId,
                            MaChungTu = maChungTu,
                            GhiNo = item.SoTien,
                            SoDu = newDebtBalance,
                            ThoiGian = ngayDauKy,
                            NguoiThucHienId = userId,
                            GhiChu = ghiChuDayDu
                        },
                        transaction: transaction);
                }

                // LÆ°u Ã½: KHÃ”NG Ä‘á»“ng bá»™ cá»™t no_can_tra_hien_tai á»Ÿ Ä‘Ã¢y vÃ¬ cÃ´ng ná»£ Ä‘áº§u ká»³
                // Ä‘Ã£ Ä‘Æ°á»£c ghi nháº­n vÃ o sá»• cÃ¡i lich_su_cong_no_nha_cung_cap. Vá»«a ghi sá»• cÃ¡i
                // vá»«a gÃ¡n cá»™t ná»n táº£ng sáº½ khiáº¿n sá»‘ dÆ° nhÃ  cung cáº¥p bá»‹ cá»™ng hai láº§n.
            }

            // ==============================================================
            // 3. GHI LÆ¯U VÃ€O Báº¢NG chi_tiet_cong_no_dau_ky Äá»’NG Bá»˜
            // ==============================================================
            const string upsertDauKySql = """
            INSERT INTO chi_tiet_cong_no_dau_ky
            (
                loai_doi_tac, doi_tac_id, ma_doi_tac, ten_doi_tac, so_dien_thoai,
                so_tien, ngay_dau_ky, ngay_hen_tra, noi_dung, chi_nhanh_id, nguoi_tao_id
            )
            VALUES
            (
                @LoaiDoiTac, @DoiTacId, @MaDoiTac, @TenDoiTac, @SoDienThoai,
                @SoTien, @NgayDauKy, @NgayHenTra, @NoiDung, @ChiNhanhId, @NguoiTaoId
            )
            ON DUPLICATE KEY UPDATE
                so_tien = @SoTien,
                ngay_hen_tra = @NgayHenTra,
                noi_dung = @NoiDung,
                nguoi_tao_id = @NguoiTaoId,
                ngay_cap_nhat = NOW();
            """;

            await connection.ExecuteAsync(
                upsertDauKySql,
                new
                {
                    LoaiDoiTac = loaiDoiTac,
                    DoiTacId = item.DoiTacId,
                    MaDoiTac = item.MaDoiTac,
                    TenDoiTac = item.TenDoiTac,
                    SoDienThoai = item.SoDienThoai,
                    SoTien = item.SoTien,
                    NgayDauKy = ngayDauKy,
                    NgayHenTra = item.NgayHenTra?.Date,
                    NoiDung = item.NoiDung,
                    ChiNhanhId = branchId,
                    NguoiTaoId = userId
                },
                transaction: transaction);

            if (loaiDoiTac == "NHA_CUNG_CAP")
            {
                await EnsureBangCongNoNccKyAsync(connection, transaction);
                await CapNhatNoDauKyNccVaoSnapshotAsync(
                    connection,
                    transaction,
                    item.DoiTacId,
                    ngayDauKy,
                    item.SoTien);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "ThÃªm cÃ´ng ná»£ Ä‘áº§u ká»³",
                loaiDoiTac == "KHACH_HANG" ? "KHACH_HANG" : "NHA_CUNG_CAP",
                item.DoiTacId,
                item.MaDoiTac,
                $"Khai bÃ¡o cÃ´ng ná»£ Ä‘áº§u ká»³: {item.SoTien:N0} Ä‘ cho Ä‘á»‘i tÃ¡c {item.TenDoiTac} ({loaiDoiTac}). {item.NoiDung}",
                item.SoTien);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lá»—i LuuCongNoDauKyAsync", ex);
            throw;
        }
    }
    #endregion

    #region Khuyáº¿n MÃ£i
    /// <summary>
    /// Äáº£m báº£o cÃ¡c báº£ng khuyáº¿n mÃ£i tá»“n táº¡i trong cÆ¡ sá»Ÿ dá»¯ liá»‡u.
    /// </summary>
    public async Task EnsureBangKhuyenMaiAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        try
        {
            const string createTableSql = """
            CREATE TABLE IF NOT EXISTS chuong_trinh_khuyen_mai (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_chuong_trinh VARCHAR(50) NOT NULL UNIQUE,
                ten_chuong_trinh VARCHAR(255) NOT NULL,
                ngay_bat_dau DATETIME NOT NULL,
                ngay_ket_thuc DATETIME NOT NULL,
                nhom_khach_hang_id INT UNSIGNED NULL,
                trang_thai VARCHAR(50) NOT NULL DEFAULT 'DANG_HOAT_DONG',
                loai_khuyen_mai VARCHAR(50) NOT NULL DEFAULT 'PRODUCT_DISCOUNT',
                gia_tri_don_toi_thieu DECIMAL(15,2) NOT NULL DEFAULT 0,
                loai_giam_gia VARCHAR(20) NOT NULL DEFAULT 'PHAN_TRAM',
                gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu TEXT NULL,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                nguoi_tao_id INT UNSIGNED NULL,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_ctkm_ngay (ngay_bat_dau, ngay_ket_thuc),
                INDEX idx_ctkm_trangthai (trang_thai)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS chi_tiet_khuyen_mai (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                chuong_trinh_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                ma_san_pham VARCHAR(50) NULL,
                ten_san_pham VARCHAR(255) NULL,
                don_vi_tinh VARCHAR(50) NULL,
                so_luong_mua DECIMAL(15,2) NOT NULL DEFAULT 1,
                loai_giam_gia VARCHAR(20) NOT NULL DEFAULT 'PHAN_TRAM',
                gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0,
                so_luong_tang DECIMAL(15,2) NOT NULL DEFAULT 0,
                san_pham_tang_id INT UNSIGNED NULL,
                ten_san_pham_tang VARCHAR(255) NULL,
                ghi_chu VARCHAR(255) NULL,
                INDEX idx_ctkm_ct (chuong_trinh_id),
                INDEX idx_ctkm_sp (san_pham_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(createTableSql, transaction: transaction);

            // Báº£ng Ä‘Ã£ tá»“n táº¡i á»Ÿ phiÃªn báº£n cÅ© (tÃªn cá»™t khÃ¡c) thÃ¬ CREATE TABLE IF
            // NOT EXISTS khÃ´ng lÃ m gÃ¬, nÃªn pháº£i tá»± kiá»ƒm tra vÃ  bá»• sung cÃ²n thiáº¿u.
            if (transaction is null)
            {
                await EnsureCotBangKhuyenMaiAsync(connection);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhÃ´ng táº¡o Ä‘Æ°á»£c báº£ng khuyáº¿n mÃ£i", ex);
        }
    }

    /// <summary>
    /// Tá»± kiá»ƒm tra vÃ  Bá»” SUNG cÃ¡c cá»™t cÃ²n thiáº¿u cho hai báº£ng khuyáº¿n mÃ£i.
    /// <para>
    /// Database cÅ© dÃ¹ng tÃªn cá»™t khÃ¡c (chuong_trinh_khuyen_mai: thoi_gian_bat_dau /
    /// thoi_gian_ket_thuc / doi_tac_id / trang_thai dáº¡ng tinyint; chi_tiet_khuyen_mai:
    /// hang_hoa_id / hang_hoa_tang_id / so_luong_hang_tang / phan_tram_giam_gia /
    /// so_tien_giam). Thay vÃ¬ báº¯t ngÆ°á»i dÃ¹ng cháº¡y láº¡i script xoÃ¡ báº£ng (máº¥t dá»¯
    /// liá»‡u), hÃ m nÃ y thÃªm cá»™t má»›i rá»“i chuyá»ƒn dá»¯ liá»‡u sang â€” khÃ´ng xoÃ¡ gÃ¬.
    /// </para>
    /// </summary>
    private async Task EnsureCotBangKhuyenMaiAsync(MySqlConnection connection)
    {
        try
        {
            var cotChuongTrinh = await DocCotBangAsync(connection, "chuong_trinh_khuyen_mai");
            var cotChiTiet = await DocCotBangAsync(connection, "chi_tiet_khuyen_mai");

            if (cotChuongTrinh.Count == 0 || cotChiTiet.Count == 0)
                return;

            // ---------------- chuong_trinh_khuyen_mai ----------------
            if (!cotChuongTrinh.ContainsKey("ngay_bat_dau"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_bat_dau DATETIME NULL AFTER ten_chuong_trinh;");
            }

            if (!cotChuongTrinh.ContainsKey("ngay_ket_thuc"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_ket_thuc DATETIME NULL AFTER ngay_bat_dau;");
            }

            if (cotChuongTrinh.ContainsKey("thoi_gian_bat_dau"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chuong_trinh_khuyen_mai SET ngay_bat_dau = thoi_gian_bat_dau WHERE ngay_bat_dau IS NULL;");
            }

            if (cotChuongTrinh.ContainsKey("thoi_gian_ket_thuc"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chuong_trinh_khuyen_mai SET ngay_ket_thuc = thoi_gian_ket_thuc WHERE ngay_ket_thuc IS NULL;");
            }

            // Hai cá»™t ngÃ y khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng: gÃ¡n ngÃ y máº·c Ä‘á»‹nh rá»“i siáº¿t láº¡i.
            await connection.ExecuteAsync(
                "UPDATE chuong_trinh_khuyen_mai SET ngay_bat_dau = '2000-01-01 00:00:00' WHERE ngay_bat_dau IS NULL;");
            await connection.ExecuteAsync(
                "UPDATE chuong_trinh_khuyen_mai SET ngay_ket_thuc = '2099-12-31 23:59:59' WHERE ngay_ket_thuc IS NULL;");
            await connection.ExecuteAsync(
                "ALTER TABLE chuong_trinh_khuyen_mai MODIFY COLUMN ngay_bat_dau DATETIME NOT NULL;");
            await connection.ExecuteAsync(
                "ALTER TABLE chuong_trinh_khuyen_mai MODIFY COLUMN ngay_ket_thuc DATETIME NOT NULL;");

            if (!cotChuongTrinh.ContainsKey("nhom_khach_hang_id"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN nhom_khach_hang_id INT UNSIGNED NULL AFTER ngay_ket_thuc;");
            }

            if (cotChuongTrinh.ContainsKey("doi_tac_id"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chuong_trinh_khuyen_mai SET nhom_khach_hang_id = doi_tac_id WHERE nhom_khach_hang_id IS NULL;");
            }

            // trang_thai: báº£n cÅ© dÃ¹ng tinyint, báº£n má»›i dÃ¹ng chuá»—i tráº¡ng thÃ¡i.
            if (!cotChuongTrinh.ContainsKey("trang_thai"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN trang_thai VARCHAR(50) NOT NULL DEFAULT 'DANG_HOAT_DONG' AFTER nhom_khach_hang_id;");
            }
            else if (!cotChuongTrinh["trang_thai"].StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
                     && !cotChuongTrinh["trang_thai"].StartsWith("char", StringComparison.OrdinalIgnoreCase)
                     && !cotChuongTrinh["trang_thai"].StartsWith("text", StringComparison.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(
                    "UPDATE chuong_trinh_khuyen_mai SET trang_thai = 1;");
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai MODIFY COLUMN trang_thai VARCHAR(50) NOT NULL DEFAULT 'DANG_HOAT_DONG';");
                await connection.ExecuteAsync(
                    "UPDATE chuong_trinh_khuyen_mai SET trang_thai = 'DANG_HOAT_DONG' WHERE trang_thai = '1';");
            }

            if (!cotChuongTrinh.ContainsKey("loai_khuyen_mai"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN loai_khuyen_mai VARCHAR(50) NOT NULL DEFAULT 'PRODUCT_DISCOUNT' AFTER trang_thai;");
            }

            // Chuyá»ƒn mÃ£ loáº¡i khuyáº¿n mÃ£i cÅ© sang bá»™ mÃ£ dÃ¹ng chung má»›i.
            await connection.ExecuteAsync(
                "UPDATE chuong_trinh_khuyen_mai SET loai_khuyen_mai = 'ORDER_DISCOUNT' WHERE loai_khuyen_mai = 'THEO_HOA_DON';");
            await connection.ExecuteAsync(
                "UPDATE chuong_trinh_khuyen_mai SET loai_khuyen_mai = 'BUY_X_GET_Y' WHERE loai_khuyen_mai = 'TANG_KEM';");
            await connection.ExecuteAsync(
                "UPDATE chuong_trinh_khuyen_mai SET loai_khuyen_mai = 'PRODUCT_DISCOUNT' WHERE loai_khuyen_mai = 'THEO_HANG_HOA';");

            if (!cotChuongTrinh.ContainsKey("gia_tri_don_toi_thieu"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN gia_tri_don_toi_thieu DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_khuyen_mai;");
            }

            if (!cotChuongTrinh.ContainsKey("loai_giam_gia"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN loai_giam_gia VARCHAR(20) NOT NULL DEFAULT 'PHAN_TRAM' AFTER gia_tri_don_toi_thieu;");
            }

            if (!cotChuongTrinh.ContainsKey("gia_tri_giam"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_giam_gia;");
            }

            if (!cotChuongTrinh.ContainsKey("ghi_chu"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ghi_chu TEXT NULL AFTER loai_khuyen_mai;");
            }

            if (cotChuongTrinh.ContainsKey("mo_ta"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chuong_trinh_khuyen_mai SET ghi_chu = mo_ta WHERE (ghi_chu IS NULL OR ghi_chu = '') AND mo_ta IS NOT NULL;");
            }

            if (!cotChuongTrinh.ContainsKey("chi_nhanh_id"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1 AFTER ghi_chu;");
            }

            if (!cotChuongTrinh.ContainsKey("nguoi_tao_id"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN nguoi_tao_id INT UNSIGNED NULL AFTER chi_nhanh_id;");
            }

            if (!cotChuongTrinh.ContainsKey("ngay_tao"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP AFTER nguoi_tao_id;");
            }

            if (!cotChuongTrinh.ContainsKey("ngay_cap_nhat"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER ngay_tao;");
            }

            // ---------------- chi_tiet_khuyen_mai ----------------
            if (!cotChiTiet.ContainsKey("san_pham_id"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN san_pham_id INT UNSIGNED NOT NULL DEFAULT 0 AFTER chuong_trinh_id;");
            }

            if (cotChiTiet.ContainsKey("hang_hoa_id"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chi_tiet_khuyen_mai SET san_pham_id = hang_hoa_id WHERE san_pham_id = 0;");
            }

            if (!cotChiTiet.ContainsKey("ma_san_pham"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ma_san_pham VARCHAR(50) NULL AFTER san_pham_id;");
            }

            if (!cotChiTiet.ContainsKey("ten_san_pham"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ten_san_pham VARCHAR(255) NULL AFTER ma_san_pham;");
            }

            if (!cotChiTiet.ContainsKey("don_vi_tinh"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN don_vi_tinh VARCHAR(50) NULL AFTER ten_san_pham;");
            }

            if (!cotChiTiet.ContainsKey("so_luong_mua"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN so_luong_mua DECIMAL(15,2) NOT NULL DEFAULT 1 AFTER don_vi_tinh;");
            }

            if (!cotChiTiet.ContainsKey("loai_giam_gia"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN loai_giam_gia VARCHAR(20) NOT NULL DEFAULT 'PHAN_TRAM' AFTER so_luong_mua;");
            }

            if (!cotChiTiet.ContainsKey("gia_tri_giam"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_giam_gia;");
            }

            if (cotChiTiet.ContainsKey("phan_tram_giam_gia"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chi_tiet_khuyen_mai SET gia_tri_giam = phan_tram_giam_gia WHERE gia_tri_giam = 0 AND phan_tram_giam_gia > 0;");
            }

            if (cotChiTiet.ContainsKey("so_tien_giam"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chi_tiet_khuyen_mai SET gia_tri_giam = so_tien_giam WHERE gia_tri_giam = 0 AND so_tien_giam > 0;");
                await connection.ExecuteAsync(
                    "UPDATE chi_tiet_khuyen_mai SET loai_giam_gia = 'SO_TIEN' WHERE loai_giam_gia = 'PHAN_TRAM' AND so_tien_giam > 0 AND phan_tram_giam_gia = 0;");
            }

            if (!cotChiTiet.ContainsKey("so_luong_tang"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN so_luong_tang DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER gia_tri_giam;");
            }

            if (cotChiTiet.ContainsKey("so_luong_hang_tang"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chi_tiet_khuyen_mai SET so_luong_tang = so_luong_hang_tang WHERE so_luong_tang = 0 AND so_luong_hang_tang > 0;");
            }

            if (!cotChiTiet.ContainsKey("san_pham_tang_id"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN san_pham_tang_id INT UNSIGNED NULL AFTER so_luong_tang;");
            }

            if (cotChiTiet.ContainsKey("hang_hoa_tang_id"))
            {
                await connection.ExecuteAsync(
                    "UPDATE chi_tiet_khuyen_mai SET san_pham_tang_id = hang_hoa_tang_id WHERE san_pham_tang_id IS NULL;");
            }

            if (!cotChiTiet.ContainsKey("ten_san_pham_tang"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ten_san_pham_tang VARCHAR(255) NULL AFTER san_pham_tang_id;");
            }

            if (!cotChiTiet.ContainsKey("ghi_chu"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ghi_chu VARCHAR(255) NULL AFTER ten_san_pham_tang;");
            }

            // Báº£ng cÅ© khai bÃ¡o hang_hoa_id / don_vi_tinh_id lÃ  NOT NULL (kÃ¨m khoÃ¡
            // ngoáº¡i sang báº£ng hÃ ng hÃ³a cÅ©). á»¨ng dá»¥ng chá»‰ ghi bá»™ cá»™t má»›i
            // san_pham_* nÃªn hai cá»™t cÅ© pháº£i cho phÃ©p NULL, náº¿u khÃ´ng lÆ°u
            // khuyáº¿n mÃ£i sáº½ lá»—i "doesn't have a default value" / khoÃ¡ ngoáº¡i.
            await NoptCotCuKhuyenMaiAsync(connection, cotChiTiet, "hang_hoa_id");
            await NoptCotCuKhuyenMaiAsync(connection, cotChiTiet, "don_vi_tinh_id");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Khong bo sung duoc cot cho bang khuyen mai", ex);
        }
    }

    /// <summary>
    /// Cho phÃ©p má»™t cá»™t cÅ© cá»§a báº£ng chi tiáº¿t khuyáº¿n mÃ£i (Ä‘ang khai bÃ¡o NOT NULL
    /// vÃ  cÃ³ khoÃ¡ ngoáº¡i sang báº£ng hÃ ng hÃ³a cÅ©) nháº­n giÃ¡ trá»‹ NULL, Ä‘á»ƒ á»©ng dá»¥ng má»›i
    /// chá»‰ cáº§n ghi bá»™ cá»™t "san_pham_*" mÃ  váº«n lÆ°u Ä‘Æ°á»£c khuyáº¿n mÃ£i.
    /// </summary>
    private static async Task NoptCotCuKhuyenMaiAsync(
        MySqlConnection connection,
        Dictionary<string, string> cotBang,
        string tenCot)
    {
        if (!cotBang.ContainsKey(tenCot))
            return;

        var thongTin = await connection.QueryAsync<(string Nullable, string Kieu)>(
            """
            SELECT IS_NULLABLE, COLUMN_TYPE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'chi_tiet_khuyen_mai'
              AND COLUMN_NAME = @TenCot;
            """,
            new { TenCot = tenCot });

        var cot = thongTin.FirstOrDefault();

        if (cot.Nullable is null ||
            cot.Nullable.Equals("YES", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var kieu = cot.Kieu?.Contains("unsigned", StringComparison.OrdinalIgnoreCase) == true
            ? "INT(10) UNSIGNED"
            : "INT";

        await connection.ExecuteAsync(
            $"ALTER TABLE chi_tiet_khuyen_mai MODIFY COLUMN {tenCot} {kieu} NULL DEFAULT NULL;");
    }

    /// <summary>
    /// Äá»c danh sÃ¡ch cá»™t (tÃªn -> kiá»ƒu dá»¯ liá»‡u) cá»§a má»™t báº£ng trong database hiá»‡n táº¡i.
    /// </summary>
    private static async Task<Dictionary<string, string>> DocCotBangAsync(
        MySqlConnection connection,
        string tenBang)
    {
        var cot = await connection.QueryAsync<(string Ten, string Kieu)>(
            """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TenBang;
            """,
            new { TenBang = tenBang });

        return cot.ToDictionary(x => x.Ten, x => x.Kieu, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sinh mÃ£ chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i tá»± Ä‘á»™ng theo ngÃ y: KM{yyyyMMdd}{001}.
    /// </summary>
    public async Task<string> SinhMaKhuyenMaiMoiAsync()
    {
        var prefix = $"KM{DateTime.Now:yyyyMMdd}";
        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);

        const string sql = """
        SELECT ma_chuong_trinh
        FROM chuong_trinh_khuyen_mai
        WHERE ma_chuong_trinh LIKE CONCAT(@Prefix, '%')
        ORDER BY ma_chuong_trinh DESC
        LIMIT 1;
        """;

        var lastMa = await connection.QueryFirstOrDefaultAsync<string>(sql, new { Prefix = prefix });
        if (string.IsNullOrWhiteSpace(lastMa) || lastMa.Length < prefix.Length + 3)
        {
            return $"{prefix}001";
        }

        var suffix = lastMa.Substring(prefix.Length);
        if (int.TryParse(suffix, out var num))
        {
            return $"{prefix}{(num + 1):D3}";
        }

        return $"{prefix}001";
    }

    /// <summary>
    /// Kiá»ƒm tra xem mÃ£ chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i Ä‘Ã£ tá»“n táº¡i chÆ°a.
    /// </summary>
    public async Task<bool> KiemTraMaKhuyenMaiTonTaiAsync(string ma, uint? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return false;

        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);

        const string sql = """
        SELECT COUNT(*)
        FROM chuong_trinh_khuyen_mai
        WHERE ma_chuong_trinh = @Ma AND (@ExcludeId IS NULL OR id != @ExcludeId);
        """;

        var count = await connection.ExecuteScalarAsync<int>(sql, new { Ma = ma.Trim(), ExcludeId = excludeId });
        return count > 0;
    }

    /// <summary>
    /// LÆ°u chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i vÃ  cÃ¡c dÃ²ng chi tiáº¿t hÃ ng hÃ³a (há»— trá»£ táº¡o má»›i hoáº·c cáº­p nháº­t) báº±ng Transaction.
    /// </summary>
    public async Task<uint> LuuChuongTrinhKhuyenMaiAsync(ChuongTrinhKhuyenMai ct, IReadOnlyList<ChiTietKhuyenMai> chiTiets)
    {
        if (ct is null)
            throw new ArgumentNullException(nameof(ct));

        if (string.IsNullOrWhiteSpace(ct.TenChuongTrinh))
            throw new ArgumentException("TÃªn chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.", nameof(ct.TenChuongTrinh));

        if (ct.NgayKetThuc < ct.NgayBatDau)
            throw new ArgumentException("NgÃ y káº¿t thÃºc khÃ´ng Ä‘Æ°á»£c trÆ°á»›c ngÃ y báº¯t Ä‘áº§u.", nameof(ct.NgayKetThuc));

        var branchId = ct.ChiNhanhId > 0 ? ct.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);
        var userId = Session.CurrentUser?.Id;

        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            uint chuongTrinhId = ct.Id;

            if (chuongTrinhId == 0)
            {
                // Kiá»ƒm tra trÃ¹ng mÃ£
                var exists = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM chuong_trinh_khuyen_mai WHERE ma_chuong_trinh = @Ma;",
                    new { Ma = ct.MaChuongTrinh.Trim() },
                    transaction: transaction) > 0;

                if (exists)
                {
                    // Náº¿u trÃ¹ng thÃ¬ tá»± sinh mÃ£ má»›i
                    ct.MaChuongTrinh = await SinhMaKhuyenMaiMoiAsync();
                }

                const string insertSql = """
                INSERT INTO chuong_trinh_khuyen_mai
                (
                    ma_chuong_trinh, ten_chuong_trinh, ngay_bat_dau, ngay_ket_thuc,
                    trang_thai, loai_khuyen_mai, gia_tri_don_toi_thieu,
                    loai_giam_gia, gia_tri_giam, ghi_chu,
                    chi_nhanh_id, nguoi_tao_id
                )
                VALUES
                (
                    @MaChuongTrinh, @TenChuongTrinh, @NgayBatDau, @NgayKetThuc,
                    @TrangThai, @LoaiKhuyenMai, @GiaTriDonToiThieu,
                    @LoaiGiamGia, @GiaTriGiam, @GhiChu,
                    @ChiNhanhId, @NguoiTaoId
                );
                SELECT LAST_INSERT_ID();
                """;

                chuongTrinhId = await connection.ExecuteScalarAsync<uint>(
                    insertSql,
                    new
                    {
                        ct.MaChuongTrinh,
                        ct.TenChuongTrinh,
                        ct.NgayBatDau,
                        ct.NgayKetThuc,
                        ct.TrangThai,
                        ct.LoaiKhuyenMai,
                        ct.GiaTriDonToiThieu,
                        ct.LoaiGiamGia,
                        ct.GiaTriGiam,
                        ct.GhiChu,
                        ChiNhanhId = branchId,
                        NguoiTaoId = userId
                    },
                    transaction: transaction);
                ct.Id = chuongTrinhId;
            }
            else
            {
                const string updateSql = """
                UPDATE chuong_trinh_khuyen_mai
                SET ma_chuong_trinh = @MaChuongTrinh,
                    ten_chuong_trinh = @TenChuongTrinh,
                    ngay_bat_dau = @NgayBatDau,
                    ngay_ket_thuc = @NgayKetThuc,
                    trang_thai = @TrangThai,
                    loai_khuyen_mai = @LoaiKhuyenMai,
                    gia_tri_don_toi_thieu = @GiaTriDonToiThieu,
                    loai_giam_gia = @LoaiGiamGia,
                    gia_tri_giam = @GiaTriGiam,
                    ghi_chu = @GhiChu,
                    chi_nhanh_id = @ChiNhanhId
                WHERE id = @Id;
                """;

                await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        ct.MaChuongTrinh,
                        ct.TenChuongTrinh,
                        ct.NgayBatDau,
                        ct.NgayKetThuc,
                        ct.TrangThai,
                        ct.LoaiKhuyenMai,
                        ct.GiaTriDonToiThieu,
                        ct.LoaiGiamGia,
                        ct.GiaTriGiam,
                        ct.GhiChu,
                        ChiNhanhId = branchId,
                        ct.Id
                    },
                    transaction: transaction);

                // XÃ³a chi tiáº¿t cÅ© trÆ°á»›c khi ghi láº¡i
                await connection.ExecuteAsync(
                    "DELETE FROM chi_tiet_khuyen_mai WHERE chuong_trinh_id = @ChuongTrinhId;",
                    new { ChuongTrinhId = chuongTrinhId },
                    transaction: transaction);
            }

            // Ghi cÃ¡c dÃ²ng chi tiáº¿t hÃ ng hÃ³a Ã¡p dá»¥ng
            if (chiTiets != null && chiTiets.Count > 0)
            {
                const string insertDetailSql = """
                INSERT INTO chi_tiet_khuyen_mai
                (
                    chuong_trinh_id, san_pham_id, ma_san_pham, ten_san_pham, don_vi_tinh,
                    so_luong_mua, loai_giam_gia, gia_tri_giam, so_luong_tang,
                    san_pham_tang_id, ten_san_pham_tang, ghi_chu
                )
                VALUES
                (
                    @ChuongTrinhId, @SanPhamId, @MaSanPham, @TenSanPham, @DonViTinh,
                    @SoLuongMua, @LoaiGiamGia, @GiaTriGiam, @SoLuongTang,
                    @SanPhamTangId, @TenSanPhamTang, @GhiChu
                );
                """;

                foreach (var d in chiTiets.Where(c => c.SanPhamId > 0))
                {
                    await connection.ExecuteAsync(
                        insertDetailSql,
                        new
                        {
                            ChuongTrinhId = chuongTrinhId,
                            d.SanPhamId,
                            d.MaSanPham,
                            d.TenSanPham,
                            d.DonViTinh,
                            d.SoLuongMua,
                            d.LoaiGiamGia,
                            d.GiaTriGiam,
                            d.SoLuongTang,
                            d.SanPhamTangId,
                            d.TenSanPhamTang,
                            d.GhiChu
                        },
                        transaction: transaction);
                }
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Khuyáº¿n mÃ£i",
                "CHUONG_TRINH_KHUYEN_MAI",
                chuongTrinhId,
                ct.MaChuongTrinh,
                $"LÆ°u chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i: {ct.TenChuongTrinh} ({chiTiets?.Count ?? 0} sáº£n pháº©m)",
                0);

            return chuongTrinhId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lá»—i LuuChuongTrinhKhuyenMaiAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch cÃ¡c chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i trong há»‡ thá»‘ng.
    /// </summary>
    public async Task<IReadOnlyList<ChuongTrinhKhuyenMai>> GetDanhSachKhuyenMaiAsync(
        string? search = null,
        string? trangThai = null,
        DateTime? tuNgay = null,
        DateTime? denNgay = null)
    {
        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);

        var sql = """
        SELECT 
            km.id AS Id,
            km.ma_chuong_trinh AS MaChuongTrinh,
            km.ten_chuong_trinh AS TenChuongTrinh,
            km.ngay_bat_dau AS NgayBatDau,
            km.ngay_ket_thuc AS NgayKetThuc,
            NULL AS NhomKhachHangId,
            NULL AS TenNhomKhachHang,
            km.trang_thai AS TrangThai,
            km.loai_khuyen_mai AS LoaiKhuyenMai,
            COALESCE(km.gia_tri_don_toi_thieu, 0) AS GiaTriDonToiThieu,
            COALESCE(km.loai_giam_gia, 'PHAN_TRAM') AS LoaiGiamGia,
            COALESCE(km.gia_tri_giam, 0) AS GiaTriGiam,
            km.ghi_chu AS GhiChu,
            km.chi_nhanh_id AS ChiNhanhId,
            km.ngay_tao AS NgayTao
        FROM chuong_trinh_khuyen_mai km
        WHERE 1 = 1
        """;

        var param = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (km.ma_chuong_trinh LIKE @Search OR km.ten_chuong_trinh LIKE @Search)";
            param.Add("Search", $"%{search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            if (trangThai == "DANG_HOAT_DONG")
            {
                sql += " AND km.trang_thai = 'DANG_HOAT_DONG' AND km.ngay_bat_dau <= NOW() AND km.ngay_ket_thuc >= CURDATE()";
            }
            else if (trangThai == "CHUA_BAT_DAU")
            {
                sql += " AND (km.trang_thai = 'CHUA_BAT_DAU' OR (km.trang_thai = 'DANG_HOAT_DONG' AND km.ngay_bat_dau > NOW()))";
            }
            else if (trangThai == "DA_KET_THUC")
            {
                sql += " AND (km.trang_thai = 'DA_KET_THUC' OR km.ngay_ket_thuc < CURDATE())";
            }
            else if (trangThai == "TAM_NGUNG")
            {
                sql += " AND km.trang_thai = 'TAM_NGUNG'";
            }
            else
            {
                sql += " AND km.trang_thai = @TrangThai";
                param.Add("TrangThai", trangThai);
            }
        }

        if (tuNgay.HasValue)
        {
            sql += " AND km.ngay_ket_thuc >= @TuNgay";
            param.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            sql += " AND km.ngay_bat_dau <= @DenNgay";
            param.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        sql += " ORDER BY km.id DESC;";

        var list = (await connection.QueryAsync<ChuongTrinhKhuyenMai>(sql, param)).AsList();

        // Chuáº©n hÃ³a loáº¡i khuyáº¿n mÃ£i cÅ©
        foreach (var item in list)
        {
            item.LoaiKhuyenMai = ShopHoa.Models.KhuyenMaiLoai.ChuanHoa(item.LoaiKhuyenMai);
        }

        // Náº¡p sá»‘ lÆ°á»£ng sáº£n pháº©m Ã¡p dá»¥ng vÃ  tÃ³m táº¯t Æ°u Ä‘Ã£i cho má»—i chÆ°Æ¡ng trÃ¬nh
        if (list.Count > 0)
        {
            const string countSql = "SELECT chuong_trinh_id AS CtId, COUNT(*) AS SoLuong FROM chi_tiet_khuyen_mai GROUP BY chuong_trinh_id;";
            var counts = (await connection.QueryAsync<(uint CtId, int SoLuong)>(countSql)).ToDictionary(x => x.CtId, x => x.SoLuong);

            const string detailSummarySql = """
            SELECT 
                ct.chuong_trinh_id AS CtId,
                ct.loai_giam_gia AS LoaiGiamGia,
                ct.gia_tri_giam AS GiaTriGiam,
                ct.so_luong_tang AS SoLuongTang,
                ct.ten_san_pham_tang AS TenSanPhamTang
            FROM chi_tiet_khuyen_mai ct
            ORDER BY ct.id ASC;
            """;
            var details = (await connection.QueryAsync<(uint CtId, string LoaiGiamGia, decimal GiaTriGiam, decimal SoLuongTang, string? TenSanPhamTang)>(detailSummarySql))
                .GroupBy(x => x.CtId)
                .ToDictionary(g => g.Key, g => g.First());

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                item.Stt = i + 1;

                if (counts.TryGetValue(item.Id, out var count))
                {
                    for (int c = 0; c < count; c++)
                        item.DanhSachChiTiet.Add(new ChiTietKhuyenMai());
                }

                if (string.Equals(item.LoaiKhuyenMai, ShopHoa.Models.KhuyenMaiLoai.TheoDonHang, StringComparison.OrdinalIgnoreCase))
                {
                    item.TomTatUuDai = item.GiaTriGiam > 0
                        ? (string.Equals(item.LoaiGiamGia, "PHAN_TRAM", StringComparison.OrdinalIgnoreCase)
                            ? $"Giáº£m {item.GiaTriGiam:0.##}% cho Ä‘Æ¡n tá»« {item.GiaTriDonToiThieu:N0} Ä‘"
                            : $"Giáº£m {item.GiaTriGiam:N0} Ä‘ cho Ä‘Æ¡n tá»« {item.GiaTriDonToiThieu:N0} Ä‘")
                        : "Giáº£m giÃ¡ theo tá»•ng Ä‘Æ¡n hÃ ng";
                }
                else if (details.TryGetValue(item.Id, out var det))
                {
                    if (det.SoLuongTang > 0)
                    {
                        var tenTang = string.IsNullOrWhiteSpace(det.TenSanPhamTang) ? "sáº£n pháº©m" : det.TenSanPhamTang;
                        item.TomTatUuDai = $"Mua Ä‘á»§ táº·ng {det.SoLuongTang:0.##} {tenTang} ({item.SoLuongSanPham} sáº£n pháº©m)";
                    }
                    else if (string.Equals(det.LoaiGiamGia, "PHAN_TRAM", StringComparison.OrdinalIgnoreCase))
                    {
                        item.TomTatUuDai = det.GiaTriGiam > 0 ? $"Giáº£m {det.GiaTriGiam:0.##}% ({item.SoLuongSanPham} sáº£n pháº©m)" : $"{item.SoLuongSanPham} sáº£n pháº©m";
                    }
                    else
                    {
                        item.TomTatUuDai = det.GiaTriGiam > 0 ? $"Giáº£m {det.GiaTriGiam:N0} Ä‘ ({item.SoLuongSanPham} sáº£n pháº©m)" : $"{item.SoLuongSanPham} sáº£n pháº©m";
                    }
                }
                else
                {
                    item.TomTatUuDai = item.SoLuongSanPham > 0 ? $"{item.SoLuongSanPham} sáº£n pháº©m Ã¡p dá»¥ng" : "ChÆ°a cáº¥u hÃ¬nh Æ°u Ä‘Ã£i";
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Láº¥y chi tiáº¿t cÃ¡c sáº£n pháº©m Ã¡p dá»¥ng trong má»™t chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i.
    /// </summary>
    public async Task<IReadOnlyList<ChiTietKhuyenMai>> GetChiTietKhuyenMaiAsync(uint chuongTrinhId)
    {
        await using var connection = await OpenAsync();
        await EnsureBangKhuyenMaiAsync(connection);

        const string sql = """
        SELECT 
            ct.id AS Id,
            ct.chuong_trinh_id AS ChuongTrinhId,
            ct.san_pham_id AS SanPhamId,
            ct.ma_san_pham AS MaSanPham,
            ct.ten_san_pham AS TenSanPham,
            COALESCE(ct.don_vi_tinh, dvt.ten_don_vi, 'CÃ¡i') AS DonViTinh,
            COALESCE(sp.gia_ban, 0) AS GiaBanHienTai,
            ct.so_luong_mua AS SoLuongMua,
            ct.loai_giam_gia AS LoaiGiamGia,
            ct.gia_tri_giam AS GiaTriGiam,
            ct.so_luong_tang AS SoLuongTang,
            ct.san_pham_tang_id AS SanPhamTangId,
            ct.ten_san_pham_tang AS TenSanPhamTang,
            ct.ghi_chu AS GhiChu
        FROM chi_tiet_khuyen_mai ct
        LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        WHERE ct.chuong_trinh_id = @ChuongTrinhId
        ORDER BY ct.id ASC;
        """;

        var list = (await connection.QueryAsync<ChiTietKhuyenMai>(sql, new { ChuongTrinhId = chuongTrinhId })).AsList();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Stt = i + 1;
        }

        return list;
    }

    /// <summary>
    /// XÃ³a má»™t chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i.
    /// </summary>
    public async Task<bool> XoaChuongTrinhKhuyenMaiAsync(uint chuongTrinhId)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM chi_tiet_khuyen_mai WHERE chuong_trinh_id = @Id;",
                new { Id = chuongTrinhId },
                transaction: transaction);

            var rows = await connection.ExecuteAsync(
                "DELETE FROM chuong_trinh_khuyen_mai WHERE id = @Id;",
                new { Id = chuongTrinhId },
                transaction: transaction);

            await transaction.CommitAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lá»—i XoaChuongTrinhKhuyenMaiAsync", ex);
            throw;
        }
    }
    #endregion

    #region Sá»• Quá»¹ Äáº§u Ká»³
    /// <summary>
    /// Äáº£m báº£o báº£ng so_quy_dau_ky tá»“n táº¡i Ä‘á»ƒ lÆ°u sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ do ngÆ°á»i dÃ¹ng khai bÃ¡o.
    /// </summary>
    public async Task EnsureBangSoQuyDauKyAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        try
        {
            const string createTableSql = """
            CREATE TABLE IF NOT EXISTS so_quy_dau_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ngay_dau_ky DATE NOT NULL,
                phuong_thuc VARCHAR(50) NOT NULL DEFAULT 'TIEN_MAT',
                so_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                noi_dung VARCHAR(255) NULL,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                nguoi_tao_id INT UNSIGNED NULL,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_so_quy_dau_ky (phuong_thuc, ngay_dau_ky, chi_nhanh_id),
                INDEX idx_sqdk_ngay (ngay_dau_ky)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(createTableSql, transaction: transaction);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhÃ´ng táº¡o Ä‘Æ°á»£c báº£ng so_quy_dau_ky", ex);
        }
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch cÃ¡c báº£n ghi sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ cá»§a chi nhÃ¡nh hiá»‡n táº¡i.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyDauKy>> GetDanhSachSoQuyDauKyAsync(string? search = null)
    {
        await using var connection = await OpenAsync();
        await EnsureBangSoQuyDauKyAsync(connection);

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;

        var sql = """
        SELECT
            id AS Id,
            ngay_dau_ky AS NgayDauKy,
            phuong_thuc AS PhuongThuc,
            so_tien AS SoTien,
            noi_dung AS NoiDung,
            chi_nhanh_id AS ChiNhanhId,
            ngay_tao AS NgayTao
        FROM so_quy_dau_ky
        WHERE chi_nhanh_id = @ChiNhanhId
        """;

        var param = new DynamicParameters();
        param.Add("ChiNhanhId", branchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (phuong_thuc LIKE @Search OR noi_dung LIKE @Search)";
            param.Add("Search", $"%{search.Trim()}%");
        }

        sql += " ORDER BY ngay_dau_ky DESC, id DESC;";

        return (await connection.QueryAsync<SoQuyDauKy>(sql, param)).AsList();
    }

    /// <summary>
    /// LÆ°u (thÃªm má»›i hoáº·c cáº­p nháº­t) má»™t báº£n ghi sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³.
    /// Má»—i quá»¹ chá»‰ cÃ³ má»™t sá»‘ dÆ° cho má»—i ngÃ y nÃªn dÃ¹ng khÃ³a duy nháº¥t (phuong_thuc, ngay_dau_ky, chi_nhanh_id).
    /// </summary>
    public async Task<uint> LuuSoQuyDauKyAsync(SoQuyDauKy item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        if (item.NgayDauKy == default)
            throw new ArgumentException("NgÃ y Ä‘áº§u ká»³ khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.", nameof(item.NgayDauKy));

        if (string.IsNullOrWhiteSpace(item.PhuongThuc))
            throw new ArgumentException("Vui lÃ²ng chá»n quá»¹ cho sá»‘ dÆ° Ä‘áº§u ká»³.", nameof(item.PhuongThuc));

        var branchId = item.ChiNhanhId > 0 ? item.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);
        var userId = Session.CurrentUser?.Id;
        var phuongThuc = item.PhuongThuc.Trim().ToUpperInvariant();
        var ngay = item.NgayDauKy.Date;
        var noiDung = string.IsNullOrWhiteSpace(item.NoiDung) ? "Sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³" : item.NoiDung.Trim();

        await using var connection = await OpenAsync();
        await EnsureBangSoQuyDauKyAsync(connection);

        if (item.Id == 0)
        {
            const string upsertSql = """
            INSERT INTO so_quy_dau_ky (ngay_dau_ky, phuong_thuc, so_tien, noi_dung, chi_nhanh_id, nguoi_tao_id)
            VALUES (@NgayDauKy, @PhuongThuc, @SoTien, @NoiDung, @ChiNhanhId, @NguoiTaoId)
            ON DUPLICATE KEY UPDATE
                id = LAST_INSERT_ID(id),
                so_tien = @SoTien,
                noi_dung = @NoiDung,
                nguoi_tao_id = @NguoiTaoId,
                ngay_cap_nhat = NOW();
            SELECT LAST_INSERT_ID();
            """;

            return await connection.ExecuteScalarAsync<uint>(upsertSql, new
            {
                NgayDauKy = ngay,
                PhuongThuc = phuongThuc,
                SoTien = item.SoTien,
                NoiDung = noiDung,
                ChiNhanhId = branchId,
                NguoiTaoId = userId
            });
        }

        const string updateSql = """
        UPDATE so_quy_dau_ky
        SET ngay_dau_ky = @NgayDauKy,
            phuong_thuc = @PhuongThuc,
            so_tien = @SoTien,
            noi_dung = @NoiDung,
            chi_nhanh_id = @ChiNhanhId,
            nguoi_tao_id = @NguoiTaoId,
            ngay_cap_nhat = NOW()
        WHERE id = @Id;
        """;

        await connection.ExecuteAsync(updateSql, new
        {
            item.Id,
            NgayDauKy = ngay,
            PhuongThuc = phuongThuc,
            SoTien = item.SoTien,
            NoiDung = noiDung,
            ChiNhanhId = branchId,
            NguoiTaoId = userId
        });

        return item.Id;
    }

    /// <summary>XÃ³a má»™t báº£n ghi sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³.</summary>
    public async Task<bool> XoaSoQuyDauKyAsync(uint id)
    {
        await using var connection = await OpenAsync();
        await EnsureBangSoQuyDauKyAsync(connection);

        var rows = await connection.ExecuteAsync(
            "DELETE FROM so_quy_dau_ky WHERE id = @Id;",
            new { Id = id });

        return rows > 0;
    }

    /// <summary>
    /// Tá»•ng sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ cá»§a chi nhÃ¡nh hiá»‡n táº¡i (tÃ¹y chá»n lá»c theo má»™t quá»¹
    /// vÃ  theo má»‘c ngÃ y). DÃ¹ng Ä‘á»ƒ cá»™ng vÃ o "quá»¹ Ä‘áº§u ká»³" khi tÃ­nh tá»“n quá»¹.
    /// <para>
    /// <paramref name="ngayDauKyToiDa"/> giá»›i háº¡n láº¥y nhá»¯ng khai bÃ¡o cÃ³ ngÃ y Ä‘áº§u ká»³
    /// khÃ´ng vÆ°á»£t quÃ¡ má»‘c Ä‘ang xem. Nhá» váº­y khi xem má»™t ká»³ thÃ¬ quá»¹ Ä‘áº§u ká»³ chá»‰ cá»™ng
    /// nhá»¯ng khai bÃ¡o Ä‘Ã£ cÃ³ trÆ°á»›c ká»³ Ä‘Ã³, cÃ²n khai bÃ¡o cá»§a ká»³ sau khÃ´ng lÃ m lá»‡ch sá»‘.
    /// </para>
    /// </summary>
    public async Task<decimal> GetTongSoDuQuyDauKyAsync(string? phuongThuc = null, DateTime? ngayDauKyToiDa = null)
    {
        try
        {
            await using var connection = await OpenAsync();
            await EnsureBangSoQuyDauKyAsync(connection);

            var sql = "SELECT COALESCE(SUM(so_tien), 0) FROM so_quy_dau_ky WHERE chi_nhanh_id = @ChiNhanhId";
            var param = new DynamicParameters();
            param.Add("ChiNhanhId", Session.CurrentUser?.ChiNhanhId ?? 1);

            if (!string.IsNullOrWhiteSpace(phuongThuc))
            {
                sql += " AND phuong_thuc = @PhuongThuc";
                param.Add("PhuongThuc", phuongThuc.Trim().ToUpperInvariant());
            }

            if (ngayDauKyToiDa.HasValue)
            {
                sql += " AND ngay_dau_ky <= @NgayDauKyToiDa";
                param.Add("NgayDauKyToiDa", ngayDauKyToiDa.Value.Date);
            }

            return await connection.ExecuteScalarAsync<decimal>(sql, param);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i GetTongSoDuQuyDauKyAsync", ex);
            return 0m;
        }
    }

    /// <summary>
    /// CÃ¡c báº£n ghi sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ Ä‘Ã£ khai bÃ¡o cho Ä‘Ãºng má»™t má»‘c ngÃ y Ä‘áº§u ká»³.
    /// DÃ¹ng cho mÃ n Sá»• quá»¹ Ä‘á»ƒ biáº¿t ká»³ Ä‘ang xem Ä‘Ã£ khá»Ÿi táº¡o quá»¹ chÆ°a vÃ  Ä‘iá»n sáºµn
    /// sá»‘ tiá»n vÃ o Ã´ nháº­p khi sá»­a láº¡i.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyDauKy>> GetSoQuyDauKyTheoKyAsync(DateTime ngayDauKy, string? phuongThuc = null)
    {
        try
        {
            await using var connection = await OpenAsync();
            await EnsureBangSoQuyDauKyAsync(connection);

            var sql = """
            SELECT
                id AS Id,
                ngay_dau_ky AS NgayDauKy,
                phuong_thuc AS PhuongThuc,
                so_tien AS SoTien,
                noi_dung AS NoiDung,
                chi_nhanh_id AS ChiNhanhId,
                ngay_tao AS NgayTao
            FROM so_quy_dau_ky
            WHERE chi_nhanh_id = @ChiNhanhId AND ngay_dau_ky = @NgayDauKy
            """;

            var param = new DynamicParameters();
            param.Add("ChiNhanhId", Session.CurrentUser?.ChiNhanhId ?? 1);
            param.Add("NgayDauKy", ngayDauKy.Date);

            if (!string.IsNullOrWhiteSpace(phuongThuc))
            {
                sql += " AND phuong_thuc = @PhuongThuc";
                param.Add("PhuongThuc", phuongThuc.Trim().ToUpperInvariant());
            }

            sql += " ORDER BY phuong_thuc;";

            return (await connection.QueryAsync<SoQuyDauKy>(sql, param)).AsList();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i GetSoQuyDauKyTheoKyAsync", ex);
            return Array.Empty<SoQuyDauKy>();
        }
    }

    /// <summary>
    /// ToÃ n bá»™ cÃ¡c báº£n ghi sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ Ä‘Ã£ khai bÃ¡o, tuá»³ chá»n lá»c theo quá»¹
    /// vÃ  theo má»‘c ngÃ y tá»‘i Ä‘a. DÃ¹ng cho mÃ n Sá»• quá»¹ khi xem "toÃ n thá»i gian"
    /// Ä‘á»ƒ biáº¿t quá»¹ Ä‘áº§u ká»³ Ä‘Ã£ cÃ³ khai bÃ¡o nÃ o hay chÆ°a.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyDauKy>> GetSoQuyDauKyListAsync(
        string? phuongThuc = null,
        DateTime? ngayDauKyToiDa = null)
    {
        try
        {
            await using var connection = await OpenAsync();
            await EnsureBangSoQuyDauKyAsync(connection);

            var sql = """
            SELECT
                id AS Id,
                ngay_dau_ky AS NgayDauKy,
                phuong_thuc AS PhuongThuc,
                so_tien AS SoTien,
                noi_dung AS NoiDung,
                chi_nhanh_id AS ChiNhanhId,
                ngay_tao AS NgayTao
            FROM so_quy_dau_ky
            WHERE chi_nhanh_id = @ChiNhanhId
            """;

            var param = new DynamicParameters();
            param.Add("ChiNhanhId", Session.CurrentUser?.ChiNhanhId ?? 1);

            if (!string.IsNullOrWhiteSpace(phuongThuc))
            {
                sql += " AND phuong_thuc = @PhuongThuc";
                param.Add("PhuongThuc", phuongThuc.Trim().ToUpperInvariant());
            }

            if (ngayDauKyToiDa.HasValue)
            {
                sql += " AND ngay_dau_ky <= @NgayDauKyToiDa";
                param.Add("NgayDauKyToiDa", ngayDauKyToiDa.Value.Date);
            }

            sql += " ORDER BY ngay_dau_ky, phuong_thuc;";

            return (await connection.QueryAsync<SoQuyDauKy>(sql, param)).AsList();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i GetSoQuyDauKyListAsync", ex);
            return Array.Empty<SoQuyDauKy>();
        }
    }

    /// <summary>
    /// XoÃ¡ sá»‘ dÆ° quá»¹ Ä‘áº§u ká»³ Ä‘Ã£ khai bÃ¡o cho má»™t quá»¹ táº¡i má»™t má»‘c ngÃ y.
    /// DÃ¹ng khi ngÆ°á»i dÃ¹ng muá»‘n bá» khai bÃ¡o cá»§a má»™t ká»³ trÃªn mÃ n Sá»• quá»¹.
    /// </summary>
    public async Task<bool> XoaSoQuyDauKyTheoKyAsync(DateTime ngayDauKy, string phuongThuc)
    {
        try
        {
            await using var connection = await OpenAsync();
            await EnsureBangSoQuyDauKyAsync(connection);

            var rows = await connection.ExecuteAsync(
                """
                DELETE FROM so_quy_dau_ky
                WHERE chi_nhanh_id = @ChiNhanhId
                  AND ngay_dau_ky = @NgayDauKy
                  AND phuong_thuc = @PhuongThuc;
                """,
                new
                {
                    ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1,
                    NgayDauKy = ngayDauKy.Date,
                    PhuongThuc = (phuongThuc ?? "").Trim().ToUpperInvariant()
                });

            return rows > 0;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i XoaSoQuyDauKyTheoKyAsync", ex);
            return false;
        }
    }
    #endregion

    #region Thiáº¿t Láº­p GiÃ¡ KhÃ¡ch HÃ ng
    /// <summary>
    /// Äáº£m báº£o báº£ng lÆ°u trá»¯ thiáº¿t láº­p giÃ¡ riÃªng cho khÃ¡ch hÃ ng (gia_ban_khach_hang) tá»“n táº¡i.
    /// </summary>
    public async Task EnsureBangGiaKhachHangAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        try
        {
            const string createTableSql = """
            CREATE TABLE IF NOT EXISTS gia_ban_khach_hang (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                khach_hang_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                don_vi_tinh_id INT UNSIGNED NULL,
                gia_rieng DECIMAL(15, 2) NOT NULL DEFAULT 0,
                chiet_khau DECIMAL(5, 2) NOT NULL DEFAULT 0,
                ghi_chu VARCHAR(255) NULL,
                ngay_ap_dung DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uq_khach_sanpham (khach_hang_id, san_pham_id),
                INDEX idx_gbkh_khach (khach_hang_id),
                INDEX idx_gbkh_sp (san_pham_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(createTableSql, transaction: transaction);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhÃ´ng táº¡o Ä‘Æ°á»£c báº£ng gia_ban_khach_hang", ex);
        }
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch cÃ¡c máº·t hÃ ng Ä‘Ã£ Ä‘Æ°á»£c thiáº¿t láº­p giÃ¡ riÃªng cho má»™t khÃ¡ch hÃ ng.
    /// </summary>
    public async Task<IReadOnlyList<GiaBanKhachHangItem>> GetBangGiaKhachHangAsync(
        uint khachHangId,
        string? search = null,
        uint? nhomSanPhamId = null)
    {
        await using var connection = await OpenAsync();
        await EnsureBangGiaKhachHangAsync(connection);

        var sql = """
        SELECT 
            gb.id AS Id,
            gb.khach_hang_id AS KhachHangId,
            gb.san_pham_id AS SanPhamId,
            sp.ma_san_pham AS MaSanPham,
            sp.ten_san_pham AS TenSanPham,
            COALESCE(dvt.ten_don_vi, 'CÃ¡i') AS DonViTinh,
            sp.don_vi_tinh_id AS DonViTinhId,
            COALESCE(nsp.ten_nhom, sp.nhom_hang_3_cap_excel, 'ChÆ°a phÃ¢n nhÃ³m') AS NhomSanPham,
            sp.nhom_san_pham_id AS NhomSanPhamId,
            COALESCE(sp.gia_ban, 0) AS GiaGoc,
            gb.gia_rieng AS GiaRieng,
            gb.ghi_chu AS GhiChu,
            gb.ngay_ap_dung AS NgayApDung,
            gb.ngay_cap_nhat AS NgayCapNhat
        FROM gia_ban_khach_hang gb
        INNER JOIN san_pham sp ON sp.id = gb.san_pham_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
        WHERE gb.khach_hang_id = @KhachHangId
        """;

        var param = new DynamicParameters();
        param.Add("KhachHangId", khachHangId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (sp.ma_san_pham LIKE @Search OR sp.ten_san_pham LIKE @Search)";
            param.Add("Search", $"%{search.Trim()}%");
        }

        if (nhomSanPhamId.HasValue && nhomSanPhamId.Value > 0)
        {
            if (nhomSanPhamId.Value == 999999)
            {
                sql += " AND (sp.nhom_san_pham_id IS NULL OR sp.nhom_san_pham_id = 0)";
            }
            else
            {
                sql += " AND sp.nhom_san_pham_id = @NhomId";
                param.Add("NhomId", nhomSanPhamId.Value);
            }
        }

        sql += " ORDER BY sp.ten_san_pham, sp.id DESC;";

        var list = (await connection.QueryAsync<GiaBanKhachHangItem>(sql, param)).AsList();
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Stt = i + 1;
        }

        return list;
    }

    /// <summary>
    /// ThÃªm hoáº·c cáº­p nháº­t giÃ¡ bÃ¡n riÃªng cho má»™t sáº£n pháº©m cá»§a khÃ¡ch hÃ ng.
    /// KhÃ´ng lÃ m thay Ä‘á»•i giÃ¡ bÃ¡n chung cá»§a sáº£n pháº©m trong báº£ng san_pham.
    /// </summary>
    public async Task<bool> LuuGiaBanKhachHangAsync(
        uint khachHangId,
        uint sanPhamId,
        decimal giaRieng,
        string? ghiChu = null,
        DateTime? ngayApDung = null)
    {
        if (khachHangId == 0 || sanPhamId == 0)
            throw new ArgumentException("MÃ£ khÃ¡ch hÃ ng vÃ  mÃ£ sáº£n pháº©m khÃ´ng há»£p lá»‡.");

        if (giaRieng < 0)
            throw new ArgumentException("GiÃ¡ bÃ¡n riÃªng khÃ´ng Ä‘Æ°á»£c nhá» hÆ¡n 0.");

        await using var connection = await OpenAsync();
        await EnsureBangGiaKhachHangAsync(connection);

        const string sql = """
        INSERT INTO gia_ban_khach_hang
        (
            khach_hang_id, san_pham_id, gia_rieng, ghi_chu, ngay_ap_dung
        )
        VALUES
        (
            @KhachHangId, @SanPhamId, @GiaRieng, @GhiChu, @NgayApDung
        )
        ON DUPLICATE KEY UPDATE
            gia_rieng = @GiaRieng,
            ghi_chu = @GhiChu,
            ngay_ap_dung = @NgayApDung,
            ngay_cap_nhat = NOW();
        """;

        var affected = await connection.ExecuteAsync(
            sql,
            new
            {
                KhachHangId = khachHangId,
                SanPhamId = sanPhamId,
                GiaRieng = giaRieng,
                GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim(),
                NgayApDung = ngayApDung ?? DateTime.Now
            });

        _ = LogSubAccountActivityAsync(
            "Thiáº¿t láº­p giÃ¡",
            "GIA_BAN_KHACH_HANG",
            khachHangId,
            $"SP{sanPhamId}",
            $"Thiáº¿t láº­p giÃ¡ bÃ¡n riÃªng: {giaRieng:N0} Ä‘ cho sáº£n pháº©m #{sanPhamId} (KhÃ¡ch #{khachHangId})",
            giaRieng);

        return affected > 0;
    }

    /// <summary>
    /// XÃ³a thiáº¿t láº­p giÃ¡ bÃ¡n riÃªng Ä‘á»ƒ máº·t hÃ ng quay vá» Ã¡p dá»¥ng giÃ¡ chung.
    /// </summary>
    public async Task<bool> XoaGiaBanKhachHangAsync(uint id)
    {
        await using var connection = await OpenAsync();
        await EnsureBangGiaKhachHangAsync(connection);

        var affected = await connection.ExecuteAsync(
            "DELETE FROM gia_ban_khach_hang WHERE id = @Id;",
            new { Id = id });

        return affected > 0;
    }

    /// <summary>
    /// Láº¥y giÃ¡ bÃ¡n riÃªng cá»§a má»™t sáº£n pháº©m Ä‘á»‘i vá»›i khÃ¡ch hÃ ng (phá»¥c vá»¥ kiá»ƒm tra nhanh khi bÃ¡n hÃ ng).
    /// </summary>
    public async Task<decimal?> LayGiaRiengKhachHangAsync(uint khachHangId, uint sanPhamId)
    {
        if (khachHangId == 0 || sanPhamId == 0)
            return null;

        try
        {
            await using var connection = await OpenAsync();
            await EnsureBangGiaKhachHangAsync(connection);

            const string sql = """
            SELECT gia_rieng
            FROM gia_ban_khach_hang
            WHERE khach_hang_id = @KhachHangId AND san_pham_id = @SanPhamId
            LIMIT 1;
            """;

            return await connection.QueryFirstOrDefaultAsync<decimal?>(
                sql,
                new { KhachHangId = khachHangId, SanPhamId = sanPhamId });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Láº¥y táº¥t cáº£ giÃ¡ riÃªng cá»§a khÃ¡ch hÃ ng dÆ°á»›i dáº¡ng tá»« Ä‘iá»ƒn { SanPhamId => GiaRieng }
    /// Ä‘á»ƒ Ã¡p dá»¥ng tá»± Ä‘á»™ng cho giá» hÃ ng khi chá»n khÃ¡ch hÃ ng.
    /// </summary>
    public async Task<Dictionary<uint, decimal>> LayTatCaGiaRiengKhachHangAsync(uint khachHangId)
    {
        var result = new Dictionary<uint, decimal>();
        if (khachHangId == 0)
            return result;

        try
        {
            await using var connection = await OpenAsync();
            await EnsureBangGiaKhachHangAsync(connection);

            const string sql = """
            SELECT san_pham_id AS SanPhamId, gia_rieng AS GiaRieng
            FROM gia_ban_khach_hang
            WHERE khach_hang_id = @KhachHangId;
            """;

            var pairs = await connection.QueryAsync<(uint SanPhamId, decimal GiaRieng)>(
                sql,
                new { KhachHangId = khachHangId });

            foreach (var p in pairs)
            {
                result[p.SanPhamId] = p.GiaRieng;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lá»—i LayTatCaGiaRiengKhachHangAsync", ex);
        }

        return result;
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch khÃ¡ch hÃ ng thuá»™c má»™t nhÃ³m khÃ¡ch hÃ ng cá»¥ thá»ƒ.
    /// </summary>
    public async Task<IReadOnlyList<KhachHang>> GetKhachHangTheoNhomAsync(uint nhomId)
    {
        await using var connection = await OpenAsync();
        const string sql = """
        SELECT
            kh.id AS Id,
            kh.ma_khach_hang AS MaKhachHang,
            kh.ten_khach_hang AS TenKhachHang,
            kh.loai_khach_hang AS LoaiKhachHang,
            kh.so_dien_thoai AS SoDienThoai,
            kh.dia_chi AS DiaChi,
            kh.nhom_khach_hang_id AS NhomKhachHangId,
            nkh.ten_nhom AS NhomKhachHang
        FROM khach_hang kh
        LEFT JOIN nhom_khach_hang nkh ON nkh.id = kh.nhom_khach_hang_id
        WHERE kh.trang_thai = 1 AND kh.nhom_khach_hang_id = @NhomId
        ORDER BY kh.ten_khach_hang;
        """;

        return (await connection.QueryAsync<KhachHang>(sql, new { NhomId = nhomId })).AsList();
    }

    /// <summary>
    /// Thiáº¿t láº­p giÃ¡ riÃªng hÃ ng loáº¡t cho nhiá»u khÃ¡ch hÃ ng vÃ  nhiá»u sáº£n pháº©m cÃ¹ng lÃºc.
    /// DÃ¹ng transaction Ä‘áº£m báº£o toÃ n váº¹n.
    /// </summary>
    public async Task<int> LuuGiaBanKhachHangHangLoatAsync(
        IReadOnlyList<uint> khachHangIds,
        IReadOnlyList<(uint SanPhamId, decimal GiaRieng, string? GhiChu, DateTime? NgayApDung)> danhSachGia)
    {
        if (khachHangIds == null || khachHangIds.Count == 0 || danhSachGia == null || danhSachGia.Count == 0)
            return 0;

        await using var connection = await OpenAsync();
        await EnsureBangGiaKhachHangAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        const string sql = """
        INSERT INTO gia_ban_khach_hang
        (
            khach_hang_id, san_pham_id, gia_rieng, ghi_chu, ngay_ap_dung
        )
        VALUES
        (
            @KhachHangId, @SanPhamId, @GiaRieng, @GhiChu, @NgayApDung
        )
        ON DUPLICATE KEY UPDATE
            gia_rieng = @GiaRieng,
            ghi_chu = @GhiChu,
            ngay_ap_dung = @NgayApDung,
            ngay_cap_nhat = NOW();
        """;

        var totalSaved = 0;
        try
        {
            foreach (var khId in khachHangIds)
            {
                if (khId == 0) continue;

                foreach (var item in danhSachGia)
                {
                    if (item.SanPhamId == 0 || item.GiaRieng < 0)
                        continue;

                    var affected = await connection.ExecuteAsync(
                        sql,
                        new
                        {
                            KhachHangId = khId,
                            SanPhamId = item.SanPhamId,
                            GiaRieng = item.GiaRieng,
                            GhiChu = string.IsNullOrWhiteSpace(item.GhiChu) ? null : item.GhiChu.Trim(),
                            NgayApDung = item.NgayApDung ?? DateTime.Now
                        },
                        transaction: transaction);

                    if (affected > 0) totalSaved++;
                }
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Thiáº¿t láº­p giÃ¡ theo nhÃ³m",
                "GIA_BAN_KHACH_HANG",
                null,
                null,
                $"Thiáº¿t láº­p giÃ¡ riÃªng cho {khachHangIds.Count} khÃ¡ch hÃ ng vá»›i {danhSachGia.Count} sáº£n pháº©m ({totalSaved} báº£n ghi)",
                null);

            return totalSaved;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lá»—i LuuGiaBanKhachHangHangLoatAsync", ex);
            throw;
        }
    }

    #endregion
}



