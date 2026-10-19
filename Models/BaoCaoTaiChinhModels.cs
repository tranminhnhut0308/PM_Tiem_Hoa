using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng chỉ tiêu trong bảng Báo cáo tài chính (Kết quả kinh doanh hoặc Vốn kinh doanh).
/// </summary>
public sealed class ChiTieuBaoCaoItem
{
    public int Stt { get; set; }
    public string MaSo { get; set; } = "";
    public string ChiTieu { get; set; } = "";
    public string ThuyetMinh { get; set; } = "";
    public decimal KyNay { get; set; }
    public double? TyLe { get; set; }
    public string GhiChu { get; set; } = "";
    public bool IsHeader { get; set; }
    public bool IsHighlight { get; set; }
    public int CapDo { get; set; } = 0; // Thụt lề theo cấp độ

    public string IndentPadding => CapDo switch
    {
        1 => "16,0,0,0",
        2 => "32,0,0,0",
        _ => "0,0,0,0"
    };

    public string KyNayHienThi => IsHeader ? "" : KyNay.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeHienThi => TyLe.HasValue ? $"{TyLe.Value:F1}%" : "";
    public string FontWeightHienThi => IsHighlight ? "Bold" : "Normal";
    public string MauSacHienThi => IsHighlight ? "#087F6E" : "#2C3A37";
}

/// <summary>
/// Chi tiết nhóm chi phí kinh doanh trong kỳ.
/// </summary>
public sealed class ChiPhiTheoNhomItem
{
    public int Stt { get; set; }
    public string MaNhom { get; set; } = "";
    public string TenNhom { get; set; } = "";
    public int SoChungTu { get; set; }
    public decimal SoTien { get; set; }
    public double TyLePhanTram { get; set; }

    public string SoTienHienThi => SoTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeHienThi => $"{TyLePhanTram:F1}%";
}

/// <summary>
/// Chi tiết kết quả kinh doanh theo ngày trong khoảng thời gian.
/// </summary>
public sealed class KetQuaKinhDoanhTheoNgayItem
{
    public int Stt { get; set; }
    public DateTime Ngay { get; set; }
    public int SoHoaDon { get; set; }
    public decimal DoanhThuHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal DoanhThuThuan { get; set; }
    public decimal GiaVon { get; set; }
    public decimal LoiNhuanGop { get; set; }
    public decimal ChiPhi { get; set; }
    public decimal LoiNhuanThuan { get; set; }

    public string NgayHienThi => Ngay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public string DoanhThuHangHienThi => DoanhThuHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiamGiaHienThi => GiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string DoanhThuThuanHienThi => DoanhThuThuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanGopHienThi => LoiNhuanGop.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string ChiPhiHienThi => ChiPhi.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanThuanHienThi => LoiNhuanThuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>
/// Toàn bộ dữ liệu của Giao diện 57 – Báo cáo kết quả kinh doanh.
/// </summary>
public sealed class BaoCaoKetQuaKinhDoanhDuLieu
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    // 1. Doanh thu bán hàng
    public decimal DoanhThuBanHang { get; set; }
    // 2. Giảm trừ doanh thu (chiết khấu)
    public decimal GiamTruDoanhThu { get; set; }
    // 3. Doanh thu thuần (1 - 2)
    public decimal DoanhThuThuan { get; set; }
    // 4. Giá vốn hàng bán
    public decimal GiaVonHangBan { get; set; }
    // 5. Lợi nhuận gộp (3 - 4)
    public decimal LoiNhuanGop { get; set; }
    public double TySuatLoiNhuanGop { get; set; }

    // 6. Chi phí kinh doanh (hoạt động)
    public decimal ChiPhiKinhDoanh { get; set; }
    // 7. Lợi nhuận thuần từ hoạt động kinh doanh (5 - 6)
    public decimal LoiNhuanThuanHDKD { get; set; }

    // 8. Thu nhập khác
    public decimal ThuNhapKhac { get; set; }
    // 9. Chi phí khác
    public decimal ChiPhiKhac { get; set; }
    // 10. Lợi nhuận khác (8 - 9)
    public decimal LoiNhuanKhac { get; set; }

    // 11. Tổng lợi nhuận kế toán (7 + 10)
    public decimal TongLoiNhuan { get; set; }
    public double TySuatLoiNhuanThuan { get; set; }

    public int TongSoHoaDon { get; set; }
    public int TongSoPhieuChi { get; set; }

