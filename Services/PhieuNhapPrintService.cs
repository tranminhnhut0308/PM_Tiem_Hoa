using System.Globalization;
using System.Text;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng bản in phiếu nhập hàng dạng HTML rồi mở bằng trình duyệt mặc định để
/// người dùng in lại (Ctrl+P). Cách này không phụ thuộc driver máy in và chạy
/// được trên cả Windows lẫn Linux, phù hợp để "xuất lại" phiếu nhập cũ.
/// </summary>
public static class PhieuNhapPrintService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Ghi phiếu nhập ra file HTML tạm và trả về đường dẫn file.</summary>
    public static string LuuFileTam(PhieuNhapHangDetail detail, ThongTinTiem tiem)
    {
        var ten = MaAnToan(detail.MaPhieuNhap);
        var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "PhieuNhap");
        Directory.CreateDirectory(thuMuc);

        var duongDan = Path.Combine(thuMuc, $"phieunhap-{ten}.html");

        File.WriteAllText(duongDan, TaoHtml(detail, tiem), new UTF8Encoding(false));

        return duongDan;
    }

    /// <summary>Dựng nội dung HTML hoàn chỉnh của phiếu nhập.</summary>
    public static string TaoHtml(PhieuNhapHangDetail detail, ThongTinTiem tiem)
    {
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>").Append(Enc($"Phiếu nhập {detail.MaPhieuNhap}")).Append("</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#263B35;margin:0;padding:24px;background:#fff;}");
        sb.Append(".wrap{max-width:560px;margin:0 auto;}");
        sb.Append(".center{text-align:center;}");
        sb.Append("h1{font-size:20px;margin:8px 0 2px;color:#163F38;}");
        sb.Append(".muted{color:#61736E;font-size:12px;}");
        sb.Append("table{width:100%;border-collapse:collapse;margin-top:12px;font-size:13px;}");
        sb.Append("th{text-align:left;border-bottom:1px solid #D8E5E0;padding:6px 4px;color:#163F38;}");
        sb.Append("td{padding:6px 4px;border-bottom:1px solid #EEF3F1;vertical-align:top;}");
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

        // Đầu phiếu: thông tin tiệm.
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

        sb.Append("<h1 class=\"center\">PHIẾU NHẬP HÀNG</h1>");
        sb.Append("<div class=\"center muted\">Mã phiếu: ").Append(Enc(detail.MaPhieuNhap)).Append("</div>");
        sb.Append("<div class=\"center muted\">Ngày nhập: ")
          .Append(Enc(detail.ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)))
          .Append("</div>");

        sb.Append("<table>");
        DongThongTin(sb, "Nhà cung cấp", detail.TenNhaCungCapHienThi);
        if (!string.IsNullOrWhiteSpace(detail.MaNhaCungCap))
            DongThongTin(sb, "Mã nhà cung cấp", detail.MaNhaCungCap!);
        DongThongTin(sb, "Nhân viên nhập", detail.TenNhanVienHienThi);
        if (detail.CoSoHoaDon)
            DongThongTin(sb, "Số hóa đơn đầu vào", detail.SoHoaDonDauVao!);
        if (detail.CoMaDatHang)
            DongThongTin(sb, "Mã đặt hàng nhập", detail.MaDatHangNhap!);
        DongThongTin(sb, "Phương thức thanh toán", detail.PhuongThucThanhToanHienThi);
        sb.Append("</table>");

        // Chi tiết hàng hóa.
        sb.Append("<table>");
        sb.Append("<tr><th>STT</th><th>Sản phẩm</th><th>ĐVT</th><th class=\"r\">SL</th><th class=\"r\">Đơn giá</th><th class=\"r\">Thành tiền</th></tr>");

        foreach (var item in detail.Items)
        {
            sb.Append("<tr>");
            sb.Append("<td class=\"r\">").Append(Enc(item.Index.ToString(CultureInfo.InvariantCulture))).Append("</td>");
            sb.Append("<td>").Append(Enc(item.TenSanPham)).Append("</td>");
            sb.Append("<td>").Append(Enc(item.DonViTinhHienThi)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(Enc(item.SoLuongText)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.DonGiaNhap.ToString("N0", Vi)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(item.ThanhTien.ToString("N0", Vi)).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</table>");

        // Tổng kết.
        sb.Append("<div class=\"sum\">");
        DongTien(sb, "Tổng tiền hàng", detail.TongTienHang);
        if (detail.GiamGia != 0m)
            DongTien(sb, "Giảm giá", -detail.GiamGia);
        if (detail.ChiPhiKhac != 0m)
            DongTien(sb, "Chi phí khác", detail.ChiPhiKhac);
        DongTien(sb, "TỔNG NHẬP", detail.TongTienNhap, "total");
        if (detail.DaThanhToan > 0m)
            DongTien(sb, "Đã thanh toán", detail.DaThanhToan);
        DongTien(sb, "Công nợ", detail.ConNo, detail.ConNo > 0 ? "debt" : "");
        sb.Append("</div>");

        sb.Append("<div class=\"muted\" style=\"margin-top:6px;\">Trạng thái: ")
          .Append(Enc(detail.TrangThaiHienThi)).Append("</div>");

        if (detail.CoGhiChu)
        {
            sb.Append("<div class=\"note\"><b>Ghi chú:</b> ")
              .Append(Enc(detail.GhiChu)).Append("</div>");
        }

        sb.Append("<div class=\"thanks\">").Append(Enc(tiem.LoiCamOnHienThi)).Append("</div>");

        sb.Append("<div class=\"noprint center\" style=\"margin-top:20px;\">");
        sb.Append("<button onclick=\"window.print()\" style=\"padding:10px 24px;border:0;border-radius:8px;background:#087F6E;color:#fff;font-size:14px;cursor:pointer;\">🖨 In phiếu nhập</button>");
        sb.Append("</div>");

        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    private static void DongThongTin(StringBuilder sb, string nhan, string giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return;

        sb.Append("<tr><td style=\"border:0;padding-left:0;width:45%;\"><b>")
          .Append(Enc(nhan)).Append(":</b></td><td style=\"border:0;\">")
          .Append(Enc(giaTri)).Append("</td></tr>");
    }

    private static void DongTien(StringBuilder sb, string nhan, decimal giaTri, string lopCss = "")
    {
        var css = string.IsNullOrWhiteSpace(lopCss) ? "" : $" {lopCss}";
        var tien = giaTri.ToString("N0", Vi);

        sb.Append($"<div class=\"row{css}\"><span>{Enc(nhan)}</span><span>{tien} đ</span></div>");
    }

    private static string MaAnToan(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return "phieunhap";

        var sb = new StringBuilder(ma.Length);

        foreach (var c in ma)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        return sb.Length == 0 ? "phieunhap" : sb.ToString();
    }

    private static string Enc(string? value)
        => string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
}
