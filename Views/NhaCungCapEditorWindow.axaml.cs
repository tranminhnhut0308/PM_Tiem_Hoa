using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;

namespace ShopHoa.Views;

public partial class NhaCungCapEditorWindow : Window, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Regex EmailPrefixRegex = new(
        @"^[a-zA-Z0-9]+([._-][a-zA-Z0-9]+)*$",
        RegexOptions.Compiled);

    private static readonly Regex EmailDomainRegex = new(
        @"^@[a-zA-Z0-9]+(-[a-zA-Z0-9]+)*(\.[a-zA-Z0-9]+(-[a-zA-Z0-9]+)*)*\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    private int _soLuongSoDienThoai = 1;

    public NhaCungCap Supplier { get; }

    public bool IsEditMode { get; }

    public string TieuDe =>
        IsEditMode
            ? "Sửa nhà cung cấp"
            : "Thêm nhà cung cấp";

    private string _loiMessage = "";

    public string LoiMessage
    {
        get => _loiMessage;
        private set
        {
            if (_loiMessage == value)
                return;

            _loiMessage = value;
            OnPropertyChanged();
        }
    }

    public NhaCungCapEditorWindow(NhaCungCap? supplier)
    {
        IsEditMode = supplier is not null && supplier.Id > 0;

        if (supplier is null)
        {
            Supplier = new NhaCungCap
            {
                MaNhaCungCap = "",
                TenNhaCungCap = "",
                TrangThai = true,
                NgayTao = DateTime.Now
            };
        }
        else
        {
            // Tạo bản sao để khi bấm Hủy dữ liệu trong danh sách không bị thay đổi.
            Supplier = new NhaCungCap
            {
                Id = supplier.Id,
                MaNhaCungCap = supplier.MaNhaCungCap,
                TenNhaCungCap = supplier.TenNhaCungCap,
                SoDienThoai = supplier.SoDienThoai,
                Email = supplier.Email,
                DiaChi = supplier.DiaChi,
                PhuongXa = supplier.PhuongXa,
                QuanHuyen = supplier.QuanHuyen,
                TinhThanh = supplier.TinhThanh,
                MaSoThue = supplier.MaSoThue,
                NguoiLienHe = supplier.NguoiLienHe,
                GhiChu = supplier.GhiChu,
                TrangThai = supplier.TrangThai,
                ConNo = supplier.ConNo,
                NgayTao = supplier.NgayTao
            };
        }

        InitializeComponent();

        // ===== HIỂN THỊ EMAIL CŨ LÊN 2 Ô (TIỀN TỐ + HẬU TỐ) =====
        var emailUserBox = this.FindControl<TextBox>("EmailUserBox");
        var emailDomainBox = this.FindControl<TextBox>("EmailDomainBox");

        if (!string.IsNullOrWhiteSpace(Supplier.Email))
        {
            var parts = Supplier.Email.Trim().Split('@', 2);
            if (parts.Length == 2)
            {
                if (emailUserBox is not null) emailUserBox.Text = parts[0];
                if (emailDomainBox is not null) emailDomainBox.Text = "@" + parts[1];
            }
            else
            {
                if (emailUserBox is not null) emailUserBox.Text = Supplier.Email.Trim();
            }
        }

        // ===== TÁCH SỐ ĐIỆN THOẠI KHI SỬA (HỖ TRỢ TỐI ĐA 3 SỐ LIÊN HỆ) =====
        if (!string.IsNullOrWhiteSpace(Supplier.SoDienThoai))
        {
            var cacSo = Supplier.SoDienThoai
                .Split(new[] { " - ", "-", ",", ";", "/" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(3)
                .ToArray();

            var box1 = this.FindControl<TextBox>("SoDienThoai1Box");
            var box2 = this.FindControl<TextBox>("SoDienThoai2Box");
            var box3 = this.FindControl<TextBox>("SoDienThoai3Box");
            var btnThemSo = this.FindControl<Button>("BtnThemSo");

            if (cacSo.Length > 0 && box1 is not null)
                box1.Text = cacSo[0];

            if (cacSo.Length > 1 && box2 is not null)
            {
                box2.Text = cacSo[1];
                box2.IsVisible = true;
                _soLuongSoDienThoai = 2;
            }

            if (cacSo.Length > 2 && box3 is not null)
            {
                box3.Text = cacSo[2];
                box3.IsVisible = true;
                _soLuongSoDienThoai = 3;
                if (btnThemSo is not null)
                    btnThemSo.IsEnabled = false;
            }
        }

        // ===== CHỌN LOẠI ĐỊA CHỈ HÀNH CHÍNH (MỚI 2 CẤP / CŨ 3 CẤP CÓ QUẬN HUYỆN) =====
        var radioMoi = this.FindControl<RadioButton>("RadioDiaChiMoi");
        var radioCu = this.FindControl<RadioButton>("RadioDiaChiCu");
        var panelQuanHuyen = this.FindControl<StackPanel>("PanelQuanHuyen");

        if (!string.IsNullOrWhiteSpace(Supplier.QuanHuyen))
        {
            if (radioCu is not null) radioCu.IsChecked = true;
            if (panelQuanHuyen is not null) panelQuanHuyen.IsVisible = true;
        }
        else
        {
            if (radioMoi is not null) radioMoi.IsChecked = true;
            if (panelQuanHuyen is not null) panelQuanHuyen.IsVisible = false;
        }

        DataContext = this;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    // =========================================================
    // XỬ LÝ SỐ ĐIỆN THOẠI (NHIỀU SỐ LIÊN HỆ + RÀNG BUỘC 10-11 SỐ)
    // =========================================================

    private void OnThemSoDienThoai(object? sender, RoutedEventArgs e)
    {
        if (_soLuongSoDienThoai >= 3)
            return;

        _soLuongSoDienThoai++;

        if (_soLuongSoDienThoai == 2)
        {
            var box2 = this.FindControl<TextBox>("SoDienThoai2Box");
            if (box2 is not null)
            {
                box2.IsVisible = true;
                box2.Focus();
            }
        }
        else if (_soLuongSoDienThoai == 3)
        {
            var box3 = this.FindControl<TextBox>("SoDienThoai3Box");
            if (box3 is not null)
            {
                box3.IsVisible = true;
                box3.Focus();
            }

            var btn = this.FindControl<Button>("BtnThemSo");
            if (btn is not null)
                btn.IsEnabled = false;
        }
    }

    private TextBlock? TimHintBoxSoDienThoai(TextBox box)
    {
        return box.Name switch
        {
            "SoDienThoai1Box" => this.FindControl<TextBlock>("Sdt1HintBox"),
            "SoDienThoai2Box" => this.FindControl<TextBlock>("Sdt2HintBox"),
            "SoDienThoai3Box" => this.FindControl<TextBlock>("Sdt3HintBox"),
            _ => null
        };
    }

    private void OnSoDienThoaiTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        // Chỉ giữ lại các ký tự chữ số 0-9
        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());

        if (digits != box.Text)
        {
            box.Text = digits;
            box.CaretIndex = digits.Length;
        }

        var hintBox = TimHintBoxSoDienThoai(box);
        if (hintBox is null)
            return;

        if (digits.Length == 0)
        {
            hintBox.Text = "";
            return;
        }

        if (!digits.StartsWith('0'))
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D64545");
            hintBox.Text = $"❌ Số điện thoại phải bắt đầu bằng số 0 ({digits.Length}/10)";
            return;
        }

        if (digits.Length < 10)
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D97706");
            hintBox.Text = $"⚠️ Đang nhập ({digits.Length}/10 số) — yêu cầu đủ 10 số";
        }
        else if (digits.Length == 10)
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#087F6E");
            hintBox.Text = "✓ Đạt chuẩn 10 số (10/10)";
        }
        else if (digits.Length == 11)
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#087F6E");
            hintBox.Text = "✓ Hợp lệ 11 số (số máy bàn / tổng đài)";
        }
        else
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D64545");
            hintBox.Text = $"❌ Không hợp lệ: {digits.Length} số (vượt quá quy định 10–11 số)";
        }
    }

    private void OnSoDienThoaiLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
        var hintBox = TimHintBoxSoDienThoai(box);

        if (hintBox is null || digits.Length == 0)
            return;

        if (!digits.StartsWith('0'))
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D64545");
            hintBox.Text = "❌ Số điện thoại bắt buộc phải bắt đầu bằng số 0.";
        }
        else if (digits.Length < 10)
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D64545");
            hintBox.Text = $"❌ Chưa đủ 10 chữ số ({digits.Length}/10). Vui lòng kiểm tra lại.";
        }
        else if (digits.Length > 11)
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D64545");
            hintBox.Text = $"❌ Số điện thoại thừa ({digits.Length} số). Chỉ cho phép tối đa 10–11 chữ số.";
        }
    }

    private static bool KiemTraMotSoDienThoai(string? so, string tenO, out string thongBaoLoi)
    {
        thongBaoLoi = "";
        if (string.IsNullOrWhiteSpace(so))
            return true;

        var digits = so.Trim();
        if (!digits.All(char.IsDigit))
        {
            thongBaoLoi = $"{tenO} chỉ được chứa chữ số (0–9).";
            return false;
        }

        if (!digits.StartsWith('0'))
        {
            thongBaoLoi = $"{tenO} ({digits}) không hợp lệ: phải bắt đầu bằng chữ số 0.";
            return false;
        }

        if (digits.Length < 10)
        {
            thongBaoLoi = $"{tenO} ({digits}) chưa đủ 10 chữ số (hiện có {digits.Length} số).";
            return false;
        }

        if (digits.Length > 11)
        {
            thongBaoLoi = $"{tenO} ({digits}) vượt quá quy định (hiện có {digits.Length} số, chỉ cho phép 10–11 số).";
            return false;
        }

        return true;
    }

    // =========================================================
    // XỬ LÝ EMAIL (TÁCH TIỀN TỐ + HẬU TỐ @gmail.com & KIỂM TRA ĐỊNH DẠNG)
    // =========================================================

    private void OnEmailUserTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox userBox)
            return;

        var text = userBox.Text ?? "";
        if (text.Contains('@'))
        {
            var parts = text.Split('@', 2);
            userBox.Text = parts[0];
            userBox.CaretIndex = userBox.Text.Length;

            var domainBox = this.FindControl<TextBox>("EmailDomainBox");
            if (domainBox is not null && !string.IsNullOrWhiteSpace(parts[1]))
            {
                domainBox.Text = "@" + parts[1];
            }
        }

        CapNhatHintEmail();
    }

    private void OnEmailDomainTextChanged(object? sender, TextChangedEventArgs e)
    {
        CapNhatHintEmail();
    }

    private void OnEmailLostFocus(object? sender, RoutedEventArgs e)
    {
        CapNhatHintEmail();
    }

    private void OnEmailDomainLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox domainBox)
        {
            var text = domainBox.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith('@'))
            {
                domainBox.Text = "@" + text;
            }
        }

        CapNhatHintEmail();
    }

    private void CapNhatHintEmail()
    {
        var hintBox = this.FindControl<TextBlock>("EmailHintBox");
        if (hintBox is null)
            return;

        var userText = this.FindControl<TextBox>("EmailUserBox")?.Text?.Trim() ?? "";
        var domainText = this.FindControl<TextBox>("EmailDomainBox")?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(userText))
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#7A8C86");
            hintBox.Text = "Nhập tiền tố tài khoản (chữ không dấu, số, dấu chấm) và hậu tố tên miền (mặc định @gmail.com)";
            return;
        }

        if (!KiemTraHopLeEmail(userText, domainText, out var emailGhep, out var loiEmail))
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#D64545");
            hintBox.Text = $"❌ {loiEmail}";
        }
        else
        {
            hintBox.Foreground = Avalonia.Media.Brush.Parse("#087F6E");
            hintBox.Text = $"✓ Email hợp lệ: {emailGhep}";
        }
    }

    private static bool KiemTraHopLeEmail(
        string userPart,
        string domainPart,
        out string emailHoanChinh,
        out string thongBaoLoi)
    {
        emailHoanChinh = "";
        thongBaoLoi = "";

        var user = (userPart ?? "").Trim();
        var domain = (domainPart ?? "").Trim();

        if (string.IsNullOrWhiteSpace(user))
            return true;

        if (user.Length < 3 || user.Length > 64)
        {
            thongBaoLoi = "Tiền tố email (trước @) phải có từ 3 đến 64 ký tự.";
            return false;
        }

        if (!EmailPrefixRegex.IsMatch(user))
        {
            thongBaoLoi = "Tiền tố email không hợp lệ (chỉ gồm chữ cái không dấu a–z, số 0–9, dấu chấm '.', gạch dưới '_', gạch ngang '-', không bắt đầu/kết thúc bằng dấu chấm).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(domain) || domain == "@")
        {
            thongBaoLoi = "Thiếu hậu tố tên miền email (ví dụ: @gmail.com).";
            return false;
        }

        if (!domain.StartsWith('@'))
        {
            thongBaoLoi = "Hậu tố email phải bắt đầu bằng ký tự '@' (ví dụ: @gmail.com).";
            return false;
        }

        if (!EmailDomainRegex.IsMatch(domain))
        {
            thongBaoLoi = $"Hậu tố tên miền '{domain}' không đúng định dạng chuẩn (ví dụ hợp lệ: @gmail.com, @yahoo.com, @congty.vn).";
            return false;
        }

        emailHoanChinh = user + domain.ToLowerInvariant();
        return true;
    }

    // =========================================================
    // XỬ LÝ PHÂN CẤP ĐỊA CHỈ HÀNH CHÍNH (MỚI 2 CẤP / CŨ 3 CẤP)
    // =========================================================

    private void OnLoaiDiaChiChanged(object? sender, RoutedEventArgs e)
    {
        var panel = this.FindControl<StackPanel>("PanelQuanHuyen");
        if (panel is null)
            return;

        var isDiaChiCu = this.FindControl<RadioButton>("RadioDiaChiCu")?.IsChecked == true;
        panel.IsVisible = isDiaChiCu;

        // Khi chọn phân cấp hành chính mới (chỉ có Xã/Phường và Tỉnh/Thành phố) thì xóa Quận/Huyện
        if (!isDiaChiCu)
            Supplier.QuanHuyen = null;
    }

    // =========================================================
    // LƯU / HỦY / XÓA
    // =========================================================

    private void OnSaveClicked(
        object? sender,
        RoutedEventArgs e)
    {
        LoiMessage = "";

        // 1. Chuẩn hóa các trường cơ bản
        Supplier.MaNhaCungCap = Supplier.MaNhaCungCap?.Trim() ?? "";
        Supplier.TenNhaCungCap = Supplier.TenNhaCungCap?.Trim() ?? "";
        Supplier.DiaChi = Supplier.DiaChi?.Trim();
        Supplier.PhuongXa = Supplier.PhuongXa?.Trim();
        Supplier.TinhThanh = Supplier.TinhThanh?.Trim();
        Supplier.MaSoThue = Supplier.MaSoThue?.Trim();
        Supplier.NguoiLienHe = Supplier.NguoiLienHe?.Trim();
        Supplier.GhiChu = Supplier.GhiChu?.Trim();

        var isDiaChiCu = this.FindControl<RadioButton>("RadioDiaChiCu")?.IsChecked == true;
        Supplier.QuanHuyen = isDiaChiCu ? Supplier.QuanHuyen?.Trim() : null;

        // 2. Kiểm tra Mã & Tên nhà cung cấp
        if (string.IsNullOrWhiteSpace(Supplier.MaNhaCungCap))
        {
            LoiMessage = "Vui lòng nhập mã nhà cung cấp.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Supplier.TenNhaCungCap))
        {
            LoiMessage = "Vui lòng nhập tên nhà cung cấp.";
            this.FindControl<TextBox>("TenNhaCungCapBox")?.Focus();
            return;
        }

        // 3. Kiểm tra các số điện thoại liên hệ (Chính + Cấp 2 + Cấp 3)
        var box1 = this.FindControl<TextBox>("SoDienThoai1Box");
        var box2 = this.FindControl<TextBox>("SoDienThoai2Box");
        var box3 = this.FindControl<TextBox>("SoDienThoai3Box");

        var so1 = box1?.Text?.Trim().Replace(" ", "") ?? "";
        var so2 = box2?.Text?.Trim().Replace(" ", "") ?? "";
        var so3 = box3?.Text?.Trim().Replace(" ", "") ?? "";

        if (!KiemTraMotSoDienThoai(so1, "Số điện thoại chính", out var loiSdt1))
        {
            LoiMessage = loiSdt1;
            box1?.Focus();
            return;
        }

        if (!KiemTraMotSoDienThoai(so2, "Số điện thoại liên hệ cấp 2", out var loiSdt2))
        {
            LoiMessage = loiSdt2;
            box2?.Focus();
            return;
        }

        if (!KiemTraMotSoDienThoai(so3, "Số điện thoại liên hệ cấp 3", out var loiSdt3))
        {
            LoiMessage = loiSdt3;
            box3?.Focus();
            return;
        }

        var danhSachSdt = new List<string>();
        if (!string.IsNullOrWhiteSpace(so1)) danhSachSdt.Add(so1);
        if (!string.IsNullOrWhiteSpace(so2)) danhSachSdt.Add(so2);
        if (!string.IsNullOrWhiteSpace(so3)) danhSachSdt.Add(so3);

        if (danhSachSdt.Count != danhSachSdt.Distinct(StringComparer.Ordinal).Count())
        {
            LoiMessage = "Các số điện thoại liên hệ không được trùng nhau.";
            return;
        }

        Supplier.SoDienThoai = danhSachSdt.Count > 0
            ? string.Join(" - ", danhSachSdt)
            : null;

        // 4. Kiểm tra Email (Tiền tố + Hậu tố @gmail.com / tên miền hợp lệ)
        var emailUserBox = this.FindControl<TextBox>("EmailUserBox");
        var emailDomainBox = this.FindControl<TextBox>("EmailDomainBox");
        var userText = emailUserBox?.Text?.Trim() ?? "";
        var domainText = emailDomainBox?.Text?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(userText))
        {
            if (!KiemTraHopLeEmail(userText, domainText, out var emailHopLe, out var loiEmail))
            {
                LoiMessage = loiEmail;
                emailUserBox?.Focus();
                return;
            }

            Supplier.Email = emailHopLe;
        }
        else
        {
            Supplier.Email = null;
        }

        // 5. Ngày tạo & Lưu
        if (!IsEditMode)
        {
            Supplier.NgayTao = DateTime.Now;
        }

        Close(true);
    }

    private void OnCancelClicked(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private async void OnDeleteClicked(
        object? sender,
        RoutedEventArgs e)
    {
        var result =
            await ShowDeleteConfirmationAsync();

        if (!result)
            return;

        Close(false);
    }

    private async Task<bool> ShowDeleteConfirmationAsync()
    {
        var confirmWindow = new Window
        {
            Title = "Xác nhận xóa",
            Width = 420,
            Height = 220,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var yesButton = new Button
        {
            Content = "Xóa",
            Width = 100,
            Background =
                Avalonia.Media.Brushes.IndianRed,
            Foreground =
                Avalonia.Media.Brushes.White
        };

        var noButton = new Button
        {
            Content = "Hủy",
            Width = 100
        };

        var result = false;

        yesButton.Click += (_, _) =>
        {
            result = true;
            confirmWindow.Close();
        };

        noButton.Click += (_, _) =>
        {
            result = false;
            confirmWindow.Close();
        };

        confirmWindow.Content =
            new StackPanel
            {
                Margin =
                    new Avalonia.Thickness(25),

                Spacing = 18,

                Children =
                {
                    new TextBlock
                    {
                        Text =
                            $"Bạn có chắc muốn xóa nhà cung cấp " +
                            $"\"{Supplier.TenNhaCungCap}\" không?",

                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap,

                        FontSize = 15
                    },

                    new StackPanel
                    {
                        Orientation =
                            Avalonia.Layout.Orientation.Horizontal,

                        HorizontalAlignment =
                            Avalonia.Layout.HorizontalAlignment.Right,

                        Spacing = 10,

                        Children =
                        {
                            noButton,
                            yesButton
                        }
                    }
                }
            };

        await confirmWindow.ShowDialog(this);

        return result;
    }
}