using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

/// <summary>
/// Cửa sổ ánh xạ cột: cho phép chọn cột nào của file ứng với trường nào, đổi vai
/// trò từng sheet hoặc bỏ qua sheet không cần nhập. Chỉ đọc dòng tiêu đề, chưa
/// đọc dữ liệu và chưa ghi gì xuống database.
/// </summary>
public partial class HoaDonImportMappingWindow : Window
{
    private readonly MappingHoaDon _mapping;

    /// <summary>Số dòng mẫu lấy từ sheet để dựng danh sách cột và xem trước giá trị.</summary>
    private const int SoDongXemTruoc = 5;

    private HoaDonImportMappingWindow(ThongTinFileHoaDon thongTin)
    {
        InitializeComponent();

        _mapping = thongTin.Mapping;

        Title = "Ánh xạ cột của file hóa đơn";
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        TieudeFileTextBlock.Text =
            $"File: {thongTin.TenFile} — đọc được {_mapping.Sheets.Count:N0} sheet. "
            + "Kiểm tra lại các cột bên dưới rồi bấm \"Tiếp tục\".";

        foreach (var sheet in _mapping.Sheets)
        {
            var panel = TaoPanelSheet(sheet);

            if (panel is not null)
                SheetPanels.Children.Add(panel);
        }

        CapNhatTrangThai();
    }

    /// <summary>
    /// Mở cửa sổ ánh xạ cột. Trả về ánh xạ đã chỉnh sửa, null nghĩa là người
    /// dùng hủy và không nhập gì.
    /// </summary>
    public static Task<MappingHoaDon?> ShowAsync(Window owner, ThongTinFileHoaDon thongTin)
    {
        var dialog = new HoaDonImportMappingWindow(thongTin);
        return dialog.ShowDialog<MappingHoaDon?>(owner);
    }

