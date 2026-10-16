using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ShopHoa.Views;

public partial class QuickOptionEditorWindow : Window
{
    private string? _result;

    public QuickOptionEditorWindow()
    {
        InitializeComponent();
    }

    public QuickOptionEditorWindow(string title, string label, string placeholder, string? initialValue = null)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        LabelTextBlock.Text = label;
        InputTextBox.Watermark = placeholder;
        if (!string.IsNullOrEmpty(initialValue))
        {
            InputTextBox.Text = initialValue;
        }

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            if (!string.IsNullOrEmpty(InputTextBox.Text))
            {
                InputTextBox.SelectAll();
            }
        };
    }

    private void InputTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Submit()
    {
        var text = InputTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            ErrorTextBlock.Text = "Vui lòng không để trống thông tin này.";
            ErrorTextBlock.IsVisible = true;
            InputTextBox.Focus();
            return;
        }

        _result = text;
        Close(_result);
    }

    public static async Task<string?> ShowAsync(Window? owner, string title, string label, string placeholder, string? initialValue = null)
    {
        var dialog = new QuickOptionEditorWindow(title, label, placeholder, initialValue);
        if (owner != null)
        {
            return await dialog.ShowDialog<string?>(owner);
        }

        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            return await dialog.ShowDialog<string?>(desktop.MainWindow);
        }

        dialog.Show();
        return null;
    }
}
