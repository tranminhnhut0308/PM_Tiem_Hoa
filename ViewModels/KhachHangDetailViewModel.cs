using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Vùng "Chi tiết khách hàng" mở rộng ngay dưới dòng khách hàng được chọn.
///
/// Ba tab: Thông tin / Lịch sử mua hàng / Công nợ.
/// Mọi số liệu đều lấy từ database, trường chưa có dữ liệu hiển thị "Chưa có".
/// </summary>
public sealed partial class KhachHangDetailViewModel : ObservableObject
{
    /// <summary>Giá trị hiển thị khi trường dữ liệu chưa có.</summary>
    public const string ChuaCo = "Chưa có";

    /// <summary>Trạng thái khi khách hàng chưa phát sinh giao dịch nào.</summary>
    public const string ChuaCoGiaoDich = "Chưa có giao dịch";

    /// <summary>Trạng thái khi hệ thống chưa có dữ liệu công nợ cho khách hàng.</summary>
    public const string ChuaCoDuLieuCongNo = "Chưa có dữ liệu";

    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaKhachHang))]
    [NotifyPropertyChangedFor(nameof(TenKhachHang))]
    [NotifyPropertyChangedFor(nameof(SoDienThoai))]
    [NotifyPropertyChangedFor(nameof(Email))]
    [NotifyPropertyChangedFor(nameof(DiaChiDayDu))]
    [NotifyPropertyChangedFor(nameof(NhomKhachHang))]
    [NotifyPropertyChangedFor(nameof(NgayTaoText))]
    [NotifyPropertyChangedFor(nameof(GioiTinh))]
    [NotifyPropertyChangedFor(nameof(NgaySinhText))]
    [NotifyPropertyChangedFor(nameof(GhiChu))]
    [NotifyPropertyChangedFor(nameof(MaSoThue))]
    [NotifyPropertyChangedFor(nameof(TenCongTy))]
    [NotifyPropertyChangedFor(nameof(TenNguoiMuaHoaDon))]
    [NotifyPropertyChangedFor(nameof(DiaChiXuatHoaDon))]
    [NotifyPropertyChangedFor(nameof(ChuCaiDau))]
    [NotifyPropertyChangedFor(nameof(LoaiKhachHangHienThi))]
    private KhachHang? customer;

    [ObservableProperty] private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTabThongTin))]
    [NotifyPropertyChangedFor(nameof(IsTabLichSuMuaHang))]
    [NotifyPropertyChangedFor(nameof(IsTabCongNo))]
    private string selectedTab = "thongtin";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCongNoData))]
    [NotifyPropertyChangedFor(nameof(TongTienDaMuaText))]
    [NotifyPropertyChangedFor(nameof(TongDaThanhToanText))]
    [NotifyPropertyChangedFor(nameof(TongCongNoText))]
    [NotifyPropertyChangedFor(nameof(TongBanTruTraHangText))]
    [NotifyPropertyChangedFor(nameof(NguonCongNoText))]
    [NotifyPropertyChangedFor(nameof(SoHoaDonText))]
    [NotifyPropertyChangedFor(nameof(LanGhiCongNoText))]
    [NotifyPropertyChangedFor(nameof(SoLanGhiCongNoText))]
    private ThongKeMuaHangKhachHang? thongKe;

    public ObservableCollection<HoaDon> HoaDons { get; } = new();

    // ============================================================
    // TRẠNG THÁI TAB
    // ============================================================

    public bool IsTabThongTin => SelectedTab == "thongtin";

    public bool IsTabLichSuMuaHang => SelectedTab == "lichsu";

    public bool IsTabCongNo => SelectedTab == "congno";

    [RelayCommand]
    private void ChonTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
            return;

        SelectedTab = tab;
    }

    // ============================================================
    // THÔNG TIN CÁ NHÂN
    // ============================================================

    public string MaKhachHang => HienThi(Customer?.MaKhachHang);

    public string TenKhachHang => HienThi(Customer?.TenKhachHang);

    public string SoDienThoai => HienThi(Customer?.SoDienThoai);

    public string Email => HienThi(Customer?.Email);

    public string NhomKhachHang => HienThi(Customer?.NhomKhachHang);

    public string GioiTinh => HienThi(Customer?.GioiTinh);

    public string NgaySinhText => Customer?.NgaySinh?.ToString("dd/MM/yyyy") ?? ChuaCo;

    public string NgayTaoText => Customer?.NgayTao?.ToString("dd/MM/yyyy HH:mm") ?? ChuaCo;


    public string GhiChu => HienThi(Customer?.GhiChu);

    public string MaSoThue => HienThi(Customer?.MaSoThue);

    public string TenCongTy => HienThi(Customer?.TenCongTy);

    public string TenNguoiMuaHoaDon => HienThi(Customer?.TenNguoiMuaHoaDon);

    public string DiaChiXuatHoaDon => HienThi(Customer?.DiaChiXuatHoaDon);

    public string LoaiKhachHangHienThi =>
        Customer?.LoaiKhachHang == "DOANH_NGHIEP" ? "Khách doanh nghiệp" : "Khách cá nhân";

    /// <summary>Gộp địa chỉ + phường/xã + quận/huyện + tỉnh/thành thành một chuỗi.</summary>
    public string DiaChiDayDu
    {
        get
        {
            var parts = new[]
                {
                    Customer?.DiaChi,
                    Customer?.PhuongXa,
                    Customer?.QuanHuyen,
                    Customer?.TinhThanh
                }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToList();

            return parts.Count == 0 ? ChuaCo : string.Join(", ", parts);
        }
    }

    public string ChuCaiDau
    {
        get
        {
            var ten = (Customer?.TenKhachHang ?? "").Trim();

            if (ten.Length == 0)
                return "?";

            return ten[..1].ToUpperInvariant();
        }
    }

    // ============================================================
    // LỊCH SỬ MUA HÀNG
    // ============================================================

    public bool HasLichSuMuaHang => HoaDons.Count > 0;

    public string LichSuMuaHangRongText => ChuaCoGiaoDich;

    public void NapLichSuMuaHang(IReadOnlyList<HoaDon> hoaDons)
    {
        HoaDons.Clear();

        foreach (var hoaDon in hoaDons)
            HoaDons.Add(hoaDon);

        OnPropertyChanged(nameof(HasLichSuMuaHang));
    }

    // ============================================================
    // CÔNG NỢ
    // ============================================================

    public bool HasCongNoData => ThongKe?.CoDuLieu == true;

    public string CongNoRongText => ChuaCoDuLieuCongNo;

    public string TongTienDaMuaText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongTienDaMua);

    /// <summary>
    /// Số tiền đã thanh toán lấy từ cột <c>khach_da_thanh_toan</c> của hóa đơn.
    /// Khách hàng chỉ có số tiền tổng hợp từ file KiotViet thì không có cột này,
    /// nên báo "Chưa có" thay vì hiện 0 đ (số 0 sẽ mâu thuẫn với tổng đã mua).
    /// </summary>
    public string TongDaThanhToanText =>
        ThongKe is null || ThongKe.SoHoaDon == 0
            ? ChuaCo
            : FormatTien(ThongKe.TongDaThanhToan);

    public string TongBanTruTraHangText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongBanTruTraHang);

    public string TongCongNoText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongConNoHienTai);

    public string SoHoaDonText =>
        ThongKe is null ? ChuaCo : $"{ThongKe.SoHoaDon:N0} hóa đơn";

    public string SoLanGhiCongNoText =>
        ThongKe is null ? ChuaCo : $"{ThongKe.SoLanGhiCongNo:N0} lần ghi công nợ";

    public string LanGhiCongNoText =>
        ThongKe?.LanGhiCongNoGanNhat?.ToString("dd/MM/yyyy HH:mm") ?? ChuaCo;

    /// <summary>Nguồn dữ liệu đang dùng để tính công nợ, nói rõ với người dùng.</summary>
    public string NguonCongNoText
    {
        get
        {
            if (ThongKe is null)
                return ChuaCoDuLieuCongNo;

            if (ThongKe.SoLanGhiCongNo > 0)
                return "Theo sổ cái công nợ khách hàng (ghi nợ - ghi có)";

            if (ThongKe.SoHoaDon > 0)
                return "Theo tổng công nợ còn lại trên hóa đơn bán hàng";

            // Số tiền lấy từ dữ liệu tổng hợp nhập từ file KiotViet
            if (ThongKe.CoDuLieuKiotViet && ThongKe.CoDuLieu)
                return "Theo số tiền tổng hợp đã nhập từ file KiotViet";

            return ChuaCoDuLieuCongNo;
        }
    }

    public bool CongNoDuong => (ThongKe?.TongConNoHienTai ?? 0) > 0;

    private static string HienThi(string? value)
        => string.IsNullOrWhiteSpace(value) ? ChuaCo : value.Trim();

    private static string FormatTien(decimal value)
        => $"{value.ToString("N0", VietnameseCulture)} đ";
}
