using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Kiểu phương thức thanh toán dùng trong báo cáo. Giá trị lấy nguyên văn từ
/// cột <c>phieu_chi.phuong_thuc</c> của sổ quỹ hiện có, không tự sinh thêm
/// phương thức mà database không có dữ liệu.
/// </summary>
public static class BaoCaoPhuongThucThanhToan
{
    public const string TienMat = "TIEN_MAT";
    public const string ChuyenKhoan = "CHUYEN_KHOAN";
    public const string Qr = "QR";
    public const string ViDienTu = "VI_DIEN_TU";
    public const string Khac = "KHAC";
    public const string KhongRoi = "KHONG_ROI";

    /// <summary>Đổi mã trong database sang tên tiếng Việt có dấu để hiển thị.</summary>
    public static string TenHienThi(string? ma)
    {
        var goc = (ma ?? "").Trim().ToUpperInvariant();

        return goc switch
        {
            TienMat => "Tiền mặt",
            ChuyenKhoan => "Chuyển khoản",
            Qr => "QR",
            ViDienTu => "Ví điện tử",
            Khac => "Phương thức khác",
            KhongRoi => "Phương thức khác",
            "" => "Phương thức khác",
            _ => goc
        };
    }
}

/// <summary>
/// Tổng kết bán hàng trong một ngày.
/// <para>
/// Quy tắc doanh thu giữ nguyên như màn Hóa đơn và màn Tổng quan:
/// chỉ tính hóa đơn <c>trang_thai_hoa_don = 'HOAN_THANH'</c>, doanh thu là
/// <c>khach_can_tra</c>, tiền thu là <c>khach_da_thanh_toan</c>, công nợ là
/// <c>con_no</c>. Tiền thối (<c>thu_khac</c>) không được tính vào doanh thu.
/// </para>
/// </summary>
public sealed class BaoCaoTongKetBanHang
{
    public int SoHoaDon { get; set; }

    public decimal TongTienHang { get; set; }

    public decimal GiamGia { get; set; }

    /// <summary>Doanh thu thuần = SUM(khach_can_tra).</summary>
    public decimal DoanhThu { get; set; }

    /// <summary>Tiền khách đã trả = SUM(khach_da_thanh_toan).</summary>
    public decimal DaThu { get; set; }

    /// <summary>Tiền còn nợ = SUM(con_no).</summary>
    public decimal ConNo { get; set; }

    /// <summary>Tiền thối đã trả khách = SUM(thu_khac). Không tính vào doanh thu.</summary>
    public decimal TienThua { get; set; }

    /// <summary>
    /// Tiền khách đưa = DaThu + TienThua. Chỉ để tham khảo, tuyệt đối không dùng
    /// làm doanh thu (hóa đơn 500.000, khách đưa 600.000 vẫn ra doanh thu 500.000).
    /// </summary>
    public decimal TienKhachDua => DaThu + TienThua;

    public int SoHoaDonDaThanhToan { get; set; }

    public int SoHoaDonConCongNo { get; set; }

    // --- Chuỗi hiển thị định sẵn để binding không cần StringFormat ---
    public string SoHoaDonHienThi => $"{SoHoaDon:N0}";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string DoanhThuChuThich => $"Tổng: {DoanhThu:N0} đ";

    public string TongTienHangHienThi => $"{TongTienHang:N0} đ";

    public string GiamGiaHienThi => $"{GiamGia:N0} đ";

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string DaThuHienThi => $"{DaThu:N0} đ";

    public string ConNoHienThi => $"{ConNo:N0} đ";

    public string TienThuaHienThi => $"{TienThua:N0} đ";

    public string TienKhachDuaHienThi => $"{TienKhachDua:N0} đ";

    public string SoHoaDonDaThanhToanHienThi => $"{SoHoaDonDaThanhToan:N0}";

    public string SoHoaDonConCongNoHienThi => $"{SoHoaDonConCongNo:N0}";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoHoaDonDaThanhToanChuThich => $"{SoHoaDonDaThanhToan:N0} hóa đơn đã thanh toán";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoHoaDonConCongNoChuThich => $"{SoHoaDonConCongNo:N0} hóa đơn còn nợ";

