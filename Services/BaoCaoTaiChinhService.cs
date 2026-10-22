using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ lập báo cáo tài chính cho phần mềm Quản lý Shop Hoa:
/// - Giao diện 57: Báo cáo kết quả hoạt động kinh doanh (P&amp;L)
/// - Giao diện 58: Báo cáo cơ cấu vốn kinh doanh và tài sản lưu động
/// </summary>
public sealed class BaoCaoTaiChinhService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoTaiChinhService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    #region GIAO DIỆN 57 – BÁO CÁO KẾT QUẢ KINH DOANH

    /// <summary>
    /// Tải dữ liệu Báo cáo kết quả kinh doanh từ cơ sở dữ liệu thật theo khoảng thời gian.
    /// </summary>
    public async Task<BaoCaoKetQuaKinhDoanhDuLieu> TaiBaoCaoKetQuaKinhDoanhAsync(
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token = default)
    {
        var tu = tuNgay.Date;
        var denKeTiep = denNgay.Date.AddDays(1);

        await using var conn = await _taoKetNoi();

        // 1. Doanh thu bán hàng & Giảm trừ doanh thu
        const string sqlDoanhThu = """
        SELECT
            COUNT(*)                              AS SoHoaDon,
            COALESCE(SUM(hd.tong_tien_hang), 0)   AS TongTienHang,
            COALESCE(SUM(hd.giam_gia), 0)         AS GiamGia,
            COALESCE(SUM(hd.khach_can_tra), 0)    AS DoanhThuThuan
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban < @DenKeTiep;
        """;

        var doanhThuInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition(sqlDoanhThu, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

        int soHoaDon = doanhThuInfo != null ? Convert.ToInt32(doanhThuInfo.SoHoaDon) : 0;
        decimal tongTienHang = doanhThuInfo != null ? Convert.ToDecimal(doanhThuInfo.TongTienHang) : 0m;
        decimal giamGia = doanhThuInfo != null ? Convert.ToDecimal(doanhThuInfo.GiamGia) : 0m;
        decimal doanhThuThuan = tongTienHang - giamGia;

        // 2. Giá vốn hàng bán (COGS)
        const string sqlGiaVon = """
        SELECT
            COALESCE(SUM(ct.so_luong * COALESCE(ct.gia_von, sp.gia_von, 0)), 0) AS GiaVon
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
        LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban < @DenKeTiep;
        """;

        decimal giaVon = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sqlGiaVon, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

        // Lợi nhuận gộp
        decimal loiNhuanGop = doanhThuThuan - giaVon;
        double tySuatLoiNhuanGop = doanhThuThuan > 0 ? (double)(loiNhuanGop / doanhThuThuan * 100m) : 0;

        // 3. Chi phí kinh doanh (chi hoạt động cửa hàng, không bao gồm trả nợ NCC)
        const string sqlChiPhi = """
        SELECT
            COALESCE(ncp.ten_nhom, 'Chi phí vận hành khác') AS TenNhom,
            COALESCE(ncp.ma_nhom, 'CPK')                    AS MaNhom,
            COUNT(*)                                        AS SoChungTu,
            COALESCE(SUM(pc.so_tien), 0)                    AS SoTien
        FROM phieu_chi pc
        LEFT JOIN nhom_chi_phi ncp ON ncp.id = pc.nhom_chi_phi_id
        WHERE pc.ma_phieu_chi LIKE 'PC%'
          AND pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi >= @TuNgay
          AND pc.thoi_gian_chi < @DenKeTiep
          AND (pc.nhom_chi_phi_id IS NULL OR COALESCE(ncp.ma_nhom, '') NOT IN ('CHI_NHA_CUNG_CAP', 'NHA_CUNG_CAP'))
        GROUP BY COALESCE(ncp.ten_nhom, 'Chi phí vận hành khác'), COALESCE(ncp.ma_nhom, 'CPK')
        ORDER BY SoTien DESC;
        """;

        var chiPhiRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlChiPhi, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token))).AsList();

        var chiTietChiPhi = new List<ChiPhiTheoNhomItem>();
        decimal tongChiPhi = 0m;
        int tongSoPhieuChi = 0;

        int sttCp = 1;
        foreach (var row in chiPhiRows)
        {
            var st = Convert.ToDecimal(row.SoTien);
            tongChiPhi += st;
            tongSoPhieuChi += Convert.ToInt32(row.SoChungTu);
            chiTietChiPhi.Add(new ChiPhiTheoNhomItem
            {
                Stt = sttCp++,
                MaNhom = Convert.ToString(row.MaNhom) ?? "",
                TenNhom = Convert.ToString(row.TenNhom) ?? "",
                SoChungTu = Convert.ToInt32(row.SoChungTu),
                SoTien = st
            });
        }

        foreach (var cp in chiTietChiPhi)
        {
            cp.TyLePhanTram = tongChiPhi > 0 ? (double)(cp.SoTien / tongChiPhi * 100m) : 0;
        }

        // Lợi nhuận thuần từ HĐKD
        decimal loiNhuanThuanHDKD = loiNhuanGop - tongChiPhi;

        // 4. Thu nhập khác (Phiếu thu ngoài doanh thu bán hàng)
        const string sqlThuKhac = """
        SELECT COALESCE(SUM(pc.so_tien), 0)
        FROM phieu_chi pc
        LEFT JOIN nhom_chi_phi ncp ON ncp.id = pc.nhom_chi_phi_id
        WHERE pc.ma_phieu_chi LIKE 'PT%'
          AND pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi >= @TuNgay
          AND pc.thoi_gian_chi < @DenKeTiep
          AND pc.hoa_don_ban_hang_id IS NULL
          AND (pc.nhom_chi_phi_id IS NULL OR COALESCE(ncp.ma_nhom, '') NOT IN ('THU_KHACH_HANG', 'KHACH_HANG'));
        """;

        decimal thuNhapKhac = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sqlThuKhac, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

        // 5. Chi phí khác (Khoản chi bất thường/ngoài kinh doanh)
        decimal chiPhiKhac = 0m;
        decimal loiNhuanKhac = thuNhapKhac - chiPhiKhac;

        // 6. Tổng lợi nhuận kế toán trước thuế
        decimal tongLoiNhuan = loiNhuanThuanHDKD + loiNhuanKhac;
        double tySuatLoiNhuanThuan = doanhThuThuan > 0 ? (double)(tongLoiNhuan / doanhThuThuan * 100m) : 0;

        // 7. Bảng kết quả theo ngày
        const string sqlTheoNgay = """
        SELECT
            DATE(hd.thoi_gian_ban) AS Ngay,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(hd.tong_tien_hang), 0) AS DoanhThuHang,
            COALESCE(SUM(hd.giam_gia), 0) AS GiamGia,
            COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThuThuan
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban < @DenKeTiep
        GROUP BY DATE(hd.thoi_gian_ban)
        ORDER BY Ngay ASC;
        """;

        var ngayRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlTheoNgay, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token))).AsList();

        // Giá vốn theo ngày
        const string sqlGiaVonTheoNgay = """
        SELECT
            DATE(hd.thoi_gian_ban) AS Ngay,
            COALESCE(SUM(ct.so_luong * COALESCE(ct.gia_von, sp.gia_von, 0)), 0) AS GiaVon
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
        LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban < @DenKeTiep
        GROUP BY DATE(hd.thoi_gian_ban);
        """;

        var gvTheoNgayMap = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlGiaVonTheoNgay, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)))
            .ToDictionary(
                r => (DateTime)Convert.ToDateTime(r.Ngay).Date,
                r => (decimal)Convert.ToDecimal(r.GiaVon));

        // Chi phí theo ngày
        const string sqlChiPhiTheoNgay = """
        SELECT
            DATE(pc.thoi_gian_chi) AS Ngay,
            COALESCE(SUM(pc.so_tien), 0) AS ChiPhi
        FROM phieu_chi pc
        LEFT JOIN nhom_chi_phi ncp ON ncp.id = pc.nhom_chi_phi_id
        WHERE pc.ma_phieu_chi LIKE 'PC%'
          AND pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi >= @TuNgay
          AND pc.thoi_gian_chi < @DenKeTiep
          AND (pc.nhom_chi_phi_id IS NULL OR COALESCE(ncp.ma_nhom, '') NOT IN ('CHI_NHA_CUNG_CAP', 'NHA_CUNG_CAP'))
        GROUP BY DATE(pc.thoi_gian_chi);
        """;

        var cpTheoNgayMap = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlChiPhiTheoNgay, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)))
            .ToDictionary(
                r => (DateTime)Convert.ToDateTime(r.Ngay).Date,
                r => (decimal)Convert.ToDecimal(r.ChiPhi));

        var chiTietTheoNgay = new List<KetQuaKinhDoanhTheoNgayItem>();
        int sttNgay = 1;
        foreach (var nr in ngayRows)
        {
            DateTime ngay = Convert.ToDateTime(nr.Ngay).Date;
            var dtHang = Convert.ToDecimal(nr.DoanhThuHang);
            var gg = Convert.ToDecimal(nr.GiamGia);
            var dtThuan = dtHang - gg;
            var gv = gvTheoNgayMap.GetValueOrDefault(ngay, 0m);
            var lnGop = dtThuan - gv;
            var cp = cpTheoNgayMap.GetValueOrDefault(ngay, 0m);
            var lnThuan = lnGop - cp;

            chiTietTheoNgay.Add(new KetQuaKinhDoanhTheoNgayItem
            {
                Stt = sttNgay++,
                Ngay = ngay,
                SoHoaDon = Convert.ToInt32(nr.SoHoaDon),
                DoanhThuHang = dtHang,
                GiamGia = gg,
                DoanhThuThuan = dtThuan,
                GiaVon = gv,
                LoiNhuanGop = lnGop,
                ChiPhi = cp,
                LoiNhuanThuan = lnThuan
            });
        }

        // 8. Tạo Bảng chỉ tiêu chuẩn kế toán
        var bangChiTieu = new List<ChiTieuBaoCaoItem>
        {
            new ChiTieuBaoCaoItem
            {
                Stt = 1,
                MaSo = "01",
                ChiTieu = "1. Doanh thu bán hàng và cung cấp dịch vụ",
                ThuyetMinh = "Tổng tiền hàng ghi trên các hóa đơn hoàn thành",
                KyNay = tongTienHang,
                TyLe = 100.0,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 2,
                MaSo = "02",
                ChiTieu = "2. Các khoản giảm trừ doanh thu",
                ThuyetMinh = "Chiết khấu, giảm giá bán hàng cho khách",
                KyNay = giamGia,
                TyLe = tongTienHang > 0 ? (double)(giamGia / tongTienHang * 100m) : 0,
                CapDo = 1
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 3,
                MaSo = "10",
                ChiTieu = "3. Doanh thu thuần về bán hàng và cung cấp dịch vụ",
                ThuyetMinh = "Mã 10 = Mã 01 - Mã 02",
                KyNay = doanhThuThuan,
                TyLe = 100.0,
                IsHighlight = true,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 4,
                MaSo = "11",
                ChiTieu = "4. Giá vốn hàng bán (COGS)",
                ThuyetMinh = "Tổng giá vốn các sản phẩm hoa, phụ liệu đã xuất bán",
                KyNay = giaVon,
                TyLe = doanhThuThuan > 0 ? (double)(giaVon / doanhThuThuan * 100m) : 0,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 5,
                MaSo = "20",
                ChiTieu = "5. Lợi nhuận gộp về bán hàng",
                ThuyetMinh = "Mã 20 = Mã 10 - Mã 11 (Tỷ suất LN gộp)",
                KyNay = loiNhuanGop,
                TyLe = tySuatLoiNhuanGop,
                IsHighlight = true,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 6,
                MaSo = "25",
                ChiTieu = "6. Chi phí kinh doanh & vận hành cửa hàng",
                ThuyetMinh = "Tổng chi phí mặt bằng, điện nước, nhân công, vật tư...",
                KyNay = tongChiPhi,
                TyLe = doanhThuThuan > 0 ? (double)(tongChiPhi / doanhThuThuan * 100m) : 0,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 7,
                MaSo = "30",
                ChiTieu = "7. Lợi nhuận thuần từ hoạt động kinh doanh",
                ThuyetMinh = "Mã 30 = Mã 20 - Mã 25",
                KyNay = loiNhuanThuanHDKD,
                TyLe = doanhThuThuan > 0 ? (double)(loiNhuanThuanHDKD / doanhThuThuan * 100m) : 0,
                IsHighlight = true,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 8,
                MaSo = "31",
                ChiTieu = "8. Thu nhập khác",
                ThuyetMinh = "Các khoản thu ngoài bán hàng (thanh lý, hoa hồng, đền bù...)",
                KyNay = thuNhapKhac,
                CapDo = 1
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 9,
                MaSo = "32",
                ChiTieu = "9. Chi phí khác",
                ThuyetMinh = "Các chi phí bất thường ngoài hoạt động chính",
                KyNay = chiPhiKhac,
                CapDo = 1
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 10,
                MaSo = "40",
                ChiTieu = "10. Lợi nhuận khác",
                ThuyetMinh = "Mã 40 = Mã 31 - Mã 32",
                KyNay = loiNhuanKhac,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 11,
                MaSo = "50",
                ChiTieu = "11. Tổng lợi nhuận kế toán trước thuế (Lợi nhuận ròng)",
                ThuyetMinh = "Mã 50 = Mã 30 + Mã 40 (Biên lợi nhuận ròng)",
                KyNay = tongLoiNhuan,
                TyLe = tySuatLoiNhuanThuan,
                IsHighlight = true,
                CapDo = 0
            }
        };

        return new BaoCaoKetQuaKinhDoanhDuLieu
        {
            TuNgay = tu,
            DenNgay = denNgay.Date,
            DoanhThuBanHang = tongTienHang,
            GiamTruDoanhThu = giamGia,
            DoanhThuThuan = doanhThuThuan,
            GiaVonHangBan = giaVon,
            LoiNhuanGop = loiNhuanGop,
            TySuatLoiNhuanGop = tySuatLoiNhuanGop,
            ChiPhiKinhDoanh = tongChiPhi,
            LoiNhuanThuanHDKD = loiNhuanThuanHDKD,
            ThuNhapKhac = thuNhapKhac,
            ChiPhiKhac = chiPhiKhac,
            LoiNhuanKhac = loiNhuanKhac,
            TongLoiNhuan = tongLoiNhuan,
            TySuatLoiNhuanThuan = tySuatLoiNhuanThuan,
            TongSoHoaDon = soHoaDon,
            TongSoPhieuChi = tongSoPhieuChi,
            BangChiTieu = bangChiTieu,
            ChiTietChiPhi = chiTietChiPhi,
            ChiTietTheoNgay = chiTietTheoNgay
        };
    }

    #endregion

    #region GIAO DIỆN 58 – BÁO CÁO VỐN KINH DOANH

    /// <summary>
    /// Tải dữ liệu Báo cáo vốn kinh doanh & Cơ cấu tài sản lưu động tại mốc thời gian.
    /// </summary>
    public async Task<BaoCaoVonKinhDoanhDuLieu> TaiBaoCaoVonKinhDoanhAsync(
        DateTime? mocThoiGian = null,
        CancellationToken token = default)
    {
        var moc = mocThoiGian ?? DateTime.Now;

        await using var conn = await _taoKetNoi();

        // 1. Tiền mặt tại quỹ
        const string sqlTienMat = """
        SELECT COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN pc.so_tien ELSE -pc.so_tien END), 0)
        FROM phieu_chi pc
        WHERE pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi <= @Moc
          AND UPPER(TRIM(COALESCE(pc.phuong_thuc, 'TIEN_MAT'))) = 'TIEN_MAT';
        """;

        decimal tienMat = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sqlTienMat, new { Moc = moc }, cancellationToken: token));

        // 2. Tiền gửi ngân hàng
        const string sqlTienGui = """
        SELECT COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN pc.so_tien ELSE -pc.so_tien END), 0)
        FROM phieu_chi pc
        WHERE pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi <= @Moc
          AND UPPER(TRIM(pc.phuong_thuc)) IN ('CHUYEN_KHOAN', 'NGAN_HANG', 'CHUYEN_KHOAN_NGAN_HANG', 'QR', 'THE', 'VI');
        """;

        decimal tienGui = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sqlTienGui, new { Moc = moc }, cancellationToken: token));

        // 3. Giá trị hàng tồn kho
        const string sqlTonKho = """
        SELECT
            COUNT(DISTINCT sp.id)                                                                     AS SoMatHang,
            COALESCE(SUM(COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0)), 0)                         AS TongSoLuong,
            COALESCE(SUM(COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) * COALESCE(sp.gia_von, 0)), 0) AS TongGiaTri
        FROM san_pham sp
        LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
        WHERE COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) > 0;
        """;

        var tonKhoInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition(sqlTonKho, cancellationToken: token));

        int soMatHangTon = tonKhoInfo != null ? Convert.ToInt32(tonKhoInfo.SoMatHang) : 0;
        decimal tongSoLuongTon = tonKhoInfo != null ? Convert.ToDecimal(tonKhoInfo.TongSoLuong) : 0m;
        decimal giaTriTonKho = tonKhoInfo != null ? Convert.ToDecimal(tonKhoInfo.TongGiaTri) : 0m;

        // Chi tiết tồn kho theo nhóm hàng
        const string sqlTonKhoTheoNhom = """
        SELECT
            COALESCE(nsp.ten_nhom, 'Chưa phân nhóm')                                                  AS TenNhom,
            COUNT(DISTINCT sp.id)                                                                     AS SoMatHang,
            COALESCE(SUM(COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0)), 0)                         AS SoLuongTon,
            COALESCE(SUM(COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) * COALESCE(sp.gia_von, 0)), 0) AS GiaTriTonKho
        FROM san_pham sp
        LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
        LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
        WHERE COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0) > 0
        GROUP BY COALESCE(nsp.ten_nhom, 'Chưa phân nhóm')
        ORDER BY GiaTriTonKho DESC;
        """;

        var nhomTonRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlTonKhoTheoNhom, cancellationToken: token))).AsList();

        var chiTietTonKho = new List<TonKhoTheoNhomItem>();
        int sttNhomTon = 1;
        foreach (var r in nhomTonRows)
        {
            var gt = Convert.ToDecimal(r.GiaTriTonKho);
            chiTietTonKho.Add(new TonKhoTheoNhomItem
            {
                Stt = sttNhomTon++,
                TenNhom = Convert.ToString(r.TenNhom) ?? "",
                SoMatHang = Convert.ToInt32(r.SoMatHang),
                SoLuongTon = Convert.ToDecimal(r.SoLuongTon),
                GiaTriTonKho = gt,
                TyTrong = giaTriTonKho > 0 ? (double)(gt / giaTriTonKho * 100m) : 0
            });
        }

        // 4. Phải thu khách hàng (Công nợ khách hàng)
        const string sqlKh = """
        SELECT 
            kh.id                                        AS DoiTacId,
            kh.ma_khach_hang                             AS MaDoiTac,
            kh.ten_khach_hang                            AS TenDoiTac,
            COALESCE(kh.so_dien_thoai, '')               AS SoDienThoai,
            COALESCE(ls.SoDu, kh.no_can_thu_hien_tai, 0) AS SoDuNo
        FROM khach_hang kh
        LEFT JOIN (
            SELECT khach_hang_id, SUM(ghi_no - ghi_co) AS SoDu
            FROM lich_su_cong_no_khach_hang
            GROUP BY khach_hang_id
        ) ls ON ls.khach_hang_id = kh.id
        WHERE COALESCE(ls.SoDu, kh.no_can_thu_hien_tai, 0) > 0
        ORDER BY SoDuNo DESC;
        """;

        var khRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlKh, cancellationToken: token))).AsList();

        decimal tongPhaiThu = khRows.Sum(r => (decimal)Convert.ToDecimal(r.SoDuNo));
        var topKhachHang = new List<CongNoDoiTacItem>();
        int sttKh = 1;
        foreach (var r in khRows)
        {
            var no = Convert.ToDecimal(r.SoDuNo);
            topKhachHang.Add(new CongNoDoiTacItem
            {
                Stt = sttKh++,
                LoaiDoiTac = "KHACH_HANG",
                DoiTacId = Convert.ToUInt32(r.DoiTacId),
                MaDoiTac = Convert.ToString(r.MaDoiTac) ?? "",
                TenDoiTac = Convert.ToString(r.TenDoiTac) ?? "",
                SoDienThoai = Convert.ToString(r.SoDienThoai) ?? "",
                SoDuNo = no,
                TyTrong = tongPhaiThu > 0 ? (double)(no / tongPhaiThu * 100m) : 0
            });
        }

        // 5. Phải trả nhà cung cấp (Công nợ NCC)
        const string sqlNcc = """
        SELECT 
            ncc.id                                        AS DoiTacId,
            ncc.ma_nha_cung_cap                           AS MaDoiTac,
            ncc.ten_nha_cung_cap                          AS TenDoiTac,
            COALESCE(ncc.so_dien_thoai, '')               AS SoDienThoai,
            COALESCE(ls.SoDu, ncc.no_can_tra_hien_tai, 0) AS SoDuNo
        FROM nha_cung_cap ncc
        LEFT JOIN (
            SELECT nha_cung_cap_id, SUM(ghi_no - ghi_co) AS SoDu
            FROM lich_su_cong_no_nha_cung_cap
            GROUP BY nha_cung_cap_id
        ) ls ON ls.nha_cung_cap_id = ncc.id
        WHERE COALESCE(ls.SoDu, ncc.no_can_tra_hien_tai, 0) > 0
        ORDER BY SoDuNo DESC;
        """;

        var nccRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlNcc, cancellationToken: token))).AsList();

        decimal tongPhaiTra = nccRows.Sum(r => (decimal)Convert.ToDecimal(r.SoDuNo));
        var topNhaCungCap = new List<CongNoDoiTacItem>();
        int sttNcc = 1;
        foreach (var r in nccRows)
        {
            var no = Convert.ToDecimal(r.SoDuNo);
            topNhaCungCap.Add(new CongNoDoiTacItem
            {
                Stt = sttNcc++,
                LoaiDoiTac = "NHA_CUNG_CAP",
                DoiTacId = Convert.ToUInt32(r.DoiTacId),
                MaDoiTac = Convert.ToString(r.MaDoiTac) ?? "",
                TenDoiTac = Convert.ToString(r.TenDoiTac) ?? "",
                SoDienThoai = Convert.ToString(r.SoDienThoai) ?? "",
                SoDuNo = no,
                TyTrong = tongPhaiTra > 0 ? (double)(no / tongPhaiTra * 100m) : 0
            });
        }

        // 6. Tổng tài sản lưu động & Vốn kinh doanh ròng
        decimal tongTien = tienMat + tienGui;
        decimal tongTaiSanLuuDong = tongTien + giaTriTonKho + tongPhaiThu;
        decimal vonKinhDoanhRong = tongTaiSanLuuDong - tongPhaiTra;

        // 7. Bảng chỉ tiêu vốn kinh doanh chuẩn
        var bangChiTieu = new List<ChiTieuBaoCaoItem>
        {
            new ChiTieuBaoCaoItem
            {
                Stt = 1,
                MaSo = "100",
                ChiTieu = "A. TỔNG TÀI SẢN LƯU ĐỘNG (VỐN HOẠT ĐỘNG)",
                ThuyetMinh = "Mã 100 = 110 + 120 + 130",
                KyNay = tongTaiSanLuuDong,
                TyLe = 100.0,
                IsHighlight = true,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 2,
                MaSo = "110",
                ChiTieu = "I. Tiền và các khoản tương đương tiền",
                ThuyetMinh = "Mã 110 = 111 + 112",
                KyNay = tongTien,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(tongTien / tongTaiSanLuuDong * 100m) : 0,
                IsHighlight = true,
                CapDo = 1
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 3,
                MaSo = "111",
                ChiTieu = "1. Tiền mặt tồn quỹ",
                ThuyetMinh = "Số dư tiền mặt tại két",
                KyNay = tienMat,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(tienMat / tongTaiSanLuuDong * 100m) : 0,
                CapDo = 2
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 4,
                MaSo = "112",
                ChiTieu = "2. Tiền gửi ngân hàng & ví điện tử",
                ThuyetMinh = "Số dư tài khoản chuyển khoản ngân hàng",
                KyNay = tienGui,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(tienGui / tongTaiSanLuuDong * 100m) : 0,
                CapDo = 2
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 5,
                MaSo = "120",
                ChiTieu = "II. Giá trị hàng tồn kho",
                ThuyetMinh = $"Giá trị tồn kho tính theo giá vốn ({soMatHangTon} mặt hàng)",
                KyNay = giaTriTonKho,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(giaTriTonKho / tongTaiSanLuuDong * 100m) : 0,
                IsHighlight = true,
                CapDo = 1
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 6,
                MaSo = "130",
                ChiTieu = "III. Các khoản phải thu ngắn hạn",
                ThuyetMinh = $"Công nợ phải thu của khách hàng ({topKhachHang.Count} khách còn nợ)",
                KyNay = tongPhaiThu,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(tongPhaiThu / tongTaiSanLuuDong * 100m) : 0,
                IsHighlight = true,
                CapDo = 1
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 7,
                MaSo = "200",
                ChiTieu = "B. NỢ PHẢI TRẢ NGẮN HẠN",
                ThuyetMinh = $"Công nợ phải trả cho nhà cung cấp ({topNhaCungCap.Count} NCC còn nợ)",
                KyNay = tongPhaiTra,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(tongPhaiTra / tongTaiSanLuuDong * 100m) : 0,
                IsHighlight = true,
                CapDo = 0
            },
            new ChiTieuBaoCaoItem
            {
                Stt = 8,
                MaSo = "300",
                ChiTieu = "C. VỐN KINH DOANH RÒNG (VỐN LƯU ĐỘNG THUẦN)",
                ThuyetMinh = "Mã 300 = Mã 100 - Mã 200 (Tài sản lưu động trừ Nợ phải trả)",
                KyNay = vonKinhDoanhRong,
                TyLe = tongTaiSanLuuDong > 0 ? (double)(vonKinhDoanhRong / tongTaiSanLuuDong * 100m) : 0,
                IsHighlight = true,
                CapDo = 0
            }
        };

        return new BaoCaoVonKinhDoanhDuLieu
        {
            MocThoiGian = moc,
            TienMatTaiQuy = tienMat,
            TienGuiNganHang = tienGui,
            GiaTriHangTonKho = giaTriTonKho,
            TongSoMatHangTon = soMatHangTon,
            TongSoLuongTon = tongSoLuongTon,
            PhaiThuKhachHang = tongPhaiThu,
            SoKhachHangNo = topKhachHang.Count,
            PhaiTraNhaCungCap = tongPhaiTra,
            SoNhaCungCapNo = topNhaCungCap.Count,
            BangChiTieu = bangChiTieu,
            ChiTietTonKhoTheoNhom = chiTietTonKho,
            TopKhachHangNo = topKhachHang,
            TopNhaCungCapNo = topNhaCungCap
        };
    }

    #endregion

    #region XUẤT FILE EXCEL

    private const string MauHeaderBg = "#087F6E";
    private const string DinhDangTien = "#,##0";

    /// <summary>
    /// Xuất Excel Báo cáo kết quả kinh doanh bằng ClosedXML.
    /// </summary>
    public static XLWorkbook TaoWorkbookKetQuaKinhDoanh(BaoCaoKetQuaKinhDoanhDuLieu data)
    {
        var wb = new XLWorkbook();
        var ws1 = wb.Worksheets.Add("Kết quả kinh doanh");

        // Header Title
        ws1.Cell(1, 1).Value = "BÁO CÁO KẾT QUẢ KINH DOANH";
        ws1.Cell(1, 1).Style.Font.Bold = true;
        ws1.Cell(1, 1).Style.Font.FontSize = 16;
        ws1.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#164B40");

        ws1.Cell(2, 1).Value = $"Kỳ báo cáo: Từ {data.TuNgayHienThi} đến {data.DenNgayHienThi}";
        ws1.Cell(2, 1).Style.Font.Italic = true;

        // Table Header
        int r = 4;
        string[] headers = ["STT", "Mã số", "Chỉ tiêu", "Thuyết minh / Công thức", "Kỳ này (VNĐ)", "Tỷ lệ %"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws1.Cell(r, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Table Rows
        foreach (var item in data.BangChiTieu)
        {
            r++;
            ws1.Cell(r, 1).Value = item.Stt;
            ws1.Cell(r, 2).Value = item.MaSo;
            ws1.Cell(r, 3).Value = item.ChiTieu;
            ws1.Cell(r, 4).Value = item.ThuyetMinh;

            var valCell = ws1.Cell(r, 5);
            valCell.Value = item.KyNay;
            valCell.Style.NumberFormat.Format = DinhDangTien;

            var tyLeCell = ws1.Cell(r, 6);
            if (item.TyLe.HasValue)
            {
                tyLeCell.Value = item.TyLe.Value / 100.0;
                tyLeCell.Style.NumberFormat.Format = "0.0%";
            }

            if (item.IsHighlight)
            {
                ws1.Row(r).Style.Font.Bold = true;
                ws1.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F7F5");
            }
        }

        ws1.Columns().AdjustToContents();

        // Sheet 2: Chi tiết chi phí
        if (data.ChiTietChiPhi.Count > 0)
        {
            var ws2 = wb.Worksheets.Add("Chi tiết chi phí");
            ws2.Cell(1, 1).Value = "CHI TIẾT CHI PHÍ KINH DOANH THEO NHÓM";
            ws2.Cell(1, 1).Style.Font.Bold = true;
            ws2.Cell(1, 1).Style.Font.FontSize = 14;

            int r2 = 3;
            string[] h2 = ["STT", "Mã nhóm", "Tên nhóm chi phí", "Số chứng từ", "Số tiền (VNĐ)", "Tỷ trọng %"];
            for (int c = 0; c < h2.Length; c++)
            {
                var cell = ws2.Cell(r2, c + 1);
                cell.Value = h2[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            }

            foreach (var cp in data.ChiTietChiPhi)
            {
                r2++;
                ws2.Cell(r2, 1).Value = cp.Stt;
                ws2.Cell(r2, 2).Value = cp.MaNhom;
                ws2.Cell(r2, 3).Value = cp.TenNhom;
                ws2.Cell(r2, 4).Value = cp.SoChungTu;
                ws2.Cell(r2, 5).Value = cp.SoTien;
                ws2.Cell(r2, 5).Style.NumberFormat.Format = DinhDangTien;
                ws2.Cell(r2, 6).Value = cp.TyLePhanTram / 100.0;
                ws2.Cell(r2, 6).Style.NumberFormat.Format = "0.0%";
            }
            ws2.Columns().AdjustToContents();
        }

        // Sheet 3: Theo ngày
        if (data.ChiTietTheoNgay.Count > 0)
        {
            var ws3 = wb.Worksheets.Add("Theo ngày");
            ws3.Cell(1, 1).Value = "KẾT QUẢ KINH DOANH THEO NGÀY";
            ws3.Cell(1, 1).Style.Font.Bold = true;
            ws3.Cell(1, 1).Style.Font.FontSize = 14;

            int r3 = 3;
            string[] h3 = ["STT", "Ngày", "Số HĐ", "Doanh thu hàng", "Giảm giá", "Doanh thu thuần", "Giá vốn", "Lợi nhuận gộp", "Chi phí", "Lợi nhuận thuần"];
            for (int c = 0; c < h3.Length; c++)
            {
                var cell = ws3.Cell(r3, c + 1);
                cell.Value = h3[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            }

            foreach (var d in data.ChiTietTheoNgay)
            {
                r3++;
                ws3.Cell(r3, 1).Value = d.Stt;
                ws3.Cell(r3, 2).Value = d.NgayHienThi;
                ws3.Cell(r3, 3).Value = d.SoHoaDon;
                ws3.Cell(r3, 4).Value = d.DoanhThuHang;
                ws3.Cell(r3, 4).Style.NumberFormat.Format = DinhDangTien;
                ws3.Cell(r3, 5).Value = d.GiamGia;
                ws3.Cell(r3, 5).Style.NumberFormat.Format = DinhDangTien;
                ws3.Cell(r3, 6).Value = d.DoanhThuThuan;
                ws3.Cell(r3, 6).Style.NumberFormat.Format = DinhDangTien;
                ws3.Cell(r3, 7).Value = d.GiaVon;
                ws3.Cell(r3, 7).Style.NumberFormat.Format = DinhDangTien;
                ws3.Cell(r3, 8).Value = d.LoiNhuanGop;
                ws3.Cell(r3, 8).Style.NumberFormat.Format = DinhDangTien;
                ws3.Cell(r3, 9).Value = d.ChiPhi;
                ws3.Cell(r3, 9).Style.NumberFormat.Format = DinhDangTien;
                ws3.Cell(r3, 10).Value = d.LoiNhuanThuan;
                ws3.Cell(r3, 10).Style.NumberFormat.Format = DinhDangTien;
            }
            ws3.Columns().AdjustToContents();
        }

        return wb;
    }

    /// <summary>
    /// Xuất Excel Báo cáo vốn kinh doanh bằng ClosedXML.
    /// </summary>
    public static XLWorkbook TaoWorkbookVonKinhDoanh(BaoCaoVonKinhDoanhDuLieu data)
    {
        var wb = new XLWorkbook();
        var ws1 = wb.Worksheets.Add("Cơ cấu vốn kinh doanh");

        // Header Title
        ws1.Cell(1, 1).Value = "BÁO CÁO VỐN KINH DOANH VÀ TÀI SẢN LƯU ĐỘNG";
        ws1.Cell(1, 1).Style.Font.Bold = true;
        ws1.Cell(1, 1).Style.Font.FontSize = 16;
        ws1.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#164B40");

        ws1.Cell(2, 1).Value = $"Thời điểm báo cáo: {data.MocThoiGianHienThi}";
        ws1.Cell(2, 1).Style.Font.Italic = true;

        // Table Header
        int r = 4;
        string[] headers = ["STT", "Mã số", "Chỉ tiêu tài sản & nguồn vốn", "Diễn giải / Căn cứ số liệu", "Giá trị (VNĐ)", "Tỷ trọng %"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws1.Cell(r, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        foreach (var item in data.BangChiTieu)
        {
            r++;
            ws1.Cell(r, 1).Value = item.Stt;
            ws1.Cell(r, 2).Value = item.MaSo;
            ws1.Cell(r, 3).Value = item.ChiTieu;
            ws1.Cell(r, 4).Value = item.ThuyetMinh;

            var valCell = ws1.Cell(r, 5);
            valCell.Value = item.KyNay;
            valCell.Style.NumberFormat.Format = DinhDangTien;

            var tyLeCell = ws1.Cell(r, 6);
            if (item.TyLe.HasValue)
            {
                tyLeCell.Value = item.TyLe.Value / 100.0;
                tyLeCell.Style.NumberFormat.Format = "0.0%";
            }

            if (item.IsHighlight)
            {
                ws1.Row(r).Style.Font.Bold = true;
                ws1.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F7F5");
            }
        }
        ws1.Columns().AdjustToContents();

        // Sheet 2: Chi tiết tồn kho
        if (data.ChiTietTonKhoTheoNhom.Count > 0)
        {
            var ws2 = wb.Worksheets.Add("Tồn kho theo nhóm");
            ws2.Cell(1, 1).Value = "GIÁ TRỊ TỒN KHO THEO NHÓM HÀNG";
            ws2.Cell(1, 1).Style.Font.Bold = true;
            ws2.Cell(1, 1).Style.Font.FontSize = 14;

            int r2 = 3;
            string[] h2 = ["STT", "Tên nhóm hàng", "Số mặt hàng", "Số lượng tồn", "Giá trị tồn kho (VNĐ)", "Tỷ trọng %"];
            for (int c = 0; c < h2.Length; c++)
            {
                var cell = ws2.Cell(r2, c + 1);
                cell.Value = h2[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            }

            foreach (var tk in data.ChiTietTonKhoTheoNhom)
            {
                r2++;
                ws2.Cell(r2, 1).Value = tk.Stt;
                ws2.Cell(r2, 2).Value = tk.TenNhom;
                ws2.Cell(r2, 3).Value = tk.SoMatHang;
                ws2.Cell(r2, 4).Value = tk.SoLuongTon;
                ws2.Cell(r2, 5).Value = tk.GiaTriTonKho;
                ws2.Cell(r2, 5).Style.NumberFormat.Format = DinhDangTien;
                ws2.Cell(r2, 6).Value = tk.TyTrong / 100.0;
                ws2.Cell(r2, 6).Style.NumberFormat.Format = "0.0%";
            }
            ws2.Columns().AdjustToContents();
        }

        // Sheet 3: Công nợ phải thu & phải trả
        var ws3 = wb.Worksheets.Add("Chi tiết công nợ");
        ws3.Cell(1, 1).Value = "CHI TIẾT CÔNG NỢ ĐỐI TÁC";
        ws3.Cell(1, 1).Style.Font.Bold = true;
        ws3.Cell(1, 1).Style.Font.FontSize = 14;

        int r3 = 3;
        ws3.Cell(r3, 1).Value = "I. CÔNG NỢ PHẢI THU KHÁCH HÀNG";
        ws3.Cell(r3, 1).Style.Font.Bold = true;
        r3++;

        string[] h3 = ["STT", "Mã khách hàng", "Tên khách hàng", "Số điện thoại", "Số dư nợ (VNĐ)", "Tỷ trọng %"];
        for (int c = 0; c < h3.Length; c++)
        {
            var cell = ws3.Cell(r3, c + 1);
            cell.Value = h3[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
        }

        foreach (var kh in data.TopKhachHangNo)
        {
            r3++;
            ws3.Cell(r3, 1).Value = kh.Stt;
            ws3.Cell(r3, 2).Value = kh.MaDoiTac;
            ws3.Cell(r3, 3).Value = kh.TenDoiTac;
            ws3.Cell(r3, 4).Value = kh.SoDienThoai;
            ws3.Cell(r3, 5).Value = kh.SoDuNo;
            ws3.Cell(r3, 5).Style.NumberFormat.Format = DinhDangTien;
            ws3.Cell(r3, 6).Value = kh.TyTrong / 100.0;
            ws3.Cell(r3, 6).Style.NumberFormat.Format = "0.0%";
        }

        r3 += 2;
        ws3.Cell(r3, 1).Value = "II. CÔNG NỢ PHẢI TRẢ NHÀ CUNG CẤP";
        ws3.Cell(r3, 1).Style.Font.Bold = true;
        r3++;

        string[] h4 = ["STT", "Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Số dư nợ (VNĐ)", "Tỷ trọng %"];
        for (int c = 0; c < h4.Length; c++)
        {
            var cell = ws3.Cell(r3, c + 1);
            cell.Value = h4[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E5B94");
        }

        foreach (var ncc in data.TopNhaCungCapNo)
        {
            r3++;
            ws3.Cell(r3, 1).Value = ncc.Stt;
            ws3.Cell(r3, 2).Value = ncc.MaDoiTac;
            ws3.Cell(r3, 3).Value = ncc.TenDoiTac;
            ws3.Cell(r3, 4).Value = ncc.SoDienThoai;
            ws3.Cell(r3, 5).Value = ncc.SoDuNo;
            ws3.Cell(r3, 5).Style.NumberFormat.Format = DinhDangTien;
            ws3.Cell(r3, 6).Value = ncc.TyTrong / 100.0;
            ws3.Cell(r3, 6).Style.NumberFormat.Format = "0.0%";
        }

        ws3.Columns().AdjustToContents();

        return wb;
    }

    #endregion
}
