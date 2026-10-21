using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // ============================================================
    // GIAO DIỆN 25 – THÊM PHIẾU XUẤT HÀNG (STATE & COMMANDS)
    // ============================================================

    public ObservableCollection<ChiTietPhieuXuatHang> PhieuXuatItems { get; } = new();
    public ObservableCollection<SanPham> PhieuXuatProductSuggestions { get; } = new();

    public IReadOnlyList<string> PhieuXuatKhoOptions { get; } =
    [
        "Kho Tổng (Kho mặc định)",
        "Kho Trưng Bày Tiệm",
        "Kho Hàng Hỏng / Chờ Xử Lý"
    ];

    public IReadOnlyList<string> PhieuXuatLoaiOptions { get; } =
    [
        "Xuất hủy / Hao hụt / Hết hạn",
        "Xuất sử dụng nội bộ / Trưng bày",
        "Xuất trả nhà cung cấp",
        "Xuất chuyển kho / chi nhánh",
        "Xuất khác"
    ];

    [ObservableProperty] private string phieuXuatMa = "";
    [ObservableProperty] private DateTime? phieuXuatNgayXuat = DateTime.Now;
    [ObservableProperty] private string phieuXuatKhoXuat = "Kho Tổng (Kho mặc định)";
    [ObservableProperty] private string phieuXuatLoaiXuat = "Xuất hủy / Hao hụt / Hết hạn";
    [ObservableProperty] private string phieuXuatDoiTuongNhan = "";
    [ObservableProperty] private string phieuXuatNhanVien = "";
    [ObservableProperty] private string phieuXuatGhiChu = "";
    [ObservableProperty] private string phieuXuatProductSearch = "";
    [ObservableProperty] private decimal phieuXuatTongSoLuong;
    [ObservableProperty] private decimal phieuXuatTongTien;
    [ObservableProperty] private bool isSavingPhieuXuat;
    [ObservableProperty] private bool isPhieuXuatCalendarOpen;

    public string PhieuXuatTongSoLuongText =>
        PhieuXuatTongSoLuong.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public string PhieuXuatTongTienText =>
        PhieuXuatTongTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public string PhieuXuatNgayXuatText =>
        PhieuXuatNgayXuat?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "";

    public bool HasPhieuXuatItems => PhieuXuatItems.Count > 0;
    public bool IsPhieuXuatEmpty => PhieuXuatItems.Count == 0;
    public bool HasPhieuXuatSuggestions => PhieuXuatProductSuggestions.Count > 0;
    public bool CanSavePhieuXuat => !IsSavingPhieuXuat && PhieuXuatItems.Count > 0;

    public bool IsXuatHangSelected => SelectedMenu == "Xuất hàng";

    private void RecalculatePhieuXuatTotals()
    {
        PhieuXuatTongSoLuong = PhieuXuatItems.Sum(x => x.SoLuong);
        PhieuXuatTongTien = PhieuXuatItems.Sum(x => x.ThanhTien);

        OnPropertyChanged(nameof(PhieuXuatTongSoLuong));
        OnPropertyChanged(nameof(PhieuXuatTongSoLuongText));
        OnPropertyChanged(nameof(PhieuXuatTongTien));
        OnPropertyChanged(nameof(PhieuXuatTongTienText));
        OnPropertyChanged(nameof(HasPhieuXuatItems));
        OnPropertyChanged(nameof(IsPhieuXuatEmpty));
        OnPropertyChanged(nameof(CanSavePhieuXuat));
    }

    [RelayCommand]
    private void MoLichPhieuXuat()
    {
        IsPhieuXuatCalendarOpen = !IsPhieuXuatCalendarOpen;
    }

    [RelayCommand]
    public async Task MoGiaoDienXuatHangAsync()
    {
        ChonMenu("Xuất hàng");
        await KhoiTaoPhieuXuatMoiAsync();
    }

    /// <summary>
    /// Điều hướng tới trung tâm "Xuất hàng" ở thẻ Tạo phiếu.
    /// </summary>
    [RelayCommand]
    public async Task MoTaoPhieuXuatMoiAsync()
    {
        ChonMenu("Xuất hàng");
        await KhoiTaoPhieuXuatMoiAsync();
    }

    /// <summary>
    /// Khởi tạo dữ liệu cho một phiếu xuất hàng mới.
    /// </summary>
    public async Task KhoiTaoPhieuXuatMoiAsync()
    {
        IsPhieuXuatTabDanhSach = false;
        PhieuXuatItems.Clear();
        PhieuXuatProductSuggestions.Clear();
        PhieuXuatProductSearch = "";
        PhieuXuatNgayXuat = DateTime.Now;
        PhieuXuatKhoXuat = "Kho Tổng (Kho mặc định)";
        PhieuXuatLoaiXuat = "Xuất hủy / Hao hụt / Hết hạn";
        PhieuXuatDoiTuongNhan = "";
        PhieuXuatGhiChu = "";
        PhieuXuatNhanVien = Session.CurrentUser?.HoTen ?? "Quản trị viên";
        IsSavingPhieuXuat = false;

        try
        {
            PhieuXuatMa = await _db.TaoMaPhieuXuatMoiAsync();
        }
        catch
        {
            PhieuXuatMa = $"PX{DateTime.Now:yyMMdd}-001";
        }

        RecalculatePhieuXuatTotals();
        StatusMessage = "Sẵn sàng lập phiếu xuất hàng mới.";
    }

    /// <summary>
    /// Tìm kiếm hàng hóa xuất kho realtime theo từ khóa.
    /// </summary>
    public async Task TimKiemHangHoaXuatAsync(string? keyword)
    {
        PhieuXuatProductSearch = keyword ?? "";
        var text = PhieuXuatProductSearch.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            PhieuXuatProductSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuXuatSuggestions));
            return;
        }

        try
        {
            var (products, _) = await _db.GetSanPhamPagedAsync(text, 1, 15);
            PhieuXuatProductSuggestions.Clear();
            foreach (var p in products)
            {
                PhieuXuatProductSuggestions.Add(p);
            }
            OnPropertyChanged(nameof(HasPhieuXuatSuggestions));
        }
        catch
        {
            PhieuXuatProductSuggestions.Clear();
            OnPropertyChanged(nameof(HasPhieuXuatSuggestions));
        }
    }

    /// <summary>
    /// Chọn sản phẩm từ danh sách gợi ý để thêm vào phiếu xuất.
    /// Tự động lấy tồn kho khả dụng hiện tại từ database.
    /// </summary>
    [RelayCommand]
    private async Task ChonSanPhamXuatAsync(SanPham? sp)
    {
        if (sp is null || sp.Id == 0)
            return;

        PhieuXuatProductSuggestions.Clear();
        PhieuXuatProductSearch = "";
        OnPropertyChanged(nameof(HasPhieuXuatSuggestions));

        var existing = PhieuXuatItems.FirstOrDefault(x => x.SanPham.Id == sp.Id);
        if (existing != null)
        {
            // Kiểm tra tồn kho trước khi tăng
            if (existing.SoLuong + 1 > existing.TonKhoHienTai)
            {
                StatusMessage = $"⚠️ Sản phẩm '{sp.TenSanPham}' chỉ còn tồn kho {existing.TonKhoHienTai:N0}!";
                await ShowMessageAsync("Cảnh báo tồn kho", $"Sản phẩm '{sp.TenSanPham}' hiện chỉ còn tồn kho {existing.TonKhoHienTai:N0}. Không thể xuất vượt quá tồn kho.");
                return;
            }

            existing.SoLuong += 1;
            RecalculatePhieuXuatTotals();
            StatusMessage = $"Đã tăng số lượng '{sp.TenSanPham}': {existing.SoLuong:N0}";
            return;
        }

        // Lấy tồn kho thực tế từ database
        var tonKho = await _db.LayTonKhoSanPhamAsync(sp.Id, Session.CurrentUser?.ChiNhanhId ?? 1);
        if (tonKho <= 0)
        {
            StatusMessage = $"⚠️ Sản phẩm '{sp.TenSanPham}' đã hết hàng trong kho (tồn: 0). Không thể xuất!";
            await ShowMessageAsync("Không đủ tồn kho", $"Sản phẩm '{sp.TenSanPham}' hiện đã hết hàng trong kho (Tồn kho: 0). Vui lòng kiểm tra lại.");
            return;
        }

        var item = new ChiTietPhieuXuatHang(sp, tonKho)
        {
            Index = PhieuXuatItems.Count + 1,
            SoLuong = 1
        };

        // Đăng ký tính lại tổng khi dòng thay đổi
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ChiTietPhieuXuatHang.SoLuong) or nameof(ChiTietPhieuXuatHang.DonGia))
            {
                RecalculatePhieuXuatTotals();
            }
        };

        PhieuXuatItems.Add(item);
        ReindexPhieuXuatItems();
        RecalculatePhieuXuatTotals();

        StatusMessage = $"Đã thêm '{sp.TenSanPham}' vào phiếu xuất.";
    }

    [RelayCommand]
    private void TangSoLuongXuat(ChiTietPhieuXuatHang? item)
    {
        if (item is null) return;
        if (item.SoLuong + 1 > item.TonKhoHienTai)
        {
            StatusMessage = $"⚠️ Không thể xuất vượt tồn kho ({item.TonKhoHienTai:N0}) của '{item.TenSanPham}'!";
            return;
        }
        item.SoLuong += 1;
        RecalculatePhieuXuatTotals();
    }

    [RelayCommand]
    private void GiamSoLuongXuat(ChiTietPhieuXuatHang? item)
    {
        if (item is null) return;
        if (item.SoLuong > 1)
        {
            item.SoLuong -= 1;
            RecalculatePhieuXuatTotals();
        }
        else
        {
            XoaDongPhieuXuat(item);
        }
    }

    [RelayCommand]
    private void XoaDongPhieuXuat(ChiTietPhieuXuatHang? item)
    {
        if (item is null) return;
        PhieuXuatItems.Remove(item);
        ReindexPhieuXuatItems();
        RecalculatePhieuXuatTotals();
        StatusMessage = $"Đã xóa '{item.TenSanPham}' khỏi phiếu xuất.";
    }

    private void ReindexPhieuXuatItems()
    {
        for (int i = 0; i < PhieuXuatItems.Count; i++)
        {
            PhieuXuatItems[i].Index = i + 1;
        }
    }

    [RelayCommand]
    private async Task XoaTatPhieuXuatAsync()
    {
        if (PhieuXuatItems.Count == 0) return;
        var confirm = await ShowConfirmationAsync(
            "Xóa tất cả",
            "Bạn có chắc muốn xóa tất cả mặt hàng trong phiếu xuất hiện tại?",
            "Xóa tất cả",
            "Hủy");

        if (confirm)
        {
            PhieuXuatItems.Clear();
            RecalculatePhieuXuatTotals();
            StatusMessage = "Đã xóa toàn bộ mặt hàng trong phiếu xuất.";
        }
    }

    [RelayCommand]
    private async Task HuyPhieuXuatAsync()
    {
        if (PhieuXuatItems.Count > 0)
        {
            var confirm = await ShowConfirmationAsync(
                "Hủy phiếu xuất",
                "Phiếu xuất đang có mặt hàng chưa lưu. Bạn có chắc muốn hủy bỏ và quay lại?",
                "Hủy và thoát",
                "Ở lại");

            if (!confirm)
                return;
        }

        PhieuXuatItems.Clear();
        RecalculatePhieuXuatTotals();

        // Hủy phiếu xuất thì quay về tab "Danh sách phiếu xuất" của trung tâm Xuất hàng.
        IsPhieuXuatTabDanhSach = true;
        StatusMessage = "Đã hủy thao tác thêm phiếu xuất.";
    }

    private string ChuyenDoiLoaiXuatSangMa(string tenHienThi)
    {
        if (tenHienThi.Contains("hủy", StringComparison.OrdinalIgnoreCase) || tenHienThi.Contains("hỏng", StringComparison.OrdinalIgnoreCase))
            return "XUAT_HUY";
        if (tenHienThi.Contains("nội bộ", StringComparison.OrdinalIgnoreCase) || tenHienThi.Contains("trưng bày", StringComparison.OrdinalIgnoreCase))
            return "XUAT_NOI_BO";
        if (tenHienThi.Contains("nhà cung cấp", StringComparison.OrdinalIgnoreCase))
            return "XUAT_TRA_NCC";
        if (tenHienThi.Contains("chuyển kho", StringComparison.OrdinalIgnoreCase))
            return "XUAT_CHUYEN_KHO";
        return "XUAT_KHAC";
    }

    [RelayCommand]
    private async Task LuuPhieuXuatHangAsync()
    {
        await ThucHienLuuPhieuXuatAsync(inPhieuSauKhiLuu: false);
    }

    [RelayCommand]
    private async Task LuuVaInPhieuXuatAsync()
    {
        await ThucHienLuuPhieuXuatAsync(inPhieuSauKhiLuu: true);
    }

    private async Task ThucHienLuuPhieuXuatAsync(bool inPhieuSauKhiLuu)
    {
        if (IsSavingPhieuXuat)
            return;

        // 1. Kiểm tra quyền
        if (!await KiemTraQuyenAsync(PermissionKeys.ProductsAdjustStock, "Bạn không có quyền xuất kho hoặc điều chỉnh tồn kho."))
        {
            return;
        }

        // 2. Kiểm tra dữ liệu hợp lệ
        if (PhieuXuatItems.Count == 0)
        {
            await ShowMessageAsync("Thiếu hàng hóa", "Vui lòng chọn ít nhất một mặt hàng trước khi lưu phiếu xuất.");
            return;
        }

        var vuotTon = PhieuXuatItems.FirstOrDefault(x => x.SoLuong > x.TonKhoHienTai);
        if (vuotTon != null)
        {
            await ShowMessageAsync("Vượt tồn kho", $"Mặt hàng '{vuotTon.TenSanPham}' có số lượng xuất ({vuotTon.SoLuong:N0}) vượt quá tồn kho hiện tại ({vuotTon.TonKhoHienTai:N0})!");
            return;
        }

        var soLuongKhongHopLe = PhieuXuatItems.FirstOrDefault(x => x.SoLuong <= 0);
        if (soLuongKhongHopLe != null)
        {
            await ShowMessageAsync("Số lượng không hợp lệ", $"Mặt hàng '{soLuongKhongHopLe.TenSanPham}' có số lượng phải lớn hơn 0.");
            return;
        }

        IsSavingPhieuXuat = true;
        StatusMessage = "Đang lưu phiếu xuất hàng vào cơ sở dữ liệu...";

        try
        {
            var phieu = new PhieuXuatHang
            {
                MaPhieuXuat = PhieuXuatMa,
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1,
                KhoXuat = PhieuXuatKhoXuat,
                LoaiXuat = ChuyenDoiLoaiXuatSangMa(PhieuXuatLoaiXuat),
                DoiTuongNhan = PhieuXuatDoiTuongNhan,
                TenNhanVien = PhieuXuatNhanVien,
                ThoiGianXuat = PhieuXuatNgayXuat ?? DateTime.Now,
                TongSoLuong = PhieuXuatTongSoLuong,
                TongTien = PhieuXuatTongTien,
                GhiChu = PhieuXuatGhiChu
            };

            var itemsSnapshot = PhieuXuatItems.ToList();
            var id = await _db.LuuPhieuXuatHangAsync(phieu, itemsSnapshot);

            // Đồng bộ tồn kho trong bộ nhớ để các màn hình khác cập nhật ngay
            foreach (var item in itemsSnapshot)
            {
                var tonMoi = Math.Max(0, item.TonKhoHienTai - item.SoLuong);
                DongBoTonKho(item.SanPham.Id, tonMoi);
            }

            StatusMessage = $"✅ Đã lưu phiếu xuất kho {phieu.MaPhieuXuat} thành công (Tổng: {phieu.TongSoLuong:N0} SP, {phieu.TongTien:N0} đ).";

            if (inPhieuSauKhiLuu)
            {
                InBaoCaoPhieuXuatHtml(phieu, itemsSnapshot);
            }

            await NotificationWindow.ShowSuccessAsync(
                $"Đã lưu phiếu xuất kho {phieu.MaPhieuXuat} thành công!\nTồn kho đã được cập nhật.",
                owner: OwnerWindow);

            // Khởi tạo phiếu mới sạch sẽ
            await KhoiTaoPhieuXuatMoiAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi lưu phiếu xuất hàng", ex);
            StatusMessage = "❌ Lưu phiếu xuất thất bại: " + SafeLog.Redact(ex.Message);
            await ShowMessageAsync("Lỗi lưu phiếu xuất", ex.Message);
        }
        finally
        {
            IsSavingPhieuXuat = false;
        }
    }

    private void InBaoCaoPhieuXuatHtml(PhieuXuatHang phieu, IReadOnlyList<ChiTietPhieuXuatHang> items)
    {
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"PhieuXuat_{phieu.MaPhieuXuat}_{DateTime.Now:yyyyMMddHHmmss}.html");
            var rowsHtml = string.Join("", items.Select(x => $"""
                <tr>
                    <td style="text-align:center;">{x.Index}</td>
                    <td>{x.MaSanPham}</td>
                    <td><strong>{x.TenSanPham}</strong></td>
                    <td style="text-align:center;">{x.DonViTinh ?? "Cái"}</td>
                    <td style="text-align:right;">{x.SoLuong:N0}</td>
                    <td style="text-align:right;">{x.DonGia:N0} đ</td>
                    <td style="text-align:right;font-weight:bold;">{x.ThanhTien:N0} đ</td>
                </tr>
            """));

            // CSS để trong raw string thường (không interpolation) vì CSS dùng
            // nhiều dấu { } liên tiếp; để trực tiếp trong $""" thì compiler bắt
            // phải tăng số ký tự $ (xem CS9006).
            var css = """
                    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 30px; color: #1e293b; }
                    .header { text-align: center; margin-bottom: 24px; }
                    .title { font-size: 24px; font-weight: bold; color: #0f172a; text-transform: uppercase; }
                    .code { font-size: 14px; color: #0284c7; margin-top: 4px; font-weight: 600; }
                    .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 20px; font-size: 13.5px; background: #f8fafc; padding: 14px; border-radius: 8px; border: 1px solid #e2e8f0; }
                    table { width: 100%; border-collapse: collapse; margin-top: 14px; font-size: 13px; }
                    th, td { border: 1px solid #cbd5e1; padding: 8px 10px; }
                    th { background-color: #f1f5f9; color: #334155; text-align: left; }
                    .total-box { margin-top: 16px; text-align: right; font-size: 15px; font-weight: bold; }
                    .sign-grid { margin-top: 40px; display: grid; grid-template-columns: 1fr 1fr 1fr; text-align: center; font-size: 13px; }
                """;

            var html = $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>Phiếu Xuất Kho - {phieu.MaPhieuXuat}</title>
                <style>
                {css}
                </style>
            </head>
            <body>
                <div class="header">
                    <div class="title">PHIẾU XUẤT KHO</div>
                    <div class="code">Mã phiếu: {phieu.MaPhieuXuat}</div>
                </div>
                <div class="info-grid">
                    <div><strong>Ngày xuất:</strong> {phieu.ThoiGianXuatText}</div>
                    <div><strong>Kho xuất:</strong> {phieu.KhoXuat}</div>
                    <div><strong>Loại phiếu xuất:</strong> {phieu.LoaiXuatHienThi}</div>
                    <div><strong>Đối tượng nhận:</strong> {phieu.DoiTuongNhan ?? "Nội bộ tiệm"}</div>
                    <div><strong>Nhân viên lập:</strong> {phieu.TenNhanVien}</div>
                    <div><strong>Ghi chú:</strong> {phieu.GhiChu ?? "Không có"}</div>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th style="width:40px;text-align:center;">STT</th>
                            <th style="width:110px;">Mã hàng</th>
                            <th>Tên hàng hóa</th>
                            <th style="width:70px;text-align:center;">ĐVT</th>
                            <th style="width:80px;text-align:right;">Số lượng</th>
                            <th style="width:110px;text-align:right;">Đơn giá</th>
                            <th style="width:120px;text-align:right;">Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rowsHtml}
                    </tbody>
                </table>
                <div class="total-box">
                    <div>Tổng số lượng xuất: <span style="color:#0284c7;">{phieu.TongSoLuongText}</span></div>
                    <div style="margin-top:4px;">Tổng giá trị xuất: <span style="color:#059669;">{phieu.TongTienText}</span></div>
                </div>
                <div class="sign-grid">
                    <div><strong>Người lập phiếu</strong><br><br><br>{phieu.TenNhanVien}</div>
                    <div><strong>Người nhận hàng</strong><br><br><br>{phieu.DoiTuongNhan ?? "................"}</div>
                    <div><strong>Thủ kho</strong><br><br><br>................</div>
                </div>
            </body>
            </html>
            """;

            File.WriteAllText(tempFile, html, System.Text.Encoding.UTF8);
            Process.Start(new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi in phiếu xuất", ex);
        }
    }

    // ============================================================
    // GIAO DIỆN 26 – DANH SÁCH PHIẾU BÁN LẺ (STATE & COMMANDS)
    // ============================================================

    [ObservableProperty] private DateTime? invoiceTuNgay;
    [ObservableProperty] private DateTime? invoiceDenNgay;
    [ObservableProperty] private string invoiceTrangThaiLoc = "ALL";
    [ObservableProperty] private string invoicePhuongThucLoc = "ALL";
    [ObservableProperty] private int invoicePage = 1;
    [ObservableProperty] private int invoicePageSize = 20;
    [ObservableProperty] private int invoiceTotalCount;
    [ObservableProperty] private decimal invoiceTongTienHang;
    [ObservableProperty] private decimal invoiceTongGiamGia;
    [ObservableProperty] private decimal invoiceTongDoanhThu;
    [ObservableProperty] private decimal invoiceTongDaThu;
    [ObservableProperty] private decimal invoiceTongCongNo;
    [ObservableProperty] private bool isLoadingInvoices;

    public int InvoiceTotalPages => InvoicePageSize <= 0 ? 0 : (int)Math.Ceiling(InvoiceTotalCount / (double)InvoicePageSize);

    public string InvoiceRangeText => InvoiceTotalCount == 0
        ? "0 - 0 của 0"
        : $"Hiển thị {(InvoicePage - 1) * InvoicePageSize + 1} - {Math.Min(InvoicePage * InvoicePageSize, InvoiceTotalCount)} trên tổng {InvoiceTotalCount}";

    public string InvoiceTongDoanhThuText => InvoiceTongDoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string InvoiceTongDaThuText => InvoiceTongDaThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string InvoiceTongCongNoText => InvoiceTongCongNo.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string InvoiceTongTienHangText => InvoiceTongTienHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string InvoiceTongGiamGiaText => InvoiceTongGiamGia.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";

    public bool CanInvoiceTrangTruoc => InvoicePage > 1 && !IsLoadingInvoices;
    public bool CanInvoiceTrangSau => InvoicePage < InvoiceTotalPages && !IsLoadingInvoices;

    public bool IsInvoiceTrangThaiAll => InvoiceTrangThaiLoc == "ALL";
    public bool IsInvoiceTrangThaiPaid => InvoiceTrangThaiLoc == "PAID";
    public bool IsInvoiceTrangThaiDebt => InvoiceTrangThaiLoc == "DEBT";
    public bool IsInvoiceTrangThaiCancel => InvoiceTrangThaiLoc == "CANCEL";

    public bool CoBoLocHoaDon => !string.IsNullOrWhiteSpace(InvoiceSearch)
                                 || InvoiceTuNgay.HasValue
                                 || InvoiceDenNgay.HasValue
                                 || InvoiceTrangThaiLoc != "ALL"
                                 || InvoicePhuongThucLoc != "ALL";

    public bool IsHoaDonsEmpty => HoaDons.Count == 0 && !IsLoadingInvoices;
    public bool HasHoaDons => HoaDons.Count > 0;

    public IReadOnlyList<int> InvoicePageSizeOptions { get; } = [20, 50, 100];

    partial void OnInvoiceSearchChanged(string value)
    {
        OnPropertyChanged(nameof(CoBoLocHoaDon));
        InvoicePage = 1;
        _ = LoadInvoicesPagedAsync();
    }

    partial void OnInvoiceTuNgayChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(CoBoLocHoaDon));
        InvoicePage = 1;
        _ = LoadInvoicesPagedAsync();
    }

    partial void OnInvoiceDenNgayChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(CoBoLocHoaDon));
        InvoicePage = 1;
        _ = LoadInvoicesPagedAsync();
    }

    partial void OnInvoicePageSizeChanged(int value)
    {
        InvoicePage = 1;
        _ = LoadInvoicesPagedAsync();
    }

    [RelayCommand]
    public async Task LocHoaDonTheoTrangThaiAsync(string param)
    {
        InvoiceTrangThaiLoc = (param ?? "ALL").ToUpperInvariant();
        OnPropertyChanged(nameof(IsInvoiceTrangThaiAll));
        OnPropertyChanged(nameof(IsInvoiceTrangThaiPaid));
        OnPropertyChanged(nameof(IsInvoiceTrangThaiDebt));
        OnPropertyChanged(nameof(IsInvoiceTrangThaiCancel));
        OnPropertyChanged(nameof(CoBoLocHoaDon));

        InvoicePage = 1;
        await LoadInvoicesPagedAsync();
    }

    [RelayCommand]
    public async Task XoaBoLocHoaDonAsync()
    {
        InvoiceSearch = "";
        InvoiceTuNgay = null;
        InvoiceDenNgay = null;
        InvoiceTrangThaiLoc = "ALL";
        InvoicePhuongThucLoc = "ALL";

        OnPropertyChanged(nameof(IsInvoiceTrangThaiAll));
        OnPropertyChanged(nameof(IsInvoiceTrangThaiPaid));
        OnPropertyChanged(nameof(IsInvoiceTrangThaiDebt));
        OnPropertyChanged(nameof(IsInvoiceTrangThaiCancel));
        OnPropertyChanged(nameof(CoBoLocHoaDon));

        InvoicePage = 1;
        await LoadInvoicesPagedAsync();
    }

    [RelayCommand]
    public async Task LoadInvoicesPagedAsync()
    {
        if (IsLoadingInvoices)
            return;

        IsLoadingInvoices = true;
        StatusMessage = "Đang tải danh sách phiếu bán lẻ...";

        try
        {
            var (items, stats) = await _db.GetHoaDonPagedAsync(
                InvoiceSearch,
                InvoiceTuNgay,
                InvoiceDenNgay,
                InvoiceTrangThaiLoc,
                InvoicePhuongThucLoc,
                InvoicePage,
                InvoicePageSize);

            HoaDons.Clear();
            foreach (var item in items)
            {
                HoaDons.Add(item);
            }

            InvoiceTotalCount = stats.TongSoHoaDon;
            InvoiceTongTienHang = stats.TongTienHang;
            InvoiceTongGiamGia = stats.TongGiamGia;
            InvoiceTongDoanhThu = stats.TongDoanhThu;
            InvoiceTongDaThu = stats.TongDaThu;
            InvoiceTongCongNo = stats.TongCongNo;

            OnPropertyChanged(nameof(InvoiceTotalPages));
            OnPropertyChanged(nameof(InvoiceRangeText));
            OnPropertyChanged(nameof(InvoiceTongDoanhThuText));
            OnPropertyChanged(nameof(InvoiceTongDaThuText));
            OnPropertyChanged(nameof(InvoiceTongCongNoText));
            OnPropertyChanged(nameof(InvoiceTongTienHangText));
            OnPropertyChanged(nameof(InvoiceTongGiamGiaText));
            OnPropertyChanged(nameof(CanInvoiceTrangTruoc));
            OnPropertyChanged(nameof(CanInvoiceTrangSau));
            OnPropertyChanged(nameof(IsHoaDonsEmpty));
            OnPropertyChanged(nameof(HasHoaDons));

            StatusMessage = HoaDons.Count == 0
                ? "Không tìm thấy phiếu bán lẻ nào."
                : $"Đã tải {HoaDons.Count} phiếu bán lẻ (Trang {InvoicePage}/{Math.Max(1, InvoiceTotalPages)})";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi LoadInvoicesPagedAsync", ex);
            StatusMessage = "Không thể tải danh sách phiếu bán lẻ: " + ex.Message;
        }
        finally
        {
            IsLoadingInvoices = false;
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangDauAsync()
    {
        if (InvoicePage != 1)
        {
            InvoicePage = 1;
            await LoadInvoicesPagedAsync();
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangTruocAsync()
    {
        if (InvoicePage > 1)
        {
            InvoicePage--;
            await LoadInvoicesPagedAsync();
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangSauAsync()
    {
        if (InvoicePage < InvoiceTotalPages)
        {
            InvoicePage++;
            await LoadInvoicesPagedAsync();
        }
    }

    [RelayCommand]
    private async Task InvoiceTrangCuoiAsync()
    {
        if (InvoicePage != InvoiceTotalPages && InvoiceTotalPages > 0)
        {
            InvoicePage = InvoiceTotalPages;
            await LoadInvoicesPagedAsync();
        }
    }

    [RelayCommand]
    private async Task HuyHoaDonBanLeAsync(HoaDon? item)
    {
        if (item is null || item.Id == 0)
            return;

        if (item.DaHuy)
        {
            await ShowMessageAsync("Thông báo", $"Hóa đơn {item.MaHoaDon} đã ở trạng thái hủy.");
            return;
        }

        if (!await KiemTraQuyenAsync(PermissionKeys.SalesCancel, "Bạn không có quyền hủy hóa đơn."))
        {
            return;
        }

        var confirm = await ShowConfirmationAsync(
            "Hủy phiếu bán lẻ",
            $"Bạn có chắc chắn muốn hủy phiếu bán {item.MaHoaDon}?\n"
            + $"Toàn bộ hàng hóa trong hóa đơn sẽ được hoàn trả lại vào tồn kho, và công nợ (nếu có) sẽ được giảm.",
            "Xác nhận hủy",
            "Bỏ qua");

        if (!confirm)
            return;

        try
        {
            await _db.HuyHoaDonBanLeAsync(item.Id, "Hủy trực tiếp từ danh sách phiếu bán lẻ");

            // Nạp lại danh sách sản phẩm để thấy tồn kho đã được hoàn
            await RefreshProductsAsync();

            // Nạp lại danh sách hóa đơn
            await LoadInvoicesPagedAsync();

            StatusMessage = $"✅ Đã hủy phiếu bán lẻ {item.MaHoaDon} thành công. Hàng hóa đã được hoàn về tồn kho.";
            await NotificationWindow.ShowSuccessAsync($"Đã hủy thành công phiếu bán lẻ {item.MaHoaDon}.", owner: OwnerWindow);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi hủy hóa đơn", ex);
            StatusMessage = "❌ Không thể hủy hóa đơn: " + ex.Message;
            await ShowMessageAsync("Lỗi hủy hóa đơn", ex.Message);
        }
    }

    [RelayCommand]
    private async Task InHoaDonBanLeAsync(HoaDon? item)
    {
        if (item is null || item.Id == 0)
            return;

        await XemHoaDonAsync(item);
    }
}
