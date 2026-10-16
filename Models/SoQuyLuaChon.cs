using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một lựa chọn trong bộ lọc sổ quỹ (quỹ, khoảng thời gian).
/// Lấy từ dữ liệu thật trong database, không có lựa chọn giả.
/// </summary>
public sealed partial class SoQuyLuaChon : ObservableObject
{
    /// <summary>Giá trị gửi xuống SQL (mã quỹ hoặc mã khoảng thời gian).</summary>
    public string GiaTri { get; set; } = "";

    /// <summary>Nhãn hiển thị cho người dùng.</summary>
    public string HienThi { get; set; } = "";

    /// <summary>
    /// true khi mục này đang được chọn. Với nhóm quỹ thì chỉ một mục được chọn
    /// nên giao diện dùng RadioButton bind thẳng vào cờ này.
    /// </summary>
    [ObservableProperty]
    private bool isSelected;

    public SoQuyLuaChon()
    {
    }

    public SoQuyLuaChon(string giaTri, string hienThi)
    {
        GiaTri = giaTri;
        HienThi = hienThi;
    }
}

/// <summary>
/// Một mục có thể tick trong bộ lọc (loại thu/chi, trạng thái, người tạo).
/// Giữ trạng thái tick ngay trong item để giao diện chỉ cần bind IsChecked.
/// </summary>
public sealed partial class SoQuyLuaChonItem : ObservableObject
{
    public string GiaTri { get; }

    public string HienThi { get; }

    [ObservableProperty]
    private bool isChecked;

    public SoQuyLuaChonItem(string giaTri, string hienThi)
    {
        GiaTri = giaTri;
        HienThi = hienThi;
    }
}
