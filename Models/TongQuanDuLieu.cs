using System.Globalization;

namespace ShopHoa.Models;

/// <summary>
/// Khoảng thời gian dùng để lọc số liệu Dashboard. Giá trị <c>Key</c> được lưu
/// vào ViewModel và truyền xuống SQL nên phải khớp với tên cột trong
/// <see cref="TongQuanKhoangThoiGian"/>.
/// </summary>
public static class TongQuanKhoangThoiGian
{
    public const string HomNay = "HOM_NAY";
    public const string BayNgay = "7_NGAY";
    public const string ThangNay = "THANG_NAY";
    public const string ThangTruoc = "THANG_TRUOC";
    public const string NamNay = "NAM_NAY";
    public const string TuDien = "TU_DIEN";

    public static readonly IReadOnlyList<string> TatCa =
    [
        HomNay,
        BayNgay,
        ThangNay,
        ThangTruoc,
        NamNay,
        TuDien
    ];

    /// <summary>Chuỗi hiển thị trên nút bộ lọc.</summary>
    public static string TenHienThi(string key) => key switch
    {
        HomNay => "Hôm nay",
        BayNgay => "7 ngày",
        ThangNay => "Tháng này",
        ThangTruoc => "Tháng trước",
        NamNay => "Năm nay",
        TuDien => "Tùy chọn",
        _ => "Hôm nay"
    };
}

/// <summary>Cách gom dữ liệu doanh thu trên biểu đồ.</summary>
public static class TongQuanChieuDoanhThu
{
    public const string TheoNgay = "NGAY";
    public const string TheoGio = "GIO";
    public const string TheoThu = "THU";

    public static readonly IReadOnlyList<string> TatCa = [TheoNgay, TheoGio, TheoThu];

    public static string TenHienThi(string key) => key switch
    {
        TheoGio => "Theo giờ",
        TheoThu => "Theo thứ",
        _ => "Theo ngày"
    };
}

/// <summary>Kiểu xếp hạng của các thẻ "Top 10".</summary>
public static class TongQuanXepHang
{
    public const string TheoSoLuong = "SO_LUONG";
    public const string TheoDoanhThu = "DOANH_THU";

    public static readonly IReadOnlyList<string> TatCa = [TheoSoLuong, TheoDoanhThu];

    public static string TenHienThi(string key) =>
        key == TheoDoanhThu ? "Theo doanh thu" : "Theo số lượng";
}

/// <summary>Một điểm dữ liệu trên biểu đồ doanh thu.</summary>
public sealed class TongQuanDiemDoanhThu
{
    /// <summary>Nhãn hiển thị trên trục X (ví dụ "01/10", "14:00", "Thứ 2").</summary>
    public string Nhan { get; set; } = "";

    /// <summary>Nhãn đầy đủ dùng cho tooltip (ví dụ "Thứ hai 01/10/2026").</summary>
    public string NhanDayDu { get; set; } = "";

    public decimal DoanhThu { get; set; }

    public int SoHoaDon { get; set; }

    public bool LaDiemCaoNhat { get; set; }

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string SoHoaDonHienThi => $"{SoHoaDon:N0} hóa đơn";

    public string DoanhThuNganGon
    {
        get
        {
            if (DoanhThu <= 0) return "";
            if (DoanhThu >= 1_000_000_000m) return $"{DoanhThu / 1_000_000_000m:0.#} tỷ";
            if (DoanhThu >= 1_000_000m) return $"{DoanhThu / 1_000_000m:0.##} tr";
            if (DoanhThu >= 1_000m) return $"{DoanhThu / 1_000m:0.#}k";
            return $"{DoanhThu:N0}";
        }
    }

    public string SoHoaDonNganGon => SoHoaDon > 0 ? $"{SoHoaDon:N0} HĐ" : "";

    /// <summary>
    /// Tỉ lệ chiều cao cột so với giá trị lớn nhất. Database đã chia tỉ lệ sẵn
    /// để tránh phải load cả danh sách ra RAM rồi tính lại ở client.
    /// </summary>
    public double TyLeChieuCao { get; set; }

    /// <summary>Bề rộng cột (0..100) dùng chung cho cả chế độ giờ và ngày.</summary>
    public double TyLeBeRong { get; set; }

    /// <summary>Bề rộng cột tùy chỉnh theo góc nhìn (ngày/giờ/thứ).</summary>
    public double BeRongCotTuyChinh { get; set; }

    /// <summary>Chiều cao tối đa của vùng vẽ cột (px) — XAML lấy hằng này làm mốc.</summary>
    public const double ChieuCaoToiDa = 150;

