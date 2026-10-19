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
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 32: DANH SÁCH PHIẾU NHẬP KHÁC
    // =========================================================

    public bool IsNhapKhacSelected => SelectedMenu == "Nhập khác";

    [ObservableProperty] private bool isThemPhieuNhapKhacVisible;

    public ObservableCollection<PhieuNhapKhac> DsPhieuNhapKhacItems { get; } = new();

    [ObservableProperty] private string dsPhieuNhapKhacSearch = "";
    [ObservableProperty] private DateTime? dsPhieuNhapKhacTuNgay;
    [ObservableProperty] private DateTime? dsPhieuNhapKhacDenNgay;
    [ObservableProperty] private string selectedPhieuNhapKhacLoai = "TAT_CA";
    [ObservableProperty] private string selectedPhieuNhapKhacTrangThai = "TAT_CA";
    [ObservableProperty] private bool isLoadingDsPhieuNhapKhac;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuNhapKhacTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuNhapKhacRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuNhapKhacTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuNhapKhacTrangSau))]
    private int dsPhieuNhapKhacTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuNhapKhacTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuNhapKhacRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuNhapKhacTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuNhapKhacTrangSau))]
    private int dsPhieuNhapKhacPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuNhapKhacTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuNhapKhacRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuNhapKhacTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuNhapKhacTrangSau))]
    private int dsPhieuNhapKhacPageSize = 20;

    public int DsPhieuNhapKhacTotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)DsPhieuNhapKhacTotalCount / Math.Max(1, DsPhieuNhapKhacPageSize)));

    public bool CanDsPhieuNhapKhacTrangTruoc => DsPhieuNhapKhacPage > 1;
    public bool CanDsPhieuNhapKhacTrangSau => DsPhieuNhapKhacPage < DsPhieuNhapKhacTotalPages;

    public string DsPhieuNhapKhacRangeText
    {
        get
        {
            if (DsPhieuNhapKhacTotalCount == 0) return "0 phiếu nhập khác";
            var from = (DsPhieuNhapKhacPage - 1) * DsPhieuNhapKhacPageSize + 1;
            var to = Math.Min(from + DsPhieuNhapKhacPageSize - 1, DsPhieuNhapKhacTotalCount);
            return $"{from} - {to} trong {DsPhieuNhapKhacTotalCount:N0} phiếu";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongPhieuNhapKhacText))]
    private int thongKeTongPhieuNhapKhac;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongGiaTriNhapKhacText))]
    private decimal thongKeTongGiaTriNhapKhac;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoLuongNhapKhacText))]
    private decimal thongKeTongSoLuongNhapKhac;

    public string ThongKeTongPhieuNhapKhacText => $"{ThongKeTongPhieuNhapKhac:N0} phiếu";
    public string ThongKeTongGiaTriNhapKhacText => MoneyInput.Format(ThongKeTongGiaTriNhapKhac) + " đ";
    public string ThongKeTongSoLuongNhapKhacText => $"{ThongKeTongSoLuongNhapKhac:N0} SP";

    public IReadOnlyList<string> DsPhieuNhapKhacLoaiOptions { get; } =
        ["Tất cả loại", "Nhập nội bộ", "Nhập điều chỉnh", "Nhập khuyến mãi", "Nhập thu hồi", "Nhập khác"];

    public string SelectedPhieuNhapKhacLoaiHienThi
    {
        get => SelectedPhieuNhapKhacLoai switch
        {
            "NHAP_NOI_BO" => "Nhập nội bộ",
            "NHAP_DIEU_CHINH" => "Nhập điều chỉnh",
            "NHAP_KHUYEN_MAI" => "Nhập khuyến mãi",
            "NHAP_THU_HOI" => "Nhập thu hồi",
            "NHAP_KHAC" => "Nhập khác",
            _ => "Tất cả loại"
        };
        set
        {
            SelectedPhieuNhapKhacLoai = value switch
            {
                "Nhập nội bộ" => "NHAP_NOI_BO",
                "Nhập điều chỉnh" => "NHAP_DIEU_CHINH",
                "Nhập khuyến mãi" => "NHAP_KHUYEN_MAI",
                "Nhập thu hồi" => "NHAP_THU_HOI",
                "Nhập khác" => "NHAP_KHAC",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuNhapKhacAsync();
        }
    }

    public IReadOnlyList<string> DsPhieuNhapKhacTrangThaiOptions { get; } =
        ["Tất cả trạng thái", "Hoàn thành", "Đã hủy"];

    public string SelectedPhieuNhapKhacTrangThaiHienThi
    {
        get => SelectedPhieuNhapKhacTrangThai switch
        {
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            _ => "Tất cả trạng thái"
        };
        set
        {
            SelectedPhieuNhapKhacTrangThai = value switch
            {
                "Hoàn thành" => "HOAN_THANH",
                "Đã hủy" => "DA_HUY",
                _ => "TAT_CA"
            };
            OnPropertyChanged();
            _ = TimKiemDsPhieuNhapKhacAsync();
        }
    }

    /// <summary>
    /// Mở màn hình "Nghiệp vụ → Nhập khác → Danh sách phiếu nhập khác".
    /// </summary>
    [RelayCommand]
    public async Task MoGiaoDienNhapKhacAsync()
    {
        IsSalesPanelVisible = false;
        SelectedMenu = "Nhập khác";
        IsThemPhieuNhapKhacVisible = false;
        DsPhieuNhapKhacPage = 1;
        await LoadDsPhieuNhapKhacAsync();
    }

    /// <summary>
    /// Tải dữ liệu danh sách phiếu nhập khác và thống kê theo bộ lọc hiện tại.
    /// </summary>
    [RelayCommand]
    public async Task LoadDsPhieuNhapKhacAsync()
    {
        if (IsLoadingDsPhieuNhapKhac)
            return;

        try
        {
            IsLoadingDsPhieuNhapKhac = true;
            StatusMessage = "Đang tải danh sách phiếu nhập khác...";

            var (items, totalCount) = await _db.GetPhieuNhapKhacPagedAsync(
                DsPhieuNhapKhacSearch,
                DsPhieuNhapKhacTuNgay,
                DsPhieuNhapKhacDenNgay,
                SelectedPhieuNhapKhacLoai,
                SelectedPhieuNhapKhacTrangThai,
                DsPhieuNhapKhacPage,
                DsPhieuNhapKhacPageSize);

            DsPhieuNhapKhacItems.Clear();
            foreach (var item in items)
            {
                DsPhieuNhapKhacItems.Add(item);
            }

            DsPhieuNhapKhacTotalCount = totalCount;

            var (tongPhieu, tongGiaTri, tongSoLuong) = await _db.GetThongKePhieuNhapKhacAsync(
                DsPhieuNhapKhacSearch,
                DsPhieuNhapKhacTuNgay,
                DsPhieuNhapKhacDenNgay,
                SelectedPhieuNhapKhacLoai,
                SelectedPhieuNhapKhacTrangThai);

            ThongKeTongPhieuNhapKhac = tongPhieu;
            ThongKeTongGiaTriNhapKhac = tongGiaTri;
            ThongKeTongSoLuongNhapKhac = tongSoLuong;

            StatusMessage = $"Đã tải {DsPhieuNhapKhacItems.Count:N0} phiếu nhập khác.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách phiếu nhập khác", ex);
            StatusMessage = "Không thể tải danh sách phiếu nhập khác: " + ex.Message;
        }
        finally
        {
            IsLoadingDsPhieuNhapKhac = false;
        }
    }

    [RelayCommand]
    public Task TimKiemDsPhieuNhapKhacAsync()
    {
        DsPhieuNhapKhacPage = 1;
        return LoadDsPhieuNhapKhacAsync();
    }

    [RelayCommand]
    public async Task LamMoiDsPhieuNhapKhacAsync()
    {
        DsPhieuNhapKhacSearch = "";
        DsPhieuNhapKhacTuNgay = null;
        DsPhieuNhapKhacDenNgay = null;
        SelectedPhieuNhapKhacLoai = "TAT_CA";
        SelectedPhieuNhapKhacTrangThai = "TAT_CA";
        OnPropertyChanged(nameof(SelectedPhieuNhapKhacLoaiHienThi));
        OnPropertyChanged(nameof(SelectedPhieuNhapKhacTrangThaiHienThi));
        DsPhieuNhapKhacPage = 1;
        await LoadDsPhieuNhapKhacAsync();
    }

    [RelayCommand]
    public async Task DsPhieuNhapKhacTrangTruocAsync()
    {
        if (CanDsPhieuNhapKhacTrangTruoc)
        {
            DsPhieuNhapKhacPage--;
            await LoadDsPhieuNhapKhacAsync();
        }
    }

    [RelayCommand]
    public async Task DsPhieuNhapKhacTrangSauAsync()
    {
        if (CanDsPhieuNhapKhacTrangSau)
        {
            DsPhieuNhapKhacPage++;
            await LoadDsPhieuNhapKhacAsync();
        }
    }

    /// <summary>
    /// Mở xem chi tiết một phiếu nhập khác.
    /// </summary>
    [RelayCommand]
    public async Task XemChiTietPhieuNhapKhacAsync(PhieuNhapKhac? phieu)
    {
        if (phieu is null || OwnerWindow is null)
            return;

        try
        {
            var chiTiets = await _db.GetChiTietPhieuNhapKhacAsync(phieu.Id);
            var dongHang = string.Join("\n", chiTiets.Select(c =>
                $"• {c.TenSanPham} ({c.MaSanPham}): {c.SoLuong:N0} {c.DonViTinh} × {c.DonGia:N0} đ = {c.ThanhTien:N0} đ" +
                (!string.IsNullOrWhiteSpace(c.GhiChu) ? $" [Ghi chú: {c.GhiChu}]" : "")));

            var thongBao =
                $"MÃ PHIẾU NHẬP KHÁC: {phieu.MaPhieuNhapKhac}\n" +
                $"Thời gian nhập: {phieu.ThoiGianNhapText}\n" +
                $"Loại phiếu: {phieu.LoaiNhapKhacText}\n" +
                $"Kho nhận hàng: {phieu.KhoNhan}\n" +
                $"Đối tượng / Bộ phận liên quan: {phieu.DoiTuongHienThi}\n" +
                $"Trạng thái: {phieu.TrangThaiText}\n" +
                $"Người lập: {phieu.TenNguoiTao ?? "Nhân viên"}\n" +
                $"Ghi chú: {phieu.GhiChu ?? "Không có"}\n\n" +
                $"DANH SÁCH MẶT HÀNG NHẬP ({chiTiets.Count} mặt hàng):\n" +
                (string.IsNullOrWhiteSpace(dongHang) ? "Chưa có chi tiết mặt hàng." : dongHang) + "\n\n" +
                $"TỔNG SỐ LƯỢNG: {phieu.TongSoLuongText}\n" +
                $"TỔNG GIÁ TRỊ: {phieu.TongGiaTriText}";

            await ShowMessageAsync($"Chi tiết phiếu nhập khác {phieu.MaPhieuNhapKhac}", thongBao);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xem chi tiết phiếu nhập khác", ex);
            await ShowMessageAsync("Lỗi", "Không thể xem chi tiết: " + ex.Message);
        }
    }

    /// <summary>
    /// In phiếu nhập khác.
    /// </summary>
    [RelayCommand]
    public async Task InPhieuNhapKhacAsync(PhieuNhapKhac? phieu)
    {
        if (phieu is null)
            return;

        await ShowMessageAsync("In phiếu nhập khác",
            $"Chức năng in phiếu nhập khác {phieu.MaPhieuNhapKhac} đã sẵn sàng.\nLoại phiếu: {phieu.LoaiNhapKhacText} • Tổng giá trị: {phieu.TongGiaTriText}");
    }

    /// <summary>
    /// Xuất danh sách phiếu nhập khác ra file Excel.
    /// </summary>
    [RelayCommand]
    public async Task XuatExcelDsPhieuNhapKhacAsync()
    {
        if (OwnerWindow is null || DsPhieuNhapKhacItems.Count == 0)
        {
            await ShowMessageAsync("Thông báo", "Không có dữ liệu phiếu nhập khác để xuất file.");
            return;
        }

        try
        {
            var saveFile = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Xuất danh sách phiếu nhập khác",
                SuggestedFileName = $"DanhSachPhieuNhapKhac_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExtension = "xlsx"
            });

            if (saveFile is null)
                return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PhieuNhapKhac");

            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã phiếu nhập khác";
            ws.Cell(1, 3).Value = "Thời gian";
            ws.Cell(1, 4).Value = "Loại phiếu";
            ws.Cell(1, 5).Value = "Kho nhận";
            ws.Cell(1, 6).Value = "Đối tượng liên quan";
            ws.Cell(1, 7).Value = "Tổng số lượng";
            ws.Cell(1, 8).Value = "Tổng giá trị";
            ws.Cell(1, 9).Value = "Trạng thái";
            ws.Cell(1, 10).Value = "Ghi chú";

            var row = 2;
            foreach (var item in DsPhieuNhapKhacItems)
            {
                ws.Cell(row, 1).Value = item.SoThuTu;
                ws.Cell(row, 2).Value = item.MaPhieuNhapKhac;
                ws.Cell(row, 3).Value = item.ThoiGianNhapText;
                ws.Cell(row, 4).Value = item.LoaiNhapKhacText;
                ws.Cell(row, 5).Value = item.KhoNhan;
                ws.Cell(row, 6).Value = item.DoiTuongHienThi;
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
            await ShowMessageAsync("Thành công", "Đã xuất danh sách phiếu nhập khác ra file Excel thành công!");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất Excel phiếu nhập khác", ex);
            await ShowMessageAsync("Lỗi", "Không thể xuất file Excel: " + ex.Message);
        }
    }

    // =========================================================
    // GIAO DIỆN 33: THÊM PHIẾU NHẬP KHÁC
    // =========================================================

    public ObservableCollection<ChiTietPhieuNhapKhac> PhieuNhapKhacChiTietItems { get; } = new();
    public ObservableCollection<SanPham> PhieuNhapKhacProductSuggestions { get; } = new();

    public IReadOnlyList<string> PhieuNhapKhacLoaiFormOptions { get; } =
        ["Nhập nội bộ", "Nhập điều chỉnh", "Nhập khuyến mãi", "Nhập thu hồi", "Nhập khác"];

    [ObservableProperty] private string phieuNhapKhacMa = "";
    [ObservableProperty] private DateTime? phieuNhapKhacNgayNhap = DateTime.Now;
    [ObservableProperty] private string phieuNhapKhacLoai = "NHAP_NOI_BO";
    [ObservableProperty] private string phieuNhapKhacKhoNhan = "Kho Tổng (Kho mặc định)";
    [ObservableProperty] private string phieuNhapKhacDoiTuong = "";
    [ObservableProperty] private string phieuNhapKhacNhanVien = "";
    [ObservableProperty] private string phieuNhapKhacGhiChu = "";
    [ObservableProperty] private string phieuNhapKhacProductSearch = "";
    [ObservableProperty] private bool isSavingPhieuNhapKhac;

    public string PhieuNhapKhacNgayNhapText =>
        PhieuNhapKhacNgayNhap?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "";

    public string SelectedPhieuNhapKhacLoaiFormHienThi
    {
        get => PhieuNhapKhacLoai switch
        {
            "NHAP_NOI_BO" => "Nhập nội bộ",
            "NHAP_DIEU_CHINH" => "Nhập điều chỉnh",
            "NHAP_KHUYEN_MAI" => "Nhập khuyến mãi",
            "NHAP_THU_HOI" => "Nhập thu hồi",
            _ => "Nhập khác"
        };
        set
        {
            PhieuNhapKhacLoai = value switch
            {
                "Nhập nội bộ" => "NHAP_NOI_BO",
                "Nhập điều chỉnh" => "NHAP_DIEU_CHINH",
                "Nhập khuyến mãi" => "NHAP_KHUYEN_MAI",
                "Nhập thu hồi" => "NHAP_THU_HOI",
                _ => "NHAP_KHAC"
            };
            OnPropertyChanged();
            OnPropertyChanged(nameof(PhieuNhapKhacLoaiGiaiThich));
        }
    }

    public string PhieuNhapKhacLoaiGiaiThich => PhieuNhapKhacLoai switch
    {
        "NHAP_NOI_BO" => "💡 Điều chuyển hàng hóa, nguyên vật liệu từ chi nhánh hoặc bộ phận khác về kho",
        "NHAP_DIEU_CHINH" => "💡 Cân bằng số liệu thực tế sau kiểm kê định kỳ hoặc bù chênh lệch kiểm kê thừa",
        "NHAP_KHUYEN_MAI" => "💡 Hàng tặng kèm, mẫu dùng thử hoặc quà biếu từ nhà cung cấp / đối tác (đơn giá 0đ)",
        "NHAP_THU_HOI" => "💡 Thu hồi giỏ hoa, bình gốm, kệ hoa và vật tư còn dùng tốt sau khi kết thúc sự kiện",
        _ => "💡 Nhập hàng hóa vào kho từ các nguồn phát sinh nghiệp vụ khác"
    };

    public int PhieuNhapKhacTongDong => PhieuNhapKhacChiTietItems.Count;

    public decimal PhieuNhapKhacTongSoLuong =>
        PhieuNhapKhacChiTietItems.Sum(c => c.SoLuong);

    public decimal PhieuNhapKhacTongGiaTri =>
        PhieuNhapKhacChiTietItems.Sum(c => c.ThanhTien);

    public string PhieuNhapKhacTongDongText => $"{PhieuNhapKhacTongDong:N0} dòng";
    public string PhieuNhapKhacTongSoLuongText => $"{PhieuNhapKhacTongSoLuong:N0} SP";
    public string PhieuNhapKhacTongGiaTriText => MoneyInput.Format(PhieuNhapKhacTongGiaTri) + " đ";

    public bool HasPhieuNhapKhacSuggestions => PhieuNhapKhacProductSuggestions.Count > 0;
    public bool HasPhieuNhapKhacItems => PhieuNhapKhacChiTietItems.Count > 0;
    public bool IsPhieuNhapKhacEmpty => PhieuNhapKhacChiTietItems.Count == 0;

    /// <summary>
    /// Mở màn hình Thêm phiếu nhập khác mới.
    /// </summary>
    [RelayCommand]
    public async Task MoFormThemPhieuNhapKhacAsync()
    {
        IsSalesPanelVisible = false;
        SelectedMenu = "Nhập khác";
        IsThemPhieuNhapKhacVisible = true;
        await LamMoiFormThemPhieuNhapKhacAsync();
    }

    /// <summary>
    /// Tương thích nút mở thêm phiếu từ danh sách.
    /// </summary>
    [RelayCommand]
    public Task TaoPhieuNhapKhacMoiAsync() => MoFormThemPhieuNhapKhacAsync();

    /// <summary>
    /// Đóng form Thêm phiếu nhập khác và quay về danh sách.
    /// </summary>
    [RelayCommand]
    public void DongFormThemPhieuNhapKhac()
    {
        IsThemPhieuNhapKhacVisible = false;
    }

    /// <summary>
    /// Khởi tạo lại toàn bộ dữ liệu trên form Thêm phiếu nhập khác.
    /// </summary>
    [RelayCommand]
    public async Task LamMoiFormThemPhieuNhapKhacAsync()
    {
        PhieuNhapKhacMa = await _db.TaoMaPhieuNhapKhacMoiAsync();
        PhieuNhapKhacNgayNhap = DateTime.Now;
        PhieuNhapKhacLoai = "NHAP_NOI_BO";
        OnPropertyChanged(nameof(SelectedPhieuNhapKhacLoaiFormHienThi));
        OnPropertyChanged(nameof(PhieuNhapKhacLoaiGiaiThich));

        PhieuNhapKhacKhoNhan = "Kho Tổng (Kho mặc định)";
        PhieuNhapKhacDoiTuong = "";
        PhieuNhapKhacNhanVien = Session.CurrentUser?.HoTen ?? "Quản trị viên";
        PhieuNhapKhacGhiChu = "";
        PhieuNhapKhacProductSearch = "";

        BoDangKyChiTietPhieuNhapKhac();
        PhieuNhapKhacChiTietItems.Clear();
        PhieuNhapKhacProductSuggestions.Clear();

        CapNhatTongKetPhieuNhapKhac();
        StatusMessage = $"Đã khởi tạo phiếu nhập khác {PhieuNhapKhacMa}.";
    }

    private void DangKyChiTietPhieuNhapKhac()
    {
        foreach (var item in PhieuNhapKhacChiTietItems)
        {
            item.PropertyChanged -= ChiTietPhieuNhapKhac_PropertyChanged;
            item.PropertyChanged += ChiTietPhieuNhapKhac_PropertyChanged;
        }
    }

    private void BoDangKyChiTietPhieuNhapKhac()
    {
        foreach (var item in PhieuNhapKhacChiTietItems)
        {
            item.PropertyChanged -= ChiTietPhieuNhapKhac_PropertyChanged;
        }
    }

    private void ChiTietPhieuNhapKhac_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChiTietPhieuNhapKhac.SoLuong)
            or nameof(ChiTietPhieuNhapKhac.DonGia)
            or nameof(ChiTietPhieuNhapKhac.ThanhTien))
        {
            CapNhatTongKetPhieuNhapKhac();
        }
    }

    private void CapNhatTongKetPhieuNhapKhac()
    {
        OnPropertyChanged(nameof(PhieuNhapKhacTongDong));
        OnPropertyChanged(nameof(PhieuNhapKhacTongSoLuong));
        OnPropertyChanged(nameof(PhieuNhapKhacTongGiaTri));
        OnPropertyChanged(nameof(PhieuNhapKhacTongDongText));
        OnPropertyChanged(nameof(PhieuNhapKhacTongSoLuongText));
        OnPropertyChanged(nameof(PhieuNhapKhacTongGiaTriText));
        OnPropertyChanged(nameof(HasPhieuNhapKhacItems));
        OnPropertyChanged(nameof(IsPhieuNhapKhacEmpty));
        OnPropertyChanged(nameof(HasPhieuNhapKhacSuggestions));
    }

    /// <summary>
    /// Tìm kiếm hàng hóa gợi ý để thêm vào phiếu nhập khác.
    /// </summary>
    [RelayCommand]
    public async Task TimKiemSanPhamChoNhapKhacAsync(string? keyword)
    {
        var kw = keyword ?? PhieuNhapKhacProductSearch ?? "";
        PhieuNhapKhacProductSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(kw))
        {
            OnPropertyChanged(nameof(HasPhieuNhapKhacSuggestions));
            return;
        }

        try
        {
            var products = await _db.GetSanPhamAsync(kw.Trim());
            foreach (var p in products.Take(15))
            {
                PhieuNhapKhacProductSuggestions.Add(p);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tìm kiếm sản phẩm cho phiếu nhập khác", ex);
        }

        OnPropertyChanged(nameof(HasPhieuNhapKhacSuggestions));
    }

    /// <summary>
    /// Chọn sản phẩm từ danh sách gợi ý để đưa vào bảng chi tiết.
    /// </summary>
    [RelayCommand]
    public void ChonSanPhamNhapKhac(SanPham? sp)
    {
        if (sp is null)
            return;

        ThemSanPhamVaoPhieuNhapKhac(sp);
        PhieuNhapKhacProductSearch = "";
        PhieuNhapKhacProductSuggestions.Clear();
        OnPropertyChanged(nameof(HasPhieuNhapKhacSuggestions));
    }

    /// <summary>
    /// Đưa mặt hàng vào bảng chi tiết phiếu nhập khác.
    /// </summary>
    public void ThemSanPhamVaoPhieuNhapKhac(SanPham sp)
    {
        var daCo = PhieuNhapKhacChiTietItems.FirstOrDefault(c => c.SanPhamId == sp.Id);
        if (daCo is not null)
        {
            daCo.SoLuong += 1;
            CapNhatTongKetPhieuNhapKhac();
            return;
        }

        decimal donGia = 0;
        if (PhieuNhapKhacLoai == "NHAP_KHUYEN_MAI")
        {
            donGia = 0;
        }
        else if (sp.GiaVon > 0)
        {
            donGia = sp.GiaVon;
        }
        else if (sp.GiaBan > 0)
        {
            donGia = sp.GiaBan;
        }

        var ct = new ChiTietPhieuNhapKhac(sp, donGia: donGia, soLuong: 1);
        ct.PropertyChanged += ChiTietPhieuNhapKhac_PropertyChanged;
        PhieuNhapKhacChiTietItems.Add(ct);

        CapNhatSoThuTuNhapKhac();
        CapNhatTongKetPhieuNhapKhac();
    }

    /// <summary>
    /// Tăng số lượng dòng hàng thêm 1 đơn vị.
    /// </summary>
    [RelayCommand]
    public void TangSoLuongNhapKhac(ChiTietPhieuNhapKhac? item)
    {
        if (item is null) return;
        item.SoLuong += 1;
        CapNhatTongKetPhieuNhapKhac();
    }

    /// <summary>
    /// Giảm số lượng dòng hàng đi 1 đơn vị. Nếu về 0 thì tự xóa dòng.
    /// </summary>
    [RelayCommand]
    public void GiamSoLuongNhapKhac(ChiTietPhieuNhapKhac? item)
    {
        if (item is null) return;
        if (item.SoLuong > 1)
        {
            item.SoLuong -= 1;
        }
        else
        {
            XoaDongPhieuNhapKhac(item);
        }
        CapNhatTongKetPhieuNhapKhac();
    }

    /// <summary>
    /// Xóa một dòng hàng khỏi phiếu nhập khác.
    /// </summary>
    [RelayCommand]
    public void XoaDongPhieuNhapKhac(ChiTietPhieuNhapKhac? item)
    {
        if (item is null) return;
        item.PropertyChanged -= ChiTietPhieuNhapKhac_PropertyChanged;
        PhieuNhapKhacChiTietItems.Remove(item);
        CapNhatSoThuTuNhapKhac();
        CapNhatTongKetPhieuNhapKhac();
    }

    /// <summary>
    /// Xóa tất cả các dòng hàng đã thêm.
    /// </summary>
    [RelayCommand]
    public void XoaTatCaDongPhieuNhapKhac()
    {
        BoDangKyChiTietPhieuNhapKhac();
        PhieuNhapKhacChiTietItems.Clear();
        CapNhatTongKetPhieuNhapKhac();
    }

    private void CapNhatSoThuTuNhapKhac()
    {
        for (var i = 0; i < PhieuNhapKhacChiTietItems.Count; i++)
        {
            PhieuNhapKhacChiTietItems[i].SoThuTu = i + 1;
        }
    }

    /// <summary>
    /// Lưu phiếu nhập khác vào database: tạo phiếu, tạo chi tiết và tăng tồn kho.
    /// </summary>
    [RelayCommand]
    public async Task LuuPhieuNhapKhacAsync()
    {
        if (IsSavingPhieuNhapKhac)
            return;

        if (PhieuNhapKhacChiTietItems.Count == 0)
        {
            await ShowMessageAsync("Chưa có mặt hàng", "Vui lòng chọn ít nhất một mặt hàng để tạo phiếu nhập khác.");
            return;
        }

        foreach (var item in PhieuNhapKhacChiTietItems)
        {
            if (item.SoLuong <= 0)
            {
                await ShowMessageAsync("Số lượng không hợp lệ", $"Mặt hàng '{item.TenSanPham}' có số lượng phải lớn hơn 0.");
                return;
            }
            if (item.DonGia < 0)
            {
                await ShowMessageAsync("Đơn giá không hợp lệ", $"Mặt hàng '{item.TenSanPham}' có đơn giá không được âm.");
                return;
            }
        }

        try
        {
            IsSavingPhieuNhapKhac = true;
            StatusMessage = "Đang lưu phiếu nhập khác...";

            var phieu = new PhieuNhapKhac
            {
                MaPhieuNhapKhac = PhieuNhapKhacMa?.Trim() ?? "",
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1,
                LoaiNhapKhac = PhieuNhapKhacLoai,
                ThoiGianNhap = PhieuNhapKhacNgayNhap ?? DateTime.Now,
                KhoNhan = string.IsNullOrWhiteSpace(PhieuNhapKhacKhoNhan) ? "Kho Tổng (Kho mặc định)" : PhieuNhapKhacKhoNhan.Trim(),
                DoiTuongLienQuan = PhieuNhapKhacDoiTuong?.Trim(),
                TongSoLuong = PhieuNhapKhacTongSoLuong,
                TongGiaTri = PhieuNhapKhacTongGiaTri,
                GhiChu = PhieuNhapKhacGhiChu?.Trim(),
                TrangThai = "HOAN_THANH",
                NguoiTaoId = Session.CurrentUser?.Id,
                TenNguoiTao = Session.CurrentUser?.HoTen ?? "Quản trị viên"
            };

            var chiTiets = PhieuNhapKhacChiTietItems.ToList();
            var (success, message, maPhieu, _) = await _db.LuuPhieuNhapKhacAsync(phieu, chiTiets);

            if (!success)
            {
                await ShowMessageAsync("Lưu phiếu thất bại", message);
                return;
            }

            StatusMessage = $"Đã tạo thành công phiếu nhập khác {maPhieu}!";
            await ShowMessageAsync("Thành công", $"Đã lưu thành công phiếu nhập khác {maPhieu}!\nSố lượng mặt hàng: {chiTiets.Count:N0}\nTổng số lượng: {PhieuNhapKhacTongSoLuongText}\nTổng giá trị: {PhieuNhapKhacTongGiaTriText}\nTồn kho các sản phẩm liên quan đã được tự động cộng thêm.");

            // Làm mới danh sách phiếu nhập khác
            await LamMoiDsPhieuNhapKhacAsync();
            IsThemPhieuNhapKhacVisible = false;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi LuuPhieuNhapKhacAsync", ex);
            await ShowMessageAsync("Lỗi", "Không thể lưu phiếu nhập khác: " + ex.Message);
        }
        finally
        {
            IsSavingPhieuNhapKhac = false;
        }
    }
}
