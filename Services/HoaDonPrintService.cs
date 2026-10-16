using System.Globalization;
using System.Text;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng bản in hóa đơn bán hàng dạng HTML rồi mở bằng trình duyệt mặc định để
/// người dùng in lại (Ctrl+P). Cách này không phụ thuộc driver máy in và chạy
/// được trên cả Windows lẫn Linux, phù hợp để "xuất lại" hóa đơn cũ.
/// </summary>
public static class HoaDonPrintService
{
    /// <summary>Ghi hóa đơn ra file HTML tạm và trả về đường dẫn file.</summary>
    public static string LuuFileTam(HoaDonBanHangReceipt receipt)
    {
        var ten = MaAnToan(receipt.MaHoaDon);
        var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "HoaDon");
        Directory.CreateDirectory(thuMuc);

        var duongDan = Path.Combine(thuMuc, $"hoadon-{ten}.html");

        File.WriteAllText(duongDan, TaoHtml(receipt), new UTF8Encoding(false));

        return duongDan;
    }

    /// <summary>Dựng nội dung HTML hoàn chỉnh của hóa đơn.</summary>
    public static string TaoHtml(HoaDonBanHangReceipt receipt)
    {
        var tiem = receipt.ThongTinTiem;
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>").Append(Enc($"Hóa đơn {receipt.MaHoaDon}")).Append("</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#263B35;margin:0;padding:24px;background:#fff;}");
        sb.Append(".wrap{max-width:480px;margin:0 auto;}");
        sb.Append(".center{text-align:center;}");
        sb.Append("h1{font-size:20px;margin:8px 0 2px;color:#163F38;}");
        sb.Append(".muted{color:#61736E;font-size:12px;}");
        sb.Append("table{width:100%;border-collapse:collapse;margin-top:12px;font-size:13px;}");
        sb.Append("th{text-align:left;border-bottom:1px solid #D8E5E0;padding:6px 4px;color:#163F38;}");
        sb.Append("td{padding:6px 4px;border-bottom:1px solid #EEF3F1;}");
        sb.Append(".r{text-align:right;}");
        sb.Append(".sum{margin-top:12px;font-size:13px;}");
        sb.Append(".sum .row{display:flex;justify-content:space-between;padding:3px 0;}");
        sb.Append(".total{font-size:16px;font-weight:bold;color:#087F6E;}");
        sb.Append(".debt{color:#D64545;font-weight:bold;}");
        sb.Append(".note{margin-top:14px;padding:8px;background:#FFF9ED;border:1px solid #F0E0BB;border-radius:6px;font-size:12px;}");
        sb.Append(".thanks{margin-top:18px;text-align:center;font-style:italic;color:#61736E;}");
        sb.Append(".hr{border:0;border-top:1px dashed #C9D8D3;margin:14px 0;}");
        sb.Append("@media print{body{padding:0;} .noprint{display:none;}}");
        sb.Append("</style></head><body><div class=\"wrap\">");

        // Đầu hóa đơn: thông tin tiệm.
        sb.Append("<div class=\"center\">");
        if (!string.IsNullOrWhiteSpace(tiem.TenTiem))
            sb.Append("<div style=\"font-size:18px;font-weight:bold;color:#163F38;\">")
              .Append(Enc(tiem.TenTiem)).Append("</div>");

        foreach (var dong in new[]
                 {
                     tiem.DiaChi,
                     tiem.SoDienThoaiHienThi,
                     tiem.Email,
                     tiem.WebsiteHienThi,
                     tiem.MaSoThueHienThi
                 })
        {
            if (!string.IsNullOrWhiteSpace(dong))
                sb.Append("<div class=\"muted\">").Append(Enc(dong)).Append("</div>");
        }

        sb.Append("</div><hr class=\"hr\">");

        sb.Append("<h1 class=\"center\">HÓA ĐƠN BÁN HÀNG</h1>");
        sb.Append("<div class=\"center muted\">Mã hóa đơn: ").Append(Enc(receipt.MaHoaDon)).Append("</div>");
        sb.Append("<div class=\"center muted\">Ngày bán: ")
          .Append(Enc(receipt.ThoiGianBan.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)))
          .Append("</div>");

        sb.Append("<table>");
        sb.Append("<tr><td style=\"border:0;padding-left:0;\"><b>Khách hàng:</b></td><td style=\"border:0;\">")
          .Append(Enc(receipt.TenKhachHang)).Append("</td></tr>");
        if (receipt.HienThiThongTinLienHe)
        {
            sb.Append("<tr><td style=\"border:0;padding-left:0;\"><b>Số điện thoại:</b></td><td style=\"border:0;\">")
              .Append(Enc(receipt.SoDienThoaiHienThi)).Append("</td></tr>");
            sb.Append("<tr><td style=\"border:0;padding-left:0;\"><b>Địa chỉ:</b></td><td style=\"border:0;\">")
              .Append(Enc(receipt.DiaChiHienThi)).Append("</td></tr>");
        }
        if (!string.IsNullOrWhiteSpace(receipt.TenNhanVien))
        {
            sb.Append("<tr><td style=\"border:0;padding-left:0;\"><b>Nhân viên:</b></td><td style=\"border:0;\">")
              .Append(Enc(receipt.TenNhanVien)).Append("</td></tr>");
        }
        sb.Append("</table>");

        // Chi tiết hàng hóa.
        sb.Append("<table>");
        sb.Append("<tr><th>Sản phẩm</th><th class=\"r\">SL</th><th class=\"r\">Đơn giá</th><th class=\"r\">Thành tiền</th></tr>");

        foreach (var item in receipt.Items)
        {
            sb.Append("<tr>");
            sb.Append("<td>").Append(Enc(item.TenSanPham)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.SoLuong.ToString("N0", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.DonGia.ToString("N0", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.ThanhTien.ToString("N0", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</table>");

        // Tổng kết.
        sb.Append("<div class=\"sum\">");
        DongTien(sb, "Tổng tiền hàng", receipt.TongTienHang);
        if (receipt.GiamGia > 0)
            DongTien(sb, "Giảm giá", -receipt.GiamGia);
        DongTien(sb, "TỔNG THANH TOÁN", receipt.KhachCanTra, "total");
        DongTien(sb, "Tiền khách đưa", receipt.TienKhachDua);
        DongTien(sb, "Đã thanh toán", receipt.KhachDaThanhToan);
        if (receipt.TienThoi > 0)
            DongTien(sb, "Tiền thối", receipt.TienThoi);
        DongTien(sb, "Công nợ", receipt.ConNo, receipt.ConNo > 0 ? "debt" : "");
        sb.Append("</div>");

        sb.Append("<div class=\"muted\" style=\"margin-top:6px;\">Trạng thái: ")
          .Append(Enc(receipt.TrangThaiThanhToan)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(receipt.GhiChu))
        {
            sb.Append("<div class=\"note\"><b>Ghi chú:</b> ")
              .Append(Enc(receipt.GhiChu)).Append("</div>");
        }

        sb.Append("<div class=\"thanks\">").Append(Enc(tiem.LoiCamOnHienThi)).Append("</div>");

        sb.Append("<div class=\"noprint center\" style=\"margin-top:20px;\">");
        sb.Append("<button onclick=\"window.print()\" style=\"padding:10px 24px;border:0;border-radius:8px;background:#087F6E;color:#fff;font-size:14px;cursor:pointer;\">🖨 In hóa đơn</button>");
        sb.Append("</div>");

        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    private static void DongTien(StringBuilder sb, string nhan, decimal giaTri, string lopCss = "")
    {
        var css = string.IsNullOrWhiteSpace(lopCss) ? "" : $" {lopCss}";
        var tien = giaTri.ToString("N0", CultureInfo.InvariantCulture);

        sb.Append($"<div class=\"row{css}\"><span>{Enc(nhan)}</span><span>{tien} đ</span></div>");
    }

    private static string MaAnToan(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return "hoadon";

        var sb = new StringBuilder(ma.Length);

        foreach (var c in ma)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        return sb.Length == 0 ? "hoadon" : sb.ToString();
    }

    private static string Enc(string? value)
        => string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
}
