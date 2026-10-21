using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

public sealed partial class QuanLyKhoTheoKyViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public Window? OwnerWindow { get; set; }
    public Action? ChuyenSangNhapHangAction { get; set; }

    public ObservableCollection<KyKeToan> DanhSachKy { get; } = new();
    public ObservableCollection<HangHoaKyKhoItem> HangHoas { get; } = new();

    [ObservableProperty]
    private KyKeToan? selectedKy;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private ProductOption? selectedNhom;

    [ObservableProperty]
    private IReadOnlyList<ProductOption> nhomOptions = new List<ProductOption>();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "";

    // Thống kê tổng hợp realtime
    [ObservableProperty] private int tongSoMatHang;
    [ObservableProperty] private decimal tongSoLuongDauKy;
    [ObservableProperty] private decimal tongSoLuongDieuChinh;
    [ObservableProperty] private decimal tongSoLuongThucTeDauKy;
    [ObservableProperty] private decimal tongGiaTriTonDauKy;
    [ObservableProperty] private decimal tongNhapTrongKy;
    [ObservableProperty] private decimal tongXuatTrongKy;
    [ObservableProperty] private decimal tongTonHienTai;

    public QuanLyKhoTheoKyViewModel(DatabaseService? db = null)
    {
        _db = db ?? new DatabaseService();
    }

    public async Task KhoiTaoAsync()
    {
        IsLoading = true;
        try
        {
            // 1. Tải danh mục nhóm hàng
            var nhoms = await _db.GetNhomSanPhamAsync();
            var fullNhoms = new List<ProductOption>
            {
                new() { Id = 0, Name = "Tất cả nhóm hàng" },
                new() { Id = 999999, Name = "-- Chưa phân nhóm --" }
            };
            fullNhoms.AddRange(nhoms);
            NhomOptions = fullNhoms;
            SelectedNhom = fullNhoms[0];

            // 2. Tải danh sách các kỳ
            await TaiDanhSachKyAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khởi tạo quản lý kho theo kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi QuanLyKhoTheoKyViewModel.KhoiTaoAsync", ex);
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
                // Tự động tạo kỳ cho tháng hiện tại nếu chưa có
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
                    // Ưu tiên chọn kỳ đang mở (active)
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
        _ = TaiHangHoaTheoKyAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = TaiHangHoaTheoKyAsync();
    }

    partial void OnSelectedNhomChanged(ProductOption? value)
    {
        _ = TaiHangHoaTheoKyAsync();
    }

    [RelayCommand]
    public async Task TaiHangHoaTheoKyAsync()
    {
        if (SelectedKy is null || SelectedKy.Id == 0)
        {
            HangHoas.Clear();
            CapNhatThongKe();
            return;
        }

        IsLoading = true;
        try
        {
            uint? nhomId = (SelectedNhom != null && SelectedNhom.Id > 0) ? SelectedNhom.Id : null;
            var list = await _db.GetHangHoaTheoKyAsync(SelectedKy.Id, SearchText, nhomId);

            HangHoas.Clear();
            foreach (var item in list)
            {
                // Lắng nghe thay đổi trên từng dòng để cập nhật thống kê ngay lập tức
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(HangHoaKyKhoItem.SoLuongDieuChinh)
                        or nameof(HangHoaKyKhoItem.GiaVonDauKy)
                        or nameof(HangHoaKyKhoItem.SoLuongDauKy))
                    {
                        CapNhatThongKe();
                    }
                };

                HangHoas.Add(item);
            }

            CapNhatThongKe();
            StatusMessage = $"Đã nạp {HangHoas.Count} mặt hàng trong kỳ {SelectedKy.TenKy}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi tải hàng hóa theo kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiHangHoaTheoKyAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CapNhatThongKe()
    {
        TongSoMatHang = HangHoas.Count;
        TongSoLuongDauKy = HangHoas.Sum(x => x.SoLuongDauKy);
        TongSoLuongDieuChinh = HangHoas.Sum(x => x.SoLuongDieuChinh);
        TongSoLuongThucTeDauKy = HangHoas.Sum(x => x.SoLuongThucTeDauKy);
        TongGiaTriTonDauKy = HangHoas.Sum(x => x.ThanhTienDauKy);
        TongNhapTrongKy = HangHoas.Sum(x => x.SoLuongNhap);
        TongXuatTrongKy = HangHoas.Sum(x => x.SoLuongXuat);
        TongTonHienTai = HangHoas.Sum(x => x.TonHienTai);
    }

    /// <summary>
    /// Mở hộp thoại tạo kỳ kinh doanh mới và tự động chuyển sang kỳ vừa tạo.
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
    /// Nạp lại tồn cuối kỳ trước vào kỳ hiện tại.
    /// </summary>
    [RelayCommand]
    private async Task LoadTonKyTruocAsync()
    {
        if (SelectedKy is null)
            return;

        if (SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã kết kỳ (khóa sổ), không thể nạp lại số liệu.", owner: OwnerWindow);
            return;
        }

        // Tìm kỳ trước (kỳ có ngày bắt đầu trước kỳ này)
        var kyTruoc = DanhSachKy
            .Where(k => k.Id != SelectedKy.Id && k.NgayBatDauKy < SelectedKy.NgayBatDauKy)
            .OrderByDescending(k => k.NgayBatDauKy)
            .FirstOrDefault();

        if (kyTruoc is null)
        {
            await NotificationWindow.ShowWarningAsync("Không tìm thấy kỳ trước để kế thừa số liệu.", owner: OwnerWindow);
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Đồng bộ tồn cuối kỳ trước",
                $"Bạn có chắc muốn nạp lại số lượng tồn cuối từ \"{kyTruoc.TenKy}\" sang kỳ \"{SelectedKy.TenKy}\" không?\n" +
                "Số lượng tồn đầu kỳ của các mặt hàng sẽ được cập nhật theo số tồn cuối kỳ trước.");

            if (!confirmed)
                return;

            IsLoading = true;
            var rows = await _db.LoadLaiTonKyTruocAsync(SelectedKy.Id, kyTruoc.Id);
            await TaiHangHoaTheoKyAsync();

            StatusMessage = $"Đã đồng bộ tồn kho từ {kyTruoc.TenKy} cho {rows} mặt hàng.";
            await NotificationWindow.ShowSuccessAsync($"Đã nạp tồn từ \"{kyTruoc.TenKy}\" cho {rows} mặt hàng.", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi load tồn kỳ trước: " + ex.Message;
            SafeLog.WriteError("Lỗi LoadTonKyTruocAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Mở hộp thoại chọn hoa từ Danh mục hàng hóa để bổ sung vào kỳ này.
    /// </summary>
    [RelayCommand]
    private async Task ThemHangVaoKyAsync()
    {
        if (SelectedKy is null)
            return;

        if (SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã kết kỳ (khóa sổ), không thể thêm mặt hàng mới.", owner: OwnerWindow);
            return;
        }

        if (OwnerWindow is null)
            return;

        try
        {
            var daCoIds = HangHoas.Select(x => x.HangHoaId).ToList();
            var dialog = new ChonHangHoaVaoKyDialog(SelectedKy.Id, daCoIds);
            var result = await dialog.ShowDialog<bool>(OwnerWindow);

            if (result && dialog.SelectedSanPhamIds.Count > 0)
            {
                IsLoading = true;
                int added = await _db.ThemNhieuHangVaoKyAsync(SelectedKy.Id, dialog.SelectedSanPhamIds);
                await TaiHangHoaTheoKyAsync();

                StatusMessage = $"Đã bổ sung {added} mặt hàng vào kỳ {SelectedKy.TenKy}.";
                await NotificationWindow.ShowSuccessAsync($"Đã bổ sung {added} mặt hàng vào kỳ này.", owner: OwnerWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi thêm hàng vào kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi ThemHangVaoKyAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Lưu toàn bộ điều chỉnh kiểm kê, giá vốn, giá bán và trạng thái mở bán vào hệ thống.
    /// </summary>
    [RelayCommand]
    private async Task LuuDieuChinhAsync()
    {
        if (SelectedKy is null)
            return;

        if (SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã kết kỳ (khóa sổ), không thể lưu chỉnh sửa.", owner: OwnerWindow);
            return;
        }

        IsLoading = true;
        try
        {
            await _db.LuuDieuChinhHangHoaKyAsync(SelectedKy.Id, HangHoas);
            StatusMessage = $"✅ Đã lưu dữ liệu kho cho {HangHoas.Count} mặt hàng trong kỳ {SelectedKy.TenKy}.";
            await NotificationWindow.ShowSuccessAsync($"Đã lưu cập nhật kho thành công cho {HangHoas.Count} mặt hàng.", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể lưu dữ liệu kho: " + ex.Message;
            SafeLog.WriteError("Lỗi LuuDieuChinhAsync", ex);
            await NotificationWindow.ShowErrorAsync("Không thể lưu: " + ex.Message, owner: OwnerWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Chốt số liệu tồn cuối kỳ và khóa sổ kỳ này.
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
                "Xác nhận Kết kỳ kho",
                $"Bạn có chắc chắn muốn KẾT KỲ cho \"{SelectedKy.TenKy}\" không?\n\n" +
                "⚠️ Khi kết kỳ:\n" +
                "• Số liệu Tồn cuối kỳ sẽ được chốt tự động = (Tồn đầu + Điều chỉnh) + Nhập - Xuất bán.\n" +
                "• Kỳ sẽ được chuyển sang trạng thái [Khóa sổ] để bảo toàn số liệu kế toán.\n" +
                "• Số tồn chốt này sẽ là căn cứ chính xác để kế thừa cho kỳ tiếp theo.");

            if (!confirmed)
                return;

            IsLoading = true;
            await _db.KetKyAsync(SelectedKy.Id);

            // Làm mới lại
            await TaiDanhSachKyAsync(SelectedKy.Id);
            StatusMessage = $"🎉 Kỳ {SelectedKy.TenKy} đã được KẾT KỲ thành công!";
            await NotificationWindow.ShowSuccessAsync($"Đã kết kỳ \"{SelectedKy.TenKy}\" thành công. Số liệu tồn kho đã được chốt sổ.", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi kết kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi KetKyAsync", ex);
            await NotificationWindow.ShowErrorAsync("Lỗi kết kỳ: " + ex.Message, owner: OwnerWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Mở lại kỳ đã kết sổ (dành cho quản trị viên khi cần hiệu chỉnh lại số liệu).
    /// </summary>
    [RelayCommand]
    private async Task MoLaiKyAsync()
    {
        if (SelectedKy is null || !SelectedKy.TrangThaiKhoaSo)
            return;

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Mở lại kỳ đã kết sổ",
                $"Bạn có muốn mở lại kỳ \"{SelectedKy.TenKy}\" để chỉnh sửa dữ liệu kho không?");

            if (!confirmed)
                return;

            IsLoading = true;
            await _db.MoLaiKyAsync(SelectedKy.Id);
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
    /// Xóa một mặt hàng khỏi kỳ kinh doanh nếu mặt hàng đó chưa phát sinh nhập/xuất.
    /// </summary>
    [RelayCommand]
    private async Task XoaHangKhoiKyAsync(HangHoaKyKhoItem? item)
    {
        if (item is null || SelectedKy is null)
            return;

        if (SelectedKy.TrangThaiKhoaSo)
        {
            await NotificationWindow.ShowWarningAsync("Kỳ này đã kết kỳ (khóa sổ), không thể xóa mặt hàng.", owner: OwnerWindow);
            return;
        }

        if (item.SoLuongNhap > 0 || item.SoLuongXuat > 0)
        {
            await NotificationWindow.ShowWarningAsync(
                $"Mặt hàng \"{item.TenHangHoa}\" đã phát sinh giao dịch nhập/xuất trong kỳ này, không thể xóa.",
                owner: OwnerWindow);
            return;
        }

        try
        {
            var confirmed = await ShowConfirmationAsync(
                "Bỏ mặt hàng khỏi kỳ",
                $"Bạn có chắc muốn bỏ \"{item.TenHangHoa}\" ra khỏi kỳ kinh doanh này không?");

            if (!confirmed)
                return;

            await _db.XoaHangKhoiKyAsync(item.Id);
            HangHoas.Remove(item);
            CapNhatThongKe();

            StatusMessage = $"Đã bỏ mặt hàng {item.MaHangHoa} khỏi kỳ.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi xóa hàng khỏi kỳ: " + ex.Message;
            SafeLog.WriteError("Lỗi XoaHangKhoiKyAsync", ex);
        }
    }

    /// <summary>
    /// Chuyển nhanh sang màn hình Nhập hàng để nhập thêm hoa cho kỳ này.
    /// </summary>
    [RelayCommand]
    private void ChuyenSangNhapHang()
    {
        ChuyenSangNhapHangAction?.Invoke();
    }

    /// <summary>
    /// Xuất toàn bộ bảng tồn kho theo kỳ hiện tại ra file Excel (.xlsx).
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        if (SelectedKy is null || HangHoas.Count == 0 || OwnerWindow is null)
            return;

        try
        {
            var file = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Xuất bảng tồn kho kỳ {SelectedKy.TenKy}",
                SuggestedFileName = $"TonKho_{SelectedKy.MaKy}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Workbook (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
                }
            });

            if (file is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Tồn Kho Theo Kỳ");

            // Tiêu đề
            ws.Cell(1, 1).Value = $"BẢNG BÁO CÁO TỒN KHO - {SelectedKy.TenKy.ToUpperInvariant()}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(8, 127, 110);

            ws.Cell(2, 1).Value = $"Thời gian: {SelectedKy.NgayBatDauKy:dd/MM/yyyy} - {SelectedKy.NgayKetThucKy:dd/MM/yyyy} | Trạng thái: {(SelectedKy.TrangThaiKhoaSo ? "ĐÃ KẾT KỲ" : "ĐANG MỞ")}";
            ws.Cell(2, 1).Style.Font.Italic = true;

            // Header bảng
            string[] headers =
            [
                "STT", "Mã hàng", "Tên hoa / Sản phẩm", "ĐVT", "Nhóm hàng",
                "Tồn đầu kỳ", "Điều chỉnh (+/-)", "Tồn thực tế đầu kỳ",
                "Giá vốn đầu kỳ (đ)", "Giá bán mở bán (đ)", "Thành tiền đầu kỳ (đ)",
                "Nhập trong kỳ", "Xuất bán trong kỳ", "Tồn hiện tại / Tồn cuối",
                "Trạng thái mở bán", "Ghi chú"
            ];

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(231, 243, 239);
                cell.Style.Font.FontColor = XLColor.FromArgb(22, 75, 64);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 5;
            for (int i = 0; i < HangHoas.Count; i++)
            {
                var item = HangHoas[i];
                ws.Cell(row, 1).Value = i + 1;
                ws.Cell(row, 2).Value = item.MaHangHoa;
                ws.Cell(row, 3).Value = item.TenHangHoa;
                ws.Cell(row, 4).Value = item.DonViTinh;
                ws.Cell(row, 5).Value = item.NhomHangHoa;

                ws.Cell(row, 6).Value = item.SoLuongDauKy;
                ws.Cell(row, 7).Value = item.SoLuongDieuChinh;
                ws.Cell(row, 8).Value = item.SoLuongThucTeDauKy;
                ws.Cell(row, 9).Value = item.GiaVonDauKy;
                ws.Cell(row, 10).Value = item.GiaBanDauKy;
                ws.Cell(row, 11).Value = item.ThanhTienDauKy;

                ws.Cell(row, 12).Value = item.SoLuongNhap;
                ws.Cell(row, 13).Value = item.SoLuongXuat;
                ws.Cell(row, 14).Value = item.TonCuoiKyHienThi;

                ws.Cell(row, 15).Value = item.DangMoBan ? "Đang bán" : "Tạm ngưng";
                ws.Cell(row, 16).Value = item.GhiChu ?? "";

                // Format số
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 13).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0";

                row++;
            }

            // Dòng tổng cộng
            ws.Cell(row, 3).Value = "TỔNG CỘNG";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = TongSoLuongDauKy;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = TongSoLuongDieuChinh;
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 8).Value = TongSoLuongThucTeDauKy;
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 11).Value = TongGiaTriTonDauKy;
            ws.Cell(row, 11).Style.Font.Bold = true;
            ws.Cell(row, 12).Value = TongNhapTrongKy;
            ws.Cell(row, 12).Style.Font.Bold = true;
            ws.Cell(row, 13).Value = TongXuatTrongKy;
            ws.Cell(row, 13).Style.Font.Bold = true;
            ws.Cell(row, 14).Value = TongTonHienTai;
            ws.Cell(row, 14).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            await using var stream = await file.OpenWriteAsync();
            workbook.SaveAs(stream);

            StatusMessage = "Đã xuất bảng tồn kho ra file Excel thành công!";
            await NotificationWindow.ShowSuccessAsync("Đã xuất file Excel thành công!", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi xuất file Excel: " + ex.Message;
            SafeLog.WriteError("Lỗi XuatExcelAsync", ex);
        }
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (OwnerWindow is null)
            return false;

        var dialog = new ConfirmationWindow(title, message, "Đồng ý", "Hủy");
        return await dialog.ShowDialog<bool>(OwnerWindow);
    }
}
