using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho GIAO DIỆN 67 – Báo cáo tổng hợp khuyến mãi.
/// </summary>
public partial class BaoCaoTongHopKhuyenMaiViewModel : ObservableObject
{
    private readonly BaoCaoKhuyenMaiService _service;
    private CancellationTokenSource? _cts;

    public BaoCaoTongHopKhuyenMaiViewModel(DatabaseService db)
    {
        _service = new BaoCaoKhuyenMaiService(db.TaoKetNoiAsync);

        KhoangThoiGian = "Tháng này";
        TuyChinh = false;
        var today = DateTime.Today;
        TuNgay = new DateTime(today.Year, today.Month, 1);
        DenNgay = today;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
    }

    // ============================================================
    // TRẠNG THÁI & THÔNG BÁO
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoChuongTrinhHienThi))]
    [NotifyPropertyChangedFor(nameof(SoChuongTrinhDangChayHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoMatHangApDungHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoHoaDonApDungHienThi))]
    [NotifyPropertyChangedFor(nameof(TongDoanhThuHienThi))]
    [NotifyPropertyChangedFor(nameof(TongGiaTriKhuyenMaiHienThi))]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    [NotifyPropertyChangedFor(nameof(KhongCoDuLieu))]
    private BaoCaoTongHopKhuyenMaiDuLieu? duLieu;

    public bool CoDuLieu => DuLieu != null && DuLieu.Items.Count > 0;
    public bool KhongCoDuLieu => !DangTai && !CoDuLieu;

    public ObservableCollection<BaoCaoTongHopKhuyenMaiItem> Items { get; } = new();

    // KPI
    public string TongSoChuongTrinhHienThi => DuLieu?.TongSoChuongTrinhHienThi ?? "0";
    public string SoChuongTrinhDangChayHienThi => DuLieu?.SoChuongTrinhDangChayHienThi ?? "0";
    public string TongSoMatHangApDungHienThi => DuLieu?.TongSoMatHangApDungHienThi ?? "0";
    public string TongSoHoaDonApDungHienThi => DuLieu?.TongSoHoaDonApDungHienThi ?? "0";
    public string TongDoanhThuHienThi => DuLieu?.TongDoanhThuHienThi ?? "0 đ";
    public string TongGiaTriKhuyenMaiHienThi => DuLieu?.TongGiaTriKhuyenMaiHienThi ?? "0 đ";

    // ============================================================
    // BỘ LỌC
    // ============================================================

    [ObservableProperty] private string khoangThoiGian = "Tháng này";
    [ObservableProperty] private bool tuyChinh;
    [ObservableProperty] private DateTime tuNgay;
    [ObservableProperty] private DateTime denNgay;
    [ObservableProperty] private DateTimeOffset? tuNgayChon;
    [ObservableProperty] private DateTimeOffset? denNgayChon;

    public IReadOnlyList<string> TrangThaiOptions { get; } =
    [
        "Tất cả trạng thái",
        "Đang diễn ra",
        "Chưa bắt đầu",
        "Đã kết thúc",
        "Tạm ngưng"
    ];

    [ObservableProperty] private string selectedTrangThaiOption = "Tất cả trạng thái";

    partial void OnSelectedTrangThaiOptionChanged(string value)
    {
        TrangThaiChon = value switch
        {
            "Đang diễn ra" => "DANG_HOAT_DONG",
            "Chưa bắt đầu" => "CHUA_BAT_DAU",
            "Đã kết thúc" => "DA_KET_THUC",
            "Tạm ngưng" => "TAM_NGUNG",
            _ => "TAT_CA"
        };
        _ = TaiLaiAsync();
    }

    public IReadOnlyList<string> LoaiKmOptions { get; } =
    [
        "Tất cả loại",
        "Theo hàng hóa",
        "Theo hóa đơn",
        "Tặng kèm"
    ];

    [ObservableProperty] private string selectedLoaiKmOption = "Tất cả loại";

    partial void OnSelectedLoaiKmOptionChanged(string value)
    {
        LoaiKmChon = value switch
        {
            "Theo hàng hóa" => "THEO_HANG_HOA",
            "Theo hóa đơn" => "THEO_HOA_DON",
            "Tặng kèm" => "TANG_KEM",
            _ => "TAT_CA"
        };
        _ = TaiLaiAsync();
    }

    [ObservableProperty] private string trangThaiChon = "TAT_CA";
    [ObservableProperty] private string loaiKmChon = "TAT_CA";
    [ObservableProperty] private string timKiem = string.Empty;

    partial void OnTimKiemChanged(string value)
    {
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void ChonKhoangThoiGian(string? khoang)
    {
        if (string.IsNullOrWhiteSpace(khoang)) return;
        KhoangThoiGian = khoang;
        var today = DateTime.Today;

        switch (khoang)
        {
            case "Hôm nay":
                TuyChinh = false;
                TuNgay = today;
                DenNgay = today;
                break;
            case "Hôm qua":
                TuyChinh = false;
                TuNgay = today.AddDays(-1);
                DenNgay = today.AddDays(-1);
                break;
            case "7 ngày qua":
                TuyChinh = false;
                TuNgay = today.AddDays(-6);
                DenNgay = today;
                break;
            case "Tuần này":
                TuyChinh = false;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                TuNgay = today.AddDays(-1 * diff);
                DenNgay = today;
                break;
            case "Tháng này":
                TuyChinh = false;
                TuNgay = new DateTime(today.Year, today.Month, 1);
                DenNgay = today;
                break;
            case "Tháng trước":
                TuyChinh = false;
                var dauThangTruoc = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                var cuoiThangTruoc = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                TuNgay = dauThangTruoc;
                DenNgay = cuoiThangTruoc;
                break;
            case "Tùy chọn":
                TuyChinh = true;
                break;
        }

        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);

        if (!TuyChinh)
        {
            _ = TaiLaiAsync();
        }
    }

    [RelayCommand]
    public void ApDungTuyChinh()
    {
        if (TuNgayChon.HasValue)
            TuNgay = TuNgayChon.Value.Date;
        if (DenNgayChon.HasValue)
            DenNgay = DenNgayChon.Value.Date;

        if (TuNgay > DenNgay)
        {
            ThongBaoLoi = "Từ ngày không được lớn hơn Đến ngày.";
            return;
        }

        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void ChonTrangThai(string? tt)
    {
        TrangThaiChon = tt ?? "TAT_CA";
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void ChonLoaiKm(string? loai)
    {
        LoaiKmChon = loai ?? "TAT_CA";
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void LamMoiBoLoc()
    {
        TrangThaiChon = "TAT_CA";
        LoaiKmChon = "TAT_CA";
        TimKiem = string.Empty;
        ChonKhoangThoiGian("Tháng này");
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;

            var dl = await _service.TaiBaoCaoKhuyenMaiAsync(
                TuNgay,
                DenNgay,
                TrangThaiChon,
                LoaiKmChon,
                TimKiem,
                token);

            if (token.IsCancellationRequested) return;

            DuLieu = dl;
            Items.Clear();
            foreach (var item in dl.Items)
            {
                Items.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua khi người dùng đổi bộ lọc liên tục
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải báo cáo tổng hợp khuyến mãi", ex);
            ThongBaoLoi = "Không thể tải dữ liệu báo cáo: " + ex.Message;
        }
        finally
        {
            DangTai = false;
        }
    }

    // ============================================================
    // XUẤT EXCEL
    // ============================================================

    [RelayCommand]
    public async Task XuatExcelAsync()
    {
        if (DuLieu == null || DuLieu.Items.Count == 0)
        {
            ThongBaoLoi = "Không có dữ liệu để xuất file Excel.";
            return;
        }

        try
        {
            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var window = desktop?.MainWindow;
            if (window == null) return;

            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất báo cáo tổng hợp khuyến mãi",
                SuggestedFileName = $"BaoCaoTongHopKhuyenMai_{TuNgay:yyyyMMdd}_{DenNgay:yyyyMMdd}.xlsx",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Workbook (*.xlsx)")
                    {
                        Patterns = new[] { "*.xlsx" }
                    }
                }
            });

            if (file != null)
            {
                using var wb = BaoCaoKhuyenMaiService.TaoWorkbookKhuyenMai(DuLieu);
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);

                await NotificationWindow.ShowSuccessAsync("Đã xuất file Excel báo cáo tổng hợp khuyến mãi thành công!", owner: window);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel báo cáo tổng hợp khuyến mãi", ex);
            ThongBaoLoi = "Lỗi khi xuất file Excel: " + ex.Message;
        }
    }
}
