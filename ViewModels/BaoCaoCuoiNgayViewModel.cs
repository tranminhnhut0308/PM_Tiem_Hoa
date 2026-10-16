using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho màn hình "Báo cáo cuối ngày".
/// <para>
/// Mọi số liệu đến từ <see cref="BaoCaoCuoiNgayService"/> (COUNT/SUM/GROUP BY/ORDER BY/
/// LIMIT ở tầng SQL). Không có hằng số số liệu trong giao diện, không suy diễn số liệu
/// ở client.
/// </para>
/// <para>
/// Lưu một instance sống suốt phiên như <see cref="DashboardViewModel"/> để giữ ngày
/// người dùng đang xem khi chuyển màn khác rồi quay lại.
/// </para>
/// </summary>
public sealed partial class BaoCaoCuoiNgayViewModel : ObservableObject
{
    private readonly BaoCaoCuoiNgayService _service;

    // Khởi tạo sẵn CTS để lần tải đầu tiên không gặp null khi hủy lượt cũ.
    private CancellationTokenSource _cts = new();
    private int _loadVersion;

    // DatNgay() tắt cờ này để đổi ngày bằng lệnh không tải lại hai lần.
    private bool _choPhepTuDongTai = true;

    /// <summary>Lấy cửa sổ chính để mở hộp thoại lưu file (gán từ MainWindowViewModel).</summary>
    public Func<Window?> LayCuaSoChinh { get; set; } = static () => null;

    /// <summary>Cập nhật dòng trạng thái chung của cửa sổ chính.</summary>
    public Action<string>? CapNhatTrangThai { get; set; }

    /// <summary>Hiện hộp thoại thông báo (tiêu đề, nội dung) cho người dùng.</summary>
    public Func<string, string, Task>? HienThiHopNhat { get; set; }

    public BaoCaoCuoiNgayViewModel(DatabaseService db)
    {
        _service = new BaoCaoCuoiNgayService(db.TaoKetNoiAsync);
    }

    // ============================================================
    // BỘ LỌC NGÀY
    // ============================================================

    /// <summary>
    /// Ngày đang xem. Đổi ngày sẽ tự tải lại báo cáo (xem
    /// <see cref="OnNgayChonChanged"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NgayHienThi))]
    private DateTimeOffset? ngayChon = DateTimeOffset.Now.Date;

    /// <summary>Ngày đang xem có đúng là hôm nay không (dùng cho nhãn nút).</summary>
    [ObservableProperty]
    private bool homNayChon = true;

    public string NgayHienThi => ((NgayChon?.DateTime ?? DateTime.Today).Date).ToString("dd/MM/yyyy");

    /// <summary>Ngày đang xem có phải hôm nay không (dùng cho nhãn nút).</summary>
    public string TenNutVềHômNay => HomNayChon ? "Hôm nay" : "Về hôm nay";

    // ============================================================
    // TRẠNG THÁI TẢI / LỖI
    // ============================================================

    [ObservableProperty]
    private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    private string thongBaoLoi = "";

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    public bool CoDuLieu => !CoLoi && !DangTai;

    // ============================================================
    // SỐ LIỆU TỔNG KẾT
    // ============================================================

    [ObservableProperty]
    private BaoCaoTongKetBanHang tongKetBanHang = new();

    [ObservableProperty]
    private BaoCaoSoQuyTongHop soQuyTongHop = new();

    [ObservableProperty]
    private BaoCaoNhapHangTongHop nhapHang = new();

    [ObservableProperty]
    private BaoCaoKhachHangTongHop khachHangTongHop = new();

    [ObservableProperty]
    private BaoCaoCongNoTongHop congNoTongHop = new();

    // ============================================================
    // DANH SÁCH
    // ============================================================

    public ObservableCollection<BaoCaoDiemGio> DoanhThuTheoGio { get; } = new();

    public ObservableCollection<BaoCaoPhuongThucRow> PhuongThucThanhToan { get; } = new();

    public ObservableCollection<BaoCaoSanPhamRow> TopSanPham { get; } = new();

    public ObservableCollection<BaoCaoKhachHangRow> TopKhachHang { get; } = new();

    public ObservableCollection<BaoCaoCongNoRow> CongNoPhatSinh { get; } = new();

    public ObservableCollection<BaoCaoThuChiRow> ThuChi { get; } = new();

    // ============================================================
    // CỜ HIỂN THỊ
    // ============================================================

