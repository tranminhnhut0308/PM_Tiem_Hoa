using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng file Excel cho báo cáo bán hàng đầy đủ các mục:
/// Tổng hợp, Doanh thu theo ngày, Chi tiết hóa đơn, Top sản phẩm, Top khách hàng,
/// Doanh thu theo nhóm hàng, Phương thức thanh toán.
/// <para>
/// Tách riêng khỏi ViewModel để kiểm thử định dạng file mà không cần mở hộp thoại lưu.
/// </para>
/// </summary>
public static class BaoCaoBanHangExcel
{
    private const string MauTieuDe = "#164B40";
    private const string DinhDangTien = "#,##0";
    private const string DinhDangNgayGio = "dd/MM/yyyy HH:mm";

    public static XLWorkbook TaoWorkbook(BaoCaoBanHangDuLieu duLieu)
    {
        var wb = new XLWorkbook();

        GhiSheetTongHop(wb, duLieu);
        GhiSheetTheoNgay(wb, duLieu);
        GhiSheetChiTietHoaDon(wb, duLieu);
        GhiSheetTopSanPham(wb, duLieu);
        GhiSheetTopKhachHang(wb, duLieu);
        GhiSheetNhomHang(wb, duLieu);
        GhiSheetPhuongThuc(wb, duLieu);

        return wb;
    }

    // ============================================================
    // TIỆN ÍCH CHUNG
    // ============================================================

