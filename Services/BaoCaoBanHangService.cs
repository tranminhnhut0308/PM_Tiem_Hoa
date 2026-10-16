using System.Globalization;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ báo cáo bán hàng.
/// </summary>
public sealed class BaoCaoBanHangService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoBanHangService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var den = denNgay.Date;
        return (tu, den.AddDays(1));
    }

    public async Task<BaoCaoBanHangDuLieu> TaiDuLieuAsync(
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = (tuNgay.Date, denNgay.Date.AddDays(1));

        // _taoKetNoi() (DatabaseService.TaoKetNoiAsync) đã mở connection sẵn,
        // KHÔNG được gọi OpenAsync lần nữa (MySqlConnector ném
        // "Cannot Open when State is Open"). Dùng connection đã mở trực tiếp.
        await using var conn = await _taoKetNoi();

        var tongHop = await TaiTongHopAsync(conn, tu, denKeTiep, token);
        var bieuDo = await TaiBieuDoAsync(conn, tu, denKeTiep, token);
        var bangDuLieu = await TaiBangDuLieuAsync(conn, tu, denKeTiep, token);
        var topSanPham = await TaiTopSanPhamAsync(conn, tu, denKeTiep, token);
        var topKhachHang = await TaiTopKhachHangAsync(conn, tu, denKeTiep, token);
        var nhomHang = await TaiNhomHangAsync(conn, tu, denKeTiep, token);
        var phuongThuc = await TaiPhuongThucThanhToanAsync(conn, tu, denKeTiep, token);

        return new BaoCaoBanHangDuLieu
        {
            TuNgay = tu,
            DenNgay = denNgay.Date,
            TongHop = tongHop,
            BieuDo = bieuDo,
            BangDuLieu = bangDuLieu,
            TopSanPham = topSanPham,
            TopKhachHang = topKhachHang,
            NhomHang = nhomHang,
            PhuongThucThanhToan = phuongThuc
        };
    }

    public async Task<List<BaoCaoBanHangChiTietHoaDon>> TaiChiTietHoaDonAsync(
        DateTime ngay,
        CancellationToken token = default)
    {
        var tu = ngay.Date;
        var denKeTiep = tu.AddDays(1);

        // Connection đã mở sẵn bởi _taoKetNoi() — không OpenAsync lần nữa.
        await using var conn = await _taoKetNoi();

        string sql = @"
SELECT 
  hd.id AS Id,
  hd.ma_hoa_don AS MaHoaDon,
  hd.thoi_gian_ban AS ThoiGianBan,
  COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS KhachHang,
  hd.tong_tien_hang AS TongTienHang,
  hd.giam_gia AS GiamGia,
  hd.thu_khac AS ThuKhac,
  hd.khach_can_tra AS DoanhThu,
  hd.khach_da_thanh_toan AS DaThu,
  hd.con_no AS CongNo,
  COALESCE(hd.trang_thai_thanh_toan, '') AS TrangThai
FROM hoa_don_ban_hang hd
LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
ORDER BY hd.thoi_gian_ban ASC, hd.id ASC;
";
        var rs = await conn.QueryAsync<BaoCaoBanHangChiTietHoaDon>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        return rs.AsList();
    }

    private async Task<BaoCaoBanHangTongHop> TaiTongHopAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        string sql = @"
SELECT
  COUNT(*) AS SoHoaDon,
  COALESCE(SUM(hd.tong_tien_hang), 0) AS TongTienHang,
  COALESCE(SUM(hd.giam_gia), 0) AS GiamGia,
  COALESCE(SUM(hd.thu_khac), 0) AS ThuKhac,
  COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu,
  COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThu,
  COALESCE(SUM(hd.con_no), 0) AS CongNo
FROM hoa_don_ban_hang hd
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep;
";
        var r = await conn.QueryFirstOrDefaultAsync<BaoCaoBanHangTongHop>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)) ?? new BaoCaoBanHangTongHop();
        r.TuNgay = tu;
        r.DenNgay = denKeTiep.AddDays(-1);
        return r;
    }


    private async Task<List<BaoCaoBanHangDiemThoiGian>> TaiBieuDoAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        bool theoGio = (denKeTiep - tu).TotalDays <= 1;
        string bucket = theoGio
            ? "DATE_FORMAT(hd.thoi_gian_ban, '%Y-%m-%d %H:00:00')"
            : "DATE(hd.thoi_gian_ban)";
        string groupBy = bucket;

        string sql = @"
