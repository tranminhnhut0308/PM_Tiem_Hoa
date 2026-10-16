namespace ShopHoa.Models;

/// <summary>
/// Cách xử lý dòng có mã phiếu đã tồn tại trong database khi nhập từ file.
/// Không có lựa chọn nào là mặc định ghi đè: người dùng phải chọn rõ ràng.
/// </summary>
public enum XuLyPhieuTrung
{
    /// <summary>Người dùng hủy cả lượt nhập.</summary>
    Huy = 0,

    /// <summary>Giữ dữ liệu cũ trong database, bỏ qua dòng trùng.</summary>
    GiuDuLieuCu = 1,

    /// <summary>UPDATE bản ghi cũ theo giá trị trong file.</summary>
    ThayThe = 2,

    /// <summary>Bỏ qua dòng trùng (giữ dữ liệu cũ nhưng tính là dòng bị bỏ qua).</summary>
    BoQua = 3
}

/// <summary>Các trường một dòng phiếu trong file nhập có thể chứa.</summary>
public enum TruongSoQuy
{
    MaPhieu,
    NgayGio,
    LoaiPhieu,
    NguoiNopNhan,
    LoaiThuChi,
    SoTien,
    TrangThai,
    NoiDung,
    GhiChu,
    NguoiTao,
    Quy
}

/// <summary>
/// Tên cột hiển thị + tên cột trong file mẫu, dùng cho cả bước xem trước,
/// bước validate và file Excel mẫu tải xuống nên không lệch nhau.
/// </summary>
public static class TenTruongSoQuy
{
    public static string Lay(TruongSoQuy truong) => truong switch
    {
        TruongSoQuy.MaPhieu => "Mã phiếu",
        TruongSoQuy.NgayGio => "Ngày giờ",
        TruongSoQuy.LoaiPhieu => "Loại phiếu",
        TruongSoQuy.NguoiNopNhan => "Người nộp/nhận",
        TruongSoQuy.LoaiThuChi => "Loại thu/chi",
        TruongSoQuy.SoTien => "Số tiền",
        TruongSoQuy.TrangThai => "Trạng thái",
        TruongSoQuy.NoiDung => "Nội dung",
        TruongSoQuy.GhiChu => "Ghi chú",
        TruongSoQuy.NguoiTao => "Người tạo",
        TruongSoQuy.Quy => "Quỹ",
        _ => truong.ToString()
    };

    /// <summary>
    /// Thứ tự cột trong file mẫu, đúng như yêu cầu nghiệp vụ: mã phiếu, ngày giờ,
    /// loại phiếu, người nộp/nhận, loại thu/chi, số tiền, trạng thái, nội dung,
    /// ghi chú, người tạo (kèm quỹ ở cuối vì có thể để trống).
    /// </summary>
    public static readonly IReadOnlyList<TruongSoQuy> ThuTuCot =
    [
        TruongSoQuy.MaPhieu,
        TruongSoQuy.NgayGio,
        TruongSoQuy.LoaiPhieu,
        TruongSoQuy.NguoiNopNhan,
        TruongSoQuy.LoaiThuChi,
        TruongSoQuy.SoTien,
        TruongSoQuy.TrangThai,
        TruongSoQuy.NoiDung,
        TruongSoQuy.GhiChu,
        TruongSoQuy.NguoiTao,
        TruongSoQuy.Quy
    ];
}

/// <summary>Kết quả validate của một dòng trong file nhập.</summary>
public enum KetQuaDongSoQuy
{
    /// <summary>Dòng hợp lệ, sẽ được ghi (mới hoặc cập nhật).</summary>
    HopLe = 0,

    /// <summary>Dòng có lỗi chặn, không ghi.</summary>
    Loi = 1,

    /// <summary>Mã phiếu đã có trong database, chờ người dùng chọn cách xử lý.</summary>
    TrungMa = 2,

