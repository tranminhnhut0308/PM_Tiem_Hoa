using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Models;
using Avalonia.Input;
using System.Globalization;

namespace ShopHoa.Views;

/// <summary>
/// Form thêm / sửa khách hàng.
/// Chỉ dùng cho thêm mới và chỉnh sửa; xem chi tiết nằm ngay trong danh sách.
/// </summary>
public partial class KhachHangEditorWindow : Window
{
    // Đánh dấu form đã bị đóng (Hủy hoặc nút X) để không Close lần nữa sau khi await.
    private bool _daDongForm;

    public KhachHang Customer { get; }

    public bool IsEditMode { get; }

    public IReadOnlyList<NhomKhachHang> DanhSachNhom { get; }

    public NhomKhachHang? NhomKhachHangDaChon { get; set; }

    public string TieuDe =>
        IsEditMode
            ? "Sửa khách hàng"
            : "Thêm khách hàng";

    /// <summary>
    /// Kiểm tra trùng số điện thoại do nơi gọi cung cấp
    /// (form không truy cập database trực tiếp).
    /// Tham số: số điện thoại, id khách hàng hiện tại (0 = thêm mới).
    /// Trả về true nếu số đã tồn tại.
    /// </summary>
    public Func<string?, uint, Task<bool>>? TrungSoDienThoaiAsync { get; }

