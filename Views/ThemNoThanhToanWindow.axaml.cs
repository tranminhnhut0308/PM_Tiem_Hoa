using System;
using System.Collections.Generic;
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

public partial class ThemNoThanhToanWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<DoiTacOption> _danhSachDoiTac = new();
    private readonly List<ChungTuNoOption> _danhSachChungTu = new();
    private DoiTacOption? _doiTacDaChon;
    private ChungTuNoOption? _chungTuDaChon;
    private bool _dangLuu;
    private bool _dangFormatTien;
    private bool _dangTaiDuLieu;

    /// <summary>
    /// Đánh dấu đã thực hiện ít nhất 1 lần thanh toán thành công trong phiên mở cửa sổ.
    /// </summary>
    public bool DaThanhToanThanhCong { get; private set; }

    public ThemNoThanhToanWindow() : this(null, null, null)
    {
    }

    public ThemNoThanhToanWindow(string? loaiDoiTacMacDinh = null, uint? doiTacIdMacDinh = null, string? maChungTuMacDinh = null)
    {
        InitializeComponent();

        Loaded += async (_, _) => await KhoiTaoDuLieuAsync(loaiDoiTacMacDinh, doiTacIdMacDinh, maChungTuMacDinh);
    }

    private async Task KhoiTaoDuLieuAsync(string? loaiDoiTacMacDinh, uint? doiTacIdMacDinh, string? maChungTuMacDinh)
    {
        try
        {
            NgayThanhToanPicker.SelectedDate = DateTime.Today;

            var loai = string.Equals(loaiDoiTacMacDinh, "NHA_CUNG_CAP", StringComparison.OrdinalIgnoreCase)
                ? "NHA_CUNG_CAP"
                : "KHACH_HANG";

            for (int i = 0; i < LoaiDoiTacComboBox.ItemCount; i++)
            {
                if (LoaiDoiTacComboBox.Items[i] is ComboBoxItem cbi &&
                    string.Equals(cbi.Tag?.ToString(), loai, StringComparison.OrdinalIgnoreCase))
                {
                    LoaiDoiTacComboBox.SelectedIndex = i;
                    break;
                }
            }

            await TaiDanhSachDoiTacAsync(loai, doiTacIdMacDinh, maChungTuMacDinh);
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể khởi tạo dữ liệu: " + ex.Message, isError: true);
        }
    }

    private string LayLoaiDoiTacHienTai()
    {
        if (LoaiDoiTacComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            return item.Tag.ToString() ?? "KHACH_HANG";
        }
        return "KHACH_HANG";
    }

    private string LayPhuongThucHienTai()
    {
        if (PhuongThucComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            return item.Tag.ToString() ?? "TIEN_MAT";
        }
        return "TIEN_MAT";
    }

    private async Task TaiDanhSachDoiTacAsync(string loaiDoiTac, uint? doiTacIdMacDinh = null, string? maChungTuMacDinh = null)
    {
        if (_dangTaiDuLieu)
            return;

        _dangTaiDuLieu = true;
        try
        {
            var ds = await _db.GetDanhSachDoiTacCongNoAsync(loaiDoiTac);

            _danhSachDoiTac.Clear();
            _danhSachDoiTac.AddRange(ds);

            MaDoiTacAutoCompleteBox.ItemsSource = null;
            MaDoiTacAutoCompleteBox.ItemsSource = _danhSachDoiTac;

            MaDoiTacAutoCompleteBox.ItemFilter = (search, item) =>
            {
                if (string.IsNullOrWhiteSpace(search))
                    return true;

                if (item is DoiTacOption dt)
                {
                    var ma = dt.MaDoiTac ?? "";
                    var ten = dt.TenDoiTac ?? "";
                    var sdt = dt.SoDienThoai ?? "";

                    return ma.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           ten.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           sdt.Contains(search, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            };

            if (doiTacIdMacDinh.HasValue && doiTacIdMacDinh.Value > 0)
            {
                var dt = _danhSachDoiTac.FirstOrDefault(d => d.Id == doiTacIdMacDinh.Value);
                if (dt != null)
                {
                    await ChonDoiTacAsync(dt, maChungTuMacDinh);
                }
            }
        }
        catch (Exception ex)
        {
            HienThongBao("Không thể tải danh sách đối tác: " + ex.Message, isError: true);
        }
        finally
        {
            _dangTaiDuLieu = false;
        }
    }

    private async void LoaiDoiTacComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _doiTacDaChon = null;
        _chungTuDaChon = null;

        if (MaDoiTacAutoCompleteBox != null)
            MaDoiTacAutoCompleteBox.Text = "";
        if (TenDoiTacTextBox != null)
            TenDoiTacTextBox.Text = "";

        ResetThongTinNo();
        AnThongBao();

        var loai = LayLoaiDoiTacHienTai();
        await TaiDanhSachDoiTacAsync(loai);
    }

    private async Task ChonDoiTacAsync(DoiTacOption dt, string? maChungTuMacDinh = null)
    {
        _doiTacDaChon = dt;
        MaDoiTacAutoCompleteBox.Text = dt.HienThiGoiY;
        TenDoiTacTextBox.Text = string.IsNullOrWhiteSpace(dt.SoDienThoai)
            ? dt.TenDoiTac
            : $"{dt.TenDoiTac} - SĐT: {dt.SoDienThoai}";

        AnThongBao();

        // Tải danh sách chứng từ nợ của đối tác này
        try
        {
            var dsChungTu = await _db.GetChungTuNoCuaDoiTacAsync(dt.LoaiDoiTac, dt.Id);
            _danhSachChungTu.Clear();
            _danhSachChungTu.AddRange(dsChungTu);

            ChungTuComboBox.Items.Clear();
            ChungTuComboBox.Items.Add(new ComboBoxItem
            {
                Content = "[Tất cả] Phân bổ tự động từ chứng từ nợ cũ nhất",
                Tag = "[ALL]",
                IsSelected = true
            });

            int selectedIndex = 0;
            for (int i = 0; i < _danhSachChungTu.Count; i++)
            {
                var ct = _danhSachChungTu[i];
                var cbi = new ComboBoxItem
                {
                    Content = ct.HienThiGoiY,
                    Tag = ct.MaChungTu
                };
                ChungTuComboBox.Items.Add(cbi);

                if (!string.IsNullOrWhiteSpace(maChungTuMacDinh) &&
                    string.Equals(ct.MaChungTu, maChungTuMacDinh.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1; // Bù 1 do mục [Tất cả]
                }
            }

            ChungTuComboBox.SelectedIndex = selectedIndex;

            // Cập nhật tổng nợ và đề xuất số tiền thanh toán
            CapNhatSoLieuTheoChungTu();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải chứng từ nợ của đối tác", ex);
        }
    }

    private void ChungTuComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CapNhatSoLieuTheoChungTu();
    }

    private void CapNhatSoLieuTheoChungTu()
    {
        if (_doiTacDaChon == null)
        {
            ResetThongTinNo();
            return;
        }

        string tag = "";
        if (ChungTuComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            tag = item.Tag.ToString() ?? "";
        }

        decimal tongNoHienTai;
        if (string.IsNullOrEmpty(tag) || tag == "[ALL]")
        {
            _chungTuDaChon = null;
            // Nếu có danh sách chứng từ, tổng nợ = tổng con_no các chứng từ (hoặc dt.CongNoHienTai nếu lớn hơn)
            decimal tongChungTu = _danhSachChungTu.Sum(c => c.ConNo);
            tongNoHienTai = Math.Max(tongChungTu, _doiTacDaChon.CongNoHienTai);
            if (tongNoHienTai < 0) tongNoHienTai = 0;
        }
        else
        {
            _chungTuDaChon = _danhSachChungTu.FirstOrDefault(c => c.MaChungTu == tag);
            tongNoHienTai = _chungTuDaChon?.ConNo ?? 0m;
        }

        TxtTongNoHienTai.Text = $"{tongNoHienTai:N0} đ";

        // Mặc định điền số tiền thanh toán bằng tổng nợ
        _dangFormatTien = true;
        SoTienTextBox.Text = MoneyInput.Format(tongNoHienTai);
        _dangFormatTien = false;

        CapNhatConLaiCard(tongNoHienTai, tongNoHienTai);
    }

    private void CapNhatConLaiCard(decimal tongNo, decimal soTienTra)
    {
        TxtSoTienThanhToanCard.Text = $"{soTienTra:N0} đ";
        var conLai = Math.Max(0m, tongNo - soTienTra);
        TxtConLaiSauTra.Text = $"{conLai:N0} đ";
    }

    private void ResetThongTinNo()
    {
        TxtTongNoHienTai.Text = "0 đ";
        TxtSoTienThanhToanCard.Text = "0 đ";
        TxtConLaiSauTra.Text = "0 đ";
        SoTienTextBox.Text = "0";

        ChungTuComboBox.Items.Clear();
        ChungTuComboBox.Items.Add(new ComboBoxItem
        {
            Content = "[Tất cả] Phân bổ tự động từ chứng từ nợ cũ nhất",
            Tag = "[ALL]",
            IsSelected = true
        });
    }

    private async void MaDoiTacAutoCompleteBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MaDoiTacAutoCompleteBox.SelectedItem is DoiTacOption dt)
        {
            await ChonDoiTacAsync(dt);
        }
    }

    private async void MaDoiTacAutoCompleteBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var text = (MaDoiTacAutoCompleteBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _doiTacDaChon = null;
            TenDoiTacTextBox.Text = "";
            ResetThongTinNo();
            AnThongBao();
            return;
        }

        if (_doiTacDaChon != null &&
            (string.Equals(_doiTacDaChon.MaDoiTac, text, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(_doiTacDaChon.HienThiGoiY, text, StringComparison.OrdinalIgnoreCase)))
        {
            AnThongBao();
            return;
        }

        var khop = _danhSachDoiTac.FirstOrDefault(d =>
            string.Equals(d.MaDoiTac, text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.TenDoiTac, text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.HienThiGoiY, text, StringComparison.OrdinalIgnoreCase));

        if (khop != null)
        {
            await ChonDoiTacAsync(khop);
        }
        else
        {
            _doiTacDaChon = null;
            TenDoiTacTextBox.Text = "";
            ResetThongTinNo();
            HienThongBao("Đối tác không tồn tại hoặc không khớp loại đã chọn. Vui lòng chọn đối tác từ danh sách gợi ý.", isError: true);
        }
    }

    private void SoTienTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dangFormatTien)
            return;

        FormatTienRealtime();
    }

    private void SoTienTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        FormatTienRealtime();
    }

    private void FormatTienRealtime()
    {
        if (_dangFormatTien)
            return;

        _dangFormatTien = true;
        try
        {
            var raw = (SoTienTextBox.Text ?? "").Trim();
            var tien = DocTien(raw);

            if (tien < 0)
                tien = 0;

            SoTienTextBox.Text = MoneyInput.Format(tien);

            decimal tongNo = DocTien(TxtTongNoHienTai.Text);
            CapNhatConLaiCard(tongNo, tien);
        }
        finally
        {
            _dangFormatTien = false;
        }
    }

    private static decimal DocTien(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        if (MoneyInput.TryParse(text, batBuoc: false, out var val, out _))
            return val;

        var clean = text.Replace(".", "")
                        .Replace(",", "")
                        .Replace(" ", "")
                        .Replace("đ", "")
                        .Replace("₫", "")
                        .Trim();

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var fallbackVal))
        {
            return fallbackVal;
        }

        return 0m;
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ThucHienLuuThanhToanAsync(dongCuaSoSauKhiLuu: true);
    }

    private async void SaveAndNewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ThucHienLuuThanhToanAsync(dongCuaSoSauKhiLuu: false);
    }

    private async Task ThucHienLuuThanhToanAsync(bool dongCuaSoSauKhiLuu)
    {
        if (_dangLuu)
            return;

        AnThongBao();

        // 1. Kiểm tra đối tác
        if (_doiTacDaChon == null)
        {
            HienThongBao("Vui lòng chọn đối tượng công nợ hợp lệ từ danh sách gợi ý.", isError: true);
            MaDoiTacAutoCompleteBox.Focus();
            return;
        }

        // 2. Kiểm tra quyền
        var loaiDoiTac = LayLoaiDoiTacHienTai();
        if (loaiDoiTac == "KHACH_HANG")
        {
            if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CustomersDebtPay) && !Quyen.IsAdmin(Session.CurrentUser))
            {
                HienThongBao("Bạn không có quyền thu công nợ khách hàng (PermissionKeys.CustomersDebtPay).", isError: true);
                return;
            }
        }
        else
        {
            if (!Quyen.HasPermission(Session.CurrentUser, PermissionKeys.SuppliersDebtPay) && !Quyen.IsAdmin(Session.CurrentUser))
            {
                HienThongBao("Bạn không có quyền thanh toán công nợ nhà cung cấp (PermissionKeys.SuppliersDebtPay).", isError: true);
                return;
            }
        }

        // 3. Kiểm tra số tiền
        var soTien = DocTien(SoTienTextBox.Text);
        if (soTien <= 0)
        {
            HienThongBao("Số tiền thanh toán phải lớn hơn 0.", isError: true);
            SoTienTextBox.Focus();
            return;
        }

        decimal tongNo = DocTien(TxtTongNoHienTai.Text);
        if (soTien > tongNo && tongNo > 0)
        {
            HienThongBao($"Số tiền thanh toán ({soTien:N0} đ) vượt quá tổng nợ hiện tại ({tongNo:N0} đ). Vui lòng nhập số tiền không vượt quá dư nợ.", isError: true);
            SoTienTextBox.Focus();
            return;
        }

        _dangLuu = true;
        SaveButton.IsEnabled = false;
        SaveAndNewButton.IsEnabled = false;
        SaveButton.Content = "⏳ Đang lưu...";

        try
        {
            string? maChungTu = null;
            if (ChungTuComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var tag = item.Tag.ToString();
                if (!string.IsNullOrEmpty(tag) && tag != "[ALL]")
                {
                    maChungTu = tag;
                }
            }

            var yeuCau = new ThanhToanNoYeuCau
            {
                LoaiDoiTac = loaiDoiTac,
                DoiTacId = _doiTacDaChon.Id,
                MaDoiTac = _doiTacDaChon.MaDoiTac,
                TenDoiTac = _doiTacDaChon.TenDoiTac,
                MaChungTu = maChungTu,
                NgayThanhToan = NgayThanhToanPicker.SelectedDate ?? DateTime.Today,
                SoTien = soTien,
                PhuongThuc = LayPhuongThucHienTai(),
                GhiChu = string.IsNullOrWhiteSpace(NoiDungTextBox.Text) ? null : NoiDungTextBox.Text.Trim()
            };

            var thanhCong = await _db.ThucHienThanhToanCongNoAsync(yeuCau);
            if (thanhCong)
            {
                DaThanhToanThanhCong = true;

                if (dongCuaSoSauKhiLuu)
                {
                    Close(true);
                }
                else
                {
                    // Reset form để thêm tiếp
                    HienThongBao($"✅ Đã ghi nhận thanh toán {soTien:N0} đ thành công cho {_doiTacDaChon.TenDoiTac}. Bạn có thể tiếp tục thêm thanh toán mới.", isError: false);
                    _doiTacDaChon = null;
                    _chungTuDaChon = null;
                    MaDoiTacAutoCompleteBox.Text = "";
                    TenDoiTacTextBox.Text = "";
                    NoiDungTextBox.Text = "";
                    ResetThongTinNo();
                    await TaiDanhSachDoiTacAsync(loaiDoiTac);
                    MaDoiTacAutoCompleteBox.Focus();
                }
            }
        }
        catch (Exception ex)
        {
            HienThongBao("Lỗi khi lưu thanh toán công nợ: " + ex.Message, isError: true);
            SafeLog.WriteError("Lỗi ThemNoThanhToanWindow", ex);
        }
        finally
        {
            _dangLuu = false;
            SaveButton.IsEnabled = true;
            SaveAndNewButton.IsEnabled = true;
            SaveButton.Content = "💾  Lưu thanh toán";
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(DaThanhToanThanhCong);
    }

    private void CloseWindowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(DaThanhToanThanhCong);
    }

    private void HienThongBao(string thongDiep, bool isError)
    {
        NotificationBorder.IsVisible = true;
        NotificationTextBlock.Text = thongDiep;

        if (isError)
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#FEE4E2"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#FECDCA"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#B42318"));
            NotificationIconText.Text = "❌";
        }
        else
        {
            NotificationBorder.Background = new SolidColorBrush(Color.Parse("#ECFDF3"));
            NotificationBorder.BorderBrush = new SolidColorBrush(Color.Parse("#ABEFC6"));
            NotificationTextBlock.Foreground = new SolidColorBrush(Color.Parse("#067647"));
            NotificationIconText.Text = "✅";
        }
    }

    private void AnThongBao()
    {
        NotificationBorder.IsVisible = false;
        NotificationTextBlock.Text = "";
    }
}
