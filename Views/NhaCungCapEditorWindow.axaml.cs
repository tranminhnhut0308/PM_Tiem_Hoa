using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ thêm mới và chỉnh sửa nhà cung cấp theo chuẩn giao diện hiện đại WINWON.
/// </summary>
public partial class NhaCungCapEditorWindow : Window, INotifyPropertyChanged
{
    private readonly DatabaseService _db = new();

    public NhaCungCap Supplier { get; }

    public bool IsEditMode { get; }

    public string TieuDe => IsEditMode ? "Cập nhật thông tin nhà cung cấp - WINWON" : "Thêm nhà cung cấp mới - WINWON";

    public string HeaderText => IsEditMode ? "CẬP NHẬT NHÀ CUNG CẤP" : "THÊM NHÀ CUNG CẤP MỚI";

    public string BreadcrumbCurrentText => IsEditMode ? "Cập nhật nhà cung cấp" : "Thêm nhà cung cấp";

    public bool CoTheThietLapGia => IsEditMode && Supplier.Id > 0;

    private string _diaChiDayDuPreview = "(Chưa nhập địa chỉ)";
    public string DiaChiDayDuPreview
    {
        get => _diaChiDayDuPreview;
        set => SetField(ref _diaChiDayDuPreview, value);
    }

    private bool _coLoi;
    public bool CoLoi
    {
        get => _coLoi;
        set => SetField(ref _coLoi, value);
    }