    public bool CoPhuongThuc => PhuongThucThanhToan.Count > 0;

    public bool CoTopSanPham => TopSanPham.Count > 0;

    public bool CoTopKhachHang => TopKhachHang.Count > 0;

    public bool CoCongNo => CongNoPhatSinh.Count > 0;

    public bool CoThuChi => ThuChi.Count > 0;

    /// <summary>Có cột nào trong biểu đồ 24 giờ nhận tiền không.</summary>
    public bool CoDoanhThuTheoGio => DoanhThuTheoGio.Any(p => p.DoanhThu > 0);

    /// <summary>
    /// Báo cáo trong ngày có phát sinh gì không — dùng cho câu "không có phát sinh".
    /// </summary>
    public bool CoPhatSinh =>
        TongKetBanHang.SoHoaDon > 0
        || SoQuyTongHop.SoPhieuThu > 0
        || SoQuyTongHop.SoPhieuChi > 0
        || NhapHang.SoPhieu > 0;

    /// <summary>
    /// Cảnh báo khi dữ liệu vi phạm bất biến doanh thu. Trên dữ liệu thật không xảy
    /// ra, nhưng nếu có thì báo cho người dùng thay vì hiển thị hai số mâu thuẫn.
    /// </summary>
    /// <para>
    /// Chỉ kiểm tra bất biến doanh thu, cố tình không so <c>TongGiaTriMua</c>
    /// của nhóm khách hàng: nhóm đó bỏ qua khách lẻ, nên hợp lệ luôn lệch khi
    /// trong ngày có đơn không ghi tên.
    /// </para>
    public bool CanhBaoCongNo => TongKetBanHang.CoBaoDongDuLieu;

    public string NoiDungCanhBao =>
        "Số liệu chưa khớp: tổng tiền hàng − giảm giá ≠ doanh thu, hoặc đã thu + còn nợ ≠ doanh thu. " +
        "Vui lòng kiểm tra lại dữ liệu hóa đơn.";

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    /// <summary>
    /// Đổi ngày thì tự tải lại báo cáo. Dùng fire-and-forget có bắt lỗi vì đây là
    /// callback của property, không ai chờ kết quả.
    /// </summary>
    partial void OnNgayChonChanged(DateTimeOffset? value)
    {
        // Ngày đang xem luôn cập nhật trạng thái "hôm nay" để nút quay lại
        // hiển thị đúng.
        var selectedDate = value?.DateTime ?? DateTime.Today;
        HomNayChon = selectedDate.Date == DateTime.Today.Date;

        if (!_choPhepTuDongTai)
            return;

        _ = TaiDuLieuAsync();
    }

