using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Phiếu nhập hàng (đầu vào từ nhà cung cấp).
/// Ánh xạ trực tiếp với bảng phieu_nhap_hang.
/// </summary>
public sealed partial class PhieuNhapHang : ObservableObject
{
    public uint Id { get; set; }

    public string MaPhieuNhap { get; set; } = "";

    public uint ChiNhanhId { get; set; }

    public uint? NhaCungCapId { get; set; }

    public string? TenNhaCungCap { get; set; }

    /// <summary>Mã nhà cung cấp, chỉ dùng cho báo cáo (join từ bảng nha_cung_cap).</summary>
    public string? MaNhaCungCap { get; set; }

    public uint? NguoiNhapId { get; set; }

    public string? TenNhanVien { get; set; }

    public DateTime ThoiGianNhap { get; set; } = DateTime.Now;

    public decimal TongTienHang { get; set; }

    public decimal ChiPhiKhac { get; set; }

    public decimal TongTienNhap { get; set; }

    public decimal DaThanhToan { get; set; }

    public decimal ConNo { get; set; }

    /// <summary>
    /// DA_NHAP_HANG = đã cập nhật tồn kho,
    /// LUU_TAM = phiếu nháp, chưa động vào tồn kho.
    /// </summary>
    public string TrangThai { get; set; } = "DA_NHAP_HANG";

    public string? GhiChu { get; set; }

    public string? SoHoaDonDauVao { get; set; }

    public string? MaDatHangNhap { get; set; }

    /// <summary>
    /// Phương thức thanh toán khi nhập hàng:
    /// TIEN_MAT = Tiền mặt (Quỹ tiền mặt),
    /// CHUYEN_KHOAN = Chuyển khoản (Tài khoản ngân hàng).
    /// </summary>
    public string PhuongThucThanhToan { get; set; } = "TIEN_MAT";

    public string PhuongThucThanhToanHienThi => PhuongThucThanhToan switch
    {
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Chuyển khoản",
        "THE" or "THE_TIN_DUNG" => "Thẻ",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => "Tiền mặt"
    };

    public string NguonQuyHienThi => PhuongThucThanhToan switch
    {
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Quỹ ngân hàng",
        "THE" or "THE_TIN_DUNG" => "Quỹ thẻ",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => "Quỹ tiền mặt"
    };

    public string TrangThaiHienThi => TrangThai == "LUU_TAM"
        ? "Lưu tạm"
        : "Đã nhập hàng";

    // ============================================================
    // CÔNG NỢ / THANH TOÁN: trạng thái thanh toán được suy ra từ
    // cặp (da_thanh_toan, con_no), không lưu cột riêng.
    // ============================================================

    /// <summary>Phiếu đã hoàn thành và còn nợ thì mới được ghi nhận thanh toán.</summary>
    public bool CoTheThanhToan =>
        TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(TrangThai) && ConNo > 0m;

    public bool CoCongNo => ConNo > 0m;

    /// <summary>Mã trạng thái thanh toán (xem <see cref="TrangThaiThanhToanPhieuNhap"/>).</summary>
    public string TrangThaiThanhToan => TrangThaiThanhToanPhieuNhap.Tinh(DaThanhToan, ConNo);

    public string TrangThaiThanhToanHienThi =>
        TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(TrangThai)
            ? TrangThaiThanhToanPhieuNhap.HienThi(TrangThaiThanhToan)
            : "—";

    // ============================================================
    // THÔNG TIN CHỈ DÙNG CHO BÁO CÁO
    // ============================================================

    /// <summary>Định dạng dd/MM/yyyy HH:mm cho cột "Thời gian" trong báo cáo.</summary>
    public string ThoiGianNhapText => ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Số công nợ còn lại, định dạng theo kiểu Việt Nam cho cột "Còn nợ".</summary>
    public string ConNoText => ConNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    private static readonly IBrush NenDaNhapHang = new SolidColorBrush(Color.Parse("#E8F6F1"));
    private static readonly IBrush ChuDaNhapHang = new SolidColorBrush(Color.Parse("#087F6E"));
    private static readonly IBrush NenLuuTam = new SolidColorBrush(Color.Parse("#FFF7E8"));
    private static readonly IBrush ChuLuuTam = new SolidColorBrush(Color.Parse("#B06A00"));

    /// <summary>Màu nền badge trạng thái: xanh nhạt khi đã nhập hàng, vàng nhạt khi lưu tạm.</summary>
    public IBrush TrangThaiBadgeBrush => TrangThai == "LUU_TAM"
        ? NenLuuTam
        : NenDaNhapHang;

    /// <summary>Màu chữ badge trạng thái.</summary>
    public IBrush TrangThaiBadgeForeground => TrangThai == "LUU_TAM"
        ? ChuLuuTam
        : ChuDaNhapHang;

    private static readonly IBrush NenChuaThanhToan = new SolidColorBrush(Color.Parse("#FDECEC"));
    private static readonly IBrush ChuChuaThanhToan = new SolidColorBrush(Color.Parse("#C0392B"));
    private static readonly IBrush NenThanhToanMotPhan = new SolidColorBrush(Color.Parse("#FFF7E8"));
    private static readonly IBrush ChuThanhToanMotPhan = new SolidColorBrush(Color.Parse("#B06A00"));
    private static readonly IBrush NenDaThanhToan = new SolidColorBrush(Color.Parse("#E8F6F1"));
    private static readonly IBrush ChuDaThanhToan = new SolidColorBrush(Color.Parse("#087F6E"));
    private static readonly IBrush NenTrungTinh = new SolidColorBrush(Color.Parse("#F1F4F3"));
    private static readonly IBrush ChuTrungTinh = new SolidColorBrush(Color.Parse("#7A8C87"));

    /// <summary>Màu nền badge trạng thái thanh toán.</summary>
    public IBrush TrangThaiThanhToanBadgeBrush
    {
        get
        {
            if (!TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(TrangThai))
                return NenTrungTinh;

            return TrangThaiThanhToan switch
            {
                TrangThaiThanhToanPhieuNhap.DaThanhToan => NenDaThanhToan,
                TrangThaiThanhToanPhieuNhap.ThanhToanMotPhan => NenThanhToanMotPhan,
                _ => NenChuaThanhToan
            };
        }
    }

    /// <summary>Màu chữ badge trạng thái thanh toán.</summary>
    public IBrush TrangThaiThanhToanBadgeForeground
    {
        get
        {
            if (!TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(TrangThai))
                return ChuTrungTinh;

            return TrangThaiThanhToan switch
            {
                TrangThaiThanhToanPhieuNhap.DaThanhToan => ChuDaThanhToan,
                TrangThaiThanhToanPhieuNhap.ThanhToanMotPhan => ChuThanhToanMotPhan,
                _ => ChuChuaThanhToan
            };
        }
    }
}
