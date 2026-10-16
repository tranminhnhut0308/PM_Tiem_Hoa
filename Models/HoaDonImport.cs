namespace ShopHoa.Models;

/// <summary>
/// Cách xử lý hóa đơn trùng mã với dữ liệu đang có trong database.
/// Người dùng chọn một lần cho toàn bộ lượt import, không hỏi từng hóa đơn.
/// </summary>
public enum XuLyHoaDonTrung
{
    /// <summary>Giữ nguyên hóa đơn cũ trong database, không ghi gì thêm.</summary>
    BoQua,

    /// <summary>Ghi đè hóa đơn cũ bằng dữ liệu trong file (kể cả chi tiết hàng hóa).</summary>
    CapNhat,

    /// <summary>Hủy toàn bộ lượt import, không thay đổi dữ liệu nào.</summary>
    Huy
}

/// <summary>
/// Kết quả đối chiếu khách hàng của một hóa đơn trong file.
/// Hóa đơn thiếu khách hàng vẫn được giữ lại để người dùng quyết định
/// bỏ qua hay tạo khách hàng mới, thay vì loại thẳng khỏi danh sách.
/// </summary>
public enum TrangThaiKhachHangHoaDon
{
    /// <summary>Bán lẻ, không gắn khách hàng (khach_hang_id rỗng).</summary>
    KhachLe,

    /// <summary>Đã tìm thấy khách hàng trong database.</summary>
    DaTimThay,

    /// <summary>Không tìm thấy khách hàng, chờ người dùng chọn tạo mới hay bỏ qua.</summary>
    ChuaCoTrongDatabase,

    /// <summary>Đã tạo khách hàng mới trong lượt import này.</summary>
    DaTaoMoi
}

/// <summary>
/// Các trường dữ liệu hóa đơn mà phần mềm cần tìm trong file. Mỗi trường được
/// ánh xạ tới đúng một cột của file, người dùng có thể tự chọn khi tên cột
/// trong file không khớp.
/// </summary>
public enum TruongHoaDon
{
    MaHoaDon,
    ThoiGian,
    MaKhachHang,
    TenKhachHang,
    SoDienThoai,
    TongTien,
    GiamGia,
    DaThanhToan,
    ConNo,
    TrangThai,
    GhiChu,
    MaSanPham,
    TenSanPham,
    SoLuong,
    DonGia,
    ThanhTien,
    DonViTinh
}

/// <summary>
/// Vai trò của một sheet trong file hóa đơn. Sheet chi tiết và sheet gộp đều
/// chứa dòng hàng hóa, khác nhau ở chỗ sheet gộp còn mang thông tin hóa đơn.
/// </summary>
public enum LoaiSheetHoaDon
{
    /// <summary>Mỗi dòng là một hóa đơn, không có dòng hàng hóa riêng.</summary>
    HoaDon,

    /// <summary>Mỗi dòng là một dòng hàng hóa, khớp hóa đơn bằng mã hóa đơn.</summary>
    ChiTiet,

    /// <summary>Mỗi dòng vừa mang thông tin hóa đơn vừa là một dòng hàng hóa.</summary>
    Gop
}

/// <summary>Tên tiếng Việt của từng trường, dùng cho bảng ánh xạ cột.</summary>
public static class TenTruongHoaDon
{
    /// <summary>Các trường thuộc phần thông tin hóa đơn.</summary>
    public static readonly IReadOnlyList<TruongHoaDon> ThongTinHoaDon =
    [
        TruongHoaDon.MaHoaDon,
        TruongHoaDon.ThoiGian,
        TruongHoaDon.MaKhachHang,
        TruongHoaDon.TenKhachHang,
        TruongHoaDon.SoDienThoai,
        TruongHoaDon.TongTien,
        TruongHoaDon.GiamGia,
        TruongHoaDon.DaThanhToan,
        TruongHoaDon.ConNo,
        TruongHoaDon.TrangThai,
        TruongHoaDon.GhiChu
    ];

