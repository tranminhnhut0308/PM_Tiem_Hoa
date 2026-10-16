using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng file Excel cho báo cáo nhà cung cấp: Tổng hợp, Top nhà cung cấp (biểu đồ)
/// và bảng chi tiết đầy đủ theo bộ lọc đang áp dụng.
/// <para>Tách riêng khỏi ViewModel để kiểm thử định dạng file mà không cần hộp thoại lưu.</para>
/// </summary>
public static class BaoCaoNhaCungCapExcel
{
    private const string MauTieuDe = "#164B40";
    private const string DinhDangTien = "#,##0";

    public static XLWorkbook TaoWorkbook(BaoCaoNhaCungCapDuLieu duLieu)
    {
        var wb = new XLWorkbook();

        GhiSheetTongHop(wb, duLieu);
        GhiSheetTop(wb, duLieu);
        GhiSheetChiTiet(wb, duLieu);

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

    private static readonly string[] TieuDeChiTiet =
    [
        "STT", "Mã nhà cung cấp", "Tên nhà cung cấp", "Số điện thoại", "Số phiếu nhập",
        "Tổng tiền nhập", "Tiền trả hàng", "Giá trị nhập hàng", "Đã thanh toán", "Công nợ"
    ];

    private static void GhiDongChiTiet(IXLWorksheet sheet, int dong, BaoCaoNhaCungCapDong r)
    {
        GhiDongSo(sheet, dong, 1, r.Stt);
        GhiDongSo(sheet, dong, 2, r.MaNhaCungCap);
        GhiDongSo(sheet, dong, 3, r.TenNhaCungCap);
        GhiDongSo(sheet, dong, 4, r.SoDienThoai);
        GhiDongSo(sheet, dong, 5, r.SoPhieuNhap);
        GhiDongSo(sheet, dong, 6, r.TongTienNhap, true);
        GhiDongSo(sheet, dong, 7, r.TienTraHang, true);
        GhiDongSo(sheet, dong, 8, r.GiaTriNhapHang, true);
        GhiDongSo(sheet, dong, 9, r.DaThanhToan, true);
        GhiDongSo(sheet, dong, 10, r.CongNo, true);
    }

    // ============================================================
    // 1. TỔNG HỢP
    // ============================================================

    private static void GhiSheetTongHop(XLWorkbook wb, BaoCaoNhaCungCapDuLieu d)
    {
        var ws = wb.Worksheets.Add("Tổng hợp");
        var t = d.TongHop;

        ws.Cell(1, 1).Value = "Báo cáo nhà cung cấp";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Từ {t.TuNgay:dd/MM/yyyy} đến {t.DenNgay:dd/MM/yyyy}";

        var dong = 4;
        GhiDongTieuDeNhom(ws, dong, "CHỈ SỐ");
        dong += 1;

        GhiDongSo(ws, dong++, 1, "Tổng nhà cung cấp"); GhiDongSo(ws, dong - 1, 2, t.TongNhaCungCap);
        GhiDongSo(ws, dong++, 1, "Số phiếu nhập"); GhiDongSo(ws, dong - 1, 2, t.SoPhieuNhap);
        GhiDongSo(ws, dong++, 1, "Tổng tiền nhập"); GhiDongSo(ws, dong - 1, 2, t.TongTienNhap, true);
        GhiDongSo(ws, dong++, 1, "Tiền trả hàng"); GhiDongSo(ws, dong - 1, 2, t.TienTraHang, true);
        GhiDongSo(ws, dong++, 1, "Giá trị nhập hàng (đã trừ trả hàng)");
        GhiDongSo(ws, dong - 1, 2, t.GiaTriNhapHang, true);
        GhiDongSo(ws, dong++, 1, "Đã thanh toán"); GhiDongSo(ws, dong - 1, 2, t.DaThanhToan, true);
        GhiDongSo(ws, dong++, 1, "Công nợ"); GhiDongSo(ws, dong - 1, 2, t.CongNo, true);

        ws.Column(1).Width = 38;
        ws.Column(2).Width = 20;
        ws.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 2. TOP NHÀ CUNG CẤP (BIỂU ĐỒ)
    // ============================================================

    private static void GhiSheetTop(XLWorkbook wb, BaoCaoNhaCungCapDuLieu d)
    {
        var ws = wb.Worksheets.Add("Top nhà cung cấp");
        GhiTieuDeCot(ws, TieuDeChiTiet);

        var dong = 2;
        foreach (var t in d.Top)
        {
            GhiDongSo(ws, dong, 1, t.Stt);
            GhiDongSo(ws, dong, 2, t.MaNhaCungCap);
            GhiDongSo(ws, dong, 3, t.TenNhaCungCap);
            GhiDongSo(ws, dong, 4, t.SoDienThoai);
            GhiDongSo(ws, dong, 5, t.SoPhieuNhap);
            GhiDongSo(ws, dong, 6, t.TongTienNhap, true);
            GhiDongSo(ws, dong, 7, t.TienTraHang, true);
            GhiDongSo(ws, dong, 8, t.GiaTriNhapHang, true);
            GhiDongSo(ws, dong, 9, t.DaThanhToan, true);
            GhiDongSo(ws, dong, 10, t.CongNo, true);
            dong++;
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 30;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 12;
        for (var c = 6; c <= 10; c++) ws.Column(c).Width = 16;
        ws.Range(2, 5, Math.Max(2, dong - 1), 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ============================================================
    // 3. CHI TIẾT (TOÀN BỘ THEO BỘ LỌC)
    // ============================================================

    private static void GhiSheetChiTiet(XLWorkbook wb, BaoCaoNhaCungCapDuLieu d)
    {
        var ws = wb.Worksheets.Add("Chi tiết");
        GhiTieuDeCot(ws, TieuDeChiTiet);
        ws.SheetView.FreezeRows(1);

        var dong = 2;
        foreach (var r in d.DongTrang)
        {
            GhiDongChiTiet(ws, dong, r);
            dong++;
        }

        var th = d.TongHop;
        GhiDongSo(ws, dong, 3, "Tổng cộng");
        ws.Cell(dong, 3).Style.Font.Bold = true;
        GhiDongSo(ws, dong, 5, th.SoPhieuNhap);
        GhiDongSo(ws, dong, 6, th.TongTienNhap, true);
        GhiDongSo(ws, dong, 7, th.TienTraHang, true);
        GhiDongSo(ws, dong, 8, th.GiaTriNhapHang, true);
        GhiDongSo(ws, dong, 9, th.DaThanhToan, true);
        GhiDongSo(ws, dong, 10, th.CongNo, true);
        for (var c = 3; c <= 10; c++) ws.Cell(dong, c).Style.Font.Bold = true;

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 30;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 12;
        for (var c = 6; c <= 10; c++) ws.Column(c).Width = 16;
        ws.Range(2, 5, dong, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }
}
