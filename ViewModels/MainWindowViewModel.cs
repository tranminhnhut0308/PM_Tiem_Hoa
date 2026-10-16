using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using System.Threading;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;
using System.Globalization;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _customerSearchCts;
    private int _customerLoadVersion;
    private Task? _customerLoadTask;
    private CancellationTokenSource? _productSearchCts;
    private int _productLoadVersion;
    public Window? OwnerWindow { get; set; }
    private bool _customersLoaded;
    private bool _invoicesLoaded;
    private bool _overviewLoaded;
    private bool _customerNhomOptionsLoaded;
    private CancellationTokenSource? _baoCaoPhieuNhapSearchCts;
    private int _baoCaoPhieuNhapLoadVersion;
    private bool _baoCaoPhieuNhapLoaded;
    private bool _baoCaoCuoiNgayLoaded;
    private bool _baoCaoBanHangLoaded;
    private bool _baoCaoHangHoaLoaded;
    private bool _baoCaoKhachHangLoaded;
    private bool _baoCaoNhaCungCapLoaded;

    /// <summary>
    /// Màn "Tổng quan" có ViewModel riêng. Giữ một instance sống suốt phiên để
    /// giữ nguyên bộ lọc người dùng đang chọn khi chuyển sang màn khác rồi quay lại.
    /// </summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>
    /// Màn "Báo cáo cuối ngày" cũng giữ một instance sống suốt phiên để nhớ ngày
    /// đang xem giữa các lần chuyển màn.
    /// </summary>
    /// <summary>Màn "Báo cáo bán hàng" cũng giữ một instance sống suốt phiên.</summary>
    public BaoCaoBanHangViewModel BaoCaoBanHang { get; }

    /// <summary>Màn "Báo cáo hàng hóa" cũng giữ một instance sống suốt phiên.</summary>
    public BaoCaoHangHoaViewModel BaoCaoHangHoa { get; }

    /// <summary>Màn "Báo cáo khách hàng" cũng giữ một instance sống suốt phiên.</summary>
    public BaoCaoKhachHangViewModel BaoCaoKhachHang { get; }

    /// <summary>Màn "Báo cáo nhà cung cấp" cũng giữ một instance sống suốt phiên.</summary>
    public BaoCaoNhaCungCapViewModel BaoCaoNhaCungCap { get; }

    public BaoCaoCuoiNgayViewModel BaoCaoCuoiNgay { get; }

    /// <summary>
    /// Nguồn dữ liệu thông tin tiệm dùng chung. Mọi mẫu hóa đơn và màn "Thông
    /// tin tiệm" đều đọc/ghi qua đúng service này nên không tự tạo bản sao riêng.
    /// </summary>
    private readonly StoreInfoService _storeInfo;

    /// <summary>Màn "Thông tin tiệm". Giữ instance sống suốt phiên như các màn khác.</summary>
    public ThongTinTiemViewModel ThongTinTiem { get; }

    public MainWindowViewModel()
    {
        Dashboard = new DashboardViewModel(_db);
        _storeInfo = new StoreInfoService(_db.TaoKetNoiAsync);
        BaoCaoBanHang = new BaoCaoBanHangViewModel(_db);
        BaoCaoHangHoa = new BaoCaoHangHoaViewModel(_db);
        BaoCaoKhachHang = new BaoCaoKhachHangViewModel(_db);
        BaoCaoNhaCungCap = new BaoCaoNhaCungCapViewModel(_db);

        if (!CanViewDashboard)
        {
            if (CanViewProducts) SelectedMenu = "Hàng hóa";
            else if (CanViewPurchases) SelectedMenu = "Nhập hàng";
            else if (CanViewInvoices) SelectedMenu = "Hóa đơn";
            else if (CanViewCustomers) SelectedMenu = "Khách hàng";
            else if (CanViewSuppliers) SelectedMenu = "Nhà cung cấp";
            else if (CanViewCashBook) SelectedMenu = "Sổ quỹ";
            else if (CanViewReports) SelectedMenu = "Báo cáo";
            else SelectedMenu = "";
        }

        BaoCaoCuoiNgay = new BaoCaoCuoiNgayViewModel(_db)
        {
            // Cửa sổ chính chỉ có sau khi ViewModel được tạo, nên các callback
            // đọc nó theo thời điểm dùng thay vì chụp giá trị lúc khởi tạo.
            LayCuaSoChinh = () => OwnerWindow,
            CapNhatTrangThai = message => StatusMessage = message,
            HienThiHopNhat = (tieuDe, noiDung) => ShowMessageAsync(tieuDe, noiDung)
        };

        ThongTinTiem = new ThongTinTiemViewModel(_storeInfo)
        {
            LayCuaSoChinh = () => OwnerWindow,
            CapNhatTrangThai = message => StatusMessage = message,
            HienThiHopNhat = (tieuDe, noiDung) => ShowMessageAsync(tieuDe, noiDung),
            // Nút Lưu khoá theo quyền SettingsEdit: người chỉ được xem thì vẫn
            // thấy thông tin hiện tại nhưng không sửa được.
            CoTheChinhSua = () => CanEditStoreInfo
        };

        // Giỏ hàng thêm/bớt dòng thì trạng thái nút "Thanh toán" phải cập nhật
        // theo, nếu không nút sẽ mở khi giỏ hàng vẫn còn trống.
        SaleCart.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanThanhToan));

        // Gọi load danh sách nhóm sản phẩm tự động khi ViewModel khởi tạo
        _ = LoadNhomSanPhamAsync();
    }

    /// <summary>
    /// Đọc thông tin tiệm để đưa vào hóa đơn. Gọi mỗi lần mở hóa đơn nên hóa
    /// đơn luôn thấy thông tin vừa sửa ở màn "Thông tin tiệm".
    /// </summary>
    private async Task<ThongTinTiem> LayThongTinTiemChoHoaDonAsync()
        => await _storeInfo.LayThongTinKemLogoAsync();

    [ObservableProperty] private string selectedMenu = "Tổng quan";
    [ObservableProperty] private int selectedDataTabIndex = 0;
    [ObservableProperty] private string selectedReportKey = "";
    [ObservableProperty] private string statusMessage = "Sẵn sàng";
    [ObservableProperty] private bool isImporting;
    [ObservableProperty]
    private string importStatus = "";

    /// <summary>
    /// Thanh tiến độ xác định 0-100 cho các luồng import biết trước tổng số
    /// đơn vị công việc. Luồng không biết trước thì để false để hiện thanh
    /// chạy vô hạn như cũ.
    /// </summary>
    [ObservableProperty] private bool importHasProgress;
    [ObservableProperty] private double importProgress;
    [ObservableProperty] private bool coBanCapNhatMoi;
    [ObservableProperty] private string phienBanMoiText = "";
    [ObservableProperty] private string productSearch = "";
    [ObservableProperty] private string customerSearch = "";
    [ObservableProperty] private string invoiceSearch = "";
    [ObservableProperty] private string supplierSearch = "";
    [ObservableProperty] private string productFilter = "";
    private bool _sortByPrice;

    public ObservableCollection<SanPham> SanPhams { get; } = new();

    /// <summary>
    /// Tổng số hàng hóa trong database khớp từ khóa đang gõ.
    /// Được MySQL đếm trên toàn bộ bảng san_pham, không phụ thuộc số dòng đang hiển thị.
    /// </summary>
    [ObservableProperty] private int productTotalCount;

    public bool IsSanPhamListEmpty => SanPhams.Count == 0;

    /// <summary>Dòng tiêu đề hiển thị khi bảng hàng hóa rỗng, đổi theo từ khóa đang tìm.</summary>
    public string SanPhamEmptyText =>
        string.IsNullOrWhiteSpace(ProductSearch)
            ? "Chưa có hàng hóa nào trong hệ thống"
            : $"Không tìm thấy hàng hóa với từ khóa \"{ProductSearch.Trim()}\"";

    public string SanPhamEmptyHint =>
        string.IsNullOrWhiteSpace(ProductSearch)
            ? "Bấm “＋ Nhập hàng” hoặc “⇩ Import file” để thêm hàng hóa."
            : "Thử từ khóa khác hoặc xóa nội dung ô tìm kiếm.";

    /// <summary>
    /// Ô tìm kiếm hàng hóa bind TwoWay nên khi người dùng gõ thì SearchText đổi.
    /// Tự tìm lại ngay ở đây, không cần nhấn Enter và không cần bấm nút tìm kiếm.
    /// </summary>
    partial void OnProductSearchChanged(string value)
    {
        OnPropertyChanged(nameof(SanPhamEmptyText));
        OnPropertyChanged(nameof(SanPhamEmptyHint));

        TimKiemSanPhamTuDong();
    }

    /// <summary>Các dòng khách hàng của trang hiện tại (đã phân trang).</summary>
    public ObservableCollection<KhachHangRowViewModel> CustomerRows { get; } = new();

    public ObservableCollection<HoaDon> HoaDons { get; } = new();
    public ObservableCollection<SanPham> ProductSearchResults { get; } = new();
    public ObservableCollection<SaleCartItem> SaleCart { get; } = new();
    public ObservableCollection<KhachHang> SaleCustomerSuggestions { get; } = new();
    public IReadOnlyList<string> DiscountModes { get; } = ["Số tiền", "Phần trăm"];

    [ObservableProperty] private string saleSearchText = "";
    [ObservableProperty] private bool isSalesPanelVisible;
    [ObservableProperty] private string saleCustomerName = "Khách lẻ";
    [ObservableProperty] private string saleCustomerPhone = "";
    [ObservableProperty] private string saleCustomerAddress = "";
    [ObservableProperty] private bool saleCanGiaoHang;
    [ObservableProperty] private bool saleInKemSdtDiaChi;

    partial void OnSaleCanGiaoHangChanged(bool value)
    {
        if (value)
            SaleInKemSdtDiaChi = true;
    }

    /// <summary>Ghi chú riêng của hóa đơn (yêu cầu riêng của khách...).</summary>
    [ObservableProperty] private string saleGhiChu = "";

    [ObservableProperty] private decimal saleTotal;
    [ObservableProperty] private decimal saleSubtotal;
    [ObservableProperty] private decimal saleDiscountInput;
    [ObservableProperty] private decimal saleDiscountAmount;
    [ObservableProperty]
    private decimal tienKhachDua = 0;

    [ObservableProperty]
    private decimal tienThoi = 0;

    [ObservableProperty]
    private decimal tienCongNo = 0;

    [ObservableProperty]
    private string trangThaiThanhToan = "";
    [ObservableProperty] private string saleDiscountMode = "Số tiền";
    [ObservableProperty] private NhaCungCap? selectedNhaCungCap;
    [ObservableProperty] private KhachHang? selectedSaleCustomer;

    /// <summary>
    /// Đang bán cho "Khách lẻ" (không gắn hồ sơ khách hàng) hay không.
    /// <para>
    /// Khách lẻ KHÔNG được phép ghi nợ nên bắt buộc thanh toán đủ. Nhận diện theo
    /// hồ sơ khách hàng (SelectedSaleCustomer rỗng hoặc chưa có Id), tuyệt đối không
    /// dựa vào tên hiển thị vì tên có thể trùng với khách hàng thật.
    /// </para>
    /// </summary>
    public bool LaKhachLe => SelectedSaleCustomer is null || SelectedSaleCustomer.Id == 0;

    /// <summary>
    /// Đổi khách hàng bán (kể cả về "Khách lẻ") phải tính lại tiền và kiểm tra lại
    /// công nợ, tránh trường hợp đang nợ của khách cũ rồi đổi sang khách lẻ mà vẫn lưu.
    /// </summary>
    partial void OnSelectedSaleCustomerChanged(KhachHang? value)
    {
        OnPropertyChanged(nameof(LaKhachLe));
        RecalculatePayment();
    }

    // =========================================================
    // TRẠNG THÁI ĐƠN HÀNG ĐANG BÁN
    // =========================================================

    /// <summary>
    /// Khoá chống bấm nút "Thanh toán" nhiều lần / gọi lệnh chồng nhau.
    /// Trước đây lệnh thanh toán không có cờ này nên bấm hai lần là tạo hai hóa đơn.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanThanhToan))]
    [NotifyPropertyChangedFor(nameof(TrangThaiXuLyThanhToan))]
    private bool isThanhToanDangXuLy;

    /// <summary>Có thể bấm "Thanh toán" hay không (có hàng + có quyền thanh toán).</summary>
    public bool CanThanhToan => !IsThanhToanDangXuLy
                                && SaleCart.Count > 0
                                && Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SalesPayment);

    /// <summary>Chữ hiển thị trên nút thanh toán khi đang xử lý.</summary>
    public string TrangThaiXuLyThanhToan =>
        IsThanhToanDangXuLy ? "Đang lưu..." : "Thanh toán";

    /// <summary>Mã hóa đơn của đơn đang bán (xem trước, do database sinh khi lưu).</summary>
    [ObservableProperty] private string saleMaHoaDonHienThi = "";

    // =========================================================
    // DANH SÁCH KHÁCH HÀNG: PHÂN TRANG + MỞ RỘNG CHI TIẾT
    // =========================================================

    /// <summary>Trang đang xem (bắt đầu từ 1).</summary>
    [ObservableProperty] private int customerPage = 1;

    /// <summary>Số khách hàng hiển thị trên một trang.</summary>
    [ObservableProperty] private int customerPageSize = 20;

    /// <summary>Tổng số khách hàng khớp điều kiện tìm kiếm (trên toàn database).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerTotalPages))]
    [NotifyPropertyChangedFor(nameof(CustomerRangeText))]
    [NotifyPropertyChangedFor(nameof(CanCustomerTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanCustomerTrangSau))]
    [NotifyPropertyChangedFor(nameof(HasCustomerRows))]
    [NotifyPropertyChangedFor(nameof(IsCustomerListEmpty))]
    private int customerTotalCount;

    /// <summary>
    /// Tổng số khách hàng đang có trong hệ thống, không phụ thuộc từ khóa tìm kiếm.
    /// Đây là con số hiển thị trên thẻ "Tổng khách hàng".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerRangeText))]
    private int customerAllCount;

    /// <summary>Số khách hàng mới phát sinh trong khoảng số ngày gần nhất.</summary>
    [ObservableProperty] private int customerNewCount;

    /// <summary>
    /// Chữ dưới thẻ "Khách hàng mới". Lấy từ đúng hằng số mà query dùng để đếm
    /// để chữ này không bao giờ lệch với số liệu đang hiển thị.
    /// </summary>
    public string CustomerNewCountCaption =>
        $"Phát sinh trong {DatabaseService.SoNgayKhachMoiMacDinh} ngày gần nhất";

    /// <summary>Tổng nợ hiện tại của toàn bộ khách hàng (cùng cách tính với cột tiền trong bảng).</summary>
    [ObservableProperty] private decimal customerTotalDebt;

    /// <summary>Tổng tiền bán của toàn bộ khách hàng.</summary>
    [ObservableProperty] private decimal customerTotalSales;

    // =========================================================
    // BỘ LỌC DANH SÁCH KHÁCH HÀNG (NHÓM + TÌNH TRẠNG CÔNG NỢ)
    // =========================================================

    /// <summary>Các nhóm khách hàng cho ô lọc; dòng đầu tiên là "Tất cả nhóm khách hàng" (Id = 0).</summary>
    public ObservableCollection<NhomKhachHang> CustomerNhomOptions { get; } = new();

    /// <summary>Id nhóm khách hàng đang lọc. 0 = tất cả các nhóm.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerHasFilter))]
    [NotifyPropertyChangedFor(nameof(CustomerFilterButtonText))]
    private NhomKhachHang? selectedCustomerNhom;

    /// <summary>Giá trị bộ lọc công nợ, dùng chung hằng số CongNoTatCa/CongNoConNo/CongNoKhongConNo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomerHasFilter))]
    [NotifyPropertyChangedFor(nameof(CustomerFilterButtonText))]
    private string? customerCongNoFilter = CongNoTatCa;

    [ObservableProperty] private bool isCustomerFilterPanelOpen;

    /// <summary>Bộ lọc đang có điều kiện khác mặc định (để hiện nút "Xóa").</summary>
    public bool CustomerHasFilter =>
        SelectedCustomerNhom is { Id: > 0 }
        || (!string.IsNullOrWhiteSpace(CustomerCongNoFilter) && CustomerCongNoFilter != CongNoTatCa);

    /// <summary>
    /// Nhãn nút bộ lọc, ghi rõ điều kiện đang áp dụng:
    /// "Bộ lọc", "Bộ lọc (VIP)", "Bộ lọc (Còn nợ)", "Bộ lọc (VIP, Còn nợ)", ...
    /// </summary>
    public string CustomerFilterButtonText
    {
        get
        {
            var dieuKien = new List<string>();

            if (SelectedCustomerNhom is { Id: > 0 })
                dieuKien.Add(SelectedCustomerNhom.TenNhom);

            switch (CustomerCongNoFilter)
            {
                case CongNoConNo:
                    dieuKien.Add("Còn nợ");
                    break;
                case CongNoKhongConNo:
                    dieuKien.Add("Không còn nợ");
                    break;
            }

            return dieuKien.Count == 0
                ? "Bộ lọc"
                : $"Bộ lọc ({string.Join(", ", dieuKien)})";
        }
    }

    /// <summary>Mũi tên trên nút mở/đóng menu bộ lọc.</summary>
    public string CustomerFilterArrowText => IsCustomerFilterPanelOpen ? "▲" : "▼";

    partial void OnIsCustomerFilterPanelOpenChanged(bool value)
        => OnPropertyChanged(nameof(CustomerFilterArrowText));

    /// <summary>Nạp nhóm khách hàng cho ô lọc (gọi một lần khi mở màn khách hàng).</summary>
    public async Task LoadCustomerNhomOptionsAsync()
    {
        var nhoms = await LoadNhomKhachHangOptionsAsync();

        // Nhóm đang lọc có thể bị vô hiệu: đưa về "tất cả nhóm" thay vì giữ giá trị mù.
        if (SelectedCustomerNhom is { Id: > 0 } && nhoms.All(n => n.Id != SelectedCustomerNhom.Id))
            SelectedCustomerNhom = null;

        CustomerNhomOptions.Clear();
        CustomerNhomOptions.Add(new NhomKhachHang { Id = 0, TenNhom = "Tất cả nhóm khách hàng" });

        foreach (var nhom in nhoms)
            CustomerNhomOptions.Add(nhom);
    }

    /// <summary>Bấm "Áp dụng" trong menu lọc: quay về trang 1 rồi tải lại danh sách.</summary>
    [RelayCommand]
    private async Task ApplyCustomerFilterAsync()
    {
        CustomerPage = 1;

        await LoadCustomersAsync();
    }

    /// <summary>Bấm "Xóa" trong menu lọc: reset về mặc định rồi tải lại danh sách.</summary>
    [RelayCommand]
    private async Task ClearCustomerFilterAsync()
    {
        CustomerCongNoFilter = CongNoTatCa;
        SelectedCustomerNhom = null;
        CustomerPage = 1;

        await LoadCustomersAsync();
    }

    /// <summary>Checkbox "chọn tất cả" của bảng danh sách.</summary>
    [ObservableProperty] private bool isAllCustomerRowsSelected;

    /// <summary>Đang áp trạng thái "chọn tất cả" xuống các dòng, tránh cập nhật ngược.</summary>
    private bool _dangApDungChonTatCa;

    [ObservableProperty] private bool isLoadingCustomers;

    /// <summary>Id của khách hàng đang mở vùng chi tiết. null = không có khách hàng nào đang mở.</summary>
    [ObservableProperty] private uint? selectedCustomerId;

    /// <summary>Dòng khách hàng đang mở chi tiết.</summary>
    [ObservableProperty] private KhachHangRowViewModel? selectedCustomer;

    public int CustomerTotalPages =>
        CustomerPageSize <= 0
            ? 0
            : (int)Math.Ceiling(CustomerTotalCount / (double)CustomerPageSize);

    /// <summary>
    /// Ví dụ không tìm kiếm: "1 - 20 trong 850 khách hàng".
    /// Khi đang tìm kiếm: "1 - 20 trong 29 khách hàng tìm theo \"Thủy\" (tổng 850)".
    /// </summary>
    public string CustomerRangeText
    {
        get
        {
            var dangTimKiem = !string.IsNullOrWhiteSpace(CustomerSearch);

            if (CustomerTotalCount == 0)
                return dangTimKiem
                    ? $"0 khách hàng tìm theo \"{CustomerSearch.Trim()}\" (tổng {CustomerAllCount:N0})"
                    : "0 khách hàng";

            var from = (CustomerPage - 1) * CustomerPageSize + 1;
            var to = Math.Min(from + CustomerPageSize - 1, CustomerTotalCount);

            if (dangTimKiem)
                return $"{from} - {to} trong {CustomerTotalCount:N0} khách hàng tìm theo \"{CustomerSearch.Trim()}\" (tổng {CustomerAllCount:N0})";

            return $"{from} - {to} trong {CustomerTotalCount:N0} khách hàng";
        }
    }

    public bool HasCustomerRows => CustomerRows.Count > 0;

    public bool IsCustomerListEmpty => CustomerRows.Count == 0;

    /// <summary>Thông báo khi danh sách rỗng, thay đổi theo từ khóa đang tìm.</summary>
    public string CustomerEmptyText =>
        string.IsNullOrWhiteSpace(CustomerSearch)
            ? "Chưa có khách hàng nào trong hệ thống"
            : $"Không tìm thấy khách hàng với từ khóa \"{CustomerSearch.Trim()}\"";

    public string CustomerEmptyHint =>
        string.IsNullOrWhiteSpace(CustomerSearch)
            ? "Bấm “＋ Thêm mới” để tạo khách hàng đầu tiên."
            : "Thử từ khóa khác hoặc xóa nội dung ô tìm kiếm.";

    partial void OnCustomerSearchChanged(string value)
    {
        OnPropertyChanged(nameof(CustomerEmptyText));
        OnPropertyChanged(nameof(CustomerEmptyHint));
    }

    public bool CanCustomerTrangTruoc => CustomerPage > 1;

    public bool CanCustomerTrangSau => CustomerPage < CustomerTotalPages;

    public string CustomerTotalDebtText => MoneyInput.Format(CustomerTotalDebt) + " đ";

    public string CustomerTotalSalesText => MoneyInput.Format(CustomerTotalSales) + " đ";

    /// <summary>Số khách hàng đang được tick trên trang hiện tại.</summary>
    public int CustomerSelectedCount => CustomerRows.Count(row => row.IsSelected);

    public string CustomerSelectedCountText =>
        CustomerSelectedCount == 0
            ? "Chưa chọn khách hàng nào"
            : $"Đã chọn {CustomerSelectedCount:N0} khách hàng";

    public bool HasSelectedCustomerRows => CustomerSelectedCount > 0;

    /// <summary>
    /// Khi người dùng tick ô "chọn tất cả" ở tiêu đề bảng thì áp dụng
    /// trạng thái đó cho tất cả dòng của trang đang xem.
    /// </summary>
    partial void OnIsAllCustomerRowsSelectedChanged(bool value)
    {
        if (_dangApDungChonTatCa || CustomerRows.Count == 0)
            return;

        // Cờ này tránh việc từng dòng báo lại ngược lại làm đảo trạng thái
        // của ô "chọn tất cả" giữa chừng lúc đang áp dụng.
        _dangApDungChonTatCa = true;

        try
        {
            foreach (var row in CustomerRows)
                row.IsSelected = value;
        }
        finally
        {
            _dangApDungChonTatCa = false;
        }

        CapNhatTrangThaiChonCustomer();
    }

    /// <summary>Tick/bỏ tick toàn bộ khách hàng của trang đang xem.</summary>
    [RelayCommand]
    private void ChonTatCaCustomerRows()
        => IsAllCustomerRowsSelected = !IsAllCustomerRowsSelected;

    /// <summary>Bỏ tick toàn bộ khách hàng đang chọn.</summary>
    [RelayCommand]
    private void BoChonTatCaCustomerRows()
    {
        foreach (var row in CustomerRows)
            row.IsSelected = false;

        IsAllCustomerRowsSelected = false;
        CapNhatTrangThaiChonCustomer();
    }

    private void CapNhatTrangThaiChonCustomer()
    {
        OnPropertyChanged(nameof(CustomerSelectedCount));
        OnPropertyChanged(nameof(CustomerSelectedCountText));
        OnPropertyChanged(nameof(HasSelectedCustomerRows));
    }

    /// <summary>Khách hàng đang mở chi tiết (dạng model, phục vụ nghiệp vụ).</summary>
    public KhachHang? ExpandedCustomer => SelectedCustomer?.Customer;

    partial void OnCustomerPageChanged(int value)
    {
        OnPropertyChanged(nameof(CustomerTotalPages));
        OnPropertyChanged(nameof(CustomerRangeText));
        OnPropertyChanged(nameof(CanCustomerTrangTruoc));
        OnPropertyChanged(nameof(CanCustomerTrangSau));
    }

    partial void OnCustomerPageSizeChanged(int value)
    {
        if (CustomerPage != 1)
            CustomerPage = 1;

        OnPropertyChanged(nameof(CustomerTotalPages));
        OnPropertyChanged(nameof(CustomerRangeText));
        OnPropertyChanged(nameof(CanCustomerTrangTruoc));
        OnPropertyChanged(nameof(CanCustomerTrangSau));
    }

    partial void OnSelectedCustomerChanged(KhachHangRowViewModel? value)
        => OnPropertyChanged(nameof(ExpandedCustomer));

    [RelayCommand]
    private void ChonKhachHangBan(KhachHang? customer)
    {
        if (customer is null)
            return;

        SelectedSaleCustomer = customer;
        SaleCustomerName = customer.TenKhachHang;
        SaleCustomerPhone = customer.SoDienThoai ?? "";
        SaleCustomerAddress = customer.DiaChi ?? "";
        SaleCustomerSuggestions.Clear();
    }

    [RelayCommand]
    private void ChonKhachLe()
    {
        SelectedSaleCustomer = null;
        SaleCustomerName = "Khách lẻ";
        SaleCustomerPhone = "";
        SaleCustomerAddress = "";
        SaleCustomerSuggestions.Clear();
    }

    [RelayCommand]
    private async Task MoDanhSachKhachBanAsync()
    {
        try
        {
            SaleCustomerSuggestions.Clear();
            foreach (var customer in await _db.GetKhachHangAsync())
                SaleCustomerSuggestions.Add(customer);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải danh sách khách hàng: " + ex.Message;
        }
    }

    /// <summary>
    /// Thêm khách hàng ngay trong màn Bán hàng: mở form khách hàng đang có,
    /// lưu bằng DatabaseService hiện tại rồi tự chọn khách vừa tạo cho hóa đơn đang bán.
    /// </summary>
    [RelayCommand]
    private async Task ThemKhachHangBanAsync()
    {
        if (OwnerWindow is null)
            return;

        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersCreate, "Bạn không có quyền thêm khách hàng."))
            return;

        try
        {
            var maKhachHangMoi = await _db.TaoMaKhachHangMoiAsync();

            var editor = new KhachHangEditorWindow(
                new KhachHang
                {
                    Id = 0,
                    MaKhachHang = maKhachHangMoi,
                    TenKhachHang = "",
                    LoaiKhachHang = "CA_NHAN",
                    TrangThai = true
                },
                await LoadNhomKhachHangOptionsAsync(),
                isEditMode: false,
                trungSoDienThoaiAsync: KiemTraTrungSoDienThoaiAsync);

            // Người dùng hủy: giữ nguyên khách hàng và hóa đơn hiện tại.
            if (!await editor.ShowDialog<bool>(OwnerWindow))
                return;

            var newId = await _db.ThemKhachHangAsync(editor.Customer);

            // Đọc lại bản ghi từ database để có Id và các giá trị do database sinh.
            var khachHangMoi = await _db.GetKhachHangByIdAsync(newId) ?? editor.Customer;
            khachHangMoi.Id = newId;

            // Tự chọn khách vừa tạo: phải gán khách trước rồi mới đổi tên
            // để ô "Khách hàng" không làm mất lựa chọn.
            SelectedSaleCustomer = khachHangMoi;
            SaleCustomerName = khachHangMoi.TenKhachHang;
            SaleCustomerPhone = khachHangMoi.SoDienThoai ?? "";
            SaleCustomerAddress = khachHangMoi.DiaChi ?? "";
            SaleCustomerSuggestions.Clear();

            StatusMessage = $"Đã thêm khách hàng: {khachHangMoi.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm khách hàng: " + ex.Message;
        }
    }

    /// <summary>
    /// Kiểm tra số điện thoại đã tồn tại chưa (dùng cho form thêm khách trong Bán hàng).
    /// </summary>
    private Task<bool> KiemTraTrungSoDienThoaiAsync(string? soDienThoai, uint khachHangId)
        => _db.KiemTraTrungSoDienThoaiAsync(soDienThoai, khachHangId);

    /// <summary>
    /// Tìm kiếm khách hàng (nút "Tìm kiếm"): luôn quay về trang đầu.
    /// </summary>
    [RelayCommand]
    private async Task TimKiemKhachHangAsync()
    {
        CustomerPage = 1;

        await LoadCustomersAsync();
    }

    /// <summary>
    /// Tải lại trang đầu sau khi người dùng đổi số bản ghi mỗi trang.
    /// </summary>
    public Task TaiLaiDanhSachKhachHangAsync() => TimKiemKhachHangAsync();

    /// <summary>
    /// Tìm kiếm realtime khi người dùng gõ: chống debounce 350ms
    /// để không gọi database liên tục.
    /// </summary>
    public void TimKiemKhachHangTuDong()
    {
        _customerSearchCts?.Cancel();
        _customerSearchCts?.Dispose();
        _customerSearchCts = new CancellationTokenSource();
        var token = _customerSearchCts.Token;

        _ = DebounceCustomerSearchAsync(token);
    }

    private async Task DebounceCustomerSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);

            if (token.IsCancellationRequested)
                return;

            await TimKiemKhachHangAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp -> huỷ lần tìm kiếm cũ
        }
    }

    // =========================================================
    // MỞ / ĐÓNG VÙNG CHI TIẾT NGAY DƯỚI DÒNG
    // =========================================================

    /// <summary>
    /// Người dùng click vào một dòng khách hàng:
    /// click khác dòng đang mở -> chuyển sang dòng mới (đóng dòng cũ, mở dòng mới);
    /// click đúng dòng đang mở -> đóng lại. Chỉ một khách hàng mở tại một thời điểm.
    /// </summary>
    [RelayCommand]
    private async Task MoRongDongKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (row is null)
            return;

        if (SelectedCustomerId == row.CustomerId)
        {
            DongChiTietKhachHang();

            StatusMessage = "Đã đóng chi tiết khách hàng";

            return;
        }

        await MoChiTietKhachHangAsync(row);
    }

    private async Task MoChiTietKhachHangAsync(KhachHangRowViewModel row)
    {
        // Đóng vùng chi tiết của khách hàng đang mở trước (nếu có)
        if (SelectedCustomer is not null && SelectedCustomer.CustomerId != row.CustomerId)
        {
            SelectedCustomer.IsExpanded = false;
            SelectedCustomer.Detail = null;
        }

        SelectedCustomer = row;
        SelectedCustomerId = row.CustomerId;
        row.IsExpanded = true;

        var detail = row.Detail ?? new KhachHangDetailViewModel();
        row.Detail = detail;
        detail.SelectedTab = "thongtin";
        detail.IsLoading = true;

        StatusMessage = $"Đang tải chi tiết {row.TenKhachHang}...";

        try
        {
            // Chỉ đọc đúng dữ liệu của khách hàng đang xem
            var full = await _db.GetKhachHangByIdAsync(row.CustomerId);

            if (full is null)
            {
                detail.IsLoading = false;
                StatusMessage = "Không tìm thấy khách hàng trong database";

                return;
            }

            if (SelectedCustomerId != row.CustomerId)
                return;

            row.NapLai(full);
            detail.Customer = full;

            var historyTask = _db.GetLichSuMuaHangCuaKhachHangAsync(row.CustomerId);
            var statsTask = _db.GetThongKeMuaHangCuaKhachHangAsync(row.CustomerId);

            await Task.WhenAll(historyTask, statsTask);

            if (SelectedCustomerId != row.CustomerId)
                return;

            detail.NapLichSuMuaHang(await historyTask);
            detail.ThongKe = await statsTask;
            detail.IsLoading = false;

            StatusMessage = full.TenKhachHang;
        }
        catch (Exception ex)
        {
            detail.IsLoading = false;
            StatusMessage = "Không thể tải chi tiết khách hàng: " + ex.Message;
        }
    }

    private void DongChiTietKhachHang()
    {
        if (SelectedCustomer is not null)
        {
            SelectedCustomer.IsExpanded = false;
            SelectedCustomer.Detail = null;
        }

        SelectedCustomer = null;
        SelectedCustomerId = null;
    }

    // =========================================================
    // PHÂN TRANG DANH SÁCH KHÁCH HÀNG
    // =========================================================

    [RelayCommand]
    private async Task CustomerTrangDauAsync()
    {
        CustomerPage = 1;

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task CustomerTrangCuoiAsync()
    {
        CustomerPage = Math.Max(1, CustomerTotalPages);

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task CustomerTrangTruocAsync()
    {
        if (CustomerPage > 1)
            CustomerPage--;

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task CustomerTrangSauAsync()
    {
        if (CustomerPage < CustomerTotalPages)
            CustomerPage++;

        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task ThemMoiKhachHangAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersCreate, "Bạn không có quyền thêm khách hàng."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        try
        {
            var maKhachHangMoi = await _db.TaoMaKhachHangMoiAsync();

            var editor = new KhachHangEditorWindow(
                new KhachHang
                {
                    Id = 0,
                    MaKhachHang = maKhachHangMoi,
                    TenKhachHang = "",
                    LoaiKhachHang = "CA_NHAN",
                    TrangThai = true
                },
                await LoadNhomKhachHangOptionsAsync(),
                isEditMode: false);

            if (!await editor.ShowDialog<bool>(OwnerWindow))
                return;

            var newId = await _db.ThemKhachHangAsync(editor.Customer);

            editor.Customer.Id = newId;

            // Khách mới tên không rỗng, có thể nằm ở trang khác -> tải lại trang đầu
            CustomerPage = 1;
            await LoadCustomersAsync();

            StatusMessage = $"Đã thêm khách hàng: {editor.Customer.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm khách hàng: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemKhachHangTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersImport, "Bạn không có quyền import khách hàng."))
        {
            return;
        }
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file danh sách khách hàng",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                    {
                        Patterns = ["*.xlsx", "*.xls", "*.csv"]
                    }
                ]
            });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file để nhập khách hàng hàng loạt.";
            return;
        }

        try
        {
            IsImporting = true;
            StatusMessage = $"⏳ Đã chọn file \"{file.Name}\". Đang đọc dữ liệu...";
            await Task.Yield();

            var doc = await Task.Run(() => _db.DocKhachHangTuFile(file.Path.LocalPath));

            if (doc.KhachHangs.Count == 0)
            {
                StatusMessage = $"❌ File \"{file.Name}\" không có dữ liệu khách hàng hợp lệ.";
                return;
            }

            StatusMessage =
                $"⏳ Đã đọc {doc.SoDongExcel:N0} dòng, hợp lệ {doc.SoKhachHangHopLe:N0} khách hàng. Đang kiểm tra dữ liệu trùng...";
            await Task.Yield();

            // Chỉ nạp mã và số điện thoại đang có để đếm trùng, không tải cả danh sách lên giao diện.
            var existingKeys = await _db.LayKhoaTrungKhopKhachHangAsync();
            var duplicateCustomers = doc.KhachHangs
                .Where(customer => IsDuplicateCustomer(customer, existingKeys))
                .ToList();

            bool replaceExisting = false;

            if (duplicateCustomers.Count > 0)
            {
                var replace = await ShowConfirmationAsync(
                    "Khách hàng bị trùng",
                    $"Phát hiện {duplicateCustomers.Count:N0} khách hàng trong file đã tồn tại.\n\nBạn muốn giữ dữ liệu khách hàng cũ hay thay thế bằng dữ liệu trong file?",
                    "Thay thế",
                    "Giữ khách hàng cũ");

                replaceExisting = replace;

                StatusMessage = replaceExisting
                    ? "⏳ Đã chọn thay thế. Đang nhập và cập nhật dữ liệu..."
                    : "⏳ Đã chọn giữ dữ liệu cũ. Đang bỏ qua khách hàng trùng và nhập khách hàng mới...";
            }
            else
            {
                StatusMessage = $"⏳ Không có khách hàng trùng. Đang nhập {doc.SoKhachHangHopLe:N0} khách hàng...";
            }

            await Task.Yield();

            var result = await Task.Run(() =>
                _db.ImportKhachHangTuFileAsync(file.Path.LocalPath, doc, replaceExisting));

            StatusMessage = "⏳ Đang làm mới danh sách khách hàng...";

            // Xóa từ khóa tìm kiếm còn sót lại để thẻ "Tổng khách hàng" và bảng
            // hiển thị đúng toàn bộ khách hàng sau khi nhập.
            CustomerSearch = string.Empty;
            CustomerPage = 1;
            await LoadCustomersAsync();

            var thongBao = new System.Text.StringBuilder();
            thongBao.Append($"✅ {file.Name}: đọc {result.SoDongExcel:N0} dòng, "
                            + $"thêm mới {result.SoThemMoi:N0}, cập nhật {result.SoCapNhat:N0}");

            if (result.SoBoQua > 0)
                thongBao.Append($", giữ nguyên {result.SoBoQua:N0}");

            thongBao.Append($", lỗi {result.SoLoi:N0}");

            if (result.SoNhomKhachHangKhongKhop > 0)
                thongBao.Append($" | {result.SoNhomKhachHangKhongKhop:N0} nhóm khách hàng chưa có trong phần mềm");

            if (result.SoNguoiTaoKhongKhop > 0)
                thongBao.Append($" | {result.SoNguoiTaoKhongKhop:N0} người tạo chưa có tài khoản");

            thongBao.Append($" ({result.ThoiGian.TotalSeconds:F1}s)");

            StatusMessage = thongBao.ToString();
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ Không thể nhập khách hàng từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    private static bool IsDuplicateCustomer(KhachHang customer, IReadOnlySet<string> existingKeys)
    {
        var code = customer.MaKhachHang?.Trim();
        var phone = customer.SoDienThoai?.Trim();

        if (!string.IsNullOrWhiteSpace(code) && existingKeys.Contains("ma:" + code))
            return true;

        return !string.IsNullOrWhiteSpace(phone) && existingKeys.Contains("dt:" + phone);
    }

    /// <summary>
    /// Nút "Chỉnh sửa" ở cuối vùng chi tiết: mở form sửa khách hàng đang chọn.
    /// Sau khi lưu chỉ refresh lại đúng dòng đó, không tải lại cả ứng dụng.
    /// </summary>
    [RelayCommand]
    private async Task SuaKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersEdit, "Bạn không có quyền sửa khách hàng."))
        {
            return;
        }
        if (row is null || OwnerWindow is null)
            return;

        var customer = await _db.GetKhachHangByIdAsync(row.CustomerId);

        if (customer is null)
        {
            StatusMessage = "Không tìm thấy khách hàng để sửa";

            return;
        }

        var editor = new KhachHangEditorWindow(
            customer,
            await LoadNhomKhachHangOptionsAsync(),
            isEditMode: true);

        if (!await editor.ShowDialog<bool>(OwnerWindow))
            return;

        try
        {
            await _db.CapNhatKhachHangAsync(editor.Customer);

            // Refresh đúng dòng vừa sửa, vùng chi tiết vẫn mở
            var updated = await _db.GetKhachHangByIdAsync(row.CustomerId);

            if (updated is null)
            {
                await LoadCustomersAsync();

                return;
            }

            row.NapLai(updated);

            if (row.Detail is not null)
                row.Detail.Customer = updated;

            StatusMessage = $"Đã cập nhật khách hàng: {updated.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể cập nhật khách hàng: " + ex.Message;
        }
    }

    private async Task<IReadOnlyList<NhomKhachHang>> LoadNhomKhachHangOptionsAsync()
    {
        try
        {
            return await _db.GetNhomKhachHangAsync();
        }
        catch
        {
            return Array.Empty<NhomKhachHang>();
        }
    }

    /// <summary>
    /// Nút "Xóa" ở cuối vùng chi tiết: xác nhận trước, xóa xong cập nhật
    /// danh sách ngay mà không tải lại toàn bộ ứng dụng.
    /// </summary>
    [RelayCommand]
    private async Task XoaKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CustomersDelete, "Bạn không có quyền xóa khách hàng."))
        {
            return;
        }
        if (row is null)
            return;

        var customer = row.Customer;

        var confirmed = await ShowConfirmationAsync(
            "Xóa khách hàng",
            $"Bạn có chắc muốn xóa khách hàng \"{row.TenKhachHang}\" không? Khách hàng sẽ không còn hiển thị trong danh sách.");

        if (!confirmed)
            return;

        var customerId = row.CustomerId;

        try
        {
            await _db.XoaKhachHangAsync(customerId);

            var suggestion = SaleCustomerSuggestions
                .FirstOrDefault(item => item.Id == customerId);

            if (suggestion is not null)
                SaleCustomerSuggestions.Remove(suggestion);

            if (SelectedSaleCustomer?.Id == customerId)
                ChonKhachLe();

            if (SelectedCustomerId == customerId)
                DongChiTietKhachHang();

            CustomerRows.Remove(row);

            OnPropertyChanged(nameof(HasCustomerRows));
            OnPropertyChanged(nameof(IsCustomerListEmpty));
            OnPropertyChanged(nameof(CustomerRangeText));
            OnPropertyChanged(nameof(CanCustomerTrangSau));

            // Trang cuối vừa bị rỗng -> lùi về trang trước để không bị trang trắng
            if (CustomerRows.Count == 0 && CustomerPage > 1)
                CustomerPage--;

            // Bốn thẻ thống kê phải đọc lại từ database thay vì tự trừ tay:
            // "Khách hàng mới" chỉ giảm khi khách vừa xóa nằm trong khoảng ngày
            // đang đếm, còn "Nợ hiện tại"/"Tổng bán" còn phụ thuộc hóa đơn, sổ cái
            // công nợ và phiếu trả hàng nên không thể suy ra từ thao tác xóa.
            await LoadCustomersAsync();

            StatusMessage = $"Đã xóa khách hàng {customer.TenKhachHang}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể xóa khách hàng: " + ex.Message;
        }
    }

    private bool _isInitialized;

    // =========================================================
    // THÔNG TIN NGƯỜI DÙNG ĐANG ĐĂNG NHẬP
    // Lấy từ Session, không gắn cứng tài khoản nào trong giao diện.
    // =========================================================

    public string TenDangNhapHienThi =>
        string.IsNullOrWhiteSpace(Session.CurrentUser?.TenDangNhap)
            ? "Chưa đăng nhập"
            : Session.CurrentUser!.TenDangNhap;

    public string HoTenHienThi =>
        string.IsNullOrWhiteSpace(Session.CurrentUser?.HoTen)
            ? TenDangNhapHienThi
            : Session.CurrentUser!.HoTen;

    public string VaiTroHienThi =>
        string.IsNullOrWhiteSpace(Session.CurrentUser?.VaiTro)
            ? "Nhân viên"
            : Session.CurrentUser!.VaiTro;

    public string ChuiChuCaiHienThi
    {
        get
        {
            var name = Session.CurrentUser is null
                ? ""
                : (Session.CurrentUser.HoTen ?? "").Trim();

            if (string.IsNullOrEmpty(name))
            {
                name = Session.CurrentUser?.TenDangNhap?.Trim() ?? "";
            }

            if (string.IsNullOrEmpty(name))
                return "?";

            return name[..1].ToUpperInvariant();
        }
    }

    public string ChaoMungNguoiDung =>
        $"Chào mừng trở lại, {HoTenHienThi}";

    public bool DaDangNhap => Session.IsLoggedIn;

    // =========================================================
    // ĐĂNG XUẤT
    // =========================================================

    /// <summary>
    /// Kết thúc phiên hiện tại rồi mở lại cửa sổ đăng nhập.
    ///
    /// - Không đụng tới database nên tài khoản vẫn còn nguyên.
    /// - Xoá thông tin đăng nhập đã ghi nhớ để lần sau mở app
    ///   KHÔNG tự động đăng nhập lại (đúng như Chrome/Firefox).
    ///   Checkbox "Ghi nhớ đăng nhập" vẫn được giữ, chỉ mất credential.
    /// </summary>
    [RelayCommand]
    private async Task DangXuatAsync()
    {
        if (Session.IsLoggedIn &&
            OwnerWindow is not null)
        {
            var confirm = new ConfirmationWindow(
                "Đăng xuất",
                "Bạn có chắc chắn muốn đăng xuất?",
                "Đăng xuất",
                "Hủy");

            var confirmed = await confirm.ShowDialog<bool>(OwnerWindow);

            if (!confirmed)
                return;
        }

        Session.Logout();

        new LoginCredentialStore().Clear();

        if (OwnerWindow is not null)
        {
            var loginWindow = new LoginWindow();

            loginWindow.Show();

            OwnerWindow.Close();
        }
    }

    public async Task InitializeAsync()
    {
        RaiseAllCanProperties();

        OnPropertyChanged(nameof(TenDangNhapHienThi));
        OnPropertyChanged(nameof(HoTenHienThi));
        OnPropertyChanged(nameof(VaiTroHienThi));
        OnPropertyChanged(nameof(ChuiChuCaiHienThi));
        OnPropertyChanged(nameof(ChaoMungNguoiDung));
        OnPropertyChanged(nameof(DaDangNhap));

        if (_isInitialized)
            return;

        _isInitialized = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = "Đang tải dữ liệu...";

        // Nạp dữ liệu màn hình đang chọn mặc định (Tổng quan hoặc màn hình theo phân quyền)
        var error = await LoadSafelyAsync(SelectedMenu, () => LoadSelectedSectionAsync(SelectedMenu));

        // Nạp trước danh mục sản phẩm nếu màn hình hiện tại chưa phải Hàng hóa
        if (SelectedMenu != "Hàng hóa")
        {
            _ = LoadSafelyAsync("sản phẩm", LoadProductsAsync);
        }

        StatusMessage = error is null
            ? "Sẵn sàng"
            : $"Đã mở {SelectedMenu}. Lỗi: {error}";
    }

    private static async Task<string?> LoadSafelyAsync(
        string dataName,
        Func<Task> load)
    {
        try
        {
            await load();
            return null;
        }
        catch (Exception ex)
        {
            return dataName + ": " + ex.Message;
        }
    }
    private async Task LoadSelectedSectionAsync(string menu)
    {
        try
        {
            switch (menu)
            {
                case "Tổng quan":
                    await LoadOverviewAsync();
                    _overviewLoaded = true;
                    break;

                case "Khách hàng":
                    await LoadCustomersAsync();
                    _customersLoaded = true;
                    break;

                case "Hóa đơn":
                    // Mỗi lần vào Hóa đơn đều đọc lại database
                    await LoadInvoicesAsync();
                    _invoicesLoaded = true;
                    break;

                case "Nhà cung cấp":
                    await LoadSuppliersAsync();
                    break;

                case "Nhập hàng":
                    // Mỗi lần bấm menu "Nhập hàng" phải khởi tạo phiếu mới:
                    // sinh mã phiếu, gán nhân viên/ngày/trạng thái và nạp danh sách hàng hóa.
                    // Thiếu bước này thì PhieuNhapMa rỗng => lưu phiếu sẽ lỗi
                    // "Duplicate entry '' for key 'uk_pnh_ma'".
                    await MoPhieuNhapHangAsync();
                    break;

                case "Sổ quỹ":
                    await TaiSoQuyBoLocOptionsAsync();
                    await TaiLaiSoQuyAsync();
                    break;

                case "Thông tin tiệm":
                    // Luôn đọc lại database mỗi lần mở màn để thấy đúng thông
                    // tin đã lưu, kể cả khi vừa sửa xong ở phiên trước.
                    await ThongTinTiem.TaiLaiCommand.ExecuteAsync(null);
                    break;

                case "Báo cáo":
                    // Báo cáo chưa chọn mục nào (hoặc mục đang chọn đã bị ẩn do thiếu
                    // quyền) thì mở mặc định báo cáo đầu tiên mà người dùng được xem.
                    if (string.IsNullOrEmpty(SelectedReportKey)
                        || BaoCaoMenuItems.All(i => !i.IsVisible || i.Key != SelectedReportKey))
                    {
                        SelectedReportKey = BaoCaoMenuItems.FirstOrDefault(i => i.IsVisible)?.Key ?? "";
                    }

                    // Chỉ tải lần đầu khi vào màn, các lần sau giữ nguyên trang/tìm kiếm đang xem
                    if (SelectedReportKey == "phieu-nhap-hang" && !_baoCaoPhieuNhapLoaded)
                    {
                        _baoCaoPhieuNhapLoaded = true;
                        await LoadBaoCaoPhieuNhapAsync();
                    }

                    // Báo cáo bán hàng cũng chỉ tải lần đầu; các lần sau giữ nguyên
                    // khoảng thời gian người dùng đang xem.
                    if (SelectedReportKey == "ban-hang" && !_baoCaoBanHangLoaded)
                    {
                        _baoCaoBanHangLoaded = true;
                        await BaoCaoBanHang.TaiLaiAsync();
                    }

                    // Báo cáo hàng hóa cũng chỉ tải lần đầu; các lần sau giữ nguyên
                    // bộ lọc người dùng đang xem.
                    if (SelectedReportKey == "hang-hoa" && !_baoCaoHangHoaLoaded)
                    {
                        _baoCaoHangHoaLoaded = true;
                        await BaoCaoHangHoa.TaiLaiAsync();
                    }

                    // Báo cáo khách hàng cũng chỉ tải lần đầu; các lần sau giữ
                    // nguyên bộ lọc người dùng đang xem.
                    if (SelectedReportKey == "khach-hang" && !_baoCaoKhachHangLoaded)
                    {
                        _baoCaoKhachHangLoaded = true;
                        await BaoCaoKhachHang.TaiLaiAsync();
                    }

                    // Báo cáo nhà cung cấp cũng chỉ tải lần đầu; các lần sau giữ
                    // nguyên bộ lọc người dùng đang xem.
                    if (SelectedReportKey == "nha-cung-cap" && !_baoCaoNhaCungCapLoaded)
                    {
                        _baoCaoNhaCungCapLoaded = true;
                        await BaoCaoNhaCungCap.TaiLaiAsync();
                    }

                    break;
            }

            StatusMessage = $"Đã tải {menu.ToLowerInvariant()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể tải {menu}: {ex.Message}";
        }
    }
    private async Task LoadProductsAsync()
    {
        // Dùng chung version guard với tìm kiếm: nếu người dùng gõ từ khóa ngay
        // lúc vừa vào màn hình thì kết quả tải đầu tiên bị bỏ, không ghi đè
        // danh sách đang lọc theo từ khóa.
        var version = ++_productLoadVersion;

        var (products, tongSoKhop) = await _db.GetSanPhamPagedAsync(
            ProductSearch,
            1,
            DatabaseService.SoDongSanPhamToiDa);

        if (version != _productLoadVersion)
            return;

        SanPhams.Clear();
        foreach (var item in products)
        {
            if (!string.IsNullOrWhiteSpace(item.HinhAnh) && File.Exists(item.HinhAnh))
                item.HinhAnhBitmap = new Bitmap(item.HinhAnh);

            SanPhams.Add(item);
        }

        ProductTotalCount = tongSoKhop;

        OnPropertyChanged(nameof(IsSanPhamListEmpty));
    }

    private async Task LoadOverviewAsync()
    {
        // Dashboard tự tải số liệu của nó (COUNT/SUM/GROUP BY ở tầng SQL).
        // Các danh sách dùng chung với các tab khác không còn cần nạp ở đây
        // vì màn Tổng quan không hiển thị chúng nữa.
        await Dashboard.TaiDuLieuAsync();
    }

    /// <summary>
    /// Làm mới số liệu Tổng quan sau khi ghi xong một nghiệp vụ (bán hàng, nhập
    /// hàng, ghi sổ quỹ). Chỉ nạp lại khi màn đang mở để không tốn truy vấn
    /// nền, nhưng dữ liệu vẫn luôn sẵn sàng khi người dùng quay lại màn này.
    /// </summary>
    public async Task LamMoiTongQuanNeuDangMoAsync()
    {
        if (IsTongQuanSelected)
            await Dashboard.TaiDuLieuAsync();
    }

    /// <summary>
    /// Tải trang khách hàng hiện tại.
    /// Chỉ đọc đúng dữ liệu của trang đang xem (LIMIT/OFFSET) nên không bị nghẽn
    /// khi database có hàng nghìn khách hàng.
    /// </summary>
    private async Task LoadCustomersAsync()
    {
        // Chờ lần tải đang chạy xong để không bỏ sót từ khóa tìm kiếm mới nhất
        if (_customerLoadTask is { IsCompleted: false })
        {
            try
            {
                await _customerLoadTask;
            }
            catch
            {
                // Lỗi của lần tải trước đã được báo trong hàm bên trong
            }
        }

        _customerLoadTask = LoadCustomersCoreAsync();

        await _customerLoadTask;
    }

    private async Task LoadCustomersCoreAsync()
    {
        IsLoadingCustomers = true;

        // Version guard: bỏ qua kết quả của lần tải cũ nếu người dùng đã tìm/chuyển trang mới
        var version = ++_customerLoadVersion;

        try
        {
            var page = CustomerPage;

            if (!_customerNhomOptionsLoaded)
            {
                await LoadCustomerNhomOptionsAsync();
                _customerNhomOptionsLoaded = true;

                if (version != _customerLoadVersion)
                    return;
            }

            var (items, tongSoBanGhi) = await _db.GetKhachHangPagedAsync(
                CustomerSearch,
                page,
                CustomerPageSize,
                false,
                SelectedCustomerNhom?.Id ?? 0,
                CustomerCongNoFilter);

            if (version != _customerLoadVersion)
                return;

            var totalPages = CustomerPageSize <= 0
                ? 0
                : (int)Math.Ceiling(tongSoBanGhi / (double)CustomerPageSize);

            // Trang vượt quá số trang thực tế (ví dụ sau khi tìm kiếm) -> quay về trang cuối
            if (items.Count == 0 && totalPages > 0 && page > totalPages)
            {
                CustomerPage = totalPages;

                await LoadCustomersCoreAsync();

                return;
            }

            // Danh sách được dựng lại từ đầu nên đóng vùng chi tiết cũ,
            // tránh trạng thái "đang mở" trỏ vào dòng không còn tồn tại.
            if (SelectedCustomer is not null)
                DongChiTietKhachHang();

            CustomerRows.Clear();

            foreach (var item in items)
            {
                var row = new KhachHangRowViewModel(item, this);

                row.PropertyChanged += OnCustomerRowPropertyChanged;

                CustomerRows.Add(row);
            }

            // Sang trang mới thì bỏ các tick cũ để không chọn nhầm sang khách hàng khác.
            IsAllCustomerRowsSelected = false;
            CapNhatTrangThaiChonCustomer();

            CustomerTotalCount = tongSoBanGhi;

            var thongKeTask = _db.GetThongKeDanhSachKhachHangAsync();

            var thongKe = await thongKeTask;

            CustomerAllCount = thongKe.TongSoKhachHang;
            CustomerNewCount = thongKe.SoKhachMoi;
            CustomerTotalDebt = thongKe.TongNoHienTai;
            CustomerTotalSales = thongKe.TongBan;

            OnPropertyChanged(nameof(CustomerTotalDebtText));
            OnPropertyChanged(nameof(CustomerTotalSalesText));
            OnPropertyChanged(nameof(HasCustomerRows));
            OnPropertyChanged(nameof(IsCustomerListEmpty));
            OnPropertyChanged(nameof(CustomerRangeText));
            OnPropertyChanged(nameof(CanCustomerTrangTruoc));
            OnPropertyChanged(nameof(CanCustomerTrangSau));

            _customersLoaded = true;

            StatusMessage = CustomerRows.Count == 0
                ? (string.IsNullOrWhiteSpace(CustomerSearch)
                    ? "Chưa có khách hàng nào trong hệ thống"
                    : "Không tìm thấy khách hàng phù hợp")
                : $"{CustomerRangeText} - trang {CustomerPage}/{Math.Max(1, totalPages)}";
        }
        catch (Exception ex)
        {
            if (version == _customerLoadVersion)
                StatusMessage = "Không thể tải danh sách khách hàng: " + ex.Message;
        }
        finally
        {
            if (version == _customerLoadVersion)
                IsLoadingCustomers = false;
        }
    }

    /// <summary>
    /// Khi người dùng tick một dòng thì checkbox "chọn tất cả" và số khách hàng
    /// đang chọn phải cập nhật theo.
    /// </summary>
    private void OnCustomerRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(KhachHangRowViewModel.IsSelected))
            return;

        if (_dangApDungChonTatCa)
            return;

        var chonTatCa = CustomerRows.Count > 0
                        && CustomerRows.All(row => row.IsSelected);

        if (chonTatCa != IsAllCustomerRowsSelected)
            IsAllCustomerRowsSelected = chonTatCa;

        CapNhatTrangThaiChonCustomer();
    }

    private async Task LoadInvoicesAsync()
    {
        try
        {
            var invoices = await _db.GetHoaDonAsync(InvoiceSearch, LocHoaDonHienTai);

            HoaDons.Clear();

            foreach (var item in invoices)
                HoaDons.Add(item);

            StatusMessage = $"Đã tải {HoaDons.Count} hóa đơn";
        }
        catch (Exception ex)
        {
            HoaDons.Clear();

            StatusMessage = $"Lỗi tải hóa đơn: {ex.Message}";
        }
    }

    private async Task LoadSuppliersAsync()
        => await LoadSupplierPageAsync();

    /// <summary>
    /// Tìm kiếm hàng hóa theo từ khóa (nút "Tìm kiếm").
    /// Ô tìm kiếm đã tự tìm realtime nên lệnh này chỉ phục vụ khi cần tìm lại ngay.
    /// </summary>
    [RelayCommand]
    private async Task SearchProductsAsync(string? searchText)
    {
        if (searchText is not null)
            ProductSearch = searchText;

        // Gán SearchText ở trên đã lên lịch một lần tìm kiếm tự động,
        // huỷ đi ở đây để không chạy truy vấn hai lần.
        HuyDongTimKiemSanPham();

        await RefreshProductsAsync();
    }

    /// <summary>
    /// Tìm kiếm realtime: chờ người dùng ngừng gõ 300ms mới gọi database,
    /// tránh mỗi ký tự lại chạy một câu truy vấn làm giao diện bị giật.
    /// </summary>
    public void TimKiemSanPhamTuDong()
    {
        HuyDongTimKiemSanPham();

        var cts = new CancellationTokenSource();
        _productSearchCts = cts;

        _ = TimKiemSanPhamSauDebounceAsync(cts.Token);
    }

    private async Task TimKiemSanPhamSauDebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);

            if (token.IsCancellationRequested)
                return;

            await RefreshProductsAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp: huỷ lần tìm kiếm trước, không cần báo lỗi.
        }
    }

    private void HuyDongTimKiemSanPham()
    {
        if (_productSearchCts is null)
            return;

        _productSearchCts.Cancel();
        _productSearchCts.Dispose();
        _productSearchCts = null;
    }
