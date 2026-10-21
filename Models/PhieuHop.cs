using ShopHoa.Helpers;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng phiếu trong danh sách hợp nhất Nhập hàng / Xuất hàng.
/// Một hàng có thể đến từ nhiều bảng vật lý khác nhau nên cần thuộc tính
/// <see cref="NguonPhieu"/> để biết phiếu đó thuộc nhóm nghiệp vụ nào.
/// </summary>
public class PhieuHopRow
{
    public uint Id { get; set; }

    public string MaPhieu { get; set; } = "";

    public DateTime ThoiGian { get; set; }

    /// <summary>Mã loại phiếu (chuẩn hoá, ví dụ NHAP_TU_NCC, XUAT_CHUYEN_KHO).</summary>
    public string LoaiPhieu { get; set; } = "";

    /// <summary>Nhà cung cấp / đối tượng liên quan của phiếu.</summary>
    public string DoiTuong { get; set; } = "";

    /// <summary>Kho xuất - kho nhận hoặc bộ phận nhận.</summary>
    public string Kho { get; set; } = "";

    public decimal TongSoLuong { get; set; }

    public decimal TongTien { get; set; }

    public string TrangThai { get; set; } = "";

    public string GhiChu { get; set; } = "";

    /// <summary>Tên bảng vật lý chứa phiếu: phieu_nhap_hang, phieu_nhap_khac, ...</summary>
    public string NguonPhieu { get; set; } = "";

    /// <summary>Số thứ tự hiển thị trong danh sách (gán khi nạp dữ liệu).</summary>
    public int SoThuTu { get; set; }

    /// <summary>Công nợ còn phải trả (chỉ có ý nghĩa với phiếu nhập từ nhà cung cấp).</summary>
    public decimal ConNo { get; set; }

    public string ThoiGianText => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    public string LoaiPhieuText => PhieuHopLabels.TenLoai(LoaiPhieu);

    public string TongTienText => MoneyInput.Format(TongTien) + " đ";

    public string TongSoLuongText => $"{TongSoLuong:N0} SP";

    public string TrangThaiText => PhieuHopLabels.TenTrangThai(TrangThai);

    public bool IsDaHuy => string.Equals(TrangThai, "DA_HUY", StringComparison.OrdinalIgnoreCase);

    public bool IsChoThanhToan => string.Equals(TrangThai, "CHUA_THANH_TOAN", StringComparison.OrdinalIgnoreCase);

