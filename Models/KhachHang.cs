namespace ShopHoa.Models;

public sealed class KhachHang
{
    public uint Id { get; set; }
    public string MaKhachHang { get; set; } = "";
    public string TenKhachHang { get; set; } = "";
    public string LoaiKhachHang { get; set; } = "CA_NHAN";
    public string? SoDienThoai { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
    public string? PhuongXa { get; set; }
    public string? QuanHuyen { get; set; }
    public string? TinhThanh { get; set; }
    /// <summary>Tên nhóm khách hàng, lấy từ bảng nhom_khach_hang khi đọc danh sách.</summary>
    public string? NhomKhachHang { get; set; }

    /// <summary>Khóa nhóm khách hàng, dùng khi thêm/sửa để lưu đúng nhóm đã chọn.</summary>
    public uint? NhomKhachHangId { get; set; }

    /// <summary>
    /// Nợ hiện tại của khách hàng, tính từ sổ cái công nợ
    /// (SUM(ghi_no - ghi_co)), nếu chưa có sổ cái thì lấy SUM(con_no) của hóa đơn.
    /// Chỉ dùng khi hiển thị danh sách, không ghi xuống database.
    /// </summary>
    public decimal NoHienTai { get; set; }

    /// <summary>Tổng tiền bán của khách hàng (SUM(khach_can_tra) hóa đơn đã hoàn thành).</summary>
    public decimal TongBan { get; set; }

    /// <summary>Tổng tiền bán trừ các phiếu trả hàng đã hoàn thành.</summary>
    public decimal TongBanTruTraHang { get; set; }

    public string? MaSoThue { get; set; }
    public string? TenNguoiMuaHoaDon { get; set; }
    public string? TenCongTy { get; set; }
    public string? DiaChiXuatHoaDon { get; set; }
    public string? GhiChu { get; set; }
    public bool TrangThai { get; set; } = true;
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    // ---------------------------------------------------------------------
    // Các cột bổ sung lấy từ file danh sách khách hàng của KiotViet.
    // Ba cột tiền bên dưới là giá trị gốc đọc từ file, khác với
    // NoHienTai/TongBan/TongBanTruTraHang ở trên là số tiền hiển thị
    // (đã ưu tiên dữ liệu KiotViet, không có thì mới tính từ hóa đơn nội bộ).
    // ---------------------------------------------------------------------

    /// <summary>Khu vực giao hàng trong file KiotViet.</summary>
    public string? KhuVucGiaoHang { get; set; }

    /// <summary>Chi nhánh tạo khách hàng trong file KiotViet.</summary>
    public string? ChiNhanhTao { get; set; }

    /// <summary>Tên người tạo ghi trong file KiotViet (giữ nguyên chữ).</summary>
    public string? NguoiTaoTen { get; set; }

    /// <summary>Ngày giao dịch cuối cùng trong file KiotViet.</summary>
    public DateTime? NgayGiaoDichCuoi { get; set; }

    /// <summary>Ngày nhập bản ghi từ file KiotViet, dùng để truy vết.</summary>
    public DateTime? NgayNhapExcel { get; set; }

    /// <summary>
    /// true khi ba cột tiền bên dưới lấy từ file KiotViet.
    /// false thì ba cột tiền đó bị bỏ qua và số tiền hiển thị được tính từ
    /// hóa đơn, công nợ và phiếu trả hàng của phần mềm.
    /// </summary>
    public bool CoDuLieuKiotViet { get; set; }

    /// <summary>Nợ cần thu hiện tại theo file KiotViet (null nếu file không có).</summary>
    public decimal? NoCanThuHienTaiExcel { get; set; }

    /// <summary>Tổng bán theo file KiotViet (null nếu file không có).</summary>
    public decimal? TongBanExcel { get; set; }

    /// <summary>Tổng bán trừ trả hàng theo file KiotViet (null nếu file không có).</summary>
    public decimal? TongBanTruTraHangExcel { get; set; }

    /// <summary>
    /// true = Địa chỉ cũ (cần nhập thêm Quận/Huyện).
    /// false = Địa chỉ mới (chỉ Phường/Xã + Tỉnh/Thành).
    /// Chỉ dùng trên form, không lưu database.
    /// </summary>
    public bool LaDiaChiCu { get; set; }
}
