namespace ShopHoa.Models;

/// <summary>
/// Bộ lọc của sổ quỹ. Mọi điều kiện ở đây đều được đưa thẳng xuống SQL
/// (WHERE / COUNT / SUM) chứ không lọc trên UI, nên sổ quỹ vẫn chạy được
/// khi bảng phieu_chi có hàng chục nghìn phiếu.
/// </summary>
public sealed class SoQuyBoLoc
{
    /// <summary>Xem cả phiếu thu và phiếu chi.</summary>
    public const int TatCaLoaiChungTu = 0;

    /// <summary>Chỉ xem phiếu thu.</summary>
    public const int ChiPhieuThu = 1;

    /// <summary>Chỉ xem phiếu chi.</summary>
    public const int ChiPhieuChi = 2;

    /// <summary>
    /// Chuỗi tìm kiếm, so khớp một phần với mã phiếu, nội dung phiếu,
    /// tên người thực hiện và tên loại thu/chi.
    /// </summary>
    public string TimKiem { get; set; } = "";

    /// <summary>Một trong <see cref="TatCaLoaiChungTu"/>, <see cref="ChiPhieuThu"/>, <see cref="ChiPhieuChi"/>.</summary>
    public int LoaiChungTu { get; set; } = TatCaLoaiChungTu;

    /// <summary>Mã phương thức quỹ (tiền mặt, ngân hàng, ví điện tử...). Rỗng = tất cả.</summary>
    public string PhuongThuc { get; set; } = "";

    /// <summary>
    /// Thời điểm bắt đầu khoảng thời gian. Null = không giới hạn bên trái
    /// (khoảng thời gian mở rộng vô hạn, quỹ đầu kỳ = 0).
    /// </summary>
    public DateTime? TuNgay { get; set; }

    /// <summary>
    /// Mốc thời gian loại trừ để chọn. Người dùng chọn "đến ngày" theo ngày
    /// nên giá trị này đã được dịch sẵn sang 00:00:00 của ngày kế tiếp.
    /// </summary>
    public DateTime? DenNgay { get; set; }

    /// <summary>Danh sách id loại thu/chi, phân tách bằng dấu phẩy. Rỗng = tất cả.</summary>
    public string LoaiThuChiIds { get; set; } = "";

    /// <summary>Danh sách id người tạo phiếu, phân tách bằng dấu phẩy. Rỗng = tất cả.</summary>
    public string NguoiTaoIds { get; set; } = "";

    /// <summary>Số tiền nhỏ nhất. Null = không giới hạn.</summary>
    public decimal? TienTu { get; set; }

    /// <summary>Số tiền lớn nhất. Null = không giới hạn.</summary>
    public decimal? TienDen { get; set; }

    /// <summary>Có bộ lọc nào khác ngoài "toàn thời gian" hay không (dùng cho nút Xóa bộ lọc).</summary>
    public bool CoLoc
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TimKiem)
                || LoaiChungTu != TatCaLoaiChungTu
                || !string.IsNullOrWhiteSpace(PhuongThuc)
                || TuNgay.HasValue
                || DenNgay.HasValue
                || !string.IsNullOrWhiteSpace(LoaiThuChiIds)
                || !string.IsNullOrWhiteSpace(NguoiTaoIds)
                || TienTu.HasValue
                || TienDen.HasValue)
                return true;

            return false;
        }
    }
}
