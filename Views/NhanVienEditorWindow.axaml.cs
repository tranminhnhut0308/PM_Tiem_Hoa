using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ShopHoa.ViewModels;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ thêm / sửa hồ sơ nhân viên.
/// </summary>
public partial class NhanVienEditorWindow : Window
{
    private NhanVienEditorViewModel? _vm;

    public NhanVienEditorWindow()
    {
        InitializeComponent();
    }

    public NhanVienEditorWindow(NhanVienEditorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Đóng cửa sổ ngay khi lưu thành công.
        vm.OnSaved += () => Dispatcher.UIThread.Post(() => Close());
    }

    /// <summary>Nạp dữ liệu (bộ phận, vai trò, hồ sơ) trước khi hiển thị.</summary>
    public async Task LoadDataAsync()
    {
        if (_vm != null)
            await _vm.LoadAsync();
    }

    private void Huy_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
