using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ báo cáo hàng hóa: tổng hợp doanh thu / giá vốn / lợi nhuận,
/// top 10 sản phẩm, doanh thu theo nhóm hàng và bảng chi tiết theo mã hàng.
/// <para>
/// Mọi con số đều tính trong SQL (GROUP BY / ORDER BY / LIMIT) theo đúng
/// pattern của <see cref="BaoCaoBanHangService"/>; bộ lọc thời gian dùng
/// <c>thoi_gian_ban &gt;= Tu AND &lt; Den+1</c> với hóa đơn
/// <c>trang_thai_hoa_don = 'HOAN_THANH'</c>.
/// </para>
/// </summary>
public sealed class BaoCaoHangHoaService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoHangHoaService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var den = denNgay.Date;
        return (tu, den.AddDays(1));
    }

    public async Task<BaoCaoHangHoaDuLieu> TaiDuLieuAsync(
        DateTime tuNgay,
        DateTime denNgay,
        bool gopHangHoa,
        string? loaiHang,
        int nhomId,
        string? timKiem,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);

        // _taoKetNoi() (DatabaseService.TaoKetNoiAsync) đã mở connection sẵn,
        // KHÔNG được gọi OpenAsync lần nữa (MySqlConnector ném
        // "Cannot Open when State is Open"). Dùng connection đã mở trực tiếp.
        await using var conn = await _taoKetNoi();

        var dieuKien = new DieuKien
        {
            TuNgay = tu,
            DenKeTiep = denKeTiep,
            LoaiHang = string.IsNullOrWhiteSpace(loaiHang) ? null : loaiHang.Trim(),
            NhomId = nhomId,
            TimKiem = string.IsNullOrWhiteSpace(timKiem) ? null : "%" + timKiem.Trim() + "%"
        };

        // "Gộp hàng hóa cùng loại": gộp theo tên đã trim (không phân biệt hoa thường
        // theo collation của MySQL); không gộp thì giữ nguyên từng mã sản phẩm.
        string nhomGroup = gopHangHoa
            ? "TRIM(ct.ten_san_pham)"
            : "ct.san_pham_id, ct.ma_san_pham, ct.ten_san_pham";
        string chonTen = gopHangHoa ? "TRIM(ct.ten_san_pham) AS TenHang" : "ct.ten_san_pham AS TenHang";
        string chonMa = gopHangHoa ? "COALESCE(MIN(ct.ma_san_pham), '') AS MaHang" : "ct.ma_san_pham AS MaHang";
        string demMa = gopHangHoa
            ? "COUNT(DISTINCT TRIM(ct.ten_san_pham))"
            : "COUNT(DISTINCT ct.san_pham_id, ct.ma_san_pham)";

        var tongHop = await TaiTongHopAsync(conn, dieuKien, nhomGroup, token);
        var topDoanhThu = await TaiTopAsync(conn, dieuKien, nhomGroup, chonTen, chonMa,
            "DoanhThu DESC, SoLuongBan DESC", token);
        var topSoLuong = await TaiTopAsync(conn, dieuKien, nhomGroup, chonTen, chonMa,
            "SoLuongBan DESC, DoanhThu DESC", token);
        var nhomHang = await TaiNhomHangAsync(conn, dieuKien, demMa, token);
        var chiTiet = await TaiChiTietAsync(conn, dieuKien, nhomGroup, chonTen, chonMa, token);

        tongHop.TuNgay = tu;
        tongHop.DenNgay = denKeTiep.AddDays(-1);

        TinhThanhBar(topDoanhThu, x => x.DoanhThu);
        TinhThanhBar(topSoLuong, x => x.SoLuongBan);
        TinhThanhNhom(nhomHang);

        for (int i = 0; i < topDoanhThu.Count; i++) topDoanhThu[i].Stt = i + 1;
        for (int i = 0; i < topSoLuong.Count; i++) topSoLuong[i].Stt = i + 1;

        return new BaoCaoHangHoaDuLieu
        {
            TuNgay = tu,
            DenNgay = denKeTiep.AddDays(-1),
            TongHop = tongHop,
            TopDoanhThu = topDoanhThu,
            TopSoLuong = topSoLuong,
            NhomHang = nhomHang,
            ChiTiet = chiTiet
        };
    }

    /// <summary>Tất cả loại hàng (cột loai_hang_excel) để nạp ComboBox bộ lọc.</summary>
    public async Task<List<string>> TaiLoaiHangAsync(CancellationToken token = default)
    {
        await using var conn = await _taoKetNoi();
        var rs = await conn.QueryAsync<string>(new CommandDefinition(
            @"SELECT DISTINCT TRIM(loai_hang_excel) FROM san_pham
WHERE loai_hang_excel IS NOT NULL AND TRIM(loai_hang_excel) <> ''
ORDER BY 1;",
            cancellationToken: token));
        return rs.AsList();
    }

    /// <summary>Tất cả nhóm hàng (kèm mục "Tất cả" và "Chưa phân nhóm") cho bộ lọc.</summary>
    public async Task<List<BaoCaoHangHoaNhomOption>> TaiNhomHangAsync(CancellationToken token = default)
    {
        await using var conn = await _taoKetNoi();
        var rs = await conn.QueryAsync<(uint Id, string TenNhom)>(new CommandDefinition(
            @"SELECT id, ten_nhom FROM nhom_san_pham WHERE trang_thai = 1 ORDER BY id;",
            cancellationToken: token));

        var list = new List<BaoCaoHangHoaNhomOption>
        {
            new() { Id = -1, TenNhom = "", TenHienThi = "Tất cả nhóm hàng" }
        };
        foreach (var (id, ten) in rs)
            list.Add(new BaoCaoHangHoaNhomOption { Id = (int)id, TenNhom = ten, TenHienThi = ten });
        list.Add(new BaoCaoHangHoaNhomOption { Id = 0, TenNhom = "", TenHienThi = "Chưa phân nhóm" });
        return list;
    }

    // ============================================================
    // TRUY VẤN CON
    // ============================================================

    private sealed class DieuKien
    {
        public DateTime TuNgay { get; init; }
        public DateTime DenKeTiep { get; init; }
        public string? LoaiHang { get; init; }
        public int NhomId { get; init; }
        public string? TimKiem { get; init; }

        /// <summary>WHERE dùng chung cho mọi truy vấn (tham số hoá, không nối tay người dùng vào SQL).</summary>
        public string Where =>
            @"hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep"
            + (LoaiHang is null ? "" : "\n  AND TRIM(sp.loai_hang_excel) = @LoaiHang")
            + (NhomId > 0 ? "\n  AND sp.nhom_san_pham_id = @NhomId"
               : NhomId == 0 ? "\n  AND sp.nhom_san_pham_id IS NULL" : "")
            + (TimKiem is null ? "" : "\n  AND (ct.ten_san_pham LIKE @TimKiem OR ct.ma_san_pham LIKE @TimKiem)");

        public object Params => new
        {
            TuNgay,
            DenKeTiep,
            LoaiHang,
            NhomId,
            TimKiem
        };

        public const string From = @"
FROM chi_tiet_hoa_don_ban_hang ct
INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id";
    }

    private async Task<BaoCaoHangHoaTongHop> TaiTongHopAsync(
        MySqlConnection conn, DieuKien dk, string nhomGroup, CancellationToken token)
    {
        string sql = @"
SELECT
  COUNT(*) AS SoMaHang,
  COALESCE(SUM(q.SoLuongBan), 0) AS SoLuongBan,
  COALESCE(SUM(q.DoanhThu), 0) AS DoanhThu,
  COALESCE(SUM(q.GiaVon), 0) AS GiaVon
FROM (
  SELECT
    COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
    COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu,
    COALESCE(SUM(ct.gia_von * ct.so_luong), 0) AS GiaVon
  " + DieuKien.From + @"
  WHERE " + dk.Where + @"
  GROUP BY " + nhomGroup + @"
) q;
";
        var r = await conn.QueryFirstOrDefaultAsync<BaoCaoHangHoaTongHop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token)) ?? new BaoCaoHangHoaTongHop();
        return r;
    }

    private async Task<List<BaoCaoHangHoaTop>> TaiTopAsync(
        MySqlConnection conn, DieuKien dk, string nhomGroup, string chonTen, string chonMa,
        string orderBy, CancellationToken token)
    {
        string sql = @"
SELECT
  " + chonMa + @",
  " + chonTen + @",
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu
" + DieuKien.From + @"
WHERE " + dk.Where + @"
GROUP BY " + nhomGroup + @"
ORDER BY " + orderBy + @"
LIMIT 10;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaTop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoHangHoaNhom>> TaiNhomHangAsync(
        MySqlConnection conn, DieuKien dk, string demMa, CancellationToken token)
    {
        string sql = @"
SELECT
  COALESCE(NULLIF(TRIM(n.ten_nhom), ''), 'Chưa phân nhóm') AS TenNhom,
  " + demMa + @" AS SoMaHang,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu
" + DieuKien.From + @"
LEFT JOIN nhom_san_pham n ON n.id = sp.nhom_san_pham_id
WHERE " + dk.Where + @"
GROUP BY n.id, COALESCE(NULLIF(TRIM(n.ten_nhom), ''), 'Chưa phân nhóm')
ORDER BY DoanhThu DESC, SoLuongBan DESC;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaNhom>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoHangHoaChiTiet>> TaiChiTietAsync(
        MySqlConnection conn, DieuKien dk, string nhomGroup, string chonTen, string chonMa,
        CancellationToken token)
    {
        string sql = @"
SELECT
  " + chonMa + @",
  " + chonTen + @",
  COALESCE(NULLIF(TRIM(n.ten_nhom), ''), 'Chưa phân nhóm') AS NhomHang,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu,
  COALESCE(SUM(ct.gia_von * ct.so_luong), 0) AS GiaVon
" + DieuKien.From + @"
LEFT JOIN nhom_san_pham n ON n.id = sp.nhom_san_pham_id
WHERE " + dk.Where + @"
GROUP BY " + nhomGroup + @"
ORDER BY DoanhThu DESC, SoLuongBan DESC;
";
        var rs = await conn.QueryAsync<BaoCaoHangHoaChiTiet>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    // ============================================================
    // TÍNH THÀNH BIỂU ĐỒ (giống pattern TinhKhoang/ThanhBar của báo cáo bán hàng)
    // ============================================================

    private static void TinhThanhBar(List<BaoCaoHangHoaTop> list, Func<BaoCaoHangHoaTop, decimal> giaTri)
    {
        if (list.Count == 0)
            return;

        var max = list.Max(giaTri);
        foreach (var x in list)
            x.TyLeBar = max <= 0 ? 0 : (double)(giaTri(x) / max * 100m);
    }

    private static void TinhThanhNhom(List<BaoCaoHangHoaNhom> list)
    {
        if (list.Count == 0)
            return;

        var max = list.Max(x => x.DoanhThu);
        var tong = list.Sum(x => x.DoanhThu);
        foreach (var x in list)
        {
            x.TyLeBar = max <= 0 ? 0 : (double)(x.DoanhThu / max * 100m);
            x.TyLePhanTram = tong <= 0 ? 0 : (double)(x.DoanhThu / tong * 100m);
        }
    }
}
