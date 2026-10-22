using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // =========================================================
    // GIAO DIỆN 27 – THÊM PHIẾU BÁN LẺ (HELPERS & QUICK BUTTONS)
    // =========================================================

    /// <summary>
    /// Danh sách phương thức thanh toán chọn nhanh ngay trên màn bán hàng.
    /// <see cref="PhuongThucOption.Ma"/> là mã ghi vào <c>phieu_chi.phuong_thuc</c>.
    /// </summary>
    private static readonly IReadOnlyList<PhuongThucOption> _danhSachPhuongThuc = new[]
    {
        new PhuongThucOption { Ma = "TIEN_MAT", Ten = "Tiền mặt" },
        new PhuongThucOption { Ma = "CHUYEN_KHOAN", Ten = "Chuyển khoản" },
        new PhuongThucOption { Ma = "THE", Ten = "Thẻ" },
        new PhuongThucOption { Ma = "VI_DIEN_TU", Ten = "Ví điện tử" }
    };

    /// <summary>Nguồn dữ liệu cho ComboBox phương thức thanh toán trên màn bán hàng.</summary>
    public IReadOnlyList<PhuongThucOption> SalePhuongThucOptions => _danhSachPhuongThuc;

    /// <summary>
    /// Phương thức thanh toán đang chọn trên màn bán hàng.
    /// Mặc định là tiền mặt (chỉ phương thức này mới sinh tiền thối).
    /// </summary>
    [ObservableProperty]
    private PhuongThucOption? salePhuongThucThanhToan = _danhSachPhuongThuc[0];

    /// <summary>Tên phương thức thanh toán hiển thị cho người bán.</summary>
    public string SalePhuongThucThanhToanHienThi =>
        SalePhuongThucThanhToan?.Ten ?? "Tiền mặt";

    /// <summary>Đang thanh toán bằng tiền mặt (cho phép tính tiền thối).</summary>
    public bool LaThanhToanTienMat =>
        string.Equals(SalePhuongThucThanhToan?.Ma, "TIEN_MAT", StringComparison.Ordinal);

    /// <summary>
    /// Mã vạch vừa quét (USB barcode scanner gõ vào rồi nhấn Enter).
    /// </summary>
    [ObservableProperty]
    private string saleBarcodeInput = "";

    /// <summary>Thông báo ngắn hiển thị ngay dưới ô quét mã.</summary>
    [ObservableProperty]
    private string saleBarcodeStatus = "";

    /// <summary>Ô quét mã đang báo lỗi (không tìm thấy mã, hết hàng...).</summary>
    [ObservableProperty]
    private bool saleBarcodeCoLoi;

    /// <summary>Màu hiển thị dòng thông báo của ô quét mã (xanh = OK, đỏ = lỗi).</summary>
    public string SaleBarcodeStatusMau => SaleBarcodeCoLoi ? "#DC2626" : "#15803D";

    partial void OnSaleBarcodeCoLoiChanged(bool value)
    {
        OnPropertyChanged(nameof(SaleBarcodeStatusMau));
    }

    /// <summary>Số dòng hàng tặng kèm đang có trong giỏ.</summary>
    [ObservableProperty]
    private int saleSoDongHangTang;

    /// <summary>Tóm tắt chương trình khuyến mãi đang áp dụng (hiển thị trên giỏ).</summary>
    [ObservableProperty]
    private string saleThongTinKhuyenMai = "";

    /// <summary>Cảnh báo về hàng tặng (ví dụ tồn kho hàng tặng không đủ).</summary>
    [ObservableProperty]
    private string saleCanhBaoKhuyenMai = "";

    /// <summary>Giỏ có ít nhất một dòng hàng tặng (dùng để hiện nhãn "TẶNG KÈM").</summary>
    public bool SaleCoHangTang => SaleSoDongHangTang > 0;

    // Quy tắc khuyến mãi tặng kèm đã nạp từ database.
    private IReadOnlyList<KhuyenMaiTangKem> _quyTacTangKem = Array.Empty<KhuyenMaiTangKem>();
    private bool _dangDoiChieuKhuyenMai;
    private bool _dangQuetBarcode;

    /// <summary>
    /// Mã phiếu bán hiển thị xem trước (HDyyMMdd-NNN).
    /// </summary>
    public string SaleMaPhieuHienThi =>
        string.IsNullOrWhiteSpace(SaleMaHoaDonHienThi)
            ? $"HD{DateTime.Now:yyMMdd}-001"
            : SaleMaHoaDonHienThi;

    /// <summary>
    /// Ngày bán hiện tại định dạng dd/MM/yyyy HH:mm.
    /// </summary>
    public string SaleNgayBanHienThi => DateTime.Now.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Nhân viên lập phiếu bán lẻ (lấy từ tài khoản đăng nhập).
    /// </summary>
    public string SaleNhanVienHienThi => HoTenHienThi;

    /// <summary>
    /// Kho xuất bán lẻ (mặc định Kho Tổng).
    /// </summary>
    public string SaleKhoXuatHienThi => "Kho Tổng (Kho mặc định)";

    /// <summary>
    /// Tổng số lượng các sản phẩm có trong giỏ hàng.
    /// </summary>
    public decimal SaleTongSoLuongItem => SaleCart.Sum(i => i.SoLuong);

    /// <summary>
    /// Tổng số dòng mặt hàng trong giỏ bán.
    /// </summary>
    public int SaleTongSoMatHang => SaleCart.Count;

    /// <summary>
    /// Cảnh báo khách lẻ ghi nợ (khách lẻ không được nợ).
    /// </summary>
    public bool SaleCanhBaoKhachLeNo => LaKhachLe && TienCongNo > 0;

    /// <summary>
    /// Lệnh chọn nhanh tiền khách đưa từ các nút mệnh giá:
    /// 50.000, 100.000, 200.000, 500.000 hoặc "DU" (đúng số tiền).
    /// </summary>
    [RelayCommand]
    private void ChonTienKhachDuaNhanh(string? amountStr)
    {
        if (string.IsNullOrWhiteSpace(amountStr))
            return;

        if (!LaThanhToanTienMat)
        {
            StatusMessage = "Chỉ nhập tiền khách đưa khi thanh toán bằng tiền mặt.";
            return;
        }

        if (string.Equals(amountStr, "DU", StringComparison.OrdinalIgnoreCase))
        {
            TienKhachDua = Math.Max(0m, SaleTotal);
        }
        else if (decimal.TryParse(amountStr, out var val))
        {
            TienKhachDua = val;
        }

        RecalculatePayment();
        NotifyBanLeProperties();
    }

    /// <summary>
    /// Thông báo cập nhật các thuộc tính phụ thuộc của phiếu bán lẻ.
    /// </summary>
    public void NotifyBanLeProperties()
    {
        OnPropertyChanged(nameof(SaleMaPhieuHienThi));
        OnPropertyChanged(nameof(SaleNgayBanHienThi));
        OnPropertyChanged(nameof(SaleNhanVienHienThi));
        OnPropertyChanged(nameof(SaleKhoXuatHienThi));
        OnPropertyChanged(nameof(SaleTongSoLuongItem));
        OnPropertyChanged(nameof(SaleTongSoMatHang));
        OnPropertyChanged(nameof(SaleCanhBaoKhachLeNo));
        OnPropertyChanged(nameof(SalePhuongThucThanhToanHienThi));
        OnPropertyChanged(nameof(LaThanhToanTienMat));
        OnPropertyChanged(nameof(SaleCoHangTang));
    }

    // =========================================================
    // QUÉT MÃ VẠCH
    // =========================================================

    /// <summary>
    /// Quét mã vạch bằng máy quét USB: tìm sản phẩm theo mã và thêm vào giỏ.
    /// <para>
    /// Mã khớp CHÍNH XÁC (sau khi cắt khoảng trắng) với <c>san_pham.ma_vach</c>,
    /// nếu không có thì thử <c>san_pham.ma_san_pham</c>. Nếu một mã trỏ tới
    /// nhiều sản phẩm thì mở hộp thoại để người bán chọn đúng mặt hàng.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task QuetBarcodeAsync()
    {
        var code = (SaleBarcodeInput ?? "").Trim();

        if (code.Length == 0)
            return;

        if (_dangQuetBarcode)
            return;

        _dangQuetBarcode = true;

        try
        {
            SaleBarcodeCoLoi = false;

            var sanPhams = await _db.GetSanPhamTheoBarcodeAsync(code);

            if (sanPhams.Count == 0)
            {
                SaleBarcodeCoLoi = true;
                SaleBarcodeStatus = $"Không tìm thấy mã \"{code}\".";
                StatusMessage = $"⚠️ Không tìm thấy sản phẩm nào có mã vạch \"{code}\".";
                return;
            }

            var sanPham = sanPhams.Count == 1
                ? sanPhams[0]
                : await ChonSanPhamTheoBarcodeAsync(sanPhams, code);

            if (sanPham is null)
            {
                SaleBarcodeStatus = "Đã huỷ chọn sản phẩm.";
                SaleBarcodeCoLoi = false;
                return;
            }

            var themThanhCong = await ThemVaoGioHangCoreAsync(sanPham);

            if (!themThanhCong)
            {
                SaleBarcodeCoLoi = true;
                SaleBarcodeStatus = $"Không thêm được \"{sanPham.TenSanPham}\" vào giỏ.";
                return;
            }

            // Thêm thành công: xoá mã để máy quét mã tiếp theo không bị dính.
            SaleBarcodeInput = "";
            SaleBarcodeCoLoi = false;
            SaleBarcodeStatus = $"✓ Đã thêm {sanPham.TenSanPham}.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Quet ma vach ban hang", ex);

            SaleBarcodeCoLoi = true;
            SaleBarcodeStatus = "Lỗi khi quét mã: " + SafeLog.Redact(ex.Message);
            StatusMessage = "Lỗi khi quét mã vạch: " + SafeLog.Redact(ex.Message);
        }
        finally
        {
            _dangQuetBarcode = false;
        }
    }

    /// <summary>
    /// Mở hộp thoại cho người bán chọn mặt hàng khi một mã vạch trỏ tới nhiều sản phẩm.
    /// </summary>
    private async Task<SanPham?> ChonSanPhamTheoBarcodeAsync(IReadOnlyList<SanPham> sanPhams, string maVach)
    {
        if (OwnerWindow is null)
            return sanPhams.FirstOrDefault();

        try
        {
            return await ChonSanPhamBarcodeWindow.ShowAsync(OwnerWindow, sanPhams, maVach);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Chon san pham theo ma vach", ex);
            return null;
        }
    }

    // =========================================================
    // KHUYẾN MÃI TẶNG KÈM
    // =========================================================

    /// <summary>
    /// Nạp quy tắc khuyến mãi tặng kèm đang hiệu lực từ database.
    /// </summary>
    private async Task TaiQuyTacTangKemAsync()
    {
        try
        {
            _quyTacTangKem = await _db.GetQuyTacTangKemAsync();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Tai quy tac khuyen mai tang kem", ex);
            _quyTacTangKem = Array.Empty<KhuyenMaiTangKem>();
        }

        CapNhatHangTangTrongGio();
    }

    /// <summary>
    /// Đối chiếu giỏ hàng với các quy tắc khuyến mãi tặng kèm và dựng lại
    /// các dòng hàng tặng.
    /// <para>
    /// Số lượng hàng tặng = floor(SL mua / ngưỡng mua) × SL tặng. Hàng tặng có
    /// đơn giá 0 nên không tính doanh thu, không được sửa số lượng tay và không
    /// được xoá riêng — muốn bỏ thì sửa dòng hàng mua.
    /// </para>
    /// </summary>
    private void CapNhatHangTangTrongGio()
    {
        if (_dangDoiChieuKhuyenMai)
            return;

        _dangDoiChieuKhuyenMai = true;

        try
        {
            // 1. Gỡ toàn bộ dòng hàng tặng cũ (số lượng do chương trình quyết định).
            foreach (var dongTang in SaleCart.Where(i => i.IsHangTang).ToList())
            {
                UnhookDongGio(dongTang);
                SaleCart.Remove(dongTang);
            }

            var canhBao = new List<string>();
            var moTa = new List<string>();

            if (_quyTacTangKem.Count > 0)
            {
                var nhomKhach = SelectedSaleCustomer?.NhomKhachHangId;
                var dongBan = SaleCart.Where(i => !i.IsHangTang).ToList();

                foreach (var quyTac in _quyTacTangKem)
                {
                    if (quyTac.SanPhamTang is null)
                        continue;

                    if (quyTac.SoLuongMua <= 0 || quyTac.SoLuongTang <= 0)
                        continue;

                    // Chương trình giới hạn nhóm khách hàng thì chỉ áp dụng cho nhóm đó.
                    if (quyTac.NhomKhachHangId.HasValue && quyTac.NhomKhachHangId.Value != nhomKhach)
                        continue;

                    foreach (var item in dongBan.Where(i => i.SanPham.Id == quyTac.SanPhamId))
                    {
                        var soLan = (int)Math.Floor(item.SoLuong / quyTac.SoLuongMua);

                        if (soLan <= 0)
                            continue;

                        var soLuongTang = soLan * quyTac.SoLuongTang;

                        if (soLuongTang <= 0)
                            continue;

                        // Không tặng vượt quá tồn kho thực tế của hàng tặng.
                        var tonKhoTang = quyTac.SanPhamTang.TonKho;

                        if (soLuongTang > tonKhoTang)
                        {
                            soLuongTang = tonKhoTang;
                            canhBao.Add(
                                $"Hàng tặng \"{quyTac.TenSanPhamTang}\" chỉ còn {tonKhoTang:N0} trong kho nên không tặng đủ.");
                        }

                        if (soLuongTang <= 0)
                        {
                            canhBao.Add($"Hàng tặng \"{quyTac.TenSanPhamTang}\" đã hết hàng.");
                            continue;
                        }

                        var dongTang = new SaleCartItem
                        {
                            SanPham = quyTac.SanPhamTang,
                            SoLuong = soLuongTang,
                            IsHangTang = true,
                            ChuongTrinhKhuyenMaiId = quyTac.ChuongTrinhId,
                            ChuongTrinhKhuyenMaiTen = quyTac.TenChuongTrinh,
                            SanPhamIdMua = quyTac.SanPhamId,
                            SoLuongMua = item.SoLuong,
                            GhiChu = $"Hàng tặng kèm - {quyTac.MoTa}"
                        };

                        HookDongGio(dongTang);
                        SaleCart.Add(dongTang);

                        moTa.Add(string.IsNullOrWhiteSpace(quyTac.MoTa)
                            ? quyTac.TenChuongTrinh
                            : quyTac.MoTa);
                    }
                }
            }

            SaleSoDongHangTang = SaleCart.Count(i => i.IsHangTang);
            SaleThongTinKhuyenMai = string.Join(" • ", moTa.Distinct());
            SaleCanhBaoKhuyenMai = string.Join(" ", canhBao.Distinct());

            OnPropertyChanged(nameof(SaleCoHangTang));
            OnPropertyChanged(nameof(SaleTongSoLuongItem));
            OnPropertyChanged(nameof(SaleTongSoMatHang));
        }
        finally
        {
            _dangDoiChieuKhuyenMai = false;
        }
    }

    /// <summary>Gắn sự kiện theo dõi số lượng cho một dòng giỏ.</summary>
    private void HookDongGio(SaleCartItem item)
    {
        item.QuantityChanged += RecalculateSaleTotal;
        item.QuantityChanged += CapNhatHangTangTrongGio;
    }

    /// <summary>Gỡ sự kiện theo dõi số lượng cho một dòng giỏ.</summary>
    private void UnhookDongGio(SaleCartItem item)
    {
        item.QuantityChanged -= RecalculateSaleTotal;
        item.QuantityChanged -= CapNhatHangTangTrongGio;
    }

    /// <summary>
    /// Đổi phương thức thanh toán: với phương thức không phải tiền mặt thì
    /// tiền khách đưa mặc định bằng đúng số tiền phải trả và không sinh tiền thối.
    /// </summary>
    partial void OnSalePhuongThucThanhToanChanged(PhuongThucOption? value)
    {
        OnPropertyChanged(nameof(SalePhuongThucThanhToanHienThi));
        OnPropertyChanged(nameof(LaThanhToanTienMat));

        if (!LaThanhToanTienMat)
        {
            TienKhachDua = Math.Max(0m, SaleTotal);
        }
        else
        {
            TienKhachDua = 0;
        }

        RecalculatePayment();
        NotifyBanLeProperties();
    }
}