    /// <summary>Các trường thuộc phần dòng hàng hóa.</summary>
    public static readonly IReadOnlyList<TruongHoaDon> HangHoa =
    [
        TruongHoaDon.MaSanPham,
        TruongHoaDon.TenSanPham,
        TruongHoaDon.SoLuong,
        TruongHoaDon.DonGia,
        TruongHoaDon.ThanhTien,
        TruongHoaDon.DonViTinh
    ];

    public static string Lay(TruongHoaDon truong) => truong switch
    {
        TruongHoaDon.MaHoaDon => "Mã hóa đơn",
        TruongHoaDon.ThoiGian => "Thời gian bán",
        TruongHoaDon.MaKhachHang => "Mã khách hàng",
        TruongHoaDon.TenKhachHang => "Tên khách hàng",
        TruongHoaDon.SoDienThoai => "Số điện thoại",
        TruongHoaDon.TongTien => "Tổng tiền hàng",
        TruongHoaDon.GiamGia => "Giảm giá",
        TruongHoaDon.DaThanhToan => "Khách đã thanh toán",
        TruongHoaDon.ConNo => "Còn nợ",
        TruongHoaDon.TrangThai => "Trạng thái",
        TruongHoaDon.GhiChu => "Ghi chú",
        TruongHoaDon.MaSanPham => "Mã hàng hóa",
        TruongHoaDon.TenSanPham => "Tên hàng hóa",
        TruongHoaDon.SoLuong => "Số lượng",
        TruongHoaDon.DonGia => "Đơn giá",
        TruongHoaDon.ThanhTien => "Thành tiền",
        _ => "Đơn vị tính"
    };

    /// <summary>Gợi ý cách điền, hiển thị cạnh ô chọn cột.</summary>
    public static string GoiY(TruongHoaDon truong) => truong switch
    {
        TruongHoaDon.MaHoaDon => "Bắt buộc, dùng để gom các dòng cùng hóa đơn",
        TruongHoaDon.ThoiGian => "Bỏ trống thì lấy thời gian lúc nhập",
        TruongHoaDon.MaKhachHang => "Ưu tiên tìm khách hàng theo mã",
        TruongHoaDon.TenKhachHang => "Cả mã lẫn tên đều trống thì coi là bán lẻ",
        TruongHoaDon.SoDienThoai => "Dùng khi không có mã khách hàng",
        TruongHoaDon.TongTien => "Bỏ trống thì tính từ tổng dòng chi tiết",
        TruongHoaDon.GiamGia => "Giảm trên cả hóa đơn",
        TruongHoaDon.DaThanhToan => "Bỏ trống thì coi như đã trả đủ",
        TruongHoaDon.ConNo => "Chỉ để đối chiếu, phần mềm tự tính lại",
        TruongHoaDon.TrangThai => "Ghi \"Hủy\" thì bỏ qua hóa đơn đó",
        TruongHoaDon.MaSanPham => "Bắt buộc, đối chiếu hàng hóa theo mã",
        TruongHoaDon.TenSanPham => "Bỏ trống thì lấy tên trong danh mục",
        TruongHoaDon.SoLuong => "Bắt buộc, phải lớn hơn 0",
        TruongHoaDon.DonGia => "Bắt buộc, có thể bằng 0 nếu cho miễn phí",
        TruongHoaDon.ThanhTien => "Bỏ trống thì tính từ số lượng × đơn giá",
        _ => "Bỏ trống thì lấy đơn vị tính trong danh mục"
    };

    /// <summary>Chuyển chữ cái cột trong Excel sang số thứ tự, ví dụ A → 1.</summary>
    public static int CotSangSo(string? chuoiCot)
    {
        if (string.IsNullOrWhiteSpace(chuoiCot))
            return 1;

        var so = 0;

        foreach (var kyTu in chuoiCot.Trim().ToUpperInvariant())
        {
            if (kyTu is < 'A' or > 'Z')
                break;

            so = so * 26 + (kyTu - 'A' + 1);
        }

        return so == 0 ? 1 : so;
    }