// 1. Khai báo các thuộc tính
    [ObservableProperty]
    private long? _selectedNhomId;

    [ObservableProperty]
    private IReadOnlyList<ProductOption>? _nhomSanPhamOptions;

    [ObservableProperty]
    private ProductOption? _selectedNhomSanPham;

    partial void OnSelectedNhomSanPhamChanged(ProductOption? value)
    {
        // Mỗi khi người dùng chọn một mục trong ComboBox, tự động gọi hàm làm mới danh sách
        _ = RefreshProductsAsync();
        _ = TimKiemHoaBanAsync();
    }

    [ObservableProperty]
    private string _currentSortOption = "Tên sản phẩm";

    // 2. Hàm Load nhóm (dùng uint cho Id để khớp với ProductOption của bạn)
    [RelayCommand]
    private async Task LoadNhomSanPhamAsync()
    {
        try
        {
            var list = await _db.GetNhomSanPhamAsync();

            // Vì Id là uint nên dùng 0 hoặc các giá trị dương an toàn
            var fullList = new List<ProductOption>
            {
                new ProductOption { Id = 0, Name = "Tất cả sản phẩm" },
                new ProductOption { Id = 999999, Name = "-- Chưa phân nhóm --" }
            };

            if (list != null)
            {
                fullList.AddRange(list);
            }

            NhomSanPhamOptions = fullList;

            // Dùng tên thuộc tính viết hoa (SelectedNhomSanPham) thay vì _selectedNhomSanPham
            SelectedNhomSanPham = fullList.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải danh sách nhóm sản phẩm: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemNhomHangNhanhAsync()
    {
        if (OwnerWindow is null)
            return;

        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsCreate, "Bạn không có quyền thêm nhóm hàng."))
            return;

        try
        {
            var tenNhom = await QuickOptionEditorWindow.ShowAsync(
                OwnerWindow,
                "Thêm nhóm hàng mới",
                "Tên nhóm hàng *",
                "Ví dụ: Hoa cúc, Hoa hồng, Phụ kiện...");

            if (string.IsNullOrWhiteSpace(tenNhom))
                return;

            var newOption = await _db.ThemNhomSanPhamAsync(tenNhom);
            await LoadNhomSanPhamAsync();

            SelectedNhomSanPham = NhomSanPhamOptions?.FirstOrDefault(x => x.Id == newOption.Id);

            await NotificationWindow.ShowSuccessAsync(
                $"Đã thêm nhóm hàng \"{newOption.Name}\" thành công.",
                owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm nhóm hàng: " + ex.Message;
            await NotificationWindow.ShowErrorAsync(
                "Không thể thêm nhóm hàng: " + ex.Message,
                owner: OwnerWindow);
        }
    }

    // 3. Lệnh Lọc theo nhóm
    [RelayCommand]
    private async Task LocTheoNhomAsync(ProductOption? nhom)
    {
        SelectedNhomSanPham = nhom;
        SelectedNhomId = nhom?.Id; 

        await RefreshProductsAsync(); 

        StatusMessage = nhom == null || nhom.Id == 0
            ? $"Hiển thị tất cả {SanPhams.Count} sản phẩm" 
            : $"Lọc theo nhóm '{nhom.Name}': {SanPhams.Count} sản phẩm";
    }

    // 4. Sắp xếp đa tiêu chuẩn
    [RelayCommand]
    private void SapXepTheoTieuChi(string criteria)
    {
        CurrentSortOption = criteria;
        List<SanPham> sorted = criteria switch
        {
            "Mã hàng" => SanPhams.OrderBy(p => p.MaSanPham).ToList(),
            "Giá tăng dần" => SanPhams.OrderBy(p => p.GiaBan).ToList(),
            "Giá giảm dần" => SanPhams.OrderByDescending(p => p.GiaBan).ToList(),
            _ => SanPhams.OrderBy(p => p.TenSanPham).ToList(),
        };

        SanPhams.Clear();
        foreach (var product in sorted)
        {
            SanPhams.Add(product);
        }

        StatusMessage = $"Đã sắp xếp theo: {criteria}";
    }


    [RelayCommand]
    private async Task LocSanPhamAsync(string? filter)
    {
        ProductFilter = filter ?? "";
        await RefreshProductsAsync();
        StatusMessage = string.IsNullOrWhiteSpace(ProductFilter)
            ? $"Hiển thị {SanPhams.Count} sản phẩm"
            : $"Lọc được {SanPhams.Count} sản phẩm";
    }

    [RelayCommand]
    private void SapXepSanPham()
    {
        _sortByPrice = !_sortByPrice;
        var sorted = _sortByPrice
            ? SanPhams.OrderBy(product => product.GiaBan).ToList()
            : SanPhams.OrderBy(product => product.TenSanPham).ToList();

        SanPhams.Clear();
        foreach (var product in sorted)
            SanPhams.Add(product);

        StatusMessage = _sortByPrice ? "Đã sắp xếp theo giá tăng dần" : "Đã sắp xếp theo tên";
    }

    // =========================================================
    // HÀNG HÓA: CHỈNH GIÁ BÁN
    // =========================================================

    /// <summary>
    /// Người dùng hiện tại có quyền chỉnh giá bán không (dựa trên quyền chi tiết,
    /// không gắn cứng theo tên vai trò).
    /// </summary>
    public bool DuocSuaGiaBan => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsUpdatePrice);

    /// <summary>
    /// Mở ô nhập giá ngay tại dòng sản phẩm (chỉnh nhanh).
    /// Chỉ một dòng được mở tại một thời điểm để không lẫn giá của sản phẩm khác.
    /// </summary>
    [RelayCommand(CanExecute = nameof(DuocSuaGiaBan))]
    private void BatDauChinhGiaBan(SanPham? product)
    {
        if (!DuocSuaGiaBan)
        {
            StatusMessage = "Bạn không có quyền chỉnh giá bán hàng hóa.";
            return;
        }

        if (product is null || product.Id == 0)
        {
            StatusMessage = "Sản phẩm này chưa có mã dữ liệu để chỉnh giá bán.";
            return;
        }

        // Đóng các dòng đang mở khác, tránh sửa nhầm sang sản phẩm khác
        foreach (var item in SanPhams)
        {
            if (!ReferenceEquals(item, product))
                item.DongGiaBanNhanh();
        }

        // Nạp đúng giá của sản phẩm đang chọn, bỏ dấu phân cách cho dễ gõ
        product.DangChinhGia = true;
        product.GiaBanDangNhap = MoneyInput.ToRawText(product.GiaBan);
        product.LoiGiaBanNhanh = null;
        product.DangLuGiaBan = false;
    }

    /// <summary>Nhấn ✓: validate, UPDATE đúng một dòng, rồi đóng ô nhập.</summary>
    [RelayCommand(CanExecute = nameof(DuocSuaGiaBan))]
    private async Task LuuGiaBanNhanhAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsUpdatePrice, "Bạn không có quyền cập nhật giá bán hàng hóa."))
        {
            return;
        }
        if (product is null)
            return;

        if (!DuocSuaGiaBan)
        {
            product.LoiGiaBanNhanh = "Bạn không có quyền chỉnh giá bán hàng hóa.";
            return;
        }

        // Validate ngay trên dòng, chưa đụng database
        if (!MoneyInput.TryParse(
                product.GiaBanDangNhap,
                true,
                out var giaBan,
                out var loi))
        {
            product.LoiGiaBanNhanh = loi;
            return;
        }

        if (giaBan == product.GiaBan)
        {
            product.DongGiaBanNhanh();
            StatusMessage = "Giá bán không thay đổi.";
            return;
        }

        product.DangLuGiaBan = true;
        product.LoiGiaBanNhanh = null;

        try
        {
            var updated =
                await _db.CapNhatGiaBanSanPhamAsync(product.Id, giaBan);

            // Sản phẩm đã bị người khác xóa: đóng ô nhập, bỏ dòng, tải lại danh sách
            if (!updated)
            {
                product.DongGiaBanNhanh();
                SanPhams.Remove(product);

                await RefreshProductsAsync();

                // Nói sau khi tải lại danh sách để không bị thông điệp
                // "đã tải N sản phẩm" của lần tải này ghi đè mất.
                StatusMessage = "Sản phẩm không còn tồn tại trong hệ thống.";
                return;
            }

            DongBoGiaBan(product.Id, giaBan);

            product.DongGiaBanNhanh();
            StatusMessage = "Cập nhật giá bán thành công";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Cập nhật giá bán sản phẩm", ex);

            var message = SafeLog.Redact(ex.Message);

            product.LoiGiaBanNhanh = message;
            StatusMessage = "Không thể cập nhật giá bán: " + message;
        }
        finally
        {
            product.DangLuGiaBan = false;
        }
    }

    /// <summary>
    /// Nhấn ✕: đóng ô nhập và giữ nguyên giá cũ trong database
    /// (không gọi UPDATE, không đụng vào SanPham.GiaBan).
    /// </summary>
    [RelayCommand]
    private void HuyChinhGiaBan(SanPham? product)
    {
        if (product is null)
            return;

        product.DongGiaBanNhanh();

        StatusMessage = "Đã hủy chỉnh giá bán.";
    }

    /// <summary>
    /// Đồng bộ giá bán mới cho mọi bản sao của sản phẩm đang nạp trong bộ nhớ
    /// (danh sách hàng hóa, tìm kiếm bán hàng, giỏ hàng) để không màn hình nào
    /// còn giữ giá cũ. Chỉ cập nhật vài đối tượng, không SELECT lại toàn bảng.
    /// </summary>
    private void DongBoGiaBan(uint productId, decimal giaBan)
    {
        foreach (var item in SanPhams)
        {
            if (item.Id == productId)
                item.GiaBan = giaBan;
        }

        foreach (var item in ProductSearchResults)
        {
            if (item.Id == productId)
                item.GiaBan = giaBan;
        }

        var doiGioHang = false;

        foreach (var item in SaleCart)
        {
            if (item.SanPham.Id != productId)
                continue;

            item.SanPham.GiaBan = giaBan;
            item.CapNhatThanhTien();
            doiGioHang = true;
        }

        if (doiGioHang)
            RecalculateSaleTotal();
    }

    /// <summary>
    /// Đồng bộ số lượng tồn kho mới cho mọi bản sao của sản phẩm đang nạp trong bộ nhớ
    /// (danh sách hàng hóa, tìm kiếm bán hàng, giỏ hàng) để không màn hình nào
    /// còn giữ số tồn cũ.
    /// </summary>
    private void DongBoTonKho(uint productId, decimal tonKho)
    {
        foreach (var item in SanPhams)
        {
            if (item.Id == productId)
            {
                item.TonKho = tonKho;
                item.TonKhoExcel = tonKho;
            }
        }

        foreach (var item in ProductSearchResults)
        {
            if (item.Id == productId)
            {
                item.TonKho = tonKho;
                item.TonKhoExcel = tonKho;
            }
        }

        foreach (var item in SaleCart)
        {
            if (item.SanPham.Id == productId)
            {
                item.SanPham.TonKho = tonKho;
                item.SanPham.TonKhoExcel = tonKho;
            }
        }
    }

    

    [RelayCommand]
    private async Task XoaSanPhamAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsDelete, "Bạn không có quyền xóa hàng hóa."))
        {
            return;
        }
        if (product is null || product.Id == 0)
        {
            StatusMessage = "Sản phẩm này chưa có mã dữ liệu để xóa.";
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Xóa sản phẩm",
                $"Bạn có chắc muốn xóa sản phẩm \"{product.TenSanPham}\" không?");

            if (!confirmed)
                return;

            await _db.XoaSanPhamAsync(product.Id);
            SanPhams.Remove(product);
            StatusMessage = $"Đã xóa sản phẩm {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể xóa sản phẩm: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SuaSanPhamAsync(SanPham? product)
    {
        // Mở trang chi tiết chỉ cần quyền "xem chi tiết hàng hóa" (hanghoa.xem_chi_tiet).
        // Quyền "sửa hàng hóa" (hanghoa.sua) chỉ dùng để cho phép lưu thay đổi.
        var coQuyenSua = CoQuyen(PermissionKeys.ProductsEdit);
        var coQuyenXem = CoQuyen(PermissionKeys.ProductsDetail);

        if (!coQuyenSua && !coQuyenXem)
        {
            await KiemTraQuyenAsync(PermissionKeys.ProductsDetail, "Bạn không có quyền xem chi tiết hàng hóa.");
            return;
        }

        if (product is null)
        {
            StatusMessage = "Vui lòng chọn sản phẩm.";
            return;
        }

        if (product.Id == 0)
        {
            StatusMessage = "Sản phẩm này chưa có mã dữ liệu để xem.";
            return;
        }

        var (groups, units) = await LoadProductOptionsAsync();

        // Không có quyền sửa thì mở ở chế độ chỉ xem. Ô giá bán / giá vốn / tồn kho
        // mở theo đúng quyền chi tiết tương ứng (sua_gia_ban / sua_gia_von / dieu_chinh_ton_kho).
        var editor = new ProductEditorWindow(
            product,
            groups,
            units,
            duocSuaGiaBan: DuocSuaGiaBan,
            duocSuaGiaVon: CoQuyen(PermissionKeys.ProductsUpdateCost),
            duocDieuChinhTonKho: CoQuyen(PermissionKeys.ProductsAdjustStock),
            cheDoXem: !coQuyenSua);

        var saved = OwnerWindow is not null && await editor.ShowDialog<bool>(OwnerWindow);

        // Hủy hoặc chỉ xem thì không gọi UPDATE: dữ liệu cũ giữ nguyên trong database
        if (!saved || !coQuyenSua)
            return;

        try
        {
            await _db.CapNhatSanPhamAsync(product);

            // Đồng bộ giá bán mới sang bán hàng / tìm kiếm / giỏ hàng
            DongBoGiaBan(product.Id, product.GiaBan);

            product.DongGiaBanNhanh();

            // Chỉ cập nhật đúng dòng vừa sửa (kèm tên ĐVT / nhóm hàng lấy qua
            // JOIN) thay vì nạp lại cả danh sách: giữ nguyên vị trí cuộn và lựa
            // chọn nên bảng hàng hóa không bị "nhảy" làm người dùng nhầm lẫn.
            var index = SanPhams.IndexOf(product);
            var refreshed = await _db.GetSanPhamByIdAsync(product.Id);

            if (index >= 0)
            {
                if (refreshed is null)
                {
                    // Không còn đọc được sản phẩm (bị xóa ở nơi khác) mới nạp lại cả danh sách.
                    await RefreshProductsAsync();
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(refreshed.HinhAnh) && File.Exists(refreshed.HinhAnh))
                        refreshed.HinhAnhBitmap = new Bitmap(refreshed.HinhAnh);

                    SanPhams[index] = refreshed;
                    OnPropertyChanged(nameof(IsSanPhamListEmpty));
                }
            }

            var maHienThi = refreshed?.MaSanPham ?? product.MaSanPham;

            StatusMessage = $"Đã cập nhật hàng hóa {maHienThi}";

            _ = LoadNhomSanPhamAsync();

            await NotificationWindow.ShowSuccessAsync(
                $"Đã cập nhật hàng hóa {maHienThi}.",
                owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Cập nhật sản phẩm", ex);
            StatusMessage = "Không thể cập nhật sản phẩm: " + SafeLog.Redact(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CapNhatHinhAnhAsync(SanPham? product)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsEdit, "Bạn không có quyền cập nhật hàng hóa."))
        {
            return;
        }
        if (product is null || product.Id == 0 || OwnerWindow is null)
        {
            StatusMessage = "Không thể mở chọn ảnh cho sản phẩm này.";
            return;
        }

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn ảnh sản phẩm",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Ảnh")
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp"]
                    }
                ]
            });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        var imagePath = file.Path.LocalPath;

        try
        {
            await _db.CapNhatHinhAnhSanPhamAsync(product.Id, imagePath);
            product.HinhAnh = imagePath;
            product.HinhAnhBitmap = new Bitmap(imagePath);
            StatusMessage = $"Đã cập nhật ảnh cho {product.MaSanPham}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể cập nhật ảnh: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemSanPhamAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsCreate, "Bạn không có quyền thêm hàng hóa."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        var (groups, units) = await LoadProductOptionsAsync();
        var editor = new ProductEditorWindow(null, groups, units);
        if (!await editor.ShowDialog<bool>(OwnerWindow))
            return;

        try
        {
            editor.Product.Id = await _db.ThemSanPhamAsync(editor.Product);
            var refreshed = await _db.GetSanPhamByIdAsync(editor.Product.Id);
            SanPhams.Add(refreshed ?? editor.Product);
            ProductTotalCount = SanPhams.Count;
            OnPropertyChanged(nameof(IsSanPhamListEmpty));
            StatusMessage = $"Đã thêm sản phẩm {editor.Product.MaSanPham}";
            _ = LoadNhomSanPhamAsync();

            await NotificationWindow.ShowSuccessAsync(
                $"Đã thêm hàng hóa {editor.Product.MaSanPham}.",
                owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm sản phẩm: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task ThemSanPhamTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsImport, "Bạn không có quyền import hàng hóa."))
        {
            return;
        }
        if (OwnerWindow is null)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file danh sách hàng hóa",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                    {
                        Patterns = ["*.xlsx", "*.xls", "*.csv"]
                    }
                ]
            });

        var file = files.FirstOrDefault();

        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file để nhập hàng loạt.";
            return;
        }

        KetQuaImportSanPham? ketQua = null;

        try
        {
            IsImporting = true;
            ImportStatus = $"Đang đọc dữ liệu từ file \"{file.Name}\"...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            // 1. ĐỌC FILE — đọc hết mọi dòng, không giới hạn 20/30/50/100 dòng.
            // Dòng đọc lỗi không bị loại âm thầm mà được giữ lại để báo trong kết quả.
            var productsFromFile = _db.ParseProductsFromFile(file.Path.LocalPath, out var loiDocFile);
            var tongSoDongTrongFile = productsFromFile.Count + loiDocFile.Count;

            if (productsFromFile.Count == 0)
            {
                IsImporting = false;

                // Vẫn phải báo cáo nếu mọi dòng đều lỗi, không phải chỉ nói "không có dữ liệu".
                var ketQuaLoi = new KetQuaImportSanPham
                {
                    TenFile = file.Name,
                    TongSoDongExcel = tongSoDongTrongFile,
                    SoLoi = loiDocFile.Count
                };

                ketQuaLoi.LoiDong.AddRange(loiDocFile);

                if (OwnerWindow is not null && loiDocFile.Count > 0)
                    await KetQuaImportWindow.ShowAsync(OwnerWindow, ketQuaLoi);
                else
                    await ShowConfirmationAsync(
                        "File không có dữ liệu",
                        $"File \"{file.Name}\" không có sản phẩm hợp lệ nào để nhập.",
                        "Đã hiểu",
                        "Đóng");

                return;
            }

            ImportStatus = $"Đã đọc {tongSoDongTrongFile:N0} dòng"
                + (loiDocFile.Count > 0 ? $", trong đó {loiDocFile.Count:N0} dòng lỗi" : "")
                + ". Đang kiểm tra trùng...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            // 2. PHÁT HIỆN TRÙNG — khóa chính là MÃ HÀNG, kiểm tra thêm MÃ VẠCH.
            var sanPhamTrung = await _db.TimSanPhamTrungAsync(productsFromFile);

            var quyetDinh = await HoiQuyetDinhSanPhamTrungAsync(
                productsFromFile,
                sanPhamTrung);

            if (quyetDinh == XuLySanPhamTrung.Huy)
            {
                IsImporting = false;
                StatusMessage = "Đã hủy nhập file. Không có dữ liệu nào được thay đổi.";
                return;
            }

            // 3. LẬP DANH SÁCH THAO TÁC cho từng dòng
            var danhSachHang = new List<HangNhapSanPham>(productsFromFile.Count);
            var thongTinTrung = TaoDanhSachHangNhap(
                productsFromFile,
                sanPhamTrung,
                quyetDinh,
                danhSachHang);

            var soGhi = danhSachHang.Count(h => h.hanDong is HanDong.ThemMoi or HanDong.ThayThe);

            if (soGhi == 0)
            {
                IsImporting = false;

                var soGiu = danhSachHang.Count(h => h.hanDong == HanDong.GiuCu);
                StatusMessage =
                    $"Đã giữ nguyên {soGiu:N0} sản phẩm trùng, không ghi thêm dữ liệu nào.";

                // Vẫn phải báo các dòng lỗi khi đọc file, nếu không người dùng
                // không biết vì sao tổng số dòng không khớp với số sản phẩm nhập.
                var ketQuaGiu = new KetQuaImportSanPham
                {
                    TenFile = file.Name,
                    TongSoDongExcel = tongSoDongTrongFile,
                    SoGiuCu = soGiu,
                    SoLoi = loiDocFile.Count
                };

                ketQuaGiu.LoiDong.AddRange(loiDocFile);

                if (OwnerWindow is not null && loiDocFile.Count > 0)
                    await KetQuaImportWindow.ShowAsync(OwnerWindow, ketQuaGiu);

                return;
            }

            // 4. GHI XUỐNG DATABASE trong một transaction
            ImportStatus = $"Đang nhập {soGhi:N0} sản phẩm vào database...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            ketQua = await _db.ImportSanPhamAsync(
                            file.Name,
                            danhSachHang,
                            tongSoDongTrongFile,
                            loiDocFile);

            // Số giữ cũ do người dùng quyết định, không phải do database ghi.
            ketQua.SoGiuCu = thongTinTrung.SoGiuCu;

            // 5. RELOAD LẠI TỪ DATABASE, không chỉ thêm vào collection tạm.
            ImportStatus = "Đang tải lại danh sách hàng hóa từ database...";
            StatusMessage = ImportStatus;

            await Task.Delay(100);

            await RefreshProductsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể nhập hàng loạt từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }

        // 6. BÁO CÁO KẾT QUẢ cho người dùng
        if (ketQua is not null && OwnerWindow is not null)
        {
            StatusMessage = ketQua.DaRollback
                ? "Nhập file thất bại, đã rollback toàn bộ."
                : $"Nhập xong: thêm mới {ketQua.SoThemMoi:N0}, "
                  + $"thay thế {ketQua.SoThayThe:N0}, giữ cũ {ketQua.SoGiuCu:N0}, "
                  + $"lỗi {ketQua.SoLoi:N0}.";

            await KetQuaImportWindow.ShowAsync(OwnerWindow, ketQua);
        }
    }

    /// <summary>
    /// Hỏi người dùng xử lý sản phẩm trùng: giữ cũ / thay thế / hủy.
    /// Trả về lựa chọn, hoặc null nếu không có gì trùng.
    /// </summary>
    private async Task<XuLySanPhamTrung?> HoiQuyetDinhSanPhamTrungAsync(
        IReadOnlyList<SanPham> productsFromFile,
        IReadOnlyList<SanPham> sanPhamDaCo)
    {
        if (OwnerWindow is null || sanPhamDaCo.Count == 0)
            return null;

        var danhSachTrung = TaoDanhSachSanPhamTrung(productsFromFile, sanPhamDaCo);

        if (danhSachTrung.Count == 0)
            return null;

        // Tắt loading để hộp thoại hiện được trên UI thread.
        IsImporting = false;
        await Task.Delay(50);

        return await SanPhamTrungWindow.ShowAsync(OwnerWindow, danhSachTrung);
    }

    /// <summary>
    /// Ghép từng sản phẩm trong file với bản ghi đang có trong database.
    /// Mã hàng là khóa chính; mã vạch chỉ dùng khi không trùng mã hàng.
    /// Cố tình không so tên hàng vì hai món khác tên dễ bị trùng tên.
    /// </summary>
    private static List<SanPhamTrung> TaoDanhSachSanPhamTrung(
        IReadOnlyList<SanPham> productsFromFile,
        IReadOnlyList<SanPham> sanPhamDaCo)
    {
        var theoMaSanPham = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);
        var theoMaVach = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);

        foreach (var sanPham in sanPhamDaCo)
        {
            var maSanPham = sanPham.MaSanPham?.Trim();

            if (!string.IsNullOrWhiteSpace(maSanPham) && !theoMaSanPham.ContainsKey(maSanPham))
                theoMaSanPham[maSanPham] = sanPham;

            var maVach = sanPham.MaVach?.Trim();

            if (!string.IsNullOrWhiteSpace(maVach) && !theoMaVach.ContainsKey(maVach))
                theoMaVach[maVach] = sanPham;
        }

        var result = new List<SanPhamTrung>();
        var daThem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in productsFromFile)
        {
            var maSanPham = product.MaSanPham?.Trim();
            var maVach = product.MaVach?.Trim();

            SanPham? cu = null;
            var lyDo = string.Empty;

            if (!string.IsNullOrWhiteSpace(maSanPham)
                && theoMaSanPham.TryGetValue(maSanPham, out var theoMa))
            {
                cu = theoMa;
                lyDo = "Mã hàng";
            }
            else if (!string.IsNullOrWhiteSpace(maVach)
                     && theoMaVach.TryGetValue(maVach, out var theoVach))
            {
                cu = theoVach;
                lyDo = "Mã vạch";
            }

            if (cu is null)
                continue;

            // Sản phẩm trùng cả mã hàng lẫn mã vạch chỉ báo một lần.
            if (!daThem.Add(cu.Id.ToString()))
                continue;

            result.Add(new SanPhamTrung
            {
                SanPhamCu = cu,
                SanPhamMoi = product,
                SoDongExcel = product.SoDongExcel,
                LyDoTrung = lyDo
            });
        }

        return result;
    }

    /// <summary>
    /// Biến danh sách sản phẩm đọc từ file thành danh sách quyết định cho từng dòng:
    /// thêm mới / thay thế / giữ cũ.
    /// </summary>
    private static (int SoGiuCu, List<SanPhamTrung> DanhSachTrung) TaoDanhSachHangNhap(
        IReadOnlyList<SanPham> productsFromFile,
        IReadOnlyList<SanPham> sanPhamDaCo,
        XuLySanPhamTrung? quyetDinh,
        List<HangNhapSanPham> danhSachHang)
    {
        var theoMaSanPham = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);
        var theoMaVach = new Dictionary<string, SanPham>(StringComparer.OrdinalIgnoreCase);

        foreach (var sanPham in sanPhamDaCo)
        {
            var maSanPham = sanPham.MaSanPham?.Trim();

            if (!string.IsNullOrWhiteSpace(maSanPham) && !theoMaSanPham.ContainsKey(maSanPham))
                theoMaSanPham[maSanPham] = sanPham;

            var maVach = sanPham.MaVach?.Trim();

            if (!string.IsNullOrWhiteSpace(maVach) && !theoMaVach.ContainsKey(maVach))
                theoMaVach[maVach] = sanPham;
        }

        var thayThe = quyetDinh == XuLySanPhamTrung.ThayTheBangExcel;
        var danhSachTrung = new List<SanPhamTrung>();
        var soGiuCu = 0;

        foreach (var product in productsFromFile)
        {
            var maSanPham = product.MaSanPham?.Trim();
            var maVach = product.MaVach?.Trim();

            SanPham? cu = null;

            if (!string.IsNullOrWhiteSpace(maSanPham)
                && theoMaSanPham.TryGetValue(maSanPham, out var theoMa))
            {
                cu = theoMa;
            }
            else if (!string.IsNullOrWhiteSpace(maVach)
                     && theoMaVach.TryGetValue(maVach, out var theoVach))
            {
                cu = theoVach;
            }

            if (cu is null)
            {
                danhSachHang.Add(new HangNhapSanPham(product, product.SoDongExcel));
                continue;
            }

            if (thayThe)
            {
                danhSachHang.Add(new HangNhapSanPham(
                    product,
                    product.SoDongExcel,
                    cu.Id,
                    cu,
                    HanDong.ThayThe));
            }
            else
            {
                soGiuCu++;
                danhSachTrung.Add(new SanPhamTrung
                {
                    SanPhamCu = cu,
                    SanPhamMoi = product,
                    SoDongExcel = product.SoDongExcel,
                    LyDoTrung = string.Equals(cu.MaSanPham?.Trim(), maSanPham,
                        StringComparison.OrdinalIgnoreCase)
                        ? "Mã hàng"
                        : "Mã vạch"
                });

                // Giữ cũ: KHÔNG UPDATE, giữ nguyên toàn bộ dữ liệu database.
                danhSachHang.Add(new HangNhapSanPham(
                    product,
                    product.SoDongExcel,
                    cu.Id,
                    cu,
                    HanDong.GiuCu));
            }
        }

        return (soGiuCu, danhSachTrung);
    }
    /// <summary>
    /// Tải lại danh sách hàng hóa theo từ khóa đang gõ.
    /// Từ khóa được MySQL lọc trên TOÀN BỘ bảng san_pham rồi mới cắt bằng LIMIT,
    /// nên sản phẩm nằm ở trang khác / ngoài phần đang hiển thị vẫn tìm ra được.
    /// </summary>
    private async Task RefreshProductsAsync()
{
    // Version guard: người dùng gõ tiếp trong lúc chờ database thì bỏ kết quả cũ,
    // tránh danh sách bị nhảy ngược về từ khóa cũ.
    var version = ++_productLoadVersion;

    try
    {
        var (products, tongSoKhop) = await _db.GetSanPhamPagedAsync(
            ProductSearch,
            1,
            DatabaseService.SoDongSanPhamToiDa);

        if (version != _productLoadVersion)
            return;

        // --- BỔ SUNG LOGIC LỌC THEO NHÓM SẢN PHẨM ---
        if (SelectedNhomSanPham != null && SelectedNhomSanPham.Id > 0)
        {
            if (SelectedNhomSanPham.Id == 999999)
            {
                // Lọc các sản phẩm chưa phân nhóm (Id nhóm bằng 0 hoặc null)
                products = products.Where(p => p.NhomSanPhamId == null || p.NhomSanPhamId == 0).ToList();
            }
            else
            {
                // Lọc theo nhóm cụ thể
                products = products.Where(p => p.NhomSanPhamId == SelectedNhomSanPham.Id).ToList();
            }
        }
        // Nếu Id == 0 (Tất cả sản phẩm) thì bỏ qua, giữ nguyên danh sách.
        // -------------------------------------------

        if (!string.IsNullOrWhiteSpace(ProductFilter))
        {
            products = products
                .Where(product => MatchesFilter(product, ProductFilter))
                .ToList();
        }

        SanPhams.Clear();
        foreach (var product in products)
        {
            if (!string.IsNullOrWhiteSpace(product.HinhAnh) && File.Exists(product.HinhAnh))
                product.HinhAnhBitmap = new Bitmap(product.HinhAnh);

            SanPhams.Add(product);
        }

        ProductTotalCount = products.Count; // Cập nhật lại số lượng theo kết quả thực tế sau khi lọc

        OnPropertyChanged(nameof(IsSanPhamListEmpty));

        var dangTimKiem = !string.IsNullOrWhiteSpace(ProductSearch);
        var tenNhomDangChon = SelectedNhomSanPham?.Id > 0 && SelectedNhomSanPham.Id != 999999 ? SelectedNhomSanPham.Name : null;

        StatusMessage = SanPhams.Count == 0
            ? SanPhamEmptyText
            : (dangTimKiem
                ? $"Tìm thấy {SanPhams.Count:N0} sản phẩm theo \"{ProductSearch.Trim()}\""
                : (tenNhomDangChon != null 
                    ? $"Lọc theo nhóm \"{tenNhomDangChon}\": {SanPhams.Count:N0} sản phẩm" 
                    : $"Đã tải {SanPhams.Count:N0} sản phẩm"));
    }
    catch (Exception ex)
    {
        if (version != _productLoadVersion)
            return;

        SanPhams.Clear();
        ProductTotalCount = 0;

        OnPropertyChanged(nameof(IsSanPhamListEmpty));

        SafeLog.WriteError("Tải danh sách hàng hóa", ex);

        StatusMessage =
            "Không thể tải danh sách hàng hóa: " + SafeLog.Redact(ex.Message);
    }
}

    private static bool MatchesFilter(SanPham product, string filter)
    {
        var normalizedFilter = NormalizeText(filter);

        if (string.IsNullOrWhiteSpace(normalizedFilter))
            return true;

        var haystacks = new[]
        {
            product.LoaiHang,
            product.NhomHangBaCap,
            product.TenSanPham,
            product.NhomSanPham,
            product.DonViTinh
        };

        return haystacks
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => NormalizeText(value).Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace("/", " ")
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace("  ", " ");
    }
    private async Task ShowMessageAsync(string title, string message, Window? owner = null)
    {
        var targetOwner = owner ?? OwnerWindow;
        if (targetOwner is null)
            return;

        var dialog = new Window
        {
            Title = title,
            Width = 430,
            // Cao theo nội dung: tiêu đề/nguyên nhân dài không bị cắt chữ.
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            MinHeight = 230,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.White
        };

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brush.Parse("#173F38"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 15,
            Foreground = Avalonia.Media.Brush.Parse("#4F625D"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            MaxWidth = 350
        };

        var okButton = new Button
        {
            Content = "Đã hiểu",
            Width = 110,
            Height = 38,
            Background = Avalonia.Media.Brush.Parse("#078C7A"),
            Foreground = Avalonia.Media.Brushes.White,
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            Padding = new Avalonia.Thickness(16, 0)
        };

        var panel = new StackPanel
        {
            Spacing = 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        panel.Children.Add(titleText);
        panel.Children.Add(messageText);
        panel.Children.Add(okButton);

        var border = new Border
        {
            Background = Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(12),
            Padding = new Avalonia.Thickness(25),
            Child = panel
        };

        dialog.Content = border;

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(targetOwner);
    }
    private async Task<bool> ShowConfirmationAsync(string title, string message)
            => await ShowConfirmationAsync(title, message, "Xóa", "Hủy");

    private async Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText, string cancelButtonText)
    {
        if (OwnerWindow is null)
            return false;

        var dialog = new ConfirmationWindow(title, message, confirmButtonText, cancelButtonText);
        return await dialog.ShowDialog<bool>(OwnerWindow);
    }

    /// <summary>
    /// Báo lỗi khi lưu phiếu nhập: hiện hộp thoại "Thất bại" kèm nguyên nhân và
    /// giữ nguyên toàn bộ form để người dùng sửa rồi bấm lại.
    /// </summary>
    private async Task BaoLoiLuuPhieuNhapAsync(string lyDo)
    {
        var noiDung = string.IsNullOrWhiteSpace(lyDo)
            ? "Lỗi không xác định. Vui lòng thử lại."
            : lyDo.Trim();

        StatusMessage = "❌ Thất bại – Không thể lưu phiếu nhập: " + noiDung;

        await ShowMessageAsync(
            "Thất bại – Không thể lưu phiếu nhập.",
            $"{noiDung}\n\nDữ liệu đang nhập vẫn được giữ nguyên, hãy sửa và bấm Hoàn thành lại.");
    }

    private async Task<(IReadOnlyList<ProductOption> Groups, IReadOnlyList<ProductOption> Units)> LoadProductOptionsAsync()
    {
        try
        {
            var groupsTask = _db.GetNhomSanPhamAsync();
            var unitsTask = _db.GetDonViTinhAsync();
            await Task.WhenAll(groupsTask, unitsTask);
            return (await groupsTask, await unitsTask);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải nhóm hàng/đơn vị tính: " + ex.Message;
            return (Array.Empty<ProductOption>(), Array.Empty<ProductOption>());
        }
    }

    private static bool IsDuplicateProduct(SanPham product, IReadOnlyList<SanPham> existingProducts)
    {
        // Còn dùng cho luồng nhập KHÁCH HÀNG, giữ nguyên logic cũ ở đây.
        var trimmedMaSanPham = product.MaSanPham.Trim();
        var trimmedMaVach = product.MaVach?.Trim();
        var trimmedTenSanPham = product.TenSanPham.Trim();

        return existingProducts.Any(existing =>
            (!string.IsNullOrWhiteSpace(trimmedMaSanPham) &&
                existing.MaSanPham.Equals(trimmedMaSanPham, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(trimmedMaVach) &&
                !string.IsNullOrWhiteSpace(existing.MaVach) &&
                existing.MaVach.Equals(trimmedMaVach, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(trimmedTenSanPham) &&
                existing.TenSanPham.Equals(trimmedTenSanPham, StringComparison.OrdinalIgnoreCase)));
    }
    [RelayCommand]
    public async Task SearchSaleCustomersAsync(string? search)
    {
        try
        {
            var keyword = search?.Trim() ?? string.Empty;

            SaleCustomerSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(keyword) ||
                keyword.Equals("Khách lẻ", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var customers = await _db.GetKhachHangAsync(keyword);

            foreach (var customer in customers.Take(20))
            {
                SaleCustomerSuggestions.Add(customer);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm khách hàng: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task SearchInvoicesAsync()
    {
        try
        {
            HoaDons.Clear();
            foreach (var item in await _db.GetHoaDonAsync(InvoiceSearch, LocHoaDonHienTai))
                HoaDons.Add(item);
            StatusMessage = $"Tìm thấy {HoaDons.Count} hóa đơn";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }
    [RelayCommand]
    private async Task ClearInvoiceSearchAsync()
    {
        InvoiceSearch = "";

        try
        {
            HoaDons.Clear();

            foreach (var item in await _db.GetHoaDonAsync(InvoiceSearch, LocHoaDonHienTai))
                HoaDons.Add(item);

            StatusMessage = $"Đã tải {HoaDons.Count} hóa đơn";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải hóa đơn: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task ThemNhaCungCapAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersCreate, "Bạn không có quyền thêm nhà cung cấp."))
        {
            return;
        }
        var supplier = await HienThiFormThemNhaCungCapAsync();

        if (supplier is null)
            return;

        StatusMessage =
            $"Đã thêm nhà cung cấp {supplier.TenNhaCungCap}";
    }

    /// <summary>
    /// Mở form thêm nhà cung cấp dùng chung cho cả module quản lý
    /// và màn lập phiếu nhập. Trả về nhà cung cấp vừa lưu, null nếu người dùng hủy.
    /// </summary>
    private async Task<NhaCungCap?> HienThiFormThemNhaCungCapAsync()
    {
        if (OwnerWindow is null)
            return null;

        // Tự phát sinh mã NCC từ database
        var generatedCode = await _db.TaoMaNhaCungCapMoiAsync();

        var newSupplier = new NhaCungCap
        {
            MaNhaCungCap = generatedCode,
            TenNhaCungCap = string.Empty,
            TrangThai = true
        };

        // Mở giao diện nhập thông tin nhà cung cấp
        var editor = new NhaCungCapEditorWindow(newSupplier);

        var saved = await editor.ShowDialog<bool>(OwnerWindow);

        if (!saved)
            return null;

        // Lưu database
        editor.Supplier.Id =
            await _db.ThemNhaCungCapAsync(editor.Supplier);

        // Danh sách đang phân trang theo database nên tải lại trang hiện tại
        // thay vì thêm thủ công một dòng có thể không thuộc trang đang xem.
        await LoadSupplierPageAsync();

        return editor.Supplier;
    }
    [RelayCommand]
    private async Task SuaNhaCungCapAsync(NhaCungCap? supplier)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersEdit, "Bạn không có quyền sửa nhà cung cấp."))
        {
            return;
        }
        if (supplier is null || OwnerWindow is null)
            return;

        var editor = new NhaCungCapEditorWindow(supplier);
        var saved = await editor.ShowDialog<bool>(OwnerWindow);

        SelectedNhaCungCap = null;

        if (!saved)
            return;

        try
        {
            await _db.CapNhatNhaCungCapAsync(editor.Supplier);

            StatusMessage =
                $"Đã cập nhật nhà cung cấp {editor.Supplier.TenNhaCungCap}";

            // Thứ tự hiển thị có thể đổi theo tên nên tải lại đúng trang đang xem.
            await LoadSupplierPageAsync();
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể cập nhật nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ThemNhaCungCapTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersImport, "Bạn không có quyền import nhà cung cấp."))
        {
            return;
        }
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file danh sách nhà cung cấp",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                {
                    Patterns = ["*.xlsx", "*.xls", "*.csv"]
                }
                ]
            });

        var file = files.FirstOrDefault();

        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file nhà cung cấp.";
            return;
        }

        try
        {
            IsImporting = true;

            ImportStatus = $"Đang đọc file \"{file.Name}\"...";
            StatusMessage = ImportStatus;

            await Task.Yield();

            // Đọc file Excel/CSV
            var suppliersFromFile = await Task.Run(() =>
                _db.ParseNhaCungCapFromFile(file.Path.LocalPath));

            if (suppliersFromFile.Count == 0)
            {
                StatusMessage =
                    $"❌ File \"{file.Name}\" không có dữ liệu nhà cung cấp hợp lệ.";
                return;
            }

            ImportStatus =
                $"Đã đọc {suppliersFromFile.Count:N0} nhà cung cấp. Đang kiểm tra...";

            StatusMessage = ImportStatus;

            await Task.Yield();

            // Chỉ hỏi database về các mã có thật trong file, không tải toàn bộ NCC.
            var existingCodes = await _db.LayMaNhaCungCapDaTonTaiAsync(
                suppliersFromFile.Select(x => x.MaNhaCungCap));

            var duplicateCount = suppliersFromFile.Count(x =>
                !string.IsNullOrWhiteSpace(x.MaNhaCungCap) &&
                existingCodes.Contains(x.MaNhaCungCap.Trim()));

            if (duplicateCount > 0)
            {
                // Tắt loading trước khi hiện hộp thoại
                IsImporting = false;

                var shouldUpdate = await ShowConfirmationAsync(
                    "Nhà cung cấp bị trùng",
                    $"Phát hiện {duplicateCount:N0} nhà cung cấp trong file " +
                    $"đã tồn tại trong database.\n\n" +
                    "\"Cập nhật dữ liệu\" sẽ ghi đè thông tin (kèm số tiền đã mua, " +
                    "công nợ) theo đúng file. Ngược lại các mã bị trùng sẽ được bỏ qua " +
                    "và chỉ những nhà cung cấp mới được nhập.",
                    "Cập nhật dữ liệu",
                    "Bỏ qua nhà cung cấp trùng");

                IsImporting = true;

                if (shouldUpdate)
                {
                    ImportStatus = "Đang cập nhật dữ liệu nhà cung cấp bị trùng...";
                    StatusMessage = ImportStatus;

                    await Task.Yield();
                }
                else
                {
                    ImportStatus = "Đang loại bỏ dữ liệu nhà cung cấp bị trùng...";
                    StatusMessage = ImportStatus;

                    await Task.Yield();

                    suppliersFromFile = suppliersFromFile
                        .Where(x =>
                            string.IsNullOrWhiteSpace(x.MaNhaCungCap) ||
                            !existingCodes.Contains(x.MaNhaCungCap.Trim()))
                        .ToList();
                }
            }

            if (suppliersFromFile.Count == 0)
            {
                StatusMessage =
                    "❌ Tất cả nhà cung cấp trong file đều đã tồn tại.";
                return;
            }

            ImportStatus =
                $"Đang nhập {suppliersFromFile.Count:N0} nhà cung cấp vào database...";

            StatusMessage = ImportStatus;

            await Task.Yield();

            // Báo tiến độ theo số dòng đã xử lý để người dùng không tưởng ứng cứng.
            var progress = new Progress<int>(daXuLy =>
            {
                ImportStatus =
                    $"Đang nhập dòng {daXuLy:N0}/{suppliersFromFile.Count:N0}...";
            });

            var ketQua = await Task.Run(() =>
                _db.ImportNhaCungCapTuFileAsync(
                    file.Path.LocalPath,
                    suppliersFromFile,
                    progress,
                    capNhatNhaCungCapDaTonTai: true));

            ImportStatus = "Đang cập nhật danh sách nhà cung cấp...";
            StatusMessage = ImportStatus;

            await LoadSuppliersAsync();

            StatusMessage = (ketQua.SoThemMoi > 0 || ketQua.SoCapNhat > 0)
                ? $"✅ Đã nhập thành công {ketQua.SoThemMoi:N0}, cập nhật {ketQua.SoCapNhat:N0} nhà cung cấp từ file \"{file.Name}\". {ketQua.ThongBaoTatCa}"
                : $"⚠️ Không có nhà cung cấp mới nào được nhập. {ketQua.ThongBaoTatCa}";

            if (ketQua.SoTrungTenVaDienThoai > 0)
            {
                StatusMessage +=
                    $" Cảnh báo: {ketQua.SoTrungTenVaDienThoai:N0} nhà cung cấp " +
                    "trùng tên và số điện thoại với nhà cung cấp đã có.";
            }

            if (ketQua.DanhSachLoi.Count > 0)
            {
                StatusMessage += " " + string.Join(" | ", ketQua.DanhSachLoi.Take(3));
            }
        }
        catch (Exception ex)
        {
            StatusMessage =
                "❌ Không thể nhập nhà cung cấp từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private async Task XoaNhaCungCapAsync(NhaCungCap? supplier)
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersDelete, "Bạn không có quyền xóa nhà cung cấp."))
        {
            return;
        }
        if (supplier is null)
            return;

        if (supplier.Id == 0)
        {
            StatusMessage =
                "Nhà cung cấp này chưa có ID trong database.";

            return;
        }

        var confirmed = await ShowConfirmationAsync(
            "Xóa nhà cung cấp",
            $"Bạn có chắc muốn xóa nhà cung cấp " +
            $"\"{supplier.TenNhaCungCap}\" không?");

        if (!confirmed)
            return;

        try
        {
            await _db.XoaNhaCungCapAsync(supplier.Id);

            SelectedNhaCungCap = null;

            StatusMessage =
                $"Đã xóa nhà cung cấp {supplier.TenNhaCungCap}";

            // Tải lại trang để số trang, tổng và thống kê khớp với database.
            await LoadSupplierPageAsync();
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Không thể xóa nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
private async Task TimKiemHoaBanAsync(string? keyword = null)
{
    var search = keyword ?? SaleSearchText ?? "";
    var products = await _db.GetSanPhamAsync(search);

    // --- BỔ SUNG LOGIC LỌC THEO NHÓM SẢN PHẨM ---
    if (SelectedNhomSanPham != null && SelectedNhomSanPham.Id > 0)
    {
        if (SelectedNhomSanPham.Id == 999999)
        {
            // Lọc các sản phẩm chưa phân nhóm (Id nhóm bằng 0 hoặc null)
            products = products.Where(p => p.NhomSanPhamId == null || p.NhomSanPhamId == 0).ToList();
        }
        else
        {
            // Lọc theo nhóm cụ thể
            products = products.Where(p => p.NhomSanPhamId == SelectedNhomSanPham.Id).ToList();
        }
    }
    // -------------------------------------------

    ProductSearchResults.Clear();
    foreach (var product in products)
    {
        ProductSearchResults.Add(product);
    }
}

    /// <summary>
    /// Đặt lại TOÀN BỘ trạng thái của đơn hàng đang bán.
    /// <para>
    /// Bản cũ chỉ xoá SaleCart và vài ô tổng tiền, giữ lại TienKhachDua,
    /// TienThoi, TienCongNo, TrangThaiThanhToan, SelectedSaleCustomer và
    /// SaleCustomerName — nên đơn thứ hai kế thừa tiền của đơn thứ nhất và
    /// khách hàng của đơn trước. Hàm này gom tất cả vào một chỗ để mọi đường
    /// vào (mở màn bán hàng, đóng màn, thanh toán xong, bấm "Đơn mới") đều
    /// xoá trạng thái cũ giống nhau.
    /// </para>
    /// </summary>
    private void ResetDonHangVeMacDinh()
    {
        foreach (var item in SaleCart)
            item.QuantityChanged -= RecalculateSaleTotal;

        SaleCart.Clear();
        SaleCustomerSuggestions.Clear();
        ProductSearchResults.Clear();

        SelectedSaleCustomer = null;
        SaleCustomerName = "Khách lẻ";
        SaleCustomerPhone = "";
        SaleCustomerAddress = "";
        SaleCanGiaoHang = false;
        SaleInKemSdtDiaChi = false;
        SaleSearchText = "";
        SaleGhiChu = "";

        SaleTotal = 0;
        SaleSubtotal = 0;
        SaleDiscountInput = 0;
        SaleDiscountAmount = 0;
        SaleDiscountMode = "Số tiền";

        TienKhachDua = 0;
        TienThoi = 0;
        TienCongNo = 0;
        TrangThaiThanhToan = "";

        SaleMaHoaDonHienThi = "";
        IsThanhToanDangXuLy = false;
    }

    /// <summary>
    /// Bắt đầu một đơn hàng mới: xoá sạch đơn cũ rồi tải lại danh sách sản phẩm.
    /// Dùng chung cho lệnh "🛒 Bán hàng" và lệnh "Đơn mới".
    /// </summary>
    [RelayCommand]
    private async Task MoGiaoDienBanHangAsync()
    {
        // Nút "Bán hàng" hiển thị theo CanViewSales (SalesView || SalesPayment
        // || admin) nên handler phải đồng bộ: người chỉ có quyền thanh toán vẫn
        // mở được màn để làm thao tác thanh toán.
        if (!CanViewSales)
        {
            await ShowMessageAsync("Thông báo", "Bạn không có quyền mở màn hình bán hàng.");
            return;
        }

        ResetDonHangVeMacDinh();

        IsSalesPanelVisible = true;

        StatusMessage = "Đã tạo đơn hàng mới. Chọn sản phẩm để bắt đầu.";

        // TẢI DANH SÁCH SẢN PHẨM KHI MỞ BÁN HÀNG
        try
        {
            var products = await _db.GetSanPhamAsync();

            foreach (var product in products)
                ProductSearchResults.Add(product);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải danh sách sản phẩm: " + ex.Message;
        }

        RecalculateSaleTotal();
    }

    /// <summary>Nút "Đơn mới" trên màn bán hàng: xoá sạch đơn hiện tại để bán tiếp.</summary>
    [RelayCommand]
    private async Task TaoDonMoiAsync()
    {
        if (IsThanhToanDangXuLy)
            return;

        var owner = OwnerWindow;

        if (owner is null)
        {
            StatusMessage = "Không xác định được cửa sổ bán hàng.";
            return;
        }

        // Đơn đang có hàng chưa thanh toán thì phải hỏi trước khi xoá.
        if (SaleCart.Count > 0)
        {
            var confirm = new ConfirmationWindow(
                "Tạo đơn mới",
                "Đơn hàng hiện tại chưa thanh toán. Xoá đơn này và bắt đầu đơn mới?",
                "Tạo đơn mới",
                "Huỷ");

            if (!await confirm.ShowDialog<bool>(owner))
            {
                StatusMessage = "Đã giữ nguyên đơn hàng hiện tại.";
                return;
            }
        }

        await MoGiaoDienBanHangAsync();
    }

    [RelayCommand]
    private void DongGiaoDienBanHang()
    {
        IsSalesPanelVisible = false;

        ResetDonHangVeMacDinh();
    }

    /// <summary>
    /// Mở hộp thoại chỉnh sửa nhanh số lượng tồn kho cho sản phẩm đã hết hàng trong màn hình bán hàng.
    /// Cho phép người dùng bổ sung số lượng tồn kho để bán ngay lập tức.
    /// </summary>
    [RelayCommand]
    private async Task ChinhSoLuongNhanhAsync(SanPham? product)
    {
        if (product is null)
            return;

        if (OwnerWindow is null)
        {
            StatusMessage = "Không xác định được cửa sổ bán hàng.";
            return;
        }

        try
        {
            var dialog = new ChinhSoLuongNhanhWindow(product);
            var result = await dialog.ShowDialog<KetQuaChinhSoLuong?>(OwnerWindow);

            if (result is null || result.SoLuongTonMoi <= 0)
            {
                StatusMessage = "Đã hủy chỉnh sửa số lượng.";
                return;
            }

            var updated = await _db.CapNhatTonKhoSanPhamAsync(product.Id, result.SoLuongTonMoi);

            if (!updated)
            {
                StatusMessage = "Không thể cập nhật số lượng tồn kho cho sản phẩm.";
                return;
            }

            // Đồng bộ số lượng tồn kho mới vào bộ nhớ
            DongBoTonKho(product.Id, result.SoLuongTonMoi);

            StatusMessage = $"Đã cập nhật tồn kho cho '{product.TenSanPham}': {result.SoLuongTonMoi:N0}.";

            // Nếu người dùng chọn thêm vào giỏ hàng ngay để bán
            if (result.ThemVaoGioHang)
            {
                await ThemVaoGioHangAsync(product);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Chỉnh sửa số lượng nhanh", ex);
            StatusMessage = "Lỗi khi cập nhật số lượng tồn kho: " + SafeLog.Redact(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ThemVaoGioHangAsync(SanPham? product)
    {
        if (product is null)
            return;

        // Reset thông báo cũ để UI luôn nhận biết có sự thay đổi mới
        StatusMessage = string.Empty;

        // 1. Sản phẩm đã hết hàng -> không thêm vào giỏ, không mở form chỉnh tồn kho
        if (product.HetHang)
        {
            StatusMessage = $"⚠️ Sản phẩm '{product.TenSanPham}' đã hết hàng, không thể thêm vào giỏ.";
            return;
        }

        // 2. Kiểm tra số lượng tồn kho với giỏ hàng
        var existing = SaleCart.FirstOrDefault(item => item.SanPham.Id == product.Id);
        var currentQuantityInCart = existing?.SoLuong ?? 0;

        if (currentQuantityInCart + 1 > product.TonKho)
        {
            StatusMessage = $"⚠️ Sản phẩm '{product.TenSanPham}' chỉ còn tồn kho {product.TonKho:N0}!";
            return;
        }

        // 3. Sản phẩm chưa có giá bán
        if (product.GiaBan <= 0)
        {
            if (OwnerWindow is null)
            {
                StatusMessage = "Không xác định được cửa sổ bán hàng.";
                return;
            }

            try
            {
                var window = new NhapGiaBanWindow();
                var giaBan = await window.ShowDialog<decimal?>(OwnerWindow);

                if (!giaBan.HasValue || giaBan.Value <= 0)
                {
                    StatusMessage = "Chưa nhập giá bán. Sản phẩm chưa được thêm vào giỏ.";
                    return;
                }

                var updated = await _db.CapNhatGiaBanSanPhamAsync(product.Id, giaBan.Value);

                if (!updated)
                {
                    StatusMessage = "Sản phẩm không còn tồn tại trong hệ thống.";
                    return;
                }

                DongBoGiaBan(product.Id, giaBan.Value);
            }
            catch (Exception ex)
            {
                StatusMessage = "Không thể cập nhật giá bán: " + ex.Message;
                return;
            }
        }

        // 4. Có giá rồi thì thêm vào giỏ
        if (existing is not null)
        {
            existing.SoLuong += 1;
        }
        else
        {
            var item = new SaleCartItem
            {
                SanPham = product,
                SoLuong = 1
            };

            item.QuantityChanged += RecalculateSaleTotal;
            SaleCart.Add(item);
        }

        RecalculateSaleTotal();

        // Thông báo thành công khi thêm vào giỏ thành công
        StatusMessage = $"Đã thêm '{product.TenSanPham}' vào giỏ hàng.";
    }
    [RelayCommand]
    private void CapNhatTongTienBanHang()
    {
        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void TangSoLuong(SaleCartItem? item)
    {
        if (item is null || item.SanPham is null) return;

        // Kiểm tra nếu tăng thêm 1 mà vượt quá tồn kho
        if (item.SoLuong + 1 > item.SanPham.TonKho)
        {
            StatusMessage = $"⚠️ Sản phẩm '{item.SanPham.TenSanPham}' chỉ còn tồn kho {item.SanPham.TonKho:N0}!";
            return;
        }

        StatusMessage = string.Empty;
        item.SoLuong += 1;
        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void GiamSoLuong(SaleCartItem? item)
    {
        if (item is null)
            return;

        item.SoLuong = Math.Max(1, item.SoLuong - 1);
        RecalculateSaleTotal();
    }

    [RelayCommand]
    private void XoaKhoiGioHang(SaleCartItem? item)
    {
        if (item is null)
            return;

        item.QuantityChanged -= RecalculateSaleTotal;
        SaleCart.Remove(item);
        RecalculateSaleTotal();
    }

    private void RecalculateSaleTotal()
    {
        SaleSubtotal = SaleCart.Sum(item => item.ThanhTien);

        SaleDiscountAmount = SaleDiscountMode == "Phần trăm"
            ? SaleSubtotal * Math.Clamp(SaleDiscountInput, 0, 100) / 100
            : Math.Clamp(SaleDiscountInput, 0, SaleSubtotal);

        SaleTotal = Math.Max(
            0,
            SaleSubtotal - SaleDiscountAmount);

        RecalculatePayment();
    }

    // ============================================================
    // BÁO CÁO
    // ============================================================

    /// <summary>
    /// Danh sách các báo cáo trong nhóm menu "Báo cáo".
    /// Muốn thêm báo cáo mới (bán hàng, tồn kho, doanh thu...) chỉ cần
    /// thêm một phần tử nữa vào đây, giao diện tự hiển thị theo.
    /// </summary>
    public ObservableCollection<BaoCaoMenuItem> BaoCaoMenuItems { get; } = new()
    {
        new BaoCaoMenuItem
        {
            Key = "cuoi-ngay",
            TieuDe = "Báo cáo cuối ngày",
            BieuTuong = "🗓",
            PermissionKey = PermissionKeys.ReportsEndOfDay
        },
        new BaoCaoMenuItem
        {
            Key = "ban-hang",
            TieuDe = "Báo cáo bán hàng",
            BieuTuong = "📊",
            PermissionKey = PermissionKeys.ReportsSales
        },
        new BaoCaoMenuItem
        {
            Key = "hang-hoa",
            TieuDe = "Báo cáo hàng hóa",
            BieuTuong = "📦",
            PermissionKey = PermissionKeys.ReportsProducts
        },
        new BaoCaoMenuItem
        {
            Key = "khach-hang",
            TieuDe = "Báo cáo khách hàng",
            BieuTuong = "👥",
            PermissionKey = PermissionKeys.ReportsSales
        },
        new BaoCaoMenuItem
        {
            Key = "nha-cung-cap",
            TieuDe = "Báo cáo nhà cung cấp",
            BieuTuong = "🏢",
            PermissionKey = PermissionKeys.ReportsPurchases
        },
        new BaoCaoMenuItem
        {
            Key = "phieu-nhap-hang",
            TieuDe = "Báo cáo phiếu nhập hàng",
            BieuTuong = "📥",
            PermissionKey = PermissionKeys.ReportsPurchases
        }
    };

    /// <summary>
    /// Chỉ hiện những mục báo cáo mà người dùng có quyền xem.
    /// Gọi mỗi khi phiên đăng nhập thay đổi để menu không hiện mục thừa.
    /// </summary>
    public void CapNhatBaoCaoMenuVisibility()
    {
        foreach (var item in BaoCaoMenuItems)
        {
            item.IsVisible = string.IsNullOrWhiteSpace(item.PermissionKey)
                || CoQuyen(item.PermissionKey);
        }
    }

    public ObservableCollection<PhieuNhapHang> BaoCaoPhieuNhaps { get; } = new();

    [ObservableProperty] private string baoCaoPhieuNhapSearch = "";
    [ObservableProperty] private int baoCaoPhieuNhapPage = 1;
    [ObservableProperty] private int baoCaoPhieuNhapPageSize = 10;
    [ObservableProperty] private int baoCaoPhieuNhapTotalCount;
    [ObservableProperty] private bool isLoadingBaoCaoPhieuNhap;
    [ObservableProperty] private string baoCaoPhieuNhapLocThanhToan = "TAT_CA";

    public bool IsBaoCaoPhieuNhapLocTatCa => BaoCaoPhieuNhapLocThanhToan == "TAT_CA";
    public bool IsBaoCaoPhieuNhapLocDaThanhToan => BaoCaoPhieuNhapLocThanhToan == "DA_THANH_TOAN";
    public bool IsBaoCaoPhieuNhapLocChuaThanhToan => BaoCaoPhieuNhapLocThanhToan == "CHUA_THANH_TOAN";
    public bool IsBaoCaoPhieuNhapLocThanhToanMotPhan => BaoCaoPhieuNhapLocThanhToan == "MOT_PHAN";

    [RelayCommand]
    private async Task LocBaoCaoPhieuNhapThanhToanAsync(string? loai)
    {
        var filter = string.IsNullOrWhiteSpace(loai) ? "TAT_CA" : loai;
        if (BaoCaoPhieuNhapLocThanhToan == filter)
            return;

        BaoCaoPhieuNhapLocThanhToan = filter;
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapLocTatCa));
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapLocDaThanhToan));
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapLocChuaThanhToan));
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapLocThanhToanMotPhan));

        BaoCaoPhieuNhapPage = 1;
        await LoadBaoCaoPhieuNhapAsync();
    }

    public int BaoCaoPhieuNhapTotalPages =>
        BaoCaoPhieuNhapPageSize <= 0
            ? 0
            : (int)Math.Ceiling(BaoCaoPhieuNhapTotalCount / (double)BaoCaoPhieuNhapPageSize);

    /// <summary>Ví dụ: "1 - 15 trong 367 giao dịch"</summary>
    public string BaoCaoPhieuNhapRangeText
    {
        get
        {
            if (BaoCaoPhieuNhapTotalCount == 0)
                return "0 giao dịch";

            var from = (BaoCaoPhieuNhapPage - 1) * BaoCaoPhieuNhapPageSize + 1;
            var to = Math.Min(from + BaoCaoPhieuNhapPageSize - 1, BaoCaoPhieuNhapTotalCount);

            return $"{from} - {to} trong {BaoCaoPhieuNhapTotalCount} giao dịch";
        }
    }

    public bool HasBaoCaoPhieuNhapData => BaoCaoPhieuNhaps.Count > 0;

    public bool IsBaoCaoPhieuNhapEmpty => BaoCaoPhieuNhaps.Count == 0;

    public bool CanBaoCaoPhieuNhapTrangTruoc => BaoCaoPhieuNhapPage > 1;

    public bool CanBaoCaoPhieuNhapTrangSau => BaoCaoPhieuNhapPage < BaoCaoPhieuNhapTotalPages;

    public bool IsBaoCaoSectionSelected => SelectedMenu == "Báo cáo";

    public bool IsBaoCaoPhieuNhapSelected =>
        SelectedMenu == "Báo cáo" && SelectedReportKey == "phieu-nhap-hang";

    public bool IsBaoCaoBanHangSelected =>
        IsBaoCaoSectionSelected && SelectedReportKey == "ban-hang";

    public bool IsBaoCaoHangHoaSelected =>
        IsBaoCaoSectionSelected && SelectedReportKey == "hang-hoa";

    public bool IsBaoCaoKhachHangSelected =>
        IsBaoCaoSectionSelected && SelectedReportKey == "khach-hang";

    public bool IsBaoCaoNhaCungCapSelected =>
        IsBaoCaoSectionSelected && SelectedReportKey == "nha-cung-cap";

    public bool IsBaoCaoCuoiNgaySelected =>
        SelectedMenu == "Báo cáo" && SelectedReportKey == "cuoi-ngay";

    partial void OnBaoCaoPhieuNhapSearchChanged(string value)
    {
        // Mỗi lần tìm kiếm luôn quay về trang đầu
        if (BaoCaoPhieuNhapPage != 1)
            BaoCaoPhieuNhapPage = 1;
    }

    partial void OnBaoCaoPhieuNhapPageChanged(int value)
    {
        OnPropertyChanged(nameof(BaoCaoPhieuNhapTotalPages));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapRangeText));
        OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangTruoc));
        OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangSau));
    }

    partial void OnBaoCaoPhieuNhapPageSizeChanged(int value)
    {
        // Chặn giá trị không hợp lệ (0/âm) để không bao giờ chia cho 0
        // hoặc hiển thị khoảng bản ghi sai.
        if (value <= 0)
        {
            BaoCaoPhieuNhapPageSize = 10;
            return;
        }

        if (BaoCaoPhieuNhapPage != 1)
            BaoCaoPhieuNhapPage = 1;

        // Đổi số dòng mỗi trang thì tải lại ngay danh sách và tính lại tổng số trang.
        _ = LoadBaoCaoPhieuNhapAsync();
    }

    partial void OnBaoCaoPhieuNhapTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(BaoCaoPhieuNhapTotalPages));
        OnPropertyChanged(nameof(BaoCaoPhieuNhapRangeText));
    }

    partial void OnSelectedReportKeyChanged(string value)
    {
        CapNhatTrangThaiChonBaoCao();
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapSelected));
        OnPropertyChanged(nameof(IsBaoCaoBanHangSelected));
        OnPropertyChanged(nameof(IsBaoCaoHangHoaSelected));
        OnPropertyChanged(nameof(IsBaoCaoKhachHangSelected));
        OnPropertyChanged(nameof(IsBaoCaoNhaCungCapSelected));
        OnPropertyChanged(nameof(IsBaoCaoCuoiNgaySelected));
    }

    [RelayCommand]
    private void ChonBaoCao(BaoCaoMenuItem? item)
    {
        if (item is null)
            return;

        var quyenBaoCao = item.Key switch
        {
            "cuoi-ngay" => PermissionKeys.ReportsEndOfDay,
            "ban-hang" => PermissionKeys.ReportsSales,
            "hang-hoa" => PermissionKeys.ReportsProducts,
            "khach-hang" => PermissionKeys.ReportsSales,
            "phieu-nhap-hang" => PermissionKeys.ReportsPurchases,
            "nha-cung-cap" => PermissionKeys.ReportsPurchases,
            _ => PermissionKeys.ReportsView
        };

        if (!CoQuyen(quyenBaoCao))
        {
            StatusMessage = "Bạn không có quyền xem báo cáo này.";
            return;
        }

        // Vì được gọi từ menu sidebar khi màn bán hàng (ZIndex=10, nền đục) đang
        // mở, phải đóng màn bán hàng trước như ChonMenu, không thì nó vẫn phủ
        // lên và che khuất báo cáo vừa chọn.
        IsSalesPanelVisible = false;
        IsBaoCaoExpanded = true;

        SelectedMenu = "Báo cáo";
        SelectedReportKey = item.Key;
        CapNhatTrangThaiChonBaoCao();

        // Mỗi báo cáo có danh sách riêng
        BaoCaoPhieuNhapPage = 1;

        if (item.Key == "phieu-nhap-hang" && !_baoCaoPhieuNhapLoaded)
        {
            _baoCaoPhieuNhapLoaded = true;
            _ = LoadBaoCaoPhieuNhapAsync();
        }

        // Báo cáo bán hàng cũng chỉ tải một lần đầu, các lần sau giữ nguyên
        // bộ lọc ngày người dùng đang xem.
        if (item.Key == "ban-hang" && !_baoCaoBanHangLoaded)
        {
            _baoCaoBanHangLoaded = true;
            _ = BaoCaoBanHang.TaiLaiAsync();
        }

        // Báo cáo hàng hóa cũng chỉ tải một lần đầu, các lần sau giữ nguyên
        // bộ lọc người dùng đang xem.
        if (item.Key == "hang-hoa" && !_baoCaoHangHoaLoaded)
        {
            _baoCaoHangHoaLoaded = true;
            _ = BaoCaoHangHoa.TaiLaiAsync();
        }

        // Báo cáo khách hàng cũng chỉ tải một lần đầu, các lần sau giữ nguyên
        // bộ lọc người dùng đang xem.
        if (item.Key == "khach-hang" && !_baoCaoKhachHangLoaded)
        {
            _baoCaoKhachHangLoaded = true;
            _ = BaoCaoKhachHang.TaiLaiAsync();
        }

        // Báo cáo nhà cung cấp cũng chỉ tải một lần đầu, các lần sau giữ nguyên
        // bộ lọc người dùng đang xem.
        if (item.Key == "nha-cung-cap" && !_baoCaoNhaCungCapLoaded)
        {
            _baoCaoNhaCungCapLoaded = true;
            _ = BaoCaoNhaCungCap.TaiLaiAsync();
        }

        // Báo cáo cuối ngày chỉ tải khi mở lần đầu, sau đó giữ nguyên ngày đang
        // xem trong suốt phiên (người dùng tự bấm "Làm mới" khi cần).
        if (item.Key == "cuoi-ngay" && !_baoCaoCuoiNgayLoaded)
        {
            _baoCaoCuoiNgayLoaded = true;
            _ = BaoCaoCuoiNgay.TaiDuLieuAsync();
        }

        StatusMessage = item.TieuDe;
    }

    [RelayCommand]
    private async Task LoadBaoCaoPhieuNhapAsync()
    {
        if (IsLoadingBaoCaoPhieuNhap)
            return;

        IsLoadingBaoCaoPhieuNhap = true;

        // Version guard: bỏ qua kết quả của lần tải cũ nếu người dùng đã tìm/chuyển trang mới
        var version = ++_baoCaoPhieuNhapLoadVersion;

        try
        {
            var page = BaoCaoPhieuNhapPage;
            var (items, tongSoBanGhi) = await _db.GetPhieuNhapHangPagedAsync(
                BaoCaoPhieuNhapSearch,
                page,
                BaoCaoPhieuNhapPageSize,
                BaoCaoPhieuNhapLocThanhToan);

            if (version != _baoCaoPhieuNhapLoadVersion)
                return;

            // Trang vượt quá số trang thực tế (ví dụ sau khi tìm kiếm) -> quay về trang cuối
            var totalPages = BaoCaoPhieuNhapPageSize <= 0
                ? 0
                : (int)Math.Ceiling(tongSoBanGhi / (double)BaoCaoPhieuNhapPageSize);

            if (items.Count == 0 && totalPages > 0 && page > totalPages)
            {
                BaoCaoPhieuNhapPage = totalPages;
                IsLoadingBaoCaoPhieuNhap = false;
                await LoadBaoCaoPhieuNhapAsync();
                return;
            }

            BaoCaoPhieuNhaps.Clear();

            foreach (var item in items)
                BaoCaoPhieuNhaps.Add(item);

            BaoCaoPhieuNhapTotalCount = tongSoBanGhi;

            OnPropertyChanged(nameof(HasBaoCaoPhieuNhapData));
            OnPropertyChanged(nameof(IsBaoCaoPhieuNhapEmpty));
            OnPropertyChanged(nameof(BaoCaoPhieuNhapRangeText));
            OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangTruoc));
            OnPropertyChanged(nameof(CanBaoCaoPhieuNhapTrangSau));

            StatusMessage = BaoCaoPhieuNhaps.Count == 0
                ? "Không có dữ liệu phiếu nhập phù hợp"
                : $"Đã tải {BaoCaoPhieuNhaps.Count} phiếu nhập (trang {BaoCaoPhieuNhapPage}/{Math.Max(1, totalPages)})";
        }
        catch (Exception ex)
        {
            if (version == _baoCaoPhieuNhapLoadVersion)
                StatusMessage = "Không thể tải báo cáo phiếu nhập: " + ex.Message;
        }
        finally
        {
            if (version == _baoCaoPhieuNhapLoadVersion)
                IsLoadingBaoCaoPhieuNhap = false;
        }
    }

    [RelayCommand]
    private async Task TimKiemBaoCaoPhieuNhapAsync()
    {
        BaoCaoPhieuNhapPage = 1;
        await LoadBaoCaoPhieuNhapAsync();
    }

    /// <summary>
    /// Xuất Excel toàn bộ phiếu nhập khớp từ khóa tìm kiếm hiện tại (không phụ
    /// thuộc trang đang xem): truy vấn lại với cỡ trang bằng tổng số bản ghi.
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelBaoCaoPhieuNhapAsync()
    {
        try
        {
            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            var soDong = BaoCaoPhieuNhapTotalCount < 1 ? 1 : BaoCaoPhieuNhapTotalCount;

            var (items, _) = await _db.GetPhieuNhapHangPagedAsync(
                BaoCaoPhieuNhapSearch,
                1,
                soDong,
                BaoCaoPhieuNhapLocThanhToan);

            using var wb = BaoCaoPhieuNhapExcel.TaoWorkbook(items, DateTime.Now);

            var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Xuất Excel báo cáo phiếu nhập hàng",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                },
                SuggestedFileName = $"BaoCaoPhieuNhap_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            });

            if (file is not null)
            {
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Không xuất được file Excel: " + ex.Message;
        }
    }

    /// <summary>
    /// Tìm kiếm khi người dùng gõ: chống debounce để không gọi database liên tục.
    /// </summary>
    public void TimKiemBaoCaoPhieuNhapTuDong()
    {
        _baoCaoPhieuNhapSearchCts?.Cancel();
        _baoCaoPhieuNhapSearchCts?.Dispose();
        _baoCaoPhieuNhapSearchCts = new CancellationTokenSource();
        var token = _baoCaoPhieuNhapSearchCts.Token;

        _ = DebounceBaoCaoPhieuNhapAsync(token);
    }

    private async Task DebounceBaoCaoPhieuNhapAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);

            if (token.IsCancellationRequested)
                return;

            await TimKiemBaoCaoPhieuNhapAsync();
        }
        catch (OperationCanceledException)
        {
            // Người dùng gõ tiếp -> huỷ lần tìm kiếm cũ
        }
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangDauAsync()
    {
        BaoCaoPhieuNhapPage = 1;
        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangCuoiAsync()
    {
        BaoCaoPhieuNhapPage = Math.Max(1, BaoCaoPhieuNhapTotalPages);
        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangTruocAsync()
    {
        if (BaoCaoPhieuNhapPage > 1)
            BaoCaoPhieuNhapPage--;

        await LoadBaoCaoPhieuNhapAsync();
    }

    [RelayCommand]
    private async Task BaoCaoPhieuNhapTrangSauAsync()
    {
        if (BaoCaoPhieuNhapPage < BaoCaoPhieuNhapTotalPages)
            BaoCaoPhieuNhapPage++;

        await LoadBaoCaoPhieuNhapAsync();
    }

    /// <summary>
    /// Mở cửa sổ xem chi tiết một phiếu nhập trong báo cáo (chỉ đọc).
    /// Truy vấn lại đầy đủ header + dòng hàng theo khoá chính phieu_nhap_hang.id,
    /// không sửa/xóa. Nếu phiếu không còn trong database thì báo rõ cho người dùng.
    /// </summary>
    [RelayCommand]
    private async Task XemChiTietPhieuNhapAsync(PhieuNhapHang? item)
    {
        if (item is null || OwnerWindow is null)
            return;

        var maPhieu = item.MaPhieuNhap;

        try
        {
            var detail = await _db.GetPhieuNhapHangDetailAsync(item.Id);

            if (detail is null)
            {
                StatusMessage = string.IsNullOrWhiteSpace(maPhieu)
                    ? "Không tìm thấy phiếu nhập trong database."
                    : $"Không tìm thấy phiếu nhập {maPhieu} trong database.";
                return;
            }

            var window = new PhieuNhapDetailWindow(detail, ThanhToanTuChiTietPhieuNhapAsync);
            await window.ShowDialog(OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = string.IsNullOrWhiteSpace(maPhieu)
                ? $"Không mở được chi tiết phiếu nhập: {ex.Message}"
                : $"Không mở được chi tiết phiếu nhập {maPhieu}: {ex.Message}";

            SafeLog.WriteError("Loi xem chi tiet phieu nhap", ex);
        }
    }

    /// <summary>
    /// In lại một phiếu nhập đã lưu: đọc đầy đủ chi tiết + thông tin tiệm, dựng
    /// file HTML tạm rồi mở bằng trình duyệt mặc định để người dùng Ctrl+P.
    /// Chỉ đọc dữ liệu, không sửa/xóa phiếu.
    /// </summary>
    [RelayCommand]
    private async Task InPhieuNhapAsync(PhieuNhapHang? item)
    {
        if (item is null || item.Id == 0)
            return;

        var maPhieu = item.MaPhieuNhap;

        try
        {
            var detail = await _db.GetPhieuNhapHangDetailAsync(item.Id);

            if (detail is null)
            {
                StatusMessage = string.IsNullOrWhiteSpace(maPhieu)
                    ? "Không tìm thấy phiếu nhập trong database."
                    : $"Không tìm thấy phiếu nhập {maPhieu} trong database.";
                return;
            }

            var tiem = await LayThongTinTiemChoHoaDonAsync();
            var duongDan = PhieuNhapPrintService.LuuFileTam(detail, tiem);

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            StatusMessage = string.IsNullOrWhiteSpace(maPhieu)
                ? "Đã mở bản in phiếu nhập."
                : $"Đã mở bản in phiếu nhập {maPhieu}.";
        }
        catch (Exception ex)
        {
            StatusMessage = string.IsNullOrWhiteSpace(maPhieu)
                ? $"Không in được phiếu nhập: {ex.Message}"
                : $"Không in được phiếu nhập {maPhieu}: {ex.Message}";

            SafeLog.WriteError("Loi in phieu nhap", ex);
        }
    }

    [RelayCommand]
    private async Task MoGiaoDienNhapHangTuBaoCaoAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesView, "Bạn không có quyền mở màn hình nhập hàng."))
        {
            return;
        }
        // Nút "+ Nhập hàng" trong báo cáo: chuyển sang màn lập phiếu nhập hàng
        await MoGiaoDienNhapHangAsync();
    }

    // ============================================================
    // PHIẾU NHẬP HÀNG
    // ============================================================

    public ObservableCollection<ChiTietPhieuNhapHang> PhieuNhapItems { get; } = new();
    public ObservableCollection<SanPham> PhieuNhapProductSuggestions { get; } = new();
    public ObservableCollection<NhaCungCap> PhieuNhapSupplierSuggestions { get; } = new();
    public ObservableCollection<ProductOption> PhieuNhapDonViTinhOptions { get; } = new();

    [ObservableProperty] private bool isNhapHangFormVisible;
    [ObservableProperty] private string importProductSearch = "";
    [ObservableProperty] private string importSupplierSearch = "";
    [ObservableProperty] private string phieuNhapMa = "";
    [ObservableProperty] private string phieuNhapSoHoaDonDauVao = "";
    [ObservableProperty] private string phieuNhapGhiChu = "";
    [ObservableProperty] private string phieuNhapNhanVien = "";
    [ObservableProperty] private NhaCungCap? selectedPhieuNhapNcc;
    [ObservableProperty] private DateTime? phieuNhapNgayNhap = DateTime.Now;
    [ObservableProperty] private decimal phieuNhapTongTienHang;
    [ObservableProperty] private decimal phieuNhapChiPhiKhac;
    [ObservableProperty] private decimal phieuNhapTongTienNhap;
    [ObservableProperty] private decimal phieuNhapDaThanhToan;
    [ObservableProperty] private string phieuNhapPhuongThuc = "Tiền mặt";
    public IReadOnlyList<string> PhieuNhapPhuongThucOptions { get; } = ["Tiền mặt", "Chuyển khoản"];

    [ObservableProperty] private bool isSavingPhieuNhap;

    /// <summary>Ngày nhập luôn dùng kiểu DateTime vì cột thoi_gian_nhap trong database là DATETIME.</summary>
    [ObservableProperty] private bool isPhieuNhapCalendarOpen;

    private uint _phieuNhapId;

    public string PhieuNhapTongTienHangText => PhieuNhapTongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string PhieuNhapTongTienNhapText => PhieuNhapTongTienNhap.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Ngày nhập hiển thị theo định dạng Việt Nam dd/MM/yyyy HH:mm.</summary>
    public string PhieuNhapNgayNhapText => PhieuNhapNgayNhap?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                                           ?? string.Empty;

    /// <summary>Công nợ còn phải trả cho nhà cung cấp, khớp với cột con_no trong database.</summary>
    public decimal PhieuNhapConNo => Math.Max(0, PhieuNhapTongTienNhap - PhieuNhapDaThanhToan);

    public string PhieuNhapConNoText => PhieuNhapConNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Hiển thị giải thích nguồn quỹ chi trả tương ứng với số tiền và phương thức chọn.</summary>
    public string PhieuNhapNguonQuyText
    {
        get
        {
            if (PhieuNhapDaThanhToan <= 0)
                return "Chưa thanh toán (toàn bộ tính vào công nợ nhà cung cấp)";

            return PhieuNhapPhuongThuc == "Chuyển khoản"
                ? "💡 Chi từ Tài khoản ngân hàng (tự động ghi sổ quỹ)"
                : "💡 Chi từ Quỹ tiền mặt (tự động ghi sổ quỹ)";
        }
    }

    public bool HasPhieuNhapItems => PhieuNhapItems.Count > 0;

    public bool IsPhieuNhapEmpty => PhieuNhapItems.Count == 0;

    public bool HasPhieuNhapSuggestions => PhieuNhapProductSuggestions.Count > 0;

    public bool HasPhieuNhapSupplierSuggestions => PhieuNhapSupplierSuggestions.Count > 0;

    public bool IsNhapHangListVisible => !IsNhapHangFormVisible;

    public bool CanSavePhieuNhap => !IsSavingPhieuNhap;

    partial void OnIsNhapHangFormVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(IsNhapHangListVisible));

    partial void OnPhieuNhapTongTienHangChanged(decimal value)
    {
        RecalculatePhieuNhapTotal();

        OnPropertyChanged(nameof(PhieuNhapTongTienHangText));
        OnPropertyChanged(nameof(PhieuNhapTongTienNhapText));
    }

    partial void OnPhieuNhapTongTienNhapChanged(decimal value)
    {
        OnPropertyChanged(nameof(PhieuNhapTongTienNhapText));
        OnPropertyChanged(nameof(PhieuNhapConNoText));
    }

    partial void OnPhieuNhapChiPhiKhacChanged(decimal value) => RecalculatePhieuNhapTotal();

    partial void OnPhieuNhapDaThanhToanChanged(decimal value)
    {
        OnPropertyChanged(nameof(PhieuNhapConNoText));
        OnPropertyChanged(nameof(PhieuNhapNguonQuyText));
    }

    partial void OnPhieuNhapPhuongThucChanged(string value) =>
        OnPropertyChanged(nameof(PhieuNhapNguonQuyText));

    partial void OnIsSavingPhieuNhapChanged(bool value) => OnPropertyChanged(nameof(CanSavePhieuNhap));

    partial void OnPhieuNhapNgayNhapChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(PhieuNhapNgayNhapText));

        // Chọn xong ngày thì đóng lịch luôn
        IsPhieuNhapCalendarOpen = false;
    }

    [RelayCommand]
    private void MoLichPhieuNhap() => IsPhieuNhapCalendarOpen = !IsPhieuNhapCalendarOpen;

    private void RecalculatePhieuNhapTotal()
    {
        PhieuNhapTongTienNhap = PhieuNhapTongTienHang + PhieuNhapChiPhiKhac;
    }

    private async Task LoadPhieuNhapDonViTinhOptionsAsync()
    {
        try
        {
            var units = await _db.GetDonViTinhAsync();
            PhieuNhapDonViTinhOptions.Clear();
            foreach (var unit in units)
                PhieuNhapDonViTinhOptions.Add(unit);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tai danh sach don vi tinh cho phieu nhap", ex);
        }
    }

    [RelayCommand]
    private async Task ThemDonViTinhPhieuNhapAsync(ChiTietPhieuNhapHang? item)
    {
        if (OwnerWindow is null)
            return;

        try
        {
            var tenDvt = await QuickOptionEditorWindow.ShowAsync(
                OwnerWindow,
                "Thêm đơn vị tính mới",
                "Tên đơn vị tính *",
                "Ví dụ: Bó, Cành, Chậu, Cây, Giỏ, Lẵng, Bình...");

            if (string.IsNullOrWhiteSpace(tenDvt))
                return;

            var newOption = await _db.ThemDonViTinhAsync(tenDvt);

            var existing = PhieuNhapDonViTinhOptions.FirstOrDefault(x => x.Id == newOption.Id);
            if (existing is null)
            {
                var insertIndex = 0;
                while (insertIndex < PhieuNhapDonViTinhOptions.Count &&
                       string.Compare(
                           PhieuNhapDonViTinhOptions[insertIndex].Name,
                           newOption.Name,
                           StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    insertIndex++;
                }

                PhieuNhapDonViTinhOptions.Insert(insertIndex, newOption);
                existing = newOption;
            }

            if (item is not null)
                item.SelectedDonViTinh = existing;

            StatusMessage = $"Đã thêm và chọn đơn vị tính \"{existing.Name}\"";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm đơn vị tính: " + ex.Message;
            await NotificationWindow.ShowErrorAsync(
                "Không thể thêm đơn vị tính: " + ex.Message,
                owner: OwnerWindow);
        }
    }

    [RelayCommand]
    private async Task MoPhieuNhapHangAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Bạn không có quyền tạo phiếu nhập hàng."))
        {
            return;
        }
        IsNhapHangFormVisible = true;

        _phieuNhapId = 0;

        BoDangKyPhieuNhapItems();

        PhieuNhapItems.Clear();
        PhieuNhapProductSuggestions.Clear();
        PhieuNhapSupplierSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));
        PhieuNhapSoHoaDonDauVao = "";
        PhieuNhapGhiChu = "";
        PhieuNhapChiPhiKhac = 0;
        PhieuNhapDaThanhToan = 0;
        PhieuNhapPhuongThuc = "Tiền mặt";
        ImportProductSearch = "";
        ImportSupplierSearch = "";
        SelectedPhieuNhapNcc = null;

        PhieuNhapNhanVien = Session.CurrentUser?.HoTen ?? "Chưa đăng nhập";
        PhieuNhapNgayNhap = DateTime.Now;

        try
        {
            PhieuNhapMa = await _db.TaoMaPhieuNhapMoiAsync();
        }
        catch (Exception ex)
        {
            // Không nuốt lỗi: in ra console/debug để thấy đúng lỗi SQL/connection.
            // Tuyệt đối không gán chuỗi tạm vào PhieuNhapMa vì màn hình sẽ lưu
            // thẳng giá trị này xuống phieu_nhap_hang (làm hỏng dữ liệu mã phiếu).
            Console.WriteLine("========== LỖI TẠO MÃ PHIẾU NHẬP ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("==========================================");

            PhieuNhapMa = "";
            StatusMessage =
                "Không thể tạo mã phiếu nhập tự động: " + ex.Message
                + " (xem console để biết chi tiết lỗi SQL/kết nối)";
        }

        RecalculatePhieuNhapTongTienHang();

        await LoadPhieuNhapDonViTinhOptionsAsync();
        await LoadPhieuNhapProductSuggestionsAsync();
    }

    [RelayCommand]
    private async Task MoGiaoDienNhapHangAsync()
    {
        // Vào đây từ 2 nguồn: nút "＋ Nhập hàng" trên màn Hàng hóa (hiện theo
        // CanCreatePurchase) và nút "＋ Nhập hàng" trong Báo cáo phiếu nhập (hiện
        // theo CanViewPurchases). Cho phép khi có quyền xem HOẶC tạo phiếu nhập.
        if (!CanViewPurchases && !CanCreatePurchase)
        {
            await ShowMessageAsync("Thông báo", "Bạn không có quyền mở màn hình nhập hàng.");
            return;
        }
        // Nếu đang đứng ở màn Nhập hàng thì SelectedMenu không đổi,
        // LoadSelectedSectionAsync sẽ không chạy nên phải tự khởi tạo phiếu mới.
        if (SelectedMenu == "Nhập hàng")
        {
            await MoPhieuNhapHangAsync();
            return;
        }

        // SelectedMenu đổi => case "Nhập hàng" trong LoadSelectedSectionAsync gọi
        // MoPhieuNhapHangAsync() giúp, không gọi ở đây để tránh chạy 2 lần.
        SelectedMenu = "Nhập hàng";
    }

    private void BoDangKyPhieuNhapItems()
    {
        foreach (var item in PhieuNhapItems)
            item.PropertyChanged -= ChiTietPhieuNhapHang_OnPropertyChanged;
    }

    [RelayCommand]
    private async Task TimKiemSanPhamNhapAsync(string? keyword)
    {
        // Không gán đè khi lệnh được gọi không có tham số,
        // nếu không TextBox sẽ bị xoá ngay khi vừa gõ ký tự đầu tiên.
        if (keyword is not null && keyword != ImportProductSearch)
            ImportProductSearch = keyword;

        try
        {
            await LoadPhieuNhapProductSuggestionsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm hàng hóa: " + ex.Message;
        }
    }

    private async Task LoadPhieuNhapProductSuggestionsAsync()
    {
        var keyword = ImportProductSearch.Trim();

        // Rút gọn danh sách gợi ý: sản phẩm đã có trong phiếu thì ẩn đi
        var sanPhamDaChon = PhieuNhapItems
            .Select(item => item.SanPham.Id)
            .ToHashSet();

        var products = await _db.GetSanPhamAsync(keyword);

        PhieuNhapProductSuggestions.Clear();

        foreach (var product in products
                     .Where(product => !sanPhamDaChon.Contains(product.Id))
                     .Take(20))
        {
            PhieuNhapProductSuggestions.Add(product);
        }

        OnPropertyChanged(nameof(HasPhieuNhapSuggestions));
    }

    [RelayCommand]
    private void ThemSanPhamVaoPhieuNhap(SanPham? product)
    {
        if (product is null)
            return;

        var existing = PhieuNhapItems
            .FirstOrDefault(item => item.SanPham.Id == product.Id);

        if (existing is not null)
        {
            existing.SoLuong += 1;
            RecalculatePhieuNhapTongTienHang();
            StatusMessage = $"Đã tăng số lượng {product.TenSanPham} lên {existing.SoLuong:N0}";
            return;
        }

        var dongMoi = new ChiTietPhieuNhapHang(product, PhieuNhapDonViTinhOptions);

        // Nghe thay đổi số lượng / giá nhập / giảm giá để tính lại tổng realtime
        dongMoi.PropertyChanged += ChiTietPhieuNhapHang_OnPropertyChanged;

        PhieuNhapItems.Add(dongMoi);

        PhieuNhapProductSuggestions.Remove(product);

        OnPropertyChanged(nameof(HasPhieuNhapSuggestions));

        RecalculatePhieuNhapTongTienHang();

        StatusMessage = $"Đã thêm {product.TenSanPham} vào phiếu nhập";
    }

    private void ChiTietPhieuNhapHang_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChiTietPhieuNhapHang.SoLuong)
            or nameof(ChiTietPhieuNhapHang.DonGiaNhap)
            or nameof(ChiTietPhieuNhapHang.GiamGia)
            or nameof(ChiTietPhieuNhapHang.ThanhTien))
        {
            RecalculatePhieuNhapTongTienHang();
        }
    }

    [RelayCommand]
    private void XoaDongPhieuNhap(ChiTietPhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.PropertyChanged -= ChiTietPhieuNhapHang_OnPropertyChanged;

        PhieuNhapItems.Remove(item);

        RecalculatePhieuNhapTongTienHang();
    }

    [RelayCommand]
    private async Task XoaTatPhieuNhapAsync()
    {
        if (PhieuNhapItems.Count == 0)
        {
            StatusMessage = "Phiếu nhập không có dòng hàng nào để xóa.";
            return;
        }

        var soDong = PhieuNhapItems.Count;

        var confirmed = await ShowConfirmationAsync(
            "Xóa tất cả dòng hàng",
            $"Bạn có chắc muốn xóa tất cả hàng hóa trong phiếu nhập? ({soDong} dòng sẽ bị xóa khỏi phiếu, sản phẩm trong database không bị ảnh hưởng)",
            "Xóa tất cả",
            "Hủy");

        if (!confirmed)
        {
            StatusMessage = "Đã hủy xóa tất cả dòng hàng.";
            return;
        }

        BoDangKyPhieuNhapItems();
        PhieuNhapItems.Clear();

        // Tổng tiền hàng = 0, Tổng tiền nhập = Chi phí khác, Còn phải trả = Tổng tiền nhập - Đã thanh toán
        RecalculatePhieuNhapTongTienHang();
        RecalculatePhieuNhapTotal();

        StatusMessage = $"Đã xóa toàn bộ {soDong} dòng hàng khỏi phiếu nhập";
    }

    [RelayCommand]
    private void TangSoLuongNhap(ChiTietPhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.SoLuong += 1;
        RecalculatePhieuNhapTongTienHang();
    }

    [RelayCommand]
    private void GiamSoLuongNhap(ChiTietPhieuNhapHang? item)
    {
        if (item is null)
            return;

        item.SoLuong = Math.Max(0, item.SoLuong - 1);
        RecalculatePhieuNhapTongTienHang();
    }

    private void RecalculatePhieuNhapTongTienHang()
    {
        PhieuNhapTongTienHang = PhieuNhapItems.Sum(item => item.ThanhTien);

        for (var i = 0; i < PhieuNhapItems.Count; i++)
            PhieuNhapItems[i].Index = i + 1;

        OnPropertyChanged(nameof(HasPhieuNhapItems));
        OnPropertyChanged(nameof(IsPhieuNhapEmpty));
    }

    /// <summary>
    /// Ô tìm nhà cung cấp đang chứa đúng tên NCC vừa chọn
    /// thì không cần tìm lại lần nữa.
    /// </summary>
    private bool DaChonNhaCungCapNhap =>
        SelectedPhieuNhapNcc is not null &&
        ImportSupplierSearch.Trim()
            .Equals(SelectedPhieuNhapNcc.TenNhaCungCap, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task TimKiemNhaCungCapNhapAsync(string? keyword)
    {
        // Không gán đè khi lệnh được gọi không có tham số,
        // nếu không TextBox sẽ bị xoá ngay khi vừa gõ ký tự đầu tiên.
        if (keyword is not null && keyword != ImportSupplierSearch)
            ImportSupplierSearch = keyword;

        try
        {
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            if (string.IsNullOrWhiteSpace(ImportSupplierSearch))
                return;

            // Đã chọn xong NCC thì không tìm lại, tránh mở lại danh sách gợi ý
            if (DaChonNhaCungCapNhap)
                return;

            var suppliers = await _db.GetNhaCungCapAsync(ImportSupplierSearch);

            foreach (var supplier in suppliers.Take(20))
                PhieuNhapSupplierSuggestions.Add(supplier);
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tìm nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ChonNhaCungCapNhap(NhaCungCap? supplier)
    {
        if (supplier is null)
            return;

        SelectedPhieuNhapNcc = supplier;
        ImportSupplierSearch = supplier.TenNhaCungCap;

        PhieuNhapSupplierSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

        StatusMessage = $"Đã chọn nhà cung cấp {supplier.TenNhaCungCap}";
    }

    [RelayCommand]
    private async Task ThemNhaCungCapNhapNhanhAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SuppliersCreate, "Bạn không có quyền thêm nhà cung cấp."))
        {
            return;
        }
        try
        {
            // Tái sử dụng đúng form thêm nhà cung cấp sẵn có
            var supplier = await HienThiFormThemNhaCungCapAsync();

            if (supplier is null)
                return;

            // Tự động chọn nhà cung cấp vừa thêm cho phiếu nhập
            SelectedPhieuNhapNcc = supplier;
            ImportSupplierSearch = supplier.TenNhaCungCap;
            PhieuNhapSupplierSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuNhapSupplierSuggestions));

            StatusMessage = $"Đã thêm và chọn nhà cung cấp {supplier.TenNhaCungCap}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể thêm nhà cung cấp: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task HoanThanhPhieuNhapAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Bạn không có quyền lưu phiếu nhập hàng."))
        {
            return;
        }
        if (IsSavingPhieuNhap)
            return;

        // ------------------------------------------
        // VALIDATE
        // Mọi lỗi nghiệp vụ đều báo qua hộp thoại và GIỮ NGUYÊN form,
        // không bao giờ xóa dữ liệu đang nhập.
        // Mọi lỗi nghiệp vụ đều báo qua hộp thoại và GIỮ NGUYÊN form,
        // không bao giờ xóa dữ liệu đang nhập.
        // ------------------------------------------

        // 1. Phiếu phải có ít nhất 1 hàng hóa
        if (PhieuNhapItems.Count == 0)
        {
            await BaoLoiLuuPhieuNhapAsync("Phiếu nhập chưa có hàng hóa.");
            return;
        }

        // 2. Số lượng mỗi hàng phải > 0
        var dongSoLuongSai = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.SoLuong <= 0)
            .ToList();

        if (dongSoLuongSai.Count > 0)
        {
            await BaoLoiLuuPhieuNhapAsync(dongSoLuongSai.Count == 1
                ? $"Vui lòng kiểm tra số lượng của hàng \"{dongSoLuongSai[0].SanPham.TenSanPham}\" (phải lớn hơn 0)."
                : $"Vui lòng kiểm tra số lượng hàng hóa: còn {dongSoLuongSai.Count} dòng chưa nhập số lượng.");
            return;
        }

        // 3. Giá nhập phải hợp lệ (không âm)
        var dongGiaSai = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.DonGiaNhap < 0)
            .ToList();

        if (dongGiaSai.Count > 0)
        {
            await BaoLoiLuuPhieuNhapAsync(dongGiaSai.Count == 1
                ? $"Vui lòng kiểm tra giá nhập của hàng \"{dongGiaSai[0].SanPham.TenSanPham}\"."
                : $"Vui lòng kiểm tra giá nhập: còn {dongGiaSai.Count} dòng có giá nhập không hợp lệ.");
            return;
        }

        // Chỉ lấy những dòng thực sự gắn với một sản phẩm trong database
        var dongHopLe = PhieuNhapItems
            .Where(item => item.SanPham.Id != 0 && item.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
        {
            await BaoLoiLuuPhieuNhapAsync("Phiếu nhập chưa có hàng hóa hợp lệ nào để hoàn thành.");
            return;
        }

        // 4. Nhà cung cấp là trường bắt buộc khi hoàn thành phiếu
        if (SelectedPhieuNhapNcc is null || SelectedPhieuNhapNcc.Id == 0)
        {
            await BaoLoiLuuPhieuNhapAsync("Vui lòng chọn nhà cung cấp.");
            return;
        }

        // 5. Ngày nhập hợp lệ
        if (PhieuNhapNgayNhap is null)
        {
            await BaoLoiLuuPhieuNhapAsync("Vui lòng chọn ngày nhập.");
            return;
        }

        // 6. Mã phiếu phải sinh được trước khi lưu
        if (string.IsNullOrWhiteSpace(PhieuNhapMa))
        {
            await BaoLoiLuuPhieuNhapAsync("Chưa tạo được mã phiếu nhập, vui lòng mở lại màn hình và thử lại.");
            return;
        }

        var soHangCapNhatDvt = dongHopLe.Count(item => item.DonViTinhIdMoi is > 0);
        var thongBaoDvtConfirm = soHangCapNhatDvt > 0
            ? $"\nĐồng thời cập nhật đơn vị tính cho {soHangCapNhatDvt} mặt hàng vào danh sách hàng hóa."
            : string.Empty;

        var confirmed = await ShowConfirmationAsync(
            "Hoàn thành phiếu nhập",
            $"Tồn kho sẽ được cộng thêm số lượng vừa nhập và giá vốn được cập nhật.{thongBaoDvtConfirm}\nBạn có chắc muốn tiếp tục?",
            "Hoàn thành",
            "Hủy");

        if (!confirmed)
            return;

        IsSavingPhieuNhap = true;

        var maPhieuVuaLuu = PhieuNhapMa;
        var tongTienVuaLuu = PhieuNhapTongTienNhapText;

        try
        {
            var phieu = new PhieuNhapHang
            {
                Id = _phieuNhapId,
                MaPhieuNhap = PhieuNhapMa,
                NhaCungCapId = SelectedPhieuNhapNcc.Id,
                TenNhaCungCap = SelectedPhieuNhapNcc.TenNhaCungCap,
                ThoiGianNhap = PhieuNhapNgayNhap.Value,
                ChiPhiKhac = PhieuNhapChiPhiKhac,
                DaThanhToan = PhieuNhapDaThanhToan,
                PhuongThucThanhToan = PhieuNhapPhuongThuc == "Chuyển khoản" ? "CHUYEN_KHOAN" : "TIEN_MAT",
                GhiChu = PhieuNhapGhiChu,
                SoHoaDonDauVao = PhieuNhapSoHoaDonDauVao
            };

            // Lưu header + chi tiết + tồn kho + giá vốn + ĐVT + công nợ trong MỘT transaction.
            // Service tự rollback toàn bộ nếu bất kỳ bước nào lỗi.
            _phieuNhapId = await _db.LuuPhieuNhapHangAsync(phieu, dongHopLe);
        }
        catch (Exception ex)
        {
            // Lưu thất bại: KHÔNG reset, KHÔNG chuyển màn hình, giữ nguyên
            // toàn bộ dữ liệu đang nhập để người dùng sửa rồi bấm lại.
            SafeLog.WriteError("Loi luu phieu nhap hang", ex);

            await BaoLoiLuuPhieuNhapAsync(ex.Message);
            return;
        }
        finally
        {
            IsSavingPhieuNhap = false;
        }

        // ---- Đã lưu thành công ----

        // Phiếu vừa lưu phải xuất hiện trong "Báo cáo → Báo cáo phiếu nhập hàng"
        // và "Báo cáo → Báo cáo nhà cung cấp" nên đánh dấu cần tải lại thay vì giữ cache cũ.
        _baoCaoPhieuNhapLoaded = false;
        _baoCaoNhaCungCapLoaded = false;

        // Dashboard thống kê phiếu nhập trong hoạt động gần đây và thống kê kho.
        _ = LamMoiTongQuanNeuDangMoAsync();

        // Tồn kho, giá vốn và đơn vị tính đã đổi -> nạp lại danh sách sản phẩm.
        // Bọc try/catch riêng: nạp lại thất bại không được làm hỏng kết quả
        // đã commit (phiếu vẫn lưu thành công).
        try
        {
            await RefreshProductsAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tai lai danh sach san pham sau khi nhap hang", ex);
        }

        // Tải lại dữ liệu nhà cung cấp (công nợ, tổng mua, thanh toán)
        try
        {
            await LoadSupplierPageAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tai lai danh sach nha cung cap sau khi nhap hang", ex);
        }

        // Tải lại sổ quỹ nếu màn sổ quỹ đang mở
        try
        {
            if (IsSoQuySelected)
            {
                await TaiLaiSoQuyAsync();
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi lam moi so quy sau khi nhap hang", ex);
        }

        var ghiChuDvt = soHangCapNhatDvt > 0
            ? $"\nĐã cập nhật ĐVT cho {soHangCapNhatDvt} mặt hàng vào danh sách hàng hóa."
            : string.Empty;

        // Báo thành công và CHỜ người dùng đóng hộp thoại.
        StatusMessage = soHangCapNhatDvt > 0
            ? $"✅ Phiếu nhập {maPhieuVuaLuu} đã được lưu ({tongTienVuaLuu} đ) • Đã cập nhật ĐVT cho {soHangCapNhatDvt} mặt hàng."
            : $"✅ Phiếu nhập {maPhieuVuaLuu} đã được lưu - tổng {tongTienVuaLuu} đ.";

        await ShowMessageAsync(
            "Thành công – Phiếu nhập đã được lưu.",
            $"Mã phiếu: {maPhieuVuaLuu}\nTổng tiền nhập: {tongTienVuaLuu} đ{ghiChuDvt}");

        // Chỉ reset SAU khi hộp thoại thành công đã đóng, và vẫn giữ nguyên
        // màn hình "Lập phiếu nhập hàng" (IsNhapHangFormVisible vẫn = true).
        // MoPhieuNhapHangAsync() xóa hàng hóa, nhà cung cấp, số hóa đơn đầu vào,
        // ghi chú, chi phí khác, đã thanh toán, ngày nhập về hôm nay,
        // tính lại tổng tiền và SINH MÃ PHIẾU MỚI từ database.
        await MoPhieuNhapHangAsync();

        // MoPhieuNhapHangAsync() để PhieuNhapMa = "" nếu sinh mã thất bại
        // (lỗi SQL/kết nối). Không được báo "sẵn sàng" khi mã còn rỗng.
        StatusMessage = string.IsNullOrWhiteSpace(PhieuNhapMa)
            ? $"✅ Đã lưu phiếu nhập {maPhieuVuaLuu}. Không tạo được mã phiếu mới, vui lòng mở lại màn hình."
            : $"✅ Đã lưu phiếu nhập {maPhieuVuaLuu}. Sẵn sàng lập phiếu mới: {PhieuNhapMa}";
    }

    partial void OnTienKhachDuaChanged(decimal value)
    {
        RecalculatePayment();
    }

    /// <summary>
    /// Tổng tiền thay đổi thì tiền thối / công nợ / trạng thái phải tính lại.
    /// Bản cũ chỉ gắn hook cho TienKhachDua nên khi tổng tiền đổi (thêm hàng,
    /// đổi số lượng, giảm giá) các ô này giữ số cũ — và sau khi thanh toán
    /// xong chúng cũng không tự về 0.
    /// </summary>
    partial void OnSaleTotalChanged(decimal value)
    {
        RecalculatePayment();
    }

    private void RecalculatePayment()
    {
        var canTra = Math.Max(0, SaleTotal);
        var khachDua = Math.Max(0, TienKhachDua);

        if (canTra <= 0)
        {
            TienThoi = 0;
            TienCongNo = 0;
            TrangThaiThanhToan = "";
            return;
        }

        if (khachDua > canTra)
        {
            TienThoi = khachDua - canTra;
            TienCongNo = 0;
            TrangThaiThanhToan = "Đã thanh toán đủ - Tiền thối";
        }
        else if (khachDua < canTra)
        {
            TienThoi = 0;
            TienCongNo = canTra - khachDua;

            if (LaKhachLe)
                TrangThaiThanhToan = "Khách lẻ không được ghi nợ - cần thanh toán đủ";
            else if (khachDua == 0)
                TrangThaiThanhToan = "Chưa thanh toán - Công nợ";
            else
                TrangThaiThanhToan = "Thanh toán thiếu - Công nợ";
        }
        else
        {
            TienThoi = 0;
            TienCongNo = 0;
            TrangThaiThanhToan = "Đã thanh toán đủ";
        }

        if (TienCongNo > 0)
        {
            SaleInKemSdtDiaChi = true;
        }
    }




    partial void OnSaleDiscountInputChanged(decimal value)
    {
        if (SaleDiscountMode == "Số tiền")
        {
            var maxDiscount = Math.Max(0, SaleSubtotal);

            if (value > maxDiscount)
            {
                SaleDiscountInput = maxDiscount;
                return;
            }
        }

        RecalculateSaleTotal();
    }

    partial void OnSaleDiscountModeChanged(string value) => RecalculateSaleTotal();

    /// <summary>
    /// Thanh toán đơn hàng đang bán.
    /// <para>
    /// Thứ tự bắt buộc: khoá chống bấm hai lần → kiểm tra dữ liệu → xác nhận
    /// người dùng → LƯU VÀO DATABASE (hóa đơn + chi tiết + tồn kho + công nợ
    /// trong một transaction) → chỉ khi lưu thành công mới mở hóa đơn và báo
    /// "thanh toán thành công" → cuối cùng xoá sạch đơn cũ để bán đơn tiếp theo.
    /// Nếu database lỗi thì rollback toàn bộ, giữ nguyên đơn hàng và báo lỗi
    /// thật, tuyệt đối không báo thành công giả.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task ThanhToanAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.SalesPayment, "Bạn không có quyền bán hàng."))
        {
            return;
        }

        // 1. Chống bấm nút nhiều lần / gọi lệnh chồng nhau
        if (IsThanhToanDangXuLy)
            return;

        // 2. Kiểm tra dữ liệu
        if (SaleCart.Count == 0)
        {
            StatusMessage = "Vui lòng chọn ít nhất một sản phẩm trước khi thanh toán.";
            return;
        }

        var sanPhamChuaCoGia = SaleCart.FirstOrDefault(x => x.SanPham.GiaBan <= 0);

        if (sanPhamChuaCoGia is not null)
        {
            StatusMessage =
                $"Sản phẩm \"{sanPhamChuaCoGia.SanPham.TenSanPham}\" chưa có giá bán, không thể thanh toán.";
            return;
        }

        var soLuongSai = SaleCart.FirstOrDefault(x => x.SoLuong <= 0);

        if (soLuongSai is not null)
        {
            StatusMessage =
                $"Số lượng của \"{soLuongSai.SanPham.TenSanPham}\" phải lớn hơn 0.";
            return;
        }

        if (SaleTotal < 0)
        {
            StatusMessage = "Tổng tiền không hợp lệ. Vui lòng kiểm tra lại giỏ hàng.";
            return;
        }

        if (OwnerWindow is null)
        {
            StatusMessage = "Không xác định được cửa sổ bán hàng.";
            return;
        }

        var owner = OwnerWindow;

        // Chụp lại dữ liệu đơn hàng TRƯỚC khi mở hộp thoại, tránh việc người
        // dùng sửa giỏ hàng lúc đang xác nhận làm lệch số liệu.
        var cartSnapshot = SaleCart.ToList();
        var customer = SelectedSaleCustomer;
        var customerName = customer?.TenKhachHang ?? "Khách lẻ";
        var discountSnapshot = SaleDiscountAmount;
        var cashSnapshot = TienKhachDua;

        // 2b. Khách lẻ không được phép mua chịu: bắt buộc thanh toán đủ trước khi
        // hoàn tất. Kiểm tra bằng số tiền thực tế (decimal) chứ không chỉ dựa vào
        // nội dung hiển thị, và chặn TRƯỚC khi gọi database nên không tạo hóa đơn,
        // không trừ tồn kho, không ghi sổ quỹ và không ghi công nợ.
        if (customer is null || customer.Id == 0)
        {
            var canTra = Math.Max(0m, SaleTotal);
            var khachDua = Math.Max(0m, cashSnapshot);
            var conNoConLai = canTra - khachDua;

            if (conNoConLai > 0m)
            {
                StatusMessage =
                    "Khách lẻ không được phép ghi nợ. Vui lòng nhập đủ số tiền khách thanh toán " +
                    "hoặc chọn khách hàng có thông tin để ghi nhận công nợ.";
                return;
            }
        }

        // 2c. Bắt buộc thông tin liên hệ và in kèm đối với đơn nợ lại hoặc giao hàng:
        // nếu không thỏa mãn thì không thể tạo đơn.
        var laDonNoLai = TienCongNo > 0;
        var laDonGiaoHang = SaleCanGiaoHang;
        if (laDonNoLai || laDonGiaoHang)
        {
            var lyDo = laDonNoLai && laDonGiaoHang
                ? "đơn còn công nợ và giao hàng tận nơi"
                : laDonNoLai ? "đơn còn công nợ (nợ lại)" : "đơn cần giao đến địa chỉ của khách";

            if (!SaleInKemSdtDiaChi)
            {
                StatusMessage = $"⚠️ {lyDo}: Bắt buộc phải tích chọn in kèm SĐT và địa chỉ.";
                await ShowMessageAsync("Bắt buộc in kèm thông tin liên hệ", $"Đối với {lyDo}, bạn bắt buộc phải tích chọn 'In kèm SĐT và địa chỉ' lên hóa đơn.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SaleCustomerPhone))
            {
                StatusMessage = $"⚠️ {lyDo}: Vui lòng nhập số điện thoại của khách hàng.";
                await ShowMessageAsync("Thiếu số điện thoại", $"Đối với {lyDo}, bắt buộc phải có số điện thoại khách hàng. Vui lòng nhập số điện thoại.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SaleCustomerAddress))
            {
                StatusMessage = $"⚠️ {lyDo}: Vui lòng nhập địa chỉ của khách hàng.";
                await ShowMessageAsync("Thiếu địa chỉ", $"Đối với {lyDo}, bắt buộc phải có địa chỉ khách hàng. Vui lòng nhập địa chỉ.");
                return;
            }
        }

        // 3. Xác nhận thanh toán
        var confirmWindow = new ThanhToanConfirmWindow(
            customerName,
            SaleTotal,
            TienKhachDua,
            TienThoi,
            TienCongNo);

        if (!await confirmWindow.ShowDialog<bool>(owner))
        {
            StatusMessage = "Đã hủy thanh toán.";
            return;
        }

        // 4. Lưu vào database
        HoaDonBanHangReceipt? receipt = null;

        IsThanhToanDangXuLy = true;
        StatusMessage = "Đang lưu hóa đơn...";

        try
        {
            receipt = await _db.TaoHoaDonBanHangAsync(
                cartSnapshot,
                customer,
                discountSnapshot,
                cashSnapshot,
                ghiChu: SaleGhiChu,
                soDienThoai: SaleCustomerPhone,
                diaChi: SaleCustomerAddress,
                inKemThongTin: SaleInKemSdtDiaChi);
        }
        catch (Exception ex)
        {
            // Giao dịch đã được rollback trong DatabaseService: giữ nguyên đơn
            // hàng để người dùng sửa rồi thử lại, tuyệt đối không báo thành công.
            StatusMessage =
                $"KHÔNG thanh toán được - dữ liệu chưa được lưu vào database: {ex.Message}";

            SafeLog.WriteError("Loi thanh toan ban hang", ex);

            return;
        }
        finally
        {
            IsThanhToanDangXuLy = false;
        }

        // 5. Lưu thành công mới dọn đơn cũ, không làm mất dữ liệu đơn vừa bán
        var maHoaDon = receipt.MaHoaDon;

        ResetDonHangVeMacDinh();

        _invoicesLoaded = false;
        ChonMenu("Hóa đơn");

        // Dashboard phải thấy ngay hóa đơn vừa tạo (doanh thu, số hóa đơn,
        // hoạt động gần đây, top hàng bán chạy, top khách hàng).
        _ = LamMoiTongQuanNeuDangMoAsync();

        // Sổ quỹ tự đọc lại database mỗi lần mở menu nên phiếu thu mới sẽ
        // hiện ra ngay khi người dùng chuyển sang tab "Sổ quỹ".
        var thongTinPhieuThu = string.IsNullOrWhiteSpace(receipt.MaPhieuThu)
            ? ""
            : $" Đã ghi phiếu thu {receipt.MaPhieuThu} vào sổ quỹ.";

        var thongBaoThanhToan = receipt.CanhBao.Count > 0
            ? $"Đã tạo hóa đơn {maHoaDon}. {string.Join(" ", receipt.CanhBao)}"
            : $"Đã tạo hóa đơn {maHoaDon} thành công.";

        StatusMessage = thongBaoThanhToan + thongTinPhieuThu;

        // 6. Hiển thị hóa đơn vừa bán (đọc lại từ database nên khớp dữ liệu đã lưu)
        try
        {
            // Gắn thông tin tiệm lúc mở hóa đơn (không phải lúc thanh toán) để người
            // dùng vừa sửa ở màn "Thông tin tiệm" cũng thấy ngay ở hóa đơn này.
            receipt.ThongTinTiem = await LayThongTinTiemChoHoaDonAsync();

            var invoiceWindow = new InvoiceWindow(receipt, ThuCongNoTuChiTietHoaDonAsync);
            await invoiceWindow.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Đã tạo hóa đơn {maHoaDon} nhưng không mở được cửa sổ hóa đơn: {ex.Message}";

            SafeLog.WriteError("Loi mo cua so hoa don", ex);
        }
    }

    /// <summary>Mở lại một hóa đơn đã có để xem / in (đọc dữ liệu từ database).</summary>
    [RelayCommand]
    private async Task XemHoaDonAsync(HoaDon? hoaDon)
    {
        if (hoaDon is null || OwnerWindow is null)
            return;

        var maHoaDon = hoaDon.MaHoaDon;

        if (string.IsNullOrWhiteSpace(maHoaDon))
            return;

        try
        {
            var receipt = await _db.GetHoaDonBanHangTheoMaAsync(maHoaDon);

            if (receipt is null)
            {
                StatusMessage = $"Không tìm thấy hóa đơn {maHoaDon} trong database.";
                return;
            }

            receipt.ThongTinTiem = await LayThongTinTiemChoHoaDonAsync();

            var invoiceWindow = new InvoiceWindow(receipt, ThuCongNoTuChiTietHoaDonAsync);
            await invoiceWindow.ShowDialog(OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không mở được hóa đơn {maHoaDon}: {ex.Message}";

            SafeLog.WriteError("Loi xem hoa don", ex);
        }
    }

    /// <summary>
    /// Nhận yêu cầu thu công nợ từ cửa sổ chi tiết hóa đơn: hỏi số tiền + phương
    /// thức rồi trừ đúng vào hóa đơn đang xem. Trả về hóa đơn đã đọc lại để cửa
    /// sổ chi tiết cập nhật số nợ, hoặc null khi người dùng hủy / gặp lỗi.
    /// </summary>
    private async Task<HoaDonBanHangReceipt?> ThuCongNoTuChiTietHoaDonAsync(
        HoaDonBanHangReceipt hoaDonDangXem,
        Window parentWindow)
    {
        var owner = parentWindow ?? OwnerWindow;
        if (owner is null || hoaDonDangXem.ConNo <= 0)
            return null;

        if (!await KiemTraQuyenAsync(
                PermissionKeys.CustomersDebtPay,
                "Bạn không có quyền thu công nợ khách hàng."))
        {
            return null;
        }

        var hoaDon = new HoaDon
        {
            MaHoaDon = hoaDonDangXem.MaHoaDon,
            TenKhachHang = hoaDonDangXem.TenKhachHang,
            KhachCanTra = hoaDonDangXem.KhachCanTra,
            KhachDaThanhToan = hoaDonDangXem.KhachDaThanhToan,
            ConNo = hoaDonDangXem.ConNo
        };

        var dialog = new ThuNoHoaDonWindow(hoaDon);
        var ketQua = await dialog.ShowDialog<ThuNoHoaDonKetQua?>(owner);

        if (ketQua is null)
        {
            StatusMessage = "Đã hủy thu công nợ.";
            return null;
        }

        try
        {
            var receipt = await _db.ThuNoHoaDonAsync(
                hoaDon.MaHoaDon,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.GhiChu);

            // Cập nhật lại danh sách hóa đơn
            _invoicesLoaded = false;
            await LoadInvoicesAsync();

            // Cập nhật lại công nợ và danh sách khách hàng
            _customersLoaded = false;
            await LoadCustomersAsync();

            // Cập nhật lại sổ quỹ nếu đang mở tab Sổ quỹ
            if (IsSoQuySelected)
            {
                try
                {
                    await TaiLaiSoQuyAsync();
                }
                catch (Exception sqEx)
                {
                    SafeLog.WriteError("Loi lam moi so quy sau khi thu no", sqEx);
                }
            }

            _ = LamMoiTongQuanNeuDangMoAsync();

            var thongBaoPhieu = string.IsNullOrWhiteSpace(receipt.MaPhieuThu)
                ? ""
                : $" Đã ghi phiếu thu {receipt.MaPhieuThu} vào sổ quỹ.";

            StatusMessage =
                $"Đã thu {ketQua.SoTien:N0} đ cho hóa đơn {hoaDon.MaHoaDon}. "
                + $"Còn nợ {receipt.ConNo:N0} đ.{thongBaoPhieu}";

            // Giữ nguyên thông tin tiệm đang hiển thị trên cửa sổ chi tiết.
            receipt.ThongTinTiem = hoaDonDangXem.ThongTinTiem;

            await ShowMessageAsync(
                "Thu nợ thành công",
                $"Đã thu {ketQua.SoTien:N0} đ cho hóa đơn {hoaDon.MaHoaDon}.\n"
                + $"Còn nợ: {receipt.ConNo:N0} đ.\n"
                + (string.IsNullOrWhiteSpace(receipt.MaPhieuThu) ? "" : $"Mã phiếu thu: {receipt.MaPhieuThu} (đã ghi vào Sổ quỹ)."),
                owner);

            return receipt;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không thu được công nợ", ex.Message, owner);
            SafeLog.WriteError("Loi thu no hoa don", ex);
            return null;
        }
    }

    /// <summary>
    /// Thu công nợ từ nút "Thu" trên từng dòng của bảng danh sách hóa đơn.
    /// </summary>
    [RelayCommand]
    private async Task ThuNoHoaDonAsync(HoaDon? hoaDon)
    {
        if (hoaDon is null || OwnerWindow is null || hoaDon.ConNo <= 0)
            return;

        if (!await KiemTraQuyenAsync(
                PermissionKeys.CustomersDebtPay,
                "Bạn không có quyền thu công nợ khách hàng."))
        {
            return;
        }

        var dialog = new ThuNoHoaDonWindow(hoaDon);
        var ketQua = await dialog.ShowDialog<ThuNoHoaDonKetQua?>(OwnerWindow);

        if (ketQua is null)
        {
            StatusMessage = "Đã hủy thu công nợ.";
            return;
        }

        try
        {
            var receipt = await _db.ThuNoHoaDonAsync(
                hoaDon.MaHoaDon,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.GhiChu);

            // Cập nhật lại danh sách hóa đơn
            _invoicesLoaded = false;
            await LoadInvoicesAsync();

            // Cập nhật lại công nợ và danh sách khách hàng
            _customersLoaded = false;
            await LoadCustomersAsync();

            // Cập nhật lại sổ quỹ nếu đang mở tab Sổ quỹ
            if (IsSoQuySelected)
            {
                try
                {
                    await TaiLaiSoQuyAsync();
                }
                catch (Exception sqEx)
                {
                    SafeLog.WriteError("Loi lam moi so quy sau khi thu no", sqEx);
                }
            }

            _ = LamMoiTongQuanNeuDangMoAsync();

            var thongBaoPhieu = string.IsNullOrWhiteSpace(receipt.MaPhieuThu)
                ? ""
                : $" Đã ghi phiếu thu {receipt.MaPhieuThu} vào sổ quỹ.";

            StatusMessage =
                $"Đã thu {ketQua.SoTien:N0} đ cho hóa đơn {hoaDon.MaHoaDon}. "
                + $"Còn nợ {receipt.ConNo:N0} đ.{thongBaoPhieu}";

            await ShowMessageAsync(
                "Thu nợ thành công",
                $"Đã thu {ketQua.SoTien:N0} đ cho hóa đơn {hoaDon.MaHoaDon}.\n"
                + $"Còn nợ: {receipt.ConNo:N0} đ.\n"
                + (string.IsNullOrWhiteSpace(receipt.MaPhieuThu) ? "" : $"Mã phiếu thu: {receipt.MaPhieuThu} (đã ghi vào Sổ quỹ)."));
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không thu được công nợ", ex.Message);
            SafeLog.WriteError("Loi thu no hoa don", ex);
        }
    }

    #region Thanh toán công nợ phiếu nhập

    /// <summary>
    /// Thanh toán công nợ cho một phiếu nhập từ nút trên bảng báo cáo phiếu nhập.
    /// Mở hộp thoại nhập số tiền/phương thức, ghi nhận vào database rồi làm mới
    /// báo cáo, danh sách nhà cung cấp, sổ quỹ và tổng quan (nếu đang mở).
    /// </summary>
    [RelayCommand]
    private async Task ThanhToanPhieuNhapAsync(PhieuNhapHang? phieu)
    {
        if (phieu is null || OwnerWindow is null || phieu.Id == 0)
            return;

        if (!phieu.CoTheThanhToan)
        {
            StatusMessage = phieu.ConNo <= 0
                ? $"Phiếu nhập {phieu.MaPhieuNhap} đã thanh toán đủ."
                : $"Phiếu nhập {phieu.MaPhieuNhap} không thể thanh toán ở trạng thái hiện tại.";
            return;
        }

        if (!await KiemTraQuyenAsync(
                PermissionKeys.SuppliersDebtPay,
                "Bạn không có quyền thanh toán công nợ nhà cung cấp."))
        {
            return;
        }

        var dialog = new ThanhToanPhieuNhapWindow(phieu);
        var ketQua = await dialog.ShowDialog<ThanhToanPhieuNhapYeuCau?>(OwnerWindow);

        if (ketQua is null)
        {
            StatusMessage = "Đã hủy thanh toán công nợ.";
            return;
        }

        try
        {
            var kq = await _db.ThanhToanPhieuNhapAsync(
                phieu.MaPhieuNhap,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.ThoiGian,
                ketQua.GhiChu);

            await LamMoiSauThanhToanPhieuNhapAsync();

            StatusMessage = $"Đã thanh toán {kq.SoTienThanhToan:N0} đ cho phiếu {kq.MaPhieuNhap}. "
                + $"Còn nợ {kq.ConNo:N0} đ.";

            await ShowMessageAsync(
                "Thanh toán thành công",
                $"Đã thanh toán {kq.SoTienThanhToan:N0} đ cho phiếu {kq.MaPhieuNhap}.\n"
                + $"Còn nợ: {kq.ConNo:N0} đ.\n"
                + $"Mã lịch sử: {kq.MaThanhToan}"
                + (string.IsNullOrWhiteSpace(kq.MaPhieuChi) ? "" : $" • Phiếu chi: {kq.MaPhieuChi} (đã ghi Sổ quỹ)."));
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không thanh toán được công nợ", ex.Message);
            SafeLog.WriteError("Loi thanh toan phieu nhap", ex);
        }
    }

    /// <summary>
    /// Nhận yêu cầu thanh toán từ cửa sổ chi tiết phiếu nhập: mở hộp thoại, ghi
    /// nhận thanh toán rồi đọc lại chi tiết để cửa sổ cập nhật số nợ. Trả về null
    /// khi người dùng hủy hoặc gặp lỗi.
    /// </summary>
    private async Task<PhieuNhapHangDetail?> ThanhToanTuChiTietPhieuNhapAsync(
        PhieuNhapHangDetail detail,
        Window parentWindow)
    {
        var owner = parentWindow ?? OwnerWindow;
        if (owner is null || detail.ConNo <= 0m)
            return null;

        if (!await KiemTraQuyenAsync(
                PermissionKeys.SuppliersDebtPay,
                "Bạn không có quyền thanh toán công nợ nhà cung cấp."))
        {
            return null;
        }

        var dialog = new ThanhToanPhieuNhapWindow(detail);
        var ketQua = await dialog.ShowDialog<ThanhToanPhieuNhapYeuCau?>(owner);

        if (ketQua is null)
        {
            StatusMessage = "Đã hủy thanh toán công nợ.";
            return null;
        }

        try
        {
            var kq = await _db.ThanhToanPhieuNhapAsync(
                detail.MaPhieuNhap,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.ThoiGian,
                ketQua.GhiChu);

            await LamMoiSauThanhToanPhieuNhapAsync();

            StatusMessage = $"Đã thanh toán {kq.SoTienThanhToan:N0} đ cho phiếu {kq.MaPhieuNhap}. "
                + $"Còn nợ {kq.ConNo:N0} đ.";

            await ShowMessageAsync(
                "Thanh toán thành công",
                $"Đã thanh toán {kq.SoTienThanhToan:N0} đ cho phiếu {kq.MaPhieuNhap}.\n"
                + $"Còn nợ: {kq.ConNo:N0} đ.\n"
                + $"Mã lịch sử: {kq.MaThanhToan}"
                + (string.IsNullOrWhiteSpace(kq.MaPhieuChi) ? "" : $" • Phiếu chi: {kq.MaPhieuChi} (đã ghi Sổ quỹ)."),
                owner);

            return await _db.GetPhieuNhapHangDetailAsync(detail.PhieuNhapId);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không thanh toán được công nợ", ex.Message, owner);
            SafeLog.WriteError("Loi thanh toan phieu nhap", ex);
            return null;
        }
    }

    /// <summary>Làm mới các màn hình bị ảnh hưởng sau khi thanh toán công nợ phiếu nhập.</summary>
    private async Task LamMoiSauThanhToanPhieuNhapAsync()
    {
        // Báo cáo phiếu nhập: cập nhật số còn nợ + trạng thái thanh toán của dòng.
        if (IsBaoCaoPhieuNhapSelected)
        {
            await LoadBaoCaoPhieuNhapAsync();
        }

        // Danh sách nhà cung cấp: cập nhật lại tổng công nợ.
        if (IsNhaCungCapSelected)
        {
            await LoadSupplierPageAsync();
        }

        // Sổ quỹ: phiếu chi vừa ghi.
        if (IsSoQuySelected)
        {
            try
            {
                await TaiLaiSoQuyAsync();
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Loi lam moi so quy sau khi thanh toan phieu nhap", ex);
            }
        }

        _ = LamMoiTongQuanNeuDangMoAsync();
    }

    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BaoCaoArrowText))]
    private bool isBaoCaoExpanded;

    public string BaoCaoArrowText => IsBaoCaoExpanded ? "▾" : "▸";

    [RelayCommand]
    private void ChonMenu(string menu)
    {
        IsSalesPanelVisible = false;
        if (menu == "Báo cáo")
        {
            if (SelectedMenu == "Báo cáo")
            {
                IsBaoCaoExpanded = !IsBaoCaoExpanded;
            }
            else
            {
                IsBaoCaoExpanded = true;
            }
        }
        SelectedMenu = menu;
    }

    [RelayCommand]
    private void ToggleBaoCaoMenu()
    {
        IsBaoCaoExpanded = !IsBaoCaoExpanded;
    }

    private void CapNhatTrangThaiChonBaoCao()
    {
        foreach (var item in BaoCaoMenuItems)
        {
            item.IsSelected = (SelectedMenu == "Báo cáo" && item.Key == SelectedReportKey);
        }
    }

    partial void OnSelectedMenuChanged(string value)
    {
        SelectedDataTabIndex = value switch
        {
            "Khách hàng" => 1,
            "Hóa đơn" => 2,
            "Nhà cung cấp" => 3,
            "Nhập hàng" => 4,
            "Sổ quỹ" => 5,
            _ => 0
        };

        // Rời khỏi màn lập phiếu nhập thì đóng lại lịch đang mở
        if (value != "Nhập hàng")
            IsPhieuNhapCalendarOpen = false;

        OnPropertyChanged(nameof(IsTongQuanSelected));
        OnPropertyChanged(nameof(IsHangHoaSelected));
        OnPropertyChanged(nameof(IsHoaDonSelected));
        OnPropertyChanged(nameof(IsKhachHangSelected));
        OnPropertyChanged(nameof(IsNhaCungCapSelected));
        OnPropertyChanged(nameof(IsSoQuySelected));
        OnPropertyChanged(nameof(IsDataSectionSelected));
        OnPropertyChanged(nameof(IsBaoCaoSectionSelected));
        OnPropertyChanged(nameof(IsBaoCaoPhieuNhapSelected));
        OnPropertyChanged(nameof(IsBaoCaoBanHangSelected));
        OnPropertyChanged(nameof(IsBaoCaoHangHoaSelected));
        OnPropertyChanged(nameof(IsBaoCaoKhachHangSelected));
        OnPropertyChanged(nameof(IsBaoCaoNhaCungCapSelected));
        OnPropertyChanged(nameof(IsBaoCaoCuoiNgaySelected));
        OnPropertyChanged(nameof(IsThongTinTiemSelected));

        CapNhatTrangThaiChonBaoCao();

        _ = LoadSelectedSectionAsync(value);
    }

    /// <summary>Thông báo lại toàn bộ cờ quyền chi tiết sau khi phiên đăng nhập thay đổi.</summary>
    public void RaiseAllCanProperties()
    {
        string[] names =
        [
            nameof(CanViewDashboard), nameof(CanViewProducts), nameof(CanViewPurchases),
            nameof(CanViewInvoices), nameof(CanViewSales), nameof(CanViewCustomers),
            nameof(CanViewSuppliers), nameof(CanViewCashBook), nameof(CanViewReports),
            nameof(CanViewUsers), nameof(CanViewSettings), nameof(CanViewStoreInfo),
            nameof(CanManageUsers),
            // Hàng hóa
            nameof(CanCreateProduct), nameof(CanEditProduct), nameof(CanDeleteProduct),
            nameof(CanImportProduct), nameof(CanExportProduct), nameof(CanUpdateProductPrice),
            nameof(CanUpdateProductCost), nameof(CanAdjustStock), nameof(CanViewProductDetail),
            // Bán hàng
            nameof(CanSearchProduct), nameof(CanAddProductToSale), nameof(CanEditSaleQuantity),
            nameof(CanRemoveProductFromSale), nameof(CanApplySaleDiscount), nameof(CanPaySale),
            nameof(CanPrintSaleInvoice), nameof(CanCancelSaleInvoice), nameof(CanViewSaleHistory),
            // Hóa đơn
            nameof(CanViewInvoiceDetail), nameof(CanCreateInvoice), nameof(CanEditInvoice),
            nameof(CanCancelInvoice), nameof(CanDeleteInvoice), nameof(CanPrintInvoice),
            nameof(CanImportInvoice), nameof(CanExportInvoice),
            // Khách hàng
            nameof(CanCreateCustomer), nameof(CanEditCustomer), nameof(CanDeleteCustomer),
            nameof(CanImportCustomer), nameof(CanExportCustomer), nameof(CanViewCustomerDetail),
            nameof(CanViewCustomerDebt), nameof(CanCollectCustomerDebt),
            // Nhà cung cấp
            nameof(CanCreateSupplier), nameof(CanEditSupplier), nameof(CanDeleteSupplier),
            nameof(CanImportSupplier), nameof(CanExportSupplier), nameof(CanViewSupplierDetail),
            nameof(CanViewSupplierDebt), nameof(CanPaySupplierDebt),
            // Phiếu nhập
            nameof(CanCreatePurchase), nameof(CanEditPurchase), nameof(CanDeletePurchase),
            nameof(CanImportPurchase), nameof(CanExportPurchase), nameof(CanPrintPurchase),
            nameof(CanViewPurchaseDetail),
            // Sổ quỹ
            nameof(CanCreateReceipt), nameof(CanCreatePayment), nameof(CanEditCashBook),
            nameof(CanDeleteCashBook), nameof(CanCancelCashBook), nameof(CanImportCashBook),
            nameof(CanExportCashBook), nameof(CanViewCashBookBalance), nameof(CanInitCashBook),
            nameof(CanSetOpeningBalance), nameof(CanViewCashBookDetail),
            // Báo cáo
            nameof(CanViewReportEndOfDay), nameof(CanViewReportSales), nameof(CanViewReportProducts),
            nameof(CanViewReportPurchases), nameof(CanViewRevenue), nameof(CanViewProfit),
            nameof(CanViewCost), nameof(CanPrintReport), nameof(CanExportReport),
            // Tài khoản
            nameof(CanCreateUser), nameof(CanEditUser), nameof(CanDeleteUser),
            nameof(CanAssignPermission), nameof(CanLockUser), nameof(CanUnlockUser),
            nameof(CanResetPassword),
            // Cài đặt / Thông tin tiệm
            nameof(CanEditSettings), nameof(CanEditStoreInfo)
    ];

    foreach (var name in names)
    {
        OnPropertyChanged(name);
    }

    CapNhatBaoCaoMenuVisibility();
}

public bool IsTongQuanSelected => SelectedMenu == "Tổng quan";
public bool IsHangHoaSelected => SelectedMenu == "Hàng hóa";
public bool IsHoaDonSelected => SelectedMenu == "Hóa đơn";
public bool IsKhachHangSelected => SelectedMenu == "Khách hàng";
public bool IsNhaCungCapSelected => SelectedMenu == "Nhà cung cấp";
public bool IsSoQuySelected => SelectedMenu == "Sổ quỹ";

// Báo cáo có vùng nội dung riêng nên không dùng chung TabControl dữ liệu
// Màn nào có vùng nội dung riêng thì không được bật TabControl dữ liệu,
// nếu không TabControl sẽ hiện chồng lên màn đó.
public bool IsDataSectionSelected =>
    !IsTongQuanSelected
    && !IsBaoCaoSectionSelected
    && !IsThongTinTiemSelected;

/// <summary>Màn "Thông tin tiệm" có vùng nội dung riêng, không nằm trong TabControl dữ liệu.</summary>
public bool IsThongTinTiemSelected => SelectedMenu == "Thông tin tiệm";

/// <summary>Được phép xem màn thông tin tiệm.</summary>
public bool CanViewStoreInfo => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.StoreInfoView)
    || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SettingsView)
    || Quyen.IsAdmin(Session.CurrentUser);