    /// <summary>Dựng phần ánh xạ của một sheet: vai trò, danh sách cột và các ô chọn.</summary>
    private Control TaoPanelSheet(SheetHoaDon sheet)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(0, 0, 0, 12) };

        var tieuDe = new TextBlock
        {
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brush.Parse("#163F38"),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = $"📄 {sheet.TenSheet}"
        };

        panel.Children.Add(tieuDe);

        // Hàng chọn vai trò sheet và bỏ qua.
        var hangThietLap = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 16 };

        var boQua = new CheckBox
        {
            Content = "Bỏ qua sheet này",
            FontSize = 13,
            IsChecked = sheet.BoQua
        };

        var loai = new ComboBox
        {
            ItemsSource = sheet.LuaChonLoai,
            SelectedItem = sheet.Loai,
            MinWidth = 240,
            FontSize = 13
        };

        hangThietLap.Children.Add(new TextBlock
        {
            Text = "Vai trò:",
            FontSize = 13,
            Foreground = Avalonia.Media.Brush.Parse("#5A7B72"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        hangThietLap.Children.Add(loai);
        hangThietLap.Children.Add(boQua);

        panel.Children.Add(hangThietLap);

        var xemTruoc = TaoBangXemTruoc(sheet);

        if (xemTruoc is not null)
            panel.Children.Add(xemTruoc);

        var truongs = LayTruongTheoLoai(sheet.Loai);

        panel.Children.Add(new TextBlock
        {
            FontSize = 12,
            Foreground = Avalonia.Media.Brush.Parse("#5A7B72"),
            Text = $"Tiêu đề nằm ở dòng {sheet.DongTieuDe}, dữ liệu bắt đầu từ dòng {sheet.DongTieuDe + 1}."
        });

        panel.Children.Add(TaoBangTruong(sheet, truongs));

        // Đổi vai trò thì danh sách trường cần ánh xạ cũng đổi theo.
        loai.SelectionChanged += (_, _) =>
        {
            if (loai.SelectedItem is LoaiSheetHoaDon loaiMoi)
            {
                sheet.Loai = loaiMoi;
                CapNhatDanhSachTruong(sheet, panel, truongs);
            }
        };

        boQua.IsCheckedChanged += (_, _) => CapNhatTrangThai();

        return new Border
        {
            Background = Avalonia.Media.Brush.Parse("#FFFFFF"),
            BorderBrush = Avalonia.Media.Brush.Parse("#D7E7E2"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(10),
            Padding = new Avalonia.Thickness(16, 14),
            Child = panel
        };
    }

    private static List<TruongHoaDon> LayTruongTheoLoai(LoaiSheetHoaDon loai) =>
        loai == LoaiSheetHoaDon.HoaDon
            ? [.. TenTruongHoaDon.ThongTinHoaDon]
            : [.. TenTruongHoaDon.HangHoa];

    /// <summary>
    /// Bảng chọn cột cho từng trường. Ô "(không dùng)" nghĩa là bỏ trống trường
    /// đó, phần mềm sẽ tự tính hoặc lấy giá trị mặc định.
    /// </summary>
    private Border TaoBangTruong(SheetHoaDon sheet, List<TruongHoaDon> truongs)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("250,*,150")
        };

        grid.Children.Add(TaoHeader("Trường cần nhập", 0));
        grid.Children.Add(TaoHeader("Cột trong file", 1));
        grid.Children.Add(TaoHeader("Gợi ý", 2));

        var batBuoc = new HashSet<TruongHoaDon>(
            MappingHoaDon.LayTruongBatBuoc(sheet.Loai));

        foreach (var truong in truongs)
        {
            var luaChon = new ComboBox
            {
                ItemsSource = TaoDanhSachCot(sheet),
                FontSize = 13,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            luaChon.SelectedItem = sheet.Map.TryGetValue(truong, out var viTri)
                ? sheet.Cot.FirstOrDefault(c => c.ViTri == viTri)
                : null;

            luaChon.SelectionChanged += (_, _) =>
            {
                if (luaChon.SelectedItem is not CotFileHoaDon cot)
                    sheet.Map.Remove(truong);
                else
                    sheet.Map[truong] = cot.ViTri;

                CapNhatTrangThai();
            };

            var tenTruong = new TextBlock
            {
                Text = (batBuoc.Contains(truong) ? "• " : "  ") + TenTruongHoaDon.Lay(truong),
                FontSize = 13,
                FontWeight = batBuoc.Contains(truong)
                    ? Avalonia.Media.FontWeight.SemiBold
                    : Avalonia.Media.FontWeight.Normal,
                Foreground = Avalonia.Media.Brush.Parse("#263B35"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            grid.Children.Add(tenTruong);
            SetColumn(tenTruong, 0);

            grid.Children.Add(luaChon);
            SetColumn(luaChon, 1);

            var goiY = new TextBlock
            {
                Text = TenTruongHoaDon.GoiY(truong),
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = Avalonia.Media.Brush.Parse("#61736E"),
                Margin = new Avalonia.Thickness(10, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            grid.Children.Add(goiY);
            SetColumn(goiY, 2);
        }

        return new Border
        {
            BorderBrush = Avalonia.Media.Brush.Parse("#D7E7E2"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Child = grid
        };
    }

    /// <summary>
    /// Khi đổi vai trò sheet, thay bảng chọn cột bằng bảng mới theo vai trò mới.
    /// Cột đã gán và vẫn dùng được thì giữ nguyên, phần không còn liên quan thì
    /// bỏ khỏi ánh xạ.
    /// </summary>
    private void CapNhatDanhSachTruong(
        SheetHoaDon sheet,
        Panel panel,
        List<TruongHoaDon> truongsCu)
    {
        var truongsMoi = LayTruongTheoLoai(sheet.Loai);

        foreach (var truong in sheet.Map.Keys.ToList())
        {
            if (!truongsMoi.Contains(truong))
                sheet.Map.Remove(truong);
        }

        var bangCu = panel.Children.OfType<Border>().LastOrDefault();

        if (bangCu is null)
            return;

        var viTri = panel.Children.IndexOf(bangCu);
        panel.Children.RemoveAt(viTri);
        panel.Children.Insert(viTri, TaoBangTruong(sheet, truongsMoi));

        CapNhatTrangThai();
    }

    private static List<CotFileHoaDon> TaoDanhSachCot(SheetHoaDon sheet)
    {
        var cot = new List<CotFileHoaDon>
        {
            new() { ViTri = -1, Ten = "(không dùng cột này)" }
        };

        cot.AddRange(sheet.Cot);
        return cot;
    }

    /// <summary>Bảng xem trước vài dòng đầu của sheet để dễ đối chiếu tên cột.</summary>
    private static Border? TaoBangXemTruoc(SheetHoaDon sheet)
    {
        var mau = DocDongMau(sheet);

        if (mau.Count == 0)
            return null;

        var grid = new Grid();
        var cotDef = new ColumnDefinitions();

        for (var i = 0; i < mau[0].Length; i++)
            cotDef.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions = cotDef;

        for (var i = 0; i < mau[0].Length; i++)
        {
            var tieuDe = new TextBlock
            {
                Text = TenTruongHoaDon.CotSangChuoi(i + 1) + " — "
                    + (sheet.Cot[i].TenHienThi),
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = Avalonia.Media.Brush.Parse("#163F38"),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                Margin = new Avalonia.Thickness(6, 0, 6, 0)
            };

            grid.Children.Add(tieuDe);
            SetColumn(tieuDe, i);
        }

        foreach (var dong in mau)
        {
            for (var i = 0; i < dong.Length && i < mau[0].Length; i++)
            {
                var o = new TextBlock
                {
                    Text = dong[i],
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brush.Parse("#5A7B72"),
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    Margin = new Avalonia.Thickness(6, 1, 6, 1)
                };

                grid.Children.Add(o);
                SetColumn(o, i);
            }
        }

        return new Border
        {
            Background = Avalonia.Media.Brush.Parse("#F7FAF9"),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(10, 8),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = grid
            }
        };
    }

    /// <summary>Lấy vài dòng dữ liệu đầu tiên sau dòng tiêu đề để xem trước.</summary>
    private static List<string[]> DocDongMau(SheetHoaDon sheet)
        => string.IsNullOrEmpty(sheet.DuongDanFile)
            ? []
            : HoaDonImportService.DocDongMau(
                sheet.DuongDanFile,
                sheet.TenSheet,
                sheet.DongTieuDe,
                sheet.CotDau,
                sheet.Cot.Count,
                SoDongXemTruoc);

    private static TextBlock TaoHeader(string noiDung, int cot)
    {
        var textBlock = new TextBlock
        {
            Text = noiDung,
            FontSize = 12,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brush.Parse("#163F38"),
            Margin = new Avalonia.Thickness(6, 8, 6, 8)
        };

        SetColumn(textBlock, cot);
        return textBlock;
    }

    private static void SetColumn(Control control, int cot)
        => Avalonia.Controls.Grid.SetColumn(control, cot);

    /// <summary>Cập nhật phần chú thích và trạng thái nút Tiếp tục.</summary>
    private void CapNhatTrangThai()
    {
        var thieu = _mapping.CoTruongChuaGan().Distinct().ToList();

        if (_mapping.SheetsDung.Any())
        {
            TieudeTrangThaiTextBlock.Text = thieu.Count == 0
                ? "✅ Đã đủ cột cần thiết. Bấm \"Tiếp tục\" để xem trước hóa đơn."
                : "⚠️ Còn thiếu: " + string.Join(", ", thieu.Select(TenTruongHoaDon.Lay))
                    + ". Bấm \"Tiếp tục\" sẽ báo những hóa đơn bị ảnh hưởng.";
        }
        else
        {
            TieudeTrangThaiTextBlock.Text =
                "❌ Chưa sheet nào dùng được. Hãy bỏ chọn \"Bỏ qua sheet này\" "
                + "và gán cột \"Mã hóa đơn\" cho ít nhất một sheet.";
        }

        TieudeTrangThaiTextBlock.Foreground = Avalonia.Media.Brush.Parse(
            _mapping.SheetsDung.Any() && thieu.Count == 0 ? "#1B7A4B" : "#A32020");

        TienTucButton.IsEnabled = _mapping.SheetsDung.Any();
    }

    private void HuyButton_OnClick(object? sender, RoutedEventArgs e)
        => Close();

    private void TienTucButton_OnClick(object? sender, RoutedEventArgs e)
        => Close(_mapping);
}
