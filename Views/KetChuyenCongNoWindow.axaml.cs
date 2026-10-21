using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class KetChuyenCongNoWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<KyKeToanItem> _danhSachKy = new();
    private KetChuyenCongNoPreview? _previewHienTai;
    private bool _dangXuLy;
    private bool _dangKhoiTao = true;

    /// <summary>
    /// Đánh dấu đã thực hiện kết chuyển công nợ thành công.
    /// </summary>
    public bool DaKetChuyenThanhCong { get; private set; }

    public KetChuyenCongNoWindow()
    {
        InitializeComponent();

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync();
    }

    private async Task KhoiTaoDuLieuAsync()
    {
        try
        {
            _dangKhoiTao = true;

            var kyTask = await _db.GetDanhSachKyKeToanItemAsync();

            _danhSachKy.Clear();
            _danhSachKy.AddRange(kyTask);

            KyNguonComboBox.ItemsSource = _danhSachKy;
            KyDichComboBox.ItemsSource = _danhSachKy;

            if (_danhSachKy.Count >= 2)
            {
                // Mặc định: Kỳ đích = kỳ kế tiếp (index 0), Kỳ nguồn = kỳ hiện tại (index 1)
                KyDichComboBox.SelectedIndex = 0;
                KyNguonComboBox.SelectedIndex = 1;
            }
            else if (_danhSachKy.Count == 1)
            {
                KyNguonComboBox.SelectedIndex = 0;
                KyDichComboBox.SelectedIndex = 0;
            }

            _dangKhoiTao = false;

            await TaiDuLieuXemTruocAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhoiTaoDuLieuAsync KetChuyenCongNo", ex);
            HienThongBao("Lỗi khi tải dữ liệu khởi tạo: " + ex.Message, isError: true);
        }
        finally
        {
            _dangKhoiTao = false;
        }
    }

    private string LayLoaiDoiTacDangChon()
    {
        if (LoaiDoiTacComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag;
        }
        return "TAT_CA";
    }

    private async Task TaiDuLieuXemTruocAsync()
    {
        if (_dangKhoiTao)
            return;

        if (KyNguonComboBox.SelectedItem is not KyKeToanItem kyNguon ||
            KyDichComboBox.SelectedItem is not KyKeToanItem kyDich)
        {
            return;
        }

        var loaiDoiTac = LayLoaiDoiTacDangChon();

        try
        {
            CheckButton.IsEnabled = false;
            ExecuteButton.IsEnabled = false;

            _previewHienTai = await _db.PreviewKetChuyenCongNoAsync(kyNguon.Id, kyDich.Id, loaiDoiTac);

            // Cập nhật thẻ KPI
            TxtTongDoiTac.Text = _previewHienTai.TongSoDoiTac.ToString("N0");
            TxtTongPhaiThu.Text = _previewHienTai.TongPhaiThuHienThi;
            TxtTongPhaiTra.Text = _previewHienTai.TongPhaiTraHienThi;
            TxtTongConNoRong.Text = _previewHienTai.TongConNoRongHienThi;

            // Gán dữ liệu DataGrid
            CongNoDataGrid.ItemsSource = _previewHienTai.Items;

            // Kiểm tra trạng thái và cảnh báo
            if (_previewHienTai.KyDichDaKhoaSo)
            {
                HienThongBao(_previewHienTai.CanhBao ?? "Kỳ đích đã khóa sổ. Không thể kết chuyển công nợ vào kỳ này.", isError: true);
                ExecuteButton.IsEnabled = false;
            }
            else if (_previewHienTai.DaCoDuLieuDauKyDich)
            {
                HienThongBao(_previewHienTai.CanhBao ?? "Kỳ đích đã có công nợ đầu kỳ. Thao tác sẽ cập nhật lại số dư mới nhất mà không gây cộng dồn trùng lặp.", isError: false);
                ExecuteButton.IsEnabled = true;
            }
            else
            {
                AnThongBao();
                ExecuteButton.IsEnabled = _previewHienTai.TongSoDoiTac > 0;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("TaiDuLieuXemTruocAsync KetChuyenCongNo", ex);
            HienThongBao("Lỗi khi tải dữ liệu công nợ xem trước: " + ex.Message, isError: true);
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void KyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await TaiDuLieuXemTruocAsync();
    }

    private async void LoaiDoiTacComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await TaiDuLieuXemTruocAsync();
    }

    private async void CheckButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await TaiDuLieuXemTruocAsync();
    }

    private async void ExecuteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangXuLy)
            return;

        // 1. Kiểm tra quyền
        if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CustomersDebtView) &&
            !Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SuppliersDebtView) &&
            !Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SettingsEdit) &&
            !Quyen.IsAdmin(Session.CurrentUser))
        {
            HienThongBao("Bạn không có quyền thực hiện kết chuyển công nợ (Yêu cầu quyền xem công nợ hoặc Quản trị viên).", isError: true);
            return;
        }

        // 2. Kiểm tra chọn kỳ
        if (KyNguonComboBox.SelectedItem is not KyKeToanItem kyNguon ||
            KyDichComboBox.SelectedItem is not KyKeToanItem kyDich)
        {
            HienThongBao("Vui lòng chọn đầy đủ kỳ kế toán nguồn và kỳ kế toán đích.", isError: true);
            return;
        }

        if (kyNguon.Id == kyDich.Id)
        {
            HienThongBao("Kỳ nguồn và kỳ đích không được trùng nhau. Vui lòng chọn 2 kỳ khác nhau.", isError: true);
            return;
        }

        if (kyDich.TrangThaiKhoaSo)
        {
            HienThongBao($"Kỳ đích [{kyDich.TenKy}] đã khóa sổ. Không thể kết chuyển công nợ vào kỳ đã khóa.", isError: true);
            return;
        }

        if (_previewHienTai == null || _previewHienTai.Items.Count == 0)
        {
            HienThongBao("Không có đối tác nào có công nợ để thực hiện kết chuyển.", isError: true);
            return;
        }

        // 3. Hộp thoại xác nhận an toàn
        var ghiChuThem = _previewHienTai.DaCoDuLieuDauKyDich
            ? $"\n\nLưu ý: Kỳ đích [{kyDich.TenKy}] đã có {_previewHienTai.SoBanGhiDauKyHienCo} đối tác có nợ đầu kỳ. Thao tác này sẽ cập nhật lại số dư chính xác theo kỳ nguồn mà không làm nhân đôi số nợ."
            : "";

        var confirm = new ConfirmationWindow(
            "Xác nhận kết chuyển công nợ",
            $"Bạn có chắc chắn muốn thực hiện kết chuyển công nợ?\n\n" +
            $"• Kỳ nguồn: {kyNguon.TenKy}\n" +
            $"• Kỳ đích: {kyDich.TenKy}\n" +
            $"• Số đối tác có nợ: {_previewHienTai.TongSoDoiTac:N0} (Khách hàng: {_previewHienTai.SoKhachHang}, NCC: {_previewHienTai.SoNhaCungCap})\n" +
            $"• Tổng nợ phải thu: {_previewHienTai.TongPhaiThuHienThi}\n" +
            $"• Tổng nợ phải trả: {_previewHienTai.TongPhaiTraHienThi}\n" +
            $"• Công nợ ròng: {_previewHienTai.TongConNoRongHienThi}" + ghiChuThem,
            "Đồng ý kết chuyển",
            "Hủy bỏ");

        var xacNhan = await confirm.ShowDialog<bool>(this);
        if (!xacNhan)
            return;

        // 4. Thực hiện kết chuyển
        try
        {
            _dangXuLy = true;
            ExecuteButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            CheckButton.IsEnabled = false;
            ExecuteButton.Content = "⏳ Đang kết chuyển...";

            var loaiDoiTac = LayLoaiDoiTacDangChon();
            var ghiChu = GhiChuTextBox.Text;

            var ketQua = await _db.ThucHienKetChuyenCongNoAsync(kyNguon.Id, kyDich.Id, loaiDoiTac, ghiChu);

            if (ketQua.ThanhCong)
            {
                DaKetChuyenThanhCong = true;

                await NotificationWindow.ShowSuccessAsync(
                    ketQua.ThongBao,
                    "Kết chuyển công nợ thành công",
                    owner: this);

                Close(true);
            }
            else
            {
                HienThongBao(ketQua.ThongBao, isError: true);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khi kết chuyển công nợ", ex);
            HienThongBao("Lỗi khi thực hiện kết chuyển: " + ex.Message, isError: true);
        }
        finally
        {
            _dangXuLy = false;
            ExecuteButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            CheckButton.IsEnabled = true;
            ExecuteButton.Content = "🔄  Thực hiện kết chuyển";
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void HienThongBao(string message, bool isError)
    {
        StatusNoticeBorder.IsVisible = true;
        StatusNoticeText.Text = message;

        if (isError)
        {
            StatusNoticeBorder.Background = new SolidColorBrush(Color.Parse("#FEE4E2"));
            StatusNoticeBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FDA29B"));
            StatusNoticeText.Foreground = new SolidColorBrush(Color.Parse("#B42318"));
            StatusNoticeIcon.Text = "❌";
        }
        else
        {
            StatusNoticeBorder.Background = new SolidColorBrush(Color.Parse("#EFF8FF"));
            StatusNoticeBorder.BorderBrush = new SolidColorBrush(Color.Parse("#B2DDFF"));
            StatusNoticeText.Foreground = new SolidColorBrush(Color.Parse("#175CD3"));
            StatusNoticeIcon.Text = "ℹ️";
        }
    }

    private void AnThongBao()
    {
        StatusNoticeBorder.IsVisible = false;
        StatusNoticeText.Text = "";
    }
}
