using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ShopHoa.Services;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một chương trình khuyến mãi (bảng chuong_trinh_khuyen_mai).
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
    private uint? nhomKhachHangId;

    [ObservableProperty]
    private string? tenNhomKhachHang;

    [ObservableProperty]
    private string trangThai = "DANG_HOAT_DONG";

    [ObservableProperty]
    private string loaiKhuyenMai = "THEO_HANG_HOA";

    [ObservableProperty]
    private string? ghiChu = "Chương trình khuyến mãi ưu đãi khách hàng";

    [ObservableProperty]
    private string? tomTatUuDai;

    public uint ChiNhanhId { get; set; } = 1;
    public uint? NguoiTaoId { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    public ObservableCollection<ChiTietKhuyenMai> DanhSachChiTiet { get; } = new();

    public int SoLuongSanPham => DanhSachChiTiet.Count;

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
        "DANG_HOAT_DONG" => "Đang diễn ra",
        "CHUA_BAT_DAU" => "Chưa bắt đầu",
        "TAM_NGUNG" => "Tạm ngưng",
        "DA_KET_THUC" => "Đã kết thúc",
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

    public string LoaiKhuyenMaiHienThi => LoaiKhuyenMai switch
    {
        "THEO_HANG_HOA" => "Theo hàng hóa",
        "THEO_HOA_DON" => "Theo hóa đơn",
        "TANG_KEM" => "Tặng hàng kèm",
        _ => "Chiết khấu giá"
    };

    public string UuDaiHienThi
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TomTatUuDai))
                return TomTatUuDai;

            if (SoLuongSanPham > 0)
                return $"Áp dụng {SoLuongSanPham} sản phẩm";

            return "Toàn bộ danh mục";
        }
    }

    public string DoiTuongHienThi => string.IsNullOrWhiteSpace(TenNhomKhachHang)
        ? "Tất cả khách hàng"
        : $"Nhóm: {TenNhomKhachHang}";

    public bool CanEdit => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsEdit) || Quyen.IsAdmin(Session.CurrentUser));
    public bool CanDelete => Session.CurrentUser != null && (Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsDelete) || Quyen.IsAdmin(Session.CurrentUser));
}

/// <summary>
/// Chi tiết hàng hóa được áp dụng ưu đãi trong chương trình khuyến mãi (bảng chi_tiet_khuyen_mai).
/// </summary>
public sealed partial class ChiTietKhuyenMai : ObservableObject
{
    public uint Id { get; set; }
    public uint ChuongTrinhId { get; set; }
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; } = "Cái";
    public decimal GiaBanHienTai { get; set; }

    [ObservableProperty]
    private int stt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UuDaiHienThi))]
    [NotifyPropertyChangedFor(nameof(GiaSauGiam))]
    private decimal soLuongMua = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UuDaiHienThi))]
    [NotifyPropertyChangedFor(nameof(GiaSauGiam))]
    [NotifyPropertyChangedFor(nameof(IsGiamPhanTram))]
    [NotifyPropertyChangedFor(nameof(IsGiamSoTien))]
    private string loaiGiamGia = "PHAN_TRAM"; // "PHAN_TRAM" hoặc "SO_TIEN"

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
            else
            {
                var tien = Math.Clamp(GiaTriGiam, 0, GiaBanHienTai);
                return Math.Max(0, GiaBanHienTai - tien);
            }
        }
    }

    public string UuDaiHienThi
    {
        get
        {
            if (SoLuongTang > 0)
            {
                var tenTang = string.IsNullOrWhiteSpace(TenSanPhamTang) ? "sản phẩm" : TenSanPhamTang;
                return $"Mua {SoLuongMua:0.##} tặng {SoLuongTang:0.##} {tenTang}";
            }

            if (LoaiGiamGia == "PHAN_TRAM")
            {
                return GiaTriGiam > 0 ? $"-{GiaTriGiam:0.##}%" : "0%";
            }

            return GiaTriGiam > 0 ? $"-{GiaTriGiam:N0} đ" : "0 đ";
        }
    }
}
