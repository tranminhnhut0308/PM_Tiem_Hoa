using System.Data;
using System.Diagnostics;
using System.Text;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Nhập hóa đơn bán hàng từ file Excel/CSV vào database.
///
/// Vì sao tách riêng thành service:
/// - Phần đọc file, kiểm tra và ghi database rất dài, tách ra cho ViewModel
///   chỉ lo cập nhật giao diện.
/// - Chỉ dùng chung kết quả với <see cref="DatabaseService"/>, không tự tạo
///   connection string riêng và không viết lại các hàm đọc tiền/ngày đã có.
///
/// Bảng dùng đúng tên đang có trong database:
/// hoa_don_ban_hang, chi_tiet_hoa_don_ban_hang và lich_su_cong_no_khach_hang.
/// </summary>
public sealed class HoaDonImportService
{
    /// <summary>Số hóa đơn ghi trong một bát. Vừa đủ lớn để nhanh, vừa nhỏ để lỗi không ảnh hưởng nhiều.</summary>
    private const int SoHoaDonMoiTrenBatch = 400;

    /// <summary>Số dòng chi tiết gộp trong một câu lệnh INSERT.</summary>
    private const int SoDongChiTietTrenCauLenh = 500;

    /// <summary>Số hóa đơn gộp trong một câu lệnh INSERT.</summary>
    private const int SoDongHeaderTrenCauLenh = 200;

    /// <summary>Số lỗi chi tiết hiển thị tối đa cho một hóa đơn, phần còn lại gom thành một dòng.</summary>
    private const int SoLoiChiTietToiDaMoiHoaDon = 5;

    /// <summary>Số cảnh báo tối đa, để file thiếu cột không làm ngập cửa sổ xem trước.</summary>
    private const int SoCanhBaoToiDa = 30;

    /// <summary>Độ dài tối đa của cột ma_hoa_don trong database.</summary>
    private const int DoDaiToiDaMaHoaDon = 50;

    /// <summary>Độ dài tối đa của cột ma_san_pham trong database.</summary>
    private const int DoDaiToiDaMa = 50;

    /// <summary>Độ dài tối đa của cột ten_san_pham trong database.</summary>
    private const int DoDaiToiDaTenSanPham = 255;

    private const string BangTamCapNhat = "tmp_nhap_hoa_don";
    private const string BangHoaDon = "hoa_don_ban_hang";
    private const string BangChiTiet = "chi_tiet_hoa_don_ban_hang";
    private const string BangCongNo = "lich_su_cong_no_khach_hang";

    private const string DaThanhToan = "DA_THANH_TOAN";
    private const string HoanThanh = "HOAN_THANH";

    private readonly DatabaseService _db = new();

    // ============================================================
    // TÊN CỘT ĐƯỢC CHẤP NHẬN
    // Tất cả đã bỏ dấu và bỏ ký tự phân tách, đúng kiểu khóa mà
    // DatabaseService.NormalizeHeaderKey tạo ra. Nhờ vậy "Mã hóa đơn",
    // "MaHoaDon", "Ma HD" hay "Mã HĐ" đều khớp với cùng một khóa.
    // ============================================================

/// <summary>
    /// Tên cột được chấp nhận cho từng trường, tất cả đã bỏ dấu và bỏ ký tự
    /// phân tách, đúng kiểu khóa mà DatabaseService.NormalizeHeaderKey tạo ra.
    /// Nhờ vậy "Mã hóa đơn", "MaHoaDon", "Ma HD" hay "Mã HĐ" đều khớp với
    /// cùng một trường. Thứ tự trong bảng quyết định trường nào được ưu tiên
    /// khi một cột khớp nhiều trường.
    /// </summary>
    private static readonly (TruongHoaDon Truong, HashSet<string> Khoa)[] BangDoanCot =
    [
        (TruongHoaDon.MaHoaDon, TaoKhoa(
            "mahoadon", "mahd", "sohoadon", "sohd", "mahdbh", "mahdbanhang", "mahdbh",
            "machungtu", "mahdcu", "mahdcuhoa", "invoicecode", "billcode", "mabill",
            "sophieu", "maphieu", "soct", "mact", "billno", "invoiceno", "sophieuban")),
        (TruongHoaDon.ThoiGian, TaoKhoa(
            "thoigian", "thoigianban", "thoigianbanhang", "ngay", "ngayban", "ngaylap",
            "ngayhoadon", "ngaytao", "ngayxuat", "ngayhd", "ngaygd", "date", "datetime",
            "thoidiem", "ngayvao")),
        (TruongHoaDon.MaKhachHang, TaoKhoa(
            "makhachhang", "makh", "mkh", "mach", "customercode", "mkhach",
            "codekhachhang", "makkh", "makhachhangcu", "codekh")),
        (TruongHoaDon.TenKhachHang, TaoKhoa(
            "tenkhachhang", "tenkh", "tenkhach", "khachhang", "ten", "hoten",
            "tenkhachhangdaydu", "khachhangten", "tennguoidung", "nguoidung")),
        (TruongHoaDon.SoDienThoai, TaoKhoa(
            "sodienthoai", "sdt", "dienthoai", "sodt", "sodienthoaikhachhang",
            "tel", "phone", "telephone", "mobile", "dt")),
        (TruongHoaDon.TongTien, TaoKhoa(
            "tongtien", "tongtienhang", "tongtiendonhang", "tongtienthanhtoan",
            "tonggiatri", "tongtienhd", "total", "subtotal", "tongtienban", "tong",
            "tongtienthanhtoanhd", "amounttotal", "tongthanhtoanhd")),
        (TruongHoaDon.GiamGia, TaoKhoa(
            "giamgia", "giamgiahd", "giamgiatong", "discount", "chietkhau",
            "chietkhauhd", "giam", "giamgiadong", "giamgiahang")),
        (TruongHoaDon.DaThanhToan, TaoKhoa(
            "datra", "dathanhtoan", "khachdathanhtoan", "tiendatra", "tiendathanhtoan",
            "paid", "amountpaid", "dathanh", "khachdatra", "tiendathanh", "tiendathanhtoanhd")),
        (TruongHoaDon.ConNo, TaoKhoa(
            "conno", "con", "conn", "congno", "duno", "conocn", "condebt",
            "khachconno", "tienno", "connoai", "nochuntrahd")),
        (TruongHoaDon.TrangThai, TaoKhoa(
            "trangthai", "trangthaithanhtoan", "trangthaihd", "tinhtrang", "status",
            "trangthaiban", "tinhtranghd")),
        (TruongHoaDon.GhiChu, TaoKhoa(
            "ghichu", "ghiuchu", "note", "notes", "chukhucn", "ghichuhd", "lydophieu",
            "ghichucuahoadon", "notehd")),
        (TruongHoaDon.MaSanPham, TaoKhoa(
            "masanpham", "mahang", "mahanghoa", "ma", "msp", "mhh", "masp", "masanphamcu",
            "productcode", "itemcode", "mhhcua")),
        (TruongHoaDon.TenSanPham, TaoKhoa(
            "tensanpham", "tenhang", "tensp", "productname", "tenhanghoa", "tenhh",
            "tenhanghoacu", "tenhhcu", "itemname")),
        (TruongHoaDon.SoLuong, TaoKhoa(
            "soluong", "sl", "qty", "quantity", "soluongban", "slban", "soluongmua", "so")),
        (TruongHoaDon.DonGia, TaoKhoa(
            "dongia", "dg", "donviagia", "gia", "price", "giaban", "dongiaban",
            "dongiavnd", "dongiacp", "giadonga", "dongiaban")),
        (TruongHoaDon.ThanhTien, TaoKhoa(
            "thanhtien", "thanhtienban", "amount", "sotienthanhtoan", "tienthanhtoan",
            "thanhtienhang", "amountnet", "tienhang")),
        (TruongHoaDon.DonViTinh, TaoKhoa(
            "donvitinh", "dvt", "dv", "unit", "donvitinhban", "dvtban", "donvitinhhh"))
    ];

    /// <summary>Các tên ghi trong cột trạng thái mà bị hiểu là hóa đơn đã hủy.</summary>
    private static readonly HashSet<string> TrangThaiDaHuy = TaoKhoa(
        "huy", "huyhoadon", "huydon", "huyhd", "cancelled", "canceled", "cancel",
        "void", "voided", "deleted", "dahuy");

    /// <summary>Các tên khách hàng dùng cho hóa đơn bán lẻ, không gắn khách hàng.</summary>
    private static readonly HashSet<string> TenKhachLe = TaoKhoa(
        "khachle", "khachvanglai", "khachvang", "le", "khac", "retail", "walkin",
        "khachkhongten", "anonymous");

    private static HashSet<string> TaoKhoa(params string[] keys) =>
        new(keys, StringComparer.Ordinal);

    /// <summary>
    /// Khóa so sánh tên cột: đã bỏ dấu, không phân biệt hoa thường và bỏ cả dấu
    /// gạch dưới. Nhờ vậy "Mã hóa đơn", "Ma_Hoa_Don" và "MaHoaDon" đều ra cùng
    /// một khóa "mahoadon" khớp với danh sách bí danh viết liền.
    /// </summary>
    private static string KhoaCot(string? ten)
        => DatabaseService.NormalizeHeaderKey(ten).Replace("_", "", StringComparison.Ordinal);

    // ============================================================
    // ĐỌC FILE
    // ============================================================

    /// <summary>
    /// Bước đầu tiên của quy trình nhập: đọc dòng tiêu đề của từng sheet, tự
    /// đối chiếu tên cột với từng trường hóa đơn rồi trả về ánh xạ đoán được.
    /// Chưa đọc dữ liệu và chưa gì xuống database. Nếu ánh xạ còn thiếu thì
    /// giao diện cho người dùng chọn cột rồi gọi lại <see cref="DocFileAsync"/>.
    /// </summary>
    public async Task<ThongTinFileHoaDon> DocTieuDeAsync(
        string filePath,
        CancellationToken ct = default)
    {
        KiemTraDuongDan(filePath);

        var sheets = await Task.Run(() => DocTieuDeSheetFile(filePath, ct), ct);

        foreach (var sheet in sheets)
        {
            sheet.Loai = DoanLoaiSheet(sheet);
            sheet.DuongDanFile = filePath;
        }

        return new ThongTinFileHoaDon
        {
            TenFile = Path.GetFileName(filePath),
            Mapping = new MappingHoaDon
            {
                TenFile = Path.GetFileName(filePath),
                Sheets = sheets
            }
        };
    }

    /// <summary>
    /// Đọc file theo ánh xạ cột đã chốt, gom các dòng cùng mã hóa đơn thành một
    /// hóa đơn kèm danh sách chi tiết, rồi kiểm tra toàn bộ mà chưa ghi gì
    /// xuống database.
    /// </summary>
    public async Task<HoaDonImportPreview> DocFileAsync(
        string filePath,
        MappingHoaDon mapping,
        IProgress<HoaDonImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        KiemTraDuongDan(filePath);
        ArgumentNullException.ThrowIfNull(mapping);

        if (!mapping.SheetsDung.Any())
        {
            throw new InvalidOperationException(
                "Chưa chọn cột nào dùng được cho hóa đơn. "
                + "Hãy chọn sheet cần nhập và gán cột \"Mã hóa đơn\" cho nó.");
        }

        progress?.Report(new HoaDonImportProgress
        {
            GiaiDoan = "Đang đọc dữ liệu từ file..."
        });

        var duLieu = await Task.Run(() => DocNoiDungFile(filePath, mapping, ct), ct);

        progress?.Report(new HoaDonImportProgress
        {
            GiaiDoan = "Đang kiểm tra khách hàng, hàng hóa và hóa đơn trùng..."
        });

        return await KiemTraAsync(filePath, duLieu, progress, ct);
    }

    private static void KiemTraDuongDan(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Đường dẫn file không hợp lệ.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file hóa đơn đã chọn.", filePath);
    }

    /// <summary>
    /// Lấy vài dòng dữ liệu đầu tiên của một sheet để màn hình ánh xạ cột hiển
    /// thị giá trị thật bên cạnh tên cột. Xem trước thất bại không được làm hỏng
    /// việc ánh xạ nên lỗi ở đây được bỏ qua.
    /// </summary>
    public static List<string[]> DocDongMau(
        string filePath,
        string tenSheet,
        int dongTieuDe,
        int cotDau,
        int soCot,
        int soDong)
    {
        var ketQua = new List<string[]>();

        if (soCot <= 0 || soDong <= 0)
            return ketQua;

        try
        {
            var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

            if (extension == ".csv")
            {
                using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
                var dong = TachDongCsv(reader.ReadToEnd());

                for (var i = dongTieuDe; i < dong.Count && ketQua.Count < soDong; i++)
                    ketQua.Add(TaoDongMau(dong[i], soCot));

                return ketQua;
            }

            if (extension != ".xlsx" && extension != ".xlsm")
                return ketQua;

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name == tenSheet);

            if (sheet is null)
                return ketQua;

            var dongCuoi = sheet.LastRowUsed()?.RowNumber() ?? 0;

            for (var row = dongTieuDe + 1; row <= dongCuoi && ketQua.Count < soDong; row++)
            {
                ketQua.Add([.. sheet.Row(row)
                    .Cells(cotDau, cotDau + soCot - 1)
                    .Select(c => c.GetFormattedString())]);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Bỏ qua xem trước sheet \"{tenSheet}\" của file \"{filePath}\": {ex.Message}");
        }