    /// <summary>Chuyển số thứ tự cột sang chữ cái trong Excel, ví dụ 1 → A.</summary>
    public static string CotSangChuoi(int soCot)
    {
        if (soCot <= 0)
            return "A";

        var chuoi = "";

        while (soCot > 0)
        {
            soCot--;
            chuoi = (char)('A' + soCot % 26) + chuoi;
            soCot /= 26;
        }

        return chuoi;
    }
}

/// <summary>Một cột trong file, dùng làm lựa chọn khi ánh xạ cột cho trường.</summary>
public sealed class CotFileHoaDon
{
    /// <summary>Vị trí cột trong sheet, bắt đầu từ 0.</summary>
    public required int ViTri { get; init; }

    /// <summary>Tên cột ghi trong dòng tiêu đề của file.</summary>
    public required string Ten { get; init; }

    /// <summary>Tên hiển thị trong danh sách chọn: "C — Mã hóa đơn".</summary>
    public string HienThi => $"{TenTruongHoaDon.CotSangChuoi(ViTri + 1)} — {TenHienThi}";

    public string TenHienThi => Ten.Trim().Length == 0 ? "(cột không tên)" : Ten.Trim();
}

/// <summary>Ánh xạ cột của một sheet: mỗi trường trỏ tới một cột trong file.</summary>
public sealed class SheetHoaDon
{
    public required string TenSheet { get; init; }

    /// <summary>Số dòng chứa tiêu đề, dữ liệu bắt đầu từ dòng kế tiếp.</summary>
    public required int DongTieuDe { get; init; }

    /// <summary>Cột đầu tiên có tiêu đề, dùng khi sheet có cột trống ở đầu.</summary>
    public required int CotDau { get; init; }

    public required List<CotFileHoaDon> Cot { get; init; }

    /// <summary>File gốc, dùng lấy vài dòng mẫu cho màn hình ánh xạ cột.</summary>
    public string? DuongDanFile { get; set; }

    /// <summary>Trường nào ứng với cột thứ mấy trong sheet.</summary>
    public Dictionary<TruongHoaDon, int> Map { get; init; } = [];

    public LoaiSheetHoaDon Loai { get; set; } = LoaiSheetHoaDon.HoaDon;

    /// <summary>Người dùng chọn bỏ qua sheet này, thường là sheet hướng dẫn.</summary>
    public bool BoQua { get; set; }

    /// <summary>Sheet chỉ đọc được khi đã gán cột mã hóa đơn.</summary>
    public bool CoTheDoc => Map.ContainsKey(TruongHoaDon.MaHoaDon);

    /// <summary>Tên hiển thị kèm vị trí tiêu đề và vai trò đoán được.</summary>
    public string MoTa =>
        $"{TenSheet} (tiêu đề ở dòng {DongTieuDe}, {Cot.Count} cột)"
        + (Loai == LoaiSheetHoaDon.HoaDon
            ? " — danh sách hóa đơn"
            : Loai == LoaiSheetHoaDon.ChiTiet
                ? " — danh sách dòng hàng hóa"
                : " — hóa đơn gộp kèm dòng hàng hóa");

    public string TenLoai => Loai switch
    {
        LoaiSheetHoaDon.HoaDon => "Danh sách hóa đơn",
        LoaiSheetHoaDon.ChiTiet => "Danh sách dòng hàng hóa",
        _ => "Hóa đơn gộp kèm dòng hàng hóa"
    };

    /// <summary>Các lựa chọn vai trò cho sheet.</summary>
    public IReadOnlyList<LoaiSheetHoaDon> LuaChonLoai { get; } =
    [
        LoaiSheetHoaDon.HoaDon,
        LoaiSheetHoaDon.ChiTiet,
        LoaiSheetHoaDon.Gop
    ];
}

