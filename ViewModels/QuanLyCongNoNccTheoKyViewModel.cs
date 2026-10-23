using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public sealed partial class QuanLyCongNoNccTheoKyViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public Window? OwnerWindow { get; set; }

    public ObservableCollection<KyKeToan> DanhSachKy { get; } = new();
    public ObservableCollection<NhaCungCapKyCongNoItem> NccNos { get; } = new();

    [ObservableProperty]
    private KyKeToan? selectedKy;

    [ObservableProperty]
    private NhaCungCapKyCongNoItem? selectedNccNo;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private int selectedSoKyFilterIndex; // 0: Tất cả, 1: 1 kỳ, 2: 2 kỳ, 3: >= 3 kỳ

    [ObservableProperty]
    private int selectedUuTienFilterIndex; // 0: Tất cả, 1: Cao, 2: TB, 3: Thấp

    [ObservableProperty]
    private int selectedMucNoFilterIndex; // 0: Tất cả, 1: < 5 triệu, 2: 5-10 triệu, 3: > 10 triệu

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "";

    // Thống kê tổng hợp realtime
    [ObservableProperty] private int tongSoNccNo;
    [ObservableProperty] private decimal tongNoDauKy;
    [ObservableProperty] private decimal tongPhatSinhTrongKy;
    [ObservableProperty] private decimal tongDaTraTrongKy;
    [ObservableProperty] private decimal tongNoHienTai;
    [ObservableProperty] private int soNccNoUuTienCao;

    public string TongNoDauKyText => MoneyInput.Format(TongNoDauKy) + " đ";
    public string TongPhatSinhText => MoneyInput.Format(TongPhatSinhTrongKy) + " đ";
    public string TongDaTraText => MoneyInput.Format(TongDaTraTrongKy) + " đ";
    public string TongNoHienTaiText => MoneyInput.Format(TongNoHienTai) + " đ";

    public QuanLyCongNoNccTheoKyViewModel(DatabaseService? db = null)
    {
        _db = db ?? new DatabaseService();
    }

    public async Task KhoiTaoAsync()
    {
        IsLoading = true;
        try
        {
            await TaiDanhSachKyAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khởi tạo quản lý công nợ NCC theo kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi QuanLyCongNoNccTheoKyViewModel.KhoiTaoAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task TaiDanhSachKyAsync(uint? selectKyId = null)
    {
        try
        {
            var list = await _db.GetDanhSachKyKeToanAsync();
            DanhSachKy.Clear();

            foreach (var item in list)
            {
                DanhSachKy.Add(item);
            }

            if (DanhSachKy.Count == 0)
            {
                var defaultKy = await _db.GetKyHienTaiHoacTaoMoiAsync();
                DanhSachKy.Add(defaultKy);
                SelectedKy = defaultKy;
            }
            else
            {
                if (selectKyId.HasValue && selectKyId.Value > 0)
                {
                    SelectedKy = DanhSachKy.FirstOrDefault(k => k.Id == selectKyId.Value) ?? DanhSachKy[0];
                }
                else
                {
                    SelectedKy = DanhSachKy.FirstOrDefault(k => !k.TrangThaiKhoaSo) ?? DanhSachKy[0];
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể tải danh sách kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiDanhSachKyAsync", ex);
        }
    }

    partial void OnSelectedKyChanged(KyKeToan? value)
    {
        _ = TaiCongNoTheoKyAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = TaiCongNoTheoKyAsync();
    }

    partial void OnSelectedSoKyFilterIndexChanged(int value)
    {
        _ = TaiCongNoTheoKyAsync();
    }

    partial void OnSelectedUuTienFilterIndexChanged(int value)
    {
        _ = TaiCongNoTheoKyAsync();
    }

    partial void OnSelectedMucNoFilterIndexChanged(int value)
    {
        _ = TaiCongNoTheoKyAsync();
    }

    [RelayCommand]
    public async Task TaiCongNoTheoKyAsync()
    {
        if (SelectedKy is null || SelectedKy.Id == 0)
        {
            NccNos.Clear();
            CapNhatThongKe();
            return;
        }

        IsLoading = true;
        try
        {
            int? soKyParam = SelectedSoKyFilterIndex switch
            {
                1 => 1,
                2 => 2,
                3 => 3,
                _ => null
            };

            string? uuTienParam = SelectedUuTienFilterIndex switch
            {
                1 => "CAO",
                2 => "TRUNG_BINH",
                3 => "THAP",
                _ => null
            };

            decimal? minNoParam = SelectedMucNoFilterIndex switch
            {
                1 => null,
                2 => 5_000_000m,
                3 => 10_000_000m,
                _ => null
            };

            decimal? maxNoParam = SelectedMucNoFilterIndex switch
            {
                1 => 5_000_000m,
                2 => 10_000_000m,
                3 => null,
                _ => null
            };

            var items = await _db.GetCongNoNccTheoKyAsync(
                SelectedKy.Id,
                SearchText,
                soKyParam,
                uuTienParam,
                minNoParam,
                maxNoParam);

            NccNos.Clear();
            foreach (var item in items)
            {
                NccNos.Add(item);
            }

            CapNhatThongKe();
            StatusMessage = $"Đã tải {NccNos.Count} nhà cung cấp còn nợ trong kỳ {SelectedKy.TenKy}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi nạp công nợ NCC theo kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiCongNoTheoKyAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CapNhatThongKe()
    {
        TongSoNccNo = NccNos.Count;
        TongNoDauKy = NccNos.Sum(x => x.NoDauKy);
        TongPhatSinhTrongKy = NccNos.Sum(x => x.PhatSinhTrongKy);
        TongDaTraTrongKy = NccNos.Sum(x => x.DaTraTrongKy);
        TongNoHienTai = NccNos.Sum(x => x.NoCuoiKy);
        SoNccNoUuTienCao = NccNos.Count(x => x.MucDoUuTien == "CAO");

        OnPropertyChanged(nameof(TongNoDauKyText));
        OnPropertyChanged(nameof(TongPhatSinhText));
        OnPropertyChanged(nameof(TongDaTraText));
        OnPropertyChanged(nameof(TongNoHienTaiText));
    }

    /// <summary>
    /// Tạo kỳ kế toán mới và tự động chuyển sang kỳ vừa tạo.
    /// </summary>
    [RelayCommand]
    private async Task TaoKyMoiAsync()
    {
        if (OwnerWindow is null)
            return;

        try
        {
            var dialog = new TaoKyMoiDialog();
            var result = await dialog.ShowDialog<bool>(OwnerWindow);

            if (result && dialog.KetQuaKyMoi != null)
            {
                await TaiDanhSachKyAsync(dialog.KetQuaKyMoi.Id);
                StatusMessage = $"Đã tạo kỳ mới: {dialog.KetQuaKyMoi.TenKy} thành công!";
                await NotificationWindow.ShowSuccessAsync($"Đã khởi tạo kỳ mới: {dialog.KetQuaKyMoi.TenKy}", owner: OwnerWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi tạo kỳ mới: " + ex.Message;
            SafeLog.WriteError("Lỗi TaoKyMoiAsync", ex);
        }
    }

    /// <summary>
    /// Đồng bộ lại nợ cuối kỳ trước sang làm nợ đầu kỳ này cho các nhà cung cấp.
    /// </summary>
    [RelayCommand]
    private async Task LoadTonKyTruocAsync()
    {
        if (SelectedKy is null)
            return;

        if (SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã khóa sổ (kết kỳ), không thể đồng bộ lại.", owner: OwnerWindow);
            return;
        }

        var kyTruoc = DanhSachKy
            .Where(k => k.Id != SelectedKy.Id && k.NgayBatDauKy < SelectedKy.NgayBatDauKy)
            .OrderByDescending(k => k.NgayBatDauKy)
            .FirstOrDefault();

        if (kyTruoc is null)
        {
            await NotificationWindow.ShowWarningAsync("Không tìm thấy kỳ trước để kế thừa số liệu công nợ.", owner: OwnerWindow);
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Đồng bộ công nợ từ kỳ trước",
                $"Bạn có chắc muốn nạp lại công nợ cuối kỳ từ \"{kyTruoc.TenKy}\" sang kỳ \"{SelectedKy.TenKy}\" không?\n" +
                "Công nợ đầu kỳ của các nhà cung cấp sẽ được cập nhật theo số nợ còn lại cuối kỳ trước.");

            if (!confirmed)
                return;

            IsLoading = true;
            var rows = await _db.LoadLaiNoKyTruocNccAsync(SelectedKy.Id, kyTruoc.Id);
            await TaiCongNoTheoKyAsync();

            StatusMessage = $"Đã đồng bộ công nợ từ {kyTruoc.TenKy} cho {rows} nhà cung cấp.";
            await NotificationWindow.ShowSuccessAsync($"Đã nạp công nợ từ \"{kyTruoc.TenKy}\" cho {rows} nhà cung cấp.", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi nạp công nợ kỳ trước: " + ex.Message;
            SafeLog.WriteError("Lỗi LoadTonKyTruocAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Khai báo công nợ đầu kỳ cho nhà cung cấp (mở form Thêm công nợ đầu kỳ loại NHA_CUNG_CAP).
    /// </summary>
    [RelayCommand]
    private async Task ThemCongNoDauKyAsync()
    {
        if (OwnerWindow is null)
            return;

        try
        {
            var window = new ThemCongNoDauKyWindow("NHA_CUNG_CAP", SelectedNccNo?.NhaCungCapId);
            var result = await window.ShowDialog<bool>(OwnerWindow);
            if (result)
            {
                await TaiCongNoTheoKyAsync();
                StatusMessage = "Đã cập nhật công nợ đầu kỳ nhà cung cấp thành công!";
                await NotificationWindow.ShowSuccessAsync("Đã lưu công nợ đầu kỳ NCC thành công!", owner: OwnerWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi khai báo công nợ đầu kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi ThemCongNoDauKyAsync", ex);
        }
    }

    /// <summary>
    /// Chốt số dư công nợ của kỳ hiện tại và chuyển số dư sang kỳ mới.
    /// </summary>
    [RelayCommand]
    private async Task KetKyAsync()
    {
        if (SelectedKy is null)
            return;

        if (SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã được kết kỳ trước đó.", owner: OwnerWindow);
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Xác nhận Kết kỳ công nợ nhà cung cấp",
                $"Bạn có chắc chắn muốn KẾT KỲ công nợ NCC cho \"{SelectedKy.TenKy}\" không?\n\n" +
                "⚠️ Khi kết kỳ:\n" +
                "• Số liệu Công nợ cuối kỳ sẽ được chốt tự động = Nợ đầu kỳ + Phát sinh nhập hàng - Đã trả.\n" +
                "• Kỳ sẽ được chuyển sang trạng thái [Khóa sổ] để bảo toàn số liệu kế toán.\n" +
                "• Số dư nợ cuối này sẽ tự động kết chuyển sang làm nợ đầu kỳ kế tiếp, tăng số kỳ nợ và cập nhật mức độ ưu tiên trả nợ.");

            if (!confirmed)
                return;

            IsLoading = true;
            int soNccChuyen = await _db.KetKyCongNoNccAsync(SelectedKy.Id);

            await TaiDanhSachKyAsync(SelectedKy.Id);
            StatusMessage = $"🎉 Kỳ {SelectedKy.TenKy} đã kết kỳ thành công! Đã kết chuyển {soNccChuyen} NCC còn nợ sang kỳ mới.";
            await NotificationWindow.ShowSuccessAsync($"Đã kết kỳ thành công. Kết chuyển {soNccChuyen} nhà cung cấp còn nợ sang kỳ mới.", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi kết kỳ công nợ NCC: " + ex.Message;
            SafeLog.WriteError("Lỗi KetKyAsync", ex);
            await NotificationWindow.ShowErrorAsync("Lỗi kết kỳ: " + ex.Message, owner: OwnerWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Mở lại kỳ công nợ đã khóa sổ để hiệu chỉnh.
    /// </summary>
    [RelayCommand]
    private async Task MoLaiKyAsync()
    {
        if (SelectedKy is null || !SelectedKy.TrangThaiKhoaSo)
            return;

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Mở lại kỳ công nợ",
                $"Bạn có chắc muốn mở lại kỳ \"{SelectedKy.TenKy}\" để chỉnh sửa dữ liệu công nợ không?");

            if (!confirmed)
                return;

            IsLoading = true;
            await _db.MoLaiKyCongNoNccAsync(SelectedKy.Id);
            await TaiDanhSachKyAsync(SelectedKy.Id);

            StatusMessage = $"Đã mở lại kỳ {SelectedKy.TenKy}.";
            await NotificationWindow.ShowSuccessAsync($"Đã mở lại kỳ \"{SelectedKy.TenKy}\".", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi mở lại kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi MoLaiKyAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Thanh toán nợ cho nhà cung cấp trên dòng đang chọn.
    /// </summary>
    [RelayCommand]
    private async Task ThanhToanCongNoAsync(NhaCungCapKyCongNoItem? item)
    {
        item ??= SelectedNccNo;
        if (item is null || OwnerWindow is null)
        {
            await NotificationWindow.ShowWarningAsync("Vui lòng chọn một nhà cung cấp để thanh toán nợ.", owner: OwnerWindow);
            return;
        }

        if (SelectedKy is null || SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã kết kỳ, không thể thanh toán công nợ.", owner: OwnerWindow);
            return;
        }

        if (item.NoCuoiKy <= 0)
        {
            await NotificationWindow.ShowWarningAsync($"Nhà cung cấp \"{item.TenNhaCungCap}\" hiện không còn nợ.", owner: OwnerWindow);
            return;
        }

        try
        {
            var dialog = new TraNoNhaCungCapWindow(item.TenNhaCungCap, item.NoCuoiKy);
            var ketQua = await dialog.ShowDialog<ThuNoHoaDonKetQua?>(OwnerWindow);

            if (ketQua is null)
            {
                StatusMessage = "Đã hủy thao tác thanh toán nợ.";
                return;
            }

            IsLoading = true;
            var res = await _db.TraNoNhaCungCapAsync(
                item.NhaCungCapId,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.GhiChu,
                SelectedKy.Id);

            await TaiCongNoTheoKyAsync();

            var phieuText = !string.IsNullOrWhiteSpace(res.MaPhieuChi) ? $" (Phiếu chi: {res.MaPhieuChi})" : "";
            StatusMessage = $"Đã thanh toán {ketQua.SoTien:N0} đ cho {item.TenNhaCungCap}. Còn nợ: {res.ConNoConLai:N0} đ.{phieuText}";
            await NotificationWindow.ShowSuccessAsync(
                $"Thanh toán thành công {ketQua.SoTien:N0} đ cho {item.TenNhaCungCap}.\nCòn nợ lại: {res.ConNoConLai:N0} đ.{phieuText}",
                owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi thanh toán nợ nhà cung cấp: " + ex.Message;
            SafeLog.WriteError("Lỗi ThanhToanCongNoAsync", ex);
            await NotificationWindow.ShowErrorAsync("Không thanh toán được nợ: " + ex.Message, owner: OwnerWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Xuất danh sách công nợ nhà cung cấp theo kỳ ra file Excel.
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        if (OwnerWindow is null || NccNos.Count == 0)
        {
            await NotificationWindow.ShowWarningAsync("Không có dữ liệu công nợ để xuất file.", owner: OwnerWindow);
            return;
        }

        try
        {
            var file = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách công nợ NCC theo kỳ",
                DefaultExtension = "xlsx",
                SuggestedFileName = $"CongNoNCC_{SelectedKy?.TenKy.Replace("/", "_").Replace(" ", "")}_{DateTime.Now:yyyyMMdd}.xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Tệp Excel (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
                }
            });

            if (file is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Công nợ NCC theo kỳ");

            // Header
            ws.Cell(1, 1).Value = $"BẢNG THEO DÕI CÔNG NỢ NHÀ CUNG CẤP THEO KỲ - {SelectedKy?.TenKy.ToUpperInvariant()}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 9).Merge();

            ws.Cell(2, 1).Value = $"Thời gian: {SelectedKy?.ThoiGianHienThi} | Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 9).Merge();

            string[] headers = [
                "MÃ NCC", "TÊN NHÀ CUNG CẤP", "ĐIỆN THOẠI", "NỢ ĐẦU KỲ",
                "PHÁT SINH TRONG KỲ", "ĐÃ TRẢ TRONG KỲ", "NỢ CUỐI KỲ", "SỐ KỲ NỢ", "MỨC ĐỘ ƯU TIÊN"
            ];

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2EFEA");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int rowIdx = 5;
            foreach (var item in NccNos)
            {
                ws.Cell(rowIdx, 1).Value = item.MaNhaCungCap;
                ws.Cell(rowIdx, 2).Value = item.TenNhaCungCap;
                ws.Cell(rowIdx, 3).Value = item.SoDienThoai;
                ws.Cell(rowIdx, 4).Value = item.NoDauKy;
                ws.Cell(rowIdx, 5).Value = item.PhatSinhTrongKy;
                ws.Cell(rowIdx, 6).Value = item.DaTraTrongKy;
                ws.Cell(rowIdx, 7).Value = item.NoCuoiKy;
                ws.Cell(rowIdx, 8).Value = item.SoKyNo;
                ws.Cell(rowIdx, 9).Value = item.MucDoUuTienText;

                ws.Cell(rowIdx, 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 7).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rowIdx, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(rowIdx, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                rowIdx++;
            }

            // Dòng tổng cộng
            ws.Cell(rowIdx, 1).Value = "TỔNG CỘNG";
            ws.Cell(rowIdx, 1).Style.Font.Bold = true;
            ws.Range(rowIdx, 1, rowIdx, 3).Merge();

            ws.Cell(rowIdx, 4).FormulaA1 = $"SUM(D5:D{rowIdx - 1})";
            ws.Cell(rowIdx, 5).FormulaA1 = $"SUM(E5:E{rowIdx - 1})";
            ws.Cell(rowIdx, 6).FormulaA1 = $"SUM(F5:F{rowIdx - 1})";
            ws.Cell(rowIdx, 7).FormulaA1 = $"SUM(G5:G{rowIdx - 1})";

            for (int col = 4; col <= 7; col++)
            {
                ws.Cell(rowIdx, col).Style.Font.Bold = true;
                ws.Cell(rowIdx, col).Style.NumberFormat.Format = "#,##0";
            }

            ws.Columns().AdjustToContents();

            await using var stream = await file.OpenWriteAsync();
            workbook.SaveAs(stream);

            StatusMessage = "Đã xuất file Excel công nợ NCC thành công!";
            await NotificationWindow.ShowSuccessAsync($"Đã xuất file: {file.Name}", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi xuất file Excel: " + ex.Message;
            SafeLog.WriteError("Lỗi XuatExcelAsync", ex);
            await NotificationWindow.ShowErrorAsync("Không xuất được file Excel: " + ex.Message, owner: OwnerWindow);
        }
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (OwnerWindow is null)
            return false;

        var confirm = new ConfirmationWindow(
            title,
            message,
            "Đồng ý",
            "Hủy bỏ");

        return await confirm.ShowDialog<bool>(OwnerWindow);
    }
}
