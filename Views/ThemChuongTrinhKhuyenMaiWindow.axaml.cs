using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class ThemChuongTrinhKhuyenMaiWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<SanPham> _danhSachSanPham = new();
    private readonly List<NhomKhachHang> _danhSachNhomKhachHang = new();
    private readonly ObservableCollection<ChiTietKhuyenMai> _chiTietList = new();
    private readonly ChuongTrinhKhuyenMai? _chuongTrinhEdit;
    private SanPham? _sanPhamDangChon;
    private bool _dangLuu;

    /// <summary>
    /// Chương trình khuyến mãi vừa được lưu thành công (nếu có).
    /// </summary>
    public ChuongTrinhKhuyenMai? KetQuaLuu { get; private set; }

    public ThemChuongTrinhKhuyenMaiWindow()
    {
        InitializeComponent();

        ChiTietItemsControl.ItemsSource = _chiTietList;

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync();
    }

    /// <summary>
    /// Mở cửa sổ ở chế độ chỉnh sửa một chương trình khuyến mãi đã có.
    /// </summary>
    public ThemChuongTrinhKhuyenMaiWindow(ChuongTrinhKhuyenMai chuongTrinhCanSua) : this()
    {
        _chuongTrinhEdit = chuongTrinhCanSua;
    }

    private async Task KhoiTaoDuLieuAsync()
    {
        try
        {
            var dangSua = _chuongTrinhEdit is not null;

            // 1. Mặc định ngày bắt đầu và kết thúc
            NgayBatDauPicker.SelectedDate = dangSua ? _chuongTrinhEdit!.NgayBatDau : DateTime.Today;
            NgayKetThucPicker.SelectedDate = dangSua ? _chuongTrinhEdit!.NgayKetThuc : DateTime.Today.AddDays(30);

            if (dangSua)
            {
                Title = "Chỉnh sửa chương trình khuyến mãi";
                HeaderTitleText.Text = "CHỈNH SỬA CHƯƠNG TRÌNH KHUYẾN MÃI";
                SaveButton.Content = "💾  Cập nhật chương trình";
            }

            // 2. Tải song song mã mới và danh sách sản phẩm
            var maTask = dangSua
                ? Task.FromResult(_chuongTrinhEdit!.MaChuongTrinh)
                : _db.SinhMaKhuyenMaiMoiAsync();
            var spTask = _db.GetSanPhamAsync();

            await Task.WhenAll(maTask, spTask);

            // Gán mã tự sinh (hoặc mã đang sửa)
            MaChuongTrinhTextBox.Text = await maTask;

            // Gán danh sách sản phẩm phục vụ tìm kiếm gợi ý
            _danhSachSanPham.Clear();
            _danhSachSanPham.AddRange(await spTask);

            SanPhamAutoCompleteBox.ItemsSource = _danhSachSanPham;            SanPhamAutoCompleteBox.ItemFilter = (search, item) =>
            {
                if (string.IsNullOrWhiteSpace(search))
                    return true;

                if (item is SanPham sp)
                {
                    var ma = sp.MaSanPham ?? "";
                    var ten = sp.TenSanPham ?? "";
                    var vach = sp.MaVach ?? "";

                    return ma.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           ten.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           vach.Contains(search, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            };

            // 3. Khi sửa: nạp thông tin chương trình và danh sách hàng hóa đã chọn
            if (dangSua)
            {
                TenChuongTrinhTextBox.Text = _chuongTrinhEdit!.TenChuongTrinh;
                GhiChuTextBox.Text = _chuongTrinhEdit.GhiChu ?? "";

                ChonTrangThai(_chuongTrinhEdit.TrangThai);
                ChonLoaiKhuyenMai(_chuongTrinhEdit.LoaiKhuyenMai);
                ApDungDuLieuChungChoDon(_chuongTrinhEdit);

                var chiTiets = await _db.GetChiTietKhuyenMaiAsync(_chuongTrinhEdit.Id);
                _chiTietList.Clear();
                var stt = 1;
                foreach (var d in chiTiets)
                {
                    d.Stt = stt++;
                    d.DanhSachSanPham = _danhSachSanPham;

                    // Khôi phục sản phẩm tặng đã lưu để ComboBox hiển thị đúng
                    if (d.SanPhamTangId is uint idTang)
                    {
                        d.SanPhamTang = _danhSachSanPham.FirstOrDefault(s => s.Id == idTang);
                    }

                    _chiTietList.Add(d);
                }
            }

            CapNhatTrangThaiBang();
            CapNhatGoiYLoaiKhuyenMai();
            CapNhatHienThiTheoLoaiKhuyenMai();
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể khởi tạo dữ liệu: " + ex.Message, isError: true);
        }
    }

    private void ChonTrangThai(string? trangThai)
    {
        if (string.IsNullOrWhiteSpace(trangThai))
            return;

        foreach (var item in TrangThaiComboBox.Items)
        {
            if (item is ComboBoxItem cbi &&
                string.Equals(cbi.Tag?.ToString(), trangThai, StringComparison.OrdinalIgnoreCase))
            {
                TrangThaiComboBox.SelectedItem = cbi;
                return;
            }
        }
    }

    /// <summary>
    /// Chọn loại khuyến mãi khi mở chương trình đang có sẵn.
    /// </summary>
    private void ChonLoaiKhuyenMai(string? loaiKhuyenMai)
    {
        if (string.IsNullOrWhiteSpace(loaiKhuyenMai))
            return;

        foreach (var item in LoaiKhuyenMaiComboBox.Items)
        {
            if (item is ComboBoxItem cbi &&
                string.Equals(cbi.Tag?.ToString(), loaiKhuyenMai, StringComparison.OrdinalIgnoreCase))
            {
                LoaiKhuyenMaiComboBox.SelectedItem = cbi;
                return;
            }
        }
    }

    /// <summary>
    /// Lấy mã loại khuyến mãi đang chọn trên giao diện.
    /// </summary>
    private string GetLoaiKhuyenMaiHienTai()
    {
        if (LoaiKhuyenMaiComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag != null)
            return KhuyenMaiLoai.ChuanHoa(cbi.Tag.ToString());

        return KhuyenMaiLoai.TheoSanPham;
    }

    /// <summary>
    /// Cập nhật dòng gợi ý nhỏ bên cạnh bảng chi tiết theo loại khuyến mãi.
    /// </summary>
    private void CapNhatGoiYLoaiKhuyenMai()
    {
        switch (GetLoaiKhuyenMaiHienTai())
        {
            case KhuyenMaiLoai.TangQua:
                LoaiKhuyenMaiGoiYText.Text =
                    "🎁 Tặng quà theo sản phẩm: Mua đủ số lượng sản phẩm chỉ định ➜ Tặng kèm sản phẩm quà.";
                break;

            case KhuyenMaiLoai.TheoDonHang:
                LoaiKhuyenMaiGoiYText.Text =
                    "🧾 Giảm giá theo tổng đơn hàng: Nhập giá trị đơn tối thiểu và mức giảm (% hoặc VNĐ).";
                break;

            default:
                LoaiKhuyenMaiGoiYText.Text =
                    "🏷 Giảm giá theo sản phẩm: Chọn sản phẩm điều kiện, số lượng tối thiểu và mức giảm trên sản phẩm đó.";
                break;
        }
    }

    private void LoaiKhuyenMaiComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CapNhatGoiYLoaiKhuyenMai();
        CapNhatHienThiTheoLoaiKhuyenMai();
    }

    private void ApDungDuLieuChungChoDon(ChuongTrinhKhuyenMai ct)
    {
    }

    private void CapNhatHienThiTheoLoaiKhuyenMai()
    {
        var loai = GetLoaiKhuyenMaiHienTai();
        bool laTheoDon = string.Equals(loai, KhuyenMaiLoai.TheoDonHang, StringComparison.OrdinalIgnoreCase);
        bool laTangQua = string.Equals(loai, KhuyenMaiLoai.TangQua, StringComparison.OrdinalIgnoreCase);

        GiamNhanhButton.IsVisible = !laTheoDon;
        XoaTatCaButton.IsVisible = !laTheoDon;

        if (laTangQua)
        {
            GiamNhanhButton.IsVisible = false;
        }
    }

    private void SanPhamAutoCompleteBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SanPhamAutoCompleteBox.SelectedItem is SanPham sp)
        {
            _sanPhamDangChon = sp;
        }
    }

    private void ThemHangButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AnThongBao();

        var loai = GetLoaiKhuyenMaiHienTai();
        if (string.Equals(loai, KhuyenMaiLoai.TheoDonHang, StringComparison.OrdinalIgnoreCase))
        {
            HienThongBao("Loại 'Giảm giá theo tổng đơn hàng' không cần danh sách sản phẩm áp dụng.", isError: true);
            return;
        }

        // 1. Xác định sản phẩm cần thêm
        var sp = _sanPhamDangChon;
        if (sp == null)
        {
            var text = (SanPhamAutoCompleteBox.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                sp = _danhSachSanPham.FirstOrDefault(s =>
                    string.Equals(s.MaSanPham, text, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.TenSanPham, text, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (sp == null)
        {
            HienThongBao("Vui lòng chọn hàng hóa hợp lệ từ danh sách gợi ý để thêm.", isError: true);
            SanPhamAutoCompleteBox.Focus();
            return;
        }

        // 2. Chống thêm trùng cùng một hàng hóa
        if (_chiTietList.Any(c => c.SanPhamId == sp.Id))
        {
            HienThongBao($"Hàng hóa [{sp.MaSanPham} - {sp.TenSanPham}] đã có trong chương trình này.", isError: true);
            SanPhamAutoCompleteBox.Text = "";
            _sanPhamDangChon = null;
            return;
        }

        // 3. Thêm vào danh sách chi tiết
        var laTangQua = string.Equals(loai, KhuyenMaiLoai.TangQua, StringComparison.OrdinalIgnoreCase);

        var dongMoi = new ChiTietKhuyenMai
        {
            Stt = _chiTietList.Count + 1,
            SanPhamId = sp.Id,
            MaSanPham = sp.MaSanPham,
            TenSanPham = sp.TenSanPham,
            DonViTinh = sp.DonViTinh ?? "Cái",
            GiaBanHienTai = sp.GiaBan,
            SoLuongMua = 1,
            LoaiGiamGia = "PHAN_TRAM",
            GiaTriGiam = laTangQua ? 0 : 10,
            DanhSachSanPham = _danhSachSanPham
        };

        _chiTietList.Add(dongMoi);

        // Reset ô tìm kiếm
        SanPhamAutoCompleteBox.Text = "";
        _sanPhamDangChon = null;

        CapNhatTrangThaiBang();
    }

    private void XoaDongChiTiet_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChiTietKhuyenMai dong)
        {
            _chiTietList.Remove(dong);

            // Đánh lại số thứ tự
            for (int i = 0; i < _chiTietList.Count; i++)
            {
                _chiTietList[i].Stt = i + 1;
            }

            CapNhatTrangThaiBang();
        }
    }

    private void XoaTatCa_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_chiTietList.Count == 0)
            return;

        _chiTietList.Clear();
        CapNhatTrangThaiBang();
    }

    private void GiamNhanhApply_OnClick(object? sender, RoutedEventArgs e)
    {
        var raw = (GiamNhanhPhanTramTextBox.Text ?? "").Trim();
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var pt))
        {
            pt = Math.Clamp(pt, 0, 100);
            foreach (var dong in _chiTietList)
            {
                dong.LoaiGiamGia = "PHAN_TRAM";
                dong.GiaTriGiam = pt;
            }
        }
    }

    private void OnChiTietNumberLostFocus(object? sender, RoutedEventArgs e)
    {
        // Khi người dùng nhập xong số lượng mua, mức giảm hoặc số lượng tặng,
        // đảm bảo dữ liệu không âm và giới hạn hợp lý
        foreach (var item in _chiTietList)
        {
            if (item.SoLuongMua <= 0)
                item.SoLuongMua = 1;

            if (item.GiaTriGiam < 0)
                item.GiaTriGiam = 0;

            if (item.LoaiGiamGia == "PHAN_TRAM" && item.GiaTriGiam > 100)
                item.GiaTriGiam = 100;

            if (item.SoLuongTang < 0)
                item.SoLuongTang = 0;

            // Không có hàng tặng thì không được để lại số lượng tặng
            if (item.SanPhamTangId is null or 0)
                item.SoLuongTang = 0;
        }
    }

    private void CapNhatTrangThaiBang()
    {
        var soLuong = _chiTietList.Count;
        SoLuongHangText.Text = $"{soLuong:N0} hàng hóa";
        EmptyStateBorder.IsVisible = soLuong == 0;
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangLuu)
            return;

        AnThongBao();

        // 1. Kiểm tra mã chương trình
        var ma = (MaChuongTrinhTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(ma))
        {
            HienThongBao("Vui lòng nhập mã chương trình khuyến mãi.", isError: true);
            MaChuongTrinhTextBox.Focus();
            return;
        }

        // 2. Kiểm tra tên chương trình
        var ten = (TenChuongTrinhTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(ten))
        {
            HienThongBao("Vui lòng nhập tên chương trình khuyến mãi.", isError: true);
            TenChuongTrinhTextBox.Focus();
            return;
        }

        // 3. Kiểm tra ngày bắt đầu & kết thúc
        if (!NgayBatDauPicker.SelectedDate.HasValue)
        {
            HienThongBao("Vui lòng chọn ngày bắt đầu khuyến mãi.", isError: true);
            NgayBatDauPicker.Focus();
            return;
        }

        if (!NgayKetThucPicker.SelectedDate.HasValue)
        {
            HienThongBao("Vui lòng chọn ngày kết thúc khuyến mãi.", isError: true);
            NgayKetThucPicker.Focus();
            return;
        }

        var ngayBatDau = NgayBatDauPicker.SelectedDate.Value;
        var ngayKetThuc = NgayKetThucPicker.SelectedDate.Value;

        if (ngayKetThuc < ngayBatDau)
        {
            HienThongBao("Ngày kết thúc không được nhỏ hơn ngày bắt đầu khuyến mãi.", isError: true);
            NgayKetThucPicker.Focus();
            return;
        }

        // 4. Kiểm tra điều kiện theo loại khuyến mãi
        var loaiKhuyenMai = GetLoaiKhuyenMaiHienTai();

        if (string.Equals(loaiKhuyenMai, KhuyenMaiLoai.TheoDonHang, StringComparison.OrdinalIgnoreCase))
        {
            // UI controls chưa có trong XAML hiện tại -> bỏ qua validate chi tiết, dùng giá trị mặc định
        }
        else
        {
            if (_chiTietList.Count == 0)
            {
                HienThongBao("Chương trình khuyến mãi cần có ít nhất một sản phẩm áp dụng.", isError: true);
                SanPhamAutoCompleteBox.Focus();
                return;
            }

            foreach (var item in _chiTietList)
            {
                if (item.SoLuongMua <= 0)
                    item.SoLuongMua = 1;

                if (string.Equals(loaiKhuyenMai, KhuyenMaiLoai.TangQua, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.SanPhamTangId is uint idTang && idTang > 0)
                    {
                        if (idTang == item.SanPhamId)
                        {
                            HienThongBao($"Sản phẩm [{item.MaSanPham}] không thể tặng kèm chính nó.", isError: true);
                            return;
                        }
                        if (item.SoLuongTang <= 0)
                        {
                            HienThongBao($"Sản phẩm [{item.MaSanPham}] chưa nhập số lượng tặng.", isError: true);
                            return;
                        }
                    }
                    else
                    {
                        HienThongBao($"Chương trình tặng quà: sản phẩm [{item.MaSanPham}] chưa chọn sản phẩm tặng.", isError: true);
                        return;
                    }
                }
                else
                {
                    if (item.LoaiGiamGia == "PHAN_TRAM" && item.GiaTriGiam > 100)
                    {
                        HienThongBao($"Mức giảm % của sản phẩm [{item.MaSanPham}] không được vượt quá 100%.", isError: true);
                        return;
                    }
                    if (item.GiaTriGiam <= 0)
                    {
                        HienThongBao($"Sản phẩm [{item.MaSanPham}] chưa nhập mức giảm hợp lệ.", isError: true);
                        return;
                    }
                }
            }
        }

        // 6. Kiểm tra trùng mã trong cơ sở dữ liệu
        try
        {
            var trungMa = await _db.KiemTraMaKhuyenMaiTonTaiAsync(ma, _chuongTrinhEdit?.Id);
            if (trungMa)
            {
                HienThongBao($"Mã chương trình [{ma}] đã tồn tại trong hệ thống. Vui lòng chọn mã khác.", isError: true);
                MaChuongTrinhTextBox.Focus();
                return;
            }
        }
        catch (Exception ex)
        {
            HienThongBao("Lỗi kiểm tra trùng mã: " + ex.Message, isError: true);
            return;
        }

        // 7. Chuẩn bị đối tượng lưu
        var trangThai = "DANG_HOAT_DONG";
        if (TrangThaiComboBox.SelectedItem is ComboBoxItem cbiTrangThai && cbiTrangThai.Tag != null)
        {
            trangThai = cbiTrangThai.Tag.ToString() ?? "DANG_HOAT_DONG";
        }

        decimal giaTriDonToiThieuLuu = 0;
        string loaiGiamGiaLuu = "PHAN_TRAM";
        decimal giaTriGiamLuu = 0;

        var ct = new ChuongTrinhKhuyenMai
        {
            Id = _chuongTrinhEdit?.Id ?? 0,
            MaChuongTrinh = ma,
            TenChuongTrinh = ten,
            NgayBatDau = ngayBatDau,
            NgayKetThuc = ngayKetThuc,
            TrangThai = trangThai,
            LoaiKhuyenMai = loaiKhuyenMai,
            GiaTriDonToiThieu = Math.Max(0, giaTriDonToiThieuLuu),
            LoaiGiamGia = loaiGiamGiaLuu,
            GiaTriGiam = Math.Max(0, giaTriGiamLuu),
            GhiChu = (GhiChuTextBox.Text ?? "").Trim()
        };

        // 8. Thực hiện lưu bằng transaction an toàn
        try
        {
            _dangLuu = true;
            SaveButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            SaveButton.Content = "⏳ Đang lưu...";

            IReadOnlyList<ChiTietKhuyenMai> chiTietLuu = string.Equals(loaiKhuyenMai, KhuyenMaiLoai.TheoDonHang, StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<ChiTietKhuyenMai>()
                : _chiTietList.ToList();

            var id = await _db.LuuChuongTrinhKhuyenMaiAsync(ct, chiTietLuu);

            if (id > 0)
            {
                ct.Id = id;
                foreach (var d in chiTietLuu)
                {
                    d.ChuongTrinhId = id;
                    ct.DanhSachChiTiet.Add(d);
                }

                KetQuaLuu = ct;
                Close(true);
            }
            else
            {
                HienThongBao("Không thể lưu chương trình khuyến mãi vào cơ sở dữ liệu. Vui lòng thử lại.", isError: true);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lưu chương trình khuyến mãi", ex);
            HienThongBao("Lỗi khi lưu chương trình khuyến mãi: " + ex.Message, isError: true);
        }
        finally
        {
            _dangLuu = false;
            SaveButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            SaveButton.Content = _chuongTrinhEdit is null ? "💾  Lưu chương trình" : "💾  Cập nhật chương trình";
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void CloseWindowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void HienThongBao(string message, bool isError)
    {
        NotificationBorder.IsVisible = true;
        NotificationTextBlock.Text = message;

        if (isError)
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#FEE2E2"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FCA5A5"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#991B1B"));
            NotificationIconText.Text = "❌";
        }
        else
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#EFF6FF"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#BFDBFE"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#1E40AF"));
            NotificationIconText.Text = "ℹ️";
        }
    }

    private void AnThongBao()
    {
        NotificationBorder.IsVisible = false;
        NotificationTextBlock.Text = "";
    }
}