    private string _loiMessage = "";
    public string LoiMessage
    {
        get => _loiMessage;
        set
        {
            if (SetField(ref _loiMessage, value))
            {
                CoLoi = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    public string StatusBadgeText => IsEditMode ? $"NCC ID: #{Supplier.Id}" : "Bản ghi mới";

    public IBrush StatusBadgeBackground => IsEditMode
        ? new SolidColorBrush(Color.Parse("#E8F4FD"))
        : new SolidColorBrush(Color.Parse("#E6F4EA"));

    public IBrush StatusBadgeForeground => IsEditMode
        ? new SolidColorBrush(Color.Parse("#0E5E9E"))
        : new SolidColorBrush(Color.Parse("#137333"));

    public ObservableCollection<string> DanhSachNhomNCC { get; } = new();

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public NhaCungCapEditorWindow(NhaCungCap? supplier)
    {
        IsEditMode = supplier is not null && supplier.Id > 0;
        Supplier = SaoChep(supplier);

        InitializeComponent();
        DataContext = this;

        // Cập nhật preview địa chỉ ban đầu
        CapNhatDiaChiDayDuPreview();

        // Tải danh sách nhóm gợi ý và sinh mã nếu cần
        _ = KhoiTaoDuLieuAsync();
    }

    private async Task KhoiTaoDuLieuAsync()
    {
        try
        {
            // 1. Gợi ý các nhóm nhà cung cấp mặc định
            var nhomMacDinh = new[]
            {
                "Vườn hoa Đà Lạt",
                "Hoa nhập khẩu cao cấp",
                "Phụ liệu cắm hoa & Xốp lẵng",
                "Chợ đầu mối hoa tươi",
                "Vườn lan & Cây kiểng",
                "Bao bì, ruy băng & Giấy gói"
            };

            foreach (var item in nhomMacDinh)
            {
                if (!DanhSachNhomNCC.Contains(item))
                    DanhSachNhomNCC.Add(item);
            }

            // 2. Lấy thêm các nhóm thực tế từ database
            var nhomTuDb = await _db.LayDanhSachNhomNhaCungCapAsync();
            foreach (var nhom in nhomTuDb)
            {
                if (!string.IsNullOrWhiteSpace(nhom) && !DanhSachNhomNCC.Contains(nhom))
                    DanhSachNhomNCC.Add(nhom);
            }

            // 3. Nếu là thêm mới và mã đang trống, tự động sinh mã
            if (!IsEditMode && string.IsNullOrWhiteSpace(Supplier.MaNhaCungCap))
            {
                var maMoi = await _db.TaoMaNhaCungCapMoiAsync();
                Supplier.MaNhaCungCap = maMoi;
                var maBox = this.FindControl<TextBox>("MaNhaCungCapBox");
                if (maBox != null)
                    maBox.Text = maMoi;
            }
        }
        catch
        {
            // Không làm gián đoạn hiển thị form nếu kết nối gợi ý lỗi
        }
    }

    private static NhaCungCap SaoChep(NhaCungCap? supplier)
    {
        if (supplier is null)
        {
            return new NhaCungCap
            {
                Id = 0,
                MaNhaCungCap = "",
                TenNhaCungCap = "",
                TrangThai = true,
                ConHopTac = true,
                NgayTao = DateTime.Now
            };
        }

        return new NhaCungCap
        {
            Id = supplier.Id,
            MaNhaCungCap = supplier.MaNhaCungCap ?? "",
            TenNhaCungCap = supplier.TenNhaCungCap ?? "",
            TenCongTy = supplier.TenCongTy,
            NhomNhaCungCapTen = supplier.NhomNhaCungCapTen,
            NhomNhaCungCapId = supplier.NhomNhaCungCapId,
            NguoiLienHe = supplier.NguoiLienHe,
            MaSoThue = supplier.MaSoThue,
            SoDienThoai = supplier.SoDienThoai,
            Email = supplier.Email,
            DiaChi = supplier.DiaChi,
            PhuongXa = supplier.PhuongXa,
            QuanHuyen = supplier.QuanHuyen,
            TinhThanh = supplier.TinhThanh,
            SoTaiKhoan = supplier.SoTaiKhoan,
            NganHang = supplier.NganHang,
            CongNoGoiDau = supplier.CongNoGoiDau,
            NhanHieu = supplier.NhanHieu,
            ChietKhau = supplier.ChietKhau,
            GhiChu = supplier.GhiChu,
            TrangThai = supplier.TrangThai,
            ConHopTac = supplier.ConHopTac,
            ConNo = supplier.ConNo,
            TongMua = supplier.TongMua,
            TongMuaTruTraHang = supplier.TongMuaTruTraHang,
            NgayTao = supplier.NgayTao
        };
    }

    private void CapNhatDiaChiDayDuPreview()
    {
        var diaChiChiTiet = this.FindControl<TextBox>("DiaChiChiTietBox")?.Text?.Trim() ?? Supplier.DiaChi?.Trim();
        var phuongXa = this.FindControl<TextBox>("PhuongXaBox")?.Text?.Trim() ?? Supplier.PhuongXa?.Trim();
        var quanHuyen = this.FindControl<TextBox>("QuanHuyenBox")?.Text?.Trim() ?? Supplier.QuanHuyen?.Trim();
        var tinhThanh = this.FindControl<TextBox>("TinhThanhBox")?.Text?.Trim() ?? Supplier.TinhThanh?.Trim();

        var parts = new[] { diaChiChiTiet, phuongXa, quanHuyen, tinhThanh }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();

        DiaChiDayDuPreview = parts.Count > 0
            ? string.Join(", ", parts)
            : "(Chưa nhập địa chỉ)";
    }

    private void OnDiaChiComponentChanged(object? sender, KeyEventArgs e)
    {
        CapNhatDiaChiDayDuPreview();
    }

    private void OnDiaChiComponentLostFocus(object? sender, RoutedEventArgs e)
    {
        CapNhatDiaChiDayDuPreview();
    }

    private async void OnSinhMaClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var maMoi = await _db.TaoMaNhaCungCapMoiAsync();
            Supplier.MaNhaCungCap = maMoi;
            var maBox = this.FindControl<TextBox>("MaNhaCungCapBox");
            if (maBox != null)
                maBox.Text = maMoi;
        }
        catch (Exception ex)
        {
            HienLoi("Lỗi sinh mã tự động: " + ex.Message);
        }
    }

    private void HienLoi(string message)
    {
        LoiMessage = message;
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        HienLoi("");

        // 1. Kiểm tra Tên nhà cung cấp (bắt buộc)
        var tenBox = this.FindControl<TextBox>("TenNhaCungCapBox");
        Supplier.TenNhaCungCap = tenBox?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(Supplier.TenNhaCungCap))
        {
            HienLoi("Vui lòng nhập tên nhà cung cấp (trường bắt buộc).");
            tenBox?.Focus();
            return;
        }

        // 2. Kiểm tra Mã nhà cung cấp
        var maBox = this.FindControl<TextBox>("MaNhaCungCapBox");
        var maNhap = maBox?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(maNhap))
        {
            try
            {
                maNhap = await _db.TaoMaNhaCungCapMoiAsync();
                Supplier.MaNhaCungCap = maNhap;
                if (maBox != null) maBox.Text = maNhap;
            }
            catch (Exception ex)
            {
                HienLoi("Không thể tạo mã nhà cung cấp tự động: " + ex.Message);
                return;
            }
        }
        else
        {
            Supplier.MaNhaCungCap = maNhap;
            try
            {
                var daTonTai = await _db.KiemTraTrungMaNhaCungCapAsync(Supplier.MaNhaCungCap, Supplier.Id);
                if (daTonTai)
                {
                    HienLoi($"Mã nhà cung cấp '{Supplier.MaNhaCungCap}' đã được sử dụng bởi nhà cung cấp khác. Vui lòng chọn mã khác.");
                    maBox?.Focus();
                    return;
                }
            }
            catch (Exception ex)
            {
                HienLoi("Lỗi kiểm tra trùng mã nhà cung cấp: " + ex.Message);
                return;
            }
        }

