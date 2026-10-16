namespace ShopHoa.Api.Models;

public sealed class SanPhamDto
{
    public uint Id { get; set; }
    public string MaSanPham { get; set; } = string.Empty;
    public string? MaVach { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public uint? NhomSanPhamId { get; set; }
    public uint? DonViTinhId { get; set; }
    public uint? ThuongHieuId { get; set; }
    public decimal GiaBan { get; set; }
    public decimal GiaVon { get; set; }
    public decimal TonKho { get; set; }
    public decimal TonKhoExcel { get; set; }
    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public decimal KhachDat { get; set; }
    public DateTime? NgayTao { get; set; }
    public string? DuKienHetHang { get; set; }
    public string? DonViTinh { get; set; }
    public string? NhomSanPham { get; set; }
    public string? LoaiHang { get; set; }
    public string? NhomHangBaCap { get; set; }
    public string? ViTriKho { get; set; }
    public bool DuocBanTrucTiep { get; set; }
    public bool DangKinhDoanh { get; set; }
    public string? MoTa { get; set; }
    public string? GhiChu { get; set; }
    public string? HinhAnh { get; set; }
}

public sealed class CreateSanPhamRequest
{
    public string? MaSanPham { get; set; }
    public string? MaVach { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public uint? NhomSanPhamId { get; set; }
    public uint? DonViTinhId { get; set; }
    public decimal GiaBan { get; set; }
    public decimal GiaVon { get; set; }
    public decimal TonKho { get; set; }
    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public string? ViTriKho { get; set; }
    public bool DuocBanTrucTiep { get; set; } = true;
    public bool DangKinhDoanh { get; set; } = true;
    public string? MoTa { get; set; }
    public string? GhiChu { get; set; }
    public string? HinhAnh { get; set; }
}

public sealed class UpdateSanPhamRequest
{
    public string? MaVach { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public uint? NhomSanPhamId { get; set; }
    public uint? DonViTinhId { get; set; }
    public decimal GiaBan { get; set; }
    public decimal GiaVon { get; set; }
    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public string? ViTriKho { get; set; }
    public bool DuocBanTrucTiep { get; set; } = true;
    public bool DangKinhDoanh { get; set; } = true;
    public string? MoTa { get; set; }
    public string? GhiChu { get; set; }
    public string? HinhAnh { get; set; }
}

public sealed class UpdateGiaBanRequest
{
    public decimal GiaBan { get; set; }
}

public sealed class NhomSanPhamDto
{
    public uint Id { get; set; }
    public string TenNhom { get; set; } = string.Empty;
    public string? MoTa { get; set; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
