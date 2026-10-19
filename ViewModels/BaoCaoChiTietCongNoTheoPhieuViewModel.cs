using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// ViewModel cho GIAO DIỆN 71 – Chi tiết công nợ theo phiếu.
/// </summary>
public partial class BaoCaoChiTietCongNoTheoPhieuViewModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private readonly BaoCaoChiTietCongNoTheoPhieuService _service;
    private readonly DatabaseService _db;
    private CancellationTokenSource? _cts;

    public Func<Window?>? LayCuaSoChinh { get; set; }
    public Action<string>? CapNhatTrangThai { get; set; }
    public Func<string, Task>? MoChiTietHoaDonAsync { get; set; }
    public Func<string, Task>? MoChiTietPhieuNhapAsync { get; set; }

    public BaoCaoChiTietCongNoTheoPhieuViewModel(DatabaseService db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _service = new BaoCaoChiTietCongNoTheoPhieuService(db.TaoKetNoiAsync);

        KhoangThoiGian = "Tháng này";
        TuyChinh = false;
        var today = DateTime.Today;
        TuNgay = new DateTime(today.Year, today.Month, 1);
        DenNgay = today;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
    }

    // ============================================================
    // TRẠNG THÁI & DỮ LIỆU
    // ============================================================

    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);

    public ObservableCollection<BaoCaoChiTietCongNoPhieuItem> DanhSach { get; } = new();

    public bool KhongCoDuLieu => !DangTai && DanhSach.Count == 0;

    // ============================================================
    // THỐNG KÊ KPI
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongPhatSinhHienThi))]
    [NotifyPropertyChangedFor(nameof(TongDaThanhToanHienThi))]
    [NotifyPropertyChangedFor(nameof(TongConNoHienThi))]
    [NotifyPropertyChangedFor(nameof(TongSoChungTuHienThi))]
    private BaoCaoChiTietCongNoPhieuThongKe thongKe = new();

    public string TongPhatSinhHienThi => ThongKe.TongPhatSinhHienThi;
    public string TongDaThanhToanHienThi => ThongKe.TongDaThanhToanHienThi;
    public string TongConNoHienThi => ThongKe.TongConNoHienThi;
    public string TongSoChungTuHienThi => ThongKe.TongSoChungTuHienThi;

    // ============================================================
    // BỘ LỌC
    // ============================================================

    public IReadOnlyList<string> DanhSachKhoangThoiGian { get; } =
        ["Hôm nay", "Hôm qua", "7 ngày qua", "Tháng này", "Tháng trước", "Năm nay", "Toàn bộ", "Tùy chỉnh"];

    [ObservableProperty] private string khoangThoiGian = "Tháng này";
    [ObservableProperty] private bool tuyChinh;
    [ObservableProperty] private DateTime tuNgay;
    [ObservableProperty] private DateTime denNgay;
    [ObservableProperty] private DateTimeOffset? tuNgayChon;
    [ObservableProperty] private DateTimeOffset? denNgayChon;

    public IReadOnlyList<string> DanhSachLoaiDoiTac { get; } =
        ["Tất cả đối tác", "Khách hàng", "Nhà cung cấp"];

    [ObservableProperty] private string selectedLoaiDoiTac = "Tất cả đối tác";

    public IReadOnlyList<string> DanhSachLoaiCongNo { get; } =
        ["Tất cả công nợ", "Phải thu (Khách hàng)", "Phải trả (Nhà cung cấp)"];

    [ObservableProperty] private string selectedLoaiCongNo = "Tất cả công nợ";

    public IReadOnlyList<string> DanhSachTrangThaiThanhToan { get; } =
        ["Tất cả trạng thái", "Còn nợ", "Đã thanh toán đủ"];

    [ObservableProperty] private string selectedTrangThaiThanhToan = "Tất cả trạng thái";

    [ObservableProperty] private string tuKhoa = string.Empty;

    // ============================================================
    // PHÂN TRANG
    // ============================================================

    public IReadOnlyList<int> DanhSachSoDong { get; } = [15, 25, 50, 100];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(ThongTinPhanTrang))]
    private int trangHienTai = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(ThongTinPhanTrang))]
    private int soDongTrenTrang = 25;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TongSoTrang))]
    [NotifyPropertyChangedFor(nameof(CanTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanTrangSau))]
    [NotifyPropertyChangedFor(nameof(ThongTinPhanTrang))]
    private int tongSoBanGhi;

    public int TongSoTrang => SoDongTrenTrang > 0 ? (int)Math.Ceiling(TongSoBanGhi / (double)SoDongTrenTrang) : 1;
    public bool CanTrangTruoc => TrangHienTai > 1;
    public bool CanTrangSau => TrangHienTai < TongSoTrang;

    public string ThongTinPhanTrang
    {
        get
        {
            if (TongSoBanGhi == 0) return "0 / 0";
            var from = (TrangHienTai - 1) * SoDongTrenTrang + 1;
            var to = Math.Min(from + DanhSach.Count - 1, TongSoBanGhi);
            return $"{from:N0} - {to:N0} / {TongSoBanGhi:N0} chứng từ";
        }
    }

    // ============================================================
    // XỬ LÝ SỰ KIỆN THAY ĐỔI BỘ LỌC
    // ============================================================

    partial void OnKhoangThoiGianChanged(string value)
    {
        if (value == "Tùy chỉnh")
        {
            TuyChinh = true;
            return;
        }

        TuyChinh = false;
        var today = DateTime.Today;

        switch (value)
        {
            case "Hôm nay":
                TuNgay = today;
                DenNgay = today;
                break;
            case "Hôm qua":
                TuNgay = today.AddDays(-1);
                DenNgay = today.AddDays(-1);
                break;
            case "7 ngày qua":
                TuNgay = today.AddDays(-6);
                DenNgay = today;
                break;
            case "Tháng này":
                TuNgay = new DateTime(today.Year, today.Month, 1);
                DenNgay = today;
                break;
            case "Tháng trước":
                var thangTruoc = today.AddMonths(-1);
                TuNgay = new DateTime(thangTruoc.Year, thangTruoc.Month, 1);
                DenNgay = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                break;
            case "Năm nay":
                TuNgay = new DateTime(today.Year, 1, 1);
                DenNgay = today;
                break;
            case "Toàn bộ":
                TuNgay = new DateTime(2020, 1, 1);
                DenNgay = today.AddYears(1);
                break;
        }

        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);

        TrangHienTai = 1;
        _ = TaiLaiAsync();
    }

    partial void OnTuNgayChonChanged(DateTimeOffset? value)
    {
        if (value.HasValue && value.Value.DateTime.Date != TuNgay)
        {
            TuNgay = value.Value.DateTime.Date;
            if (TuyChinh)
            {
                TrangHienTai = 1;
                _ = TaiLaiAsync();
            }
        }
    }

    partial void OnDenNgayChonChanged(DateTimeOffset? value)
    {
        if (value.HasValue && value.Value.DateTime.Date != DenNgay)
        {
            DenNgay = value.Value.DateTime.Date;
            if (TuyChinh)
            {
                TrangHienTai = 1;
                _ = TaiLaiAsync();
            }
        }
    }

    partial void OnSelectedLoaiDoiTacChanged(string value)
    {
        TrangHienTai = 1;
        _ = TaiLaiAsync();
    }

    partial void OnSelectedLoaiCongNoChanged(string value)
    {
        TrangHienTai = 1;
        _ = TaiLaiAsync();
    }

    partial void OnSelectedTrangThaiThanhToanChanged(string value)
    {
        TrangHienTai = 1;
        _ = TaiLaiAsync();
    }

    partial void OnSoDongTrenTrangChanged(int value)
    {
        TrangHienTai = 1;
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    private void TimKiem()
    {
        TrangHienTai = 1;
        _ = TaiLaiAsync();
    }

    [RelayCommand]
    private void XoaBoLoc()
    {
        TuKhoa = string.Empty;
        SelectedLoaiDoiTac = "Tất cả đối tác";
        SelectedLoaiCongNo = "Tất cả công nợ";
        SelectedTrangThaiThanhToan = "Tất cả trạng thái";
        KhoangThoiGian = "Tháng này";
    }

    // ============================================================
    // ĐIỀU HƯỚNG TRANG
    // ============================================================

    [RelayCommand]
    private Task DauTrangAsync()
    {
        if (TrangHienTai != 1)
        {
            TrangHienTai = 1;
            return TaiLaiAsync();
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task TrangTruocAsync()
    {
        if (CanTrangTruoc)
        {
            TrangHienTai--;
            return TaiLaiAsync();
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task TrangSauAsync()
    {
        if (CanTrangSau)
        {
            TrangHienTai++;
            return TaiLaiAsync();
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CuoiTrangAsync()
    {
        if (TrangHienTai != TongSoTrang && TongSoTrang > 0)
        {
            TrangHienTai = TongSoTrang;
            return TaiLaiAsync();
        }
        return Task.CompletedTask;
    }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        DangTai = true;
        ThongBaoLoi = string.Empty;

        try
        {
            var filter = new BaoCaoChiTietCongNoPhieuFilter
            {
                TuNgay = TuNgay,
                DenNgay = DenNgay,
                LoaiDoiTac = SelectedLoaiDoiTac switch
                {
                    "Khách hàng" => "KHACH_HANG",
                    "Nhà cung cấp" => "NHA_CUNG_CAP",
                    _ => "TAT_CA"
                },
                LoaiCongNo = SelectedLoaiCongNo switch
                {
                    "Phải thu (Khách hàng)" => "PHAI_THU",
                    "Phải trả (Nhà cung cấp)" => "PHAI_TRA",
                    _ => "TAT_CA"
                },
                TrangThaiThanhToan = SelectedTrangThaiThanhToan switch
                {
                    "Còn nợ" => "CON_NO",
                    "Đã thanh toán đủ" => "DA_THANH_TOAN",
                    _ => "TAT_CA"
                },
                TuKhoa = TuKhoa,
                Page = TrangHienTai,
                PageSize = SoDongTrenTrang
            };

            var (items, totalCount, kpi) = await _service.LayBaoCaoPagedAsync(filter);

            DanhSach.Clear();
            foreach (var item in items)
            {
                DanhSach.Add(item);
            }

            TongSoBanGhi = totalCount;
            ThongKe = kpi;

            OnPropertyChanged(nameof(KhongCoDuLieu));
            OnPropertyChanged(nameof(ThongTinPhanTrang));

            CapNhatTrangThai?.Invoke($"Đã tải {DanhSach.Count} chứng từ công nợ (Tổng: {totalCount:N0}).");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không thể tải chi tiết công nợ: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiLaiAsync BaoCaoChiTietCongNoTheoPhieu", ex);
        }
        finally
        {
            DangTai = false;
            OnPropertyChanged(nameof(KhongCoDuLieu));
        }
    }

    // ============================================================
    // XEM CHI TIẾT CHỨNG TỪ
    // ============================================================

    [RelayCommand]
    private async Task XemChiTietChungTuAsync(BaoCaoChiTietCongNoPhieuItem? item)
    {
        if (item is null)
            return;

        try
        {
            if (item.MaLoaiChungTu == "HOA_DON")
            {
                if (MoChiTietHoaDonAsync != null)
                {
                    await MoChiTietHoaDonAsync(item.MaChungTu);
                }
            }
            else if (item.MaLoaiChungTu == "PHIEU_NHAP")
            {
                if (MoChiTietPhieuNhapAsync != null)
                {
                    await MoChiTietPhieuNhapAsync(item.MaChungTu);
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi XemChiTietChungTuAsync", ex);
        }
    }

    // ============================================================
    // XUẤT EXCEL
    // ============================================================

    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        var window = LayCuaSoChinh?.Invoke();
        if (window is null)
            return;

        try
        {
            var defaultFileName = $"ChiTietCongNoTheoPhieu_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất báo cáo chi tiết công nợ theo phiếu",
                SuggestedFileName = defaultFileName,
                DefaultExtension = "xlsx",
                FileTypeChoices =
                [
                    new FilePickerFileType("Excel Workbook (*.xlsx)") { Patterns = ["*.xlsx"] }
                ]
            });

            if (file is null)
                return;

            DangTai = true;
            var filter = new BaoCaoChiTietCongNoPhieuFilter
            {
                TuNgay = TuNgay,
                DenNgay = DenNgay,
                LoaiDoiTac = SelectedLoaiDoiTac switch
                {
                    "Khách hàng" => "KHACH_HANG",
                    "Nhà cung cấp" => "NHA_CUNG_CAP",
                    _ => "TAT_CA"
                },
                LoaiCongNo = SelectedLoaiCongNo switch
                {
                    "Phải thu (Khách hàng)" => "PHAI_THU",
                    "Phải trả (Nhà cung cấp)" => "PHAI_TRA",
                    _ => "TAT_CA"
                },
                TrangThaiThanhToan = SelectedTrangThaiThanhToan switch
                {
                    "Còn nợ" => "CON_NO",
                    "Đã thanh toán đủ" => "DA_THANH_TOAN",
                    _ => "TAT_CA"
                },
                TuKhoa = TuKhoa
            };

            var count = await _service.XuatExcelAsync(filter, file.Path.LocalPath);
            CapNhatTrangThai?.Invoke($"Đã xuất {count:N0} chứng từ ra file: {Path.GetFileName(file.Path.LocalPath)}");
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Lỗi xuất file Excel: " + ex.Message;
            SafeLog.WriteError("Lỗi XuatExcelAsync BaoCaoChiTietCongNoTheoPhieu", ex);
        }
        finally
        {
            DangTai = false;
        }
    }
}
