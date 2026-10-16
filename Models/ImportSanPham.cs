namespace ShopHoa.Models;

/// <summary>
/// Cách người dùng chọn xử lý khi phát hiện sản phẩm trùng với dữ liệu đang có.
/// </summary>
public enum XuLySanPhamTrung
{
    /// <summary>Giữ nguyên sản phẩm đang có trong database, không UPDATE gì cả.</summary>
    GiuSanPhamCu,

    /// <summary>UPDATE sản phẩm đang có bằng dữ liệu trong file Excel (không tạo bản ghi mới).</summary>
    ThayTheBangExcel,

    /// <summary>Hủy toàn bộ lượt nhập.</summary>
    Huy
}

/// <summary>Thao tác sẽ thực hiện với một sản phẩm khi nhập từ file.</summary>
public enum HanDong
{
    /// <summary>Sản phẩm chưa có trong database -> INSERT bản ghi mới.</summary>
    ThemMoi,

    /// <summary>Sản phẩm đã có, người dùng chọn thay thế -> UPDATE bản ghi cũ.</summary>
    ThayThe,

    /// <summary>Sản phẩm đã có, người dùng chọn giữ -> không ghi gì.</summary>
    GiuCu,

    /// <summary>Dòng lỗi -> không ghi gì, ghi nhận vào báo cáo.</summary>
    Loi
}

/// <summary>
/// Một dòng của file Excel đã đọc được kèm quyết định xử lý.
/// </summary>
/// <param name="SanPham">Dữ liệu sản phẩm lấy từ file.</param>
/// <param name="SoDongExcel">Số dòng trong file Excel, dùng để báo lỗi cho người dùng.</param>
/// <param name="IdTrongDatabase">id của sản phẩm đã tồn tại (chỉ có khi trùng).</param>
/// <param name="SanPhamCu">Bản ghi đang có trong database (chỉ có khi trùng).</param>
/// <param name="HanDong">Thao tác sẽ thực hiện.</param>
/// <param name="LyDoLoi">Lý do bỏ qua nếu là dòng lỗi.</param>
public sealed record HangNhapSanPham(
    SanPham SanPham,
    int SoDongExcel,
    uint? IdTrongDatabase = null,
    SanPham? SanPhamCu = null,
    HanDong hanDong = HanDong.ThemMoi,
    string? LyDoLoi = null);

/// <summary>
/// Một sản phẩm trùng, kèm so sánh dữ liệu cũ trong database với dữ liệu trong file Excel.
/// Dùng để hiển thị cho người dùng trước khi quyết định giữ hay thay thế.
/// </summary>
public sealed class SanPhamTrung
{
    public required SanPham SanPhamCu { get; init; }
    public required SanPham SanPhamMoi { get; init; }

    /// <summary>Số dòng trong file Excel (để người dùng đối chiếu).</summary>
    public int SoDongExcel { get; init; }

    /// <summary>Mã hàng dùng để nhận diện sản phẩm (khóa chính).</summary>
    public string MaSanPham => SanPhamMoi.MaSanPham;

    /// <summary>Mã vạch, rỗng nếu sản phẩm không có mã vạch.</summary>
    public string? MaVach => SanPhamMoi.MaVach;

    /// <summary>Lý do được coi là trùng: "mã hàng" hoặc "mã vạch".</summary>
    public required string LyDoTrung { get; init; }

    public string TenCu => SanPhamCu.TenSanPham;
    public string TenMoi => SanPhamMoi.TenSanPham;

    /// <summary>Giá bán hiển thị dạng "cũ → mới" để so sánh ngay trên bảng.</summary>
    public string GiaBan => $"{GiaBanCu}  →  {GiaBanMoi}";

    /// <summary>Giá vốn hiển thị dạng "cũ → mới".</summary>
    public string GiaVon => $"{GiaVonCu}  →  {GiaVonMoi}";

    /// <summary>Tồn kho hiển thị dạng "cũ → mới".</summary>
    public string TonKho => $"{TonKhoCu}  →  {TonKhoMoi}";