public bool CanViewDashboard => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.DashboardView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewProducts => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewPurchases => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.PurchasesView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewInvoices => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.InvoicesView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewSales => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SalesView) || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SalesPayment) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewCustomers => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CustomersView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewSuppliers => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SuppliersView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewCashBook => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookView) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewReports => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ReportsView)
    || Quyen.HasAnyPermission(
        Session.CurrentUser,
        PermissionKeys.ReportsEndOfDay, PermissionKeys.ReportsSales,
        PermissionKeys.ReportsProducts, PermissionKeys.ReportsPurchases)
    || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewUsers => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersView) || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersCreate) || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.UsersAssignPermission) || Quyen.IsAdmin(Session.CurrentUser);
public bool CanViewSettings => Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SettingsView) || Quyen.IsAdmin(Session.CurrentUser);

public bool CanManageUsers
{
    get
    {
        var u = Session.CurrentUser;
        if (u == null) return false;
        if (Quyen.IsAdmin(u)) return true;
        return Session.HasPermission(PermissionKeys.UsersView) || Session.HasPermission(PermissionKeys.UsersAssignPermission) || Session.HasPermission(PermissionKeys.UsersCreate);
    }
}

// ===== Quyền theo từng chức năng chi tiết (dùng cho IsVisible/IsEnabled của nút) =====