SELECT
  " + bucket + @" AS ThoiGianStr,
  COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu,
  COUNT(*) AS SoHoaDon,
  COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThu,
  COALESCE(SUM(hd.con_no), 0) AS CongNo
FROM hoa_don_ban_hang hd
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
GROUP BY " + groupBy + @"
ORDER BY ThoiGianStr ASC;
";
        var rs = await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        var list = new List<BaoCaoBanHangDiemThoiGian>();
        foreach (var r in rs)
        {
            // DATE_FORMAT trả về chuỗi, DATE() trả về DateTime — chuẩn hoá về DateTime.
            object raw = r.ThoiGianStr;
            DateTime tg = raw is DateTime trucTiep
                ? trucTiep
                : DateTime.Parse(raw.ToString() ?? string.Empty, CultureInfo.InvariantCulture);

            // Dapper trả COUNT(*) kiểu long và SUM(decimal) có thể về long/decimal
            // tuỳ MySQL — ép kiểu tường minh, tránh lỗi runtime
            // "Cannot implicitly convert type 'long' to 'int'" với truy vấn dynamic.
            list.Add(new BaoCaoBanHangDiemThoiGian
            {
                ThoiGian = tg,
                DoanhThu = Convert.ToDecimal(r.DoanhThu),
                SoHoaDon = Convert.ToInt32(r.SoHoaDon),
                DaThu = Convert.ToDecimal(r.DaThu),
                CongNo = Convert.ToDecimal(r.CongNo),
                NhanXet = tg.ToString(theoGio ? "HH:00" : "dd/MM")
            });
        }

        if (list.Count > 0)
        {
            // Chiều cao cột tính sẵn theo điểm cao nhất để XAML bind thẳng.
            var max = list.Max(x => x.DoanhThu);

            // Bề rộng cột giãn theo số điểm: ít điểm thì cột rộng, nhiều điểm thì hẹp
            // để biểu đồ gọn trong màn hình, hạn chế cuộn ngang.
            double beRongMongMuon = list.Count switch
            {
                <= 10 => 64,
                <= 24 => 44,
                <= 40 => 30,
                _ => 22
            };

            foreach (var x in list)
            {
                x.TyLeChieuCao = max <= 0 ? 0 : (double)(x.DoanhThu / max * 100m);
                x.TyLeBeRong = beRongMongMuon / BaoCaoBanHangDiemThoiGian.BeRongToiDa * 100d;
            }
        }

        return list;
    }



    private async Task<List<BaoCaoBanHangBangRow>> TaiBangDuLieuAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        string sql = @"SELECT DATE(hd.thoi_gian_ban) AS ThoiGian, COUNT(*) AS SoHoaDon, COALESCE(SUM(hd.tong_tien_hang), 0) AS TongTienHang, COALESCE(SUM(hd.giam_gia), 0) AS GiamGia, COALESCE(SUM(hd.thu_khac), 0) AS ThuKhac, COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu, COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThu, COALESCE(SUM(hd.con_no), 0) AS CongNo FROM hoa_don_ban_hang hd WHERE hd.trang_thai_hoa_don = 'HOAN_THANH' AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep GROUP BY DATE(hd.thoi_gian_ban) ORDER BY ThoiGian ASC;";
        var rs = await conn.QueryAsync<BaoCaoBanHangBangRow>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        var list = rs.AsList();
        foreach (var x in list) x.ThoiGianHienThi = x.ThoiGian.ToString("dd/MM/yyyy");
        return list;
    }

    private async Task<List<BaoCaoBanHangTopSanPham>> TaiTopSanPhamAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        string sql = @"
SELECT 
  ct.ten_san_pham AS SanPham,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu
