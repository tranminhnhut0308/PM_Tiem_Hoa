namespace ShopHoa.Models;

/// <summary>
/// Thông tin chi tiết một phiếu thu/chi trong sổ quỹ.
/// Nạp theo mã phiếu nên chỉ có đúng một bản ghi trong màn hình chi tiết.
/// </summary>
public class SoQuyChiTiet
{
    public string MaPhieu { get; set; } = "";

    /// <summary>true = phiếu thu, false = phiếu chi (suy ra từ tiền tố mã).</summary>
    public bool LaThu { get; set; }

    public string LoaiPhieu => LaThu ? "Phiếu thu" : "Phiếu chi";

    /// <summary>Nhãn đúng nghiệp vụ: "Người nộp" với phiếu thu, "Người nhận" với phiếu chi.</summary>
    public string NhanNguoiNopNhan => LaThu ? "Người nộp" : "Người nhận";

    public DateTime ThoiGian { get; set; }

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Người nộp (phiếu thu) hoặc người nhận (phiếu chi).</summary>
    public string NguoiNopNhan { get; set; } = "";

    public decimal SoTien { get; set; }

    /// <summary>Số tiền, không kèm dấu: dấu +/- đã có ở màu nhãn loại phiếu.</summary>
    public string SoTienHienThi => $"{SoTien:N0} đ";

    /// <summary>Nội dung phiếu (cột <c>noi_dung</c>).</summary>
    public string NoiDung { get; set; } = "";

    /// <summary>Ghi chú phiếu (cột <c>ghi_chu</c>).</summary>
    public string GhiChu { get; set; } = "";

    public string NguoiTao { get; set; } = "";

    public string TrangThai { get; set; } = "";

    public string TrangThaiHienThi => TrangThai switch
    {
        "HOAN_THANH" => "Đã thanh toán",
        "HUY" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(TrangThai) ? "Chưa có" : TrangThai
    };

    public bool DaHuy => TrangThai.Equals("HUY", StringComparison.OrdinalIgnoreCase);

    /// <summary>Tên loại thu/chi lấy từ bảng nhom_chi_phi (rỗng nếu phiếu chưa gán loại).</summary>
    public string TenLoai { get; set; } = "";

    /// <summary>
    /// Khóa ngoại tới nhom_chi_phi. Cần cho form sửa để chọn lại đúng loại đang
    /// gắn với phiếu; null nghĩa là phiếu chưa gắn nhóm nào.
    /// </summary>
    public uint? NhomChiPhiId { get; set; }

    /// <summary>Mã phương thức quỹ trong database (TIEN_MAT / CHUYEN_KHOAN...).</summary>
    public string PhuongThuc { get; set; } = "";

    /// <summary>Mã phương thức đã đổi sang nhãn tiếng Việt để hiển thị.</summary>
    public string PhuongThucHienThi => string.IsNullOrWhiteSpace(PhuongThuc)
        ? "Chưa ghi nhận"
        : PhuongThuc.Trim().ToUpperInvariant() switch
        {
            "TIEN_MAT" => "Tiền mặt",
            "CHUYEN_KHOAN" or "CHUYEN_KHOAN_NGAN_HANG" or "NGAN_HANG" => "Ngân hàng",
            "THE" or "THE_TIN_DUNG" => "Thẻ",
            "VI_DIEN_TU" or "VI" => "Ví điện tử",
            _ => PhuongThuc
        };

    /// <summary>Màu nhấn mạnh theo loại phiếu: thu xanh dương, chi đỏ.</summary>
    public string MauLoai => LaThu ? "#1877F2" : "#D92D20";

    public string MauNenLoai => LaThu ? "#EAF1FF" : "#FEF3F2";

    /// <summary>Màu nhãn trạng thái: đã hủy thì đỏ, còn lại xám.</summary>
    public string MauTrangThai => DaHuy ? "#D92D20" : "#667085";

    public string MauNenTrangThai => DaHuy ? "#FEF3F2" : "#F2F4F7";
}