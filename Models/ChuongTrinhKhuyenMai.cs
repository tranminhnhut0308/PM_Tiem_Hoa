using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ShopHoa.Services;

namespace ShopHoa.Models;

/// <summary>
/// CÃ¡c loáº¡i chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i Ä‘Æ°á»£c há»— trá»£.
/// <para>
/// Má»i chÆ°Æ¡ng trÃ¬nh Ä‘á»u dÃ¹ng chung cho táº¥t cáº£ khÃ¡ch hÃ ng. ÄÆ¡n hÃ ng chá»‰ Ä‘Æ°á»£c
/// hÆ°á»Ÿng Æ°u Ä‘Ã£i khi thá»a mÃ£n Ä‘iá»u kiá»‡n cá»§a chÆ°Æ¡ng trÃ¬nh.
/// </para>
/// </summary>
public static class KhuyenMaiLoai
{
    public const string TheoDonHang = "ORDER_DISCOUNT";
    public const string TheoSanPham = "PRODUCT_DISCOUNT";
    public const string TangQua = "BUY_X_GET_Y";

    /// <summary>Chuáº©n hÃ³a giÃ¡ trá»‹ cÅ© trong database sang mÃ£ má»›i.</summary>
    public static string ChuanHoa(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return TheoSanPham;

        return value.Trim().ToUpperInvariant() switch
        {
            "THEO_HOA_DON" or "ORDER_DISCOUNT" => TheoDonHang,
            "TANG_KEM" or "BUY_X_GET_Y" => TangQua,
            "THEO_HANG_HOA" or "PRODUCT_DISCOUNT" => TheoSanPham,
            _ => TheoSanPham
        };
    }

    public static string HienThi(string? value) => ChuanHoa(value) switch
    {
        TheoDonHang => "Giáº£m giÃ¡ theo tá»•ng Ä‘Æ¡n hÃ ng",
        TheoSanPham => "Giáº£m giÃ¡ theo sáº£n pháº©m",
        TangQua => "Táº·ng quÃ  theo sáº£n pháº©m",
        _ => "Giáº£m giÃ¡ theo sáº£n pháº©m"
    };
}

/// <summary>
/// Äáº¡i diá»‡n cho má»™t chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i (báº£ng chuong_trinh_khuyen_mai).
/// </summary>
public sealed partial class ChuongTrinhKhuyenMai : ObservableObject
{
    public uint Id { get; set; }

    public int Stt { get; set; }

    [ObservableProperty]
    private string maChuongTrinh = "";

    [ObservableProperty]
    private string tenChuongTrinh = "";

    [ObservableProperty]
    private DateTime ngayBatDau = DateTime.Today;

    [ObservableProperty]
    private DateTime ngayKetThuc = DateTime.Today.AddDays(30);