    /// <summary>Trùng ngay trong chính file này, chỉ dùng dòng đầu tiên.</summary>
    TrungTrongFile = 3
}

/// <summary>Một lỗi cụ thể của một dòng, hiển thị trong preview và xuất ra Excel.</summary>
public sealed record SoQuyImportError(int SoDongExcel, string? MaPhieu, string LyDo)
{
    /// <summary>Câu thông báo đầy đủ, ví dụ: "Dòng 25: Số tiền không hợp lệ".</summary>
    public string MoTa => $"Dòng {SoDongExcel}: {LyDo}";
}

/// <summary>Một dòng phiếu đọc từ file, đã qua bước validate.</summary>
public sealed class SoQuyImportRow
{
    /// <summary>Số dòng trong file Excel (bắt đầu từ 2 vì dòng 1 là tiêu đề).</summary>
    public int SoDongExcel { get; init; }

    /// <summary>Mã phiếu trong file. Rỗng thì hệ thống tự sinh khi ghi.</summary>
    public string MaPhieu { get; init; } = "";

    /// <summary>true = phiếu thu, false = phiếu chi.</summary>
    public bool LaThu { get; init; }

    public DateTime? ThoiGianRaw { get; init; }

    /// <summary>Thời gian dùng để ghi (đã áp dụng khi file không có ngày).</summary>
    public DateTime ThoiGian => ThoiGianRaw ?? DateTime.Now;

    public string NguoiNopNhan { get; init; } = "";

    /// <summary>id của nhom_chi_phi sau khi tra cứu; null khi file không chỉ định loại.</summary>
    public uint? NhomChiPhiId { get; init; }

    public decimal SoTien { get; init; }

    /// <summary>Mã trạng thái sẽ ghi xuống DB (HOAN_THANH / HUY).</summary>
    public string TrangThai { get; init; } = "HOAN_THANH";

    public string NoiDung { get; init; } = "";

    public string GhiChu { get; init; } = "";

    /// <summary>Tên người tạo lấy từ file (khớp ho_ten trong bảng nguoi_dung).</summary>
    public string NguoiTaoRaw { get; init; } = "";

    /// <summary>id người tạo sau khi tra cứu; null khi không chỉ định hoặc không tồn tại.</summary>
    public uint? NguoiTaoId { get; init; }

    /// <summary>Mã phương thức quỹ; null thì lúc ghi sẽ lấy giá trị mặc định của DB.</summary>
    public string? PhuongThuc { get; init; }

    /// <summary>Kết quả validate của dòng này.</summary>
    public KetQuaDongSoQuy KetQua { get; init; } = KetQuaDongSoQuy.HopLe;

    /// <summary>Lý do lỗi/trùng, hiển thị ở cột Kết quả của bảng xem trước.</summary>
    public string LyDo { get; init; } = "";

    /// <summary>true khi mã phiếu đã có sẵn trong database.</summary>
    public bool MaPhieuDaTonTai { get; set; }

    /// <summary>Dòng này sẽ ghi thành công khi bấm "Tiến hành import".</summary>
    public bool SeGhi => KetQua is KetQuaDongSoQuy.HopLe or KetQuaDongSoQuy.TrungMa;

    public bool LaLoi => KetQua == KetQuaDongSoQuy.Loi;

    public string LoaiHienThi => LaThu ? "Thu" : "Chi";

    public string NgayHienThi => ThoiGianRaw?.ToString("dd/MM/yyyy HH:mm") ?? "(thiếu)";

    public string TrangThaiHienThi => TrangThai switch
    {
        "HOAN_THANH" => "Đã thanh toán",
        "HUY" => "Đã hủy",
        _ => TrangThai
    };

    /// <summary>Nhãn kết quả cùng màu, dùng cho cột "Kết quả" trong preview.</summary>
    public string KetQuaHienThi => KetQua switch
    {
        KetQuaDongSoQuy.HopLe => "Sẽ thêm mới",
        KetQuaDongSoQuy.TrungMa => MaPhieuDaTonTai ? "Trùng — chờ chọn cách xử lý" : "Sẽ thêm mới",
        KetQuaDongSoQuy.TrungTrongFile => $"Trùng trong file với dòng {SoDongExcel} — bỏ qua",
        _ => LyDo
    };

