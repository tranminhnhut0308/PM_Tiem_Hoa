using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Chế độ làm việc của form hồ sơ nhân viên.
/// </summary>
public enum CheDoNhanVienEditor
{
    /// <summary>Thêm mới hồ sơ nhân viên (kèm tùy chọn cấp tài khoản).</summary>
    ThemMoi,

    /// <summary>Sửa hồ sơ nhân viên đã có (hồ sơ và tài khoản là hai phần độc lập).</summary>
    SuaHoSo,

    /// <summary>Cấp tài khoản đăng nhập cho một hồ sơ nhân viên chưa có tài khoản.</summary>
    CapTaiKhoan
}

/// <summary>
/// ViewModel cho form hồ sơ nhân viên.
///
/// Phân biệt rõ hai khái niệm:
/// - HỒ SƠ NHÂN VIÊN: họ tên, chức vụ nhân sự, bộ phận, ngày vào làm, liên hệ.
/// - TÀI KHOẢN ĐĂNG NHẬP: tên đăng nhập, mật khẩu, vai trò, quyền — quản lý
///   riêng ở màn "Quản lý tài khoản". Một nhân viên KHÔNG bắt buộc phải có tài khoản.
/// </summary>
public partial class NhanVienEditorViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly CheDoNhanVienEditor _cheDo;

    private bool _dangTai = true;
    private bool _coTaiKhoanHienTai;

    [ObservableProperty] private string tieuDe = "Thêm hồ sơ nhân viên";
    [ObservableProperty] private string tenNhanVien = string.Empty;
    [ObservableProperty] private string hoTen = string.Empty;
    [ObservableProperty] private string? chucVu;
    [ObservableProperty] private string soDienThoai = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private DateTime? ngayVaoLam = DateTime.Today;
    [ObservableProperty] private bool trangThai = true;

    [ObservableProperty] private ProductOption? selectedBoPhan;
    [ObservableProperty] private ObservableCollection<ProductOption> boPhanOptions = new();

    // --- Phần tài khoản đăng nhập ---
    [ObservableProperty] private bool taoTaiKhoan;
    [ObservableProperty] private string tenDangNhap = string.Empty;
    [ObservableProperty] private string matKhau = string.Empty;
    [ObservableProperty] private string nhapLaiMatKhau = string.Empty;
    [ObservableProperty] private Role? selectedVaiTro;
    [ObservableProperty] private ObservableCollection<Role> vaiTroOptions = new();
    [ObservableProperty] private string tenDangNhapHienTai = string.Empty;
    [ObservableProperty] private string vaiTroHienTai = string.Empty;

    /// <summary>Sự kiện phát ra sau khi lưu thành công (dùng để làm mới danh sách).</summary>
    public Action? OnSaved { get; set; }

    /// <summary>Cửa sổ chủ sở dùng cho thông báo.</summary>
    public Avalonia.Controls.Window? OwnerWindow { get; set; }

    public NhanVienEditorViewModel(DatabaseService db, CheDoNhanVienEditor cheDo, uint? nhanVienId = null)
    {
        _db = db;
        _cheDo = cheDo;
        NhanVienId = nhanVienId ?? 0;

        switch (cheDo)
        {
            case CheDoNhanVienEditor.ThemMoi:
                tieuDe = "Thêm hồ sơ nhân viên";
                break;
            case CheDoNhanVienEditor.SuaHoSo:
                tieuDe = "Sửa hồ sơ nhân viên";
                break;
            case CheDoNhanVienEditor.CapTaiKhoan:
                tieuDe = "Cấp tài khoản cho nhân viên";
                break;
        }
    }

    public uint NhanVienId { get; }

    public bool IsCheDoThemMoi => _cheDo == CheDoNhanVienEditor.ThemMoi;
    public bool IsCheDoSuaHoSo => _cheDo == CheDoNhanVienEditor.SuaHoSo;
    public bool IsCheDoCapTaiKhoan => _cheDo == CheDoNhanVienEditor.CapTaiKhoan;

    /// <summary>Hồ sơ đang mở đã có tài khoản đăng nhập hay chưa.</summary>
    public bool CoTaiKhoanHienTai => _coTaiKhoanHienTai;

    /// <summary>Phần tài khoản đăng nhập có hiển thị trong form hay không.</summary>
    public bool HienThiPhanTaiKhoan =>
        IsCheDoThemMoi
        ? TaoTaiKhoan
        : IsCheDoCapTaiKhoan;

    /// <summary>Hiển thị thông tin tài khoản đã có (chỉ đọc).</summary>
    public bool HienThiTaiKhoanDaCo => IsCheDoSuaHoSo && _coTaiKhoanHienTai;

    /// <summary>Đã đủ thông tin bắt buộc để lưu hay chưa.</summary>
    public bool DuLieuHopLe =>
        !string.IsNullOrWhiteSpace(HoTen)
        && (!HienThiPhanTaiKhoan
            || (!string.IsNullOrWhiteSpace(TenDangNhap)
                && !string.IsNullOrWhiteSpace(MatKhau)
                && MatKhau == NhapLaiMatKhau));

    /// <summary>Điều kiện cho phép bấm nút "Lưu hồ sơ".</summary>
    public bool CanLuu => !_dangTai && DuLieuHopLe;

    [ObservableProperty] private string thongBaoLoi = string.Empty;

    public async Task LoadAsync()
    {
        _dangTai = true;
        try
        {
            await _db.EnsureCotNhanVienAsync();

            var boPhan = await _db.GetChiNhanhOptionsAsync(chiTrangThai: false);
            BoPhanOptions.Clear();
            foreach (var b in boPhan)
            {
                BoPhanOptions.Add(b);
            }

            var vaiTros = await _db.GetRolesAsync();
            VaiTroOptions.Clear();
            foreach (var v in vaiTros.Where(v => v.TrangThai))
            {
                VaiTroOptions.Add(v);
            }

            if (NhanVienId > 0)
            {
                var nv = await _db.GetNhanVienByIdAsync(NhanVienId);
                if (nv != null)
                {
                    HoTen = nv.HoTen;
                    ChucVu = nv.ChucVu;
                    SoDienThoai = nv.SoDienThoai ?? string.Empty;
                    Email = nv.Email ?? string.Empty;
                    NgayVaoLam = nv.NgayVaoLam;
                    TrangThai = nv.TrangThai;
                    _coTaiKhoanHienTai = nv.CoTaiKhoan;
                    OnPropertyChanged(nameof(HienThiTaiKhoanDaCo));
                    OnPropertyChanged(nameof(CoTaiKhoanHienTai));
                    TenDangNhapHienTai = nv.CoTaiKhoan ? nv.TenDangNhap : "(chưa có tài khoản)";
                    VaiTroHienTai = string.IsNullOrWhiteSpace(nv.VaiTro) ? "(chưa có vai trò)" : nv.VaiTro;

                    SelectedBoPhan = BoPhanOptions.FirstOrDefault(b => b.Id == nv.ChiNhanhId);
                }
            }

            if (SelectedBoPhan is null && BoPhanOptions.Count > 0)
            {
                SelectedBoPhan = BoPhanOptions[0];
            }

            if (SelectedVaiTro is null && VaiTroOptions.Count > 0)
            {
                SelectedVaiTro = VaiTroOptions.FirstOrDefault(v =>
                    string.Equals(v.MaVaiTro, "NHAN_VIEN", StringComparison.OrdinalIgnoreCase))
                    ?? VaiTroOptions[0];
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("NhanVienEditorViewModel.LoadAsync", ex);
            ThongBaoLoi = "Lỗi tải dữ liệu: " + ex.Message;
        }
        finally
        {
            _dangTai = false;
            RaiseCanExecuteChanged();
        }
    }

    partial void OnTaoTaiKhoanChanged(bool value)
    {
        OnPropertyChanged(nameof(HienThiPhanTaiKhoan));
        RaiseCanExecuteChanged();
    }

    partial void OnHoTenChanged(string value) => RaiseCanExecuteChanged();
    partial void OnTenDangNhapChanged(string value) => RaiseCanExecuteChanged();
    partial void OnMatKhauChanged(string value) => RaiseCanExecuteChanged();
    partial void OnNhapLaiMatKhauChanged(string value) => RaiseCanExecuteChanged();

    private void RaiseCanExecuteChanged()
    {
        OnPropertyChanged(nameof(DuLieuHopLe));
        OnPropertyChanged(nameof(CanLuu));
        LuuCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLuu))]
    private async Task LuuAsync()
    {
        _dangTai = true;
        RaiseCanExecuteChanged();

        try
        {
            var item = new NhanVienItem
            {
                Id = NhanVienId,
                HoTen = HoTen.Trim(),
                ChucVu = ChucVu,
                SoDienThoai = string.IsNullOrWhiteSpace(SoDienThoai) ? null : SoDienThoai.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                NgayVaoLam = NgayVaoLam,
                TrangThai = TrangThai,
                ChiNhanhId = SelectedBoPhan?.Id
            };

            switch (_cheDo)
            {
                case CheDoNhanVienEditor.ThemMoi:
                {
                    var vaiTroIds = TaoTaiKhoan && SelectedVaiTro is not null
                        ? new[] { SelectedVaiTro.Id }
                        : null;

                    await _db.ThemNhanVienAsync(
                        item,
                        TaoTaiKhoan ? TenDangNhap.Trim() : null,
                        TaoTaiKhoan ? MatKhau : null,
                        vaiTroIds);

                    await NotificationWindow.ShowSuccessAsync(
                        TaoTaiKhoan
                            ? $"Đã thêm hồ sơ nhân viên \"{item.HoTen}\" và cấp tài khoản đăng nhập."
                            : $"Đã thêm hồ sơ nhân viên \"{item.HoTen}\" (chưa cấp tài khoản đăng nhập).",
                        owner: OwnerWindow);
                    break;
                }

                case CheDoNhanVienEditor.SuaHoSo:
                {
                    await _db.CapNhatNhanVienAsync(item);
                    await NotificationWindow.ShowSuccessAsync(
                        $"Đã cập nhật hồ sơ nhân viên \"{item.HoTen}\".",
                        owner: OwnerWindow);
                    break;
                }

                case CheDoNhanVienEditor.CapTaiKhoan:
                {
                    var vaiTroIds = SelectedVaiTro is not null ? new[] { SelectedVaiTro.Id } : null;
                    await _db.CapTaiKhoanChoNhanVienAsync(NhanVienId, TenDangNhap.Trim(), MatKhau, vaiTroIds);
                    await NotificationWindow.ShowSuccessAsync(
                        $"Đã cấp tài khoản đăng nhập cho nhân viên \"{item.HoTen}\".",
                        owner: OwnerWindow);
                    break;
                }
            }

            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("NhanVienEditorViewModel.LuuAsync", ex);
            ThongBaoLoi = ex.Message;
            await NotificationWindow.ShowErrorAsync("Không lưu được hồ sơ nhân viên: " + ex.Message, owner: OwnerWindow);
        }
        finally
        {
            _dangTai = false;
            RaiseCanExecuteChanged();
        }
    }
}
