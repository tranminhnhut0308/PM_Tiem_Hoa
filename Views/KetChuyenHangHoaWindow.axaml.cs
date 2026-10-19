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

public partial class KetChuyenHangHoaWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<KyKeToanItem> _danhSachKy = new();
    private readonly List<ProductOption> _danhSachKho = new();
    private KetChuyenHangHoaPreview? _previewHienTai;
    private bool _dangXuLy;
    private bool _dangKhoiTao = true;

    /// <summary>
    /// Đánh dấu đã thực hiện kết chuyển hàng hóa thành công.
    /// </summary>
    public bool DaKetChuyenThanhCong { get; private set; }

    public KetChuyenHangHoaWindow()
    {
        InitializeComponent();

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync();
    }

    private async Task KhoiTaoDuLieuAsync()
    {
        try
        {
            _dangKhoiTao = true;

            // 1. Tải danh sách kỳ kế toán và danh sách kho song song
            var kyTask = _db.GetDanhSachKyKeToanAsync();
            var khoTask = _db.GetDanhSachKhoAsync();

            await Task.WhenAll(kyTask, khoTask);

            _danhSachKy.Clear();
            _danhSachKy.AddRange(await kyTask);

            _danhSachKho.Clear();
            _danhSachKho.Add(new ProductOption { Id = 0, Name = "--- Tất cả kho hàng ---" });
            _danhSachKho.AddRange(await khoTask);

            // Gán ComboBox
            KyNguonComboBox.ItemsSource = _danhSachKy;
            KyDichComboBox.ItemsSource = _danhSachKy;
            KhoHangComboBox.ItemsSource = _danhSachKho;

            KhoHangComboBox.SelectedIndex = 0;

            if (_danhSachKy.Count >= 2)
            {
                // Mặc định: Kỳ nguồn = kỳ hiện tại (thường là vị trí 1), Kỳ đích = kỳ kế tiếp (thường là vị trí 0)
                KyDichComboBox.SelectedIndex = 0;
                KyNguonComboBox.SelectedIndex = 1;
            }
            else if (_danhSachKy.Count == 1)
            {
                KyNguonComboBox.SelectedIndex = 0;
                KyDichComboBox.SelectedIndex = 0;
            }

            _dangKhoiTao = false;

            // Tải dữ liệu xem trước ban đầu
            await TaiDuLieuXemTruocAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("KhoiTaoDuLieuAsync KetChuyenHangHoa", ex);
            HienThongBao("Lỗi khi tải dữ liệu khởi tạo: " + ex.Message, isError: true);
        }
        finally
        {
            _dangKhoiTao = false;
        }
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

        uint? khoId = null;
        if (KhoHangComboBox.SelectedItem is ProductOption selectedKho && selectedKho.Id > 0)
        {
            khoId = selectedKho.Id;
        }

        try
        {
            CheckButton.IsEnabled = false;
            ExecuteButton.IsEnabled = false;

            _previewHienTai = await _db.PreviewKetChuyenHangHoaAsync(kyNguon.Id, kyDich.Id, khoId);

            // Cập nhật thẻ KPI
            TxtTongMatHang.Text = _previewHienTai.TongSoMatHang.ToString("N0");
            TxtTongSoLuong.Text = _previewHienTai.TongSoLuongHienThi;
            TxtTongGiaTri.Text = _previewHienTai.TongGiaTriHienThi;

            // Gán dữ liệu lên bảng
            HangHoaDataGrid.ItemsSource = _previewHienTai.Items;

            // Kiểm tra trạng thái và cảnh báo
            if (_previewHienTai.KyDichDaKhoaSo)
            {
                HienThongBao(_previewHienTai.CanhBao ?? "Kỳ đích đã khóa sổ. Không thể kết chuyển vào kỳ này.", isError: true);
                ExecuteButton.IsEnabled = false;
            }
            else if (_previewHienTai.DaCoDuLieuDauKyDich)
            {
                HienThongBao(_previewHienTai.CanhBao ?? "Kỳ đích đã có dữ liệu đầu kỳ. Thao tác sẽ cập nhật lại chính xác mà không gây trùng lặp.", isError: false);
                ExecuteButton.IsEnabled = true;
            }
            else
            {
                AnThongBao();
                ExecuteButton.IsEnabled = _previewHienTai.TongSoMatHang > 0;
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("TaiDuLieuXemTruocAsync KetChuyenHangHoa", ex);
            HienThongBao("Lỗi khi đọc dữ liệu xem trước: " + ex.Message, isError: true);
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

    private async void KhoComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
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
        if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.ProductsAdjustStock) &&
            !Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SettingsEdit) &&
            !Quyen.IsAdmin(Session.CurrentUser))
        {
            HienThongBao("Bạn không có quyền thực hiện kết chuyển hàng hóa (Yêu cầu quyền ProductsAdjustStock hoặc Quản trị viên).", isError: true);
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
            HienThongBao($"Kỳ đích [{kyDich.TenKy}] đã khóa sổ. Không thể kết chuyển số liệu vào kỳ đã khóa.", isError: true);
            return;
        }

        if (_previewHienTai == null || _previewHienTai.Items.Count == 0)
        {
            HienThongBao("Không có mặt hàng nào để thực hiện kết chuyển.", isError: true);
            return;
        }

        // 3. Hộp thoại xác nhận an toàn
        var ghiChuThem = _previewHienTai.DaCoDuLieuDauKyDich
            ? $"\n\nLưu ý: Kỳ đích [{kyDich.TenKy}] đã có {_previewHienTai.SoBanGhiDauKyHienCo} mặt hàng đầu kỳ. Thao tác này sẽ cập nhật lại số liệu chính xác theo kỳ nguồn mà không làm nhân đôi số tồn."
            : "";

        var confirm = new ConfirmationWindow(
            "Xác nhận kết chuyển hàng hóa",
            $"Bạn có chắc chắn muốn thực hiện kết chuyển tồn kho?\n\n" +
            $"• Kỳ nguồn: {kyNguon.TenKy}\n" +
            $"• Kỳ đích: {kyDich.TenKy}\n" +
            $"• Số lượng mặt hàng: {_previewHienTai.TongSoMatHang:N0}\n" +
            $"• Tổng số lượng tồn: {_previewHienTai.TongSoLuong:N0}\n" +
            $"• Tổng giá trị tồn kho: {_previewHienTai.TongGiaTri:N0} đ" + ghiChuThem,
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

            uint? khoId = null;
            if (KhoHangComboBox.SelectedItem is ProductOption selectedKho && selectedKho.Id > 0)
            {
                khoId = selectedKho.Id;
            }

            var ghiChu = GhiChuTextBox.Text;

            var ketQua = await _db.ThucHienKetChuyenHangHoaAsync(kyNguon.Id, kyDich.Id, khoId, ghiChu);

            if (ketQua.ThanhCong)
            {
                DaKetChuyenThanhCong = true;

                await NotificationWindow.ShowSuccessAsync(
                    ketQua.ThongBao,
                    "Kết chuyển hàng hóa thành công",
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
            SafeLog.WriteError("Lỗi khi kết chuyển hàng hóa", ex);
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
