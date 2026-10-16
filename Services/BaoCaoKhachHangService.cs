using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ màn "Báo cáo khách hàng": tổng hợp số đơn, tổng tiền hàng, giảm giá,
/// giá trị mua, đã thanh toán và công nợ theo từng khách hàng trong khoảng
/// thời gian đang chọn.
/// <para>
/// Mọi con số tính trong SQL (GROUP BY / SUM / ORDER BY / LIMIT) theo đúng
/// pattern của <see cref="BaoCaoHangHoaService"/> và
/// <see cref="BaoCaoBanHangService"/>: bộ lọc thời gian dùng
/// <c>thoi_gian_ban &gt;= Tu AND &lt; Den+1</c> với hóa đơn
/// <c>trang_thai_hoa_don = 'HOAN_THANH'</c>. Bảng danh sách phân trang bằng
/// <c>LIMIT/OFFSET</c> nên không bao giờ kéo toàn bộ khách hàng về UI.
/// </para>
/// </summary>
public sealed class BaoCaoKhachHangService
{
    /// <summary>Khoá sắp xếp hợp lệ → biểu thức ORDER BY (chỉ nhận khoá do chương trình định nghĩa).</summary>
    private static readonly IReadOnlyDictionary<string, string> CotSapXep =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaKhachHang"] = "kh.ma_khach_hang",
            ["TenKhachHang"] = "kh.ten_khach_hang",
            ["SoDienThoai"] = "kh.so_dien_thoai",
            ["SoDonHang"] = "SoDonHang",
            ["TongTienHang"] = "TongTienHang",
            ["GiamGia"] = "GiamGia",
            ["GiaTriMuaHang"] = "GiaTriMuaHang",
            ["DaThanhToan"] = "DaThanhToan",
            ["CongNo"] = "CongNo"
        };

    /// <summary>Khoá mặc định khi truyền vào khoá lạ (không bao giờ nối khoá người dùng vào SQL).</summary>
    public const string KhoaMacDinh = "GiaTriMuaHang";

    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoKhachHangService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var den = denNgay.Date;
        return (tu, den.AddDays(1));
    }

    /// <summary>
    /// Tải một lần gồm: thẻ thống kê (tổng khách, doanh số, số đơn, công nợ),
    /// top khách hàng cho biểu đồ và các dòng của trang đang xem.
    /// </summary>
    /// <param name="sapXepKho">Khoá sắp xếp của bảng (một trong <see cref="CotSapXep"/>).</param>
    /// <param name="metricKho">Khoá của "Mối quan tâm" — quyết định giá trị của biểu đồ.</param>
    public async Task<BaoCaoKhachHangDuLieu> TaiDuLieuAsync(
        DateTime tuNgay,
        DateTime denNgay,
        string? timKiem,
        string sapXepKho,
        bool sapXepTang,
        string metricKho,
        int trang,
        int soDongTrang,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);

        // _taoKetNoi() (DatabaseService.TaoKetNoiAsync) đã mở connection sẵn,
        // KHÔNG được gọi OpenAsync lần nữa (MySqlConnector ném
        // "Cannot Open when State is Open"). Dùng connection đã mở trực tiếp.
        await using var conn = await _taoKetNoi();

        var dk = new DieuKien
        {
            TuNgay = tu,
            DenKeTiep = denKeTiep,
            TimKiem = string.IsNullOrWhiteSpace(timKiem) ? null : "%" + timKiem.Trim() + "%"
        };

        var tongHop = await TaiTongHopAsync(conn, dk, token);
        tongHop.TuNgay = tu;
        tongHop.DenNgay = denKeTiep.AddDays(-1);

        int soDong = soDongTrang < 1 ? 15 : soDongTrang;
        int trangHieu = trang < 1 ? 1 : trang;
        int offset = (trangHieu - 1) * soDong;

        var dongTrang = await TaiTrangAsync(conn, dk, sapXepKho, sapXepTang, soDong, offset, token);
        for (int i = 0; i < dongTrang.Count; i++)
            dongTrang[i].Stt = offset + i + 1;

        var top = await TaiTopAsync(conn, dk, metricKho, token);
        for (int i = 0; i < top.Count; i++)
            top[i].Stt = i + 1;
        TinhThanhBar(top, metricKho);

        return new BaoCaoKhachHangDuLieu
        {
            TongHop = tongHop,
            Top = top,
            DongTrang = dongTrang
        };
    }

    // ============================================================
    // TRUY VẤN CON
    // ============================================================

    private sealed class DieuKien
    {
        public DateTime TuNgay { get; init; }
        public DateTime DenKeTiep { get; init; }
        public string? TimKiem { get; init; }

        /// <summary>WHERE dùng chung cho mọi truy vấn (tham số hoá, không nối tay người dùng vào SQL).</summary>
        public string Where =>
            @"hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep"
            + (TimKiem is null
                ? ""
                : @"
  AND (kh.ma_khach_hang LIKE @TimKiem OR kh.ten_khach_hang LIKE @TimKiem OR kh.so_dien_thoai LIKE @TimKiem)");

        public object Params => new { TuNgay, DenKeTiep, TimKiem };

        /// <summary>
        /// INNER JOIN khach_hang nên khách lẻ (khach_hang_id NULL) không xuất hiện
        /// trong báo cáo khách hàng.
        /// </summary>
        public const string From = @"
FROM hoa_don_ban_hang hd
INNER JOIN khach_hang kh ON kh.id = hd.khach_hang_id";
    }

    /// <summary>Các cột tổng hợp dùng chung cho bảng chi tiết và top biểu đồ.</summary>
    private const string TongHopCot = @"
  COUNT(*) AS SoDonHang,
  COALESCE(SUM(hd.tong_tien_hang), 0) AS TongTienHang,
  COALESCE(SUM(hd.giam_gia), 0) AS GiamGia,
  COALESCE(SUM(hd.tong_tien_hang - hd.giam_gia), 0) AS GiaTriMuaHang,
  COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThanhToan,
  COALESCE(SUM(hd.con_no), 0) AS CongNo";

    private const string NhomKhach = @"kh.id, kh.ma_khach_hang, kh.ten_khach_hang, kh.so_dien_thoai";

    private async Task<BaoCaoKhachHangTongHopKy> TaiTongHopAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        // COUNT(DISTINCT kh.id) = số nhóm khách hàng = tổng dòng bảng (để phân trang).
        string sql = @"
SELECT
  COUNT(DISTINCT kh.id) AS TongKhachHang,
  COUNT(*) AS SoDonHang,
  COALESCE(SUM(hd.tong_tien_hang), 0) AS TongTienHang,
  COALESCE(SUM(hd.giam_gia), 0) AS GiamGia,
  COALESCE(SUM(hd.tong_tien_hang - hd.giam_gia), 0) AS GiaTriMuaHang,
  COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThanhToan,
  COALESCE(SUM(hd.con_no), 0) AS CongNo
" + DieuKien.From + @"
WHERE " + dk.Where + @";
";

        return await conn.QueryFirstOrDefaultAsync<BaoCaoKhachHangTongHopKy>(
                   new CommandDefinition(sql, dk.Params, cancellationToken: token))
               ?? new BaoCaoKhachHangTongHopKy();
    }

    private async Task<List<BaoCaoKhachHangDong>> TaiTrangAsync(
        MySqlConnection conn, DieuKien dk, string sapXepKho, bool sapXepTang,
        int soDong, int offset, CancellationToken token)
    {
        string orderBy = LayOrderBy(sapXepKho, sapXepTang);

        string sql = @"
SELECT
  kh.id AS Id,
  COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
  COALESCE(kh.ten_khach_hang, '') AS TenKhachHang,
  COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
  " + TongHopCot + DieuKien.From + @"
WHERE " + dk.Where + @"
GROUP BY " + NhomKhach + @"
ORDER BY " + orderBy + @", kh.id
LIMIT @SoDong OFFSET @Offset;
";

        var thamSo = new
        {
            TuNgay = dk.TuNgay,
            DenKeTiep = dk.DenKeTiep,
            TimKiem = dk.TimKiem,
            SoDong = soDong,
            Offset = offset
        };

        var rs = await conn.QueryAsync<BaoCaoKhachHangDong>(
            new CommandDefinition(sql, thamSo, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoKhachHangTop>> TaiTopAsync(
        MySqlConnection conn, DieuKien dk, string metricKho, CancellationToken token)
    {
        string orderBy = LayOrderBy(metricKho, sapXepTang: false);

        string sql = @"
SELECT
  kh.id AS Id,
  COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
  COALESCE(kh.ten_khach_hang, '') AS TenKhachHang,
  COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
  " + TongHopCot + DieuKien.From + @"
WHERE " + dk.Where + @"
GROUP BY " + NhomKhach + @"
ORDER BY " + orderBy + @", SoDonHang DESC, kh.id
LIMIT 10;
";

        var rs = await conn.QueryAsync<BaoCaoKhachHangTop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    /// <summary>Chuyển khoá sắp xếp của UI sang biểu thức ORDER BY (chỉ nhận khoá whitelist).</summary>
    private static string LayOrderBy(string? kho, bool sapXepTang)
    {
        var cot = kho is not null && CotSapXep.TryGetValue(kho, out var expr)
            ? expr
            : CotSapXep[KhoaMacDinh];

        return $"{cot} {(sapXepTang ? "ASC" : "DESC")}";
    }

    // ============================================================
    // TÍNH THANH BIỂU ĐỒ
    // ============================================================

    /// <summary>
    /// Gán độ lớn của thanh theo giá trị mối quan tâm đang chọn và cờ định dạng
    /// (tiền hay số đơn) để XAML hiển thị đúng, có tooltip.
    /// </summary>
    private static void TinhThanhBar(List<BaoCaoKhachHangTop> list, string metricKho)
    {
        string kho = (metricKho ?? KhoaMacDinh).Trim().ToUpperInvariant();
        bool laTien = kho != "SODONHANG";

        foreach (var x in list)
            x.LaTien = laTien;

        if (list.Count == 0)
            return;

        decimal max = list.Max(x => GiaTriCua(x, kho));
        foreach (var x in list)
        {
            x.GiaTri = GiaTriCua(x, kho);
            x.TyLeBar = max <= 0 ? 0 : (double)(x.GiaTri / max * 100m);
        }
    }

    /// <summary>Giá trị của mối quan tâm đang chọn trên một dòng top.</summary>
    private static decimal GiaTriCua(BaoCaoKhachHangTop x, string kho) => kho switch
    {
        "SODONHANG" => x.SoDonHang,
        "CONGNO" => x.CongNo,
        "TONGTIENHANG" => x.TongTienHang,
        _ => x.GiaTriMuaHang
    };
}
