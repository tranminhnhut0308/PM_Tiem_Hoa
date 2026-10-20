using System;
using System.Collections.ObjectModel;
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
/// ViewModel cho GIAO DIỆN 68 – Báo cáo tổng hợp tình hình nhập/xuất hàng hóa.
/// </summary>
public partial class BaoCaoTinhHinhNhapXuatViewModel : ObservableObject
{
    private readonly BaoCaoTinhHinhNhapXuatService _service;
    private readonly BaoCaoNhapXuatService _nhomService;
    private CancellationTokenSource? _cts;

    public BaoCaoTinhHinhNhapXuatViewModel(DatabaseService db)
    {
        _service = new BaoCaoTinhHinhNhapXuatService(db.TaoKetNoiAsync);
        _nhomService = new BaoCaoNhapXuatService(db.TaoKetNoiAsync);

        KhoangThoiGian = "Tháng này";
        TuyChinh = false;
        var today = DateTime.Today;
        TuNgay = new DateTime(today.Year, today.Month, 1);
        DenNgay = today;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);

        _ = TaiDanhSachNhomAsync();
    }

    // ============================================================
    // TRẠNG THÁI & DỮ LIỆU
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongGiaTriNhapHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoLuongNhapHienThi))]
    [NotifyPropertyChangedFor(nameof(TongGiaTriXuatVonHienThi))]
    [NotifyPropertyChangedFor(nameof(TongDoanhThuXuatHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoLuongXuatHienThi))]
    [NotifyPropertyChangedFor(nameof(TongLoiNhuanGopHienThi))]
    [NotifyPropertyChangedFor(nameof(ChenhLechGiaTriTongHienThi))]
    [NotifyPropertyChangedFor(nameof(TyLeTieuThuTbHienThi))]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    [NotifyPropertyChangedFor(nameof(KhongCoDuLieu))]
    private BaoCaoTinhHinhNhapXuatDuLieu? duLieu;

    public bool CoDuLieu => DuLieu != null && DuLieu.Items.Count > 0;
    public bool KhongCoDuLieu => !DangTai && !CoDuLieu;

    public ObservableCollection<BaoCaoTinhHinhNhapXuatItem> Items { get; } = new();
    public ObservableCollection<BaoCaoNhapXuatNhomOption> NhomOptions { get; } = new();

    // KPI
    public string TongGiaTriNhapHienThi => DuLieu?.TongGiaTriNhapHienThi ?? "0 đ";
    public string TongSoLuongNhapHienThi => DuLieu?.TongSoLuongNhapHienThi ?? "0";
    public string TongGiaTriXuatVonHienThi => DuLieu?.TongGiaTriXuatVonHienThi ?? "0 đ";
    public string TongDoanhThuXuatHienThi => DuLieu?.TongDoanhThuXuatHienThi ?? "0 đ";
    public string TongSoLuongXuatHienThi => DuLieu?.TongSoLuongXuatHienThi ?? "0";
    public string TongLoiNhuanGopHienThi => DuLieu?.TongLoiNhuanGopHienThi ?? "0 đ";
    public string ChenhLechGiaTriTongHienThi => DuLieu?.ChenhLechGiaTriTongHienThi ?? "0 đ";
    public string TyLeTieuThuTbHienThi => DuLieu?.TyLeTieuThuTbHienThi ?? "0%";

    // ============================================================
    // BỘ LỌC
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTheoHangHoa))]
    [NotifyPropertyChangedFor(nameof(IsTheoNhomHang))]
    [NotifyPropertyChangedFor(nameof(IsTheoNgay))]
    [NotifyPropertyChangedFor(nameof(CotDoiTuongHeader))]
    private string cheDoXem = "THEO_HANG_HOA"; // "THEO_HANG_HOA", "THEO_NHOM_HANG", "THEO_NGAY"

    public bool IsTheoHangHoa => CheDoXem == "THEO_HANG_HOA";
    public bool IsTheoNhomHang => CheDoXem == "THEO_NHOM_HANG";
    public bool IsTheoNgay => CheDoXem == "THEO_NGAY";

    public string CotDoiTuongHeader => CheDoXem switch
    {
        "THEO_NHOM_HANG" => "TÊN NHÓM HÀNG",
        "THEO_NGAY" => "NGÀY PHÁT SINH",
        _ => "TÊN HÀNG HÓA"
    };

    [ObservableProperty] private string khoangThoiGian = "Tháng này";
    [ObservableProperty] private bool tuyChinh;
    [ObservableProperty] private DateTime tuNgay;
    [ObservableProperty] private DateTime denNgay;
    [ObservableProperty] private DateTimeOffset? tuNgayChon;
    [ObservableProperty] private DateTimeOffset? denNgayChon;

    [ObservableProperty] private BaoCaoNhapXuatNhomOption? selectedNhom;
    [ObservableProperty] private string timKiem = string.Empty;

    partial void OnTimKiemChanged(string value)
    {
        _ = TaiLaiAsync();
    }

    partial void OnSelectedNhomChanged(BaoCaoNhapXuatNhomOption? value)
    {
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void ChonCheDoXem(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode) || CheDoXem == mode) return;
        CheDoXem = mode;
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
    public void LamMoiBoLoc()
    {
        CheDoXem = "THEO_HANG_HOA";
        SelectedNhom = NhomOptions.FirstOrDefault();
        TimKiem = string.Empty;
        ChonKhoangThoiGian("Tháng này");
    }

    private async Task TaiDanhSachNhomAsync()
    {
        try
        {
            var nhoms = await _nhomService.TaiNhomHangAsync();
            NhomOptions.Clear();
            foreach (var n in nhoms)
                NhomOptions.Add(n);
            SelectedNhom = NhomOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải nhóm hàng cho báo cáo tình hình nhập xuất", ex);
        }
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

            int nhomId = SelectedNhom?.Id ?? -1;

            var dl = await _service.TaiBaoCaoTinhHinhAsync(
                TuNgay,
                DenNgay,
                CheDoXem,
                nhomId,
                TimKiem,
                token);

            if (token.IsCancellationRequested) return;

            DuLieu = dl;
            Items.Clear();
            foreach (var it in dl.Items)
            {
                Items.Add(it);
            }
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua khi người dùng đổi bộ lọc liên tục
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải báo cáo tình hình nhập xuất", ex);
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
                Title = "Xuất báo cáo tổng hợp tình hình nhập/xuất hàng hóa",
                SuggestedFileName = $"BaoCaoTinhHinhNhapXuat_{CheDoXem}_{TuNgay:yyyyMMdd}_{DenNgay:yyyyMMdd}.xlsx",
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
                using var wb = BaoCaoTinhHinhNhapXuatService.TaoWorkbookTinhHinhNhapXuat(DuLieu);
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);

                await NotificationWindow.ShowSuccessAsync("Đã xuất file Excel báo cáo tình hình nhập/xuất thành công!", owner: window);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel báo cáo tình hình nhập xuất", ex);
            ThongBaoLoi = "Lỗi khi xuất file Excel: " + ex.Message;
        }
    }
}