        return ketQua;
    }

    /// <summary>Cắt dòng mẫu về đúng số cột của sheet, phần thiếu thì để trống.</summary>
    private static string[] TaoDongMau(string[] dong, int soCot)
    {
        var ketQua = new string[soCot];

        for (var i = 0; i < soCot; i++)
            ketQua[i] = i < dong.Length ? dong[i].Trim() : "";

        return ketQua;
    }

    /// <summary>
    /// Đọc dòng tiêu đề và tự gán cột cho từng sheet. Sheet nào không tìm được
    /// cột mã hóa đơn vẫn được giữ lại để người dùng tự gán tay.
    /// </summary>
    private static List<SheetHoaDon> DocTieuDeSheetFile(string filePath, CancellationToken ct)
    {
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        if (extension == ".xlsx" || extension == ".xlsm")
            return DocTieuDeExcel(filePath, ct);

        if (extension == ".csv")
            return [DocTieuDeCsv(filePath)];

        if (extension == ".xls")
        {
            throw new NotSupportedException(
                "Định dạng .xls (Excel 97-2003) chưa được phần mềm hỗ trợ. "
                + "Hãy mở file rồi lưu lại dưới dạng .xlsx hoặc .csv, sau đó nhập lại.");
        }

        throw new NotSupportedException(
            $"Định dạng \"{(extension.Length == 0 ? filePath : extension)}\" không được hỗ trợ. "
            + "Chỉ nhận file .xlsx (Excel) hoặc .csv.");
    }

    private static List<SheetHoaDon> DocTieuDeExcel(string filePath, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheets = new List<SheetHoaDon>();

        foreach (var worksheet in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();

            var sheet = DocTieuDeSheet(worksheet);

            if (sheet is not null)
                sheets.Add(sheet);
        }

        return sheets;
    }

    private static SheetHoaDon DocTieuDeCsv(string filePath)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        var dong = TachDongCsv(reader.ReadToEnd());

        if (dong.Count == 0)
            throw new InvalidOperationException("File CSV rỗng hoặc không đọc được nội dung.");

        var cot = new List<CotFileHoaDon>(dong[0].Length);

        for (var i = 0; i < dong[0].Length; i++)
        {
            cot.Add(new CotFileHoaDon
            {
                ViTri = i,
                Ten = dong[0][i].Trim()
            });
        }

        var sheet = new SheetHoaDon
        {
            TenSheet = Path.GetFileName(filePath),
            DongTieuDe = 1,
            CotDau = 0,
            Cot = cot,
            Map = TuDoanCot(cot)
        };

        sheet.Loai = DoanLoaiSheet(sheet);

        return sheet;
    }

    /// <summary>Loại sheet tìm thấy trong file, dùng để đoán sheet nào là hóa đơn.</summary>
    private enum LoaiSheet
    {
        /// <summary>Chỉ có thông tin hóa đơn, mỗi dòng là một hóa đơn.</summary>
        HoaDon,

        /// <summary>Chỉ có dòng chi tiết hàng hóa, khớp với hóa đơn bằng mã hóa đơn.</summary>
        ChiTiet,

        /// <summary>Mỗi dòng vừa là hóa đơn vừa là một dòng chi tiết của chính nó.</summary>
        Gop
    }

    /// <summary>Một dòng dữ liệu đã đọc, tra cứu theo tên trường đã ánh xạ.</summary>
    private sealed class DongFile
    {
        public required int SoDong { get; init; }

        public required Dictionary<TruongHoaDon, string> GiaTri { get; init; }
    }

    /// <summary>Thông tin đọc dữ liệu của một sheet theo ánh xạ đã chốt.</summary>
    private sealed class TieuDeSheet
    {
        public required string TenSheet { get; init; }

        /// <summary>Số dòng chứa tiêu đề, dữ liệu bắt đầu từ dòng kế tiếp.</summary>
        public required int DongTieuDe { get; init; }

        /// <summary>Cột đầu tiên có dữ liệu, dùng khi sheet có cột trống ở đầu.</summary>
        public required int CotDau { get; init; }

        /// <summary>Trường nào nằm ở cột thứ mấy tính từ <see cref="CotDau"/>.</summary>
        public required Dictionary<TruongHoaDon, int> Map { get; init; }

        public required LoaiSheetHoaDon Loai { get; set; }

        public bool CoCotMaHoaDon => Map.ContainsKey(TruongHoaDon.MaHoaDon);

        public bool CoCotSoLuong => Map.ContainsKey(TruongHoaDon.SoLuong)
                                    || Map.ContainsKey(TruongHoaDon.DonGia);

        public bool CoCotTien => Map.ContainsKey(TruongHoaDon.TongTien)
                                 || Map.ContainsKey(TruongHoaDon.DaThanhToan)
                                 || Map.ContainsKey(TruongHoaDon.ConNo)
                                 || Map.ContainsKey(TruongHoaDon.TrangThai);

        public bool CoCotKhachHang => Map.ContainsKey(TruongHoaDon.MaKhachHang)
                                      || Map.ContainsKey(TruongHoaDon.TenKhachHang)
                                      || Map.ContainsKey(TruongHoaDon.SoDienThoai);

        public bool CoCotNgay => Map.ContainsKey(TruongHoaDon.ThoiGian);
    }

    /// <summary>Tất cả dòng của cùng một mã hóa đơn đã được gom lại.</summary>
    private sealed class HoaDonGom
    {
        public required string MaHoaDon { get; init; }

        public int SoDongDau { get; init; }

        /// <summary>Dòng chứa thông tin hóa đơn, null nếu file không có cột thông tin nào.</summary>
        public DongFile? Header { get; set; }

        /// <summary>Các dòng tiêu đề lặp lại mã hóa đơn đã có, chỉ giữ dòng đầu tiên.</summary>
        public List<int> DongTieuDeTrung { get; } = [];

        public List<DongFile> ChiTiet { get; } = [];
    }

    /// <summary>Kết quả đọc file thô, chưa đối chiếu với database.</summary>
    private sealed class NoiDungFile
    {
        public List<HoaDonGom> Nhom { get; init; } = [];

        public List<HoaDonImportError> LoiDoc { get; init; } = [];

        public List<string> CanhBao { get; init; } = [];

        public int TongSoDongChiTiet { get; init; }
    }

    private static NoiDungFile DocNoiDungFile(string filePath, MappingHoaDon mapping, CancellationToken ct)
    {
        var duLieu = new NoiDungFile();
        var extension = Path.GetExtension(filePath).Trim().ToLowerInvariant();

        List<(TieuDeSheet TieuDe, List<DongFile> Dong)> sheets;

        if (extension == ".csv")
        {
            var sheet = mapping.SheetsDung.FirstOrDefault();

            if (sheet is null)
                throw new InvalidOperationException("File CSV chưa được gán cột mã hóa đơn.");

            sheets = [DocCsv(sheet, filePath, duLieu.CanhBao)];
        }
        else if (extension == ".xlsx" || extension == ".xlsm")
        {
            sheets = DocExcel(filePath, mapping, ct, duLieu.CanhBao);
        }
        else
        {
            throw new NotSupportedException(
                $"Định dạng \"{(extension.Length == 0 ? filePath : extension)}\" không được hỗ trợ. "
                + "Chỉ nhận file .xlsx (Excel) hoặc .csv.");
        }

        if (sheets.Count == 0)
        {
            throw new InvalidOperationException(
                "File không có bảng nào chứa cột \"Mã hóa đơn\". "
                + "Hãy mở lại màn hình ánh xạ cột và chọn sheet cùng cột mã hóa đơn.");
        }

        // File chỉ có một bảng chi tiết mà không có bảng hóa đơn riêng thì coi
        // như bảng gộp: khách hàng để trống, tổng tiền tính từ chi tiết.
        if (!sheets.Any(s => s.TieuDe.Loai == LoaiSheetHoaDon.HoaDon))
        {
            foreach (var sheet in sheets)
            {
                if (sheet.TieuDe.Loai != LoaiSheetHoaDon.ChiTiet)
                    continue;

                sheet.TieuDe.Loai = LoaiSheetHoaDon.Gop;
            }

            duLieu.CanhBao.Add(
                "File không có bảng thông tin hóa đơn riêng (khách hàng, ngày, tổng tiền). "
                + "Phần mềm sẽ coi các hóa đơn là bán lẻ và tính tổng tiền từ dòng chi tiết.");
        }

        return GomNhom(sheets, duLieu);
    }

    private static NoiDungFile GomNhom(
        List<(TieuDeSheet TieuDe, List<DongFile> Dong)> sheets,
        NoiDungFile duLieu)
    {
        var nhom = new Dictionary<string, HoaDonGom>(StringComparer.OrdinalIgnoreCase);
        var thuTu = new List<HoaDonGom>();
        var loi = new List<HoaDonImportError>(duLieu.LoiDoc);

        HoaDonGom LayNhom(string maHoaDon, int soDong)
        {
            if (!nhom.TryGetValue(maHoaDon, out var group))
            {
                group = new HoaDonGom { MaHoaDon = maHoaDon, SoDongDau = soDong };
                nhom[maHoaDon] = group;
                thuTu.Add(group);
            }

            return group;
        }

        // Lượt 1: gom dòng thông tin hóa đơn. Bảng chi tiết được xử lý sau để
        // không phụ thuộc thứ tự các sheet trong file.
        foreach (var (tieuDe, dong) in sheets)
        {
            if (tieuDe.Loai == LoaiSheetHoaDon.ChiTiet)
                continue;

            foreach (var row in dong)
            {
                var ma = LayGiaTri(row, TruongHoaDon.MaHoaDon) ?? "";

                if (ma.Length == 0)
                {
                    loi.Add(new HoaDonImportError(
                        row.SoDong,
                        null,
                        $"Sheet \"{tieuDe.TenSheet}\": dòng không có mã hóa đơn nên bị bỏ qua."));
                    continue;
                }

                var group = LayNhom(ma, row.SoDong);

                // Trong bảng gộp, mã hóa đơn lặp là chuyện bình thường vì mỗi
                // dòng là một dòng chi tiết. Chỉ bảng hóa đơn riêng mới báo lặp.
                if (tieuDe.Loai == LoaiSheetHoaDon.HoaDon && group.Header is not null)
                    group.DongTieuDeTrung.Add(row.SoDong);
                else
                    group.Header ??= row;
            }
        }

        // Lượt 2: gắn dòng chi tiết vào hóa đơn đã có mã tương ứng.
        var tongSoDongChiTiet = 0;

        foreach (var (tieuDe, dong) in sheets)
        {
            if (tieuDe.Loai == LoaiSheetHoaDon.HoaDon)
                continue;

            foreach (var row in dong)
            {
                tongSoDongChiTiet++;
                var ma = LayGiaTri(row, TruongHoaDon.MaHoaDon) ?? "";

                if (ma.Length == 0)
                {
                    if (tieuDe.Loai == LoaiSheetHoaDon.ChiTiet)
                    {
                        loi.Add(new HoaDonImportError(
                            row.SoDong,
                            null,
                            $"Sheet \"{tieuDe.TenSheet}\": dòng chi tiết không có mã hóa đơn nên bị bỏ qua."));
                    }

                    continue;
                }

                if (!nhom.TryGetValue(ma, out var group))
                {
                    loi.Add(new HoaDonImportError(
                        row.SoDong,
                        ma,
                        "Dòng chi tiết không khớp hóa đơn nào trong file."));
                    continue;
                }

                group.ChiTiet.Add(row);
            }
        }

        return new NoiDungFile
        {
            Nhom = thuTu,
            LoiDoc = loi,
            CanhBao = duLieu.CanhBao,
            TongSoDongChiTiet = tongSoDongChiTiet
        };
    }

    private static List<(TieuDeSheet TieuDe, List<DongFile> Dong)> DocExcel(
        string filePath,
        MappingHoaDon mapping,
        CancellationToken ct,
        List<string> canhBao)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheets = new List<(TieuDeSheet, List<DongFile>)>();

        foreach (var worksheet in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();

            var daChon = mapping.SheetsDung.FirstOrDefault(s => s.TenSheet == worksheet.Name);

            if (daChon is null)
            {
                if (mapping.Sheets.Any(s => s.TenSheet == worksheet.Name))
                    canhBao.Add($"Sheet \"{worksheet.Name}\" đã được bỏ qua theo lựa chọn của bạn.");
                else
                    canhBao.Add($"Sheet \"{worksheet.Name}\" không có dòng tiêu đề nên bị bỏ qua.");

                continue;
            }

            var tieuDe = new TieuDeSheet
            {
                TenSheet = worksheet.Name,
                DongTieuDe = daChon.DongTieuDe,
                CotDau = daChon.CotDau,
                Map = ChuyenMapSangAm(daChon),
                Loai = daChon.Loai
            };

            var dong = DocDongSheet(worksheet, tieuDe, ct);

            if (dong.Count == 0)
                canhBao.Add($"Sheet \"{worksheet.Name}\" có tiêu đề nhưng không có dòng dữ liệu nào.");

            sheets.Add((tieuDe, dong));
        }

        return sheets;
    }

    /// <summary>
    /// Lấy phần ánh xạ cột của sheet sau khi người dùng chỉnh sửa, bỏ trường
    /// nào trỏ ra ngoài vùng dữ liệu để tránh đọc nhầm cột khác.
    /// </summary>
    private static Dictionary<TruongHoaDon, int> ChuyenMapSangAm(SheetHoaDon sheet)
    {
        var map = new Dictionary<TruongHoaDon, int>();

        foreach (var (truong, viTri) in sheet.Map)
        {
            if (viTri < 0 || viTri >= sheet.Cot.Count)
                continue;

            map[truong] = viTri;
        }

        return map;
    }

    private static (TieuDeSheet TieuDe, List<DongFile> Dong) DocCsv(
        SheetHoaDon sheet,
        string filePath,
        List<string> canhBao)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        var noiDung = reader.ReadToEnd();
        var dong = TachDongCsv(noiDung);

        if (dong.Count == 0)
            throw new InvalidOperationException("File CSV rỗng hoặc không đọc được nội dung.");

        var tieuDe = new TieuDeSheet
        {
            TenSheet = sheet.TenSheet,
            DongTieuDe = 1,
            CotDau = sheet.CotDau,
            Map = ChuyenMapSangAm(sheet),
            Loai = sheet.Loai
        };

        if (dong.Count == 1)
            canhBao.Add("File CSV chỉ có dòng tiêu đề, không có dữ liệu hàng hóa nào.");

        var ketQua = new List<DongFile>(dong.Count - 1);

        for (var i = 1; i < dong.Count; i++)
            ketQua.Add(TaoDongFile(tieuDe, i + 1, dong[i]));

        return (tieuDe, ketQua);
    }

    /// <summary>
    /// Tách nội dung CSV thành từng dòng và từng ô. Tự nhận dấu phân cách
    /// (dấu phẩy, chấm phẩy hoặc tab) và hiểu được dấu nháy kép bao quanh
    /// ô chứa dấu phẩi, để không làm sai các cột tiền và tên khách hàng.
    /// </summary>
    private static List<string[]> TachDongCsv(string noiDung)
    {
        var phanCach = '\0';

        foreach (var kyTu in noiDung.Take(2000))
        {
            if (kyTu is ',' or ';' or '\t')
            {
                phanCach = kyTu;
                break;
            }
        }

        if (phanCach == '\0')
            phanCach = ',';

        var dong = new List<string[]>();
        var o = new List<string>();
        var chuoi = new StringBuilder();
        var trongNhay = false;

        for (var i = 0; i < noiDung.Length; i++)
        {
            var kyTu = noiDung[i];

            if (trongNhay)
            {
                if (kyTu == '"')
                {
                    if (i + 1 < noiDung.Length && noiDung[i + 1] == '"')
                    {
                        chuoi.Append('"');
                        i++;
                        continue;
                    }

                    trongNhay = false;
                    continue;
                }

                chuoi.Append(kyTu);
                continue;
            }

            switch (kyTu)
            {
                case '"':
                    trongNhay = true;
                    continue;
                case '\r':
                    continue;
                case '\n':
                    o.Add(chuoi.ToString());
                    dong.Add([.. o]);
                    o.Clear();
                    chuoi.Clear();
                    continue;
                default:
                    if (kyTu == phanCach)
                    {
                        o.Add(chuoi.ToString());
                        chuoi.Clear();
                        continue;
                    }

                    chuoi.Append(kyTu);
                    continue;
            }
        }

        if (chuoi.Length > 0 || o.Count > 0)
        {
            o.Add(chuoi.ToString());
            dong.Add([.. o]);
        }

        return dong
            .Where(r => r.Any(v => v.Trim().Length > 0))
            .ToList();
    }

    /// <summary>
    /// Tìm dòng tiêu đề và tự gán cột cho một sheet Excel. Sheet không tìm được
    /// cột mã hóa đơn vẫn được trả về để người dùng tự gán tay.
    /// </summary>
    private static SheetHoaDon DocTieuDeSheet(IXLWorksheet sheet)
    {
        var dongCuoi = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var dongTieuDe = 0;
        var cotDau = 1;
        List<CotFileHoaDon> cot = [];

        // Dòng tiêu đề có thể nằm sau vài dòng tiêu đề báo cáo nên thử 30 dòng đầu.
        for (var row = 1; row <= Math.Min(dongCuoi, 30); row++)
        {
            var oDong = sheet.Row(row);
            var cotThu = DocCotTieuDe(oDong);

            if (cotThu.Count == 0 || !TuDoanCot(cotThu).ContainsKey(TruongHoaDon.MaHoaDon))
                continue;

            dongTieuDe = row;
            cotDau = oDong.FirstCellUsed()?.Address.ColumnNumber ?? 1;
            cot = cotThu;
            break;
        }

        if (cot.Count == 0)
        {
            // Chưa tìm thấy dòng tiêu đề: vẫn lấy dòng đầu có dữ liệu để
            // người dùng xem và tự gán cột thay vì không thấy sheet này.
            for (var row = 1; row <= Math.Min(dongCuoi, 30); row++)
            {
                var oDong = sheet.Row(row);

                if (oDong.IsEmpty())
                    continue;

                dongTieuDe = row;
                cotDau = oDong.FirstCellUsed()?.Address.ColumnNumber ?? 1;
                cot = DocCotTieuDe(oDong);
                break;
            }
        }

        if (cot.Count == 0)
        {
            cot =
            [
                new CotFileHoaDon
                {
                    ViTri = 0,
                    Ten = sheet.Name
                }
            ];
            cotDau = 1;
        }

        var sheetHoaDon = new SheetHoaDon
        {
            TenSheet = sheet.Name,
            DongTieuDe = dongTieuDe == 0 ? 1 : dongTieuDe,
            CotDau = cotDau,
            Cot = cot,
            Map = TuDoanCot(cot)
        };

        return sheetHoaDon;
    }

    private static List<CotFileHoaDon> DocCotTieuDe(IXLRow row)
    {
        var cotDau = row.FirstCellUsed()?.Address.ColumnNumber ?? 0;
        var cotCuoi = row.LastCellUsed()?.Address.ColumnNumber ?? 0;

        if (cotCuoi < cotDau || cotCuoi == 0)
            return [];

        var cot = new List<CotFileHoaDon>(cotCuoi - cotDau + 1);

        for (var c = cotDau; c <= cotCuoi; c++)
        {
            cot.Add(new CotFileHoaDon
            {
                ViTri = c - cotDau,
                Ten = row.Cell(c).GetString().Trim()
            });
        }

        return cot;
    }

    /// <summary>
    /// Tự gán cột cho từng trường bằng cách so tên cột đã chuẩn hóa với danh
    /// sách bí danh. Mỗi trường chỉ nhận đúng một cột, và khi có nhiều cột
    /// cùng nghĩa thì lấy cột bên trái.
    /// </summary>
    private static Dictionary<TruongHoaDon, int> TuDoanCot(List<CotFileHoaDon> cot)
    {
        var map = new Dictionary<TruongHoaDon, int>();

        foreach (var (truong, khoa) in BangDoanCot)
        {
            foreach (var c in cot)
            {
                if (khoa.Contains(KhoaCot(c.Ten)))
                {
                    map[truong] = c.ViTri;
                    break;
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Đoán sheet nào là danh sách hóa đơn, sheet nào là danh sách chi tiết.
    /// Ưu tiên tên sheet, sau đó mới dựa vào cột có trong sheet.
    /// </summary>
    private static LoaiSheetHoaDon DoanLoaiSheet(SheetHoaDon sheet)
    {
        var ten = DatabaseService.NormalizeHeaderKey(sheet.TenSheet);
        var coSoLuong = sheet.Map.ContainsKey(TruongHoaDon.SoLuong)
                        || sheet.Map.ContainsKey(TruongHoaDon.DonGia);
        var coMaHoaDon = sheet.Map.ContainsKey(TruongHoaDon.MaHoaDon);

        if (ten.Contains("chitiet") || ten.Contains("detail"))
            return LoaiSheetHoaDon.ChiTiet;

        if (ten.Contains("hoadon") || ten.Contains("donhang") || ten.Contains("invoice"))
            return coSoLuong ? LoaiSheetHoaDon.Gop : LoaiSheetHoaDon.HoaDon;

        if (!coSoLuong)
            return LoaiSheetHoaDon.HoaDon;

        // Sheet không có cột mã hóa đơn thì chưa biết là chi tiết hay gộp,
        // để mặc định chi tiết vì đây là dạng phổ biến và dễ sửa tay.
        if (!coMaHoaDon)
            return LoaiSheetHoaDon.ChiTiet;

        return sheet.Map.ContainsKey(TruongHoaDon.TongTien)
               || sheet.Map.ContainsKey(TruongHoaDon.MaKhachHang)
               || sheet.Map.ContainsKey(TruongHoaDon.TenKhachHang)
               || sheet.Map.ContainsKey(TruongHoaDon.SoDienThoai)
               || sheet.Map.ContainsKey(TruongHoaDon.ThoiGian)
            ? LoaiSheetHoaDon.Gop
            : LoaiSheetHoaDon.ChiTiet;
    }

    private static List<DongFile> DocDongSheet(
        IXLWorksheet sheet,
        TieuDeSheet tieuDe,
        CancellationToken ct)
    {
        var dongCuoi = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var ketQua = new List<DongFile>();

        for (var row = tieuDe.DongTieuDe + 1; row <= dongCuoi; row++)
        {
            if ((row & 255) == 0)
                ct.ThrowIfCancellationRequested();

            var oDong = sheet.Row(row);
            var giaTri = new Dictionary<TruongHoaDon, string>();
            var coGiaTri = false;

            foreach (var (truong, index) in tieuDe.Map)
            {
                var giaTriO = DatabaseService.DocGiaTriOTheoKieu(
                    oDong.Cell(tieuDe.CotDau + index));

                if (giaTriO.Length == 0)
                    continue;

                coGiaTri = true;
                giaTri[truong] = giaTriO;
            }

            if (!coGiaTri)
                continue;

            ketQua.Add(new DongFile { SoDong = row, GiaTri = giaTri });
        }

        return ketQua;
    }

    private static DongFile TaoDongFile(TieuDeSheet tieuDe, int soDong, string[] giaTri)
    {
        var giaTriTheoTruong = new Dictionary<TruongHoaDon, string>();
        var coGiaTri = false;

        foreach (var (truong, index) in tieuDe.Map)
        {
            var giaTriO = index < giaTri.Length ? giaTri[index].Trim() : "";

            if (giaTriO.Length == 0)
                continue;

            coGiaTri = true;
            giaTriTheoTruong[truong] = giaTriO;
        }

        return new DongFile
        {
            SoDong = soDong,
            GiaTri = coGiaTri ? giaTriTheoTruong : []
        };
    }

    private static string? LayGiaTri(DongFile row, TruongHoaDon truong) =>
        row.GiaTri.TryGetValue(truong, out var giaTri) && giaTri.Trim().Length > 0
            ? giaTri.Trim()
            : null;

    // ============================================================
    // KIỂM TRA TOÀN BỘ DỮ LIỆU TRƯỚC KHI GHI
    // ============================================================

    private async Task<HoaDonImportPreview> KiemTraAsync(
        string filePath,
        NoiDungFile duLieu,
        IProgress<HoaDonImportProgress>? progress,
        CancellationToken ct)
    {
        var loi = new List<HoaDonImportError>(duLieu.LoiDoc);
        var canhBao = new List<string>(duLieu.CanhBao);
        var danhSach = new List<HoaDonImportRow>(duLieu.Nhom.Count);
        var maKhachCoTheTao = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var soHoaDonMoi = 0;
        var soHoaDonTrung = 0;
        var soKhachHangThieu = 0;
        var soSanPhamThieu = 0;
        var soHoaDonHuy = 0;

        await using var connection = await _db.OpenAsync();

        var khachHang = await TaiKhachHangAsync(connection, ct);
        var sanPham = await TaiSanPhamAsync(connection, ct);
        var maHoaDonTonTai = await TaiMaHoaDonTonTaiAsync(
            connection,
            duLieu.Nhom.Select(h => h.MaHoaDon).ToList(),
            ct);

        var index = 0;

        // Chốt một mốc thời gian cho cả lượt: hóa đơn không có cột ngày sẽ
        // dùng chung mốc này thay vì gọi DateTime.Now ở từng dòng.
        var thoiGianMacDinh = DateTime.Now;

        foreach (var group in duLieu.Nhom)
        {
            ct.ThrowIfCancellationRequested();
            index++;

            if (group.MaHoaDon.Length > DoDaiToiDaMaHoaDon)
            {
                loi.Add(new HoaDonImportError(
                    group.SoDongDau,
                    group.MaHoaDon,
                    $"Mã hóa đơn dài {group.MaHoaDon.Length} ký tự, "
                    + $"vượt quá {DoDaiToiDaMaHoaDon} ký tự mà database cho phép."));

                continue;
            }

            if (progress is not null && index % 200 == 0)
            {
                progress.Report(new HoaDonImportProgress
                {
                    GiaiDoan = "Đang kiểm tra dữ liệu...",
                    DaXuLy = index,
                    TongSo = duLieu.Nhom.Count
                });
            }

            foreach (var soDong in group.DongTieuDeTrung)
            {
                loi.Add(new HoaDonImportError(
                    soDong,
                    group.MaHoaDon,
                    $"Mã hóa đơn bị lặp trong file, chỉ giữ dòng đầu tiên (dòng {group.SoDongDau})."));
            }

            var (hoaDon, loiHoaDon) = DocHoaDon(group, canhBao, thoiGianMacDinh);

            if (hoaDon is null)
            {
                foreach (var lyDo in loiHoaDon)
                    loi.Add(new HoaDonImportError(group.SoDongDau, group.MaHoaDon, lyDo));

                continue;
            }

            hoaDon.LaHoaDonTrung = maHoaDonTonTai.Contains(group.MaHoaDon);

            if (hoaDon.LaHoaDonTrung)
                soHoaDonTrung++;
            else
                soHoaDonMoi++;

            if (hoaDon.DaHuyTrongFile)
                soHoaDonHuy++;

            if (group.ChiTiet.Count == 0)
            {
                loi.Add(new HoaDonImportError(
                    group.SoDongDau,
                    group.MaHoaDon,
                    "Không có dòng chi tiết hàng hóa nào khớp mã hóa đơn này."));
                continue;
            }

            var ketQuaChiTiet = DocChiTiet(group, sanPham, ct);

            if (ketQuaChiTiet.Loi.Count > 0)
            {
                if (ketQuaChiTiet.CoThieuSanPham)
                    soSanPhamThieu++;

                // Giữ lại toàn bộ lỗi từng dòng để file xuất lỗi có đủ số liệu,
                // không cắt bớt ở đây.
                loi.AddRange(ketQuaChiTiet.Loi);

                continue;
            }

            hoaDon.ChiTiet.AddRange(ketQuaChiTiet.ChiTiet);
            CanhBaoTienLlech(hoaDon, canhBao);

            hoaDon.TrangThaiKhachHang = ResolveKhachHang(hoaDon, khachHang);

            if (hoaDon.TrangThaiKhachHang == TrangThaiKhachHangHoaDon.ChuaCoTrongDatabase)
            {
                soKhachHangThieu++;

                var ma = (hoaDon.MaKhachHang ?? "").Trim();
                var ten = (hoaDon.TenKhachHang ?? "").Trim();

                // Chỉ tạo được khách mới khi có cả mã và tên: phần mềm không
                // tự sinh mã khách hàng để tránh trùng với khách đang có.
                if (ma.Length > 0 && ma.Length <= DoDaiToiDaMaKhachHang && ten.Length > 0)
                    maKhachCoTheTao.Add(ma);
            }

            danhSach.Add(hoaDon);
        }

        if (danhSach.Count == 0 && loi.Count > 0)
            canhBao.Add("Không có hóa đơn nào đọc được từ file.");

        if (soKhachHangThieu > maKhachCoTheTao.Count)
        {
            canhBao.Add(
                $"{soKhachHangThieu:N0} hóa đơn không tìm thấy khách hàng, "
                + $"trong đó {maKhachCoTheTao.Count:N0} hóa đơn có đủ mã và tên để tạo mới. "
                + "Hóa đơn còn lại sẽ bị bỏ qua vì thiếu mã hoặc thiếu tên khách hàng.");
        }

        return new HoaDonImportPreview
        {
            TenFile = Path.GetFileName(filePath),
            TongSoHoaDon = duLieu.Nhom.Count,
            TongSoDongChiTiet = duLieu.TongSoDongChiTiet,
            SoHoaDonMoi = soHoaDonMoi,
            SoHoaDonTrung = soHoaDonTrung,
            SoHoaDonHopLe = danhSach.Count,
            SoKhachHangKhongTonTai = soKhachHangThieu,
            SoKhachHangCoTheTaoMoi = maKhachCoTheTao.Count,
            SoSanPhamKhongTonTai = soSanPhamThieu,
            SoHoaDonHuy = soHoaDonHuy,
            SoLoi = loi.Count,
            DanhSachHoaDon = danhSach,
            MaHoaDonTonTai = maHoaDonTonTai,
            DanhSachLoi = loi,
            CanhBao = canhBao
        };
    }

    /// <summary>Độ dài tối đa của cột ma_khach_hang trong database.</summary>
    private const int DoDaiToiDaMaKhachHang = 30;

    /// <summary>
    /// Báo cho người dùng khi tổng tiền hoặc cột "còn nợ" trong file lệch với
    /// số tiền phần mềm tính. Chỉ cảnh báo, vẫn ghi theo cách tính đúng.
    /// </summary>
    private static void CanhBaoTienLlech(HoaDonImportRow hoaDon, List<string> canhBao)
    {
        if (hoaDon.TongTienRaw is { } tongTienFile
            && Math.Abs(tongTienFile - hoaDon.TongTienHieuDung) > 1m)
        {
            ThemCanhBao(canhBao,
                $"Hóa đơn {hoaDon.MaHoaDon}: tổng tiền trong file ({tongTienFile:N0}) lệch với tổng tiền các dòng chi tiết "
                + $"({hoaDon.TongChiTietTinhToi:N0}). Phần mềm giữ theo file.");
        }

        if (hoaDon.ConNoRaw is { } conNoFile
            && Math.Abs(conNoFile - hoaDon.ConNoChuan) > 1m)
        {
            ThemCanhBao(canhBao,
                $"Hóa đơn {hoaDon.MaHoaDon}: cột \"Còn nợ\" trong file ({conNoFile:N0}) lệch với số tiền phần mềm tính "
                + $"({hoaDon.ConNoChuan:N0}). Phần mềm dùng số tiền tính từ tổng tiền, giảm giá và tiền đã trả.");
        }

        if (hoaDon.ThuKhacChuan > 0m)
        {
            ThemCanhBao(canhBao,
                $"Có hóa đơn khách trả nhiều hơn số tiền cần trả, phần dư được ghi vào cột \"Thu khác\".");
        }
    }

    /// <summary>
    /// Đọc phần thông tin hóa đơn. Trả về null kèm danh sách lý do nếu dữ liệu
    /// không dùng được.
    /// </summary>
    private static (HoaDonImportRow? HoaDon, List<string> Loi) DocHoaDon(
        HoaDonGom group,
        List<string> canhBao,
        DateTime thoiGianMacDinh)
    {
        var loi = new List<string>();
        var header = group.Header;

        var rawThoiGian = header is null ? null : LayGiaTri(header, TruongHoaDon.ThoiGian);
        DateTime? thoiGian = null;

        if (!string.IsNullOrWhiteSpace(rawThoiGian))
        {
            thoiGian = DatabaseService.ParseNgayTaiFile(rawThoiGian);

            if (thoiGian is null)
                loi.Add($"Ngày không hợp lệ: \"{rawThoiGian}\".");
        }
        else
        {
            ThemCanhBao(canhBao, "File không có cột thời gian, hóa đơn sẽ nhận thời gian lúc nhập.");
        }

        var rawTongTien = header is null ? null : LayGiaTri(header, TruongHoaDon.TongTien);
        decimal? tongTien = null;

        if (!string.IsNullOrWhiteSpace(rawTongTien))
        {
            tongTien = DatabaseService.ParseSoTien(rawTongTien);

            if (tongTien is null)
                loi.Add($"Tổng tiền không phải là số hợp lệ: \"{rawTongTien}\".");
            else if (tongTien < 0m)
                loi.Add($"Tổng tiền bị âm: \"{rawTongTien}\".");
        }
        else
        {
            ThemCanhBao(canhBao, "File không có cột tổng tiền, tổng tiền sẽ tính từ các dòng chi tiết.");
        }

        var rawDaTra = header is null ? null : LayGiaTri(header, TruongHoaDon.DaThanhToan);
        decimal? daThanhToan = null;

        if (!string.IsNullOrWhiteSpace(rawDaTra))
        {
            daThanhToan = DatabaseService.ParseSoTien(rawDaTra);

            if (daThanhToan is null)
                loi.Add($"Số tiền khách đã trả không phải là số hợp lệ: \"{rawDaTra}\".");
            else if (daThanhToan < 0m)
                loi.Add($"Số tiền khách đã trả bị âm: \"{rawDaTra}\".");
        }

        var rawGiamGia = header is null ? null : LayGiaTri(header, TruongHoaDon.GiamGia);
        decimal giamGia = 0m;

        if (!string.IsNullOrWhiteSpace(rawGiamGia))
        {
            var gia = DatabaseService.ParseSoTien(rawGiamGia);

            if (gia is null)
                loi.Add($"Giảm giá không phải là số hợp lệ: \"{rawGiamGia}\".");
            else if (gia < 0m)
                loi.Add($"Giảm giá bị âm: \"{rawGiamGia}\".");
            else
                giamGia = gia.Value;
        }

        var rawConNo = header is null ? null : LayGiaTri(header, TruongHoaDon.ConNo);
        decimal? conNo = null;

        if (!string.IsNullOrWhiteSpace(rawConNo))
        {
            conNo = DatabaseService.ParseSoTien(rawConNo);

            if (conNo is null)
                loi.Add($"Cột \"Còn nợ\" không phải là số hợp lệ: \"{rawConNo}\".");
        }

        if (loi.Count > 0)
            return (null, loi);

        var rawTrangThai = header is null ? null : LayGiaTri(header, TruongHoaDon.TrangThai);
        var daHuy = rawTrangThai is not null
                    && TrangThaiDaHuy.Contains(KhoaCot(rawTrangThai));

        return (new HoaDonImportRow
        {
            SoDongExcel = group.SoDongDau,
            MaHoaDon = group.MaHoaDon,
            ThoiGianRaw = thoiGian,
            ThoiGianMacDinh = thoiGianMacDinh,
            MaKhachHang = header is null ? null : LayGiaTri(header, TruongHoaDon.MaKhachHang),
            TenKhachHang = header is null ? null : LayGiaTri(header, TruongHoaDon.TenKhachHang),
            SoDienThoai = header is null ? null : LayGiaTri(header, TruongHoaDon.SoDienThoai),
            TongTienRaw = tongTien,
            GiamGia = giamGia,
            KhachDaThanhToanRaw = daThanhToan,
            ConNoRaw = conNo,
            TrangThaiRaw = rawTrangThai,
            DaHuyTrongFile = daHuy,
            GhiChu = header is null ? null : LayGiaTri(header, TruongHoaDon.GhiChu)
        }, loi);
    }

    private static void ThemCanhBao(List<string> canhBao, string noiDung)
    {
        if (canhBao.Count >= SoCanhBaoToiDa || canhBao.Contains(noiDung))
            return;

        canhBao.Add(noiDung);
    }

    private sealed class KetQuaDocChiTiet
    {
        public List<HoaDonImportDetailRow> ChiTiet { get; init; } = [];

        public List<HoaDonImportError> Loi { get; init; } = [];

        public int SoDongLoi { get; init; }

        public bool CoThieuSanPham { get; init; }
    }

    private static KetQuaDocChiTiet DocChiTiet(
        HoaDonGom group,
        Dictionary<string, HoaDonImportSanPham> sanPham,
        CancellationToken ct)
    {
        var chiTiet = new List<HoaDonImportDetailRow>(group.ChiTiet.Count);
        var loi = new List<HoaDonImportError>();
        var soDongLoi = 0;
        var thieuSanPham = false;

        foreach (var row in group.ChiTiet)
        {
            ct.ThrowIfCancellationRequested();

            var maSanPham = LayGiaTri(row, TruongHoaDon.MaSanPham) ?? "";

            if (maSanPham.Length == 0)
            {
                GhiLoi(row.SoDong, "thiếu mã hàng hóa.");
                continue;
            }

            if (maSanPham.Length > DoDaiToiDaMa)
            {
                GhiLoi(row.SoDong,
                    $"mã hàng hóa dài {maSanPham.Length} ký tự, vượt quá {DoDaiToiDaMa} ký tự mà database cho phép.");
                continue;
            }

            // Phân biệt "ô trống" với "ô có chữ không đọc được thành số": ô trống
            // thì dùng giá trị mặc định, ô có chữ sai thì báo lỗi cho dòng này.
            var rawSoLuong = LayGiaTri(row, TruongHoaDon.SoLuong);
            var rawDonGia = LayGiaTri(row, TruongHoaDon.DonGia);
            var rawGiamGia = LayGiaTri(row, TruongHoaDon.GiamGia);
            var rawThanhTien = LayGiaTri(row, TruongHoaDon.ThanhTien);

            if (CoChuNhungKhongDocDuocSo(rawSoLuong))
            {
                GhiLoi(row.SoDong, $"số lượng không đọc được thành số (\"{rawSoLuong}\").");
                continue;
            }

            if (CoChuNhungKhongDocDuocSo(rawDonGia))
            {
                GhiLoi(row.SoDong, $"đơn giá không đọc được thành số (\"{rawDonGia}\").");
                continue;
            }

            if (CoChuNhungKhongDocDuocSo(rawGiamGia))
            {
                GhiLoi(row.SoDong, $"giảm giá của dòng không đọc được thành số (\"{rawGiamGia}\").");
                continue;
            }

            if (CoChuNhungKhongDocDuocSo(rawThanhTien))
            {
                GhiLoi(row.SoDong, $"thành tiền của dòng không đọc được thành số (\"{rawThanhTien}\").");
                continue;
            }

            var soLuong = DatabaseService.ParseSoTien(rawSoLuong);
            var donGia = DatabaseService.ParseSoTien(rawDonGia);
            var giamGiaDong = DatabaseService.ParseSoTien(rawGiamGia) ?? 0m;
            var thanhTien = DatabaseService.ParseSoTien(rawThanhTien) ?? 0m;

            if (soLuong is null || soLuong <= 0m)
            {
                GhiLoi(row.SoDong, $"số lượng phải lớn hơn 0 (đang là \"{rawSoLuong ?? "(trống)"}\").");
                continue;
            }

            if (donGia is null || donGia < 0m)
            {
                GhiLoi(row.SoDong, $"đơn giá không hợp lệ (\"{rawDonGia ?? "(trống)"}\").");
                continue;
            }

            if (giamGiaDong < 0m)
            {
                GhiLoi(row.SoDong, "giảm giá của dòng bị âm.");
                continue;
            }

            if (thanhTien < 0m)
            {
                GhiLoi(row.SoDong, "thành tiền của dòng bị âm.");
                continue;
            }

            if (!sanPham.TryGetValue(maSanPham, out var sanPhamDaCo))
            {
                thieuSanPham = true;
                GhiLoi(row.SoDong, $"không tìm thấy hàng hóa có mã \"{maSanPham}\" trong database.");
                continue;
            }

            var tenSanPham = LayGiaTri(row, TruongHoaDon.TenSanPham) ?? "";

            if (tenSanPham.Length > DoDaiToiDaTenSanPham)
                tenSanPham = tenSanPham[..DoDaiToiDaTenSanPham];

            chiTiet.Add(new HoaDonImportDetailRow
            {
                SoDongExcel = row.SoDong,
                MaHoaDon = group.MaHoaDon,
                MaSanPham = maSanPham,
                TenSanPhamTrongFile = tenSanPham,
                SoLuong = soLuong.Value,
                DonGia = donGia.Value,
                GiamGia = giamGiaDong,
                ThanhTien = thanhTien,
                GhiChu = LayGiaTri(row, TruongHoaDon.GhiChu),
                SanPham = sanPhamDaCo
            });
        }

        return new KetQuaDocChiTiet
        {
            ChiTiet = chiTiet,
            Loi = loi,
            SoDongLoi = soDongLoi,
            CoThieuSanPham = thieuSanPham
        };

        bool CoChuNhungKhongDocDuocSo(string? raw) =>
            raw is { Length: > 0 } && DatabaseService.ParseSoTien(raw) is null;

        void GhiLoi(int soDong, string lyDo)
        {
            soDongLoi++;
            loi.Add(new HoaDonImportError(soDong, group.MaHoaDon, lyDo));
        }
    }

    // ============================================================
    // TRA CỨU DANH MỤC
    // ============================================================

    private sealed class DanhSachKhachHang
    {
        public Dictionary<string, uint> TheoMa { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, uint> TheoDienThoai { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, uint> TheoTen { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nạp khách hàng đang có để đối chiếu mã, điện thoại và tên.
    /// Chỉ lấy ba cột cần đối chiếu, không tải hết thông tin khách hàng.
    /// </summary>
    private static async Task<DanhSachKhachHang> TaiKhachHangAsync(
        MySqlConnection connection,
        CancellationToken ct)
    {
        var ketQua = new DanhSachKhachHang();

        var rows = (await connection.QueryAsync<(uint Id, string? Ma, string? Ten, string? Sdt)>(
            """
            SELECT
                id AS Id,
                ma_khach_hang AS Ma,
                ten_khach_hang AS Ten,
                so_dien_thoai AS Sdt
            FROM khach_hang;
            """)).AsList();

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(row.Ma))
                ketQua.TheoMa.TryAdd(row.Ma.Trim(), row.Id);

            if (!string.IsNullOrWhiteSpace(row.Sdt))
                ketQua.TheoDienThoai.TryAdd(row.Sdt.Trim(), row.Id);

            if (!string.IsNullOrWhiteSpace(row.Ten))
                ketQua.TheoTen.TryAdd(DatabaseService.NormalizeHeaderKey(row.Ten), row.Id);
        }

        return ketQua;
    }

    private static async Task<Dictionary<string, HoaDonImportSanPham>> TaiSanPhamAsync(
        MySqlConnection connection,
        CancellationToken ct)
    {
        var ketQua = new Dictionary<string, HoaDonImportSanPham>(StringComparer.OrdinalIgnoreCase);

        var rows = (await connection.QueryAsync<(uint Id, string Ma, string Ten, string? Dvt, decimal GiaVon)>(
            """
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS Ma,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS Ten,
                dvt.ten_don_vi AS Dvt,
                sp.gia_von AS GiaVon
            FROM san_pham sp
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id;
            """)).AsList();

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            var ma = row.Ma?.Trim();

            if (string.IsNullOrWhiteSpace(ma))
                continue;

            ketQua.TryAdd(ma, new HoaDonImportSanPham
            {
                Id = row.Id,
                TenSanPham = row.Ten,
                DonViTinh = row.Dvt,
                GiaVon = row.GiaVon
            });
        }

        return ketQua;
    }

    /// <summary>
    /// Lấy danh sách mã hóa đơn đã có trong database. Chỉ hỏi đúng những mã có
    /// trong file, không tải cả bảng hóa đơn.
    /// </summary>
    private static async Task<HashSet<string>> TaiMaHoaDonTonTaiAsync(
        MySqlConnection connection,
        List<string> maTrongFile,
        CancellationToken ct)
    {
        var ketQua = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (maTrongFile.Count == 0)
            return ketQua;

        foreach (var chunk in Chunk(maTrongFile, 1000))
        {
            ct.ThrowIfCancellationRequested();

            var parameters = new DynamicParameters();
            var dieuKien = new List<string>(chunk.Count);

            for (var i = 0; i < chunk.Count; i++)
            {
                parameters.Add("Ma" + i, chunk[i], DbType.String);
                dieuKien.Add("@Ma" + i);
            }

            var sql = $"""
                SELECT ma_hoa_don FROM {BangHoaDon}
                WHERE ma_hoa_don IN ({string.Join(", ", dieuKien)});
                """;

            foreach (var ma in await connection.QueryAsync<string>(sql, parameters))
            {
                if (!string.IsNullOrWhiteSpace(ma))
                    ketQua.Add(ma.Trim());
            }
        }

        return ketQua;
    }

    /// <summary>
    /// Tìm khách hàng theo thứ tự ưu tiên: mã khách hàng, số điện thoại, tên.
    /// Không tự tạo khách hàng mới ở bước kiểm tra.
    /// </summary>
    private static TrangThaiKhachHangHoaDon ResolveKhachHang(
        HoaDonImportRow hoaDon,
        DanhSachKhachHang khachHang)
    {
        var ma = (hoaDon.MaKhachHang ?? "").Trim();
        var ten = (hoaDon.TenKhachHang ?? "").Trim();
        var sdt = (hoaDon.SoDienThoai ?? "").Trim();

        // Không có mã lẫn tên: hóa đơn bán lẻ, để khach_hang_id rỗng.
        if (ma.Length == 0 && ten.Length == 0)
            return TrangThaiKhachHangHoaDon.KhachLe;

        if (ma.Length > 0 && khachHang.TheoMa.TryGetValue(ma, out var idTheoMa))
        {
            hoaDon.IdKhachHang = idTheoMa;
            return TrangThaiKhachHangHoaDon.DaTimThay;
        }

        if (sdt.Length > 0 && khachHang.TheoDienThoai.TryGetValue(sdt, out var idTheoSdt))
        {
            hoaDon.IdKhachHang = idTheoSdt;
            return TrangThaiKhachHangHoaDon.DaTimThay;
        }

        // Tên ghi rõ là khách lẻ thì không cần tạo khách hàng.
        if (ten.Length > 0 && TenKhachLe.Contains(KhoaCot(ten)))
            return TrangThaiKhachHangHoaDon.KhachLe;

        if (ten.Length > 0 && khachHang.TheoTen.TryGetValue(DatabaseService.NormalizeHeaderKey(ten), out var idTheoTen))
        {
            hoaDon.IdKhachHang = idTheoTen;
            return TrangThaiKhachHangHoaDon.DaTimThay;
        }

        return TrangThaiKhachHangHoaDon.ChuaCoTrongDatabase;
    }

    // ============================================================
    // GHI XUỐNG DATABASE
    // ============================================================

    /// <summary>
    /// Ghi hóa đơn đã kiểm tra xuống database: một connection, một transaction,
    /// chia nhiều bát, mỗi bát có savepoint riêng nên một hóa đơn hỏng không
    /// làm mất các hóa đơn đã ghi trước đó.
    /// </summary>
    public async Task<HoaDonImportResult> ImportAsync(
        HoaDonImportPreview preview,
        XuLyHoaDonTrung xuLyTrung,
        bool taoKhachHangMoi,
        IProgress<HoaDonImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var result = new HoaDonImportResult
        {
            TenFile = preview.TenFile,
            TongSoHoaDon = preview.TongSoHoaDon,
            TongSoDongChiTiet = preview.TongSoDongChiTiet,
            SoLoi = preview.SoLoi
        };

        foreach (var loi in preview.DanhSachLoi)
            result.DanhSachLoi.Add(loi);

        if (xuLyTrung == XuLyHoaDonTrung.Huy)
        {
            result.DaHuy = true;
            result.ThoiGian = stopwatch.Elapsed;
            GhiLog(result, "Người dùng hủy import.");
            return result;
        }

        var canGhi = new List<HoaDonImportRow>(preview.DanhSachHoaDon.Count);

        foreach (var hoaDon in preview.DanhSachHoaDon)
        {
            // Hóa đơn bị hủy ngay trong file: không nhập.
            if (hoaDon.DaHuyTrongFile)
            {
                result.SoBoQua++;
                continue;
            }

            // Hóa đơn trùng mà người dùng chọn bỏ qua: giữ nguyên dữ liệu cũ.
            if (hoaDon.LaHoaDonTrung && xuLyTrung == XuLyHoaDonTrung.BoQua)
            {
                result.SoBoQua++;
                continue;
            }

            // Không tìm thấy khách hàng mà người dùng không đồng ý tạo mới.
            if (hoaDon.TrangThaiKhachHang == TrangThaiKhachHangHoaDon.ChuaCoTrongDatabase
                && !taoKhachHangMoi)
            {
                result.SoBoQua++;
                continue;
            }

            canGhi.Add(hoaDon);
        }

        if (canGhi.Count == 0)
        {
            result.ThoiGian = stopwatch.Elapsed;
            GhiLog(result, "Không còn hóa đơn nào để ghi.");
            return result;
        }

        // Sắp theo thời gian để lịch sử công nợ được cộng dồn đúng thứ tự.
        canGhi.Sort((a, b) =>
        {
            var soSanh = a.ThoiGianBan.CompareTo(b.ThoiGianBan);
            return soSanh != 0 ? soSanh : string.CompareOrdinal(a.MaHoaDon, b.MaHoaDon);
        });

        var branchId = Session.CurrentUser?.ChiNhanhId ?? 1u;
        var userId = Session.CurrentUser?.Id;

        await using var connection = await _db.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            progress?.Report(new HoaDonImportProgress
            {
                GiaiDoan = "Đang ghi dữ liệu vào database...",
                TongSo = canGhi.Count
            });

            // Đọc lại danh mục trong transaction: hàng hóa có thể đã bị xóa
            // hoặc sửa sau khi người dùng xem bước xem trước.
            var sanPham = await TaiSanPhamAsync(connection, ct);
            var khongGhiDuoc = new List<HoaDonImportRow>();

            foreach (var hoaDon in canGhi)
            {
                ct.ThrowIfCancellationRequested();

                var thieuSanPham = new List<string>();

                foreach (var dong in hoaDon.ChiTiet)
                {
                    if (!sanPham.TryGetValue(dong.MaSanPham, out var sanPhamMoi))
                    {
                        thieuSanPham.Add(dong.MaSanPham);
                        continue;
                    }

                    // Giá vốn và đơn vị tính lấy theo danh mục lúc ghi.
                    dong.SanPham = sanPhamMoi;
                }

                if (thieuSanPham.Count == 0)
                    continue;

                khongGhiDuoc.Add(hoaDon);
                result.SoLoi++;
                result.DanhSachLoi.Add(new HoaDonImportError(
                    hoaDon.SoDongExcel,
                    hoaDon.MaHoaDon,
                    "Hàng hóa đã bị xóa khỏi danh mục từ lúc xem trước: "
                    + string.Join(", ", thieuSanPham.Distinct().Take(5))));
            }

            canGhi.RemoveAll(khongGhiDuoc.Contains);

            var capNhatHoaDon = canGhi.Where(h => h.LaHoaDonTrung).Select(h => h.MaHoaDon).ToList();

            if (capNhatHoaDon.Count > 0 && xuLyTrung == XuLyHoaDonTrung.CapNhat)
            {
                var soHoaDonLienPhieuTra = await DemHoaDonLienPhieuTraHangAsync(
                    connection,
                    capNhatHoaDon,
                    ct);

                if (soHoaDonLienPhieuTra > 0)
                {
                    result.CanhBao.Add(
                        $"{soHoaDonLienPhieuTra:N0} hóa đơn được cập nhật đang có phiếu trả hàng liên kết. "
                        + "Do database quy định xóa chi tiết sẽ gỡ liên kết đó, "
                        + "nên các phiếu trả hàng vẫn còn nhưng không còn gắn với dòng hàng cũ.");
                }
            }

            var idKhachHang = await GanIdKhachHangAsync(connection, canGhi, taoKhachHangMoi, result, ct);
            canGhi.RemoveAll(h => !idKhachHang.ContainsKey(h.MaHoaDon));

            if (canGhi.Count == 0)
            {
                await transaction.RollbackAsync();
                result.ThoiGian = stopwatch.Elapsed;
                GhiLog(result, "Không còn hóa đơn nào để ghi.");
                return result;
            }

            var soDuCongNo = new SoDuCongNo(await TaiSoDuCongNoAsync(connection, ct));
            var congNoCu = await TaiCongNoCuAsync(connection, capNhatHoaDon, ct);

            var batchIndex = 0;

            foreach (var batch in Chunk(canGhi, SoHoaDonMoiTrenBatch))
            {
                ct.ThrowIfCancellationRequested();
                batchIndex++;

                var savepoint = $"sp_nhap_hd_{batchIndex}";
                var soDuTruoc = soDuCongNo.SaoLuu();

                await connection.ExecuteAsync($"SAVEPOINT {savepoint};", transaction: transaction);

                try
                {
                    await GhiMotBatchAsync(
                        connection,
                        transaction,
                        batch,
                        soDuCongNo,
                        congNoCu,
                        branchId,
                        userId,
                        result,
                        ct);

                    await connection.ExecuteAsync($"RELEASE SAVEPOINT {savepoint};", transaction: transaction);
                }
                catch (OperationCanceledException)
                {
                    await connection.ExecuteAsync($"ROLLBACK TO SAVEPOINT {savepoint};", transaction: transaction);
                    throw;
                }
                catch (Exception)
                {
                    // Bát hỏng thì thử lại từng hóa đơn để chỉ loại đúng hóa đơn
                    // có dữ liệu sai, các hóa đơn khác trong bát vẫn được giữ.
                    await connection.ExecuteAsync($"ROLLBACK TO SAVEPOINT {savepoint};", transaction: transaction);
                    soDuCongNo.KhoiPhuc(soDuTruoc);

                    await GhiLaiTungHoaDonAsync(
                        connection,
                        transaction,
                        batch,
                        soDuCongNo,
                        congNoCu,
                        branchId,
                        userId,
                        result,
                        ct);

                    await connection.ExecuteAsync($"RELEASE SAVEPOINT {savepoint};", transaction: transaction);
                }

                progress?.Report(new HoaDonImportProgress
                {
                    GiaiDoan = "Đang ghi dữ liệu vào database...",
                    DaXuLy = Math.Min(batchIndex * SoHoaDonMoiTrenBatch, canGhi.Count),
                    TongSo = canGhi.Count,
                    ThanhCong = result.TongSoGhiThanhCong,
                    BoQua = result.SoBoQua,
                    Loi = result.SoLoi
                });
            }

            await connection.ExecuteAsync(
                $"DROP TEMPORARY TABLE IF EXISTS {BangTamCapNhat};",
                transaction: transaction);

            await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            await RollbackAnToanAsync(transaction);

            result.DaHuy = true;
            result.SoThemMoi = 0;
            result.SoCapNhat = 0;
        }
        catch (Exception ex)
        {
            await RollbackAnToanAsync(transaction);

            result.DaRollback = true;
            result.SoThemMoi = 0;
            result.SoCapNhat = 0;
            result.DanhSachLoi.Add(new HoaDonImportError(
                0,
                null,
                "Lỗi nghiêm trọng, đã rollback toàn bộ lượt nhập: "
                + DatabaseService.MoTaLoiNhap(ex)));
        }
        finally
        {
            result.ThoiGian = stopwatch.Elapsed;
            GhiLog(result, "Hoàn tất import hóa đơn.");
        }

        return result;
    }

    private static async Task RollbackAnToanAsync(MySqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (InvalidOperationException)
        {
            // Transaction đã được commit hoặc đã tự động hủy thì không cần làm gì.
        }
    }

    // ============================================================
    // GHI THEO BÁT
    // ============================================================

    /// <summary>
    /// Ghi một bát hóa đơn bằng vài câu lệnh: một lệnh ghi header, một lệnh
    /// tra lại id, một lệnh ghi toàn bộ chi tiết. Không mở connection mới,
    /// không tạo SQL bằng nối chuỗi dữ liệu người dùng.
    /// </summary>
    private static async Task GhiMotBatchAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyList<HoaDonImportRow> batch,
        SoDuCongNo soDuCongNo,
        IReadOnlyDictionary<string, CongNoCu> congNoCu,
        uint branchId,
        uint? userId,
        HoaDonImportResult result,
        CancellationToken ct)
    {
        var idHoaDon = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var moi = new List<HoaDonImportRow>(batch.Count);
        var capNhat = new List<HoaDonImportRow>();

        foreach (var hoaDon in batch)
        {
            ct.ThrowIfCancellationRequested();

            if (hoaDon.LaHoaDonTrung)
                capNhat.Add(hoaDon);
            else
                moi.Add(hoaDon);
        }

        if (capNhat.Count > 0)
        {
            await CapNhatHeaderAsync(connection, transaction, capNhat, branchId, userId, ct);

            // Xóa chi tiết và công nợ cũ trước, cùng một transaction.
            await XoaChiTietCuAsync(connection, transaction, capNhat.Select(h => h.MaHoaDon), ct);
            await XoaCongNoCuAsync(connection, transaction, capNhat.Select(h => h.MaHoaDon), ct);
        }

        if (moi.Count > 0)
        {
            foreach (var chunk in Chunk(moi, SoDongHeaderTrenCauLenh))
            {
                await connection.ExecuteAsync(
                    SqlInsertHeader(),
                    chunk.Select(hd => TaoDongHeader(hd, branchId, userId)).ToList(),
                    transaction);
            }
        }

        await NapIdHoaDonAsync(connection, transaction, batch.Select(h => h.MaHoaDon), idHoaDon, ct);

        var chiTiet = new List<DongChiTiet>();

        foreach (var hoaDon in batch)
        {
            ct.ThrowIfCancellationRequested();

            if (!idHoaDon.TryGetValue(hoaDon.MaHoaDon, out var idHd))
                continue;

            foreach (var dong in hoaDon.ChiTiet)
            {
                if (dong.SanPham is null)
                    continue;

                chiTiet.Add(new DongChiTiet
                {
                    HoaDonId = idHd,
                    SanPhamId = dong.SanPham.Id,
                    MaSanPham = dong.MaSanPham,
                    TenSanPham = DongChiTietCatTen(dong.TenSanPhamDung),
                    DonViTinh = dong.SanPham.DonViTinh,
                    SoLuong = Math.Round(dong.SoLuong, 3, MidpointRounding.AwayFromZero),
                    DonGia = Tien(dong.DonGia),
                    GiamGia = Tien(dong.GiamGia),
                    ThanhTien = Tien(dong.ThanhTienDung),
                    GiaVon = Tien(dong.SanPham.GiaVon),
                    GhiChu = dong.GhiChu
                });
            }
        }

        foreach (var chunk in Chunk(chiTiet, SoDongChiTietTrenCauLenh))
            await connection.ExecuteAsync(SqlInsertChiTiet(), chunk, transaction);

        await GhiCongNoAsync(
            connection,
            transaction,
            batch,
            idHoaDon,
            soDuCongNo,
            congNoCu,
            branchId,
            userId,
            ct);

        result.SoThemMoi += moi.Count;
        result.SoCapNhat += capNhat.Count;
    }

    /// <summary>
    /// Khi một bát hỏng thì ghi lại từng hóa đơn một, mỗi hóa đơn một savepoint
    /// riêng: hóa đơn nào lỗi thì chỉ mất riêng hóa đơn đó.
    /// </summary>
    private static async Task GhiLaiTungHoaDonAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyList<HoaDonImportRow> batch,
        SoDuCongNo soDuCongNo,
        IReadOnlyDictionary<string, CongNoCu> congNoCu,
        uint branchId,
        uint? userId,
        HoaDonImportResult result,
        CancellationToken ct)
    {
        for (var index = 0; index < batch.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var hoaDon = batch[index];
            var savepoint = $"sp_nhap_hd_1_{index}";
            var truoc = soDuCongNo.SaoLuu();

            await connection.ExecuteAsync($"SAVEPOINT {savepoint};", transaction: transaction);

            try
            {
                await GhiMotBatchAsync(
                    connection,
                    transaction,
                    [hoaDon],
                    soDuCongNo,
                    congNoCu,
                    branchId,
                    userId,
                    result,
                    ct);

                await connection.ExecuteAsync($"RELEASE SAVEPOINT {savepoint};", transaction: transaction);
            }
            catch (OperationCanceledException)
            {
                await connection.ExecuteAsync($"ROLLBACK TO SAVEPOINT {savepoint};", transaction: transaction);
                soDuCongNo.KhoiPhuc(truoc);
                throw;
            }
            catch (Exception ex)
            {
                await connection.ExecuteAsync($"ROLLBACK TO SAVEPOINT {savepoint};", transaction: transaction);
                soDuCongNo.KhoiPhuc(truoc);

                result.SoLoi++;
                result.DanhSachLoi.Add(new HoaDonImportError(
                    hoaDon.SoDongExcel,
                    hoaDon.MaHoaDon,
                    DatabaseService.MoTaLoiNhap(ex)));
            }
        }
    }

    private static DongHeader TaoDongHeader(HoaDonImportRow hd, uint branchId, uint? userId) =>
        new()
        {
            MaHoaDon = hd.MaHoaDon,
            ChiNhanhId = branchId,
            KhachHangId = hd.IdKhachHang,
            NguoiBanId = userId,
            ThoiGianBan = hd.ThoiGianBan,
            TongTienHang = Tien(hd.TongTienHieuDung),
            GiamGia = Tien(hd.GiamGia),
            ThuKhac = Tien(hd.ThuKhacChuan),
            KhachCanTra = Tien(hd.KhachCanTra),
            KhachDaThanhToan = Tien(hd.DaThanhToanChuan),
            ConNo = Tien(hd.ConNoChuan),
            TrangThaiThanhToan = hd.TrangThaiThanhToanChuan,
            TrangThaiHoaDon = HoanThanh,
            GhiChu = hd.GhiChu
        };

    private static string SqlInsertHeader() =>
        $"""
         INSERT INTO {BangHoaDon}
         (
             ma_hoa_don, chi_nhanh_id, khach_hang_id, nguoi_ban_id, thoi_gian_ban,
             tong_tien_hang, giam_gia, thu_khac, khach_can_tra, khach_da_thanh_toan,
             con_no, trang_thai_thanh_toan, trang_thai_hoa_don, ghi_chu,
             ngay_tao, ngay_cap_nhat
         )
         VALUES
         (
             @MaHoaDon, @ChiNhanhId, @KhachHangId, @NguoiBanId, @ThoiGianBan,
             @TongTienHang, @GiamGia, @ThuKhac, @KhachCanTra, @KhachDaThanhToan,
             @ConNo, @TrangThaiThanhToan, @TrangThaiHoaDon, @GhiChu,
             @ThoiGianBan, @ThoiGianBan
         );
         """;

    private static string SqlInsertChiTiet() =>
        $"""
         INSERT INTO {BangChiTiet}
         (
             hoa_don_id, san_pham_id, ma_san_pham, ten_san_pham, don_vi_tinh,
             so_luong, don_gia, giam_gia, thanh_tien, gia_von, ghi_chu
         )
         VALUES
         (
             @HoaDonId, @SanPhamId, @MaSanPham, @TenSanPham, @DonViTinh,
             @SoLuong, @DonGia, @GiamGia, @ThanhTien, @GiaVon, @GhiChu
         );
         """;

    /// <summary>
    /// Cập nhật hàng loạt header của các hóa đơn đã tồn tại, đi qua bảng tạm
    /// để mỗi bát chỉ tốn vài câu lệnh thay vì một lệnh cho mỗi hóa đơn.
    /// Ngày tạo cũ được giữ nguyên, chỉ ngày cập nhật là đổi.
    /// </summary>
    private static async Task CapNhatHeaderAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        List<HoaDonImportRow> capNhat,
        uint branchId,
        uint? userId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await connection.ExecuteAsync(
            $"CREATE TEMPORARY TABLE IF NOT EXISTS {BangTamCapNhat} LIKE {BangHoaDon};",
            transaction: transaction);

        await connection.ExecuteAsync($"DELETE FROM {BangTamCapNhat};", transaction: transaction);

        foreach (var chunk in Chunk(capNhat, SoDongHeaderTrenCauLenh))
        {
            var dong = new List<DongHeader>(chunk.Count);

            foreach (var hd in chunk)
                dong.Add(TaoDongHeader(hd, branchId, userId));

            await connection.ExecuteAsync(SqlInsertHeader(), dong, transaction);
        }

        await connection.ExecuteAsync(
            $"""
             UPDATE {BangHoaDon} h
             INNER JOIN {BangTamCapNhat} t ON t.ma_hoa_don = h.ma_hoa_don
             SET
                 h.khach_hang_id = t.khach_hang_id,
                 h.nguoi_ban_id = t.nguoi_ban_id,
                 h.thoi_gian_ban = t.thoi_gian_ban,
                 h.tong_tien_hang = t.tong_tien_hang,
                 h.giam_gia = t.giam_gia,
                 h.thu_khac = t.thu_khac,
                 h.khach_can_tra = t.khach_can_tra,
                 h.khach_da_thanh_toan = t.khach_da_thanh_toan,
                 h.con_no = t.con_no,
                 h.trang_thai_thanh_toan = t.trang_thai_thanh_toan,
                 h.trang_thai_hoa_don = t.trang_thai_hoa_don,
                 h.ghi_chu = t.ghi_chu,
                 h.ngay_cap_nhat = NOW();
             """,
            transaction: transaction);
    }

    /// <summary>Xóa chi tiết cũ của các hóa đơn được cập nhật, theo đúng mã hóa đơn.</summary>
    private static async Task XoaChiTietCuAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IEnumerable<string> maHoaDon,
        CancellationToken ct)
    {
        foreach (var chunk in Chunk(maHoaDon, 500))
        {
            ct.ThrowIfCancellationRequested();
            var (parameters, dieuKien) = TaoDieuKienMa(chunk);
            await connection.ExecuteAsync(
                $"""
                 DELETE c FROM {BangChiTiet} c
                 INNER JOIN {BangHoaDon} h ON h.id = c.hoa_don_id
                 WHERE h.ma_hoa_don IN ({dieuKien});
                 """,
                parameters,
                transaction);
        }
    }

    /// <summary>Xóa dòng lịch sử công nợ cũ của các hóa đơn được cập nhật.</summary>
    private static async Task XoaCongNoCuAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IEnumerable<string> maHoaDon,
        CancellationToken ct)
    {
        foreach (var chunk in Chunk(maHoaDon, 500))
        {
            ct.ThrowIfCancellationRequested();
            var (parameters, dieuKien) = TaoDieuKienMa(chunk);
            await connection.ExecuteAsync(
                $"""
                 DELETE FROM {BangCongNo}
                 WHERE loai_chung_tu = 'HOA_DON'
                   AND ma_chung_tu IN ({dieuKien});
                 """,
                parameters,
                transaction);
        }
    }

    /// <summary>
    /// Tra lại id của các hóa đơn vừa ghi. Dùng khóa duy nhất uk_hdbh_ma nên
    /// không phụ thuộc vào việc MySQL cấp id tăng dần liên tiếp hay không.
    /// </summary>
    private static async Task NapIdHoaDonAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IEnumerable<string> maHoaDon,
        Dictionary<string, uint> ketQua,
        CancellationToken ct)
    {
        foreach (var chunk in Chunk(maHoaDon, 500))
        {
            ct.ThrowIfCancellationRequested();
            var (parameters, dieuKien) = TaoDieuKienMa(chunk);

            var rows = (await connection.QueryAsync<(uint Id, string Ma)>(
                $"""
                 SELECT id AS Id, ma_hoa_don AS Ma
                 FROM {BangHoaDon}
                 WHERE ma_hoa_don IN ({dieuKien});
                 """,
                parameters,
                transaction)).AsList();

            foreach (var row in rows)
                ketQua[row.Ma.Trim()] = row.Id;
        }
    }

    private static (DynamicParameters Parameters, string DieuKien) TaoDieuKienMa(IReadOnlyList<string> maHoaDon)
    {
        var parameters = new DynamicParameters();
        var dieuKien = new List<string>(maHoaDon.Count);

        for (var i = 0; i < maHoaDon.Count; i++)
        {
            parameters.Add("Ma" + i, maHoaDon[i], DbType.String);
            dieuKien.Add("@Ma" + i);
        }

        return (parameters, string.Join(", ", dieuKien));
    }

    // ============================================================
    // CÔNG NỢ
    // ============================================================

    /// <summary>Số dư công nợ đang ghi trong bộ nhớ, có sao lưu để khôi phục khi bát hỏng.</summary>
    private sealed class SoDuCongNo
    {
        private Dictionary<uint, decimal> _giaTri;

        public SoDuCongNo(Dictionary<uint, decimal> giaTri) => _giaTri = giaTri;

        public decimal Lay(uint khachHangId) => _giaTri.TryGetValue(khachHangId, out var value) ? value : 0m;

        public void Cong(uint khachHangId, decimal soTien) => _giaTri[khachHangId] = Lay(khachHangId) + soTien;

        public void Tru(uint khachHangId, decimal soTien) => _giaTri[khachHangId] = Lay(khachHangId) - soTien;

        public Dictionary<uint, decimal> SaoLuu() => new(_giaTri);

        public void KhoiPhuc(Dictionary<uint, decimal> saoLuu) => _giaTri = saoLuu;
    }

    private static async Task<Dictionary<uint, decimal>> TaiSoDuCongNoAsync(
        MySqlConnection connection,
        CancellationToken ct)
    {
        var ketQua = new Dictionary<uint, decimal>();

        var rows = (await connection.QueryAsync<(uint Id, decimal SoDu)>(
            $"""
             SELECT khach_hang_id AS Id, COALESCE(SUM(ghi_no - ghi_co), 0) AS SoDu
             FROM {BangCongNo}
             GROUP BY khach_hang_id;
             """)).AsList();

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            ketQua[row.Id] = row.SoDu;
        }

        return ketQua;
    }

    /// <summary>Công nợ đang ghi của các hóa đơn sẽ được cập nhật, để trừ đúng khi ghi lại.</summary>
    private sealed record CongNoCu(uint? KhachHangId, decimal ConNo);

    private static async Task<Dictionary<string, CongNoCu>> TaiCongNoCuAsync(
        MySqlConnection connection,
        IReadOnlyList<string> maHoaDon,
        CancellationToken ct)
    {
        var ketQua = new Dictionary<string, CongNoCu>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in Chunk(maHoaDon, 500))
        {
            ct.ThrowIfCancellationRequested();
            var (parameters, dieuKien) = TaoDieuKienMa(chunk);

            var rows = (await connection.QueryAsync<(string Ma, uint? KhachHangId, decimal ConNo)>(
                $"""
                 SELECT ma_hoa_don AS Ma, khach_hang_id AS KhachHangId, con_no AS ConNo
                 FROM {BangHoaDon}
                 WHERE ma_hoa_don IN ({dieuKien});
                 """,
                parameters,
                transaction: null)).AsList();

            foreach (var row in rows)
                ketQua[row.Ma.Trim()] = new CongNoCu(row.KhachHangId, row.ConNo);
        }

        return ketQua;
    }

    /// <summary>
    /// Đếm hóa đơn sắp cập nhật có chi tiết đang được phiếu trả hàng liên kết,
    /// để cảnh báo người dùng trước khi ghi.
    /// </summary>
    private static async Task<int> DemHoaDonLienPhieuTraHangAsync(
        MySqlConnection connection,
        IReadOnlyList<string> maHoaDon,
        CancellationToken ct)
    {
        var tong = 0;

        foreach (var chunk in Chunk(maHoaDon, 500))
        {
            ct.ThrowIfCancellationRequested();
            var (parameters, dieuKien) = TaoDieuKienMa(chunk);

            tong += await connection.ExecuteScalarAsync<int>(
                $"""
                 SELECT COUNT(DISTINCT h.id)
                 FROM {BangHoaDon} h
                 INNER JOIN chi_tiet_hoa_don_ban_hang c ON c.hoa_don_id = h.id
                 INNER JOIN chi_tiet_phieu_tra_hang t ON t.chi_tiet_hoa_don_id = c.id
                 WHERE h.ma_hoa_don IN ({dieuKien});
                 """,
                parameters);
        }

        return tong;
    }

    /// <summary>
    /// Ghi lịch sử công nợ cho các hóa đơn còn nợ, đúng như lúc bán hàng.
    /// Số dư được cộng dồn theo thứ tự thời gian nên khớp với cột công nợ
    /// mà màn hình khách hàng đang hiển thị.
    /// </summary>
    private static async Task GhiCongNoAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyList<HoaDonImportRow> batch,
        IReadOnlyDictionary<string, uint> idHoaDon,
        SoDuCongNo soDuCongNo,
        IReadOnlyDictionary<string, CongNoCu> congNoCu,
        uint branchId,
        uint? userId,
        CancellationToken ct)
    {
        var rows = new List<DongCongNo>();

        foreach (var hoaDon in batch)
        {
            ct.ThrowIfCancellationRequested();

            // Hóa đơn được cập nhật: công nợ cũ vừa bị xóa nên trừ khỏi số dư
            // trước khi cộng công nợ mới, nếu không sẽ bị tính hai lần.
            if (hoaDon.LaHoaDonTrung
                && congNoCu.TryGetValue(hoaDon.MaHoaDon, out var cu)
                && cu.ConNo != 0m
                && cu.KhachHangId.HasValue)
            {
                soDuCongNo.Tru(cu.KhachHangId.Value, cu.ConNo);
            }

            if (hoaDon.ConNoChuan <= 0m
                || hoaDon.IdKhachHang is not uint idKhachHang
                || !idHoaDon.ContainsKey(hoaDon.MaHoaDon))
                continue;

            soDuCongNo.Cong(idKhachHang, hoaDon.ConNoChuan);

            rows.Add(new DongCongNo
            {
                KhachHangId = idKhachHang,
                ChiNhanhId = branchId,
                LoaiGiaoDich = "BAN_HANG",
                LoaiChungTu = "HOA_DON",
                MaChungTu = hoaDon.MaHoaDon,
                GhiNo = Tien(hoaDon.ConNoChuan),
                SoDu = Tien(soDuCongNo.Lay(idKhachHang)),
                ThoiGian = hoaDon.ThoiGianBan,
                NguoiThucHienId = userId,
                GhiChu = $"Công nợ phát sinh từ hóa đơn {hoaDon.MaHoaDon}"
            });
        }

        foreach (var chunk in Chunk(rows, SoDongChiTietTrenCauLenh))
        {
            await connection.ExecuteAsync(
                $"""
                 INSERT INTO {BangCongNo}
                 (
                     khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                     ma_chung_tu, ghi_no, ghi_co, so_du, thoi_gian,
                     nguoi_thuc_hien_id, ghi_chu
                 )
                 VALUES
                 (
                     @KhachHangId, @ChiNhanhId, @LoaiGiaoDich, @LoaiChungTu,
                     @MaChungTu, @GhiNo, 0, @SoDu, @ThoiGian,
                     @NguoiThucHienId, @GhiChu
                 );
                 """,
                chunk,
                transaction);
        }
    }

    // ============================================================
    // KHÁCH HÀNG THIẾU
    // ============================================================

    /// <summary>
    /// Gán id khách hàng cho từng hóa đơn. Nếu người dùng cho phép, khách hàng
    /// thiếu sẽ được tạo mới một lần duy nhất cho mỗi mã, không tạo trùng.
    /// Hóa đơn không gán được khách hàng sẽ không có trong kết quả và bị loại.
    /// </summary>
    private static async Task<Dictionary<string, uint?>> GanIdKhachHangAsync(
        MySqlConnection connection,
        IReadOnlyList<HoaDonImportRow> canGhi,
        bool taoKhachHangMoi,
        HoaDonImportResult result,
        CancellationToken ct)
    {
        var ketQua = new Dictionary<string, uint?>(StringComparer.OrdinalIgnoreCase);
        var daTao = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        foreach (var hoaDon in canGhi)
        {
            ct.ThrowIfCancellationRequested();

            if (hoaDon.TrangThaiKhachHang != TrangThaiKhachHangHoaDon.ChuaCoTrongDatabase)
            {
                // Hóa đơn bán lẻ để khach_hang_id rỗng: phần mềm hiển thị "Khách lẻ".
                ketQua[hoaDon.MaHoaDon] = hoaDon.IdKhachHang;
                continue;
            }

            var ma = (hoaDon.MaKhachHang ?? "").Trim();
            var ten = (hoaDon.TenKhachHang ?? "").Trim();

            if (!taoKhachHangMoi
                || ma.Length == 0
                || ma.Length > DoDaiToiDaMaKhachHang
                || ten.Length == 0)
            {
                var lyDo = ma.Length == 0
                    ? "Thiếu mã khách hàng nên không tạo được khách hàng mới."
                    : ma.Length > DoDaiToiDaMaKhachHang
                        ? $"Mã khách hàng dài {ma.Length} ký tự, vượt quá {DoDaiToiDaMaKhachHang} ký tự mà database cho phép."
                        : "Thiếu tên khách hàng nên không tạo được khách hàng mới.";

                result.SoLoi++;
                result.DanhSachLoi.Add(new HoaDonImportError(hoaDon.SoDongExcel, hoaDon.MaHoaDon, lyDo));
                continue;
            }

            // Khách đã có sẵn nhưng lúc đọc danh mục chưa thấy: tra lại một lần.
            uint? idKhach = await TraIdKhachHangTheoMaAsync(connection, ma, ct);

            if (!idKhach.HasValue && daTao.TryGetValue(ma, out var idDaTao))
                idKhach = idDaTao;

            if (!idKhach.HasValue)
            {
                if (!await ThemKhachHangAsync(connection, ma, ten, hoaDon.SoDienThoai, ct))
                {
                    result.SoLoi++;
                    result.DanhSachLoi.Add(new HoaDonImportError(
                        hoaDon.SoDongExcel,
                        hoaDon.MaHoaDon,
                        $"Không tạo được khách hàng \"{ten}\" (mã {ma})."));
                    continue;
                }

                idKhach = await TraIdKhachHangTheoMaAsync(connection, ma, ct);
                result.SoKhachHangTaoMoi++;
            }

            if (!idKhach.HasValue)
            {
                result.SoLoi++;
                result.DanhSachLoi.Add(new HoaDonImportError(
                    hoaDon.SoDongExcel,
                    hoaDon.MaHoaDon,
                    $"Đã tạo khách hàng mã {ma} nhưng không đọc lại được id."));
                continue;
            }

            daTao[ma] = idKhach.Value;
            hoaDon.IdKhachHang = idKhach;
            hoaDon.TrangThaiKhachHang = TrangThaiKhachHangHoaDon.DaTaoMoi;
            ketQua[hoaDon.MaHoaDon] = idKhach;
        }

        return ketQua;
    }

    private static async Task<uint?> TraIdKhachHangTheoMaAsync(
        MySqlConnection connection,
        string maKhachHang,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await connection.ExecuteScalarAsync<uint?>(
            "SELECT id FROM khach_hang WHERE ma_khach_hang = @MaKhachHang LIMIT 1;",
            new { MaKhachHang = maKhachHang });
    }

    /// <summary>
    /// Tạo khách hàng mới từ dữ liệu file. Không dùng tên làm khoá vì database
    /// chỉ có ràng buộc duy nhất trên mã; người dùng đã được hỏi trước khi
    /// đồng ý tạo mới.
    /// </summary>
    private static async Task<bool> ThemKhachHangAsync(
        MySqlConnection connection,
        string maKhachHang,
        string tenKhachHang,
        string? soDienThoai,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO khach_hang
                    (ma_khach_hang, ten_khach_hang, so_dien_thoai, ngay_tao, ngay_cap_nhat)
                VALUES
                    (@MaKhachHang, @TenKhachHang, @SoDienThoai, NOW(), NOW());
                """,
                new
                {
                    MaKhachHang = maKhachHang.Length > DoDaiToiDaMaKhachHang
                        ? maKhachHang[..DoDaiToiDaMaKhachHang]
                        : maKhachHang,
                    TenKhachHang = tenKhachHang.Length > 150 ? tenKhachHang[..150] : tenKhachHang,
                    SoDienThoai = string.IsNullOrWhiteSpace(soDienThoai) ? null : soDienThoai.Trim()
                });

            return true;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            // Mã đã có sẵn trong database: coi như đã tồn tại.
            return false;
        }
    }

    // ============================================================
    // XUẤT DANH SÁCH LỖI
    // ============================================================

    /// <summary>Ghi danh sách lỗi ra file Excel để người dùng sửa lại file gốc.</summary>
    public static void XuatDanhSachLoi(
        string filePath,
        string tenFileNguon,
        IEnumerable<HoaDonImportError> danhSachLoi)
    {
        var loi = danhSachLoi.ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Danh sách lỗi");

        var cot = new (string Ten, double DoRong)[]
        {
            ("Dòng trong file", 14),
            ("Mã hóa đơn", 24),
            ("Lý do", 90)
        };

        for (var i = 0; i < cot.Length; i++)
        {
            var oTieuDe = sheet.Cell(1, i + 1);
            oTieuDe.Value = cot[i].Ten;
            oTieuDe.Style.Font.Bold = true;
            oTieuDe.Style.Font.FontColor = XLColor.White;
            oTieuDe.Style.Fill.BackgroundColor = XLColor.FromHtml("#B3261E");
            oTieuDe.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Column(i + 1).Width = cot[i].DoRong;
        }

        for (var i = 0; i < loi.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = loi[i].SoDongExcel;
            sheet.Cell(i + 2, 2).Value = loi[i].MaHoaDon ?? "";
            sheet.Cell(i + 2, 3).Value = loi[i].LyDo;
            sheet.Cell(i + 2, 3).Style.Alignment.WrapText = true;
        }

        if (loi.Count > 0)
            sheet.Range(1, 1, loi.Count + 1, cot.Length).SetAutoFilter();

        sheet.SheetView.FreezeRows(1);

        var sheetThongTin = workbook.Worksheets.Add("Thông tin");
        sheetThongTin.Column(1).Width = 24;
        sheetThongTin.Column(2).Width = 70;
        sheetThongTin.Cell(1, 1).Value = "File nguồn";
        sheetThongTin.Cell(1, 2).Value = tenFileNguon;
        sheetThongTin.Cell(2, 1).Value = "Số lỗi";
        sheetThongTin.Cell(2, 2).Value = loi.Count;
        sheetThongTin.Cell(3, 1).Value = "Thời gian xuất";
        sheetThongTin.Cell(3, 2).Value = DateTime.Now;
        sheetThongTin.Range(1, 1, 3, 1).Style.Font.Bold = true;

        workbook.SaveAs(filePath);
    }

    // ============================================================
    // TIỆN ÍCH
    // ============================================================

    /// <summary>Làm tròn về đúng độ chính xác của cột tiền trong database.</summary>
    private static decimal Tien(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string DongChiTietCatTen(string tenSanPham) =>
        tenSanPham.Length > DoDaiToiDaTenSanPham ? tenSanPham[..DoDaiToiDaTenSanPham] : tenSanPham;

    /// <summary>
    /// Chia danh sách thành từng lô để ghi bằng câu lệnh nhiều giá trị, tránh
    /// gửi từng bản ghi một lệnh. Nhận IEnumerable vì chỗ gọi thường truyền
    /// vào kết quả LINQ.
    /// </summary>
    private static IEnumerable<List<T>> Chunk<T>(IEnumerable<T> source, int size)
    {
        var chunk = new List<T>(size);

        foreach (var item in source)
        {
            chunk.Add(item);

            if (chunk.Count < size)
                continue;

            yield return chunk;
            chunk = [];
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    /// <summary>
    /// Ghi log quá trình import vào tệp trong thư mục dữ liệu ứng dụng.
    /// Chỉ ghi số liệu và tên file, không ghi thông tin kết nối.
    /// </summary>
    private void GhiLog(HoaDonImportResult result, string suKien)
    {
        _db.GhiLog(
            $"Bắt đầu: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | File: {result.TenFile}"
            + $" | Tổng hóa đơn: {result.TongSoHoaDon:N0}"
            + $" | Dòng chi tiết: {result.TongSoDongChiTiet:N0}"
            + $" | Sự kiện: {suKien}"
            + $" | {result.ThongBaoTatCa.Replace(Environment.NewLine, " | ")}",
            "nhap-hoa-don");

        foreach (var loi in result.DanhSachLoi.Take(500))
        {
            _db.GhiLog(
                $"LỖI {result.TenFile}: dòng {loi.SoDongExcel} "
                + $"[{loi.MaHoaDon ?? "(trống)"}] {loi.LyDo}",
                "nhap-hoa-don");
        }
    }

    // ============================================================
    // DÒNG SẼ GHI XUỐNG DATABASE
    // Tên thuộc tính trùng tên cột trong database để Dapper tự ánh xạ tham số.
    // ============================================================

    private sealed class DongHeader
    {
        public string MaHoaDon { get; init; } = "";
        public uint ChiNhanhId { get; init; }
        public uint? KhachHangId { get; init; }
        public uint? NguoiBanId { get; init; }
        public DateTime ThoiGianBan { get; init; }
        public decimal TongTienHang { get; init; }
        public decimal GiamGia { get; init; }
        public decimal ThuKhac { get; init; }
        public decimal KhachCanTra { get; init; }
        public decimal KhachDaThanhToan { get; init; }
        public decimal ConNo { get; init; }
        public string TrangThaiThanhToan { get; init; } = DaThanhToan;
        public string TrangThaiHoaDon { get; init; } = HoanThanh;
        public string? GhiChu { get; init; }
    }

    private sealed class DongChiTiet
    {
        public uint HoaDonId { get; init; }
        public uint SanPhamId { get; init; }
        public string MaSanPham { get; init; } = "";
        public string TenSanPham { get; init; } = "";
        public string? DonViTinh { get; init; }
        public decimal SoLuong { get; init; }
        public decimal DonGia { get; init; }
        public decimal GiamGia { get; init; }
        public decimal ThanhTien { get; init; }
        public decimal GiaVon { get; init; }
        public string? GhiChu { get; init; }
    }

    private sealed class DongCongNo
    {
        public uint KhachHangId { get; init; }
        public uint ChiNhanhId { get; init; }
        public string LoaiGiaoDich { get; init; } = "BAN_HANG";
        public string LoaiChungTu { get; init; } = "HOA_DON";
        public string MaChungTu { get; init; } = "";
        public decimal GhiNo { get; init; }
        public decimal SoDu { get; init; }
        public DateTime ThoiGian { get; init; }
        public uint? NguoiThucHienId { get; init; }
        public string? GhiChu { get; init; }
    }
}
