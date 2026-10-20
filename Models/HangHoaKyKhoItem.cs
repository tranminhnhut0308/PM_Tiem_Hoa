using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng hàng hóa trong bảng quản lý tồn kho theo kỳ kinh doanh.
/// Quản lý tồn đầu kỳ, điều chỉnh hao hụt/hoa hỏng, giá vốn/bán đầu kỳ, nhập/xuất trong kỳ,
/// tồn hiện tại và chốt tồn cuối kỳ.
/// </summary>
public sealed partial class HangHoaKyKhoItem : ObservableObject
{
    public uint Id { get; set; }
    public uint KyKeToanId { get; set; }
    public uint KhoHangId { get; set; } = 1;
    public uint HangHoaId { get; set; }
    public string MaHangHoa { get; set; } = "";
    public string TenHangHoa { get; set; } = "";
    public string DonViTinh { get; set; } = "Cành";
    public string NhomHangHoa { get; set; } = "";
    public string? HinhAnh { get; set; }

    [ObservableProperty]
    private int stt;

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Số lượng tồn chốt từ cuối kỳ trước chuyển sang làm đầu kỳ mới.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoLuongThucTeDauKy))]
    [NotifyPropertyChangedFor(nameof(ThanhTienDauKy))]
    [NotifyPropertyChangedFor(nameof(TonHienTai))]
    [NotifyPropertyChangedFor(nameof(TonCuoiKyHienThi))]
    private decimal soLuongDauKy;

    /// <summary>Số lượng điều chỉnh kiểm kê thực tế đầu kỳ (ví dụ hoa dập, hư hỏng: gõ số âm hoặc dương).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoLuongThucTeDauKy))]
    [NotifyPropertyChangedFor(nameof(ThanhTienDauKy))]
    [NotifyPropertyChangedFor(nameof(TonHienTai))]
    [NotifyPropertyChangedFor(nameof(TonCuoiKyHienThi))]
    private decimal soLuongDieuChinh;

    /// <summary>Giá vốn áp dụng cho đầu kỳ (đ).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThanhTienDauKy))]
    private decimal giaVonDauKy;

    /// <summary>Giá bán mở bán áp dụng cho kỳ này (đ).</summary>
    [ObservableProperty]
    private decimal giaBanDauKy;

    /// <summary>Tổng số lượng nhập thêm trong kỳ này (từ các phiếu nhập hàng).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TonHienTai))]
    [NotifyPropertyChangedFor(nameof(TonCuoiKyHienThi))]
    private decimal soLuongNhap;

    /// <summary>Tổng số lượng đã xuất bán trong kỳ này (từ các hóa đơn bán lẻ).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TonHienTai))]
    [NotifyPropertyChangedFor(nameof(TonCuoiKyHienThi))]
    private decimal soLuongXuat;

    /// <summary>Số lượng tồn chốt khi bấm nút Kết kỳ.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TonCuoiKyHienThi))]
    private decimal soLuongCuoiKy;

    /// <summary>Đang mở bán trong kỳ này: true = có hiển thị ở POS, false = không bán mùa này.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrangThaiMoBanText))]
    private bool dangMoBan = true;

    [ObservableProperty]
    private string? ghiChu;

    // =========================================================================
    // CÁC THUỘC TÍNH TỰ ĐỘNG TÍNH TOÁN REALTIME
    // =========================================================================

    /// <summary>Tồn thực tế đầu kỳ sau khi đã trừ hao hụt / kiểm kê = Tồn đầu + Điều chỉnh.</summary>
    public decimal SoLuongThucTeDauKy => Math.Max(0, SoLuongDauKy + SoLuongDieuChinh);

    /// <summary>Thành tiền tồn đầu kỳ = Tồn thực tế × Giá vốn đầu kỳ.</summary>
    public decimal ThanhTienDauKy => Math.Round(SoLuongThucTeDauKy * GiaVonDauKy, 2, MidpointRounding.AwayFromZero);

    /// <summary>Tồn hiện tại = (Tồn đầu kỳ + Điều chỉnh) + Tổng nhập - Tổng xuất.</summary>
    public decimal TonHienTai => SoLuongThucTeDauKy + SoLuongNhap - SoLuongXuat;

    /// <summary>Tồn cuối kỳ hiển thị (nếu đã kết kỳ thì lấy SoLuongCuoiKy, nếu đang mở thì lấy TonHienTai).</summary>
    public decimal TonCuoiKyHienThi => SoLuongCuoiKy != 0 ? SoLuongCuoiKy : TonHienTai;

    public string TrangThaiMoBanText => DangMoBan ? "Đang bán" : "Tạm ngưng";
}