/// <summary>
/// Toàn bộ ánh xạ cột của một file: mỗi sheet một phần, phần mềm dùng để đọc
/// dữ liệu đúng theo lựa chọn của người dùng.
/// </summary>
public sealed class MappingHoaDon
{
    public required string TenFile { get; init; }

    public required List<SheetHoaDon> Sheets { get; init; }

    /// <summary>Các sheet được dùng để nhập, tức không bị bỏ qua và đã gán mã hóa đơn.</summary>
    public IEnumerable<SheetHoaDon> SheetsDung =>
        Sheets.Where(s => !s.BoQua && s.CoTheDoc);

    /// <summary>Sheet nào đóng vai trò danh sách hóa đơn.</summary>
    public bool CoSheetHoaDon =>
        SheetsDung.Any(s => s.Loai == LoaiSheetHoaDon.HoaDon);

    public bool CoSheetChiTiet =>
        SheetsDung.Any(s => s.Loai != LoaiSheetHoaDon.HoaDon);

    /// <summary>
    /// Phần mềm đã tự nhận ra hết cột cần dùng thì không cần mở màn hình ánh xạ.
    /// </summary>
    public bool DaKhopDayDu => SheetsDung.Any() && !CoTruongChuaGan().Any();

    /// <summary>
    /// Các trường bắt buộc mà chưa được gán cột. Mã hóa đơn luôn bắt buộc,
    /// các trường dòng hàng hóa chỉ bắt buộc với sheet có dòng hàng hóa.
    /// </summary>
    public IEnumerable<TruongHoaDon> CoTruongChuaGan()
    {
        foreach (var sheet in SheetsDung)
        {
            foreach (var truong in LayTruongBatBuoc(sheet.Loai))
            {
                if (!sheet.Map.ContainsKey(truong))
                    yield return truong;
            }
        }
    }

    /// <summary>Các trường bắt buộc của một loại sheet.</summary>
    public static IReadOnlyList<TruongHoaDon> LayTruongBatBuoc(LoaiSheetHoaDon loai) =>
        loai == LoaiSheetHoaDon.HoaDon
            ? [TruongHoaDon.MaHoaDon]
            : [TruongHoaDon.MaHoaDon, TruongHoaDon.MaSanPham, TruongHoaDon.SoLuong, TruongHoaDon.DonGia];
}

/// <summary>Kết quả đọc dòng tiêu đề của file, dùng để dựng màn hình ánh xạ cột.</summary>
public sealed class ThongTinFileHoaDon
{
    public required string TenFile { get; init; }

    public required MappingHoaDon Mapping { get; init; }

    /// <summary>Đã tự khớp được hết cột nên không cần mở màn hình ánh xạ.</summary>
    public bool DaKhopDayDu => Mapping.DaKhopDayDu;

    /// <summary>File không có sheet nào dùng được, cần báo cho người dùng.</summary>
    public bool KhongDocDuoc => !Mapping.SheetsDung.Any();

    /// <summary>Các lý do chưa khớp đủ, hiển thị trên màn hình ánh xạ cột.</summary>
    public List<string> LyDo() =>
        Mapping.CoTruongChuaGan()
            .Select(t => TenTruongHoaDon.Lay(t))
            .Distinct()
            .ToList();
}

/// <summary>Thông tin hàng hóa lấy từ database, dùng khi ghi chi tiết hóa đơn.</summary>
public sealed class HoaDonImportSanPham
{
    public required uint Id { get; init; }

    public required string TenSanPham { get; init; }

    public string? DonViTinh { get; init; }

    public decimal GiaVon { get; init; }
}

/// <summary>
/// Một dòng chi tiết hóa đơn đọc được từ file. Tiền trong file không sửa
/// trực tiếp mà để nguyên, mọi giá trị ghi xuống database đều tính lại ở
/// các thuộc tính tính toán, nên không bao giờ phải gán đè sau khi đọc.
/// </summary>
public sealed class HoaDonImportDetailRow
{
    /// <summary>Số dòng thật trong file, dùng để báo lỗi cho người dùng.</summary>
    public int SoDongExcel { get; init; }

