using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace ShopHoa.Views;

public partial class NhapGiaBanWindow : Window
{
    public decimal? GiaBan { get; private set; }

    private bool _formatting;

    public NhapGiaBanWindow()
    {
        InitializeComponent();

        GiaBanTextBox.TextChanged += GiaBanTextBox_TextChanged;

        GiaBanTextBox.Focus();
        GiaBanTextBox.CaretIndex = GiaBanTextBox.Text?.Length ?? 0;
    }

    private void GiaBanTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_formatting)
            return;

        var text = GiaBanTextBox.Text ?? "";

        // Chỉ giữ lại số
        var digits = "";

        foreach (var c in text)
        {
            if (char.IsDigit(c))
                digits += c;
        }

        if (string.IsNullOrEmpty(digits))
            return;

        if (!decimal.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return;
        }

        var formatted =
            value.ToString("#,##0", CultureInfo.InvariantCulture)
                 .Replace(",", ".");

        if (formatted == text)
            return;

        _formatting = true;

        // Ghi lại vị trí con trỏ
        var oldCaret = GiaBanTextBox.CaretIndex;

        GiaBanTextBox.Text = formatted;

        // Đưa con trỏ về cuối
        GiaBanTextBox.CaretIndex =
            GiaBanTextBox.Text?.Length ?? 0;

        _formatting = false;
    }

    private void LuuClicked(object? sender, RoutedEventArgs e)
    {
        var text = GiaBanTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            GiaBanTextBox.Focus();
            return;
        }

        // Bỏ dấu chấm phân cách hàng nghìn
        text = text.Replace(".", "");

        if (!decimal.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var giaBan)
            || giaBan <= 0)
        {
            GiaBanTextBox.Focus();
            return;
        }

        GiaBan = giaBan;

        Close(GiaBan);
    }

    private void HuyClicked(object? sender, RoutedEventArgs e)
    {
        GiaBan = null;

        Close(null);
    }
}