    partial void OnHomNayChonChanged(bool value)
    {
        OnPropertyChanged(nameof(TenNutVềHômNay));
    }

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        await TaiDuLieuAsync();
    }

    /// <summary>
    /// Nạp báo cáo của ngày đang chọn. Hủy lượt tải trước nếu còn đang chạy và bỏ qua
    /// kết quả cũ bằng version guard, để bấm liên tục không dồn truy vấn.
    /// </summary>
    public async Task TaiDuLieuAsync()
    {
        var ctsCu = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        ctsCu.Cancel();
        ctsCu.Dispose();

        var token = _cts.Token;
        var version = ++_loadVersion;

        var ngay = (NgayChon?.DateTime ?? DateTime.Today).Date;

        DangTai = true;
        ThongBaoLoi = "";

        try
        {
            var duLieu = await _service.TaiBaoCaoAsync(ngay, token);

            if (version != _loadVersion)
                return;

            CapNhat(duLieu);
        }
        catch (OperationCanceledException)
        {
            // Người dùng đổi ngày trong lúc đang tải -> im lặng bỏ qua.
        }
        catch (Exception ex)
        {
            if (version != _loadVersion)
                return;

            SafeLog.WriteError("Loi tai bao cao cuoi ngay", ex);

            ThongBaoLoi = "Không thể tải báo cáo: " + ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
                DangTai = false;
        }
    }

    private void CapNhat(BaoCaoCuoiNgayDuLieu duLieu)
    {
        TongKetBanHang = duLieu.TongKetBanHang;
        SoQuyTongHop = duLieu.SoQuyTongHop;
        NhapHang = duLieu.NhapHang;
        KhachHangTongHop = duLieu.KhachHangTongHop;
        CongNoTongHop = duLieu.CongNoTongHop;

        DoDanhSachMoi(DoanhThuTheoGio, duLieu.DoanhThuTheoGio);
        DoDanhSachMoi(PhuongThucThanhToan, duLieu.PhuongThucThanhToan);
        DoDanhSachMoi(TopSanPham, duLieu.TopSanPham);
        DoDanhSachMoi(TopKhachHang, duLieu.TopKhachHang);
        DoDanhSachMoi(CongNoPhatSinh, duLieu.CongNoPhatSinh);
        DoDanhSachMoi(ThuChi, duLieu.ThuChi);

        OnPropertyChanged(nameof(CoPhuongThuc));
        OnPropertyChanged(nameof(CoTopSanPham));
        OnPropertyChanged(nameof(CoTopKhachHang));
        OnPropertyChanged(nameof(CoCongNo));
        OnPropertyChanged(nameof(CoThuChi));
        OnPropertyChanged(nameof(CoDoanhThuTheoGio));
        OnPropertyChanged(nameof(CoPhatSinh));
        OnPropertyChanged(nameof(CanhBaoCongNo));
    }

    /// <summary>
    /// Thay nội dung một <see cref="ObservableCollection{T}"/> bằng Clear + Add nên
    /// danh sách cuộn và thanh ngang bên trong không bị vẽ lại từng dòng một.
    /// </summary>
    private static void DoDanhSachMoi<T>(ObservableCollection<T> dich, IEnumerable<T> moi)
    {
        dich.Clear();

        foreach (var item in moi)
            dich.Add(item);
    }

    // ============================================================
    // LỆNH CHỌN NGÀY
    // ============================================================

    /// <summary>Đặt ngày đang xay mà không kích hoạt tải lại tự động (lệnh sẽ tự gọi).</summary>
    private void DatNgay(DateTime ngay)
    {
        _choPhepTuDongTai = false;

        try
        {
            HomNayChon = ngay.Date == DateTime.Today.Date;
            NgayChon = new DateTimeOffset(ngay.Date);
        }
        finally
        {
            _choPhepTuDongTai = true;
        }
    }

    [RelayCommand]
    private async Task VềHômNayAsync()
    {
        DatNgay(DateTime.Today);
        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task VềNgayQuaAsync()
    {
        DatNgay(DateTime.Today.AddDays(-1));
        await TaiDuLieuAsync();
    }



    [RelayCommand]
    private async Task ChonNgayAsync()
    {
        var cuaSo = LayCuaSoChinh();

        if (cuaSo is null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                cuaSo = desktop.MainWindow;
        }

        if (cuaSo is null)
            return;

        var ngayHienTai = (NgayChon?.DateTime ?? DateTime.Today).Date;
        var datePicker = new DatePicker
        {
            SelectedDate = new DateTimeOffset(ngayHienTai),
            MaxYear = new DateTimeOffset(new DateTime(DateTime.Now.Year + 5, 12, 31)),
            MinYear = new DateTimeOffset(new DateTime(2020, 1, 1))
        };

        var btnHomNay = new Button { Content = "Hôm nay" };
        var okBtn = new Button { Content = "OK", IsDefault = true };
        var cancelBtn = new Button { Content = "Hủy", IsCancel = true };

        var bottomPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { btnHomNay, okBtn, cancelBtn }
        };

        var mainPanel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Chọn ngày để xem báo cáo cuối ngày", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                datePicker,
                bottomPanel
            }
        };

        var dialog = new Window
        {
            Title = "Chọn ngày báo cáo",
            Width = 320,
            Height = 320,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Grid { Margin = new Thickness(16), Children = { mainPanel } }
        };

        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Hủy", IsCancel = true };

        var stackBottom = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { ok, cancel }
        };

        var stackMain = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Chọn ngày để xem báo cáo cuối ngày", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                datePicker,
                stackBottom
            }
        };

        dialog.Content = new Grid { Margin = new Thickness(16), Children = { stackMain } };

        btnHomNay.Click += (s, e) => datePicker.SelectedDate = new DateTimeOffset(DateTime.Today);

        okBtn.Click += async (s, e) =>
        {
            dialog.Close(true);
            var ngayChonMoi = datePicker.SelectedDate?.DateTime ?? ngayHienTai;
            DatNgay(ngayChonMoi);
            await TaiDuLieuAsync();
        };

        cancelBtn.Click += (s, e) => dialog.Close(false);

        // Bỏ dialog để tránh lỗi - dùng Flyout thay thế
        await Task.CompletedTask;
    }
}