    /// <summary>
    /// Bất biến kiểm tra của báo cáo: DoanhThu = TongTienHang - GiamGia và
    /// DaThu + ConNo = DoanhThu. Dùng cho cảnh báo người dùng và cho test.
    /// </summary>
    public bool CoBaoDongDuLieu =>
        Math.Abs(DoanhThu - (TongTienHang - GiamGia)) > 0.01m
        || Math.Abs(DaThu + ConNo - DoanhThu) > 0.01m;
}

/// <summary>
/// Một dòng tổng hợp số tiền theo phương thức thanh toán.
/// <para>
/// <b>Cảnh báo ngữ nghĩa:</b> đây là số tiền <b>theo sổ quỹ</b>
/// (<c>phieu_chi.phuong_thuc</c>), KHÔNG phải doanh thu của hóa đơn. Probe thực tế
/// ngày 05/10/2026 cho thấy ba con số khác nhau: hóa đơn đã thu 1.054.000,
/// phiếu thu sổ quỹ 800.000 (trong đó có 250.000 thu không gắn hóa đơn như bán
/// giấy vụn), phiếu thu gắn hóa đơn 550.000. Vì vậy báo cáo không được trộn
/// số này vào doanh thu.
/// </para>
/// </summary>
public sealed class BaoCaoPhuongThucRow
{
    public string PhuongThuc { get; set; } = "";

    public string TenPhuongThucHienThi => BaoCaoPhuongThucThanhToan.TenHienThi(PhuongThuc);

    /// <summary>Số phiếu thu sổ quỹ theo phương thức này.</summary>
    public int SoGiaoDich { get; set; }

    public decimal SoTien { get; set; }

    /// <summary>
    /// Phần trong <see cref="SoTien"/> gắn với hóa đơn bán hàng. Chỉ dùng để tham
    /// khảo, không cộng vào doanh thu.
    /// </summary>
    public decimal SoTienTuBanHang { get; set; }

    public string SoGiaoDichHienThi => $"{SoGiaoDich:N0} phiếu";

    public string SoTienHienThi => $"{SoTien:N0} đ";

    public string SoTienTuBanHangHienThi => $"{SoTienTuBanHang:N0} đ";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoTienTuBanHangChuThich => $"gắn hóa đơn: {SoTienTuBanHang:N0} đ";

    /// <summary>Tỉ lệ % của phương thức này so với tổng thu của phương thức còn lại.</summary>
    public double TyLePhanTram { get; set; }

    public double TyLeBeRong => Math.Clamp(TyLePhanTram, 0, 100);
}

/// <summary>Một cột của biểu đồ doanh thu theo giờ trong ngày.</summary>
public sealed class BaoCaoDiemGio
{
    /// <summary>Chiều cao tối đa của vùng vẽ cột, dùng để quy đổi ra pixel.</summary>
    private const double ChieuCaoToiDa = 150;

    /// <summary>Bề rộng mỗi cột trong biểu đồ (px).</summary>
    private const double BeRongMacDinh = 34;

    /// <summary>Giờ 0..23 lấy từ database.</summary>
    public int Gio { get; set; }

    public decimal DoanhThu { get; set; }

    public int SoHoaDon { get; set; }

    public string GioHienThi => $"{Gio:00}:00";

    public string SoHoaDonHienThi => $"{SoHoaDon} hóa đơn";

    /// <summary>Chiều cao cột so với giờ cao nhất, 0..100.</summary>
    public double TyLeChieuCao { get; set; }

    public double ChieuCao => Math.Clamp(TyLeChieuCao, 0, 100);

    /// <summary>Quy đổi tỉ lệ % thành pixel chiều cao để bind thẳng vào Height.</summary>
    public double ChieuCaoCot => Math.Round(ChieuCao / 100d * ChieuCaoToiDa, 2);

    public double BeRongCot => BeRongMacDinh;

    public bool CoDoanhThu => DoanhThu > 0;

    /// <summary>Màu cột: xanh lá khi có doanh thu, xám nhạt khi giờ đó không có hóa đơn.</summary>
    public string MauCot => CoDoanhThu ? "#087F6E" : "#DDE9E5";

    public string ToolTip =>
        DoanhThu <= 0
            ? $"{GioHienThi} — không có hóa đơn"
            : $"{GioHienThi} — {DoanhThu:N0} đ ({SoHoaDon} hóa đơn)";
}

/// <summary>Một dòng "Top sản phẩm bán chạy trong ngày".</summary>
public sealed class BaoCaoSanPhamRow
{
    public int ThuTu { get; set; }

    public string Ten { get; set; } = "";