    public string MaHoaDon { get; init; } = "";

    /// <summary>Mã hàng hóa trong file, dùng để tra cứu database.</summary>
    public string MaSanPham { get; init; } = "";

    /// <summary>Tên hàng hóa ghi trong file, có thể trống.</summary>
    public string TenSanPhamTrongFile { get; init; } = "";

    public decimal SoLuong { get; init; }

    public decimal DonGia { get; init; }

    public decimal GiamGia { get; init; }

    /// <summary>Thành tiền ghi trong file, bằng 0 nghĩa là không có cột này.</summary>
    public decimal ThanhTien { get; init; }

    public string? GhiChu { get; init; }

    /// <summary>Hàng hóa tìm thấy trong database, gán trong lúc kiểm tra.</summary>
    public HoaDonImportSanPham? SanPham { get; set; }

    /// <summary>
    /// Tên sẽ ghi xuống database: ưu tiên tên trong file, thiếu thì lấy tên
    /// trong danh mục.
    /// </summary>
    public string TenSanPhamDung =>
        TenSanPhamTrongFile.Length > 0 ? TenSanPhamTrongFile : SanPham?.TenSanPham ?? "";

    /// <summary>
    /// Thành tiền dùng để ghi: nếu file có cột thành tiền thì lấy theo file,
    /// ngược lại tự tính từ số lượng, đơn giá và giảm giá.
    /// </summary>
    public decimal ThanhTienDung =>
        ThanhTien > 0m ? ThanhTien : Math.Max(0m, SoLuong * DonGia - GiamGia);
}

/// <summary>
/// Một hóa đơn gom từ file: thông tin header cộng với các dòng chi tiết
/// hàng hóa có cùng mã hóa đơn. Mọi cột tiền đều là kiểu nullable để phân biệt
/// "file không có cột này" với "file ghi giá trị 0".
/// </summary>
public sealed class HoaDonImportRow
{
    /// <summary>Số dòng trong file nơi bắt đầu hóa đơn này.</summary>
    public int SoDongExcel { get; init; }

    public string MaHoaDon { get; init; } = "";

    /// <summary>Thời gian bán trong file, null nghĩa là file không có cột này.</summary>
    public DateTime? ThoiGianRaw { get; init; }

    /// <summary>
    /// Thời điểm bắt đầu lượt import, dùng làm thời gian bán cho những hóa đơn
    /// mà file không có cột ngày. Chốt một lần cho cả lượt nên không bị lệch
    /// nhau giữa các hóa đơn.
    /// </summary>
    public required DateTime ThoiGianMacDinh { get; init; }

    /// <summary>Thời gian bán sẽ ghi xuống database.</summary>
    public DateTime ThoiGianBan => ThoiGianRaw ?? ThoiGianMacDinh;

    /// <summary>Mã khách hàng trong file (có thể trống).</summary>
    public string? MaKhachHang { get; init; }

    public string? TenKhachHang { get; init; }

    public string? SoDienThoai { get; init; }

    /// <summary>Tổng tiền hàng trong file, null nghĩa là không có cột này.</summary>
    public decimal? TongTienRaw { get; init; }

    public decimal GiamGia { get; init; }

    /// <summary>Số tiền khách đã trả trong file, null nghĩa là không có cột này.</summary>
    public decimal? KhachDaThanhToanRaw { get; init; }

    /// <summary>Cột "còn nợ" trong file, chỉ dùng để cảnh báo lệch, không tin để ghi.</summary>
    public decimal? ConNoRaw { get; init; }

    /// <summary>Trạng thái trong file, dùng để nhận ra hóa đơn bị hủy.</summary>
    public string? TrangThaiRaw { get; init; }

