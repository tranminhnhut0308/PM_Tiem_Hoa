using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class ThemSuaGiaKhachHangDialog : Window
{
    private readonly DatabaseService _db = new();
    private readonly KhachHang _customer;
    private readonly GiaBanKhachHangItem? _editItem;
    private SanPham? _selectedProduct;
    private decimal _giaGoc;

    public ThemSuaGiaKhachHangDialog(KhachHang customer, GiaBanKhachHangItem? editItem = null)
    {
        InitializeComponent();
        _customer = customer;
        _editItem = editItem;

        TxtHeaderSubtitle.Text = $"Áp dụng cho: {_customer.MaKhachHang} - {_customer.TenKhachHang}";
        DpNgayApDung.SelectedDate = DateTime.Today;

        this.Opened += async (_, _) =>
        {
            await KhoiTaoAsync();
        };
    }

    private async Task KhoiTaoAsync()
    {
        if (_editItem != null)
        {
            // Chế độ chỉnh sửa
            TxtHeaderTitle.Text = "Chỉnh sửa giá bán riêng cho hàng hóa";
            CboSanPham.IsVisible = false;
            PnlFixedProduct.IsVisible = true;
            TxtFixedProductCode.Text = _editItem.MaSanPham;
            TxtFixedProductName.Text = $" - {_editItem.TenSanPham}";
            TxtDonViTinh.Text = _editItem.DonViTinh ?? "Cái";

            _giaGoc = _editItem.GiaGoc;
            TxtGiaGoc.Text = MoneyInput.Format(_giaGoc) + " đ";

            TxtGiaRieng.Text = MoneyInput.Format(_editItem.GiaRieng);
            TxtGhiChu.Text = _editItem.GhiChu ?? "";
            DpNgayApDung.SelectedDate = _editItem.NgayApDung;

            CapNhatChenhLech();
        }
        else
        {
            // Chế độ thêm mới: tải danh sách sản phẩm
            TxtHeaderTitle.Text = "Thêm giá bán riêng cho hàng hóa";
            CboSanPham.IsVisible = true;
            PnlFixedProduct.IsVisible = false;

            try
            {
                var products = await _db.GetSanPhamAsync();
                CboSanPham.ItemsSource = products;
                if (products.Count > 0)
                {
                    CboSanPham.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                HienThiLoi("Không thể tải danh sách sản phẩm: " + ex.Message);
            }
        }
    }

    private void OnSanPhamSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CboSanPham.SelectedItem is SanPham sp)
        {
            _selectedProduct = sp;
            TxtDonViTinh.Text = sp.DonViTinh ?? "Cái";
            _giaGoc = sp.GiaBan;
            TxtGiaGoc.Text = MoneyInput.Format(_giaGoc) + " đ";

            // Gợi ý giá riêng mặc định bằng giá bán chung nếu chưa nhập
            if (string.IsNullOrWhiteSpace(TxtGiaRieng.Text) || TxtGiaRieng.Text == "0")
            {
                TxtGiaRieng.Text = MoneyInput.Format(_giaGoc);
            }

            CapNhatChenhLech();
        }
    }

    private void OnGiaRiengKeyUp(object? sender, KeyEventArgs e)
    {
        CapNhatChenhLech();
    }

    private void OnGiaRiengLostFocus(object? sender, RoutedEventArgs e)
    {
        if (MoneyInput.TryParse(TxtGiaRieng.Text, true, out var gia, out _))
        {
            TxtGiaRieng.Text = MoneyInput.Format(gia);
        }
        CapNhatChenhLech();
    }

    private void CapNhatChenhLech()
    {
        PnlError.IsVisible = false;

        if (!MoneyInput.TryParse(TxtGiaRieng.Text, true, out var giaRieng, out _))
        {
            TxtChenhLechKetQua.Text = "Chưa nhập số tiền hợp lệ";
            TxtChenhLechKetQua.Foreground = new SolidColorBrush(Color.Parse("#B91C1C"));
            return;
        }

        var chenhLech = giaRieng - _giaGoc;
        var phanTram = _giaGoc > 0 ? (double)(chenhLech / _giaGoc * 100) : 0;

        if (chenhLech < 0)
        {
            TxtChenhLechKetQua.Text = $"Ưu đãi giảm: -{MoneyInput.Format(Math.Abs(chenhLech))} đ ({phanTram:0.#}% so với giá chung)";
            TxtChenhLechKetQua.Foreground = new SolidColorBrush(Color.Parse("#087F6E"));
            PnlChenhLech.Background = new SolidColorBrush(Color.Parse("#E3F2ED"));
            PnlChenhLech.BorderBrush = new SolidColorBrush(Color.Parse("#85C7B7"));
        }
        else if (chenhLech > 0)
        {
            TxtChenhLechKetQua.Text = $"Tăng giá: +{MoneyInput.Format(chenhLech)} đ (+{phanTram:0.#}% so với giá chung)";
            TxtChenhLechKetQua.Foreground = new SolidColorBrush(Color.Parse("#D97706"));
            PnlChenhLech.Background = new SolidColorBrush(Color.Parse("#FEF3C7"));
            PnlChenhLech.BorderBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
        }
        else
        {
            TxtChenhLechKetQua.Text = "Bằng giá niêm yết chung (0%)";
            TxtChenhLechKetQua.Foreground = new SolidColorBrush(Color.Parse("#4B5563"));
            PnlChenhLech.Background = new SolidColorBrush(Color.Parse("#F3F4F6"));
            PnlChenhLech.BorderBrush = new SolidColorBrush(Color.Parse("#D1D5DB"));
        }
    }

    private void HienThiLoi(string message)
    {
        TxtErrorMessage.Text = message;
        PnlError.IsVisible = true;
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        uint sanPhamId;

        if (_editItem != null)
        {
            sanPhamId = _editItem.SanPhamId;
        }
        else
        {
            if (_selectedProduct == null || _selectedProduct.Id == 0)
            {
                HienThiLoi("Vui lòng chọn hàng hóa cần thiết lập giá.");
                return;
            }
            sanPhamId = _selectedProduct.Id;
        }

        if (!MoneyInput.TryParse(TxtGiaRieng.Text, true, out var giaRieng, out var loiGia))
        {
            HienThiLoi(loiGia ?? "Vui lòng nhập giá bán riêng hợp lệ.");
            return;
        }

        if (giaRieng < 0)
        {
            HienThiLoi("Giá bán riêng không được nhỏ hơn 0.");
            return;
        }

        var ngayApDung = DpNgayApDung.SelectedDate ?? DateTime.Today;
        var ghiChu = TxtGhiChu.Text?.Trim();

        try
        {
            await _db.LuuGiaBanKhachHangAsync(_customer.Id, sanPhamId, giaRieng, ghiChu, ngayApDung);
            Close(true);
        }
        catch (Exception ex)
        {
            HienThiLoi("Không thể lưu thiết lập giá: " + ex.Message);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
