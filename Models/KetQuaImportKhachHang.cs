namespace ShopHoa.Models;

/// <summary>
/// Kết quả nhập danh sách khách hàng từ file, dùng để ghi log và báo người dùng
/// chính xác những gì đã xảy ra: đọc được bao nhiêu dòng, thêm mới bao nhiêu,
/// cập nhật bao nhiêu, bỏ qua bao nhiêu và lỗi bao nhiêu.
/// </summary>
public sealed class KetQuaImportKhachHang
{
    public string TenFile { get; init; } = "";

    /// <summary>Số dòng dữ liệu trong file (không tính dòng tiêu đề).</summary>
    public int SoDongExcel { get; init; }

    /// <summary>Số khách hàng hợp lệ sẽ xử lý.</summary>
    public int SoKhachHangHopLe { get; init; }

    /// <summary>Số khách hàng được thêm mới vào database.</summary>
    public int SoThemMoi { get; set; }

    /// <summary>Số khách hàng đã tồn tại và được cập nhật theo file.</summary>
    public int SoCapNhat { get; set; }

    /// <summary>Số khách hàng đã tồn tại và được giữ nguyên (người dùng chọn giữ dữ liệu cũ).</summary>
    public int SoBoQua { get; set; }

    /// <summary>Số dòng không thể xử lý (thiếu mã/tên hoặc lỗi database).</summary>
    public int SoLoi { get; set; }

    /// <summary>Số dòng có tên nhóm khách hàng không khớp với nhóm nào đang có trong phần mềm.</summary>
    public int SoNhomKhachHangKhongKhop { get; set; }

    /// <summary>Số dòng có tên người tạo không khớp với tài khoản nào đang có trong phần mềm.</summary>
    public int SoNguoiTaoKhongKhop { get; set; }

    /// <summary>Thời gian xử lý.</summary>
    public TimeSpan ThoiGian { get; set; }

    /// <summary>Các lỗi ghi nhận, tối đa 20 dòng đầu.</summary>
    public IReadOnlyList<string> DanhSachLoi { get; set; } = [];

    /// <summary>Tổng số khách hàng đã thay đổi trong database.</summary>
    public int TongSoDaThayDoi => SoThemMoi + SoCapNhat;

    /// <summary>Dòng log tóm tắt kết quả nhập để hiển thị cho người dùng.</summary>
    public string ThongBaoTatCa =>
        $"Đọc {SoDongExcel:N0} dòng Excel, hợp lệ {SoKhachHangHopLe:N0}, "
        + $"thêm mới {SoThemMoi:N0}, cập nhật {SoCapNhat:N0}, "
        + $"giữ nguyên {SoBoQua:N0}, lỗi {SoLoi:N0} ({ThoiGian.TotalSeconds:F1}s)";
}