    [ObservableProperty]
    private string trangThai = "DANG_HOAT_DONG";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaTheoDonHang))]
    [NotifyPropertyChangedFor(nameof(LaTheoSanPham))]
    [NotifyPropertyChangedFor(nameof(LaTangQua))]
    [NotifyPropertyChangedFor(nameof(LoaiKhuyenMaiHienThi))]
    private string loaiKhuyenMai = KhuyenMaiLoai.TheoSanPham;

    [ObservableProperty]
    private string? ghiChu = "ChÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i Æ°u Ä‘Ã£i khÃ¡ch hÃ ng";

    [ObservableProperty]
    private string? tomTatUuDai;

    // ----- Äiá»u kiá»‡n / Æ°u Ä‘Ã£i cá»§a chÆ°Æ¡ng trÃ¬nh giáº£m theo tá»•ng Ä‘Æ¡n hÃ ng -----

    /// <summary>GiÃ¡ trá»‹ Ä‘Æ¡n hÃ ng tá»‘i thiá»ƒu Ä‘á»ƒ Ä‘Æ°á»£c Ã¡p dá»¥ng (VNÄ).</summary>
    [ObservableProperty]
    private decimal giaTriDonToiThieu;

    /// <summary>PHAN_TRAM hoáº·c SO_TIEN.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaGiamPhanTram))]
    [NotifyPropertyChangedFor(nameof(LaGiamSoTien))]
    private string loaiGiamGia = "PHAN_TRAM";

    /// <summary>GiÃ¡ trá»‹ giáº£m (% hoáº·c sá»‘ tiá»n) cho chÆ°Æ¡ng trÃ¬nh theo tá»•ng Ä‘Æ¡n.</summary>
    [ObservableProperty]
    private decimal giaTriGiam;

    public uint ChiNhanhId { get; set; } = 1;
    public uint? NguoiTaoId { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    public ObservableCollection<ChiTietKhuyenMai> DanhSachChiTiet { get; } = new();

    public int SoLuongSanPham => DanhSachChiTiet.Count;

    public bool LaTheoDonHang =>
        string.Equals(KhuyenMaiLoai.TheoDonHang, LoaiKhuyenMai, StringComparison.OrdinalIgnoreCase);

    public bool LaTheoSanPham =>
        string.Equals(KhuyenMaiLoai.TheoSanPham, LoaiKhuyenMai, StringComparison.OrdinalIgnoreCase);

    public bool LaTangQua =>
        string.Equals(KhuyenMaiLoai.TangQua, LoaiKhuyenMai, StringComparison.OrdinalIgnoreCase);

    public bool LaGiamPhanTram => string.Equals(LoaiGiamGia, "PHAN_TRAM", StringComparison.OrdinalIgnoreCase);

    public bool LaGiamSoTien => !LaGiamPhanTram;

    public string NgayBatDauHienThi => NgayBatDau.ToString("dd/MM/yyyy");
    public string NgayKetThucHienThi => NgayKetThuc.ToString("dd/MM/yyyy");
    public string ThoiGianHienThi => $"{NgayBatDau:dd/MM/yyyy} - {NgayKetThuc:dd/MM/yyyy}";

    public string TrangThaiTinhToan
    {
        get
        {
            if (string.Equals(TrangThai, "TAM_NGUNG", StringComparison.OrdinalIgnoreCase))
                return "TAM_NGUNG";

            var today = DateTime.Today;
            if (NgayBatDau.Date > today)
                return "CHUA_BAT_DAU";
            if (NgayKetThuc.Date < today)
                return "DA_KET_THUC";

            return "DANG_HOAT_DONG";
        }
    }

    public string TrangThaiHienThi => TrangThaiTinhToan switch
    {
        "DANG_HOAT_DONG" => "Äang diá»…n ra",
        "CHUA_BAT_DAU" => "ChÆ°a báº¯t Ä‘áº§u",
        "TAM_NGUNG" => "Táº¡m ngÆ°ng",
        "DA_KET_THUC" => "ÄÃ£ káº¿t thÃºc",
        _ => TrangThai
    };

    public string TrangThaiBadgeBg => TrangThaiTinhToan switch
    {
        "DANG_HOAT_DONG" => "#E6F4EA",
        "CHUA_BAT_DAU" => "#E8F0FE",
        "TAM_NGUNG" => "#FEF7E0",
        "DA_KET_THUC" => "#F1F3F4",
        _ => "#F1F3F4"
    };

    public string TrangThaiBadgeFg => TrangThaiTinhToan switch
    {
        "DANG_HOAT_DONG" => "#137333",
        "CHUA_BAT_DAU" => "#1A73E8",
        "TAM_NGUNG" => "#B06000",
        "DA_KET_THUC" => "#5F6368",
        _ => "#5F6368"
    };

    public string TrangThaiBadgeBorder => TrangThaiTinhToan switch
    {
        "DANG_HOAT_DONG" => "#CEEAD6",
        "CHUA_BAT_DAU" => "#D2E3FC",
        "TAM_NGUNG" => "#FEEFC3",
        "DA_KET_THUC" => "#DADCE0",
        _ => "#DADCE0"
    };

    public IBrush TrangThaiBadgeBrush => new SolidColorBrush(Color.Parse(TrangThaiBadgeBg));
    public IBrush TrangThaiBadgeFgBrush => new SolidColorBrush(Color.Parse(TrangThaiBadgeFg));
    public IBrush TrangThaiBadgeBorderBrush => new SolidColorBrush(Color.Parse(TrangThaiBadgeBorder));

    public string LoaiKhuyenMaiHienThi => KhuyenMaiLoai.HienThi(LoaiKhuyenMai);

    public string UuDaiHienThi
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TomTatUuDai))
                return TomTatUuDai;

            if (LaTheoDonHang)
                return GiaTriGiam > 0
                    ? (LaGiamPhanTram ? $"Giáº£m {GiaTriGiam:0.##}%" : $"Giáº£m {GiaTriGiam:N0} Ä‘")
                    : "Giáº£m giÃ¡ theo tá»•ng Ä‘Æ¡n";

            if (SoLuongSanPham > 0)
                return $"Ãp dá»¥ng {SoLuongSanPham} sáº£n pháº©m";

            return "ChÆ°a cáº¥u hÃ¬nh Æ°u Ä‘Ã£i";
        }
    }

    /// <summary>Má»i chÆ°Æ¡ng trÃ¬nh Ä‘á»u dÃ¹ng chung cho táº¥t cáº£ khÃ¡ch hÃ ng.</summary>
    public string DoiTuongHienThi => "Táº¥t cáº£ khÃ¡ch hÃ ng";

    public string DieuKienHienThi
    {
        get
        {
            if (LaTheoDonHang)
                return $"ÄÆ¡n tá»« {GiaTriDonToiThieu:N0} Ä‘";

            if (SoLuongSanPham > 0)
                return $"Mua sáº£n pháº©m chá»‰ Ä‘á»‹nh";

            return "ChÆ°a cÃ³ Ä‘iá»u kiá»‡n";
        }
    }

    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsEdit) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsDelete) || Quyen.IsAdmin(Session.CurrentUser));

    /// <summary>
    /// GiÃ¡ trá»‹ giáº£m cá»§a chÆ°Æ¡ng trÃ¬nh theo tá»•ng Ä‘Æ¡n hÃ ng, Ä‘Ã£ Ã¡p lÃªn má»™t giÃ¡ trá»‹ Ä‘Æ¡n cho trÆ°á»›c.
    /// </summary>
    public decimal TinhGiamTheoDon(decimal giaTriDon)
    {
        if (giaTriDon <= 0 || GiaTriGiam <= 0)
            return 0;

        if (LaGiamPhanTram)
            return Math.Round(giaTriDon * Math.Clamp(GiaTriGiam, 0, 100) / 100m, 0);

        return Math.Min(Math.Max(0, GiaTriGiam), giaTriDon);
    }
}

