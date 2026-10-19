using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 41/42: DANH SÁCH PHIẾU CHI + THÊM PHIẾU CHI
    //
    // Dữ liệu lấy đúng từ bảng phieu_chi (bằng SoQuyBoLoc.LoaiChungTu =
    // ChiPhieuChi) nên bảng, phân trang và 3 thẻ thống kê luôn chạy xuống
    // SQL thay vì lọc toàn bộ bảng trên UI.
    //
    // Form thêm phiếu chi không có logic riêng: nó bind thẳng vào các ô của
    // form Sổ quỹ (SoQuyPhieu*) và gọi lại LuuSoQuyPhieuAsync(false) để giữ
    // nguyên toàn bộ validation + ghi database đã có.
    // =========================================================

    public bool IsPhieuChiSelected => SelectedMenu == "Phiếu chi";

    /// <summary>true = đang hiển thị form Thêm phiếu chi thay vì bảng danh sách.</summary>
    [ObservableProperty] private bool isThemPhieuChiVisible;

    // ----- Bộ lọc / bảng -----

    public ObservableCollection<SoQuyGiaoDich> DsPhieuChiItems { get; } = new();

    [ObservableProperty] private string dsPhieuChiSearch = "";
    [ObservableProperty] private DateTime? dsPhieuChiTuNgay;
    [ObservableProperty] private DateTime? dsPhieuChiDenNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiEmptyStateText))]
    private bool isLoadingDsPhieuChi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuChiTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuChiTrangSau))]
    private int dsPhieuChiTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuChiTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuChiTrangSau))]
    private int dsPhieuChiPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiTotalPages))]
    [NotifyPropertyChangedFor(nameof(DsPhieuChiRangeText))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuChiTrangTruoc))]
    [NotifyPropertyChangedFor(nameof(CanDsPhieuChiTrangSau))]
    private int dsPhieuChiPageSize = 20;

    public int DsPhieuChiTotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)DsPhieuChiTotalCount / Math.Max(1, DsPhieuChiPageSize)));

    public bool CanDsPhieuChiTrangTruoc => DsPhieuChiPage > 1;
    public bool CanDsPhieuChiTrangSau => DsPhieuChiPage < DsPhieuChiTotalPages;

    public string DsPhieuChiRangeText
    {
        get
        {
            if (DsPhieuChiTotalCount == 0)
                return "0 phiếu chi";

            var from = (DsPhieuChiPage - 1) * DsPhieuChiPageSize + 1;
            var to = Math.Min(from + DsPhieuChiPageSize - 1, DsPhieuChiTotalCount);

            return $"{from} - {to} trong {DsPhieuChiTotalCount:N0} phiếu";
        }
    }

    public string DsPhieuChiEmptyStateText => IsLoadingDsPhieuChi
        ? "Đang tải danh sách phiếu chi..."
        : "Chưa có phiếu chi nào khớp bộ lọc.";

    // ----- 3 thẻ thống kê -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongSoPhieuChiText))]
    private int thongKeTongSoPhieuChi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeTongTienChiText))]
    private decimal thongKeTongTienChi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThongKeSoPhieuDaHuyText))]
    private int thongKeSoPhieuDaHuy;

    public string ThongKeTongSoPhieuChiText => $"{ThongKeTongSoPhieuChi:N0} phiếu";
    public string ThongKeTongTienChiText => MoneyInput.Format(ThongKeTongTienChi) + " đ";
    public string ThongKeSoPhieuDaHuyText => $"{ThongKeSoPhieuDaHuy:N0} phiếu";

    // =========================================================
    // MỞ MÀN / NẠP DỮ LIỆU
    // =========================================================

    /// <summary>
    /// Mở màn "Nghiệp vụ → Phiếu chi". Nếu SelectedMenu đã trùng giá trị thì
    /// setter sẽ không gọi lại LoadSelectedSectionAsync nên phải tự nạp dữ liệu.
    /// </summary>
    [RelayCommand]
    public async Task MoGiaoDienPhieuChiAsync()
    {
        IsSalesPanelVisible = false;
        SelectedMenu = "Phiếu chi";
        IsThemPhieuChiVisible = false;
        DsPhieuChiPage = 1;

        await LoadDsPhieuChiAsync();
    }

    /// <summary>Nạp danh sách phiếu chi + 3 thẻ thống kê theo bộ lọc hiện tại.</summary>
    [RelayCommand]
    public async Task LoadDsPhieuChiAsync()
    {
        if (IsLoadingDsPhieuChi)
            return;

        try
        {
            var boLoc = await TaoBoLocPhieuChiAsync();

            if (boLoc is null)
                return;

            IsLoadingDsPhieuChi = true;
            StatusMessage = "Đang tải danh sách phiếu chi...";

            var (items, tongSoBanGhi) = await _db.GetSoQuyPhieuPagedAsync(
                boLoc, DsPhieuChiPage, DsPhieuChiPageSize);

            // Dữ liệu đổi (đã hủy/thêm mới ở màn khác) làm trang hiện tại vượt
            // tổng số trang thì lùi về trang cuối rồi nạp lại đúng trang đó.
            var tongTrang = Math.Max(
                1, (int)Math.Ceiling((double)tongSoBanGhi / Math.Max(1, DsPhieuChiPageSize)));

            if (DsPhieuChiPage > tongTrang)
            {
                DsPhieuChiPage = tongTrang;

                (items, tongSoBanGhi) = await _db.GetSoQuyPhieuPagedAsync(
                    boLoc, DsPhieuChiPage, DsPhieuChiPageSize);
            }

            DsPhieuChiItems.Clear();

            foreach (var item in items)
                DsPhieuChiItems.Add(item);

            DsPhieuChiTotalCount = tongSoBanGhi;

            var (tongPhieu, tongTienChi, soDaHuy) =
                await _db.GetSoQuyPhieuChiThongKeAsync(boLoc);

            ThongKeTongSoPhieuChi = tongPhieu;
            ThongKeTongTienChi = tongTienChi;
            ThongKeSoPhieuDaHuy = soDaHuy;

            OnPropertyChanged(nameof(DsPhieuChiEmptyStateText));

            StatusMessage = $"Đã tải {DsPhieuChiItems.Count:N0} phiếu chi.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách phiếu chi", ex);
            StatusMessage = "Không thể tải danh sách phiếu chi: " + ex.Message;
        }
        finally
        {
            IsLoadingDsPhieuChi = false;
        }
    }

    /// <summary>Áp ô tìm kiếm vào bộ lọc và quay về trang đầu.</summary>
    [RelayCommand]
    public Task TimKiemDsPhieuChiAsync()
    {
        DsPhieuChiPage = 1;
        return LoadDsPhieuChiAsync();
    }

    /// <summary>Làm mới dữ liệu nhưng giữ nguyên bộ lọc đang xem.</summary>
    [RelayCommand]
    public async Task LamMoiDsPhieuChiAsync()
    {
        await LoadDsPhieuChiAsync();
    }

    /// <summary>Xóa sạch bộ lọc (tìm kiếm + khoảng ngày) và quay về trang đầu.</summary>
    [RelayCommand]
    public async Task XoaBoLocDsPhieuChiAsync()
    {
        DsPhieuChiSearch = "";
        DsPhieuChiTuNgay = null;
        DsPhieuChiDenNgay = null;
        DsPhieuChiPage = 1;

        await LoadDsPhieuChiAsync();

        StatusMessage = "Đã xóa bộ lọc phiếu chi.";
    }

    [RelayCommand]
    public async Task DsPhieuChiTrangTruocAsync()
    {
        if (!CanDsPhieuChiTrangTruoc)
            return;

        DsPhieuChiPage--;
        await LoadDsPhieuChiAsync();
    }

    [RelayCommand]
    public async Task DsPhieuChiTrangSauAsync()
    {
        if (!CanDsPhieuChiTrangSau)
            return;

        DsPhieuChiPage++;
        await LoadDsPhieuChiAsync();
    }

    /// <summary>
    /// Nạp lại phiếu chi khi màn này đang mở. Các thao tác ghi ở Sổ quỹ
    /// (lưu/sửa/hủy phiếu) gọi hàm này để bảng và thẻ thống kê không bị lệch.
    /// </summary>
    public async Task TaiLaiDsPhieuChiNeuDangMoAsync()
    {
        if (IsPhieuChiSelected)
            await LoadDsPhieuChiAsync();
    }

    /// <summary>
    /// Dựng bộ lọc cho màn phiếu chi: chỉ phiếu chi (tiền tố PC), khoảng ngày
    /// validate trước khi gửi xuống SQL (khác Sổ quỹ là tự hoán đổi ngày).
    /// Trả về null nghĩa là bộ lọc không hợp lệ và người dùng đã được báo.
    /// </summary>
    private async Task<SoQuyBoLoc?> TaoBoLocPhieuChiAsync()
    {
        if (DsPhieuChiTuNgay is { } tu && DsPhieuChiDenNgay is { } den && tu.Date > den.Date)
        {
            StatusMessage = "Không thể lọc: Từ ngày không được lớn hơn Đến ngày.";

            await ShowMessageAsync(
                "Khoảng thời gian không hợp lệ",
                "Ngày bắt đầu (Từ ngày) không được lớn hơn ngày kết thúc (Đến ngày).");

            return null;
        }

        return new SoQuyBoLoc
        {
            LoaiChungTu = SoQuyBoLoc.ChiPhieuChi,
            TuNgay = DsPhieuChiTuNgay?.Date,
            DenNgay = DsPhieuChiDenNgay?.Date.AddDays(1),
            TimKiem = (DsPhieuChiSearch ?? "").Trim()
        };
    }

    // =========================================================
    // THÊM PHIẾU CHI (GIAO DIỆN 42)
    // =========================================================

    /// <summary>
    /// Mở form Thêm phiếu chi: sinh mã PC mới, nạp danh mục loại chi/quỹ và
    /// hiện form. Toàn bộ dữ liệu form dùng chung state của Sổ quỹ.
    /// </summary>
    [RelayCommand]
    private async Task MoFormThemPhieuChiAsync()
    {
        if (!await KiemTraQuyenAsync(
                PermissionKeys.CashBookCreatePayment, "Bạn không có quyền tạo phiếu chi."))
        {
            return;
        }

        await MoSoQuyPhieuFormAsync("PC");

        IsThemPhieuChiVisible = true;
    }

    /// <summary>Quay lại bảng danh sách, giữ nguyên dữ liệu đã nhập trong form.</summary>
    [RelayCommand]
    private void DongFormThemPhieuChi() => IsThemPhieuChiVisible = false;

    /// <summary>Làm mới form: sinh mã PC mới và xóa các ô đã nhập.</summary>
    [RelayCommand]
    private async Task LamMoiFormThemPhieuChiAsync() => await MoSoQuyPhieuFormAsync("PC");

    /// <summary>
    /// Lưu phiếu chi (dùng chung LuuSoQuyPhieuAsync với Sổ quỹ). Lưu thành công
    /// thì tự đóng form; bảng danh sách đã được nạp lại ngay trong luồng lưu,
    /// thất bại thì form giữ nguyên để sửa tiếp và lỗi đã được ghi vào
    /// SoQuyHienLoi*/StatusMessage.
    /// </summary>
    [RelayCommand]
    private async Task LuuThemPhieuChiAsync()
    {
        var daLuu = await LuuSoQuyPhieuAsync(false);

        if (daLuu)
            IsThemPhieuChiVisible = false;
    }

    /// <summary>
    /// Lưu phiếu chi rồi mở bản in. Mã phiếu phải được chụp TRƯỚC khi lưu vì
    /// luồng lưu thành công sẽ gọi ResetSoQuyPhieuForm và xóa mã khỏi form.
    /// </summary>
    [RelayCommand]
    private async Task LuuVaInThemPhieuChiAsync()
    {
        var maPhieu = SoQuyPhieuMa;

        var daLuu = await LuuSoQuyPhieuAsync(false);

        if (!daLuu)
            return;

        IsThemPhieuChiVisible = false;

        if (!string.IsNullOrWhiteSpace(maPhieu))
            await InPhieuChiTheoMaAsync(maPhieu);
    }

    // =========================================================
    // THAO TÁC TRÊN DÒNG DANH SÁCH
    // =========================================================

    /// <summary>Sửa một phiếu chi từ dòng của bảng (mở chung modal sửa của Sổ quỹ).</summary>
    [RelayCommand]
    private async Task SuaPhieuChiAsync(SoQuyGiaoDich? gd)
    {
        if (gd is null)
            return;

        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookEdit, "Bạn không có quyền sửa phiếu quỹ."))
        {
            return;
        }

        try
        {
            var chiTiet = await _db.GetSoQuyChiTietAsync(gd.MaPhieu);

            if (chiTiet is null)
            {
                StatusMessage = $"Không tìm thấy phiếu {gd.MaPhieu} trong database.";
                return;
            }

            if (chiTiet.DaHuy)
            {
                await ShowMessageAsync(
                    "Phiếu đã hủy",
                    $"Phiếu {gd.MaPhieu} đã bị hủy nên không thể sửa.\n\n"
                    + "Bạn chỉ có thể xem lại phiếu này.");

                return;
            }

            await MoFormSuaTuChiTietAsync(chiTiet);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi mở form sửa phiếu chi", ex);
            StatusMessage = "Không thể mở form sửa phiếu: " + ex.Message;
        }
    }

    /// <summary>
    /// Hủy (xóa mềm) một phiếu chi. Giữ nguyên xác nhận + cờ chống bấm đúp
    /// giống hủy phiếu ở Sổ quỹ.
    /// </summary>
    [RelayCommand]
    private async Task XoaPhieuChiAsync(SoQuyGiaoDich? gd)
    {
        if (gd is null)
            return;

        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookCancel, "Bạn không có quyền hủy phiếu quỹ."))
        {
            return;
        }

        if (gd.DaHuy)
        {
            StatusMessage = $"Phiếu {gd.MaPhieu} đã được hủy trước đó.";
            return;
        }

        if (_soQuyDangHuyPhieu)
            return;

        var xacNhan = await XacNhanHuyPhieuAsync(
            1,
            $"Bạn có chắc muốn hủy phiếu {gd.MaPhieu}?\n\n"
            + "Phiếu đã hủy sẽ không còn được tính vào tổng thu, tổng chi của sổ quỹ.\n"
            + "Thao tác này không thể hoàn tác.");

        if (!xacNhan)
            return;

        _soQuyDangHuyPhieu = true;

        try
        {
            await _db.HuyPhieuQuyAsync(gd.MaPhieu);

            StatusMessage = $"Đã hủy phiếu {gd.MaPhieu}.";

            await TaiLaiSoQuyAsync();
            await LoadDsPhieuChiAsync();

            _ = LamMoiTongQuanNeuDangMoAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi hủy phiếu chi", ex);
            StatusMessage = "Không thể hủy phiếu: " + ex.Message;
        }
        finally
        {
            _soQuyDangHuyPhieu = false;
        }
    }

    /// <summary>
    /// In một phiếu chi theo dòng đã chọn. Bản in dựng bằng
    /// <see cref="PhieuChiPrintService"/> rồi mở bằng trình duyệt mặc định.
    /// </summary>
    [RelayCommand]
    private async Task InPhieuChiAsync(SoQuyGiaoDich? gd)
    {
        if (gd is null)
            return;

        await InPhieuChiTheoMaAsync(gd.MaPhieu);
    }

    /// <summary>In một phiếu chi theo mã; dùng chung cho "Lưu &amp; In".</summary>
    private async Task InPhieuChiTheoMaAsync(string maPhieu)
    {
        if (string.IsNullOrWhiteSpace(maPhieu))
            return;

        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookView, "Bạn không có quyền xem sổ quỹ."))
        {
            return;
        }

        try
        {
            var chiTiet = await _db.GetSoQuyChiTietAsync(maPhieu);

            if (chiTiet is null)
            {
                StatusMessage = $"Không tìm thấy phiếu {maPhieu} trong database.";
                return;
            }

            var tiem = await LayThongTinTiemChoHoaDonAsync();
            var duongDan = PhieuChiPrintService.LuuFileTam(chiTiet, tiem);

            Process.Start(new ProcessStartInfo
            {
                FileName = duongDan,
                UseShellExecute = true
            });

            StatusMessage = $"Đã mở bản in phiếu {maPhieu}.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi in phiếu chi", ex);
            StatusMessage = $"Không in được phiếu {maPhieu}: {ex.Message}";
        }
    }

    // =========================================================
    // XUẤT EXCEL
    // =========================================================

    [RelayCommand]
    private async Task XuatExcelDsPhieuChiAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.CashBookExport, "Bạn không có quyền export sổ quỹ."))
        {
            return;
        }

        if (OwnerWindow is null)
            return;

        var boLoc = await TaoBoLocPhieuChiAsync();

        if (boLoc is null)
            return;

        var tenFileMacDinh = $"PhieuChi_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        var file = await OwnerWindow.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Xuất danh sách phiếu chi",
                SuggestedFileName = tenFileMacDinh,
                DefaultExtension = "xlsx",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("Excel")
                    {
                        Patterns = ["*.xlsx"]
                    }
                ]
            });

        if (file is null)
        {
            StatusMessage = "Bạn đã hủy xuất file.";
            return;
        }

        try
        {
            StatusMessage = "Đang xuất danh sách phiếu chi...";

            var danhSach = await _db.GetSoQuyPhieuExportAsync(boLoc);

            if (danhSach.Count == 0)
            {
                await ShowMessageAsync(
                    "Xuất file thất bại",
                    "Không có phiếu chi nào khớp điều kiện đang xem\nnên không thể xuất file.");

                StatusMessage = "Không có dữ liệu để xuất.";

                return;
            }

            await using var stream = await file.OpenWriteAsync();

            await Task.Run(() => TaoFileExcelSoQuy(danhSach, stream));

            StatusMessage = $"Đã xuất {danhSach.Count:N0} phiếu chi ra file {tenFileMacDinh}.";

            await ShowMessageAsync(
                "Xuất file thành công",
                $"Đã xuất {danhSach.Count:N0} phiếu chi.\n\nFile:\n{file.Name}");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xuất danh sách phiếu chi", ex);
            StatusMessage = "Xuất file thất bại: " + ex.Message;

            await ShowMessageAsync(
                "Xuất file thất bại",
                $"Không thể xuất danh sách phiếu chi.\n\nChi tiết lỗi:\n{ex.Message}");
        }
    }
}
