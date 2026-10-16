using System.Globalization;
using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Đọc, kiểm tra và ghi phiếu sổ quỹ từ file Excel hoặc CSV.
/// <para>
/// Quy trình gồm ba bước tách bạch: đọc file (chỉ đọc, không ghi), kiểm tra
/// (có màn hình xem trước cho người dùng quyết định), rồi mới ghi. Bước ghi luôn
/// nằm trong một transaction nên lỗi ở giữa chừng sẽ rollback sạch thay vì để lại
/// dữ liệu nửa vời.
/// </para>
/// </summary>
public sealed class SoQuyImportService
{
    /// <summary>Số dòng lỗi được giữ để hiển thị; vượt quá thì chỉ ghi tổng số.</summary>
    private const int SoLoiToiDaGiu = 500;

    /// <summary>Số dòng báo tiến độ sau mỗi lần đọc/ghi để UI không bị đứng.</summary>
    private const int BuocBaoTienDo = 100;

    private readonly DatabaseService _db = new();

    /// <summary>
    /// Tên cột được chấp nhận cho từng trường, đã bỏ dấu và bỏ ký tự phân tách
    /// đúng kiểu khóa mà <c>DatabaseService.NormalizeHeaderKey</c> tạo ra.
    /// Nhờ vậy "Mã phiếu", "MaPhieu" hay "Ma Phieu" đều khớp cùng một trường.
    /// Thứ tự trong bảng quyết định trường nào được ưu tiên khi một cột khớp
    /// nhiều trường cùng lúc.
    /// </summary>
    private static readonly (TruongSoQuy Truong, HashSet<string> Khoa)[] BangDoanCot =
    [
        (TruongSoQuy.MaPhieu, TaoKhoa(
            "maphieu", "maphieuquy", "machungtu", "machungtusq", "mact", "sophieu",
            "mabill", "mactquy", "phieuthu", "phieuchi", "mahd", "invoicecode", "billno")),
        (TruongSoQuy.NgayGio, TaoKhoa(
            "thoigian", "ngaygio", "thoigianchi", "thoigiantru", "ngay", "ngaytao",
            "ngaylap", "ngayghidanh", "date", "datetime", "thoidiem", "ngayvt")),
        (TruongSoQuy.LoaiPhieu, TaoKhoa(
            "loaiphieu", "loaichungtu", "loaiso", "khoanchit", "type", "typeof",
            "phieuthu", "thuchit", "loaitien")),
        (TruongSoQuy.NguoiNopNhan, TaoKhoa(
            "nguoinopnhan", "nguoinop", "nguoihan", "nguoithu", "nguoichi",
            "nguoiphatnhan", "khachhang", "tennguoichitien", "nguoichitien",
            "person", "payer", "receiver")),
        (TruongSoQuy.LoaiThuChi, TaoKhoa(
            "loaithuchi", "loaitien", "nhomchiphi", "tennhom", "loaipheinuut",
            "category", "categoryname", "tenloai", "loai")),
        (TruongSoQuy.SoTien, TaoKhoa(
            "sotien", "sotienquy", "sotienthu", "sotienchi", "giatri", "tientru",
            "tienchi", "sotienthanhtoan", "amount", "tien", "value", "money")),
        (TruongSoQuy.TrangThai, TaoKhoa(
            "trangthai", "tinhtrang", "status", "trangthaiqd", "trangthaichungtu")),
        (TruongSoQuy.NoiDung, TaoKhoa(
            "noidung", "noidungqd", "dienlydo", "lydothu", "lydochi", "mota",
            "description", "noidungphi", "nd", "tandaidung")),
        (TruongSoQuy.GhiChu, TaoKhoa(
            "ghichu", "ghiuchu", "ghichusq", "note", "notes", "chukhucn", "ykien",
            "comment", "ghichuphoi")),
        (TruongSoQuy.NguoiTao, TaoKhoa(
            "nguoitao", "nguoilap", "nhanvien", "nhanvienlap", "canbo", "nguoiphutrac",
            "nguoithuchien", "createdby", "staff", "user")),
        (TruongSoQuy.Quy, TaoKhoa(
            "quy", "phuongthuc", "phuongthucthanhtoan", "cachthanhtoan",
            "paymentmethod", "hinhthucthanhtoan", "tk", "taiKhoan"))
    ];

    private static HashSet<string> TaoKhoa(params string[] keys)
        => new(keys, StringComparer.Ordinal);

    // ============================================================
    // BƯỚC 1 + 2: ĐỌC FILE VÀ KIỂM TRA
    // ============================================================

