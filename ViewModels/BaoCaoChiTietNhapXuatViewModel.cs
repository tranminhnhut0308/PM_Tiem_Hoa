using System;
using System.Collections.Generic;
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
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho GIAO DIỆN 64 – Báo cáo chi tiết nhập/xuất (Thẻ kho / Sổ chi tiết hàng hóa).
/// </summary>
public partial class BaoCaoChiTietNhapXuatViewModel : ObservableObject
{
    private readonly BaoCaoNhapXuatService _service;
    private CancellationTokenSource? _cts;

    public BaoCaoChiTietNhapXuatViewModel(DatabaseService db)
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
    // TRẠNG THÁI & THỐNG KÊ KPI
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoGiaoDichHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoLuongNhapHienThi))]
    [NotifyPropertyChangedFor(nameof(TongThanhTienNhapHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoLuongXuatHienThi))]
    [NotifyPropertyChangedFor(nameof(TongThanhTienXuatHienThi))]
    [NotifyPropertyChangedFor(nameof(CoDuLieu))]
    [NotifyPropertyChangedFor(nameof(KhongCoDuLieu))]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    private BaoCaoChiTietNhapXuatDuLieu duLieu = new();

    public ObservableCollection<BaoCaoChiTietNhapXuatItem> BangDuLieu { get; } = new();
    public ObservableCollection<BaoCaoNhapXuatNhomOption> NhomHangOptions { get; } = new();
    public ObservableCollection<BaoCaoNhapXuatSanPhamOption> SanPhamOptions { get; } = new();

    public bool CoDuLieu => BangDuLieu.Count > 0;
    public bool KhongCoDuLieu => !DangTai && BangDuLieu.Count == 0;

    // KPI hiển thị trên thẻ tóm tắt
    public string TongSoGiaoDichHienThi => DuLieu.TongSoGiaoDichHienThi;
    public string TongSoLuongNhapHienThi => DuLieu.TongSoLuongNhapHienThi;
    public string TongThanhTienNhapHienThi => DuLieu.TongThanhTienNhapHienThi;
    public string TongSoLuongXuatHienThi => DuLieu.TongSoLuongXuatHienThi;
    public string TongThanhTienXuatHienThi => DuLieu.TongThanhTienXuatHienThi;

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

    // ============================================================
    // BỘ LỌC LOẠI CHỨNG TỪ & ĐỐI TƯỢNG
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocTatCa))]
    [NotifyPropertyChangedFor(nameof(IsLocNhapHang))]
    [NotifyPropertyChangedFor(nameof(IsLocBanHang))]
    [NotifyPropertyChangedFor(nameof(IsLocKhachTra))]
    [NotifyPropertyChangedFor(nameof(IsLocTraNcc))]
    [NotifyPropertyChangedFor(nameof(IsLocNhapKhac))]
    [NotifyPropertyChangedFor(nameof(IsLocXuatNoiBo))]
    private string loaiChungTuLoc = "TAT_CA";

    public bool IsLocTatCa => LoaiChungTuLoc == "TAT_CA";
    public bool IsLocNhapHang => LoaiChungTuLoc == "NHAP_HANG";
    public bool IsLocBanHang => LoaiChungTuLoc == "BAN_HANG";
    public bool IsLocKhachTra => LoaiChungTuLoc == "KHACH_TRA";
    public bool IsLocTraNcc => LoaiChungTuLoc == "TRA_NCC";
    public bool IsLocNhapKhac => LoaiChungTuLoc == "NHAP_KHAC";
    public bool IsLocXuatNoiBo => LoaiChungTuLoc == "XUAT_NOI_BO";

    [ObservableProperty] private BaoCaoNhapXuatNhomOption? selectedNhomHang;
    [ObservableProperty] private BaoCaoNhapXuatSanPhamOption? selectedSanPham;
    [ObservableProperty] private string timKiem = string.Empty;

    public bool IsTheKhoMotSanPham => SelectedSanPham != null && SelectedSanPham.Id > 0;

    // ============================================================
    // PHÂN TRANG
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    private int page = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    private int pageSize = 20;

    public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 20, 50, 100 };

    public int TotalPages =>
        PageSize <= 0 ? 0 : (int)Math.Ceiling(DuLieu.TotalCount / (double)PageSize);

    public string RangeText
    {
        get
        {
            if (DuLieu.TotalCount == 0) return "0 giao dịch";
            int from = (Page - 1) * PageSize + 1;
            int to = Math.Min(from + PageSize - 1, DuLieu.TotalCount);
            return $"{from} - {to} trong {DuLieu.TotalCount:N0} giao dịch";
        }
    }

    public bool CanTrangTruoc => Page > 1;
    public bool CanTrangSau => Page < TotalPages;

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

        Page = 1;
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

            Page = 1;
            await TaiLaiAsync();
        }
    }

    [RelayCommand]
    public async Task LocLoaiChungTuAsync(string? loai)
    {
        var filter = string.IsNullOrWhiteSpace(loai) ? "TAT_CA" : loai;
        if (LoaiChungTuLoc == filter) return;

        LoaiChungTuLoc = filter;
        Page = 1;
        await TaiLaiAsync();
    }

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        if (DangTai) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        DangTai = true;
        ThongBaoLoi = string.Empty;

        try
        {
            // Tải danh mục nhóm và sản phẩm lần đầu
            if (NhomHangOptions.Count == 0)
            {
                var nhomList = await _service.TaiNhomHangAsync(token);
                NhomHangOptions.Clear();
                foreach (var n in nhomList) NhomHangOptions.Add(n);
                SelectedNhomHang = NhomHangOptions.FirstOrDefault();
            }

            if (SanPhamOptions.Count == 0)
            {
                var spList = await _service.TaiDanhSachSanPhamOptionAsync(-1, null, token);
                SanPhamOptions.Clear();
                foreach (var s in spList) SanPhamOptions.Add(s);
                SelectedSanPham = SanPhamOptions.FirstOrDefault();
            }

            int nhomId = SelectedNhomHang?.Id ?? -1;
            uint? spId = (SelectedSanPham != null && SelectedSanPham.Id > 0) ? SelectedSanPham.Id : null;

            var data = await _service.TaiChiTietNhapXuatPagedAsync(
                TuNgay,
                DenNgay,
                LoaiChungTuLoc,
                spId,
                nhomId,
                TimKiem,
                Page,
                PageSize,
                token);

            if (token.IsCancellationRequested) return;

            DuLieu = data;

            BangDuLieu.Clear();
            foreach (var item in data.Items)
            {
                BangDuLieu.Add(item);
            }

            OnPropertyChanged(nameof(CoDuLieu));
            OnPropertyChanged(nameof(KhongCoDuLieu));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(RangeText));
            OnPropertyChanged(nameof(CanTrangTruoc));
            OnPropertyChanged(nameof(CanTrangSau));
            OnPropertyChanged(nameof(IsTheKhoMotSanPham));
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua do hủy
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không thể tải báo cáo chi tiết nhập xuất: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiLaiAsync BaoCaoChiTietNhapXuatViewModel", ex);
        }
        finally
        {
            DangTai = false;
        }
    }

    [RelayCommand]
    public async Task TimKiemAsync()
    {
        Page = 1;
        await TaiLaiAsync();
    }

    [RelayCommand]
    public void XoaLoc()
    {
        TimKiem = string.Empty;
        LoaiChungTuLoc = "TAT_CA";
        SelectedNhomHang = NhomHangOptions.FirstOrDefault();
        SelectedSanPham = SanPhamOptions.FirstOrDefault();
        Page = 1;
        ChonKhoang("Tháng này");
    }

    // Các lệnh phân trang
    [RelayCommand]
    public async Task TrangDauAsync()
    {
        if (Page != 1)
        {
            Page = 1;
            await TaiLaiAsync();
        }
    }

    [RelayCommand]
    public async Task TrangTruocAsync()
    {
        if (CanTrangTruoc)
        {
            Page--;
            await TaiLaiAsync();
        }
    }

    [RelayCommand]
    public async Task TrangSauAsync()
    {
        if (CanTrangSau)
        {
            Page++;
            await TaiLaiAsync();
        }
    }

    [RelayCommand]
    public async Task TrangCuoiAsync()
    {
        if (Page != TotalPages && TotalPages > 0)
        {
            Page = TotalPages;
            await TaiLaiAsync();
        }
    }

    partial void OnPageSizeChanged(int value)
    {
        if (value <= 0) return;
        Page = 1;
        _ = TaiLaiAsync();
    }

    partial void OnSelectedSanPhamChanged(BaoCaoNhapXuatSanPhamOption? value)
    {
        Page = 1;
        OnPropertyChanged(nameof(IsTheKhoMotSanPham));
        _ = TaiLaiAsync();
    }

    partial void OnSelectedNhomHangChanged(BaoCaoNhapXuatNhomOption? value)
    {
        Page = 1;
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    public async Task XuatExcelAsync()
    {
        if (DuLieu.TotalCount == 0)
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

            var fileName = $"BaoCaoChiTietNhapXuat_{TuNgay:yyyyMMdd}_{DenNgay:yyyyMMdd}.xlsx";

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Lưu Báo cáo chi tiết nhập xuất (Thẻ kho) Excel",
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
                // Xuất toàn bộ dữ liệu (không phân trang)
                int nhomId = SelectedNhomHang?.Id ?? -1;
                uint? spId = (SelectedSanPham != null && SelectedSanPham.Id > 0) ? SelectedSanPham.Id : null;

                var allData = await _service.TaiChiTietNhapXuatPagedAsync(
                    TuNgay,
                    DenNgay,
                    LoaiChungTuLoc,
                    spId,
                    nhomId,
                    TimKiem,
                    1,
                    Math.Max(1, DuLieu.TotalCount),
                    CancellationToken.None);

                using var wb = BaoCaoNhapXuatService.TaoWorkbookChiTietNhapXuat(allData);
                await using var stream = await file.OpenWriteAsync();
                wb.SaveAs(stream);

                await NotificationWindow.ShowSuccessAsync("Đã xuất file Excel báo cáo chi tiết nhập xuất thành công!", owner: window);
            }
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Lỗi khi xuất file Excel: " + ex.Message;
            SafeLog.WriteError("Lỗi XuatExcelAsync BaoCaoChiTietNhapXuat", ex);
        }
    }
}
