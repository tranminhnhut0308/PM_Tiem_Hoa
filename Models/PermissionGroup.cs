using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShopHoa.Models;

/// <summary>Một quyền chi tiết hiển thị dưới nhóm.</summary>
public sealed partial class PermissionItem : ObservableObject
{
    public uint Id { get; set; }
    public string MaQuyen { get; set; } = string.Empty;
    public string TenQuyen { get; set; } = string.Empty;
    public string? Loai { get; set; }
    public string Nhom { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isChecked;

    /// <summary>Quyền cốt lõi: bắt buộc phải có để mở được phần giao diện của nhóm.</summary>
    public bool IsCoreView => ShopHoa.Services.PermissionCatalog.ModuleViewKeys.Values
        .Contains(MaQuyen, StringComparer.Ordinal);

    /// <summary>Người đang sửa có quyền thay đổi danh sách quyền hay không.</summary>
    [ObservableProperty]
    private bool isSelectable = true;
}

/// <summary>
/// Nhóm quyền (một module) trong cây phân quyền.
/// Checkbox nhóm có trạng thái 3 chiều: đã chọn / chưa chọn / chọn một phần.
/// </summary>
public sealed partial class PermissionGroup : ObservableObject, IDisposable
{
    public string Nhom { get; set; } = string.Empty;
    public string TenNhom { get; set; } = string.Empty;
    public List<PermissionItem> Permissions { get; set; } = new();

    private bool isEditable = true;

    [ObservableProperty]
    private bool isExpanded = true;

    private bool _suppressGroupSync;

    /// <summary>Người đang sửa có quyền thay đổi nhóm quyền này hay không.</summary>
    public bool CanEditPermissions
    {
        get => isEditable;
        set
        {
            if (isEditable == value)
            {
                return;
            }

            isEditable = value;
            OnPropertyChanged();
            foreach (var item in Permissions)
            {
                item.IsSelectable = value;
            }
        }
    }

    /// <summary>true = chọn tất cả, false = bỏ chọn tất cả, null = chọn một phần.</summary>
    public bool? IsChecked
    {
        get
        {
            if (Permissions.Count == 0)
            {
                return false;
            }

            var total = Permissions.Count(p => p.IsChecked);
            if (total == 0)
            {
                return false;
            }

            return total == Permissions.Count ? true : null;
        }
        set
        {
            if (_suppressGroupSync)
            {
                return;
            }

            // Đang ở trạng thái "chọn một phần": click lần đầu sẽ chọn toàn bộ nhóm.
            var target = value == true || (value == false && IsChecked is null);
            foreach (var item in Permissions)
            {
                item.IsChecked = target;
            }

            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TrangThaiNhom));
        }
    }

    public int SelectedCount => Permissions.Count(p => p.IsChecked);
    public int TotalCount => Permissions.Count;

    public string TrangThaiNhom => SelectedCount == 0
        ? "Chưa chọn quyền nào"
        : SelectedCount == TotalCount
            ? "Đã chọn toàn bộ"
            : $"Đã chọn {SelectedCount}/{TotalCount}";

    [RelayCommand]
    private void ChonTatCa() => IsChecked = true;

    [RelayCommand]
    private void BoChonTatCa() => IsChecked = false;

    /// <summary>Gắn sự kiện đồng bộ trạng thái nhóm khi từng quyền chi tiết được tick.</summary>
    public void Attach()
    {
        foreach (var item in Permissions)
        {
            item.IsSelectable = isEditable;
            item.PropertyChanged += OnPermissionChanged;
        }

        RaiseGroupState();
    }

    public void Detach()
    {
        foreach (var item in Permissions)
        {
            item.PropertyChanged -= OnPermissionChanged;
        }
    }

    public void Dispose() => Detach();

    private void OnPermissionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressGroupSync)
        {
            return;
        }

        RaiseGroupState();
    }

    private void RaiseGroupState()
    {
        _suppressGroupSync = true;
        OnPropertyChanged(nameof(IsChecked));
        _suppressGroupSync = false;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(TrangThaiNhom));
    }
}
