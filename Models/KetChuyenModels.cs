using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một kỳ kế toán trong bảng ky_ke_toan.
/// </summary>
public sealed class KyKeToanItem
{
    public uint Id { get; set; }
    public string MaKy { get; set; } = "";
    public string TenKy { get; set; } = "";
    public DateTime NgayBatDauKy { get; set; }
    public DateTime NgayKetThucKy { get; set; }
    public bool TrangThaiKhoaSo { get; set; }

    public string TrangThaiHienThi => TrangThaiKhoaSo ? "🔒 Đã khóa sổ" : "🔓 Đang mở";
    public string ThoiGianHienThi => $"{NgayBatDauKy:dd/MM/yyyy} - {NgayKetThucKy:dd/MM/yyyy}";
    public string HienThiDayDu => $"{TenKy} ({ThoiGianHienThi}) - {TrangThaiHienThi}";

    public override string ToString() => HienThiDayDu;
}

/// <summary>
/// Chi tiết dòng hàng hóa được kết chuyển sang kỳ mới.
/// </summary>
public sealed class KetChuyenHangHoaItem
{
    public int SoThuTu { get; set; }
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public uint KhoHangId { get; set; } = 1;
    public string TenKho { get; set; } = "Kho Tổng";
    public decimal SoLuongTon { get; set; }
    public decimal GiaVon { get; set; }
    public decimal ThanhTien { get; set; }

    public string SoLuongHienThi => SoLuongTon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string ThanhTienHienThi => ThanhTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>
/// Dữ liệu xem trước và kiểm tra điều kiện kết chuyển hàng hóa (Giao diện 53).
/// </summary>
public sealed class KetChuyenHangHoaPreview
{
    public KyKeToanItem? KyNguon { get; set; }
    public KyKeToanItem? KyDich { get; set; }
    public int TongSoMatHang { get; set; }
    public decimal TongSoLuong { get; set; }
    public decimal TongGiaTri { get; set; }
    public bool DaCoDuLieuDauKyDich { get; set; }
    public int SoBanGhiDauKyHienCo { get; set; }
    public bool KyDichDaKhoaSo { get; set; }
    public List<KetChuyenHangHoaItem> Items { get; set; } = new();
    public string? CanhBao { get; set; }

    public string TongSoLuongHienThi => TongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string TongGiaTriHienThi => TongGiaTri.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>
/// Chi tiết dòng công nợ của đối tác được kết chuyển sang kỳ mới.
/// </summary>
public sealed class KetChuyenCongNoItem
{
    public int SoThuTu { get; set; }
    public string LoaiDoiTac { get; set; } = "KHACH_HANG"; // KHACH_HANG hoặc NHA_CUNG_CAP
    public uint DoiTacId { get; set; }
    public string MaDoiTac { get; set; } = "";
    public string TenDoiTac { get; set; } = "";
    public string? SoDienThoai { get; set; }
    public decimal SoDuNoCuoiKy { get; set; }

    public string LoaiDoiTacHienThi => LoaiDoiTac == "KHACH_HANG" ? "Khách hàng" : "Nhà cung cấp";
    public string LoaiCongNo => LoaiDoiTac == "KHACH_HANG" ? "Phải thu" : "Phải trả";

    public string SoDuHienThi => SoDuNoCuoiKy.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string MauLoaiCongNo => LoaiDoiTac == "KHACH_HANG" ? "#D92D20" : "#175CD3";
    public string NenLoaiCongNo => LoaiDoiTac == "KHACH_HANG" ? "#FEE4E2" : "#EFF8FF";
}

/// <summary>
/// Dữ liệu xem trước và kiểm tra điều kiện kết chuyển công nợ (Giao diện 54).
/// </summary>
public sealed class KetChuyenCongNoPreview
{
    public KyKeToanItem? KyNguon { get; set; }
    public KyKeToanItem? KyDich { get; set; }
    public int TongSoDoiTac { get; set; }
    public int SoKhachHang { get; set; }
    public int SoNhaCungCap { get; set; }
    public decimal TongNoPhaiThu { get; set; }
    public decimal TongNoPhaiTra { get; set; }
    public decimal TongConNoRong => TongNoPhaiThu - TongNoPhaiTra;
    public bool DaCoDuLieuDauKyDich { get; set; }
    public int SoBanGhiDauKyHienCo { get; set; }
    public bool KyDichDaKhoaSo { get; set; }
    public List<KetChuyenCongNoItem> Items { get; set; } = new();
    public string? CanhBao { get; set; }

    public string TongPhaiThuHienThi => TongNoPhaiThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TongPhaiTraHienThi => TongNoPhaiTra.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TongConNoRongHienThi => TongConNoRong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>
/// Kết quả sau khi thực hiện thao tác kết chuyển số liệu.
/// </summary>
public sealed class KetChuyenKetQua
{
    public bool ThanhCong { get; set; }
    public string ThongBao { get; set; } = "";
    public int SoBanGhi { get; set; }
    public decimal TongGiaTri { get; set; }
    public string? MaKyDich { get; set; }
    public string? TenKyDich { get; set; }
}