        // 3. Kiểm tra Số điện thoại nếu có nhập
        var sdtBox = this.FindControl<TextBox>("SoDienThoaiBox");
        var sdt = sdtBox?.Text?.Trim() ?? "";
        Supplier.SoDienThoai = string.IsNullOrWhiteSpace(sdt) ? null : sdt;

        if (!string.IsNullOrWhiteSpace(Supplier.SoDienThoai))
        {
            var digitsOnly = new string(Supplier.SoDienThoai.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 8 || digitsOnly.Length > 15)
            {
                HienLoi("Số điện thoại liên hệ không hợp lệ (cần từ 8 đến 15 chữ số).");
                sdtBox?.Focus();
                return;
            }
        }

        // 4. Kiểm tra Email nếu có nhập
        var emailBox = this.FindControl<TextBox>("EmailBox");
        var email = emailBox?.Text?.Trim() ?? "";
        Supplier.Email = string.IsNullOrWhiteSpace(email) ? null : email;

        if (!string.IsNullOrWhiteSpace(Supplier.Email))
        {
            if (!Supplier.Email.Contains('@') || !Supplier.Email.Contains('.') || Supplier.Email.IndexOf('@') > Supplier.Email.LastIndexOf('.'))
            {
                HienLoi("Địa chỉ email không đúng định dạng (ví dụ: contact@nhavuon.vn).");
                emailBox?.Focus();
                return;
            }
        }

        // 5. Chuẩn hóa các trường thông tin địa chỉ
        var diaChiChiTietBox = this.FindControl<TextBox>("DiaChiChiTietBox");
        var phuongXaBox = this.FindControl<TextBox>("PhuongXaBox");
        var quanHuyenBox = this.FindControl<TextBox>("QuanHuyenBox");
        var tinhThanhBox = this.FindControl<TextBox>("TinhThanhBox");

