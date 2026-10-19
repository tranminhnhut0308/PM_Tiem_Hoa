using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Đại diện cho một dòng chi tiết công nợ theo phiếu/chứng từ (GIAO DIỆN 71).
/// </summary>
public sealed class BaoCaoChiTietCongNoPhieuItem
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public int Stt { get; set; }
    public uint Id { get; set; }

    /// <summary>Thời gian lập chứng từ</summary>
    public DateTime NgayChungTu { get; set; }
    public string ThoiGianHienThi => NgayChungTu.ToString("dd/MM/yyyy HH:mm", ViCulture);
    public string NgayHienThi => NgayChungTu.ToString("dd/MM/yyyy", ViCulture);

    /// <summary>Mã chứng từ (HD..., PN..., PT..., PC..., CNDK...)</summary>
    public string MaChungTu { get; set; } = string.Empty;

    /// <summary>Loại chứng từ hiển thị (Hóa đơn bán hàng, Phiếu nhập hàng, Phiếu trả hàng, v.v.)</summary>
    public string LoaiChungTu { get; set; } = "Hóa đơn bán hàng";

    /// <summary>Mã loại chứng từ kỹ thuật: HOA_DON, PHIEU_NHAP, PHIEU_TRA, XUAT_TRA_NCC, NO_DAU_KY, PHIEU_THU, PHIEU_CHI</summary>
    public string MaLoaiChungTu { get; set; } = "HOA_DON";

    /// <summary>Loại đối tác: KHACH_HANG hoặc NHA_CUNG_CAP</summary>
    public string LoaiDoiTac { get; set; } = "KHACH_HANG";
    public string LoaiDoiTacHienThi => LoaiDoiTac == "KHACH_HANG" ? "Khách hàng" : "Nhà cung cấp";

    /// <summary>Loại công nợ: Phải thu (Khách hàng) hoặc Phải trả (Nhà cung cấp)</summary>
    public string LoaiCongNo { get; set; } = "Phải thu";
    public string MauLoaiCongNo => LoaiCongNo == "Phải thu" ? "#D92D20" : "#175CD3";
    public string NenLoaiCongNo => LoaiCongNo == "Phải thu" ? "#FEE4E2" : "#EFF8FF";

    /// <summary>Thông tin đối tác</summary>
    public uint DoiTacId { get; set; }
    public string MaDoiTac { get; set; } = string.Empty;
    public string TenDoiTac { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;
    public string DoiTacHienThi => string.IsNullOrWhiteSpace(SoDienThoai)
        ? $"{TenDoiTac} ({MaDoiTac})"
        : $"{TenDoiTac} ({MaDoiTac}) • {SoDienThoai}";

    /// <summary>Giá trị phát sinh của chứng từ (Tổng tiền bán, Tổng tiền nhập, Nợ đầu kỳ)</summary>
    public decimal GiaTriPhatSinh { get; set; }
    public string GiaTriPhatSinhHienThi => GiaTriPhatSinh.ToString("N0", ViCulture) + " đ";

    /// <summary>Số tiền đã thanh toán cho chứng từ này</summary>
    public decimal DaThanhToan { get; set; }
    public string DaThanhToanHienThi => DaThanhToan.ToString("N0", ViCulture) + " đ";

    /// <summary>Số dư nợ còn lại của chứng từ</summary>
    public decimal ConNo { get; set; }
    public string ConNoHienThi => ConNo.ToString("N0", ViCulture) + " đ";

    /// <summary>Trạng thái thanh toán của chứng từ</summary>
    public string TrangThaiThanhToan
    {
        get
        {
            if (ConNo <= 0) return "Đã thanh toán đủ";
            if (DaThanhToan > 0) return "Thanh toán 1 phần";
            return "Chưa thanh toán";
        }
    }

    public string TrangThaiBadgeBg
    {
        get
        {
            if (ConNo <= 0) return "#ECFDF3";
            if (DaThanhToan > 0) return "#FEF0C7";
            return "#FEF3F2";
        }
    }

    public string TrangThaiBadgeFg
    {
        get
        {
            if (ConNo <= 0) return "#027A48";
            if (DaThanhToan > 0) return "#B54708";
            return "#B42318";
        }
    }

    /// <summary>Ghi chú / Diễn giải chứng từ</summary>
    public string GhiChu { get; set; } = string.Empty;

    /// <summary>Có thể mở cửa sổ xem chi tiết chứng từ hay không</summary>
    public bool CoTheXemChiTiet => MaLoaiChungTu is "HOA_DON" or "PHIEU_NHAP" or "PHIEU_CHI" or "PHIEU_THU";
}

/// <summary>
/// Tiêu chí lọc báo cáo chi tiết công nợ theo phiếu (GIAO DIỆN 71).
/// </summary>
public sealed class BaoCaoChiTietCongNoPhieuFilter
{
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public string LoaiDoiTac { get; set; } = "TAT_CA"; // TAT_CA, KHACH_HANG, NHA_CUNG_CAP
    public string LoaiCongNo { get; set; } = "TAT_CA"; // TAT_CA, PHAI_THU, PHAI_TRA
    public string TrangThaiThanhToan { get; set; } = "TAT_CA"; // TAT_CA, CON_NO, DA_THANH_TOAN
    public string? TuKhoa { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>
/// Tổng kết thống kê KPI của báo cáo chi tiết công nợ theo phiếu.
/// </summary>
public sealed class BaoCaoChiTietCongNoPhieuThongKe
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public int TongSoChungTu { get; set; }
    public decimal TongPhatSinh { get; set; }
    public decimal TongDaThanhToan { get; set; }
    public decimal TongConNo { get; set; }

    public string TongSoChungTuHienThi => TongSoChungTu.ToString("N0", ViCulture);
    public string TongPhatSinhHienThi => TongPhatSinh.ToString("N0", ViCulture) + " đ";
    public string TongDaThanhToanHienThi => TongDaThanhToan.ToString("N0", ViCulture) + " đ";
    public string TongConNoHienThi => TongConNo.ToString("N0", ViCulture) + " đ";
}