    public KhachHangEditorWindow(
     KhachHang? customer,
     IReadOnlyList<NhomKhachHang>? nhomKhachHangOptions,
     bool isEditMode,
     Func<string?, uint, Task<bool>>? trungSoDienThoaiAsync = null)
    {
        IsEditMode = isEditMode;
        TrungSoDienThoaiAsync = trungSoDienThoaiAsync;
        DanhSachNhom = nhomKhachHangOptions ?? Array.Empty<NhomKhachHang>();

        Customer = SaoChep(customer);

        NhomKhachHangDaChon =
            DanhSachNhom.FirstOrDefault(
                nhom => nhom.Id == Customer.NhomKhachHangId);

        // Gán dữ liệu trước khi dựng giao diện để các binding đọc đúng giá trị
        InitializeComponent();

        // ===== HIỂN THỊ EMAIL CŨ LÊN GIAO DIỆN =====
        var emailUserBox = this.FindControl<TextBox>("EmailUserBox");
        var emailDomainBox = this.FindControl<TextBox>("EmailDomainBox");

        if (!string.IsNullOrWhiteSpace(Customer.Email))
        {
            var parts = Customer.Email.Split('@');
            if (parts.Length == 2)
            {
                if (emailUserBox is not null) emailUserBox.Text = parts[0];
                if (emailDomainBox is not null) emailDomainBox.Text = "@" + parts[1];
            }
            else
            {
                if (emailUserBox is not null) emailUserBox.Text = Customer.Email;
            }
        }

        // ===== Tách số điện thoại khi sửa =====
        if (!string.IsNullOrWhiteSpace(Customer.SoDienThoai))
        {
            var cacSo = Customer.SoDienThoai
                .Split(new[] { " - ", "-" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(3)
                .ToArray();

            if (cacSo.Length > 0)
                this.FindControl<TextBox>("SoDienThoai1Box")!.Text = cacSo[0];

            if (cacSo.Length > 1)
            {
                this.FindControl<TextBox>("SoDienThoai2Box")!.Text = cacSo[1];
                this.FindControl<TextBox>("SoDienThoai2Box")!.IsVisible = true;
                _soLuongSoDienThoai = 2;
            }

            if (cacSo.Length > 2)
            {
                this.FindControl<TextBox>("SoDienThoai3Box")!.Text = cacSo[2];
                this.FindControl<TextBox>("SoDienThoai3Box")!.IsVisible = true;
                _soLuongSoDienThoai = 3;
                this.FindControl<Button>("BtnThemSo")!.IsEnabled = false;
            }
        }

        // ===== Chọn loại địa chỉ =====
        if (!string.IsNullOrWhiteSpace(Customer.QuanHuyen))
        {
            this.FindControl<RadioButton>("RadioDiaChiCu")!.IsChecked = true;
            this.FindControl<StackPanel>("PanelQuanHuyen")!.IsVisible = true;
        }
        else
        {
            this.FindControl<RadioButton>("RadioDiaChiMoi")!.IsChecked = true;
        }

        Closed += (_, _) => _daDongForm = true;

        ChonGioiTinh(Customer.GioiTinh);

        // ===== HIỂN THỊ NGÀY SINH CŨ LÊN 3 Ô =====
        if (Customer.NgaySinh.HasValue)
        {
            var ngayBox = this.FindControl<TextBox>("NgayBox");
            var thangBox = this.FindControl<TextBox>("ThangBox");
            var namBox = this.FindControl<TextBox>("NamBox");

            if (ngayBox is not null) ngayBox.Text = Customer.NgaySinh.Value.ToString("dd");
            if (thangBox is not null) thangBox.Text = Customer.NgaySinh.Value.ToString("MM");
            if (namBox is not null) namBox.Text = Customer.NgaySinh.Value.ToString("yyyy");
        }

        // ===== Gán tên khách hàng vào ô nhập (nếu có dữ liệu cũ) =====
        var tenBox = this.FindControl<TextBox>("TenKhachHangBox");
        if (tenBox is not null && !string.IsNullOrWhiteSpace(Customer.TenKhachHang))
        {
            tenBox.Text = Customer.TenKhachHang;
        }

        DataContext = this;
    }

    private static KhachHang SaoChep(KhachHang? customer)
    {
        if (customer is null)
        {
            return new KhachHang
            {
                Id = 0,
                MaKhachHang = "",
                TenKhachHang = "",
                LoaiKhachHang = "CA_NHAN",
                TrangThai = true,
                NgayTao = DateTime.Now
            };
        }

        return new KhachHang
        {
            Id = customer.Id,
            MaKhachHang = customer.MaKhachHang,
            TenKhachHang = customer.TenKhachHang,
            LoaiKhachHang = customer.LoaiKhachHang,
            SoDienThoai = customer.SoDienThoai,
            NgaySinh = customer.NgaySinh,
            GioiTinh = customer.GioiTinh,
            Email = customer.Email,
            DiaChi = customer.DiaChi,
            PhuongXa = customer.PhuongXa,
            QuanHuyen = customer.QuanHuyen,
            TinhThanh = customer.TinhThanh,
            NhomKhachHangId = customer.NhomKhachHangId,
            MaSoThue = customer.MaSoThue,
            TenNguoiMuaHoaDon = customer.TenNguoiMuaHoaDon,
            TenCongTy = customer.TenCongTy,
            DiaChiXuatHoaDon = customer.DiaChiXuatHoaDon,
            GhiChu = customer.GhiChu,
            TrangThai = customer.TrangThai,
            NgayTao = customer.NgayTao,
            NgayCapNhat = customer.NgayCapNhat
        };
    }

    /// <summary>
    /// Đồng bộ giới tính của khách hàng vào ComboBox.
    /// "Chưa có" = không nhập, "Nam" = Nam, "Nữ" = Nữ, "Khác" = Khác.
    /// </summary>
    private void ChonGioiTinh(string? gioiTinh)
    {
        var gioiTinhBox = this.FindControl<ComboBox>("GioiTinhBox");

        if (gioiTinhBox is null)
            return;

        var value = gioiTinh?.Trim();

        var index = value?.ToLowerInvariant() switch
        {
            "nam" => 1,
            "nữ" or "nu" or "nữ " => 2,
            "khác" or "khac" => 3,
            _ => 0
        };

        gioiTinhBox.SelectedIndex = index;
    }

    private string LayGioiTinh()
    {
        var gioiTinhBox = this.FindControl<ComboBox>("GioiTinhBox");

        var content = (gioiTinhBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return content == "Chưa có" || string.IsNullOrWhiteSpace(content)
            ? ""
            : content;
    }

    private void HienLoi(string message)
    {
        var loiMessage = this.FindControl<TextBlock>("LoiMessageTextBlock");

        if (loiMessage is not null)
            loiMessage.Text = message;
    }

    private async void OnSaveClicked(
    object? sender,
    RoutedEventArgs e)
    {
        HienLoi("");

        var loiMessage = this.FindControl<TextBlock>("LoiMessageTextBlock");
        var tenBox = this.FindControl<TextBox>("TenKhachHangBox");

        // ==============================
        // CHUẨN HÓA DỮ LIỆU CƠ BẢN
        // ==============================

        Customer.MaKhachHang = Customer.MaKhachHang?.Trim() ?? "";

        var TenKhachHangBox = this.FindControl<TextBox>("TenKhachHangBox");
        Customer.TenKhachHang = TenKhachHangBox?.Text?.Trim() ?? "";

        var so1 = this.FindControl<TextBox>("SoDienThoai1Box")?.Text?.Trim().Replace(" ", "");
        var so2 = this.FindControl<TextBox>("SoDienThoai2Box")?.Text?.Trim().Replace(" ", "");
        var so3 = this.FindControl<TextBox>("SoDienThoai3Box")?.Text?.Trim().Replace(" ", "");

        var danhSach = new List<string>();
        if (!string.IsNullOrWhiteSpace(so1)) danhSach.Add(so1);
        if (!string.IsNullOrWhiteSpace(so2)) danhSach.Add(so2);
        if (!string.IsNullOrWhiteSpace(so3)) danhSach.Add(so3);
        Customer.SoDienThoai = danhSach.Count > 0 ? string.Join(" - ", danhSach) : null;

        // ==============================
        // XỬ LÝ EMAIL (Từ 2 ô: EmailUserBox & EmailDomainBox)
        // ==============================
        var emailUserBox = this.FindControl<TextBox>("EmailUserBox");
        var emailDomainBox = this.FindControl<TextBox>("EmailDomainBox");

        var userText = emailUserBox?.Text?.Trim() ?? "";
        var domainText = emailDomainBox?.Text?.Trim() ?? "@gmail.com";

        if (!string.IsNullOrWhiteSpace(userText))
        {
            if (!domainText.StartsWith("@")) domainText = "@" + domainText;
            Customer.Email = userText + domainText;
        }
        else
        {
            Customer.Email = null;
        }

        // ==============================
        // NGÀY SINH (Đọc từ 3 ô: NgayBox, ThangBox, NamBox)[cite: 6, 7]
        // ==============================
        Customer.NgaySinh = null;

        var strNgay = this.FindControl<TextBox>("NgayBox")?.Text?.Trim() ?? "";
        var strThang = this.FindControl<TextBox>("ThangBox")?.Text?.Trim() ?? "";
        var strNam = this.FindControl<TextBox>("NamBox")?.Text?.Trim() ?? "";

        if (!string.IsNullOrEmpty(strNgay) || !string.IsNullOrEmpty(strThang) || !string.IsNullOrEmpty(strNam))
        {
            if (string.IsNullOrEmpty(strNgay) || string.IsNullOrEmpty(strThang) || string.IsNullOrEmpty(strNam))
            {
                HienLoi("Vui lòng nhập đầy đủ Ngày, Tháng và Năm sinh.");
                this.FindControl<TextBox>("NgayBox")?.Focus();
                return;
            }

            if (!int.TryParse(strNgay, out int ngay) || !int.TryParse(strThang, out int thang) || !int.TryParse(strNam, out int nam))
            {
                HienLoi("Ngày sinh chỉ được nhập các ký tự số hợp lệ.");
                return;
            }

            int currentYear = DateTime.Now.Year;
            if (nam < 1900 || nam > currentYear)
            {
                HienLoi($"Năm sinh phải nằm trong khoảng từ 1900 đến {currentYear}.");
                this.FindControl<TextBox>("NamBox")?.Focus();
                return;
            }

            string dateStr = $"{ngay:D2}/{thang:D2}/{nam}";
            if (!DateTime.TryParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngaySinhParsed))
            {
                HienLoi("Ngày tháng năm sinh không tồn tại trong thực tế (Ví dụ: 30/02 hoặc 31/04).");
                return;
            }

            if (ngaySinhParsed > DateTime.Today)
            {
                HienLoi("Ngày sinh không thể lớn hơn ngày hôm nay.");
                return;
            }

            Customer.NgaySinh = ngaySinhParsed;
        }

        // ==============================
        // CÁC TRƯỜNG THÔNG TIN KHÁC
        // ==============================
        Customer.DiaChi = Customer.DiaChi?.Trim();
        Customer.PhuongXa = Customer.PhuongXa?.Trim();
        Customer.QuanHuyen = Customer.QuanHuyen?.Trim();
        Customer.TinhThanh = Customer.TinhThanh?.Trim();
        Customer.MaSoThue = Customer.MaSoThue?.Trim();
        Customer.TenNguoiMuaHoaDon = Customer.TenNguoiMuaHoaDon?.Trim();
        Customer.TenCongTy = Customer.TenCongTy?.Trim();
        Customer.DiaChiXuatHoaDon = Customer.DiaChiXuatHoaDon?.Trim();
        Customer.GhiChu = Customer.GhiChu?.Trim();
        Customer.GioiTinh = LayGioiTinh();
        Customer.NhomKhachHangId = NhomKhachHangDaChon?.Id;

        // ==============================
        // KIỂM TRA TÊN KHÁCH HÀNG
        // ==============================
        if (string.IsNullOrWhiteSpace(Customer.TenKhachHang))
        {
            HienLoi("Vui lòng nhập tên khách hàng.");
            tenBox?.Focus();
            return;
        }

        // ==============================
        // KIỂM TRA TRÙNG SỐ ĐIỆN THOẠI
        // ==============================
        if (TrungSoDienThoaiAsync is not null &&
            !string.IsNullOrWhiteSpace(Customer.SoDienThoai))
        {
            var nutLuu = this.FindControl<Button>("NutLuuButton");

            if (nutLuu is not null)
                nutLuu.IsEnabled = false;

            try
            {
                if (await TrungSoDienThoaiAsync(Customer.SoDienThoai, Customer.Id))
                {
                    HienLoi($"Số điện thoại {Customer.SoDienThoai} đã tồn tại. Vui lòng dùng số khác.");
                    this.FindControl<TextBox>("SoDienThoai1Box")?.Focus();
                    return;
                }
            }
            catch (Exception ex)
            {
                HienLoi("Không thể kiểm tra số điện thoại: " + ex.Message);
                return;
            }
            finally
            {
                if (nutLuu is not null)
                    nutLuu.IsEnabled = true;
            }
        }

        // ==============================
        // KIỂM TRA ĐỊNH DẠNG EMAIL
        // ==============================
        if (!string.IsNullOrWhiteSpace(Customer.Email) &&
            !Customer.Email.Contains("@"))
        {
            HienLoi("Email không hợp lệ.");
            return;
        }

        // ==============================
        // LƯU DỮ LIỆU
        // ==============================
        if (_daDongForm)
            return;

        if (!IsEditMode)
            Customer.NgayTao = DateTime.Now;

        Customer.NgayCapNhat = DateTime.Now;

        if (loiMessage is not null)
            loiMessage.Text = "";

        Close(true);
    }

    private void OnCancelClicked(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private int _soLuongSoDienThoai = 1;

    private void OnThemSoDienThoai(object? sender, RoutedEventArgs e)
    {
        if (_soLuongSoDienThoai >= 3) return;

        _soLuongSoDienThoai++;

        if (_soLuongSoDienThoai == 2)
        {
            this.FindControl<TextBox>("SoDienThoai2Box")!.IsVisible = true;
        }
        else if (_soLuongSoDienThoai == 3)
        {
            this.FindControl<TextBox>("SoDienThoai3Box")!.IsVisible = true;
            this.FindControl<Button>("BtnThemSo")!.IsEnabled = false; // hết chỗ thêm
        }
    }

    private void OnEmailLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        var text = textBox.Text?.Trim() ?? "";

        // Nếu người dùng xóa hết thì trả về @gmail.com
        if (string.IsNullOrWhiteSpace(text) || text == "@")
        {
            textBox.Text = "@gmail.com";
            return;
        }

        // Nếu chưa có @ thì thêm @gmail.com
        if (!text.Contains('@'))
        {
            textBox.Text = text + "@gmail.com";
        }
    }

    private void OnLoaiDiaChiChanged(object? sender, RoutedEventArgs e)
    {
        var panel = this.FindControl<StackPanel>("PanelQuanHuyen");
        if (panel is null) return;

        bool isDiaChiCu = this.FindControl<RadioButton>("RadioDiaChiCu")?.IsChecked == true;
        panel.IsVisible = isDiaChiCu;

        // Khi chuyển về địa chỉ mới thì xóa giá trị Quận/Huyện
        if (!isDiaChiCu)
            Customer.QuanHuyen = null;
    }

    private void OnDateBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.SelectAll(); // Tự bôi đen khi click vào ô để gõ đè cho nhanh
        }
    }

