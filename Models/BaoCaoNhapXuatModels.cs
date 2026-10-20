using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Tùy chọn nhóm hàng cho ComboBox bộ lọc Báo cáo nhập xuất.
/// </summary>
public sealed class BaoCaoNhapXuatNhomOption
{
    public int Id { get; set; }
    public string TenNhom { get; set; } = string.Empty;
    public string TenHienThi { get; set; } = string.Empty;
}

/// <summary>
/// Tùy chọn mặt hàng cho ComboBox bộ lọc Thẻ kho / Chi tiết nhập xuất.
/// </summary>
public sealed class BaoCaoNhapXuatSanPhamOption
{
    public uint Id { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string TenSanPham { get; set; } = string.Empty;
    public string TenHienThi => Id == 0 ? "--- Tất cả mặt hàng ---" : $"{MaSanPham} - {TenSanPham}";
}

/// <summary>
/// Dòng dữ liệu cho GIAO DIỆN 63 – Báo cáo tổng hợp nhập xuất.
/// Tổng hợp xuất - nhập - tồn từng sản phẩm theo khoảng thời gian.
/// </summary>
public sealed class BaoCaoTongHopNhapXuatItem
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public int Stt { get; set; }
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string TenSanPham { get; set; } = string.Empty;
    public string TenNhom { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;

    // Tồn đầu kỳ
    public decimal TonDauKy { get; set; }
    public decimal GiaTriDauKy { get; set; }

    // Nhập trong kỳ
    public decimal NhapMua { get; set; }
    public decimal GiaTriNhapMua { get; set; }
    public decimal NhapTraLai { get; set; }
    public decimal GiaTriNhapTraLai { get; set; }
    public decimal NhapKhac { get; set; }
    public decimal GiaTriNhapKhac { get; set; }
    public decimal TongNhap { get; set; }
    public decimal GiaTriNhap { get; set; }

    // Xuất trong kỳ
    public decimal XuatBan { get; set; }
    public decimal GiaTriXuatBan { get; set; }
    public decimal XuatTraNcc { get; set; }
    public decimal GiaTriXuatTraNcc { get; set; }
    public decimal XuatNoiBo { get; set; }
    public decimal GiaTriXuatNoiBo { get; set; }
    public decimal TongXuat { get; set; }
    public decimal GiaTriXuat { get; set; }

    // Tồn cuối kỳ
    public decimal TonCuoiKy { get; set; }
    public decimal GiaVon { get; set; }
    public decimal GiaTriTonCuoi { get; set; }

    // Hiển thị định dạng
    public string TonDauKyHienThi => TonDauKy.ToString("N0", ViCulture);
    public string GiaTriDauKyHienThi => GiaTriDauKy.ToString("N0", ViCulture) + " đ";

    public string NhapMuaHienThi => NhapMua > 0 ? NhapMua.ToString("N0", ViCulture) : "-";
    public string NhapTraLaiHienThi => NhapTraLai > 0 ? NhapTraLai.ToString("N0", ViCulture) : "-";
    public string NhapKhacHienThi => NhapKhac > 0 ? NhapKhac.ToString("N0", ViCulture) : "-";
    public string TongNhapHienThi => TongNhap.ToString("N0", ViCulture);
    public string GiaTriNhapHienThi => GiaTriNhap.ToString("N0", ViCulture) + " đ";

    public string XuatBanHienThi => XuatBan > 0 ? XuatBan.ToString("N0", ViCulture) : "-";
    public string XuatTraNccHienThi => XuatTraNcc > 0 ? XuatTraNcc.ToString("N0", ViCulture) : "-";
    public string XuatNoiBoHienThi => XuatNoiBo > 0 ? XuatNoiBo.ToString("N0", ViCulture) : "-";
    public string TongXuatHienThi => TongXuat.ToString("N0", ViCulture);
    public string GiaTriXuatHienThi => GiaTriXuat.ToString("N0", ViCulture) + " đ";

    public string TonCuoiKyHienThi => TonCuoiKy.ToString("N0", ViCulture);
    public string GiaVonHienThi => GiaVon.ToString("N0", ViCulture) + " đ";
    public string GiaTriTonCuoiHienThi => GiaTriTonCuoi.ToString("N0", ViCulture) + " đ";
}

/// <summary>
/// Gói toàn bộ dữ liệu Báo cáo tổng hợp nhập xuất (KPI + Bảng dữ liệu).
/// </summary>
public sealed class BaoCaoTongHopNhapXuatDuLieu
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public int TongSoMatHang { get; set; }

    public decimal TongTonDau { get; set; }
    public decimal TongGiaTriDau { get; set; }

    public decimal TongNhap { get; set; }
    public decimal TongGiaTriNhap { get; set; }

    public decimal TongXuat { get; set; }
    public decimal TongGiaTriXuat { get; set; }

    public decimal TongTonCuoi { get; set; }
    public decimal TongGiaTriTonCuoi { get; set; }

    public List<BaoCaoTongHopNhapXuatItem> Items { get; set; } = new();

    public string TuNgayHienThi => TuNgay.ToString("dd/MM/yyyy");
    public string DenNgayHienThi => DenNgay.ToString("dd/MM/yyyy");
    public string KhoangHienThi => $"Từ ngày {TuNgayHienThi} đến {DenNgayHienThi}";

