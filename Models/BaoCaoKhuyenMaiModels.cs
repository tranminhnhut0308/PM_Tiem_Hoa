using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng tổng hợp chương trình khuyến mãi trong GIAO DIỆN 67.
/// </summary>
public sealed class BaoCaoTongHopKhuyenMaiItem
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public int Stt { get; set; }
    public uint Id { get; set; }
    public string MaChuongTrinh { get; set; } = string.Empty;
    public string TenChuongTrinh { get; set; } = string.Empty;
    public string LoaiKhuyenMai { get; set; } = "THEO_HANG_HOA";
    public string LoaiKhuyenMaiHienThi { get; set; } = "Theo hàng hóa";

    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }
    public string ThoiGianHienThi => $"{NgayBatDau:dd/MM/yyyy} - {NgayKetThuc:dd/MM/yyyy}";

    public string DoiTuongHienThi { get; set; } = "Tất cả khách hàng";
    public string TrangThai { get; set; } = "DANG_HOAT_DONG";
    public string TrangThaiHienThi { get; set; } = "Đang diễn ra";
    public string TrangThaiBadgeBg { get; set; } = "#E6F4EA";
    public string TrangThaiBadgeFg { get; set; } = "#137333";

    public int SoLuongSanPham { get; set; }
    public string TomTatUuDai { get; set; } = string.Empty;

    // Số liệu thực tế phát sinh trong thời gian khuyến mãi
    public int SoHoaDonApDung { get; set; }
    public decimal TongDoanhThu { get; set; }
    public decimal TongTienKhuyenMai { get; set; }
    public string GhiChu { get; set; } = string.Empty;

    // Định dạng hiển thị
    public string SoHoaDonHienThi => SoHoaDonApDung.ToString("N0", ViCulture);
    public string TongDoanhThuHienThi => TongDoanhThu.ToString("N0", ViCulture) + " đ";
    public string TongTienKhuyenMaiHienThi => TongTienKhuyenMai.ToString("N0", ViCulture) + " đ";
    public string SoSanPhamHienThi => $"{SoLuongSanPham:N0} mặt hàng";
}

/// <summary>
/// Gói dữ liệu báo cáo tổng hợp khuyến mãi (KPI + Danh sách).
/// </summary>
public sealed class BaoCaoTongHopKhuyenMaiDuLieu
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    public int TongSoChuongTrinh { get; set; }
    public int SoChuongTrinhDangChay { get; set; }
    public int TongSoMatHangApDung { get; set; }
    public int TongSoHoaDonApDung { get; set; }
    public decimal TongDoanhThu { get; set; }
    public decimal TongGiaTriKhuyenMai { get; set; }

    public List<BaoCaoTongHopKhuyenMaiItem> Items { get; set; } = new();

    public string TuNgayHienThi => TuNgay.ToString("dd/MM/yyyy");
    public string DenNgayHienThi => DenNgay.ToString("dd/MM/yyyy");
    public string KhoangHienThi => $"Từ ngày {TuNgayHienThi} đến {DenNgayHienThi}";

    public string TongSoChuongTrinhHienThi => TongSoChuongTrinh.ToString("N0", ViCulture);
    public string SoChuongTrinhDangChayHienThi => SoChuongTrinhDangChay.ToString("N0", ViCulture);
    public string TongSoMatHangApDungHienThi => TongSoMatHangApDung.ToString("N0", ViCulture);
    public string TongSoHoaDonApDungHienThi => TongSoHoaDonApDung.ToString("N0", ViCulture);
    public string TongDoanhThuHienThi => TongDoanhThu.ToString("N0", ViCulture) + " đ";
    public string TongGiaTriKhuyenMaiHienThi => TongGiaTriKhuyenMai.ToString("N0", ViCulture) + " đ";
}