/// <summary>
/// Chi tiáº¿t Ä‘iá»u kiá»‡n / Æ°u Ä‘Ã£i theo sáº£n pháº©m cá»§a chÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i
/// (báº£ng chi_tiet_khuyen_mai).
/// </summary>
public sealed partial class ChiTietKhuyenMai : ObservableObject
{
    public uint Id { get; set; }
    public uint ChuongTrinhId { get; set; }
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; } = "CÃ¡i";
    public decimal GiaBanHienTai { get; set; }

    [ObservableProperty]
    private int stt;

    /// <summary>Sá»‘ lÆ°á»£ng tá»‘i thiá»ƒu pháº£i mua Ä‘á»ƒ Ä‘Æ°á»£c hÆ°á»Ÿng Æ°u Ä‘Ã£i.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UuDaiHienThi))]
    [NotifyPropertyChangedFor(nameof(GiaSauGiam))]
    private decimal soLuongMua = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UuDaiHienThi))]
    [NotifyPropertyChangedFor(nameof(GiaSauGiam))]
    [NotifyPropertyChangedFor(nameof(IsGiamPhanTram))]
    [NotifyPropertyChangedFor(nameof(IsGiamSoTien))]
    private string loaiGiamGia = "PHAN_TRAM"; // "PHAN_TRAM" hoáº·c "SO_TIEN"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UuDaiHienThi))]
    [NotifyPropertyChangedFor(nameof(GiaSauGiam))]
    private decimal giaTriGiam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UuDaiHienThi))]
    private decimal soLuongTang;

    [ObservableProperty]
    private uint? sanPhamTangId;

    [ObservableProperty]
    private string? tenSanPhamTang;

    /// <summary>
    /// Sáº£n pháº©m táº·ng kÃ¨m Ä‘ang Ä‘Æ°á»£c chá»n trÃªn giao diá»‡n (khÃ´ng lÆ°u xuá»‘ng DB,
    /// DB chá»‰ lÆ°u <see cref="SanPhamTangId"/> vÃ  <see cref="TenSanPhamTang"/>).
    /// </summary>
    [ObservableProperty]
    private SanPham? sanPhamTang;

    /// <summary>Danh sÃ¡ch sáº£n pháº©m dÃ¹ng lÃ m nguá»“n cho Ã´ chá»n "hÃ ng táº·ng".</summary>
    [ObservableProperty]
    private List<SanPham>? danhSachSanPham;

    partial void OnSanPhamTangChanged(SanPham? value)
    {
        SanPhamTangId = value?.Id;
        TenSanPhamTang = value?.TenSanPham;
    }

    [ObservableProperty]
    private string? ghiChu;

    public bool IsGiamPhanTram => LoaiGiamGia == "PHAN_TRAM";
    public bool IsGiamSoTien => LoaiGiamGia == "SO_TIEN";

    public decimal GiaSauGiam
    {
        get
        {
            if (GiaBanHienTai <= 0)
                return 0;

            if (LoaiGiamGia == "PHAN_TRAM")
            {
                var pt = Math.Clamp(GiaTriGiam, 0, 100);
                return Math.Max(0, Math.Round(GiaBanHienTai * (1 - pt / 100m), 0));
            }

            var tien = Math.Clamp(GiaTriGiam, 0, GiaBanHienTai);
            return Math.Max(0, GiaBanHienTai - tien);
        }
    }

    public string UuDaiHienThi
    {
        get
        {
            if (SoLuongTang > 0)
            {
                var tenTang = string.IsNullOrWhiteSpace(TenSanPhamTang) ? "sáº£n pháº©m" : TenSanPhamTang;
                return $"Mua {SoLuongMua:0.##} táº·ng {SoLuongTang:0.##} {tenTang}";
            }

            if (LoaiGiamGia == "PHAN_TRAM")
            {
                return GiaTriGiam > 0 ? $"-{GiaTriGiam:0.##}%" : "0%";
            }

            return GiaTriGiam > 0 ? $"-{GiaTriGiam:N0} Ä‘" : "0 Ä‘";
        }
    }

    /// <summary>TÃ­nh sá»‘ tiá»n giáº£m trÃªn má»™t Ä‘Æ¡n giÃ¡ cá»§a sáº£n pháº©m.</summary>
    public decimal TinhGiamTrenDonGia(decimal donGia)
    {
        if (donGia <= 0 || GiaTriGiam <= 0)
            return 0;

        if (LoaiGiamGia == "PHAN_TRAM")
            return Math.Round(donGia * Math.Clamp(GiaTriGiam, 0, 100) / 100m, 0);

        return Math.Min(Math.Max(0, GiaTriGiam), donGia);
    }
}


