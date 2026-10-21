using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho danh sách phiếu hợp nhất của một nhóm nghiệp vụ (Nhập hoặc Xuất).
/// Một instance cho nhóm "NHAP", một instance cho nhóm "XUAT" nhờ vậy hai danh sách
/// hoàn toàn độc lập về bộ lọc và phân trang.
/// </summary>
public sealed partial class PhieuHopListViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly string _nhom;
    private CancellationTokenSource? _searchCts;
    private int _loadVersion;

    public PhieuHopListViewModel(DatabaseService db, string nhom)
    {
        _db = db;
        _nhom = nhom;
    }

    /// <summary>Mở màn tạo phiếu của nhóm này (do MainWindowViewModel gắn vào).</summary>
    public Func<Task>? MoTaoPhieu { get; set; }

    /// <summary>Hiển thị hộp thoại thông báo (do MainWindowViewModel gắn vào).</summary>
    public Func<string, string, Task>? HienThongBao { get; set; }

    /// <summary>Lấy cửa sổ chủ để mở hộp thoại lưu file (do MainWindowViewModel gắn vào).</summary>
    public Func<Window?>? LayCuaSoChinh { get; set; }

    /// <summary>Thanh toán công nợ cho phiếu nhập từ NCC (do MainWindowViewModel gắn vào).</summary>
    public Func<PhieuHopRow, Task>? ThanhToanPhieu { get; set; }

    /// <summary>In phiếu nhập đã lưu (do MainWindowViewModel gắn vào).</summary>
    public Func<PhieuHopRow, Task>? InPhieu { get; set; }

    public string Nhom => _nhom;

    public bool IsNhap => string.Equals(_nhom, "NHAP", StringComparison.OrdinalIgnoreCase);

    public string TieuDe => IsNhap ? "Danh sách phiếu nhập" : "Danh sách phiếu xuất";

    public string PhuDe => IsNhap
        ? "Toàn bộ phiếu nhập từ nhà cung cấp, nhập nội bộ, điều chỉnh kiểm kê, khuyến mãi và thu hồi"
        : "Toàn bộ phiếu xuất hao hụt, xuất nội bộ, xuất chuyển kho và xuất trả nhà cung cấp";

    public string NhanTaoPhieu => IsNhap ? "＋  Thêm phiếu nhập" : "＋  Thêm phiếu xuất";

    public string TenFileExcel => IsNhap ? "DanhSachPhieuNhap" : "DanhSachPhieuXuat";

    // ============================================================
    // Dữ liệu hiển thị
    // ============================================================

    public ObservableCollection<PhieuHopRow> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool isLoading;

    [ObservableProperty] private string search = "";

    [ObservableProperty] private DateTime? tuNgay;

    [ObservableProperty] private DateTime? denNgay;

    /// <summary>Mã loại phiếu đang lọc (TAT_CA = không lọc).</summary>
    [ObservableProperty] private string selectedLoaiPhieu = "TAT_CA";

    /// <summary>Mã trạng thái đang lọc (TAT_CA = không lọc).</summary>
    [ObservableProperty] private string selectedTrangThai = "TAT_CA";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    private int totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    private int page = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    private int pageSize = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongPhieuText))]
    private int thongKeTongPhieu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongGiaTriText))]
    private decimal thongKeTongGiaTri;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoLuongText))]
    private decimal thongKeTongSoLuong;

    public string ThongKeTongPhieuText => $"{ThongKeTongPhieu:N0} phiếu";

    public string ThongKeTongGiaTriText => MoneyInput.Format(ThongKeTongGiaTri) + " đ";

    public string ThongKeTongSoLuongText => $"{ThongKeTongSoLuong:N0} SP";

    public int TotalPages =>
        Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));

    public bool CanTrangTruoc => Page > 1;

    public bool CanTrangSau => Page < TotalPages;

    public bool HasData => Items.Count > 0;

    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public string RangeText
    {
        get
        {
            if (TotalCount == 0)
                return IsNhap ? "0 phiếu nhập" : "0 phiếu xuất";

            var from = (Page - 1) * PageSize + 1;
            var to = Math.Min(from + PageSize - 1, TotalCount);
            return $"{from} - {to} trong {TotalCount:N0} phiếu";
        }
    }

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    // ============================================================
    // Bộ lọc
    // ============================================================

    /// <summary>Các lựa chọn loại phiếu (hiển thị theo tên, lưu theo mã).</summary>
    public IReadOnlyList<string> LoaiOptions { get; } = BuildLoaiOptions();

    public IReadOnlyList<string> TrangThaiOptions { get; } =
        ["Tất cả trạng thái", "Hoàn thành", "Đã hủy", "Lưu tạm", "Chưa thanh toán"];

    private static IReadOnlyList<string> BuildLoaiOptions()
    {
        var options = new List<string> { "Tất cả loại" };
        options.AddRange(PhieuHopLabels.DanhSachLoaiNhap.Select(x => x.Ten));
        options.AddRange(PhieuHopLabels.DanhSachLoaiXuatLoc.Select(x => x.Ten));
        return options;
    }

    /// <summary>Hiển thị loại phiếu đang lọc trên ComboBox.</summary>
    public string SelectedLoaiPhieuHienThi
    {
        get
        {
            if (string.Equals(SelectedLoaiPhieu, "TAT_CA", StringComparison.OrdinalIgnoreCase))
                return "Tất cả loại";

            return PhieuHopLabels.TenLoai(SelectedLoaiPhieu);
        }
        set
        {
            SelectedLoaiPhieu = ResolveLoaiCode(value);
            OnPropertyChanged();
            Page = 1;
            _ = LoadAsync();
        }
    }

    private string ResolveLoaiCode(string? tenHienThi)
    {
        if (string.IsNullOrWhiteSpace(tenHienThi) || tenHienThi == "Tất cả loại")
            return "TAT_CA";

        foreach (var (ma, ten) in PhieuHopLabels.DanhSachLoaiNhap)
        {
            if (ten == tenHienThi)
                return ma;
        }

        foreach (var (ma, ten) in PhieuHopLabels.DanhSachLoaiXuatLoc)
        {
            if (ten == tenHienThi)
                return ma;
        }

        return "TAT_CA";
    }

    /// <summary>Hiển thị trạng thái đang lọc trên ComboBox.</summary>
    public string SelectedTrangThaiHienThi
    {
        get => SelectedTrangThai switch
        {
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            "LUU_TAM" => "Lưu tạm",
            "CHUA_THANH_TOAN" => "Chưa thanh toán",
            _ => "Tất cả trạng thái"
        };
        set
        {
            SelectedTrangThai = value switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Đã hủy" => "DA_HUY",
                "Lưu tạm" => "LUU_TAM",
                "Chưa thanh toán" => "CHUA_THANH_TOAN",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            Page = 1;
            _ = LoadAsync();
        }
    }

    partial void OnSearchChanged(string value)
    {
        _ = DebounceLoadAsync();
    }

    private async Task DebounceLoadAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            Page = 1;
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp - huỷ lần chờ trước.
        }
    }

    partial void OnTuNgayChanged(DateTime? value)
    {
        Page = 1;
        _ = LoadAsync();
    }

    partial void OnDenNgayChanged(DateTime? value)
    {
        Page = 1;
        _ = LoadAsync();
    }

    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanTrangTruoc));
        OnPropertyChanged(nameof(CanTrangSau));
        OnPropertyChanged(nameof(RangeText));
        _ = LoadAsync();
    }

    partial void OnPageSizeChanged(int value)
    {
        Page = 1;
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(RangeText));
        _ = LoadAsync();
    }

    // ============================================================
    // Lệnh
    // ============================================================

    [RelayCommand]
    public async Task LoadAsync()
    {
        var version = ++_loadVersion;

        try
        {
            IsLoading = true;

            var (items, total) = IsNhap
                ? await _db.GetPhieuNhapHopPagedAsync(Search, TuNgay, DenNgay, SelectedLoaiPhieu, SelectedTrangThai, Page, PageSize)
                : await _db.GetPhieuXuatHopPagedAsync(Search, TuNgay, DenNgay, SelectedLoaiPhieu, SelectedTrangThai, Page, PageSize);

            if (version != _loadVersion)
                return;

            Items.Clear();
            var stt = (Page - 1) * PageSize + 1;
            foreach (var item in items)
            {
                item.SoThuTu = stt++;
                Items.Add(item);
            }

            TotalCount = total;

            var stats = IsNhap
                ? await _db.GetThongKePhieuNhapHopAsync(Search, TuNgay, DenNgay, SelectedLoaiPhieu, SelectedTrangThai)
                : await _db.GetThongKePhieuXuatHopAsync(Search, TuNgay, DenNgay, SelectedLoaiPhieu, SelectedTrangThai);

            if (version != _loadVersion)
                return;

            ThongKeTongPhieu = stats.TongSoPhieu;
            ThongKeTongGiaTri = stats.TongGiaTri;
            ThongKeTongSoLuong = stats.TongSoLuong;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tải danh sách phiếu hợp nhất", ex);

            if (HienThongBao is not null)
                await HienThongBao("Lỗi", "Không thể tải danh sách phiếu: " + ex.Message);
        }
        finally
        {
            if (version == _loadVersion)
                IsLoading = false;
        }
    }

    [RelayCommand]
    public Task LamMoiAsync() => LoadAsync();

    [RelayCommand]
    public Task TrangTruocAsync()
    {
        if (CanTrangTruoc)
            Page -= 1;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public Task TrangSauAsync()
    {
        if (CanTrangSau)
            Page += 1;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task TaoPhieuMoiAsync()
    {
        if (MoTaoPhieu is not null)
            await MoTaoPhieu();
    }

    [RelayCommand]
    public async Task XemChiTietAsync(PhieuHopRow? phieu)
    {
        if (phieu is null)
            return;

        try
        {
            var chiTiets = await _db.GetChiTietPhieuHopAsync(phieu.NguonPhieu, phieu.Id);

            var dongHang = string.Join("\n", chiTiets.Select(c =>
                $"• {c.TenSanPham} ({c.MaSanPham}): {c.SoLuongText} × {c.DonGiaText} = {c.ThanhTienText}" +
                (string.IsNullOrWhiteSpace(c.GhiChu) ? "" : $" [Ghi chú: {c.GhiChu}]")));

            var thongBao =
                $"MÃ PHIẾU: {phieu.MaPhieu}\n" +
                $"Thời gian: {phieu.ThoiGianText}\n" +
                $"Loại phiếu: {phieu.LoaiPhieuText}\n" +
                $"Đối tượng: {(string.IsNullOrWhiteSpace(phieu.DoiTuong) ? "—" : phieu.DoiTuong)}\n" +
                $"Trạng thái: {phieu.TrangThaiText}\n" +
                $"Ghi chú: {(string.IsNullOrWhiteSpace(phieu.GhiChu) ? "Không có" : phieu.GhiChu)}\n\n" +
                $"DANH SÁCH MẶT HÀNG ({chiTiets.Count} mặt hàng):\n" +
                (string.IsNullOrWhiteSpace(dongHang) ? "Chưa có chi tiết mặt hàng." : dongHang) + "\n\n" +
                $"TỔNG SỐ LƯỢNG: {phieu.TongSoLuongText}\n" +
                $"TỔNG GIÁ TRỊ: {phieu.TongTienText}";

            if (HienThongBao is not null)
                await HienThongBao($"Chi tiết phiếu {phieu.MaPhieu}", thongBao);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xem chi tiết phiếu hợp nhất", ex);

            if (HienThongBao is not null)
                await HienThongBao("Lỗi", "Không thể xem chi tiết: " + ex.Message);
        }
    }

    /// <summary>Thanh toán công nợ cho phiếu nhập từ nhà cung cấp còn nợ.</summary>
    [RelayCommand]
    public async Task ThanhToanPhieuAsync(PhieuHopRow? phieu)
    {
        if (phieu is null)
            return;

        if (ThanhToanPhieu is not null)
            await ThanhToanPhieu(phieu);
    }

    /// <summary>In lại phiếu nhập đã lưu.</summary>
    [RelayCommand]
    public async Task InPhieuAsync(PhieuHopRow? phieu)
    {
        if (phieu is null)
            return;

        if (InPhieu is not null)
            await InPhieu(phieu);
    }

    [RelayCommand]
    public async Task XuatExcelAsync()
    {
        var owner = LayCuaSoChinh?.Invoke();
        if (owner is null || Items.Count == 0)
        {
            if (HienThongBao is not null)
                await HienThongBao("Thông báo", "Không có dữ liệu để xuất file.");
            return;
        }

        try
        {
            var saveFile = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = IsNhap ? "Xuất danh sách phiếu nhập" : "Xuất danh sách phiếu xuất",
                SuggestedFileName = $"{TenFileExcel}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx"
            });

            if (saveFile is null)
                return;

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(IsNhap ? "Phieu nhập" : "Phieu xuất");

            var headers = new[] { "Mã phiếu", "Thời gian", "Loại phiếu", "Đối tượng", "Số lượng", "Tổng giá trị", "Trạng thái", "Ghi chú" };
            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(1, c + 1).Value = headers[c];

            var row = 2;
            foreach (var item in Items)
            {
                sheet.Cell(row, 1).Value = item.MaPhieu;
                sheet.Cell(row, 2).Value = item.ThoiGian;
                sheet.Cell(row, 3).Value = item.LoaiPhieuText;
                sheet.Cell(row, 4).Value = item.DoiTuong;
                sheet.Cell(row, 5).Value = item.TongSoLuong;
                sheet.Cell(row, 6).Value = item.TongTien;
                sheet.Cell(row, 7).Value = item.TrangThaiText;
                sheet.Cell(row, 8).Value = item.GhiChu;
                row++;
            }

            sheet.Columns().AdjustToContents();

            await using var stream = await saveFile.OpenWriteAsync();
            stream.Position = 0;
            workbook.SaveAs(stream);

            if (HienThongBao is not null)
                await HienThongBao("Thông báo", $"Đã xuất {Items.Count} phiếu ra file Excel.");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel danh sách phiếu hợp nhất", ex);

            if (HienThongBao is not null)
                await HienThongBao("Lỗi", "Không thể xuất file Excel: " + ex.Message);
        }
    }

    /// <summary>Gỡ bộ lọc tìm kiếm và ngày (nút "Xoá lọc").</summary>
    [RelayCommand]
    public void XoaLoc()
    {
        Search = "";
        TuNgay = null;
        DenNgay = null;
        SelectedLoaiPhieu = "TAT_CA";
        OnPropertyChanged(nameof(SelectedLoaiPhieuHienThi));
        SelectedTrangThai = "TAT_CA";
        OnPropertyChanged(nameof(SelectedTrangThaiHienThi));
        Page = 1;
        _ = LoadAsync();
    }
}
