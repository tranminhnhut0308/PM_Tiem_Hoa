using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

public partial class ThemPhieuBanLeView : UserControl
{
    private bool _formattingDiscount;
    private bool _formattingCash;

    public ThemPhieuBanLeView()
    {
        InitializeComponent();

        // Luôn đưa con trỏ về ô quét mã khi mở màn bán hàng để người bán
        // có thể quét ngay mà không phải bấm chuột.
        Loaded += (_, _) => BarcodeTextBox.Focus();
    }

    /// <summary>
    /// Máy quét mã vạch USB gõ mã rồi nhấn Enter — nhận Enter để thêm
    /// sản phẩm vào giỏ và giữ con trỏ ở ô quét cho mã tiếp theo.
    /// </summary>
    private void OnBarcodeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;

        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (viewModel.QuetBarcodeCommand.CanExecute(null))
            _ = viewModel.QuetBarcodeCommand.ExecuteAsync(null);

        BarcodeTextBox.Focus();
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

    private void OnSaleCustomerSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;
        viewModel.SaleCustomerName = text;

        if (text.Equals("Khách lẻ", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedSaleCustomer = null;
            viewModel.SaleCustomerSuggestions.Clear();
            return;
        }

        if (viewModel.SelectedSaleCustomer is not null &&
            !viewModel.SelectedSaleCustomer.TenKhachHang.Equals(text, StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedSaleCustomer = null;
        }

        _ = viewModel.SearchSaleCustomersAsync(text);
    }

    private void OnSaleDiscountTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_formattingDiscount || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var normalized = text.Replace(".", string.Empty).Replace(",", string.Empty);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return;

        var formatted = amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        if (formatted == text)
            return;

        _formattingDiscount = true;
        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;
        _formattingDiscount = false;
    }

    private void OnTienKhachDuaTextChangedUI(object? sender, TextChangedEventArgs e)
    {
        if (_formattingCash || sender is not TextBox textBox)
            return;

        var text = textBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            if (DataContext is MainWindowViewModel vm)
                vm.TienKhachDua = 0;
            return;
        }

        var normalized = text.Replace(".", "").Replace(",", "");
        if (!decimal.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
            return;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TienKhachDua = amount;
        }

        var formatted = amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        if (formatted == text)
            return;

        _formattingCash = true;
        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;
        _formattingCash = false;
    }
}
