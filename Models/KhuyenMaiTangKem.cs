namespace ShopHoa.Models;

/// <summary>
/// Một quy tắc "tặng kèm" đọc từ bảng khuyến mãi:
/// mua <see cref="SoLuongMua"/> <see cref="DonViTinh"/> <see cref="TenSanPhamMua"/>
/// thì tặng <see cref="SoLuongTang"/> <see cref="TenSanPhamTang"/>.
/// <para>
/// Quy tắc chỉ được dùng khi chương trình đang hoạt động, còn trong thời gian
/// hiệu lực và (nếu có) khách hàng đang bán thuộc đúng nhóm khách hàng của
/// chương trình.
/// </para>
/// </summary>
public sealed class KhuyenMaiTangKem
{
    public uint ChuongTrinhId { get; set; }
    public string MaChuongTrinh { get; set; } = "";
    public string TenChuongTrinh { get; set; } = "";

    /// <summary>Id nhóm khách hàng áp dụng (null = mọi khách hàng).</summary>
    public uint? NhomKhachHangId { get; set; }

    /// <summary>Id sản phẩm phải mua để được tặng.</summary>
    public uint SanPhamId { get; set; }
    public string TenSanPhamMua { get; set; } = "";
    public string? DonViTinh { get; set; }

    /// <summary>Số lượng sản phẩm phải mua để được tặng (ngưỡng).</summary>
    public decimal SoLuongMua { get; set; }

    /// <summary>Id sản phẩm được tặng.</summary>
    public uint SanPhamTangId { get; set; }
    public string TenSanPhamTang { get; set; } = "";

    /// <summary>Số lượng được tặng cho mỗi lần đạt ngưỡng.</summary>
    public decimal SoLuongTang { get; set; }

    /// <summary>
    /// Sản phẩm tặng đã nạp sẵn (kèm tồn kho, giá vốn, đơn vị tính) để khi
    /// áp dụng khuyến mãi không phải truy vấn database từng dòng.
    /// </summary>
    public SanPham? SanPhamTang { get; set; }

    /// <summary>Câu mô tả ngắn gọn hiển thị trên hóa đơn / trong giỏ hàng.</summary>
    public string MoTa
    {
        get
        {
            var donVi = string.IsNullOrWhiteSpace(DonViTinh) ? "" : DonViTinh + " ";

            return $"Mua {SoLuongMua:0.##} {donVi}{TenSanPhamMua} tặng {SoLuongTang:0.##} {TenSanPhamTang}";
        }
    }
}
