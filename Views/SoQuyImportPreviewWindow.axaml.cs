using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ xem trước trước khi nhập sổ quỹ: tổng quan số dòng, cảnh báo, lỗi và
/// từng dòng đọc được, kèm lựa chọn cách xử lý dòng trùng mã phiếu.
/// <para>
/// Cửa sổ chỉ đọc dữ liệu, chưa ghi gì xuống database. Việc ghi chỉ xảy ra sau
/// khi người dùng bấm "Tiến hành nhập".
/// </para>
/// </summary>
public partial class SoQuyImportPreviewWindow : Window
{
    /// <summary>Số dòng tối đa hiển thị, để giao diện không bị nặng với file lớn.</summary>
    private const int SoDongToiDaHienThi = 1000;

    private SoQuyImportPreview? _preview;

    private XuLyPhieuTrung _xuLyTrung = XuLyPhieuTrung.GiuDuLieuCu;

    public SoQuyImportPreviewWindow()
    {
        InitializeComponent();
    }

    /// <summary>Dữ liệu xem trước. Gán sau khi khởi tạo để cửa sổ tự dựng nội dung.</summary>
    public SoQuyImportPreview? Data
    {
        get => _preview;
        set
        {
            _preview = value;

            HienThiNoiDung();
        }
    }

    /// <summary>true nếu người dùng bấm "Tiến hành nhập".</summary>
    public bool DaXacNhan { get; private set; }

    /// <summary>Cách xử lý dòng trùng mà người dùng chọn.</summary>
    public XuLyPhieuTrung XuLyTrung => _xuLyTrung;

    private void HienThiNoiDung()
    {
        if (_preview is null)
            return;

        Title = "Xem trước trước khi nhập sổ quỹ";

        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        TieudeFileTextBlock.Text =
            $"File: {_preview.TenFile} — đọc {_preview.TongSoDong:N0} dòng, "
            + $"{_preview.SoPhieuThu:N0} phiếu thu, {_preview.SoPhieuChi:N0} phiếu chi.";

        TongDongTextBlock.Text = _preview.TongSoDong.ToString("N0");
        HopLeTextBlock.Text = _preview.SoDongHopLe.ToString("N0");
        LoiTextBlock.Text = _preview.SoDongLoi.ToString("N0");
        TrungMaTextBlock.Text = (_preview.SoDongTrungMa + _preview.SoDongTrungTrongFile).ToString("N0");
        TongThuTextBlock.Text = MoneyInput.Format(_preview.TongTienThu) + " đ";
        TongChiTextBlock.Text = MoneyInput.Format(_preview.TongTienChi) + " đ";

        CanhBaoItems.ItemsSource = _preview.CanhBao.Select(c => (object)c).ToList();
        CanhBaoBorder.IsVisible = _preview.CanhBao.Count > 0;

        LoiItems.ItemsSource = _preview.DanhSachLoi.Select(l => (object)$"• {l.MoTa}").ToList();
        LoiBorder.IsVisible = _preview.DanhSachLoi.Count > 0;
        LoiTieudeTextBlock.Text =
            $"Danh sách lỗi ({_preview.DanhSachLoi.Count:N0} dòng), sửa file gốc rồi nhập lại:";

        var soHienThi = Math.Min(_preview.DanhSachDong.Count, SoDongToiDaHienThi);

        DongItems.ItemsSource = _preview.DanhSachDong.Take(soHienThi).ToList();

        if (soHienThi < _preview.DanhSachDong.Count)
        {
            TieudeFileTextBlock.Text +=
                $" Bảng bên dưới chỉ hiện {soHienThi:N0} dòng đầu, "
                + "thanh tiến độ và kết quả vẫn tính trên toàn bộ file.";
        }

        // Chỉ hiện lựa chọn xử lý trùng khi thật sự có dòng trùng; không có thì
        // mặc định giữ dữ liệu cũ nên không cần bắt người dùng chọn.
        TrungBlock.IsVisible = _preview.CoDongTrung;

        GiuDuLieuCuRadio.IsChecked = true;

        TienHanhNhapButton.IsEnabled = _preview.CoTheNhap;
    }

    private void XuLyTrungRadio_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.IsChecked == true)
            _xuLyTrung = radio == ThayTheRadio
                ? XuLyPhieuTrung.ThayThe
                : radio == BoQuaRadio
                    ? XuLyPhieuTrung.BoQua
                    : XuLyPhieuTrung.GiuDuLieuCu;
    }

    private void TienHanhNhapButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DaXacNhan = true;

        Close();
    }

    private void HuyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DaXacNhan = false;

        Close();
    }
}