    // Bảng chuẩn kế toán
    public List<ChiTieuBaoCaoItem> BangChiTieu { get; set; } = new();

    // Bảng chi tiết chi phí theo nhóm
    public List<ChiPhiTheoNhomItem> ChiTietChiPhi { get; set; } = new();

    // Bảng chi tiết kết quả theo ngày
    public List<KetQuaKinhDoanhTheoNgayItem> ChiTietTheoNgay { get; set; } = new();

    public string TuNgayHienThi => TuNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public string DenNgayHienThi => DenNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public string KhoangThoiGianHienThi => $"{TuNgayHienThi} - {DenNgayHienThi}";
}

/// <summary>
/// Chi tiết tồn kho theo nhóm sản phẩm cho Báo cáo vốn kinh doanh.
/// </summary>
public sealed class TonKhoTheoNhomItem
{
    public int Stt { get; set; }
    public string TenNhom { get; set; } = "";
    public int SoMatHang { get; set; }
    public decimal SoLuongTon { get; set; }
    public decimal GiaTriTonKho { get; set; }
    public double TyTrong { get; set; }

    public string SoLuongTonHienThi => SoLuongTon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string GiaTriTonKhoHienThi => GiaTriTonKho.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyTrongHienThi => $"{TyTrong:F1}%";
}

/// <summary>
/// Chi tiết công nợ đối tác (Khách hàng hoặc Nhà cung cấp) trong Báo cáo vốn kinh doanh.
/// </summary>
public sealed class CongNoDoiTacItem
{
    public int Stt { get; set; }
    public string LoaiDoiTac { get; set; } = "";
    public uint DoiTacId { get; set; }
    public string MaDoiTac { get; set; } = "";
    public string TenDoiTac { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public decimal SoDuNo { get; set; }
    public double TyTrong { get; set; }

    public string SoDuNoHienThi => SoDuNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyTrongHienThi => $"{TyTrong:F1}%";
}

/// <summary>
/// Toàn bộ dữ liệu của Giao diện 58 – Báo cáo vốn kinh doanh (Cơ cấu vốn & Tài sản lưu động).
/// </summary>
public sealed class BaoCaoVonKinhDoanhDuLieu
{
    public DateTime MocThoiGian { get; set; }

    // 1. Tiền mặt tại quỹ
    public decimal TienMatTaiQuy { get; set; }
    // 2. Tiền gửi ngân hàng
    public decimal TienGuiNganHang { get; set; }
    // Tổng tiền & tương đương tiền (1 + 2)
    public decimal TongTienMatVaNganHang => TienMatTaiQuy + TienGuiNganHang;

    // 3. Giá trị hàng tồn kho
    public decimal GiaTriHangTonKho { get; set; }
    public int TongSoMatHangTon { get; set; }
    public decimal TongSoLuongTon { get; set; }

    // 4. Công nợ phải thu của khách hàng
    public decimal PhaiThuKhachHang { get; set; }
    public int SoKhachHangNo { get; set; }

    // TỔNG TÀI SẢN LƯU ĐỘNG = (1 + 2 + 3 + 4)
    public decimal TongTaiSanLuuDong => TongTienMatVaNganHang + GiaTriHangTonKho + PhaiThuKhachHang;

    // 5. Nợ phải trả nhà cung cấp
    public decimal PhaiTraNhaCungCap { get; set; }
    public int SoNhaCungCapNo { get; set; }

    // VỐN KINH DOANH RÒNG (VỐN LƯU ĐỘNG THUẦN) = TỔNG TÀI SẢN LƯU ĐỘNG - PHẢI TRẢ NHÀ CUNG CẤP
    public decimal VonKinhDoanhRong => TongTaiSanLuuDong - PhaiTraNhaCungCap;

    // Bảng chỉ tiêu vốn
    public List<ChiTieuBaoCaoItem> BangChiTieu { get; set; } = new();

    // Chi tiết tồn kho theo nhóm
    public List<TonKhoTheoNhomItem> ChiTietTonKhoTheoNhom { get; set; } = new();

    // Top khách hàng nợ nhiều nhất
    public List<CongNoDoiTacItem> TopKhachHangNo { get; set; } = new();

    // Top nhà cung cấp tiệm nợ nhiều nhất
    public List<CongNoDoiTacItem> TopNhaCungCapNo { get; set; } = new();

    public string MocThoiGianHienThi => MocThoiGian.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
