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
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 30: DANH SÁCH PHIẾU XUẤT HÀNG TRẢ LẠI (TRẢ HÀNG NCC)
    // =========================================================

    public bool IsXuatTraNccSelected => SelectedMenu == "Xuất trả NCC";

    [ObservableProperty] private bool isThemPhieuXuatTraVisible;

    public ObservableCollection<PhieuXuatTraHang> DsPhieuXuatTraItems { get; } = new();

    [ObservableProperty] private string dsPhieuXuatTraSearch = "";
    [ObservableProperty] private DateTime? dsPhieuXuatTraTuNgay;
    [ObservableProperty] private DateTime? dsPhieuXuatTraDenNgay;
    [ObservableProperty] private string selectedPhieuXuatTraTrangThai = "TAT_CA";
    [ObservableProperty] private bool isLoadingDsPhieuXuatTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatTraTrangSau))]
    private int dsPhieuXuatTraTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatTraTrangSau))]
    private int dsPhieuXuatTraPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatTraTrangSau))]
    private int dsPhieuXuatTraPageSize = 20;

    public int DsPhieuXuatTraTotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)DsPhieuXuatTraTotalCount / Math.Max(1, DsPhieuXuatTraPageSize)));

    public bool CanDsPhieuXuatTraTrangTruoc => DsPhieuXuatTraPage > 1;
    public bool CanDsPhieuXuatTraTrangSau => DsPhieuXuatTraPage < DsPhieuXuatTraTotalPages;

    public string DsPhieuXuatTraRangeText
    {
        get
        {
            if (DsPhieuXuatTraTotalCount == 0) return "0 phiếu xuất trả";
            var from = (DsPhieuXuatTraPage - 1) * DsPhieuXuatTraPageSize + 1;
            var to = Math.Min(from + DsPhieuXuatTraPageSize - 1, DsPhieuXuatTraTotalCount);
            return $"{from} - {to} trong {DsPhieuXuatTraTotalCount:N0} phiếu";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongPhieuXuatTraText))]
    private int thongKeTongPhieuXuatTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongTienXuatTraText))]
    private decimal thongKeTongTienXuatTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoLuongXuatTraText))]
    private decimal thongKeTongSoLuongXuatTra;

    public string ThongKeTongPhieuXuatTraText => $"{ThongKeTongPhieuXuatTra:N0} phiếu";
    public string ThongKeTongTienXuatTraText => MoneyInput.Format(ThongKeTongTienXuatTra) + " đ";
    public string ThongKeTongSoLuongXuatTraText => $"{ThongKeTongSoLuongXuatTra:N0} SP";

    public IReadOnlyList<string> DsPhieuXuatTraTrangThaiOptions { get; } =
        ["Tất cả trạng thái", "Hoàn thành", "Đã hủy"];

    public string SelectedPhieuXuatTraTrangThaiHienThi
    {
        get => SelectedPhieuXuatTraTrangThai switch
        {
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            _ => "Tất cả trạng thái"
        };
        set
        {
            SelectedPhieuXuatTraTrangThai = value switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Đã hủy" => "DA_HUY",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuXuatTraAsync();
        }
    }

    /// <summary>
    /// Mở màn hình "Nghiệp vụ → Xuất hàng trả lại → Danh sách phiếu xuất hàng trả lại".
    /// </summary>
    [RelayCommand]
    public async Task MoGiaoDienXuatTraNccAsync()
    {
        IsSalesPanelVisible = false;
        IsThemPhieuXuatTraVisible = false;
        SelectedMenu = "Xuất trả NCC";
        DsPhieuXuatTraPage = 1;
        await LoadDsPhieuXuatTraAsync();
    }

    /// <summary>
    /// Tải dữ liệu danh sách phiếu xuất trả NCC và thống kê theo bộ lọc hiện tại.
    /// </summary>
    [RelayCommand]
    public async Task LoadDsPhieuXuatTraAsync()
    {
        if (IsLoadingDsPhieuXuatTra)
            return;

        try
        {
            IsLoadingDsPhieuXuatTra = true;
            StatusMessage = "Đang tải danh sách phiếu xuất trả nhà cung cấp...";

            var (items, totalCount) = await _db.GetPhieuXuatTraPagedAsync(
                DsPhieuXuatTraSearch,
                DsPhieuXuatTraTuNgay,
                DsPhieuXuatTraDenNgay,
                SelectedPhieuXuatTraTrangThai,
                DsPhieuXuatTraPage,
                DsPhieuXuatTraPageSize);

            DsPhieuXuatTraItems.Clear();
            foreach (var item in items)
            {
                DsPhieuXuatTraItems.Add(item);
            }

            DsPhieuXuatTraTotalCount = totalCount;

            var (tongPhieu, tongTien, tongSoLuong) = await _db.GetThongKePhieuXuatTraAsync(
                DsPhieuXuatTraSearch,
                DsPhieuXuatTraTuNgay,
                DsPhieuXuatTraDenNgay,
                SelectedPhieuXuatTraTrangThai);

            ThongKeTongPhieuXuatTra = tongPhieu;
            ThongKeTongTienXuatTra = tongTien;
            ThongKeTongSoLuongXuatTra = tongSoLuong;

            StatusMessage = $"Đã tải {DsPhieuXuatTraItems.Count:N0} phiếu xuất trả nhà cung cấp.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách phiếu xuất trả nhà cung cấp", ex);
            StatusMessage = "Không thể tải danh sách phiếu xuất trả: " + ex.Message;
        }
        finally
        {
            IsLoadingDsPhieuXuatTra = false;
        }
    }

    [RelayCommand]
    public Task TimKiemDsPhieuXuatTraAsync()
    {
        DsPhieuXuatTraPage = 1;
        return LoadDsPhieuXuatTraAsync();
    }

    [RelayCommand]
    public async Task LamMoiDsPhieuXuatTraAsync()
    {
        DsPhieuXuatTraSearch = "";
        DsPhieuXuatTraTuNgay = null;
        DsPhieuXuatTraDenNgay = null;
        SelectedPhieuXuatTraTrangThai = "TAT_CA";
        OnPropertyChanged(nameof(SelectedPhieuXuatTraTrangThaiHienThi));
        DsPhieuXuatTraPage = 1;
        await LoadDsPhieuXuatTraAsync();
    }

    [RelayCommand]
    public async Task DsPhieuXuatTraTrangTruocAsync()
    {
        if (CanDsPhieuXuatTraTrangTruoc)
        {
            DsPhieuXuatTraPage--;
            await LoadDsPhieuXuatTraAsync();
        }
    }

    [RelayCommand]
    public async Task DsPhieuXuatTraTrangSauAsync()
    {
        if (CanDsPhieuXuatTraTrangSau)
        {
            DsPhieuXuatTraPage++;
            await LoadDsPhieuXuatTraAsync();
        }
    }

    /// <summary>
    /// Mở xem chi tiết một phiếu xuất hàng trả lại NCC.
    /// </summary>
    [RelayCommand]
    public async Task XemChiTietPhieuXuatTraAsync(PhieuXuatTraHang? phieu)
    {
        if (phieu is null || OwnerWindow is null)
            return;

        try
        {
            var chiTiets = await _db.GetChiTietPhieuXuatTraAsync(phieu.Id);
            var dongHang = string.Join("\n", chiTiets.Select(c =>
                $"• {c.TenSanPham} ({c.MaSanPham}): {c.SoLuong:N0} {c.DonViTinh} × {c.DonGia:N0} đ = {c.ThanhTien:N0} đ" +
                (!string.IsNullOrWhiteSpace(c.LyDoXuatTra) ? $" [Lý do: {c.LyDoXuatTra}]" : "")));

            var thongBao =
                $"MÃ PHIẾU XUẤT TRẢ: {phieu.MaPhieuXuatTra}\n" +
                $"Thời gian xuất trả: {phieu.ThoiGianXuatTraText}\n" +
                $"Nhà cung cấp: {phieu.NhaCungCapHienThi} (Mã: {phieu.MaNhaCungCap ?? "—"}, SĐT: {phieu.SoDienThoaiNcc ?? "—"})\n" +
                $"Phiếu nhập gốc: {phieu.PhieuNhapGocHienThi}\n" +
                $"Kho xuất: {phieu.KhoXuat}\n" +
                $"Trạng thái: {phieu.TrangThaiText}\n" +
                $"Người lập: {phieu.TenNguoiTao ?? "Nhân viên"}\n" +
                $"Ghi chú: {phieu.GhiChu ?? "Không có"}\n\n" +
                $"DANH SÁCH MẶT HÀNG XUẤT TRẢ ({chiTiets.Count} mặt hàng):\n" +
                (string.IsNullOrWhiteSpace(dongHang) ? "Chưa có chi tiết mặt hàng." : dongHang) + "\n\n" +
                $"TỔNG SỐ LƯỢNG: {phieu.TongSoLuongText}\n" +
                $"TỔNG TIỀN HOÀN TRẢ TỪ NCC: {phieu.TongTienTraText}";

            await ShowMessageAsync($"Chi tiết phiếu xuất trả {phieu.MaPhieuXuatTra}", thongBao);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xem chi tiết phiếu xuất trả NCC", ex);
            await ShowMessageAsync("Lỗi", "Không thể xem chi tiết: " + ex.Message);
        }
    }

    /// <summary>
    /// In phiếu xuất hàng trả lại NCC.
    /// </summary>
    [RelayCommand]
    public async Task InPhieuXuatTraAsync(PhieuXuatTraHang? phieu)
    {
        if (phieu is null)
            return;

        await ShowMessageAsync("In phiếu xuất trả NCC",
            $"Chức năng in phiếu xuất hàng trả lại NCC {phieu.MaPhieuXuatTra} đã sẵn sàng.\nTổng tiền hoàn trả từ NCC: {phieu.TongTienTraText}");
    }

    /// <summary>
    /// Xuất danh sách phiếu xuất trả NCC ra file Excel.
    /// </summary>
    [RelayCommand]
    public async Task XuatExcelDsPhieuXuatTraAsync()
    {
        if (OwnerWindow is null || DsPhieuXuatTraItems.Count == 0)
        {
            await ShowMessageAsync("Thông báo", "Không có dữ liệu phiếu xuất trả NCC để xuất file.");
            return;
        }

        try
        {
            var saveFile = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách phiếu xuất hàng trả lại NCC",
                SuggestedFileName = $"DanhSachPhieuXuatTraNCC_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx"
            });

            if (saveFile is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PhieuXuatTraNCC");

            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã phiếu xuất trả";
            ws.Cell(1, 3).Value = "Thời gian";
            ws.Cell(1, 4).Value = "Nhà cung cấp";
            ws.Cell(1, 5).Value = "Số điện thoại NCC";
            ws.Cell(1, 6).Value = "Phiếu nhập gốc";
            ws.Cell(1, 7).Value = "Kho xuất";
            ws.Cell(1, 8).Value = "Tổng SL trả";
            ws.Cell(1, 9).Value = "Tổng tiền trả";
            ws.Cell(1, 10).Value = "Trạng thái";
            ws.Cell(1, 11).Value = "Ghi chú";

            var row = 2;
            foreach (var item in DsPhieuXuatTraItems)
            {
                ws.Cell(row, 1).Value = item.SoThuTu;
                ws.Cell(row, 2).Value = item.MaPhieuXuatTra;
                ws.Cell(row, 3).Value = item.ThoiGianXuatTraText;
                ws.Cell(row, 4).Value = item.NhaCungCapHienThi;
                ws.Cell(row, 5).Value = item.SoDienThoaiNcc ?? "";
                ws.Cell(row, 6).Value = item.PhieuNhapGocHienThi;
                ws.Cell(row, 7).Value = item.KhoXuat;
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
            await ShowMessageAsync("Thành công", "Đã xuất danh sách phiếu xuất hàng trả lại NCC ra file Excel thành công!");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel phiếu xuất trả NCC", ex);
            await ShowMessageAsync("Lỗi", "Không thể xuất file Excel: " + ex.Message);
        }
    }

    // =========================================================
    // GIAO DIỆN 31: THÊM PHIẾU XUẤT HÀNG TRẢ LẠI (TRẢ HÀNG NCC)
    // =========================================================

    public ObservableCollection<ChiTietPhieuXuatTraHang> PhieuXuatTraChiTietItems { get; } = new();
    public ObservableCollection<SanPham> PhieuXuatTraProductSuggestions { get; } = new();
    public ObservableCollection<NhaCungCap> PhieuXuatTraSupplierSuggestions { get; } = new();
    public ObservableCollection<PhieuNhapGocGoiY> PhieuXuatTraPhieuNhapGocSuggestions { get; } = new();

    public IReadOnlyList<string> PhuongThucHoanTienNccOptions { get; } =
        ["Trừ công nợ", "Tiền mặt", "Chuyển khoản"];

    [ObservableProperty] private string phieuXuatTraMa = "";
    [ObservableProperty] private DateTime? phieuXuatTraNgayXuat = DateTime.Now;
    [ObservableProperty] private string phieuXuatTraKhoXuat = "Kho Tổng (Kho mặc định)";
    [ObservableProperty] private string phieuXuatTraGhiChu = "";
    [ObservableProperty] private string phieuXuatTraNhanVien = "";
    [ObservableProperty] private string phieuXuatTraProductSearch = "";
    [ObservableProperty] private string phieuXuatTraNccSearch = "";
    [ObservableProperty] private string phieuXuatTraPhieuNhapGocSearch = "";
    [ObservableProperty] private string phieuXuatTraPhuongThucHoanTien = "Trừ công nợ";
    [ObservableProperty] private NhaCungCap? selectedPhieuXuatTraNcc;
    [ObservableProperty] private PhieuNhapGocGoiY? selectedPhieuXuatTraPhieuNhapGoc;
    [ObservableProperty] private bool isSavingPhieuXuatTra;

    public bool HasSelectedPhieuXuatTraNcc => SelectedPhieuXuatTraNcc is not null;
    public bool HasSelectedPhieuXuatTraPhieuNhapGoc => SelectedPhieuXuatTraPhieuNhapGoc is not null;
    public bool HasPhieuXuatTraSuggestions => PhieuXuatTraProductSuggestions.Count > 0;
    public bool HasPhieuXuatTraSupplierSuggestions => PhieuXuatTraSupplierSuggestions.Count > 0;
    public bool HasPhieuXuatTraPhieuNhapGocSuggestions => PhieuXuatTraPhieuNhapGocSuggestions.Count > 0;
    public bool HasPhieuXuatTraItems => PhieuXuatTraChiTietItems.Count > 0;
    public bool IsPhieuXuatTraEmpty => PhieuXuatTraChiTietItems.Count == 0;

    public string PhieuXuatTraNgayXuatText =>
        PhieuXuatTraNgayXuat?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "";

    public decimal PhieuXuatTraTongSoLuong =>
        PhieuXuatTraChiTietItems.Sum(c => c.SoLuong);

    public decimal PhieuXuatTraTongTien =>
        PhieuXuatTraChiTietItems.Sum(c => c.ThanhTien);

    public string PhieuXuatTraTongSoLuongText =>
        $"{PhieuXuatTraTongSoLuong:N0} SP";

    public string PhieuXuatTraTongTienText =>
        MoneyInput.Format(PhieuXuatTraTongTien) + " đ";

    public string PhieuXuatTraPhuongThucGiaiThich => PhieuXuatTraPhuongThucHoanTien switch
    {
        "Trừ công nợ" => "💡 Giảm trừ công nợ phải trả Nhà cung cấp trong sổ cái công nợ",
        "Chuyển khoản" => "💡 Nhà cung cấp chuyển khoản hoàn tiền vào tài khoản ngân hàng (ghi phiếu thu)",
        _ => "💡 Nhà cung cấp hoàn trả tiền mặt vào quỹ (ghi phiếu thu)"
    };

    partial void OnPhieuXuatTraPhuongThucHoanTienChanged(string value) =>
        OnPropertyChanged(nameof(PhieuXuatTraPhuongThucGiaiThich));

    partial void OnSelectedPhieuXuatTraNccChanged(NhaCungCap? value)
    {
        OnPropertyChanged(nameof(HasSelectedPhieuXuatTraNcc));
        if (value is not null)
        {
            PhieuXuatTraNccSearch = $"{value.TenNhaCungCap} ({value.MaNhaCungCap})";
            PhieuXuatTraSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuXuatTraSupplierSuggestions));

            // Tự động tìm phiếu nhập của NCC này
            _ = TimKiemPhieuNhapGocChoXuatTraAsync("");
        }
    }

    partial void OnSelectedPhieuXuatTraPhieuNhapGocChanged(PhieuNhapGocGoiY? value)
    {
        OnPropertyChanged(nameof(HasSelectedPhieuXuatTraPhieuNhapGoc));
        if (value is not null)
        {
            PhieuXuatTraPhieuNhapGocSearch = value.HienThi;
            PhieuXuatTraPhieuNhapGocSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuXuatTraPhieuNhapGocSuggestions));

            // Nếu chưa chọn NCC hoặc NCC khác, tự động chọn NCC theo phiếu nhập
            if (value.NhaCungCapId.HasValue && (SelectedPhieuXuatTraNcc is null || SelectedPhieuXuatTraNcc.Id != value.NhaCungCapId.Value))
            {
                SelectedPhieuXuatTraNcc = new NhaCungCap
                {
                    Id = value.NhaCungCapId.Value,
                    MaNhaCungCap = value.MaNhaCungCap ?? "",
                    TenNhaCungCap = value.TenNhaCungCap ?? "",
                    SoDienThoai = value.SoDienThoaiNcc
                };
            }
        }
    }

    /// <summary>
    /// Mở màn hình Thêm phiếu xuất hàng trả lại NCC.
    /// </summary>
    [RelayCommand]
    public async Task MoFormThemPhieuXuatTraAsync()
    {
        IsSalesPanelVisible = false;
        SelectedMenu = "Xuất trả NCC";
        IsThemPhieuXuatTraVisible = true;
        await LamMoiFormThemPhieuXuatTraAsync();
    }

    /// <summary>
    /// Đóng form Thêm phiếu xuất trả NCC quay về danh sách.
    /// </summary>
    [RelayCommand]
    public void DongFormThemPhieuXuatTra()
    {
        IsThemPhieuXuatTraVisible = false;
    }

    /// <summary>
    /// Khởi tạo lại dữ liệu form Thêm phiếu xuất trả NCC.
    /// </summary>
    [RelayCommand]
    public async Task LamMoiFormThemPhieuXuatTraAsync()
    {
        PhieuXuatTraMa = await _db.TaoMaPhieuXuatTraMoiAsync();
        PhieuXuatTraNgayXuat = DateTime.Now;
        PhieuXuatTraKhoXuat = "Kho Tổng (Kho mặc định)";
        PhieuXuatTraNhanVien = Session.CurrentUser?.HoTen ?? "Quản trị viên";
        PhieuXuatTraGhiChu = "";
        PhieuXuatTraProductSearch = "";
        PhieuXuatTraNccSearch = "";
        PhieuXuatTraPhieuNhapGocSearch = "";
        PhieuXuatTraPhuongThucHoanTien = "Trừ công nợ";

        SelectedPhieuXuatTraNcc = null;
        SelectedPhieuXuatTraPhieuNhapGoc = null;

        BoDangKyChiTietPhieuXuatTra();
        PhieuXuatTraChiTietItems.Clear();
        PhieuXuatTraProductSuggestions.Clear();
        PhieuXuatTraSupplierSuggestions.Clear();
        PhieuXuatTraPhieuNhapGocSuggestions.Clear();

        CapNhatTongKetPhieuXuatTra();
        StatusMessage = $"Đã khởi tạo phiếu xuất trả NCC {PhieuXuatTraMa}.";
    }

    private void DangKyChiTietPhieuXuatTra()
    {
        foreach (var item in PhieuXuatTraChiTietItems)
        {
            item.PropertyChanged -= ChiTietPhieuXuatTra_PropertyChanged;
            item.PropertyChanged += ChiTietPhieuXuatTra_PropertyChanged;
        }
    }

    private void BoDangKyChiTietPhieuXuatTra()
    {
        foreach (var item in PhieuXuatTraChiTietItems)
        {
            item.PropertyChanged -= ChiTietPhieuXuatTra_PropertyChanged;
        }
    }

    private void ChiTietPhieuXuatTra_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChiTietPhieuXuatTraHang.SoLuong) 
            or nameof(ChiTietPhieuXuatTraHang.DonGia) 
            or nameof(ChiTietPhieuXuatTraHang.ThanhTien))
        {
            CapNhatTongKetPhieuXuatTra();
        }
    }

    private void CapNhatTongKetPhieuXuatTra()
    {
        OnPropertyChanged(nameof(PhieuXuatTraTongSoLuong));
        OnPropertyChanged(nameof(PhieuXuatTraTongTien));
        OnPropertyChanged(nameof(PhieuXuatTraTongSoLuongText));
        OnPropertyChanged(nameof(PhieuXuatTraTongTienText));
        OnPropertyChanged(nameof(HasPhieuXuatTraItems));
        OnPropertyChanged(nameof(IsPhieuXuatTraEmpty));
    }

    [RelayCommand]
    public async Task TimKiemSanPhamChoXuatTraAsync(string? keyword)
    {
        var kw = keyword ?? PhieuXuatTraProductSearch ?? "";
        PhieuXuatTraProductSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(kw))
        {
            OnPropertyChanged(nameof(HasPhieuXuatTraSuggestions));
            return;
        }

        try
        {
            var products = await _db.GetSanPhamAsync(kw.Trim());
            foreach (var p in products.Take(15))
            {
                PhieuXuatTraProductSuggestions.Add(p);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tìm sản phẩm cho xuất trả NCC", ex);
        }

        OnPropertyChanged(nameof(HasPhieuXuatTraSuggestions));
    }

    [RelayCommand]
    public void ThemSanPhamVaoPhieuXuatTra(SanPham? product)
    {
        if (product is null)
            return;

        var existing = PhieuXuatTraChiTietItems.FirstOrDefault(c => c.SanPhamId == product.Id);
        if (existing is not null)
        {
            existing.SoLuong += 1;
            StatusMessage = $"Đã tăng số lượng trả {product.TenSanPham} lên {existing.SoLuong:N0}";
        }
        else
        {
            var newItem = new ChiTietPhieuXuatTraHang(product)
            {
                SoThuTu = PhieuXuatTraChiTietItems.Count + 1
            };
            newItem.PropertyChanged += ChiTietPhieuXuatTra_PropertyChanged;
            PhieuXuatTraChiTietItems.Add(newItem);
            StatusMessage = $"Đã thêm {product.TenSanPham} vào phiếu xuất trả NCC";
        }

        PhieuXuatTraProductSearch = "";
        PhieuXuatTraProductSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuXuatTraSuggestions));
        CapNhatTongKetPhieuXuatTra();
    }

    [RelayCommand]
    public async Task TimKiemNhaCungCapChoXuatTraAsync(string? keyword)
    {
        var kw = keyword ?? PhieuXuatTraNccSearch ?? "";
        PhieuXuatTraSupplierSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(kw))
        {
            OnPropertyChanged(nameof(HasPhieuXuatTraSupplierSuggestions));
            return;
        }

        try
        {
            var (suppliers, _) = await _db.GetNhaCungCapPagedAsync(
                new BoLocNhaCungCap { TimKiem = kw.Trim() },
                1,
                10);
            foreach (var s in suppliers)
            {
                PhieuXuatTraSupplierSuggestions.Add(s);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tìm nhà cung cấp cho xuất trả NCC", ex);
        }

        OnPropertyChanged(nameof(HasPhieuXuatTraSupplierSuggestions));
    }

    [RelayCommand]
    public void ChonNhaCungCapChoXuatTra(NhaCungCap? ncc)
    {
        if (ncc is null)
            return;

        SelectedPhieuXuatTraNcc = ncc;
    }

    [RelayCommand]
    public void XoaNhaCungCapDaChonChoXuatTra()
    {
        SelectedPhieuXuatTraNcc = null;
        PhieuXuatTraNccSearch = "";
        PhieuXuatTraSupplierSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuXuatTraSupplierSuggestions));
    }

    [RelayCommand]
    public async Task TimKiemPhieuNhapGocChoXuatTraAsync(string? keyword)
    {
        var kw = keyword ?? PhieuXuatTraPhieuNhapGocSearch ?? "";
        PhieuXuatTraPhieuNhapGocSuggestions.Clear();

        try
        {
            var items = await _db.TimKiemPhieuNhapGocChoXuatTraAsync(kw, SelectedPhieuXuatTraNcc?.Id);
            foreach (var item in items)
            {
                PhieuXuatTraPhieuNhapGocSuggestions.Add(item);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tìm kiếm phiếu nhập gốc", ex);
        }

        OnPropertyChanged(nameof(HasPhieuXuatTraPhieuNhapGocSuggestions));
    }

    [RelayCommand]
    public void ChonPhieuNhapGocChoXuatTra(PhieuNhapGocGoiY? phieuNhap)
    {
        if (phieuNhap is null)
            return;

        SelectedPhieuXuatTraPhieuNhapGoc = phieuNhap;
    }

    [RelayCommand]
    public void XoaPhieuNhapGocDaChonChoXuatTra()
    {
        SelectedPhieuXuatTraPhieuNhapGoc = null;
        PhieuXuatTraPhieuNhapGocSearch = "";
        PhieuXuatTraPhieuNhapGocSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuXuatTraPhieuNhapGocSuggestions));
    }

    /// <summary>
    /// Nạp toàn bộ các mặt hàng từ phiếu nhập gốc đã chọn vào bảng chi tiết xuất trả.
    /// </summary>
    [RelayCommand]
    public async Task NapHangTuPhieuNhapGocAsync()
    {
        if (SelectedPhieuXuatTraPhieuNhapGoc is null)
        {
            await ShowMessageAsync("Thông báo", "Vui lòng chọn một phiếu nhập gốc trước khi nạp mặt hàng.");
            return;
        }

        try
        {
            var chiTiets = await _db.GetChiTietPhieuNhapChoXuatTraAsync(SelectedPhieuXuatTraPhieuNhapGoc.Id);
            if (chiTiets.Count == 0)
            {
                await ShowMessageAsync("Thông báo", "Phiếu nhập này không có mặt hàng nào hoặc toàn bộ mặt hàng đã được xuất trả.");
                return;
            }

            BoDangKyChiTietPhieuXuatTra();
            PhieuXuatTraChiTietItems.Clear();

            for (var i = 0; i < chiTiets.Count; i++)
            {
                chiTiets[i].SoThuTu = i + 1;
                chiTiets[i].PropertyChanged += ChiTietPhieuXuatTra_PropertyChanged;
                PhieuXuatTraChiTietItems.Add(chiTiets[i]);
            }

            CapNhatTongKetPhieuXuatTra();
            StatusMessage = $"Đã nạp {chiTiets.Count} mặt hàng từ phiếu nhập {SelectedPhieuXuatTraPhieuNhapGoc.MaPhieuNhap}.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi nạp hàng từ phiếu nhập gốc", ex);
            await ShowMessageAsync("Lỗi", "Không thể nạp mặt hàng từ phiếu nhập gốc: " + ex.Message);
        }
    }

    [RelayCommand]
    public void TangSoLuongXuatTra(ChiTietPhieuXuatTraHang? item)
    {
        if (item is null)
            return;

        if (item.HasPhieuNhapGoc && item.SoLuong >= item.SoLuongToiDa)
        {
            StatusMessage = $"Mặt hàng {item.TenSanPham} chỉ được trả tối đa {item.SoLuongToiDa:N0}.";
            return;
        }

        item.SoLuong += 1;
        CapNhatTongKetPhieuXuatTra();
    }

    [RelayCommand]
    public void GiamSoLuongXuatTra(ChiTietPhieuXuatTraHang? item)
    {
        if (item is null)
            return;

        if (item.SoLuong > 1)
        {
            item.SoLuong -= 1;
        }
        CapNhatTongKetPhieuXuatTra();
    }

    [RelayCommand]
    public void XoaDongPhieuXuatTra(ChiTietPhieuXuatTraHang? item)
    {
        if (item is null)
            return;

        item.PropertyChanged -= ChiTietPhieuXuatTra_PropertyChanged;
        PhieuXuatTraChiTietItems.Remove(item);

        for (var i = 0; i < PhieuXuatTraChiTietItems.Count; i++)
        {
            PhieuXuatTraChiTietItems[i].SoThuTu = i + 1;
        }

        CapNhatTongKetPhieuXuatTra();
        StatusMessage = $"Đã xóa {item.TenSanPham} khỏi phiếu xuất trả NCC.";
    }

    /// <summary>
    /// Lưu và hoàn tất phiếu xuất hàng trả lại NCC:
    /// Kiểm tra hợp lệ -> Transaction ACID ghi nhận phiếu, cập nhật kho, công nợ/sổ quỹ.
    /// </summary>
    [RelayCommand]
    public async Task LuuPhieuXuatTraHangAsync()
    {
        if (IsSavingPhieuXuatTra)
            return;

        if (PhieuXuatTraChiTietItems.Count == 0)
        {
            await ShowMessageAsync("Thông báo", "Phiếu xuất trả NCC phải có ít nhất một mặt hàng.");
            return;
        }

        foreach (var item in PhieuXuatTraChiTietItems)
        {
            if (item.SoLuong <= 0)
            {
                await ShowMessageAsync("Thông báo", $"Mặt hàng \"{item.TenSanPham}\" có số lượng trả không hợp lệ (phải > 0).");
                return;
            }
            if (item.HasPhieuNhapGoc && item.SoLuong > item.SoLuongToiDa)
            {
                await ShowMessageAsync("Thông báo", $"Mặt hàng \"{item.TenSanPham}\" vượt quá số lượng còn được phép trả ({item.SoLuongToiDa:N0}).");
                return;
            }
        }

        var xacNhan = await ShowConfirmationAsync(
            "Xác nhận lưu phiếu xuất trả NCC",
            $"Bạn có chắc muốn lưu phiếu xuất trả NCC {PhieuXuatTraMa}?\n" +
            $"• Tổng số lượng xuất trả: {PhieuXuatTraTongSoLuong:N0} SP\n" +
            $"• Tổng tiền hoàn trả: {PhieuXuatTraTongTienText}\n" +
            $"• Phương thức: {PhieuXuatTraPhuongThucHoanTien}\n" +
            "Hàng hóa sẽ được xuất trừ kho và công nợ/sổ quỹ được cập nhật.");

        if (!xacNhan)
            return;

        try
        {
            IsSavingPhieuXuatTra = true;
            StatusMessage = "Đang lưu phiếu xuất hàng trả lại NCC...";

            var phieu = new PhieuXuatTraHang
            {
                MaPhieuXuatTra = PhieuXuatTraMa,
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1,
                NhaCungCapId = SelectedPhieuXuatTraNcc?.Id,
                TenNhaCungCap = SelectedPhieuXuatTraNcc?.TenNhaCungCap,
                MaNhaCungCap = SelectedPhieuXuatTraNcc?.MaNhaCungCap,
                SoDienThoaiNcc = SelectedPhieuXuatTraNcc?.SoDienThoai,
                PhieuNhapGocId = SelectedPhieuXuatTraPhieuNhapGoc?.Id,
                MaPhieuNhapGoc = SelectedPhieuXuatTraPhieuNhapGoc?.MaPhieuNhap,
                ThoiGianXuatTra = PhieuXuatTraNgayXuat ?? DateTime.Now,
                KhoXuat = PhieuXuatTraKhoXuat,
                TongSoLuong = PhieuXuatTraTongSoLuong,
                TongTienTra = PhieuXuatTraTongTien,
                GhiChu = PhieuXuatTraGhiChu,
                TrangThai = "HOAN_THANH",
                TenNguoiTao = PhieuXuatTraNhanVien
            };

            var chiTiets = PhieuXuatTraChiTietItems.ToList();
            var maDaLuu = await _db.LuuPhieuXuatTraHangAsync(phieu, chiTiets, PhieuXuatTraPhuongThucHoanTien);

            StatusMessage = $"Đã lưu thành công phiếu xuất hàng trả lại NCC {maDaLuu}!";
            await ShowMessageAsync("Thành công", $"Phiếu xuất hàng trả lại NCC {maDaLuu} đã được lưu thành công!\nKho hàng và số liệu công nợ/sổ quỹ đã được cập nhật.");

            // Đóng form lập phiếu và tải lại danh sách
            IsThemPhieuXuatTraVisible = false;
            DsPhieuXuatTraPage = 1;
            await LoadDsPhieuXuatTraAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi lưu phiếu xuất trả NCC", ex);
            await ShowMessageAsync("Lỗi", "Không thể lưu phiếu xuất trả NCC: " + ex.Message);
        }
        finally
        {
            IsSavingPhieuXuatTra = false;
        }
    }
}