    private static void GhiTieuDeCot(IXLWorksheet sheet, params string[] tieuDe)
    {
        for (var c = 0; c < tieuDe.Length; c++)
        {
            var o = sheet.Cell(1, c + 1);
            o.Value = tieuDe[c];
            o.Style.Font.Bold = true;
            o.Style.Font.FontColor = XLColor.White;
            o.Style.Fill.BackgroundColor = XLColor.FromHtml(MauTieuDe);
            o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        sheet.Row(1).Height = 22;
        sheet.SheetView.FreezeRows(1);
    }

    private static void GhiDongSo(IXLWorksheet sheet, int dong, int cot, object giaTri, bool laTien = false)
    {
        var o = sheet.Cell(dong, cot);
        o.Value = XLCellValue.FromObject(giaTri);
        if (laTien)
            o.Style.NumberFormat.Format = DinhDangTien;
    }

    private static void GhiDongTieuDeNhom(IXLWorksheet sheet, int dong, string tieuDe)
    {
        var o = sheet.Cell(dong, 1);
        o.Value = tieuDe;
        o.Style.Font.Bold = true;
        o.Style.Font.FontColor = XLColor.FromHtml(MauTieuDe);
        o.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF6F3");
    }

    // ============================================================
    // 1. TỔNG HỢP
    // ============================================================

    private static void GhiSheetTongHop(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Tổng hợp");
        var t = d.TongHop;

        ws.Cell(1, 1).Value = "Báo cáo bán hàng";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Từ {d.TuNgay:dd/MM/yyyy} đến {d.DenNgay:dd/MM/yyyy}";

        var dong = 4;
        GhiDongTieuDeNhom(ws, dong, "CHỈ SỐ");
        dong += 1;

        GhiDongSo(ws, dong++, 1, "Số hóa đơn"); GhiDongSo(ws, dong - 1, 2, t.SoHoaDon);
        GhiDongSo(ws, dong++, 1, "Tổng tiền hàng"); GhiDongSo(ws, dong - 1, 2, t.TongTienHang, true);
        GhiDongSo(ws, dong++, 1, "Giảm giá"); GhiDongSo(ws, dong - 1, 2, t.GiamGia, true);
        GhiDongSo(ws, dong++, 1, "Thu khác"); GhiDongSo(ws, dong - 1, 2, t.ThuKhac, true);
        GhiDongSo(ws, dong++, 1, "Doanh thu (tiền hàng − giảm giá + thu khác)");
        GhiDongSo(ws, dong - 1, 2, t.DoanhThu, true);
        GhiDongSo(ws, dong++, 1, "Đã thu"); GhiDongSo(ws, dong - 1, 2, t.DaThu, true);
        GhiDongSo(ws, dong++, 1, "Công nợ"); GhiDongSo(ws, dong - 1, 2, t.CongNo, true);
        GhiDongSo(ws, dong++, 1, "Giá trị đơn trung bình");
        GhiDongSo(ws, dong - 1, 2, t.GiaTriDonTrungBinh, true);

        ws.Column(1).Width = 44;
        ws.Column(2).Width = 20;
        ws.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 2. DOANH THU THEO NGÀY
    // ============================================================

    private static void GhiSheetTheoNgay(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Theo ngày");
        GhiTieuDeCot(ws, "Ngày", "Số hóa đơn", "Tiền hàng", "Giảm giá", "Thu khác",
            "Doanh thu", "Đã thu", "Công nợ");

        var dong = 2;
        foreach (var r in d.BangDuLieu)
        {
            GhiDongSo(ws, dong, 1, r.ThoiGian.ToString("dd/MM/yyyy"));
            GhiDongSo(ws, dong, 2, r.SoHoaDon);
            GhiDongSo(ws, dong, 3, r.TongTienHang, true);
            GhiDongSo(ws, dong, 4, r.GiamGia, true);
            GhiDongSo(ws, dong, 5, r.ThuKhac, true);
            GhiDongSo(ws, dong, 6, r.DoanhThu, true);
            GhiDongSo(ws, dong, 7, r.DaThu, true);
            GhiDongSo(ws, dong, 8, r.CongNo, true);
            dong++;
        }

        // Dòng tổng cộng.
        ws.Cell(dong, 1).Value = "Tổng cộng";
        ws.Cell(dong, 2).Value = d.TongHop.SoHoaDon;
        ws.Cell(dong, 3).Value = d.TongHop.TongTienHang;
        ws.Cell(dong, 4).Value = d.TongHop.GiamGia;
        ws.Cell(dong, 5).Value = d.TongHop.ThuKhac;
        ws.Cell(dong, 6).Value = d.TongHop.DoanhThu;
        ws.Cell(dong, 7).Value = d.TongHop.DaThu;
        ws.Cell(dong, 8).Value = d.TongHop.CongNo;
        ws.Range(dong, 1, dong, 8).Style.Font.Bold = true;
        ws.Range(dong, 3, dong, 8).Style.NumberFormat.Format = DinhDangTien;

        ws.Column(1).Width = 14;
        for (var c = 2; c <= 8; c++) ws.Column(c).Width = 16;
        ws.Range(2, 2, dong, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 3. CHI TIẾT HÓA ĐƠN (ngày đang xem)
    // ============================================================

    private static void GhiSheetChiTietHoaDon(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Chi tiết hóa đơn");
        GhiTieuDeCot(ws, "Mã hóa đơn", "Thời gian", "Khách hàng", "Tiền hàng",
            "Giảm giá", "Thu khác", "Doanh thu", "Đã thu", "Còn nợ");

        var dong = 2;
        foreach (var r in d.ChiTietHoaDon)
        {
            var o = ws.Cell(dong, 2);
            o.Value = r.ThoiGianBan;
            o.Style.NumberFormat.Format = DinhDangNgayGio;

            GhiDongSo(ws, dong, 1, r.MaHoaDon);
            GhiDongSo(ws, dong, 3, r.KhachHang);
            GhiDongSo(ws, dong, 4, r.TongTienHang, true);
            GhiDongSo(ws, dong, 5, r.GiamGia, true);
            GhiDongSo(ws, dong, 6, r.ThuKhac, true);
            GhiDongSo(ws, dong, 7, r.DoanhThu, true);
            GhiDongSo(ws, dong, 8, r.DaThu, true);
            GhiDongSo(ws, dong, 9, r.CongNo, true);
            dong++;
        }

        if (d.ChiTietHoaDon.Count == 0)
            ws.Cell(2, 1).Value = "Chưa chọn ngày nào để xem chi tiết hóa đơn";

        ws.Column(1).Width = 16;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 26;
        for (var c = 4; c <= 9; c++) ws.Column(c).Width = 15;
        ws.Range(2, 4, Math.Max(2, dong - 1), 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 4. TOP SẢN PHẨM
    // ============================================================

    private static void GhiSheetTopSanPham(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Top sản phẩm");
        GhiTieuDeCot(ws, "STT", "Sản phẩm", "Số lượng bán", "Doanh thu");

        var dong = 2;
        foreach (var r in d.TopSanPham)
        {
            GhiDongSo(ws, dong, 1, r.Stt);
            GhiDongSo(ws, dong, 2, r.SanPham);
            GhiDongSo(ws, dong, 3, r.SoLuongBan);
            GhiDongSo(ws, dong, 4, r.DoanhThu, true);
            dong++;
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 40;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 18;
        ws.Range(2, 3, Math.Max(2, dong - 1), 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 5. TOP KHÁCH HÀNG
    // ============================================================

    private static void GhiSheetTopKhachHang(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Top khách hàng");
        GhiTieuDeCot(ws, "STT", "Khách hàng", "Số hóa đơn", "Tổng mua", "Đã thanh toán", "Công nợ");

        var dong = 2;
        foreach (var r in d.TopKhachHang)
        {
            GhiDongSo(ws, dong, 1, r.Stt);
            GhiDongSo(ws, dong, 2, r.KhachHang);
            GhiDongSo(ws, dong, 3, r.SoHoaDon);
            GhiDongSo(ws, dong, 4, r.TongMua, true);
            GhiDongSo(ws, dong, 5, r.DaThanhToan, true);
            GhiDongSo(ws, dong, 6, r.CongNo, true);
            dong++;
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 36;
        for (var c = 3; c <= 6; c++) ws.Column(c).Width = 16;
        ws.Range(2, 3, Math.Max(2, dong - 1), 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 6. DOANH THU THEO NHÓM HÀNG
    // ============================================================

    private static void GhiSheetNhomHang(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Nhóm hàng");
        GhiTieuDeCot(ws, "STT", "Nhóm hàng", "Số lượng bán", "Doanh thu", "Tỉ lệ (%)");

        var dong = 2;
        foreach (var r in d.NhomHang)
        {
            GhiDongSo(ws, dong, 1, r.Stt);
            GhiDongSo(ws, dong, 2, r.NhomHang);
            GhiDongSo(ws, dong, 3, r.SoLuongBan);
            GhiDongSo(ws, dong, 4, r.DoanhThu, true);
            GhiDongSo(ws, dong, 5, Math.Round(r.TyLePhanTram, 1));
            dong++;
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 30;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 12;
        ws.Range(2, 3, Math.Max(2, dong - 1), 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 7. PHƯƠNG THỨC THANH TOÁN
    // ============================================================

    private static void GhiSheetPhuongThuc(XLWorkbook wb, BaoCaoBanHangDuLieu d)
    {
        var ws = wb.Worksheets.Add("Phương thức thanh toán");
        GhiTieuDeCot(ws, "Phương thức", "Số giao dịch", "Số tiền", "Tỉ lệ (%)");

        var dong = 2;
        foreach (var r in d.PhuongThucThanhToan)
        {
            GhiDongSo(ws, dong, 1, r.PhuongThucHienThi);
            GhiDongSo(ws, dong, 2, r.SoGiaoDich);
            GhiDongSo(ws, dong, 3, r.SoTien, true);
            GhiDongSo(ws, dong, 4, Math.Round(r.TyLePhanTram, 1));
            dong++;
        }

        ws.Cell(dong, 1).Value = "Nguồn: phiếu thu (PT) trong sổ quỹ.";
        ws.Cell(dong, 1).Style.Font.Italic = true;

        ws.Column(1).Width = 26;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 12;
        ws.Range(2, 2, Math.Max(2, dong - 1), 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }
}
