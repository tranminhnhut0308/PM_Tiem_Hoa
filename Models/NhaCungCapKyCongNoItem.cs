using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ShopHoa.Helpers;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng công nợ nhà cung cấp trong một kỳ kế toán / kinh doanh.
/// Quản lý số nợ đầu kỳ, phát sinh nhập hàng, đã trả, nợ cuối kỳ, số kỳ còn nợ và mức độ ưu tiên thanh toán.
/// </summary>
public sealed partial class NhaCungCapKyCongNoItem : ObservableObject
{
    private static readonly IBrush BrushUuTienCao = new SolidColorBrush(Color.Parse("#FEE4E2"));
    private static readonly IBrush BrushTextUuTienCao = new SolidColorBrush(Color.Parse("#B42318"));

    private static readonly IBrush BrushUuTienTB = new SolidColorBrush(Color.Parse("#FEF0C7"));
    private static readonly IBrush BrushTextUuTienTB = new SolidColorBrush(Color.Parse("#B54708"));

    private static readonly IBrush BrushUuTienThap = new SolidColorBrush(Color.Parse("#EBF7F3"));
    private static readonly IBrush BrushTextUuTienThap = new SolidColorBrush(Color.Parse("#087F6E"));

    public uint Id { get; set; }
    public uint KyKeToanId { get; set; }
    public uint NhaCungCapId { get; set; }
    public string MaNhaCungCap { get; set; } = "";
    public string TenNhaCungCap { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string Email { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string NhomNhaCungCap { get; set; } = "";
    public string NguoiLienHe { get; set; } = "";

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
    [NotifyPropertyChangedFor(nameof(DaTraText))]
    [NotifyPropertyChangedFor(nameof(NoCuoiKyText))]
    private decimal daTraTrongKy;

    /// <summary>Nợ cuối kỳ cần trả = Nợ đầu kỳ + Phát sinh trong kỳ - Đã trả trong kỳ</summary>
    public decimal NoCuoiKy => Math.Max(0m, NoDauKy + PhatSinhTrongKy - DaTraTrongKy);

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
    public string DaTraText => FormatTien(DaTraTrongKy);
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
            var ten = TenNhaCungCap.Trim();
            return string.IsNullOrEmpty(ten) ? "?" : ten[..1].ToUpperInvariant();
        }
    }

    /// <summary>
    /// Đánh giá tự động mức độ ưu tiên thanh toán công nợ:
    /// - Nợ từ 3 kỳ trở lên HOẶC số nợ >= 10 triệu: Ưu tiên cao
    /// - Nợ 2 kỳ HOẶC số nợ >= 5 triệu: Ưu tiên trung bình
    /// - Nợ 1 kỳ và số nợ < 5 triệu: Ưu tiên thấp
    /// </summary>
    public void DanhGiaLaiMucDoUuTien()
    {
        if (SoKyNo >= 3 || NoCuoiKy >= 10_000_000m)
        {
            MucDoUuTien = "CAO";
        }
        else if (SoKyNo == 2 || NoCuoiKy >= 5_000_000m)
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
