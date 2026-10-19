using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng tổng hợp tình hình nhập/xuất hàng hóa trong GIAO DIỆN 68.
/// Phân tích hai chiều luồng hàng (Inflow vs Outflow), đối chiếu chênh lệch và tỷ lệ tiêu thụ.
/// </summary>
public sealed class BaoCaoTinhHinhNhapXuatItem
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public int Stt { get; set; }
    public uint Id { get; set; }

    /// <summary>Mã hàng hóa / Mã nhóm / Ngày</summary>
    public string MaDoiTuong { get; set; } = string.Empty;

    /// <summary>Tên hàng hóa / Tên nhóm hàng / Ngày hiển thị</summary>
    public string TenDoiTuong { get; set; } = string.Empty;

    public string DonViTinh { get; set; } = string.Empty;
    public string NhomHang { get; set; } = string.Empty;

    // ============================================================
    // TÌNH HÌNH NHẬP (INFLOW)
    // ============================================================
    public int SoLanNhap { get; set; }
    public decimal SoLuongNhap { get; set; }
    public decimal GiaTriNhap { get; set; }
    public decimal DonGiaNhapTrungBinh => SoLuongNhap > 0 ? Math.Round(GiaTriNhap / SoLuongNhap, 0) : 0m;

    // ============================================================
    // TÌNH HÌNH XUẤT (OUTFLOW)
    // ============================================================
    public int SoLanXuat { get; set; }
    public decimal SoLuongXuat { get; set; }
    public decimal GiaTriXuatVon { get; set; } // Giá vốn của hàng xuất (COGS)
    public decimal DoanhThuXuat { get; set; } // Doanh thu bán hàng
    public decimal LoiNhuanGop => DoanhThuXuat - GiaTriXuatVon;

    // ============================================================
    // ĐỐI CHIẾU & HIỆU QUẢ TIÊU THỤ
    // ============================================================
    public decimal ChenhLechSoLuong => SoLuongNhap - SoLuongXuat;
    public decimal ChenhLechGiaTri => GiaTriNhap - GiaTriXuatVon;
    public decimal TyLeTieuThu => SoLuongNhap > 0 ? Math.Round((SoLuongXuat / SoLuongNhap) * 100m, 1) : 0m;

    // ============================================================
    // TỒN KHO HIỆN TẠI
    // ============================================================
    public decimal TonHienTai { get; set; }
    public decimal GiaTriTonHienTai { get; set; }

    // ============================================================
    // ĐỊNH DẠNG HIỂN THỊ
    // ============================================================
    public string SoLuongNhapHienThi => SoLuongNhap.ToString("N0", ViCulture);
    public string GiaTriNhapHienThi => GiaTriNhap.ToString("N0", ViCulture) + " đ";
    public string DonGiaNhapTbHienThi => DonGiaNhapTrungBinh.ToString("N0", ViCulture) + " đ";

    public string SoLuongXuatHienThi => SoLuongXuat.ToString("N0", ViCulture);
    public string GiaTriXuatVonHienThi => GiaTriXuatVon.ToString("N0", ViCulture) + " đ";
    public string DoanhThuXuatHienThi => DoanhThuXuat.ToString("N0", ViCulture) + " đ";
    public string LoiNhuanGopHienThi => LoiNhuanGop.ToString("N0", ViCulture) + " đ";

    public string ChenhLechSoLuongHienThi => (ChenhLechSoLuong > 0 ? "+" : "") + ChenhLechSoLuong.ToString("N0", ViCulture);
    public string ChenhLechGiaTriHienThi => (ChenhLechGiaTri > 0 ? "+" : "") + ChenhLechGiaTri.ToString("N0", ViCulture) + " đ";
    public string TyLeTieuThuHienThi => $"{TyLeTieuThu:0.#}%";

    public string TonHienTaiHienThi => TonHienTai.ToString("N0", ViCulture);
    public string GiaTriTonHienTaiHienThi => GiaTriTonHienTai.ToString("N0", ViCulture) + " đ";

    public string ChenhLechColor => ChenhLechSoLuong >= 0 ? "#137333" : "#C5221F";
    public string TyLeBadgeBg => TyLeTieuThu >= 80 ? "#E6F4EA" : (TyLeTieuThu >= 50 ? "#E8F0FE" : "#FEF7E0");
    public string TyLeBadgeFg => TyLeTieuThu >= 80 ? "#137333" : (TyLeTieuThu >= 50 ? "#1A73E8" : "#B06000");
}

/// <summary>
/// Gói dữ liệu báo cáo tổng hợp tình hình nhập/xuất (Giao diện 68).
/// </summary>
public sealed class BaoCaoTinhHinhNhapXuatDuLieu
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public string CheDoXem { get; set; } = "THEO_HANG_HOA";

    public decimal TongGiaTriNhap { get; set; }
    public decimal TongSoLuongNhap { get; set; }
    public int TongSoLanNhap { get; set; }

    public decimal TongGiaTriXuatVon { get; set; }
    public decimal TongDoanhThuXuat { get; set; }
    public decimal TongSoLuongXuat { get; set; }
    public int TongSoLanXuat { get; set; }
    public decimal TongLoiNhuanGop => TongDoanhThuXuat - TongGiaTriXuatVon;

    public decimal ChenhLechSoLuongTong => TongSoLuongNhap - TongSoLuongXuat;
    public decimal ChenhLechGiaTriTong => TongGiaTriNhap - TongGiaTriXuatVon;
    public decimal TyLeTieuThuTrungBinh => TongSoLuongNhap > 0 ? Math.Round((TongSoLuongXuat / TongSoLuongNhap) * 100m, 1) : 0m;

    public List<BaoCaoTinhHinhNhapXuatItem> Items { get; set; } = new();

    public string TuNgayHienThi => TuNgay.ToString("dd/MM/yyyy");
    public string DenNgayHienThi => DenNgay.ToString("dd/MM/yyyy");
    public string KhoangHienThi => $"Từ ngày {TuNgayHienThi} đến {DenNgayHienThi}";

    public string TongGiaTriNhapHienThi => TongGiaTriNhap.ToString("N0", ViCulture) + " đ";
    public string TongSoLuongNhapHienThi => TongSoLuongNhap.ToString("N0", ViCulture);

    public string TongGiaTriXuatVonHienThi => TongGiaTriXuatVon.ToString("N0", ViCulture) + " đ";
    public string TongDoanhThuXuatHienThi => TongDoanhThuXuat.ToString("N0", ViCulture) + " đ";
    public string TongSoLuongXuatHienThi => TongSoLuongXuat.ToString("N0", ViCulture);
    public string TongLoiNhuanGopHienThi => TongLoiNhuanGop.ToString("N0", ViCulture) + " đ";

    public string ChenhLechGiaTriTongHienThi => (ChenhLechGiaTriTong > 0 ? "+" : "") + ChenhLechGiaTriTong.ToString("N0", ViCulture) + " đ";
    public string TyLeTieuThuTbHienThi => $"{TyLeTieuThuTrungBinh:0.#}%";
}
