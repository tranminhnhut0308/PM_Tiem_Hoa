namespace ShopHoa.Models;

/// <summary>
/// Kết quả nhập danh sách nhà cung cấp từ file, dùng để báo người dùng chính
/// xác những gì đã xảy ra: đọc được bao nhiêu dòng, thêm mới bao nhiêu,
/// bỏ qua bao nhiêu và lỗi bao nhiêu.
/// </summary>
public sealed class KetQuaImportNhaCungCap
{
    public string TenFile { get; init; } = "";

    /// <summary>Số dòng đọc được từ file (không tính dòng tiêu đề).</summary>
    public int SoDongExcel { get; init; }

    /// <summary>Số nhà cung cấp hợp lệ sẽ xử lý (có tên, thiếu mã sẽ được sinh tự động).</summary>
    public int SoNhaCungCapHopLe { get; set; }

    /// <summary>Số nhà cung cấp bị bỏ qua vì mã đã tồn tại trong database.</summary>
    public int SoTrungMa { get; set; }

    /// <summary>Số nhà cung cấp bị bỏ qua vì mã bị trùng ngay trong file.</summary>
    public int SoTrungMaTrongFile { get; set; }

    /// <summary>Số nhà cung cấp đã được thêm mới vào database.</summary>
    public int SoThemMoi { get; set; }

    /// <summary>Số nhà cung cấp đã có mã trùng nhưng được cập nhật lại dữ liệu từ file.</summary>
    public int SoCapNhat { get; set; }

    /// <summary>Số dòng không thể nhập (thiếu tên hoặc lỗi database).</summary>
    public int SoLoi { get; set; }

    /// <summary>
    /// Số dòng có tên + số điện thoại trùng với nhà cung cấp đã có trong database.
    /// Bảng nha_cung_cap không có ràng buộc UNIQUE cho hai cột này nên các dòng này
    /// vẫn được nhập; con số chỉ để người dùng tự quyết định có cần sửa lại không.
    /// </summary>
    public int SoTrungTenVaDienThoai { get; set; }

    /// <summary>Các lỗi ghi nhận, tối đa 20 dòng đầu.</summary>
    public List<string> DanhSachLoi { get; } = [];

    /// <summary>Tổng số bản ghi đã thay đổi trong database.</summary>
    public int TongSoDaThayDoi => SoThemMoi + SoCapNhat;

    /// <summary>Số dòng bị bỏ qua vì mã nhà cung cấp đã có (trong database hoặc trong file).</summary>
    public int SoBoQuaTrungMa => SoTrungMa + SoTrungMaTrongFile;

    /// <summary>Dòng log tóm tắt kết quả nhập để hiển thị cho người dùng.</summary>
    public string ThongBaoTatCa =>
        $"Tổng dòng: {SoDongExcel:N0} | Thành công: {SoThemMoi:N0} | "
        + $"Cập nhật: {SoCapNhat:N0} | Bỏ qua trùng mã: {SoBoQuaTrungMa:N0} | Lỗi: {SoLoi:N0}";
}