    /// <summary>Mã sản phẩm, lấy cùng nguồn với thẻ "Top hàng bán chạy" ở màn Tổng quan.</summary>
    public string MaSanPham { get; set; } = "";

    public decimal SoLuong { get; set; }

    /// <summary>
    /// Tiền hàng của dòng sản phẩm = SUM(chi_tiet.thanh_tien), tức <b>trước</b>
    /// giảm giá ở cấp hóa đơn. Vì vậy tổng cột này bằng <c>tong_tien_hang</c>
    /// (2.080.000 ngày 05/10/2026) chứ không bằng doanh thu (1.954.000). Không
    /// dùng cột này để đối chiếu với thẻ doanh thu.
    /// </summary>
    public decimal DoanhThu { get; set; }

    public double TyLePhanTram { get; set; }

    public double TyLeBeRong => Math.Clamp(TyLePhanTram, 0, 100);

    public string SoLuongHienThi => $"{SoLuong:N0}";

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string MaSanPhamHienThi => MaSanPham.Length > 0 ? MaSanPham : "-";
}

/// <summary>Một dòng "Top khách hàng trong ngày".</summary>
public sealed class BaoCaoKhachHangRow
{
    public int ThuTu { get; set; }

    public string Ten { get; set; } = "";

    public string MaKhachHang { get; set; } = "";

    /// <summary>Khách không gắn với hồ sơ (khách lẻ) thì không có mã.</summary>
    public bool LaKhachLe => MaKhachHang.Length == 0;

    public int SoHoaDon { get; set; }

    public decimal TongMua { get; set; }

    public decimal DaThanhToan { get; set; }

    public decimal ConNo { get; set; }

    public double TyLePhanTram { get; set; }

    public double TyLeBeRong => Math.Clamp(TyLePhanTram, 0, 100);

    public string SoHoaDonHienThi => $"{SoHoaDon:N0}";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoHoaDonChuThich => $"{SoHoaDon:N0} hóa đơn";

    public string TongMuaHienThi => $"{TongMua:N0} đ";

    public string DaThanhToanHienThi => $"{DaThanhToan:N0} đ";

    public string ConNoHienThi => $"{ConNo:N0} đ";
}

/// <summary>Số liệu tổng hợp về khách hàng trong ngày.</summary>
public sealed class BaoCaoKhachHangTongHop
{
    /// <summary>Số khách hàng (có hồ sơ) đã mua trong ngày.</summary>
    public int SoKhachDaMua { get; set; }

    /// <summary>Lần mua đầu tiên của khách rơi đúng vào ngày đang xem.</summary>
    public int SoKhachMoi { get; set; }

    /// <summary>Đã từng mua trước ngày đang xem rồi mua tiếp trong ngày đó.</summary>
    public int SoKhachQuayLai { get; set; }

    public decimal TongGiaTriMua { get; set; }

    public string SoKhachDaMuaHienThi => $"{SoKhachDaMua:N0}";

    public string SoKhachMoiHienThi => $"{SoKhachMoi:N0}";

    public string SoKhachQuayLaiHienThi => $"{SoKhachQuayLai:N0}";

    public string TongGiaTriMuaHienThi => $"{TongGiaTriMua:N0} đ";
}

/// <summary>
/// Một hóa đơn hoàn thành phát sinh trong ngày — vừa là dòng của bảng công nợ
/// khách hàng, vừa là dòng của sheet "Hóa đơn bán hàng".
/// <para>
/// <see cref="DoanhThu"/> lấy thẳng <c>khach_can_tra</c> từ database, không tự
/// trừ lại ở client, nên báo cáo luôn khớp với màn Hóa đơn.
/// </para>
/// </summary>
public sealed class BaoCaoCongNoRow
{
    public string TenKhachHang { get; set; } = "";

    public string MaHoaDon { get; set; } = "";

    /// <summary>Thời điểm bán đầy đủ để ghi vào Excel dạng DateTime.</summary>
    public DateTime ThoiGian { get; set; }

    /// <summary>Tổng tiền hàng trước giảm giá = <c>tong_tien_hang</c>.</summary>
    public decimal TongTienHang { get; set; }

    public decimal GiamGia { get; set; }

    /// <summary>Doanh thu = <c>khach_can_tra</c> (tiền khách phải trả).</summary>
    public decimal DoanhThu { get; set; }

    /// <summary>Tiền thối = <c>thu_khac</c>. Có thể bằng 0, không phải dữ liệu suy diễn.</summary>
    public decimal ThuKhac { get; set; }