    public string TongTonDauHienThi => TongTonDau.ToString("N0", ViCulture);
    public string TongGiaTriDauHienThi => TongGiaTriDau.ToString("N0", ViCulture) + " đ";

    public string TongNhapHienThi => TongNhap.ToString("N0", ViCulture);
    public string TongGiaTriNhapHienThi => TongGiaTriNhap.ToString("N0", ViCulture) + " đ";

    public string TongXuatHienThi => TongXuat.ToString("N0", ViCulture);
    public string TongGiaTriXuatHienThi => TongGiaTriXuat.ToString("N0", ViCulture) + " đ";

    public string TongTonCuoiHienThi => TongTonCuoi.ToString("N0", ViCulture);
    public string TongGiaTriTonCuoiHienThi => TongGiaTriTonCuoi.ToString("N0", ViCulture) + " đ";
}

/// <summary>
/// Dòng dữ liệu cho GIAO DIỆN 64 – Báo cáo chi tiết nhập xuất (Thẻ kho).
/// Thể hiện từng giao dịch nhập/xuất cụ thể.
/// </summary>
public sealed class BaoCaoChiTietNhapXuatItem
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public int Stt { get; set; }
    public DateTime ThoiGian { get; set; }
    public string MaChungTu { get; set; } = string.Empty;
    public string LoaiGiaoDich { get; set; } = string.Empty; // "NHAP" hoặc "XUAT"
    public string LoaiChungTu { get; set; } = string.Empty;
    public string LoaiChungTuHienThi { get; set; } = string.Empty;
    public string MauLoaiChungTu { get; set; } = "#087F6E";
    public string MauNenLoaiChungTu { get; set; } = "#E7F8F0";

    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string TenSanPham { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public string DoiTac { get; set; } = string.Empty;

    // Nhập
    public decimal? SoLuongNhap { get; set; }
    public decimal? DonGiaNhap { get; set; }
    public decimal? ThanhTienNhap { get; set; }

    // Xuất
    public decimal? SoLuongXuat { get; set; }
    public decimal? DonGiaXuat { get; set; }
    public decimal? ThanhTienXuat { get; set; }

    // Tồn sau giao dịch (nếu lọc 1 sản phẩm)
    public decimal? TonSau { get; set; }
    public string GhiChu { get; set; } = string.Empty;

    // Hiển thị định dạng
    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");
    public string SoLuongNhapHienThi => SoLuongNhap.HasValue && SoLuongNhap.Value > 0 ? SoLuongNhap.Value.ToString("N0", ViCulture) : "-";
    public string DonGiaNhapHienThi => DonGiaNhap.HasValue && DonGiaNhap.Value > 0 ? DonGiaNhap.Value.ToString("N0", ViCulture) + " đ" : "-";
    public string ThanhTienNhapHienThi => ThanhTienNhap.HasValue && ThanhTienNhap.Value > 0 ? ThanhTienNhap.Value.ToString("N0", ViCulture) + " đ" : "-";

    public string SoLuongXuatHienThi => SoLuongXuat.HasValue && SoLuongXuat.Value > 0 ? SoLuongXuat.Value.ToString("N0", ViCulture) : "-";
    public string DonGiaXuatHienThi => DonGiaXuat.HasValue && DonGiaXuat.Value > 0 ? DonGiaXuat.Value.ToString("N0", ViCulture) + " đ" : "-";
    public string ThanhTienXuatHienThi => ThanhTienXuat.HasValue && ThanhTienXuat.Value > 0 ? ThanhTienXuat.Value.ToString("N0", ViCulture) + " đ" : "-";

    public string TonSauHienThi => TonSau.HasValue ? TonSau.Value.ToString("N0", ViCulture) : "-";
}

/// <summary>
/// Gói toàn bộ dữ liệu Báo cáo chi tiết nhập xuất (KPI + Danh sách phân trang).
/// </summary>
public sealed class BaoCaoChiTietNhapXuatDuLieu
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    public int TongSoGiaoDich { get; set; }
    public decimal TongSoLuongNhap { get; set; }
    public decimal TongThanhTienNhap { get; set; }
    public decimal TongSoLuongXuat { get; set; }
    public decimal TongThanhTienXuat { get; set; }

    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<BaoCaoChiTietNhapXuatItem> Items { get; set; } = new();

    public string TuNgayHienThi => TuNgay.ToString("dd/MM/yyyy");
    public string DenNgayHienThi => DenNgay.ToString("dd/MM/yyyy");
    public string KhoangHienThi => $"Từ ngày {TuNgayHienThi} đến {DenNgayHienThi}";

    public string TongSoGiaoDichHienThi => TongSoGiaoDich.ToString("N0", ViCulture);
    public string TongSoLuongNhapHienThi => TongSoLuongNhap.ToString("N0", ViCulture);
    public string TongThanhTienNhapHienThi => TongThanhTienNhap.ToString("N0", ViCulture) + " đ";
    public string TongSoLuongXuatHienThi => TongSoLuongXuat.ToString("N0", ViCulture);
    public string TongThanhTienXuatHienThi => TongThanhTienXuat.ToString("N0", ViCulture) + " đ";
}
