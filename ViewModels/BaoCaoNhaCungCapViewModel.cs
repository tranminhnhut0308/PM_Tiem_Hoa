using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho màn hình "Báo cáo nhà cung cấp" (thiết kế đồng bộ với Báo cáo bán hàng):
/// - Header tiêu đề + bộ lọc thời gian (Hôm nay / Hôm qua / Tuần này / Tháng này / Tùy chỉnh) + Xuất Excel.
/// - Thẻ chỉ số tổng hợp, biểu đồ nhập hàng theo thời gian và biểu đồ Top 10 nhà cung cấp.
/// - Bảng chi tiết nhà cung cấp có sắp xếp, phân trang, dòng tổng cộng; bấm một dòng để
///   xem danh sách phiếu nhập của nhà cung cấp đó trong kỳ.
/// - Không hard-code: mọi con số truy vấn trực tiếp từ database.
/// </summary>
public partial class BaoCaoNhaCungCapViewModel : ObservableObject
{
    private readonly BaoCaoNhaCungCapService _service;

    private bool _khoiTaoXong;
    private CancellationTokenSource? _searchCts;
    private int _phienBan;

    // Danh sách phiếu nhập của nhà cung cấp đang xem chi tiết (chưa phân trang / lọc).
    private readonly List<BaoCaoNhaCungCapPhieuNhap> _chiTietPhieuNhapAll = new();

    public BaoCaoNhaCungCapViewModel(DatabaseService db)
    {
        _service = new BaoCaoNhaCungCapService(db.TaoKetNoiAsync);

        SapXepKho = BaoCaoNhaCungCapService.KhoaMacDinh;
        SapXepTang = false;
        TimKiem = string.Empty;
        SoDongTrang = 15;
        Trang = 1;

        // Mặc định chọn "Tuần này" như giao diện Báo cáo bán hàng.
        DatKhoangTuanNay();

        _khoiTaoXong = true;
    }

    // ============================================================
    // TRẠNG THÁI
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrEmpty(ThongBaoLoi);

    // ============================================================
    // BỘ LỌC THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    [NotifyPropertyChangedFor(nameof(DangChonHomNay))]
    [NotifyPropertyChangedFor(nameof(DangChonHomQua))]
    [NotifyPropertyChangedFor(nameof(DangChonTuanNay))]
    [NotifyPropertyChangedFor(nameof(DangChonThangNay))]
    [NotifyPropertyChangedFor(nameof(DangChonTuyChinh))]
    private string khoangThoiGian = "Tuần này";

    public bool DangChonHomNay => KhoangThoiGian == "Hôm nay";
    public bool DangChonHomQua => KhoangThoiGian == "Hôm qua";
    public bool DangChonTuanNay => KhoangThoiGian == "Tuần này";
    public bool DangChonThangNay => KhoangThoiGian == "Tháng này";
    public bool DangChonTuyChinh => KhoangThoiGian == "Tùy chọn";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? tuNgayChon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? denNgayChon;

    public bool CoKhoangTuyChinh => TuNgayChon is not null && DenNgayChon is not null;

    public string KhoangHienThi => $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";

