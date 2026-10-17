using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class ThietLapGiaDoiTacWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly uint _doiTacId;
    private readonly string _loaiDoiTac;

    public string MaDoiTacHienThi { get; }
    public string TenDoiTacHienThi { get; }

    public ObservableCollection<ChiTietGiaDoiTac> DanhSachGia { get; } = new();
    private readonly List<SanPham> _danhSachSanPham = new();
    private readonly List<ProductOption> _danhSachDvt = new();

    public ThietLapGiaDoiTacWindow(uint doiTacId, string maDoiTac, string tenDoiTac, string loaiDoiTac = "KH")
    {
        _doiTacId = doiTacId;
        _loaiDoiTac = loaiDoiTac;
        MaDoiTacHienThi = string.IsNullOrWhiteSpace(maDoiTac) ? "" : $"[{maDoiTac}]";
        TenDoiTacHienThi = tenDoiTac ?? "";

        InitializeComponent();
        DataContext = this;

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync();
    }

    private async Task KhoiTaoDuLieuAsync()
    {
        try
        {
            var spTask = _db.GetSanPhamAsync();
            var dvtTask = _db.GetDonViTinhAsync();
            await Task.WhenAll(spTask, dvtTask);

            _danhSachSanPham.Clear();
            _danhSachSanPham.AddRange(await spTask);

            _danhSachDvt.Clear();
            _danhSachDvt.AddRange(await dvtTask);

            var spCombo = this.FindControl<ComboBox>("ChonSanPhamComboBox");
            if (spCombo is not null)
            {
                spCombo.ItemsSource = _danhSachSanPham.Select(s => $"{s.MaSanPham} - {s.TenSanPham}").ToList();
            }

            var dvtCombo = this.FindControl<ComboBox>("ChonDvtComboBox");
            if (dvtCombo is not null)
            {
                dvtCombo.ItemsSource = _danhSachDvt.Select(d => d.Name).ToList();
            }

            if (_doiTacId > 0)
            {
                var dsGia = await _db.GetChiTietGiaDoiTacAsync(_doiTacId);
                DanhSachGia.Clear();
                foreach (var item in dsGia)
                {
                    DanhSachGia.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể tải dữ liệu: " + ex.Message, true);
        }
    }

    private void ChonSanPhamComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var spCombo = sender as ComboBox;
        if (spCombo?.SelectedIndex is int idx && idx >= 0 && idx < _danhSachSanPham.Count)
        {
            var sp = _danhSachSanPham[idx];

            // Tự động gợi ý ĐVT gốc và Giá bán/Giá nhập
            var dvtCombo = this.FindControl<ComboBox>("ChonDvtComboBox");
            if (dvtCombo is not null && !string.IsNullOrWhiteSpace(sp.DonViTinh))
            {
                var dvtIdx = _danhSachDvt.FindIndex(d => string.Equals(d.Name, sp.DonViTinh, StringComparison.OrdinalIgnoreCase));
                if (dvtIdx >= 0)
                    dvtCombo.SelectedIndex = dvtIdx;
            }

            var donGiaBox = this.FindControl<TextBox>("DonGiaMoiBox");
            if (donGiaBox is not null)
            {
                var giaGoiY = _loaiDoiTac == "NCC" ? sp.GiaVon : sp.GiaBan;
                donGiaBox.Text = giaGoiY.ToString("N0", CultureInfo.InvariantCulture);
            }
        }
    }

    private void ThemDongGia_OnClick(object? sender, RoutedEventArgs e)
    {
        var spCombo = this.FindControl<ComboBox>("ChonSanPhamComboBox");
        var dvtCombo = this.FindControl<ComboBox>("ChonDvtComboBox");
        var ckBox = this.FindControl<TextBox>("ChietKhauMoiBox");
        var giaBox = this.FindControl<TextBox>("DonGiaMoiBox");

        if (spCombo?.SelectedIndex is not int spIdx || spIdx < 0 || spIdx >= _danhSachSanPham.Count)
        {
            HienThongBao("Vui lòng chọn một mặt hàng.", true);
            return;
        }

        var sp = _danhSachSanPham[spIdx];

        uint dvtId = 0;
        string dvtTen = "";
        if (dvtCombo?.SelectedIndex is int dvtIdx && dvtIdx >= 0 && dvtIdx < _danhSachDvt.Count)
        {
            dvtId = _danhSachDvt[dvtIdx].Id;
            dvtTen = _danhSachDvt[dvtIdx].Name;
        }
        else
        {
            dvtTen = sp.DonViTinh ?? "";
        }

        decimal chietKhau = 0;
        if (ckBox is not null && !string.IsNullOrWhiteSpace(ckBox.Text))
        {
            var rawCk = ckBox.Text.Replace(",", ".").Trim();
            decimal.TryParse(rawCk, NumberStyles.Any, CultureInfo.InvariantCulture, out chietKhau);
        }

        decimal donGia = 0;
        if (giaBox is not null && !string.IsNullOrWhiteSpace(giaBox.Text))
        {
            var rawGia = giaBox.Text.Replace(",", "").Replace(".", "").Trim();
            decimal.TryParse(rawGia, NumberStyles.Any, CultureInfo.InvariantCulture, out donGia);
        }

        // Kiểm tra xem mặt hàng và ĐVT này đã có trong bảng chưa
        var daCo = DanhSachGia.FirstOrDefault(x => x.HangHoaId == sp.Id && (x.DonViTinhId == dvtId || string.Equals(x.DonViTinh, dvtTen, StringComparison.OrdinalIgnoreCase)));
        if (daCo is not null)
        {
            daCo.ChietKhau = chietKhau;
            daCo.DonGia = donGia;
            HienThongBao($"Đã cập nhật giá cho {sp.TenSanPham}.", false);
        }
        else
        {
            DanhSachGia.Add(new ChiTietGiaDoiTac
            {
                DoiTacId = _doiTacId,
                HangHoaId = sp.Id,
                MaHangHoa = sp.MaSanPham,
                TenHangHoa = sp.TenSanPham,
                DonViTinhId = dvtId,
                DonViTinh = dvtTen,
                ChietKhau = chietKhau,
                DonGia = donGia
            });
            HienThongBao($"Đã thêm giá cho {sp.TenSanPham}.", false);
        }
    }

    private void XoaDongGia_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ChiTietGiaDoiTac item)
        {
            DanhSachGia.Remove(item);
            HienThongBao($"Đã xóa dòng giá của {item.TenHangHoa}.", false);
        }
    }

    private async void LuuThietLap_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_doiTacId == 0)
        {
            HienThongBao("Vui lòng lưu thông tin đối tác trước khi lưu bảng giá.", true);
            return;
        }

        try
        {
            await _db.LuuChiTietGiaDoiTacAsync(_doiTacId, DanhSachGia);
            HienThongBao("✅ Đã lưu thiết lập giá cho đối tác thành công!", false);
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể lưu bảng giá: " + ex.Message, true);
        }
    }

    private void Dong_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void HienThongBao(string msg, bool isError)
    {
        var tb = this.FindControl<TextBlock>("ThongBaoTextBlock");
        if (tb is not null)
        {
            tb.Foreground = isError ? Avalonia.Media.Brushes.IndianRed : Avalonia.Media.Brushes.SeaGreen;
            tb.Text = msg;
        }
    }
}
