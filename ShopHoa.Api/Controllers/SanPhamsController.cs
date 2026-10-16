using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopHoa.Api.Models;
using ShopHoa.Api.Services;

namespace ShopHoa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SanPhamsController : ControllerBase
{
    private readonly IDbConnectionFactory _db;

    public SanPhamsController(IDbConnectionFactory db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetSanPhams(
        [FromQuery] string? search = null,
        [FromQuery] uint? nhomId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);
        var offset = (safePage - 1) * safePageSize;
        var keyword = (search ?? "").Trim();

        await using var conn = await _db.CreateConnectionAsync();

        var parameters = new DynamicParameters();
        parameters.Add("PageSize", safePageSize);
        parameters.Add("Offset", offset);

        var whereClauses = new List<string>();

        if (!string.IsNullOrEmpty(keyword))
        {
            parameters.Add("Pattern", $"%{keyword}%");
            whereClauses.Add("(sp.ma_san_pham LIKE @Pattern OR sp.ma_vach LIKE @Pattern OR sp.ten_san_pham LIKE @Pattern)");
        }

        if (nhomId.HasValue && nhomId.Value > 0)
        {
            if (nhomId.Value == 999999)
            {
                whereClauses.Add("(sp.nhom_san_pham_id IS NULL OR sp.nhom_san_pham_id = 0)");
            }
            else
            {
                parameters.Add("NhomId", nhomId.Value);
                whereClauses.Add("sp.nhom_san_pham_id = @NhomId");
            }
        }

        var whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        var countSql = $"SELECT COUNT(*) FROM san_pham sp {whereSql};";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        var dataSql = $"""
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenSanPham,
                sp.nhom_san_pham_id AS NhomSanPhamId,
                sp.don_vi_tinh_id AS DonViTinhId,
                sp.thuong_hieu_id AS ThuongHieuId,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                COALESCE(tk.so_luong_ton, 0) AS TonKho,
                COALESCE(sp.ton_kho_excel, COALESCE(tk.so_luong_ton, 0)) AS TonKhoExcel,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                COALESCE(sp.khach_dat, 0) AS KhachDat,
                sp.ngay_tao AS NgayTao,
                sp.du_kien_het_hang AS DuKienHetHang,
                dvt.ten_don_vi AS DonViTinh,
                nsp.ten_nhom AS NhomSanPham,
                sp.loai_hang_excel AS LoaiHang,
                sp.nhom_hang_3_cap_excel AS NhomHangBaCap,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            {whereSql}
            ORDER BY sp.id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        var items = (await conn.QueryAsync<SanPhamDto>(dataSql, parameters)).AsList();

        return Ok(new PagedResult<SanPhamDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetSanPhamById(uint id)
    {
        await using var conn = await _db.CreateConnectionAsync();

        const string sql = """
            SELECT
                sp.id AS Id,
                sp.ma_san_pham AS MaSanPham,
                sp.ma_vach AS MaVach,
                sp.ten_san_pham AS TenSanPham,
                sp.nhom_san_pham_id AS NhomSanPhamId,
                sp.don_vi_tinh_id AS DonViTinhId,
                sp.thuong_hieu_id AS ThuongHieuId,
                sp.gia_ban AS GiaBan,
                sp.gia_von AS GiaVon,
                COALESCE(tk.so_luong_ton, 0) AS TonKho,
                sp.ton_toi_thieu AS TonToiThieu,
                sp.ton_toi_da AS TonToiDa,
                dvt.ten_don_vi AS DonViTinh,
                nsp.ten_nhom AS NhomSanPham,
                sp.vi_tri_kho AS ViTriKho,
                sp.dang_kinh_doanh AS DuocBanTrucTiep,
                sp.trang_thai AS DangKinhDoanh,
                sp.mo_ta AS MoTa,
                sp.ghi_chu AS GhiChu,
                sp.hinh_anh AS HinhAnh
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            WHERE sp.id = @Id;
            """;

        var item = await conn.QuerySingleOrDefaultAsync<SanPhamDto>(sql, new { Id = id });
        if (item is null)
            return NotFound(new { Message = "Không tìm thấy sản phẩm." });

        return Ok(item);
    }

    [HttpPut("{id:long}/price")]
    public async Task<IActionResult> UpdatePrice(uint id, [FromBody] UpdateGiaBanRequest request)
    {
        if (request.GiaBan < 0)
            return BadRequest(new { Message = "Giá bán không hợp lệ." });

        await using var conn = await _db.CreateConnectionAsync();
        var rows = await conn.ExecuteAsync(
            "UPDATE san_pham SET gia_ban = @GiaBan, ngay_cap_nhat = NOW() WHERE id = @Id;",
            new { GiaBan = request.GiaBan, Id = id });

        if (rows == 0)
            return NotFound(new { Message = "Không tìm thấy sản phẩm." });

        return Ok(new { Message = "Cập nhật giá bán thành công.", GiaBan = request.GiaBan });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        await using var conn = await _db.CreateConnectionAsync();
        var list = (await conn.QueryAsync<NhomSanPhamDto>(
            "SELECT id AS Id, ten_nhom AS TenNhom, mo_ta AS MoTa FROM nhom_san_pham ORDER BY ten_nhom;"
        )).AsList();

        return Ok(list);
    }
}
