using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ShopHoa.Controls;

/// <summary>
/// Ô chọn nhiều giá trị, hiển thị giống ComboBox nhưng bấm vào thì mở danh sách
/// ô tick để chọn 0 hoặc nhiều mục cùng lúc.
///
/// Dùng cho các bộ lọc cần chọn nhiều mục (loại thu chi, người tạo) nhưng danh
/// sách lại dài, không tiện hiện thành một hàng checkbox.
///
/// - <see cref="ItemsSource"/>: danh sách item có thuộc tính IsChecked.
/// - <see cref="SummaryText"/>: chuỗi tóm tắt do ViewModel cung cấp, hiển thị trên ô.
/// - <see cref="Placeholder"/>: chuỗi mờ khi chưa chọn mục nào.
/// </summary>
public partial class MultiSelectComboBox : UserControl
{
    public static readonly StyledProperty<System.Collections.IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, System.Collections.IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string> SummaryTextProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, string>(nameof(SummaryText), string.Empty);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, string>(nameof(Placeholder), "Chọn...");

    public static readonly StyledProperty<bool> HasSelectionProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, bool>(nameof(HasSelection));

    public System.Collections.IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string SummaryText
    {
        get => GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>true khi SummaryText có nội dung (dùng để tô đậm chữ đã chọn).</summary>
    public bool HasSelection
    {
        get => GetValue(HasSelectionProperty);
        set => SetValue(HasSelectionProperty, value);
    }

    private Popup? _popup;
    private Button? _button;
    private TextBlock? _text;

    public MultiSelectComboBox()
    {
        InitializeComponent();

        _popup = this.GetControl<Popup>("PART_Popup");
        _button = this.GetControl<Button>("PART_Button");
        _text = this.GetControl<TextBlock>("PART_Text");

        if (_button is not null)
        {
            _button.Click += OnToggleClick;
            _button.PointerPressed += (_, e) => e.Handled = true;
        }

        if (_popup is not null)
            _popup.Closed += (_, _) => _button?.Classes.Remove("ms-open");

        SyncText();
    }

    /// <summary>
    /// Ghi nội dung hiển thị lên ô: chưa chọn mục nào thì hiện chữ mờ của Placeholder.
    /// </summary>
    private void SyncText()
    {
        if (_text is null)
            return;

        var coChon = !string.IsNullOrWhiteSpace(SummaryText);

        _text.Text = coChon ? SummaryText : Placeholder;
        _text.Classes.Set("ms-placeholder", !coChon);
        HasSelection = coChon;
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_popup is null || _button is null)
            return;

        // Mở lần đầu thì neo popup vào ô đã bấm.
        if (!_popup.IsOpen)
        {
            _popup.PlacementTarget = _button;
            _popup.Placement = PlacementMode.BottomEdgeAlignedLeft;
        }

        _popup.IsOpen = !_popup.IsOpen;

        if (_popup.IsOpen)
            _button.Classes.Add("ms-open");
        else
            _button.Classes.Remove("ms-open");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SummaryTextProperty || change.Property == PlaceholderProperty)
            SyncText();
    }
}