private static bool CoQuyen(string key) => Quyen.HasPermission(Session.CurrentUser, key);

// Hàng hóa
public bool CanCreateProduct => CoQuyen(PermissionKeys.ProductsCreate);
public bool CanEditProduct => CoQuyen(PermissionKeys.ProductsEdit);
public bool CanDeleteProduct => CoQuyen(PermissionKeys.ProductsDelete);
public bool CanImportProduct => CoQuyen(PermissionKeys.ProductsImport);
public bool CanExportProduct => CoQuyen(PermissionKeys.ProductsExport);
public bool CanUpdateProductPrice => CoQuyen(PermissionKeys.ProductsUpdatePrice);
public bool CanUpdateProductCost => CoQuyen(PermissionKeys.ProductsUpdateCost);
public bool CanAdjustStock => CoQuyen(PermissionKeys.ProductsAdjustStock);
public bool CanViewProductDetail => CoQuyen(PermissionKeys.ProductsDetail);

// Bán hàng
public bool CanSearchProduct => CoQuyen(PermissionKeys.SalesSearchProduct);
public bool CanAddProductToSale => CoQuyen(PermissionKeys.SalesAddProduct);
public bool CanEditSaleQuantity => CoQuyen(PermissionKeys.SalesEditQuantity);
public bool CanRemoveProductFromSale => CoQuyen(PermissionKeys.SalesRemoveProduct);
public bool CanApplySaleDiscount => CoQuyen(PermissionKeys.SalesDiscount);
public bool CanPaySale => CoQuyen(PermissionKeys.SalesPayment);
public bool CanPrintSaleInvoice => CoQuyen(PermissionKeys.SalesPrint);
public bool CanCancelSaleInvoice => CoQuyen(PermissionKeys.SalesCancel);
public bool CanViewSaleHistory => CoQuyen(PermissionKeys.SalesHistory);

