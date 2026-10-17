using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public enum TrangThaiDongExcel
{
    HopLe,      // Hợp lệ, sẵn sàng thêm mới
    TrungLap,   // Trùng mã hoặc barcode
    Loi         // Dữ liệu lỗi (thiếu mã/tên, số âm, v.v.)
}

public enum CheDoXuLyTrung
{
    BoQua,      // Bỏ qua dòng trùng, chỉ thêm các dòng mới
    CapNhat,    // Cập nhật thông tin hàng cũ (bảo vệ tồn kho/lịch sử)
    Huy         // Hủy thao tác nếu có dòng trùng
}

public class HangHoaExcelRowItem : INotifyPropertyChanged
{
    public int SoDongExcel { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? MaVach { get; set; }
    public string? NhomSanPham { get; set; }
    public string? DonViTinh { get; set; }
    public decimal GiaVon { get; set; }
    public decimal GiaBan { get; set; }
    public decimal GiaSi { get; set; }
    public decimal TonKho { get; set; }
    public string? QuyCach { get; set; }
    public string? GhiChu { get; set; }

    public TrangThaiDongExcel TrangThai { get; set; } = TrangThaiDongExcel.HopLe;
    public string ChiTietTrangThai { get; set; } = "Hợp lệ";
    public uint? ExistingProductId { get; set; }

    public string TrangThaiText => TrangThai switch
    {
        TrangThaiDongExcel.HopLe => "✅ Hợp lệ",
        TrangThaiDongExcel.TrungLap => "⚠️ Trùng lặp",
        _ => "❌ Lỗi"
    };

    public string BadgeBackground => TrangThai switch
    {
        TrangThaiDongExcel.HopLe => "#E8F5E9",
        TrangThaiDongExcel.TrungLap => "#FFF3E0",
        _ => "#FFEBEE"
    };

    public string BadgeForeground => TrangThai switch
    {
        TrangThaiDongExcel.HopLe => "#2E7D32",
        TrangThaiDongExcel.TrungLap => "#D97706",
        _ => "#DC2626"
    };

    public bool IsHopLe => TrangThai == TrangThaiDongExcel.HopLe;
    public bool IsTrungLap => TrangThai == TrangThaiDongExcel.TrungLap;
    public bool IsLoi => TrangThai == TrangThaiDongExcel.Loi;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class NhapHangHoaExcelWindow : Window, INotifyPropertyChanged
{
    private readonly DatabaseService _db = new();
    private string? _selectedFilePath;
    private readonly List<HangHoaExcelRowItem> _tatCaRows = new();

    public ObservableCollection<HangHoaExcelRowItem> DanhSachHienThi { get; } = new();
    public ObservableCollection<string> DanhSachSheets { get; } = new();

    public bool DaLuuThanhCong { get; private set; }
    public bool DaLuuTrucTiep { get; private set; } = true;
    public string ThongBaoKetQua { get; private set; } = "";
    public int SoDongThemMoi { get; private set; }
    public int SoDongCapNhat { get; private set; }
    public int SoDongBoQua { get; private set; }
    public List<SanPham> KetQuaSanPham { get; private set; } = new();

    private string _tenFileHienThi = "Chưa có file nào được chọn";
    public string TenFileHienThi
    {
        get => _tenFileHienThi;
        set { _tenFileHienThi = value; OnPropertyChanged(nameof(TenFileHienThi)); }
    }

    private string _dungLuongFileHienThi = "";
    public string DungLuongFileHienThi
    {
        get => _dungLuongFileHienThi;
        set { _dungLuongFileHienThi = value; OnPropertyChanged(nameof(DungLuongFileHienThi)); }
    }

    private string _duongDanFileHienThi = "Hỗ trợ định dạng: Microsoft Excel (.xlsx, .xls). Dòng đầu tiên là tiêu đề cột.";
    public string DuongDanFileHienThi
    {
        get => _duongDanFileHienThi;
        set { _duongDanFileHienThi = value; OnPropertyChanged(nameof(DuongDanFileHienThi)); }
    }

    private bool _daChonFile;
    public bool DaChonFile
    {
        get => _daChonFile;
        set { _daChonFile = value; OnPropertyChanged(nameof(DaChonFile)); }
    }

    private string? _sheetDangChon;
    public string? SheetDangChon
    {
        get => _sheetDangChon;
        set { _sheetDangChon = value; OnPropertyChanged(nameof(SheetDangChon)); }
    }

    private int _tongSoDong;
    public int TongSoDong
    {
        get => _tongSoDong;
        set
        {
            _tongSoDong = value;
            OnPropertyChanged(nameof(TongSoDong));
            OnPropertyChanged(nameof(TongSoDongText));
            OnPropertyChanged(nameof(FilterTatCaText));
            CapNhatNutNhap();
        }
    }

    private int _soDongHopLe;
    public int SoDongHopLe
    {
        get => _soDongHopLe;
        set
        {
            _soDongHopLe = value;
            OnPropertyChanged(nameof(SoDongHopLe));
            OnPropertyChanged(nameof(SoDongHopLeText));
            OnPropertyChanged(nameof(FilterHopLeText));
            CapNhatNutNhap();
        }
    }

    private int _soDongTrung;
    public int SoDongTrung
    {
        get => _soDongTrung;
        set
        {
            _soDongTrung = value;
            OnPropertyChanged(nameof(SoDongTrung));
            OnPropertyChanged(nameof(SoDongTrungText));
            OnPropertyChanged(nameof(FilterTrungText));
            CapNhatNutNhap();
        }
    }

    private int _soDongLoi;
    public int SoDongLoi
    {
        get => _soDongLoi;
        set
        {
            _soDongLoi = value;
            OnPropertyChanged(nameof(SoDongLoi));
            OnPropertyChanged(nameof(SoDongLoiText));
            OnPropertyChanged(nameof(FilterLoiText));
        }
    }

    public string TongSoDongText => TongSoDong.ToString("N0");
    public string SoDongHopLeText => SoDongHopLe.ToString("N0");
    public string SoDongTrungText => SoDongTrung.ToString("N0");
    public string SoDongLoiText => SoDongLoi.ToString("N0");

    public string FilterTatCaText => $"Tất cả ({TongSoDong})";
    public string FilterHopLeText => $"Hợp lệ ({SoDongHopLe})";
    public string FilterTrungText => $"Trùng lặp ({SoDongTrung})";
    public string FilterLoiText => $"Có lỗi ({SoDongLoi})";

    private string _soLuongHienThiText = "";
    public string SoLuongHienThiText
    {
        get => _soLuongHienThiText;
        set { _soLuongHienThiText = value; OnPropertyChanged(nameof(SoLuongHienThiText)); }
    }

    private string _cheDoLocHienThi = "TAT_CA";

    private CheDoXuLyTrung _cheDoXuLy = CheDoXuLyTrung.BoQua;
    public CheDoXuLyTrung CheDoXuLy
    {
        get => _cheDoXuLy;
        set
        {
            _cheDoXuLy = value;
            OnPropertyChanged(nameof(CheDoXuLy));
            OnPropertyChanged(nameof(IsXuLyBoQua));
            OnPropertyChanged(nameof(IsXuLyCapNhat));
            OnPropertyChanged(nameof(IsXuLyHuy));
            CapNhatNutNhap();
        }
    }

    public bool IsXuLyBoQua
    {
        get => CheDoXuLy == CheDoXuLyTrung.BoQua;
        set { if (value) CheDoXuLy = CheDoXuLyTrung.BoQua; }
    }

    public bool IsXuLyCapNhat
    {
        get => CheDoXuLy == CheDoXuLyTrung.CapNhat;
        set { if (value) CheDoXuLy = CheDoXuLyTrung.CapNhat; }
    }

    public bool IsXuLyHuy
    {
        get => CheDoXuLy == CheDoXuLyTrung.Huy;
        set { if (value) CheDoXuLy = CheDoXuLyTrung.Huy; }
    }

    private bool _dangXuLy;
    public bool DangXuLy
    {
        get => _dangXuLy;
        set
        {
            _dangXuLy = value;
            OnPropertyChanged(nameof(DangXuLy));
            CapNhatNutNhap();
        }
    }

    private string _thongBaoTrangThai = "Sẵn sàng chọn file dữ liệu.";
    public string ThongBaoTrangThai
    {
        get => _thongBaoTrangThai;
        set { _thongBaoTrangThai = value; OnPropertyChanged(nameof(ThongBaoTrangThai)); }
    }

    private IBrush _mauThongBaoTrangThai = Brushes.SlateGray;
    public IBrush MauThongBaoTrangThai
    {
        get => _mauThongBaoTrangThai;
        set { _mauThongBaoTrangThai = value; OnPropertyChanged(nameof(MauThongBaoTrangThai)); }
    }

    private string _nutNhapDuLieuText = "💾  Nhập hàng hóa";
    public string NutNhapDuLieuText
    {
        get => _nutNhapDuLieuText;
        set { _nutNhapDuLieuText = value; OnPropertyChanged(nameof(NutNhapDuLieuText)); }
    }

    private bool _coTheNhapDuLieu;
    public bool CoTheNhapDuLieu
    {
        get => _coTheNhapDuLieu;
        set { _coTheNhapDuLieu = value; OnPropertyChanged(nameof(CoTheNhapDuLieu)); }
    }

    public NhapHangHoaExcelWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void CapNhatNutNhap()
    {
        if (DangXuLy)
        {
            CoTheNhapDuLieu = false;
            NutNhapDuLieuText = "⏳  Đang xử lý...";
            return;
        }

        if (CheDoXuLy == CheDoXuLyTrung.Huy && SoDongTrung > 0)
        {
            CoTheNhapDuLieu = false;
            NutNhapDuLieuText = "⚠️  Không thể nhập (Có trùng)";
            return;
        }

        int soLuongSeNhap = SoDongHopLe + (CheDoXuLy == CheDoXuLyTrung.CapNhat ? SoDongTrung : 0);
        CoTheNhapDuLieu = soLuongSeNhap > 0;
        NutNhapDuLieuText = soLuongSeNhap > 0
            ? $"💾  Nhập {soLuongSeNhap:N0} hàng hóa"
            : "💾  Nhập hàng hóa";
    }

    // =========================================================
    // 1. CHỌN FILE VÀ ĐỌC SHEET
    // =========================================================
    private async void ChonFile_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Chọn file danh sách hàng hóa Excel",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Tập tin Excel (*.xlsx, *.xls)")
                {
                    Patterns = new[] { "*.xlsx", "*.xls" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        _selectedFilePath = file.Path.LocalPath;
        if (!File.Exists(_selectedFilePath))
        {
            DatThongBao("Không tìm thấy tệp đã chọn trên ổ đĩa.", Brushes.IndianRed);
            return;
        }

        var fi = new FileInfo(_selectedFilePath);
        TenFileHienThi = fi.Name;
        DuongDanFileHienThi = fi.FullName;

        long bytes = fi.Length;
        DungLuongFileHienThi = bytes switch
        {
            >= 1024 * 1024 => $"{(bytes / (1024.0 * 1024.0)):F1} MB",
            >= 1024 => $"{(bytes / 1024.0):F1} KB",
            _ => $"{bytes} B"
        };

        DaChonFile = true;

        try
        {
            using var wb = new XLWorkbook(_selectedFilePath);
            DanhSachSheets.Clear();

            foreach (var ws in wb.Worksheets)
            {
                DanhSachSheets.Add(ws.Name);
            }

            if (DanhSachSheets.Count > 0)
            {
                SheetDangChon = DanhSachSheets[0];
                await DocVaKiemTraDuLieuAsync(SheetDangChon);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi đọc file Excel", ex);
            DatThongBao("Không thể mở file Excel: " + ex.Message, Brushes.IndianRed);
        }
    }

    private void BoChonFile_OnClick(object? sender, RoutedEventArgs e)
    {
        _selectedFilePath = null;
        TenFileHienThi = "Chưa có file nào được chọn";
        DuongDanFileHienThi = "Hỗ trợ định dạng: Microsoft Excel (.xlsx, .xls). Dòng đầu tiên là tiêu đề cột.";
        DungLuongFileHienThi = "";
        DaChonFile = false;
        DanhSachSheets.Clear();
        SheetDangChon = null;

        _tatCaRows.Clear();
        DanhSachHienThi.Clear();
        TongSoDong = 0;
        SoDongHopLe = 0;
        SoDongTrung = 0;
        SoDongLoi = 0;
        SoLuongHienThiText = "";

        DatThongBao("Đã bỏ chọn file. Sẵn sàng chọn file khác.", Brushes.SlateGray);
    }

    private async void ChonSheetComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath) || !File.Exists(_selectedFilePath))
            return;

        if (sender is ComboBox cb && cb.SelectedItem is string sheetName && !string.IsNullOrWhiteSpace(sheetName))
        {
            await DocVaKiemTraDuLieuAsync(sheetName);
        }
    }

