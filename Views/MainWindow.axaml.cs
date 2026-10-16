using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.ViewModels;
namespace ShopHoa.Views;

public partial class MainWindow : Window
{
    private bool _formattingDiscount;
    private bool _formattingCash;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainWindowViewModel
        {
            OwnerWindow = this
        };

        DataContext = viewModel;

        this.Opened += async (_, _) =>
        {
            await viewModel.InitializeAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000);
                    var updateRes = await UpdateService.Instance.CheckForUpdatesAsync();
                    if (updateRes.IsUpdateAvailable)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            viewModel.CoBanCapNhatMoi = true;
                            viewModel.PhienBanMoiText = updateRes.NewVersion ?? "";

                            var updateWindow = new UpdateWindow();
                            await updateWindow.ShowDialog(this);
                        });
                    }
                }
                catch { }
            });
        };
    }
    private async void OpenSettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Đóng menu tài khoản, không thì flyout đè lên hộp thoại vừa mở.
        (AccountButton.Flyout as Flyout)?.Hide();

        if (DataContext is MainWindowViewModel
            && !Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SettingsView))
        {
            await ShowMessageAsync("Thông báo", "Bạn không có quyền xem cài đặt.");
            return;
        }

        var settingsDialog = new CategorySettingsWindow();
        await settingsDialog.ShowDialog(this);

        if (DataContext is MainWindowViewModel vm)
        {
            await vm.LoadNhomSanPhamCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Mở màn "Thông tin tiệm" từ menu tài khoản góc trái dưới.
    /// <para>
    /// Chỉ đổi <see cref="MainWindowViewModel.SelectedMenu"/> nên dùng lại đúng màn
    /// hình và logic tải dữ liệu sẵn có — không dựng màn mới, không đụng tới
    /// StoreInfoService. Đóng menu tài khoản trước để flyout không đè lên nội dung.
    /// </para>
    /// </summary>
    private void OpenStoreInfoButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Đóng menu tài khoản, không thì flyout đè lên màn vừa mở.
        (AccountButton.Flyout as Flyout)?.Hide();

        if (DataContext is not MainWindowViewModel viewModel)
            return;

        // Kiểm tra đúng quyền gắn với nút hiển thị (StoreInfoView / SettingsView / admin).
        if (!viewModel.CanViewStoreInfo)
        {
            _ = ShowMessageAsync("Thông báo", "Bạn không có quyền xem thông tin tiệm.");
            return;
        }

        // Giống hệt cách các mục menu khác đổi màn: SelectedMenu được set thì
        // OnSelectedMenuChanged bật cờ IsThongTinTiemSelected và tải lại dữ liệu.
        // Phải tắt màn bán hàng trước, không thì panel bán hàng (ZIndex=10, nền
        // đục) vẫn phủ lên và che khuất màn này.
        viewModel.IsSalesPanelVisible = false;
        viewModel.SelectedMenu = "Thông tin tiệm";
    }

    private async void LogoutButton_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        // Đóng menu tài khoản trước, không thì flyout vẫn hiện khi đăng xuất bị hủy.
        (AccountButton.Flyout as Flyout)?.Hide();

        if (DataContext is not MainWindowViewModel viewModel)
            return;

        await viewModel.DangXuatCommand.ExecuteAsync(null);
    }
    private void OnSaleSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var text = (sender as TextBox)?.Text ?? string.Empty;
        viewModel.SaleSearchText = text;

        if (viewModel.TimKiemHoaBanCommand.CanExecute(text))
        {
            _ = viewModel.TimKiemHoaBanCommand.ExecuteAsync(text);
        }
    }

    private void OnSaleCustomerSearchTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;

        viewModel.SaleCustomerName = text;

        if (text.Equals(
                "Khách lẻ",
                StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedSaleCustomer = null;
            viewModel.SaleCustomerSuggestions.Clear();
            return;
        }

        if (viewModel.SelectedSaleCustomer is not null &&
            !viewModel.SelectedSaleCustomer.TenKhachHang.Equals(
                text,
                StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedSaleCustomer = null;
        }

        // Tìm realtime
        _ = viewModel.SearchSaleCustomersAsync(text);
    }
    private void OnSaleDiscountTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_formattingDiscount || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var normalized = text
            .Replace(".", string.Empty)
            .Replace(",", string.Empty);

        if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
            return;

        var formatted = amount.ToString(
            "N0",
            CultureInfo.GetCultureInfo("vi-VN"));

        if (formatted == text)
            return;

        _formattingDiscount = true;

        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;

        _formattingDiscount = false;
    }

    private void OnImportProductSearchTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.ImportProductSearch = textBox.Text ?? string.Empty;

        if (viewModel.TimKiemSanPhamNhapCommand.CanExecute(null))
        {
            _ = viewModel.TimKiemSanPhamNhapCommand.ExecuteAsync(textBox.Text);
        }
    }

    private void OnImportSupplierSearchTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.ImportSupplierSearch = textBox.Text ?? string.Empty;

        if (viewModel.TimKiemNhaCungCapNhapCommand.CanExecute(null))
        {
            _ = viewModel.TimKiemNhaCungCapNhapCommand.ExecuteAsync(textBox.Text);
        }
    }

    private void OnPhieuNhapNgayNhapBoxPointerPressed(
    object? sender,
        PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        // Ô ngày nhập chỉ để hiển thị, bấm vào để mở lịch chọn ngày
        viewModel.MoLichPhieuNhapCommand.Execute(null);
    }

    private void OnBaoCaoPhieuNhapSearchTextChanged(
    object? sender,
        TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        viewModel.BaoCaoPhieuNhapSearch = textBox.Text ?? string.Empty;

        viewModel.TimKiemBaoCaoPhieuNhapTuDong();
    }

    private void OnBaoCaoPhieuNhapRowDoubleTapped(
    object? sender,
        TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not ListBox listBox)
            return;

        if (listBox.SelectedItem is not PhieuNhapHang item)
            return;

        if (viewModel.XemChiTietPhieuNhapCommand.CanExecute(item))
            viewModel.XemChiTietPhieuNhapCommand.Execute(item);
    }

    private void OnBaoCaoPhieuNhapRowPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        // Chỉ xử lý chuột trái (hoặc cảm ứng)
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
            return;

        // Bỏ qua nếu bấm vào Button (mã phiếu đã có command riêng mở chi tiết)
        var element = e.Source as StyledElement;
        while (element is not null && element != sender)
        {
            if (element is Button)
                return;

            element = element.Parent as StyledElement;
        }

        var item = (sender as Control)?.DataContext as PhieuNhapHang
                   ?? (e.Source as StyledElement)?.DataContext as PhieuNhapHang;

        if (item is null)
            return;

        if (viewModel.XemChiTietPhieuNhapCommand.CanExecute(item))
            viewModel.XemChiTietPhieuNhapCommand.Execute(item);
    }

    private void OnCustomerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var text = (sender as TextBox)?.Text ?? string.Empty;
        viewModel.CustomerSearch = text;

        // Tìm kiếm realtime: ViewModel tự debounce 350ms
        // để không gọi database liên tục khi người dùng gõ.
        viewModel.TimKiemKhachHangTuDong();
    }

    /// <summary>
    /// Click vào dòng khách hàng: mở / đóng / chuyển vùng chi tiết ngay dưới dòng đó.
    /// Bấm bên trong vùng chi tiết hoặc lên nút bấm thì không đóng/mở lại dòng.
    /// </summary>
    private void OnCustomerRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        var row = TimDongKhachHang(e.Source as StyledElement, out var benTrongChiTiet);

        if (row is null || benTrongChiTiet)
            return;

        viewModel.MoRongDongKhachHangCommand.Execute(row);
    }

    /// <summary>
    /// Click vào bảng thống kê (tổng chi tiêu / công nợ): mở đúng tab tương ứng
    /// của vùng chi tiết đang mở.
    /// </summary>
    private void OnCustomerSummaryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var tab = (sender as Control)?.Tag?.ToString();

        if (viewModel.ExpandedCustomer is null)
            return;

        // Mở đúng tab của vùng chi tiết đang mở
        viewModel.SelectedCustomer?.Detail?.ChonTabCommand.Execute(
            tab == "congno" ? "congno" : "lichsu");
    }

    /// <summary>
    /// Đi từ phần tử được bấm lên trên để tìm dòng khách hàng chứa nó.
    /// Báo thêm nếu phần tử nằm bên trong vùng chi tiết đã mở.
    /// </summary>
    private static KhachHangRowViewModel? TimDongKhachHang(
        StyledElement? element,
        out bool benTrongChiTiet)
    {
        benTrongChiTiet = false;

        while (element is not null)
        {
            if (element.Name == "DetailPanel")
                benTrongChiTiet = true;

            // Không coi thao tác bấm nút bên trong dòng là mở/đóng dòng
            if (element is Button or ComboBox or TextBox or Avalonia.Controls.Primitives.ToggleButton)
                benTrongChiTiet = true;

            if (element.DataContext is KhachHangRowViewModel row)
                return row;

            element = element.Parent as StyledElement;
        }

        return null;
    }

    // ============================================================
    // NHÀ CUNG CẤP
    // ============================================================

    /// <summary>
    /// Gõ từ khóa tìm nhà cung cấp: ViewModel tự debounce 350ms
    /// để không gọi database liên tục.
    /// </summary>
    private void OnSupplierSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.SupplierSearch = (sender as TextBox)?.Text ?? string.Empty;

        viewModel.TimKiemNhaCungCapTuDong();
    }

    /// <summary>
    /// Menu bộ lọc do Flyout của Avalonia mở/đóng. Hai hàm này chỉ cập nhật
    /// biến trong ViewModel để mũi tên trên nút đổi đúng chiều.
    /// </summary>
    private void OnSupplierFilterFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.IsSupplierFilterPanelOpen = true;
    }

    private void OnSupplierFilterFlyoutClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.IsSupplierFilterPanelOpen = false;
    }

    private void OnCustomerFilterFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.IsCustomerFilterPanelOpen = true;
    }

    private void OnCustomerFilterFlyoutClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.IsCustomerFilterPanelOpen = false;
    }

    /// <summary>
    /// Bấm "Áp dụng" thì đóng menu bộ lọc. Áp dụng xong danh sách và thống kê
    /// đã cập nhật, giữ menu mở chỉ che mất bảng dữ liệu.
    /// </summary>
    private void OnSupplierFilterApplyClicked(object? sender, RoutedEventArgs e)
        => DongMenuBoLocSauKhiThucThiCommand();

    /// <summary>Bấm "Xóa" thì đóng menu bộ lọc sau khi đã reset về "Tất cả".</summary>
    private void OnSupplierFilterClearClicked(object? sender, RoutedEventArgs e)
        => DongMenuBoLocSauKhiThucThiCommand();

    private void OnCustomerFilterApplyClicked(object? sender, RoutedEventArgs e)
        => DongMenuBoLocSauKhiThucThiCommand();

    private void OnCustomerFilterClearClicked(object? sender, RoutedEventArgs e)
        => DongMenuBoLocSauKhiThucThiCommand();

    /// <summary>
    /// Hoãn việc đóng menu bộ lọc sang sau khi nút xử lý xong.
    ///
    /// <see cref="Button"/> phát sự kiện <c>Click</c> TRƯỚC rồi mới chạy
    /// <c>Command</c>. Nếu hàm Click đóng Flyout ngay, nút bị gỡ khỏi cây giao
    /// diện, DataContext thành null nên Command (binding) bị mất và lệnh lọc
    /// không bao giờ chạy. Đẩy việc đóng xuống dispatcher để Command chạy trước.
    /// </summary>
    private void DongMenuBoLocSauKhiThucThiCommand()
        => Dispatcher.UIThread.Post(DongMenuBoLoc, DispatcherPriority.Background);

    /// <summary>
    /// Đóng menu bộ lọc (của khách hàng hoặc nhà cung cấp). Nội dung menu nằm
    /// trong Popup nên không sinh field, phải lấy qua chính nút đã gắn Flyout.
    /// </summary>
    private void DongMenuBoLoc()
    {
        // FlyoutBase.IsOpen không có setter công khai, dùng Hide() của FlyoutBase.
        if (CustomerFilterButton.Flyout is FlyoutBase flyoutCustomer)
            flyoutCustomer.Hide();

        if (SupplierFilterButton.Flyout is FlyoutBase flyoutSupplier)
            flyoutSupplier.Hide();
    }

    private void OnSupplierPageSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not ComboBox comboBox)
            return;

        if (!int.TryParse(comboBox.SelectedItem?.ToString(), out var pageSize) || pageSize <= 0)
            return;

        // Bỏ qua khi giá trị không đổi (kể cả lúc giao diện vừa khởi tạo)
        if (viewModel.SupplierPageSize == pageSize)
            return;

        viewModel.SupplierPageSize = pageSize;
        viewModel.SupplierPage = 1;

        _ = viewModel.LoadSupplierPageAsync();
    }

    /// <summary>
    /// Click vào dòng nhà cung cấp: mở / đóng / chuyển vùng chi tiết
    /// ngay dưới dòng đó. Bấm bên trong vùng chi tiết hoặc lên nút bấm
    /// thì không đóng/mở lại dòng.
    /// </summary>
    private void OnSupplierRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        var row = TimDongNhaCungCap(e.Source as StyledElement, out var benTrongChiTiet);

        if (row is null || benTrongChiTiet)
            return;

        viewModel.ToggleSupplierDetailCommand.Execute(row);
    }

    /// <summary>
    /// Click vào ô thống kê nhanh: mở đúng tab tương ứng của vùng chi tiết
    /// đang mở.
    /// </summary>
    private void OnSupplierSummaryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var tab = (sender as Control)?.Tag?.ToString();

        if (viewModel.ExpandedSupplierRow?.Detail is null)
            return;

        viewModel.ExpandedSupplierRow.Detail.ChonTabCommand.Execute(
            tab == "congno" ? "congno" : "lichsu");
    }

    /// <summary>
    /// Đi từ phần tử được bấm lên trên để tìm dòng nhà cung cấp chứa nó.
    /// Báo thêm nếu phần tử nằm bên trong vùng chi tiết đã mở.
    /// </summary>
    private static NhaCungCapRowViewModel? TimDongNhaCungCap(
        StyledElement? element,
        out bool benTrongChiTiet)
    {
        benTrongChiTiet = false;

        while (element is not null)
        {
            if (element.Name == "DetailPanel")
                benTrongChiTiet = true;

            // Không coi thao tác bấm nút/ô nhập bên trong dòng là mở/đóng dòng
            if (element is Button or ComboBox or TextBox or Avalonia.Controls.Primitives.ToggleButton)
                benTrongChiTiet = true;

            if (element.DataContext is NhaCungCapRowViewModel row)
                return row;

            element = element.Parent as StyledElement;
        }

        return null;
    }

    // ============================================================
    // HÀNG HÓA: MỞ FORM SỬA + CHỈNH GIÁ NHANH
    // ============================================================

    /// <summary>
    /// Click vào một dòng hàng hóa: mở form chi tiết đúng sản phẩm đang chọn.
    /// Bấm vào ô nhập giá hoặc lên nút bấm bên trong dòng thì không mở form,
    /// để không lấy nhầm sản phẩm và không đè lên ô sửa giá nhanh.
    /// </summary>
    private void OnProductRowPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        var product = TimDongSanPham(e.Source as StyledElement, out var benTrongOChinhGia);

        if (product is null || benTrongOChinhGia)
            return;

        viewModel.SuaSanPhamCommand.Execute(product);
    }

    /// <summary>
    /// Enter trong ô giá bán nhanh = nhấn ✓ (lưu), Escape = nhấn ✕ (hủy).
    /// </summary>
    private void OnInlineGiaBanKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        var product = TimDongSanPham(textBox, out _);

        if (product is null)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                if (viewModel.LuuGiaBanNhanhCommand.CanExecute(product))
                    viewModel.LuuGiaBanNhanhCommand.Execute(product);

                e.Handled = true;
                break;

            case Key.Escape:
                viewModel.HuyChinhGiaBanCommand.Execute(product);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Đi từ phần tử được bấm lên trên để tìm đúng dòng sản phẩm chứa nó.
    /// Báo thêm nếu phần tử nằm bên trong vùng sửa giá nhanh.
    /// </summary>
    private static SanPham? TimDongSanPham(
        StyledElement? element,
        out bool benTrongOChinhGia)
    {
        benTrongOChinhGia = false;

        while (element is not null)
        {
            if (element.Name == "PriceEditPanel")
                benTrongOChinhGia = true;

            // Bấm nút hoặc gõ trong ô nhập bên trong dòng thì không mở form sửa
            if (element is Button or ComboBox
                or Avalonia.Controls.Primitives.ToggleButton
                or TextBox)
            {
                benTrongOChinhGia = true;
            }

            if (element.DataContext is SanPham product)
                return product;

            element = element.Parent as StyledElement;
        }

        return null;
    }

    // ============================================================
    // XUẤT DANH SÁCH HÀNG HÓA
    // ============================================================

    private async void OnExportProductsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                await ShowMessageAsync(
                    "Thông báo",
                    "Không tìm thấy dữ liệu sản phẩm.");
                return;
            }

            if (viewModel.SanPhams == null || viewModel.SanPhams.Count == 0)
            {
                await ShowMessageAsync(
                    "Thông báo",
                    "Không có sản phẩm để xuất file.");
                return;
            }

            if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsExport))
            {
                await ShowMessageAsync("Thông báo", "Bạn không có quyền export hàng hóa.");
                return;
            }

            // ====================================================
            // CHỌN NƠI LƯU FILE
            // ====================================================

            var file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Xuất danh sách hàng hóa",

                    SuggestedFileName =
                        $"DanhSachSanPham_{DateTime.Now:yyyyMMdd_HHmmss}.csv",

                    DefaultExtension = "csv",

                    FileTypeChoices =
                    [
                        new FilePickerFileType("Excel CSV")
                        {
                            Patterns = ["*.csv"]
                        }
                    ]
                });

            if (file == null)
                return;


            // ====================================================
            // TẠO FILE CSV
            // ====================================================

            var csv = new StringBuilder();

            // UTF-8 BOM để Excel hiển thị tiếng Việt đúng
            csv.Append('\uFEFF');


            // ====================================================
            // HEADER
            // ====================================================

            csv.AppendLine(
                "Mã hàng," +
                "Tên hàng," +
                "Loại hàng," +
                "Nhóm hàng," +
                "Giá bán," +
                "Giá vốn," +
                "Tồn kho," +
                "Khách đặt," +
                "ĐVT," +
                "Thời gian tạo," +
                "Dự kiến hết hàng");


            // ====================================================
            // DỮ LIỆU SẢN PHẨM
            // ====================================================

            foreach (var sanPham in viewModel.SanPhams)
            {
                var maSanPham =
                    EscapeCsv(sanPham.MaSanPham);

                var tenSanPham =
                    EscapeCsv(sanPham.TenSanPham);

                var loaiHang = EscapeCsv(
                    !string.IsNullOrWhiteSpace(sanPham.LoaiHang)
                        ? sanPham.LoaiHang
                        : "Hàng hóa");

                var nhomHang = EscapeCsv(
                    !string.IsNullOrWhiteSpace(sanPham.NhomHangBaCap)
                        ? sanPham.NhomHangBaCap
                        : (!string.IsNullOrWhiteSpace(sanPham.NhomSanPham)
                            ? sanPham.NhomSanPham
                            : ""));

                var giaBan =
                    sanPham.GiaBan.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"));

                // Dùng Reflection để nếu model có GiaVon thì lấy,
                // nếu chưa có thì để trống, tránh lỗi build.
                var giaVonValue =
                    GetPropertyValue(sanPham, "GiaVon");

                var giaVon = FormatNumber(giaVonValue);

                var tonKho =
                    sanPham.TonKhoExcel.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"));

                var khachDat =
                    sanPham.KhachDat.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("vi-VN"));

                var donViTinh = EscapeCsv(
                    !string.IsNullOrWhiteSpace(sanPham.DonViTinh)
                        ? sanPham.DonViTinh
                        : (!string.IsNullOrWhiteSpace(sanPham.TenDonViTinh)
                            ? sanPham.TenDonViTinh
                            : ""));

                var ngayTao =
    EscapeCsv(sanPham.NgayTao);

                var duKienHetHang =
                    EscapeCsv(sanPham.DuKienHetHang);


                csv.AppendLine(
                    $"{maSanPham}," +
                    $"{tenSanPham}," +
                    $"{loaiHang}," +
                    $"{nhomHang}," +
                    $"{giaBan}," +
                    $"{giaVon}," +
                    $"{tonKho}," +
                    $"{khachDat}," +
                    $"{donViTinh}," +
                    $"{ngayTao}," +
                    $"{duKienHetHang}");
            }


            // ====================================================
            // GHI FILE
            // ====================================================

            await using var stream =
                await file.OpenWriteAsync();

            using var writer =
                new StreamWriter(
                    stream,
                    new UTF8Encoding(true));

            await writer.WriteAsync(csv.ToString());

            await writer.FlushAsync();


            // ====================================================
            // THÔNG BÁO THÀNH CÔNG
            // ====================================================

            await ShowMessageAsync(
                "Xuất file thành công",
                $"Đã xuất {viewModel.SanPhams.Count:N0} sản phẩm.\n\n" +
                $"File đã được lưu tại:\n{file.Name}");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                "Lỗi xuất file",
                $"Không thể xuất danh sách sản phẩm.\n\n" +
                $"Chi tiết lỗi:\n{ex.Message}");
        }
    }


    // ============================================================
    // ESCAPE CSV
    // ============================================================

    private static string EscapeCsv(object? value)
    {
        if (value == null)
            return "";

        var text = Convert.ToString(
            value,
            CultureInfo.InvariantCulture) ?? "";

        // CSV cần đặt trong dấu " nếu có:
        // dấu phẩy
        // xuống dòng
        // dấu "
        if (text.Contains(',') ||
            text.Contains('"') ||
            text.Contains('\n') ||
            text.Contains('\r'))
        {
            text = text.Replace(
                "\"",
                "\"\"");

            return $"\"{text}\"";
        }

        return text;
    }


    // ============================================================
    // LẤY PROPERTY BẰNG REFLECTION
    // ============================================================

    private static object? GetPropertyValue(
        object? obj,
        string propertyName)
    {
        if (obj == null)
            return null;

        var property =
            obj.GetType().GetProperty(propertyName);

        return property?.GetValue(obj);
    }


    // ============================================================
    // FORMAT SỐ
    // ============================================================

    private static string FormatNumber(object? value)
    {
        if (value == null)
            return "";

        if (value is decimal decimalValue)
        {
            return decimalValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is double doubleValue)
        {
            return doubleValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is float floatValue)
        {
            return floatValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is int intValue)
        {
            return intValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        if (value is long longValue)
        {
            return longValue.ToString(
                "N0",
                CultureInfo.GetCultureInfo("vi-VN"));
        }

        return EscapeCsv(value);
    }

    private void OnTienKhachDuaTextChangedUI(
    object? sender,
    TextChangedEventArgs e)
    {
        if (_formattingCash || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;

        // Cho phép xóa trắng
        if (string.IsNullOrWhiteSpace(text))
        {
            if (DataContext is MainWindowViewModel vm)
                vm.TienKhachDua = 0;

            return;
        }

        // Bỏ dấu phân cách hàng nghìn
        var normalized = text
            .Replace(".", "")
            .Replace(",", "");

        // Chỉ nhận số
        if (!decimal.TryParse(
                normalized,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return;
        }

        // Cập nhật số thật
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TienKhachDua = amount;
        }

        // Format giống ô Giảm giá
        var formatted = amount.ToString(
            "N0",
            CultureInfo.GetCultureInfo("vi-VN"));

        if (formatted == text)
            return;

        _formattingCash = true;

        textBox.Text = formatted;

        // Đưa con trỏ về cuối
        textBox.CaretIndex = formatted.Length;

        _formattingCash = false;
    }

    // ============================================================
    // MESSAGE BOX
    // ============================================================

    private async Task ShowMessageAsync(
        string title,
        string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 500,
            Height = 240,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 15, 0, 0)
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(25)
        };

        panel.Children.Add(
            new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 15
            });

        panel.Children.Add(okButton);

        window.Content = panel;

        okButton.Click += (_, _) =>
        {
            window.Close();
        };

        await window.ShowDialog(this);
    }
    private async void OpenUsersButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Đóng menu tài khoản, không thì flyout đè lên hộp thoại vừa mở.
        (AccountButton.Flyout as Flyout)?.Hide();

        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (!viewModel.CanManageUsers)
        {
            return;
        }

        var usersWindow = new UsersWindow();
        await usersWindow.ShowDialog(this);
    }
    private async void OpenUpdateWindow_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (AccountButton.Flyout as Flyout)?.Hide();
        var updateWindow = new UpdateWindow();
        await updateWindow.ShowDialog(this);
    }

    private void InvoiceRow_OnDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Border { DataContext: HoaDon hoaDon } && DataContext is MainWindowViewModel vm)
        {
            if (vm.XemHoaDonCommand.CanExecute(hoaDon))
            {
                vm.XemHoaDonCommand.Execute(hoaDon);
            }
        }
    }
}