    /// <summary>Tiền khách đã trả = <c>khach_da_thanh_toan</c>.</summary>
    public decimal DaTra { get; set; }

    /// <summary>Tiền còn nợ = <c>con_no</c>.</summary>
    public decimal ConNo { get; set; }

    /// <summary>Trạng thái lấy nguyên văn từ <c>trang_thai_thanh_toan</c>.</summary>
    public string TrangThai { get; set; } = "";

    public bool DaThanhToan => string.Equals(TrangThai, "DA_THANH_TOAN", StringComparison.Ordinal);

    public string TrangThaiHienThi => DaThanhToan ? "Đã thanh toán" : "Còn công nợ";

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM HH:mm");

    public string TongTienHangHienThi => $"{TongTienHang:N0} đ";

    public string GiamGiaHienThi => $"{GiamGia:N0} đ";

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string DaTraHienThi => $"{DaTra:N0} đ";

    public string ConNoHienThi => $"{ConNo:N0} đ";

    public string ThuKhacHienThi => $"{ThuKhac:N0} đ";

    public string MauTrangThai => DaThanhToan ? "#087F6E" : "#C27803";

    public string MauNenTrangThai => DaThanhToan ? "#EAF9F5" : "#FFF4E5";
}

/// <summary>Số liệu tổng hợp công nợ phát sinh trong ngày.</summary>
public sealed class BaoCaoCongNoTongHop
{
    public decimal TongCongNoPhatSinh { get; set; }

    public decimal TongDaTra { get; set; }

    public decimal TongConNo { get; set; }

    public int SoHoaDon { get; set; }

    public int SoHoaDonConNo { get; set; }

    public string TongCongNoPhatSinhHienThi => $"{TongCongNoPhatSinh:N0} đ";

    public string TongDaTraHienThi => $"{TongDaTra:N0} đ";

    public string TongConNoHienThi => $"{TongConNo:N0} đ";

    public string SoHoaDonHienThi => $"{SoHoaDon:N0}";

    public string SoHoaDonConNoHienThi => $"{SoHoaDonConNo:N0}";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string TongConNoChuThich => $"Còn nợ: {TongConNo:N0} đ";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoHoaDonConNoChuThich => $"{SoHoaDonConNo:N0} hóa đơn còn nợ";
}

/// <summary>
/// Một dòng thu hoặc chi của sổ quỹ trong ngày.
/// <para>
/// Nguồn dữ liệu là đúng bảng <c>phieu_chi</c> mà màn Sổ quỹ đang dùng: thu là
/// mã phiếu bắt đầu bằng "PT", chi là mã phiếu bắt đầu bằng "PC" — cùng quy tắc
/// với <c>DatabaseService.GetSoQuyTongHopTheoLocAsync</c>. Không tạo hệ sổ quỹ mới.
/// </para>
/// </summary>
public sealed class BaoCaoThuChiRow
{
    public DateTime ThoiGian { get; set; }

    /// <summary>"THU" | "CHI"</summary>
    public string Loai { get; set; } = "";

    public bool LaThu => string.Equals(Loai, "THU", StringComparison.Ordinal);

    public string NoiDung { get; set; } = "";

    public string MaChungTu { get; set; } = "";

    public string PhuongThuc { get; set; } = "";

    /// <summary>Số tiền luôn dương; dấu do <see cref="LaThu"/> quyết định.</summary>
    public decimal SoTien { get; set; }

    public string LoaiHienThi => LaThu ? "Thu" : "Chi";

    public string ThoiGianHienThi => ThoiGian.ToString("HH:mm", CultureInfo.InvariantCulture);

    public string SoTienHienThi => (LaThu ? "+" : "-") + $"{SoTien:N0}";

    public string SoTienDayDu => (LaThu ? "+" : "-") + $"{SoTien:N0} đ";

    public string PhuongThucHienThi => BaoCaoPhuongThucThanhToan.TenHienThi(PhuongThuc);

    public string MauSoTien => LaThu ? "#087F6E" : "#D64545";

    public string MauNenLoai => LaThu ? "#EAF9F5" : "#FDECEC";
}

/// <summary>Tổng hợp thu chi sổ quỹ trong ngày.</summary>
public sealed class BaoCaoSoQuyTongHop
{
    public decimal TongThu { get; set; }

    public decimal TongChi { get; set; }