    /// <summary>File ghi rõ hóa đơn đã hủy thì không nhập, tránh làm sai doanh thu.</summary>
    public bool DaHuyTrongFile { get; init; }

    public string? GhiChu { get; init; }

    public List<HoaDonImportDetailRow> ChiTiet { get; } = [];

    /// <summary>Mã hóa đơn đã có sẵn trong database.</summary>
    public bool LaHoaDonTrung { get; set; }

    public TrangThaiKhachHangHoaDon TrangThaiKhachHang { get; set; } = TrangThaiKhachHangHoaDon.KhachLe;

    /// <summary>Id khách hàng sau khi đối chiếu, null với hóa đơn bán lẻ.</summary>
    public uint? IdKhachHang { get; set; }

    private decimal? _tongChiTiet;

    /// <summary>Tổng tiền của các dòng chi tiết, tính một lần rồi dùng lại.</summary>
    public decimal TongChiTietTinhToi
    {
        get
        {
            if (_tongChiTiet.HasValue)
                return _tongChiTiet.Value;

            var tong = 0m;

            foreach (var dong in ChiTiet)
                tong += dong.ThanhTienDung;

            _tongChiTiet = tong;
            return tong;
        }
    }

    /// <summary>Tổng tiền dùng để ghi: theo file nếu có, không thì tính từ chi tiết.</summary>
    public decimal TongTienHieuDung => TongTienRaw ?? TongChiTietTinhToi;

    /// <summary>Số tiền khách cần trả, đã trừ giảm giá và không nhỏ hơn 0.</summary>
    public decimal KhachCanTra => Math.Max(0m, TongTienHieuDung - GiamGia);

    /// <summary>
    /// Số tiền khách đã trả: không có cột thì coi như trả đủ, có cột thì
    /// không được vượt quá số tiền cần trả (phần vượt ghi vào thu khác).
    /// </summary>
    public decimal DaThanhToanChuan =>
        Math.Clamp(KhachDaThanhToanRaw ?? KhachCanTra, 0m, KhachCanTra);

    /// <summary>Phần tiền khách trả vượt, ghi vào cột thu_khac giống lúc bán hàng.</summary>
    public decimal ThuKhacChuan => Math.Max(0m, (KhachDaThanhToanRaw ?? 0m) - KhachCanTra);

    /// <summary>
    /// Công nợ luôn được tính lại từ tiền trong file thay vì tin cột "Còn nợ",
    /// để khớp với logic phần mềm đang dùng.
    /// </summary>
    public decimal ConNoChuan => Math.Max(0m, KhachCanTra - DaThanhToanChuan);

    /// <summary>Trạng thái thanh toán: chỉ dùng hai giá trị database đang chấp nhận.</summary>
    public string TrangThaiThanhToanChuan => ConNoChuan > 0m ? "CONG_NO" : "DA_THANH_TOAN";

    /// <summary>Tên khách hàng để hiển thị trong cửa sổ xem trước.</summary>
    public string TenKhachHangHienThi => IdKhachHang.HasValue
        ? TenKhachHang ?? "(khách hàng)"
        : TenKhachHang is { Length: > 0 } ten ? ten : "Khách lẻ";

    /// <summary>Cột trạng thái dùng cho bảng xem trước trong giao diện.</summary>
    public string TrangThaiHienThi => DaHuyTrongFile
        ? "Hủy trong file"
        : LaHoaDonTrung ? "Trùng mã" : "Mới";

    /// <summary>Cột khách hàng dùng cho bảng xem trước trong giao diện.</summary>
    public string TrangThaiKhachHangHienThi => TrangThaiKhachHang switch
    {
        TrangThaiKhachHangHoaDon.DaTimThay => "Đã có trong DB",
        TrangThaiKhachHangHoaDon.ChuaCoTrongDatabase => "Chưa có trong DB",
        TrangThaiKhachHangHoaDon.DaTaoMoi => "Sẽ tạo mới",
        _ => "Khách lẻ"
    };
}

