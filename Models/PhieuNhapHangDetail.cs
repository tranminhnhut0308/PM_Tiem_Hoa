using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Dữ liệu chi tiết đầy đủ của một phiếu nhập hàng, dùng cho cửa sổ "Xem chi tiết".
/// Header đọc từ bảng phieu_nhap_hang, các dòng hàng đọc từ
/// chi_tiet_phieu_nhap_hang (nối san_pham để lấy đơn vị tính).
/// Chỉ đọc, không sửa/xóa.
/// </summary>
public sealed class PhieuNhapHangDetail
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Khoá chính phieu_nhap_hang.id.</summary>
    public uint PhieuNhapId { get; set; }

    public string MaPhieuNhap { get; set; } = "";

    public DateTime ThoiGianNhap { get; set; }

    public uint? NhaCungCapId { get; set; }

    public string? MaNhaCungCap { get; set; }

    public string? TenNhaCungCap { get; set; }

    public string? TenNhanVien { get; set; }

    /// <summary>Giảm giá cấp phiếu (cột giam_gia). Hiện nghiệp vụ luôn lưu 0.</summary>
    public decimal GiamGia { get; set; }

    /// <summary>Chi phí khác cộng thêm vào tiền nhập (cột chi_phi_khac).</summary>
    public decimal ChiPhiKhac { get; set; }

    public decimal DaThanhToan { get; set; }

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
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Chuyển khoản (Quỹ ngân hàng)",
        "THE" or "THE_TIN_DUNG" => "Thẻ (Quỹ thẻ)",
        "VI_DIEN_TU" or "VI" => "Ví điện tử",
        _ => "Tiền mặt (Quỹ tiền mặt)"
    };

    public string NguonQuyChiTraHienThi => PhuongThucThanhToan switch
    {
        "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Tài khoản ngân hàng",
        _ => "Quỹ tiền mặt"
    };

    public List<PhieuNhapHangDetailItem> Items { get; set; } = [];

    // ============================================================
    // TỔNG TIỀN: tính lại từ chính các dòng chi tiết đã đọc,
    // đúng quy tắc nghiệp vụ hiện tại (tổng thành tiền từng dòng).
    // ============================================================

    public decimal TongTienHang => Items.Sum(item => item.ThanhTien);

    public decimal TongTienNhap => TongTienHang + ChiPhiKhac;

    public decimal ConNo => Math.Max(0m, TongTienNhap - DaThanhToan);

    public bool IsLuuTam => string.Equals(TrangThai, "LUU_TAM", StringComparison.OrdinalIgnoreCase);

    public string TrangThaiHienThi => IsLuuTam ? "Lưu tạm" : "Đã nhập hàng";

    public string TieuDe => $"Chi tiết phiếu nhập {MaPhieuNhap}";

    public string ThoiGianNhapText =>
        ThoiGianNhap.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string MaNhaCungCapHienThi =>
        string.IsNullOrWhiteSpace(MaNhaCungCap) ? "—" : MaNhaCungCap!;

    public string TenNhaCungCapHienThi =>
        string.IsNullOrWhiteSpace(TenNhaCungCap) ? "Không có nhà cung cấp" : TenNhaCungCap!;

    public string TenNhanVienHienThi =>
        string.IsNullOrWhiteSpace(TenNhanVien) ? "—" : TenNhanVien!;

    public bool CoSoHoaDon => !string.IsNullOrWhiteSpace(SoHoaDonDauVao);

    public bool CoMaDatHang => !string.IsNullOrWhiteSpace(MaDatHangNhap);

    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    public bool CoHangHoa => Items.Count > 0;

    public bool HasGiamGia => GiamGia != 0m;

    public bool HasChiPhiKhac => ChiPhiKhac != 0m;

    public bool CoThanhToan => DaThanhToan > 0m;

    /// <summary>Phiếu đã hoàn thành và còn nợ thì mới được ghi nhận thanh toán.</summary>
    public bool CoTheThanhToan =>
        TrangThaiThanhToanPhieuNhap.LaPhieuThanhToanDuoc(TrangThai) && ConNo > 0m;

    public bool CoCongNo => ConNo > 0m;

    /// <summary>Mã trạng thái thanh toán (xem <see cref="TrangThaiThanhToanPhieuNhap"/>).</summary>
    public string TrangThaiThanhToan => TrangThaiThanhToanPhieuNhap.Tinh(DaThanhToan, ConNo);

    public string TrangThaiThanhToanHienThi => TrangThaiThanhToanPhieuNhap.HienThi(TrangThaiThanhToan);

    public Avalonia.Media.IBrush TrangThaiThanhToanBadgeBrush => TrangThaiThanhToan switch
    {
        TrangThaiThanhToanPhieuNhap.DaThanhToan =>
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E8F6F1")),
        TrangThaiThanhToanPhieuNhap.ThanhToanMotPhan =>
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFF7E8")),
        _ => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FDECEC"))
    };

    public Avalonia.Media.IBrush TrangThaiThanhToanBadgeForeground => TrangThaiThanhToan switch
    {
        TrangThaiThanhToanPhieuNhap.DaThanhToan =>
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#087F6E")),
        TrangThaiThanhToanPhieuNhap.ThanhToanMotPhan =>
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B06A00")),
        _ => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#C0392B"))
    };

    public string GiamGiaText => GiamGia.ToString("N0", Vi) + " đ";

    public string ChiPhiKhacText => ChiPhiKhac.ToString("N0", Vi) + " đ";

    public string TongTienHangText => TongTienHang.ToString("N0", Vi) + " đ";

    public string TongTienNhapText => TongTienNhap.ToString("N0", Vi) + " đ";

    public string DaThanhToanText => DaThanhToan.ToString("N0", Vi) + " đ";

    public string ConNoText => ConNo.ToString("N0", Vi) + " đ";
}

/// <summary>Một dòng hàng hóa trong phiếu nhập (chi_tiet_phieu_nhap_hang).</summary>
public sealed class PhieuNhapHangDetailItem
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public int Index { get; set; }

    public string MaSanPham { get; set; } = "";

    public string TenSanPham { get; set; } = "";

    public string? DonViTinh { get; set; }

    public decimal SoLuong { get; set; }

    public decimal DonGiaNhap { get; set; }

    public decimal GiamGia { get; set; }

    public decimal ThanhTien { get; set; }

    public string? GhiChu { get; set; }

    public bool CoGiamGia => GiamGia != 0m;

    public string DonViTinhHienThi =>
        string.IsNullOrWhiteSpace(DonViTinh) ? "—" : DonViTinh!;

    public string SoLuongText => SoLuong.ToString("0.###", Vi);

    public string DonGiaNhapText => DonGiaNhap.ToString("N0", Vi);

    public string GiamGiaText => GiamGia.ToString("N0", Vi);

    public string ThanhTienText => ThanhTien.ToString("N0", Vi);
}