    private async Task DatKhoangAsync(DateTime tu, DateTime den, string nhan)
    {
        TuNgay = tu.Date;
        DenNgay = den.Date;
        TuNgayChon = new DateTimeOffset(tu.Date);
        DenNgayChon = new DateTimeOffset(den.Date);
        KhoangThoiGian = nhan;

        Trang = 1;
        DongChiTietNoiBo();
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task VeHomNayAsync()
    {
        await DatKhoangAsync(DateTime.Today, DateTime.Today, "Hôm nay");
    }

    [RelayCommand]
    private async Task VeNgayQuaAsync()
    {
        var homQua = DateTime.Today.AddDays(-1);
        await DatKhoangAsync(homQua, homQua, "Hôm qua");
    }

    [RelayCommand]
    private async Task VeTuanNayAsync()
    {
        // Tuần bắt đầu từ thứ Hai.
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7;
        await DatKhoangAsync(homNay.AddDays(-soNgayDaQua), homNay, "Tuần này");
    }

    [RelayCommand]
    private async Task VeThangNayAsync()
    {
        var homNay = DateTime.Today;
        await DatKhoangAsync(new DateTime(homNay.Year, homNay.Month, 1), homNay, "Tháng này");
    }

    [RelayCommand]
    private async Task ApDungTuyChinhAsync()
    {
        if (TuNgayChon is null || DenNgayChon is null)
            return;

        var tu = TuNgayChon.Value.Date;
        var den = DenNgayChon.Value.Date;

        // Người dùng chọn ngược vẫn phải chạy được.
        if (den < tu)
            (tu, den) = (den, tu);

        await DatKhoangAsync(tu, den, "Tùy chọn");
    }

    private void DatKhoangTuanNay()
    {
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7; // Thứ 2 là đầu tuần
        TuNgay = homNay.AddDays(-soNgayDaQua);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Tuần này";
    }

    // ============================================================
    // TÌM KIẾM NHÀ CUNG CẤP
    // ============================================================

    [ObservableProperty] private string timKiem = string.Empty;

    partial void OnTimKiemChanged(string value)
    {
        if (!_khoiTaoXong)
            return;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested)
                    return;

                Trang = 1;
                await TaiLaiAsync(token);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    // ============================================================
    // SẮP XẾP BẢNG
    // ============================================================

    [ObservableProperty] private string sapXepKho = BaoCaoNhaCungCapService.KhoaMacDinh;
    [ObservableProperty] private bool sapXepTang;

    [RelayCommand]
    private async Task SapXepAsync(string? cot)
    {
        if (string.IsNullOrWhiteSpace(cot))
            return;

        if (string.Equals(SapXepKho, cot, StringComparison.OrdinalIgnoreCase))
        {
            SapXepTang = !SapXepTang;
        }
        else
        {
            SapXepKho = cot;
            SapXepTang = false;
        }

        Trang = 1;
        await TaiLaiAsync();
    }

    public string NhanSapXep(string cot)
    {
        if (!string.Equals(SapXepKho, cot, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return SapXepTang ? " ▲" : " ▼";
    }

    // ============================================================
    // PHÂN TRANG
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    private int trang = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    private int soDongTrang = 15;

    public IReadOnlyList<int> SoDongTrangOptions { get; } = [15, 25, 50, 100];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    private int tongSoBanGhi;

    public int TongSoTrang => SoDongTrang <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(TongSoBanGhi / (double)SoDongTrang));

    public bool CanTrangTruoc => Trang > 1 && !DangTai;
    public bool CanTrangSau => Trang < TongSoTrang && !DangTai;

    public string RangeText
    {
        get
        {
            if (TongSoBanGhi == 0)
                return "0 nhà cung cấp";

            var tu = (Trang - 1) * SoDongTrang + 1;
            var den = Math.Min(tu + SoDongTrang - 1, TongSoBanGhi);
            return $"{tu} - {den} trong {TongSoBanGhi:N0} nhà cung cấp";
        }
    }

    partial void OnSoDongTrangChanged(int value)
    {
        if (!_khoiTaoXong)
            return;

        Trang = 1;
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangDauAsync()
    {
        if (Trang == 1) return;
        Trang = 1;
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangTruocAsync()
    {
        if (Trang <= 1) return;
        Trang--;
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangSauAsync()
    {
        if (Trang >= TongSoTrang) return;
        Trang++;
        await TaiLaiAsync();
    }

    [RelayCommand]
    private async Task TrangCuoiAsync()
    {
        if (Trang == TongSoTrang) return;
        Trang = TongSoTrang;
        await TaiLaiAsync();
    }

    // ============================================================
    // DỮ LIỆU HIỂN THỊ
    // ============================================================

    [ObservableProperty] private BaoCaoNhaCungCapTongHop tongHop = new();

    public ObservableCollection<BaoCaoNhaCungCapDiemThoiGian> BieuDoItems { get; } = new();
    public ObservableCollection<BaoCaoNhaCungCapTop> TopItems { get; } = new();
    public ObservableCollection<BaoCaoNhaCungCapDong> DongTrangs { get; } = new();
    public ObservableCollection<BaoCaoNhaCungCapScaleMark> ScaleMarks { get; } = new();

    [ObservableProperty] private bool coBieuDo;
    [ObservableProperty] private bool coTop;
    [ObservableProperty] private bool coDongTrang;

    public string TieuDeBieuDo => "Top 10 nhà cung cấp nhập hàng nhiều nhất (đã trừ trả hàng)";

    [ObservableProperty] private decimal maxScaleValue = 30_000_000m;
    private string[] _scaleLabels = ["0", "3 tr", "6 tr", "9 tr", "12 tr", "15 tr", "18 tr", "21 tr", "24 tr", "27 tr", "30 tr"];

    public string ScaleMark0 => _scaleLabels.Length > 0 ? _scaleLabels[0] : "0";
    public string ScaleMark1 => _scaleLabels.Length > 1 ? _scaleLabels[1] : "";
    public string ScaleMark2 => _scaleLabels.Length > 2 ? _scaleLabels[2] : "";
    public string ScaleMark3 => _scaleLabels.Length > 3 ? _scaleLabels[3] : "";
    public string ScaleMark4 => _scaleLabels.Length > 4 ? _scaleLabels[4] : "";
    public string ScaleMark5 => _scaleLabels.Length > 5 ? _scaleLabels[5] : "";
    public string ScaleMark6 => _scaleLabels.Length > 6 ? _scaleLabels[6] : "";
    public string ScaleMark7 => _scaleLabels.Length > 7 ? _scaleLabels[7] : "";
    public string ScaleMark8 => _scaleLabels.Length > 8 ? _scaleLabels[8] : "";
    public string ScaleMark9 => _scaleLabels.Length > 9 ? _scaleLabels[9] : "";
    public string ScaleMark10 => _scaleLabels.Length > 10 ? _scaleLabels[10] : "";

    private string[] _bieuDoTrucY = ["", "", "", "", ""];

    public string BieuDoTrucY0 => _bieuDoTrucY.Length > 0 ? _bieuDoTrucY[0] : "";
    public string BieuDoTrucY1 => _bieuDoTrucY.Length > 1 ? _bieuDoTrucY[1] : "";
    public string BieuDoTrucY2 => _bieuDoTrucY.Length > 2 ? _bieuDoTrucY[2] : "";
    public string BieuDoTrucY3 => _bieuDoTrucY.Length > 3 ? _bieuDoTrucY[3] : "";
    public string BieuDoTrucY4 => _bieuDoTrucY.Length > 4 ? _bieuDoTrucY[4] : "";

    private double _currentPlotWidth;

    public void CapNhatDoRongBieuDo(double plotWidth)
    {
        if (plotWidth <= 20) return;
        _currentPlotWidth = plotWidth;
        if (MaxScaleValue <= 0) return;

        foreach (var item in TopItems)
        {
            item.RongBar = Math.Max(0, (double)(item.GiaTriNhapHang / MaxScaleValue) * plotWidth);
        }
    }

    // ============================================================
    // CHI TIẾT PHIẾU NHẬP THEO NHÀ CUNG CẤP
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DangXemChiTiet))]
    [NotifyPropertyChangedFor(nameof(CoChiTietPhieuNhap))]
    [NotifyPropertyChangedFor(nameof(ChiTietTieuDe))]
    [NotifyPropertyChangedFor(nameof(ChiTietPhuDe))]
    private BaoCaoNhaCungCapDong? nhaCungCapChiTiet;

    public ObservableCollection<BaoCaoNhaCungCapPhieuNhap> ChiTietPhieuNhapList { get; } = new();

    [ObservableProperty] private string chiTietSearch = string.Empty;
    [ObservableProperty] private int chiTietPage = 1;
    [ObservableProperty] private int chiTietPageSize = 20;

    /// <summary>Có đang mở bảng chi tiết phiếu nhập của một nhà cung cấp hay không.</summary>
    public bool DangXemChiTiet => NhaCungCapChiTiet is not null;

    public string ChiTietTieuDe => NhaCungCapChiTiet is null
        ? "Chi tiết phiếu nhập"
        : $"Phiếu nhập của {NhaCungCapChiTiet.TenNhaCungCap}";

    public string ChiTietPhuDe => NhaCungCapChiTiet is null
        ? string.Empty
        : $"{NhaCungCapChiTiet.MaNhaCungCap} • {KhoangHienThi}";

    private List<BaoCaoNhaCungCapPhieuNhap> ChiTietDaLoc =>
        string.IsNullOrWhiteSpace(ChiTietSearch)
            ? _chiTietPhieuNhapAll
            : _chiTietPhieuNhapAll.Where(x =>
                    x.MaPhieuNhap.Contains(ChiTietSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

    public int ChiTietTotalCount => ChiTietDaLoc.Count;
    public int ChiTietTotalPages => ChiTietTotalCount <= 0 ? 0 : (int)Math.Ceiling(ChiTietTotalCount / (double)ChiTietPageSize);
    public bool CanChiTietTrangTruoc => ChiTietPage > 1;
    public bool CanChiTietTrangSau => ChiTietPage < ChiTietTotalPages;
    public string ChiTietTrangText => ChiTietTotalPages <= 0 ? "0/0" : $"{ChiTietPage}/{ChiTietTotalPages}";

    public bool CoChiTietPhieuNhap => NhaCungCapChiTiet is not null && ChiTietDaLoc.Count > 0;

    [RelayCommand]
    private async Task XemChiTietNhaCungCapAsync(BaoCaoNhaCungCapDong? dong)
    {
        if (dong is null)
            return;

        try
        {
            NhaCungCapChiTiet = dong;
            _chiTietPhieuNhapAll.Clear();
            _chiTietPhieuNhapAll.AddRange(
                await _service.TaiPhieuNhapCuaNhaCungCapAsync(dong.Id, TuNgay, DenNgay, default));

            ChiTietSearch = string.Empty;
            ChiTietPage = 1;
            CapNhatChiTietHienThi();
            OnPropertyChanged(nameof(CoChiTietPhieuNhap));
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không tải được chi tiết phiếu nhập: " + ex.Message;
        }
    }

    [RelayCommand]
    private void DongChiTiet() => DongChiTietNoiBo();

    private void DongChiTietNoiBo()
    {
        NhaCungCapChiTiet = null;
        _chiTietPhieuNhapAll.Clear();
        ChiTietSearch = string.Empty;
        ChiTietPage = 1;
        ChiTietPhieuNhapList.Clear();

        OnPropertyChanged(nameof(CoChiTietPhieuNhap));
        ThongBaoChiTietPhanTrang();
    }

    partial void OnChiTietSearchChanged(string value)
    {
        if (ChiTietPage != 1)
            ChiTietPage = 1;
        else
            CapNhatChiTietHienThi();

        OnPropertyChanged(nameof(CoChiTietPhieuNhap));
    }

    partial void OnChiTietPageChanged(int value) => CapNhatChiTietHienThi();

    private void CapNhatChiTietHienThi()
    {
        var daLoc = ChiTietDaLoc;
        var trang = Math.Max(1, ChiTietPage);
        ChiTietPhieuNhapList.Clear();

        foreach (var row in daLoc.Skip((trang - 1) * ChiTietPageSize).Take(ChiTietPageSize))
            ChiTietPhieuNhapList.Add(row);

        ThongBaoChiTietPhanTrang();
    }

    private void ThongBaoChiTietPhanTrang()
    {
        OnPropertyChanged(nameof(ChiTietTotalCount));
        OnPropertyChanged(nameof(ChiTietTotalPages));
        OnPropertyChanged(nameof(CanChiTietTrangTruoc));
        OnPropertyChanged(nameof(CanChiTietTrangSau));
        OnPropertyChanged(nameof(ChiTietTrangText));
    }

    [RelayCommand]
    private void ChiTietTrangTruoc()
    {
        if (ChiTietPage > 1)
            ChiTietPage--;
    }

    [RelayCommand]
    private void ChiTietTrangSau()
    {
        if (ChiTietPage < ChiTietTotalPages)
            ChiTietPage++;
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync(CancellationToken token = default)
    {
        var phienBanHienTai = ++_phienBan;
        DangTai = true;
        ThongBaoLoi = string.Empty;

        try
        {
            var res = await _service.TaiDuLieuAsync(
                TuNgay,
                DenNgay,
                TimKiem,
                SapXepKho,
                SapXepTang,
                Trang,
                SoDongTrang,
                token);

            if (phienBanHienTai != _phienBan)
                return;

            TongHop = res.TongHop;
            TongSoBanGhi = res.TongSoBanGhi;
            MaxScaleValue = res.MaxScaleValue;

            if (res.ScaleLabels is { Length: >= 11 })
            {
                _scaleLabels = res.ScaleLabels;
                for (int i = 0; i <= 10; i++)
                    OnPropertyChanged($"ScaleMark{i}");
            }

            if (res.BieuDoTrucY is { Length: >= 5 })
            {
                _bieuDoTrucY = res.BieuDoTrucY;
                for (int i = 0; i < 5; i++)
                    OnPropertyChanged($"BieuDoTrucY{i}");
            }

            BieuDoItems.Clear();
            foreach (var item in res.BieuDo)
                BieuDoItems.Add(item);

            TopItems.Clear();
            foreach (var item in res.Top)
            {
                if (_currentPlotWidth > 20 && MaxScaleValue > 0)
                {
                    item.RongBar = Math.Max(0, (double)(item.GiaTriNhapHang / MaxScaleValue) * _currentPlotWidth);
                }
                TopItems.Add(item);
            }

            DongTrangs.Clear();
            foreach (var item in res.DongTrang)
                DongTrangs.Add(item);

            ScaleMarks.Clear();
            foreach (var m in res.ScaleMarks)
                ScaleMarks.Add(m);

            CoBieuDo = BieuDoItems.Count > 0;
            CoTop = TopItems.Count > 0;
            CoDongTrang = DongTrangs.Count > 0;

            OnPropertyChanged(nameof(RangeText));
            OnPropertyChanged(nameof(TongSoTrang));
            OnPropertyChanged(nameof(CanTrangTruoc));
            OnPropertyChanged(nameof(CanTrangSau));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (phienBanHienTai == _phienBan)
            {
                ThongBaoLoi = "Không thể tải báo cáo nhà cung cấp: " + ex.Message;
            }
        }
        finally
        {
            if (phienBanHienTai == _phienBan)
            {
                DangTai = false;
                OnPropertyChanged(nameof(CanTrangTruoc));
                OnPropertyChanged(nameof(CanTrangSau));
            }
        }
    }

    // ============================================================
    // XUẤT EXCEL
    // ============================================================

    /// <summary>
    /// Xuất Excel toàn bộ nhà cung cấp khớp bộ lọc hiện tại (không phụ thuộc trang
    /// đang xem): truy vấn lại với cỡ trang bằng tổng số bản ghi.
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        try
        {
            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            var soDong = Math.Max(1, TongSoBanGhi);

            var duLieu = await _service.TaiDuLieuAsync(
                TuNgay,
                DenNgay,
                TimKiem,
                SapXepKho,
                SapXepTang,
                1,
                soDong,
                default);

            using var wb = BaoCaoNhaCungCapExcel.TaoWorkbook(duLieu);

            var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Xuất Excel báo cáo nhà cung cấp",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                },
                SuggestedFileName = duLieu.TongHop.TenFileXuat()
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
