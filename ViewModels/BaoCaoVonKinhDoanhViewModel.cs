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
/// ViewModel cho Giao diện 58 – Báo cáo vốn kinh doanh &amp; Tài sản lưu động.
/// </summary>
public partial class BaoCaoVonKinhDoanhViewModel : ObservableObject
{
    private readonly BaoCaoTaiChinhService _service;
    private CancellationTokenSource? _cts;

    public BaoCaoVonKinhDoanhViewModel(DatabaseService db)
    {
        _service = new BaoCaoTaiChinhService(db.TaoKetNoiAsync);

        MocThoiGian = DateTime.Now;
        MocThoiGianChon = new DateTimeOffset(MocThoiGian);
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
    private BaoCaoVonKinhDoanhDuLieu duLieu = new();

    public ObservableCollection<ChiTieuBaoCaoItem> BangChiTieu { get; } = new();
    public ObservableCollection<TonKhoTheoNhomItem> ChiTietTonKhoTheoNhom { get; } = new();
    public ObservableCollection<CongNoDoiTacItem> TopKhachHangNo { get; } = new();
    public ObservableCollection<CongNoDoiTacItem> TopNhaCungCapNo { get; } = new();

    // ============================================================
    // MỐC THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MocThoiGianHienThi))]
    private DateTime mocThoiGian;

    [ObservableProperty] private DateTimeOffset? mocThoiGianChon;

    public string MocThoiGianHienThi => MocThoiGian.ToString("dd/MM/yyyy HH:mm");

    // Chỉ số hiển thị trên thẻ tóm tắt
    public string TongTaiSanHienThi => DuLieu.TongTaiSanLuuDong.ToString("N0") + " đ";
    public string TienMatVaNganHangHienThi => DuLieu.TongTienMatVaNganHang.ToString("N0") + " đ";
    public string TienMatHienThi => DuLieu.TienMatTaiQuy.ToString("N0") + " đ";
    public string TienGuiHienThi => DuLieu.TienGuiNganHang.ToString("N0") + " đ";
    public string GiaTriTonKhoHienThi => DuLieu.GiaTriHangTonKho.ToString("N0") + " đ";
    public string PhaiThuKhachHangHienThi => DuLieu.PhaiThuKhachHang.ToString("N0") + " đ";
    public string PhaiTraNhaCungCapHienThi => DuLieu.PhaiTraNhaCungCap.ToString("N0") + " đ";
    public string VonKinhDoanhRongHienThi => DuLieu.VonKinhDoanhRong.ToString("N0") + " đ";

    public bool CoTonKho => ChiTietTonKhoTheoNhom.Count > 0;
    public bool CoKhachHangNo => TopKhachHangNo.Count > 0;
    public bool CoNhaCungCapNo => TopNhaCungCapNo.Count > 0;

    // ============================================================
    // LỆNH THAO TÁC
    // ============================================================

    [RelayCommand]
    public async Task DatHienTaiAsync()
    {
        MocThoiGian = DateTime.Now;
        MocThoiGianChon = new DateTimeOffset(MocThoiGian);
        await TaiLaiAsync();
    }

    [RelayCommand]
    public async Task ApDungMocThoiGianAsync()
    {
        if (MocThoiGianChon.HasValue)
            MocThoiGian = MocThoiGianChon.Value.DateTime;

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
            var res = await _service.TaiBaoCaoVonKinhDoanhAsync(MocThoiGian, token);

            if (token.IsCancellationRequested)
                return;

            DuLieu = res;

            BangChiTieu.Clear();
            foreach (var item in res.BangChiTieu)
                BangChiTieu.Add(item);

            ChiTietTonKhoTheoNhom.Clear();
            foreach (var item in res.ChiTietTonKhoTheoNhom)
                ChiTietTonKhoTheoNhom.Add(item);

            TopKhachHangNo.Clear();
            foreach (var item in res.TopKhachHangNo)
                TopKhachHangNo.Add(item);

            TopNhaCungCapNo.Clear();
            foreach (var item in res.TopNhaCungCapNo)
                TopNhaCungCapNo.Add(item);

            OnPropertyChanged(nameof(TongTaiSanHienThi));
            OnPropertyChanged(nameof(TienMatVaNganHangHienThi));
            OnPropertyChanged(nameof(TienMatHienThi));
            OnPropertyChanged(nameof(TienGuiHienThi));
            OnPropertyChanged(nameof(GiaTriTonKhoHienThi));
            OnPropertyChanged(nameof(PhaiThuKhachHangHienThi));
            OnPropertyChanged(nameof(PhaiTraNhaCungCapHienThi));
            OnPropertyChanged(nameof(VonKinhDoanhRongHienThi));
            OnPropertyChanged(nameof(CoTonKho));
            OnPropertyChanged(nameof(CoKhachHangNo));
            OnPropertyChanged(nameof(CoNhaCungCapNo));
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua huỷ tác vụ
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Lỗi tải báo cáo vốn kinh doanh: " + ex.Message;
            SafeLog.WriteError("Loi TaiBaoCaoVonKinhDoanh", ex);
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

            using var wb = BaoCaoTaiChinhService.TaoWorkbookVonKinhDoanh(DuLieu);

            var fileName = $"BaoCaoVonKinhDoanh_{MocThoiGian:yyyyMMdd_HHmm}.xlsx";

            var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất Excel Báo cáo vốn kinh doanh",
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