// Hóa đơn
public bool CanViewInvoiceDetail => CoQuyen(PermissionKeys.InvoicesDetail);
public bool CanCreateInvoice => CoQuyen(PermissionKeys.InvoicesCreate);
public bool CanEditInvoice => CoQuyen(PermissionKeys.InvoicesEdit);
public bool CanCancelInvoice => CoQuyen(PermissionKeys.InvoicesCancel);
public bool CanDeleteInvoice => CoQuyen(PermissionKeys.InvoicesDelete);
public bool CanPrintInvoice => CoQuyen(PermissionKeys.InvoicesPrint);
public bool CanImportInvoice => CoQuyen(PermissionKeys.InvoicesImport);
public bool CanExportInvoice => CoQuyen(PermissionKeys.InvoicesExport);

// Khách hàng
public bool CanCreateCustomer => CoQuyen(PermissionKeys.CustomersCreate);
public bool CanEditCustomer => CoQuyen(PermissionKeys.CustomersEdit);
public bool CanDeleteCustomer => CoQuyen(PermissionKeys.CustomersDelete);
public bool CanImportCustomer => CoQuyen(PermissionKeys.CustomersImport);
public bool CanExportCustomer => CoQuyen(PermissionKeys.CustomersExport);
public bool CanViewCustomerDetail => CoQuyen(PermissionKeys.CustomersDetail);
public bool CanViewCustomerDebt => CoQuyen(PermissionKeys.CustomersDebtView);
public bool CanCollectCustomerDebt => CoQuyen(PermissionKeys.CustomersDebtPay);