    public string MauKetQua => KetQua switch
    {
        KetQuaDongSoQuy.HopLe => "#087F6E",
        KetQuaDongSoQuy.TrungMa => "#B54708",
        KetQuaDongSoQuy.TrungTrongFile => "#B54708",
        _ => "#D92D20"
    };

    public string MauNenKetQua => KetQua switch
    {
        KetQuaDongSoQuy.HopLe => "#E7F8F0",
        KetQuaDongSoQuy.TrungMa or KetQuaDongSoQuy.TrungTrongFile => "#FEF0C7",
        _ => "#FEF3F2"
    };
}

/// <summary>Tiến độ đọc/ghi file nhập, dùng cho ProgressBar và dòng trạng thái.</summary>
public sealed class SoQuyImportProgress
{
    public required string GiaiDoan { get; init; }

    public int DaXuLy { get; init; }

    public int TongSo { get; init; }

    public double PhanTram => TongSo <= 0 ? 0 : Math.Clamp(DaXuLy * 100d / TongSo, 0d, 100d);
}

/// <summary>
/// Toàn bộ kết quả đọc và kiểm tra file sổ quỹ, dùng cho bước xem trước.
/// Tại thời điểm này database chưa bị thay đổi.
/// </summary>
public sealed class SoQuyImportPreview
{
    public string TenFile { get; init; } = "";

    /// <summary>Tổng số dòng đọc được từ file (không tính dòng tiêu đề).</summary>
    public int TongSoDong { get; init; }

    /// <summary>Dòng không có lỗi chặn, sẽ được ghi (gồm cả dòng mã trùng chờ chọn cách xử lý).</summary>
    public int SoDongHopLe { get; init; }

    /// <summary>Dòng bị loại do lỗi.</summary>
    public int SoDongLoi { get; init; }

    /// <summary>Dòng có mã phiếu đã tồn tại trong database.</summary>
    public int SoDongTrungMa { get; init; }

    /// <summary>Dòng trùng mã ngay trong chính file, không ghi để tránh đè nhau.</summary>
    public int SoDongTrungTrongFile { get; init; }

    /// <summary>Số phiếu thu / chi đọc được, dùng cho thống kê phụ.</summary>
    public int SoPhieuThu { get; init; }

    public int SoPhieuChi { get; init; }

    /// <summary>Tổng tiền thu / chi trong các dòng sẽ ghi.</summary>
    public decimal TongTienThu { get; init; }

    public decimal TongTienChi { get; init; }

    public List<SoQuyImportRow> DanhSachDong { get; init; } = [];

    public List<SoQuyImportError> DanhSachLoi { get; init; } = [];

    public List<string> CanhBao { get; init; } = [];

    /// <summary>Có dòng nào sẽ được ghi không (dòng hợp lệ hoặc dòng trùng chờ chọn xử lý).</summary>
    public bool CoTheNhap => SoDongHopLe > 0;

    /// <summary>File có dòng trùng mã nên bắt buộc người dùng chọn cách xử lý.</summary>
    public bool CoDongTrung => SoDongTrungMa > 0 || SoDongTrungTrongFile > 0;
}

/// <summary>
/// Kết quả một lượt nhập sổ quỹ: thêm bao nhiêu, cập nhật bao nhiêu,
/// bỏ qua bao nhiêu, lỗi bao nhiêu và có phải đã rollback toàn bộ hay không.
/// </summary>
public sealed class SoQuyImportResult
{
    public string TenFile { get; set; } = "";

    public int TongSoDong { get; set; }

    /// <summary>Phiếu mới được thêm vào database.</summary>
    public int SoThemMoi { get; set; }

