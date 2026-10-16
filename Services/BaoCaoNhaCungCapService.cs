using System.Globalization;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ cho màn "Báo cáo nhà cung cấp":
/// - Tính toán tổng tiền nhập hàng, tiền trả hàng, giá trị nhập hàng thực tế (đã trừ trả hàng),
///   đã thanh toán và công nợ theo từng nhà cung cấp.
/// - Top 10 nhà cung cấp nhập hàng nhiều nhất trực tiếp bằng SQL (ORDER BY ... DESC LIMIT 10).
/// - Bảng dữ liệu có tìm kiếm, sắp xếp theo cột và phân trang (LIMIT @SoDong OFFSET @Offset).
/// - Mọi phép tính thực hiện ở tầng SQL, không kéo toàn bộ bảng về RAM.
/// </summary>
public sealed class BaoCaoNhaCungCapService
{
    private static readonly IReadOnlyDictionary<string, string> CotSapXep =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaNhaCungCap"] = "ncc.ma_nha_cung_cap",
            ["TenNhaCungCap"] = "ncc.ten_nha_cung_cap",
            ["SoDienThoai"] = "ncc.so_dien_thoai",
            ["SoPhieuNhap"] = "SoPhieuNhap",
            ["TongTienNhap"] = "TongTienNhap",
            ["TienTraHang"] = "TienTraHang",
            ["GiaTriNhapHang"] = "GiaTriNhapHang",
            ["DaThanhToan"] = "DaThanhToan",
            ["CongNo"] = "CongNo"
        };

    public const string KhoaMacDinh = "GiaTriNhapHang";

    private readonly Func<Task<MySqlConnection>> _taoKetNoi;
    private bool? _coCotDuLieuKiotViet;

    public BaoCaoNhaCungCapService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var den = denNgay.Date;
        return (tu, den.AddDays(1));
    }

    private async Task<bool> KiemTraCotKiotVietAsync(MySqlConnection conn)
    {
        if (_coCotDuLieuKiotViet.HasValue)
            return _coCotDuLieuKiotViet.Value;

        try
        {
            const string sql = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'nha_cung_cap'
              AND COLUMN_NAME = 'co_du_lieu_kiotviet';
            """;
            var count = await conn.ExecuteScalarAsync<int>(sql);
            _coCotDuLieuKiotViet = count > 0;
        }
        catch
        {
            _coCotDuLieuKiotViet = false;
        }

        return _coCotDuLieuKiotViet.Value;
    }

    /// <summary>
    /// Tải toàn bộ dữ liệu báo cáo gồm: Thống kê tổng hợp, Top 10 biểu đồ, và danh sách trang hiện tại.
    /// </summary>
    public async Task<BaoCaoNhaCungCapDuLieu> TaiDuLieuAsync(
        DateTime tuNgay,
        DateTime denNgay,
        string? timKiem,
        string sapXepKho,
        bool sapXepTang,
        int trang,
        int soDongTrang,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);

        await using var conn = await _taoKetNoi();
        var coCotKiotViet = await KiemTraCotKiotVietAsync(conn);

        // Kiểm tra xem trong khoảng thời gian này có phiếu nhập hàng nào không
        const string kiemTraPhieuSql = """
        SELECT COUNT(*)
        FROM phieu_nhap_hang p
        WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
          AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY';
        """;

        var soPhieuTrongKy = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(kiemTraPhieuSql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

        var suDungKiotVietFallback = soPhieuTrongKy == 0 && coCotKiotViet;

        var dk = new DieuKien
        {
            TuNgay = tu,
            DenKeTiep = denKeTiep,
            TimKiem = string.IsNullOrWhiteSpace(timKiem) ? null : "%" + timKiem.Trim() + "%",
            SuDungKiotViet = suDungKiotVietFallback,
            CoCotKiotViet = coCotKiotViet
        };

        // 1. Thống kê tổng hợp
        var tongHop = await TaiTongHopAsync(conn, dk, token);
        tongHop.TuNgay = tu;
        tongHop.DenNgay = denKeTiep.AddDays(-1);

        // 2. Tổng số bản ghi (cho phân trang)
        var tongSoBanGhi = await TaiTongSoBanGhiAsync(conn, dk, token);

        // 3. Phân trang
        int soDong = soDongTrang < 1 ? 15 : soDongTrang;
        int trangHieu = trang < 1 ? 1 : trang;
        int offset = (trangHieu - 1) * soDong;

        var dongTrang = await TaiTrangAsync(conn, dk, sapXepKho, sapXepTang, soDong, offset, token);
        for (int i = 0; i < dongTrang.Count; i++)
            dongTrang[i].Stt = offset + i + 1;

        // 4. Top 10 biểu đồ
        var top = await TaiTop10Async(conn, dk, token);
        for (int i = 0; i < top.Count; i++)
            top[i].Stt = i + 1;

        // 4b. Biểu đồ nhập hàng theo thời gian (ngày/giờ)
        var (bieuDo, bieuDoTrucY) = await TaiBieuDoAsync(conn, dk, token);

        // 5. Tính thanh biểu đồ và mốc chia trục X (chuẩn 10 khoảng như hình tham khảo)
        var (scaleMarks, scaleLabels, maxScale) = TinhThanhBieuDoVaMocChia(top);

        return new BaoCaoNhaCungCapDuLieu
        {
            TongHop = tongHop,
            Top = top,
            BieuDo = bieuDo,
            DongTrang = dongTrang,
            ScaleMarks = scaleMarks,
            ScaleLabels = scaleLabels,
            BieuDoTrucY = bieuDoTrucY,
            TongSoBanGhi = tongSoBanGhi,
            MaxScaleValue = maxScale
        };
    }

    private sealed class DieuKien
    {
        public DateTime TuNgay { get; init; }
        public DateTime DenKeTiep { get; init; }
        public string? TimKiem { get; init; }
        public bool SuDungKiotViet { get; init; }
        public bool CoCotKiotViet { get; init; }

        public object Params => new { TuNgay, DenKeTiep, TimKiem };
    }

    private static string BieuThucTraHang(DieuKien dk)
    {
        // Tiền trả hàng từ sổ cái công nợ (loại giao dịch trả hàng/giảm trừ)
        var subSql = @"COALESCE((
            SELECT SUM(ls.ghi_co)
            FROM lich_su_cong_no_nha_cung_cap ls
            WHERE ls.nha_cung_cap_id = ncc.id
              AND ls.loai_giao_dich IN ('TRA_HANG', 'TRA_HANG_NHAP', 'XUAT_TRA', 'GIAM_TRU')
              AND ls.thoi_gian >= @TuNgay AND ls.thoi_gian < @DenKeTiep
        ), 0)";

        if (dk.SuDungKiotViet)
        {
            return @"CASE
                WHEN ncc.co_du_lieu_kiotviet = 1 AND COALESCE(ncc.tong_mua, 0) > COALESCE(ncc.tong_mua_tru_tra_hang, 0)
                THEN (COALESCE(ncc.tong_mua, 0) - COALESCE(ncc.tong_mua_tru_tra_hang, 0))
                ELSE " + subSql + @"
            END";
        }

        return subSql;
    }

    private static string BieuThucCongNo(DieuKien dk)
    {
        var congNoNoiBo = @"COALESCE((
            SELECT SUM(ls.ghi_no - ls.ghi_co)
            FROM lich_su_cong_no_nha_cung_cap ls
            WHERE ls.nha_cung_cap_id = ncc.id
        ), COALESCE((
            SELECT SUM(p_cn.con_no)
            FROM phieu_nhap_hang p_cn
            WHERE p_cn.nha_cung_cap_id = ncc.id
              AND p_cn.trang_thai != 'HUY'
        ), 0))";

        if (dk.CoCotKiotViet)
        {
            return @"CASE
                WHEN ncc.co_du_lieu_kiotviet = 1 AND ncc.no_can_tra_hien_tai IS NOT NULL
                THEN (COALESCE(ncc.no_can_tra_hien_tai, 0) + " + congNoNoiBo + @")
                ELSE " + congNoNoiBo + @"
            END";
        }

        return congNoNoiBo;
    }

    private async Task<BaoCaoNhaCungCapTongHop> TaiTongHopAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        if (dk.SuDungKiotViet)
        {
            var sqlKiotViet = $"""
            SELECT
                COUNT(*) AS TongNhaCungCap,
                0 AS SoPhieuNhap,
                COALESCE(SUM(ncc.tong_mua), 0) AS TongTienNhap,
                COALESCE(SUM(CASE WHEN ncc.tong_mua > ncc.tong_mua_tru_tra_hang THEN ncc.tong_mua - ncc.tong_mua_tru_tra_hang ELSE 0 END), 0) AS TienTraHang,
                COALESCE(SUM(CASE WHEN ncc.tong_mua > ncc.no_can_tra_hien_tai THEN ncc.tong_mua - ncc.no_can_tra_hien_tai ELSE 0 END), 0) AS DaThanhToan,
                COALESCE(SUM(ncc.no_can_tra_hien_tai), 0) AS CongNo
            FROM nha_cung_cap ncc
            WHERE ncc.trang_thai = 1
              AND ncc.co_du_lieu_kiotviet = 1
              AND COALESCE(ncc.tong_mua, 0) > 0
              {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")};
            """;

            return await conn.QueryFirstOrDefaultAsync<BaoCaoNhaCungCapTongHop>(
                new CommandDefinition(sqlKiotViet, dk.Params, cancellationToken: token))
                ?? new BaoCaoNhaCungCapTongHop();
        }

        var tienTraHangExpr = BieuThucTraHang(dk);
        var congNoExpr = BieuThucCongNo(dk);

        var sql = $"""
        SELECT
            COUNT(DISTINCT ncc.id) AS TongNhaCungCap,
            COUNT(p.id) AS SoPhieuNhap,
            COALESCE(SUM(p.tong_tien_nhap), 0) AS TongTienNhap,
            COALESCE(SUM({tienTraHangExpr}), 0) AS TienTraHang,
            COALESCE(SUM(p.da_thanh_toan), 0) AS DaThanhToan,
            COALESCE(SUM({congNoExpr}), 0) AS CongNo
        FROM phieu_nhap_hang p
        INNER JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
          AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY'
          AND ncc.trang_thai = 1
          {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")};
        """;

        return await conn.QueryFirstOrDefaultAsync<BaoCaoNhaCungCapTongHop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token))
            ?? new BaoCaoNhaCungCapTongHop();
    }

    private async Task<int> TaiTongSoBanGhiAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        if (dk.SuDungKiotViet)
        {
            var sqlKiotViet = $"""
            SELECT COUNT(*)
            FROM nha_cung_cap ncc
            WHERE ncc.trang_thai = 1
              AND ncc.co_du_lieu_kiotviet = 1
              AND COALESCE(ncc.tong_mua, 0) > 0
              {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")};
            """;

            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(sqlKiotViet, dk.Params, cancellationToken: token));
        }

        var sql = $"""
        SELECT COUNT(DISTINCT ncc.id)
        FROM phieu_nhap_hang p
        INNER JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
          AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY'
          AND ncc.trang_thai = 1
          {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")};
        """;

        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
    }

    private async Task<List<BaoCaoNhaCungCapDong>> TaiTrangAsync(
        MySqlConnection conn, DieuKien dk, string sapXepKho, bool sapXepTang,
        int soDong, int offset, CancellationToken token)
    {
        string orderBy = LayOrderBy(sapXepKho, sapXepTang);

        if (dk.SuDungKiotViet)
        {
            var sqlKiotViet = $"""
            SELECT
                ncc.id AS Id,
                COALESCE(ncc.ma_nha_cung_cap, '') AS MaNhaCungCap,
                COALESCE(ncc.ten_nha_cung_cap, '') AS TenNhaCungCap,
                COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
                0 AS SoPhieuNhap,
                COALESCE(ncc.tong_mua, 0) AS TongTienNhap,
                COALESCE(CASE WHEN ncc.tong_mua > ncc.tong_mua_tru_tra_hang THEN ncc.tong_mua - ncc.tong_mua_tru_tra_hang ELSE 0 END, 0) AS TienTraHang,
                COALESCE(ncc.tong_mua_tru_tra_hang, COALESCE(ncc.tong_mua, 0)) AS GiaTriNhapHang,
                COALESCE(CASE WHEN ncc.tong_mua > ncc.no_can_tra_hien_tai THEN ncc.tong_mua - ncc.no_can_tra_hien_tai ELSE 0 END, 0) AS DaThanhToan,
                COALESCE(ncc.no_can_tra_hien_tai, 0) AS CongNo
            FROM nha_cung_cap ncc
            WHERE ncc.trang_thai = 1
              AND ncc.co_du_lieu_kiotviet = 1
              AND COALESCE(ncc.tong_mua, 0) > 0
              {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")}
            ORDER BY {orderBy}, ncc.id
            LIMIT @SoDong OFFSET @Offset;
            """;

            var thamSoKv = new
            {
                dk.TuNgay,
                dk.DenKeTiep,
                dk.TimKiem,
                SoDong = soDong,
                Offset = offset
            };

            var rsKv = await conn.QueryAsync<BaoCaoNhaCungCapDong>(
                new CommandDefinition(sqlKiotViet, thamSoKv, cancellationToken: token));
            return rsKv.AsList();
        }

        var tienTraHangExpr = BieuThucTraHang(dk);
        var congNoExpr = BieuThucCongNo(dk);

        var sql = $"""
        SELECT
            ncc.id AS Id,
            COALESCE(ncc.ma_nha_cung_cap, '') AS MaNhaCungCap,
            COALESCE(ncc.ten_nha_cung_cap, '') AS TenNhaCungCap,
            COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
            COUNT(p.id) AS SoPhieuNhap,
            COALESCE(SUM(p.tong_tien_nhap), 0) AS TongTienNhap,
            {tienTraHangExpr} AS TienTraHang,
            (COALESCE(SUM(p.tong_tien_nhap), 0) - {tienTraHangExpr}) AS GiaTriNhapHang,
            COALESCE(SUM(p.da_thanh_toan), 0) AS DaThanhToan,
            {congNoExpr} AS CongNo
        FROM phieu_nhap_hang p
        INNER JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
          AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY'
          AND ncc.trang_thai = 1
          {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")}
        GROUP BY ncc.id, ncc.ma_nha_cung_cap, ncc.ten_nha_cung_cap, ncc.so_dien_thoai
        ORDER BY {orderBy}, ncc.id
        LIMIT @SoDong OFFSET @Offset;
        """;

        var thamSo = new
        {
            dk.TuNgay,
            dk.DenKeTiep,
            dk.TimKiem,
            SoDong = soDong,
            Offset = offset
        };

        var rs = await conn.QueryAsync<BaoCaoNhaCungCapDong>(
            new CommandDefinition(sql, thamSo, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<List<BaoCaoNhaCungCapTop>> TaiTop10Async(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        if (dk.SuDungKiotViet)
        {
            var sqlKiotViet = $"""
            SELECT
                ncc.id AS Id,
                COALESCE(ncc.ma_nha_cung_cap, '') AS MaNhaCungCap,
                COALESCE(ncc.ten_nha_cung_cap, '') AS TenNhaCungCap,
                COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
                0 AS SoPhieuNhap,
                COALESCE(ncc.tong_mua, 0) AS TongTienNhap,
                COALESCE(CASE WHEN ncc.tong_mua > ncc.tong_mua_tru_tra_hang THEN ncc.tong_mua - ncc.tong_mua_tru_tra_hang ELSE 0 END, 0) AS TienTraHang,
                COALESCE(ncc.tong_mua_tru_tra_hang, COALESCE(ncc.tong_mua, 0)) AS GiaTriNhapHang,
                COALESCE(CASE WHEN ncc.tong_mua > ncc.no_can_tra_hien_tai THEN ncc.tong_mua - ncc.no_can_tra_hien_tai ELSE 0 END, 0) AS DaThanhToan,
                COALESCE(ncc.no_can_tra_hien_tai, 0) AS CongNo
            FROM nha_cung_cap ncc
            WHERE ncc.trang_thai = 1
              AND ncc.co_du_lieu_kiotviet = 1
              AND COALESCE(ncc.tong_mua, 0) > 0
              {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")}
            ORDER BY GiaTriNhapHang DESC, ncc.id
            LIMIT 10;
            """;

            var rsKv = await conn.QueryAsync<BaoCaoNhaCungCapTop>(
                new CommandDefinition(sqlKiotViet, dk.Params, cancellationToken: token));
            return rsKv.AsList();
        }

        var tienTraHangExpr = BieuThucTraHang(dk);
        var congNoExpr = BieuThucCongNo(dk);

        var sql = $"""
        SELECT
            ncc.id AS Id,
            COALESCE(ncc.ma_nha_cung_cap, '') AS MaNhaCungCap,
            COALESCE(ncc.ten_nha_cung_cap, '') AS TenNhaCungCap,
            COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
            COUNT(p.id) AS SoPhieuNhap,
            COALESCE(SUM(p.tong_tien_nhap), 0) AS TongTienNhap,
            {tienTraHangExpr} AS TienTraHang,
            (COALESCE(SUM(p.tong_tien_nhap), 0) - {tienTraHangExpr}) AS GiaTriNhapHang,
            COALESCE(SUM(p.da_thanh_toan), 0) AS DaThanhToan,
            {congNoExpr} AS CongNo
        FROM phieu_nhap_hang p
        INNER JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
        WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
          AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY'
          AND ncc.trang_thai = 1
          {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")}
        GROUP BY ncc.id, ncc.ma_nha_cung_cap, ncc.ten_nha_cung_cap, ncc.so_dien_thoai
        ORDER BY GiaTriNhapHang DESC, SoPhieuNhap DESC, ncc.id
        LIMIT 10;
        """;

        var rs = await conn.QueryAsync<BaoCaoNhaCungCapTop>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));
        return rs.AsList();
    }

    /// <summary>
    /// Biểu đồ "Nhập hàng theo thời gian": khoảng ≤ 1 ngày chia theo giờ, dài hơn chia
    /// theo ngày. Gộp tiền nhập (từ phiếu nhập) và tiền trả hàng (từ sổ cái công nợ).
    /// </summary>
    private async Task<(List<BaoCaoNhaCungCapDiemThoiGian> Diem, string[] TrucY)> TaiBieuDoAsync(
        MySqlConnection conn, DieuKien dk, CancellationToken token)
    {
        // Nhánh KiotViet không có phiếu nhập nội bộ nên không dựng được chuỗi thời gian.
        if (dk.SuDungKiotViet)
            return (new List<BaoCaoNhaCungCapDiemThoiGian>(), new string[5]);

        bool theoGio = (dk.DenKeTiep - dk.TuNgay).TotalDays <= 1;
        string format = theoGio ? "%Y-%m-%d %H:00:00" : "%Y-%m-%d";

        var sql = $"""
        SELECT ThoiGianStr,
               COALESCE(SUM(TienNhap), 0) AS TienNhap,
               COALESCE(SUM(TienTraHang), 0) AS TienTraHang,
               COALESCE(SUM(SoPhieuNhap), 0) AS SoPhieuNhap
        FROM (
            SELECT DATE_FORMAT(p.thoi_gian_nhap, '{format}') AS ThoiGianStr,
                   COALESCE(SUM(p.tong_tien_nhap), 0) AS TienNhap,
                   0 AS TienTraHang,
                   COUNT(*) AS SoPhieuNhap
            FROM phieu_nhap_hang p
            INNER JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            WHERE p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
              AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY'
              AND ncc.trang_thai = 1
              {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")}
            GROUP BY ThoiGianStr
            UNION ALL
            SELECT DATE_FORMAT(ls.thoi_gian, '{format}') AS ThoiGianStr,
                   0 AS TienNhap,
                   COALESCE(SUM(ls.ghi_co), 0) AS TienTraHang,
                   0 AS SoPhieuNhap
            FROM lich_su_cong_no_nha_cung_cap ls
            INNER JOIN nha_cung_cap ncc ON ncc.id = ls.nha_cung_cap_id
            WHERE ls.thoi_gian >= @TuNgay AND ls.thoi_gian < @DenKeTiep
              AND ls.loai_giao_dich IN ('TRA_HANG', 'TRA_HANG_NHAP', 'XUAT_TRA', 'GIAM_TRU')
              AND ncc.trang_thai = 1
              {(dk.TimKiem is null ? "" : "AND (ncc.ma_nha_cung_cap LIKE @TimKiem OR ncc.ten_nha_cung_cap LIKE @TimKiem OR ncc.so_dien_thoai LIKE @TimKiem)")}
            GROUP BY ThoiGianStr
        ) t
        GROUP BY ThoiGianStr
        ORDER BY ThoiGianStr ASC;
        """;

        var rs = await conn.QueryAsync<dynamic>(
            new CommandDefinition(sql, dk.Params, cancellationToken: token));

        var list = new List<BaoCaoNhaCungCapDiemThoiGian>();
        foreach (var r in rs)
        {
            var tg = DateTime.Parse(Convert.ToString(r.ThoiGianStr) ?? string.Empty, CultureInfo.InvariantCulture);

            // Dapper trả COUNT(*) kiểu long và SUM(decimal) có thể về long/decimal tuỳ
            // MySQL — ép kiểu tường minh để tránh lỗi runtime với truy vấn dynamic.
            list.Add(new BaoCaoNhaCungCapDiemThoiGian
            {
                ThoiGian = tg,
                TienNhap = Convert.ToDecimal(r.TienNhap),
                TienTraHang = Convert.ToDecimal(r.TienTraHang),
                SoPhieuNhap = Convert.ToInt32(r.SoPhieuNhap),
                NhanXet = tg.ToString(theoGio ? "HH:00" : "dd/MM")
            });
        }

        if (list.Count > 0)
        {
            // Điền đủ các mốc thời gian bị thiếu để mỗi ngày/giờ đúng một cột (không có khoảng trống).
            var dayDu = DienDayDuThoiGian(list, dk, theoGio, format);

            // Trục tung làm tròn đẹp (4 khoảng, 5 nhãn) để cột không bị lệch thang.
            var max = dayDu.Max(x => x.GiaTriNhap);
            var (trucMax, nhanTrucY) = TinhTrucY(max);

            // Bề rộng cột giãn theo số điểm: ít điểm thì cột rộng, nhiều điểm thì hẹp.
            double beRongMongMuon = dayDu.Count switch
            {
                <= 10 => 64,
                <= 24 => 44,
                <= 40 => 30,
                _ => 22
            };

            foreach (var x in dayDu)
            {
                var giaTri = Math.Max(0m, x.GiaTriNhap);
                x.TyLeChieuCao = trucMax <= 0 ? 0 : (double)(giaTri / trucMax * 100m);
                x.TyLeBeRong = beRongMongMuon / BaoCaoNhaCungCapDiemThoiGian.BeRongToiDa * 100d;
            }

            return (dayDu, nhanTrucY);
        }

        return (list, new string[5]);
    }

    /// <summary>
    /// Điền các mốc thời gian còn thiếu giữa TuNgay và DenKeTiep để biểu đồ luôn có
    /// một cột cho mỗi ngày/giờ. Nếu khoảng quá dài (&gt; 400 mốc) thì giữ nguyên dữ liệu thưa.
    /// </summary>
    private static List<BaoCaoNhaCungCapDiemThoiGian> DienDayDuThoiGian(
        List<BaoCaoNhaCungCapDiemThoiGian> tho, DieuKien dk, bool theoGio, string format)
    {
        var buoc = theoGio ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var soMoc = (int)Math.Round((dk.DenKeTiep - dk.TuNgay).TotalMinutes / buoc.TotalMinutes);
        if (soMoc <= 0 || soMoc > 400)
            return tho;

        var map = tho.ToDictionary(x => x.ThoiGian, x => x);
        var dayDu = new List<BaoCaoNhaCungCapDiemThoiGian>(soMoc);

        for (int i = 0; i < soMoc; i++)
        {
            var tg = dk.TuNgay + buoc * i;
            if (map.TryGetValue(tg, out var co))
            {
                dayDu.Add(co);
            }
            else
            {
                dayDu.Add(new BaoCaoNhaCungCapDiemThoiGian
                {
                    ThoiGian = tg,
                    TienNhap = 0,
                    TienTraHang = 0,
                    SoPhieuNhap = 0,
                    NhanXet = tg.ToString(theoGio ? "HH:00" : "dd/MM", CultureInfo.InvariantCulture)
                });
            }
        }

        return dayDu;
    }

    /// <summary>
    /// Trục tung làm tròn "đẹp" cho biểu đồ thời gian: chia 4 khoảng đều, trả về giá trị
    /// lớn nhất của trục và 5 nhãn (từ 0 ở dưới lên giá trị lớn nhất ở trên).
    /// </summary>
    private static (decimal TrucMax, string[] Nhan) TinhTrucY(decimal maxGiaTri)
    {
        var nhan = new string[5];
        if (maxGiaTri <= 0)
        {
            for (int i = 0; i < nhan.Length; i++)
                nhan[i] = string.Empty;
            return (0m, nhan);
        }

        var step = TinhBuocChiaDep(maxGiaTri / 4m);
        if (step <= 0)
            step = 1_000_000m;

        var trucMax = step * 4m;
        for (int i = 0; i < nhan.Length; i++)
            nhan[i] = DinhDangNhanMoc(step * i);

        return (trucMax, nhan);
    }

    /// <summary>
    /// Danh sách phiếu nhập hàng của MỘT nhà cung cấp trong khoảng đang xem
    /// (dùng cho bảng chi tiết khi bấm vào một dòng nhà cung cấp).
    /// </summary>
    public async Task<List<BaoCaoNhaCungCapPhieuNhap>> TaiPhieuNhapCuaNhaCungCapAsync(
        uint nhaCungCapId, DateTime tuNgay, DateTime denNgay, CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);

        await using var conn = await _taoKetNoi();

        const string sql = """
        SELECT
            p.id AS Id,
            COALESCE(p.ma_phieu_nhap, '') AS MaPhieuNhap,
            p.thoi_gian_nhap AS ThoiGianNhap,
            COALESCE(p.tong_tien_nhap, 0) AS TongTienNhap,
            COALESCE(p.da_thanh_toan, 0) AS DaThanhToan,
            COALESCE(p.con_no, 0) AS ConNo,
            COALESCE(p.trang_thai, '') AS TrangThai
        FROM phieu_nhap_hang p
        WHERE p.nha_cung_cap_id = @NhaCungCapId
          AND p.thoi_gian_nhap >= @TuNgay AND p.thoi_gian_nhap < @DenKeTiep
          AND p.trang_thai != 'LUU_TAM' AND p.trang_thai != 'HUY'
        ORDER BY p.thoi_gian_nhap DESC, p.id DESC;
        """;

        var rs = await conn.QueryAsync<BaoCaoNhaCungCapPhieuNhap>(
            new CommandDefinition(
                sql,
                new { NhaCungCapId = nhaCungCapId, TuNgay = tu, DenKeTiep = denKeTiep },
                cancellationToken: token));

        return rs.AsList();
    }

    private static string LayOrderBy(string? kho, bool sapXepTang)
    {
        var cot = kho is not null && CotSapXep.TryGetValue(kho, out var expr)
            ? expr
            : CotSapXep[KhoaMacDinh];

        return $"{cot} {(sapXepTang ? "ASC" : "DESC")}";
    }

    /// <summary>
    /// Tính toán các mốc chia trên trục hoành X (chia đúng 10 khoảng đều như hình tham khảo: 0, 3 tr, 6 tr, 9 tr... 30 tr).
    /// </summary>
    private static (List<BaoCaoNhaCungCapScaleMark> ScaleMarks, string[] ScaleLabels, decimal MaxScale) TinhThanhBieuDoVaMocChia(
        List<BaoCaoNhaCungCapTop> list)
    {
        var marks = new List<BaoCaoNhaCungCapScaleMark>();
        var labels = new string[11];

        decimal maxNet = list.Count > 0 ? list.Max(x => x.GiaTriNhapHang) : 0m;
        if (maxNet <= 0)
            maxNet = 10_000_000m;

        // Luôn chia thành 10 khoảng đều như hình tham khảo
        var step = TinhBuocChiaDep(maxNet / 10m);
        if (step <= 0)
            step = 1_000_000m;

        var maxScale = step * 10m;

        // Bộ bước "đẹp" dày nên maxScale luôn >= maxNet; chỉ chặn sai số làm tròn.
        if (maxScale < maxNet)
        {
            step = maxNet / 10m;
            maxScale = maxNet;
        }

        for (int i = 0; i <= 10; i++)
        {
            var val = i * step;
            var lbl = DinhDangNhanMoc(val);
            labels[i] = lbl;
            marks.Add(new BaoCaoNhaCungCapScaleMark
            {
                Value = val,
                Label = lbl,
                PositionPercent = i * 10.0
            });
        }

        const double beRongMacDinh = 700;

        foreach (var x in list)
        {
            x.TyLeBar = maxScale <= 0 ? 0 : Math.Max(0, (double)(x.GiaTriNhapHang / maxScale * 100m));
            x.RongBar = Math.Max(0, x.TyLeBar / 100d * beRongMacDinh);
        }

        return (marks, labels, maxScale);
    }

    /// <summary>Bộ bước chia "đẹp" dày để trục không nhảy gấp đôi khi giá trị vừa vượt mốc.</summary>
    private static readonly decimal[] BuocChiaDep =
    {
        1m, 1.2m, 1.4m, 1.6m, 1.8m, 2m, 2.5m, 3m, 3.5m, 4m, 5m, 6m, 7m, 8m, 10m
    };

    private static decimal TinhBuocChiaDep(decimal roughStep)
    {
        if (roughStep <= 0)
            return 1_000_000m;

        var power = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)roughStep)));
        var fraction = roughStep / power;

        foreach (var f in BuocChiaDep)
        {
            if (fraction <= f + 0.0000001m)
                return f * power;
        }

        return 10m * power;
    }

    private static string DinhDangNhanMoc(decimal val)
    {
        if (val == 0)
            return "0";

        if (val >= 1_000_000_000m)
        {
            var ty = val / 1_000_000_000m;
            return ty == Math.Floor(ty) ? $"{ty:0} tỷ" : $"{ty:0.#} tỷ";
        }

        if (val >= 1_000_000m)
        {
            var tr = val / 1_000_000m;
            return tr == Math.Floor(tr) ? $"{tr:0} tr" : $"{tr:0.#} tr";
        }

        if (val >= 1_000m)
        {
            var k = val / 1_000m;
            return $"{k:0} k";
        }

        return val.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    }
}
