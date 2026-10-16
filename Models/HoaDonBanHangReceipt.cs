namespace ShopHoa.Models;

public sealed class HoaDonBanHangReceipt
{
    /// <summary>Khoá chính của hóa đơn trong database (0 nghĩa là chưa lưu).</summary>
    public uint HoaDonId { get; set; }

    public string MaHoaDon { get; set; } = "";
    public DateTime ThoiGianBan { get; set; }
    public string TenKhachHang { get; set; } = "Khách lẻ";
    public string TenNhanVien { get; set; } = "";
    public List<HoaDonBanHangReceiptItem> Items { get; set; } = [];
    public decimal TongTienHang { get; set; }
    public decimal GiamGia { get; set; }
    public decimal KhachCanTra { get; set; }
    public decimal KhachDaThanhToan { get; set; }
    public decimal TienKhachDua { get; set; }

    public decimal TienThoi { get; set; }
    public decimal ConNo { get; set; }
    public string TrangThaiThanhToan { get; set; } = "";

    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }
    public bool InKemThongTin { get; set; }

    public bool HienThiThongTinLienHe => InKemThongTin && (!string.IsNullOrWhiteSpace(SoDienThoai) || !string.IsNullOrWhiteSpace(DiaChi));
    public string SoDienThoaiHienThi => string.IsNullOrWhiteSpace(SoDienThoai) ? "Chưa có" : SoDienThoai;
    public string DiaChiHienThi => string.IsNullOrWhiteSpace(DiaChi) ? "Chưa có" : DiaChi;

    /// <summary>Ghi chú riêng của hóa đơn (yêu cầu riêng của khách...).</summary>
    public string? GhiChu { get; set; }

    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    /// <summary>
    /// Mã phiếu Thu trong sổ quỹ đã sinh kèm hóa đơn này (ví dụ "PT0004").
    /// Rỗng khi khách chưa trả đồng nào nên không có dòng tiền vào quỹ.
    /// </summary>
    public string MaPhieuThu { get; set; } = "";

    /// <summary>Tiền thối đã lưu xuống database (cột thu_khac).</summary>
    public decimal ThuKhac { get; set; }

    /// <summary>
    /// Các cảnh báo phát sinh khi lưu đơn (bán vượt tồn kho, sản phẩm chưa có
    /// số tồn...). Hóa đơn vẫn được lưu nhưng người dùng phải được báo rõ.
    /// </summary>
    public List<string> CanhBao { get; set; } = [];

    public bool CoCanhBao => CanhBao.Count > 0;

    /// <summary>
    /// Thông tin tiệm in lên đầu hóa đơn, lấy từ
    /// <see cref="Services.StoreInfoService"/>. Luôn có giá trị (không null) để
    /// hóa đơn không bao giờ trắng phần đầu, kể cả khi đọc lỗi.
    /// </summary>
    public ThongTinTiem ThongTinTiem { get; set; } = new();
}

public sealed class HoaDonBanHangReceiptItem
{
    public string TenSanPham { get; set; } = "";
    public decimal SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}