    /// <summary>Bề rộng tối đa của một cột (px) để các cột không giãn quá rộng.</summary>
    public const double BeRongToiDa = 90;

    /// <summary>Chiều cao cột tính bằng pixel để XAML bind thẳng, không cần converter.</summary>
    public double ChieuCaoCot => DoanhThu <= 0 ? 4 : Math.Max(10, TyLeChieuCao / 100d * ChieuCaoToiDa);

    /// <summary>Bề rộng cột (px); nhiều điểm thì cuộn ngang trong vùng biểu đồ.</summary>
    public double BeRongCot => BeRongCotTuyChinh > 0
        ? BeRongCotTuyChinh
        : Math.Max(55, Math.Min(BeRongToiDa, TyLeBeRong / 100d * BeRongToiDa));

    public bool CoDuLieu => SoHoaDon > 0 || DoanhThu > 0;

    public string MauCot => !CoDuLieu
        ? "#E2ECE8"
        : (LaDiemCaoNhat ? "#047857" : "#0D9488");

    public string MauChuNhan => LaDiemCaoNhat
        ? "#047857"
        : (CoDuLieu ? "#163F38" : "#8A9B95");

    /// <summary>Tooltip đầy đủ khi rê chuột lên cột.</summary>
    public string ToolTipDayDu =>
        $"{NhanDayDu}\n" +
        $"Doanh thu: {DoanhThuHienThi}\n" +
        $"Số hóa đơn: {SoHoaDonHienThi}";
}

/// <summary>Một dòng trong thẻ "Top 10" (hàng bán chạy hoặc khách hàng).</summary>
public sealed class TongQuanHangTop
{
    public int ThuTu { get; set; }

    public string Ten { get; set; } = "";

    public string TenPhu { get; set; } = "";

    public decimal SoLuong { get; set; }

    public decimal DoanhThu { get; set; }

    /// <summary>Tỉ lệ bề rộng thanh ngang (0..100) so với dòng lớn nhất.</summary>
    public double TyLePhanTram { get; set; }

    public double TyLeBeRong => Math.Clamp(TyLePhanTram, 0, 100);

    public string DoanhThuHienThi => $"{DoanhThu:N0} đ";

    public string SoLuongHienThi => $"{SoLuong:N0}";
}

/// <summary>Một dòng nhật ký hoạt động của tài khoản con (dành cho Admin trên Dashboard).</summary>
public sealed class TongQuanHoatDongTaiKhoanCon
{
    public ulong Id { get; set; }
    public uint? NguoiDungId { get; set; }
    public string TenDangNhap { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string? DoiTuong { get; set; }
    public string? MaChungTu { get; set; }
    public string? NoiDung { get; set; }
    public decimal? GiaTri { get; set; }
    public DateTime ThoiGian { get; set; }

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");
    public string GioHienThi => ThoiGian.ToString("HH:mm");
    public string NgayHienThi => ThoiGian.ToString("dd/MM");
    public string GiaTriHienThi => (GiaTri.HasValue && GiaTri.Value != 0)
        ? $"{Math.Abs(GiaTri.Value):N0} đ"
        : "";

    public string NguoiThucHienHienThi => !string.IsNullOrWhiteSpace(HoTen)
        ? $"{HoTen} ({TenDangNhap})"
        : (!string.IsNullOrWhiteSpace(TenDangNhap) ? TenDangNhap : "Tài khoản");

    public string VaiTroHienThi => !string.IsNullOrWhiteSpace(VaiTro) ? VaiTro : "Nhân viên";

    public string MauBadgeVaiTro => VaiTro switch
    {
        "QUAN_LY" or "Quản lý" => "#6D28D9",
        "THU_NGAN" or "Thu ngân" => "#0284C7",
        "KHO" or "Kho" => "#D97706",
        _ => "#4B5563"
    };

    public string MauNenBadgeVaiTro => VaiTro switch
    {
        "QUAN_LY" or "Quản lý" => "#F3E8FF",
        "THU_NGAN" or "Thu ngân" => "#E0F2FE",
        "KHO" or "Kho" => "#FEF3C7",
        _ => "#F3F4F6"
    };

    public string MauBadgeHanhDong => HanhDong switch
    {
        "BAN_HANG" or "Bán hàng" => "#047857",
        "NHAP_HANG" or "Nhập hàng" => "#C2410C",
        "THU_TIEN" or "Thu tiền" => "#0284C7",
        "CHI_TIEN" or "Chi tiền" => "#B91C1C",
        "SO_QUY" or "Sổ quỹ" => "#1D4ED8",
        "HUY_DON" or "HỦY" or "Hủy hóa đơn" or "Hủy sổ quỹ" => "#B91C1C",
        "DANG_NHAP" or "Đăng nhập" => "#4338CA",
        _ => "#0F766E"
    };

    public string MauNenBadgeHanhDong => HanhDong switch
    {
        "BAN_HANG" or "Bán hàng" => "#ECFDF5",
        "NHAP_HANG" or "Nhập hàng" => "#FFF7ED",
        "THU_TIEN" or "Thu tiền" => "#E0F2FE",
        "CHI_TIEN" or "Chi tiền" => "#FEF2F2",
        "SO_QUY" or "Sổ quỹ" => "#EFF6FF",
        "HUY_DON" or "HỦY" or "Hủy hóa đơn" or "Hủy sổ quỹ" => "#FEF2F2",
        "DANG_NHAP" or "Đăng nhập" => "#EEF2FF",
        _ => "#F0FDFA"
    };
}

/// <summary>Thống kê kho lấy trực tiếp từ database, không suy diễn ở client.</summary>
public sealed class TongQuanThongKeKho
{
    public int TongSoHangHoa { get; set; }

