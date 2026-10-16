namespace ShopHoa.Models;

/// <summary>
/// Tổng hợp dữ liệu mua hàng / công nợ của một khách hàng.
/// Toàn bộ số liệu đều lấy từ database, không có giá trị mặc định giả.
/// </summary>
public sealed class ThongKeMuaHangKhachHang
{
    /// <summary>Số hóa đơn đã ghi nhận cho khách hàng.</summary>
    public int SoHoaDon { get; set; }

    /// <summary>Tổng tiền khách phải trả trên các hóa đơn.</summary>
    public decimal TongTienDaMua { get; set; }

    /// <summary>Tổng tiền khách phải trả trên các hóa đơn, chưa cộng dữ liệu file KiotViet.</summary>
    public decimal TongTienDaMuaHoaDon { get; set; }

    /// <summary>Tổng tiền bán trừ các phiếu trả hàng đã hoàn thành.</summary>
    public decimal TongBanTruTraHang { get; set; }

    /// <summary>Tổng tiền khách đã thanh toán trên các hóa đơn.</summary>
    public decimal TongDaThanhToan { get; set; }

    /// <summary>Tổng nợ còn lại tính từ hóa đơn (hoa_don_ban_hang.con_no).</summary>
    public decimal TongConNoHoaDon { get; set; }

    /// <summary>Nợ hiện tại đúng như cột "Nợ hiện tại" của bảng danh sách.</summary>
    public decimal TongConNoHienTai { get; set; }

    /// <summary>Số lần ghi công nợ trong lịch sử công nợ khách hàng.</summary>
    public int SoLanGhiCongNo { get; set; }

    /// <summary>Công nợ hiện tại theo sổ cái công nợ (ghi nợ - ghi có).</summary>
    public decimal TongCongNoLichSu { get; set; }

    /// <summary>Thời điểm ghi công nợ gần nhất, null nếu chưa có phát sinh.</summary>
    public DateTime? LanGhiCongNoGanNhat { get; set; }

    /// <summary>
    /// Hệ thống đã có dữ liệu để hiển thị vùng "Công nợ" cho khách hàng này chưa.
    /// Phải tính cả số tiền tổng hợp đã nhập từ file KiotViet, vì phần lớn
    /// khách hàng chỉ có số tiền ở các cột đó mà chưa có hóa đơn/sổ cái trong
    /// phần mềm. Nếu chỉ nhìn SoHoaDon và SoLanGhiCongNo thì vùng này luôn báo
    /// "Chưa có dữ liệu" trong khi ô "Công nợ hiện tại" phía trên lại ra số.
    /// </summary>
    public bool CoDuLieu =>
        SoHoaDon > 0
        || SoLanGhiCongNo > 0
        || TongTienDaMua > 0
        || TongBanTruTraHang > 0
        || TongConNoHienTai > 0;

    /// <summary>
    /// Số tiền chỉ có trong dữ liệu tổng hợp nhập từ file KiotViet, tức khách
    /// hàng này chưa phát sinh hóa đơn hay lần ghi công nợ nào trong phần mềm.
    /// </summary>
    public bool CoDuLieuKiotViet => SoHoaDon == 0 && SoLanGhiCongNo == 0;

    /// <summary>
    /// Công nợ hiện tại: ưu tiên số dư từ sổ cái công nợ,
    /// khi chưa có sổ cái thì lấy tổng nợ trên hóa đơn.
    /// </summary>
    public decimal TongCongNo =>
        SoLanGhiCongNo > 0
            ? TongCongNoLichSu
            : TongConNoHoaDon;
}