    private string GiaBanCu => SanPhamCu.GiaBan.ToString("N0");
    private string GiaBanMoi => SanPhamMoi.GiaBan.ToString("N0");
    private string GiaVonCu => SanPhamCu.GiaVon.ToString("N0");
    private string GiaVonMoi => SanPhamMoi.GiaVon.ToString("N0");
    private string TonKhoCu => SanPhamCu.TonKhoExcel.ToString("N0");
    private string TonKhoMoi => SanPhamMoi.TonKhoExcel.ToString("N0");
    public string NgayTaoCu => SanPhamCu.NgayTao?.ToString("dd/MM/yyyy HH:mm:ss") ?? "(trống)";
    public string NgayTaoMoi => SanPhamMoi.NgayTao?.ToString("dd/MM/yyyy HH:mm:ss") ?? "(trống)";

    /// <summary>
    /// Thời gian tạo có khác nhau không. Khi chọn "Thay thế", nếu file có ngày tạo
    /// thì lấy ngày từ file; nếu file trống thì giữ ngày cũ trong database.
    /// </summary>
    public bool NgayTaoKhac => SanPhamCu.NgayTao != SanPhamMoi.NgayTao;
}

/// <summary>Một dòng Excel bị lỗi, để báo lại cho người dùng biết chính xác dòng nào hỏng.</summary>
public sealed record LoiDongNhap(int SoDongExcel, string? MaSanPham, string? TenSanPham, string LyDo);

/// <summary>
/// Kết quả một lượt nhập hàng hóa từ file: tổng số dòng Excel, số thêm mới,
/// số thay thế, số giữ cũ, số trùng và số lỗi kèm chi tiết từng dòng lỗi.
/// </summary>
public sealed class KetQuaImportSanPham
{
    public string TenFile { get; set; } = "";

    /// <summary>Tổng số dòng dữ liệu đọc được từ file Excel.</summary>
    public int TongSoDongExcel { get; set; }

    public int SoThemMoi { get; set; }
    public int SoThayThe { get; set; }
    public int SoGiuCu { get; set; }
    public int SoLoi { get; set; }

    /// <summary>Số sản phẩm bị phát hiện trùng (giữ cũ + thay thế).</summary>
    public int SoTrung => SoGiuCu + SoThayThe;

    /// <summary>Người dùng đã chọn Hủy nên không ghi gì.</summary>
    public bool DaHuy { get; set; }

    /// <summary>Đã rollback toàn bộ vì lỗi nghiêm trọng; database không bị dở dang.</summary>
    public bool DaRollback { get; set; }

    /// <summary>Cảnh báo không chặn (ví dụ tên quá dài bị cắt, cột ngày tạo chưa cho phép NULL).</summary>
    public List<string> CanhBao { get; } = [];

    public List<LoiDongNhap> LoiDong { get; } = [];

    public List<SanPhamTrung> DanhSachTrung { get; } = [];

    public int SoGhiThanhCong => SoThemMoi + SoThayThe;

    /// <summary>Tổng số dòng có trong file Excel kể cả dòng lỗi.</summary>
    public int TongSoDongDaXuLy => SoThemMoi + SoThayThe + SoGiuCu + SoLoi;

    public string ThongBaoTatCa
    {
        get
        {
            if (DaHuy)
                return "Đã hủy nhập file. Không có dữ liệu nào được thay đổi.";

            var dong = new List<string> { $"Tổng số dòng Excel: {TongSoDongExcel:N0}" };

            if (DaRollback)
                dong.Add("ĐÃ ROLLBACK toàn bộ: database không bị để lại trạng thái nhập dở dang.");

            dong.Add($"Thêm mới: {SoThemMoi:N0}");
            dong.Add($"Thay thế sản phẩm trùng: {SoThayThe:N0}");
            dong.Add($"Giữ nguyên sản phẩm cũ: {SoGiuCu:N0}");
            dong.Add($"Phát hiện trùng: {SoTrung:N0}");
            dong.Add($"Dòng lỗi: {SoLoi:N0}");

            return string.Join("\n", dong);
        }
    }
}