// Nhà cung cấp
public bool CanCreateSupplier => CoQuyen(PermissionKeys.SuppliersCreate);
public bool CanEditSupplier => CoQuyen(PermissionKeys.SuppliersEdit);
public bool CanDeleteSupplier => CoQuyen(PermissionKeys.SuppliersDelete);
public bool CanImportSupplier => CoQuyen(PermissionKeys.SuppliersImport);
public bool CanExportSupplier => CoQuyen(PermissionKeys.SuppliersExport);
public bool CanViewSupplierDetail => CoQuyen(PermissionKeys.SuppliersDetail);
public bool CanViewSupplierDebt => CoQuyen(PermissionKeys.SuppliersDebtView);
public bool CanPaySupplierDebt => CoQuyen(PermissionKeys.SuppliersDebtPay);

// Phiếu nhập
public bool CanCreatePurchase => CoQuyen(PermissionKeys.PurchasesCreate);
public bool CanEditPurchase => CoQuyen(PermissionKeys.PurchasesEdit);
public bool CanDeletePurchase => CoQuyen(PermissionKeys.PurchasesDelete);
public bool CanImportPurchase => CoQuyen(PermissionKeys.PurchasesImport);
public bool CanExportPurchase => CoQuyen(PermissionKeys.PurchasesExport);
public bool CanPrintPurchase => CoQuyen(PermissionKeys.PurchasesPrint);
public bool CanViewPurchaseDetail => CoQuyen(PermissionKeys.PurchasesDetail);

