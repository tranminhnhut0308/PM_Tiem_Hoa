using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class LichSuCongNoKhachHangWindow : Window
{
    private readonly DatabaseService? _db;
    private readonly uint _khachHangId;
    private readonly string _tenKhachHang;

    public LichSuCongNoKhachHangWindow() : this(null, 0, "")
    {
    }

    public LichSuCongNoKhachHangWindow(DatabaseService? db, uint khachHangId, string tenKhachHang)
    {
        InitializeComponent();

        _db = db;
        _khachHangId = khachHangId;
        _tenKhachHang = tenKhachHang;

        TxtTieuDe.Text = string.IsNullOrWhiteSpace(tenKhachHang)
            ? "Lịch sử công nợ"
            : $"Lịch sử công nợ - {tenKhachHang}";

        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (_db is null || _khachHangId == 0)
            return;

        try
        {
            TxtTrangThai.Text = "Đang tải...";
            var rows = await _db.GetLichSuCongNoKhachHangAsync(_khachHangId);
            GridLichSu.ItemsSource = rows;

            var soDu = rows.Count > 0
                ? rows.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.Id).First().SoDu
                : 0m;
            TxtSoDu.Text = $"Sổ cái: {soDu:N0} đ";
            TxtTrangThai.Text = $"{rows.Count} bút toán";
        }
        catch (System.Exception ex)
        {
            TxtTrangThai.Text = "Lỗi tải lịch sử: " + ex.Message;
            SafeLog.WriteError("Lỗi LichSuCongNoKhachHangWindow", ex);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
