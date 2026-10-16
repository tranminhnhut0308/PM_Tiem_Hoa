using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;

namespace ShopHoa.Controls;

/// <summary>
/// Ô nhập số tiền:
/// - Chưa có focus: hiển thị định dạng Việt Nam có dấu phân cách hàng nghìn (3.842.857),
///   số nguyên thì không hiện ".00".
/// - Có focus: bỏ hết dấu phân cách để gõ/sửa số bình thường.
/// - Mỗi lần gõ đều cập nhật ngay <see cref="Value"/> nên Thành tiền / Tổng tiền
///   được tính realtime mà không bị mất khả năng nhập số.
/// Cho phép nhập 0.
/// </summary>
public partial class MoneyTextBox : UserControl
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<MoneyTextBox, decimal>(nameof(Value));

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Nội dung TextBox. Người dùng không cần bind trực tiếp.</summary>
    public string Text { get; set; } = "0";

    private bool _updatingValue;
    private bool _hasFocus;

    public MoneyTextBox()
    {
        InitializeComponent();

        ValueProperty.Changed.AddClassHandler<MoneyTextBox>(
            static (control, args) => control.SyncFromValue());

        Input.GotFocus += OnInputGotFocus;
        Input.LostFocus += OnInputLostFocus;
        Input.TextChanged += OnInputTextChanged;

        SyncFromValue();
    }

    private void OnInputGotFocus(object? sender, RoutedEventArgs e)
    {
        _hasFocus = true;

        // Bỏ dấu phân cách để gõ/sửa số thoải mái
        var raw = MoneyInput.ToRawText(Value);

        if (Input.Text != raw)
            Input.Text = raw;

        Input.SelectAll();
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        _hasFocus = false;

        // Định dạng lại tiền Việt Nam khi rời ô
        var formatted = MoneyInput.Format(Value);

        if (Input.Text != formatted)
            Input.Text = formatted;
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingValue)
            return;

        var parsed = MoneyInput.Parse(Input.Text);

        if (parsed < 0)
            parsed = 0;

        Value = parsed;
    }

    /// <summary>Đồng bộ hiển thị khi giá trị thay đổi từ ViewModel (nạp phiếu cũ, tính lại tổng...).</summary>
    private void SyncFromValue()
    {
        if (_hasFocus)
            return;

        var formatted = MoneyInput.Format(Value);

        _updatingValue = true;

        try
        {
            Text = formatted;

            if (Input is not null && Input.Text != formatted)
                Input.Text = formatted;
        }
        finally
        {
            _updatingValue = false;
        }
    }
}