FROM chi_tiet_hoa_don_ban_hang ct
INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
GROUP BY ct.san_pham_id, ct.ten_san_pham
ORDER BY SoLuongBan DESC, DoanhThu DESC
LIMIT 10;
";
        var rs = await conn.QueryAsync<BaoCaoBanHangTopSanPham>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        var list = rs.AsList();
        for (int i = 0; i < list.Count; i++) list[i].Stt = i + 1;
        return list;
    }
    private async Task<List<BaoCaoBanHangTopKhachHang>> TaiTopKhachHangAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        string sql = @"
SELECT 
  COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS KhachHang,
  COUNT(*) AS SoHoaDon,
  COALESCE(SUM(hd.khach_can_tra), 0) AS TongMua,
  COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThanhToan,
  COALESCE(SUM(hd.con_no), 0) AS CongNo
FROM hoa_don_ban_hang hd
LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
GROUP BY hd.khach_hang_id, COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ')
ORDER BY TongMua DESC, SoHoaDon DESC
LIMIT 10;
";
        var rs = await conn.QueryAsync<BaoCaoBanHangTopKhachHang>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        var list = rs.AsList();
        for (int i = 0; i < list.Count; i++) list[i].Stt = i + 1;
        return list;
    }
    private async Task<List<BaoCaoBanHangNhomHang>> TaiNhomHangAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        string sql = @"
SELECT
  COALESCE(nh.ten_nhom, 'Chưa phân nhóm') AS NhomHang,
  COALESCE(SUM(ct.so_luong), 0) AS SoLuongBan,
  COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu
FROM chi_tiet_hoa_don_ban_hang ct
INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
LEFT JOIN nhom_san_pham nh ON nh.id = sp.nhom_san_pham_id
WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
  AND hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep
GROUP BY nh.id, COALESCE(nh.ten_nhom, 'Chưa phân nhóm')
ORDER BY DoanhThu DESC;
";
        var rs = await conn.QueryAsync<BaoCaoBanHangNhomHang>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        var list = rs.AsList();

        var tongDoanhThu = list.Sum(x => x.DoanhThu);
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Stt = i + 1;
            list[i].TyLePhanTram = tongDoanhThu <= 0 ? 0 : (double)(list[i].DoanhThu / tongDoanhThu * 100m);
        }

        return list;
    }

    private async Task<List<BaoCaoBanHangPhuongThucRow>> TaiPhuongThucThanhToanAsync(MySqlConnection conn, DateTime tu, DateTime denKeTiep, CancellationToken token)
    {
        string sql = @"
SELECT 
  UPPER(TRIM(pc.phuong_thuc)) AS PhuongThuc,
  COUNT(*) AS SoGiaoDich,
  COALESCE(SUM(pc.so_tien), 0) AS SoTien
FROM phieu_chi pc
WHERE pc.trang_thai = 'HOAN_THANH'
  AND pc.ma_phieu_chi LIKE 'PT%'
  AND pc.thoi_gian_chi >= @TuNgay AND pc.thoi_gian_chi < @DenKeTiep
GROUP BY UPPER(TRIM(pc.phuong_thuc))
ORDER BY SoTien DESC;
";
        var rs = await conn.QueryAsync<BaoCaoBanHangPhuongThucRow>(new CommandDefinition(sql, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));
        var list = rs.AsList();
        foreach (var x in list)
        {
            x.PhuongThucHienThi = x.PhuongThuc switch
            {
                "TIEN_MAT" => "Tiền mặt",
                "CHUYEN_KHOAN" => "Chuyển khoản",
                "NGAN_HANG" => "Chuyển khoản",
                "CHUYEN_KHOAN_NGAN_HANG" => "Chuyển khoản",
                "QR" => "QR",
                "VI_DIEN_TU" => "Ví điện tử",
                "THE" => "Thẻ",
                "THE_TIN_DUNG" => "Thẻ",
                "VI" => "Ví",
                "KHAC" => "Khác",
                _ => string.IsNullOrWhiteSpace(x.PhuongThuc) ? "Khác" : x.PhuongThuc
            };
        }

        var tongTien = list.Sum(x => x.SoTien);
        foreach (var x in list)
            x.TyLePhanTram = tongTien <= 0 ? 0 : (double)(x.SoTien / tongTien * 100m);

        return list;
    }
}
