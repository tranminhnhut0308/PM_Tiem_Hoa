using System.Data;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed class DatabaseService
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
    private bool _daBackfillNhomSoQuy;

    public DatabaseService()
    {
        _configService = new DatabaseConfigService();
    }

    /// <summary>
    /// Mở kết nối mới tới database. Công khai để các service khác (ví dụ
    /// <see cref="DashboardService"/>) dùng chung đúng một cách cấu hình kết nối
    /// thay vì tự dựng connection string riêng.
    /// </summary>
    public async Task<MySqlConnection> TaoKetNoiAsync()
    {
        var connection = new MySqlConnection(GetConnectionString());

        await connection.OpenAsync();

        return connection;
    }

    private string GetConnectionString()
    {
        var config = _configService.Load();

        return
            $"Server={config.Server};" +
            $"Port={config.Port};" +
            $"Database={config.Database};" +
            $"User ID={config.User};" +
            $"Password={config.Password};" +
            $"SslMode={config.SslMode};" +
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
        try
        {
            await using var connection = await OpenAsync();

            await connection.ExecuteScalarAsync<int>("SELECT 1;");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== DATABASE ERROR ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("====================================");

            throw;
        }
    }

    // Mở sẵn một kết nối để MySqlConnector đưa vào connection pool.
    // Lần đăng nhập sau đó có thể dùng lại kết nối này thay vì bắt tay lại với server.
    public async Task WarmUpAsync()
    {
        await using var connection = await OpenAsync();
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

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { TenDangNhap = tenDangNhap });

        if (row is null)
        {
            return new LoginResultExtended { IsValid = false, IsLocked = false };
        }

        var user = new NguoiDung
        {
            Id = (uint)row.Id,
            MaNguoiDung = row.MaNguoiDung,
            TenDangNhap = row.TenDangNhap,
            MatKhau = row.MatKhau,
            HoTen = row.HoTen,
            SoDienThoai = row.SoDienThoai,
            Email = row.Email,
            ChiNhanhId = row.ChiNhanhId,
            TenChiNhanh = row.TenChiNhanh,
            VaiTro = row.VaiTro,
            MaVaiTro = row.MaVaiTro
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
            return new LoginResultExtended { IsValid = false, IsLocked = true, ErrorMessage = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." };
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
        catch (Exception ex)
        {
            // Không để lỗi ghi nhật ký làm ảnh hưởng đến phiên đăng nhập hợp lệ.
            Console.WriteLine($"Không thể cập nhật lần đăng nhập cuối: {ex.Message}");
        }
    }

    /// <summary>
    /// Số hàng hóa tối đa được đọc trong một lần tải danh sách.
    /// Màn hình "Hàng hóa" hiện chưa có phân trang nên lấy đủ danh sách để người dùng xem.
    /// </summary>
    public const int SoDongSanPhamToiDa = 1000;

    /// <summary>
    /// Lấy danh sách hàng hóa. Từ khóa được MySQL đối chiếu trực tiếp trên TOÀN BỘ bảng
    /// <c>san_pham</c>, chỉ sau đó mới áp dụng LIMIT, nên sản phẩm nằm ngoài phần đang
    /// hiển thị vẫn tìm ra được (không lấy sẵn một phần rồi mới lọc).
    /// </summary>
    public async Task<IReadOnlyList<SanPham>> GetSanPhamAsync(string? search = null)
        => (await GetSanPhamPagedAsync(search, 1, SoDongSanPhamToiDa)).Items;

    /// <summary>
    /// Lấy một trang hàng hóa kèm tổng số bản ghi khớp từ khóa.
    /// Tổng số bản ghi được đếm trên toàn database nên không phụ thuộc số dòng của trang,
    /// dùng cho phân trang và cho thông điệp "Tìm thấy N sản phẩm".
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

        // Từ khóa được tách thành từng từ, mỗi từ phải xuất hiện trong một trong các
        // cột tìm kiếm (AND giữa các từ). Nhờ vậy gõ "Hồng mua bên" vẫn ra sản phẩm
        // "hồng ( mua bên VL )" dù hai từ không nối liền nhau.
        // Không có từ khóa thì không thêm điều kiện nào để MySQL đọc bảng như bình thường.
        var parameters = new DynamicParameters();

        var dieuKien = TaoDieuKienTimKiemSanPham(
            parameters,
            keyword,
            "sp.ma_san_pham",
            "sp.ma_vach",
            "sp.ten_san_pham");

        var whereSql = dieuKien.Length == 0 ? "" : "WHERE " + dieuKien;

        // Khớp chính xác mã hàng / mã vạch thì đưa lên đầu, tên hàng bắt đầu bằng
        // từ khóa thì kế sau, còn lại sắp theo tên hàng. Điều kiện này so sánh trên
        // biểu thức đã chuẩn hóa y hệt điều kiện lọc ở trên.
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
                COALESCE(sp.ton_kho_excel, COALESCE(tk.so_luong_ton, 0)) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                COALESCE(nsp.ten_nhom, NULLIF(TRIM(sp.nhom_hang_3_cap_excel), '')) AS NhomSanPham,
                COALESCE(NULLIF(TRIM(sp.loai_hang_excel), ''), 'Hàng hóa') AS LoaiHang,
                COALESCE(NULLIF(TRIM(sp.nhom_hang_3_cap_excel), ''), nsp.ten_nhom) AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
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

        // Chỉ dùng bảng import cũ khi bảng san_pham không có dữ liệu.
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
    /// Đọc lại MỘT hàng hóa kèm tên ĐVT / nhóm hàng (qua JOIN) sau khi sửa, để
    /// màn hình chỉ cập nhật đúng dòng vừa sửa mà không phải nạp lại cả danh sách.
    /// Trả về null nếu không còn tìm thấy sản phẩm.
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
                COALESCE(sp.ton_kho_excel, COALESCE(tk.so_luong_ton, 0)) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                COALESCE(nsp.ten_nhom, NULLIF(TRIM(sp.nhom_hang_3_cap_excel), '')) AS NhomSanPham,
                COALESCE(NULLIF(TRIM(sp.loai_hang_excel), ''), 'Hàng hóa') AS LoaiHang,
                COALESCE(NULLIF(TRIM(sp.nhom_hang_3_cap_excel), ''), nsp.ten_nhom) AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            WHERE sp.id = @Id
            LIMIT 1
            """;

        return await connection.QueryFirstOrDefaultAsync<SanPham>(sql, new { Id = id });
    }

    /// <summary>
    /// Các cặp ký tự tiếng Việt cần bỏ dấu khi so sánh tìm kiếm.
    /// Đặt "đ"/"Đ" lên đầu vì đây là ký tự duy nhất mà chính collation
    /// utf8mb4_unicode_ci của MySQL/MariaDB KHÔNG coi là bằng "d"/"D"
    /// (đã kiểm tra trên MariaDB 10.1: <c>'đ' = 'd'</c> trả về FALSE).
    /// </summary>
    private static readonly (string CoDau, string KhongDau)[] BangChuanHoaTiengViet =
    [
        ("đ", "d"), ("Đ", "D"),
        ("á", "a"), ("à", "a"), ("ả", "a"), ("ã", "a"), ("ạ", "a"),
        ("â", "a"), ("ấ", "a"), ("ầ", "a"), ("ẩ", "a"), ("ẫ", "a"), ("ậ", "a"),
        ("ă", "a"), ("ắ", "a"), ("ằ", "a"), ("ẳ", "a"), ("ẵ", "a"), ("ặ", "a"),
        ("é", "e"), ("è", "e"), ("ẻ", "e"), ("ẽ", "e"), ("ẹ", "e"),
        ("ê", "e"), ("ế", "e"), ("ề", "e"), ("ể", "e"), ("ễ", "e"), ("ệ", "e"),
        ("í", "i"), ("ì", "i"), ("ỉ", "i"), ("ĩ", "i"), ("ị", "i"),
        ("ó", "o"), ("ò", "o"), ("ỏ", "o"), ("õ", "o"), ("ọ", "o"),
        ("ô", "o"), ("ố", "o"), ("ồ", "o"), ("ổ", "o"), ("ỗ", "o"), ("ộ", "o"),
        ("ơ", "o"), ("ớ", "o"), ("ờ", "o"), ("ở", "o"), ("ỡ", "o"), ("ợ", "o"),
        ("ú", "u"), ("ù", "u"), ("ủ", "u"), ("ũ", "u"), ("ụ", "u"),
        ("ư", "u"), ("ứ", "u"), ("ừ", "u"), ("ử", "u"), ("ữ", "u"), ("ự", "u"),
        ("ý", "y"), ("ỳ", "y"), ("ỷ", "y"), ("ỹ", "y"), ("ỵ", "y")
    ];

    /// <summary>
    /// Biểu thức SQL chuẩn hóa một cột về dạng "chữ thường không dấu" để so sánh.
    /// <para>
    /// MySQL/MariaDB không có hàm bỏ dấu sẵn có, và collation utf8mb4_unicode_ci dù
    /// bỏ được hầu hết dấu thì vẫn KHÔNG bỏ được "đ" — nên gõ "cuc mau don trang"
    /// sẽ không tìm ra "Cúc mẫu đơn trắng". Chuỗi REPLACE ở đây bỏ dấu cả "đ"
    /// lẫn các dấu còn lại, để việc tìm kiếm không phụ thuộc collation của server.
    /// </para>
    /// <para>
    /// Cột rỗng/NULL trả về chuỗi rỗng nên không bao giờ loại mất sản phẩm.
    /// Biểu thức được dựng sẵn một lần cho mỗi tên cột vì nó là hằng số.
    /// </para>
    /// </summary>
    private static string BieuThucChuanHoaSql(string cot)
        => "COALESCE(" + ChuoiReplaceBoDau($"LOWER({cot})") + ", '')";

    /// <summary>Quy đổi ký tự có dấu thành ký tự gốc, bọc trong COALESCE để NULL thành chuỗi rỗng.</summary>
    private static string ChuoiReplaceBoDau(string bieuThuc)
    {
        foreach (var (coDau, khongDau) in BangChuanHoaTiengViet)
            bieuThuc = $"REPLACE({bieuThuc}, '{coDau}', '{khongDau}')";

        return bieuThuc;
    }

    /// <summary>
    /// Chuẩn hóa từ khóa ở C# theo ĐÚNG quy tắc mà biểu thức SQL áp dụng cho cột:
    /// hạ chữ thường, bỏ dấu tiếng Việt (kể cả đ/Đ), gộp mọi kiểu khoảng trắng
    /// (space, tab, xuống dòng, khoảng trắng không ngắt dòng NBSP, ...) thành một
    /// dấu cách rồi cắt bỏ khoảng trắng đầu/cuối.
    /// <para>
    /// Hai vế dùng chung một quy tắc nên kết quả không còn phụ thuộc collation,
    /// người dùng gõ "Cúc mẫu đơn trắng hàng A" hay "cuc mau don trang hang a"
    /// đều ra cùng một danh sách.
    /// </para>
    /// </summary>
    private static string ChuauHoaTuKhoa(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return string.Empty;

        // FormD tách chữ có dấu thành chữ gốc + dấu phụ, bỏ dấu phụ đi là xong.
        var khongDau = keyword.Trim().Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(khongDau.Length);

        foreach (var character in khongDau)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(character))
                builder.Append(' ');
            else if (character == 'đ')
                builder.Append('d');
            else if (character == 'Đ')
                builder.Append('D');
            else
                builder.Append(char.ToLowerInvariant(character));
        }

        return string.Join(' ', builder.ToString().Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Tách từ khóa đã chuẩn hóa thành các từ cần có trong sản phẩm.
    /// Mọi ký tự không phải chữ/số (dấu phẩy, gạch nối, ngoặc, dấu chấm câu)
    /// đều bị coi là ranh giới để "Cúc mẫu, đơn trắng" vẫn ra kết quả đúng.
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
    /// Dựng điều kiện tìm kiếm hàng hóa và gắn các tham số vào
    /// <paramref name="parameters"/>.
    /// Từ khóa được tách thành từng từ, mỗi từ phải xuất hiện trong một trong các
    /// cột được tìm, các từ nối với nhau bằng AND (giữ được ý cũ: gõ "Hồng mua bên"
    /// vẫn ra sản phẩm "hồng ( mua bên VL )" dù hai từ không nối liền nhau).
    /// <para>
    /// Cả từ khóa lẫn cột đều được chuẩn hóa về chữ thường không dấu trước khi
    /// so sánh, nên không phân biệt hoa/thường, không phân biệt dấu tiếng Việt
    /// (kể cả đ/Đ) và không phụ thuộc collation của server.
    /// </para>
    /// <para>
    /// Mã vạch và các cột khác có thể NULL vẫn được giữ lại: COALESCE đưa NULL
    /// thành chuỗi rỗng nên sản phẩm thiếu mã vạch không bị loại vì lý do đó.
    /// </para>
    /// Trả về chuỗi rỗng khi không có từ khóa để người gọi tự ghép vào WHERE.
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
    /// Điều kiện sắp xếp khi đang tìm kiếm: mã hàng/mã vạch khớp chính xác đưa lên
    /// đầu, tên hàng bắt đầu bằng từ khóa kế sau, còn lại sắp theo tên hàng.
    /// So sánh cũng dùng biểu thức chuẩn hóa để không lệch với điều kiện lọc.
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
    /// Bảng import Excel cũ: chỉ đọc khi bảng san_pham chưa có dữ liệu.
    /// Dùng chung cách tìm kiếm với bảng san_pham để hành vi nhất quán.
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
            "`Mã hàng`",
            "`Mã vạch`",
            "`Tên hàng`");

        var whereSql = "WHERE COALESCE(`Đang kinh doanh`, 1) = 1"
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
                `Mã hàng` AS MaSanPham,
                `Mã vạch` AS MaVach,
                COALESCE(`Tên hàng`, `Mã hàng`) AS TenSanPham,
                COALESCE(`Giá bán`, 0) AS GiaBan,
                COALESCE(`Giá vốn`, 0) AS GiaVon,
                COALESCE(`Tồn kho`, 0) AS TonKho,
                COALESCE(`Tồn kho`, 0) AS TonKhoExcel,
                COALESCE(`Tồn nhỏ nhất`, 0) AS TonToiThieu,
                COALESCE(`Tồn lớn nhất`, 0) AS TonToiDa,
                COALESCE(`KH đặt`, 0) AS KhachDat,
                `Thời gian tạo` AS NgayTao,
                `Dự kiến hết hàng` AS DuKienHetHang,
                `ĐVT` AS DonViTinh,
                `Nhóm hàng(3 Cấp)` AS NhomSanPham,
                `Loại hàng` AS LoaiHang,
                `Nhóm hàng(3 Cấp)` AS NhomHangBaCap,
                `Vị trí` AS ViTriKho,
                COALESCE(`Đang kinh doanh`, 1) AS DangKinhDoanh,
                COALESCE(`Được bán trực tiếp`, 1) AS DuocBanTrucTiep
            FROM import_san_pham_excel
            {whereSql}
            ORDER BY `Tên hàng`
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
            // Bảng import cũ không tồn tại -> coi như không có dữ liệu.
            return (Array.Empty<SanPham>(), 0);
        }
    }

    public async Task<IReadOnlyList<ProductOption>> GetNhomSanPhamAsync()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<ProductOption>(
            "SELECT id AS Id, ten_nhom AS Name FROM nhom_san_pham WHERE trang_thai = 1 ORDER BY ten_nhom;"
        )).AsList();
    }

    public async Task<IReadOnlyList<ProductOption>> GetDonViTinhAsync()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<ProductOption>(
            "SELECT id AS Id, ten_don_vi AS Name FROM don_vi_tinh WHERE trang_thai = 1 ORDER BY ten_don_vi;"
        )).AsList();
    }

    public async Task<ProductOption> ThemNhomSanPhamAsync(string tenNhom)
    {
        var ten = tenNhom.Trim();
        if (string.IsNullOrWhiteSpace(ten))
            throw new ArgumentException("Tên nhóm hàng không được để trống.", nameof(tenNhom));

        await using var connection = await OpenAsync();

        var existing = await connection.QueryFirstOrDefaultAsync<ProductOption>(
            "SELECT id AS Id, ten_nhom AS Name FROM nhom_san_pham WHERE LOWER(TRIM(ten_nhom)) = LOWER(@Ten) LIMIT 1;",
            new { Ten = ten });

        if (existing != null)
        {
            await connection.ExecuteAsync(
                "UPDATE nhom_san_pham SET trang_thai = 1 WHERE id = @Id;",
                new { Id = existing.Id });
            return new ProductOption { Id = existing.Id, Name = existing.Name };
        }

        bool hasMaNhom = false;
        try
        {
            hasMaNhom = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'nhom_san_pham' AND LOWER(COLUMN_NAME) = 'ma_nhom';") > 0;
        }
        catch { }

        uint newId;
        if (hasMaNhom)
        {
            var maNhom = "N" + DateTime.Now.ToString("yyMMddHHmmss");
            await connection.ExecuteAsync(
                "INSERT INTO nhom_san_pham (ma_nhom, ten_nhom, trang_thai) VALUES (@Ma, @Ten, 1);",
                new { Ma = maNhom, Ten = ten });
        }
        else
        {
            await connection.ExecuteAsync(
                "INSERT INTO nhom_san_pham (ten_nhom, trang_thai) VALUES (@Ten, 1);",
                new { Ten = ten });
        }

        newId = await connection.ExecuteScalarAsync<uint>("SELECT LAST_INSERT_ID();");
        if (newId == 0)
        {
            newId = await connection.ExecuteScalarAsync<uint>(
                "SELECT id FROM nhom_san_pham WHERE LOWER(TRIM(ten_nhom)) = LOWER(@Ten) ORDER BY id DESC LIMIT 1;",
                new { Ten = ten });
        }

        return new ProductOption { Id = newId, Name = ten };
    }

    public async Task<ProductOption> ThemDonViTinhAsync(string tenDonVi, string? maDonVi = null)
    {
        var ten = tenDonVi.Trim();
        if (string.IsNullOrWhiteSpace(ten))
            throw new ArgumentException("Tên đơn vị tính không được để trống.", nameof(tenDonVi));

        await using var connection = await OpenAsync();

        var existing = await connection.QueryFirstOrDefaultAsync<ProductOption>(
            "SELECT id AS Id, ten_don_vi AS Name FROM don_vi_tinh WHERE LOWER(TRIM(ten_don_vi)) = LOWER(@Ten) LIMIT 1;",
            new { Ten = ten });

        if (existing != null)
        {
            await connection.ExecuteAsync(
                "UPDATE don_vi_tinh SET trang_thai = 1 WHERE id = @Id;",
                new { Id = existing.Id });
            return new ProductOption { Id = existing.Id, Name = existing.Name };
        }

        bool hasMaDvt = false;
        try
        {
            hasMaDvt = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'don_vi_tinh' AND LOWER(COLUMN_NAME) = 'ma_don_vi';") > 0;
        }
        catch { }

        uint newId;
        if (hasMaDvt)
        {
            var ma = !string.IsNullOrWhiteSpace(maDonVi) ? maDonVi.Trim() : ("DVT" + DateTime.Now.ToString("yyMMddHHmmss"));
            await connection.ExecuteAsync(
                "INSERT INTO don_vi_tinh (ma_don_vi, ten_don_vi, trang_thai) VALUES (@Ma, @Ten, 1);",
                new { Ma = ma, Ten = ten });
        }
        else
        {
            await connection.ExecuteAsync(
                "INSERT INTO don_vi_tinh (ten_don_vi, trang_thai) VALUES (@Ten, 1);",
                new { Ten = ten });
        }

        newId = await connection.ExecuteScalarAsync<uint>("SELECT LAST_INSERT_ID();");
        if (newId == 0)
        {
            newId = await connection.ExecuteScalarAsync<uint>(
                "SELECT id FROM don_vi_tinh WHERE LOWER(TRIM(ten_don_vi)) = LOWER(@Ten) ORDER BY id DESC LIMIT 1;",
                new { Ten = ten });
        }

        return new ProductOption { Id = newId, Name = ten };
    }

    public async Task CapNhatHinhAnhSanPhamAsync(uint productId, string imagePath)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsEdit, "Bạn không có quyền cập nhật hàng hóa.");
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsCreate, "Bạn không có quyền thêm hàng hóa.");
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(product.LoaiHang))
        {
            product.LoaiHang = "Hàng hóa";
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
        _ = LogSubAccountActivityAsync("Thêm hàng hóa", "SAN_PHAM", newId, product.MaSanPham, $"Thêm sản phẩm mới: {product.TenSanPham} ({product.MaSanPham})", product.GiaBan);
        return newId;
    }

    public async Task CapNhatSanPhamAsync(SanPham product)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsEdit, "Bạn không có quyền sửa hàng hóa.");
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(product.LoaiHang))
        {
            product.LoaiHang = "Hàng hóa";
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

        // Trường nào người dùng thiếu quyền chi tiết tương ứng thì bỏ hẳn khỏi
        // câu UPDATE (giá bán -> sua_gia_ban, giá vốn -> sua_gia_von, tồn kho ->
        // dieu_chinh_ton_kho). Nhờ vậy giá trị đang có trong database được giữ
        // nguyên, kể cả khi hàm này bị gọi thẳng thay vì qua giao diện đã khóa ô.
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

        _ = LogSubAccountActivityAsync("Cập nhật hàng hóa", "SAN_PHAM", product.Id, product.MaSanPham, $"Cập nhật sản phẩm: {product.TenSanPham} ({product.MaSanPham})", product.GiaBan);
    }
    /// <summary>
    /// Chỉ cập nhật đúng cột <c>san_pham.gia_ban</c> của một sản phẩm.
    /// Không đụng tới mã hàng, tồn kho, giá vốn hay các cột khác.
    /// Giá bán bằng 0 vẫn hợp lệ (sản phẩm chưa định giá).
    /// <para>
    /// Trả về false khi sản phẩm không còn tồn tại (bị người khác xóa)
    /// để lớp giao diện báo lỗi và tải lại danh sách thay vì crash.
    /// </para>
    /// </summary>
    public async Task<bool> CapNhatGiaBanSanPhamAsync(
        uint productId,
        decimal giaBan)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsUpdatePrice, "Bạn không có quyền cập nhật giá bán hàng hóa.");
        if (productId == 0)
            throw new ArgumentException(
                "Sản phẩm chưa có mã dữ liệu để cập nhật giá bán.",
                nameof(productId));

        if (giaBan < 0)
            throw new ArgumentException(
                "Giá bán không được âm.",
                nameof(giaBan));

        if (giaBan > Helpers.MoneyInput.GiaTienToiDa)
            throw new ArgumentOutOfRangeException(
                nameof(giaBan),
                "Giá bán vượt quá giới hạn của cột DECIMAL(15,2).");

        await using var connection = await OpenAsync();

        // Parameterized query: giá bán do người dùng nhập không bao giờ nối vào SQL.
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

        // MySQL trả 0 dòng bị ảnh hưởng khi sản phẩm không tồn tại.
        return affectedRows > 0;
    }

    /// <summary>
    /// Cập nhật nhanh số lượng tồn kho của một sản phẩm (dùng trong màn hình bán hàng
    /// khi sản phẩm hết hàng và người dùng muốn bổ sung tồn để bán ngay).
    /// Cập nhật bảng ton_kho, san_pham.ton_kho_excel và ghi nhật ký lịch sử kho.
    /// </summary>
    public async Task<bool> CapNhatTonKhoSanPhamAsync(
        uint productId,
        decimal soLuongTonMoi,
        uint? chiNhanhId = null)
    {
        if (productId == 0)
            throw new ArgumentException(
                "Sản phẩm chưa có mã dữ liệu để cập nhật tồn kho.",
                nameof(productId));

        if (soLuongTonMoi < 0)
            throw new ArgumentException(
                "Số lượng tồn kho không được âm.",
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
                        GhiChu = "Chỉnh sửa tồn kho nhanh tại giao diện bán hàng"
                    },
                    transaction);
            }
            catch (Exception exLichSu)
            {
                SafeLog.WriteError("Ghi lich su ton kho nhanh", exLichSu);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Chỉnh tồn kho nhanh",
                "SAN_PHAM",
                productId,
                null,
                $"Cập nhật tồn kho sản phẩm ID {productId} từ {tonTruoc:N0} thành {soLuongTonMoi:N0}",
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsDelete, "Bạn không có quyền xóa hàng hóa.");
        await using var connection = await OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM san_pham WHERE id = @Id;",
            new { Id = productId });
    }

    /// <summary>
    /// Đưa ký tự xuống dòng trong SQL về dạng LF.
    /// <para>
    /// Cần thiết vì các hằng SQL viết dạng raw string literal giữ nguyên CRLF của file
    /// nguồn. MariaDB chấp nhận CR trong danh sách cột của INSERT (nằm trong ngoặc
    /// tròn) nhưng lại báo lỗi cú pháp khi CR xuất hiện trong mệnh đề SET — mỗi lần
    /// thay thế sản phẩm trùng đều hỏng vì lý do này.
    /// </para>
    /// </summary>
    private static string ChuanHoaDongMoiSql(string sql)
    {
        return sql.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>Cột của bảng san_pham được ghi khi nhập từ file, dùng chung cho INSERT và UPDATE.</summary>
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
    /// Cặp "cột = tham số" cho mệnh đề SET của UPDATE, sinh ra từ chính
    /// <see cref="CotNhapSanPham"/> và <see cref="GiaTriNhapSanPham"/>.
    /// <para>
    /// UPDATE không dùng lại được danh sách cột trần của INSERT: "SET ma_san_pham,
    /// ma_vach, ..." là cú pháp sai, phải là "SET ma_san_pham = @MaSanPham,
    /// ma_vach = @MaVach, ...". Sinh tự động từ hai danh sách trên để hai bên
    /// không thể lệch nhau — lệch chỗ này là mọi lần thay thế sản phẩm trùng đều hỏng.
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
                $"Danh sách cột ({cot.Length}) và danh sách giá trị ({giaTri.Length}) "
                + "của phép nhập sản phẩm không khớp nhau.");

        return ChuanHoaDongMoiSql(
            string.Join(",\n", cot.Zip(giaTri, (c, v) => $"{c} = {v}")));
    }

    /// <summary>Độ dài tối đa của các cột varchar trong bảng san_pham (được xác nhận từ schema).</summary>
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
    /// Tra cứu sản phẩm trùng trong database theo MÃ HÀNG (khóa chính) và MÃ VẠCH.
    /// <para>
    /// Cố tình KHÔNG so tên hàng: hai món hàng khác nhau rất dễ trùng tên, coi tên là
    /// khóa trùng sẽ âm thầm bỏ sót sản phẩm hợp lệ.
    /// </para>
    /// <para>
    /// Truy vấn chạy trên database với danh sách mã cần tra cứu, không tải toàn bộ
    /// bảng về bộ nhớ, nên vẫn nhanh khi file có hàng nghìn dòng.
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

        // Tự dựng danh sách tham số cho mệnh đề IN thay vì đưa cả mảng vào một tham số.
        // (Dapper coi mảng là một giá trị duy nhất nên SQL sẽ thành IN (('a','b'))
        // và MariaDB báo "Operand should contain 1 column".)
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
    /// Ghi danh sách sản phẩm xuống database trong MỘT transaction.
    /// <list type="bullet">
    /// <item>Chưa có trong database -> INSERT bản ghi mới.</item>
    /// <item>Đã có và người dùng chọn thay thế -> UPDATE đúng bản ghi cũ, không tạo bản ghi mới.</item>
    /// <item>Đã có và người dùng chọn giữ -> không ghi gì.</item>
    /// <item>Dòng lỗi -> ghi nhận lỗi và tiếp tục dòng sau, không làm hỏng cả lượt nhập.</item>
    /// </list>
    /// Nếu xảy ra lỗi nghiêm trọng (mất kết nối, bảng không tồn tại...) thì rollback
    /// toàn bộ để database không bị để lại trạng thái nhập dở dang.
    /// </summary>
    /// <summary>
    /// Ghi danh sách sản phẩm xuống database trong một transaction.
    /// </summary>
    /// <param name="tenFile">Tên file, chỉ để hiển thị trong báo cáo.</param>
    /// <param name="danhSachHang">Danh sách thao tác đã được quyết định cho từng dòng.</param>
    /// <param name="tongSoDongExcel">Tổng số dòng dữ liệu có trong file.</param>
    /// <param name="loiDocFile">
    /// Các dòng đã lỗi ngay khi đọc file (không đọc được ô, thiếu cả mã lẫn tên).
    /// Được gộp vào báo cáo để không có dòng nào của file biến mất im lặng.
    /// </param>
    public async Task<KetQuaImportSanPham> ImportSanPhamAsync(
        string tenFile,
        IReadOnlyList<HangNhapSanPham> danhSachHang,
        int tongSoDongExcel,
        IReadOnlyCollection<LoiDongNhap>? loiDocFile = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.ProductsImport, "Bạn không có quyền import hàng hóa.");
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

        // Người dùng chọn giữ hết sản phẩm trùng thì không mở transaction.
        if (canGhi.Count == 0)
            return ketQua;

        // san_pham.ngay_tao mặc định là NOT NULL DEFAULT CURRENT_TIMESTAMP, nên nếu
        // không gửi cột này thì MySQL tự điền ngày LÚC NHẬP, làm sai lịch sử.
        // Cho phép cột nhận NULL để ghi đúng NULL khi ô "Thời gian tạo" trong
        // Excel trống (không có dữ liệu cũ thì không bịa ra ngày giả).
        var ghiDuLieuNgayTao = await DamBaoCotNgayTaoSanPhamAsync();

        if (!ghiDuLieuNgayTao)
            ketQua.CanhBao.Add(
                "Không sửa được cột ngay_tao để cho phép NULL: những dòng không có "
                + "'Thời gian tạo' trong file sẽ nhận ngày lúc nhập. Cần cấp quyền ALTER "
                + "trên bảng san_pham để sửa.");

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

        // Khi thay thế: ngày tạo lấy từ Excel nếu file có, còn không thì GIỮ NGUYÊN
        // ngày đang có trong database — tuyệt đối không dùng DateTime.Now.
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

                // SAVEPOINT cho từng dòng: một dòng hỏng (ví dụ trùng mã vạch) chỉ
                // bị loại riêng, không mất các dòng đã ghi trước đó và không phá vỡ
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
                            throw new InvalidOperationException("Thiếu id sản phẩm đã tồn tại để thay thế.");

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
                "Lỗi nghiêm trọng, đã rollback toàn bộ lượt nhập: " + MoTaLoiNhap(ex)));
        }

        return ketQua;
    }

    /// <summary>Chuyển lỗi MySQL thành thông báo dễ hiểu cho người dùng.</summary>
    internal static string MoTaLoiNhap(Exception ex)
    {
        if (ex is MySqlException mySql)
        {
            return mySql.Number switch
            {
                1062 => $"trùng khóa duy nhất ({mySql.Message}). Kiểm tra lại mã hàng/mã vạch.",
                1406 => $"giá trị quá dài so với độ dài cột ({mySql.Message}).",
                1048 => $"thiếu giá trị bắt buộc ({mySql.Message}).",
                1452 => $"không tìm thấy bản ghi tham chiếu ({mySql.Message}).",
                _ => $"lỗi cơ sở dữ liệu {mySql.Number}: {mySql.Message}"
            };
        }

        return ex.Message;
    }

    /// <summary>
    /// Nạp bảng danh mục (thương hiệu / đơn vị tính / nhóm hàng) để tra id theo tên.
    /// Bảng rỗng hoặc không tồn tại thì trả về từ điển rỗng, lúc đó cột id tương ứng
    /// được để NULL — dòng vẫn nhập bình thường.
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
            // Không có bảng danh mục: bỏ qua, không làm hỏng lượt nhập.
        }

        return result;
    }

    /// <summary>
    /// Gom tham số cho INSERT/UPDATE. Các cột không bắt buộc được để NULL/rỗng bình
    /// thường, và giá trị vượt độ dài cột varchar được cắt lại kèm cảnh báo thay vì
    /// làm hỏng cả dòng.
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
                $"Cột \"{tenCot}\" dài {trimmed.Length} ký tự, đã cắt còn {doDaiToiDa} ký tự cho vừa độ dài cột.");

            return trimmed[..doDaiToiDa];
        }

        var maSanPham = Cat(product.MaSanPham, DoDaiToiDaMaSanPham, "Mã hàng") ?? string.Empty;
        var maVach = Cat(product.MaVach, DoDaiToiDaMaVach, "Mã vạch");

        // ma_vach có UNIQUE index. Nhiều dòng không có mã vạch thì ghi NULL (MySQL cho
        // phép nhiều NULL trong UNIQUE); để chuỗi rỗng sẽ khiến chỉ một dòng được nhập
        // rồi các dòng sau báo trùng.
        var parameters = new DynamicParameters();
        parameters.Add("MaSanPham", maSanPham, DbType.String);
        parameters.Add("MaVach", maVach, DbType.String);
        parameters.Add("TenSanPham", Cat(product.TenSanPham, DoDaiToiDaTenSanPham, "Tên hàng") ?? maSanPham, DbType.String);
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
        parameters.Add("ViTriKho", Cat(product.ViTriKho, DoDaiToiDaViTriKho, "Vị trí"), DbType.String);
        parameters.Add("MoTa", BlankToNull(product.MoTa), DbType.String);
        parameters.Add("GhiChu", BlankToNull(product.GhiChu), DbType.String);
        parameters.Add("HinhAnh", BlankToNull(product.HinhAnh), DbType.String);
        parameters.Add("DuocBanTrucTiep", product.DuocBanTrucTiep);
        parameters.Add("DangKinhDoanh", product.DangKinhDoanh);
        parameters.Add("LoaiHang", Cat(product.LoaiHang, DoDaiToiDaLoaiHang, "Loại hàng"), DbType.String);
        parameters.Add("NhomHangBaCap", Cat(product.NhomHangBaCap, DoDaiToiDaNhomHangBaCap, "Nhóm hàng(3 Cấp)"), DbType.String);
        parameters.Add("TonKhoExcel", product.TonKhoExcel);
        parameters.Add("KhachDat", product.KhachDat);
        parameters.Add("DuKienHetHang", Cat(product.DuKienHetHang, DoDaiToiDaDuKienHetHang, "Dự kiến hết hàng"), DbType.String);
        parameters.Add("MaDvtCoBan", Cat(product.MaDvtCoBan, DoDaiToiDaMaDvtCoBan, "Mã ĐVT Cơ bản"), DbType.String);
        parameters.Add("QuyDoi", product.QuyDoi <= 0 ? 1m : product.QuyDoi);
        parameters.Add("ThuocTinh", BlankToNull(product.ThuocTinh), DbType.String);
        parameters.Add("MaHhLienQuan", Cat(product.MaHhLienQuan, DoDaiToiDaMaHhLienQuan, "Mã HH Liên quan"), DbType.String);
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
    /// Bảo đảm <c>san_pham.ngay_tao</c> cho phép giá trị NULL.
    /// <para>
    /// Cột này được khai báo <c>NOT NULL DEFAULT CURRENT_TIMESTAMP</c>, nên khi
    /// nhập hàng từ file mà ô "Thời gian tạo" bị trống, MySQL sẽ tự điền ngày
    /// lúc nhập — đúng cái lỗi làm sai dữ liệu lịch sử. Sửa thành
    /// <c>DATETIME NULL DEFAULT CURRENT_TIMESTAMP</c>:
    /// nhập tay sản phẩm mới (bỏ trống cột) vẫn nhận ngày tạo = thời điểm thêm,
    /// còn nhập từ file thì ghi đúng giá trị trong file hoặc NULL.
    /// </para>
    /// <para>
    /// Chạy một lần duy nhất rồi ghi nhớ kết quả, không đụng tới dữ liệu sẵn có.
    /// Nếu database không cho sửa (thiếu quyền) thì phần mềm vẫn chạy, chỉ không
    /// ghi cột ngay_tao khi nhập từ file.
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
                // Không đọc được cột (bảng thiếu / không đủ quyền): giữ nguyên
                // định nghĩa cũ thay vì đoán.
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
    /// Đọc danh sách hàng hóa từ file và trả về kèm các dòng không đọc được.
    /// <para>
    /// Dòng thiếu mã hàng/tên không được âm thầm loại bỏ — nếu không, người dùng
    /// thấy báo cáo "736 dòng" trong khi file có 740 dòng mà không biết 4 dòng đó
    /// đã mất ở đâu. Dòng không đọc được được trả về qua <paramref name="loiDocFile"/>
    /// để đưa vào báo cáo lỗi của lượt nhập.
    /// </para>
    /// </summary>
    public List<SanPham> ParseProductsFromFile(
        string filePath,
        out List<LoiDongNhap> loiDocFile)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách hàng hóa.", filePath);

        loiDocFile = [];
        var rows = ReadProductRowsFromFile(filePath);

        // Đọc HẾT mọi dòng trong file. Chỉ dòng không có bất kỳ dữ liệu nào mới bị
        // loại; một dòng thiếu mã vạch / thương hiệu / mô tả / hình ảnh / nhà cung
        // cấp vẫn là dòng hợp lệ và phải được giữ lại.
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
                // Lỗi đọc một dòng không được làm hỏng cả lượt nhập: ghi nhận
                // dòng lỗi rồi tiếp tục dòng tiếp theo.
                loiDocFile.Add(new LoiDongNhap(
                    dong.SoDongExcel,
                    GetFirstString(ChuanHoaTieuDe(dong.GiaTri), "mahang", "masanpham", "ma_san_pham"),
                    null,
                    "Không đọc được dữ liệu của dòng: " + MoTaLoiNhap(ex)));

                continue;
            }

            if (string.IsNullOrWhiteSpace(product.MaSanPham)
                && string.IsNullOrWhiteSpace(product.TenSanPham))
            {
                loiDocFile.Add(new LoiDongNhap(
                    dong.SoDongExcel,
                    null,
                    null,
                    "Dòng không có mã hàng lẫn tên sản phẩm nên không tạo được sản phẩm."));

                continue;
            }

            products.Add(product);
        }

        return products;
    }

    /// <summary>Một dòng đọc từ file kèm số thứ tự dòng trong file, để báo lỗi chính xác.</summary>
    private sealed record DongFile(int SoDongExcel, Dictionary<string, string> GiaTri);

    private static List<DongFile> ReadProductRowsFromFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        if (extension == ".csv")
            return ReadCsvProductRows(filePath);

        if (extension is ".xlsx" or ".xls")
            return ReadExcelProductRows(filePath);

        throw new NotSupportedException("File không được hỗ trợ. Vui lòng chọn file Excel (.xlsx, .xls) hoặc CSV.");
    }

    private static List<DongFile> ReadExcelProductRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
            throw new InvalidOperationException("File Excel không chứa sheet dữ liệu hợp lệ.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return [];

        var headers = new List<string>();
        var firstRow = worksheet.Row(1);
        foreach (var cell in firstRow.Cells())
            headers.Add(cell.GetString().Trim());

        var result = new List<DongFile>();

        // Duyệt từ dòng 2 đến dòng cuối cùng của sheet: không giới hạn số dòng,
        // nên file 737 dòng thì đọc đủ 736 dòng dữ liệu.
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
    /// Đọc một ô của file danh sách hàng hóa.
    /// <para>
    /// Ô ngày giờ PHẢI lấy theo kiểu dữ liệu (XLDataType.DateTime) chứ không lấy
    /// chuỗi định dạng của ô: "20/09/2026 21:00" trong Excel là một DateTime thật
    /// (định dạng chỉ là cách hiển thị), đọc theo chuỗi sẽ phụ thuộc vào format ô
    /// và có thể mất giờ/phút. DateTime của Excel là giá trị không múi giờ nên
    /// chuyển thẳng ra chuỗi "yyyy-MM-dd HH:mm:ss" — không có bước đổi múi giờ
    /// nào và không tự đổi sang ngày hiện tại.
    /// </para>
    /// <para>
    /// Số / chữ / boolean / ô trống vẫn giữ đúng cách đọc cũ (GetString) để không
    /// đổi hành vi của các cột khác trong file.
    /// </para>
    /// </summary>
    private static string DocGiaTriOTheoKieuSanPham(IXLCell cell)
    {
        return cell.Value.Type switch
        {
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            XLDataType.Blank => string.Empty,

            // Ô lỗi công thức (#DIV/0!, #VALUE!...) lấy nội dung lỗi làm chuỗi
            // thay vì để GetString() ném exception, vì một ô hỏng trong file
            // không được làm hỏng cả 736 dòng còn lại.
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
    /// Đưa các tên cột trong file về dạng đã chuẩn hóa để tra cứu theo tên cột
    /// mà code dùng (sau <see cref="NormalizeHeader"/>). Cột trùng nhau sau khi
    /// chuẩn hóa thì giữ giá trị đầu tiên, tránh lỗi "key đã tồn tại".
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

        // Không có mã hàng thì để trống thay vì tự sinh mã theo giờ nhập: mã sinh tự động
        // (SP-yyyyMMdd-HHmmssfff) mỗi lần nhập là một sản phẩm khác, khiến cùng một
        // món hàng bị nhân bản mỗi lần nhập lại file. Việc sinh mã được đưa sang
        // thao tác tạo sản phẩm thủ công, nơi người dùng thực sự cần mã mới.
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
            // "Thời gian tạo"/"Ngày tạo" phải lấy đúng từ file. Không có giá trị
            // hợp lệ thì để NULL (xem DamBaoCotNgayTaoSanPhamAsync) chứ không
            // gán DateTime.Now, nếu không sản phẩm khôi phục từ dữ liệu cũ sẽ bị
            // đổi thành ngày lúc nhập và sai lịch sử.
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
            // Header thực tế là "Hình ảnh (url1,url2...)"; sau khi chuẩn hóa
            // ngoặc bị bỏ nên cần thử cả dạng có và không có phần trong ngoặc.
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
    /// Chuyển đổi an toàn giá trị bất kỳ từ cơ sở dữ liệu sang kiểu decimal.
    /// Hỗ trợ null, DBNull, số nguyên, số thực hoặc chuỗi số.
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
    /// Giống <see cref="ParseDecimal"/> nhưng trả về null khi ô trống hoặc không đọc
    /// được số, để phân biệt "không có dữ liệu" với giá trị 0.
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

        return normalized is "1" or "yes" or "y" or "co" or "đúng" ? true : defaultValue;
    }

    /// <summary>
    /// Đọc ô "Thời gian tạo" / "Ngày tạo" của file danh sách hàng hóa.
    /// <para>
    /// Danh sách định dạng là CỐ ĐỊNH, không nhờ vào văn hóa của máy. Nếu để
    /// <c>DateTime.TryParse</c> với văn hóa InvariantCulture chạy trước thì
    /// "05/09/2026" sẽ bị hiểu theo thứ tự tháng/ngày của văn hóa Mỹ, ra
    /// 9 tháng 5 — lệch so với 5 tháng 9 mà người dùng nhìn thấy trong file.
    /// </para>
    /// <para>
    /// Ô trống hoặc không nhận ra được thì trả về NULL — không bao giờ trả về
    /// ngày hiện tại, để người dùng biết dữ liệu cũ không có giá trị này.
    /// </para>
    /// </summary>
    private static DateTime? ParseNgayTaiFileSanPham(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Excel không lưu múi giờ: bỏ hậu tố Z / +07:00 nếu có để giữ đúng
        // giờ ghi trong ô, tuyệt đối không đổi theo múi giờ của máy.
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

        // Ô ngày bị Excel/CSV lưu thành số serial (46287 = 20/09/2026).
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
    /// Bỏ hậu tố múi giờ (Z, +07:00, -05:00) ở cuối chuỗi ngày giờ để giữ nguyên
    /// giờ ghi trong file, không chuyển đổi theo múi giờ của máy.
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
    /// Chuẩn hóa tên cột để so khớp giữa tên cột trong file và tên cột trong code.
    /// Bỏ dấu, hạ chữ thường và loại bỏ mọi ký tự phân tách/đặc biệt, kể cả dấu
    /// ngoặc — vì file thực tế có cột "Nhóm hàng(3 Cấp)" và "Hình ảnh (url1,url2...)"
    /// mà không bỏ ngoặc thì tên cột không bao giờ khớp được với khoá tra cứu.
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

            // Chỉ giữ lại chữ và số; bỏ hết dấu câu, ngoặc, khoảng trắng.
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
    /// Sinh mã khách hàng tuần tự theo dạng KH0000001.
    /// Chỉ xét các mã KH + số để không bị ảnh hưởng bởi mã cũ khác định dạng.
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
            throw new InvalidOperationException("Đã vượt giới hạn mã khách hàng tự động KH9999999.");

        return $"KH{nextNumber:D7}";
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
    /// Kiểm tra số điện thoại đã tồn tại trong khách hàng đang hoạt động chưa.
    /// So sánh sau khi bỏ khoảng trắng, dấu gạch và dấu chấm
    /// để tránh lưu trùng khi khách gõ khác định dạng.
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersDelete, "Bạn không có quyền xóa khách hàng.");
        await using var connection = await OpenAsync();

        // Giữ lại lịch sử hóa đơn/công nợ; danh sách khách hàng chỉ hiển thị bản ghi còn hoạt động.
        var affectedRows = await connection.ExecuteAsync(
            "UPDATE khach_hang SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id AND trang_thai = 1;",
            new { Id = customerId });

        if (affectedRows == 0)
            throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã bị xóa.");
    }

    /// <summary>
    /// Kiểm tra bảng lưu phiếu trả hàng có tồn tại trong database hay không
    /// (một số bản cài cũ chưa có bảng này). Kết quả được ghi nhớ để
    /// không phải hỏi lại database ở mỗi lần tải danh sách.
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
    /// Tổng tiền trả hàng của một khách hàng, tính theo biểu thức định danh
    /// khách hàng đang có trong câu truy vấn gốc (ví dụ "kh.id" hoặc "@Id").
    /// Nếu chưa có bảng trả hàng thì coi như không có giao dịch trả hàng nào,
    /// không tự tạo số liệu.
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
    /// Ba biểu thức tiền của một khách hàng, dùng chung cho bảng danh sách,
    /// thanh thống kê và vùng chi tiết để ba nơi luôn hiển thị cùng một số.
    /// Ưu tiên số tiền đã nhập từ file KiotViet, không có thì tính từ
    /// dữ liệu bán hàng, công nợ và trả hàng của phần mềm.
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
    /// Ba cột tiền của bảng danh sách khách hàng, dùng subquery tương quan theo
    /// kh.id nên chỉ tính cho các dòng thuộc trang đang xem thay vì gom nhóm toàn bảng.
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
    /// Bảo đảm bảng khach_hang có đủ ba cột phục vụ việc nhập từ file KiotViet.
    /// Các cột này được thêm một lần duy nhất, không đụng tới dữ liệu sẵn có:
    /// co_du_lieu_kiotviet đánh dấu bản ghi có số tiền lấy từ file,
    /// nguoi_tao_ten giữ tên người tạo ghi trong file, ngay_nhap_excel để truy vết.
    /// Nếu thêm không được (database cũ, không đủ quyền) thì phần mềm vẫn chạy
    /// bình thường và chỉ dùng số tiền tính từ hóa đơn, công nợ, trả hàng.
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

        // Thêm từng cột một vì bảng cũ có thể đã có sẵn một trong ba cột.
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
            // Không thêm được cột thì dùng số liệu tính từ hóa đơn, công nợ, trả hàng.
            _coCotDuLieuKiotViet = false;
        }

        return _coCotDuLieuKiotViet.Value;
    }

    /// <summary>
    /// Bảo đảm bảng nha_cung_cap có đủ các cột phục vụ việc nhập từ file danh sách
    /// nhà cung cấp. Đây là cột MỐI theo đúng cách đã làm với bảng khach_hang:
    /// <list type="bullet">
    /// <item>co_du_lieu_kiotviet đánh dấu bản ghi có số tiền lấy từ file,
    /// nguoi_tao_ten giữ người tạo ghi trong file, ngay_nhap_excel để truy vết;</item>
    /// <item>no_can_tra_hien_tai, tong_mua, tong_mua_tru_tra_hang lưu ba cột tiền
    /// của file, không có cột này thì không thể giữ số liệu sau khi nhập;</item>
    /// <item>khu_vuc, so_cccd, nhom_nha_cung_cap_ten, ten_cong_ty lưu các cột chữ
    /// còn lại của file mà bảng cũ chưa có chỗ để lưu.</item>
    /// </list>
    /// Các cột được thêm một lần duy nhất, không đụng tới dữ liệu sẵn có. Nếu thêm
    /// không được (database cũ, không đủ quyền) thì phần mềm vẫn chạy bình thường
    /// và chỉ dùng số tiền tính từ phiếu nhập hàng và sổ cái công nợ.
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

        // Thêm từng cột một vì bảng cũ có thể đã có sẵn một trong các cột này.
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
            // Không thêm được cột thì dùng số liệu tính từ phiếu nhập hàng, sổ cái công nợ.
            _coCotDuLieuKiotVietNhaCungCap = false;
        }

        return _coCotDuLieuKiotVietNhaCungCap.Value;
    }

    /// <summary>
    /// Ba biểu thức tiền của một nhà cung cấp, dùng chung cho bảng danh sách,
    /// thanh thống kê, bộ lọc công nợ và vùng chi tiết để ba nơi luôn hiển thị
    /// cùng một số. Ưu tiên số tiền đã nhập từ file danh sách, không có thì
    /// tính từ dữ liệu nhập hàng và công nợ của phần mềm.
    /// </summary>
    private static (string NoHienTai, string TongMua, string TongMuaTruTraHang)
        TaoBieuThucTienNhaCungCap(string idExpression, bool coCotKiotViet)
    {
        // Sổ cái công nợ là nguồn công nợ chính, giống hệt cột "Còn nợ" và
        // cột "Công nợ hiện tại" của vùng chi tiết.
        // Nếu chưa có dòng sổ cái nào, tự động fallback sang tổng còn nợ của phiếu nhập.
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

        // Tổng mua lấy từ các phiếu nhập hàng hợp lệ (DA_NHAP_HANG hoặc HOAN_THANH).
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

        // Phần mềm chưa có phiếu trả hàng cho nhà cung cấp, nên tổng mua trừ trả
        // hàng tính từ nội bộ bằng chính tổng mua. Khi có dữ liệu file thì lấy
        // số của file vì file đã trừ sẵn các phiếu trả hàng.
        var tienMuaTruTraNoiBo = tienMuaNoiBo;

        if (!coCotKiotViet)
            return (tienNoiBo, tienMuaNoiBo, tienMuaTruTraNoiBo);

        return (
            $"CASE WHEN ncc.co_du_lieu_kiotviet = 1 OR ncc.no_can_tra_hien_tai IS NOT NULL THEN (COALESCE(ncc.no_can_tra_hien_tai, 0) + {tienNoiBo}) ELSE {tienNoiBo} END",
            $"CASE WHEN ncc.co_du_lieu_kiotviet = 1 OR ncc.tong_mua IS NOT NULL THEN (COALESCE(ncc.tong_mua, 0) + {tienMuaNoiBo}) ELSE {tienMuaNoiBo} END",
            $"CASE WHEN ncc.co_du_lieu_kiotviet = 1 OR ncc.tong_mua_tru_tra_hang IS NOT NULL THEN (COALESCE(ncc.tong_mua_tru_tra_hang, 0) + {tienMuaTruTraNoiBo}) ELSE {tienMuaTruTraNoiBo} END");
    }

    /// <summary>Lấy danh sách khách hàng theo trang cho màn hình "Danh sách khách hàng".
    /// Chỉ đọc dữ liệu của trang hiện tại (LIMIT/OFFSET) kèm tổng số bản ghi,
    /// nên giao diện vẫn mượt khi bảng có hàng nghìn khách hàng.
    /// </summary>
    /// <param name="dayDuThongTin">
    /// false: lấy đúng các cột của bảng danh sách (mã, tên, điện thoại, email,
    /// địa chỉ, nhóm, ngày tạo) kèm nợ hiện tại, tổng bán và tổng bán trừ trả hàng.
    /// true: lấy đầy đủ mọi cột để dùng cho form nhập/sửa.
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

        // Bộ lọc nhóm khách hàng và tình trạng công nợ, dùng cùng biểu thức
        // NoHienTai với cột tiền trong bảng nên số liệu lọc luôn khớp.
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

    /// <summary>Lấy đầy đủ thông tin một khách hàng để mở vùng chi tiết / form sửa.</summary>
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
    /// Số ngày dùng để đếm "Khách hàng mới" khi giao diện không chỉ định.
    /// Giữ ở đây để query và chữ dưới thẻ thống kê cùng một con số.
    /// </summary>
    public const int SoNgayKhachMoiMacDinh = 30;

    /// <summary>
    /// Số liệu tổng quan cho thanh thống kê phía trên bảng danh sách khách hàng.
    /// Cả bốn chỉ số đều tính thẳng từ database, không lấy từ các dòng đang hiển thị:
    /// <list type="bullet">
    /// <item>Tổng khách hàng = COUNT(*) của khach_hang có trang_thai = 1.</item>
    /// <item>Khách hàng mới = số khách hàng đang hoạt động có ngay_tao nằm trong
    /// <paramref name="soNgay"/> ngày gần nhất (mặc định 30 ngày), tính bằng
    /// DATE_SUB(NOW(), INTERVAL ...) chứ không phải COUNT toàn bộ bảng.</item>
    /// <item>Nợ hiện tại = SUM của biểu thức nợ của từng khách hàng, ưu tiên sổ cái
    /// công nợ (SUM(ghi_no - ghi_co)), không có sổ cái thì lấy SUM(con_no) của
    /// hóa đơn đã hoàn thành.</item>
    /// <item>Tổng bán = SUM của biểu thức bán của từng khách hàng, tức tổng
    /// khach_can_tra của các hóa đơn đã hoàn thành (đã trừ giảm giá, chưa trừ trả hàng).</item>
    /// </list>
    /// Nhờ vậy thẻ thống kê và tổng các dòng trong bảng luôn dùng chung một quy tắc.
    /// </summary>
    public async Task<(int TongSoKhachHang, int SoKhachMoi, decimal TongNoHienTai, decimal TongBan)>
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
            COALESCE(SUM({tien.NoHienTai}), 0) AS TongNoHienTai,
            COALESCE(SUM({tien.TongBan}), 0) AS TongBan
        FROM khach_hang kh
        WHERE kh.trang_thai = 1;
        """;

        await using var connection = await OpenAsync();

        var row = await connection.QueryFirstOrDefaultAsync<ThongKeDanhSachKhachHang>(
            sql,
            new { SoNgay = soNgay < 1 ? SoNgayKhachMoiMacDinh : soNgay });

        return row is null
            ? (0, 0, 0m, 0m)
            : (row.TongSoKhachHang, row.SoKhachMoi, row.TongNoHienTai, row.TongBan);
    }

    /// <summary>Danh sách nhóm khách hàng đang hoạt động, dùng cho ComboBox trong form nhập/sửa.</summary>
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
    /// Lịch sử hóa đơn của một khách hàng (mới nhất trước).
    /// Chỉ đọc tối đa <paramref name="gioiHan"/> dòng, không quét toàn bộ bảng hóa đơn.
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
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
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
    /// Tổng hợp tổng tiền đã mua và công nợ hiện tại của khách hàng.
    /// Các số tiền lấy đúng theo cùng quy tắc với bảng danh sách: ưu tiên số tiền
    /// đã nhập từ file KiotViet, không có thì tính từ hóa đơn, sổ cái công nợ và
    /// phiếu trả hàng. Hóa đơn chỉ tính loại đã hoàn thành.
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
            -- Số tiền hiển thị: khi có dữ liệu file KiotViet thì lấy từ file,
            -- ngược lại lấy từ hóa đơn/sổ cái/trả hàng như trước.
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersCreate, "Bạn không có quyền thêm khách hàng.");
        await using var connection = await OpenAsync();

        if (string.IsNullOrWhiteSpace(customer.MaKhachHang))
            throw new ArgumentException("Mã khách hàng không được để trống.");

        if (string.IsNullOrWhiteSpace(customer.TenKhachHang))
            throw new ArgumentException("Tên khách hàng không được để trống.");

        // Kiểm tra trùng mã khách hàng
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
                $"Mã khách hàng {customer.MaKhachHang} đã tồn tại.");

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

        _ = LogSubAccountActivityAsync("Thêm khách hàng", "KHACH_HANG", id, customer.MaKhachHang, $"Thêm khách hàng: {customer.TenKhachHang} ({customer.SoDienThoai})");

        return id;
    }
    public async Task CapNhatKhachHangAsync(KhachHang customer)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersEdit, "Bạn không có quyền sửa khách hàng.");
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
            throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã bị xóa.");

        _ = LogSubAccountActivityAsync("Cập nhật khách hàng", "KHACH_HANG", customer.Id, customer.MaKhachHang, $"Cập nhật khách hàng: {customer.TenKhachHang} ({customer.SoDienThoai})");
    }

    /// <summary>Tên bảng khách hàng, dùng để sinh câu lệnh SQL.</summary>
    private const string BangKhachHang = "khach_hang";

    /// <summary>Bảng tạm dùng để cập nhật hàng loạt khách hàng.</summary>
    private const string BangTamCapNhat = "tmp_nhap_khach_hang";

    /// <summary>
    /// Các cột được ghi khi nhập từ file, đúng thứ tự với <see cref="GiaTriDongNhap"/>.
    /// Gồm cả những cột lấy từ file KiotViet: khu vực giao hàng, người tạo,
    /// ngày giao dịch cuối và ba cột tiền.
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
    /// Giá trị tương ứng với từng cột trong <see cref="CotNhapKhachHang"/>.
    /// Ngày tạo lấy theo file, khi file không có thì dùng thời điểm nhập.
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
    /// Lấy về tập khóa "ma:" + mã khách hàng và "dt:" + số điện thoại đang có,
    /// chỉ để đếm khách hàng trùng trước khi nhập file. Không tải danh sách
    /// khách hàng lên giao diện.
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

    /// <summary>Nhập danh sách khách hàng từ file, tự đọc file và ghi log đầy đủ.</summary>
    public async Task<KetQuaImportKhachHang> ImportKhachHangTuFileAsync(
        string filePath,
        bool replaceExisting = false)
        => await ImportKhachHangTuFileAsync(filePath, DocKhachHangTuFile(filePath), replaceExisting);

    /// <summary>
    /// Nhập danh sách khách hàng đã đọc từ file.
    /// Khách hàng đã tồn tại (trùng mã, hoặc trùng số điện thoại) được cập nhật
    /// nếu <paramref name="replaceExisting"/> = true, ngược lại được giữ nguyên.
    /// Không bao giờ xóa khách hàng nào không có trong file.
    /// </summary>
    public async Task<KetQuaImportKhachHang> ImportKhachHangTuFileAsync(
        string filePath,
        KetQuaDocKhachHang doc,
        bool replaceExisting = false)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CustomersImport, "Bạn không có quyền import khách hàng.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

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

        // Nạp sẵn khách hàng đang có, nhóm khách hàng và tài khoản để tra cứu trong
        // bộ nhớ, tránh hỏi database 849 lần cho 849 dòng.
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

            // File đã có mã lặp thì chỉ giữ dòng đầu tiên.
            if (!seenCodes.Add(ma))
            {
                result.SoBoQua++;
                errors.Add($"Mã '{ma}' bị lặp trong file, bỏ qua dòng lặp");
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
    /// Câu lệnh ghi một lô khách hàng. Khi ghi vào bảng tạm phải kèm cột id
    /// để còn nối được với đúng khách hàng đang cần cập nhật.
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
    /// Cập nhật hàng loạt qua bảng tạm, để 849 dòng chỉ tốn vài câu lệnh thay vì
    /// 849 lần gọi database. Ngày tạo và nhóm/người tạo chỉ bị ghi đè khi file thật sự có dữ liệu.
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
    /// Nạp danh sách tên kèm khóa để tra cứu khi nhập. Nếu bảng không tồn tại
    /// thì trả về danh sách rỗng, không làm hỏng việc nhập.
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

                // Tên trong file KiotViet có thể là "Phương Khánh" hoặc "Khách lẻ",
                // nên phải giữ nguyên cả tên chứ không tách từng từ.
                result.TryAdd(ten, row.Id);
                result.TryAdd(NormalizeHeaderKey(ten), row.Id);
            }
        }
        catch
        {
            // Bảng tra cứu không tồn tại thì bỏ qua phần này.
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
    /// Ghi log kết quả nhập ra tệp trong thư mục dữ liệu ứng dụng để tra cứu được
    /// khi có sự cố, không ảnh hưởng tới việc chạy của ứng dụng.
    /// </summary>
    private void GhiLogNhapKhachHang(KetQuaImportKhachHang result)
    {
        var dong = $"[NHAP KHACH HANG] {result.ThongBaoTatCa}";

        if (result.SoNhomKhachHangKhongKhop > 0)
            dong += $" | nhóm khách hàng không khớp: {result.SoNhomKhachHangKhongKhop}";

        if (result.SoNguoiTaoKhongKhop > 0)
            dong += $" | người tạo không khớp tài khoản: {result.SoNguoiTaoKhongKhop}";

        GhiLog(dong);

        foreach (var loi in result.DanhSachLoi)
            GhiLog($"[NHAP KHACH HANG] LỖI {result.TenFile}: {loi}");
    }

    /// <param name="message">Nội dung cần ghi.</param>
    /// <param name="tenTep">
    /// Tiền tố tên tệp log theo ngày. Mặc định là log nhập khách hàng;
    /// mỗi loại nhập dùng một tệp riêng để tra cứu không lẫn lộn.
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
            // Không ghi được log thì bỏ qua, không được làm hỏng luồng nhập.
        }
    }

    /// <summary>Một dòng khách hàng sẵn có trong database, dùng để tìm khách hàng trùng.</summary>
    private sealed class DongKhachHangTonTai
    {
        public uint Id { get; set; }
        public string? MaKhachHang { get; set; }
        public string? SoDienThoai { get; set; }
    }

    /// <summary>Một dòng chỉ có khóa và tên, dùng cho bảng nhóm khách hàng và người dùng.</summary>
    private sealed class DongTen
    {
        public uint Id { get; set; }
        public string? Ten { get; set; }
    }

    /// <summary>
    /// Một dòng khách hàng sẽ ghi xuống database, đúng thứ tự cột trong
    /// <see cref="CotNhapKhachHang"/>. Các cột tiền lấy từ file KiotViet.
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

        /// <summary>Tên nhóm trong file, chỉ dùng để tra cứu rồi bỏ đi.</summary>
        public string? NhomKhachHangTen { get; set; }
    }

    public List<KhachHang> ParseKhachHangFromFile(string filePath)
        => DocKhachHangTuFile(filePath).KhachHangs.ToList();

    /// <summary>
    /// Đọc file danh sách khách hàng và trả về kèm số liệu thống kê để ghi log:
    /// tổng số dòng trong file, số dòng hợp lệ và chi tiết các dòng bị loại.
    /// </summary>
    public KetQuaDocKhachHang DocKhachHangTuFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách khách hàng.", filePath);

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
                    errors.Add($"Dòng {index + 2}: thiếu tên khách hàng");

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

        throw new NotSupportedException("File không được hỗ trợ. Vui lòng chọn file Excel (.xlsx, .xls) hoặc CSV.");
    }

    private static List<Dictionary<string, string>> ReadExcelRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
            throw new InvalidOperationException("File Excel không chứa sheet dữ liệu hợp lệ.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
            return [];

        var headerRowNumber = FindCustomerHeaderRow(worksheet, lastRow);
        if (headerRowNumber == 0)
            throw new InvalidOperationException("Không tìm thấy dòng tiêu đề khách hàng trong file.");

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
    /// Lấy giá trị ô theo đúng kiểu dữ liệu thay vì lấy chuỗi đã định dạng.
    /// Cần thiết vì file KiotViet định dạng tiền theo kiểu "195,000" và ngày theo
    /// kiểu "30/09/2026": đọc chuỗi định dạng sẽ làm sai số tiền (văn hóa Việt
    /// dùng dấu phẩy làm dấu thập phân). Giá trị số/ngày được chuyển sang
    /// chuỗi theo quy ước bất biến để ParseSoTien/ParseNgayTaiFile đọc đúng.
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
    /// Đưa chữ "Đ" (U+0110) và "đ" về "d"/"d".
    /// Hai ký tự này không có dạng phân rã chuẩn nên FormD giữ nguyên chúng,
    /// khiến tiêu đề "Điện thoại" hoá thành "đien_thoai" và không khớp với
    /// danh sách tên cột viết bằng chữ "d" thông thường. Ô "Điện thoại" và
    /// "Địa chỉ" trong file Excel sẽ bị bỏ qua nếu thiếu bước gộp này.
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
    /// Chuyển một dòng đã đọc từ file thành khách hàng.
    /// Mọi cột đều tìm theo TÊN CỘT (đã bỏ dấu và chữ thường), không theo vị trí,
    /// nên thứ tự cột trong file không ảnh hưởng kết quả.
    /// </summary>
    private static KhachHang MapRowToKhachHang(Dictionary<string, string> row)
    {
        var customer = new KhachHang
        {
            MaKhachHang = GetCellValue(row, "ma_khach_hang", "mã khách hàng", "ma khach hang", "mã khách hàng", "customer code", "customer_code", "mã", "mã khách hàng") ?? string.Empty,
            TenKhachHang = GetCellValue(row, "ten_khach_hang", "tên khách hàng", "ten khach hang", "customer name", "name", "tên", "tên khách hàng") ?? string.Empty,
            LoaiKhachHang = ChuanHoaLoaiKhachHang(GetCellValue(row, "loai_khach_hang", "loại khách hàng", "loai khach hang", "customer type", "loại khách", "loai khach", "loại")),
            SoDienThoai = GetCellValue(row, "so_dien_thoai", "số điện thoại", "so dien thoai", "dien thoai", "điện thoại", "phone", "sdt", "đt", "số điện thoại"),
            Email = GetCellValue(row, "email"),
            DiaChi = GetCellValue(row, "dia_chi", "địa chỉ", "dia chi", "address", "địa chỉ"),
            PhuongXa = GetCellValue(row, "phuong_xa", "phường/xã", "phuong xa", "ward", "phường xã"),
            QuanHuyen = GetCellValue(row, "quan_huyen", "quận/huyện", "quan huyen", "district"),
            TinhThanh = GetCellValue(row, "tinh_thanh", "tỉnh/thành", "tinh thanh", "province"),
            NhomKhachHang = GetCellValue(row, "nhom_khach_hang", "nhóm khách hàng", "nhom khach hang", "group", "nhóm", "nhom"),
            MaSoThue = GetCellValue(row, "ma_so_thue", "mã số thuế", "ma so thue", "tax code", "mst"),
            TenNguoiMuaHoaDon = GetCellValue(row, "ten_nguoi_mua_hoa_don", "tên người mua hóa đơn", "nguoi mua hoa don"),
            TenCongTy = GetCellValue(row, "ten_cong_ty", "tên công ty", "cong ty"),
            DiaChiXuatHoaDon = GetCellValue(row, "dia_chi_xuat_hoa_don", "địa chỉ xuất hóa đơn", "dia chi xuat hoa don"),
            GhiChu = GetCellValue(row, "ghi_chu", "ghi chú", "note", "chú thích"),
            KhuVucGiaoHang = GetCellValue(row, "khu_vuc_giao_hang", "khu vuc giao hang", "khu vực", "khu vuc", "khu vực giao hàng"),
            ChiNhanhTao = GetCellValue(row, "chi_nhanh_tao", "chi nhánh tạo", "chi nhanh tao", "chi nhánh", "chi nhanh"),
            NguoiTaoTen = GetCellValue(row, "nguoi_tao", "người tạo", "nguoi tao", "nguoi_tao", "người tạo"),
            NgayGiaoDichCuoi = ParseNgayTaiFile(GetCellValue(row, "ngay_giao_dich_cuoi", "ngày giao dịch cuối", "ngay giao dich cuoi", "ngày giao dịch cuối cùng", "ngay giao dich gan nhat", "last transaction")),
            NgayTao = ParseNgayTaiFile(GetCellValue(row, "ngay_tao", "ngày tạo", "ngay tao", "created at", "ngày tạo")),
            NoCanThuHienTaiExcel = ParseSoTien(GetCellValue(row, "no_can_thu_hien_tai", "nợ cần thu hiện tại", "no can thu hien tai", "nợ hiện tại", "no hien tai", "nợ cần thu")),
            TongBanExcel = ParseSoTien(GetCellValue(row, "tong_ban", "tổng bán", "tong ban", "tổng bán")),
            TongBanTruTraHangExcel = ParseSoTien(GetCellValue(row, "tong_ban_tru_tra_hang", "tổng bán trừ trả hàng", "tong ban tru tra hang", "tổng bán trừ trả", "tong ban tru tra")),
            GioiTinh = GetCellValue(row, "gioi_tinh", "giới tính", "gender", "sex", "gioi tinh"),
            NgaySinh = ParseNgayTaiFile(GetCellValue(row, "ngay_sinh", "ngày sinh", "ngay sinh", "birthday", "dob"))
        };

        var trangThai = ParseTrangThai(GetCellValue(row, "trang_thai", "trạng thái", "trang thai", "status", "active", "trạng thái"));
        if (trangThai.HasValue)
            customer.TrangThai = trangThai.Value;

        if (string.IsNullOrWhiteSpace(customer.MaKhachHang))
            customer.MaKhachHang = GenerateCustomerCode();

        if (string.IsNullOrWhiteSpace(customer.LoaiKhachHang))
            customer.LoaiKhachHang = "CA_NHAN";

        return customer;
    }

    /// <summary>
    /// Chuẩn hóa loại khách hàng. File KiotViet ghi "Cá nhân" (sai chính tả),
    /// phần mềm dùng mã "CA_NHAN"/"DOAN".
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
    /// Đọc số tiền từ ô. Giá trị đọc từ file Excel đã là chuỗi số theo quy ước
    /// bất biến, còn dữ liệu CSV có thể là "195.000", "195,000" hoặc "1.234,56".
    /// </summary>
    internal static decimal? ParseSoTien(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim()
            .Replace("\u00A0", "")
            .Replace(" ", "")
            .Replace("₫", "")
            .Replace("đ", "")
            .Replace("Đ", "");

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

            // Dấu trừ ở đầu là dấu âm, các ký tự khác bị bỏ qua.
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
    /// Đọc ngày từ ô. Chấp nhận cả chuỗi không dấu phân cách do Excel sinh ra
    /// ("2026-09-30 12:37:50"), dạng Việt Nam ("30/09/2026") và dạng quốc tế.
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

    /// <summary>Đọc trạng thái: 1/0, Có/Không, Active/Inactive, Đang hoạt động...</summary>
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
    COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,

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
    /// Sinh mã hóa đơn kế tiếp trong ngày, dạng HDyyMMdd-NNN (VD: HD261005-002).
    /// <para>
    /// PHẢI cắt chuỗi tại <c>CHAR_LENGTH(@Prefix) + 2</c>. Mã hóa đơn gồm 8 ký tự
    /// tiền tố + 1 dấu gạch nối nên phần số bắt đầu ở vị trí 10. Bản cũ dùng cứng
    /// <c>SUBSTRING(ma_hoa_don, 9)</c> nên cắt trúng dấu "-" và cắt dấu "-" đi
    /// ("-001"); MySQL/MariaDB ép "-001" sang UNSIGNED thành 18446744073709551615,
    /// rồi <c>MAX(...) + 1</c> tràn số khiến Dapper ném
    /// <see cref="OverflowException"/>. Hệ quả: đơn đầu tiên vẫn tạo được (bảng
    /// rỗng nên MAX là NULL, COALESCE về 0) nhưng TỪ ĐƠN THỨ HAI TRỞ ĐI LUÔN LỖI:
    /// không INSERT được gì và không hiện hóa đơn.
    /// </para>
    /// <para>
    /// Đọc kết quả bằng <see cref="decimal"/> để mã hóa đơn bẩn/sai định dạng
    /// không làm tràn số lúc quy đổi sang <see cref="long"/>.
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

    /// <summary>Lấy mã hóa đơn kế tiếp (dùng ngoài giao dịch bán hàng).</summary>
    public async Task<string> TaoMaHoaDonMoiAsync()
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesPayment, "Bạn không có quyền tạo hóa đơn bán hàng.");
        await using var connection = await OpenAsync();

        return await TaoMaHoaDonMoiTrongGiaoDichAsync(connection, null);
    }

    /// <summary>
    /// Dựng lại hóa đơn bán hàng từ database (đầy đủ cả chi tiết) để mở lại
    /// xem / in một hóa đơn đã có. Trả về null nếu mã hóa đơn không tồn tại.
    /// </summary>
    public async Task<HoaDonBanHangReceipt?> GetHoaDonBanHangTheoMaAsync(
        string maHoaDon)
    {
        if (string.IsNullOrWhiteSpace(maHoaDon))
            return null;

        var maCanTim = maHoaDon.Trim();

        var coCotContact = await EnsureHoaDonContactColumnsAsync();

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

        var headerSql = $"""
        SELECT
            hd.id                       AS HoaDonId,
            hd.ma_hoa_don               AS MaHoaDon,
            hd.thoi_gian_ban            AS ThoiGianBan,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
            COALESCE(nd.ho_ten, '')     AS TenNhanVien,
            hd.tong_tien_hang           AS TongTienHang,
            hd.giam_gia                 AS GiamGia,
            hd.thu_khac                 AS ThuKhac,
            hd.khach_can_tra            AS KhachCanTra,
            hd.khach_da_thanh_toan      AS KhachDaThanhToan,
            hd.con_no                   AS ConNo,
            hd.ghi_chu                  AS GhiChu,
            {contactCols}
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

        var items = (await connection.QueryAsync<HoaDonBanHangReceiptItem>(
            detailSql,
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
            TenKhachHang = header.TenKhachHang ?? "Khách lẻ",
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

            // "Tiền khách đưa" = số tiền đã trả + tiền thối
            TienKhachDua = daThanhToan + thuKhac,
            TienThoi = thuKhac,

            TrangThaiThanhToan = TrangThaiThanhToanHienThi(conNo > 0, thuKhac > 0),

            GhiChu = header.GhiChu as string,

            Items = items
        };
    }

    private static string TrangThaiThanhToanHienThi(bool conNo, bool coTienThoi)
        => conNo
            ? "Thanh toán thiếu - Công nợ"
            : coTienThoi
                ? "Đã thanh toán đủ - Tiền thối"
                : "Đã thanh toán đủ";

    /// <summary>
    /// Lưu một đơn bán hàng: hóa đơn + chi tiết + trừ tồn kho + ghi lịch sử xuất
    /// kho + công nợ khách hàng, tất cả trong MỘT giao dịch duy nhất.
    /// Chỉ khi commit thành công mới trả về hóa đơn đã đọc lại từ database;
    /// bất kỳ bước nào lỗi thì rollback toàn bộ và ném lỗi ra ngoài.
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesPayment, "Bạn không có quyền tạo hóa đơn bán hàng.");
        if (items.Count == 0)
            throw new InvalidOperationException("Giỏ hàng đang trống.");

        var now = DateTime.Now;

        // ============================================================
        // 0. KIỂM TRA DỮ LIỆU ĐẦU VÀO (trước khi mở giao dịch)
        // ============================================================

        var cart = new List<SaleCartItem>(items.Count);

        foreach (var item in items)
        {
            if (item?.SanPham is null)
                throw new InvalidOperationException("Giỏ hàng có dòng sản phẩm không hợp lệ.");

            if (item.SanPham.Id == 0)
                throw new InvalidOperationException(
                    $"Sản phẩm \"{item.SanPham.TenSanPham}\" chưa được lưu vào hệ thống nên không thể bán.");

            if (item.SoLuong <= 0)
                throw new InvalidOperationException(
                    $"Số lượng của sản phẩm \"{item.SanPham.TenSanPham}\" phải lớn hơn 0.");

            if (item.SanPham.GiaBan <= 0)
                throw new InvalidOperationException(
                    $"Sản phẩm \"{item.SanPham.TenSanPham}\" chưa có giá bán.");

            cart.Add(item);
        }

        // Tổng tiền hàng
        var total = cart.Sum(item => item.ThanhTien);

        // Giảm giá hợp lệ
        var safeDiscount = Math.Clamp(discount, 0, total);

        // Khách cần thanh toán
        var amountDue = total - safeDiscount;

        // Tiền khách đưa không được âm
        var cashReceived = Math.Max(0, tienKhachDua);

        // Số tiền thực tế được tính là đã thanh toán
        // Nếu khách đưa dư thì chỉ ghi nhận bằng số tiền cần trả.
        var paidAmount = Math.Min(cashReceived, amountDue);

        // Công nợ phát sinh
        var debtAmount = Math.Max(0, amountDue - paidAmount);

        // Khách lẻ (không gắn hồ sơ khách hàng) KHÔNG được phép ghi nợ.
        // Chặn ngay tại tầng nghiệp vụ, TRƯỚC khi mở giao dịch, nên không tạo hóa đơn,
        // không trừ tồn kho, không ghi sổ quỹ và không ghi công nợ khách hàng.
        if (debtAmount > 0m && (customer is null || customer.Id == 0))
        {
            throw new InvalidOperationException(
                "Khách lẻ không được phép ghi nợ. Vui lòng nhập đủ số tiền khách thanh toán " +
                "hoặc chọn khách hàng có thông tin để ghi nhận công nợ.");
        }

        // Tiền thối
        var changeAmount = Math.Max(0, cashReceived - amountDue);

        // Trạng thái thanh toán
        var paymentStatus = debtAmount > 0 ? "CONG_NO" : "DA_THANH_TOAN";

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
        var userId = Session.CurrentUser?.Id;

        // Đảm bảo cột liên kết phiếu thu và cột thông tin liên hệ tồn tại TRƯỚC khi mở giao dịch.
        // ALTER TABLE bên trong transaction sẽ làm MySQL báo
        // "Table definition has changed" và huỷ cả giao dịch.
        var coCotPhieuThuLienKet = await EnsurePhieuQuyHoaDonColumnsAsync();
        var coCotContactHoaDon = await EnsureHoaDonContactColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;

        // Mã phiếu thu sổ quỹ sinh ra từ hóa đơn này (rỗng nếu khách trả 0 đồng).
        var maPhieuThu = "";

        try
        {
            // ============================================================
            // 1. TẠO HÓA ĐƠN
            //    Mã hóa đơn sinh NGAY TRONG giao dịch (cùng connection) để
            //    không trùng mã, và thử lại nếu chạm UNIQUE index ma_hoa_don.
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

                            // Tiền thối
                            ThuKhac = changeAmount,

                            // Khách cần trả
                            KhachCanTra = amountDue,

                            // Khách thực tế đã thanh toán
                            KhachDaThanhToan = paidAmount,

                            // Công nợ
                            ConNo = debtAmount,

                            TrangThaiThanhToan = paymentStatus,

                            GhiChu = BlankToNull(ghiChu)
                        },
                        transaction);

                    // Đọc LAST_INSERT_ID() bằng lệnh riêng, không gộp batch nhiều
                    // câu lệnh (batch phụ thuộc cờ AllowBatch của driver).
                    invoiceId = await connection.ExecuteScalarAsync<uint>(
                        "SELECT LAST_INSERT_ID();",
                        null,
                        transaction);

                    break;
                }
                catch (MySqlException ex) when (ex.Number == 1062 && lan < soLanThuLai)
                {
                    // 1062 = Duplicate entry: mã hóa đơn bị trùng, thử mã kế tiếp.
                }
            }

            if (invoiceId == 0)
                throw new InvalidOperationException(
                    "Không tạo được hóa đơn trong database. Giao dịch đã hủy, dữ liệu bán hàng không bị thay đổi.");

            // ============================================================
            // 2. CHI TIẾT HÓA ĐƠN
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

            foreach (var item in cart)
            {
                var affectedRows = await connection.ExecuteAsync(
                    detailSql,
                    new
                    {
                        HoaDonId = invoiceId,
                        SanPhamId = item.SanPham.Id,
                        MaSanPham = item.SanPham.MaSanPham,
                        TenSanPham = item.SanPham.TenSanPham,
                        DonViTinh = item.SanPham.DonViTinh,
                        item.SoLuong,
                        DonGia = item.SanPham.GiaBan,
                        item.ThanhTien,
                        GiaVon = item.SanPham.GiaVon
                    },
                    transaction);

                if (affectedRows != 1)
                    throw new InvalidOperationException(
                        $"Không lưu được dòng sản phẩm \"{item.SanPham.TenSanPham}\" vào hóa đơn {invoiceCode}.");
            }

            // Bảng chi tiết KHÔNG có khoá ngoại nên phải tự đối chiếu số dòng,
            // tránh trường hợp "có hóa đơn nhưng hụt chi tiết" vẫn commit.
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
                    $"Hóa đơn {invoiceCode} chỉ lưu được {soDongChiTiet}/{cart.Count} dòng sản phẩm. Giao dịch đã hủy.");

            // ============================================================
            // 3. TRỪ TỒN KHO + GHI LỊCH SỬ NHẬP/XUẤT
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
            // 4. NẾU CÓ CÔNG NỢ -> LƯU VÀO LỊCH SỬ CÔNG NỢ KHÁCH HÀNG
            // ============================================================

            if (customer is not null && customer.Id > 0 && debtAmount > 0)
            {
                // Lấy công nợ hiện tại của khách
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

                // Công nợ mới sau khi phát sinh hóa đơn
                var newDebtBalance = currentDebt + debtAmount;

                // Ghi lịch sử công nợ
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

                        // Khoản nợ phát sinh
                        GhiNo = debtAmount,

                        // Số dư công nợ mới
                        SoDu = newDebtBalance,

                        ThoiGian = now,
                        NguoiThucHienId = userId,

                        GhiChu =
                            $"Công nợ phát sinh từ hóa đơn {invoiceCode}"
                    },
                    transaction);
            }

            // ============================================================
            // 5. GHI PHIẾU THU SỔ QUỸ
            //    Chỉ ghi phần tiền khách trả thật (không kể tiền thối và
            //    không tính phần còn nợ) và ghi trong CÙNG giao dịch với hóa đơn
            //    nên không bao giờ có hóa đơn không kèm phiếu thu, hoặc phiếu thu
            //    mồ côi khi hóa đơn bị rollback.
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
                "Bán hàng",
                "HOA_DON",
                invoiceId,
                invoiceCode,
                $"Bán hóa đơn {invoiceCode} cho {(customer != null && !string.IsNullOrWhiteSpace(customer.TenKhachHang) ? customer.TenKhachHang : "Khách lẻ")} (Tổng thanh toán: {amountDue:N0} đ)",
                amountDue);

            // ============================================================
            // 7. ĐỌC LẠI HÓA ĐƠN ĐÃ LƯU ĐỂ HIỂN THỊ
            //    Lấy dữ liệu thật trong database thay vì dựng lại từ RAM, nhờ
            //    vậy mã hóa đơn, tổng tiền, tiền thối, công nợ và danh sách hàng
            //    trên hóa đơn luôn khớp với dữ liệu đã lưu.
            // ============================================================

            var receipt = await GetHoaDonBanHangTheoMaAsync(invoiceCode);

            if (receipt is null)
                throw new InvalidOperationException(
                    $"Đã lưu hóa đơn {invoiceCode} nhưng không đọc lại được. Hãy kiểm tra danh sách hóa đơn.");

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
    /// Ghi nhận khách trả thêm tiền cho một hóa đơn còn nợ: cập nhật số đã trả /
    /// còn nợ / trạng thái của hóa đơn, lưu một dòng vào bảng <c>thanh_toan</c>,
    /// cập nhật lịch sử công nợ khách hàng và tạo phiếu thu trong sổ quỹ — tất cả
    /// trong MỘT giao dịch. Trả về hóa đơn đã đọc lại từ database.
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
            "Bạn không có quyền thu công nợ khách hàng.");

        if (string.IsNullOrWhiteSpace(maHoaDon))
            throw new InvalidOperationException("Thiếu mã hóa đơn cần thu nợ.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền thu phải lớn hơn 0.");

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
                throw new InvalidOperationException($"Không tìm thấy hóa đơn {maCanTim} trong database.");

            var hoaDonId = hd.Id;
            var branchCuaHoaDon = hd.ChiNhanhId;
            var khachHangId = hd.KhachHangId;

            var khachCanTra = hd.KhachCanTra;
            var daTraCu = hd.KhachDaThanhToan;
            var conNoCu = hd.ConNo;

            if (conNoCu <= 0)
                throw new InvalidOperationException(
                    $"Hóa đơn {maCanTim} đã thanh toán đủ, không còn công nợ.");

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
                "Thu nợ",
                "HOA_DON",
                hoaDonId,
                maCanTim,
                $"Thu nợ hóa đơn {maCanTim}: {thu:N0} đ (còn nợ {conNoMoi:N0} đ)",
                thu);

            var receipt = await GetHoaDonBanHangTheoMaAsync(maCanTim);

            if (receipt is null)
                throw new InvalidOperationException(
                    $"Đã thu nợ nhưng không đọc lại được hóa đơn {maCanTim}.");

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
    /// Ghi nhận một lần thu nợ cho đúng MỘT hóa đơn trong giao dịch đang mở:
    /// cập nhật số đã trả / còn nợ / trạng thái của hóa đơn, lưu dòng thanh toán,
    /// ghi có vào sổ cái công nợ khách hàng và tạo phiếu thu sổ quỹ. Không tự mở
    /// giao dịch — dùng chung connection/transaction của hàm gọi để nhiều hóa đơn
    /// hoặc phần nợ không theo hóa đơn có thể nằm trong CÙNG một giao dịch.
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
        // Không cho thu vượt quá số còn nợ của hóa đơn.
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

        // Lưu dòng thanh toán.
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

        // Cập nhật lịch sử công nợ khách hàng (ghi có = giảm nợ).
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
                    GhiChu = $"Thu nợ hóa đơn {maHoaDon}"
                },
                transaction);
        }

        // Ghi phiếu thu sổ quỹ (mã PT). KHÔNG gán hoa_don_ban_hang_id vì chỉ mục
        // duy nhất chỉ cho phép 1 phiếu/hóa đơn — phiếu thu đầu tiên đã giữ chỗ đó.
        var maPhieu = await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction);

        // Phiếu thu nợ hóa đơn luôn thuộc loại "Thu khách hàng".
        var idNhomThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "THU_KHACH_HANG");

        var noiDung = $"Thu nợ hóa đơn {maHoaDon}";
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
    /// Thu công nợ theo khách hàng: trừ dần số tiền khách trả vào các hóa đơn còn nợ
    /// (cũ nhất trước), phần vượt quá tổng nợ trên hóa đơn (ví dụ nợ nhập từ KiotViet
    /// không gắn hóa đơn) ghi thẳng vào sổ cái công nợ khách hàng. Mọi hóa đơn được
    /// cấn trừ, dòng thanh toán và phiếu thu sổ quỹ đều nằm trong CÙNG một giao dịch
    /// nên không bao giờ lệch sổ.
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
            "Bạn không có quyền thu công nợ khách hàng.");

        if (khachHangId == 0)
            throw new InvalidOperationException("Thiếu khách hàng cần thu nợ.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền thu phải lớn hơn 0.");

        var now = DateTime.Now;
        var userId = Session.CurrentUser?.Id;

        var coCotLienKet = await EnsurePhieuQuyHoaDonColumnsAsync();

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
                    $"Không tìm thấy khách hàng (id {khachHangId}) trong database.");

            string? tenKhach = string.IsNullOrWhiteSpace(khach.TenKhachHang)
                ? null
                : khach.TenKhachHang.Trim();

            uint chiNhanhCuaKhach = khach.ChiNhanhId ?? (Session.CurrentUser?.ChiNhanhId ?? 1);

            // Các hóa đơn còn nợ của khách, cũ nhất trước, khoá để tránh thu trùng.
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
                "Thu nợ",
                "KHACH_HANG",
                khachHangId,
                null,
                $"Thu nợ khách hàng {(tenKhach ?? khachHangId.ToString())}: {tongDaThu:N0} đ",
                tongDaThu);

            // Đọc lại công nợ còn lại theo cùng cách hiển thị ở danh sách khách hàng.
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
    /// Ghi nhận phần tiền thu nợ không gắn với hóa đơn nào (nợ tổng hợp từ KiotViet
    /// hoặc ghi nợ tay) vào sổ cái công nợ khách hàng và tạo phiếu thu sổ quỹ.
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
                    ? "Thu nợ khách hàng"
                    : $"Thu nợ khách hàng. {ghiChu.Trim()}"
            },
            transaction);

        var maPhieu = await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction);

        // Phiếu thu nợ khách hàng luôn thuộc loại "Thu khách hàng".
        var idNhomThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "THU_KHACH_HANG");

        var noiDung = "Thu nợ khách hàng";
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

    /// <summary>Dòng khách hàng dùng khi thu nợ theo khách hàng.</summary>
    private sealed class KhachHangCongNoRow
    {
        public uint Id { get; set; }
        public string TenKhachHang { get; set; } = "";
        public uint? ChiNhanhId { get; set; }
    }

    /// <summary>Dòng hóa đơn còn nợ dùng khi thu nợ theo hóa đơn/khách hàng.</summary>
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

    /// <summary>Dòng phiếu nhập cần cho việc ghi nhận thanh toán công nợ.</summary>
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
    /// Ghi nhận thanh toán công nợ cho MỘT phiếu nhập hàng. Toàn bộ thao tác nằm
    /// trong MỘT giao dịch:
    /// <list type="number">
    /// <item>Khoá dòng phiếu và kiểm tra còn nợ, số tiền không vượt quá công nợ.</item>
    /// <item>Cập nhật <c>da_thanh_toan</c> / <c>con_no</c> của đầu phiếu.</item>
    /// <item>Lưu một dòng lịch sử vào <c>thanh_toan_nha_cung_cap</c> (mã TTNCC...).</item>
    /// <item>Ghi có vào sổ cái công nợ nhà cung cấp để công nợ giảm đúng số đã trả.</item>
    /// <item>Ghi phiếu chi (mã PC...) vào sổ quỹ, gắn với chính phiếu nhập.</item>
    /// </list>
    /// Trạng thái thanh toán (chưa/một phần/đủ) được SUY RA từ số đã trả và còn nợ,
    /// không lưu thêm cột riêng.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Phiếu không tồn tại, đang lưu tạm/hủy, đã trả đủ, hoặc số tiền không hợp lệ.
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
            "Bạn không có quyền thanh toán công nợ nhà cung cấp.");

        if (string.IsNullOrWhiteSpace(maPhieuNhap))
            throw new InvalidOperationException("Thiếu mã phiếu nhập cần thanh toán.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền thanh toán phải lớn hơn 0.");

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
                throw new InvalidOperationException($"Không tìm thấy phiếu nhập {maCanTim} trong database.");

            if (!TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(phieu.TrangThai))
                throw new InvalidOperationException(
                    $"Phiếu nhập {maCanTim} đang ở trạng thái không thể thanh toán.");

            if (phieu.ConNo <= 0m)
                throw new InvalidOperationException(
                    $"Phiếu nhập {maCanTim} đã thanh toán đủ, không còn công nợ.");

            if (soTien > phieu.ConNo)
                throw new InvalidOperationException(
                    $"Số tiền thanh toán ({soTien:N0} đ) vượt quá công nợ còn lại ({phieu.ConNo:N0} đ).");

            var daTraMoi = phieu.DaThanhToan + soTien;
            var conNoMoi = Math.Max(0m, phieu.TongTienNhap - daTraMoi);
            var phuongThuc = ChuanHoaPhuongThuc(phuongThucThanhToan);

            // 1. Cập nhật đầu phiếu: tổng đã trả tăng, còn nợ giảm.
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

            // 2. Lưu một dòng lịch sử thanh toán riêng cho lần trả này.
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

                // 3. Ghi có sổ cái công nợ nhà cung cấp -> công nợ giảm đúng số đã trả.
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
                        ? $"Thanh toán {soTien:N0} đ cho phiếu {maCanTim}"
                        : $"Thanh toán {soTien:N0} đ cho phiếu {maCanTim}. {ghiChu.Trim()}");
            }

            // 4. Ghi phiếu chi trong sổ quỹ.
            var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);

            // Phiếu chi thanh toán nhập hàng luôn thuộc loại "Chi nhà cung cấp".
            var idNhomChiNhaCungCap = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");

            var tenNcc = string.IsNullOrWhiteSpace(phieu.TenNhaCungCap)
                ? phieu.NhaCungCapId?.ToString()
                : phieu.TenNhaCungCap;
            var noiDung = $"Thanh toán công nợ nhập hàng - phiếu {maCanTim}";
            var ghiChuPhieu = $"Chi trả công nợ cho nhà cung cấp {tenNcc} theo phiếu nhập {maCanTim}.";

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
                "Thanh toán NCC",
                "PHIEU_NHAP",
                phieu.Id,
                maCanTim,
                $"Thanh toán công nợ phiếu {maCanTim}: {soTien:N0} đ (còn nợ {conNoMoi:N0} đ)",
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
                    SafeLog.WriteError("Không rollback được giao dịch thanh toán phiếu nhập", rollbackEx);
                }
            }

            SafeLog.WriteError("Lỗi thanh toán công nợ phiếu nhập", ex);
            throw;
        }
    }

    /// <summary>
    /// Cấp mã lịch sử thanh toán nhà cung cấp kế tiếp (TTNCC0001, TTNCC0002...)
    /// bằng chính connection và transaction đang chạy, tránh trùng mã
    /// (cột <c>ma_thanh_toan</c> có UNIQUE).
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
    /// Cấp mã thanh toán kế tiếp (TT0001, TT0002...) bằng chính connection và
    /// transaction đang chạy, tránh trùng mã (cột <c>ma_thanh_toan</c> có UNIQUE).
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
    /// Trừ tồn kho khi bán hàng và ghi lịch sử nhập/xuất kho, trong CÙNG giao dịch
    /// với hóa đơn. Trước đây luồng bán hàng không hề đụng tới bảng ton_kho nên
    /// tồn kho không bao giờ giảm và không có dấu vết xuất hàng.
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
                $"\"{item.SanPham.TenSanPham}\" chưa có số tồn trong kho, tồn sau khi bán = {tonSau:N0}.");
        else if (tonSau < 0)
            canhBao.Add(
                $"\"{item.SanPham.TenSanPham}\" bán vượt tồn kho (còn {tonTruoc:N0}, bán {item.SoLuong:N0}).");

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
                GhiChu = $"Xuất kho bán hàng theo hóa đơn {invoiceCode}"
            },
            transaction);
    }

    /// <summary>
    /// Ghi phiếu Thu trong sổ quỹ cho một hóa đơn bán hàng, dùng CHUNG connection
    /// và transaction với lần ghi hóa đơn nên hai thao tác cùng thành công hoặc
    /// cùng bị rollback.
    /// Chỉ ghi khi khách trả thật (&gt; 0); tiền thối và phần còn nợ không ghi vào quỹ.
    /// Trả về mã phiếu Thu vừa tạo, rỗng nếu không tạo phiếu.
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

        // Chống tạo trùng: nếu hóa đơn này đã có phiếu thu thì lấy lại mã cũ,
        // không ghi thêm (an toàn kể cả khi giao dịch bị chạy lại).
        if (coCotLienKet)
        {
            var maPhieuDaCo = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT ma_phieu_chi FROM phieu_chi WHERE hoa_don_ban_hang_id = @HoaDonId LIMIT 1;",
                new { HoaDonId = invoiceId },
                transaction);

            if (!string.IsNullOrWhiteSpace(maPhieuDaCo))
                return maPhieuDaCo;
        }

        // Cấp mã PT ngay trong giao dịch để không trùng với phiếu đang mở ở máy khác.
        var maPhieu = await SinhMaPhieuThuTrongGiaoDichAsync(connection, transaction);

        // Phiếu thu bán hàng luôn thuộc loại "Thu khách hàng".
        var idNhomThuKhachHang = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "THU_KHACH_HANG");

        var ghiChu = $"Thu tiền thanh toán hóa đơn {invoiceCode}.";

        if (changeAmount > 0)
            ghiChu += $" Tiền thối {changeAmount:N0} đã trả lại khách, không tính vào quỹ.";

        if (debtAmount > 0)
            ghiChu += $" Còn nợ {debtAmount:N0} ghi vào sổ công nợ khách hàng.";

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
                NoiDung = $"Thu tiền bán hàng - hóa đơn {invoiceCode}",
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
    /// Chuẩn hóa mã phương thức thanh toán cho cột <c>phieu_chi.phuong_thuc</c>
    /// (varchar(30)). Mã rỗng/không hợp lệ quay về "TIEN_MAT".
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
    /// Cấp mã phiếu Thu kế tiếp (PT0001, PT0002...) bằng chính connection và
    /// transaction của giao dịch đang chạy, tránh mở thêm kết nối và tránh trùng mã.
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
    /// Cấp mã phiếu Chi kế tiếp (PC0001, PC0002...) bằng chính connection và
    /// transaction của giao dịch đang chạy, tránh mở thêm kết nối và tránh trùng mã.
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
    /// Sinh mã nhà cung cấp tuần tự theo dạng NCC0000001.
    /// Chỉ dùng các mã NCC + số tối đa 7 chữ số để không bị ảnh hưởng
    /// bởi các mã cũ dạng timestamp.
    /// </summary>
    // ==========================================
    // DANH SÁCH NHÀ CUNG CẤP: LỌC + PHÂN TRANG SERVER-SIDE
    // ==========================================

    /// <summary>
    /// Mệnh đề FROM dùng chung cho cả truy vấn lấy trang và truy vấn đếm/thống kê,
    /// để danh sách và các thẻ thống kê luôn nói về cùng một tập nhà cung cấp.
    /// Công nợ, tổng mua và tổng mua trừ trả hàng đều lấy từ cùng một biểu thức
    /// trong <see cref="TaoBieuThucTienNhaCungCap"/> nên không cần JOIN bảng view.
    /// </summary>
    private const string NhaCungCapFrom = """
    FROM nha_cung_cap ncc
    """;

    /// <summary>
    /// Danh sách cột của bảng nhà cung cấp, dùng chung cho nhiều câu SQL.
    /// Ba cột tiền dùng đúng biểu thức mà bộ lọc và thẻ thống kê đang dùng nên
    /// số tiền hiển thị ở mọi nơi luôn khớp nhau.
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
    /// Các cột lấy từ file danh sách (đã lưu xuống database) dành cho truy vấn
    /// chi tiết một nhà cung cấp. Không dùng cho bảng danh sách vì chỉ cần ba
    /// cột tiền đã được tính sẵn trong <see cref="TaoDanhSachCotNhaCungCap"/>.
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
    /// Sinh mệnh đề WHERE từ bộ lọc. Mọi điều kiện đều là tham số hóa nên an toàn
    /// với dấu nháy trong từ khóa, và đều được đẩy xuống MySQL xử lý.
    /// Lọc công nợ dùng chính biểu thức công nợ của bảng danh sách để người
    /// dùng không lọc ra một danh sách mà cột "Còn nợ" lại hiển thị khác.
    /// </summary>
    private static string TaoDieuKienNhaCungCap(BoLocNhaCungCap loc, string bieuThucConNo)
    {
        var dieuKien = new StringBuilder();

        dieuKien.Append("ncc.trang_thai = 1");

        // Ô tìm kiếm: mã NCC, tên, số điện thoại, email.
        dieuKien.Append(
            " AND (@TimKiem = ''"
            + " OR ncc.ma_nha_cung_cap LIKE @TimKiemLike"
            + " OR ncc.ten_nha_cung_cap LIKE @TimKiemLike"
            + " OR COALESCE(ncc.so_dien_thoai, '') LIKE @TimKiemLike"
            + " OR COALESCE(ncc.email, '') LIKE @TimKiemLike)");

        // Từng ô của bộ lọc chi tiết.
        dieuKien.Append(" AND (@MaNhaCungCap = '' OR ncc.ma_nha_cung_cap LIKE @MaNhaCungCapLike)");
        dieuKien.Append(" AND (@TenNhaCungCap = '' OR ncc.ten_nha_cung_cap LIKE @TenNhaCungCapLike)");
        dieuKien.Append(" AND (@SoDienThoai = '' OR COALESCE(ncc.so_dien_thoai, '') LIKE @SoDienThoaiLike)");
        dieuKien.Append(" AND (@Email = '' OR COALESCE(ncc.email, '') LIKE @EmailLike)");

        // Địa chỉ khớp cả 4 cột địa chỉ để lọc được cả khi người dùng chỉ nhớ
        // tên phường/xã, quận/huyện hoặc tỉnh/thành.
        dieuKien.Append(
            " AND (@DiaChi = ''"
            + " OR COALESCE(ncc.dia_chi, '') LIKE @DiaChiLike"
            + " OR COALESCE(ncc.phuong_xa, '') LIKE @DiaChiLike"
            + " OR COALESCE(ncc.quan_huyen, '') LIKE @DiaChiLike"
            + " OR COALESCE(ncc.tinh_thanh, '') LIKE @DiaChiLike)");

        // Trạng thái công nợ: tất cả / còn nợ / không còn nợ.
        dieuKien.Append(
            " AND (@CongNoMode = 0"
            + $" OR (@CongNoMode = 1 AND COALESCE({bieuThucConNo}, 0) > 0)"
            + $" OR (@CongNoMode = 2 AND COALESCE({bieuThucConNo}, 0) = 0))");

        // Khoảng công nợ.
        dieuKien.Append($" AND (@ConNoTu IS NULL OR COALESCE({bieuThucConNo}, 0) >= @ConNoTu)");
        dieuKien.Append($" AND (@ConNoDen IS NULL OR COALESCE({bieuThucConNo}, 0) <= @ConNoDen)");

        return dieuKien.ToString();
    }

    /// <summary>Gom bộ lọc thành đúng bộ tham số mà các câu SQL kia đang mong đợi.</summary>
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
    /// Lấy một trang nhà cung cấp theo bộ lọc, kèm tổng số bản ghi khớp bộ lọc.
    /// Chỉ đọc đúng <paramref name="pageSize"/> dòng (LIMIT/OFFSET), không bao giờ
    /// kéo toàn bộ bảng nha_cung_cap về rồi mới lọc ở C#.
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
    /// Lấy TOÀN BỘ nhà cung cấp khớp bộ lọc để xuất file Excel, không phân trang.
    ///
    /// Cố tình dùng chung <see cref="TaoDieuKienNhaCungCap"/> và chung bộ cột
    /// tiền với truy vấn lấy danh sách trên màn hình, nên số tiền xuất ra file
    /// luôn khớp với thẻ thống kê và cột "Tổng mua"/"Còn nợ" đang hiển thị.
    /// Không có LIMIT/OFFSET nên xuất 48 dòng khi trang đang xem 20 dòng.
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
    /// Thống kê phía trên bảng nhà cung cấp. Cùng bộ lọc với truy vấn lấy danh
    /// sách nên khi đổi bộ lọc thì cả bảng lẫn thống kê cùng cập nhật.
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

    /// <summary>Lấy đầy đủ thông tin một nhà cung cấp để mở vùng chi tiết / form sửa.</summary>
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
    /// Lịch sử nhập hàng của một nhà cung cấp. Chỉ gọi khi người dùng mở vùng
    /// chi tiết, và chỉ lấy <paramref name="limit"/> phiếu gần nhất.
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
    /// Sổ cái công nợ của một nhà cung cấp (bảng lich_su_cong_no_nha_cung_cap).
    /// Cũng chỉ đọc khi người dùng mở chi tiết.
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
    /// Tổng hợp tài chính của một nhà cung cấp: tổng tiền nhập, đã thanh toán,
    /// còn nợ (theo sổ cái công nợ nếu có, không thì theo phiếu nhập hàng).
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
    /// Trong số mã nhà cung cấp được truyền vào, trả về tập mã đã tồn tại trong
    /// database (kể cả nhà cung cấp đã bị ẩn). Dùng cho bước kiểm tra trùng khi
    /// import: chỉ hỏi đúng các mã trong file thay vì tải toàn bộ danh sách NCC.
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
            throw new InvalidOperationException("Đã vượt giới hạn mã nhà cung cấp tự động NCC9999999.");

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
            // Bỏ qua nếu không đủ quyền ALTER hoặc cột đã thỏa mãn
            _daMoRongCotSoDienThoaiNcc = true;
        }
    }

    public async Task<uint> ThemNhaCungCapAsync(NhaCungCap supplier)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersCreate, "Bạn không có quyền thêm nhà cung cấp.");
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        await using var connection = await OpenAsync();
        await DamBaoDoDaiSoDienThoaiNhaCungCapAsync(connection);

        // Bản đầy đủ ghi cả các cột lấy từ file danh sách nhà cung cấp. Nếu bảng
        // cũ không thêm được cột thì dùng bản rút gọn như trước để vẫn nhập được.
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
                @NoCanTraHienTaiExcel, @TongMuaExcel, @TongMuaTruTraHangExcel,
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersEdit, "Bạn không có quyền sửa nhà cung cấp.");
        await using var connection = await OpenAsync();
        await DamBaoDoDaiSoDienThoaiNhaCungCapAsync(connection);

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
            ngay_cap_nhat = NOW()
        WHERE id = @Id AND trang_thai = 1;
        """;

        var affectedRows = await connection.ExecuteAsync(sql, supplier);
        if (affectedRows == 0)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã bị xóa.");
    }

    /// <summary>
    /// Cập nhật nhà cung cấp với đầy đủ dữ liệu đọc từ file danh sách, bao gồm
    /// ba cột tiền, người tạo, ngày tạo và các cột chữ còn lại. Chỉ dùng cho
    /// bước cập nhật khi nhập file, không dùng cho form sửa (form sửa không
    /// được phép xoá số tiền lấy từ file).
    /// </summary>
    public async Task CapNhatNhaCungCapTuFileAsync(NhaCungCap supplier)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersEdit, "Bạn không có quyền sửa nhà cung cấp.");
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersDelete, "Bạn không có quyền xóa nhà cung cấp.");
        await using var connection = await OpenAsync();

        var affectedRows = await connection.ExecuteAsync(
            "UPDATE nha_cung_cap SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id AND trang_thai = 1;",
            new { Id = supplierId });

        if (affectedRows == 0)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại hoặc đã bị xóa.");
    }

    public async Task<int> ImportNhaCungCapTuFileAsync(string filePath)
        => (await ImportNhaCungCapTuFileAsync(filePath, null, null)).SoThemMoi;

    public async Task<int> ImportNhaCungCapTuFileAsync(string filePath, IReadOnlyCollection<NhaCungCap>? suppliersOverride)
        => (await ImportNhaCungCapTuFileAsync(filePath, suppliersOverride, null)).SoThemMoi;

    /// <summary>
    /// Nhập danh sách nhà cung cấp từ file và trả về báo cáo chi tiết để
    /// người dùng biết chính xác tổng số dòng, số thành công, số bỏ qua và số lỗi.
    /// Mã trùng với database hoặc trùng ngay trong file đều được bỏ qua an toàn,
    /// trừ khi <paramref name="capNhatNhaCungCapDaTonTai"/> = true: khi đó nhà cung
    /// cấp có mã đã có trong database được cập nhật lại bằng dữ liệu của file.
    /// </summary>
    public async Task<KetQuaImportNhaCungCap> ImportNhaCungCapTuFileAsync(
        string filePath,
        IReadOnlyCollection<NhaCungCap>? suppliersOverride,
        IProgress<int>? progress,
        bool capNhatNhaCungCapDaTonTai = false)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SuppliersImport, "Bạn không có quyền import nhà cung cấp.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file danh sách nhà cung cấp.", filePath);

        var suppliers = suppliersOverride ?? ParseNhaCungCapFromFile(filePath);

        // Khi chọn cập nhật nhà cung cấp đã có, bảo đảm các cột lưu dữ liệu từ
        // file đã tồn tại trước khi UPDATE, nếu không toàn bộ file sẽ bị lỗi.
        if (capNhatNhaCungCapDaTonTai)
            await DamBaoCotDuLieuKiotVietNhaCungCapAsync();

        var result = new KetQuaImportNhaCungCap
        {
            TenFile = Path.GetFileName(filePath),
            SoDongExcel = suppliers.Count
        };

        if (suppliers.Count == 0)
            return result;

        // Ô tên trống thì không tạo được nhà cung cấp: bỏ qua và ghi log,
        // không ném lỗi để phần còn lại của file vẫn được nhập.
        var hopLe = new List<NhaCungCap>(suppliers.Count);

        for (var i = 0; i < suppliers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(suppliers.ElementAt(i).TenNhaCungCap))
            {
                result.SoLoi++;
                GhiLoi(result, $"Dòng {i + 2}: thiếu tên nhà cung cấp.");
            }
            else
            {
                hopLe.Add(suppliers.ElementAt(i));
            }
        }

        result.SoNhaCungCapHopLe = hopLe.Count;

        if (hopLe.Count == 0)
            return result;

        // Chỉ hỏi database về những mã thực sự có trong file, không tải toàn bộ danh sách.
        var maTrongDatabase = await LayMaNhaCungCapDaTonTaiAsync(
            hopLe.Select(ncc => ncc.MaNhaCungCap));

        var maDaDung = new HashSet<string>(maTrongDatabase, StringComparer.OrdinalIgnoreCase);

        // Cảnh báo mềm: tên + số điện thoại trùng nhà cung cấp đã có.
        // Bảng không có UNIQUE cho hai cột này nên vẫn nhập, chỉ báo cho người dùng.
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
                // Mã đã có trong database.
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
                        GhiLoi(result, $"Dòng {i + 2} ({ma}): {ex.Message}");
                    }
                }
                else
                {
                    result.SoTrungMa++;
                }
            }
            else if (!maDaDung.Add(ma))
            {
                // Mã bị trùng ngay trong chính file này.
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
                    // Có tiến trình khác vừa chèn mã này: bỏ qua và tiếp tục.
                    result.SoTrungMa++;
                }
                catch (Exception ex)
                {
                    result.SoLoi++;
                    GhiLoi(result, $"Dòng {i + 2} ({ma}): {ex.Message}");
                }
            }
        }

        progress?.Report(hopLe.Count);

        return result;
    }

    /// <summary>
    /// Đếm số nhà cung cấp trong danh sách đang xét trùng (tên + số điện thoại)
    /// với nhà cung cấp đã có trong database. Chỉ dùng để báo người dùng.
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
        // Chỉ giữ 20 lỗi đầu để thông báo không bị quá dài.
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
                "Đường dẫn file không hợp lệ.",
                nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Không tìm thấy file danh sách nhà cung cấp.",
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
            "File không được hỗ trợ. Vui lòng chọn file Excel (.xlsx) hoặc CSV.");
    }
    private static List<Dictionary<string, string>> ReadSupplierExcelRows(
    string filePath)
    {
        using var workbook = new XLWorkbook(filePath);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
            throw new InvalidOperationException(
                "File Excel không chứa sheet dữ liệu hợp lệ.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastRow == 0)
            return [];

        // Tìm dòng tiêu đề nhà cung cấp
        var headerRowNumber = FindSupplierHeaderRow(
            worksheet,
            lastRow);

        if (headerRowNumber == 0)
        {
            throw new InvalidOperationException(
                "Không tìm thấy dòng tiêu đề nhà cung cấp trong file.");
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
            GetCellValue(row, "no_can_tra_hien_tai", "nợ cần trả hiện tại", "no can tra hien tai", "nợ hiện tại", "no hien tai", "nợ cần trả"));
        var tongMua = ParseSoTien(
            GetCellValue(row, "tong_mua", "tổng mua", "tong mua"));
        var tongMuaTruTraHang = ParseSoTien(
            GetCellValue(row, "tong_mua_tru_tra_hang", "tổng mua trừ trả hàng", "tong mua tru tra hang", "tổng mua trừ trả", "tong mua tru tra"));

        var trangThai = ParseBoolean(
            GetCellValue(row, "trang_thai", "trạng thái", "trang thai", "status"),
            true);

        var supplier = new NhaCungCap
        {
            MaNhaCungCap = GetCellValue(row, "ma_nha_cung_cap", "mã nhà cung cấp", "ma ncc", "supplier code") ?? string.Empty,
            TenNhaCungCap = GetCellValue(row, "ten_nha_cung_cap", "tên nhà cung cấp", "ten ncc", "supplier name", "name") ?? string.Empty,
            SoDienThoai = GetCellValue(row, "so_dien_thoai", "số điện thoại", "dien thoai", "phone", "sdt"),
            Email = GetCellValue(row, "email"),
            DiaChi = GetCellValue(row, "dia_chi", "địa chỉ", "dia chi", "address"),
            PhuongXa = GetCellValue(row, "phuong_xa", "phường/xã", "phuong xa", "ward"),
            QuanHuyen = GetCellValue(row, "quan_huyen", "quận/huyện", "quan huyen", "district"),
            TinhThanh = GetCellValue(row, "tinh_thanh", "tỉnh/thành", "tinh thanh", "province"),
            MaSoThue = GetCellValue(row, "ma_so_thue", "mã số thuế", "ma so thue", "tax code"),
            NguoiLienHe = GetCellValue(row, "nguoi_lien_he", "người liên hệ", "lien he", "contact"),
            GhiChu = GetCellValue(row, "ghi_chu", "ghi chú", "note"),
            KhuVuc = GetCellValue(row, "khu_vuc", "khu vực", "khu vuc", "region"),
            SoCccd = GetCellValue(row, "so_cmnd_cccd", "số cmnd/cccd", "so cmnd cccd", "cmnd", "cccd"),
            NhomNhaCungCapTen = GetCellValue(row, "nhom_nha_cung_cap", "nhóm nhà cung cấp", "nhom ncc", "nhóm"),
            TenCongTy = GetCellValue(row, "cong_ty", "công ty", "ten cong ty"),
            NguoiTaoTen = GetCellValue(row, "nguoi_tao", "người tạo", "nguoi tao", "nguoi_tao_ten"),
            NgayTao = ParseNgayTaiFile(GetCellValue(row, "ngay_tao", "ngày tạo", "ngay tao")),
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
    // QUẢN LÝ NHÓM SẢN PHẨM & ĐƠN VỊ TÍNH
    // ==========================================

    public async Task XoaNhomSanPhamAsync(uint id)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SettingsEdit, "Bạn không có quyền xóa nhóm hàng hóa.");
        await using var connection = await OpenAsync();
        const string sql = "UPDATE nhom_san_pham SET trang_thai = 0 WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task XoaDonViTinhAsync(uint id)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SettingsEdit, "Bạn không có quyền xóa đơn vị tính.");
        await using var connection = await OpenAsync();
        const string sql = "UPDATE don_vi_tinh SET trang_thai = 0 WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    // ==========================================
    // PHIẾU NHẬP HÀNG
    // ==========================================

    /// <summary>
    /// Sinh mã phiếu nhập tự động dạng PN000001, PN000002, PN000003...
    /// Số thứ tự lấy từ MAX của phần số trong các mã ĐÚNG ĐỊNH DẠNG:
    ///   - Mã mới          : PN000001    (bỏ 2 ký tự "PN", phần còn lại "000001")
    ///   - Mã cũ có ngày  : PN260929-001 (bỏ "PN", lấy đoạn sau dấu '-' cuối: "001")
    /// Mọi mã lệch định dạng (NULL, rỗng, chứa chữ) bị loại bằng REGEXP nên dữ liệu
    /// rác không làm sai/hỏng số thứ tự và không làm ứng dụng bị lỗi.
    /// </summary>
    public async Task<string> TaoMaPhieuNhapMoiAsync()
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.PurchasesCreate, "Bạn không có quyền tạo phiếu nhập hàng.");
        await using var connection = await OpenAsync();

        // SUBSTRING(ma, 3) bỏ đúng 2 ký tự tiền tố "PN" (SQL đếm từ 1).
        // SUBSTRING_INDEX(..., '-', -1) lấy đoạn sau dấu '-' cuối cùng để vẫn lấy
        // đúng số thứ tự với mã cũ dạng PNyyMMdd-NNN.
        //
        // CAST(... AS SIGNED) để giá trị trả về luôn nằm trong kiểu long.
        // Nếu dùng AS UNSIGNED, chuỗi bắt đầu bằng '-' sẽ ra số khổng lồ và làm
        // ExecuteScalarAsync<long> ném exception.
        const string maxSuffixSql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING_INDEX(SUBSTRING(TRIM(ma_phieu_nhap), 3), '-', -1) AS SIGNED)),
            0
        )
        FROM phieu_nhap_hang
        WHERE TRIM(ma_phieu_nhap) REGEXP '^PN[0-9]{6,}(-[0-9]+)?$';
        """;

        var maxSuffix = await connection.ExecuteScalarAsync<long?>(maxSuffixSql) ?? 0L;

        // Database chưa có phiếu nào (hoặc mã toàn sai định dạng) => bắt đầu từ PN000001.
        var nextNumber = maxSuffix > 0 ? maxSuffix + 1 : 1L;

        // ma_phieu_nhap có UNIQUE KEY uk_pnh_ma: nếu mã vừa sinh trùng
        // (ví dụ 2 máy cùng mở màn hình) thì tăng tiếp tới khi ra mã còn trống.
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
            "Không sinh được mã phiếu nhập: đã thử 100 lần liên tiếp mà mã vẫn bị trùng.");
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
    /// Lấy danh sách phiếu nhập theo trang cho báo cáo.
    /// Hỗ trợ tìm kiếm theo mã phiếu, mã NCC, tên NCC và lọc theo tình trạng thanh toán.
    /// Chỉ đọc dữ liệu của trang hiện tại (LIMIT/OFFSET) kèm tổng số bản ghi,
    /// không tải toàn bộ bảng lên giao diện.
    /// </summary>
    public async Task<(IReadOnlyList<PhieuNhapHang> Items, int TongSoBanGhi)> GetPhieuNhapHangPagedAsync(
        string? search,
        int page,
        int pageSize,
        string? tinhTrangThanhToan = null)
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
          {paymentCondition};
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
          {paymentCondition}
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC
        LIMIT @PageSize OFFSET @Offset;
        """;

        await using var connection = await OpenAsync();

        var parameters = new
        {
            Search = keyword,
            PageSize = pageSize,
            Offset = offset
        };

        var tongSoBanGhi = Convert.ToInt32(
            await connection.ExecuteScalarAsync<long>(countSql, parameters));

        var items = (
            await connection.QueryAsync<PhieuNhapHang>(pageSql, parameters)
        ).AsList();

        return (items, tongSoBanGhi);
    }

    /// <summary>
    /// Đọc đầy đủ một phiếu nhập theo khoá chính phieu_nhap_hang.id để hiển thị
    /// cửa sổ "Xem chi tiết": header + toàn bộ dòng hàng trong
    /// chi_tiet_phieu_nhap_hang (nối san_pham/don_vi_tinh để lấy đơn vị tính).
    /// Trả về null nếu không tìm thấy phiếu. Chỉ đọc, không sửa.
    /// </summary>
    public async Task<PhieuNhapHangDetail?> GetPhieuNhapHangDetailAsync(uint phieuNhapId)
    {
        if (phieuNhapId == 0)
            return null;

        await using var connection = await OpenAsync();

        // Hai cột so_hoa_don_dau_vao / ma_dat_hang_nhap là tuỳ chọn (database cũ
        // chưa có); chỉ thêm vào câu SQL khi thực sự tồn tại.
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
    /// Hoàn thành phiếu nhập: lưu header, toàn bộ dòng chi tiết, cộng tồn kho,
    /// ghi lịch sử nhập xuất kho và cập nhật lại giá vốn bình quân — tất cả trong
    /// một transaction, hoặc rollback trọn vẹn nếu có bước nào lỗi.
    /// </summary>
    public async Task<uint> LuuPhieuNhapHangAsync(
        PhieuNhapHang phieu,
        IReadOnlyList<ChiTietPhieuNhapHang> items)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.PurchasesCreate, "Bạn không có quyền lưu phiếu nhập hàng.");
        if (phieu is null)
            throw new ArgumentNullException(nameof(phieu));

        if (items is null)
            throw new ArgumentNullException(nameof(items));

        var dongHopLe = items
            .Where(item => item.SanPham.Id != 0 && item.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
            throw new InvalidOperationException("Phiếu nhập chưa có hàng hóa nào hợp lệ.");

        // Chặn ở tầng service: mã phiếu rỗng hoặc sai định dạng tuyệt đối không được
        // ghi xuống phieu_nhap_hang (ràng buộc UNIQUE uk_pnh_ma chỉ bắt được mã rỗng).
        if (string.IsNullOrWhiteSpace(phieu.MaPhieuNhap))
            throw new InvalidOperationException("Mã phiếu nhập rỗng, không thể lưu phiếu nhập.");

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
            // 0. CHỐN GHI TRÙNG KHI BẤM "HOÀN THÀNH" NHIỀU LẦN
            //    Đọc trạng thái hiện tại trong transaction để tồn kho
            //    không bị cộng hai lần cho cùng một phiếu.
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

            // Phiếu đã hoàn thành thì tồn kho đã được cộng một lần,
            // không được cộng thêm lần nữa.
            if (trangThaiHienTai == "DA_NHAP_HANG")
                throw new InvalidOperationException(
                    "Phiếu nhập này đã hoàn thành trước đó. Tồn kho đã được cập nhật nên không thể hoàn thành lại.");

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
            // 2. CHI TIẾT PHIẾU NHẬP
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
            // 3. CẬP NHẬT TỒN KHO + LỊCH SỬ NHẬP XUẤT KHO
            // ============================================================

            await CapNhatTonKhoPhieuNhapAsync(
                connection,
                transaction,
                phieu,
                dongHopLe);

            // ============================================================
            // 4. CÔNG NỢ NHÀ CUNG CẤP
            //    Ghi vào lich_su_cong_no_nha_cung_cap ngay trong giao dịch này
            //    để số dư công nợ khách mới luôn khớp với phiếu vừa lưu.
            // ============================================================

            await GhiCongNoNhaCungCapPhieuNhapAsync(
                connection,
                transaction,
                phieu,
                branchId);

            // ============================================================
            // 5. SỔ QUỸ (PHIẾU CHI)
            //    Nếu phiếu nhập có thanh toán tiền (DaThanhToan > 0), ghi
            //    phiếu chi tương ứng vào sổ quỹ để số dư quỹ tự động giảm đúng nguồn tiền.
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
                "Nhập hàng",
                "PHIEU_NHAP",
                phieu.Id,
                phieu.MaPhieuNhap,
                $"Nhập hàng hóa {phieu.MaPhieuNhap} (Tổng tiền nhập: {phieu.TongTienNhap:N0} đ)",
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
    /// Ghi sổ cái công nợ nhà cung cấp cho một phiếu nhập, trong CÙNG transaction
    /// với lần lưu phiếu: một dòng ghi khoản phải trả khi nhập hàng và một dòng
    /// ghi khoản đã trả nếu phiếu có ghi "đã thanh toán".
    /// Tổng ghi nợ - ghi có của mỗi phiếu luôn bằng <c>phieu.ConNo</c> nên số dư
    /// công nợ khớp với cột con_no trên đầu phiếu.
    /// </summary>
    private static async Task GhiCongNoNhaCungCapPhieuNhapAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PhieuNhapHang phieu,
        uint branchId)
    {
        if (phieu.NhaCungCapId is not uint nccId || nccId == 0)
            return;

        // Chống ghi trùng khi một phiếu được sửa rồi hoàn thành lại:
        // xoá các dòng cũ của chính mã phiếu này trước khi ghi lại.
        await connection.ExecuteAsync(
            "DELETE FROM lich_su_cong_no_nha_cung_cap WHERE ma_chung_tu = @MaChungTu;",
            new { phieu.MaPhieuNhap },
            transaction);

        // Khoản mua làm tăng nợ phải trả, nên ghi nợ.
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
                $"Nhập hàng tạo khoản phải trả {phieu.TongTienNhap:N0} cho phiếu {phieu.MaPhieuNhap}");
        }

        // Khoản đã trả làm giảm nợ phải trả, nên ghi có.
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
                $"Đã thanh toán {phieu.DaThanhToan:N0} cho phiếu {phieu.MaPhieuNhap}");
        }
    }

    /// <summary>
    /// Ghi phiếu Chi trong sổ quỹ cho một phiếu nhập hàng có thanh toán tiền (DaThanhToan > 0),
    /// dùng CHUNG connection và transaction với lần ghi phiếu nhập.
    /// Giúp sổ quỹ và các quỹ tiền mặt/ngân hàng phản ánh chính xác nguồn tiền chi trả khi nhập hàng.
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
        // 1. Dọn dẹp phiếu chi cũ nếu phiếu nhập được cập nhật lại / hoàn thành lại
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

        // 2. Nếu phiếu có thanh toán tiền (> 0), tạo phiếu chi mới
        if (phieu.DaThanhToan > 0)
        {
            var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);

            // Phiếu chi thanh toán nhập hàng luôn thuộc loại "Chi nhà cung cấp".
            var idNhomChiNhaCungCap = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");

            var phuongThuc = ChuanHoaPhuongThuc(phieu.PhuongThucThanhToan);
            var noiDung = $"Thanh toán tiền nhập hàng - phiếu {phieu.MaPhieuNhap}";
            var tenNcc = string.IsNullOrWhiteSpace(phieu.TenNhaCungCap) ? phieu.NhaCungCapId?.ToString() : phieu.TenNhaCungCap;
            var ghiChu = $"Chi trả tiền nhập hàng cho nhà cung cấp {tenNcc} theo phiếu nhập {phieu.MaPhieuNhap}.";

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
    /// Ghi một dòng vào sổ công nợ nhà cung cấp, tự tính lại số dư cộng dồn
    /// (đọc khoá dòng hiện tại để hai phiếu song song không ghi đè số dư của nhau).
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

            // Giá vốn bình quân = (tồn cũ * giá cũ + lượng nhập * giá nhập) / tồn sau
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
    /// Hai cột so_hoa_don_dau_vao / ma_dat_hang_nhap là tuỳ chọn.
    /// Kiểm tra information_schema một lần để database cũ vẫn chạy được.
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
    /// Bảo đảm bảng phieu_nhap_hang có cột phuong_thuc_thanh_toan để lưu nguồn tiền chi trả
    /// (TIEN_MAT, CHUYEN_KHOAN...). Nếu database cũ chưa có thì tự động ALTER TABLE.
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
    /// Bảo đảm bảng phieu_chi có cột liên kết phiếu nhập hàng:
    /// phieu_nhap_hang_id và ma_phieu_nhap.
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
    /// Bảng <c>phieu_chi</c> dùng chung cho cả phiếu thu và phiếu chi. Ngoài cột
    /// <c>noi_dung</c>, sổ quỹ cần tách riêng <c>nguoi_nop_nhan</c> (người nộp với
    /// phiếu thu, người nhận với phiếu chi) và <c>ghi_chu</c>; trước đây hai thông
    /// tin này bị dồn chung vào <c>noi_dung</c> nên không hiển thị tách biệt được.
    /// <para>
    /// Hai cột là bắt buộc nên hàm này tự thêm vào database cũ thay vì chỉ báo lỗi.
    /// Việc kiểm tra chỉ làm một lần rồi cache. Nếu tài khoản MySQL không có quyền
    /// ALTER TABLE thì hàm trả về false và các câu SQL sổ quỹ tự bỏ qua hai cột này
    /// để ứng dụng vẫn chạy được với dữ liệu cũ.
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

            // ADD COLUMN không có IF NOT EXISTS trên MySQL nên phải kiểm tra từng cột.
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
            // Không có quyền ALTER hoặc mạng lỗi: chạy tiếp với cấu trúc cũ.
            _hasPhieuQuyExtraColumns = false;
        }

        return _hasPhieuQuyExtraColumns.Value;
    }

    /// <summary>
    /// Bảo đảm <c>phieu_chi</c> có 2 cột liên kết hóa đơn bán hàng:
    /// <c>hoa_don_ban_hang_id</c> (UNIQUE) và <c>ma_hoa_don</c>.
    /// Phiếu thu sinh ra khi bán hàng sẽ gắn vào đây nên mỗi hóa đơn chỉ có
    /// tối đa một phiếu thu — UNIQUE index chặn trùng ngay ở tầng database, không
    /// phụ thuộc code kiểm tra của ứng dụng.
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

            // MySQL không có ADD COLUMN IF NOT EXISTS nên phải kiểm tra từng cột.
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
                // Cột cho phép NULL nên phiếu thu/chi tay vẫn insert bình thường,
                // chỉ mỗi hóa đơn bán hàng mới bị giới hạn 1 phiếu thu.
                await connection.ExecuteAsync(
                    "CREATE UNIQUE INDEX uk_phieu_chi_hoa_don ON phieu_chi (hoa_don_ban_hang_id);");
            }

            _hasPhieuQuyHoaDonColumns = true;
        }
        catch
        {
            // Thiếu quyền ALTER: vẫn chạy tiếp, code sẽ tự kiểm tra trùng
            // bằng SELECT trước khi INSERT.
            _hasPhieuQuyHoaDonColumns = false;
        }

        return _hasPhieuQuyHoaDonColumns.Value;
    }

    /// <summary>
    /// Trả về id của một loại thu/chi theo mã (<c>nhom_chi_phi.ma_nhom</c>),
    /// <c>null</c> nếu danh mục chưa tồn tại. Dùng để gắn đúng loại
    /// "Thu khách hàng" / "Chi nhà cung cấp" cho các phiếu quỹ do hệ thống tự
    /// sinh (bán hàng, thu nợ khách hàng, thanh toán nhập hàng).
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
    /// Gắn loại thu/chi cho các phiếu quỹ cũ do hệ thống tự sinh nhưng còn để
    /// <c>nhom_chi_phi_id = NULL</c>: phiếu thu bán hàng / thu nợ khách hàng
    /// (mã hóa đơn, liên kết hóa đơn hoặc nội dung "Thu nợ...") và phiếu chi
    /// thanh toán nhập hàng (liên kết phiếu nhập). Nhờ vậy bộ lọc
    /// "Thu khách hàng" / "Chi nhà cung cấp" lọc đúng cả dữ liệu cũ.
    /// <para>
    /// Chỉ chạy một lần, chỉ đụng các dòng đang NULL nên chạy lại vẫn an toàn
    /// và không bao giờ ghi đè loại mà người dùng đã chọn.
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
                // Phiếu thu bán hàng có liên kết hóa đơn hoặc mã hóa đơn; phiếu
                // thu nợ có nội dung bắt đầu bằng "Thu nợ".
                var nhanDienKhachHang = coCotHoaDon
                    ? "(pc.hoa_don_ban_hang_id IS NOT NULL"
                      + " OR NULLIF(TRIM(pc.ma_hoa_don), '') IS NOT NULL"
                      + " OR COALESCE(pc.noi_dung, '') LIKE 'Thu nợ%')"
                    : "COALESCE(pc.noi_dung, '') LIKE 'Thu nợ%'";

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
                    : "COALESCE(pc.noi_dung, '') LIKE 'Thanh toán%nhập hàng%'";

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
            // Best-effort: lỗi migration không được làm hỏng việc nạp sổ quỹ.
        }
    }

    /// <summary>
    /// Bảo đảm bảng hoa_don_ban_hang có 3 cột:
    /// so_dien_thoai, dia_chi, in_kem_thong_tin.
    /// Nếu database chưa có thì tự động ALTER TABLE trước khi mở transaction.
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
    // SỔ QUỸ - PHIẾU THU / PHIẾU CHI
    // Database hiện có bảng `phieu_chi` (chưa có bảng `phieu_thu`),
    // nên cả dãy mã PT và PC đều lấy số thứ tự lớn nhất từ `phieu_chi`.
    // ================================================================

    /// <summary>
    /// Sinh mã phiếu sổ quỹ tự động: "PT0001", "PT0002"... hoặc "PC0001", "PC0002"...
    /// Số thứ tự lấy từ MAX phần số của các mã ĐÚNG ĐỊNH DẠNG có cùng tiền tố
    /// (không dùng COUNT(*) để tránh trùng khi dữ liệu bị xóa).
    /// Mã NULL/rỗng/không đúng định dạng bị loại bằng REGEXP nên dữ liệu rác không làm hỏng số thứ tự.
    /// Mọi lỗi kết nối đều được nuốt và trả về mã mặc định để ứng dụng không bị crash.
    /// </summary>
    public async Task<string> TaoMaPhieuQuyMoiAsync(string tienTo)
    {
        var prefix = string.IsNullOrWhiteSpace(tienTo)
            ? "PT"
            : tienTo.Trim().ToUpperInvariant();

        try
        {
            await using var connection = await OpenAsync();

            // REGEXP loại bỏ mã rỗng / sai định dạng (VD "PCxxxx").
            // Collation utf8mb4_unicode_ci nên 'pt0001' cũng khớp với 'PT0001'.
            var maxSuffix = await connection.ExecuteScalarAsync<long?>(
                """
                SELECT COALESCE(MAX(CAST(SUBSTRING(TRIM(ma_phieu_chi), 3) AS SIGNED)), 0)
                FROM phieu_chi
                WHERE ma_phieu_chi LIKE CONCAT(@Prefix, '%')
                  AND TRIM(ma_phieu_chi) REGEXP CONCAT('^', @Prefix, '[0-9]+$');
                """,
                new { Prefix = prefix }) ?? 0L;

            // Chưa có phiếu nào (hoặc mã toàn sai định dạng) => bắt đầu từ 0001.
            var nextNumber = maxSuffix > 0 ? maxSuffix + 1 : 1L;

            // ma_phieu_chi có UNIQUE KEY: nếu mã vừa sinh trùng (2 máy cùng mở màn hình)
            // thì tăng tiếp cho tới khi ra mã còn trống.
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
            // Không chặn người dùng vì lỗi mạng/database.
            return prefix + "0001";
        }
    }

    /// <summary>Trạng thái chứng từ đã hủy: không được tính vào tổng thu/tổng chi của quỹ.</summary>
    public const string TrangThaiPhieuHuy = "HUY";

    /// <summary>
    /// Trạng thái chứng tư đã hoàn tất: duy nhất được tính vào quỹ.
    /// Xem <see cref="LuuPhieuQuyAsync"/> đang ghi trạng thái này.
    /// </summary>
    public const string TrangThaiPhieuHoanThanh = "HOAN_THANH";

    /// <summary>
    /// FROM + JOIN dùng chung cho mọi truy vấn sổ quỹ.
    /// Phiếu thu và phiếu chi cùng nằm trong bảng <c>phieu_chi</c>, loại phiếu
    /// được phân biệt bằng tiền tố mã (PTxxxx / PCxxxx).
    /// </summary>
    private const string SoQuyFrom = """
    FROM phieu_chi pc
    LEFT JOIN nguoi_dung nd ON nd.id = pc.nguoi_thuc_hien_id
    LEFT JOIN nhom_chi_phi ncp ON ncp.id = pc.nhom_chi_phi_id
    """;

    /// <summary>
    /// Danh sách cột lấy từ <see cref="SoQuyFrom"/>.
    /// Bản đầy đủ dùng khi database đã có <c>nguoi_nop_nhan</c> và <c>ghi_chu</c>;
    /// bản rút gọn dùng khi chưa thêm được (xem
    /// <c>EnsurePhieuQuyExtraColumnsAsync</c>) để ứng dụng vẫn chạy với dữ liệu cũ.
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
    /// Dựng điều kiện WHERE cho sổ quỹ. Cùng một hàm được dùng cho cả
    /// COUNT (phân trang), SELECT (danh sách) và SUM (thống kê) nên
    /// danh sách với thống kê không thể lệch nhau.
    /// <para>
    /// Mọi điều kiện đều gắn tham số nên không có chuyện dán chuỗi từ người dùng
    /// vào SQL. Khoảng thời gian và khoảng tiền có thể tắt riêng vì quỹ đầu kỳ
    /// cần tới tận các phiếu trước mốc thời gian nhưng không được lọc theo số tiền.
    /// </para>
    /// <para>
    /// <paramref name="apDungBoLocHienThi"/> = false để bỏ toàn bộ các bộ lọc chỉ
    /// dùng cho việc xem danh sách (tìm kiếm, loại chứng từ, loại thu/chi, người
    /// tạo, trạng thái, khoảng tiền). Thẻ tổng quan của sổ quỹ dùng chế độ này để
    /// tồn quỹ luôn đúng số dư thực tế, không bị bóp méo khi người dùng lọc bảng.
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
            // Ô tìm kiếm: mã phiếu, nội dung, ghi chú, người nộp/nhận, người tạo, tên loại thu/chi.
            var timKiem = new StringBuilder();
            timKiem.Append(
                "(@TimKiem = ''"
                + " OR pc.ma_phieu_chi LIKE @TimKiemLike"
                + " OR COALESCE(pc.noi_dung, '') LIKE @TimKiemLike"
                + " OR COALESCE(nd.ho_ten, '') LIKE @TimKiemLike"
                + " OR COALESCE(ncp.ten_nhom, '') LIKE @TimKiemLike");

            // Hai cột này chỉ tồn tại khi migration đã chạy thành công.
            if (coCotNguoiNopNhanGhiChu)
            {
                timKiem.Append(" OR COALESCE(pc.nguoi_nop_nhan, '') LIKE @TimKiemLike");
                timKiem.Append(" OR COALESCE(pc.ghi_chu, '') LIKE @TimKiemLike");
            }

            timKiem.Append(")");
            dieuKien.Add(timKiem.ToString());

            // Loại chứng từ: 0 = cả hai, 1 = chỉ phiếu thu, 2 = chỉ phiếu chi.
            dieuKien.Add(
                "(@LoaiChungTu = 0"
                + " OR (@LoaiChungTu = 1 AND pc.ma_phieu_chi LIKE 'PT%')"
                + " OR (@LoaiChungTu = 2 AND pc.ma_phieu_chi LIKE 'PC%'))");
        }

        // Quỹ: so khớp đúng mã phương thức đang có trong bảng phieu_chi.
        dieuKien.Add("(@PhuongThuc = '' OR pc.phuong_thuc = @PhuongThuc)");

        if (apDungKhoangThoiGian)
        {
            dieuKien.Add("(@TuNgay IS NULL OR pc.thoi_gian_chi >= @TuNgay)");
            dieuKien.Add("(@DenNgay IS NULL OR pc.thoi_gian_chi < @DenNgay)");
        }

        if (apDungBoLocHienThi)
        {
            // Các nhóm lựa chọn nhiều mục gửi lên dạng danh sách id cách nhau bằng dấu phẩy.
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

    /// <summary>Gom bộ lọc thành đúng bộ tham số mà các câu SQL kia đang mong đợi.</summary>
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
    /// Lấy một trang phiếu của sổ quỹ theo bộ lọc, kèm tổng số phiếu khớp bộ lọc
    /// để phân trng. Chỉ đọc đúng <c>pageSize</c> dòng, không kéo toàn bộ bảng về.
    /// </summary>
    public async Task<(IReadOnlyList<SoQuyGiaoDich> Items, int TongSoBanGhi)> GetSoQuyPhieuPagedAsync(
        SoQuyBoLoc loc,
        int page,
        int pageSize)
    {
        loc ??= new SoQuyBoLoc();

        // Gắn loại cho các phiếu tự sinh còn NULL trước khi lọc, để bộ lọc
        // "Thu khách hàng" / "Chi nhà cung cấp" áp đúng cả dữ liệu cũ.
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
    /// Chuyển dòng kết quả SQL thành đối tượng hiển thị.
    /// Dùng chung cho danh sách phân trang và xuất Excel nên hai nơi không thể lệch nhau.
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
                // Phiếu thu và phiếu chi cùng nằm trong bảng phieu_chi nên loại
                // phiếu được suy ra từ tiền tố mã (PTxxxx / PCxxxx).
                LaThu = maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase),
                // Người nộp/nhận là thông tin riêng của phiếu (cột nguoi_nop_nhan),
                // còn người tạo là nhân viên thao tác (FK nguoi_thuc_hien_id).
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
    /// Lấy chi tiết một phiếu thu/chi để mở màn hình chi tiết.
    /// Chỉ đọc đúng một dòng theo mã phiếu nên không ảnh hưởng hiệu năng.
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
    /// Hủy phiếu: chỉ chuyển sang trạng thái HUY khi phiếu đang HOAN_THANH.
    /// Trả về số dòng được cập nhật (0 = phiếu không tồn tại hoặc đã hủy).
    /// </summary>
    public async Task<int> HuyPhieuQuyAsync(string maPhieu)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookCancel, "Bạn không có quyền hủy phiếu thu/chi.");
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
            _ = LogSubAccountActivityAsync("Hủy sổ quỹ", "SO_QUY", null, maPhieu.Trim(), $"Hủy phiếu sổ quỹ {maPhieu.Trim()}");
        }

        return affected;
    }

    /// <summary>
    /// Hủy nhiều phiếu cùng lúc trong một transaction: hoặc cả nhóm thành công,
    /// hoặc không phiếu nào bị đổi. Dùng cho thao tác chọn nhiều trên danh sách.
    /// Trả về số phiếu thực sự chuyển sang trạng thái hủy (những phiếu đã hủy trước đó không tính).
    /// </summary>
    public async Task<int> HuyNhieuPhieuQuyAsync(IReadOnlyCollection<string> maPhieus)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookCancel, "Bạn không có quyền hủy phiếu thu/chi.");
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
    /// Lấy toàn bộ phiếu khớp bộ lọc để xuất Excel (không phân trang).
    /// Dùng đúng điều kiện WHERE với danh sách nên file xuất ra luôn khớp
    /// với những gì người dùng đang nhìn thấy trên màn hình.
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

    /// <summary>Trộn bộ tham số lọc với LIMIT/OFFSET thành một đối tượng duy nhất.</summary>
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
    /// Thống kê sổ quỹ: quỹ đầu kỳ, tổng thu, tổng chi, tồn quỹ, tổng số phiếu.
    /// Bốn thẻ chỉ số luôn tính trên CẢ phiếu thu lẫn phiếu chi, chỉ theo kỳ thời
    /// gian và quỹ đang chọn, nên tồn quỹ luôn là số dư thực tế chứ không bị
    /// bóp méo khi người dùng lọc loại chứng từ / tìm kiếm / loại thu chi / người
    /// tạo / khoảng tiền (các bộ lọc đó chỉ ảnh hưởng danh sách bên dưới).
    /// Tổng thu/tổng chi chỉ cộng phiếu <see cref="TrangThaiPhieuHoanThanh"/> nên
    /// phiếu đã hủy không làm thay đổi quỹ.
    /// </summary>
    public async Task<SoQuyTongHop> GetSoQuyTongHopTheoLocAsync(SoQuyBoLoc loc)
    {
        loc ??= new SoQuyBoLoc();

        try
        {
            var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

            await using var connection = await OpenAsync();

            // Thẻ tổng quan bỏ qua các bộ lọc chỉ dùng để xem danh sách
            // (loại chứng từ, tìm kiếm, loại thu/chi, người tạo, trạng thái,
            // khoảng tiền) để quỹ đầu kỳ và tồn quỹ luôn là số dư thực tế.
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

            // Quỹ đầu kỳ chỉ có nghĩa khi đang giới hạn bởi mốc bắt đầu: gom tất cả
            // phiếu trước mốc đó, thu trừ chi.
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

            // Tổng số phiếu phải khớp danh sách đang hiển thị nên đếm theo đúng
            // bộ lọc hiển thị (gồm cả phiếu đã hủy).
            var dieuKienDem = TaoDieuKienSoQuy(apDungKhoangThoiGian: true, apDungKhoangTien: true, coCotMoi);
            var thamSoDem = TaoThamSoSoQuy(loc, apDungKhoangTien: true);

            var quyDauKy = row?.QuyDauKy ?? 0m;
            var tongThu = row?.TongThu ?? 0m;
            var tongChi = row?.TongChi ?? 0m;
            var tonQuy = quyDauKy + tongThu - tongChi;

            // Nghiệp vụ: khi lọc phiếu thu thì tổng chi = 0, tồn quỹ = 0.
            // Khi lọc phiếu chi thì tổng thu = 0, tồn quỹ = 0.
            // Khi lọc tất cả thì hiển thị đầy đủ tổng thu, tổng chi và tồn quỹ thực tế.
            if (loc.LoaiChungTu == SoQuyBoLoc.ChiPhieuThu)
            {
                tongChi = 0m;
                tonQuy = 0m;
                quyDauKy = 0m;
            }
            else if (loc.LoaiChungTu == SoQuyBoLoc.ChiPhieuChi)
            {
                tongThu = 0m;
                tonQuy = 0m;
                quyDauKy = 0m;
            }

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
            // Tổng quỹ chỉ để hiển thị: lỗi truy vấn không được chặn màn hình.
            return new SoQuyTongHop();
        }
    }

    /// <summary>
    /// Các quỹ thực sự đang có dữ liệu trong bảng phieu_chi.
    /// Không tự thêm lựa chọn "ngân hàng"/"ví điện tử" khi database chưa có.
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
    /// Bảo đảm hai danh mục "Thu khách hàng" và "Chi nhà cung cấp" luôn có trong
    /// bảng <c>nhom_chi_phi</c> để người dùng chọn khi tạo phiếu thu/chi thủ công
    /// và dùng làm tiêu chí lọc ở sổ quỹ. Mục nào đã tồn tại thì bỏ qua.
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
                new { MaNhom = "THU_KHACH_HANG", TenNhom = "Thu khách hàng" },
                new { MaNhom = "CHI_NHA_CUNG_CAP", TenNhom = "Chi nhà cung cấp" }
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
    /// Các loại thu/chi thực tế đang có trong bảng nhom_chi_phi.
    /// Bảng này rỗng thì bộ lọc hiển thị rỗng, không bịa thêm loại giả.
    /// </summary>
    public async Task<IReadOnlyList<SoQuyLuaChonItem>> GetSoQuyLoaiThuChiOptionsAsync()
    {
        try
        {
            await EnsureNhomChiPhiMacDinhAsync();

            await using var connection = await OpenAsync();

            // Dapper khớp tên cột theo đúng tên thuộc tính (bỏ qua dấu gạch dưới),
            // nên phải đặt alias là TenNhom chứ không để tên cột là ten_nhom.
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
    /// Tên hiển thị của một mã loại thu/chi trong bảng nhom_chi_phi.
    /// Trả về r��ng nếu id không tồn tại (phiếu cũ chưa gán nhóm).
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

    /// <summary>Người dùng thực tế trong bảng nguoi_dung, dùng cho bộ lọc "Người tạo".</summary>
    public async Task<IReadOnlyList<SoQuyLuaChonItem>> GetSoQuyNguoiTaoOptionsAsync()
    {
        try
        {
            await using var connection = await OpenAsync();

            // Dùng chung kiểu dòng với nhom_chi_phi, xem chú thích alias bên trên.
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
    /// Đổi mã phương thức trong database sang nhãn tiếng Việt.
    /// Mã lạ thì hiện nguyên văn, không bịa nghĩa.
    /// </summary>
    private static string TenHienThiPhuongThuc(string ma) => ma.Trim().ToUpperInvariant() switch
    {
        "TIEN_MAT" => "Tiền mặt",
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Ngân hàng",
        "THE" or "THE_TIN_DUNG" => "Thẻ",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => ma
    };

    /// <summary>Dòng kết quả truy vấn sổ quỹ (chỉ dùng nội bộ DatabaseService).</summary>
    private sealed class SoQuyPhieuRow
    {
        public string MaPhieu { get; set; } = "";
        public decimal SoTien { get; set; }
        public string NoiDung { get; set; } = "";
        public string NguoiNopNhan { get; set; } = "";
        public string GhiChu { get; set; } = "";
        public DateTime ThoiGian { get; set; }

        /// <summary>
        /// Nhân viên thao tác (FK nguoi_thuc_hien_id), tức người tạo chứng từ.
        /// Không phải người nộp/nhận — thông tin đó nằm ở cột riêng.
        /// </summary>
        public string NguoiTao { get; set; } = "";

        public string TrangThai { get; set; } = "";
        public string TenLoai { get; set; } = "";

        /// <summary>Khóa ngoại tới nhom_chi_phi, cần cho form sửa để chọn lại loại.</summary>
        public uint? NhomChiPhiId { get; set; }
        public string PhuongThuc { get; set; } = "";
    }

    /// <summary>Danh mục chuẩn hóa dùng để tra cứu khi validate file nhập.</summary>
    private sealed class SoQuyChuanHoaRow
    {
        public uint Id { get; set; }
        public string Ten { get; set; } = "";
    }

    /// <summary>Dòng kết quả tổng hợp sổ quỹ (chỉ dùng nội bộ DatabaseService).</summary>
    private sealed class SoQuyTongHopRow
    {
        public decimal QuyDauKy { get; set; }
        public decimal TongThu { get; set; }
        public decimal TongChi { get; set; }
        public int TongSoPhieu { get; set; }
    }

    /// <summary>Cặp id + tên dùng chung cho nhom_chi_phi và nguoi_dung.</summary>
    private sealed class SoQuyTenNhomRow
    {
        public uint Id { get; set; }
        public string TenNhom { get; set; } = "";
    }

    /// <summary>
    /// Tra cứu một lần danh mục cần cho bước validate file nhập.
    /// Trả về cả chiều id -&gt; tên và tên đã bỏ dấu -&gt; id để dòng trong file chỉ cần
    /// ghi tên loại / tên người tạo cũng tra khớp được, không query từng dòng.
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

            // Quỹ lấy từ chính dữ liệu đang có, không tự thêm mã mới.
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

            // Nếu database còn chưa có phiếu nào thì vẫn cho phép dùng giá trị mặc định
            // của cột phuong_thuc, vẫn là dữ liệu thật của database chứ không phải bịa thêm.
            if (danhMuc.QuyTheoMa.Count == 0)
            {
                danhMuc.QuyTheoMa[danhMuc.PhuongThucMacDinh] = TenHienThiPhuongThuc(danhMuc.PhuongThucMacDinh);
                danhMuc.QuyTheoTen[ChuanHoaChuoi(danhMuc.PhuongThucMacDinh)] = danhMuc.PhuongThucMacDinh;
            }
        }
        catch
        {
            // Để danh mục rỗng: mọi dòng cần tra cứu sẽ báo lỗi ở bước validate,
            // người dùng thấy ngay trong bước xem trước thay vì ghi sai dữ liệu.
        }

        return danhMuc;
    }

    /// <summary>
    /// Lấy tập mã phiếu đã tồn tại trong database. Dùng cho bước xem trước để biết
    /// dòng nào trùng mã, tránh gọi vòng lặp "SELECT từng dòng".
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

        // Chia nhỏ để câu IN không vượt quá giới hạn tham số của MySQL.
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
    /// Ghi hàng loạt phiếu sổ quỹ trong một transaction.
    /// Một dòng lỗi khiến toàn bộ lượt ghi được rollback để không để lại dữ liệu nửa vời.
    /// Tiến độ được báo sau mỗi lô để thanh tiến trên UI không bị đứng.
    /// </summary>
    public async Task<SoQuyImportResult> NhapPhieuQuyAsync(
        IReadOnlyList<SoQuyImportRow> dongGhi,
        XuLyPhieuTrung xuLyTrung,
        Func<SoQuyImportProgress, Task>? baoTienDo = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookImport, "Bạn không có quyền import sổ quỹ.");
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

            // Số thứ tự mã tự sinh: đọc MAX một lần rồi cấp phát liên tục trong bộ nhớ.
            var soThuTuThu = await DocSoThuTuMaPhieuQuyAsync(connection, transaction, "PT");
            var soThuTuChi = await DocSoThuTuMaPhieuQuyAsync(connection, transaction, "PC");

            var dongDaXuLy = 0;

            foreach (var dong in dongGhi)
            {
                var maPhieu = dong.MaPhieu.Trim();

                if (maPhieu.Length == 0)
                {
                    // File không ghi mã phiếu thì hệ thống tự cấp.
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

                    // UPDATE không sinh mã mới nên vẫn cần nới số thứ tự về sau mã vừa dùng.
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
                        ketQua.DanhSachLoi.Add(new SoQuyImportError(dong.SoDongExcel, dong.MaPhieu, "Không tìm thấy phiếu để cập nhật."));
                    }
                }
                else if (dong.MaPhieuDaTonTai)
                {
                    // Giữ dữ liệu cũ hoặc bỏ qua dòng trùng: hai cách này đều không ghi.
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
                        GiaiDoan = "Đang ghi phiếu",
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

            // Lỗi này chặn cả lượt ghi vì có thể là hết quyền, mất kết nối, trùng mã...
            // Rollback sạch thì báo người dùng chứ không ghi tiếp.
            ketQua.DaRollback = true;
            ketQua.DaHuy = true;
            ketQua.CanhBao.Add($"Đã rollback toàn bộ lượt nhập: {ex.Message}");
            ketQua.SoThemMoi = 0;
            ketQua.SoCapNhat = 0;
            ketQua.SoLoi++;
            ketQua.DanhSachLoi.Add(new SoQuyImportError(0, null, ex.Message));

            return ketQua;
        }
    }

    /// <summary>Gom tham số chung cho INSERT và UPDATE của một dòng phiếu nhập từ file.</summary>
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
    /// Đọc số thứ tự lớn nhất của một tiền tố mã phiếu, dùng để cấp mã tự sinh khi
    /// nhập hàng loạt (tránh gọi SELECT cho từng dòng).
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

    /// <summary>Lấy phần số của mã phiếu, trả về 0 nếu mã không đúng định dạng.</summary>
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
    /// Bỏ dấu và viết thường để so khớp tên loại thu/chi, tên người và tên quỹ
    /// giữa file nhập và database, không phụ thuộc khác biệt hoa/thường hay dấu tiếng Việt.
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

    /// <summary>Tổng hợp số tiền trên các phiếu nhập hàng (chỉ dùng nội bộ DatabaseService).</summary>
    private sealed class NhaCungCapPhieuTongHopRow
    {
        public int SoPhieuNhap { get; set; }
        public decimal TongTienNhap { get; set; }
        public decimal TongDaThanhToan { get; set; }
        public decimal TongConNoPhieuNhap { get; set; }
    }

    /// <summary>Tổng hợp sổ cái công nợ (chỉ dùng nội bộ DatabaseService).</summary>
    private sealed class NhaCungCapCongNoTongHopRow
    {
        public int SoLanGhiCongNo { get; set; }
        public decimal TongCongNoLichSu { get; set; }
        public DateTime? LanGhiCongNoGanNhat { get; set; }
    }

    /// <summary>
    /// Lưu 1 phiếu thu (PTxxxx) hoặc phiếu chi (PCxxxx) của sổ quỹ.
    /// Cột số <c>so_tien</c> luôn nhận giá trị decimal thô, không nhận chuỗi đã định dạng.
    /// Không nuốt exception: lỗi UNIQUE/connection được ném ra để ViewModel hiển thị cho người dùng.
    /// <para>
    /// Ánh xạ cột: <c>chi_nhanh_id</c> lấy chi nhánh của người đang đăng nhập,
    /// <c>nhom_chi_phi_id</c> là loại thu/chi, <c>phuong_thuc</c> là mã quỹ,
    /// <c>nguoi_thuc_hien_id</c> là người tạo, còn <c>nguoi_nop_nhan</c> và
    /// <c>ghi_chu</c> là hai thông tin riêng của từng phiếu.
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
            throw new InvalidOperationException("Thiếu mã phiếu khi lưu sổ quỹ.");

        var laPhieuThu = maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase);
        Quyen.RequirePermission(
            Session.CurrentUser,
            laPhieuThu ? PermissionKeys.CashBookCreateReceipt : PermissionKeys.CashBookCreatePayment,
            laPhieuThu ? "Bạn không có quyền tạo phiếu thu." : "Bạn không có quyền tạo phiếu chi.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền phải lớn hơn 0.");

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        // Cột tùy chọn được ghép vào danh sách hàng/cột; mọi giá trị vẫn đi qua tham số
        // nên không có chuyện dán chuỗi của người dùng vào SQL.
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
            // Rỗng thì để MySQL lấy giá trị mặc định của cột (TIEN_MAT).
            PhuongThuc = string.IsNullOrWhiteSpace(phuongThuc) ? null : phuongThuc.Trim().ToUpperInvariant(),
            NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim(),
            NguoiNopNhan = Truncate150(nguoiNopNhan),
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim(),
            ThoiGian = thoiGian,
            NguoiTaoId = nguoiTaoId,
            TrangThai = TrangThaiPhieuHoanThanh
        });

        _ = LogSubAccountActivityAsync(
            laPhieuThu ? "Thu tiền" : "Chi tiền",
            "SO_QUY",
            null,
            maPhieu.Trim(),
            noiDung ?? (laPhieuThu ? $"Lập phiếu thu {maPhieu.Trim()}" : $"Lập phiếu chi {maPhieu.Trim()}"),
            laPhieuThu ? soTien : -soTien);
    }

    /// <summary>Cắt chuỗi cho vừa độ dài của cột varchar(150) của nguoi_nop_nhan.</summary>
    private static string? Truncate150(string? giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return null;

        var chuoi = giaTri.Trim();

        return chuoi.Length <= 150 ? chuoi : chuoi[..150];
    }

    /// <summary>
    /// Sửa một phiếu đang hiệu lực. Cho sửa được cả loại thu/chi, quỹ, người nộp/nhận,
    /// nội dung và ghi chú vì đều là thông tin của phiếu; không cho sửa mã phiếu,
    /// người tạo và trạng thái vì đó là dữ liệu sinh ra lúc tạo chứng từ.
    /// Trả về số dòng được cập nhật (0 = phiếu không tồn tại hoặc đã hủy).
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.CashBookEdit, "Bạn không có quyền sửa phiếu thu/chi.");
        if (string.IsNullOrWhiteSpace(maPhieu))
            throw new InvalidOperationException("Thiếu mã phiếu khi sửa sổ quỹ.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền phải lớn hơn 0.");

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        // Hai cột mới chỉ xuất hiện trong câu UPDATE khi migration đã chạy, tránh lỗi
        // "Unknown column" với database cũ. Giá trị vẫn đi qua tham số.
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
                "Sửa sổ quỹ",
                "SO_QUY",
                null,
                maPhieu.Trim(),
                $"Cập nhật phiếu {maPhieu.Trim()}: {noiDung}",
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

    public async Task<UserWithRoles?> GetUserWithRolesAsync(uint userId)
    {
        await using var connection = await OpenAsync();
        var sqlUser = """
        SELECT nd.id AS Id, nd.ma_nguoi_dung AS MaNguoiDung, nd.ten_dang_nhap AS TenDangNhap, nd.ho_ten AS HoTen, nd.trang_thai AS TrangThai,
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
    /// Tài khoản này có quyền được gán riêng hay không.
    /// Nếu cột phan_quyen_rieng đã tồn tại thì dùng cột đó (xử lý được cả trường hợp
    /// quản trị bỏ chọn tất cả). Nếu chưa chạy migration thì lùi về cách đếm dòng
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

            // Đã có quyền gán riêng cho tài khoản => dùng đúng danh sách này,
            // không cộng thêm quyền kế thừa từ vai trò.
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
    /// Bảo đảm bảng <c>quyen</c> có đủ mọi mã quyền trong <see cref="PermissionCatalog"/>.
    /// Nhờ vậy cây phân quyền luôn có Id &gt; 0 và không bị mất quyền khi lưu.
    /// Chạy idempotent, an toàn khi gọi lại nhiều lần.
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

            // Bảo đảm các bảng phân quyền tồn tại (database cũ chưa chạy migration).
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

            // Bảo đảm cột phan_quyen_rieng tồn tại trong bảng nguoi_dung
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

            // Dọn dẹp tài khoản kiểm thử nếu có
            try
            {
                await connection.ExecuteAsync("DELETE ndvt, ndq, nd FROM nguoi_dung nd LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id LEFT JOIN nguoi_dung_quyen ndq ON ndq.nguoi_dung_id = nd.id WHERE nd.ten_dang_nhap = 'repro_test_x'");
            }
            catch { }

            // Đồng bộ quyền mặc định theo vai trò vào vai_tro_quyen cho tất cả vai trò
            foreach (var kvp in PermissionCatalog.DefaultRolePermissions)
            {
                var roleId = await connection.ExecuteScalarAsync<uint?>(
                    "SELECT id FROM vai_tro WHERE ma_vai_tro = @Ma",
                    new { Ma = kvp.Key });

                if (roleId.HasValue)
                {
                    if (string.Equals(kvp.Key, Quyen.QuanTri, StringComparison.OrdinalIgnoreCase))
                    {
                        // QUAN_TRI luôn có tất cả quyền
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
            // Không chặn đăng nhập nếu danh mục quyền chưa đồng bộ được.
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

    /// <summary>Tài khoản đang có ít nhất một quyền được gán riêng (không kế thừa vai trò).</summary>
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission, "Bạn không có quyền phân quyền tài khoản.");
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission, "Bạn không có quyền phân quyền vai trò.");
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersCreate, "Bạn không có quyền tạo tài khoản.");
        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var maNguoiDung = await GenerateMaNguoiDungAsync(connection, tx);
            var hashed = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);
            var chiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1u;
            var sql = """
            INSERT INTO nguoi_dung (ma_nguoi_dung, ten_dang_nhap, mat_khau, ho_ten, chi_nhanh_id, trang_thai, ngay_tao, ngay_cap_nhat)
            VALUES (@MaNguoiDung, @TenDangNhap, @MatKhau, @HoTen, @ChiNhanhId, @TrangThai, NOW(), NOW())
            """;
            await connection.ExecuteAsync(sql, new
            {
                MaNguoiDung = maNguoiDung,
                TenDangNhap = dto.TenDangNhap,
                MatKhau = hashed,
                HoTen = dto.HoTen,
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
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersEdit, "Bạn không có quyền sửa tài khoản.");

        // Không cho bỏ vai trò QUAN_TRI của chính mình hoặc của tài khoản Admin khác.
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
                throw new InvalidOperationException("Bạn không thể tự bỏ vai trò QUAN_TRI của chính mình.");
            }
        }

        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.MatKhau))
            {
                var hashed = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);
                await connection.ExecuteAsync("UPDATE nguoi_dung SET ho_ten = @HoTen, mat_khau = @MatKhau, trang_thai = @TrangThai, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = dto.Id, HoTen = dto.HoTen, MatKhau = hashed, TrangThai = dto.TrangThai ? 1 : 0 }, tx);
            }
            else
            {
                await connection.ExecuteAsync("UPDATE nguoi_dung SET ho_ten = @HoTen, trang_thai = @TrangThai, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = dto.Id, HoTen = dto.HoTen, TrangThai = dto.TrangThai ? 1 : 0 }, tx);
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

    /// <summary>Tài khoản có vai trò QUAN_TRI hay không.</summary>
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
            throw new InvalidOperationException($"Không thể {hanhDong} tài khoản có vai trò QUAN_TRI.");
        }
    }

    public async Task LockUserAsync(uint userId, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersLock, "Bạn không có quyền khóa tài khoản.");
        await BaoVeQuanTriAsync(userId, "khóa");
        await using var connection = await OpenAsync();
        await connection.ExecuteAsync("UPDATE nguoi_dung SET trang_thai = 0, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = userId });
    }

    public async Task UnlockUserAsync(uint userId, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersUnlock, "Bạn không có quyền mở khóa tài khoản.");
        await using var connection = await OpenAsync();
        await connection.ExecuteAsync("UPDATE nguoi_dung SET trang_thai = 1, ngay_cap_nhat = NOW() WHERE id = @Id", new { Id = userId });
    }

    public async Task DeleteUserAsync(uint userId, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersDelete, "Bạn không có quyền xóa tài khoản.");
        await using var connection = await OpenAsync();
        // Không xóa admin (id=1 thường) - phòng ngừa
        if (userId == 1) throw new InvalidOperationException("Không thể xóa tài khoản Admin mặc định.");
        await BaoVeQuanTriAsync(userId, "xóa");
        await connection.ExecuteAsync("DELETE FROM nguoi_dung_vai_tro WHERE nguoi_dung_id = @UserId", new { UserId = userId });
        await connection.ExecuteAsync("DELETE FROM nguoi_dung_quyen WHERE nguoi_dung_id = @UserId", new { UserId = userId });
        await connection.ExecuteAsync("DELETE FROM nguoi_dung WHERE id = @Id", new { Id = userId });
    }

    public async Task ResetPasswordAsync(uint userId, string newPassword, uint? actorId = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.UsersResetPassword, "Bạn không có quyền đặt lại mật khẩu.");
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ArgumentException("Mật khẩu không được để trống");
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

}

