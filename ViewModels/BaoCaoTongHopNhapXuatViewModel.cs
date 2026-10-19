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
/// ViewModel cho GIAO DIỆN 63 – Báo cáo tổng hợp nhập xuất (Xuất - Nhập - Tồn kho).
/// </summary>
public partial class BaoCaoTongHopNhapXuatViewModel : ObservableObject
{
    private readonly BaoCaoNhapXuatService _service;
    private CancellationTokenSource? _cts;

    public BaoCaoTongHopNhapXuatViewModel(DatabaseService db)
    {
        _service = new BaoCaoNhapXuatService(db.TaoKetNoiAsync);

        // Mặc định khoảng "Tháng này"
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
    [NotifyPropertyChangedFor(nameof(TongTonDauHienThi))]
    [NotifyPropertyChangedFor(nameof(TongGiaTriDauHienThi))]
    [NotifyPropertyChangedFor(nameof(TongNhapHienThi))]
    [NotifyPropertyChangedFor(nameof(TongGiaTriNhapHienThi))]
    [NotifyPropertyChangedFor(nameof(TongXuatHienThi))]
    [NotifyPropertyChangedFor(nameof(TongGiaTriXuatHienThi))]
    [NotifyPropertyChangedFor(nameof(TongTonCuoiHienThi))]
    [NotifyPropertyChangedFor(nameof(TongGiaTriTonCuoiHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoMatHangHienThi))]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    [NotifyPropertyChangedFor(nameof(KhongCoDuLieu))]
    private BaoCaoTongHopNhapXuatDuLieu duLieu = new();

    public ObservableCollection<BaoCaoTongHopNhapXuatItem> BangDuLieu { get; } = new();
    public ObservableCollection<BaoCaoNhapXuatNhomOption> NhomHangOptions { get; } = new();

    public bool CoDuLieu => BangDuLieu.Count > 0;
    public bool KhongCoDuLieu => !DangTai && BangDuLieu.Count == 0;

    // ============================================================
    // BỘ LỌC
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    [ObservableProperty] private DateTimeOffset? tuNgayChon;
    [ObservableProperty] private DateTimeOffset? denNgayChon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomNaySelected))]
    [NotifyPropertyChangedFor(nameof(IsHomQuaSelected))]
    [NotifyPropertyChangedFor(nameof(Is7NgaySelected))]
    [NotifyPropertyChangedFor(nameof(IsTuanNaySelected))]
    [NotifyPropertyChangedFor(nameof(IsThangNaySelected))]
    [NotifyPropertyChangedFor(nameof(IsThangTruocSelected))]
    [NotifyPropertyChangedFor(nameof(IsTuyChinhSelected))]
    private string khoangThoiGian = "Tháng này";

    [ObservableProperty] private bool tuyChinh;

    public bool IsHomNaySelected => KhoangThoiGian == "Hôm nay";
    public bool IsHomQuaSelected => KhoangThoiGian == "Hôm qua";
    public bool Is7NgaySelected => KhoangThoiGian == "7 ngày";
    public bool IsTuanNaySelected => KhoangThoiGian == "Tuần này";
    public bool IsThangNaySelected => KhoangThoiGian == "Tháng này";
    public bool IsThangTruocSelected => KhoangThoiGian == "Tháng trước";
    public bool IsTuyChinhSelected => TuyChinh;

    public string KhoangHienThi => $"{TuNgay:dd/MM/yyyy} – {DenNgay:dd/MM/yyyy}";

    [ObservableProperty] private BaoCaoNhapXuatNhomOption? selectedNhomHang;
    [ObservableProperty] private string timKiem = string.Empty;
    [ObservableProperty] private bool chiHienCoPhatSinhHoacTon = true;

    // KPI hiển thị trên thẻ
    public string TongTonDauHienThi => DuLieu.TongTonDauHienThi;
    public string TongGiaTriDauHienThi => DuLieu.TongGiaTriDauHienThi;
    public string TongNhapHienThi => DuLieu.TongNhapHienThi;
    public string TongGiaTriNhapHienThi => DuLieu.TongGiaTriNhapHienThi;
    public string TongXuatHienThi => DuLieu.TongXuatHienThi;
    public string TongGiaTriXuatHienThi => DuLieu.TongGiaTriXuatHienThi;
    public string TongTonCuoiHienThi => DuLieu.TongTonCuoiHienThi;
    public string TongGiaTriTonCuoiHienThi => DuLieu.TongGiaTriTonCuoiHienThi;
    public string TongSoMatHangHienThi => $"{DuLieu.TongSoMatHang:N0} mặt hàng";

    // ============================================================
    // LỆNH THAO TÁC
    // ============================================================

    [RelayCommand]
    public void ChonKhoang(string? khoang)
    {
        if (string.IsNullOrWhiteSpace(khoang))
            return;

        KhoangThoiGian = khoang;
        TuyChinh = false;

        var homNay = DateTime.Today;

        switch (khoang)
        {
            case "Hôm nay":
                TuNgay = homNay;
                DenNgay = homNay;
                break;

            case "Hôm qua":
                TuNgay = homNay.AddDays(-1);
                DenNgay = homNay.AddDays(-1);
                break;

            case "7 ngày":
                TuNgay = homNay.AddDays(-6);
                DenNgay = homNay;
                break;

            case "Tuần này":
                int delta = DayOfWeek.Monday - homNay.DayOfWeek;
                if (delta > 0) delta -= 7;
                TuNgay = homNay.AddDays(delta);
                DenNgay = homNay;
                break;

            case "Tháng này":
                TuNgay = new DateTime(homNay.Year, homNay.Month, 1);
                DenNgay = homNay;
                break;

            case "Tháng trước":
                var thangTruoc = homNay.AddMonths(-1);
                TuNgay = new DateTime(thangTruoc.Year, thangTruoc.Month, 1);
                DenNgay = new DateTime(thangTruoc.Year, thangTruoc.Month, DateTime.DaysInMonth(thangTruoc.Year, thangTruoc.Month));
                break;

            case "Tùy chọn":
                TuyChinh = true;
                return;
        }

        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);

        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void BatTuyChinh()
    {
        TuyChinh = !TuyChinh;
        if (TuyChinh)
        {
            KhoangThoiGian = "Tùy chọn";
        }
    }

    [RelayCommand]
    public async Task ApDungTuyChinhAsync()
    {
        if (TuNgayChon.HasValue && DenNgayChon.HasValue)
        {
            var tu = TuNgayChon.Value.Date;
            var den = DenNgayChon.Value.Date;

            if (tu > den)
            {
                ThongBaoLoi = "Từ ngày không được lớn hơn Đến ngày.";
                return;
            }

            TuNgay = tu;
            DenNgay = den;
            KhoangThoiGian = "Tùy chọn";
            TuyChinh = false;

            await TaiLaiAsync();
        }
    }

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        if (DangTai)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        DangTai = true;
        ThongBaoLoi = string.Empty;

        try
        {
            // Tải nhóm hàng lần đầu nếu danh sách trống
            if (NhomHangOptions.Count == 0)
            {
                var nhomList = await _service.TaiNhomHangAsync(token);
                NhomHangOptions.Clear();
                foreach (var n in nhomList)
                {
                    NhomHangOptions.Add(n);
                }
                SelectedNhomHang = NhomHangOptions.FirstOrDefault();
            }

            int nhomId = SelectedNhomHang?.Id ?? -1;

            var data = await _service.TaiTongHopNhapXuatAsync(
                TuNgay,
                DenNgay,
                nhomId,
                TimKiem,
                ChiHienCoPhatSinhHoacTon,
                token);

            if (token.IsCancellationRequested)
                return;

            DuLieu = data;

            BangDuLieu.Clear();
            foreach (var item in data.Items)
            {
                BangDuLieu.Add(item);
            }

            OnPropertyChanged(nameof(CoDuLieu));
            OnPropertyChanged(nameof(KhongCoDuLieu));
        }
        catch (OperationCanceledException)
        {
            // Đã hủy truy vấn khi người dùng thao tác mới
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không thể tải báo cáo tổng hợp nhập xuất: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiLaiAsync BaoCaoTongHopNhapXuatViewModel", ex);
        }
        finally
        {
            DangTai = false;
        }
    }

    [RelayCommand]
    public async Task TimKiemAsync()
    {
        await TaiLaiAsync();
    }

    [RelayCommand]
    public async Task XoaLocAsync()
    {
        TimKiem = string.Empty;
        SelectedNhomHang = NhomHangOptions.FirstOrDefault();
        ChiHienCoPhatSinhHoacTon = true;
        ChonKhoang("Tháng này");
    }

    [RelayCommand]
    public async Task XuatExcelAsync()
    {
        if (BangDuLieu.Count == 0)
        {
            ThongBaoLoi = "Không có dữ liệu để xuất Excel.";
            return;
        }

        try
        {
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var window = app?.MainWindow;
            if (window == null) return;

            var storageProvider = window.StorageProvider;
            if (!storageProvider.CanSave) return;

            var fileName = $"BaoCaoTongHopNhapXuat_{TuNgay:yyyyMMdd}_{DenNgay:yyyyMMdd}.xlsx";

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Lưu Báo cáo tổng hợp nhập xuất Excel",
                SuggestedFileName = fileName,
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
                using var wb = BaoCaoNhapXuatService.TaoWorkbookTongHopNhapXuat(DuLieu);
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);
            }
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Lỗi khi xuất file Excel: " + ex.Message;
            SafeLog.WriteError("Lỗi XuatExcelAsync BaoCaoTongHopNhapXuat", ex);
        }
    }
}
