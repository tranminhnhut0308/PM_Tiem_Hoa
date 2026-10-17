using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một chương trình khuyến mãi (bảng chuong_trinh_khuyen_mai).
/// </summary>
public sealed partial class ChuongTrinhKhuyenMai : ObservableObject
{
    public uint Id { get; set; }

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

    public uint ChiNhanhId { get; set; } = 1;
    public uint? NguoiTaoId { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    public ObservableCollection<ChiTietKhuyenMai> DanhSachChiTiet { get; } = new();

    public int SoLuongSanPham => DanhSachChiTiet.Count;

    public string TrangThaiHienThi => TrangThai switch
    {
        "DANG_HOAT_DONG" => "Đang diễn ra",
        "TAM_NGUNG" => "Tạm ngưng",
        "DA_KET_THUC" => "Đã kết thúc",
        "CHUA_BAT_DAU" => "Chưa bắt đầu",
        _ => TrangThai
    };

    public string ThoiGianHienThi => $"{NgayBatDau:dd/MM/yyyy} - {NgayKetThuc:dd/MM/yyyy}";

    public string DoiTuongHienThi => string.IsNullOrWhiteSpace(TenNhomKhachHang)
        ? "Tất cả khách hàng"
        : $"Nhóm: {TenNhomKhachHang}";
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