        Supplier.DiaChi = string.IsNullOrWhiteSpace(diaChiChiTietBox?.Text) ? null : diaChiChiTietBox.Text.Trim();
        Supplier.PhuongXa = string.IsNullOrWhiteSpace(phuongXaBox?.Text) ? null : phuongXaBox.Text.Trim();
        Supplier.QuanHuyen = string.IsNullOrWhiteSpace(quanHuyenBox?.Text) ? null : quanHuyenBox.Text.Trim();
        Supplier.TinhThanh = string.IsNullOrWhiteSpace(tinhThanhBox?.Text) ? null : tinhThanhBox.Text.Trim();

        // 6. Chuẩn hóa các trường chuỗi còn lại
        Supplier.TenCongTy = string.IsNullOrWhiteSpace(Supplier.TenCongTy) ? null : Supplier.TenCongTy.Trim();
        var nhomBox = this.FindControl<AutoCompleteBox>("NhomNhaCungCapBox");
        Supplier.NhomNhaCungCapTen = string.IsNullOrWhiteSpace(nhomBox?.Text) ? null : nhomBox.Text.Trim();
        Supplier.NguoiLienHe = string.IsNullOrWhiteSpace(Supplier.NguoiLienHe) ? null : Supplier.NguoiLienHe.Trim();
        Supplier.MaSoThue = string.IsNullOrWhiteSpace(Supplier.MaSoThue) ? null : Supplier.MaSoThue.Trim();
        Supplier.SoTaiKhoan = string.IsNullOrWhiteSpace(Supplier.SoTaiKhoan) ? null : Supplier.SoTaiKhoan.Trim();
        Supplier.NganHang = string.IsNullOrWhiteSpace(Supplier.NganHang) ? null : Supplier.NganHang.Trim();
        Supplier.NhanHieu = string.IsNullOrWhiteSpace(Supplier.NhanHieu) ? null : Supplier.NhanHieu.Trim();
        Supplier.GhiChu = string.IsNullOrWhiteSpace(Supplier.GhiChu) ? null : Supplier.GhiChu.Trim();

        // 7. Parse số tiền Hạn mức nợ / C.Nợ gối đầu
        var congNoBox = this.FindControl<TextBox>("CongNoGoiDauBox");
        if (congNoBox is not null && !string.IsNullOrWhiteSpace(congNoBox.Text))
        {
            var raw = congNoBox.Text.Replace(",", "").Replace(".", "").Trim();
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var congNo))
                Supplier.CongNoGoiDau = congNo;
            else
                Supplier.CongNoGoiDau = 0;
        }
        else
        {
            Supplier.CongNoGoiDau = 0;
        }

        // 8. Parse Chiết khấu (%)
        var chietKhauBox = this.FindControl<TextBox>("ChietKhauBox");
        if (chietKhauBox is not null && !string.IsNullOrWhiteSpace(chietKhauBox.Text))
        {
            var raw = chietKhauBox.Text.Replace(",", ".").Trim();
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ck))
                Supplier.ChietKhau = Math.Max(0, Math.Min(100, ck));
            else
                Supplier.ChietKhau = 0;
        }
        else
        {
            Supplier.ChietKhau = 0;
        }

        // 9. Trạng thái hợp tác
        var conHopTacCheck = this.FindControl<CheckBox>("ConHopTacCheckBox");
        Supplier.ConHopTac = conHopTacCheck?.IsChecked ?? true;
        Supplier.TrangThai = Supplier.ConHopTac;

        if (!IsEditMode)
            Supplier.NgayTao = DateTime.Now;

        // Thành công: đóng form trả về true
        Close(true);
    }

    private async void OnThietLapGiaClicked(object? sender, RoutedEventArgs e)
    {
        if (Supplier.Id == 0)
        {
            HienLoi("Vui lòng lưu thông tin nhà cung cấp trước khi thiết lập bảng giá chi tiết.");
            return;
        }

        var window = new ThietLapGiaDoiTacWindow(
            Supplier.Id,
            Supplier.MaNhaCungCap,
            string.IsNullOrWhiteSpace(Supplier.TenNhaCungCap) ? "Nhà cung cấp" : Supplier.TenNhaCungCap,
            "NCC");

        await window.ShowDialog(this);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}