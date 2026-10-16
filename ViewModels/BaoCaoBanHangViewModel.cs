using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho màn "Báo cáo bán hàng": tổng hợp doanh thu theo khoảng thời gian,
/// biểu đồ, bảng theo ngày, top sản phẩm / khách hàng, nhóm hàng và phương thức thanh toán.
/// <para>
/// Khác với <see cref="BaoCaoCuoiNgayViewModel"/> (chốt số của MỘT ngày), màn này
/// luôn làm việc trên khoảng <c>TuNgay → DenNgay</c>.
/// </para>
/// </summary>
public partial class BaoCaoBanHangViewModel : ObservableObject
{
    private readonly BaoCaoBanHangService _service;

    // Toàn bộ dữ liệu gốc (chưa phân trang / chưa lọc) dùng để dựng lại trang.
    private readonly List<BaoCaoBanHangBangRow> _bangAll = new();
    private readonly List<BaoCaoBanHangChiTietHoaDon> _chiTietAll = new();

    public BaoCaoBanHangViewModel(DatabaseService db)
    {
        _service = new BaoCaoBanHangService(db.TaoKetNoiAsync);

        TuNgay = DateTime.Today.AddDays(-6);
        DenNgay = DateTime.Today;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "7 ngày";
        TuyChinh = false;

        // Không tải dữ liệu trong constructor: MainWindowViewModel chỉ gọi
        // TaiLaiAsync() khi người dùng mở màn lần đầu (giống BaoCaoCuoiNgayViewModel),
        // tránh truy vấn database ngay khi khởi động ứng dụng.
    }

    // ============================================================
    // TRẠNG THÁI
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    // ============================================================
    // BỘ LỌC KHOẢNG THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    /// <summary>Tên khoảng đang xem: "Hôm nay", "Tuần này", "Tùy chọn"...</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    [NotifyPropertyChangedFor(nameof(DangChonHomNay))]
    [NotifyPropertyChangedFor(nameof(DangChonHomQua))]
    [NotifyPropertyChangedFor(nameof(DangChonTuanNay))]
    [NotifyPropertyChangedFor(nameof(DangChonThangNay))]
    [NotifyPropertyChangedFor(nameof(DangChonTuyChinh))]
    private string khoangThoiGian = "7 ngày";

    /// <summary>Cờ cho biết khoảng đang xem do người dùng tự chọn ngày.</summary>
    [ObservableProperty] private bool tuyChinh;

    /// <summary>Đánh dấu nút khoảng thời gian đang được chọn trên header.</summary>
    public bool DangChonHomNay => KhoangThoiGian == "Hôm nay";
    public bool DangChonHomQua => KhoangThoiGian == "Hôm qua";
    public bool DangChonTuanNay => KhoangThoiGian == "Tuần này";
    public bool DangChonThangNay => KhoangThoiGian == "Tháng này";
    public bool DangChonTuyChinh => KhoangThoiGian == "Tùy chọn";

    /// <summary>Từ ngày trên DatePicker (kiểu khớp với DatePicker.SelectedDate).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? tuNgayChon;

    /// <summary>Đến ngày trên DatePicker.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? denNgayChon;

    /// <summary>Có đủ ngày bắt đầu/kết thúc để bấm "Áp dụng" không.</summary>
    public bool CoKhoangTuyChinh => TuNgayChon is not null && DenNgayChon is not null;

    /// <summary>Khoảng thời gian đang xem, ví dụ: "Khoảng thời gian: 30/09/2026 - 06/10/2026".</summary>
    public string KhoangHienThi => $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";

    // ============================================================
    // DỮ LIỆU
    // ============================================================