/// <summary>Một hóa đơn bị loại khỏi lượt import, kèm lý do cụ thể.</summary>
public sealed record HoaDonImportError(int SoDongExcel, string? MaHoaDon, string LyDo);

/// <summary>
/// Ảnh chụp tiến trình import để hiển thị lên giao diện.
/// </summary>
public sealed class HoaDonImportProgress
{
    /// <summary>Giai đoạn đang chạy: đọc file, kiểm tra hoặc ghi database.</summary>
    public required string GiaiDoan { get; init; }

    /// <summary>Số hóa đơn đã xử lý xong.</summary>
    public int DaXuLy { get; init; }

    /// <summary>Tổng số hóa đơn cần xử lý.</summary>
    public int TongSo { get; init; }

    /// <summary>Số hóa đơn đã ghi thành công.</summary>
    public int ThanhCong { get; init; }

    /// <summary>Số hóa đơn bị bỏ qua (trùng hoặc theo lựa chọn của người dùng).</summary>
    public int BoQua { get; init; }

    /// <summary>Số hóa đơn bị lỗi.</summary>
    public int Loi { get; init; }

    /// <summary>Phần trăm đã hoàn thành, luôn nằm trong khoảng 0 → 100.</summary>
    public double PhanTram => TongSo <= 0 ? 0 : Math.Clamp(DaXuLy * 100d / TongSo, 0d, 100d);

    /// <summary>Dòng chữ kiểu "Đã xử lý 1.250 / 5.000 hóa đơn (25%)".</summary>
    public string MoTa =>
        $"Đã xử lý {DaXuLy:N0} / {TongSo:N0} hóa đơn ({PhanTram:F0}%)"
        + $"  •  Thành công: {ThanhCong:N0}  •  Bỏ qua: {BoQua:N0}  •  Lỗi: {Loi:N0}";
}

/// <summary>
/// Toàn bộ kết quả đọc và kiểm tra file, dùng cho bước xem trước trước khi ghi.
/// Chưa có thay đổi nào trên database tại thời điểm này.
/// </summary>
public sealed class HoaDonImportPreview
{
    public string TenFile { get; init; } = "";

    /// <summary>Số hóa đơn đọc được sau khi gộp các dòng cùng mã.</summary>
    public int TongSoHoaDon { get; init; }

    /// <summary>Tổng số dòng chi tiết hàng hóa đọc được từ file.</summary>
    public int TongSoDongChiTiet { get; init; }

    /// <summary>Số hóa đơn mã chưa có trong database.</summary>
    public int SoHoaDonMoi { get; init; }

    /// <summary>Số hóa đơn mã đã có trong database.</summary>
    public int SoHoaDonTrung { get; init; }

    /// <summary>
    /// Số hóa đơn đọc được và không có lỗi chặn, gồm cả những hóa đơn đang
    /// chờ người dùng quyết định tạo khách hàng.
    /// </summary>
    public int SoHoaDonHopLe { get; init; }

    /// <summary>Số hóa đơn không tìm thấy khách hàng.</summary>
    public int SoKhachHangKhongTonTai { get; init; }

    /// <summary>
    /// Số khách hàng có thể tạo mới: những mã khách hàng trong file chưa có
    /// trong database và có đủ tên để tạo.
    /// </summary>
    public int SoKhachHangCoTheTaoMoi { get; init; }

    /// <summary>Số hóa đơn không tìm thấy hàng hóa ở ít nhất một dòng chi tiết.</summary>
    public int SoSanPhamKhongTonTai { get; init; }

    /// <summary>Số hóa đơn bị hủy ngay trong file, sẽ không nhập.</summary>
    public int SoHoaDonHuy { get; init; }

    /// <summary>Số hóa đơn lỗi không thể nhập (thiếu mã, sai ngày, thiếu chi tiết...).</summary>
    public int SoLoi { get; init; }

