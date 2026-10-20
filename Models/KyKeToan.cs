using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một kỳ kinh doanh / kỳ kế toán kho (Tháng, đợt vụ hoa, mùa lễ...).
/// Quản lý việc đóng/mở kỳ, chốt tồn cuối kỳ và làm căn cứ kế thừa cho kỳ kế tiếp.
/// </summary>
public sealed partial class KyKeToan : ObservableObject
{
    public uint Id { get; set; }

    [ObservableProperty]
    private string maKy = "";

    [ObservableProperty]
    private string tenKy = "";

    [ObservableProperty]
    private DateTime ngayBatDauKy = DateTime.Today;

    [ObservableProperty]
    private DateTime ngayKetThucKy = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrangThaiText))]
    [NotifyPropertyChangedFor(nameof(IsDangMo))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private bool trangThaiKhoaSo; // 0: Đang mở, 1: Khóa sổ / Đã kết kỳ

    [ObservableProperty]
    private DateTime? ngayKetKy;

    public uint? NguoiKetKyId { get; set; }

    [ObservableProperty]
    private string? ghiChu;

    public uint ChiNhanhId { get; set; } = 1;

    public DateTime NgayTao { get; set; } = DateTime.Now;

    // Thuộc tính tiện ích hiển thị
    public bool IsDangMo => !TrangThaiKhoaSo;

    public string TrangThaiText => TrangThaiKhoaSo ? "🔒 Đã kết kỳ" : "🟢 Đang mở";

    public string ThoiGianHienThi => $"{NgayBatDauKy:dd/MM/yyyy} - {NgayKetThucKy:dd/MM/yyyy}";

    public string DisplayName => $"{TenKy} ({(TrangThaiKhoaSo ? "Đã kết kỳ" : "Đang mở")})";

    public override string ToString() => DisplayName;
}