    /// <summary>Số dư phát sinh trong ngày = Tổng thu - Tổng chi.</summary>
    public decimal ChenhLech { get; set; }

    public int SoPhieuThu { get; set; }

    public int SoPhieuChi { get; set; }

    public string TongThuHienThi => $"{TongThu:N0} đ";

    public string TongChiHienThi => $"{TongChi:N0} đ";

    public string ChenhLechHienThi => $"{ChenhLech:N0} đ";

    public string SoPhieuThuHienThi => $"{SoPhieuThu:N0}";

    public string SoPhieuChiHienThi => $"{SoPhieuChi:N0}";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoPhieuThuChuThich => $"{SoPhieuThu:N0} phiếu thu";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string SoPhieuChiChuThich => $"{SoPhieuChi:N0} phiếu chi";

    public string ChenhLechDauCham =>
        ChenhLech > 0 ? "+" : ChenhLech < 0 ? "-" : "";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string TongThuChuThich => $"Thu: {TongThu:N0} đ";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string TongChiChuThich => $"Chi: {TongChi:N0} đ";

    /// <summary>Đã định dạng sẵn để binding không cần StringFormat.</summary>
    public string ChenhLechChuThich => $"Chênh lệch: {ChenhLech:N0} đ";
}

/// <summary>Thống kê nhập hàng trong ngày.</summary>
public sealed class BaoCaoNhapHangTongHop
{
    public int SoPhieu { get; set; }

    public decimal TongTienNhap { get; set; }

    /// <summary>Tiền đã trả nhà cung cấp = SUM(da_thanh_toan).</summary>
    public decimal DaThanhToan { get; set; }

    /// <summary>Tiền còn nợ nhà cung cấp = SUM(con_no).</summary>
    public decimal ConNo { get; set; }

    public string SoPhieuHienThi => $"{SoPhieu:N0}";

    public string TongTienNhapHienThi => $"{TongTienNhap:N0} đ";

    public string DaThanhToanHienThi => $"{DaThanhToan:N0} đ";

    public string ConNoHienThi => $"{ConNo:N0} đ";
}

/// <summary>
/// Kết quả tải một lần của toàn bộ báo cáo cuối ngày. Mỗi nhóm là một câu SQL
/// độc lập; không có số liệu nào được hard-code trong lớp này.
/// </summary>
public sealed class BaoCaoCuoiNgayDuLieu
{
    /// <summary>Ngày được chọn (00:00:00).</summary>
    public DateTime Ngay { get; set; }

    /// <summary>Ngày kế tiếp, dùng làm mốc trên của điều kiện mở nửa khoảng.</summary>
    public DateTime NgayKeTiep { get; set; }

    public BaoCaoTongKetBanHang TongKetBanHang { get; set; } = new();

    public List<BaoCaoDiemGio> DoanhThuTheoGio { get; set; } = [];

    public List<BaoCaoPhuongThucRow> PhuongThucThanhToan { get; set; } = [];

    public List<BaoCaoSanPhamRow> TopSanPham { get; set; } = [];

    public List<BaoCaoKhachHangRow> TopKhachHang { get; set; } = [];

    public BaoCaoKhachHangTongHop KhachHangTongHop { get; set; } = new();

    public List<BaoCaoCongNoRow> CongNoPhatSinh { get; set; } = [];

    public BaoCaoCongNoTongHop CongNoTongHop { get; set; } = new();

    public List<BaoCaoThuChiRow> ThuChi { get; set; } = [];

    public BaoCaoSoQuyTongHop SoQuyTongHop { get; set; } = new();

    public BaoCaoNhapHangTongHop NhapHang { get; set; } = new();

    public string NgayHienThi => Ngay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Báo cáo có dữ liệu hay không: chỉ cần một phần bất rỗng trong ngày.
    /// Dùng để hiện/ẩn câu "không có phát sinh".
    /// </summary>
    public bool CoDuLieu =>
        TongKetBanHang.SoHoaDon > 0
        || SoQuyTongHop.SoPhieuThu > 0
        || SoQuyTongHop.SoPhieuChi > 0
        || NhapHang.SoPhieu > 0;

    /// <summary>Tên file xuất Excel theo yêu cầu: BaoCaoCuoiNgay_yyyyMMdd.xlsx</summary>
    public string TenFileXuat() => $"BaoCaoCuoiNgay_{Ngay:yyyyMMdd}.xlsx";
}