    /// <summary>True khi đây là phiếu nhập từ NCC còn công nợ thì được thanh toán.</summary>
    public bool CoTheThanhToan =>
        string.Equals(NguonPhieu, PhieuHopLabels.NGUON_NHAP_HANG, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(TrangThai, "LUU_TAM", StringComparison.OrdinalIgnoreCase)
        && ConNo > 0m;

    /// <summary>True khi phiếu đến từ bảng phieu_nhap_hang (có thể thanh toán / in phiếu).</summary>
    public bool IsPhieuNhapHang =>
        string.Equals(NguonPhieu, PhieuHopLabels.NGUON_NHAP_HANG, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Một dòng chi tiết mặt hàng của phiếu hợp nhất (dùng chung cho cả nhập và xuất).
/// </summary>
public class PhieuHopChiTiet
{
    public string MaSanPham { get; set; } = "";

    public string TenSanPham { get; set; } = "";

    public string DonViTinh { get; set; } = "";

    public decimal SoLuong { get; set; }

    public decimal DonGia { get; set; }

    public decimal ThanhTien { get; set; }

    public string GhiChu { get; set; } = "";

    public string SoLuongText => $"{SoLuong:N0} {DonViTinh}".Trim();

    public string DonGiaText => MoneyInput.Format(DonGia) + " đ";

    public string ThanhTienText => MoneyInput.Format(ThanhTien) + " đ";
}

/// <summary>
/// Danh mục mã loại phiếu và nhãn hiển thị dùng chung cho cả bộ chọn loại phiếu
/// khi tạo đơn lẫn cột "Loại phiếu" trong danh sách hợp nhất.
/// </summary>
public static class PhieuHopLabels
{
    // ---------------- Nhập hàng ----------------
    public const string NHAP_TU_NCC = "NHAP_TU_NCC";
    public const string NHAP_NOI_BO = "NHAP_NOI_BO";
    public const string NHAP_DIEU_CHINH = "NHAP_DIEU_CHINH";
    public const string NHAP_KHUYEN_MAI = "NHAP_KHUYEN_MAI";
    public const string NHAP_THU_HOI = "NHAP_THU_HOI";
    public const string NHAP_KHAC = "NHAP_KHAC";

    // ---------------- Xuất hàng ----------------
    public const string XUAT_HAO_HUT = "XUAT_HUY";
    public const string XUAT_NOI_BO = "XUAT_NOI_BO";
    public const string XUAT_CAP_PHAT = "XUAT_CAP_PHAT";
    public const string XUAT_SU_DUNG = "XUAT_SU_DUNG";
    public const string XUAT_CHUYEN_KHO = "XUAT_CHUYEN_KHO";
    public const string XUAT_TRA_NCC = "XUAT_TRA_NCC";
    public const string XUAT_KHAC = "XUAT_KHAC";

    // ---------------- Nguồn phiếu gốc (tên bảng vật lý) ----------------
    public const string NGUON_NHAP_HANG = "phieu_nhap_hang";
    public const string NGUON_NHAP_KHAC = "phieu_nhap_khac";
    public const string NGUON_XUAT_HANG = "phieu_xuat_hang";
    public const string NGUON_XUAT_NOI_BO = "phieu_xuat_noi_bo";
    public const string NGUON_XUAT_TRA_NCC = "phieu_xuat_tra_ncc";

    /// <summary>Các loại phiếu nhập có thể chọn khi tạo đơn (theo thứ tự hiển thị).</summary>
    public static IReadOnlyList<(string Ma, string Ten)> DanhSachLoaiNhap { get; } =
    [
        (NHAP_TU_NCC, "Nhập từ nhà cung cấp"),
        (NHAP_NOI_BO, "Nhập nội bộ"),
        (NHAP_DIEU_CHINH, "Nhập điều chỉnh kiểm kê"),
        (NHAP_KHUYEN_MAI, "Nhập khuyến mãi"),
        (NHAP_THU_HOI, "Nhập thu hồi"),
        (NHAP_KHAC, "Nhập khác")
    ];

    /// <summary>Các loại phiếu xuất có thể chọn khi tạo đơn (theo thứ tự hiển thị).</summary>
    public static IReadOnlyList<(string Ma, string Ten)> DanhSachLoaiXuat { get; } =
    [
        (XUAT_HAO_HUT, "Xuất hủy / hao hụt / hết hạn"),
        (XUAT_NOI_BO, "Xuất sử dụng nội bộ / trưng bày"),
        (XUAT_CHUYEN_KHO, "Xuất chuyển kho / chi nhánh"),
        (XUAT_TRA_NCC, "Xuất trả nhà cung cấp"),
        (XUAT_KHAC, "Xuất khác")
    ];

    /// <summary>
    /// Các loại phiếu xuất dùng cho bộ lọc danh sách (bao gồm thêm các loại chỉ có
    /// trong bảng phieu_xuat_noi_bo, không tạo mới được từ giao diện).
    /// </summary>
    public static IReadOnlyList<(string Ma, string Ten)> DanhSachLoaiXuatLoc { get; } =
    [
        (XUAT_HAO_HUT, "Xuất hủy / hao hụt / hết hạn"),
        (XUAT_NOI_BO, "Xuất sử dụng nội bộ / trưng bày"),
        (XUAT_CAP_PHAT, "Xuất cấp phát nội bộ"),
        (XUAT_SU_DUNG, "Xuất sử dụng cho bộ phận"),
        (XUAT_CHUYEN_KHO, "Xuất chuyển kho / chi nhánh"),
        (XUAT_TRA_NCC, "Xuất trả nhà cung cấp"),
        (XUAT_KHAC, "Xuất khác")
    ];

    private static readonly Dictionary<string, string> _tenLoai = BuildTenLoai();

    private static Dictionary<string, string> BuildTenLoai()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (ma, ten) in DanhSachLoaiNhap)
            dict[ma] = ten;

        foreach (var (ma, ten) in DanhSachLoaiXuat)
            dict[ma] = ten;

        foreach (var (ma, ten) in DanhSachLoaiXuatLoc)
            dict[ma] = ten;

        // Bổ sung các mã lịch sử có thể xuất hiện trong CSDL.
        dict["XUAT_HUY_HONG"] = "Xuất hủy / hao hụt / hết hạn";

        return dict;
    }

    public static string TenLoai(string? ma) =>
        !string.IsNullOrWhiteSpace(ma) && _tenLoai.TryGetValue(ma.Trim(), out var ten)
            ? ten
            : "Phiếu khác";

    public static string TenTrangThai(string? ma) =>
        (ma ?? "").Trim().ToUpperInvariant() switch
        {
            "DA_NHAP_HANG" => "Đã nhập hàng",
            "DA_XUAT_KHO" => "Đã xuất kho",
            "HOAN_THANH" => "Hoàn thành",
            "DA_HUY" => "Đã hủy",
            "LUU_TAM" => "Lưu tạm",
            "CHUA_THANH_TOAN" => "Chưa thanh toán",
            "DA_THANH_TOAN" => "Đã thanh toán",
            _ => "Hoàn thành"
        };
}