    /// <summary>Hóa đơn đọc được, đã gom đầy đủ chi tiết.</summary>
    public List<HoaDonImportRow> DanhSachHoaDon { get; init; } = [];

    /// <summary>Các mã hóa đơn đã tồn tại trong database.</summary>
    public HashSet<string> MaHoaDonTonTai { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Chi tiết lỗi để hiển thị trong cửa sổ xem trước và xuất ra file.</summary>
    public List<HoaDonImportError> DanhSachLoi { get; init; } = [];

    /// <summary>Các cột không tìm thấy trong file, dùng để cảnh báo cho người dùng.</summary>
    public List<string> CanhBao { get; init; } = [];

    /// <summary>Có hàng hóa nào trong file không tồn tại trong database hay không.</summary>
    public bool CoChiTietHangHoa => TongSoDongChiTiet > 0;

    /// <summary>Có thể tạo khách hàng mới hay không: cần mã khách hàng và tên.</summary>
    public bool CoTheTaoKhachHangMoi => SoKhachHangCoTheTaoMoi > 0;
}

/// <summary>
/// Kết quả một lượt import hóa đơn: đã ghi được bao nhiêu, bỏ qua bao nhiêu,
/// lỗi bao nhiêu và lỗi chi tiết nằm ở đâu.
/// </summary>
public sealed class HoaDonImportResult
{
    public string TenFile { get; init; } = "";

    public int TongSoHoaDon { get; init; }

    public int TongSoDongChiTiet { get; init; }

    /// <summary>Số hóa đơn bị lỗi ngay khi đọc và kiểm tra file.</summary>
    public int SoLoi { get; set; }

    /// <summary>Hóa đơn mới được thêm vào database.</summary>
    public int SoThemMoi { get; set; }

    /// <summary>Hóa đơn trùng đã được cập nhật theo file.</summary>
    public int SoCapNhat { get; set; }

    /// <summary>Hóa đơn bị bỏ qua theo lựa chọn của người dùng.</summary>
    public int SoBoQua { get; set; }

    /// <summary>Người dùng hủy nên không ghi gì.</summary>
    public bool DaHuy { get; set; }

    /// <summary>Gặp lỗi nghiêm trọng nên đã rollback toàn bộ, database không bị dở dang.</summary>
    public bool DaRollback { get; set; }

    /// <summary>Số khách hàng được tạo mới trong lượt import này.</summary>
    public int SoKhachHangTaoMoi { get; set; }

    public TimeSpan ThoiGian { get; set; }

    public List<HoaDonImportError> DanhSachLoi { get; } = [];

    public List<string> CanhBao { get; } = [];

    /// <summary>Tổng số hóa đơn đã thay đổi trong database.</summary>
    public int TongSoGhiThanhCong => SoThemMoi + SoCapNhat;

    public string ThongBaoTatCa
    {
        get
        {
            if (DaHuy)
                return "Đã hủy import, không có dữ liệu nào được thay đổi.";

            var dong = new List<string>
            {
                $"Tổng số hóa đơn: {TongSoHoaDon:N0}",
                $"Dòng chi tiết hàng hóa: {TongSoDongChiTiet:N0}",
                $"Import thành công: {TongSoGhiThanhCong:N0}"
                    + $" (thêm mới {SoThemMoi:N0}, cập nhật {SoCapNhat:N0})",
                $"Bỏ qua: {SoBoQua:N0}",
                $"Dữ liệu lỗi: {SoLoi:N0}"
            };

            if (SoKhachHangTaoMoi > 0)
                dong.Add($"Khách hàng đã tạo mới: {SoKhachHangTaoMoi:N0}");

            if (DaRollback)
                dong.Add("ĐÃ ROLLBACK toàn bộ: database không bị để lại trạng thái nhập dở dang.");

            dong.Add($"Thời gian xử lý: {ThoiGian.TotalSeconds:F1} giây");

            return string.Join("\n", dong);
        }
    }
}