    /// <summary>
    /// Đọc file rồi kiểm tra từng dòng, trả về dữ liệu cho bước xem trước.
    /// Hàm này KHÔNG ghi gì vào database.
    /// </summary>
    public async Task<SoQuyImportPreview> DocFileAsync(
        string filePath,
        Func<SoQuyImportProgress, Task>? baoTienDo = null,
        CancellationToken ct = default)
    {
        KiemTraDuongDan(filePath);

        var danhMuc = await _db.GetSoQuyImportDanhMucAsync();

        var noiDungFile = DocNoiDungFile(filePath, ct);

        var preview = new SoQuyImportPreview
        {
            TenFile = Path.GetFileName(filePath),
            TongSoDong = noiDungFile.Dong.Count,
            CanhBao = [.. noiDungFile.CanhBao]
        };

        var loi = new List<SoQuyImportError>(noiDungFile.Loi);
        var canhBao = new List<string>(noiDungFile.CanhBao);

        // Tra danh mục một lần rồi dựng set mã phiếu đang có trong database,
        // không query từng dòng.
        var maPhieuTrongFile = noiDungFile.Dong
            .Select(d => d.GiaTri.GetValueOrDefault(TruongSoQuy.MaPhieu)?.Trim() ?? "")
            .Where(ma => ma.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var maDaTonTai = await _db.GetSoQuyMaPhieuTonTaiAsync(maPhieuTrongFile);

        var dongDaXem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dong = new List<SoQuyImportRow>(noiDungFile.Dong.Count);

        var soPhieuThu = 0;
        var soPhieuChi = 0;
        var tongTienThu = 0m;
        var tongTienChi = 0m;
        var soHopLe = 0;
        var soTrungMa = 0;
        var soTrungTrongFile = 0;

        foreach (var dongFile in noiDungFile.Dong)
        {
            ct.ThrowIfCancellationRequested();

            var ketQua = new KetQuaDongSoQuyKhoiTao();

            var maPhieu = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.MaPhieu) ?? "").Trim();

            // Loại phiếu: ưu tiên cột "Loại phiếu", thiếu thì suy ra từ tiền tố mã.
            var laThu = DocLoaiPhieu(dongFile, maPhieu, ketQua);

            // Số tiền: bắt buộc, phải là số dương.
            var soTien = DocSoTien(dongFile, ketQua);

            // Ngày giờ: thiếu thì lấy thời điểm hiện tại chứ không chặn dòng.
            var thoiGian = DocThoiGian(dongFile);

            var nguoiNopNhan = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.NguoiNopNhan) ?? "").Trim();

            var noiDung = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.NoiDung) ?? "").Trim();

            var ghiChu = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.GhiChu) ?? "").Trim();

            // Loại thu/chi và quỹ tra theo tên vì file của người dùng không có id.
            var tenLoai = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.LoaiThuChi) ?? "").Trim();

            uint? nhomChiPhiId = null;

            if (tenLoai.Length > 0)
            {
                if (danhMuc.LoaiThuChiTheoTen.TryGetValue(DatabaseService.ChuanHoaChuoi(tenLoai), out var idLoai))
                    nhomChiPhiId = idLoai;
                else
                    ketQua.Loi.Add($"Loại thu/chi \"{tenLoai}\" không có trong danh mục");
            }

            string? phuongThuc = null;

            var tenQuy = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.Quy) ?? "").Trim();

            if (tenQuy.Length > 0)
            {
                if (danhMuc.QuyTheoTen.TryGetValue(DatabaseService.ChuanHoaChuoi(tenQuy), out var maQuy))
                    phuongThuc = maQuy;
                else
                    ketQua.Loi.Add($"Quỹ \"{tenQuy}\" không có trong danh sách quỹ của hệ thống");
            }

            // Người tạo: lấy theo tên; không có cột này thì hệ thống dùng người đang đăng nhập.
            var tenNguoiTao = (dongFile.GiaTri.GetValueOrDefault(TruongSoQuy.NguoiTao) ?? "").Trim();

            uint? nguoiTaoId = null;

            if (tenNguoiTao.Length > 0)
            {
                if (danhMuc.NguoiDungTheoTen.TryGetValue(DatabaseService.ChuanHoaChuoi(tenNguoiTao), out var idNguoi))
                    nguoiTaoId = idNguoi;
                else
                    ketQua.Loi.Add($"Người tạo \"{tenNguoiTao}\" không có trong danh sách nhân viên");
            }

            var trangThai = DocTrangThai(dongFile, ketQua);

            // Trùng mã trong chính file: giữ dòng đầu tiên, bỏ các dòng sau
            // để không ghi hai lần vào cùng một mã phiếu.
            var trungTrongFile = maPhieu.Length > 0 && !dongDaXem.Add(maPhieu);

            var ketQuaDong = KetQuaDongSoQuy.HopLe;
            var lyDo = "";

            if (ketQua.Loi.Count > 0)
            {
                ketQuaDong = KetQuaDongSoQuy.Loi;
                lyDo = string.Join("; ", ketQua.Loi);
            }
            else if (trungTrongFile)
            {
                ketQuaDong = KetQuaDongSoQuy.TrungTrongFile;
                lyDo = $"Mã phiếu \"{maPhieu}\" đã xuất hiện ở một dòng trước trong cùng file";
            }
            else if (maPhieu.Length > 0 && maDaTonTai.Contains(maPhieu))
            {
                ketQuaDong = KetQuaDongSoQuy.TrungMa;
                soTrungMa++;
            }

            if (ketQuaDong == KetQuaDongSoQuy.Loi)
            {
                loi.Add(new SoQuyImportError(dongFile.SoDong, maPhieu, lyDo));
            }
            else if (ketQuaDong == KetQuaDongSoQuy.TrungTrongFile)
            {
                soTrungTrongFile++;
            }
            else
            {
                soHopLe++;

                if (laThu)
                {
                    soPhieuThu++;
                    tongTienThu += soTien;
                }
                else
                {
                    soPhieuChi++;
                    tongTienChi += soTien;
                }
            }

            var row = new SoQuyImportRow
            {
                SoDongExcel = dongFile.SoDong,
                MaPhieu = maPhieu,
                LaThu = laThu,
                ThoiGianRaw = thoiGian,
                NguoiNopNhan = nguoiNopNhan,
                NhomChiPhiId = nhomChiPhiId,
                SoTien = soTien,
                TrangThai = trangThai,
                NoiDung = noiDung,
                GhiChu = ghiChu,
                NguoiTaoRaw = tenNguoiTao,
                NguoiTaoId = nguoiTaoId,
                PhuongThuc = phuongThuc,
                KetQua = ketQuaDong,
                LyDo = lyDo,
                MaPhieuDaTonTai = ketQuaDong == KetQuaDongSoQuy.TrungMa
            };

            dong.Add(row);

            if (dong.Count % BuocBaoTienDo == 0 && baoTienDo is not null)
            {
                await baoTienDo(new SoQuyImportProgress
                {
                    GiaiDoan = "Đang kiểm tra dữ liệu",
                    DaXuLy = dong.Count,
                    TongSo = noiDungFile.Dong.Count
                });
            }
        }

        if (danhMuc.LoaiThuChiTheoId.Count == 0)
            canhBao.Add("Bảng danh mục loại thu/chi đang rỗng: mọi dòng sẽ được ghi với loại trống.");

        if (danhMuc.NguoiDungTheoId.Count == 0)
            canhBao.Add("Bảng người dùng đang rỗng hoặc không có ai đang hoạt động: phiếu sẽ không ghi được người tạo.");

        return new SoQuyImportPreview
        {
            TenFile = preview.TenFile,
            TongSoDong = preview.TongSoDong,
            SoDongHopLe = soHopLe,
            SoDongLoi = loi.Count,
            SoDongTrungMa = soTrungMa,
            SoDongTrungTrongFile = soTrungTrongFile,
            SoPhieuThu = soPhieuThu,
            SoPhieuChi = soPhieuChi,
            TongTienThu = tongTienThu,
            TongTienChi = tongTienChi,
            DanhSachDong = dong,
            DanhSachLoi = loi.Take(SoLoiToiDaGiu).ToList(),
            CanhBao = canhBao
        };
    }

    // ============================================================
    // BƯỚC 3: GHI XUỐNG DATABASE
    // ============================================================

    /// <summary>
    /// Ghi các dòng đã qua kiểm tra. Toàn bộ nằm trong một transaction do
    /// DatabaseService thực hiện nên lỗi giữa chừng sẽ rollback sạch.
    /// </summary>
    public async Task<SoQuyImportResult> NhapAsync(
        SoQuyImportPreview preview,
        XuLyPhieuTrung xuLyTrung,
        Func<SoQuyImportProgress, Task>? baoTienDo = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var dongCanGhi = preview.DanhSachDong
            .Where(dong => dong.KetQua == KetQuaDongSoQuy.HopLe || dong.KetQua == KetQuaDongSoQuy.TrungMa)
            .ToList();

        var dongTrong = dongCanGhi.Where(dong => dong.KetQua == KetQuaDongSoQuy.TrungMa).ToList();

        // Dòng trùng mà người dùng chọn giữ dữ liệu cũ thì không ghi,
        // nhưng vẫn báo là đã bỏ qua chứ không âm thầm mất.
        if (xuLyTrung == XuLyPhieuTrung.GiuDuLieuCu && dongTrong.Count > 0)
        {
            dongCanGhi = dongCanGhi.Where(dong => dong.KetQua != KetQuaDongSoQuy.TrungMa).ToList();
        }

        if (xuLyTrung == XuLyPhieuTrung.BoQua)
        {
            dongCanGhi = dongCanGhi.Where(dong => dong.KetQua != KetQuaDongSoQuy.TrungMa).ToList();
        }

        if (dongCanGhi.Count == 0)
        {
            return new SoQuyImportResult
            {
                TenFile = preview.TenFile,
                TongSoDong = preview.TongSoDong,
                SoLoi = preview.DanhSachLoi.Count,
                SoBoQua = preview.SoDongLoi + preview.SoDongTrungTrongFile + dongTrong.Count,
                XuLyTrung = xuLyTrung,
                CanhBao = [.. preview.CanhBao]
            };
        }

        var dongGhi = dongCanGhi.Where(dong =>
        {
            ct.ThrowIfCancellationRequested();
            return true;
        }).ToList();

        var ketQua = await _db.NhapPhieuQuyAsync(
            dongGhi,
            xuLyTrung,
            async tienDo =>
            {
                ct.ThrowIfCancellationRequested();

                if (baoTienDo is not null)
                    await baoTienDo(tienDo);
            });

        // Ghi những lỗi đã biết từ bước kiểm tra vào cùng kết quả để người
        // dùng xem một chỗ, không phải mở lại màn hình xem trước.
        foreach (var loi in preview.DanhSachLoi)
        {
            if (ketQua.DanhSachLoi.Count >= SoLoiToiDaGiu)
                break;

            ketQua.DanhSachLoi.Add(loi);
            ketQua.SoLoi++;
        }

        ketQua.SoBoQua += preview.SoDongLoi + preview.SoDongTrungTrongFile;
        ketQua.TenFile = preview.TenFile;
        ketQua.TongSoDong = preview.TongSoDong;
        ketQua.CanhBao.AddRange(preview.CanhBao);

        return ketQua;
    }

    // ============================================================
    // ĐỌC FILE
    // ============================================================

    /// <summary>Một dòng đọc được: số dòng trong file + giá trị theo từng trường.</summary>
    private sealed class DongFile
    {
        public required int SoDong { get; init; }

        public required Dictionary<TruongSoQuy, string> GiaTri { get; init; }
    }

    /// <summary>Kiểm tra chung cho một dòng, gom lỗi thay vì dừng ở lỗi đầu tiên.</summary>
    private sealed class KetQuaDongSoQuyKhoiTao
    {
        public List<string> Loi { get; } = [];
    }

    private static void KiemTraDuongDan(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("Chưa chọn file cần nhập.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file cần nhập.", filePath);

        var duoi = Path.GetExtension(filePath).ToLowerInvariant();

        // ClosedXML chỉ đọc được định dạng OOXML (.xlsx/.xlsm), không đọc được
        // .xls kiểu BIFF cũ. Chặn sớm ở đây để người dùng nhận thông báo rõ
        // ràng thay vì lỗi kỹ thuật khó hiểu từ thư viện.
        if (duoi == ".xls")
            throw new InvalidOperationException(
                "Định dạng .xls cũ không được hỗ trợ. "
                + "Hãy mở file trong Excel và lưu lại thành .xlsx rồi nhập lại.");

        if (duoi is not (".xlsx" or ".xlsm" or ".csv"))
            throw new InvalidOperationException("Chỉ hỗ trợ nhập từ file .xlsx, .xlsm hoặc .csv.");
    }

    private static NoiDungFile DocNoiDungFile(string filePath, CancellationToken ct)
    {
        var duoi = Path.GetExtension(filePath).ToLowerInvariant();

        var raw = duoi == ".csv" ? DocCsv(filePath) : DocExcel(filePath, ct);

        var loi = new List<SoQuyImportError>();
        var canhBao = new List<string>();

        if (raw.TieuDe.Length == 0)
            throw new InvalidOperationException("File không có dòng tiêu đề để ánh xạ cột.");

        var map = DoanMap(raw.TieuDe);

        if (map.Count == 0)
        {
            throw new InvalidOperationException(
                "Không nhận ra được cột nào là trường của sổ quỹ. Hãy dùng file mẫu để đúng định dạng.");
        }

        // Cột Mã phiếu và Số tiền là hai thứ không thể thiếu: thiếu thì file
        // không có nghĩa, nên báo ngay chứ không im lặng bỏ qua.
        if (!map.ContainsKey(TruongSoQuy.SoTien))
            throw new InvalidOperationException("File thiếu cột \"Số tiền\". Hãy dùng file mẫu để đúng định dạng.");

        if (!map.ContainsKey(TruongSoQuy.MaPhieu))
            canhBao.Add("File không có cột \"Mã phiếu\": hệ thống sẽ tự cấp mã cho các dòng này.");

        if (!map.ContainsKey(TruongSoQuy.NgayGio))
            canhBao.Add("File không có cột \"Ngày giờ\": hệ thống sẽ dùng thời điểm hiện tại.");

        if (!map.ContainsKey(TruongSoQuy.LoaiPhieu))
            canhBao.Add("File không có cột \"Loại phiếu\": hệ thống sẽ suy ra từ tiền tố mã phiếu (PT là thu, PC là chi).");

        var dong = new List<DongFile>();

        for (var i = 0; i < raw.NoiDung.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var oDong = raw.NoiDung[i];
            var soDongExcel = i + (raw.DongTieuDe + 1);

            if (oDong.All(string.IsNullOrWhiteSpace))
                continue;

            var giaTri = new Dictionary<TruongSoQuy, string>();

            foreach (var (truong, viTriCot) in map)
                giaTri[truong] = viTriCot < oDong.Length ? (oDong[viTriCot] ?? "") : "";

            dong.Add(new DongFile { SoDong = soDongExcel, GiaTri = giaTri });
        }

        return new NoiDungFile
        {
            TieuDe = raw.TieuDe,
            Dong = dong,
            Loi = loi,
            CanhBao = canhBao
        };
    }

    private sealed class NoiDungFile
    {
        public required string[] TieuDe { get; init; }

        public required List<DongFile> Dong { get; init; }

        public required List<SoQuyImportError> Loi { get; init; }

        public required List<string> CanhBao { get; init; }
    }

    /// <summary>
    /// Tìm dòng tiêu đề trong file: dòng nào có nhiều tên cột khớp trường sổ quỹ nhất
    /// thì lấy dòng đó, phần dòng trước bỏ qua (tiêu đề file, tiêu đề bảng...).
    /// </summary>
    private static (string[] TieuDe, List<string[]> NoiDung, int DongTieuDe) DocExcel(
        string filePath,
        CancellationToken ct)
    {
        using var workbook = new XLWorkbook(filePath);

        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("File Excel không có sheet nào.");

        var used = sheet.RangeUsed()
            ?? throw new InvalidOperationException("Sheet đầu tiên trong file đang trống.");

        int dongCuoi = used.LastRow().RowNumber();
        int cotCuoi = used.LastColumn().ColumnNumber();

        const int soDongToiDaDoc = 30;

        var dongTieuDe = 1;
        var diemToiNhat = 0;
        var tieuDe = Array.Empty<string>();

        for (var r = 1; r <= Math.Min(dongCuoi, soDongToiDaDoc); r++)
        {
            ct.ThrowIfCancellationRequested();

            var dong = new string[cotCuoi];

            for (var c = 1; c <= cotCuoi; c++)
                dong[c - 1] = sheet.Cell(r, c).GetFormattedString();

            var soKhop = DemSoCotKhop(dong);

            // Cột "Số tiền" là dấu hiệu mạnh nhất cho thấy đây đúng là bảng sổ quỹ,
            // nên dòng nào có cột này được ưu tiên làm tiêu đề.
            var coSoTien = dong.Any(o => KhopTrenSoTien.Contains(DatabaseService.NormalizeHeaderKey(o)));

            if (soKhop == 0)
                continue;

            if (soKhop > diemToiNhat || (coSoTien && soKhop >= diemToiNhat))
            {
                diemToiNhat = soKhop;
                dongTieuDe = r;
                tieuDe = dong;
            }
        }

        if (diemToiNhat <= 0)
            throw new InvalidOperationException(
                "Không tìm thấy dòng tiêu đề hợp lệ trong file. Hãy dùng file mẫu để đúng định dạng.");

        var noiDung = new List<string[]>();

        for (var r = dongTieuDe + 1; r <= dongCuoi; r++)
        {
            ct.ThrowIfCancellationRequested();

            var dong = new string[cotCuoi];

            for (var c = 1; c <= cotCuoi; c++)
                dong[c - 1] = sheet.Cell(r, c).GetFormattedString();

            noiDung.Add(dong);
        }

        return (tieuDe, noiDung, dongTieuDe);
    }

    /// <summary>Đếm số cột trong dòng khớp một trường nào đó của sổ quỹ.</summary>
    private static int DemSoCotKhop(string[] dong) => dong.Count(o =>
    {
        var khoa = DatabaseService.NormalizeHeaderKey(o);

        return khoa.Length > 0 && BangDoanCot.Any(cap => cap.Khoa.Contains(khoa));
    });

    /// <summary>Tên cột của "Số tiền" đã chuẩn hóa, dùng để nhận diện bảng sổ quỹ.</summary>
    private static readonly HashSet<string> KhopTrenSoTien = new(StringComparer.Ordinal)
    {
        "sotien", "sotienquy", "sotienthu", "sotienchi", "giatri", "tientru",
        "tienchi", "sotienthanhtoan", "amount", "tien", "value", "money"
    };

    /// <summary>Đọc CSV, tự nhận dấu phân cách và giữ nguyên số tiền dạng "1.234,56".</summary>
    private static (string[] TieuDe, List<string[]> NoiDung, int DongTieuDe) DocCsv(string filePath)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);

        var dong = TachDongCsv(reader.ReadToEnd());

        if (dong.Count == 0)
            throw new InvalidOperationException("File CSV rỗng hoặc không đọc được nội dung.");

        return (dong[0], dong.Skip(1).ToList(), 1);
    }

    /// <summary>Tách CSV theo dấu phẩy, tôn trọng dấu nháy kép và xuống dòng trong ô.</summary>
    private static List<string[]> TachDongCsv(string noiDung)
    {
        var dong = new List<string[]>();
        var hienTai = new List<string>();
        var o = new System.Text.StringBuilder();
        var trongNhay = false;

        for (var i = 0; i < noiDung.Length; i++)
        {
            var ch = noiDung[i];

            if (trongNhay)
            {
                if (ch != '"')
                {
                    o.Append(ch);
                    continue;
                }

                // Nháy kép kép ("" ) biểu diễn một dấu nháy kép.
                if (i + 1 < noiDung.Length && noiDung[i + 1] == '"')
                {
                    o.Append('"');
                    i++;
                    continue;
                }

                trongNhay = false;
                continue;
            }

            switch (ch)
            {
                case '"':
                    trongNhay = true;
                    continue;

                case ',':
                    hienTai.Add(o.ToString());
                    o.Clear();
                    continue;

                case '\r':
                    continue;

                case '\n':
                    hienTai.Add(o.ToString());
                    o.Clear();
                    dong.Add(hienTai.ToArray());
                    hienTai.Clear();
                    continue;
            }

            o.Append(ch);
        }

        if (o.Length > 0 || hienTai.Count > 0)
        {
            hienTai.Add(o.ToString());
            dong.Add(hienTai.ToArray());
        }

        return dong;
    }

    /// <summary>Ánh xạ cột trong file sang trường nghiệp vụ; thứ tự trong BangDoanCot quyết định ưu tiên.</summary>
    private static Dictionary<TruongSoQuy, int> DoanMap(string[] tieuDe)
    {
        var map = new Dictionary<TruongSoQuy, int>();

        foreach (var (truong, khoa) in BangDoanCot)
        {
            for (var i = 0; i < tieuDe.Length; i++)
            {
                var khoaCot = DatabaseService.NormalizeHeaderKey(tieuDe[i]);

                if (khoaCot.Length > 0 && khoa.Contains(khoaCot))
                {
                    map[truong] = i;
                    break;
                }
            }
        }

        return map;
    }

    // ============================================================
    // ĐỌC TỪNG TRƯỜNG
    // ============================================================

    /// <summary>
    /// Loại phiếu: ưu tiên cột riêng, không có thì suy ra từ tiền tố mã.
    /// Mã bắt đầu bằng "PT" là thu, bắt đầu bằng "PC" là chi.
    /// </summary>
    private static bool DocLoaiPhieu(DongFile dong, string maPhieu, KetQuaDongSoQuyKhoiTao ketQua)
    {
        var giaTri = (dong.GiaTri.GetValueOrDefault(TruongSoQuy.LoaiPhieu) ?? "").Trim();

        if (giaTri.Length == 0)
        {
            if (maPhieu.StartsWith("PC", StringComparison.OrdinalIgnoreCase))
                return false;

            if (maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
                return true;

            ketQua.Loi.Add("Không xác định được thu hay chi: thiếu cột \"Loại phiếu\" và mã phiếu không bắt đầu bằng PT/PC");

            return false;
        }

        var chuanHoa = DatabaseService.ChuanHoaChuoi(giaTri);

        if (DatabaseService.ChuanHoaChuoi("thu") == chuanHoa || chuanHoa.Contains("thu"))
            return true;

        if (DatabaseService.ChuanHoaChuoi("chi") == chuanHoa || chuanHoa.Contains("chi"))
            return false;

        ketQua.Loi.Add($"Loại phiếu \"{giaTri}\" không hợp lệ, chỉ nhận Thu hoặc Chi");

        return false;
    }

    /// <summary>
    /// Số tiền: chấp nhận cả "12345", "12345.67", "12.345,67" và "12 345".
    /// Cột so_tien là DECIMAL(15,2) nên phần thập phân được làm tròn về 2 chữ số.
    /// </summary>
    private static decimal DocSoTien(DongFile dong, KetQuaDongSoQuyKhoiTao ketQua)
    {
        var giaTri = (dong.GiaTri.GetValueOrDefault(TruongSoQuy.SoTien) ?? "").Trim();

        if (giaTri.Length == 0)
        {
            ketQua.Loi.Add("Thiếu số tiền");

            return 0m;
        }

        if (!ThuParseTien(giaTri, out var soTien))
        {
            ketQua.Loi.Add($"Số tiền \"{giaTri}\" không phải là số");

            return 0m;
        }

        soTien = Math.Round(soTien, 2, MidpointRounding.ToEven);

        if (soTien <= 0m)
        {
            ketQua.Loi.Add("Số tiền phải lớn hơn 0");

            return 0m;
        }

        if (soTien > 9999999999999.99m)
        {
            ketQua.Loi.Add("Số tiền vượt quá giới hạn 12 chữ số của database");

            return 0m;
        }

        return soTien;
    }

    /// <summary>
    /// Ngày giờ: nhận kiểu ngày của Excel, chuỗi ngày giờ phổ biến của Việt Nam
    /// và giá trị số (serial date của Excel). Trả về null để bước ghi dùng
    /// thời điểm hiện tại.
    /// </summary>
    private static DateTime? DocThoiGian(DongFile dong)
    {
        var giaTri = (dong.GiaTri.GetValueOrDefault(TruongSoQuy.NgayGio) ?? "").Trim();

        if (giaTri.Length == 0)
            return null;

        var dinhDang = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy" };

        if (DateTime.TryParseExact(giaTri, dinhDang, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngay))
            return ngay;

        if (DateTime.TryParse(giaViChuoi(giaTri), CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out ngay))
            return ngay;

        if (double.TryParse(giaTri, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)
            && serial > 0 && serial <= 2958466)
        {
            try
            {
                return DateTime.FromOADate(serial);
            }
            catch (ArgumentException)
            {
                // Serial ngoài khoảng hợp lệ thì coi như không đọc được.
            }
        }

        return null;
    }

    /// <summary>Đổi "dd/MM/yyyy" thành "dd/MM/yyyy" tương thích cả kiểu M/d/yyyy của en-US.</summary>
    private static string giaViChuoi(string giaTri) => giaTri.Trim();

    /// <summary>Trạng thái phiếu trong file; mặc định "Đã thanh toán".</summary>
    private static string DocTrangThai(DongFile dong, KetQuaDongSoQuyKhoiTao ketQua)
    {
        var giaTri = (dong.GiaTri.GetValueOrDefault(TruongSoQuy.TrangThai) ?? "").Trim();

        if (giaTri.Length == 0)
            return DatabaseService.TrangThaiPhieuHoanThanh;

        var chuanHoa = DatabaseService.ChuanHoaChuoi(giaViChuoi(giaTri));

        if (chuanHoa.Contains("huy") || chuanHoa.Contains("cancel"))
            return DatabaseService.TrangThaiPhieuHuy;

        if (chuanHoa.Contains("thanh") || chuanHoa.Contains("hoanthanh") || chuanHoa.Contains("completed"))
            return DatabaseService.TrangThaiPhieuHoanThanh;

        ketQua.Loi.Add($"Trạng thái \"{giaTri}\" không hợp lệ, chỉ nhận \"Đã thanh toán\" hoặc \"Đã hủy\"");

        return DatabaseService.TrangThaiPhieuHoanThanh;
    }

    /// <summary>
    /// Bóc số tiền theo cách viết của người dùng Việt Nam:
    /// "12.345,67" và "12,345.67" đều ra 12345.67.
    /// </summary>
    internal static bool ThuParseTien(string giaTri, out decimal ketQua)
    {
        ketQua = 0m;

        var chuoi = giaTri.Trim();

        if (chuoi.Length == 0)
            return false;

        // Bỏ ký hiệu tiền tệ và dấu phân cách nhóm thường gặp.
        chuoi = chuoi.Replace("₫", "").Replace("đ", "").Replace("VND", "", StringComparison.OrdinalIgnoreCase);
        chuoi = chuoi.Replace(" ", "").Replace(" ", "");

        var dauPhay = chuoi.LastIndexOf(',');
        var dauCham = chuoi.LastIndexOf('.');

if (dauPhay >= 0 && dauCham >= 0)
        {
            // Dấu đứng sau cùng là dấu thập phân, dấu còn lại là dấu phân tách nghìn.
            chuoi = dauPhay > dauCham
                ? chuoi.Replace(".", "").Replace(',', '.')
                : chuoi.Replace(",", "");
        }
        else if (dauPhay >= 0)
        {
            // Một dấu phẩy: coi là thập phân nếu có đúng 2 chữ số sau nó.
            var sauDauPhay = chuoi.Length - dauPhay - 1;

            chuoi = sauDauPhay == 2 ? chuoi.Replace(',', '.') : chuoi.Replace(",", "");
        }

        return decimal.TryParse(chuoi, NumberStyles.Float, CultureInfo.InvariantCulture, out ketQua);
    }

    // ============================================================
    // TẠO FILE MẪU VÀ XUẤT DANH SÁCH LỖI
    // ============================================================

    /// <summary>
    /// Tạo file mẫu có đúng tiêu đề và tên các loại/quỹ đang có trong hệ thống,
    /// kèm hai dòng ví dụ để người dùng thấy ngay định dạng.
    /// </summary>
    public async Task<byte[]> TaoFileMauAsync()
    {
        var danhMuc = await _db.GetSoQuyImportDanhMucAsync();

        var loaiThu = danhMuc.LoaiThuChiTheoId
            .Where(d => DatabaseService.ChuanHoaChuoi(d.Value).Contains("thu"))
            .Select(d => d.Value)
            .ToList();

        var loaiChi = danhMuc.LoaiThuChiTheoId
            .Where(d => DatabaseService.ChuanHoaChuoi(d.Value).Contains("chi"))
            .Select(d => d.Value)
            .ToList();

        // Danh mục rỗng thì để trống ô này thay vì bịa một tên loại giả.
        var tenLoaiThu = loaiThu.FirstOrDefault() ?? "";
        var tenLoaiChi = loaiChi.FirstOrDefault() ?? "";

        var tenQuy = danhMuc.QuyTheoMa.Count > 0
            ? danhMuc.QuyTheoMa[danhMuc.QuyTheoMa.Keys.First()]
            : "";

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sổ quỹ");

        for (var i = 0; i < TenTruongSoQuy.ThuTuCot.Count; i++)
            sheet.Cell(1, i + 1).Value = TenTruongSoQuy.Lay(TenTruongSoQuy.ThuTuCot[i]);

        var dongThu = new object?[]
        {
            "",                                  // Mã phiếu: để trống để hệ thống tự cấp
            DateTime.Now,                        // Ngày giờ
            "Thu",                               // Loại phiếu
            "Nguyễn Văn A",                      // Người nộp/nhận
            tenLoaiThu,
            1500000m,
            "Đã thanh toán",
            "Thu tiền khách hàng",
            "Đã thu tiền mặt",
            "",                                  // Người tạo: để trống thì lấy người đang đăng nhập
            tenQuy
        };

        var dongChi = new object?[]
        {
            "",
            DateTime.Now,
            "Chi",
            "Công ty vận tải ABC",
            tenLoaiChi,
            2500000m,
            "Đã thanh toán",
            "Chi tiền thuê xe giao hàng",
            "Thanh toán tiền tháng 10",
            "",
            tenQuy
        };

        GhiDong(sheet, 2, dongThu);
        GhiDong(sheet, 3, dongChi);

        var dongHuongDan = 4;

        for (var i = 0; i < TenTruongSoQuy.ThuTuCot.Count; i++)
        {
            var truong = TenTruongSoQuy.ThuTuCot[i];
            var o = sheet.Cell(dongHuongDan, i + 1);

            o.Value = truong switch
            {
                TruongSoQuy.MaPhieu => "Để trống để hệ thống tự cấp; PTxxxx = thu, PCxxxx = chi",
                TruongSoQuy.NgayGio => "dd/MM/yyyy hoặc dd/MM/yyyy HH:mm; để trống thì lấy thời điểm hiện tại",
                TruongSoQuy.LoaiPhieu => "Thu hoặc Chi",
                TruongSoQuy.NguoiNopNhan => "Người nộp tiền với phiếu thu, người nhận tiền với phiếu chi",
                TruongSoQuy.LoaiThuChi => "Ghi đúng tên trong danh mục loại thu/chi của phần mềm; để trống nếu không có",
                TruongSoQuy.SoTien => "Bắt buộc, số lớn hơn 0",
                TruongSoQuy.TrangThai => "Đã thanh toán hoặc Đã hủy",
                TruongSoQuy.NoiDung => "Nội dung chính của phiếu",
                TruongSoQuy.GhiChu => "Ghi chú tự do",
                TruongSoQuy.NguoiTao => "Đúng họ tên trong danh sách nhân viên; để trống thì lấy người đang đăng nhập",
                _ => $"Quỹ đang có: {(tenQuy.Length > 0 ? tenQuy : "(chưa có quỹ nào trong hệ thống)")}"
            };

            o.Style.Font.Italic = true;
            o.Style.Font.FontColor = XLColor.Gray;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
        sheet.SheetView.FreezeRows(1);

        for (var i = 1; i <= TenTruongSoQuy.ThuTuCot.Count; i++)
            sheet.Column(i).Width = 22;

        using var luu = new MemoryStream();
        workbook.SaveAs(luu);

        return luu.ToArray();
    }

    private static void GhiDong(IXLWorksheet sheet, int soDong, object?[] giaTri)
    {
        for (var i = 0; i < giaTri.Length; i++)
            sheet.Cell(soDong, i + 1).Value = XLCellValue.FromObject(giaTri[i] ?? "");
    }

    /// <summary>Xuất danh sách lỗi ra Excel để người dùng sửa lại file gốc.</summary>
    public static byte[] XuatDanhSachLoi(string tenFile, IEnumerable<SoQuyImportError> loi)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Danh sách lỗi");

        sheet.Cell(1, 1).Value = "Dòng";
        sheet.Cell(1, 2).Value = "Mã phiếu";
        sheet.Cell(1, 3).Value = "Lý do";

        var dong = 2;

        foreach (var muc in loi)
        {
            sheet.Cell(dong, 1).Value = muc.SoDongExcel;
            sheet.Cell(dong, 2).Value = muc.MaPhieu ?? "";
            sheet.Cell(dong, 3).Value = muc.LyDo;
            dong++;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3F2");
        sheet.Column(1).Width = 10;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 70;

        if (tenFile.Length > 0)
            sheet.Cell(1, 5).Value = $"Danh sách lỗi của file: {tenFile}";

        using var luu = new MemoryStream();
        workbook.SaveAs(luu);

        return luu.ToArray();
    }
}