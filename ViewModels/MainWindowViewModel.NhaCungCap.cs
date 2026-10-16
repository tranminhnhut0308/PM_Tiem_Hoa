using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

/// <summary>
/// Phần ViewModel riêng cho màn hình Nhà cung cấp: tìm kiếm, bộ lọc nâng cao,
/// thống kê, phân trang và mở dòng xem chi tiết.
///
/// Danh sách luôn lấy từ database theo trang (LIMIT/OFFSET) nên dùng được với
/// hàng nghìn nhà cung cấp; vùng chi tiết chỉ được tải khi người dùng bấm vào dòng.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Số nhà cung cấp tải mỗi lần, có thể đổi trong bộ phân trang.</summary>
    private const int SupplierPageSizeMacDinh = 20;

    /// <summary>Giá trị của bộ lọc "còn nợ / không còn nợ / tất cả".</summary>
    public const string CongNoTatCa = "tatca";

    public const string CongNoConNo = "conno";

    public const string CongNoKhongConNo = "khongconno";

    /// <summary>Các dòng nhà cung cấp của trang hiện tại.</summary>
    public ObservableCollection<NhaCungCapRowViewModel> SupplierRows { get; } = new();

    /// <summary>Các lựa chọn của bộ lọc tình trạng công nợ.</summary>
    public IReadOnlyList<SoQuyLuaChonItem> SupplierCongNoOptions { get; } =
    [
        new SoQuyLuaChonItem(CongNoTatCa, "Tất cả"),
        new SoQuyLuaChonItem(CongNoConNo, "Còn nợ"),
        new SoQuyLuaChonItem(CongNoKhongConNo, "Không còn nợ")
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierHasFilter))]
    [NotifyPropertyChangedFor(nameof(SupplierFilterButtonText))]
    [NotifyPropertyChangedFor(nameof(SupplierRongText))]
    [NotifyPropertyChangedFor(nameof(SupplierRongHint))]
    private string? supplierCongNoFilter = CongNoTatCa;

    [ObservableProperty] private bool isSupplierFilterPanelOpen;

    [ObservableProperty] private bool isLoadingSuppliers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPages))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangDau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangCuoi))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangSau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(SupplierPageText))]
    private int supplierPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPages))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangDau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangCuoi))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangSau))]
    [NotifyPropertyChangedFor(nameof(CanSupplierTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(SupplierPageText))]
    [NotifyPropertyChangedFor(nameof(SupplierRangeText))]
    [NotifyPropertyChangedFor(nameof(HasSupplierRows))]
    [NotifyPropertyChangedFor(nameof(IsSupplierListEmpty))]
    private int supplierTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPages))]
    [NotifyPropertyChangedFor(nameof(SupplierRangeText))]
    private int supplierAllCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalDebtText))]
    private decimal supplierTotalDebt;

    /// <summary>Tổng tiền đã mua của các nhà cung cấp khớp bộ lọc đang xem.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierTotalPurchaseText))]
    private decimal supplierTotalPurchase;

    /// <summary>Số nhà cung cấp còn nợ, đếm cùng bộ lọc đang xem.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupplierDebtSupplierCaption))]
    private int supplierDebtSupplierCount;

    [ObservableProperty] private int supplierPageSize = SupplierPageSizeMacDinh;

    [ObservableProperty] private NhaCungCapRowViewModel? expandedSupplierRow;

    /// <summary>Số dòng đang được tick chọn ở trang hiện tại.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSupplierRows))]
    [NotifyPropertyChangedFor(nameof(SupplierSelectedCountText))]
    private int supplierSelectedCount;

    [ObservableProperty] private bool isAllSupplierRowsSelected;

    /// <summary>Hủy tìm kiếm realtime khi người dùng gõ tiếp từ khóa mới.</summary>
    private CancellationTokenSource? _supplierSearchCts;

    // ============================================================
    // TEXT HIỂN THỊ
    // ============================================================

    /// <summary>
    /// Panel lọc chỉ còn một điều kiện là "Tình trạng công nợ".
    /// Ô tìm kiếm ở thanh trên cố ý không tính vào đây.
    /// </summary>
    public bool SupplierHasFilter => !string.IsNullOrWhiteSpace(SupplierCongNoFilter)
        && SupplierCongNoFilter != CongNoTatCa;

    public int SupplierTotalPages
    {
        get
        {
            if (SupplierPageSize <= 0)
                return 1;

            return Math.Max(1, (SupplierTotalCount + SupplierPageSize - 1) / SupplierPageSize);
        }
    }

    public bool CanSupplierTrangDau => SupplierPage > 1;

    public bool CanSupplierTrangCuoi => SupplierPage < SupplierTotalPages;

    public bool CanSupplierTrangSau => SupplierPage < SupplierTotalPages;

    public bool CanSupplierTrangTruoc => SupplierPage > 1;

    /// <summary>Khoảng dòng đang hiển thị, ví dụ "Hiển thị 1-20 trên 137 nhà cung cấp".</summary>
    public string SupplierRangeText
    {
        get
        {
            if (SupplierTotalCount == 0)
                return "Không có nhà cung cấp nào phù hợp";

            var dau = (SupplierPage - 1) * SupplierPageSize + 1;
            var cuoi = Math.Min(SupplierPage * SupplierPageSize, SupplierTotalCount);

            return $"Hiển thị {dau}-{cuoi} trên tổng {SupplierTotalCount:N0} nhà cung cấp";
        }
    }

    public string SupplierTotalDebtText
        => MoneyInput.Format(SupplierTotalDebt) + " đ";

    public string SupplierTotalPurchaseText
        => MoneyInput.Format(SupplierTotalPurchase) + " đ";

    public string SupplierDebtSupplierCaption
        => $"{SupplierDebtSupplierCount:N0} nhà cung cấp còn nợ";

    /// <summary>Nhãn trang hiện tại, ví dụ "Trang 2 / 7".</summary>
    public string SupplierPageText => $"Trang {SupplierPage} / {SupplierTotalPages}";

    public string SupplierRongText => SupplierHasFilter
        ? "Không tìm thấy nhà cung cấp nào phù hợp với điều kiện lọc"
        : "Chưa có nhà cung cấp nào trong hệ thống";

    public string SupplierRongHint => SupplierHasFilter
        ? "Hãy thử xoá bớt điều kiện lọc hoặc nhập từ khóa khác."
        : "Bấm \"Thêm nhà cung cấp\" hoặc nhập danh sách từ file Excel/CSV.";

    public bool IsSupplierListEmpty => SupplierRows.Count == 0;

    public bool HasSupplierRows => SupplierRows.Count > 0;

    public bool HasSelectedSupplierRows => SupplierSelectedCount > 0;

    /// <summary>
    /// Báo lại trạng thái rỗng/không rỗng của danh sách sau khi <see cref="SupplierRows"/>
    /// vừa được nạp lại.
    ///
    /// Hai thuộc tính trên là biểu thức tính theo <see cref="SupplierRows"/>, mà
    /// SupplierTotalCount lại được gán TRƯỚC khi danh sách được nạp, nên thông báo
    /// của nó tới lúc đánh giá vẫn thấy danh sách rỗng và dòng "Chưa có nhà cung
    /// cấp nào trong hệ thống" cứ hiện lại dù đã có dữ liệu. Gọi hàm này sau khi
    /// nạp xong danh sách để giao diện đánh giá lại đúng số dòng đang hiển thị.
    /// </summary>
    private void CapNhatTrangThaiDanhSachNhaCungCap()
    {
        OnPropertyChanged(nameof(IsSupplierListEmpty));
        OnPropertyChanged(nameof(HasSupplierRows));
        OnPropertyChanged(nameof(SupplierRangeText));
        OnPropertyChanged(nameof(IsAllSupplierRowsSelected));
    }

    public string SupplierSelectedCountText => $"Đã chọn {SupplierSelectedCount} nhà cung cấp";

    /// <summary>
    /// Nhãn nút bộ lọc. Chỉ có một điều kiện là tình trạng công nợ nên nhãn
    /// ghi rõ đang lọc gì: "Bộ lọc", "Bộ lọc (Còn nợ)", "Bộ lọc (Không còn nợ)".
    /// </summary>
    public string SupplierFilterButtonText => SupplierCongNoFilter switch
    {
        CongNoConNo => "Bộ lọc (Còn nợ)",
        CongNoKhongConNo => "Bộ lọc (Không còn nợ)",
        _ => "Bộ lọc"
    };

    /// <summary>Mũi tên của nút mở/đóng menu bộ lọc.</summary>
    public string SupplierFilterArrowText => IsSupplierFilterPanelOpen ? "▲" : "▼";

    partial void OnIsSupplierFilterPanelOpenChanged(bool value)
        => OnPropertyChanged(nameof(SupplierFilterArrowText));

    // ============================================================
    // TẢI DANH SÁCH THEO TRANG
    // ============================================================

    /// <summary>
    /// Tải trang nhà cung cấp hiện tại kèm số liệu thống kê.
    /// Mọi điều kiện lọc đều được đẩy xuống SQL.
    /// </summary>
    public async Task LoadSupplierPageAsync()
    {
        if (IsLoadingSuppliers)
            return;

        IsLoadingSuppliers = true;

        try
        {
            var boLoc = TaoBoLocNhaCungCap();

            if (SupplierPageSize <= 0)
                SupplierPageSize = SupplierPageSizeMacDinh;

            // Trang vượt quá số trang thực tế sau khi lọc: đưa về trang cuối.
            var soTrangToiDa = Math.Max(
                1,
                (SupplierTotalCount + SupplierPageSize - 1) / SupplierPageSize);

            if (SupplierPage > soTrangToiDa)
                SupplierPage = soTrangToiDa;

            var thongKe = await _db.GetThongKeDanhSachNhaCungCapAsync(boLoc);
            var duLieu = await _db.GetNhaCungCapPagedAsync(
                boLoc,
                SupplierPage,
                SupplierPageSize);

            SupplierTotalCount = duLieu.TongSoBanGhi;
            SupplierAllCount = thongKe.TongSoNhaCungCap;
            SupplierTotalDebt = thongKe.TongCongNo;
            SupplierTotalPurchase = thongKe.TongMua;
            SupplierDebtSupplierCount = thongKe.SoNhaCungCapConNo;

            SupplierRows.Clear();

            foreach (var ncc in duLieu.Items)
            {
                var dong = new NhaCungCapRowViewModel(ncc, this);
                dong.PropertyChanged += DongNhaCungCap_PropertyChanged;
                SupplierRows.Add(dong);
            }

            // Đóng dòng đang mở rộng vì dữ liệu trang đã được thay mới.
            ExpandedSupplierRow = null;

            SupplierSelectedCount = 0;

            // Đánh giá lại trạng thái rỗng sau khi danh sách đã nạp xong.
            CapNhatTrangThaiDanhSachNhaCungCap();
        }
        catch (Exception ex)
        {
            SupplierRows.Clear();
            SupplierTotalCount = 0;
            SupplierSelectedCount = 0;

            CapNhatTrangThaiDanhSachNhaCungCap();

            StatusMessage = $"Lỗi tải danh sách nhà cung cấp: {ex.Message}";
        }
        finally
        {
            IsLoadingSuppliers = false;
        }
    }

    /// <summary>
    /// Gom điều kiện trên giao diện thành đối tượng bộ lọc cho database.
    /// Panel lọc chỉ còn "Tình trạng công nợ"; từ khóa tìm kiếm lấy từ thanh tìm kiếm.
    /// </summary>
    private BoLocNhaCungCap TaoBoLocNhaCungCap() => new()
    {
        TimKiem = SupplierSearch?.Trim() ?? "",
        CongNoMode = ChuyenCongNoFilterSangSo(SupplierCongNoFilter)
    };

    /// <summary>Đổi lựa chọn trên ComboBox thành mã số mà SQL hiểu.</summary>
    private static int ChuyenCongNoFilterSangSo(string? giaTri) => giaTri switch
    {
        "conno" => BoLocNhaCungCap.CongNoConNo,
        "khongconno" => BoLocNhaCungCap.CongNoKhongConNo,
        _ => BoLocNhaCungCap.CongNoTatCa
    };

    // ============================================================
    // LỆNH TÌM KIẾM / LỌC / PHÂN TRANG
    // ============================================================

    /// <summary>Tìm kiếm theo từ khóa và bộ lọc nâng cao, luôn quay về trang 1.</summary>
    [RelayCommand]
    private async Task SearchSuppliersAsync()
    {
        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private void ToggleSupplierFilter()
        => IsSupplierFilterPanelOpen = !IsSupplierFilterPanelOpen;

    /// <summary>
    /// Tìm kiếm realtime: chờ người dùng ngừng gõ 350ms mới gọi database,
    /// tránh mỗi ký tự lại chạy một câu truy vấn.
    /// </summary>
    public void TimKiemNhaCungCapTuDong()
    {
        _supplierSearchCts?.Cancel();
        _supplierSearchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _supplierSearchCts = cts;

        _ = TimKiemNhaCungCapSauDebounceAsync(cts.Token);
    }

    private async Task TimKiemNhaCungCapSauDebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);

            if (token.IsCancellationRequested)
                return;

            await SearchSuppliersAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp: huỷ lần tìm kiếm trước, không cần báo lỗi.
        }
    }

    /// <summary>Tải lại trang hiện tại, dùng cho nút làm mới.</summary>
    [RelayCommand]
    private async Task RefreshSuppliersAsync()
        => await LoadSupplierPageAsync();

    [RelayCommand]
    private async Task ApplySupplierFilterAsync()
    {
        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task ClearSupplierFilterAsync()
    {
        SupplierCongNoFilter = CongNoTatCa;
        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierDenTrangAsync()
    {
        if (!CanSupplierTrangSau)
            return;

        SupplierPage = SupplierTotalPages;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierTrangDauAsync()
    {
        if (!CanSupplierTrangTruoc)
            return;

        SupplierPage = 1;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierTrangSauAsync()
    {
        if (!CanSupplierTrangSau)
            return;

        SupplierPage++;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierTrangTruocAsync()
    {
        if (!CanSupplierTrangTruoc)
            return;

        SupplierPage--;

        await LoadSupplierPageAsync();
    }

    [RelayCommand]
    private async Task SupplierDenTrangNhatAsync(int trang)
    {
        if (trang < 1 || trang > SupplierTotalPages || trang == SupplierPage)
            return;

        SupplierPage = trang;

        await LoadSupplierPageAsync();
    }

    // ============================================================
    // MỞ DÒNG XEM CHI TIẾT
    // ============================================================

    /// <summary>
    /// Mở hoặc đóng vùng chi tiết của một dòng. Chỉ một dòng mở tại một thời điểm,
    /// dữ liệu chi tiết chỉ được tải ở lần mở đầu tiên.
    /// </summary>
    [RelayCommand]
    private async Task ToggleSupplierDetailAsync(NhaCungCapRowViewModel? row)
    {
        if (row is null)
            return;

        if (row.IsExpanded)
        {
            ExpandedSupplierRow = null;
            row.IsExpanded = false;
            return;
        }

        if (ExpandedSupplierRow is not null && !ReferenceEquals(ExpandedSupplierRow, row))
            ExpandedSupplierRow.IsExpanded = false;

        ExpandedSupplierRow = row;
        row.IsExpanded = true;

        if (row.Detail is not null)
            return;

        var detail = new NhaCungCapDetailViewModel
        {
            Supplier = row.Supplier
        };

        row.Detail = detail;

        detail.IsLoading = true;

        try
        {
            var duLieu = await _db.GetNhaCungCapByIdAsync(row.SupplierId);

            if (duLieu is not null)
            {
                detail.Supplier = duLieu;
            }

            var phieuNhap = await _db.GetPhieuNhapCuaNhaCungCapAsync(row.SupplierId, 100);
            detail.NapPhieuNhapHang(phieuNhap);

            var lichSu = await _db.GetLichSuCongNoNhaCungCapAsync(row.SupplierId, 100);
            detail.NapLichSuCongNo(lichSu);

            var thongKe = await _db.GetThongKeTaiChinhNhaCungCapAsync(row.SupplierId);
            detail.ThongKe = thongKe;
        }
        catch (Exception ex)
        {
            detail.ThongKe = null;
            StatusMessage = $"Lỗi tải chi tiết nhà cung cấp: {ex.Message}";
        }
        finally
        {
            detail.IsLoading = false;
        }
    }

    // ============================================================
    // CHỌN DÒNG
    // ============================================================

    private void DongNhaCungCap_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Chỉ cần đếm lại số dòng đã tick, không cần xử lý thuộc tính khác.
        if (e.PropertyName == nameof(NhaCungCapRowViewModel.IsSelected))
        {
            SupplierSelectedCount = SupplierRows.Count(dong => dong.IsSelected);
            CapNhatTrangThaiChonTatCaNhaCungCap();
        }
    }

    /// <summary>Bộ tick chọn tất cả chỉ theo trang hiện tại.</summary>
    partial void OnIsAllSupplierRowsSelectedChanged(bool value)
    {
        foreach (var dong in SupplierRows)
        {
            if (dong.IsSelected != value)
                dong.IsSelected = value;
        }

        SupplierSelectedCount = SupplierRows.Count(dong => dong.IsSelected);
    }

    /// <summary>Giữ checkbox "Chọn tất cả" đồng bộ khi người dùng tick từng dòng.</summary>
    public void CapNhatTrangThaiChonTatCaNhaCungCap()
    {
        IsAllSupplierRowsSelected = SupplierRows.Count > 0
            && SupplierRows.All(dong => dong.IsSelected);
    }

    /// <summary>Bỏ chọn tất cả dòng trên trang hiện tại.</summary>
    [RelayCommand]
    private void BoChonTatCaSupplierRows()
    {
        foreach (var dong in SupplierRows)
            dong.IsSelected = false;

        SupplierSelectedCount = 0;
        IsAllSupplierRowsSelected = false;
    }

    // ============================================================
    // XUẤT FILE EXCEL
    // ============================================================

    /// <summary>Tên file mặc định khi mở hộp thoại lưu, ví dụ DanhSachNhaCungCap_20261001_161500.xlsx</summary>
    public static string TaoTenFileXuatNhaCungCap(DateTime thoiDiem)
        => $"DanhSachNhaCungCap_{thoiDiem:yyyyMMdd_HHmmss}.xlsx";

    /// <summary>
    /// Xuất toàn bộ nhà cung cấp đang được lọc ra file Excel.
    ///
    /// Cố tình gọi lại đúng <see cref="TaoBoLocNhaCungCap"/> của màn hình nên
    /// từ khoá tìm kiếm và bộ lọc nâng cao được giữ nguyên, và lấy hết từ
    /// database (không phân trang) nên xuất đủ 48 dòng dù trang đang xem 20 dòng.
    /// </summary>
    [RelayCommand]
    private async Task XuatFileNhaCungCapAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersExport, "Bạn không có quyền export nhà cung cấp."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        // Đặt tên file trước để hiện sẵn trong hộp thoại lưu.
        var tenFileMacDinh = TaoTenFileXuatNhaCungCap(DateTime.Now);

        var file = await OwnerWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Xuất danh sách nhà cung cấp",

                SuggestedFileName = tenFileMacDinh,

                DefaultExtension = "xlsx",

                // Không tự động ghi đè file đang tồn tại.
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
            StatusMessage = "Đang xuất danh sách nhà cung cấp...";

            // Xuất đúng bộ lọc đang xem trên màn hình, lấy hết bản ghi.
            var boLoc = TaoBoLocNhaCungCap();

            var danhSach = await _db.GetAllNhaCungCapForExportAsync(boLoc);

            if (danhSach.Count == 0)
            {
                await ShowMessageAsync(
                    "Xuất file thất bại",
                    "Không có nhà cung cấp nào khớp điều kiện đang xem\n" +
                    "nên không thể xuất file.");

                StatusMessage = "Không có dữ liệu để xuất.";
                return;
            }

            // Dựng file trên luồng nền cho danh sách lớn, không làm treo giao diện.
            await using var stream = await file.OpenWriteAsync();

            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();

                GhiSheetNhaCungCap(workbook, danhSach);

                workbook.SaveAs(stream);
            });

            StatusMessage =
                $"✅ Đã xuất {danhSach.Count:N0} nhà cung cấp ra file {tenFileMacDinh}.";

            await ShowMessageAsync(
                "Xuất file thành công",
                $"Đã xuất {danhSach.Count:N0} nhà cung cấp.\n\n" +
                $"File:\n{file.Name}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Xuất file thất bại: " + ex.Message;

            await ShowMessageAsync(
                "Xuất file thất bại",
                $"Không thể xuất danh sách nhà cung cấp.\n\n" +
                $"Chi tiết lỗi:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Ghi dữ liệu nhà cung cấp ra sheet: tiêu đề in đậm có nền, các cột tiền
    /// ghi bằng kiểu số thập phân (người dùng SUM/FILTER được), ngày ghi bằng
    /// kiểu DateTime, độ rộng cột tự co theo nội dung.
    /// </summary>
    private static void GhiSheetNhaCungCap(
        XLWorkbook workbook,
        IReadOnlyList<NhaCungCap> danhSach)
    {
        var sheet = workbook.Worksheets.Add("Nhà cung cấp");

        // (Tiêu đề cột, độ rộng tối thiểu)
        var tieuDeCot = new (string Ten, double DoRong)[]
        {
            ("Mã nhà cung cấp", 16),
            ("Tên nhà cung cấp", 32),
            ("Điện thoại", 15),
            ("Email", 26),
            ("Địa chỉ", 34),
            ("Khu vực", 14),
            ("Phường/Xã", 18),
            ("Tổng mua", 18),
            ("Nợ cần trả hiện tại", 20),
            ("Mã số thuế", 15),
            ("Số CMND/CCCD", 16),
            ("Ghi chú", 28),
            ("Nhóm nhà cung cấp", 20),
            ("Trạng thái", 16),
            ("Tổng mua trừ trả hàng", 22),
            ("Công ty", 24),
            ("Người tạo", 18),
            ("Ngày tạo", 20)
        };

        // Dòng tiêu đề.
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

        // Các cột tiền và ngày: căn phải, tiền là số thập phân thuần.
        var cotTien = new HashSet<int> { 8, 9, 15 };

        for (var i = 0; i < danhSach.Count; i++)
        {
            var ncc = danhSach[i];
            var dong = i + 2;

            sheet.Cell(dong, 1).Value = ncc.MaNhaCungCap ?? "";
            sheet.Cell(dong, 2).Value = ncc.TenNhaCungCap ?? "";
            sheet.Cell(dong, 3).Value = ncc.SoDienThoai ?? "";
            sheet.Cell(dong, 4).Value = ncc.Email ?? "";
            sheet.Cell(dong, 5).Value = ncc.DiaChi ?? "";
            sheet.Cell(dong, 6).Value = ncc.KhuVuc ?? "";
            sheet.Cell(dong, 7).Value = ncc.PhuongXa ?? "";

            // Ghi bằng giá trị số thập phân để Excel nhận đúng kiểu Number.
            sheet.Cell(dong, 8).Value = ncc.TongMua;
            sheet.Cell(dong, 9).Value = ncc.ConNo;

            sheet.Cell(dong, 10).Value = ncc.MaSoThue ?? "";
            sheet.Cell(dong, 11).Value = ncc.SoCccd ?? "";
            sheet.Cell(dong, 12).Value = ncc.GhiChu ?? "";
            sheet.Cell(dong, 13).Value = ncc.NhomNhaCungCapTen ?? "";
            sheet.Cell(dong, 14).Value = ncc.TrangThaiHienThi;

            sheet.Cell(dong, 15).Value = ncc.TongMuaTruTraHang;
            sheet.Cell(dong, 16).Value = ncc.TenCongTy ?? "";
            sheet.Cell(dong, 17).Value = ncc.NguoiTaoTen ?? "";

            // Ưu tiên ngày nhập từ file, không có thì dùng ngày tạo trong database.
            var ngayTao = ncc.NgayNhapExcel ?? ncc.NgayTao;

            if (ngayTao.HasValue)
                sheet.Cell(dong, 18).Value = ngayTao.Value;
            else
                sheet.Cell(dong, 18).Clear(XLClearOptions.Contents);

            foreach (var cotTienID in cotTien)
            {
                var oTien = sheet.Cell(dong, cotTienID);
                oTien.Style.NumberFormat.Format = "#,##0";
                oTien.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            // Ngày ghi dạng DateTime, hiển thị dd/MM/yyyy HH:mm.
            var oNgay = sheet.Cell(dong, 18);
            oNgay.Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            oNgay.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Độ rộng cột tự co theo nội dung, giới hạn trên/dưới để không bị
        // quá hẹp (tiêu đề dài) hoặc quá rộng (mô tả dài).
        var dongCuoi = 1 + danhSach.Count;

        for (var c = 0; c < tieuDeCot.Length; c++)
        {
            sheet.Column(c + 1)
                .AdjustToContents(1, dongCuoi, tieuDeCot[c].DoRong, 46);
        }

        sheet.Row(1).Height = 22;
        sheet.SheetView.FreezeRows(1);
    }
}
