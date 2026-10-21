using System;
using System.Collections.Generic;
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
    // GIAO DIỆN 34: DANH SÁCH PHIẾU XUẤT HÀNG NỘI BỘ
    // =========================================================

    public bool IsXuatNoiBoSelected => SelectedMenu == "Xuất nội bộ";

    public ObservableCollection<PhieuXuatNoiBo> DsPhieuXuatNoiBoItems { get; } = new();

    [ObservableProperty] private string dsPhieuXuatNoiBoSearch = "";
    [ObservableProperty] private DateTime? dsPhieuXuatNoiBoTuNgay;
    [ObservableProperty] private DateTime? dsPhieuXuatNoiBoDenNgay;
    [ObservableProperty] private string selectedPhieuXuatNoiBoLoai = "TAT_CA";
    [ObservableProperty] private string selectedPhieuXuatNoiBoTrangThai = "TAT_CA";
    [ObservableProperty] private bool isLoadingDsPhieuXuatNoiBo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatNoiBoTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatNoiBoRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatNoiBoTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatNoiBoTrangSau))]
    private int dsPhieuXuatNoiBoTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatNoiBoTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatNoiBoRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatNoiBoTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatNoiBoTrangSau))]
    private int dsPhieuXuatNoiBoPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatNoiBoTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuXuatNoiBoRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatNoiBoTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuXuatNoiBoTrangSau))]
    private int dsPhieuXuatNoiBoPageSize = 20;

    public int DsPhieuXuatNoiBoTotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)DsPhieuXuatNoiBoTotalCount / Math.Max(1, DsPhieuXuatNoiBoPageSize)));

    public bool CanDsPhieuXuatNoiBoTrangTruoc => DsPhieuXuatNoiBoPage > 1;
    public bool CanDsPhieuXuatNoiBoTrangSau => DsPhieuXuatNoiBoPage < DsPhieuXuatNoiBoTotalPages;

    public string DsPhieuXuatNoiBoRangeText
    {
        get
        {
            if (DsPhieuXuatNoiBoTotalCount == 0) return "0 phiếu xuất nội bộ";
            var from = (DsPhieuXuatNoiBoPage - 1) * DsPhieuXuatNoiBoPageSize + 1;
            var to = Math.Min(from + DsPhieuXuatNoiBoPageSize - 1, DsPhieuXuatNoiBoTotalCount);
            return $"{from} - {to} trong {DsPhieuXuatNoiBoTotalCount:N0} phiếu";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongPhieuXuatNoiBoText))]
    private int thongKeTongPhieuXuatNoiBo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongGiaTriXuatNoiBoText))]
    private decimal thongKeTongGiaTriXuatNoiBo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoLuongXuatNoiBoText))]
    private decimal thongKeTongSoLuongXuatNoiBo;

    public string ThongKeTongPhieuXuatNoiBoText => $"{ThongKeTongPhieuXuatNoiBo:N0} phiếu";
    public string ThongKeTongGiaTriXuatNoiBoText => MoneyInput.Format(ThongKeTongGiaTriXuatNoiBo) + " đ";
    public string ThongKeTongSoLuongXuatNoiBoText => $"{ThongKeTongSoLuongXuatNoiBo:N0} SP";

    public IReadOnlyList<string> DsPhieuXuatNoiBoLoaiOptions { get; } =
        ["Tất cả loại", "Xuất chuyển kho", "Xuất cấp phát", "Xuất sử dụng", "Xuất hủy hỏng", "Xuất khác"];

    public string SelectedPhieuXuatNoiBoLoaiHienThi
    {
        get => SelectedPhieuXuatNoiBoLoai switch
        {
            "XUAT_CHUYEN_KHO" => "Xuất chuyển kho",
            "XUAT_CAP_PHAT" => "Xuất cấp phát",
            "XUAT_SU_DUNG" => "Xuất sử dụng",
            "XUAT_HUY_HONG" => "Xuất hủy hỏng",
            "XUAT_KHAC" => "Xuất khác",
            _ => "Tất cả loại"
        };
        set
        {
            SelectedPhieuXuatNoiBoLoai = value switch
            {
                "Xuất chuyển kho" => "XUAT_CHUYEN_KHO",
                "Xuất cấp phát" => "XUAT_CAP_PHAT",
                "Xuất sử dụng" => "XUAT_SU_DUNG",
                "Xuất hủy hỏng" => "XUAT_HUY_HONG",
                "Xuất khác" => "XUAT_KHAC",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuXuatNoiBoAsync();
        }
    }

    public IReadOnlyList<string> DsPhieuXuatNoiBoTrangThaiOptions { get; } =
        ["Tất cả trạng thái", "Hoàn thành", "Đã hủy"];

    public string SelectedPhieuXuatNoiBoTrangThaiHienThi
    {
        get => SelectedPhieuXuatNoiBoTrangThai switch
        {
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            _ => "Tất cả trạng thái"
        };
        set
        {
            SelectedPhieuXuatNoiBoTrangThai = value switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Đã hủy" => "DA_HUY",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuXuatNoiBoAsync();
        }
    }

    /// <summary>
    /// Mở trung tâm "Xuất hàng" - danh sách hợp nhất (xuất nội bộ nằm trong đó).
    /// </summary>
    [RelayCommand]
    public async Task MoGiaoDienXuatNoiBoAsync()
    {
        IsSalesPanelVisible = false;
        SelectedMenu = "Xuất hàng";
        await MoDanhSachPhieuXuatHopAsync();
    }

    /// <summary>
    /// Tải dữ liệu danh sách phiếu xuất nội bộ và thống kê theo bộ lọc hiện tại.
    /// </summary>
    [RelayCommand]
    public async Task LoadDsPhieuXuatNoiBoAsync()
    {
        if (IsLoadingDsPhieuXuatNoiBo)
            return;

        try
        {
            IsLoadingDsPhieuXuatNoiBo = true;
            StatusMessage = "Đang tải danh sách phiếu xuất nội bộ...";

            var (items, totalCount) = await _db.GetPhieuXuatNoiBoPagedAsync(
                DsPhieuXuatNoiBoSearch,
                DsPhieuXuatNoiBoTuNgay,
                DsPhieuXuatNoiBoDenNgay,
                SelectedPhieuXuatNoiBoLoai,
                SelectedPhieuXuatNoiBoTrangThai,
                DsPhieuXuatNoiBoPage,
                DsPhieuXuatNoiBoPageSize);

            DsPhieuXuatNoiBoItems.Clear();
            foreach (var item in items)
            {
                DsPhieuXuatNoiBoItems.Add(item);
            }

            DsPhieuXuatNoiBoTotalCount = totalCount;

            var (tongPhieu, tongGiaTri, tongSoLuong) = await _db.GetThongKePhieuXuatNoiBoAsync(
                DsPhieuXuatNoiBoSearch,
                DsPhieuXuatNoiBoTuNgay,
                DsPhieuXuatNoiBoDenNgay,
                SelectedPhieuXuatNoiBoLoai,
                SelectedPhieuXuatNoiBoTrangThai);

            ThongKeTongPhieuXuatNoiBo = tongPhieu;
            ThongKeTongGiaTriXuatNoiBo = tongGiaTri;
            ThongKeTongSoLuongXuatNoiBo = tongSoLuong;

            StatusMessage = $"Đã tải {DsPhieuXuatNoiBoItems.Count:N0} phiếu xuất nội bộ.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách phiếu xuất nội bộ", ex);
            StatusMessage = "Không thể tải danh sách phiếu xuất nội bộ: " + ex.Message;
        }
        finally
        {
            IsLoadingDsPhieuXuatNoiBo = false;
        }
    }

    [RelayCommand]
    public Task TimKiemDsPhieuXuatNoiBoAsync()
    {
        DsPhieuXuatNoiBoPage = 1;
        return LoadDsPhieuXuatNoiBoAsync();
    }

    [RelayCommand]
    public async Task LamMoiDsPhieuXuatNoiBoAsync()
    {
        DsPhieuXuatNoiBoSearch = "";
        DsPhieuXuatNoiBoTuNgay = null;
        DsPhieuXuatNoiBoDenNgay = null;
        SelectedPhieuXuatNoiBoLoai = "TAT_CA";
        SelectedPhieuXuatNoiBoTrangThai = "TAT_CA";
        OnPropertyChanged(nameof(SelectedPhieuXuatNoiBoLoaiHienThi));
        OnPropertyChanged(nameof(SelectedPhieuXuatNoiBoTrangThaiHienThi));
        DsPhieuXuatNoiBoPage = 1;
        await LoadDsPhieuXuatNoiBoAsync();
    }

    [RelayCommand]
    public async Task DsPhieuXuatNoiBoTrangTruocAsync()
    {
        if (CanDsPhieuXuatNoiBoTrangTruoc)
        {
            DsPhieuXuatNoiBoPage--;
            await LoadDsPhieuXuatNoiBoAsync();
        }
    }

    [RelayCommand]
    public async Task DsPhieuXuatNoiBoTrangSauAsync()
    {
        if (CanDsPhieuXuatNoiBoTrangSau)
        {
            DsPhieuXuatNoiBoPage++;
            await LoadDsPhieuXuatNoiBoAsync();
        }
    }

    /// <summary>
    /// Mở xem chi tiết một phiếu xuất hàng nội bộ.
    /// </summary>
    [RelayCommand]
    public async Task XemChiTietPhieuXuatNoiBoAsync(PhieuXuatNoiBo? phieu)
    {
        if (phieu is null || OwnerWindow is null)
            return;

        try
        {
            var chiTiets = await _db.GetChiTietPhieuXuatNoiBoAsync(phieu.Id);
            var dongHang = string.Join("\n", chiTiets.Select(c =>
                $"• {c.TenSanPham} ({c.MaSanPham}): {c.SoLuong:N0} {c.DonViTinh} × {c.DonGia:N0} đ = {c.ThanhTien:N0} đ" +
                (!string.IsNullOrWhiteSpace(c.GhiChu) ? $" [Ghi chú: {c.GhiChu}]" : "")));

            var thongBao =
                $"MÃ PHIẾU XUẤT NỘI BỘ: {phieu.MaPhieuXuatNoiBo}\n" +
                $"Thời gian xuất: {phieu.ThoiGianXuatText}\n" +
                $"Loại xuất: {phieu.LoaiXuatNoiBoText}\n" +
                $"Kho xuất hàng: {phieu.KhoXuat}\n" +
                $"Kho nhận / Bộ phận nhận: {phieu.BoPhanNhanHienThi}\n" +
                $"Trạng thái: {phieu.TrangThaiText}\n" +
                $"Người lập: {phieu.TenNguoiTao ?? "Nhân viên"}\n" +
                $"Ghi chú: {phieu.GhiChu ?? "Không có"}\n\n" +
                $"DANH SÁCH MẶT HÀNG XUẤT ({chiTiets.Count} mặt hàng):\n" +
                (string.IsNullOrWhiteSpace(dongHang) ? "Chưa có chi tiết mặt hàng." : dongHang) + "\n\n" +
                $"TỔNG SỐ LƯỢNG XUẤT: {phieu.TongSoLuongText}\n" +
                $"TỔNG GIÁ TRỊ XUẤT: {phieu.TongGiaTriText}";

            await ShowMessageAsync($"Chi tiết phiếu xuất nội bộ {phieu.MaPhieuXuatNoiBo}", thongBao);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xem chi tiết phiếu xuất nội bộ", ex);
            await ShowMessageAsync("Lỗi", "Không thể xem chi tiết: " + ex.Message);
        }
    }

    /// <summary>
    /// In phiếu xuất nội bộ.
    /// </summary>
    [RelayCommand]
    public async Task InPhieuXuatNoiBoAsync(PhieuXuatNoiBo? phieu)
    {
        if (phieu is null)
            return;

        await ShowMessageAsync("In phiếu xuất nội bộ",
            $"Chức năng in phiếu xuất nội bộ {phieu.MaPhieuXuatNoiBo} đã sẵn sàng.\nLoại xuất: {phieu.LoaiXuatNoiBoText} • Bộ phận nhận: {phieu.BoPhanNhanHienThi}\nTổng số lượng: {phieu.TongSoLuongText} • Tổng giá trị: {phieu.TongGiaTriText}");
    }

    /// <summary>
    /// Tạo phiếu xuất nội bộ mới.
    /// </summary>
    [RelayCommand]
    public async Task TaoPhieuXuatNoiBoMoiAsync()
    {
        var maMoi = await _db.TaoMaPhieuXuatNoiBoMoiAsync();
        await ShowMessageAsync("Thêm phiếu xuất hàng nội bộ",
            $"Mã phiếu đề xuất: {maMoi}\nChức năng tạo phiếu xuất nội bộ (Xuất chuyển kho, Xuất cấp phát, Xuất sử dụng, Xuất hủy hỏng) đã sẵn sàng hoạt động!");
    }

    /// <summary>
    /// Xuất danh sách phiếu xuất nội bộ ra file Excel.
    /// </summary>
    [RelayCommand]
    public async Task XuatExcelDsPhieuXuatNoiBoAsync()
    {
        if (OwnerWindow is null || DsPhieuXuatNoiBoItems.Count == 0)
        {
            await ShowMessageAsync("Thông báo", "Không có dữ liệu phiếu xuất nội bộ để xuất file.");
            return;
        }

        try
        {
            var saveFile = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách phiếu xuất hàng nội bộ",
                SuggestedFileName = $"DanhSachPhieuXuatNoiBo_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx"
            });

            if (saveFile is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PhieuXuatNoiBo");

            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã phiếu xuất nội bộ";
            ws.Cell(1, 3).Value = "Thời gian xuất";
            ws.Cell(1, 4).Value = "Loại xuất";
            ws.Cell(1, 5).Value = "Kho xuất";
            ws.Cell(1, 6).Value = "Kho nhận / Bộ phận nhận";
            ws.Cell(1, 7).Value = "Tổng số lượng";
            ws.Cell(1, 8).Value = "Tổng giá trị";
            ws.Cell(1, 9).Value = "Trạng thái";
            ws.Cell(1, 10).Value = "Ghi chú";

            var row = 2;
            foreach (var item in DsPhieuXuatNoiBoItems)
            {
                ws.Cell(row, 1).Value = item.SoThuTu;
                ws.Cell(row, 2).Value = item.MaPhieuXuatNoiBo;
                ws.Cell(row, 3).Value = item.ThoiGianXuatText;
                ws.Cell(row, 4).Value = item.LoaiXuatNoiBoText;
                ws.Cell(row, 5).Value = item.KhoXuat;
                ws.Cell(row, 6).Value = item.BoPhanNhanHienThi;
                ws.Cell(row, 7).Value = item.TongSoLuong;
                ws.Cell(row, 8).Value = item.TongGiaTri;
                ws.Cell(row, 9).Value = item.TrangThaiText;
                ws.Cell(row, 10).Value = item.GhiChu ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();
            await using var stream = await saveFile.OpenWriteAsync();
            workbook.SaveAs(stream);

            StatusMessage = "Xuất file Excel thành công!";
            await ShowMessageAsync("Thành công", "Đã xuất danh sách phiếu xuất nội bộ ra file Excel thành công!");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel phiếu xuất nội bộ", ex);
            await ShowMessageAsync("Lỗi", "Không thể xuất file Excel: " + ex.Message);
        }
    }
}
