using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using System.Globalization;

namespace ShopHoa.Views;

/// <summary>
/// Form thông tin chi tiết khách hàng chuẩn hóa theo SalesPro (Ảnh 4).
/// </summary>
public partial class KhachHangEditorWindow : Window
{
    private bool _daDongForm;

    public KhachHang Customer { get; }

    public bool IsEditMode { get; }

    public IReadOnlyList<NhomKhachHang> DanhSachNhom { get; }

    public NhomKhachHang? NhomKhachHangDaChon { get; set; }

    public string TieuDe => IsEditMode ? "Sửa khách hàng" : "Thêm khách hàng";

    public Func<string?, uint, Task<bool>>? TrungSoDienThoaiAsync { get; }

    public KhachHangEditorWindow(
        KhachHang? customer,
        IReadOnlyList<NhomKhachHang>? nhomKhachHangOptions,
        bool isEditMode,
        Func<string?, uint, Task<bool>>? trungSoDienThoaiAsync = null)
    {
        IsEditMode = isEditMode;
        TrungSoDienThoaiAsync = trungSoDienThoaiAsync;
        DanhSachNhom = nhomKhachHangOptions ?? Array.Empty<NhomKhachHang>();

        Customer = SaoChep(customer);

        NhomKhachHangDaChon = DanhSachNhom.FirstOrDefault(nhom => nhom.Id == Customer.NhomKhachHangId);

        InitializeComponent();

        Closed += (_, _) => _daDongForm = true;

        DataContext = this;
    }

    private static KhachHang SaoChep(KhachHang? customer)
    {
        if (customer is null)
        {
            return new KhachHang
            {
                Id = 0,
                MaKhachHang = "",
                TenKhachHang = "",
                LoaiKhachHang = "CA_NHAN",
                TrangThai = true,
                ConHopTac = true,
                NgayTao = DateTime.Now
            };
        }

        return new KhachHang
        {
            Id = customer.Id,
            MaKhachHang = customer.MaKhachHang ?? "",
            TenKhachHang = customer.TenKhachHang ?? "",
            LoaiKhachHang = customer.LoaiKhachHang ?? "CA_NHAN",
            SoDienThoai = customer.SoDienThoai,
            NgaySinh = customer.NgaySinh,
            GioiTinh = customer.GioiTinh,
            Email = customer.Email,
            DiaChi = customer.DiaChi,
            PhuongXa = customer.PhuongXa,
            QuanHuyen = customer.QuanHuyen,
            TinhThanh = customer.TinhThanh,
            NhomKhachHangId = customer.NhomKhachHangId,
            NhomKhachHang = customer.NhomKhachHang,
            MaSoThue = customer.MaSoThue,
            SoTaiKhoan = customer.SoTaiKhoan,
            NganHang = customer.NganHang,
            CongNoGoiDau = customer.CongNoGoiDau,
            NhanHieu = customer.NhanHieu,
            ChietKhau = customer.ChietKhau,
            TenNguoiMuaHoaDon = customer.TenNguoiMuaHoaDon,
            TenCongTy = customer.TenCongTy,
            DiaChiXuatHoaDon = customer.DiaChiXuatHoaDon,
            GhiChu = customer.GhiChu,
            TrangThai = customer.TrangThai,
            ConHopTac = customer.ConHopTac,
            NgayTao = customer.NgayTao,
            NgayCapNhat = customer.NgayCapNhat
        };
    }

    private void HienLoi(string message)
    {
        var loiMessage = this.FindControl<TextBlock>("LoiMessageTextBlock");
        if (loiMessage is not null)
            loiMessage.Text = message;
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        HienLoi("");

        var tenBox = this.FindControl<TextBox>("TenKhachHangBox");
        Customer.TenKhachHang = tenBox?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(Customer.TenKhachHang))
        {
            HienLoi("Vui lòng nhập tên khách hàng.");
            tenBox?.Focus();
            return;
        }

        Customer.MaKhachHang = Customer.MaKhachHang?.Trim() ?? "";
        Customer.DiaChi = Customer.DiaChi?.Trim();
        Customer.SoDienThoai = Customer.SoDienThoai?.Trim();
        Customer.Email = Customer.Email?.Trim();
        Customer.SoTaiKhoan = Customer.SoTaiKhoan?.Trim();
        Customer.NganHang = Customer.NganHang?.Trim();
        Customer.MaSoThue = Customer.MaSoThue?.Trim();
        Customer.NhanHieu = Customer.NhanHieu?.Trim();
        Customer.GhiChu = Customer.GhiChu?.Trim();
        Customer.NhomKhachHangId = NhomKhachHangDaChon?.Id;
        Customer.NhomKhachHang = NhomKhachHangDaChon?.TenNhom;

        // Parse Công nợ gối đầu
        var congNoBox = this.FindControl<TextBox>("CongNoGoiDauBox");
        if (congNoBox is not null && !string.IsNullOrWhiteSpace(congNoBox.Text))
        {
            var raw = congNoBox.Text.Replace(",", "").Replace(".", "").Trim();
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var congNo))
                Customer.CongNoGoiDau = congNo;
        }

        // Parse Chiết khấu
        var chietKhauBox = this.FindControl<TextBox>("ChietKhauBox");
        if (chietKhauBox is not null && !string.IsNullOrWhiteSpace(chietKhauBox.Text))
        {
            var raw = chietKhauBox.Text.Replace(",", ".").Trim();
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ck))
                Customer.ChietKhau = ck;
        }

        // Kiểm tra trùng SĐT nếu có hàm kiểm tra
        if (TrungSoDienThoaiAsync is not null && !string.IsNullOrWhiteSpace(Customer.SoDienThoai))
        {
            try
            {
                if (await TrungSoDienThoaiAsync(Customer.SoDienThoai, Customer.Id))
                {
                    HienLoi($"Số điện thoại {Customer.SoDienThoai} đã tồn tại trong hệ thống.");
                    return;
                }
            }
            catch (Exception ex)
            {
                HienLoi("Lỗi kiểm tra số điện thoại: " + ex.Message);
                return;
            }
        }

        if (_daDongForm) return;

        if (!IsEditMode)
            Customer.NgayTao = DateTime.Now;

        Customer.NgayCapNhat = DateTime.Now;

        Close(true);
    }

    private async void OnThietLapGiaClicked(object? sender, RoutedEventArgs e)
    {
        var window = new ThietLapGiaDoiTacWindow(
            Customer.Id,
            Customer.MaKhachHang,
            string.IsNullOrWhiteSpace(Customer.TenKhachHang) ? "Khách hàng" : Customer.TenKhachHang,
            "KH");

        await window.ShowDialog(this);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
