using System;
using System.Collections.ObjectModel;
using System.IO;
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
/// ViewModel cho Giao diện 57 – Báo cáo kết quả hoạt động kinh doanh (P&amp;L).
/// </summary>
public partial class BaoCaoKetQuaKinhDoanhViewModel : ObservableObject
{
    private readonly BaoCaoTaiChinhService _service;
    private CancellationTokenSource? _cts;

    public BaoCaoKetQuaKinhDoanhViewModel(DatabaseService db)
    {
        _service = new BaoCaoTaiChinhService(db.TaoKetNoiAsync);

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
    // TRẠNG THÁI
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    [ObservableProperty]
    private BaoCaoKetQuaKinhDoanhDuLieu duLieu = new();

    public ObservableCollection<ChiTieuBaoCaoItem> BangChiTieu { get; } = new();
    public ObservableCollection<ChiPhiTheoNhomItem> ChiTietChiPhi { get; } = new();
    public ObservableCollection<KetQuaKinhDoanhTheoNgayItem> ChiTietTheoNgay { get; } = new();

    // ============================================================
    // BỘ LỌC THỜI GIAN
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
    private string khoangThoiGian = "Tháng này";

    [ObservableProperty] private bool tuyChinh;

    public bool IsHomNaySelected => KhoangThoiGian == "Hôm nay";
    public bool IsHomQuaSelected => KhoangThoiGian == "Hôm qua";
    public bool Is7NgaySelected => KhoangThoiGian == "7 ngày";
    public bool IsTuanNaySelected => KhoangThoiGian == "Tuần này";
    public bool IsThangNaySelected => KhoangThoiGian == "Tháng này";
    public bool IsThangTruocSelected => KhoangThoiGian == "Tháng trước";

    public string KhoangHienThi => $"{TuNgay:dd/MM/yyyy} – {DenNgay:dd/MM/yyyy}";

    // Chỉ số hiển thị trên thẻ tóm tắt
    public string DoanhThuThuanHienThi => DuLieu.DoanhThuThuan.ToString("N0") + " đ";
    public string GiaVonHienThi => DuLieu.GiaVonHangBan.ToString("N0") + " đ";
    public string LoiNhuanGopHienThi => DuLieu.LoiNhuanGop.ToString("N0") + " đ";
    public string TySuatLoiNhuanGopHienThi => $"{DuLieu.TySuatLoiNhuanGop:F1}%";
    public string ChiPhiHienThi => DuLieu.ChiPhiKinhDoanh.ToString("N0") + " đ";
    public string LoiNhuanThuanHienThi => DuLieu.TongLoiNhuan.ToString("N0") + " đ";
    public string TySuatLoiNhuanThuanHienThi => $"{DuLieu.TySuatLoiNhuanThuan:F1}%";

    public bool CoChiPhi => ChiTietChiPhi.Count > 0;
    public bool CoChiTietTheoNgay => ChiTietTheoNgay.Count > 0;

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
                int diff = (7 + (homNay.DayOfWeek - DayOfWeek.Monday)) % 7;
                TuNgay = homNay.AddDays(-diff);
                DenNgay = homNay;
                break;
            case "Tháng này":
                TuNgay = new DateTime(homNay.Year, homNay.Month, 1);
                DenNgay = homNay;
                break;
            case "Tháng trước":
                var dauThangNay = new DateTime(homNay.Year, homNay.Month, 1);
                var cuoiThangTruoc = dauThangNay.AddDays(-1);
                TuNgay = new DateTime(cuoiThangTruoc.Year, cuoiThangTruoc.Month, 1);
                DenNgay = cuoiThangTruoc;
                break;
        }

        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);

        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public void BatTuyChinh()
    {
        TuyChinh = true;
        KhoangThoiGian = "Tùy chọn";
    }

    [RelayCommand]
    public async Task ApDungTuyChinhAsync()
    {
        if (TuNgayChon.HasValue)
            TuNgay = TuNgayChon.Value.Date;
        if (DenNgayChon.HasValue)
            DenNgay = DenNgayChon.Value.Date;

        if (TuNgay > DenNgay)
        {
            (TuNgay, DenNgay) = (DenNgay, TuNgay);
            TuNgayChon = new DateTimeOffset(TuNgay);
            DenNgayChon = new DateTimeOffset(DenNgay);
        }

        await TaiLaiAsync();
    }

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        DangTai = true;
        ThongBaoLoi = string.Empty;

        try
        {
            var res = await _service.TaiBaoCaoKetQuaKinhDoanhAsync(TuNgay, DenNgay, token);

            if (token.IsCancellationRequested)
                return;

            DuLieu = res;

            BangChiTieu.Clear();
            foreach (var item in res.BangChiTieu)
                BangChiTieu.Add(item);

            ChiTietChiPhi.Clear();
            foreach (var item in res.ChiTietChiPhi)
                ChiTietChiPhi.Add(item);

            ChiTietTheoNgay.Clear();
            foreach (var item in res.ChiTietTheoNgay)
                ChiTietTheoNgay.Add(item);

            OnPropertyChanged(nameof(DoanhThuThuanHienThi));
            OnPropertyChanged(nameof(GiaVonHienThi));
            OnPropertyChanged(nameof(LoiNhuanGopHienThi));
            OnPropertyChanged(nameof(TySuatLoiNhuanGopHienThi));
            OnPropertyChanged(nameof(ChiPhiHienThi));
            OnPropertyChanged(nameof(LoiNhuanThuanHienThi));
            OnPropertyChanged(nameof(TySuatLoiNhuanThuanHienThi));
            OnPropertyChanged(nameof(CoChiPhi));
            OnPropertyChanged(nameof(CoChiTietTheoNgay));
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua khi người dùng bấm chuyển bộ lọc liên tục
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Lỗi tải báo cáo kết quả kinh doanh: " + ex.Message;
            SafeLog.WriteError("Loi TaiBaoCaoKetQuaKinhDoanh", ex);
        }
        finally
        {
            DangTai = false;
        }
    }

    [RelayCommand]
    public async Task XuatExcelAsync()
    {
        try
        {
            var cuaSo = Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            using var wb = BaoCaoTaiChinhService.TaoWorkbookKetQuaKinhDoanh(DuLieu);

            var fileName = $"BaoCaoKetQuaKinhDoanh_{TuNgay:yyyyMMdd}_{DenNgay:yyyyMMdd}.xlsx";

            var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất Excel Báo cáo kết quả kinh doanh",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                },
                SuggestedFileName = fileName
            });

            if (file is not null)
            {
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);
            }
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không xuất được file Excel: " + ex.Message;
        }
    }
}
