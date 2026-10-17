using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class BaoCaoInDanhSachWindow : Window, INotifyPropertyChanged
{
    private readonly DatabaseService _db = new();
    private readonly string _loaiBaoCao;
    private readonly List<object> _tatCaItems = new();
    private const int SoDongMoiTrang = 24;

    public string TenCuaHang { get; set; } = "TIỆM HOA TƯƠI WINWON";
    public string DiaChiCuaHang { get; set; } = "Ninh Kiều, Cần Thơ";
    public string DienThoaiCuaHang { get; set; } = "ĐT: 0901 000 001";
    public string ThoiGianInHienThi { get; set; } = "";
    public string TieuDeBaoCao { get; set; } = "DANH SÁCH BÁO CÁO";
    public string NguoiInHienThi { get; set; } = "";
    public string BoLocHienThi { get; set; } = "Tất cả";
    public string TongKetSoLuongText { get; set; } = "";
    public string TongKetChiTietText { get; set; } = "";
    public bool DuocXemGiaVon { get; set; }

    public bool IsBaoCaoHangHoa => _loaiBaoCao == "HANG_HOA";
    public bool IsBaoCaoKhachHang => _loaiBaoCao == "KHACH_HANG";
    public bool IsBaoCaoNhaCungCap => _loaiBaoCao == "NHA_CUNG_CAP";

    private int _trangHienTai = 1;
    public int TrangHienTai
    {
        get => _trangHienTai;
        set
        {
            if (_trangHienTai != value)
            {
                _trangHienTai = value;
                OnPropertyChanged(nameof(TrangHienTai));
                OnPropertyChanged(nameof(ThongTinTrangHienThi));
                OnPropertyChanged(nameof(SoTrangChanTrangHienThi));
                OnPropertyChanged(nameof(IsTrangCuoi));
            }
        }
    }

    private int _tongSoTrang = 1;
    public int TongSoTrang
    {
        get => _tongSoTrang;
        set
        {
            if (_tongSoTrang != value)
            {
                _tongSoTrang = value;
                OnPropertyChanged(nameof(TongSoTrang));
                OnPropertyChanged(nameof(ThongTinTrangHienThi));
                OnPropertyChanged(nameof(SoTrangChanTrangHienThi));
                OnPropertyChanged(nameof(IsTrangCuoi));
            }
        }
    }

    private double _zoomScale = 1.0;
    public double ZoomScale
    {
        get => _zoomScale;
        set
        {
            if (Math.Abs(_zoomScale - value) > 0.001)
            {
                _zoomScale = value;
                OnPropertyChanged(nameof(ZoomScale));
                OnPropertyChanged(nameof(ZoomPercentText));
            }
        }
    }

    public string ZoomPercentText => $"{(int)(ZoomScale * 100)}%";
    public string ThongTinTrangHienThi => $"Trang {TrangHienTai} / {TongSoTrang}";
    public string SoTrangChanTrangHienThi => $"— Trang {TrangHienTai} / {TongSoTrang} —";
    public bool IsTrangCuoi => TrangHienTai == TongSoTrang;
    public bool IsDanhSachRong => _tatCaItems.Count == 0;

    public ObservableCollection<InDanhSachRow> ItemsHienThi { get; } = new();

    public BaoCaoInDanhSachWindow() : this("BÁO CÁO", Array.Empty<object>(), "HANG_HOA")
    {
    }

    public BaoCaoInDanhSachWindow(
        string tieuDe,
        IEnumerable items,
        string loaiBaoCao = "HANG_HOA",
        string? boLocText = null,
        bool duocXemGiaVon = false)
    {
        TieuDeBaoCao = tieuDe;
        _loaiBaoCao = (loaiBaoCao ?? "HANG_HOA").Trim().ToUpperInvariant();
        DuocXemGiaVon = duocXemGiaVon;
        BoLocHienThi = string.IsNullOrWhiteSpace(boLocText) ? "Tất cả" : boLocText.Trim();

        var now = DateTime.Now;
        ThoiGianInHienThi = $"Thời gian: {now:dd/MM/yyyy HH:mm}";
        var user = Session.CurrentUser?.HoTen ?? Session.CurrentUser?.TenDangNhap ?? "Quản trị viên";
        NguoiInHienThi = $"Người lập: {user} • Ngày lập: {now:dd/MM/yyyy}";

        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null)
                    _tatCaItems.Add(item);
            }
        }

        TongSoTrang = Math.Max(1, (int)Math.Ceiling(_tatCaItems.Count / (double)SoDongMoiTrang));
        TinhTongKet();

        InitializeComponent();
        DataContext = this;

        Loaded += async (_, _) =>
        {
            await NapThongTinTiemAsync();
            CapNhatTrangHienThi();
        };
    }

    private async Task NapThongTinTiemAsync()
    {
        try
        {
            var storeService = new StoreInfoService(_db.TaoKetNoiAsync);
            var info = await storeService.LayThongTinAsync();
            if (info != null)
            {
                if (!string.IsNullOrWhiteSpace(info.TenTiem))
                    TenCuaHang = info.TenTiem;

                if (!string.IsNullOrWhiteSpace(info.DiaChi))
                    DiaChiCuaHang = info.DiaChi;

                if (!string.IsNullOrWhiteSpace(info.SoDienThoai))
                    DienThoaiCuaHang = "ĐT: " + info.SoDienThoai;

                OnPropertyChanged(nameof(TenCuaHang));
                OnPropertyChanged(nameof(DiaChiCuaHang));
                OnPropertyChanged(nameof(DienThoaiCuaHang));
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi đọc thông tin tiệm cho báo cáo in", ex);
        }
    }

    private void TinhTongKet()
    {
        int count = _tatCaItems.Count;
        TongKetSoLuongText = $"Tổng cộng: {count:N0} mặt hàng / bản ghi";

        if (IsBaoCaoHangHoa)
        {
            decimal tongTon = 0;
            foreach (var it in _tatCaItems)
            {
                if (it is SanPham sp)
                    tongTon += sp.TonKho;
            }
            TongKetChiTietText = $"Tổng tồn kho: {tongTon:N0}";
        }
        else if (IsBaoCaoKhachHang)
        {
            decimal tongNo = 0;
            foreach (var it in _tatCaItems)
            {
                if (it is KhachHang kh)
                    tongNo += kh.NoHienTai;
            }
            TongKetChiTietText = $"Tổng nợ hiện tại: {tongNo:N0} đ";
        }
        else if (IsBaoCaoNhaCungCap)
        {
            decimal tongNo = 0;
            foreach (var it in _tatCaItems)
            {
                if (it is NhaCungCap ncc)
                    tongNo += ncc.ConNo;
            }
            TongKetSoLuongText = $"Tổng số nhà cung cấp: {count:N0}";
            TongKetChiTietText = $"Tổng nợ cần trả: {tongNo:N0} đ";
        }
    }

    private void CapNhatTrangHienThi()
    {
        ItemsHienThi.Clear();

        if (_tatCaItems.Count == 0)
        {
            OnPropertyChanged(nameof(IsDanhSachRong));
            return;
        }

        int batDau = (TrangHienTai - 1) * SoDongMoiTrang;
        var trangItems = _tatCaItems.Skip(batDau).Take(SoDongMoiTrang).ToList();

        for (int i = 0; i < trangItems.Count; i++)
        {
            int sttIn = batDau + i + 1;
            var obj = trangItems[i];

            if (obj is SanPham sp)
            {
                ItemsHienThi.Add(new InHangHoaRow
                {
                    SttIn = sttIn,
                    MaSanPham = sp.MaSanPham,
                    TenSanPham = sp.TenSanPham,
                    DonViTinh = sp.DonViTinh ?? "Cái",
                    GiaVon = sp.GiaVon,
                    GiaBan = sp.GiaBan,
                    TonKho = sp.TonKho
                });
            }
            else if (obj is KhachHang kh)
            {
                ItemsHienThi.Add(new InKhachHangRow
                {
                    SttIn = sttIn,
                    MaKhachHang = kh.MaKhachHang,
                    TenKhachHang = kh.TenKhachHang,
                    SoDienThoai = kh.SoDienThoai,
                    DiaChi = kh.DiaChi,
                    NoHienTai = kh.NoHienTai
                });
            }
            else if (obj is NhaCungCap ncc)
            {
                var diaChiDayDu = ncc.DiaChi;
                if (string.IsNullOrWhiteSpace(diaChiDayDu))
                {
                    var phanDiaChi = new[] { ncc.PhuongXa, ncc.QuanHuyen, ncc.TinhThanh }
                        .Where(p => !string.IsNullOrWhiteSpace(p));
                    diaChiDayDu = string.Join(", ", phanDiaChi);
                }

                ItemsHienThi.Add(new InNhaCungCapRow
                {
                    SttIn = sttIn,
                    MaNhaCungCap = ncc.MaNhaCungCap,
                    TenNhaCungCap = ncc.TenNhaCungCap,
                    NguoiLienHe = !string.IsNullOrWhiteSpace(ncc.NguoiLienHe) ? ncc.NguoiLienHe : ncc.LienHe,
                    SoDienThoai = ncc.SoDienThoai,
                    DiaChi = diaChiDayDu,
                    MaSoThue = ncc.MaSoThue,
                    Email = ncc.Email,
                    ConNo = ncc.ConNo
                });
            }
        }

        OnPropertyChanged(nameof(IsDanhSachRong));
        OnPropertyChanged(nameof(IsTrangCuoi));
    }

    // =========================================================
    // ĐIỀU KHIỂN PHÂN TRANG
    // =========================================================
    private void TrangDau_OnClick(object? sender, RoutedEventArgs e)
    {
        TrangHienTai = 1;
        CapNhatTrangHienThi();
    }

    private void TrangTruoc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TrangHienTai > 1)
        {
            TrangHienTai--;
            CapNhatTrangHienThi();
        }
    }

    private void TrangSau_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TrangHienTai < TongSoTrang)
        {
            TrangHienTai++;
            CapNhatTrangHienThi();
        }
    }

    private void TrangCuoi_OnClick(object? sender, RoutedEventArgs e)
    {
        TrangHienTai = TongSoTrang;
        CapNhatTrangHienThi();
    }

    // =========================================================
    // ĐIỀU KHIỂN THU PHÓNG (ZOOM)
    // =========================================================
    private void ZoomIn_OnClick(object? sender, RoutedEventArgs e)
    {
        ZoomScale = Math.Min(2.0, Math.Round(ZoomScale + 0.15, 2));
    }

    private void ZoomOut_OnClick(object? sender, RoutedEventArgs e)
    {
        ZoomScale = Math.Max(0.5, Math.Round(ZoomScale - 0.15, 2));
    }

    private void ZoomReset_OnClick(object? sender, RoutedEventArgs e)
    {
        ZoomScale = 1.0;
    }

    // =========================================================
    // IN ẤN VÀ XUẤT FILE
    // =========================================================
    private void InAn_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var html = SinhHtmlBaoCaoA4(tuDongIn: true);
            var tempPath = Path.Combine(Path.GetTempPath(), $"BaoCao_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.WriteAllText(tempPath, html, Encoding.UTF8);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi in báo cáo", ex);
        }
    }

    private async void XuatPdf_OnClick(object? sender, RoutedEventArgs e)
    {
        var tenFileGoiY = IsBaoCaoNhaCungCap
            ? $"DanhSachNhaCungCap_{DateTime.Now:yyyyMMdd}.pdf"
            : (IsBaoCaoKhachHang
                ? $"DanhSachKhachHang_{DateTime.Now:yyyyMMdd}.pdf"
                : $"DanhSachHangHoa_{DateTime.Now:yyyyMMdd}.pdf");

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Xuất tài liệu in (PDF / HTML)",
            DefaultExtension = "pdf",
            SuggestedFileName = tenFileGoiY,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Tài liệu PDF (*.pdf)") { Patterns = new[] { "*.pdf" } },
                new FilePickerFileType("Tài liệu in Web (*.html)") { Patterns = new[] { "*.html", "*.htm" } }
            }
        });

        if (file is null) return;

        try
        {
            var html = SinhHtmlBaoCaoA4(tuDongIn: false);
            var path = file.Path.LocalPath;

            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var converted = await ChuyenHtmlSangPdfAsync(html, path);
                if (!converted)
                {
                    // Fallback nếu không có Edge/Chrome headless: lưu file HTML với tên tương ứng
                    var htmlFallback = Path.ChangeExtension(path, ".html");
                    await File.WriteAllTextAsync(htmlFallback, html, Encoding.UTF8);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = htmlFallback,
                        UseShellExecute = true
                    });
                    return;
                }
            }
            else
            {
                await File.WriteAllTextAsync(path, html, Encoding.UTF8);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất tài liệu in", ex);
        }
    }

    private static async Task<bool> ChuyenHtmlSangPdfAsync(string htmlContent, string targetPdfPath)
    {
        var tempHtml = Path.Combine(Path.GetTempPath(), $"temp_in_{Guid.NewGuid():N}.html");
        try
        {
            await File.WriteAllTextAsync(tempHtml, htmlContent, Encoding.UTF8);

            var browserCandidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe")
            };

            var browserPath = browserCandidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(browserPath))
                return false;

            var psi = new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = $"--headless --disable-gpu --run-all-compositor-stages-before-draw --no-pdf-header-footer --print-to-pdf=\"{targetPdfPath}\" \"{tempHtml}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return File.Exists(targetPdfPath) && new FileInfo(targetPdfPath).Length > 0;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi chuyển đổi HTML sang PDF", ex);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempHtml))
                    File.Delete(tempHtml);
            }
            catch { }
        }
    }

    private string SinhHtmlBaoCaoA4(bool tuDongIn)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='vi'><head><meta charset='utf-8'><title>" + TieuDeBaoCao + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("@page { size: A4 portrait; margin: 12mm 15mm; }");
        sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; margin: 0; font-size: 12.5px; color: #111; line-height: 1.4; }");
        sb.AppendLine(".print-bar { background: #2B2E31; color: white; padding: 10px 20px; display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }");
        sb.AppendLine(".print-btn { background: #2E7D32; color: white; border: none; padding: 8px 20px; border-radius: 4px; font-weight: bold; cursor: pointer; font-size: 13px; }");
        sb.AppendLine("h2 { text-align: center; color: #0B4B82; margin: 8px 0 2px 0; font-size: 20px; text-transform: uppercase; letter-spacing: 0.5px; }");
        sb.AppendLine(".store-info { font-size: 12px; color: #333; line-height: 1.5; }");
        sb.AppendLine(".sub-header { text-align: center; font-style: italic; color: #555; margin-bottom: 16px; font-size: 11.5px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 10px; page-break-inside: auto; }");
        sb.AppendLine("tr { page-break-inside: avoid; page-break-after: auto; }");
        sb.AppendLine("thead { display: table-header-group; }");
        sb.AppendLine("th, td { border: 1px solid #778899; padding: 6px 8px; }");
        sb.AppendLine("th { background-color: #E6EFF7 !important; color: #0B4B82; font-weight: bold; font-size: 11.5px; -webkit-print-color-adjust: exact; print-color-adjust: exact; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #F8FAFC; -webkit-print-color-adjust: exact; print-color-adjust: exact; }");
        sb.AppendLine(".text-right { text-align: right; }");
        sb.AppendLine(".text-center { text-align: center; }");
        sb.AppendLine(".bold { font-weight: bold; }");
        sb.AppendLine(".summary-row { margin-top: 14px; display: flex; justify-content: space-between; font-weight: bold; font-size: 12.5px; }");
        sb.AppendLine(".footer-sign { width: 100%; margin-top: 35px; display: flex; justify-content: space-around; text-align: center; page-break-inside: avoid; }");
        sb.AppendLine(".sign-box { width: 30%; }");
        sb.AppendLine("@media print { .print-bar { display: none !important; } }");
        sb.AppendLine("</style>");

        if (tuDongIn)
        {
            sb.AppendLine("<script>window.onload = function() { setTimeout(function() { window.print(); }, 400); };</script>");
        }

        sb.AppendLine("</head><body>");

        sb.AppendLine("<div class='print-bar'>");
        sb.AppendLine($"<span>Bản xem trước A4: <b>{TieuDeBaoCao}</b> ({_tatCaItems.Count:N0} bản ghi)</span>");
        sb.AppendLine("<button class='print-btn' onclick='window.print()'>🖨️ In trang này (A4 / PDF)</button>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div style='display: flex; justify-content: space-between; align-items: flex-start;'>");
        sb.AppendLine($"<div class='store-info'><b style='font-size: 14px; color: #0B4B82;'>{TenCuaHang}</b><br>{DiaChiCuaHang}<br>{DienThoaiCuaHang}</div>");
        sb.AppendLine($"<div style='text-align: right; font-size: 11px; color: #666;'><i>Mẫu: Báo cáo danh mục A4</i><br>{ThoiGianInHienThi}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<hr style='border: none; border-top: 1px solid #CBD5E1; margin: 10px 0 12px 0;'>");
        sb.AppendLine($"<h2>{TieuDeBaoCao}</h2>");
        sb.AppendLine($"<div class='sub-header'>Bộ lọc: {BoLocHienThi} • {NguoiInHienThi}</div>");

        sb.AppendLine("<table><thead><tr>");
        if (IsBaoCaoHangHoa)
        {
            sb.Append("<th class='text-center' style='width: 40px;'>STT</th><th style='width: 100px;'>Mã hàng</th><th>Tên hàng hóa</th><th class='text-center' style='width: 65px;'>ĐVT</th>");
            if (DuocXemGiaVon)
            {
                sb.Append("<th class='text-right' style='width: 95px;'>Giá nhập</th>");
            }
            sb.Append("<th class='text-right' style='width: 95px;'>Giá bán</th><th class='text-right' style='width: 80px;'>Tồn kho</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int idx = 1;
            foreach (var obj in _tatCaItems)
            {
                if (obj is SanPham sp)
                {
                    sb.Append($"<tr><td class='text-center'>{idx++}</td><td class='bold'>{sp.MaSanPham}</td><td>{sp.TenSanPham}</td><td class='text-center'>{sp.DonViTinh}</td>");
                    if (DuocXemGiaVon)
                    {
                        sb.Append($"<td class='text-right'>{sp.GiaVon:N0} đ</td>");
                    }
                    sb.Append($"<td class='text-right bold'>{sp.GiaBan:N0} đ</td><td class='text-right'>{sp.TonKho:N0}</td></tr>");
                }
            }
        }
        else if (IsBaoCaoKhachHang)
        {
            sb.AppendLine("<th class='text-center' style='width: 40px;'>STT</th><th style='width: 90px;'>Mã KH</th><th>Tên khách hàng</th><th style='width: 100px;'>Điện thoại</th><th>Địa chỉ</th><th class='text-right' style='width: 110px;'>Nợ hiện tại</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int idx = 1;
            foreach (var obj in _tatCaItems)
            {
                if (obj is KhachHang kh)
                {
                    sb.AppendLine($"<tr><td class='text-center'>{idx++}</td><td class='bold'>{kh.MaKhachHang}</td><td>{kh.TenKhachHang}</td><td>{kh.SoDienThoai}</td><td>{kh.DiaChi}</td><td class='text-right bold' style='color: #B91C1C;'>{kh.NoHienTai:N0} đ</td></tr>");
                }
            }
        }
        else
        {
            sb.AppendLine("<th class='text-center' style='width: 35px;'>STT</th><th style='width: 85px;'>Mã NCC</th><th>Tên nhà cung cấp</th><th style='width: 110px;'>Người liên hệ</th><th style='width: 95px;'>Điện thoại</th><th>Địa chỉ</th><th class='text-right' style='width: 105px;'>Còn nợ</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int idx = 1;
            foreach (var obj in _tatCaItems)
            {
                if (obj is NhaCungCap ncc)
                {
                    var nguoiLienHe = !string.IsNullOrWhiteSpace(ncc.NguoiLienHe) ? ncc.NguoiLienHe : (ncc.LienHe ?? "—");
                    var sdt = string.IsNullOrWhiteSpace(ncc.SoDienThoai) ? "—" : ncc.SoDienThoai;
                    var diaChi = string.IsNullOrWhiteSpace(ncc.DiaChi) ? "—" : ncc.DiaChi;
                    sb.AppendLine($"<tr><td class='text-center'>{idx++}</td><td class='bold'>{ncc.MaNhaCungCap}</td><td><b>{ncc.TenNhaCungCap}</b></td><td>{nguoiLienHe}</td><td>{sdt}</td><td>{diaChi}</td><td class='text-right bold' style='color: #B91C1C;'>{ncc.ConNo:N0} đ</td></tr>");
                }
            }
        }

        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class='summary-row'>");
        sb.AppendLine($"<div>{TongKetSoLuongText}</div>");
        sb.AppendLine($"<div style='color: #0B4B82;'>{TongKetChiTietText}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='footer-sign'>");
        sb.AppendLine("<div class='sign-box'><b>Người lập biểu</b><br><i>(Ký, ghi rõ họ tên)</i></div>");
        sb.AppendLine("<div class='sign-box'><b>Kế toán trưởng</b><br><i>(Ký, ghi rõ họ tên)</i></div>");
        sb.AppendLine("<div class='sign-box'><b>Thủ trưởng đơn vị</b><br><i>(Ký, đóng dấu)</i></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private async void XuatExcel_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Xuất Excel báo cáo",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"{TieuDeBaoCao.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
            }
        });

        if (file is null) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("BaoCao");

            ws.Cell(1, 1).Value = TenCuaHang;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Value = TieuDeBaoCao;
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 14;
            ws.Cell(3, 1).Value = $"Bộ lọc: {BoLocHienThi} • {ThoiGianInHienThi}";
            ws.Cell(3, 1).Style.Font.Italic = true;

            int rowIdx = 5;
            if (IsBaoCaoHangHoa)
            {
                var headers = new List<string> { "STT", "Mã hàng", "Tên hàng hóa", "ĐVT" };
                if (DuocXemGiaVon) headers.Add("Giá nhập");
                headers.Add("Giá bán");
                headers.Add("Tồn kho");

                for (int c = 0; c < headers.Count; c++)
                {
                    ws.Cell(rowIdx, c + 1).Value = headers[c];
                    ws.Cell(rowIdx, c + 1).Style.Font.Bold = true;
                    ws.Cell(rowIdx, c + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 239, 247);
                }

                int stt = 1;
                foreach (var obj in _tatCaItems)
                {
                    if (obj is SanPham sp)
                    {
                        rowIdx++;
                        int c = 1;
                        ws.Cell(rowIdx, c++).Value = stt++;
                        ws.Cell(rowIdx, c++).Value = sp.MaSanPham;
                        ws.Cell(rowIdx, c++).Value = sp.TenSanPham;
                        ws.Cell(rowIdx, c++).Value = sp.DonViTinh;
                        if (DuocXemGiaVon) ws.Cell(rowIdx, c++).Value = sp.GiaVon;
                        ws.Cell(rowIdx, c++).Value = sp.GiaBan;
                        ws.Cell(rowIdx, c++).Value = sp.TonKho;
                    }
                }
            }
            else if (IsBaoCaoKhachHang)
            {
                string[] headers = { "STT", "Mã KH", "Tên khách hàng", "Điện thoại", "Địa chỉ", "Email", "Nợ hiện tại" };
                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cell(rowIdx, c + 1).Value = headers[c];
                    ws.Cell(rowIdx, c + 1).Style.Font.Bold = true;
                    ws.Cell(rowIdx, c + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 239, 247);
                }

                int stt = 1;
                foreach (var obj in _tatCaItems)
                {
                    if (obj is KhachHang kh)
                    {
                        rowIdx++;
                        ws.Cell(rowIdx, 1).Value = stt++;
                        ws.Cell(rowIdx, 2).Value = kh.MaKhachHang;
                        ws.Cell(rowIdx, 3).Value = kh.TenKhachHang;
                        ws.Cell(rowIdx, 4).Value = kh.SoDienThoai;
                        ws.Cell(rowIdx, 5).Value = kh.DiaChi;
                        ws.Cell(rowIdx, 6).Value = kh.Email;
                        ws.Cell(rowIdx, 7).Value = kh.NoHienTai;
                    }
                }
            }
            else
            {
                string[] headers = { "STT", "Mã NCC", "Tên nhà cung cấp", "Người liên hệ", "Điện thoại", "Địa chỉ", "Mã số thuế", "Email", "Còn nợ" };
                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cell(rowIdx, c + 1).Value = headers[c];
                    ws.Cell(rowIdx, c + 1).Style.Font.Bold = true;
                    ws.Cell(rowIdx, c + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 239, 247);
                }

                int stt = 1;
                foreach (var obj in _tatCaItems)
                {
                    if (obj is NhaCungCap ncc)
                    {
                        rowIdx++;
                        ws.Cell(rowIdx, 1).Value = stt++;
                        ws.Cell(rowIdx, 2).Value = ncc.MaNhaCungCap;
                        ws.Cell(rowIdx, 3).Value = ncc.TenNhaCungCap;
                        ws.Cell(rowIdx, 4).Value = ncc.NguoiLienHe ?? ncc.LienHe ?? "";
                        ws.Cell(rowIdx, 5).Value = ncc.SoDienThoai ?? "";
                        ws.Cell(rowIdx, 6).Value = ncc.DiaChi ?? "";
                        ws.Cell(rowIdx, 7).Value = ncc.MaSoThue ?? "";
                        ws.Cell(rowIdx, 8).Value = ncc.Email ?? "";
                        ws.Cell(rowIdx, 9).Value = ncc.ConNo;
                        ws.Cell(rowIdx, 9).Style.NumberFormat.Format = "#,##0";
                    }
                }
            }

            ws.Columns().AdjustToContents();

            await using var stream = await file.OpenWriteAsync();
            wb.SaveAs(stream);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel báo cáo", ex);
        }
    }

    private void Dong_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class InDanhSachRow
{
    public int SttIn { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public decimal GiaVon { get; set; }
    public decimal GiaBan { get; set; }
    public decimal TonKho { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public decimal NoHienTai { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string? NguoiLienHe { get; set; }
    public string NguoiLienHeHienThi => string.IsNullOrWhiteSpace(NguoiLienHe) ? "—" : NguoiLienHe.Trim();
    public string? MaSoThue { get; set; }
    public string? Email { get; set; }
    public decimal ConNo { get; set; }
}

public sealed class InHangHoaRow : InDanhSachRow
{
}

public sealed class InKhachHangRow : InDanhSachRow
{
}

public sealed class InNhaCungCapRow : InDanhSachRow
{
}