// Sổ quỹ
public bool CanCreateReceipt => CoQuyen(PermissionKeys.CashBookCreateReceipt);
public bool CanCreatePayment => CoQuyen(PermissionKeys.CashBookCreatePayment);
public bool CanEditCashBook => CoQuyen(PermissionKeys.CashBookEdit);
public bool CanDeleteCashBook => CoQuyen(PermissionKeys.CashBookDelete);
public bool CanCancelCashBook => CoQuyen(PermissionKeys.CashBookCancel);
public bool CanImportCashBook => CoQuyen(PermissionKeys.CashBookImport);
public bool CanExportCashBook => CoQuyen(PermissionKeys.CashBookExport);
public bool CanViewCashBookBalance => CoQuyen(PermissionKeys.CashBookBalance);
public bool CanInitCashBook => CoQuyen(PermissionKeys.CashBookInit);
public bool CanSetOpeningBalance => CoQuyen(PermissionKeys.CashBookOpeningBalance);
public bool CanViewCashBookDetail => CoQuyen(PermissionKeys.CashBookDetail);

// Báo cáo
public bool CanViewReportEndOfDay => CoQuyen(PermissionKeys.ReportsEndOfDay);
public bool CanViewReportSales => CoQuyen(PermissionKeys.ReportsSales);
public bool CanViewReportProducts => CoQuyen(PermissionKeys.ReportsProducts);
public bool CanViewReportPurchases => CoQuyen(PermissionKeys.ReportsPurchases);
public bool CanViewReportSuppliers => CoQuyen(PermissionKeys.ReportsPurchases);
public bool CanViewRevenue => CoQuyen(PermissionKeys.ReportsRevenue);
public bool CanViewProfit => CoQuyen(PermissionKeys.ReportsProfit);
public bool CanViewCost => CoQuyen(PermissionKeys.ReportsCost);
public bool CanPrintReport => CoQuyen(PermissionKeys.ReportsPrint);
public bool CanExportReport => CoQuyen(PermissionKeys.ReportsExport);

// Tài khoản
public bool CanCreateUser => CoQuyen(PermissionKeys.UsersCreate);
public bool CanEditUser => CoQuyen(PermissionKeys.UsersEdit);
public bool CanDeleteUser => CoQuyen(PermissionKeys.UsersDelete);
public bool CanAssignPermission => CoQuyen(PermissionKeys.UsersAssignPermission);
public bool CanLockUser => CoQuyen(PermissionKeys.UsersLock);
public bool CanUnlockUser => CoQuyen(PermissionKeys.UsersUnlock);
public bool CanResetPassword => CoQuyen(PermissionKeys.UsersResetPassword);

// Cài đặt / Thông tin tiệm
public bool CanEditSettings => CoQuyen(PermissionKeys.SettingsEdit);
public bool CanEditStoreInfo => CoQuyen(PermissionKeys.StoreInfoEdit);

/// <summary>Kiểm tra quyền ngay tại thời điểm gọi lệnh (bắt được cả trường hợp gọi trực tiếp).</summary>
private async Task<bool> KiemTraQuyenAsync(string key, string message)
{
    if (CoQuyen(key))
    {
        return true;
    }

    await ShowMessageAsync("Thông báo", message);
    return false;
}

}