    private async void KiemTraLai_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SheetDangChon))
        {
            await DocVaKiemTraDuLieuAsync(SheetDangChon);
        }
    }

    // =========================================================
    // 2. ĐỌC DỮ LIỆU TỪ SHEET VÀ KIỂM TRA TOÀN VẸN
    // =========================================================
    private async Task DocVaKiemTraDuLieuAsync(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath) || !File.Exists(_selectedFilePath))
            return;

        DangXuLy = true;
        DatThongBao($"Đang đọc và kiểm tra dữ liệu từ sheet '{sheetName}'...", Brushes.DarkCyan);

        _tatCaRows.Clear();
        DanhSachHienThi.Clear();

        try
        {
            // 1. Tải danh sách hàng hóa đang có trong database để đối chiếu trùng
            var dbProducts = await LoadDatabaseProductsAsync();
            var dbByCode = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);
            var dbByBarcode = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in dbProducts)
            {
                var code = p.MaSanPham?.Trim();
                if (!string.IsNullOrWhiteSpace(code) && !dbByCode.ContainsKey(code))
                    dbByCode[code] = p;

                var bc = p.MaVach?.Trim();
                if (!string.IsNullOrWhiteSpace(bc) && !dbByBarcode.ContainsKey(bc))
                    dbByBarcode[bc] = p;
            }

            // 2. Mở file Excel đọc bằng ClosedXML
            using var wb = new XLWorkbook(_selectedFilePath);
            var ws = wb.Worksheet(sheetName);
            if (ws is null)
            {
                DatThongBao($"Không tìm thấy bảng tính '{sheetName}'.", Brushes.IndianRed);
                return;
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2)
            {
                DatThongBao("Bảng tính này không có dữ liệu hàng hóa (hoặc chỉ có 1 dòng tiêu đề).", Brushes.DarkOrange);
                TongSoDong = 0;
                SoDongHopLe = 0;
                SoDongTrung = 0;
                SoDongLoi = 0;
                return;
            }

            // 3. Phân tích tiêu đề cột (Header Mapping)
            var colMap = XacDinhViTriCot(ws);

            var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenBarcodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 4. Duyệt từng dòng dữ liệu từ dòng startRow đến lastRow
            int startRow = colMap.StartRow;
            for (int r = startRow; r <= lastRow; r++)
            {
                var row = ws.Row(r);

                // Lấy các giá trị chuỗi an toàn
                string maHang = DocCell(row, colMap.ColMaHang);
                string tenHang = DocCell(row, colMap.ColTenHang);
                string maVach = DocCell(row, colMap.ColMaVach);
                string nhomHang = DocCell(row, colMap.ColNhomHang);
                string dvt = DocCell(row, colMap.ColDvt);
                string quycach = DocCell(row, colMap.ColQuyCach);
                string ghichu = DocCell(row, colMap.ColGhiChu);

                // Nếu cả mã và tên đều rỗng và không có cột nào có giá trị -> bỏ qua dòng trống
                if (string.IsNullOrWhiteSpace(maHang) && string.IsNullOrWhiteSpace(tenHang) && string.IsNullOrWhiteSpace(nhomHang))
                    continue;

                // Parse số an toàn
                bool parseGiaVonOk = TryParseMoney(DocCell(row, colMap.ColGiaVon), out decimal giaVon);
                bool parseGiaBanOk = TryParseMoney(DocCell(row, colMap.ColGiaBan), out decimal giaBan);
                bool parseGiaSiOk = TryParseMoney(DocCell(row, colMap.ColGiaSi), out decimal giaSi);
                bool parseTonKhoOk = TryParseMoney(DocCell(row, colMap.ColTonKho), out decimal tonKho);

                var item = new HangHoaExcelRowItem
                {
                    SoDongExcel = r,
                    MaSanPham = maHang,
                    TenSanPham = tenHang,
                    MaVach = maVach,
                    NhomSanPham = nhomHang,
                    DonViTinh = dvt,
                    GiaVon = giaVon,
                    GiaBan = giaBan,
                    GiaSi = giaSi,
                    TonKho = tonKho,
                    QuyCach = quycach,
                    GhiChu = ghichu
                };

                // 5. Kiểm tra tính hợp lệ nghiệp vụ
                var loiList = new List<string>();

                if (string.IsNullOrWhiteSpace(maHang))
                    loiList.Add("Thiếu mã hàng hóa (bắt buộc)");

                if (string.IsNullOrWhiteSpace(tenHang))
                    loiList.Add("Thiếu tên hàng hóa (bắt buộc)");

                if (!parseGiaVonOk || giaVon < 0)
                    loiList.Add("Giá vốn không hợp lệ hoặc âm");

                if (!parseGiaBanOk || giaBan < 0)
                    loiList.Add("Giá bán không hợp lệ hoặc âm");

                if (!parseTonKhoOk || tonKho < 0)
                    loiList.Add("Tồn kho không hợp lệ hoặc âm");

                if (loiList.Count > 0)
                {
                    item.TrangThai = TrangThaiDongExcel.Loi;
                    item.ChiTietTrangThai = string.Join("; ", loiList);
                }
                else
                {
                    // Kiểm tra trùng lặp
                    var trungList = new List<string>();

                    // a. Trùng trong nội bộ file
                    if (seenCodesInFile.Contains(maHang))
                    {
                        trungList.Add($"Mã hàng '{maHang}' bị trùng lặp ở dòng trước trong file");
                    }
                    else
                    {
                        seenCodesInFile.Add(maHang);
                    }

                    if (!string.IsNullOrWhiteSpace(maVach))
                    {
                        if (seenBarcodesInFile.Contains(maVach))
                        {
                            trungList.Add($"Mã vạch '{maVach}' bị trùng lặp ở dòng trước trong file");
                        }
                        else
                        {
                            seenBarcodesInFile.Add(maVach);
                        }
                    }

                    // b. Trùng với dữ liệu trong database
                    if (dbByCode.TryGetValue(maHang, out var existingCode))
                    {
                        item.ExistingProductId = existingCode.Id;
                        trungList.Add($"Mã hàng đã có trong phần mềm ({existingCode.TenSanPham})");
                    }
                    else if (!string.IsNullOrWhiteSpace(maVach) && dbByBarcode.TryGetValue(maVach, out var existingBarcode))
                    {
                        item.ExistingProductId = existingBarcode.Id;
                        trungList.Add($"Mã vạch đã được dùng cho '{existingBarcode.TenSanPham}' ({existingBarcode.MaSanPham})");
                    }

                    if (trungList.Count > 0)
                    {
                        item.TrangThai = TrangThaiDongExcel.TrungLap;
                        item.ChiTietTrangThai = string.Join("; ", trungList);
                    }
                    else
                    {
                        item.TrangThai = TrangThaiDongExcel.HopLe;
                        item.ChiTietTrangThai = "Hợp lệ - sẵn sàng nhập";
                    }
                }

                _tatCaRows.Add(item);
            }

            // 6. Tính toán các chỉ số thống kê thực tế
            TongSoDong = _tatCaRows.Count;
            SoDongHopLe = _tatCaRows.Count(r => r.IsHopLe);
            SoDongTrung = _tatCaRows.Count(r => r.IsTrungLap);
            SoDongLoi = _tatCaRows.Count(r => r.IsLoi);

            ApDungBoLocHienThi();

            if (TongSoDong == 0)
            {
                DatThongBao("Không tìm thấy dòng dữ liệu hàng hóa nào hợp lệ để đọc.", Brushes.DarkOrange);
            }
            else
            {
                var msg = $"Đã kiểm tra xong {TongSoDong:N0} dòng: {SoDongHopLe:N0} hợp lệ";
                if (SoDongTrung > 0) msg += $", {SoDongTrung:N0} trùng lặp";
                if (SoDongLoi > 0) msg += $", {SoDongLoi:N0} có lỗi";
                msg += ".";
                DatThongBao(msg, Brushes.SeaGreen);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi kiểm tra file Excel", ex);
            DatThongBao("Lỗi khi phân tích dữ liệu Excel: " + ex.Message, Brushes.IndianRed);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    private static string DocCell(IXLRow row, int colIndex)
    {
        if (colIndex <= 0) return "";
        try
        {
            return row.Cell(colIndex).GetString().Trim();
        }
        catch
        {
            return "";
        }
    }

    private static bool TryParseMoney(string text, out decimal val)
    {
        val = 0;
        if (string.IsNullOrWhiteSpace(text)) return true; // để trống hiểu là 0

        var clean = text.Replace(".", "").Replace(",", ".").Replace(" ", "").Replace("đ", "").Replace("VND", "");
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            val = parsed;
            return true;
        }

        // Thử theo Culture vi-VN
        if (decimal.TryParse(clean, NumberStyles.Any, new CultureInfo("vi-VN"), out var parsedVi))
        {
            val = parsedVi;
            return true;
        }

        return false;
    }

    private sealed record ColumnMapping
    {
        public int StartRow { get; init; } = 2;
        public int ColMaHang { get; init; } = 3;
        public int ColTenHang { get; init; } = 4;
        public int ColMaVach { get; init; } = 5;
        public int ColNhomHang { get; init; } = 2;
        public int ColDvt { get; init; } = 6;
        public int ColGiaVon { get; init; } = 7;
        public int ColGiaBan { get; init; } = 8;
        public int ColGiaSi { get; init; } = 9;
        public int ColTonKho { get; init; } = 10;
        public int ColQuyCach { get; init; } = 11;
        public int ColGhiChu { get; init; } = 15;
    }

    private static ColumnMapping XacDinhViTriCot(IXLWorksheet ws)
    {
        // Duyệt hàng 1 để tìm tiêu đề
        var headerRow = ws.Row(1);
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 15;

        int colMaHang = 0, colTenHang = 0, colMaVach = 0, colNhomHang = 0;
        int colDvt = 0, colGiaVon = 0, colGiaBan = 0, colGiaSi = 0;
        int colTonKho = 0, colQuyCach = 0, colGhiChu = 0;

        for (int c = 1; c <= lastCol; c++)
        {
            var h = ChuanHoaHeader(headerRow.Cell(c).GetString());
            if (string.IsNullOrWhiteSpace(h)) continue;

            if (colMaHang == 0 && (h.Contains("mahang") || h.Contains("masanpham") || h.Contains("macode") || h == "ma"))
                colMaHang = c;
            else if (colTenHang == 0 && (h.Contains("tenhang") || h.Contains("tensanpham") || h == "ten" || h.Contains("tenhanghoa")))
                colTenHang = c;
            else if (colMaVach == 0 && (h.Contains("mavach") || h.Contains("barcode")))
                colMaVach = c;
            else if (colNhomHang == 0 && (h.Contains("nhomhang") || h.Contains("nhomsanpham") || h.Contains("nhom") || h.Contains("loaihang")))
                colNhomHang = c;
            else if (colDvt == 0 && (h == "dvt" || h.Contains("donvitinh") || h.Contains("dvtgoc")))
                colDvt = c;
            else if (colGiaVon == 0 && (h.Contains("giavon") || h.Contains("gianhap") || h.Contains("giamua")))
                colGiaVon = c;
            else if (colGiaBan == 0 && (h.Contains("giaban") || h.Contains("giale") || h.Contains("dongia") || h == "gia"))
                colGiaBan = c;
            else if (colGiaSi == 0 && (h.Contains("giasi") || h.Contains("giabuon")))
                colGiaSi = c;
            else if (colTonKho == 0 && (h.Contains("tonkho") || h.Contains("soluongton") || h == "ton" || h.Contains("soluong")))
                colTonKho = c;
            else if (colQuyCach == 0 && (h.Contains("quycach") || h.Contains("dacdiem")))
                colQuyCach = c;
            else if (colGhiChu == 0 && (h.Contains("ghichu") || h.Contains("mota")))
                colGhiChu = c;
        }

        // Nếu nhận diện được cả mã hàng và tên hàng
        if (colMaHang > 0 && colTenHang > 0)
        {
            return new ColumnMapping
            {
                StartRow = 2,
                ColMaHang = colMaHang,
                ColTenHang = colTenHang,
                ColMaVach = colMaVach > 0 ? colMaVach : 5,
                ColNhomHang = colNhomHang > 0 ? colNhomHang : 2,
                ColDvt = colDvt > 0 ? colDvt : 6,
                ColGiaVon = colGiaVon > 0 ? colGiaVon : 7,
                ColGiaBan = colGiaBan > 0 ? colGiaBan : 8,
                ColGiaSi = colGiaSi > 0 ? colGiaSi : 9,
                ColTonKho = colTonKho > 0 ? colTonKho : 10,
                ColQuyCach = colQuyCach > 0 ? colQuyCach : 11,
                ColGhiChu = colGhiChu > 0 ? colGhiChu : 15
            };
        }

        // Nếu dòng 1 là tiêu đề biểu mẫu, kiểm tra dòng 2
        var row2 = ws.Row(2);
        for (int c = 1; c <= lastCol; c++)
        {
            var h = ChuanHoaHeader(row2.Cell(c).GetString());
            if (string.IsNullOrWhiteSpace(h)) continue;

            if (colMaHang == 0 && (h.Contains("mahang") || h.Contains("masanpham") || h.Contains("macode")))
                colMaHang = c;
            else if (colTenHang == 0 && (h.Contains("tenhang") || h.Contains("tensanpham") || h.Contains("tenhanghoa")))
                colTenHang = c;
            else if (colMaVach == 0 && (h.Contains("mavach") || h.Contains("barcode")))
                colMaVach = c;
            else if (colNhomHang == 0 && (h.Contains("nhomhang") || h.Contains("nhom")))
                colNhomHang = c;
            else if (colDvt == 0 && (h == "dvt" || h.Contains("donvitinh")))
                colDvt = c;
            else if (colGiaVon == 0 && (h.Contains("giavon") || h.Contains("gianhap")))
                colGiaVon = c;
            else if (colGiaBan == 0 && (h.Contains("giaban") || h.Contains("giale")))
                colGiaBan = c;
            else if (colTonKho == 0 && (h.Contains("tonkho") || h == "ton"))
                colTonKho = c;
        }

        if (colMaHang > 0 && colTenHang > 0)
        {
            return new ColumnMapping
            {
                StartRow = 3,
                ColMaHang = colMaHang,
                ColTenHang = colTenHang,
                ColMaVach = colMaVach > 0 ? colMaVach : 5,
                ColNhomHang = colNhomHang > 0 ? colNhomHang : 2,
                ColDvt = colDvt > 0 ? colDvt : 6,
                ColGiaVon = colGiaVon > 0 ? colGiaVon : 7,
                ColGiaBan = colGiaBan > 0 ? colGiaBan : 8,
                ColGiaSi = colGiaSi > 0 ? colGiaSi : 9,
                ColTonKho = colTonKho > 0 ? colTonKho : 10,
                ColQuyCach = colQuyCach > 0 ? colQuyCach : 11,
                ColGhiChu = colGhiChu > 0 ? colGhiChu : 15
            };
        }

        // Mặc định fallback theo định dạng SalesPro chuẩn
        return new ColumnMapping();
    }

    private static string ChuanHoaHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
            {
                sb.Append(ch == 'đ' ? 'd' : ch);
            }
        }
        return sb.ToString();
    }

    private async Task<IReadOnlyList<SanPham>> LoadDatabaseProductsAsync()
    {
        try
        {
            await using var connection = await _db.OpenAsync();
            const string sql = """
                SELECT id AS Id, ma_san_pham AS MaSanPham, ma_vach AS MaVach, ten_san_pham AS TenSanPham, gia_ban AS GiaBan, gia_von AS GiaVon
                FROM san_pham;
                """;
            return (await connection.QueryAsync<SanPham>(sql)).AsList();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi đọc danh sách sản phẩm để so sánh trùng", ex);
            return Array.Empty<SanPham>();
        }
    }

    // =========================================================
    // 3. BỘ LỌC HIỂN THỊ DANH SÁCH XEM TRƯỚC
    // =========================================================
    private void LocTatCa_OnClick(object? sender, RoutedEventArgs e)
    {
        _cheDoLocHienThi = "TAT_CA";
        ApDungBoLocHienThi();
    }

    private void LocHopLe_OnClick(object? sender, RoutedEventArgs e)
    {
        _cheDoLocHienThi = "HOP_LE";
        ApDungBoLocHienThi();
    }

    private void LocTrung_OnClick(object? sender, RoutedEventArgs e)
    {
        _cheDoLocHienThi = "TRUNG";
        ApDungBoLocHienThi();
    }

    private void LocLoi_OnClick(object? sender, RoutedEventArgs e)
    {
        _cheDoLocHienThi = "LOI";
        ApDungBoLocHienThi();
    }

    private void ApDungBoLocHienThi()
    {
        DanhSachHienThi.Clear();

        var filtered = _cheDoLocHienThi switch
        {
            "HOP_LE" => _tatCaRows.Where(r => r.IsHopLe),
            "TRUNG" => _tatCaRows.Where(r => r.IsTrungLap),
            "LOI" => _tatCaRows.Where(r => r.IsLoi),
            _ => _tatCaRows
        };

        foreach (var item in filtered)
        {
            DanhSachHienThi.Add(item);
        }

        SoLuongHienThiText = $"Đang hiển thị {DanhSachHienThi.Count:N0} / {TongSoDong:N0} dòng";
    }

    // =========================================================
    // 4. XUẤT TẬP TIN MẪU THAM KHẢO
    // =========================================================
    private async void LayMauThamKhao_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lưu file Excel mẫu nhập hàng hóa",
            DefaultExtension = "xlsx",
            SuggestedFileName = "Mau_Nhap_Hang_Hoa_WINWON.xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
            }
        });

        if (file is null) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("HangHoa");

            string[] headers =
            {
                "STT", "Nhóm hàng", "Mã hàng (*)", "Tên hàng hóa (*)", "Mã vạch (Barcode)", "ĐVT",
                "Giá vốn (Giá nhập)", "Giá bán (Giá lẻ)", "Giá sỉ", "Tồn kho ban đầu", "Quy cách", "Ghi chú"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.FromArgb(11, 75, 130);
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(230, 239, 247);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(148, 163, 184);
            }

            // Dòng mẫu 1
            ws.Cell(2, 1).Value = 1;
            ws.Cell(2, 2).Value = "Hoa tươi cắt cành";
            ws.Cell(2, 3).Value = "HH001";
            ws.Cell(2, 4).Value = "Hoa hồng đỏ Đà Lạt loại 1";
            ws.Cell(2, 5).Value = "893000000001";
            ws.Cell(2, 6).Value = "Cành";
            ws.Cell(2, 7).Value = 6000;
            ws.Cell(2, 8).Value = 12000;
            ws.Cell(2, 9).Value = 10000;
            ws.Cell(2, 10).Value = 200;
            ws.Cell(2, 11).Value = "Cành dài 70cm";
            ws.Cell(2, 12).Value = "Hoa tươi nhập hàng ngày";

            // Dòng mẫu 2
            ws.Cell(3, 1).Value = 2;
            ws.Cell(3, 2).Value = "Hoa bó nghệ thuật";
            ws.Cell(3, 3).Value = "HH002";
            ws.Cell(3, 4).Value = "Bó hoa Baby trắng Hà Lan";
            ws.Cell(3, 5).Value = "893000000002";
            ws.Cell(3, 6).Value = "Bó";
            ws.Cell(3, 7).Value = 180000;
            ws.Cell(3, 8).Value = 320000;
            ws.Cell(3, 9).Value = 280000;
            ws.Cell(3, 10).Value = 15;
            ws.Cell(3, 11).Value = "Bó hoa kèm giấy gói lụa cao cấp";
            ws.Cell(3, 12).Value = "Thích hợp tặng sinh nhật";

            // Dòng mẫu 3
            ws.Cell(4, 1).Value = 3;
            ws.Cell(4, 2).Value = "Lan & Hoa chậu";
            ws.Cell(4, 3).Value = "HH003";
            ws.Cell(4, 4).Value = "Chậu Lan Hồ Điệp tím 5 cành";
            ws.Cell(4, 5).Value = "893000000003";
            ws.Cell(4, 6).Value = "Chậu";
            ws.Cell(4, 7).Value = 850000;
            ws.Cell(4, 8).Value = 1450000;
            ws.Cell(4, 9).Value = 1300000;
            ws.Cell(4, 10).Value = 8;
            ws.Cell(4, 11).Value = "Chậu sứ men trắng nhập khẩu";
            ws.Cell(4, 12).Value = "Hoa trưng đại sảnh / khai trương";

            ws.Columns().AdjustToContents();

            await using var stream = await file.OpenWriteAsync();
            wb.SaveAs(stream);

            DatThongBao("✅ Đã xuất tập tin Excel mẫu tham khảo thành công!", Brushes.SeaGreen);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất file mẫu Excel", ex);
            DatThongBao("Không thể lưu file mẫu: " + ex.Message, Brushes.IndianRed);
        }
    }

    // =========================================================
    // 5. THỰC HIỆN LƯU VÀO DATABASE BẰNG TRANSACTION
    // =========================================================
    private async void ThucHienNhap_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_tatCaRows.Count == 0)
        {
            DatThongBao("Chưa có dữ liệu nào được đọc từ file để nhập.", Brushes.IndianRed);
            return;
        }

        if (CheDoXuLy == CheDoXuLyTrung.Huy && SoDongTrung > 0)
        {
            DatThongBao("Phát hiện dữ liệu trùng lặp. Đã cấu hình hủy theo yêu cầu.", Brushes.IndianRed);
            return;
        }

        DangXuLy = true;
        DatThongBao("⏳ Đang lưu dữ liệu hàng hóa vào cơ sở dữ liệu...", Brushes.DarkCyan);

        try
        {
            await using var connection = await _db.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Nạp danh mục nhóm và ĐVT
                var nhomList = (await connection.QueryAsync<(uint Id, string Ten)>(
                    "SELECT id AS Id, ten_nhom AS Ten FROM nhom_san_pham;", transaction: transaction)).ToList();
                var nhomDict = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in nhomList) if (!string.IsNullOrWhiteSpace(n.Ten)) nhomDict[n.Ten.Trim()] = n.Id;

                var dvtList = (await connection.QueryAsync<(uint Id, string Ten)>(
                    "SELECT id AS Id, ten_don_vi AS Ten FROM don_vi_tinh;", transaction: transaction)).ToList();
                var dvtDict = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in dvtList) if (!string.IsNullOrWhiteSpace(d.Ten)) dvtDict[d.Ten.Trim()] = d.Id;

                int soThem = 0;
                int soCapNhat = 0;
                int soBoQua = 0;
                KetQuaSanPham.Clear();

                foreach (var item in _tatCaRows)
                {
                    if (item.IsLoi)
                    {
                        soBoQua++;
                        continue;
                    }

                    if (item.IsTrungLap)
                    {
                        if (CheDoXuLy == CheDoXuLyTrung.BoQua)
                        {
                            soBoQua++;
                            continue;
                        }

                        // Cập nhật hàng cũ
                        if (item.ExistingProductId.HasValue && item.ExistingProductId.Value > 0)
                        {
                            uint? nhomId = ResolveNhomId(connection, transaction, nhomDict, item.NhomSanPham);
                            uint? dvtId = ResolveDvtId(connection, transaction, dvtDict, item.DonViTinh);

                            const string updateSql = """
                                UPDATE san_pham
                                SET ten_san_pham = @TenSanPham,
                                    nhom_san_pham_id = COALESCE(@NhomId, nhom_san_pham_id),
                                    don_vi_tinh_id = COALESCE(@DvtId, don_vi_tinh_id),
                                    gia_ban = @GiaBan,
                                    gia_von = @GiaVon,
                                    ghi_chu = @GhiChu,
                                    ngay_cap_nhat = NOW()
                                WHERE id = @Id;
                                """;

                            await connection.ExecuteAsync(updateSql, new
                            {
                                Id = item.ExistingProductId.Value,
                                TenSanPham = item.TenSanPham,
                                NhomId = nhomId,
                                DvtId = dvtId,
                                GiaBan = item.GiaBan,
                                GiaVon = item.GiaVon,
                                GhiChu = item.GhiChu
                            }, transaction: transaction);

                            soCapNhat++;
                        }
                        continue;
                    }

                    // Thêm sản phẩm mới (INSERT)
                    uint? newNhomId = ResolveNhomId(connection, transaction, nhomDict, item.NhomSanPham);
                    uint? newDvtId = ResolveDvtId(connection, transaction, dvtDict, item.DonViTinh);

                    const string insertSql = """
                        INSERT INTO san_pham
                        (
                            ma_san_pham,
                            ma_vach,
                            ten_san_pham,
                            nhom_san_pham_id,
                            don_vi_tinh_id,
                            gia_ban,
                            gia_von,
                            ton_kho_excel,
                            ghi_chu,
                            co_ban_truc_tiep,
                            trang_thai,
                            ngay_tao
                        )
                        VALUES
                        (
                            @MaSanPham,
                            @MaVach,
                            @TenSanPham,
                            @NhomId,
                            @DvtId,
                            @GiaBan,
                            @GiaVon,
                            @TonKho,
                            @GhiChu,
                            1,
                            1,
                            NOW()
                        );
                        SELECT LAST_INSERT_ID();
                        """;

                    var newId = await connection.ExecuteScalarAsync<uint>(insertSql, new
                    {
                        MaSanPham = item.MaSanPham,
                        MaVach = string.IsNullOrWhiteSpace(item.MaVach) ? null : item.MaVach.Trim(),
                        TenSanPham = item.TenSanPham,
                        NhomId = newNhomId,
                        DvtId = newDvtId,
                        GiaBan = item.GiaBan,
                        GiaVon = item.GiaVon,
                        TonKho = item.TonKho,
                        GhiChu = item.GhiChu
                    }, transaction: transaction);

                    if (newId > 0)
                    {
                        if (item.TonKho > 0)
                        {
                            const string insertTonKho = """
                                INSERT INTO ton_kho (chi_nhanh_id, san_pham_id, so_luong_ton, gia_von_binh_quan, ngay_cap_nhat)
                                VALUES (1, @SanPhamId, @TonKho, @GiaVon, NOW())
                                ON DUPLICATE KEY UPDATE so_luong_ton = @TonKho, ngay_cap_nhat = NOW();
                                """;

                            await connection.ExecuteAsync(insertTonKho, new
                            {
                                SanPhamId = newId,
                                TonKho = item.TonKho,
                                GiaVon = item.GiaVon
                            }, transaction: transaction);

                            try
                            {
                                const string insertLichSu = """
                                    INSERT INTO lich_su_nhap_xuat_kho
                                    (
                                        chi_nhanh_id, san_pham_id, loai_giao_dich, loai_chung_tu,
                                        so_luong, ton_truoc, ton_sau, gia_von, thoi_gian, ghi_chu
                                    )
                                    VALUES
                                    (
                                        1, @SanPhamId, 'NHAP_KHO', 'NHAP_EXCEL',
                                        @TonKho, 0, @TonKho, @GiaVon, NOW(), 'Nhập tồn kho ban đầu từ file Excel'
                                    );
                                    """;
                                await connection.ExecuteAsync(insertLichSu, new
                                {
                                    SanPhamId = newId,
                                    TonKho = item.TonKho,
                                    GiaVon = item.GiaVon
                                }, transaction: transaction);
                            }
                            catch { }
                        }

                        KetQuaSanPham.Add(new SanPham
                        {
                            Id = newId,
                            MaSanPham = item.MaSanPham,
                            TenSanPham = item.TenSanPham,
                            GiaBan = item.GiaBan,
                            GiaVon = item.GiaVon,
                            TonKho = item.TonKho
                        });

                        soThem++;
                    }
                }

                await transaction.CommitAsync();

                DaLuuThanhCong = true;
                SoDongThemMoi = soThem;
                SoDongCapNhat = soCapNhat;
                SoDongBoQua = soBoQua;
                ThongBaoKetQua = $"Nhập Excel hoàn tất: Thêm mới {soThem:N0}, Cập nhật {soCapNhat:N0}, Bỏ qua {soBoQua:N0} dòng.";

                Close(true);
            }
            catch (Exception exTrans)
            {
                await transaction.RollbackAsync();
                SafeLog.WriteError("Lỗi lưu dữ liệu hàng hóa Excel", exTrans);
                DatThongBao("Lỗi ghi dữ liệu: " + exTrans.Message, Brushes.IndianRed);
            }
        }
        catch (Exception exConn)
        {
            SafeLog.WriteError("Lỗi kết nối cơ sở dữ liệu khi nhập Excel", exConn);
            DatThongBao("Không thể kết nối cơ sở dữ liệu: " + exConn.Message, Brushes.IndianRed);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    private static uint? ResolveNhomId(MySqlConnection conn, MySqlTransaction tran, Dictionary<string, uint> nhomDict, string? tenNhom)
    {
        if (string.IsNullOrWhiteSpace(tenNhom)) return null;
        var trimmed = tenNhom.Trim();
        if (nhomDict.TryGetValue(trimmed, out var id)) return id;

        try
        {
            bool hasMaNhom = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'nhom_san_pham' AND LOWER(COLUMN_NAME) = 'ma_nhom';",
                transaction: tran) > 0;

            if (hasMaNhom)
            {
                var maNhom = "N" + DateTime.Now.ToString("yyMMddHHmmssfff");
                conn.Execute(
                    "INSERT INTO nhom_san_pham (ma_nhom, ten_nhom, trang_thai) VALUES (@Ma, @Ten, 1);",
                    new { Ma = maNhom, Ten = trimmed }, transaction: tran);
            }
            else
            {
                conn.Execute(
                    "INSERT INTO nhom_san_pham (ten_nhom, trang_thai) VALUES (@Ten, 1);",
                    new { Ten = trimmed }, transaction: tran);
            }

            var newId = conn.ExecuteScalar<uint>("SELECT LAST_INSERT_ID();", transaction: tran);
            if (newId > 0)
            {
                nhomDict[trimmed] = newId;
                return newId;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tự động tạo nhóm sản phẩm khi nhập Excel", ex);
        }

        return null;
    }

    private static uint? ResolveDvtId(MySqlConnection conn, MySqlTransaction tran, Dictionary<string, uint> dvtDict, string? tenDvt)
    {
        if (string.IsNullOrWhiteSpace(tenDvt)) return null;
        var trimmed = tenDvt.Trim();
        if (dvtDict.TryGetValue(trimmed, out var id)) return id;

        try
        {
            bool hasMaDvt = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'don_vi_tinh' AND LOWER(COLUMN_NAME) = 'ma_don_vi';",
                transaction: tran) > 0;

            if (hasMaDvt)
            {
                var ma = "DVT" + DateTime.Now.ToString("yyMMddHHmmssfff");
                conn.Execute(
                    "INSERT INTO don_vi_tinh (ma_don_vi, ten_don_vi, trang_thai) VALUES (@Ma, @Ten, 1);",
                    new { Ma = ma, Ten = trimmed }, transaction: tran);
            }
            else
            {
                conn.Execute(
                    "INSERT INTO don_vi_tinh (ten_don_vi, trang_thai) VALUES (@Ten, 1);",
                    new { Ten = trimmed }, transaction: tran);
            }

            var newId = conn.ExecuteScalar<uint>("SELECT LAST_INSERT_ID();", transaction: tran);
            if (newId > 0)
            {
                dvtDict[trimmed] = newId;
                return newId;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tự động tạo đơn vị tính khi nhập Excel", ex);
        }

        return null;
    }

    private void Dong_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void DatThongBao(string text, IBrush mau)
    {
        ThongBaoTrangThai = text;
        MauThongBaoTrangThai = mau;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