    [ObservableProperty] private BaoCaoBanHangDuLieu duLieu = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoNgayChiTiet))]
    [NotifyPropertyChangedFor(nameof(NgayChiTietHienThi))]
    private DateTime? ngayChiTiet;

    public ObservableCollection<BaoCaoBanHangDiemThoiGian> BieuDoItems { get; } = new();
    public ObservableCollection<BaoCaoBanHangTopSanPham> TopSanPhamItems { get; } = new();
    public ObservableCollection<BaoCaoBanHangTopKhachHang> TopKhachHangItems { get; } = new();
    public ObservableCollection<BaoCaoBanHangNhomHang> NhomHangItems { get; } = new();
    public ObservableCollection<BaoCaoBanHangPhuongThucRow> PhuongThucItems { get; } = new();

    /// <summary>Bảng doanh thu theo ngày của TRANG đang xem (đã phân trang).</summary>
    public ObservableCollection<BaoCaoBanHangBangRow> BangItems { get; } = new();

    /// <summary>Danh sách hóa đơn chi tiết của ngày đang xem (đã lọc + phân trang).</summary>
    public ObservableCollection<BaoCaoBanHangChiTietHoaDon> ChiTietHoaDonList { get; } = new();

    public BaoCaoBanHangTongHop TongHop => DuLieu.TongHop;

    // ============================================================
    // PHÂN TRANG BẢNG THEO NGÀY
    // ============================================================

    [ObservableProperty] private int bangPage = 1;
    [ObservableProperty] private int bangPageSize = 15;

    public int BangTotalCount => _bangAll.Count;
    public int BangTotalPages => BangTotalCount <= 0 ? 0 : (int)Math.Ceiling(BangTotalCount / (double)BangPageSize);
    public bool CanBangTrangTruoc => BangPage > 1;
    public bool CanBangTrangSau => BangPage < BangTotalPages;
    public string BangTrangText => BangTotalPages <= 0 ? "0/0" : $"{BangPage}/{BangTotalPages}";

    // ============================================================
    // TÌM KIẾM + PHÂN TRANG CHI TIẾT HÓA ĐƠN
    // ============================================================

    [ObservableProperty] private string chiTietSearch = string.Empty;
    [ObservableProperty] private int chiTietPage = 1;
    [ObservableProperty] private int chiTietPageSize = 20;

    private List<BaoCaoBanHangChiTietHoaDon> ChiTietDaLoc =>
        string.IsNullOrWhiteSpace(ChiTietSearch)
            ? _chiTietAll
            : _chiTietAll.Where(x =>
                    x.MaHoaDon.Contains(ChiTietSearch, StringComparison.OrdinalIgnoreCase)
                    || x.KhachHang.Contains(ChiTietSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

    public int ChiTietTotalCount => ChiTietDaLoc.Count;
    public int ChiTietTotalPages => ChiTietTotalCount <= 0 ? 0 : (int)Math.Ceiling(ChiTietTotalCount / (double)ChiTietPageSize);
    public bool CanChiTietTrangTruoc => ChiTietPage > 1;
    public bool CanChiTietTrangSau => ChiTietPage < ChiTietTotalPages;
    public string ChiTietTrangText => ChiTietTotalPages <= 0 ? "0/0" : $"{ChiTietPage}/{ChiTietTotalPages}";

    // ============================================================
    // CỜ HIỂN THỊ
    // ============================================================

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);
    public bool CoBieuDo => DuLieu.BieuDo.Count > 0;
    public bool CoBang => _bangAll.Count > 0;
    public bool CoTopSanPham => DuLieu.TopSanPham.Count > 0;
    public bool CoTopKhachHang => DuLieu.TopKhachHang.Count > 0;
    public bool CoNhomHang => DuLieu.NhomHang.Count > 0;
    public bool CoPhuongThuc => DuLieu.PhuongThucThanhToan.Count > 0;
    public bool CoChiTietHoaDon => ChiTietDaLoc.Count > 0;
    public bool CoNgayChiTiet => NgayChiTiet is not null;

    public string NgayChiTietHienThi => NgayChiTiet?.ToString("dd/MM/yyyy") ?? string.Empty;

    /// <summary>Thông báo lại toàn bộ cờ rỗng / có dữ liệu sau mỗi lần tải.</summary>
    private void ThongBaoCoGiaTri()
    {
        OnPropertyChanged(nameof(CoBieuDo));
        OnPropertyChanged(nameof(CoBang));
        OnPropertyChanged(nameof(CoTopSanPham));
        OnPropertyChanged(nameof(CoTopKhachHang));
        OnPropertyChanged(nameof(CoNhomHang));
        OnPropertyChanged(nameof(CoPhuongThuc));
        OnPropertyChanged(nameof(CoChiTietHoaDon));
        OnPropertyChanged(nameof(TongHop));
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task TaiDuLieuAsync()
    {
        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;

            if (DenNgay < TuNgay)
                DenNgay = TuNgay;

            DuLieu = await _service.TaiDuLieuAsync(TuNgay, DenNgay, default);

            DoDanhSachMoi(BieuDoItems, DuLieu.BieuDo);
            DoDanhSachMoi(TopSanPhamItems, DuLieu.TopSanPham);
            DoDanhSachMoi(TopKhachHangItems, DuLieu.TopKhachHang);
            DoDanhSachMoi(NhomHangItems, DuLieu.NhomHang);
            DoDanhSachMoi(PhuongThucItems, DuLieu.PhuongThucThanhToan);

            // Bảng theo ngày: nạp toàn bộ rồi phân trang lại từ đầu.
            _bangAll.Clear();
            _bangAll.AddRange(DuLieu.BangDuLieu);
            BangPage = 1;
            CapNhatBangHienThi();

            // Chi tiết hóa đơn: đóng lại phần đang xem vì dữ liệu ngày cũ không còn đúng.
            DongChiTietNoiBo();

            ThongBaoCoGiaTri();
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không thể tải báo cáo bán hàng: " + ex.Message;
        }
        finally
        {
            DangTai = false;
        }
    }

    private static void DoDanhSachMoi<T>(ObservableCollection<T> dich, IReadOnlyList<T> moi)
    {
        dich.Clear();
        foreach (var item in moi)
            dich.Add(item);
    }

    // ============================================================
    // PHÂN TRANG BẢNG THEO NGÀY
    // ============================================================

    private void CapNhatBangHienThi()
    {
        var trang = Math.Max(1, BangPage);
        BangItems.Clear();

        foreach (var row in _bangAll.Skip((trang - 1) * BangPageSize).Take(BangPageSize))
            BangItems.Add(row);

        OnPropertyChanged(nameof(BangTotalCount));
        OnPropertyChanged(nameof(BangTotalPages));
        OnPropertyChanged(nameof(CanBangTrangTruoc));
        OnPropertyChanged(nameof(CanBangTrangSau));
        OnPropertyChanged(nameof(BangTrangText));
    }

    partial void OnBangPageChanged(int value) => CapNhatBangHienThi();

    [RelayCommand]
    private void BangTrangTruoc()
    {
        if (BangPage > 1)
            BangPage--;
    }

    [RelayCommand]
    private void BangTrangSau()
    {
        if (BangPage < BangTotalPages)
            BangPage++;
    }

    // ============================================================
    // TÌM KIẾM + PHÂN TRANG CHI TIẾT HÓA ĐƠN
    // ============================================================

    partial void OnChiTietSearchChanged(string value)
    {
        // Đổi từ khóa luôn quay về trang đầu. Nếu trang đang là 1 thì việc gán
        // không phát sinh thông báo, phải tự vẽ lại danh sách.
        if (ChiTietPage != 1)
            ChiTietPage = 1;
        else
            CapNhatChiTietHienThi();

        OnPropertyChanged(nameof(CoChiTietHoaDon));
    }

    partial void OnChiTietPageChanged(int value) => CapNhatChiTietHienThi();

    private void CapNhatChiTietHienThi()
    {
        var daLoc = ChiTietDaLoc;
        var trang = Math.Max(1, ChiTietPage);
        ChiTietHoaDonList.Clear();

        foreach (var row in daLoc.Skip((trang - 1) * ChiTietPageSize).Take(ChiTietPageSize))
            ChiTietHoaDonList.Add(row);

        OnPropertyChanged(nameof(ChiTietTotalCount));
        OnPropertyChanged(nameof(ChiTietTotalPages));
        OnPropertyChanged(nameof(CanChiTietTrangTruoc));
        OnPropertyChanged(nameof(CanChiTietTrangSau));
        OnPropertyChanged(nameof(ChiTietTrangText));
    }

    [RelayCommand]
    private void ChiTietTrangTruoc()
    {
        if (ChiTietPage > 1)
            ChiTietPage--;
    }

    [RelayCommand]
    private void ChiTietTrangSau()
    {
        if (ChiTietPage < ChiTietTotalPages)
            ChiTietPage++;
    }

    // ============================================================
    // LỆNH CHỌN KHOẢNG THỜI GIAN
    // ============================================================

    private async Task DatKhoangAsync(DateTime tu, DateTime den, string nhan)
    {
        TuNgay = tu.Date;
        DenNgay = den.Date;
        TuNgayChon = new DateTimeOffset(tu.Date);
        DenNgayChon = new DateTimeOffset(den.Date);
        KhoangThoiGian = nhan;
        TuyChinh = nhan == "Tùy chọn";

        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task VeHomNayAsync()
    {
        await DatKhoangAsync(DateTime.Today, DateTime.Today, "Hôm nay");
    }

    [RelayCommand]
    private async Task VeNgayQuaAsync()
    {
        var homQua = DateTime.Today.AddDays(-1);
        await DatKhoangAsync(homQua, homQua, "Hôm qua");
    }

    [RelayCommand]
    private async Task VeTuanNayAsync()
    {
        // Tuần bắt đầu từ thứ Hai.
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7;
        await DatKhoangAsync(homNay.AddDays(-soNgayDaQua), homNay, "Tuần này");
    }

    [RelayCommand]
    private async Task VeThangNayAsync()
    {
        var homNay = DateTime.Today;
        await DatKhoangAsync(new DateTime(homNay.Year, homNay.Month, 1), homNay, "Tháng này");
    }

    [RelayCommand]
    private async Task ApDungTuyChinhAsync()
    {
        if (TuNgayChon is null || DenNgayChon is null)
            return;

        var tu = TuNgayChon.Value.Date;
        var den = DenNgayChon.Value.Date;

        // Người dùng chọn ngược vẫn phải chạy được.
        if (den < tu)
            (tu, den) = (den, tu);

        await DatKhoangAsync(tu, den, "Tùy chọn");
    }

    // ============================================================
    // LỆNH KHÁC
    // ============================================================

    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        try
        {
            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            using (var wb = BaoCaoBanHangExcel.TaoWorkbook(DuLieu))
            {
                var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Xuất Excel báo cáo bán hàng",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                    },
                    SuggestedFileName = DuLieu.TongHop.TenFileXuat()
                });

                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    wb.SaveAs(stream);
                }
            }
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không xuất được file Excel: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task XemChiTietNgayAsync(DateTime ngay)
    {
        try
        {
            NgayChiTiet = ngay.Date;
            _chiTietAll.Clear();
            _chiTietAll.AddRange(await _service.TaiChiTietHoaDonAsync(ngay, default));

            ChiTietSearch = string.Empty;
            ChiTietPage = 1;
            CapNhatChiTietHienThi();
            OnPropertyChanged(nameof(CoChiTietHoaDon));
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không tải được chi tiết hóa đơn: " + ex.Message;
        }
    }

    [RelayCommand]
    private void DongChiTiet() => DongChiTietNoiBo();

    private void DongChiTietNoiBo()
    {
        NgayChiTiet = null;
        _chiTietAll.Clear();
        ChiTietSearch = string.Empty;
        ChiTietPage = 1;
        ChiTietHoaDonList.Clear();

        OnPropertyChanged(nameof(CoChiTietHoaDon));
        OnPropertyChanged(nameof(ChiTietTotalCount));
        OnPropertyChanged(nameof(ChiTietTotalPages));
        OnPropertyChanged(nameof(CanChiTietTrangTruoc));
        OnPropertyChanged(nameof(CanChiTietTrangSau));
        OnPropertyChanged(nameof(ChiTietTrangText));
    }
}
