using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class ProductEditorWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<ProductOption> _groupsList;
    private readonly List<ProductOption> _unitsList;
    private readonly SanPham _product;
    private readonly bool _isNew;
    private readonly bool _cheDoXem;
    private readonly bool _duocSuaGiaBan;
    private readonly bool _duocSuaGiaVon;
    private readonly bool _duocDieuChinhTonKho;

    public SanPham Product => _product;

    public ProductEditorWindow() : this(null)
    {
    }

    public ProductEditorWindow(
        SanPham? product = null,
        IReadOnlyList<ProductOption>? groups = null,
        IReadOnlyList<ProductOption>? units = null,
        bool duocSuaGiaBan = true,
        bool duocSuaGiaVon = true,
        bool duocDieuChinhTonKho = true,
        bool cheDoXem = false)
    {
        InitializeComponent();

        _cheDoXem = cheDoXem;
        _duocSuaGiaBan = duocSuaGiaBan && !cheDoXem;
        _duocSuaGiaVon = duocSuaGiaVon && !cheDoXem;
        _duocDieuChinhTonKho = duocDieuChinhTonKho && !cheDoXem;
        _isNew = product is null;
        _product = product ?? new SanPham
        {
            MaSanPham = GenerateProductCode(),
            DuocBanTrucTiep = true,
            DangKinhDoanh = true,
            InAn = true
        };

        _groupsList = (groups ?? []).ToList();
        _unitsList = (units ?? []).ToList();

        NhomSanPhamComboBox.ItemsSource = _groupsList;
        DonViTinhComboBox.ItemsSource = _unitsList;
        DonViTinhCap2ComboBox.ItemsSource = _unitsList;
        DonViTinhCap3ComboBox.ItemsSource = _unitsList;

        ApCheQuyenTungTruong();

        // Gắn sự kiện format số tiền cho các ô giá
        GanSuKienTien(GiaVonTextBox);
        GanSuKienTien(GiaBanTextBox);
        GanSuKienTien(GiaSiTextBox);
        GanSuKienTien(GiaNhapCap2TextBox);
        GanSuKienTien(GiaLeCap2TextBox);
        GanSuKienTien(GiaSiCap2TextBox);
        GanSuKienTien(GiaNhapCap3TextBox);
        GanSuKienTien(GiaLeCap3TextBox);
        GanSuKienTien(GiaSiCap3TextBox);

        Title = _isNew ? "Thêm hàng hóa mới" : (_cheDoXem ? "Chi tiết hàng hóa" : "Sửa hàng hóa");
        LoadProduct();
    }

    private void ApCheQuyenTungTruong()
    {
        if (_cheDoXem)
        {
            foreach (var textBox in new[]
                     {
                         MaSanPhamTextBox, BarcodeTextBox, TenSanPhamTextBox,
                         GiaVonTextBox, GiaBanTextBox, GiaSiTextBox,
                         QuyDoiCap2TextBox, GiaNhapCap2TextBox, GiaLeCap2TextBox, GiaSiCap2TextBox,
                         QuyDoiCap3TextBox, GiaNhapCap3TextBox, GiaLeCap3TextBox, GiaSiCap3TextBox,
                         QuyCachTextBox, TrongLuongTextBox, DuTruMinTextBox, DuTruMaxTextBox, GhiChuTextBox
                     })
            {
                textBox.IsReadOnly = true;
            }

            NhomSanPhamComboBox.IsEnabled = false;
            DonViTinhComboBox.IsEnabled = false;
            DonViTinhCap2ComboBox.IsEnabled = false;
            DonViTinhCap3ComboBox.IsEnabled = false;
            InAnCheckBox.IsEnabled = false;
            ConSuDungCheckBox.IsEnabled = false;
            SaveButton.IsVisible = false;
            CancelButton.Content = "Đóng";
            return;
        }

        GiaBanTextBox.IsEnabled = _duocSuaGiaBan;
        GiaBanTextBox.IsReadOnly = !_duocSuaGiaBan;

        GiaVonTextBox.IsEnabled = _duocSuaGiaVon;
        GiaVonTextBox.IsReadOnly = !_duocSuaGiaVon;
    }

    private void LoadProduct()
    {
        MaSanPhamTextBox.Text = _product.MaSanPham;
        BarcodeTextBox.Text = _product.Barcode ?? _product.MaVach ?? "";
        TenSanPhamTextBox.Text = _product.TenSanPham;

        NhomSanPhamComboBox.SelectedItem = FindOption(_groupsList, _product.NhomSanPhamId);

        // Cấp 1
        DonViTinhComboBox.SelectedItem = FindOption(_unitsList, _product.DonViTinhId);
        GiaVonTextBox.Text = MoneyInput.Format(_product.GiaVon);
        GiaBanTextBox.Text = MoneyInput.Format(_product.GiaBan);
        GiaSiTextBox.Text = MoneyInput.Format(_product.GiaSi);

        // Cấp 2
        DonViTinhCap2ComboBox.SelectedItem = FindOption(_unitsList, _product.DonViTinhCap2Id);
        QuyDoiCap2TextBox.Text = _product.QuyDoiCap2.ToString("G29", CultureInfo.InvariantCulture);
        GiaNhapCap2TextBox.Text = MoneyInput.Format(_product.GiaNhapCap2);
        GiaLeCap2TextBox.Text = MoneyInput.Format(_product.GiaLeCap2);
        GiaSiCap2TextBox.Text = MoneyInput.Format(_product.GiaSiCap2);

        // Cấp 3
        DonViTinhCap3ComboBox.SelectedItem = FindOption(_unitsList, _product.DonViTinhCap3Id);
        QuyDoiCap3TextBox.Text = _product.QuyDoiCap3.ToString("G29", CultureInfo.InvariantCulture);
        GiaNhapCap3TextBox.Text = MoneyInput.Format(_product.GiaNhapCap3);
        GiaLeCap3TextBox.Text = MoneyInput.Format(_product.GiaLeCap3);
        GiaSiCap3TextBox.Text = MoneyInput.Format(_product.GiaSiCap3);

        // Quy cách, trọng lượng, min/max
        QuyCachTextBox.Text = _product.QuyCach ?? "";
        TrongLuongTextBox.Text = (_product.TrongLuong ?? 0m).ToString("G29", CultureInfo.InvariantCulture);
        DuTruMinTextBox.Text = _product.DuTruMin.ToString("G29", CultureInfo.InvariantCulture);
        DuTruMaxTextBox.Text = _product.DuTruMax.ToString("G29", CultureInfo.InvariantCulture);

        GhiChuTextBox.Text = _product.GhiChu ?? "";
        InAnCheckBox.IsChecked = _product.InAn;
        ConSuDungCheckBox.IsChecked = _product.ConSuDung;
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_cheDoXem)
            return;

        ErrorTextBlock.Text = "";

        var ma = (MaSanPhamTextBox.Text ?? "").Trim();
        var ten = (TenSanPhamTextBox.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(ten))
        {
            ErrorTextBlock.Text = "Vui lòng nhập tên hàng hóa.";
            TenSanPhamTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(ma))
        {
            ma = GenerateProductCode();
        }

        _product.MaSanPham = ma;
        _product.Barcode = BarcodeTextBox.Text?.Trim();
        _product.MaVach = _product.Barcode;
        _product.TenSanPham = ten;

        if (NhomSanPhamComboBox.SelectedItem is ProductOption selectedGroup)
        {
            _product.NhomSanPhamId = selectedGroup.Id;
            _product.NhomSanPham = selectedGroup.Name;
        }

        // Cấp 1
        if (DonViTinhComboBox.SelectedItem is ProductOption selectedUnit)
        {
            _product.DonViTinhId = selectedUnit.Id;
            _product.DonViTinh = selectedUnit.Name;
            _product.TenDonViTinh = selectedUnit.Name;
        }

        _product.GiaVon = DocTien(GiaVonTextBox.Text);
        _product.GiaBan = DocTien(GiaBanTextBox.Text);
        _product.GiaSi = DocTien(GiaSiTextBox.Text);

        // Cấp 2
        if (DonViTinhCap2ComboBox.SelectedItem is ProductOption selectedUnit2)
        {
            _product.DonViTinhCap2Id = selectedUnit2.Id;
            _product.DonViTinhCap2 = selectedUnit2.Name;
        }
        _product.QuyDoiCap2 = DocSoThapPhan(QuyDoiCap2TextBox.Text);
        _product.GiaNhapCap2 = DocTien(GiaNhapCap2TextBox.Text);
        _product.GiaLeCap2 = DocTien(GiaLeCap2TextBox.Text);
        _product.GiaSiCap2 = DocTien(GiaSiCap2TextBox.Text);

        // Cấp 3
        if (DonViTinhCap3ComboBox.SelectedItem is ProductOption selectedUnit3)
        {
            _product.DonViTinhCap3Id = selectedUnit3.Id;
            _product.DonViTinhCap3 = selectedUnit3.Name;
        }
        _product.QuyDoiCap3 = DocSoThapPhan(QuyDoiCap3TextBox.Text);
        _product.GiaNhapCap3 = DocTien(GiaNhapCap3TextBox.Text);
        _product.GiaLeCap3 = DocTien(GiaLeCap3TextBox.Text);
        _product.GiaSiCap3 = DocTien(GiaSiCap3TextBox.Text);

        // Quy cách, trọng lượng, min/max
        _product.QuyCach = QuyCachTextBox.Text?.Trim();
        _product.TrongLuong = DocSoThapPhan(TrongLuongTextBox.Text);
        _product.DuTruMin = DocSoThapPhan(DuTruMinTextBox.Text);
        _product.DuTruMax = DocSoThapPhan(DuTruMaxTextBox.Text);

        _product.GhiChu = GhiChuTextBox.Text?.Trim();
        _product.InAn = InAnCheckBox.IsChecked ?? true;
        _product.ConSuDung = ConSuDungCheckBox.IsChecked ?? true;
        _product.DangKinhDoanh = _product.ConSuDung;

        Close(true);
    }

    private void GenerateBarcodeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_cheDoXem) return;
        BarcodeTextBox.Text = GenerateProductCode();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private static ProductOption? FindOption(IEnumerable<ProductOption>? items, uint? id)
    {
        if (items is null || id is null)
            return null;

        return items.FirstOrDefault(item => item.Id == id.Value);
    }

    private static string GenerateProductCode()
    {
        return $"HH{DateTime.Now:yyMMddHHmmss}";
    }

    private static decimal DocTien(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        var cleaned = text.Replace(".", "").Replace(",", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var val) ? val : 0m;
    }

    private static decimal DocSoThapPhan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        var normalized = text.Replace(" ", "").Replace(",", ".").Trim();
        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : 0m;
    }

    private static void GanSuKienTien(TextBox textBox)
    {
        var dangFormat = false;

        textBox.GotFocus += (_, _) =>
        {
            var raw = textBox.Text?.Replace(".", "").Replace(",", "").Trim() ?? "";
            if (raw == "0") raw = "";
            textBox.Text = raw;
            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            var val = DocTien(textBox.Text);
            dangFormat = true;
            textBox.Text = MoneyInput.Format(val);
            dangFormat = false;
        };

        textBox.TextChanged += (_, _) =>
        {
            if (dangFormat) return;

            var digits = new string((textBox.Text ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return;

            if (decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var val))
            {
                var formatted = MoneyInput.Format(val);
                if (formatted != textBox.Text)
                {
                    dangFormat = true;
                    textBox.Text = formatted;
                    textBox.CaretIndex = formatted.Length;
                    dangFormat = false;
                }
            }
        };
    }
}