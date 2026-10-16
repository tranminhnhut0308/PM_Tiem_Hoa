using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// Phần xuất Excel cho báo cáo cuối ngày.
/// <para>
/// File gồm đúng 6 sheet: Tổng quan, Hóa đơn bán hàng, Sản phẩm bán chạy,
/// Thu chi, Công nợ khách hàng, Nhập hàng. Tên file là
/// <c>BaoCaoCuoiNgay_yyyyMMdd.xlsx</c> theo ngày đang xem trên màn hình.
/// </para>
/// <para>
/// Quy ước dùng chung với phần xuất nhà cung cấp: tiêu đề in đậm nền xanh, cột
/// tiền ghi bằng số thập phân thuần (người dùng cộng/lọc được), ngày ghi bằng
/// DateTime, cột tự co theo nội dung và khoá dòng tiêu đề.
/// </para>
/// </summary>
public sealed partial class BaoCaoCuoiNgayViewModel
{
    /// <summary>
    /// Xuất báo cáo của ngày đang xem ra file Excel do người dùng chọn.
    /// </summary>
    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        var cuaSo = LayCuaSoChinh();

        if (cuaSo is null)
            return;

        // Đặt tên file trước để hiện sẵn trong hộp thoại lưu.
        var ngay = (NgayChon?.DateTime ?? DateTime.Today).Date;
        var tenFileMacDinh = new BaoCaoCuoiNgayDuLieu { Ngay = ngay }.TenFileXuat();

        var file = await cuaSo.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Xuất báo cáo cuối ngày",

                SuggestedFileName = tenFileMacDinh,

                DefaultExtension = "xlsx",

                // Không tự động ghi đè file đang tồn tại.
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
            CapNhatTrangThai?.Invoke("Bạn đã hủy xuất file.");
            return;
        }

        try
        {
            CapNhatTrangThai?.Invoke("Đang xuất báo cáo cuối ngày...");

            // Tải lại dữ liệu để file khớp chính xác với ngày đang chọn.
            await TaiDuLieuAsync();

            if (CoLoi)
            {
                await HienThiHopNhat?.Invoke(
                    "Xuất file thất bại",
                    "Không tải được dữ liệu nên không thể xuất file.\n\n" +
                    "Chi tiết:\n" + ThongBaoLoi);

                CapNhatTrangThai?.Invoke("Không thể xuất file do lỗi tải dữ liệu.");
                return;
            }

            var duLieu = new BaoCaoCuoiNgayDuLieu
            {
                Ngay = ngay,
                NgayKeTiep = ngay.AddDays(1),
                TongKetBanHang = TongKetBanHang,
                SoQuyTongHop = SoQuyTongHop,
                NhapHang = NhapHang,
                KhachHangTongHop = KhachHangTongHop,
                CongNoTongHop = CongNoTongHop,
                DoanhThuTheoGio = [.. DoanhThuTheoGio],
                PhuongThucThanhToan = [.. PhuongThucThanhToan],
                TopSanPham = [.. TopSanPham],
                TopKhachHang = [.. TopKhachHang],
                CongNoPhatSinh = [.. CongNoPhatSinh],
                ThuChi = [.. ThuChi]
            };

            // Dựng file trên luồng nền để không làm treo giao diện.
            await using var stream = await file.OpenWriteAsync();

            await Task.Run(() =>
            {
                using var workbook = BaoCaoCuoiNgayExcel.TaoWorkbook(duLieu);

                workbook.SaveAs(stream);
            });

            CapNhatTrangThai?.Invoke($"✅ Đã xuất báo cáo ngày {duLieu.NgayHienThi} ra file {tenFileMacDinh}.");

            await HienThiHopNhat?.Invoke(
                "Xuất file thành công",
                $"Đã xuất báo cáo cuối ngày {duLieu.NgayHienThi}.\n\n" +
                $"File:\n{file.Name}");
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi xuat file bao cao cuoi ngay", ex);

            CapNhatTrangThai?.Invoke("Xuất file thất bại: " + ex.Message);

            await HienThiHopNhat?.Invoke(
                "Xuất file thất bại",
                "Không thể xuất báo cáo cuối ngày.\n\n" +
                $"Chi tiết lỗi:\n{ex.Message}");
        }
    }
}