using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel riêng cho màn hình "Tổng quan".
/// <para>
/// Mọi số liệu đều lấy từ database qua <see cref="DashboardService"/> (COUNT/SUM/
/// GROUP BY ở tầng SQL). Không có hằng số số liệu trong giao diện.
/// </para>
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly DashboardService _service;
    // Khởi tạo sẵn một CTS để lần tải đầu tiên không gặp null khi hủy lượt cũ.
    private CancellationTokenSource _cts = new();
    private int _loadVersion;

    public DashboardViewModel(DatabaseService db)
    {
        _db = db;
        _service = new DashboardService(db.TaoKetNoiAsync);
    }

    // ============================================================
    // BỘ LỌC
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangThoiGianHienThi))]
    [NotifyPropertyChangedFor(nameof(TieuDeTheDoanhThu))]
    [NotifyPropertyChangedFor(nameof(PhuDeTheDoanhThu))]
    [NotifyPropertyChangedFor(nameof(PhuDeTheSoHoaDon))]
    [NotifyPropertyChangedFor(nameof(PhuDeTheDaThu))]
    [NotifyPropertyChangedFor(nameof(PhuDeTheConNo))]
    [NotifyPropertyChangedFor(nameof(PhuDeTheSoDonHang))]
    private string khoangThoiGian = TongQuanKhoangThoiGian.ThangNay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChieuDoanhThuHienThi))]
    [NotifyPropertyChangedFor(nameof(TieuDeBieuDo))]
    [NotifyPropertyChangedFor(nameof(MoTaChieuDoanhThu))]
    private string chieuDoanhThu = TongQuanChieuDoanhThu.TheoNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(XepHangTopHangHienThi))]
    private string xepHangTopHang = TongQuanXepHang.TheoDoanhThu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoTheDungNgayTuChon))]
    private bool dungKhoangTuDien;

    [ObservableProperty]
    private DateTimeOffset? tuNgayTuChon = DateTimeOffset.Now.Date;

    [ObservableProperty]
    private DateTimeOffset? denNgayTuChon = DateTimeOffset.Now.Date;

    public string KhoangThoiGianHienThi => TongQuanKhoangThoiGian.TenHienThi(KhoangThoiGian);

    public string ChieuDoanhThuHienThi => TongQuanChieuDoanhThu.TenHienThi(ChieuDoanhThu);

    public string XepHangTopHangHienThi => TongQuanXepHang.TenHienThi(XepHangTopHang);

    public string TieuDeTheDoanhThu => KhoangThoiGian switch
    {
        TongQuanKhoangThoiGian.HomNay => "DOANH THU HÔM NAY",
        TongQuanKhoangThoiGian.BayNgay => "DOANH THU 7 NGÀY",
        TongQuanKhoangThoiGian.ThangNay => "DOANH THU THÁNG NÀY",
        TongQuanKhoangThoiGian.ThangTruoc => "DOANH THU THÁNG TRƯỚC",
        TongQuanKhoangThoiGian.NamNay => "DOANH THU NĂM NAY",
        TongQuanKhoangThoiGian.TuDien => "DOANH THU KỲ CHỌN",
        _ => "DOANH THU"
    };

    public string PhuDeTheDoanhThu => KhoangThoiGian switch
    {
        TongQuanKhoangThoiGian.HomNay => "Tổng khách phải trả hôm nay",
        TongQuanKhoangThoiGian.BayNgay => "Tổng khách phải trả 7 ngày qua",
        TongQuanKhoangThoiGian.ThangNay => "Tổng khách phải trả tháng này",
        TongQuanKhoangThoiGian.ThangTruoc => "Tổng khách phải trả tháng trước",
        TongQuanKhoangThoiGian.NamNay => "Tổng khách phải trả năm nay",
        _ => "Tổng khách phải trả trong kỳ"
    };

    public string PhuDeTheSoHoaDon => KhoangThoiGian switch
    {
        TongQuanKhoangThoiGian.HomNay => "Hóa đơn hoàn thành hôm nay",
        TongQuanKhoangThoiGian.BayNgay => "Hóa đơn hoàn thành 7 ngày qua",
        TongQuanKhoangThoiGian.ThangNay => "Hóa đơn hoàn thành tháng này",
        TongQuanKhoangThoiGian.ThangTruoc => "Hóa đơn hoàn thành tháng trước",
        TongQuanKhoangThoiGian.NamNay => "Hóa đơn hoàn thành năm nay",
        _ => "Hóa đơn hoàn thành trong kỳ"
    };

    public string PhuDeTheDaThu => KhoangThoiGian switch
    {
        TongQuanKhoangThoiGian.HomNay => "Tiền đã thu trong ngày",
        TongQuanKhoangThoiGian.BayNgay => "Tiền đã thu trong 7 ngày",
        TongQuanKhoangThoiGian.ThangNay => "Tiền đã thu trong tháng này",
        TongQuanKhoangThoiGian.ThangTruoc => "Tiền đã thu trong tháng trước",
        TongQuanKhoangThoiGian.NamNay => "Tiền đã thu trong năm nay",
        _ => "Tiền đã thu trong kỳ"
    };

    public string PhuDeTheConNo => KhoangThoiGian switch
    {
        TongQuanKhoangThoiGian.HomNay => "Khách còn nợ hôm nay",
        TongQuanKhoangThoiGian.BayNgay => "Khách còn nợ 7 ngày qua",
        TongQuanKhoangThoiGian.ThangNay => "Khách còn nợ tháng này",
        TongQuanKhoangThoiGian.ThangTruoc => "Khách còn nợ tháng trước",
        TongQuanKhoangThoiGian.NamNay => "Khách còn nợ năm nay",
        _ => "Khách còn nợ trong kỳ"
    };

    public string PhuDeTheSoDonHang => KhoangThoiGian switch
    {
        TongQuanKhoangThoiGian.HomNay => "Lượt bán trong ngày",
        TongQuanKhoangThoiGian.BayNgay => "Lượt bán trong 7 ngày",
        TongQuanKhoangThoiGian.ThangNay => "Lượt bán trong tháng này",
        TongQuanKhoangThoiGian.ThangTruoc => "Lượt bán trong tháng trước",
        TongQuanKhoangThoiGian.NamNay => "Lượt bán trong năm nay",
        _ => "Lượt bán trong kỳ"
    };

    public string TieuDeBieuDo => ChieuDoanhThu switch
    {
        TongQuanChieuDoanhThu.TheoGio => "DOANH THU THEO KHUNG GIỜ TRONG NGÀY (00:00 – 23:00)",
        TongQuanChieuDoanhThu.TheoThu => "DOANH THU THEO THỨ TRONG TUẦN (THỨ 2 – CHỦ NHẬT)",
        _ => "DOANH THU THEO TỪNG NGÀY TRONG KỲ"
    };

    public string MoTaChieuDoanhThu => ChieuDoanhThu switch
    {
        TongQuanChieuDoanhThu.TheoGio => "Tổng hợp theo 24 khung giờ trong ngày để nhận diện giờ cao điểm bán hàng",
        TongQuanChieuDoanhThu.TheoThu => "So sánh doanh thu giữa các thứ trong tuần để nắm bắt ngày đông khách nhất",
        _ => "Theo dõi biến động doanh thu qua từng ngày trong khoảng thời gian đang chọn"
    };

    public bool CoTheDungNgayTuChon => DungKhoangTuDien;

    /// <summary>Danh sách "Top hàng" có dòng để ẩn câu "chưa có dữ liệu".</summary>
    public bool CoHangBanChay => TopHangBanChay.Count > 0;

    /// <summary>Danh sách "Top khách hàng" có dòng để ẩn câu "chưa có dữ liệu".</summary>
    public bool CoKhachHang => TopKhachHang.Count > 0;

    public IReadOnlyList<string> DanhSachKhoangThoiGian => TongQuanKhoangThoiGian.TatCa;

    public IReadOnlyList<string> DanhSachChieuDoanhThu => TongQuanChieuDoanhThu.TatCa;

    public IReadOnlyList<string> DanhSachXepHang => TongQuanXepHang.TatCa;

    // ============================================================
    // TRẠNG THÁI TẢI / LỖI
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = "";

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    public bool CoDuLieu => !CoLoi && !DangTai;

    /// <summary>Nội dung mô tả khoảng đang xem, hiển thị trên đầu thẻ doanh thu.</summary>
    [ObservableProperty]
    private string thoiGianHienThi = "";

    // ============================================================
    // 5 THẺ KẾT QUẢ BÁN HÀNG THEO KỲ ĐANG CHỌN
    // ============================================================

    [ObservableProperty]
    private string doanhThuHomNayHienThi = "0 đ";

    [ObservableProperty]
    private string soHoaDonHomNayHienThi = "0";

    [ObservableProperty]
    private string daThuHomNayHienThi = "0 đ";

    [ObservableProperty]
    private string conNoHomNayHienThi = "0 đ";

    [ObservableProperty]
    private string soDonHangHomNayHienThi = "0";

    // ============================================================
    // THẺ DOANH THU + BIỂU ĐỒ + CHỈ SỐ PHÂN TÍCH GÓC NHÌN
    // ============================================================

    [ObservableProperty]
    private string tongDoanhThuKyChonHienThi = "0 đ";

    [ObservableProperty]
    private string tongSoHoaDonKyChonHienThi = "0 hóa đơn";

    [ObservableProperty]
    private string tieuDeDiemCaoNhat = "MỐC CAO ĐIỂM NHẤT";

    [ObservableProperty]
    private string giaTriDiemCaoNhat = "Chưa có phát sinh";

    [ObservableProperty]
    private string phuDeDiemCaoNhat = "0 đ • 0 hóa đơn";

    [ObservableProperty]
    private string tieuDeTrungBinhChieu = "DOANH THU TRUNG BÌNH";

    [ObservableProperty]
    private string giaTriTrungBinhChieu = "0 đ";

    [ObservableProperty]
    private string phuDeTrungBinhChieu = "Trung bình theo kỳ";

    [ObservableProperty]
    private string tieuDeMatDoChieu = "MẬT ĐỘ GIAO DỊCH";

    [ObservableProperty]
    private string giaTriMatDoChieu = "0";

    [ObservableProperty]
    private string phuDeMatDoChieu = "Chưa có đơn hoàn thành";

    public ObservableCollection<TongQuanDiemDoanhThu> BieuDoDoanhThu { get; } = new();

    // ============================================================
    // HOẠT ĐỘNG TÀI KHOẢN CON (CHỈ ADMIN MỚI XEM ĐƯỢC)
    // ============================================================

    public bool IsAdmin => Session.CurrentUser != null && Quyen.IsAdmin(Session.CurrentUser);

    private List<TongQuanHoatDongTaiKhoanCon> _tatCaHoatDongTaiKhoanCon = [];

    public ObservableCollection<TongQuanHoatDongTaiKhoanCon> HoatDongTaiKhoanCon { get; } = new();

    public int SoLuongHoatDongTaiKhoanCon => HoatDongTaiKhoanCon.Count;

    public bool CoHoatDongTaiKhoanCon => HoatDongTaiKhoanCon.Count > 0;

    [ObservableProperty]
    private string timKiemHoatDongSub = "";

    partial void OnTimKiemHoatDongSubChanged(string value)
    {
        ApDungBoLocHoatDongSub();
    }

    [ObservableProperty]
    private string locTaiKhoanSub = "Tất cả nhân viên";

    partial void OnLocTaiKhoanSubChanged(string value)
    {
        ApDungBoLocHoatDongSub();
    }

    [ObservableProperty]
    private string locHanhDongSub = "Tất cả nghiệp vụ";

    partial void OnLocHanhDongSubChanged(string value)
    {
        ApDungBoLocHoatDongSub();
    }

    public ObservableCollection<string> DanhSachTaiKhoanCon { get; } = new();
    public ObservableCollection<string> DanhSachHanhDongCon { get; } = new();

    private void ApDungBoLocHoatDongSub()
    {
        var ketQua = _tatCaHoatDongTaiKhoanCon.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(TimKiemHoatDongSub))
        {
            var kw = TimKiemHoatDongSub.Trim().ToLowerInvariant();
            ketQua = ketQua.Where(h =>
                (h.TenDangNhap?.ToLowerInvariant().Contains(kw) ?? false) ||
                (h.HoTen?.ToLowerInvariant().Contains(kw) ?? false) ||
                (h.VaiTro?.ToLowerInvariant().Contains(kw) ?? false) ||
                (h.HanhDong?.ToLowerInvariant().Contains(kw) ?? false) ||
                (h.MaChungTu?.ToLowerInvariant().Contains(kw) ?? false) ||
                (h.NoiDung?.ToLowerInvariant().Contains(kw) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(LocTaiKhoanSub) && LocTaiKhoanSub != "Tất cả nhân viên" && LocTaiKhoanSub != "Tất cả")
        {
            ketQua = ketQua.Where(h =>
                string.Equals(h.TenDangNhap, LocTaiKhoanSub, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(h.NguoiThucHienHienThi, LocTaiKhoanSub, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(LocHanhDongSub) && LocHanhDongSub != "Tất cả nghiệp vụ" && LocHanhDongSub != "Tất cả")
        {
            ketQua = ketQua.Where(h => string.Equals(h.HanhDong, LocHanhDongSub, StringComparison.OrdinalIgnoreCase));
        }

        DoDanhSachMoi(HoatDongTaiKhoanCon, ketQua);
        OnPropertyChanged(nameof(SoLuongHoatDongTaiKhoanCon));
        OnPropertyChanged(nameof(CoHoatDongTaiKhoanCon));
    }

    [RelayCommand]
    private void XoaBoLocHoatDongSub()
    {
        TimKiemHoatDongSub = "";
        LocTaiKhoanSub = "Tất cả nhân viên";
        LocHanhDongSub = "Tất cả nghiệp vụ";
    }

    // ============================================================
    // TOP 10
    // ============================================================

    public ObservableCollection<TongQuanHangTop> TopHangBanChay { get; } = new();

    public ObservableCollection<TongQuanHangTop> TopKhachHang { get; } = new();

    // ============================================================
    // THỐNG KÊ KHO
    // ============================================================

    [ObservableProperty]
    private int tongSoHangHoa;

    [ObservableProperty]
    private int hangSapHet;

    [ObservableProperty]
    private int hangHetHang;

    [ObservableProperty]
    private int tongSoKhachHang;

    [ObservableProperty]
    private int tongSoNhaCungCap;

    [ObservableProperty]
    private string giaTriTonKhoHienThi = "0 đ";

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    /// <summary>
    /// Nạp lại Dashboard. Mỗi lần gọi sẽ hủy yêu cầu đang chạy trước đó để
    /// người dùng bấm liên tục không dồn nhiều truy vấn chồng nhau; kết quả
    /// của lần cũ bị bỏ qua qua version guard.
    /// </summary>
    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        await TaiDuLieuAsync();
    }

    /// <summary>
    /// Hàm tải dữ liệu dùng chung cho nút "Làm mới" và cho nơi khác trong
    /// phần mềm gọi sau khi ghi xong hóa đơn / phiếu nhập / phiếu quỹ.
    /// </summary>
    public async Task TaiDuLieuAsync()
    {
        // Hủy lượt tải trước nếu còn đang chạy.
        var ctsCu = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        ctsCu.Cancel();
        ctsCu.Dispose();

        var token = _cts.Token;
        var version = ++_loadVersion;

        DangTai = true;
        ThongBaoLoi = "";

        try
        {
            var duLieu = await _service.TaiDuLieuAsync(
                KhoangThoiGian,
                ChieuDoanhThu,
                XepHangTopHang,
                TuNgayTuChon?.Date,
                DenNgayTuChon?.Date,
                DateTime.Now,
                IsAdmin,
                token);

            // Bỏ qua nếu đã có lượt tải mới hơn (version guard).
            if (version != _loadVersion)
                return;

            CapNhat(duLieu);
        }
        catch (OperationCanceledException)
        {
            // Người dùng đã đổi bộ lọc trong lúc đang tải -> im lặng bỏ qua.
        }
        catch (Exception ex)
        {
            if (version != _loadVersion)
                return;

            SafeLog.WriteError("Loi tai du lieu tong quan", ex);

            // Không nuốt lỗi: hiển thị trạng thái lỗi rõ ràng, các màn khác vẫn chạy.
            ThongBaoLoi = "Không thể tải dữ liệu tổng quan: " + ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
                DangTai = false;
        }
    }

    private void CapNhat(TongQuanDuLieu duLieu)
    {
        // Khoảng/thời gian đang xem được suy ra từ lựa chọn nên "Tùy chọn" mở
        // ô chọn ngày ngay khi bấm, không phụ thuộc thứ tự gán của binding.
        DungKhoangTuDien = KhoangThoiGian == TongQuanKhoangThoiGian.TuDien;

        ThoiGianHienThi = duLieu.ThoiGianHienThi;

        DoanhThuHomNayHienThi = duLieu.DoanhThuHomNayHienThi;
        SoHoaDonHomNayHienThi = duLieu.SoHoaDonHomNayHienThi;
        DaThuHomNayHienThi = duLieu.DaThuHomNayHienThi;
        ConNoHomNayHienThi = duLieu.ConNoHomNayHienThi;
        SoDonHangHomNayHienThi = duLieu.SoDonHangHomNayHienThi;

        TongDoanhThuKyChonHienThi = duLieu.TongDoanhThuKyChonHienThi;
        TongSoHoaDonKyChonHienThi = duLieu.TongSoHoaDonKyChonHienThi;

        TieuDeDiemCaoNhat = duLieu.TieuDeDiemCaoNhat;
        GiaTriDiemCaoNhat = duLieu.GiaTriDiemCaoNhat;
        PhuDeDiemCaoNhat = duLieu.PhuDeDiemCaoNhat;

        TieuDeTrungBinhChieu = duLieu.TieuDeTrungBinhChieu;
        GiaTriTrungBinhChieu = duLieu.GiaTriTrungBinhChieu;
        PhuDeTrungBinhChieu = duLieu.PhuDeTrungBinhChieu;

        TieuDeMatDoChieu = duLieu.TieuDeMatDoChieu;
        GiaTriMatDoChieu = duLieu.GiaTriMatDoChieu;
        PhuDeMatDoChieu = duLieu.PhuDeMatDoChieu;

        DoDanhSachMoi(BieuDoDoanhThu, duLieu.BieuDoDoanhThu);
        DoDanhSachMoi(TopHangBanChay, duLieu.TopHangBanChay);
        DoDanhSachMoi(TopKhachHang, duLieu.TopKhachHang);

        _tatCaHoatDongTaiKhoanCon = duLieu.HoatDongTaiKhoanCon;

        var dsTaiKhoan = new List<string> { "Tất cả nhân viên" };
        dsTaiKhoan.AddRange(_tatCaHoatDongTaiKhoanCon
            .Select(h => h.TenDangNhap)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t));
        DoDanhSachMoi(DanhSachTaiKhoanCon, dsTaiKhoan);

        var dsHanhDong = new List<string> { "Tất cả nghiệp vụ" };
        dsHanhDong.AddRange(_tatCaHoatDongTaiKhoanCon
            .Select(h => h.HanhDong)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => h));
        DoDanhSachMoi(DanhSachHanhDongCon, dsHanhDong);

        if (string.IsNullOrWhiteSpace(LocTaiKhoanSub) || !dsTaiKhoan.Contains(LocTaiKhoanSub))
            LocTaiKhoanSub = "Tất cả nhân viên";

        if (string.IsNullOrWhiteSpace(LocHanhDongSub) || !dsHanhDong.Contains(LocHanhDongSub))
            LocHanhDongSub = "Tất cả nghiệp vụ";

        ApDungBoLocHoatDongSub();

        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(CoHangBanChay));
        OnPropertyChanged(nameof(CoKhachHang));

        var kho = duLieu.ThongKeKho;
        TongSoHangHoa = kho.TongSoHangHoa;
        HangSapHet = kho.HangSapHet;
        HangHetHang = kho.HangHetHang;
        TongSoKhachHang = kho.TongSoKhachHang;
        TongSoNhaCungCap = kho.TongSoNhaCungCap;
        GiaTriTonKhoHienThi = kho.GiaTriTonKhoHienThi;
    }

    /// <summary>
    /// Thay nội dung một <see cref="ObservableCollection{T}"/> mà không để ItemsControl
    /// nháy sống theo từng phần tử: dùng Clear + Add nên danh sách cuộn và các
    /// thanh ngang bên trong không bị vẽ lại một lần cho mỗi dòng.
    /// </summary>
    private static void DoDanhSachMoi<T>(ObservableCollection<T> dich, IEnumerable<T> moi)
    {
        dich.Clear();

        foreach (var item in moi)
            dich.Add(item);
    }

    // ============================================================
    // LỆNH ĐỔI BỘ LỌC
    // ============================================================

    [RelayCommand]
    private async Task ChonKhoangThoiGianAsync(string? gia)
    {
        var key = gia ?? "";

        if (!TongQuanKhoangThoiGian.TatCa.Contains(key))
            return;

        // Nút vừa được tick thì chuỗi đã được binding ghi vào trước; vẫn chạy lại
        // tải dữ liệu để ô chọn ngày và kết quả cập nhật đúng theo khoảng mới.
        if (key == KhoangThoiGian)
        {
            DungKhoangTuDien = key == TongQuanKhoangThoiGian.TuDien;

            await TaiDuLieuAsync();

            return;
        }

        KhoangThoiGian = key;
        DungKhoangTuDien = key == TongQuanKhoangThoiGian.TuDien;

        // "Tùy chọn" mở ra thì đặt sẵn khoảng 7 ngày gần nhất cho tiện.
        if (DungKhoangTuDien)
        {
            if (DenNgayTuChon is null)
                DenNgayTuChon = DateTimeOffset.Now.Date;

            if (TuNgayTuChon is null)
                TuNgayTuChon = DenNgayTuChon.Value.AddDays(-6);
        }

        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task ChonChieuDoanhThuAsync(string? gia)
    {
        var key = gia ?? "";

        if (!TongQuanChieuDoanhThu.TatCa.Contains(key))
            return;

        // Ô radio đã ghi giá trị mới vào binding trước khi lệnh chạy nên khi key
        // trùng giá trị hiện tại vẫn phải tải lại, nếu không biểu đồ không đổi.
        ChieuDoanhThu = key;

        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task ChonXepHangTopAsync(string? gia)
    {
        var key = gia ?? "";

        if (!TongQuanXepHang.TatCa.Contains(key))
            return;

        // Ô radio đã ghi giá trị mới vào binding trước khi lệnh chạy nên khi key
        // trùng giá trị hiện tại vẫn phải tải lại, nếu không bảng Top không đổi.
        XepHangTopHang = key;

        await TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task ApDungKhoangTuDienAsync()
    {
        if (DenNgayTuChon is not null && TuNgayTuChon is not null && TuNgayTuChon > DenNgayTuChon)
            (TuNgayTuChon, DenNgayTuChon) = (DenNgayTuChon, TuNgayTuChon);

        DungKhoangTuDien = true;
        KhoangThoiGian = TongQuanKhoangThoiGian.TuDien;

        await TaiDuLieuAsync();
    }
}