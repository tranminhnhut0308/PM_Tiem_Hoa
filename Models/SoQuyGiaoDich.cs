using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShopHoa.Models;

/// <summary>
/// Một dòng giao dịch hiển thị trong bảng Sổ quỹ.
/// Dữ liệu nạp từ bảng <c>phieu_chi</c> (dùng chung cho cả phiếu thu và phiếu chi),
/// loại phiếu phân biệt bằng tiền tố mã PTxxxx / PCxxxx.
/// </summary>
public partial class SoQuyGiaoDich : ObservableObject
{
    public string MaPhieu { get; set; } = "";

    public DateTime ThoiGian { get; set; } = DateTime.Now;

    /// <summary>true = phiếu thu, false = phiếu chi.</summary>
    public bool LaThu { get; set; }

    /// <summary>
    /// Người nộp (phiếu thu) hoặc người nhận (phiếu chi) - lấy từ cột
    /// <c>nguoi_nop_nhan</c>. Có thể rỗng với phiếu tạo trước khi có cột này.
    /// </summary>
    public string NguoiNopNhan { get; set; } = "";

    /// <summary>Số tiền của phiếu (luôn dương; thu/chi phân biệt qua <see cref="LaThu"/>).</summary>
    public decimal GiaTri { get; set; }

    /// <summary>Nội dung phiếu (cột <c>noi_dung</c>).</summary>
    public string NoiDung { get; set; } = "";

    /// <summary>Ghi chú của phiếu (cột <c>ghi_chu</c>).</summary>
    public string GhiChu { get; set; } = "";

    /// <summary>Tên loại thu/chi lấy từ bảng nhom_chi_phi (rỗng nếu phiếu chưa gán loại).</summary>
    public string TenLoai { get; set; } = "";

    /// <summary>Mã phương thức quỹ (tiền mặt, chuyển khoản...).</summary>
    public string PhuongThuc { get; set; } = "";

    /// <summary>Mã trạng thái chứng từ trong database (HOAN_THANH / HUY).</summary>
    public string TrangThai { get; set; } = "";

    /// <summary>Người tạo phiếu (join từ bảng nguoi_dung).</summary>
    public string NguoiTao { get; set; } = "";

    /// <summary>Ô chọn dòng để thao tác hàng loạt.</summary>
    [ObservableProperty] private bool daChon;

    public string LoaiHienThi => LaThu ? "Thu" : "Chi";

    public string MauLoai => LaThu ? "#1877F2" : "#D92D20";

    public string MauNenLoai => LaThu ? "#EAF1FF" : "#FEF3F2";

    public string MauGiaTri => LaThu ? "#1877F2" : "#D92D20";

    /// <summary>Chỉ phiếu đã hủy mới bị loại khỏi số tiền của quỹ.</summary>
    public bool DaHuy => TrangThai.Equals("HUY", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nhãn trạng thái hiển thị trên dòng phiếu.</summary>
    public string TrangThaiHienThi => TrangThai switch
    {
        "HOAN_THANH" => "Đã thanh toán",
        "HUY" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(TrangThai) ? "Chưa có" : TrangThai
    };

    /// <summary>Màu chữ của nhãn trạng thái: hủy thì nổi bật hơn.</summary>
    public string MauTrangThai => DaHuy ? "#D92D20" : "#667085";

    /// <summary>Nền nhãn trạng thái.</summary>
    public string MauNenTrangThai => DaHuy ? "#FEF3F2" : "#F2F4F7";

    /// <summary>Phiếu đã hủy thì làm mờ cả dòng cho dễ nhận biết.</summary>
    public string MauDong => DaHuy ? "#98A2B3" : "#263B35";

    public string ThoiGianHienThi => ThoiGian.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Giá trị hiển thị: chỉ số tiền, không kèm dấu +/-.
    /// Dấu đã thể hiện bằng màu (xanh dương = thu, đỏ = chi) ở cột LOẠI nên
    /// thêm dấu vào đây chỉ làm cột GIÁ TRỊ bị dính chữ với cột NỘI DUNG.
    /// </summary>
    public string GiaTriHienThi => $"{GiaTri:N0} đ";

    partial void OnDaChonChanged(bool value) => OnPropertyChanged(nameof(DaChonHienThi));

    public bool DaChonHienThi => DaChon;
}

/// <summary>
/// Một nút số trang trên thanh phân trang của sổ quỹ.
/// Dùng chung cho cả số trang thật và dấu "..." bị khuất giữa các trang.
/// </summary>
public sealed class SoQuyNutTrang
{
    /// <summary>Số thứ tự trang; bằng 0 với dấu "..." vì dấu này không bấm được.</summary>
    public int SoTrang { get; }

    /// <summary>true nếu đây là trang đang xem.</summary>
    public bool DangChon { get; }

    /// <summary>true nếu là dấu "..." bị khuất.</summary>
    public bool LaDauTrang { get; }

    public string HienThi => LaDauTrang ? "..." : SoTrang.ToString(CultureInfo.InvariantCulture);

    private SoQuyNutTrang(int soTrang, bool dangChon, bool laDauTrang)
    {
        SoTrang = soTrang;
        DangChon = dangChon;
        LaDauTrang = laDauTrang;
    }

    public static SoQuyNutTrang Tao(int soTrang, bool dangChon)
        => new(soTrang, dangChon, laDauTrang: false);

    public static SoQuyNutTrang TaoPhanTich() => new(0, dangChon: false, laDauTrang: true);
}