    public int HangSapHet { get; set; }

    public int HangHetHang { get; set; }

    public int TongSoKhachHang { get; set; }

    public int TongSoNhaCungCap { get; set; }

    public decimal GiaTriTonKho { get; set; }

    public string GiaTriTonKhoHienThi => $"{GiaTriTonKho:N0} đ";
}

/// <summary>Kết quả tải một lần của toàn bộ Dashboard.</summary>
public sealed class TongQuanDuLieu
{
    // --- 5 thẻ "Kết quả bán hàng hôm nay" ---
    public decimal DoanhThuHomNay { get; set; }

    public int SoHoaDonHomNay { get; set; }

    public decimal DaThuHomNay { get; set; }

    public decimal ConNoHomNay { get; set; }

    public int SoDonHangHomNay { get; set; }

    // --- Biểu đồ doanh thu theo bộ lọc đang chọn ---
    public decimal TongDoanhThuKyChon { get; set; }

    public int TongSoHoaDonKyChon { get; set; }

    public List<TongQuanDiemDoanhThu> BieuDoDoanhThu { get; set; } = [];

    // --- Phân tích chuyên sâu theo Góc nhìn (Theo ngày / Theo giờ / Theo thứ) ---
    public string TieuDeDiemCaoNhat { get; set; } = "MỐC CAO ĐIỂM NHẤT";
    public string GiaTriDiemCaoNhat { get; set; } = "Chưa có phát sinh";
    public string PhuDeDiemCaoNhat { get; set; } = "0 đ • 0 hóa đơn";

    public string TieuDeTrungBinhChieu { get; set; } = "DOANH THU TRUNG BÌNH";
    public string GiaTriTrungBinhChieu { get; set; } = "0 đ";
    public string PhuDeTrungBinhChieu { get; set; } = "Trung bình theo kỳ";

    public string TieuDeMatDoChieu { get; set; } = "MẬT ĐỘ GIAO DỊCH";
    public string GiaTriMatDoChieu { get; set; } = "0";
    public string PhuDeMatDoChieu { get; set; } = "Chưa có đơn hoàn thành";

    // --- Hoạt động tài khoản con (dành riêng cho Admin) ---
    public List<TongQuanHoatDongTaiKhoanCon> HoatDongTaiKhoanCon { get; set; } = [];

    // --- Top 10 ---
    public List<TongQuanHangTop> TopHangBanChay { get; set; } = [];

    public List<TongQuanHangTop> TopKhachHang { get; set; } = [];

    // --- Thống kê kho ---
    public TongQuanThongKeKho ThongKeKho { get; set; } = new();

    // --- Chuỗi hiển thị đã định dạng sẵn để binding không cần StringFormat ---
    public string DoanhThuHomNayHienThi => $"{DoanhThuHomNay:N0} đ";

    public string SoHoaDonHomNayHienThi => $"{SoHoaDonHomNay:N0}";

    public string DaThuHomNayHienThi => $"{DaThuHomNay:N0} đ";

    public string ConNoHomNayHienThi => $"{ConNoHomNay:N0} đ";

    public string SoDonHangHomNayHienThi => $"{SoDonHangHomNay:N0}";

    public string TongDoanhThuKyChonHienThi => $"{TongDoanhThuKyChon:N0} đ";

    public string TongSoHoaDonKyChonHienThi => $"{TongSoHoaDonKyChon:N0} hóa đơn";

    public string ThoiGianHienThi { get; set; } = "";

    public static string NgayHienThi(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}