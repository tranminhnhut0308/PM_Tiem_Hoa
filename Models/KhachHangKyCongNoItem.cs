using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ShopHoa.Helpers;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng công nợ khách hàng trong một kỳ kế toán / kinh doanh.
/// Quản lý số nợ đầu kỳ, phát sinh, đã thu, nợ cuối kỳ, số kỳ còn nợ và mức độ ưu tiên nhắc nợ.
/// </summary>
public sealed partial class KhachHangKyCongNoItem : ObservableObject
{
    private static readonly IBrush BrushUuTienCao = new SolidColorBrush(Color.Parse("#FEE4E2"));
    private static readonly IBrush BrushTextUuTienCao = new SolidColorBrush(Color.Parse("#B42318"));

    private static readonly IBrush BrushUuTienTB = new SolidColorBrush(Color.Parse("#FEF0C7"));
    private static readonly IBrush BrushTextUuTienTB = new SolidColorBrush(Color.Parse("#B54708"));

    private static readonly IBrush BrushUuTienThap = new SolidColorBrush(Color.Parse("#EBF7F3"));
    private static readonly IBrush BrushTextUuTienThap = new SolidColorBrush(Color.Parse("#087F6E"));

    public uint Id { get; set; }
    public uint KyKeToanId { get; set; }
    public uint KhachHangId { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string NhomKhachHang { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCuoiKy))]
    [NotifyPropertyChangedFor(nameof(NoDauKyText))]
    [NotifyPropertyChangedFor(nameof(NoCuoiKyText))]
    private decimal noDauKy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCuoiKy))]
    [NotifyPropertyChangedFor(nameof(PhatSinhText))]
    [NotifyPropertyChangedFor(nameof(NoCuoiKyText))]
    private decimal phatSinhTrongKy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCuoiKy))]
    [NotifyPropertyChangedFor(nameof(DaThuText))]
    [NotifyPropertyChangedFor(nameof(NoCuoiKyText))]
    private decimal daThuTrongKy;

    /// <summary>Nợ cuối kỳ = Nợ đầu kỳ + Phát sinh - Đã thu</summary>
    public decimal NoCuoiKy => Math.Max(0m, NoDauKy + PhatSinhTrongKy - DaThuTrongKy);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoKyNoText))]
    [NotifyPropertyChangedFor(nameof(MucDoUuTienText))]
    [NotifyPropertyChangedFor(nameof(MucDoUuTienBadgeBrush))]
    [NotifyPropertyChangedFor(nameof(MucDoUuTienTextBrush))]
    private int soKyNo = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MucDoUuTienText))]
    [NotifyPropertyChangedFor(nameof(MucDoUuTienBadgeBrush))]
    [NotifyPropertyChangedFor(nameof(MucDoUuTienTextBrush))]
    private string mucDoUuTien = "TRUNG_BINH"; // "CAO", "TRUNG_BINH", "THAP"

    [ObservableProperty]
    private string? ghiChu;

    // Formatting Helpers
    public string NoDauKyText => FormatTien(NoDauKy);
    public string PhatSinhText => FormatTien(PhatSinhTrongKy);
    public string DaThuText => FormatTien(DaThuTrongKy);
    public string NoCuoiKyText => FormatTien(NoCuoiKy);

    public string SoKyNoText => SoKyNo <= 1 ? "1 kỳ" : $"{SoKyNo} kỳ liên tiếp";

    public string MucDoUuTienText => MucDoUuTien switch
    {
        "CAO" => "🔴 Ưu tiên cao",
        "TRUNG_BINH" => "🟡 Ưu tiên TB",
        _ => "🟢 Ưu tiên thấp"
    };

    public IBrush MucDoUuTienBadgeBrush => MucDoUuTien switch
    {
        "CAO" => BrushUuTienCao,
        "TRUNG_BINH" => BrushUuTienTB,
        _ => BrushUuTienThap
    };

    public IBrush MucDoUuTienTextBrush => MucDoUuTien switch
    {
        "CAO" => BrushTextUuTienCao,
        "TRUNG_BINH" => BrushTextUuTienTB,
        _ => BrushTextUuTienThap
    };

    public string ChuCaiDau
    {
        get
        {
            var ten = TenKhachHang.Trim();
            return string.IsNullOrEmpty(ten) ? "?" : ten[..1].ToUpperInvariant();
        }
    }

    /// <summary>
    /// Đánh giá tự động mức độ ưu tiên:
    /// - Nợ từ 3 kỳ trở lên HOẶC số nợ >= 5 triệu: Ưu tiên cao
    /// - Nợ 2 kỳ HOẶC số nợ >= 2 triệu: Ưu tiên trung bình
    /// - Nợ 1 kỳ và số nợ < 2 triệu: Ưu tiên thấp
    /// </summary>
    public void DanhGiaLaiMucDoUuTien()
    {
        if (SoKyNo >= 3 || NoCuoiKy >= 5_000_000m)
        {
            MucDoUuTien = "CAO";
        }
        else if (SoKyNo == 2 || NoCuoiKy >= 2_000_000m)
        {
            MucDoUuTien = "TRUNG_BINH";
        }
        else
        {
            MucDoUuTien = "THAP";
        }
    }

    private static string FormatTien(decimal value) => MoneyInput.Format(value) + " đ";
}
