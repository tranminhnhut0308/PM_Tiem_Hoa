using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class TimKiemCongNoWindow : Window
{
    private readonly DatabaseService _db = new();
    private readonly List<CongNoItemRow> _items = new();
    private int _currentPage = 1;
    private int _pageSize = 25;
    private int _totalCount;
    private int _totalPages = 1;
    private bool _dangTai;

    public TimKiemCongNoWindow() : this(null)
    {
    }

    public TimKiemCongNoWindow(string? loaiDoiTacMacDinh = null)
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(loaiDoiTacMacDinh))
            {
                var targetTag = loaiDoiTacMacDinh.Trim().ToUpperInvariant();
                for (int i = 0; i < LoaiDoiTacCombo.ItemCount; i++)
                {
                    if (LoaiDoiTacCombo.Items[i] is ComboBoxItem cbi &&
                        string.Equals(cbi.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                    {
                        LoaiDoiTacCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            await TaiDuLieuCongNoAsync();
        };
    }

    private async Task TaiDuLieuCongNoAsync()
    {
        if (_dangTai)
            return;

        _dangTai = true;
        LoadingBorder.IsVisible = true;

        try
        {
            var loaiDoiTac = (LoaiDoiTacCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TAT_CA";
            var trangThai = (TrangThaiCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TAT_CA";
            var tuKhoa = (SearchTextBox.Text ?? "").Trim();
            var tuNgay = TuNgayPicker.SelectedDate;
            var denNgay = DenNgayPicker.SelectedDate;

            if (tuNgay.HasValue && denNgay.HasValue && tuNgay.Value > denNgay.Value)
            {
                // Hoán đổi nếu người dùng chọn ngược ngày
                var temp = tuNgay;
                tuNgay = denNgay;
                denNgay = temp;
                TuNgayPicker.SelectedDate = tuNgay;
                DenNgayPicker.SelectedDate = denNgay;
            }

            var criteria = new CongNoFilterCriteria
            {
                LoaiDoiTac = loaiDoiTac,
                TrangThaiThanhToan = trangThai,
                TuKhoa = tuKhoa,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                Page = _currentPage,
                PageSize = _pageSize
            };

            var (rows, totalCount, thongKe) = await _db.TimKiemCongNoPagedAsync(criteria);

            _items.Clear();
            _items.AddRange(rows);
            DebtItemsControl.ItemsSource = null;
            DebtItemsControl.ItemsSource = _items;

            _totalCount = totalCount;
            _totalPages = (int)Math.Ceiling(_totalCount / (double)_pageSize);
            if (_totalPages < 1) _totalPages = 1;

            // Cập nhật số liệu KPI cards
            TxtTongNoPhaiThu.Text = $"{thongKe.TongNoPhaiThu:N0} đ";
            TxtTongNoPhaiTra.Text = $"{thongKe.TongNoPhaiTra:N0} đ";
            TxtTongDaThanhToan.Text = $"{thongKe.TongDaThanhToan:N0} đ";
            TxtTongConNoRong.Text = $"{thongKe.TongConNoRong:N0} đ";

            // Cập nhật trạng thái rỗng / có dữ liệu
            EmptyBorder.IsVisible = _items.Count == 0;

            // Cập nhật thông tin phân trang
            if (_totalCount == 0)
            {
                TxtRangeInfo.Text = "Không có bản ghi công nợ nào";
                TxtCurrentPageInfo.Text = "Trang 0 / 0";
            }
            else
            {
                var from = (_currentPage - 1) * _pageSize + 1;
                var to = Math.Min(from + _items.Count - 1, _totalCount);
                TxtRangeInfo.Text = $"Hiển thị {from:N0} - {to:N0} trong tổng số {_totalCount:N0} chứng từ công nợ";
                TxtCurrentPageInfo.Text = $"Trang {_currentPage} / {_totalPages}";
            }

            CapNhatNutDieuHuongTrang();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải dữ liệu tìm kiếm công nợ", ex);
        }
        finally
        {
            _dangTai = false;
            LoadingBorder.IsVisible = false;
        }
    }

    private void CapNhatNutDieuHuongTrang()
    {
        BtnFirstPage.IsEnabled = _currentPage > 1;
        BtnPrevPage.IsEnabled = _currentPage > 1;
        BtnNextPage.IsEnabled = _currentPage < _totalPages;
        BtnLastPage.IsEnabled = _currentPage < _totalPages;
    }

    private async void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentPage = 1;
        await TaiDuLieuCongNoAsync();
    }

    private async void SearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _currentPage = 1;
            await TaiDuLieuCongNoAsync();
        }
    }

    private async void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = "";
        LoaiDoiTacCombo.SelectedIndex = 0;
        TrangThaiCombo.SelectedIndex = 0;
        TuNgayPicker.SelectedDate = null;
        DenNgayPicker.SelectedDate = null;
        _currentPage = 1;

        await TaiDuLieuCongNoAsync();
    }

    private async void PageSizeCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PageSizeCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var size) && size > 0)
        {
            if (_pageSize != size)
            {
                _pageSize = size;
                _currentPage = 1;
                if (IsLoaded)
                {
                    await TaiDuLieuCongNoAsync();
                }
            }
        }
    }

    private async void FirstPage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage = 1;
            await TaiDuLieuCongNoAsync();
        }
    }

    private async void PrevPage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            await TaiDuLieuCongNoAsync();
        }
    }

    private async void NextPage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            await TaiDuLieuCongNoAsync();
        }
    }

    private async void LastPage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage = _totalPages;
            await TaiDuLieuCongNoAsync();
        }
    }

    private async void ThemNoThanhToan_OnClick(object? sender, RoutedEventArgs e)
    {
        var loaiDoiTac = (LoaiDoiTacCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var win = new ThemNoThanhToanWindow(loaiDoiTac);
        var res = await win.ShowDialog<bool>(this);
        if (res)
        {
            await TaiDuLieuCongNoAsync();
        }
    }

    private async void QuickPayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CongNoItemRow row)
        {
            var win = new ThemNoThanhToanWindow(row.LoaiDoiTac, row.DoiTacId, row.MaChungTu);
            var res = await win.ShowDialog<bool>(this);
            if (res)
            {
                await TaiDuLieuCongNoAsync();
            }
        }
    }

    private async void ExportExcelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_dangTai || _totalCount == 0)
            return;

        try
        {
            var loaiDoiTac = (LoaiDoiTacCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TAT_CA";
            var trangThai = (TrangThaiCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TAT_CA";
            var tuKhoa = (SearchTextBox.Text ?? "").Trim();
            var tuNgay = TuNgayPicker.SelectedDate;
            var denNgay = DenNgayPicker.SelectedDate;

            // Xuất tối đa 5000 dòng để bảo đảm an toàn bộ nhớ
            var exportCriteria = new CongNoFilterCriteria
            {
                LoaiDoiTac = loaiDoiTac,
                TrangThaiThanhToan = trangThai,
                TuKhoa = tuKhoa,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                Page = 1,
                PageSize = Math.Min(5000, _totalCount)
            };

            var (exportRows, _, thongKe) = await _db.TimKiemCongNoPagedAsync(exportCriteria);
            if (exportRows.Count == 0)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("CongNo");

            // Tiêu đề báo cáo
            ws.Cell(1, 1).Value = "BÁO CÁO TRA CỨU CÔNG NỢ - WINWON";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#087F6E");

            ws.Cell(2, 1).Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm} | Tổng số chứng từ: {exportRows.Count:N0}";
            ws.Cell(2, 1).Style.Font.Italic = true;

            // Header bảng
            var headers = new[]
            {
                "STT", "Ngày chứng từ", "Loại công nợ", "Mã đối tác", "Tên đối tác",
                "Số điện thoại", "Mã chứng từ", "Loại chứng từ", "Tổng tiền (VNĐ)",
                "Đã trả (VNĐ)", "Còn nợ (VNĐ)", "Trạng thái"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = ws.Cell(4, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0F2FE");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int rowIdx = 5;
            foreach (var item in exportRows)
            {
                ws.Cell(rowIdx, 1).Value = item.SoThuTu;
                ws.Cell(rowIdx, 2).Value = item.NgayPhatSinh.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(rowIdx, 3).Value = item.LoaiCongNo;
                ws.Cell(rowIdx, 4).Value = item.MaDoiTac;
                ws.Cell(rowIdx, 5).Value = item.TenDoiTac;
                ws.Cell(rowIdx, 6).Value = item.SoDienThoai ?? "";
                ws.Cell(rowIdx, 7).Value = item.MaChungTu;
                ws.Cell(rowIdx, 8).Value = item.LoaiChungTu;
                ws.Cell(rowIdx, 9).Value = item.TongTien;
                ws.Cell(rowIdx, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 10).Value = item.DaThanhToan;
                ws.Cell(rowIdx, 10).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 11).Value = item.ConNo;
                ws.Cell(rowIdx, 11).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 12).Value = item.TrangThaiThanhToan;

                rowIdx++;
            }

            // Dòng tổng cộng
            ws.Cell(rowIdx, 8).Value = "TỔNG CỘNG:";
            ws.Cell(rowIdx, 8).Style.Font.Bold = true;
            ws.Cell(rowIdx, 9).FormulaA1 = $"SUM(I5:I{rowIdx - 1})";
            ws.Cell(rowIdx, 9).Style.Font.Bold = true;
            ws.Cell(rowIdx, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(rowIdx, 10).FormulaA1 = $"SUM(J5:J{rowIdx - 1})";
            ws.Cell(rowIdx, 10).Style.Font.Bold = true;
            ws.Cell(rowIdx, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(rowIdx, 11).FormulaA1 = $"SUM(K5:K{rowIdx - 1})";
            ws.Cell(rowIdx, 11).Style.Font.Bold = true;
            ws.Cell(rowIdx, 11).Style.NumberFormat.Format = "#,##0";

            ws.Columns().AdjustToContents();

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách công nợ ra file Excel",
                SuggestedFileName = $"BaoCaoCongNo_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Workbook (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
                }
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                workbook.SaveAs(stream);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel công nợ", ex);
        }
    }
}
