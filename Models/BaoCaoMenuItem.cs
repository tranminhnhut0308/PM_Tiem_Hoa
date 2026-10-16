using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một mục trong nhóm menu "Báo cáo".
/// Thêm báo cáo mới chỉ cần thêm một phần tử vào danh sách trong ViewModel,
/// không phải sửa lại giao diện.
/// </summary>
public sealed class BaoCaoMenuItem : ObservableObject
{
    /// <summary>Khoá định danh, dùng để quyết định hiển thị nội dung báo cáo.</summary>
    public string Key { get; init; } = "";

    public string TieuDe { get; init; } = "";

    public string BieuTuong { get; init; } = "";

    /// <summary>Mã quyền cần có để mục báo cáo này hiện trong menu.</summary>
    public string PermissionKey { get; init; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}


