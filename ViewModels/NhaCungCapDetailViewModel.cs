using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Vùng "Chi tiết nhà cung cấp" mở rộng ngay dưới dòng nhà cung cấp được chọn.
///
/// Ba tab: Thông tin / Lịch sử nhập hàng / Công nợ.
/// Mọi số liệu đều lấy từ database, trường chưa có dữ liệu hiển thị "Chưa có".
/// </summary>
public sealed partial class NhaCungCapDetailViewModel : ObservableObject
{
    /// <summary>Giá trị hiển thị khi trường dữ liệu chưa có.</summary>
    public const string ChuaCo = "Chưa có";

    /// <summary>Trạng thái khi nhà cung cấp chưa phát sinh giao dịch nào.</summary>
    public const string ChuaCoGiaoDich = "Chưa có phiếu nhập hàng";

    /// <summary>Trạng thái khi hệ thống chưa có dữ liệu công nợ cho nhà cung cấp.</summary>
    public const string ChuaCoDuLieuCongNo = "Chưa có dữ liệu";

    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaNhaCungCap))]
    [NotifyPropertyChangedFor(nameof(TenNhaCungCap))]
    [NotifyPropertyChangedFor(nameof(SoDienThoai))]
    [NotifyPropertyChangedFor(nameof(Email))]
    [NotifyPropertyChangedFor(nameof(DiaChiDayDu))]
    [NotifyPropertyChangedFor(nameof(MaSoThue))]
    [NotifyPropertyChangedFor(nameof(NguoiLienHe))]
    [NotifyPropertyChangedFor(nameof(GhiChu))]
    [NotifyPropertyChangedFor(nameof(NgayTaoText))]
    [NotifyPropertyChangedFor(nameof(TrangThaiHienThi))]
    [NotifyPropertyChangedFor(nameof(ChuCaiDau))]
    private NhaCungCap? supplier;

    [ObservableProperty] private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTabThongTin))]
    [NotifyPropertyChangedFor(nameof(IsTabLichSuNhapHang))]
    [NotifyPropertyChangedFor(nameof(IsTabCongNo))]
    private string selectedTab = "thongtin";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCongNoData))]
    [NotifyPropertyChangedFor(nameof(SoPhieuNhapText))]
    [NotifyPropertyChangedFor(nameof(TongTienNhapText))]
    [NotifyPropertyChangedFor(nameof(TongMuaText))]
    [NotifyPropertyChangedFor(nameof(TongDaThanhToanText))]
    [NotifyPropertyChangedFor(nameof(TongCongNoText))]
    [NotifyPropertyChangedFor(nameof(SoLanGhiCongNoText))]
    [NotifyPropertyChangedFor(nameof(LanGhiCongNoText))]
    [NotifyPropertyChangedFor(nameof(NguonCongNoText))]
    [NotifyPropertyChangedFor(nameof(CanhBaoCongNo))]
    private ThongKeTaiChinhNhaCungCap? thongKe;

    /// <summary>Các phiếu nhập hàng của nhà cung cấp đang xem.</summary>
    public ObservableCollection<PhieuNhapHang> PhieuNhapHangs { get; } = new();

    /// <summary>Các dòng sổ cái công nợ của nhà cung cấp đang xem.</summary>
    public ObservableCollection<LichSuCongNoNhaCungCap> LichSuCongNos { get; } = new();

    // ============================================================
    // TRẠNG THÁI TAB
    // ============================================================

    public bool IsTabThongTin => SelectedTab == "thongtin";

    public bool IsTabLichSuNhapHang => SelectedTab == "lichsu";

    public bool IsTabCongNo => SelectedTab == "congno";

    [RelayCommand]
    private void ChonTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
            return;

        SelectedTab = tab;
    }

    // ============================================================
    // THÔNG TIN NHÀ CUNG CẤP
    // ============================================================

    public string MaNhaCungCap => HienThi(Supplier?.MaNhaCungCap);

    public string TenNhaCungCap => HienThi(Supplier?.TenNhaCungCap);

    public string SoDienThoai => HienThi(Supplier?.SoDienThoai);

    public string Email => HienThi(Supplier?.Email);

    public string MaSoThue => HienThi(Supplier?.MaSoThue);

    public string NguoiLienHe => HienThi(Supplier?.NguoiLienHe);

    public string GhiChu => HienThi(Supplier?.GhiChu);

    public string NgayTaoText => Supplier?.NgayTao?.ToString("dd/MM/yyyy HH:mm") ?? ChuaCo;

    public string TrangThaiHienThi => Supplier?.TrangThai == false
        ? "Ngừng hoạt động"
        : "Đang hoạt động";

    /// <summary>Gộp địa chỉ + phường/xã + quận/huyện + tỉnh/thành thành một chuỗi.</summary>
    public string DiaChiDayDu
    {
        get
        {
            var parts = new[]
                {
                    Supplier?.DiaChi,
                    Supplier?.PhuongXa,
                    Supplier?.QuanHuyen,
                    Supplier?.TinhThanh
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
            var ten = (Supplier?.TenNhaCungCap ?? "").Trim();

            if (ten.Length == 0)
                return "?";

            return ten[..1].ToUpperInvariant();
        }
    }

    // ============================================================
    // LỊCH SỬ NHẬP HÀNG
    // ============================================================

    public bool HasLichSuNhapHang => PhieuNhapHangs.Count > 0;

    public string LichSuNhapHangRongText => ChuaCoGiaoDich;

    public void NapPhieuNhapHang(IReadOnlyList<PhieuNhapHang> phieuNhapHangs)
    {
        PhieuNhapHangs.Clear();

        foreach (var phieu in phieuNhapHangs)
            PhieuNhapHangs.Add(phieu);

        OnPropertyChanged(nameof(HasLichSuNhapHang));
    }

    // ============================================================
    // CÔNG NỢ
    // ============================================================

    public bool HasCongNoData => ThongKe?.CoDuLieu == true;

    public bool CanhBaoCongNo => (ThongKe?.TongCongNoHienTai ?? 0) > 0;

    public bool HasLichSuCongNo => LichSuCongNos.Count > 0;

    public string LichSuCongNoRongText => ChuaCoDuLieuCongNo;

    public string CongNoRongText => ChuaCoDuLieuCongNo;

    public string SoPhieuNhapText =>
        ThongKe is null ? ChuaCo : $"{ThongKe.SoPhieuNhap:N0} phiếu";

    public string TongTienNhapText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongTienNhap);

    public string TongMuaText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongMua);

    public string TongDaThanhToanText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongDaThanhToanHienThi);

    public string TongCongNoText =>
        ThongKe is null ? ChuaCo : FormatTien(ThongKe.TongCongNoHienTai);

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

            if (ThongKe.CoDuLieuKiotViet && (ThongKe.SoPhieuNhap > 0 || ThongKe.SoLanGhiCongNo > 0))
                return "Kết hợp nợ ban đầu và phát sinh nhập hàng";

            if (ThongKe.CoDuLieuKiotViet)
                return "Theo file danh sách nhà cung cấp";

            if (ThongKe.SoLanGhiCongNo > 0)
                return "Theo sổ cái công nợ nhà cung cấp (ghi nợ - ghi có)";

            if (ThongKe.SoPhieuNhap > 0)
                return "Theo tổng công nợ còn lại trên các phiếu nhập hàng";

            return ChuaCoDuLieuCongNo;
        }
    }

    public void NapLichSuCongNo(IReadOnlyList<LichSuCongNoNhaCungCap> lichSu)
    {
        LichSuCongNos.Clear();

        foreach (var dong in lichSu)
            LichSuCongNos.Add(dong);

        OnPropertyChanged(nameof(HasLichSuCongNo));
    }

    private static string HienThi(string? value)
        => string.IsNullOrWhiteSpace(value) ? ChuaCo : value.Trim();

    private static string FormatTien(decimal value)
        => $"{value.ToString("N0", VietnameseCulture)} đ";
}
