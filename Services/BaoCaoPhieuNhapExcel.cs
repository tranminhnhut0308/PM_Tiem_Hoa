using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng file Excel cho "Báo cáo phiếu nhập hàng": liệt kê toàn bộ phiếu nhập
/// khớp bộ lọc/tìm kiếm hiện tại (không phụ thuộc trang đang xem).
/// </summary>
public static class BaoCaoPhieuNhapExcel
{
    private const string MauTieuDe = "#164B40";
    private const string DinhDangTien = "#,##0";
    private const string DinhDangNgayGio = "dd/MM/yyyy HH:mm";

    public static XLWorkbook TaoWorkbook(IReadOnlyList<PhieuNhapHang> items, DateTime thoiDiemXuat)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Phiếu nhập hàng");

        ws.Cell(1, 1).Value = "Báo cáo phiếu nhập hàng";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Xuất lúc {thoiDiemXuat:dd/MM/yyyy HH:mm} — {items.Count} phiếu";

        string[] tieuDe =
        [
            "STT", "Mã phiếu nhập", "Thời gian", "Mã NCC", "Nhà cung cấp",
            "Phương thức", "Nguồn quỹ", "Trạng thái",
            "Tiền hàng", "Chi phí khác", "Tổng tiền nhập", "Đã thanh toán", "Còn nợ", "Ghi chú"
        ];

        const int dongTieuDe = 4;
        for (var c = 0; c < tieuDe.Length; c++)
        {
            var o = ws.Cell(dongTieuDe, c + 1);
            o.Value = tieuDe[c];
            o.Style.Font.Bold = true;
            o.Style.Font.FontColor = XLColor.White;
            o.Style.Fill.BackgroundColor = XLColor.FromHtml(MauTieuDe);
            o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        ws.Row(dongTieuDe).Height = 22;

        decimal tongTienHang = 0, tongChiPhi = 0, tongTienNhap = 0, tongDaTra = 0, tongConNo = 0;

        var dong = dongTieuDe + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var p = items[i];

            var oThoiGian = ws.Cell(dong, 3);
            oThoiGian.Value = p.ThoiGianNhap;
            oThoiGian.Style.NumberFormat.Format = DinhDangNgayGio;

            ws.Cell(dong, 1).Value = i + 1;
            ws.Cell(dong, 2).Value = p.MaPhieuNhap;
            ws.Cell(dong, 4).Value = p.MaNhaCungCap ?? "";
            ws.Cell(dong, 5).Value = p.TenNhaCungCap ?? "";
            ws.Cell(dong, 6).Value = p.PhuongThucThanhToanHienThi;
            ws.Cell(dong, 7).Value = p.NguonQuyHienThi;
            ws.Cell(dong, 8).Value = p.TrangThaiHienThi;

            ws.Cell(dong, 9).Value = p.TongTienHang;
            ws.Cell(dong, 10).Value = p.ChiPhiKhac;
            ws.Cell(dong, 11).Value = p.TongTienNhap;
            ws.Cell(dong, 12).Value = p.DaThanhToan;
            ws.Cell(dong, 13).Value = p.ConNo;
            ws.Cell(dong, 14).Value = p.GhiChu ?? "";

            for (var c = 9; c <= 13; c++)
                ws.Cell(dong, c).Style.NumberFormat.Format = DinhDangTien;

            tongTienHang += p.TongTienHang;
            tongChiPhi += p.ChiPhiKhac;
            tongTienNhap += p.TongTienNhap;
            tongDaTra += p.DaThanhToan;
            tongConNo += p.ConNo;

            dong++;
        }

        if (items.Count > 0)
        {
            ws.Cell(dong, 5).Value = "Tổng cộng";
            ws.Cell(dong, 9).Value = tongTienHang;
            ws.Cell(dong, 10).Value = tongChiPhi;
            ws.Cell(dong, 11).Value = tongTienNhap;
            ws.Cell(dong, 12).Value = tongDaTra;
            ws.Cell(dong, 13).Value = tongConNo;
            for (var c = 5; c <= 13; c++)
                ws.Cell(dong, c).Style.Font.Bold = true;
            for (var c = 9; c <= 13; c++)
                ws.Cell(dong, c).Style.NumberFormat.Format = DinhDangTien;
        }

        ws.SheetView.FreezeRows(dongTieuDe);

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 30;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 16;
        ws.Column(8).Width = 14;
        for (var c = 9; c <= 13; c++) ws.Column(c).Width = 16;
        ws.Column(14).Width = 30;

        return wb;
    }
}