    /// <summary>Phiếu trùng mã đã được cập nhật theo file.</summary>
    public int SoCapNhat { get; set; }

    /// <summary>Dòng bị bỏ qua (bỏ qua theo lựa chọn của người dùng hoặc trùng trong file).</summary>
    public int SoBoQua { get; set; }

    /// <summary>Dòng không ghi được vì lỗi.</summary>
    public int SoLoi { get; set; }

    public bool DaHuy { get; set; }

    /// <summary>Lỗi nghiêm trọng nên đã rollback, database không bị dở dang.</summary>
    public bool DaRollback { get; set; }

    public TimeSpan ThoiGian { get; set; }

    public List<SoQuyImportError> DanhSachLoi { get; } = [];

    public List<string> CanhBao { get; set; } = [];

    /// <summary>Cách xử lý dòng trùng mã mà người dùng đã chọn.</summary>
    public XuLyPhieuTrung XuLyTrung { get; set; } = XuLyPhieuTrung.GiuDuLieuCu;

    public string TenXuLyTrungHienThi => XuLyTrung switch
    {
        XuLyPhieuTrung.GiuDuLieuCu => "Giữ dữ liệu cũ",
        XuLyPhieuTrung.ThayThe => "Thay thế dữ liệu cũ",
        XuLyPhieuTrung.BoQua => "Bỏ qua dòng trùng",
        _ => "Không xử lý"
    };

    public int TongSoGhiThanhCong => SoThemMoi + SoCapNhat;

    public string ThongBaoTatCa
    {
        get
        {
            if (DaHuy)
                return "Đã hủy nhập, không có dữ liệu nào được thay đổi.";

            var dong = new List<string>
            {
                $"Tổng số dòng: {TongSoDong:N0}",
                $"Đã thêm: {SoThemMoi:N0}",
                $"Đã cập nhật: {SoCapNhat:N0}",
                $"Bỏ qua: {SoBoQua:N0}",
                $"Lỗi: {SoLoi:N0}"
            };

            if (DaRollback)
                dong.Add("ĐÃ ROLLBACK toàn bộ: database không bị để lại trạng thái nhập dở dang.");

            dong.Add($"Thời gian xử lý: {ThoiGian.TotalSeconds:F1} giây");

            return string.Join("\n", dong);
        }
    }
}

/// <summary>Danh mục tra cứu một lần trước khi validate, để không query lại từng dòng.</summary>
public sealed class SoQuyImportDanhMuc
{
    /// <summary>id nhom_chi_phi -> tên loại, đã bỏ dấu và viết thường để so khớp nhanh.</summary>
    public Dictionary<uint, string> LoaiThuChiTheoId { get; init; } = [];

    /// <summary>Tên loại thu/chi đã chuẩn hóa -> id, dùng khi file chỉ có tên loại.</summary>
    public Dictionary<string, uint> LoaiThuChiTheoTen { get; init; } = new(StringComparer.Ordinal);

    /// <summary>id nguoi_dung -> họ tên.</summary>
    public Dictionary<uint, string> NguoiDungTheoId { get; init; } = [];

    /// <summary>Họ tên đã chuẩn hóa -> id nguoi_dung.</summary>
    public Dictionary<string, uint> NguoiDungTheoTen { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Mã phương thức quỹ -> nhãn hiển thị, lấy từ dữ liệu đang có trong bảng.</summary>
    public Dictionary<string, string> QuyTheoMa { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nhãn quỹ đã chuẩn hóa -> mã phương thức.</summary>
    public Dictionary<string, string> QuyTheoTen { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Giá trị mặc định của cột phuong_thuc trong database (TIEN_MAT).</summary>
    public string PhuongThucMacDinh { get; init; } = "TIEN_MAT";

    /// <summary>Tiền tố mã phiếu dùng khi hệ thống phải tự sinh mã cho dòng thiếu mã.</summary>
    public string TienToThu => "PT";

    public string TienToChi => "PC";
}