using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 28 – DANH SÁCH PHIẾU NHẬP TRẢ LẠI
    // =========================================================

    public bool IsNhapTraLaiSelected => SelectedMenu == "Nhập trả lại";

    public ObservableCollection<PhieuTraHang> DsPhieuTraHangItems { get; } = new();

    [ObservableProperty] private string dsPhieuTraSearch = "";
    [ObservableProperty] private DateTime? dsPhieuTraTuNgay;
    [ObservableProperty] private DateTime? dsPhieuTraDenNgay;
    [ObservableProperty] private string selectedPhieuTraTrangThai = "TAT_CA";
    [ObservableProperty] private bool isLoadingDsPhieuTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangSau))]
    private int dsPhieuTraTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangSau))]
    private int dsPhieuTraPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuTraRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuTraTrangSau))]
    private int dsPhieuTraPageSize = 20;

    public int DsPhieuTraTotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)DsPhieuTraTotalCount / Math.Max(1, DsPhieuTraPageSize)));

    public bool CanDsPhieuTraTrangTruoc => DsPhieuTraPage > 1;
    public bool CanDsPhieuTraTrangSau => DsPhieuTraPage < DsPhieuTraTotalPages;

    public string DsPhieuTraRangeText
    {
        get
        {
            if (DsPhieuTraTotalCount == 0) return "0 phiếu trả lại";
            var from = (DsPhieuTraPage - 1) * DsPhieuTraPageSize + 1;
            var to = Math.Min(from + DsPhieuTraPageSize - 1, DsPhieuTraTotalCount);
            return $"{from} - {to} trong {DsPhieuTraTotalCount:N0} phiếu";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongPhieuTraText))]
    private int thongKeTongPhieuTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongTienTraText))]
    private decimal thongKeTongTienTra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoLuongTraText))]
    private decimal thongKeTongSoLuongTra;

    public string ThongKeTongPhieuTraText => $"{ThongKeTongPhieuTra:N0} phiếu";
    public string ThongKeTongTienTraText => MoneyInput.Format(ThongKeTongTienTra) + " đ";
    public string ThongKeTongSoLuongTraText => $"{ThongKeTongSoLuongTra:N0} SP";

    public IReadOnlyList<string> DsPhieuTraTrangThaiOptions { get; } =
        ["Tất cả trạng thái", "Hoàn thành", "Đã hủy"];

    public string SelectedPhieuTraTrangThaiHienThi
    {
        get => SelectedPhieuTraTrangThai switch
        {
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            _ => "Tất cả trạng thái"
        };
        set
        {
            SelectedPhieuTraTrangThai = value switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Đã hủy" => "DA_HUY",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuTraAsync();
        }
    }

    /// <summary>
    /// Mở màn hình "Nghiệp vụ → Nhập trả lại → Danh sách phiếu nhập trả lại".
    /// </summary>
    [RelayCommand]
    public async Task MoGiaoDienNhapTraLaiAsync()
    {
        IsSalesPanelVisible = false;
        SelectedMenu = "Nhập trả lại";
        DsPhieuTraPage = 1;
        await LoadDsPhieuTraAsync();
    }

    /// <summary>
    /// Tải dữ liệu danh sách phiếu trả hàng và thống kê theo bộ lọc hiện tại.
    /// </summary>
    [RelayCommand]
    public async Task LoadDsPhieuTraAsync()
    {
        if (IsLoadingDsPhieuTra)
            return;

        try
        {
            IsLoadingDsPhieuTra = true;
            StatusMessage = "Đang tải danh sách phiếu nhập trả lại...";

            var (items, totalCount) = await _db.GetPhieuTraHangPagedAsync(
                DsPhieuTraSearch,
                DsPhieuTraTuNgay,
                DsPhieuTraDenNgay,
                SelectedPhieuTraTrangThai,
                DsPhieuTraPage,
                DsPhieuTraPageSize);

            DsPhieuTraHangItems.Clear();
            foreach (var item in items)
            {
                DsPhieuTraHangItems.Add(item);
            }

            DsPhieuTraTotalCount = totalCount;

            // Thống kê thẻ tổng quan
            var (tongPhieu, tongTien, tongSoLuong) = await _db.GetThongKePhieuTraHangAsync(
                DsPhieuTraSearch,
                DsPhieuTraTuNgay,
                DsPhieuTraDenNgay,
                SelectedPhieuTraTrangThai);

            ThongKeTongPhieuTra = tongPhieu;
            ThongKeTongTienTra = tongTien;
            ThongKeTongSoLuongTra = tongSoLuong;

            StatusMessage = $"Đã tải {DsPhieuTraHangItems.Count:N0} phiếu nhập trả lại.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách phiếu nhập trả lại", ex);
            StatusMessage = "Không thể tải danh sách phiếu trả hàng: " + ex.Message;
        }
        finally
        {
            IsLoadingDsPhieuTra = false;
        }
    }

    [RelayCommand]
    public Task TimKiemDsPhieuTraAsync()
    {
        DsPhieuTraPage = 1;
        return LoadDsPhieuTraAsync();
    }

    [RelayCommand]
    public async Task LamMoiDsPhieuTraAsync()
    {
        DsPhieuTraSearch = "";
        DsPhieuTraTuNgay = null;
        DsPhieuTraDenNgay = null;
        SelectedPhieuTraTrangThai = "TAT_CA";
        OnPropertyChanged(nameof(SelectedPhieuTraTrangThaiHienThi));
        DsPhieuTraPage = 1;
        await LoadDsPhieuTraAsync();
    }

    [RelayCommand]
    public async Task DsPhieuTraTrangTruocAsync()
    {
        if (CanDsPhieuTraTrangTruoc)
        {
            DsPhieuTraPage--;
            await LoadDsPhieuTraAsync();
        }
    }

    [RelayCommand]
    public async Task DsPhieuTraTrangSauAsync()
    {
        if (CanDsPhieuTraTrangSau)
        {
            DsPhieuTraPage++;
            await LoadDsPhieuTraAsync();
        }
    }

    /// <summary>
    /// Mở xem chi tiết một phiếu nhập trả lại.
    /// </summary>
    [RelayCommand]
    public async Task XemChiTietPhieuTraAsync(PhieuTraHang? phieu)
    {
        if (phieu is null || OwnerWindow is null)
            return;

        try
        {
            var chiTiets = await _db.GetChiTietPhieuTraHangAsync(phieu.Id);
            var dongHang = string.Join("\n", chiTiets.Select(c =>
                $"• {c.TenSanPham} ({c.MaSanPham}): {c.SoLuong:N0} {c.DonViTinh} × {c.DonGia:N0} đ = {c.ThanhTien:N0} đ" +
                (!string.IsNullOrWhiteSpace(c.LyDoTra) ? $" [Lý do: {c.LyDoTra}]" : "")));

            var thongBao =
                $"MÃ PHIẾU: {phieu.MaPhieuTra}\n" +
                $"Ngày lập: {phieu.ThoiGianTraText}\n" +
                $"Khách hàng: {phieu.KhachHangHienThi} (SĐT: {phieu.SoDienThoaiKhach ?? "—"})\n" +
                $"Hóa đơn gốc: {phieu.HoaDonGocHienThi}\n" +
                $"Kho nhận: {phieu.KhoNhan}\n" +
                $"Trạng thái: {phieu.TrangThaiText}\n" +
                $"Người lập: {phieu.TenNguoiTao ?? "Nhân viên"}\n" +
                $"Ghi chú: {phieu.GhiChu ?? "Không có"}\n\n" +
                $"DANH SÁCH HÀNG TRẢ LẠI ({chiTiets.Count} mặt hàng):\n" +
                (string.IsNullOrWhiteSpace(dongHang) ? "Chưa có chi tiết mặt hàng." : dongHang) + "\n\n" +
                $"TỔNG SỐ LƯỢNG TRẢ: {phieu.TongSoLuongText}\n" +
                $"TỔNG TIỀN HOÀN TRẢ: {phieu.TongTienTraText}";

            await ShowMessageAsync($"Chi tiết phiếu trả lại {phieu.MaPhieuTra}", thongBao);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xem chi tiết phiếu trả hàng", ex);
            await ShowMessageAsync("Lỗi", "Không thể xem chi tiết: " + ex.Message);
        }
    }

    /// <summary>
    /// In phiếu nhập trả lại.
    /// </summary>
    [RelayCommand]
    public async Task InPhieuTraHangAsync(PhieuTraHang? phieu)
    {
        if (phieu is null)
            return;

        await ShowMessageAsync("In phiếu trả hàng",
            $"Chức năng in phiếu trả hàng {phieu.MaPhieuTra} đã sẵn sàng.\nTổng tiền hoàn trả: {phieu.TongTienTraText}");
    }

    /// <summary>
    /// Tạo phiếu trả hàng mới.
    /// </summary>
    [RelayCommand]
    public async Task TaoPhieuTraHangMoiAsync()
    {
        var maMoi = await _db.TaoMaPhieuTraMoiAsync();
        await ShowMessageAsync("Tạo phiếu trả lại",
            $"Mã phiếu đề xuất: {maMoi}\nĐể lập phiếu trả lại hàng, vui lòng chọn hóa đơn gốc cần trả hoặc lập phiếu nhập trả mới.");
    }

    /// <summary>
    /// Xuất danh sách phiếu nhập trả lại ra file Excel.
    /// </summary>
    [RelayCommand]
    public async Task XuatExcelDsPhieuTraAsync()
    {
        if (OwnerWindow is null || DsPhieuTraHangItems.Count == 0)
        {
            await ShowMessageAsync("Thông báo", "Không có dữ liệu phiếu trả hàng để xuất file.");
            return;
        }

        try
        {
            var saveFile = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách phiếu nhập trả lại",
                SuggestedFileName = $"DanhSachPhieuTra_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx"
            });

            if (saveFile is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PhieuNhapTraLai");

            // Header
            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã phiếu trả";
            ws.Cell(1, 3).Value = "Ngày lập";
            ws.Cell(1, 4).Value = "Khách hàng";
            ws.Cell(1, 5).Value = "Số điện thoại";
            ws.Cell(1, 6).Value = "Mã hóa đơn gốc";
            ws.Cell(1, 7).Value = "Kho nhận";
            ws.Cell(1, 8).Value = "Tổng SL trả";
            ws.Cell(1, 9).Value = "Tổng tiền trả";
            ws.Cell(1, 10).Value = "Trạng thái";
            ws.Cell(1, 11).Value = "Ghi chú";

            var row = 2;
            foreach (var item in DsPhieuTraHangItems)
            {
                ws.Cell(row, 1).Value = item.SoThuTu;
                ws.Cell(row, 2).Value = item.MaPhieuTra;
                ws.Cell(row, 3).Value = item.ThoiGianTraText;
                ws.Cell(row, 4).Value = item.KhachHangHienThi;
                ws.Cell(row, 5).Value = item.SoDienThoaiKhach ?? "";
                ws.Cell(row, 6).Value = item.HoaDonGocHienThi;
                ws.Cell(row, 7).Value = item.KhoNhan;
                ws.Cell(row, 8).Value = item.TongSoLuong;
                ws.Cell(row, 9).Value = item.TongTienTra;
                ws.Cell(row, 10).Value = item.TrangThaiText;
                ws.Cell(row, 11).Value = item.GhiChu ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();
            await using var stream = await saveFile.OpenWriteAsync();
            workbook.SaveAs(stream);

            StatusMessage = "Xuất file Excel thành công!";
            await ShowMessageAsync("Thành công", "Đã xuất danh sách phiếu nhập trả lại ra file Excel thành công!");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel phiếu trả hàng", ex);
            await ShowMessageAsync("Lỗi", "Không thể xuất file Excel: " + ex.Message);
        }
    }
}
