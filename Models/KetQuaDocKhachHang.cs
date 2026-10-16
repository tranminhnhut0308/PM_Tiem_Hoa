namespace ShopHoa.Models;

/// <summary>
/// Kết quả đọc một file danh sách khách hàng, dùng để ghi log trước khi nhập:
/// biết chính xác file có bao nhiêu dòng, bao nhiêu dòng hợp lệ và dòng nào bị lỗi.
/// </summary>
public sealed class KetQuaDocKhachHang
{
    public string TenFile { get; init; } = "";

    /// <summary>Số dòng dữ liệu tìm thấy trong file (không tính dòng tiêu đề).</summary>
    public int SoDongExcel { get; init; }

    /// <summary>Số dòng đọc được thành khách hàng hợp lệ (có tên khách hàng).</summary>
    public int SoKhachHangHopLe { get; init; }

    /// <summary>Số dòng bị bỏ qua vì thiếu mã hoặc thiếu tên khách hàng.</summary>
    public int SoDongLoi { get; init; }

    /// <summary>Chi tiết tối đa 20 dòng lỗi đầu tiên, phục vụ log.</summary>
    public IReadOnlyList<string> DanhSachLoi { get; init; } = [];

    public IReadOnlyList<KhachHang> KhachHangs { get; init; } = [];
}
