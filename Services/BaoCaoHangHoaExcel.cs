using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>Xuất báo cáo hàng hóa ra file .xlsx (tổng hợp + bảng chi tiết).</summary>
public static class BaoCaoHangHoaExcel
{
    public static XLWorkbook TaoWorkbook(BaoCaoHangHoaDuLieu duLieu)
    {
        var wb = new XLWorkbook();

        // ---------- Sheet 1: tổng hợp ----------
        var ws = wb.Worksheets.Add("Tổng hợp");
        ws.Cell(1, 1).Value = "Báo cáo hàng hóa";
        ws.Cell(2, 1).Value = $"Từ {duLieu.TuNgay:dd/MM/yyyy} đến {duLieu.DenNgay:dd/MM/yyyy}";

        var th = duLieu.TongHop;
        ws.Cell(4, 1).Value = "Số mã hàng"; ws.Cell(4, 2).Value = th.SoMaHang;
        ws.Cell(5, 1).Value = "Số lượng bán"; ws.Cell(5, 2).Value = th.SoLuongBan;
        ws.Cell(6, 1).Value = "Doanh thu"; ws.Cell(6, 2).Value = th.DoanhThu;
        ws.Cell(7, 1).Value = "Giá vốn"; ws.Cell(7, 2).Value = th.GiaVon;
        ws.Cell(8, 1).Value = "Lợi nhuận"; ws.Cell(8, 2).Value = th.LoiNhuan;
        ws.Cell(9, 1).Value = "Tỉ lệ lợi nhuận (%)"; ws.Cell(9, 2).Value = th.TyLeLoiNhuan;
        ws.Column(2).Style.NumberFormat.Format = "#,##0.###";

        ws.Cell(11, 1).Value = "Top sản phẩm theo doanh thu";
        GhiBangTop(ws, 12, duLieu.TopDoanhThu);

        int dongNhom = 12 + duLieu.TopDoanhThu.Count + 2;
        ws.Cell(dongNhom, 1).Value = "Doanh thu theo nhóm hàng";
        ws.Cell(dongNhom + 1, 1).Value = "Nhóm hàng";
        ws.Cell(dongNhom + 1, 2).Value = "Doanh thu";
        ws.Cell(dongNhom + 1, 3).Value = "Tỉ trọng (%)";
        int i = dongNhom + 2;
        foreach (var n in duLieu.NhomHang)
        {
            ws.Cell(i, 1).Value = n.TenNhom;
            ws.Cell(i, 2).Value = n.DoanhThu;
            ws.Cell(i, 3).Value = n.TyLePhanTram;
            i++;
        }

        // ---------- Sheet 2: bảng chi tiết ----------
        var ct = wb.Worksheets.Add("Chi tiết");
        ct.Cell(1, 1).Value = "Báo cáo hàng hóa — chi tiết theo mã hàng";
        ct.Cell(2, 1).Value = $"Từ {duLieu.TuNgay:dd/MM/yyyy} đến {duLieu.DenNgay:dd/MM/yyyy}";

        string[] cot = ["STT", "Mã hàng", "Tên hàng", "Nhóm hàng", "SL bán", "Doanh thu", "Giá vốn", "Lợi nhuận"];
        for (int c = 0; c < cot.Length; c++)
        {
            ct.Cell(4, c + 1).Value = cot[c];
            ct.Cell(4, c + 1).Style.Font.Bold = true;
        }

        int dong = 5;
        for (int r = 0; r < duLieu.ChiTiet.Count; r++)
        {
            var row = duLieu.ChiTiet[r];
            ct.Cell(dong, 1).Value = r + 1;
            ct.Cell(dong, 2).Value = row.MaHang;
            ct.Cell(dong, 3).Value = row.TenHang;
            ct.Cell(dong, 4).Value = row.NhomHang;
            ct.Cell(dong, 5).Value = row.SoLuongBan;
            ct.Cell(dong, 6).Value = row.DoanhThu;
            ct.Cell(dong, 7).Value = row.GiaVon;
            ct.Cell(dong, 8).Value = row.LoiNhuan;
            dong++;
        }

        // Dòng tổng cộng khớp KPI ở trên.
        ct.Cell(dong, 3).Value = "Tổng cộng";
        ct.Cell(dong, 3).Style.Font.Bold = true;
        ct.Cell(dong, 5).Value = th.SoLuongBan;
        ct.Cell(dong, 6).Value = th.DoanhThu;
        ct.Cell(dong, 7).Value = th.GiaVon;
        ct.Cell(dong, 8).Value = th.LoiNhuan;
        for (int c = 5; c <= 8; c++) ct.Cell(dong, c).Style.Font.Bold = true;

        ct.Columns(5, 8).Style.NumberFormat.Format = "#,##0.###";
        ct.SheetView.FreezeRows(4);

        return wb;
    }

    private static void GhiBangTop(IXLWorksheet ws, int dongDau, List<BaoCaoHangHoaTop> top)
    {
        ws.Cell(dongDau, 1).Value = "STT";
        ws.Cell(dongDau, 2).Value = "Mã hàng";
        ws.Cell(dongDau, 3).Value = "Tên hàng";
        ws.Cell(dongDau, 4).Value = "SL bán";
        ws.Cell(dongDau, 5).Value = "Doanh thu";
        for (int c = 1; c <= 5; c++) ws.Cell(dongDau, c).Style.Font.Bold = true;

        for (int r = 0; r < top.Count; r++)
        {
            int dong = dongDau + 1 + r;
            ws.Cell(dong, 1).Value = top[r].Stt;
            ws.Cell(dong, 2).Value = top[r].MaHang;
            ws.Cell(dong, 3).Value = top[r].TenHang;
            ws.Cell(dong, 4).Value = top[r].SoLuongBan;
            ws.Cell(dong, 5).Value = top[r].DoanhThu;
        }
    }
}