    // 1. Ô Ngày: Giới hạn tối đa 2 chữ số, không quá 31
    private void OnNgayTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
        if (digits.Length > 2) digits = digits[..2];

        // Chặn nếu gõ ngày > 31
        if (int.TryParse(digits, out int d))
        {
            if (d > 31) digits = "31";
        }

        if (digits != box.Text)
        {
            box.Text = digits;
            box.CaretIndex = digits.Length;
        }

        // Nếu gõ đủ 2 ký tự hoặc nhập số ngày >= 4 thì chuyển sang ô Tháng
        if (digits.Length == 2 || (digits.Length == 1 && int.TryParse(digits, out int dayVal) && dayVal >= 4))
        {
            this.FindControl<TextBox>("ThangBox")?.Focus();
        }
    }

    // 2. Ô Tháng: Giới hạn tối đa 2 chữ số, không quá 12. Tháng 1 giữ nguyên chờ nhập 01, các tháng 2-9 tự lót số 0.
    private void OnThangTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
        if (digits.Length > 2) digits = digits[..2];

        // Chặn nếu gõ tháng > 12
        if (int.TryParse(digits, out int mCheck) && mCheck > 12)
        {
            digits = "12";
        }

        // Nếu nhập tháng đơn từ 2 đến 9, tự động thêm số 0 đằng trước và nhảy sang năm
        if (digits.Length == 1)
        {
            char m = digits[0];
            if (m >= '2' && m <= '9')
            {
                box.Text = "0" + m;
                box.CaretIndex = 2;
                this.FindControl<TextBox>("NamBox")?.Focus();
                return;
            }
        }

        if (digits != box.Text)
        {
            box.Text = digits;
            box.CaretIndex = digits.Length;
        }

        // Nếu nhập đủ 2 số (VD: 01, 10, 11, 12) thì nhảy sang ô Năm
        if (digits.Length == 2)
        {
            this.FindControl<TextBox>("NamBox")?.Focus();
        }
    }

    // 3. Ô Năm: Giới hạn tối đa 4 số và không vượt quá năm hiện tại (2026)
    private void OnNamTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
        if (digits.Length > 4) digits = digits[..4];

        // Nếu đã nhập đủ 4 số, kiểm tra xem có vượt quá năm hiện tại không
        if (digits.Length == 4)
        {
            if (int.TryParse(digits, out int nam))
            {
                int currentYear = DateTime.Now.Year; // Năm 2026
                if (nam > currentYear)
                {
                    digits = currentYear.ToString(); // Không cho phép lớn hơn năm hiện tại
                }
                else if (nam < 1900)
                {
                    digits = "1900";
                }
            }
        }

        if (digits != box.Text)
        {
            box.Text = digits;
            box.CaretIndex = digits.Length;
        }
    }

    // 4. Xử lý nút mở lịch chọn nhanh (Tùy chọn bổ trợ giao diện)
    // ===== Xử lý khi người dùng chọn ngày từ bảng lịch (Calendar Flyout) =====
    private void OnCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Sử dụng tường minh tên class của Avalonia để tránh xung đột với System.Globalization.Calendar
        if (sender is Avalonia.Controls.Calendar calendar && calendar.SelectedDate.HasValue)
        {
            var selectedDate = calendar.SelectedDate.Value;

            var ngayBox = this.FindControl<TextBox>("NgayBox");
            var thangBox = this.FindControl<TextBox>("ThangBox");
            var namBox = this.FindControl<TextBox>("NamBox");

            if (ngayBox is not null) ngayBox.Text = selectedDate.ToString("dd");
            if (thangBox is not null) thangBox.Text = selectedDate.ToString("MM");
            if (namBox is not null) namBox.Text = selectedDate.ToString("yyyy");
        }
    }
    private void OnEmailUserTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Nếu người dùng lỡ gõ luôn ký tự '@' ở ô tên, tự động tách phần sau sang ô domain
        if (sender is not TextBox userBox) return;
        var text = userBox.Text ?? "";
        if (text.Contains('@'))
        {
            var parts = text.Split('@', 2);
            userBox.Text = parts[0];
            userBox.CaretIndex = userBox.Text.Length;

            var domainBox = this.FindControl<TextBox>("EmailDomainBox");
            if (domainBox is not null)
            {
                domainBox.Text = "@" + parts[1];
            }
        }
    }

    // Khi đang gõ: Tự động cập nhật số lượng dạng X/10 tăng giảm theo ý muốn
    // Khi đang gõ: Tự động lọc chỉ lấy số, đồng thời cập nhật số lượng dạng X/10
    private void OnSoDienThoaiTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;

        // Chỉ lọc lấy các ký tự số
        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());

        // Nếu có ký tự chữ/ký tự lạ bị gõ vào, tự động loại bỏ ngay trên ô nhập
        if (digits != box.Text)
        {
            box.Text = digits;
            box.CaretIndex = digits.Length;
        }

        // Tìm TextBlock hiển thị thông báo (hint) tương ứng với ô SĐT đang gõ
        TextBlock? hintBox = null;
        if (box.Name == "SoDienThoai1Box") hintBox = this.FindControl<TextBlock>("Sdt1HintBox");
        else if (box.Name == "SoDienThoai2Box") hintBox = this.FindControl<TextBlock>("Sdt2HintBox");
        else if (box.Name == "SoDienThoai3Box") hintBox = this.FindControl<TextBlock>("Sdt3HintBox");

        if (hintBox is not null)
        {
            if (digits.Length == 0)
            {
                hintBox.Text = "";
            }
            else if (digits.Length == 10)
            {
                hintBox.Foreground = Avalonia.Media.Brush.Parse("#087F6E"); // Xanh hợp lệ chuẩn
                hintBox.Text = $"Đạt chuẩn 10 số (10/10)";
            }
            else if (digits.Length == 11)
            {
                hintBox.Foreground = Avalonia.Media.Brush.Parse("#D97706"); // Vàng ngoại lệ 11 số
                hintBox.Text = $"⚠️ Hơn 1 số so với chuẩn (11/10)";
            }
            else
            {
                // Các trường hợp khác (vd: 5/10, 3/10, 12/10...)
                hintBox.Foreground = Avalonia.Media.Brush.Parse("#98A2B3");
                hintBox.Text = $"({digits.Length}/10)";
            }
        }
    }
    // Khi rời ô (LostFocus): Cảnh báo nếu không đủ hoặc quá dài, nhưng VẪN CHO LƯU BÌNH THƯỜNG
    private void OnSoDienThoaiLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var digits = new string(box.Text?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());

        TextBlock? hintBox = null;
        if (box.Name == "SoDienThoai1Box") hintBox = this.FindControl<TextBlock>("Sdt1HintBox");
        else if (box.Name == "SoDienThoai2Box") hintBox = this.FindControl<TextBlock>("Sdt2HintBox");
        else if (box.Name == "SoDienThoai3Box") hintBox = this.FindControl<TextBlock>("Sdt3HintBox");

        if (hintBox is not null && digits.Length > 0)
        {
            if (digits.Length < 10)
            {
                hintBox.Foreground = Avalonia.Media.Brush.Parse("#E05252"); // Đỏ cảnh báo thiếu
                hintBox.Text = $"⚠️ Chưa đủ 10 số ({digits.Length}/10). Vẫn có thể lưu.";
            }
            else if (digits.Length > 11)
            {
                hintBox.Foreground = Avalonia.Media.Brush.Parse("#E05252"); // Đỏ cảnh báo thừa quá nhiều
                hintBox.Text = $"⚠️ Số dài hơn quy chuẩn ({digits.Length}/10). Vẫn có thể lưu.";
            }
        }
    }
}
