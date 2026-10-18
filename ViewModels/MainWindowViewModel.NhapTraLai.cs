using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 28 & 29: NHẬP TRẢ LẠI (DANH SÁCH & THÊM PHIẾU)
    // =========================================================

    public bool IsNhapTraLaiSelected => SelectedMenu == "Nhập trả lại";

    [ObservableProperty]
    private bool isThemPhieuNhapTraLaiVisible;

    // =========================================================
    // SECTION A: DANH SÁCH PHIẾU NHẬP TRẢ LẠI (GIAO DIỆN 28)
    // =========================================================

    public ObservableCollection<PhieuTraHang> DsPhieuTraHangItems { get; } = new();

    [ObservableProperty] private string dsPhieuTraSearch = "";
    [ObservableProperty] private DateTime? dsPhieuTraTuNgay;
    [ObservableProperty] private DateTime? dsPhieuTraDenNgay;
    [ObservableProperty] private string selectedPhieuTraTrangThai = "TAT_CA";
    [ObservableProperty] private bool isLoadingDsPhieuTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangSau))]
    private int dsPhieuTraTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangSau))]
    private int dsPhieuTraPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangSau))]
    private int dsPhieuTraPageSize = 20;

    public int DsPhieuTraTotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)DsPhieuTraTotalCount / Math.Max(1, DsPhieuTraPageSize)));

    public bool CanDsPhieuTraTrangTruoc => DsPhieuTraPage > 1;
    public bool CanDsPhieuTraTrangSau => DsPhieuTraPage < DsPhieuTraTotalPages;

    public string DsPhieuTraRangeText
    {
        get
        {
            if (DsPhieuTraTotalCount == 0) return "0 phiếu trả lại";
            var from = (DsPhieuTraPage - 1) * DsPhieuTraPageSize + 1;
            var to = Math.Min(from + DsPhieuTraPageSize - 1, DsPhieuTraTotalCount);
            return $"{from} - {to} trong {DsPhieuTraTotalCount:N0} phiếu";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongPhieuTraText))]
    private int thongKeTongPhieuTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongTienTraText))]
    private decimal thongKeTongTienTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoLuongTraText))]
    private decimal thongKeTongSoLuongTra;

    public string ThongKeTongPhieuTraText => $"{ThongKeTongPhieuTra:N0} phiếu";
    public string ThongKeTongTienTraText => MoneyInput.Format(ThongKeTongTienTra) + " đ";
    public string ThongKeTongSoLuongTraText => $"{ThongKeTongSoLuongTra:N0} SP";

    public IReadOnlyList<string> DsPhieuTraTrangThaiOptions { get; } =
        ["Tất cả trạng thái", "Hoàn thành", "Đã hủy"];

    public string SelectedPhieuTraTrangThaiHienThi
    {
        get => SelectedPhieuTraTrangThai switch
        {
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            _ => "Tất cả trạng thái"
        };
        set
        {
            SelectedPhieuTraTrangThai = value switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Đã hủy" => "DA_HUY",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuTraAsync();
        }
    }

    /// <summary>
    /// Mở màn hình "Nghiệp vụ → Nhập trả lại → Danh sách phiếu nhập trả lại".
    /// </summary>
    [RelayCommand]
    public async Task MoGiaoDienNhapTraLaiAsync()
    {
        IsSalesPanelVisible = false;
        IsThemPhieuNhapTraLaiVisible = false;
        SelectedMenu = "Nhập trả lại";
        DsPhieuTraPage = 1;
        await LoadDsPhieuTraAsync();
    }

    /// <summary>
    /// Tải dữ liệu danh sách phiếu trả hàng và thống kê theo bộ lọc hiện tại.
    /// </summary>
    [RelayCommand]
    public async Task LoadDsPhieuTraAsync()
    {
        if (IsLoadingDsPhieuTra)
            return;

        try
        {
            IsLoadingDsPhieuTra = true;
            StatusMessage = "Đang tải danh sách phiếu nhập trả lại...";

            var (items, totalCount) = await _db.GetPhieuTraHangPagedAsync(
                DsPhieuTraSearch,
                DsPhieuTraTuNgay,
                DsPhieuTraDenNgay,
                SelectedPhieuTraTrangThai,
                DsPhieuTraPage,
                DsPhieuTraPageSize);

            DsPhieuTraHangItems.Clear();
            foreach (var item in items)
            {
                DsPhieuTraHangItems.Add(item);
            }

            DsPhieuTraTotalCount = totalCount;

            var (tongPhieu, tongTien, tongSoLuong) = await _db.GetThongKePhieuTraHangAsync(
                DsPhieuTraSearch,
                DsPhieuTraTuNgay,
                DsPhieuTraDenNgay,
                SelectedPhieuTraTrangThai);

            ThongKeTongPhieuTra = tongPhieu;
            ThongKeTongTienTra = tongTien;
            ThongKeTongSoLuongTra = tongSoLuong;

            StatusMessage = $"Đã tải {DsPhieuTraHangItems.Count:N0} phiếu nhập trả lại.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách phiếu nhập trả lại", ex);
            StatusMessage = "Không thể tải danh sách phiếu trả hàng: " + ex.Message;
        }
        finally
        {
            IsLoadingDsPhieuTra = false;
        }
    }

    [RelayCommand]
    public Task TimKiemDsPhieuTraAsync()
    {
        DsPhieuTraPage = 1;
        return LoadDsPhieuTraAsync();
    }

    [RelayCommand]
    public async Task LamMoiDsPhieuTraAsync()
    {
        DsPhieuTraSearch = "";
        DsPhieuTraTuNgay = null;
        DsPhieuTraDenNgay = null;
        SelectedPhieuTraTrangThai = "TAT_CA";
        OnPropertyChanged(nameof(SelectedPhieuTraTrangThaiHienThi));
        DsPhieuTraPage = 1;
        await LoadDsPhieuTraAsync();
    }

    [RelayCommand]
    public async Task DsPhieuTraTrangTruocAsync()
    {
        if (CanDsPhieuTraTrangTruoc)
        {
            DsPhieuTraPage--;
            await LoadDsPhieuTraAsync();
        }
    }

    [RelayCommand]
    public async Task DsPhieuTraTrangSauAsync()
    {
        if (CanDsPhieuTraTrangSau)
        {
            DsPhieuTraPage++;
            await LoadDsPhieuTraAsync();
        }
    }

    [RelayCommand]
    public async Task XemChiTietPhieuTraAsync(PhieuTraHang? phieu)
    {
        if (phieu is null || OwnerWindow is null)
            return;

        try
        {
            var chiTiets = await _db.GetChiTietPhieuTraHangAsync(phieu.Id);
            var dongHang = string.Join("\n", chiTiets.Select(c =>
                $"• {c.TenSanPham} ({c.MaSanPham}): {c.SoLuong:N0} {c.DonViTinh} × {c.DonGia:N0} đ = {c.ThanhTien:N0} đ" +
                (!string.IsNullOrWhiteSpace(c.LyDoTra) ? $" [Lý do: {c.LyDoTra}]" : "")));

            var thongBao =
                $"MÃ PHIẾU: {phieu.MaPhieuTra}\n" +
                $"Ngày lập: {phieu.ThoiGianTraText}\n" +
                $"Khách hàng: {phieu.KhachHangHienThi} (SĐT: {phieu.SoDienThoaiKhach ?? "—"})\n" +
                $"Hóa đơn gốc: {phieu.HoaDonGocHienThi}\n" +
                $"Kho nhận: {phieu.KhoNhan}\n" +
                $"Trạng thái: {phieu.TrangThaiText}\n" +
                $"Người lập: {phieu.TenNguoiTao ?? "Nhân viên"}\n" +
                $"Ghi chú: {phieu.GhiChu ?? "Không có"}\n\n" +
                $"DANH SÁCH HÀNG TRẢ LẠI ({chiTiets.Count} mặt hàng):\n" +
                (string.IsNullOrWhiteSpace(dongHang) ? "Chưa có chi tiết mặt hàng." : dongHang) + "\n\n" +
                $"TỔNG SỐ LƯỢNG TRẢ: {phieu.TongSoLuongText}\n" +
                $"TỔNG TIỀN HOÀN TRẢ: {phieu.TongTienTraText}";

            await ShowMessageAsync($"Chi tiết phiếu trả lại {phieu.MaPhieuTra}", thongBao);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xem chi tiết phiếu trả hàng", ex);
            await ShowMessageAsync("Lỗi", "Không thể xem chi tiết: " + ex.Message);
        }
    }

    [RelayCommand]
    public async Task InPhieuTraHangAsync(PhieuTraHang? phieu)
    {
        if (phieu is null)
            return;

        await ShowMessageAsync("In phiếu trả hàng",
            $"Chức năng in phiếu trả hàng {phieu.MaPhieuTra} đã sẵn sàng.\nTổng tiền hoàn trả: {phieu.TongTienTraText}");
    }

    [RelayCommand]
    public async Task XuatExcelDsPhieuTraAsync()
    {
        if (OwnerWindow is null || DsPhieuTraHangItems.Count == 0)
        {
            await ShowMessageAsync("Thông báo", "Không có dữ liệu phiếu trả hàng để xuất file.");
            return;
        }

        try
        {
            var saveFile = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách phiếu nhập trả lại",
                SuggestedFileName = $"DanhSachPhieuTra_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx"
            });

            if (saveFile is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PhieuNhapTraLai");

            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã phiếu trả";
            ws.Cell(1, 3).Value = "Ngày lập";
            ws.Cell(1, 4).Value = "Khách hàng";
            ws.Cell(1, 5).Value = "Số điện thoại";
            ws.Cell(1, 6).Value = "Mã hóa đơn gốc";
            ws.Cell(1, 7).Value = "Kho nhận";
            ws.Cell(1, 8).Value = "Tổng SL trả";
            ws.Cell(1, 9).Value = "Tổng tiền trả";
            ws.Cell(1, 10).Value = "Trạng thái";
            ws.Cell(1, 11).Value = "Ghi chú";

            var row = 2;
            foreach (var item in DsPhieuTraHangItems)
            {
                ws.Cell(row, 1).Value = item.SoThuTu;
                ws.Cell(row, 2).Value = item.MaPhieuTra;
                ws.Cell(row, 3).Value = item.ThoiGianTraText;
                ws.Cell(row, 4).Value = item.KhachHangHienThi;
                ws.Cell(row, 5).Value = item.SoDienThoaiKhach ?? "";
                ws.Cell(row, 6).Value = item.HoaDonGocHienThi;
                ws.Cell(row, 7).Value = item.KhoNhan;
                ws.Cell(row, 8).Value = item.TongSoLuong;
                ws.Cell(row, 9).Value = item.TongTienTra;
                ws.Cell(row, 10).Value = item.TrangThaiText;
                ws.Cell(row, 11).Value = item.GhiChu ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();
            await using var stream = await saveFile.OpenWriteAsync();
            workbook.SaveAs(stream);

            StatusMessage = "Xuất file Excel thành công!";
            await ShowMessageAsync("Thành công", "Đã xuất danh sách phiếu nhập trả lại ra file Excel thành công!");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel phiếu trả hàng", ex);
            await ShowMessageAsync("Lỗi", "Không thể xuất file Excel: " + ex.Message);
        }
    }

    // =========================================================
    // SECTION B: THÊM PHIẾU NHẬP TRẢ LẠI (GIAO DIỆN 29)
    // =========================================================

    [ObservableProperty] private string phieuTraMa = "";
    [ObservableProperty] private DateTime? phieuTraNgayTra = DateTime.Now;
    [ObservableProperty] private bool isPhieuTraCalendarOpen;
    [ObservableProperty] private string phieuTraKhoNhan = "Kho Tổng (Kho mặc định)";
    [ObservableProperty] private string phieuTraNhanVien = "";
    [ObservableProperty] private string phieuTraGhiChu = "";

    public string PhieuTraNgayTraText => (PhieuTraNgayTra ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm");

    // Khách hàng
    [ObservableProperty] private string phieuTraCustomerSearch = "";
    [ObservableProperty] private KhachHang? selectedPhieuTraKhachHang;
    public ObservableCollection<KhachHang> PhieuTraCustomerSuggestions { get; } = new();
    public bool HasPhieuTraCustomerSuggestions => PhieuTraCustomerSuggestions.Count > 0;
    public bool HasSelectedPhieuTraKhachHang => SelectedPhieuTraKhachHang is not null;
    public string PhieuTraKhachHangHienThi => SelectedPhieuTraKhachHang is not null ? SelectedPhieuTraKhachHang.TenKhachHang : "Khách lẻ";

    // Hóa đơn gốc
    [ObservableProperty] private string phieuTraHoaDonSearch = "";
    [ObservableProperty] private HoaDonGocGoiY? selectedPhieuTraHoaDon;
    public ObservableCollection<HoaDonGocGoiY> PhieuTraHoaDonSuggestions { get; } = new();
    public bool HasPhieuTraHoaDonSuggestions => PhieuTraHoaDonSuggestions.Count > 0;
    public bool HasSelectedPhieuTraHoaDon => SelectedPhieuTraHoaDon is not null;
    public string PhieuTraHoaDonGocHienThi => SelectedPhieuTraHoaDon is not null ? SelectedPhieuTraHoaDon.MaHoaDon : "Chưa gắn hóa đơn";

    // Tìm kiếm sản phẩm
    [ObservableProperty] private string phieuTraProductSearch = "";
    public ObservableCollection<SanPham> PhieuTraProductSuggestions { get; } = new();
    public bool HasPhieuTraProductSuggestions => PhieuTraProductSuggestions.Count > 0;

    // Chi tiết hàng trả
    public ObservableCollection<ChiTietPhieuTraHang> PhieuTraChiTietItems { get; } = new();
    public bool IsPhieuTraEmpty => PhieuTraChiTietItems.Count == 0;
    public bool HasPhieuTraItems => PhieuTraChiTietItems.Count > 0;
    public int PhieuTraTongMatHang => PhieuTraChiTietItems.Count;
    public decimal PhieuTraTongSoLuong => PhieuTraChiTietItems.Sum(c => c.SoLuong);
    public string PhieuTraTongSoLuongText => $"{PhieuTraTongSoLuong:N0} SP";
    public decimal PhieuTraTongTienTra => PhieuTraChiTietItems.Sum(c => c.ThanhTien);
    public string PhieuTraTongTienTraText => MoneyInput.Format(PhieuTraTongTienTra) + " đ";

    // Hoàn tiền & nguồn quỹ
    public IReadOnlyList<string> PhieuTraPhuongThucOptions { get; } = ["Tiền mặt", "Chuyển khoản", "Trừ công nợ"];
    [ObservableProperty] private string phieuTraPhuongThucHoanTien = "Tiền mặt";
    [ObservableProperty] private bool isSavingPhieuTra;

    public bool CanSavePhieuTra => !IsSavingPhieuTra && PhieuTraChiTietItems.Count > 0;

    public string PhieuTraNguonQuyText => PhieuTraPhuongThucHoanTien switch
    {
        "Tiền mặt" => "💡 Hệ thống sẽ tự động tạo một phiếu chi tiền mặt trong Sổ quỹ.",
        "Chuyển khoản" => "💡 Hệ thống sẽ tự động tạo một phiếu chi chuyển khoản ngân hàng trong Sổ quỹ.",
        "Trừ công nợ" => "💡 Hệ thống sẽ ghi giảm số dư công nợ của khách hàng tương ứng số tiền trả hàng.",
        _ => ""
    };

    partial void OnSelectedPhieuTraKhachHangChanged(KhachHang? value)
    {
        OnPropertyChanged(nameof(HasSelectedPhieuTraKhachHang));
        OnPropertyChanged(nameof(PhieuTraKhachHangHienThi));
    }

    partial void OnSelectedPhieuTraHoaDonChanged(HoaDonGocGoiY? value)
    {
        OnPropertyChanged(nameof(HasSelectedPhieuTraHoaDon));
        OnPropertyChanged(nameof(PhieuTraHoaDonGocHienThi));
    }

    partial void OnPhieuTraPhuongThucHoanTienChanged(string value)
    {
        OnPropertyChanged(nameof(PhieuTraNguonQuyText));
    }

    partial void OnPhieuTraNgayTraChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(PhieuTraNgayTraText));
    }

    public void CapNhatTongKetPhieuTra()
    {
        for (var i = 0; i < PhieuTraChiTietItems.Count; i++)
        {
            PhieuTraChiTietItems[i].SoThuTu = i + 1;
        }

        OnPropertyChanged(nameof(IsPhieuTraEmpty));
        OnPropertyChanged(nameof(HasPhieuTraItems));
        OnPropertyChanged(nameof(PhieuTraTongMatHang));
        OnPropertyChanged(nameof(PhieuTraTongSoLuong));
        OnPropertyChanged(nameof(PhieuTraTongSoLuongText));
        OnPropertyChanged(nameof(PhieuTraTongTienTra));
        OnPropertyChanged(nameof(PhieuTraTongTienTraText));
        OnPropertyChanged(nameof(CanSavePhieuTra));
    }

    /// <summary>
    /// Mở form thêm phiếu nhập trả lại hàng mới.
    /// </summary>
    [RelayCommand]
    public async Task TaoPhieuTraHangMoiAsync()
    {
        await MoFormThemPhieuNhapTraLaiAsync();
    }

    [RelayCommand]
    public async Task MoFormThemPhieuNhapTraLaiAsync()
    {
        PhieuTraMa = await _db.TaoMaPhieuTraMoiAsync();
        PhieuTraNgayTra = DateTime.Now;
        PhieuTraKhoNhan = "Kho Tổng (Kho mặc định)";
        PhieuTraNhanVien = !string.IsNullOrWhiteSpace(HoTenHienThi) ? HoTenHienThi : "Quản trị viên";
        PhieuTraGhiChu = "";

        PhieuTraCustomerSearch = "";
        SelectedPhieuTraKhachHang = null;
        PhieuTraCustomerSuggestions.Clear();

        PhieuTraHoaDonSearch = "";
        SelectedPhieuTraHoaDon = null;
        PhieuTraHoaDonSuggestions.Clear();

        PhieuTraProductSearch = "";
        PhieuTraProductSuggestions.Clear();

        PhieuTraChiTietItems.Clear();
        PhieuTraPhuongThucHoanTien = "Tiền mặt";

        CapNhatTongKetPhieuTra();
        IsThemPhieuNhapTraLaiVisible = true;
    }

    [RelayCommand]
    public void DongFormThemPhieuNhapTraLai()
    {
        IsThemPhieuNhapTraLaiVisible = false;
    }

    [RelayCommand]
    public void MoLichPhieuTra()
    {
        IsPhieuTraCalendarOpen = !IsPhieuTraCalendarOpen;
    }

    /// <summary>
    /// Tìm kiếm hàng hóa gợi ý để thêm vào phiếu trả lại.
    /// </summary>
    public async Task TimKiemHangHoaPhieuTraAsync(string? keyword)
    {
        PhieuTraProductSearch = keyword ?? "";
        PhieuTraProductSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            OnPropertyChanged(nameof(HasPhieuTraProductSuggestions));
            return;
        }

        var kw = keyword.Trim().ToLowerInvariant();
        var matches = SanPhams
            .Where(s => (!string.IsNullOrWhiteSpace(s.MaSanPham) && s.MaSanPham.ToLowerInvariant().Contains(kw))
                     || (!string.IsNullOrWhiteSpace(s.TenSanPham) && s.TenSanPham.ToLowerInvariant().Contains(kw)))
            .Take(8)
            .ToList();

        foreach (var p in matches)
        {
            PhieuTraProductSuggestions.Add(p);
        }

        OnPropertyChanged(nameof(HasPhieuTraProductSuggestions));
        await Task.CompletedTask;
    }

    /// <summary>
    /// Thêm một mặt hàng từ gợi ý danh mục vào bảng chi tiết hàng trả.
    /// </summary>
    [RelayCommand]
    public void ThemSanPhamVaoPhieuTra(SanPham? sp)
    {
        if (sp is null) return;

        PhieuTraProductSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuTraProductSuggestions));
        PhieuTraProductSearch = "";

        var existing = PhieuTraChiTietItems.FirstOrDefault(c => c.SanPhamId == sp.Id);
        if (existing is not null)
        {
            existing.SoLuong++;
        }
        else
        {
            var newItem = new ChiTietPhieuTraHang(sp);
            newItem.PropertyChanged += (_, _) => CapNhatTongKetPhieuTra();
            PhieuTraChiTietItems.Add(newItem);
        }

        CapNhatTongKetPhieuTra();
    }

    /// <summary>
    /// Tìm kiếm gợi ý khách hàng.
    /// </summary>
    public async Task TimKiemKhachHangPhieuTraAsync(string? keyword)
    {
        PhieuTraCustomerSearch = keyword ?? "";
        PhieuTraCustomerSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            OnPropertyChanged(nameof(HasPhieuTraCustomerSuggestions));
            return;
        }

        var kw = keyword.Trim().ToLowerInvariant();
        var matches = KhachHangs
            .Where(k => (!string.IsNullOrWhiteSpace(k.TenKhachHang) && k.TenKhachHang.ToLowerInvariant().Contains(kw))
                     || (!string.IsNullOrWhiteSpace(k.SoDienThoai) && k.SoDienThoai.Contains(kw))
                     || (!string.IsNullOrWhiteSpace(k.MaKhachHang) && k.MaKhachHang.ToLowerInvariant().Contains(kw)))
            .Take(6)
            .ToList();

        foreach (var k in matches)
        {
            PhieuTraCustomerSuggestions.Add(k);
        }

        OnPropertyChanged(nameof(HasPhieuTraCustomerSuggestions));
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ChonKhachHangPhieuTraAsync(KhachHang? kh)
    {
        if (kh is null) return;

        SelectedPhieuTraKhachHang = kh;
        PhieuTraCustomerSearch = kh.TenKhachHang;
        PhieuTraCustomerSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuTraCustomerSuggestions));

        // Tự động tìm kiếm các hóa đơn gần đây của khách này
        await TimKiemHoaDonPhieuTraAsync("");
    }

    [RelayCommand]
    public void XoaChonKhachHangPhieuTra()
    {
        SelectedPhieuTraKhachHang = null;
        PhieuTraCustomerSearch = "";
        PhieuTraCustomerSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuTraCustomerSuggestions));
    }

    /// <summary>
    /// Tìm kiếm gợi ý hóa đơn gốc.
    /// </summary>
    public async Task TimKiemHoaDonPhieuTraAsync(string? keyword)
    {
        PhieuTraHoaDonSearch = keyword ?? "";
        PhieuTraHoaDonSuggestions.Clear();

        try
        {
            uint? khId = SelectedPhieuTraKhachHang?.Id;
            var list = await _db.TimKiemHoaDonGocAsync(keyword, khId, 8);
            foreach (var hd in list)
            {
                PhieuTraHoaDonSuggestions.Add(hd);
            }
        }
        catch { }

        OnPropertyChanged(nameof(HasPhieuTraHoaDonSuggestions));
    }

    [RelayCommand]
    public async Task ChonHoaDonGocPhieuTraAsync(HoaDonGocGoiY? hd)
    {
        if (hd is null) return;

        SelectedPhieuTraHoaDon = hd;
        PhieuTraHoaDonSearch = hd.MaHoaDon;
        PhieuTraHoaDonSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuTraHoaDonSuggestions));

        // Nếu hóa đơn có khách hàng, tự động điền khách hàng
        if (hd.KhachHangId.HasValue && hd.KhachHangId.Value > 0 && SelectedPhieuTraKhachHang is null)
        {
            var kh = KhachHangs.FirstOrDefault(k => k.Id == hd.KhachHangId.Value);
            if (kh is not null)
            {
                SelectedPhieuTraKhachHang = kh;
                PhieuTraCustomerSearch = kh.TenKhachHang;
            }
        }

        // Tự động nạp chi tiết hàng từ hóa đơn gốc
        await NapHangTuHoaDonGocAsync();
    }

    [RelayCommand]
    public void XoaChonHoaDonGocPhieuTra()
    {
        SelectedPhieuTraHoaDon = null;
        PhieuTraHoaDonSearch = "";
        PhieuTraHoaDonSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuTraHoaDonSuggestions));
    }

    /// <summary>
    /// Nạp chi tiết các mặt hàng trong hóa đơn gốc đã chọn vào phiếu trả hàng.
    /// </summary>
    [RelayCommand]
    public async Task NapHangTuHoaDonGocAsync()
    {
        if (SelectedPhieuTraHoaDon is null)
        {
            await ShowMessageAsync("Thông báo", "Vui lòng chọn một hóa đơn gốc trước khi nạp chi tiết.");
            return;
        }

        try
        {
            var chiTiets = await _db.GetChiTietHoaDonChoTraHangAsync(SelectedPhieuTraHoaDon.Id);
            if (chiTiets.Count == 0)
            {
                await ShowMessageAsync("Thông báo", "Hóa đơn này không có dòng sản phẩm nào hoặc đã được trả hết.");
                return;
            }

            PhieuTraChiTietItems.Clear();
            foreach (var item in chiTiets)
            {
                item.PropertyChanged += (_, _) => CapNhatTongKetPhieuTra();
                PhieuTraChiTietItems.Add(item);
            }

            CapNhatTongKetPhieuTra();
            StatusMessage = $"Đã nạp {chiTiets.Count} mặt hàng từ hóa đơn {SelectedPhieuTraHoaDon.MaHoaDon}.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi nạp hàng từ hóa đơn gốc", ex);
            await ShowMessageAsync("Lỗi", "Không thể nạp hàng từ hóa đơn: " + ex.Message);
        }
    }

    [RelayCommand]
    public void TangSoLuongTra(ChiTietPhieuTraHang? item)
    {
        if (item is null) return;
        if (item.HasHoaDonGoc && item.SoLuong >= item.SoLuongToiDa)
        {
            _ = ShowMessageAsync("Thông báo", $"Sản phẩm \"{item.TenSanPham}\" chỉ còn được trả tối đa {item.SoLuongToiDa:N0}.");
            return;
        }
        item.SoLuong++;
        CapNhatTongKetPhieuTra();
    }

    [RelayCommand]
    public void GiamSoLuongTra(ChiTietPhieuTraHang? item)
    {
        if (item is null) return;
        if (item.SoLuong > 1)
        {
            item.SoLuong--;
            CapNhatTongKetPhieuTra();
        }
    }

    [RelayCommand]
    public void XoaDongPhieuTra(ChiTietPhieuTraHang? item)
    {
        if (item is null) return;
        PhieuTraChiTietItems.Remove(item);
        CapNhatTongKetPhieuTra();
    }

    [RelayCommand]
    public void XoaTatCaPhieuTra()
    {
        PhieuTraChiTietItems.Clear();
        CapNhatTongKetPhieuTra();
    }

    /// <summary>
    /// Lưu phiếu nhập trả lại vào cơ sở dữ liệu.
    /// </summary>
    [RelayCommand]
    public async Task LuuPhieuNhapTraLaiAsync()
    {
        if (IsSavingPhieuTra)
            return;

        if (PhieuTraChiTietItems.Count == 0)
        {
            await ShowMessageAsync("Cảnh báo", "Vui lòng thêm ít nhất một mặt hàng cần trả lại vào phiếu.");
            return;
        }

        foreach (var item in PhieuTraChiTietItems)
        {
            if (item.SoLuong <= 0)
            {
                await ShowMessageAsync("Cảnh báo", $"Sản phẩm \"{item.TenSanPham}\" có số lượng không hợp lệ.");
                return;
            }
            if (item.HasHoaDonGoc && item.SoLuong > item.SoLuongToiDa)
            {
                await ShowMessageAsync("Cảnh báo", $"Sản phẩm \"{item.TenSanPham}\" vượt quá số lượng còn được trả ({item.SoLuongToiDa:N0}).");
                return;
            }
        }

        if (PhieuTraPhuongThucHoanTien == "Trừ công nợ" && (SelectedPhieuTraKhachHang is null || SelectedPhieuTraKhachHang.Id == 0))
        {
            await ShowMessageAsync("Cảnh báo", "Phương thức 'Trừ công nợ' chỉ áp dụng khi đã chọn khách hàng cụ thể (không áp dụng cho Khách lẻ).");
            return;
        }

        try
        {
            IsSavingPhieuTra = true;
            StatusMessage = "Đang lưu phiếu nhập trả lại...";

            var phieu = new PhieuTraHang
            {
                MaPhieuTra = PhieuTraMa,
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1,
                KhachHangId = SelectedPhieuTraKhachHang?.Id,
                HoaDonId = SelectedPhieuTraHoaDon?.Id,
                MaHoaDon = SelectedPhieuTraHoaDon?.MaHoaDon,
                ThoiGianTra = PhieuTraNgayTra ?? DateTime.Now,
                KhoNhan = PhieuTraKhoNhan,
                GhiChu = PhieuTraGhiChu,
                TenKhachHang = SelectedPhieuTraKhachHang?.TenKhachHang
            };

            var maPhieuLuu = await _db.LuuPhieuTraHangAsync(
                phieu,
                PhieuTraChiTietItems.ToList(),
                PhieuTraPhuongThucHoanTien);

            StatusMessage = $"Đã lưu thành công phiếu nhập trả lại {maPhieuLuu}!";
            await ShowMessageAsync("Thành công",
                $"Đã lập thành công phiếu nhập trả lại: {maPhieuLuu}!\n" +
                $"Tổng số mặt hàng trả: {phieu.TongSoLuong:N0} SP\n" +
                $"Tổng tiền hoàn trả: {MoneyInput.Format(phieu.TongTienTra)} đ\n" +
                $"Hình thức: {PhieuTraPhuongThucHoanTien}\n" +
                $"Hàng hóa đã được tự động cộng trả lại tồn kho.");

            // Trở về danh sách và tải lại dữ liệu
            IsThemPhieuNhapTraLaiVisible = false;
            await LoadDsPhieuTraAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi lưu phiếu nhập trả lại", ex);
            await ShowMessageAsync("Lỗi", "Không thể lưu phiếu nhập trả lại: " + ex.Message);
        }
        finally
        {
            IsSavingPhieuTra = false;
        }
    }

    /// <summary>
    /// Lưu và in phiếu nhập trả lại.
    /// </summary>
    [RelayCommand]
    public async Task LuuVaInPhieuNhapTraLaiAsync()
    {
        await LuuPhieuNhapTraLaiAsync();
    }
}
