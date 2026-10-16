using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Toàn bộ phần ViewModel của màn hình Sổ quỹ: danh sách, phân trang, thống kê,
/// form tạo/sửa/chi tiết phiếu, chọn nhiều, xuất Excel và nhập từ file.
/// <para>
/// Bộ lọc nằm ở <c>MainWindowViewModel.SoQuyBoLoc.cs</c>, truy vấn nằm ở
/// <c>DatabaseService</c> và <c>SoQuyImportService</c>. Danh sách luôn lấy đúng
/// một trang từ MySQL, không bao giờ kéo cả bảng <c>phieu_chi</c> về rồi lọc ở C#.
/// </para>
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Số phiếu bị hủy đang xử lý, chặn bấm hai lần liên tiếp.</summary>
    private bool _soQuyDangHuyPhieu;

    /// <summary>Số phiếu đang được tick trên trang hiện tại.</summary>
    private int _soQuySoDongDaChon;

    /// <summary>
    /// true khi đang chủ động gán trạng thái cho ô "chọn tất cả", để không bị
    /// cập nhật ngược từ từng dòng làm giá trị của ô nhảy về null.
    /// </summary>
    private bool _soQuyDangGanChonTatCa;

    /// <summary>Bộ đệm nhãn tiền cho 4 thẻ tổng quan, không format lại mỗi lần render.</summary>
    private static readonly IBrush VienMacMacDinh = new SolidColorBrush(Color.Parse("#D8E5E0"));
    private static readonly IBrush VienMacLoi = new SolidColorBrush(Color.Parse("#F04438"));

    private readonly SoQuyImportService _soQuyImportService = new();

    /// <summary>
    /// Đếm phiêu bản nạp đang bay. Người dùng gõ nhanh có thể gọi nhiều lần
    /// LoadSoQuyPhieuAsync chồng lên nhau; chỉ kết quả của lần mới nhất được nhận.
    /// </summary>
    private int _soQuyLoadVersion;

    // ================================================================
    // DANH SÁCH + PHÂN TRANG
    // ================================================================

    public ObservableCollection<SoQuyGiaoDich> SoQuyGiaoDichs { get; } = new();

    public IReadOnlyList<int> SoQuyPageSizeOptions { get; } = [20, 50, 100];

    [ObservableProperty] private string soQuySearchText = "";
    [ObservableProperty] private int soQuyPageSize = 20;
    [ObservableProperty] private int soQuyPage = 1;
    [ObservableProperty] private int soQuyTotalCount;

    /// <summary>4 thẻ tổng quan, đã định dạng sẵn để hiển thị.</summary>
    [ObservableProperty] private string soQuyQuyDauKyText = "0 đ";
    [ObservableProperty] private string soQuyTongThuText = "0 đ";
    [ObservableProperty] private string soQuyTongChiText = "0 đ";
    [ObservableProperty] private string soQuyTonQuyText = "0 đ";

    /// <summary>true khi người dùng đang gõ từ khóa (đổi nội dung Empty State).</summary>
    public bool IsSoQuyDangTimKiem => !string.IsNullOrWhiteSpace(SoQuySearchText);

    public bool HasSoQuyData => SoQuyGiaoDichs.Count > 0;

    public int SoQuyTotalPages =>
        SoQuyPageSize <= 0
            ? 0
            : (int)Math.Ceiling(SoQuyTotalCount / (double)SoQuyPageSize);

    public bool CanSoQuyTrangSau => SoQuyPage < SoQuyTotalPages;

    public bool CanSoQuyTrangTruoc => SoQuyPage > 1;

    /// <summary>Ví dụ: "1 - 20 trong 356 phiếu"</summary>
    public string SoQuyRangeText
    {
        get
        {
            if (SoQuyTotalCount == 0)
                return "0 phiếu";

            var from = (SoQuyPage - 1) * SoQuyPageSize + 1;
            var to = Math.Min(from + SoQuyPageSize - 1, SoQuyTotalCount);

            return $"{from} - {to} trong {SoQuyTotalCount} phiếu";
        }
    }

    /// <summary>Empty State: phân biệt "chưa có dữ liệu" với "lọc không ra kết quả".</summary>
    public string SoQuyEmptyStateText =>
        CoSoQuyBoLoc
            ? "Không có phiếu nào khớp bộ lọc đang chọn"
            : "Chưa có giao dịch nào trong sổ quỹ";

    /// <summary>Các nút số trang trên thanh phân trang.</summary>
    public ObservableCollection<SoQuyNutTrang> SoQuyDanhSachTrang { get; } = new();

    /// <summary>Số nút trang hiện tối đa (kể cả hai dấu "...").</summary>
    private const int SoQuySoNutTrangToiDa = 7;

    partial void OnSoQuySearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSoQuyDangTimKiem));
        OnPropertyChanged(nameof(SoQuyEmptyStateText));

        // Ô tìm kiếm là một điều kiện của bộ lọc nên dùng chung đường hoãn 300ms
        // với các điều kiện khác: danh sách và 4 thẻ thống kê luôn cùng một bộ
        // điều kiện, và không gọi database liên tục khi đang gõ.
        BaoLocSoQuyThayDoi();
    }

    [RelayCommand]
    private void XoaTimKiemSoQuy()
    {
        _soQuyLocCts?.Cancel();
        SoQuySearchText = "";
        SoQuyPage = 1;
        _ = TaiLaiSoQuyAsync();
    }

    partial void OnSoQuyPageSizeChanged(int value)
    {
        SoQuyPage = 1;
        RaiseSoQuyPaginationChanged();
        _ = LoadSoQuyPhieuAsync();
    }

    partial void OnSoQuyPageChanged(int value)
    {
        RaiseSoQuyPaginationChanged();
        _ = LoadSoQuyPhieuAsync();
    }

    partial void OnSoQuyTotalCountChanged(int value) => RaiseSoQuyPaginationChanged();

    private void RaiseSoQuyPaginationChanged()
    {
        OnPropertyChanged(nameof(SoQuyTotalPages));
        OnPropertyChanged(nameof(SoQuyRangeText));
        OnPropertyChanged(nameof(CanSoQuyTrangSau));
        OnPropertyChanged(nameof(CanSoQuyTrangTruoc));

        CapNhatDanhSachTrangSoQuy();
    }

    /// <summary>
    /// Dựng cửa sổ số trang quanh trang hiện tại, kèm dấu "..." nếu còn trang bị
    /// khuất. Không dựng hết số trang vì dữ liệu sổ quỹ có thể rất lớn.
    /// </summary>
    private void CapNhatDanhSachTrangSoQuy()
    {
        var tongTrang = SoQuyTotalPages;

        SoQuyDanhSachTrang.Clear();

        if (tongTrang <= 0)
            return;

        if (tongTrang <= SoQuySoNutTrangToiDa)
        {
            for (var i = 1; i <= tongTrang; i++)
                SoQuyDanhSachTrang.Add(SoQuyNutTrang.Tao(i, i == SoQuyPage));

            return;
        }

        var batDau = Math.Max(2, SoQuyPage - 2);
        var ketThuc = Math.Min(tongTrang - 1, SoQuyPage + 2);

        SoQuyDanhSachTrang.Add(SoQuyNutTrang.Tao(1, SoQuyPage == 1));

        if (batDau > 2)
            SoQuyDanhSachTrang.Add(SoQuyNutTrang.TaoPhanTich());

        for (var i = batDau; i <= ketThuc; i++)
            SoQuyDanhSachTrang.Add(SoQuyNutTrang.Tao(i, i == SoQuyPage));

        if (ketThuc < tongTrang - 1)
            SoQuyDanhSachTrang.Add(SoQuyNutTrang.TaoPhanTich());

        SoQuyDanhSachTrang.Add(SoQuyNutTrang.Tao(tongTrang, SoQuyPage == tongTrang));
    }

    [RelayCommand]
    private void SoQuyDenTrang(int soTrang)
    {
        if (soTrang < 1 || soTrang > SoQuyTotalPages || soTrang == SoQuyPage)
            return;

        SoQuyPage = soTrang;
    }

    [RelayCommand]
    private void SoQuyTrangDau() => SoQuyPage = 1;

    [RelayCommand]
    private void SoQuyTrangCuoi() => SoQuyPage = Math.Max(1, SoQuyTotalPages);

    [RelayCommand]
    private void SoQuyTrangSau()
    {
        if (CanSoQuyTrangSau)
            SoQuyPage++;
    }

    [RelayCommand]
    private void SoQuyTrangTruoc()
    {
        if (CanSoQuyTrangTruoc)
            SoQuyPage--;
    }

    /// <summary>
    /// Nạp đúng một trang của sổ quỹ, đồng thời nạp 4 thẻ tổng quan. Cả hai dùng
    /// chung một đối tượng bộ lọc nên thống kê luôn khớp với danh sách đang xem.
    /// </summary>
    private async Task LoadSoQuyPhieuAsync()
    {
        var version = ++_soQuyLoadVersion;

        var boLoc = SoQuyLocHienTai;

        try
        {
            var (items, tongSoBanGhi) = await _db.GetSoQuyPhieuPagedAsync(boLoc, SoQuyPage, SoQuyPageSize);

            // Bỏ qua kết quả cũ nếu người dùng vừa gõ thêm ký tự hoặc chuyển trang.
            if (version != _soQuyLoadVersion)
                return;

            // Lọc xong mà trang hiện tại vượt quá số trang (đang ở trang 5 rồi
            // thu hẹp bộ lọc) thì lùi về trang cuối rồi nạp lại cho đúng.
            var tongSoTrang = SoQuyPageSize <= 0
                ? 0
                : (int)Math.Ceiling(tongSoBanGhi / (double)SoQuyPageSize);

            if (tongSoTrang > 0 && SoQuyPage > tongSoTrang)
            {
                SoQuyTotalCount = tongSoBanGhi;
                SoQuyPage = tongSoTrang;

                await LoadSoQuyPhieuAsync();

                return;
            }

            SoQuyGiaoDichs.Clear();

            foreach (var item in items)
            {
                // Dòng này có thể đã được tick từ lần nạp trước: giữ lại trạng
                // thái chọn để người dùng không mất công tick lại.
                item.PropertyChanged += OnSoQuyDongPropertyChanged;

                SoQuyGiaoDichs.Add(item);
            }

            SoQuyTotalCount = tongSoBanGhi;

            // Trang mới luôn bắt đầu chưa chọn gì, nên ô "chọn tất cả" về lại
            // trạng thái chưa tick.
            _soQuyDangGanChonTatCa = true;
            CapNhatSoLuongDaChon();
            _soQuyDangGanChonTatCa = false;

            OnPropertyChanged(nameof(HasSoQuyData));
            OnPropertyChanged(nameof(SoQuyEmptyStateText));

            await LoadSoQuyTongHopAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải sổ quỹ: " + ex.Message;
        }
    }

    /// <summary>
    /// Cập nhật 4 thẻ tổng quan. Bốn chỉ số luôn tính trên cả phiếu thu lẫn phiếu
    /// chi, chỉ theo kỳ thời gian và quỹ đang chọn, nên tồn quỹ = quỹ đầu kỳ +
    /// tổng thu - tổng chi luôn đúng số dư thực tế. Các bộ lọc hiển thị (loại
    /// chứng từ, tìm kiếm, loại thu chi...) chỉ lọc danh sách, không đổi 4 thẻ.
    /// </summary>
    private async Task LoadSoQuyTongHopAsync()
    {
        try
        {
            var tongHop = await _db.GetSoQuyTongHopTheoLocAsync(SoQuyLocHienTai);

            SoQuyQuyDauKyText = FormatSoQuyTien(tongHop.QuyDauKy);
            SoQuyTongThuText = FormatSoQuyTien(tongHop.TongThu);

            // SQL trả TongChi là số dương (tiền chi ra) nhưng thẻ thống kê phải
            // hiện số âm đúng như sổ quỹ: "-1.000.000 đ" chứ không phải "1.000.000 đ".
            SoQuyTongChiText = FormatSoQuyTien(-tongHop.TongChi);
            SoQuyTonQuyText = FormatSoQuyTien(tongHop.TonQuy);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải tổng hợp sổ quỹ: " + ex.Message;
        }
    }

    /// <summary>
    /// Định dạng tiền cho 4 thẻ. Cố tình KHÔNG thêm dấu "+" cho tổng thu và tồn
    /// quỹ: thẻ đã có nhãn và màu riêng, thêm dấu cộng chỉ làm chữ dính sát số.
    /// </summary>
    private static string FormatSoQuyTien(decimal value) => MoneyInput.Format(value) + " đ";

    // ================================================================
    // CHỌN NHIỀU PHIẾU
    //
    // Ô chọn trên tiêu đề bảng là trạng thái ba điểm: chưa tick = chưa chọn gì,
    // tick = đã chọn tất cả, gạch ngang = chỉ chọn một phần.
    // ================================================================

    /// <summary>Trạng thái ba điểm của ô "chọn tất cả" trên tiêu đề bảng.</summary>
    [ObservableProperty] private bool? isSoQuyAllSelected;

    public int SoQuySelectedCount => _soQuySoDongDaChon;

    public bool HasSoQuySelected => _soQuySoDongDaChon > 0;

    public string SoQuySelectedText => _soQuySoDongDaChon == 0
        ? "Chưa chọn phiếu nào"
        : _soQuySoDongDaChon == SoQuyGiaoDichs.Count
            ? $"Đã chọn tất cả {_soQuySoDongDaChon} phiếu"
            : $"Đã chọn {_soQuySoDongDaChon} trên {SoQuyGiaoDichs.Count} phiếu";

    private List<SoQuyGiaoDich> SoQuyDongDaChon => [.. SoQuyGiaoDichs.Where(gd => gd.DaChon)];

    partial void OnIsSoQuyAllSelectedChanged(bool? value)
    {
        if (value is null || SoQuyGiaoDichs.Count == 0)
            return;

        // Cờ này chặn việc cập nhật ngược từ dòng lên ô tiêu đề, nếu không giá
        // trị của ô sẽ nhảy về null ngay sau khi tick dòng đầu tiên.
        _soQuyDangGanChonTatCa = true;

        foreach (var gd in SoQuyGiaoDichs)
            gd.DaChon = value.Value;

        _soQuyDangGanChonTatCa = false;

        CapNhatSoLuongDaChon();
    }

    private void OnSoQuyDongPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoQuyGiaoDich.DaChon))
            CapNhatSoLuongDaChon();
    }

    private void CapNhatSoLuongDaChon()
    {
        _soQuySoDongDaChon = SoQuyGiaoDichs.Count(gd => gd.DaChon);

        if (!_soQuyDangGanChonTatCa)
            IsSoQuyAllSelected = SoQuyGiaoDichs.Count == 0
                ? null
                : _soQuySoDongDaChon switch
                {
                    0 => false,
                    var n when n == SoQuyGiaoDichs.Count => true,
                    _ => null
                };

        OnPropertyChanged(nameof(SoQuySelectedCount));
        OnPropertyChanged(nameof(HasSoQuySelected));
        OnPropertyChanged(nameof(SoQuySelectedText));
    }

    [RelayCommand]
    private void BoChonTatCaSoQuy()
    {
        _soQuyDangGanChonTatCa = true;

        foreach (var gd in SoQuyGiaoDichs)
            gd.DaChon = false;

        _soQuyDangGanChonTatCa = false;

        CapNhatSoLuongDaChon();
    }

    /// <summary>Hủy toàn bộ phiếu đang tick trong một thao tác, có xác nhận trước.</summary>
    [RelayCommand]
    private async Task HuyNhieuPhieuDaChonAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookCancel, "Bạn không có quyền hủy phiếu quỹ."))
        {
            return;
        }
        if (_soQuyDangHuyPhieu)
            return;

        var danhSach = SoQuyDongDaChon
            .Where(gd => !gd.DaHuy)
            .ToList();

        if (danhSach.Count == 0)
        {
            StatusMessage = HasSoQuySelected
                ? "Các phiếu đã chọn đều đã hủy nên không cần hủy thêm."
                : "Vui lòng tick ít nhất một phiếu trước khi hủy.";

            return;
        }

        var xacNhan = await XacNhanHuyPhieuAsync(
            danhSach.Count,
            $"Bạn có chắc muốn hủy {danhSach.Count} phiếu đang chọn?\n\n"
            + "Phiếu đã hủy sẽ không còn được tính vào tổng thu, tổng chi của sổ quỹ.\n"
            + "Thao tác này không thể hoàn tác.");

        if (!xacNhan)
            return;

        _soQuyDangHuyPhieu = true;

        try
        {
            var soDaHuy = await _db.HuyNhieuPhieuQuyAsync(
                [.. danhSach.Select(gd => gd.MaPhieu)]);

            StatusMessage = $"Đã hủy {soDaHuy:N0} phiếu.";

            await TaiLaiSoQuyAsync();

            _ = LamMoiTongQuanNeuDangMoAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể hủy các phiếu đã chọn: " + ex.Message;
        }
        finally
        {
            _soQuyDangHuyPhieu = false;
        }
    }

    // ================================================================
    // MODAL TẠO PHIẾU THU / PHIẾU CHI
    // ================================================================

    [ObservableProperty] private bool isSoQuyPhieuThuVisible;
    [ObservableProperty] private bool isSoQuyPhieuChiVisible;
    [ObservableProperty] private bool isSoQuyPhieuDangLuu;

    /// <summary>Mã phiếu do database sinh (PTxxxx / PCxxxx), người dùng không sửa.</summary>
    [ObservableProperty] private string soQuyPhieuMa = "";

    [ObservableProperty] private DateTime soQuyPhieuThoiGian = DateTime.Now;

    /// <summary>Số tiền, luôn dương; thu/chi phân biệt bằng tiền tố mã phiếu.</summary>
    [ObservableProperty] private decimal soQuyPhieuSoTien;

    /// <summary>Nội dung phiếu (cột <c>noi_dung</c>).</summary>
    [ObservableProperty] private string soQuyPhieuNoiDung = "";

    /// <summary>Ghi chú phiếu (cột <c>ghi_chu</c>).</summary>
    [ObservableProperty] private string soQuyPhieuGhiChu = "";

    /// <summary>
    /// Nhân viên thu/chi tiền, nhập bằng tên và được đối chiếu với danh sách
    /// nhân viên lấy từ database để suy ra <c>nguoi_thuc_hien_id</c>.
    /// </summary>
    [ObservableProperty] private string soQuyPhieuNguoiThucHien = "";

    /// <summary>Người nộp (phiếu thu) hoặc người nhận (phiếu chi): cột <c>nguoi_nop_nhan</c>.</summary>
    [ObservableProperty] private string soQuyPhieuTenNguoi = "";

    /// <summary>
    /// Loại thu/chi đang chọn trên form phiếu thu. Tách riêng với loại của phiếu
    /// chi để chuyển modal không mang nhầm loại sang form kia.
    /// </summary>
    [ObservableProperty] private SoQuyLuaChonItem? soQuyPhieuThuLoai;

    [ObservableProperty] private SoQuyLuaChonItem? soQuyPhieuChiLoai;

    /// <summary>Quỹ đang chọn trên form tạo phiếu.</summary>
    [ObservableProperty] private SoQuyLuaChon? soQuyPhieuQuy;

    /// <summary>
    /// Mọi phiếu tạo từ form đều được tính vào tổng thu/tổng chi của sổ quỹ và
    /// kết quả kinh doanh trong kỳ, nên thuộc tính này luôn đúng: nó chỉ để
    /// câu chữ giải thích trong form có tín hiệu, không phải một lựa chọn.
    /// </summary>
    public bool SoQuyPhieuHachToan => true;

    /// <summary>Các loại thu/chi đang hoạt động, lấy từ bảng nhom_chi_phi.</summary>
    public ObservableCollection<SoQuyLuaChonItem> SoQuyPhieuLoaiThuChiOptions { get; } = new();

    /// <summary>Nhân viên có thể làm người thu/chi tiền, lấy từ bảng nguoi_dung.</summary>
    public ObservableCollection<SoQuyLuaChonItem> SoQuyPhieuNguoiTaoOptions { get; } = new();

    /// <summary>Các quỹ đang có trong database, lấy từ dữ liệu phieu_chi thật.</summary>
    public ObservableCollection<SoQuyLuaChon> SoQuyPhieuQuyOptions { get; } = new();

    /// <summary>
    /// Bảng nhom_chi_phi không có cột phân loại thu/chi (chỉ có mã và tên), nên
    /// cả hai form đều hiện toàn bộ danh mục đang hoạt động. Thu hay chi là do
    /// tiền tố mã phiếu PT/PC quyết định, không phải do danh mục.
    /// </summary>
    public IReadOnlyList<SoQuyLuaChonItem> SoQuyLoaiThuOptions => SoQuyPhieuLoaiThuChiOptions;

    public IReadOnlyList<SoQuyLuaChonItem> SoQuyLoaiChiOptions => SoQuyPhieuLoaiThuChiOptions;

    public bool SoQuyPhieuCoLoaiThuChi => SoQuyLoaiThuOptions.Count > 0;

    public bool SoQuyPhieuCoLoaiChi => SoQuyLoaiChiOptions.Count > 0;

    public bool SoQuyPhieuCoQuy => SoQuyPhieuQuyOptions.Count > 0;

    // ================================================================
    // VALIDATION FORM TẠO PHIẾU
    //
    // Chỉ hiện lỗi sau khi người dùng đã chạm vào ô hoặc đã bấm Lưu
    // (SoQuyFormDaCham), nên form vừa mở không hiện lỗi.
    // ================================================================

    [ObservableProperty] private bool soQuyFormDaCham;

    [ObservableProperty] private bool soQuyLoiMaPhieu;
    [ObservableProperty] private bool soQuyLoiThoiGian;
    [ObservableProperty] private bool soQuyLoiLoai;
    [ObservableProperty] private bool soQuyLoiNguoiThucHien;
    [ObservableProperty] private bool soQuyLoiTenNguoi;
    [ObservableProperty] private bool soQuyLoiSoTien;

    public string SoQuyLoiMaPhieuText => "Vui lòng tạo lại mã phiếu.";
    public string SoQuyLoiThoiGianText => "Vui lòng nhập thời gian hợp lệ.";
    public string SoQuyLoiLoaiText => "Vui lòng chọn loại thu/chi.";
    public string SoQuyLoiSoTienText => "Vui lòng nhập số tiền lớn hơn 0.";

    public string SoQuyLoiNguoiThucHienHienThi =>
        "Vui lòng nhập tên nhân viên có trong danh sách.";

    public string SoQuyLoiTenNguoiHienThi => IsSoQuyPhieuThuVisible
        ? "Vui lòng nhập người nộp tiền."
        : "Vui lòng nhập người nhận tiền.";

    /// <summary>Nhãn ô người nộp đổi theo loại phiếu đang mở.</summary>
    public string SoQuyNhanTenNguoi => IsSoQuyPhieuThuVisible ? "Người nộp" : "Người nhận";

    /// <summary>Nhãn ô nhân viên thu/chi đổi theo loại phiếu đang mở.</summary>
    public string SoQuyNhanNguoiThucHien => IsSoQuyPhieuThuVisible ? "Người thu" : "Người chi";

    public bool SoQuyHienLoiMaPhieu => SoQuyFormDaCham && SoQuyLoiMaPhieu;
    public bool SoQuyHienLoiThoiGian => SoQuyFormDaCham && SoQuyLoiThoiGian;
    public bool SoQuyHienLoiLoai => SoQuyFormDaCham && SoQuyLoiLoai;
    public bool SoQuyHienLoiNguoiThucHien => SoQuyFormDaCham && SoQuyLoiNguoiThucHien;
    public bool SoQuyHienLoiTenNguoi => SoQuyFormDaCham && SoQuyLoiTenNguoi;
    public bool SoQuyHienLoiSoTien => SoQuyFormDaCham && SoQuyLoiSoTien;

    public IBrush SoQuyVienMaPhieu => SoQuyHienLoiMaPhieu ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienThoiGian => SoQuyHienLoiThoiGian ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienLoai => SoQuyHienLoiLoai ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienNguoiThucHien => SoQuyHienLoiNguoiThucHien ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienTenNguoi => SoQuyHienLoiTenNguoi ? VienMacLoi : VienMacMacDinh;
    public IBrush SoQuyVienSoTien => SoQuyHienLoiSoTien ? VienMacLoi : VienMacMacDinh;

    /// <summary>Thời gian hiển thị dạng dd/MM/yyyy HH:mm, người dùng không tự nhập.</summary>
    public string SoQuyPhieuThoiGianText =>
        SoQuyPhieuThoiGian == default ? "" : SoQuyPhieuThoiGian.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Số tiền không được âm (MoneyTextBox có thể nhận được số âm).</summary>
    partial void OnSoQuyPhieuSoTienChanged(decimal value)
    {
        if (value < 0)
            SoQuyPhieuSoTien = 0;

        SoQuyLoiSoTien = value <= 0;
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuMaChanged(string value)
    {
        SoQuyLoiMaPhieu = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuThoiGianChanged(DateTime value)
    {
        SoQuyLoiThoiGian = value == default;
        OnPropertyChanged(nameof(SoQuyPhieuThoiGianText));
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuNguoiThucHienChanged(string value)
    {
        SoQuyFormDaCham = true;
        SoQuyLoiNguoiThucHien = !TimNhanVienTheoTen(value, out _);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuTenNguoiChanged(string value)
    {
        SoQuyFormDaCham = true;
        SoQuyLoiTenNguoi = string.IsNullOrWhiteSpace(value);
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuThuLoaiChanged(SoQuyLuaChonItem? value)
    {
        SoQuyFormDaCham = true;
        SoQuyLoiLoai = value is null;
        RaiseSoQuyValidation();
    }

    partial void OnSoQuyPhieuChiLoaiChanged(SoQuyLuaChonItem? value)
    {
        SoQuyFormDaCham = true;
        SoQuyLoiLoai = value is null;
        RaiseSoQuyValidation();
    }

    partial void OnIsSoQuyPhieuThuVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(SoQuyLoiTenNguoiHienThi));
        OnPropertyChanged(nameof(SoQuyNhanTenNguoi));
        OnPropertyChanged(nameof(SoQuyNhanNguoiThucHien));
        OnPropertyChanged(nameof(SoQuyPhieuCoLoaiThuChi));
    }

    partial void OnIsSoQuyPhieuChiVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(SoQuyPhieuCoLoaiChi));

    private void RaiseSoQuyValidation()
    {
        OnPropertyChanged(nameof(SoQuyHienLoiMaPhieu));
        OnPropertyChanged(nameof(SoQuyHienLoiThoiGian));
        OnPropertyChanged(nameof(SoQuyHienLoiLoai));
        OnPropertyChanged(nameof(SoQuyHienLoiNguoiThucHien));
        OnPropertyChanged(nameof(SoQuyHienLoiTenNguoi));
        OnPropertyChanged(nameof(SoQuyHienLoiSoTien));
        OnPropertyChanged(nameof(SoQuyVienMaPhieu));
        OnPropertyChanged(nameof(SoQuyVienThoiGian));
        OnPropertyChanged(nameof(SoQuyVienLoai));
        OnPropertyChanged(nameof(SoQuyVienNguoiThucHien));
        OnPropertyChanged(nameof(SoQuyVienTenNguoi));
        OnPropertyChanged(nameof(SoQuyVienSoTien));
    }

    /// <summary>Đối chiếu tên nhân viên gõ trên form với danh sách từ database.</summary>
    private bool TimNhanVienTheoTen(string? ten, out uint id)
    {
        id = 0;

        var chuanHoa = ChuoiSoSanh(ten);

        if (string.IsNullOrEmpty(chuanHoa))
            return false;

        foreach (var nhanVien in SoQuyPhieuNguoiTaoOptions)
        {
            if (ChuoiSoSanh(nhanVien.HienThi) != chuanHoa)
                continue;

            return uint.TryParse(nhanVien.GiaTri, out id);
        }

        return false;
    }

    /// <summary>
    /// Chuẩn hóa chuỗi để so sánh tên tiếng Việt: bỏ khoảng trắng thừa và dấu.
    /// Người dùng gõ tay nên không thể đòi khớp tuyệt đối với tên trong database.
    /// </summary>
    private static string ChuoiSoSanh(string? giaTri) =>
        new((giaTri ?? "").Trim().ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    /// <summary>Kiểm tra toàn bộ trường bắt buộc. Trả về true khi dữ liệu hợp lệ.</summary>
    private bool KiemTraFormPhieu(bool laPhieuThu, out uint idNguoiThucHien)
    {
        SoQuyLoiMaPhieu = string.IsNullOrWhiteSpace(SoQuyPhieuMa);
        SoQuyLoiThoiGian = SoQuyPhieuThoiGian == default;
        SoQuyLoiLoai = (laPhieuThu ? SoQuyPhieuThuLoai : SoQuyPhieuChiLoai) is null;
        SoQuyLoiNguoiThucHien = !TimNhanVienTheoTen(SoQuyPhieuNguoiThucHien, out idNguoiThucHien);
        SoQuyLoiTenNguoi = string.IsNullOrWhiteSpace(SoQuyPhieuTenNguoi);
        SoQuyLoiSoTien = SoQuyPhieuSoTien <= 0;

        RaiseSoQuyValidation();

        return !SoQuyLoiMaPhieu
               && !SoQuyLoiThoiGian
               && !SoQuyLoiLoai
               && !SoQuyLoiNguoiThucHien
               && !SoQuyLoiTenNguoi
               && !SoQuyLoiSoTien;
    }

    [RelayCommand]
    private Task LuuSoQuyPhieuThuAsync() => LuuSoQuyPhieuAsync(true);

    [RelayCommand]
    private Task LuuSoQuyPhieuChiAsync() => LuuSoQuyPhieuAsync(false);

    /// <summary>Lưu &amp; In: hiện dùng chung luồng lưu, chờ phần in phiếu.</summary>
    [RelayCommand]
    private Task LuuVaInSoQuyPhieuThuAsync() => LuuSoQuyPhieuAsync(true);

    [RelayCommand]
    private Task LuuVaInSoQuyPhieuChiAsync() => LuuSoQuyPhieuAsync(false);

    private async Task LuuSoQuyPhieuAsync(bool laPhieuThu)
    {
        if (!await KiemTraQuyenAsync(
                laPhieuThu ? PermissionKeys.CashBookCreateReceipt : PermissionKeys.CashBookCreatePayment,
                laPhieuThu ? "Bạn không có quyền tạo phiếu thu." : "Bạn không có quyền tạo phiếu chi."))
        {
            return;
        }

        if (IsSoQuyPhieuDangLuu)
            return;

        // Bấm Lưu là bắt đầu hiển thị lỗi cho toàn bộ form.
        SoQuyFormDaCham = true;

        var tenLoai = laPhieuThu ? "thu" : "chi";

        if (!KiemTraFormPhieu(laPhieuThu, out var idNguoiThucHien))
        {
            StatusMessage = $"Không thể lưu phiếu {tenLoai}: "
                            + "vui lòng kiểm tra các ô được đánh dấu đỏ.";

            return;
        }

        var idNhomChiPhi = uint.TryParse(
            (laPhieuThu ? SoQuyPhieuThuLoai : SoQuyPhieuChiLoai)?.GiaTri,
            out var idLoai)
            ? idLoai
            : 0u;

        IsSoQuyPhieuDangLuu = true;

        try
        {
            await _db.LuuPhieuQuyAsync(
                SoQuyPhieuMa,
                SoQuyPhieuSoTien,
                SoQuyPhieuThoiGian,
                idNhomChiPhi == 0 ? null : idNhomChiPhi,
                SoQuyPhieuQuy?.GiaTri,
                idNguoiThucHien,
                SoQuyPhieuTenNguoi,
                SoQuyPhieuNoiDung,
                SoQuyPhieuGhiChu);

            StatusMessage = $"Đã lưu phiếu {tenLoai} thành công.";

            // Đóng modal rồi nạp lại danh sách + thống kê để thấy phiếu vừa lưu.
            ResetSoQuyPhieuForm();

            await TaiLaiSoQuyAsync();

            // Nạp lại Tổng quan nếu đang mở để số liệu thu/chi cập nhật kịp.
            _ = LamMoiTongQuanNeuDangMoAsync();
        }
        catch (Exception ex)
        {
            // Không nuốt lỗi: in ra console để còn tra được chi tiết SQL/kết nối.
            Console.WriteLine($"========== LỖI LƯU PHIẾU {tenLoai.ToUpperInvariant()} SỔ QUỸ ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("======================================================");

            StatusMessage = $"Không thể lưu phiếu {tenLoai}. Vui lòng thử lại. "
                            + $"(Chi tiết: {ex.Message})";
        }
        finally
        {
            IsSoQuyPhieuDangLuu = false;
        }
    }

    [RelayCommand]
    private async Task MoSoQuyPhieuThuAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookCreateReceipt, "Bạn không có quyền tạo phiếu thu."))
        {
            return;
        }
        await MoSoQuyPhieuFormAsync("PT");

        IsSoQuyPhieuThuVisible = true;
    }

    [RelayCommand]
    private async Task MoSoQuyPhieuChiAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookCreatePayment, "Bạn không có quyền tạo phiếu chi."))
        {
            return;
        }
        await MoSoQuyPhieuFormAsync("PC");

        IsSoQuyPhieuChiVisible = true;
    }

    [RelayCommand]
    private void DongSoQuyPhieu() => ResetSoQuyPhieuForm();

    // ================================================================
    // XEM CHI TIẾT / HỦY / SỬA
    // ================================================================

    [ObservableProperty] private bool isSoQuyChiTietVisible;
    [ObservableProperty] private SoQuyChiTiet? soQuyChiTietDangXem;

    [RelayCommand]
    private async Task MoChiTietPhieuAsync(SoQuyGiaoDich gd)
    {
        if (gd is null)
            return;

        try
        {
            var chiTiet = await _db.GetSoQuyChiTietAsync(gd.MaPhieu);

            SoQuyChiTietDangXem = chiTiet;
            IsSoQuyChiTietVisible = chiTiet is not null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải chi tiết phiếu: " + ex.Message;
        }
    }

    [RelayCommand]
    private void DongChiTietPhieu()
    {
        IsSoQuyChiTietVisible = false;
        SoQuyChiTietDangXem = null;
    }

    [RelayCommand]
    private async Task HuyPhieuHienTaiAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookCancel, "Bạn không có quyền hủy phiếu quỹ."))
        {
            return;
        }
        var chiTiet = SoQuyChiTietDangXem;

        if (chiTiet is null || chiTiet.DaHuy || _soQuyDangHuyPhieu)
            return;

        var xacNhan = await XacNhanHuyPhieuAsync(
            1,
            $"Bạn có chắc muốn hủy phiếu {chiTiet.MaPhieu}?\n\n"
            + "Phiếu đã hủy sẽ không còn được tính vào tổng thu, tổng chi của sổ quỹ.\n"
            + "Thao tác này không thể hoàn tác.");

        if (!xacNhan)
            return;

        _soQuyDangHuyPhieu = true;

        try
        {
            await _db.HuyPhieuQuyAsync(chiTiet.MaPhieu);

            StatusMessage = "Đã hủy phiếu thành công.";

            var maPhieu = chiTiet.MaPhieu;

            IsSoQuyChiTietVisible = false;
            SoQuyChiTietDangXem = null;

            await TaiLaiSoQuyAsync();

            // Mở lại chi tiết để người dùng thấy trạng thái vừa đổi.
            var gd = SoQuyGiaoDichs.FirstOrDefault(x => x.MaPhieu == maPhieu);

            if (gd is not null)
                await MoChiTietPhieuAsync(gd);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể hủy phiếu: " + ex.Message;
        }
        finally
        {
            _soQuyDangHuyPhieu = false;
        }
    }

    /// <summary>
    /// Hỏi lại người dùng trước khi hủy phiếu. Hủy phiếu làm thay đổi số dư quỹ
    /// nên không được bấm một phát là xong.
    /// </summary>
    private async Task<bool> XacNhanHuyPhieuAsync(int soPhieu, string noiDung)
    {
        if (OwnerWindow is null)
            return false;

        var dialog = new ConfirmationWindow(
            "Xác nhận hủy phiếu",
            noiDung,
            "Hủy phiếu",
            "Giữ nguyên");

        var daXacNhan = await dialog.ShowDialog<bool>(OwnerWindow);

        return daXacNhan;
    }

    // ================================================================
    // SỬA PHIẾU
    //
    // Cho sửa được những thông tin của phiếu: thời gian, số tiền, loại thu/chi,
    // quỹ, người nộp/nhận, nội dung và ghi chú. Mã phiếu, người tạo và trạng
    // thái là dữ liệu sinh ra lúc tạo chứng từ nên giữ nguyên; phiếu đã hủy
    // thì không sửa được.
    // ================================================================

    [ObservableProperty] private bool isSoQuySuaPhieuVisible;
    [ObservableProperty] private bool isSoQuySuaPhieuDangLuu;
    [ObservableProperty] private SoQuyChiTiet? soQuyPhieuDangSua;

    [ObservableProperty] private decimal soQuySuaSoTien;
    [ObservableProperty] private DateTime soQuySuaThoiGian = DateTime.Now;
    [ObservableProperty] private string soQuySuaTenNguoi = "";
    [ObservableProperty] private string soQuySuaNoiDung = "";
    [ObservableProperty] private string soQuySuaGhiChu = "";
    [ObservableProperty] private bool soQuySuaLoiSoTien;

    [ObservableProperty] private SoQuyLuaChonItem? soQuySuaLoai;
    [ObservableProperty] private SoQuyLuaChon? soQuySuaQuy;

    public string SoQuyLoiSuaSoTienText => "Vui lòng nhập số tiền lớn hơn 0.";

    public bool SoQuyHienLoiSuaSoTien => SoQuySuaLoiSoTien;

    public IBrush SoQuyVienSuaSoTien => SoQuyHienLoiSuaSoTien ? VienMacLoi : VienMacMacDinh;

    public string SoQuySuaThoiGianText =>
        SoQuySuaThoiGian == default ? "" : SoQuySuaThoiGian.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Danh sách loại thu/chi cho form sửa.</summary>
    public IReadOnlyList<SoQuyLuaChonItem> SoQuySuaLoaiOptions => SoQuyPhieuLoaiThuChiOptions;

    /// <summary>Nhãn ô người nộp/nhận trên form sửa, đổi theo loại phiếu.</summary>
    public string SoQuySuaNhanTenNguoi =>
        SoQuyPhieuDangSua is null ? "Người nộp/nhận" : SoQuyPhieuDangSua.NhanNguoiNopNhan;

    partial void OnSoQuySuaSoTienChanged(decimal value)
    {
        if (value < 0)
            SoQuySuaSoTien = 0;

        SoQuySuaLoiSoTien = value <= 0;
        RaiseSoQuyValidation();
    }

    partial void OnIsSoQuySuaPhieuVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(SoQuySuaNhanTenNguoi));

    [RelayCommand]
    private async Task SuaPhieuHienTaiAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookEdit, "Bạn không có quyền sửa phiếu quỹ."))
        {
            return;
        }
        var chiTiet = SoQuyChiTietDangXem;

        if (chiTiet is null || chiTiet.DaHuy)
            return;

        // Nạp danh mục trước để form sửa có loại thu/chi và quỹ thật.
        await TaiDanhMucFormSoQuyAsync();

        SoQuyPhieuDangSua = chiTiet;
        SoQuySuaSoTien = chiTiet.SoTien;
        SoQuySuaThoiGian = chiTiet.ThoiGian;
        SoQuySuaTenNguoi = chiTiet.NguoiNopNhan;
        SoQuySuaNoiDung = chiTiet.NoiDung;
        SoQuySuaGhiChu = chiTiet.GhiChu;
        SoQuySuaLoiSoTien = false;
        SoQuySuaLoai = TimLoaiThuChiTheoId(chiTiet.NhomChiPhiId);
        SoQuySuaQuy = TimQuyTheoMa(chiTiet.PhuongThuc);

        OnPropertyChanged(nameof(SoQuySuaNhanTenNguoi));

        IsSoQuyChiTietVisible = false;
        IsSoQuySuaPhieuVisible = true;
    }

    [RelayCommand]
    private void DongSoQuySuaPhieu()
    {
        IsSoQuySuaPhieuVisible = false;
        SoQuyPhieuDangSua = null;
    }

    [RelayCommand]
    private async Task LuuSoQuySuaPhieuAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookEdit, "Bạn không có quyền sửa phiếu quỹ."))
        {
            return;
        }
        var chiTiet = SoQuyPhieuDangSua;

        if (chiTiet is null || IsSoQuySuaPhieuDangLuu)
            return;

        SoQuySuaLoiSoTien = SoQuySuaSoTien <= 0;

        OnPropertyChanged(nameof(SoQuyHienLoiSuaSoTien));
        OnPropertyChanged(nameof(SoQuyVienSuaSoTien));

        if (SoQuySuaLoiSoTien)
        {
            StatusMessage = "Không thể sửa phiếu: số tiền phải lớn hơn 0.";

            return;
        }

        var idNhomChiPhi = uint.TryParse(SoQuySuaLoai?.GiaTri, out var idLoai) ? idLoai : 0u;

        IsSoQuySuaPhieuDangLuu = true;

        try
        {
            await _db.SuaPhieuQuyAsync(
                chiTiet.MaPhieu,
                SoQuySuaSoTien,
                SoQuySuaThoiGian,
                idNhomChiPhi == 0 ? null : idNhomChiPhi,
                SoQuySuaQuy?.GiaTri,
                SoQuySuaTenNguoi,
                SoQuySuaNoiDung,
                SoQuySuaGhiChu);

            var maPhieu = chiTiet.MaPhieu;

            IsSoQuySuaPhieuVisible = false;
            SoQuyPhieuDangSua = null;

            StatusMessage = $"Đã sửa phiếu {maPhieu} thành công.";

            await TaiLaiSoQuyAsync();

            _ = LamMoiTongQuanNeuDangMoAsync();

            // Mở lại chi tiết để người dùng thấy ngay giá trị vừa sửa.
            var gd = SoQuyGiaoDichs.FirstOrDefault(x => x.MaPhieu == maPhieu);

            if (gd is not null)
                await MoChiTietPhieuAsync(gd);
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== LỖI SỬA PHIẾU SỔ QUỸ ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("===========================================");

            StatusMessage = "Không thể sửa phiếu. Vui lòng thử lại. (Chi tiết: " + ex.Message + ")";
        }
        finally
        {
            IsSoQuySuaPhieuDangLuu = false;
        }
    }

    private SoQuyLuaChonItem? TimLoaiThuChiTheoId(uint? id)
    {
        if (id is null)
            return null;

        var chuoi = id.Value.ToString(CultureInfo.InvariantCulture);

        return SoQuyPhieuLoaiThuChiOptions.FirstOrDefault(o => o.GiaTri == chuoi);
    }

    private SoQuyLuaChon? TimQuyTheoMa(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma))
            return null;

        var chuanHoa = ma.Trim().ToUpperInvariant();

        return SoQuyPhieuQuyOptions.FirstOrDefault(q => q.GiaTri.Trim().ToUpperInvariant() == chuanHoa);
    }

    // ================================================================
    // CHUẨN BỊ FORM
    // ================================================================

    /// <summary>Chuẩn bị form và tự sinh mã phiếu (PTxxxx / PCxxxx).</summary>
    private async Task MoSoQuyPhieuFormAsync(string tienTo)
    {
        await TaiDanhMucFormSoQuyAsync();

        ResetSoQuyPhieuForm();

        // Thời gian lấy tự động, người dùng không phải nhập tay.
        SoQuyPhieuThoiGian = DateTime.Now;
        SoQuyPhieuMa = await _db.TaoMaPhieuQuyMoiAsync(tienTo);

        // Mặc định chọn danh mục đầu tiên để người dùng không phải mở ô chọn
        // khi danh mục chỉ có một loại.
        SoQuyPhieuThuLoai = SoQuyLoaiThuOptions.FirstOrDefault();
        SoQuyPhieuChiLoai = SoQuyLoaiChiOptions.FirstOrDefault();

        // Quỹ mặc định là quỹ đầu tiên đang có; để trống thì database tự lấy.
        SoQuyPhieuQuy = SoQuyPhieuQuyOptions.FirstOrDefault();

        // Nhân viên thu/chi mặc định là người đang đăng nhập.
        SoQuyPhieuNguoiThucHien = Session.CurrentUser?.HoTen ?? "";
    }

    /// <summary>
    /// Nạp danh sách lựa chọn cho form tạo/sửa phiếu: loại thu/chi từ bảng
    /// nhom_chi_phi, quỹ từ dữ liệu đang có trong phieu_chi và nhân viên từ bảng
    /// nguoi_dung. Danh sách nào rỗng thì để rỗng, không bịa thêm mục giả.
    /// </summary>
    private async Task TaiDanhMucFormSoQuyAsync()
    {
        var loai = await _db.GetSoQuyLoaiThuChiOptionsAsync();
        var quy = await _db.GetSoQuyQuyOptionsAsync();
        var nhanVien = await _db.GetSoQuyNguoiTaoOptionsAsync();

        // Danh sách của form tái sử dụng cấu trúc item của bộ lọc nhưng không
        // gắn handler đổi bộ lọc, nên thay thế trực tiếp ở đây.
        ThayTheDanhSach(SoQuyPhieuLoaiThuChiOptions, loai);
        ThayTheDanhSach(SoQuyPhieuNguoiTaoOptions, nhanVien);

        SoQuyPhieuQuyOptions.Clear();

        foreach (var quyItem in quy)
            SoQuyPhieuQuyOptions.Add(quyItem);

        OnPropertyChanged(nameof(SoQuyLoaiThuOptions));
        OnPropertyChanged(nameof(SoQuyLoaiChiOptions));
        OnPropertyChanged(nameof(SoQuySuaLoaiOptions));
        OnPropertyChanged(nameof(SoQuyPhieuCoLoaiThuChi));
        OnPropertyChanged(nameof(SoQuyPhieuCoLoaiChi));
        OnPropertyChanged(nameof(SoQuyPhieuCoQuy));
    }

    private static void ThayTheDanhSach<T>(ObservableCollection<T> dich, IEnumerable<T> moi)
    {
        dich.Clear();

        foreach (var item in moi)
            dich.Add(item);
    }

    private void ResetSoQuyPhieuForm()
    {
        IsSoQuyPhieuThuVisible = false;
        IsSoQuyPhieuChiVisible = false;
        SoQuyPhieuMa = "";
        SoQuyPhieuThoiGian = default;
        SoQuyPhieuSoTien = 0m;
        SoQuyPhieuGhiChu = "";
        SoQuyPhieuNguoiThucHien = "";
        SoQuyPhieuTenNguoi = "";
        SoQuyPhieuNoiDung = "";
        SoQuyPhieuThuLoai = null;
        SoQuyPhieuChiLoai = null;
        SoQuyPhieuQuy = null;

        // Mở lại form ở trạng thái sạch: không hiện lỗi cho tới khi người dùng thao tác.
        SoQuyFormDaCham = false;
        SoQuyLoiNguoiThucHien = false;
        SoQuyLoiTenNguoi = false;
        RaiseSoQuyValidation();
    }

    // ================================================================
    // XUẤT EXCEL
    // ================================================================

    private static string TaoTenFileXuatSoQuy(DateTime thoiDiem)
        => $"SoQuy_{thoiDiem:yyyyMMdd_HHmmss}.xlsx";

    [RelayCommand]
    private async Task XuatFileSoQuyAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookExport, "Bạn không có quyền export sổ quỹ."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        var tenFileMacDinh = TaoTenFileXuatSoQuy(DateTime.Now);

        var file = await OwnerWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Xuất sổ quỹ",
                SuggestedFileName = tenFileMacDinh,
                DefaultExtension = "xlsx",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("Excel")
                    {
                        Patterns = ["*.xlsx"]
                    }
                ]
            });

        if (file is null)
        {
            StatusMessage = "Bạn đã hủy xuất file.";
            return;
        }

        try
        {
            StatusMessage = "Đang xuất sổ quỹ...";

            var danhSach = await _db.GetSoQuyPhieuExportAsync(SoQuyLocHienTai);

            if (danhSach.Count == 0)
            {
                await ShowMessageAsync(
                    "Xuất file thất bại",
                    "Không có phiếu nào khớp điều kiện đang xem\nnên không thể xuất file.");

                StatusMessage = "Không có dữ liệu để xuất.";

                return;
            }

            await using var stream = await file.OpenWriteAsync();

            await Task.Run(() => TaoFileExcelSoQuy(danhSach, stream));

            StatusMessage = $"Đã xuất {danhSach.Count:N0} phiếu ra file {tenFileMacDinh}.";

            await ShowMessageAsync(
                "Xuất file thành công",
                $"Đã xuất {danhSach.Count:N0} phiếu.\n\nFile:\n{file.Name}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Xuất file thất bại: " + ex.Message;

            await ShowMessageAsync(
                "Xuất file thất bại",
                $"Không thể xuất sổ quỹ.\n\nChi tiết lỗi:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Dựng file Excel của sổ quỹ. Cột bám sát danh sách trên màn hình và có
    /// thêm "Ghi chú" để không mất thông tin khi nhập lại từ chính file này.
    /// </summary>
    private static void TaoFileExcelSoQuy(IReadOnlyList<SoQuyGiaoDich> danhSach, Stream stream)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sổ quỹ");

        var tieuDeCot = new (string Ten, double DoRong)[]
        {
            ("Mã phiếu", 16),
            ("Ngày giờ", 20),
            ("Loại phiếu", 12),
            ("Người nộp/nhận", 26),
            ("Loại thu/chi", 22),
            ("Số tiền", 16),
            ("Quỹ", 14),
            ("Trạng thái", 16),
            ("Nội dung", 40),
            ("Ghi chú", 30),
            ("Người tạo", 20)
        };

        for (var c = 0; c < tieuDeCot.Length; c++)
        {
            var oTieuDe = sheet.Cell(1, c + 1);

            oTieuDe.Value = tieuDeCot[c].Ten;
            oTieuDe.Style.Font.Bold = true;
            oTieuDe.Style.Font.FontColor = XLColor.White;
            oTieuDe.Style.Fill.BackgroundColor = XLColor.FromHtml("#164B40");
            oTieuDe.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            oTieuDe.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        var dongCuoi = 1 + danhSach.Count;

        for (var i = 0; i < danhSach.Count; i++)
        {
            var gd = danhSach[i];
            var dong = i + 2;

            sheet.Cell(dong, 1).Value = gd.MaPhieu ?? "";
            sheet.Cell(dong, 2).Value = gd.ThoiGian;
            sheet.Cell(dong, 3).Value = gd.LoaiHienThi;
            sheet.Cell(dong, 4).Value = gd.NguoiNopNhan ?? "";
            sheet.Cell(dong, 5).Value = gd.TenLoai ?? "";
            sheet.Cell(dong, 6).Value = gd.GiaTri;
            sheet.Cell(dong, 7).Value = TenQuyHienThi(gd.PhuongThuc);
            sheet.Cell(dong, 8).Value = gd.TrangThaiHienThi;
            sheet.Cell(dong, 9).Value = gd.NoiDung ?? "";
            sheet.Cell(dong, 10).Value = gd.GhiChu ?? "";
            sheet.Cell(dong, 11).Value = gd.NguoiTao ?? "";

            var oTien = sheet.Cell(dong, 6);
            oTien.Style.NumberFormat.Format = "#,##0";
            oTien.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var oNgay = sheet.Cell(dong, 2);
            oNgay.Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            oNgay.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var oLoai = sheet.Cell(dong, 3);
            oLoai.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            oLoai.Style.Font.FontColor = XLColor.FromHtml(gd.LaThu ? "#1877F2" : "#D92D20");
        }

        // Một dòng tổng cộng ở cuối để số liệu trong file khớp với thẻ thống kê.
        var dongTong = dongCuoi + 1;

        sheet.Cell(dongTong, 1).Value = "TỔNG CỘNG";
        sheet.Cell(dongTong, 1).Style.Font.Bold = true;

        sheet.Cell(dongTong, 5).Value =
            $"Thu: {danhSach.Count(gd => gd.LaThu):N0} phiếu   Chi: {danhSach.Count(gd => !gd.LaThu):N0} phiếu";
        sheet.Cell(dongTong, 5).Style.Font.Bold = true;

        sheet.Cell(dongTong, 6).Value = danhSach.Sum(gd => gd.LaThu ? gd.GiaTri : -gd.GiaTri);
        sheet.Cell(dongTong, 6).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(dongTong, 6).Style.Font.Bold = true;
        sheet.Cell(dongTong, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        sheet.Row(dongTong).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4F7");

        for (var c = 0; c < tieuDeCot.Length; c++)
            sheet.Column(c + 1).AdjustToContents(1, dongCuoi, tieuDeCot[c].DoRong, 46);

        sheet.Row(1).Height = 22;
        sheet.SheetView.FreezeRows(1);

        workbook.SaveAs(stream);
    }

    /// <summary>Đổi mã quỹ trong database sang nhãn tiếng Việt; mã lạ thì giữ nguyên.</summary>
    private static string TenQuyHienThi(string ma) => string.IsNullOrWhiteSpace(ma)
        ? ""
        : ma.Trim().ToUpperInvariant() switch
        {
            "TIEN_MAT" => "Tiền mặt",
            "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Ngân hàng",
            "THE" or "THE_TIN_DUNG" => "Thẻ",
            "VI_DIEN_TU" or "VI" => "Ví điện tử",
            _ => ma
        };

    // ================================================================
    // NHẬP TỪ FILE
    //
    // Luồng: chọn file -> đọc và kiểm tra -> xem trước (có chọn cách xử lý dòng
    // trùng) -> ghi trong transaction -> báo kết quả kèm danh sách lỗi.
    // ================================================================

    [ObservableProperty] private bool isSoQuyDangNhap;

    [ObservableProperty] private string soQuyTienDoNhap = "";

    [ObservableProperty] private double soQuyPhanTramNhap;

    [RelayCommand]
    private async Task NhapFileSoQuyAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookImport, "Bạn không có quyền import sổ quỹ."))
        {
            return;
        }
        if (OwnerWindow is null || IsSoQuyDangNhap)
            return;

        var tap = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file sổ quỹ cần nhập",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel hoặc CSV")
                    {
                        // Không có .xls: ClosedXML không đọc được định dạng BIFF cũ.
                        Patterns = ["*.xlsx", "*.xlsm", "*.csv"]
                    },
                    new FilePickerFileType("Tất cả tệp") { Patterns = ["*"] }
                ]
            });

        if (tap.Count == 0)
        {
            StatusMessage = "Bạn đã hủy chọn file.";

            return;
        }

        var duongDan = tap[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(duongDan))
        {
            await ShowMessageAsync(
                "Không đọc được file",
                "Chỉ nhập được file nằm trên ổ đĩa của máy tính.");

            return;
        }

        await NhapFileSoQuyTheoDuongDanAsync(duongDan);
    }

    private async Task NhapFileSoQuyTheoDuongDanAsync(string duongDan)
    {
        IsSoQuyDangNhap = true;

        try
        {
            var preview = await _soQuyImportService.DocFileAsync(
                duongDan,
                BaoTienDoNhapSoQuy);

            SoQuyTienDoNhap = "";
            SoQuyPhanTramNhap = 0;

            if (!preview.CoTheNhap)
            {
                await ShowMessageAsync(
                    "Không có dòng nào hợp lệ",
                    "File đã đọc xong nhưng không có dòng nào để nhập.\n\n"
                    + $"Tổng số dòng: {preview.TongSoDong:N0}\n"
                    + $"Dòng lỗi: {preview.SoDongLoi:N0}\n\n"
                    + "Hãy sửa file rồi thử lại, hoặc tải file mẫu để xem định dạng.");

                if (preview.DanhSachLoi.Count > 0)
                    await ShowMessageAsync(
                        "Chi tiết lỗi",
                        string.Join("\n", preview.DanhSachLoi.Take(15).Select(l => "• " + l.MoTa)));

                return;
            }

            // Bước xem trước: người dùng kiểm tra lại và chọn cách xử lý dòng trùng.
            var xuLyTrung = await ShowSoQuyImportPreviewAsync(preview);

            if (xuLyTrung is null)
            {
                StatusMessage = "Đã hủy nhập sổ quỹ, database không bị thay đổi.";

                return;
            }

            SoQuyTienDoNhap = "Đang ghi phiếu...";

            var batDau = DateTime.Now;

            var ketQua = await _soQuyImportService.NhapAsync(preview, xuLyTrung.Value, BaoTienDoNhapSoQuy);

            ketQua.TenFile = preview.TenFile;
            ketQua.TongSoDong = preview.TongSoDong;
            ketQua.XuLyTrung = xuLyTrung.Value;
            ketQua.ThoiGian = DateTime.Now - batDau;

            SoQuyTienDoNhap = "";
            SoQuyPhanTramNhap = 0;

            await ShowSoQuyImportResultAsync(ketQua);

            StatusMessage = ketQua.DaRollback
                ? "Nhập sổ quỹ thất bại, đã rollback toàn bộ."
                : $"Nhập sổ quỹ xong: thêm {ketQua.SoThemMoi:N0}, "
                  + $"cập nhật {ketQua.SoCapNhat:N0}, bỏ qua {ketQua.SoBoQua:N0}, lỗi {ketQua.SoLoi:N0}.";

            await TaiLaiSoQuyAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy nhập sổ quỹ.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể nhập sổ quỹ: " + ex.Message;

            await ShowMessageAsync(
                "Nhập sổ quỹ thất bại",
                $"{ex.Message}\n\nNếu file đúng định dạng, hãy thử tải file mẫu và so sánh.");
        }
        finally
        {
            IsSoQuyDangNhap = false;
            SoQuyTienDoNhap = "";
            SoQuyPhanTramNhap = 0;
        }
    }

    /// <summary>Cập nhật thanh tiến độ nhập; chạy trên luồng đọc file nên phải bọc lại.</summary>
    private Task BaoTienDoNhapSoQuy(SoQuyImportProgress tienDo)
    {
        SoQuyTienDoNhap = $"{tienDo.GiaiDoan}: {tienDo.DaXuLy:N0}/{tienDo.TongSo:N0} dòng";
        SoQuyPhanTramNhap = tienDo.PhanTram;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Mở cửa sổ xem trước. Trả về cách xử lý dòng trùng do người dùng chọn,
    /// hoặc null nếu họ bấm Hủy.
    /// </summary>
    private async Task<XuLyPhieuTrung?> ShowSoQuyImportPreviewAsync(SoQuyImportPreview preview)
    {
        if (OwnerWindow is null)
            return null;

        var window = new SoQuyImportPreviewWindow
        {
            Data = preview
        };

        await window.ShowDialog(OwnerWindow);

        return window.DaXacNhan ? window.XuLyTrung : null;
    }

    /// <summary>Mở cửa sổ báo kết quả nhập.</summary>
    private async Task ShowSoQuyImportResultAsync(SoQuyImportResult ketQua)
    {
        if (OwnerWindow is null)
            return;

        var window = new SoQuyImportResultWindow
        {
            Data = ketQua
        };

        await window.ShowDialog(OwnerWindow);
    }
}