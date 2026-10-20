using System.Globalization;
using System.Text;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng bản in phiếu chi (và cả phiếu thu nếu truyền vào) dạng HTML rồi mở bằng
/// trình duyệt mặc định để người dùng in lại (Ctrl+P). Cách này không phụ thuộc
/// driver máy in, chạy được trên cả Windows lẫn Linux và dùng chung mô hình với
/// <see cref="PhieuNhapPrintService"/>.
/// </summary>
public static class PhieuChiPrintService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Ghi phiếu quỹ ra file HTML tạm và trả về đường dẫn file.</summary>
    public static string LuuFileTam(SoQuyChiTiet phieu, ThongTinTiem tiem)
    {
        var ten = MaAnToan(phieu.MaPhieu);
        var thuMuc = Path.Combine(Path.GetTempPath(), "ShopHoa", "PhieuChi");
        Directory.CreateDirectory(thuMuc);

        var duongDan = Path.Combine(thuMuc, $"phieuchi-{ten}.html");

        File.WriteAllText(duongDan, TaoHtml(phieu, tiem), new UTF8Encoding(false));

        return duongDan;
    }

    /// <summary>Dựng nội dung HTML hoàn chỉnh của phiếu.</summary>
    public static string TaoHtml(SoQuyChiTiet phieu, ThongTinTiem tiem)
    {
        var sb = new StringBuilder();
        var tieuDe = phieu.LaThu ? "PHIẾU THU TIỀN" : "PHIẾU CHI TIỀN";

        sb.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>").Append(Enc($"{tieuDe} {phieu.MaPhieu}")).Append("</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#263B35;margin:0;padding:24px;background:#fff;}");
        sb.Append(".wrap{max-width:560px;margin:0 auto;}");
        sb.Append(".center{text-align:center;}");
        sb.Append("h1{font-size:20px;margin:8px 0 2px;color:#163F38;}");
        sb.Append(".muted{color:#61736E;font-size:12px;}");
        sb.Append("table{width:100%;border-collapse:collapse;margin-top:12px;font-size:13px;}");
        sb.Append("th{text-align:left;border-bottom:1px solid #D8E5E0;padding:6px 4px;color:#163F38;}");
        sb.Append("td{padding:6px 4px;border-bottom:1px solid #EEF3F1;vertical-align:top;}");
        sb.Append(".amount{margin-top:14px;padding:14px;border:1px dashed #C9D8D3;border-radius:10px;text-align:center;}");
        sb.Append(".amount .label{font-size:11px;color:#61736E;letter-spacing:1px;}");
        sb.Append(".amount .value{font-size:24px;font-weight:bold;color:#D64545;margin-top:4px;}");
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

        sb.Append("<h1 class=\"center\">").Append(Enc(tieuDe)).Append("</h1>");
        sb.Append("<div class=\"center muted\">Mã phiếu: ").Append(Enc(phieu.MaPhieu)).Append("</div>");

        sb.Append("<table>");
        DongThongTin(sb, "Thời gian", phieu.ThoiGianHienThi);
        DongThongTin(sb, "Loại phiếu", phieu.LoaiPhieu);
        DongThongTin(sb, "Loại thu/chi", phieu.TenLoai);
        DongThongTin(sb, "Quỹ", phieu.PhuongThucHienThi);
        DongThongTin(sb, phieu.NhanNguoiNopNhan, phieu.NguoiNopNhan);
        DongThongTin(sb, "Người lập phiếu", phieu.NguoiTao);
        DongThongTin(sb, "Trạng thái", phieu.TrangThaiHienThi);
        DongThongTin(sb, "Nội dung", phieu.NoiDung);
        sb.Append("</table>");

        // Số tiền nổi bật ở giữa để dễ đọc khi in.
        sb.Append("<div class=\"amount\">");
        sb.Append("<div class=\"label\">SỐ TIỀN</div>");
        sb.Append("<div class=\"value\">").Append(phieu.SoTien.ToString("N0", Vi)).Append(" đ</div>");
        sb.Append("</div>");

        if (!string.IsNullOrWhiteSpace(phieu.GhiChu))
        {
            sb.Append("<div class=\"note\"><b>Ghi chú:</b> ")
              .Append(Enc(phieu.GhiChu)).Append("</div>");
        }

        sb.Append("<div class=\"thanks\">").Append(Enc(tiem.LoiCamOnHienThi)).Append("</div>");

        sb.Append("<div class=\"noprint center\" style=\"margin-top:20px;\">");
        sb.Append("<button onclick=\"window.print()\" style=\"padding:10px 24px;border:0;border-radius:8px;background:#087F6E;color:#fff;font-size:14px;cursor:pointer;\">🖨 In phiếu</button>");
        sb.Append("</div>");

        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    private static void DongThongTin(StringBuilder sb, string nhan, string giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
            return;

        sb.Append("<tr><td style=\"border:0;padding-left:0;width:42%;\"><b>")
          .Append(Enc(nhan)).Append(":</b></td><td style=\"border:0;\">")
          .Append(Enc(giaTri)).Append("</td></tr>");
    }

    private static string MaAnToan(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return "phieuchi";

        var sb = new StringBuilder(ma.Length);

        foreach (var c in ma)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        return sb.Length == 0 ? "phieuchi" : sb.ToString();
    }

    private static string Enc(string? value)
        => string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
}
