using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly HoaDonImportService _hoaDonImport = new();

    // =========================================================
    // LỌC NHANH HÓA ĐƠN: TẤT CẢ / ĐÃ THANH TOÁN / CÒN NỢ
    // =========================================================

    /// <summary>Bộ lọc trạng thái đang chọn: ALL, PAID hoặc DEBT.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInvoiceFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsInvoiceFilterPaid))]
    [NotifyPropertyChangedFor(nameof(IsInvoiceFilterDebt))]
    private string invoiceFilter = "ALL";

    public bool IsInvoiceFilterAll => InvoiceFilter == "ALL";
    public bool IsInvoiceFilterPaid => InvoiceFilter == "PAID";
    public bool IsInvoiceFilterDebt => InvoiceFilter == "DEBT";

    /// <summary>Giá trị lọc truyền xuống database (rỗng = không lọc).</summary>
    private string? LocHoaDonHienTai => InvoiceFilter switch
    {
        "PAID" => "PAID",
        "DEBT" => "DEBT",
        _ => null
    };

    /// <summary>Đổi bộ lọc nhanh rồi tải lại danh sách hóa đơn.</summary>
    [RelayCommand]
    private async Task LocHoaDonAsync(string? filter)
    {
        InvoiceFilter = string.IsNullOrWhiteSpace(filter)
            ? "ALL"
            : filter.Trim().ToUpperInvariant();

        await LoadInvoicesAsync();
    }

    /// <summary>
    /// Thu công nợ theo khách hàng (nút "Thanh toán công nợ" ở tab Công nợ):
    /// hỏi số tiền + phương thức rồi tự động trừ dần vào các hóa đơn còn nợ (cũ
    /// nhất trước). Mỗi hóa đơn được cập nhật và mỗi lần thu đều tạo phiếu thu
    /// trong sổ quỹ.
    /// </summary>
    [RelayCommand]
    private async Task ThuNoKhachHangAsync(KhachHangRowViewModel? row)
    {
        if (row is null || OwnerWindow is null)
            return;

        if (!await KiemTraQuyenAsync(
                PermissionKeys.CustomersDebtPay,
                "Bạn không có quyền thu công nợ khách hàng."))
        {
            return;
        }

        var khachHangId = row.CustomerId;

        decimal conNo;

        try
        {
            var thongKe = await _db.GetThongKeMuaHangCuaKhachHangAsync(khachHangId);
            conNo = thongKe.TongConNoHienTai;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không đọc được công nợ", ex.Message);
            return;
        }

        if (conNo <= 0)
        {
            await ShowMessageAsync(
                "Thông báo",
                $"Khách hàng {row.TenKhachHang} không còn công nợ.");
            return;
        }

        var dialog = new ThuNoKhachHangWindow(row.TenKhachHang, conNo);
        var ketQua = await dialog.ShowDialog<ThuNoHoaDonKetQua?>(OwnerWindow);

        if (ketQua is null)
        {
            StatusMessage = "Đã hủy thu công nợ.";
            return;
        }

        try
        {
            var ketQuaThu = await _db.ThuNoKhachHangAsync(
                khachHangId,
                ketQua.SoTien,
                ketQua.PhuongThuc,
                ketQua.GhiChu);

            // Nạp lại danh sách để số nợ/tổng bán của dòng khớp database.
            await LoadCustomersAsync();

            // Mở lại vùng chi tiết ở tab Công nợ để người dùng thấy số nợ mới.
            var refreshedRow = CustomerRows.FirstOrDefault(r => r.CustomerId == khachHangId);

            if (refreshedRow is not null)
            {
                await MoChiTietKhachHangAsync(refreshedRow);

                if (refreshedRow.Detail is not null)
                    refreshedRow.Detail.SelectedTab = "congno";
            }

            _ = LamMoiTongQuanNeuDangMoAsync();

            var thongBaoPhieu = ketQuaThu.MaPhieuThus.Count == 0
                ? ""
                : $" Đã ghi {ketQuaThu.MaPhieuThus.Count:N0} phiếu thu "
                  + $"({string.Join(", ", ketQuaThu.MaPhieuThus)}) vào sổ quỹ.";

            StatusMessage =
                $"Đã thu {ketQuaThu.TongDaThu:N0} đ của {ketQuaThu.TenKhachHang}. "
                + $"Còn nợ {ketQuaThu.ConNoConLai:N0} đ.{thongBaoPhieu}";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Không thu được công nợ", ex.Message);
            SafeLog.WriteError("Loi thu no khach hang", ex);
        }
    }

    /// <summary>
    /// Nhập hóa đơn từ file Excel/CSV có sẵn của người dùng: đọc dòng tiêu đề để
    /// tự đối chiếu cột, chỉ mở màn hình ánh xạ khi còn cột chưa khớp, cho xem
    /// trước rồi mới ghi xuống database.
    /// </summary>
    [RelayCommand]
    private async Task NhapHoaDonTuFileAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.InvoicesImport, "Bạn không có quyền import hóa đơn."))
        {
            return;
        }
        if (OwnerWindow is null || IsImporting)
            return;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Chọn file hóa đơn cần nhập",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Excel / CSV")
                    {
                        Patterns = ["*.xlsx", "*.xlsm", "*.csv"]
                    }
                ]
            });

        var file = files.FirstOrDefault();

        if (file is null)
        {
            StatusMessage = "Bạn chưa chọn file hóa đơn.";
            return;
        }

        try
        {
            IsImporting = true;
            ImportHasProgress = false;
            ImportProgress = 0;

            CapNhatTrangThaiNhap($"Đang đọc dòng tiêu đề của \"{file.Name}\"...");

            var thongTin = await _hoaDonImport.DocTieuDeAsync(file.Path.LocalPath);

            if (thongTin.Mapping.Sheets.Count == 0)
            {
                IsImporting = false;

                await ShowMessageAsync(
                    "File không đọc được nội dung",
                    $"File \"{thongTin.TenFile}\" không có sheet nào chứa dòng tiêu đề. "
                    + "Hãy kiểm tra lại file rồi nhập lại.");

                return;
            }

            var mapping = thongTin.Mapping;

            // File đã tự khớp đủ cột thì nhảy thẳng sang xem trước, không bắt
            // người dùng xác nhận lại những gì phần mềm đã hiểu đúng. Còn lại thì
            // mở màn hình ánh xạ để người dùng tự gán cột, kể cả sheet mà phần
            // mềm chưa nhận ra cột mã hóa đơn.
            if (!thongTin.DaKhopDayDu)
            {
                IsImporting = false;

                mapping = await HoaDonImportMappingWindow.ShowAsync(OwnerWindow, thongTin)
                          ?? throw new OperationCanceledException();

                var thieu = mapping.CoTruongChuaGan().Distinct().ToList();

                if (thieu.Count > 0)
                {
                    await ShowMessageAsync(
                        "Thiếu cột bắt buộc",
                        "Các trường sau chưa được gán cột: "
                        + string.Join(", ", thieu.Select(TenTruongHoaDon.Lay))
                        + ".\n\nNhững hóa đơn bị ảnh hưởng sẽ được báo ở bước xem trước, "
                        + "bạn vẫn có thể tiếp tục nếu muốn nhập phần còn lại.");
                }
            }

            IsImporting = true;
            CapNhatTrangThaiNhap($"Đang đọc dữ liệu từ \"{mapping.TenFile}\"...");

            var progress = new Progress<HoaDonImportProgress>(CapNhatTrangThaiNhap);

            var preview = await _hoaDonImport.DocFileAsync(
                file.Path.LocalPath,
                mapping,
                progress);

            if (preview.TongSoHoaDon == 0)
            {
                IsImporting = false;

                await ShowMessageAsync(
                    "File không có dữ liệu",
                    $"File \"{preview.TenFile}\" không tìm thấy dòng nào có mã hóa đơn "
                    + "theo cách ánh xạ đã chọn.");

                return;
            }

            // Tắt overlay để cửa sổ xem trước hiện được trên UI thread.
            IsImporting = false;

            var quyetDinh = await HoaDonImportPreviewWindow.ShowAsync(OwnerWindow, preview);

            if (quyetDinh is null)
            {
                StatusMessage = "Đã hủy nhập hóa đơn. Không có dữ liệu nào được thay đổi.";
                return;
            }

            if (preview.SoHoaDonHopLe == 0)
            {
                await ShowMessageAsync(
                    "Không có hóa đơn nào nhập được",
                    "Mọi hóa đơn trong file đều bị loại. "
                    + (preview.DanhSachLoi.FirstOrDefault()?.LyDo ?? "Hãy xem lại danh sách lỗi."));

                return;
            }

            IsImporting = true;
            ImportHasProgress = false;
            ImportProgress = 0;

            CapNhatTrangThaiNhap("Đang ghi hóa đơn vào database...");

            var ketQua = await _hoaDonImport.ImportAsync(
                preview,
                quyetDinh.XuLyTrung,
                quyetDinh.TaoKhachHangMoi,
                progress);

            // Danh sách hóa đơn trên màn hình đã cũ, nạp lại để thấy dữ liệu mới.
            IsImporting = false;
            _invoicesLoaded = false;

            await LoadInvoicesAsync();

            await HoaDonImportResultWindow.ShowAsync(OwnerWindow, ketQua);

            StatusMessage = TaoThongBaoKetQua(ketQua);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy nhập hóa đơn. Không có dữ liệu nào được thay đổi.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ Không thể nhập hóa đơn từ file: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
            ImportHasProgress = false;
            ImportProgress = 0;
        }
    }

    /// <summary>
    /// Cập nhật nội dung overlay đang chạy. Khi biết tổng số đơn vị công việc
    /// thì hiện thanh tiến độ xác định, không thì dùng thanh chạy vô hạn.
    /// </summary>
    private void CapNhatTrangThaiNhap(HoaDonImportProgress tienDo)
    {
        if (tienDo.TongSo > 0)
        {
            ImportHasProgress = true;

            var daXuLy = Math.Clamp(tienDo.DaXuLy, 0, tienDo.TongSo);
            ImportProgress = daXuLy * 100d / tienDo.TongSo;
        }

        CapNhatTrangThaiNhap(
            tienDo.TongSo > 0
                ? $"{tienDo.GiaiDoan} {tienDo.DaXuLy:N0}/{tienDo.TongSo:N0}"
                : $"{tienDo.GiaiDoan} {tienDo.DaXuLy:N0}");
    }

    private void CapNhatTrangThaiNhap(string trangThai)
    {
        ImportStatus = trangThai;
        StatusMessage = trangThai;
    }

    private static string TaoThongBaoKetQua(HoaDonImportResult ketQua)
    {
        if (ketQua.DaRollback)
            return "❌ Lỗi nghiêm trọng, đã hủy toàn bộ thay đổi. Database giữ nguyên như cũ.";

        if (ketQua.TongSoGhiThanhCong == 0)
            return $"⚠️ Không có hóa đơn nào được ghi. {ketQua.ThongBaoTatCa}";

        return $"✅ Đã nhập {ketQua.SoThemMoi:N0}, cập nhật {ketQua.SoCapNhat:N0} hóa đơn "
            + $"từ file \"{ketQua.TenFile}\". {ketQua.ThongBaoTatCa}";
    }
}
