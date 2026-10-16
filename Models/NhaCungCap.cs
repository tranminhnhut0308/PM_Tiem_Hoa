namespace ShopHoa.Models;

public sealed class NhaCungCap
{
    public uint Id { get; set; }

    public string MaNhaCungCap { get; set; } = "";

    public string TenNhaCungCap { get; set; } = "";

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    public string? PhuongXa { get; set; }

    public string? QuanHuyen { get; set; }

    public string? TinhThanh { get; set; }

    public string? MaSoThue { get; set; }

    public string? NguoiLienHe { get; set; }

    public string? GhiChu { get; set; }

    public bool TrangThai { get; set; } = true;

    /// <summary>
    /// Công nợ hiện tại hiển thị cho người dùng. Ưu tiên số tiền đọc từ file
    /// danh sách, không có thì mới tính từ sổ cái công nợ. Chỉ dùng khi hiển thị,
    /// không ghi xuống database.
    /// </summary>
    public decimal ConNo { get; set; }

    /// <summary>
    /// Tổng tiền đã mua của nhà cung cấp. Ưu tiên số tiền đọc từ file danh sách,
    /// không có thì mới tính từ các phiếu nhập hàng.
    /// </summary>
    public decimal TongMua { get; set; }

    /// <summary>
    /// Tổng tiền đã mua trừ các phiếu trả hàng. Ưu tiên số tiền đọc từ file danh sách.
    /// </summary>
    public decimal TongMuaTruTraHang { get; set; }

    public DateTime? NgayTao { get; set; }

    public string TrangThaiHienThi => TrangThai ? "Đang hoạt động" : "Ngừng hoạt động";

    // ---------------------------------------------------------------------
    // Các cột lấy từ file danh sách nhà cung cấp.
    // Ba cột tiền bên dưới là giá trị gốc đọc từ file, khác với
    // ConNo/TongMua/TongMuaTruTraHang ở trên là số tiền hiển thị
    // (đã ưu tiên dữ liệu từ file, không có thì mới tính từ phiếu nhập/sổ cái).
    // ---------------------------------------------------------------------

    /// <summary>Khu vực trong file danh sách.</summary>
    public string? KhuVuc { get; set; }

    /// <summary>Số CMND/CCCD trong file danh sách.</summary>
    public string? SoCccd { get; set; }

    /// <summary>Nhóm nhà cung cấp ghi dạng chữ trong file danh sách.</summary>
    public string? NhomNhaCungCapTen { get; set; }

    /// <summary>Tên công ty trong file danh sách.</summary>
    public string? TenCongTy { get; set; }

    /// <summary>Tên người tạo ghi trong file danh sách (giữ nguyên chữ).</summary>
    public string? NguoiTaoTen { get; set; }

    /// <summary>Ngày nhập bản ghi từ file danh sách, dùng để truy vết.</summary>
    public DateTime? NgayNhapExcel { get; set; }

    /// <summary>
    /// true khi ba cột tiền bên dưới lấy từ file danh sách.
    /// false thì ba cột tiền đó bị bỏ qua và số tiền hiển thị được tính từ
    /// phiếu nhập hàng và sổ cái công nợ của phần mềm.
    /// </summary>
    public bool CoDuLieuKiotViet { get; set; }

    /// <summary>Nợ cần trả hiện tại theo file danh sách (null nếu file không có).</summary>
    public decimal? NoCanTraHienTaiExcel { get; set; }

    /// <summary>Tổng mua theo file danh sách (null nếu file không có).</summary>
    public decimal? TongMuaExcel { get; set; }

    /// <summary>Tổng mua trừ trả hàng theo file danh sách (null nếu file không có).</summary>
    public decimal? TongMuaTruTraHangExcel { get; set; }
}
