using System.Text;
using Dapper;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Truy vấn hợp nhất các bảng phiếu nhập và phiếu xuất để dựng danh sách
/// duy nhất cho phân hệ Nhập hàng / Xuất hàng.
///
/// Các bảng vật lý vẫn được giữ nguyên (không migrate dữ liệu cũ), chỉ được
/// đọc chung qua UNION ALL kèm một mã loại phiếu chuẩn hoá để người dùng
/// quản lý nhập xuất trên một danh sách duy nhất.
/// </summary>
public sealed partial class DatabaseService
{
    // ============================================================
    // DANH SÁCH PHIẾU NHẬP HỢP NHẤT
    // ============================================================

    public async Task<(IReadOnlyList<PhieuHopRow> Items, int TongSoBanGhi)> GetPhieuNhapHopPagedAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiPhieu,
        string? trangThai,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();

        var offset = (page - 1) * pageSize;
        var (unionSql, parameters) = BuildPhieuHopNhapSql(search, tuNgay, denNgay, loaiPhieu, trangThai);

        var countSql = $"SELECT COUNT(*) FROM ({unionSql}) AS hop;";
        var tongSoBanGhi = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var listSql = $"""
            SELECT * FROM ({unionSql}) AS hop
            ORDER BY hop.ThoiGian DESC, hop.Id DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", offset);

        var items = (await connection.QueryAsync<PhieuHopRow>(listSql, parameters)).AsList();
        return (items, tongSoBanGhi);
    }

    /// <summary>
    /// Tổng hợp số phiếu / giá trị / số lượng của nhóm phiếu nhập theo bộ lọc đang dùng.
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongGiaTri, decimal TongSoLuong)> GetThongKePhieuNhapHopAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiPhieu,
        string? trangThai)
    {
        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();

        var (unionSql, parameters) = BuildPhieuHopNhapSql(search, tuNgay, denNgay, loaiPhieu, trangThai);

        var sql = $"""
            SELECT
                COUNT(*) AS TongSoPhieu,
                COALESCE(SUM(hop.TongTien), 0) AS TongGiaTri,
                COALESCE(SUM(hop.TongSoLuong), 0) AS TongSoLuong
            FROM ({unionSql}) AS hop;
            """;

        var row = await connection.QueryFirstOrDefaultAsync<HopThongKe>(sql, parameters);
        return (row?.TongSoPhieu ?? 0, row?.TongGiaTri ?? 0m, row?.TongSoLuong ?? 0m);
    }

    private (string UnionSql, DynamicParameters Parameters) BuildPhieuHopNhapSql(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiPhieu,
        string? trangThai)
    {
        var parameters = new DynamicParameters();
        var keyword = $"%{(search ?? "").Trim()}%";
        parameters.Add("Keyword", keyword);

        var dateConditions = new StringBuilder();
        if (tuNgay.HasValue)
        {
            dateConditions.Append(" AND p.thoi_gian_nhap >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value);
        }
        if (denNgay.HasValue)
        {
            dateConditions.Append(" AND p.thoi_gian_nhap < @DenNgayExclusive");
            parameters.Add("DenNgayExclusive", denNgay.Value);
        }

        var loai = (loaiPhieu ?? "TAT_CA").Trim().ToUpperInvariant();
        var trang = (trangThai ?? "TAT_CA").Trim().ToUpperInvariant();

        // Nhập từ nhà cung cấp: loại phiếu là hằng số của nhánh truy vấn.
        var nccLoaiCondition = loai == "TAT_CA" ? "" : " AND @Loai = 'NHAP_TU_NCC'";
        var nccTrangThaiCondition = BuildTrangThaiCondition(trang, "DA_NHAP_HANG", "DA_HUY", "LUU_TAM", chuaThanhToan: true);

        // Nhập khác: loại phiếu nằm trong cột loai_nhap_khac.
        var khacLoaiCondition = loai == "TAT_CA" ? "" : " AND p.loai_nhap_khac = @Loai";
        var khacTrangThaiCondition = BuildTrangThaiCondition(trang, "HOAN_THANH", "DA_HUY", null, chuaThanhToan: false);

        if (loai != "TAT_CA")
            parameters.Add("Loai", loai);

        var unionSql = $"""
            SELECT
                p.id                                                           AS Id,
                p.ma_phieu_nhap                                                AS MaPhieu,
                p.thoi_gian_nhap                                               AS ThoiGian,
                'NHAP_TU_NCC'                                                  AS LoaiPhieu,
                COALESCE(ncc.ten_nha_cung_cap, '')                             AS DoiTuong,
                COALESCE((SELECT SUM(ct.so_luong)
                            FROM chi_tiet_phieu_nhap_hang ct
                            WHERE ct.phieu_nhap_id = p.id), 0)                AS TongSoLuong,
                COALESCE(p.tong_tien_nhap, 0)                                  AS TongTien,
                p.trang_thai                                                   AS TrangThai,
                COALESCE(p.ghi_chu, '')                                        AS GhiChu,
                COALESCE(p.con_no, 0)                                         AS ConNo,
                'phieu_nhap_hang'                                              AS NguonPhieu
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            WHERE (@Keyword = ''
                    OR p.ma_phieu_nhap LIKE @Keyword
                    OR ncc.ma_nha_cung_cap LIKE @Keyword
                    OR ncc.ten_nha_cung_cap LIKE @Keyword
                    OR COALESCE(p.ghi_chu, '') LIKE @Keyword){dateConditions}{nccLoaiCondition}{nccTrangThaiCondition}
            UNION ALL
            SELECT
                p.id,
                p.ma_phieu_nhap_khac,
                p.thoi_gian_nhap,
                p.loai_nhap_khac,
                COALESCE(p.doi_tuong_lien_quan, ''),
                COALESCE(p.tong_so_luong, 0),
                COALESCE(p.tong_gia_tri, 0),
                p.trang_thai,
                COALESCE(p.ghi_chu, ''),
                0,
                'phieu_nhap_khac'
            FROM phieu_nhap_khac p
            WHERE (@Keyword = ''
                    OR p.ma_phieu_nhap_khac LIKE @Keyword
                    OR COALESCE(p.doi_tuong_lien_quan, '') LIKE @Keyword
                    OR COALESCE(p.ghi_chu, '') LIKE @Keyword){dateConditions}{khacLoaiCondition}{khacTrangThaiCondition}
            """;

        return (unionSql, parameters);
    }

    // ============================================================
    // DANH SÁCH PHIẾU XUẤT HỢP NHẤT
    // ============================================================

    public async Task<(IReadOnlyList<PhieuHopRow> Items, int TongSoBanGhi)> GetPhieuXuatHopPagedAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiPhieu,
        string? trangThai,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        await EnsureBangPhieuXuatNoiBoAsync();
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        var offset = (page - 1) * pageSize;
        var (unionSql, parameters) = BuildPhieuHopXuatSql(search, tuNgay, denNgay, loaiPhieu, trangThai);

        var countSql = $"SELECT COUNT(*) FROM ({unionSql}) AS hop;";
        var tongSoBanGhi = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var listSql = $"""
            SELECT * FROM ({unionSql}) AS hop
            ORDER BY hop.ThoiGian DESC, hop.Id DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", offset);

        var items = (await connection.QueryAsync<PhieuHopRow>(listSql, parameters)).AsList();
        return (items, tongSoBanGhi);
    }

    /// <summary>
    /// Tổng hợp số phiếu / giá trị / số lượng của nhóm phiếu xuất theo bộ lọc đang dùng.
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongGiaTri, decimal TongSoLuong)> GetThongKePhieuXuatHopAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiPhieu,
        string? trangThai)
    {
        await EnsureBangPhieuXuatNoiBoAsync();
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        var (unionSql, parameters) = BuildPhieuHopXuatSql(search, tuNgay, denNgay, loaiPhieu, trangThai);

        var sql = $"""
            SELECT
                COUNT(*) AS TongSoPhieu,
                COALESCE(SUM(hop.TongTien), 0) AS TongGiaTri,
                COALESCE(SUM(hop.TongSoLuong), 0) AS TongSoLuong
            FROM ({unionSql}) AS hop;
            """;

        var row = await connection.QueryFirstOrDefaultAsync<HopThongKe>(sql, parameters);
        return (row?.TongSoPhieu ?? 0, row?.TongGiaTri ?? 0m, row?.TongSoLuong ?? 0m);
    }

    private (string UnionSql, DynamicParameters Parameters) BuildPhieuHopXuatSql(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiPhieu,
        string? trangThai)
    {
        var parameters = new DynamicParameters();
        var keyword = $"%{(search ?? "").Trim()}%";
        parameters.Add("Keyword", keyword);

        var dateConditions = new StringBuilder();
        if (tuNgay.HasValue)
        {
            dateConditions.Append(" AND p.thoi_gian_xuat >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value);
        }
        if (denNgay.HasValue)
        {
            dateConditions.Append(" AND p.thoi_gian_xuat < @DenNgayExclusive");
            parameters.Add("DenNgayExclusive", denNgay.Value);
        }

        // Phiếu trả NCC dùng cột thời gian riêng (thoi_gian_xuat_tra).
        var dateConditionsTraNcc = new StringBuilder();
        if (tuNgay.HasValue)
            dateConditionsTraNcc.Append(" AND p.thoi_gian_xuat_tra >= @TuNgay");
        if (denNgay.HasValue)
            dateConditionsTraNcc.Append(" AND p.thoi_gian_xuat_tra < @DenNgayExclusive");

        var loai = (loaiPhieu ?? "TAT_CA").Trim().ToUpperInvariant();
        var trang = (trangThai ?? "TAT_CA").Trim().ToUpperInvariant();

        // phieu_xuat_hang: loai_xuat đã chuẩn hoá sẵn.
        var xuatLoaiCondition = loai == "TAT_CA" ? "" : " AND p.loai_xuat = @Loai";
        var xuatTrangThaiCondition = BuildTrangThaiCondition(trang, "DA_XUAT_KHO", "DA_HUY", null, chuaThanhToan: false);

        // phieu_xuat_noi_bo: loai_xuat_noi_bo có thêm XUAT_HUY_HONG (gộp vào hao hụt).
        var noiBoLoaiCondition = loai == "TAT_CA"
            ? ""
            : " AND (p.loai_xuat_noi_bo = @Loai OR (@Loai = 'XUAT_HUY' AND p.loai_xuat_noi_bo = 'XUAT_HUY_HONG'))";
        var noiBoTrangThaiCondition = BuildTrangThaiCondition(trang, "HOAN_THANH", "DA_HUY", null, chuaThanhToan: false);

        // phieu_xuat_tra_ncc: loại phiếu là hằng số của nhánh truy vấn.
        var traNccLoaiCondition = loai == "TAT_CA" ? "" : " AND @Loai = 'XUAT_TRA_NCC'";
        var traNccTrangThaiCondition = BuildTrangThaiCondition(trang, "HOAN_THANH", "DA_HUY", null, chuaThanhToan: false);

        if (loai != "TAT_CA")
            parameters.Add("Loai", loai);

        var unionSql = $"""
            SELECT
                p.id                                                           AS Id,
                p.ma_phieu_xuat                                                AS MaPhieu,
                p.thoi_gian_xuat                                               AS ThoiGian,
                p.loai_xuat                                                    AS LoaiPhieu,
                COALESCE(p.doi_tuong_nhan, '')                                 AS DoiTuong,
                COALESCE(p.tong_so_luong, 0)                                   AS TongSoLuong,
                COALESCE(p.tong_tien, 0)                                       AS TongTien,
                p.trang_thai                                                   AS TrangThai,
                COALESCE(p.ghi_chu, '')                                        AS GhiChu,
                0                                                              AS ConNo,
                'phieu_xuat_hang'                                              AS NguonPhieu
            FROM phieu_xuat_hang p
            WHERE (@Keyword = ''
                    OR p.ma_phieu_xuat LIKE @Keyword
                    OR COALESCE(p.doi_tuong_nhan, '') LIKE @Keyword
                    OR COALESCE(p.ghi_chu, '') LIKE @Keyword){dateConditions}{xuatLoaiCondition}{xuatTrangThaiCondition}
            UNION ALL
            SELECT
                p.id,
                p.ma_phieu_xuat_noi_bo,
                p.thoi_gian_xuat,
                p.loai_xuat_noi_bo,
                COALESCE(p.kho_nhan_hoac_bo_phan, ''),
                COALESCE(p.tong_so_luong, 0),
                COALESCE(p.tong_gia_tri, 0),
                p.trang_thai,
                COALESCE(p.ghi_chu, ''),
                0,
                'phieu_xuat_noi_bo'
            FROM phieu_xuat_noi_bo p
            WHERE (@Keyword = ''
                    OR p.ma_phieu_xuat_noi_bo LIKE @Keyword
                    OR COALESCE(p.kho_nhan_hoac_bo_phan, '') LIKE @Keyword
                    OR COALESCE(p.ghi_chu, '') LIKE @Keyword){dateConditions}{noiBoLoaiCondition}{noiBoTrangThaiCondition}
            UNION ALL
            SELECT
                p.id,
                p.ma_phieu_xuat_tra,
                p.thoi_gian_xuat_tra,
                'XUAT_TRA_NCC',
                COALESCE(p.ten_nha_cung_cap, ''),
                COALESCE(p.tong_so_luong, 0),
                COALESCE(p.tong_tien_tra, 0),
                p.trang_thai,
                COALESCE(p.ghi_chu, ''),
                0,
                'phieu_xuat_tra_ncc'
            FROM phieu_xuat_tra_ncc p
            WHERE (@Keyword = ''
                    OR p.ma_phieu_xuat_tra LIKE @Keyword
                    OR COALESCE(p.ten_nha_cung_cap, '') LIKE @Keyword
                    OR COALESCE(p.ma_phieu_nhap_goc, '') LIKE @Keyword
                    OR COALESCE(p.ghi_chu, '') LIKE @Keyword){dateConditionsTraNcc}{traNccLoaiCondition}{traNccTrangThaiCondition}
            """;

        return (unionSql, parameters);
    }

    /// <summary>
    /// Sinh điều kiện lọc trạng thái dùng chung cho các nhánh truy vấn hợp nhất.
    /// </summary>
    private static string BuildTrangThaiCondition(
        string trangThai,
        string maHoanThanh,
        string maDaHuy,
        string? maLuuTam,
        bool chuaThanhToan)
    {
        switch (trangThai)
        {
            case "HOAN_THANH":
                return $" AND p.trang_thai IN ('{maHoanThanh}', 'HOAN_THANH')";
            case "DA_HUY":
                return " AND p.trang_thai = 'DA_HUY'";
            case "LUU_TAM":
                return maLuuTam is null ? " AND 1 = 0" : $" AND p.trang_thai = '{maLuuTam}'";
            case "CHUA_THANH_TOAN":
                return chuaThanhToan ? " AND p.con_no > 0" : " AND 1 = 0";
            default:
                return "";
        }
    }

    /// <summary>
    /// Lấy chi tiết các mặt hàng của một phiếu trong danh sách hợp nhất,
    /// tự chọn bảng chi tiết theo nguồn phiếu gốc.
    /// </summary>
    public async Task<IReadOnlyList<PhieuHopChiTiet>> GetChiTietPhieuHopAsync(string? nguonPhieu, uint phieuId)
    {
        if (phieuId == 0)
            return Array.Empty<PhieuHopChiTiet>();

        await EnsureBangPhieuNhapKhacAsync();
        await EnsureBangPhieuXuatNoiBoAsync();
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        var sql = (nguonPhieu ?? "").Trim().ToLowerInvariant() switch
        {
            "phieu_nhap_hang" => """
                SELECT
                    COALESCE(sp.ma_san_pham, '')      AS MaSanPham,
                    COALESCE(sp.ten_san_pham, '')      AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, '')       AS DonViTinh,
                    ct.so_luong                       AS SoLuong,
                    ct.don_gia_nhap                   AS DonGia,
                    ct.thanh_tien                     AS ThanhTien,
                    COALESCE(ct.ghi_chu, '')          AS GhiChu
                FROM chi_tiet_phieu_nhap_hang ct
                LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE ct.phieu_nhap_id = @PhieuId
                ORDER BY ct.id;
                """,
            "phieu_nhap_khac" => """
                SELECT
                    COALESCE(sp.ma_san_pham, '')      AS MaSanPham,
                    COALESCE(sp.ten_san_pham, '')      AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, '')       AS DonViTinh,
                    ct.so_luong                       AS SoLuong,
                    ct.don_gia                        AS DonGia,
                    ct.thanh_tien                     AS ThanhTien,
                    COALESCE(ct.ghi_chu, '')          AS GhiChu
                FROM chi_tiet_phieu_nhap_khac ct
                LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE ct.phieu_nhap_khac_id = @PhieuId
                ORDER BY ct.id;
                """,
            "phieu_xuat_hang" => """
                SELECT
                    COALESCE(ct.ma_san_pham, '')      AS MaSanPham,
                    COALESCE(ct.ten_san_pham, '')      AS TenSanPham,
                    COALESCE(ct.don_vi_tinh, '')       AS DonViTinh,
                    ct.so_luong                       AS SoLuong,
                    ct.don_gia                        AS DonGia,
                    ct.thanh_tien                     AS ThanhTien,
                    COALESCE(ct.ghi_chu, '')          AS GhiChu
                FROM chi_tiet_phieu_xuat_hang ct
                WHERE ct.phieu_xuat_id = @PhieuId
                ORDER BY ct.id;
                """,
            "phieu_xuat_noi_bo" => """
                SELECT
                    COALESCE(sp.ma_san_pham, '')      AS MaSanPham,
                    COALESCE(sp.ten_san_pham, '')      AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, '')       AS DonViTinh,
                    ct.so_luong                       AS SoLuong,
                    ct.don_gia                        AS DonGia,
                    ct.thanh_tien                     AS ThanhTien,
                    COALESCE(ct.ghi_chu, '')          AS GhiChu
                FROM chi_tiet_phieu_xuat_noi_bo ct
                LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE ct.phieu_xuat_noi_bo_id = @PhieuId
                ORDER BY ct.id;
                """,
            "phieu_xuat_tra_ncc" => """
                SELECT
                    COALESCE(sp.ma_san_pham, '')      AS MaSanPham,
                    COALESCE(sp.ten_san_pham, '')      AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, '')       AS DonViTinh,
                    ct.so_luong                       AS SoLuong,
                    ct.don_gia                        AS DonGia,
                    ct.thanh_tien                     AS ThanhTien,
                    COALESCE(ct.ly_do_xuat_tra, '')   AS GhiChu
                FROM chi_tiet_phieu_xuat_tra_ncc ct
                LEFT JOIN san_pham sp ON sp.id = ct.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE ct.phieu_xuat_tra_id = @PhieuId
                ORDER BY ct.id;
                """,
            _ => ""
        };

        if (string.IsNullOrEmpty(sql))
            return Array.Empty<PhieuHopChiTiet>();

        return (await connection.QueryAsync<PhieuHopChiTiet>(sql, new { PhieuId = phieuId })).AsList();
    }

    /// <summary>Kết quả tổng hợp cho danh sách hợp nhất.</summary>
    private sealed class HopThongKe
    {
        public int TongSoPhieu { get; set; }
        public decimal TongGiaTri { get; set; }
        public decimal TongSoLuong { get; set; }
    }

    /// <summary>
    /// Lấy một phiếu nhập hàng theo id (dùng cho thao tác Thanh toán / In phiếu
    /// trên danh sách hợp nhất).
    /// </summary>
    public async Task<PhieuNhapHang?> GetPhieuNhapHangTheoIdAsync(uint phieuNhapId)
    {
        if (phieuNhapId == 0)
            return null;

        await using var connection = await OpenAsync();

        const string sql = """
            SELECT
                p.id AS Id,
                p.ma_phieu_nhap AS MaPhieuNhap,
                p.nha_cung_cap_id AS NhaCungCapId,
                ncc.ma_nha_cung_cap AS MaNhaCungCap,
                ncc.ten_nha_cung_cap AS TenNhaCungCap,
                p.nguoi_nhap_id AS NguoiNhapId,
                nd.ho_ten AS TenNhanVien,
                p.thoi_gian_nhap AS ThoiGianNhap,
                p.tong_tien_hang AS TongTienHang,
                p.chi_phi_khac AS ChiPhiKhac,
                p.tong_tien_nhap AS TongTienNhap,
                p.da_thanh_toan AS DaThanhToan,
                p.con_no AS ConNo,
                p.trang_thai AS TrangThai,
                p.ghi_chu AS GhiChu
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON ncc.id = p.nha_cung_cap_id
            LEFT JOIN nguoi_dung   nd  ON nd.id  = p.nguoi_nhap_id
            WHERE p.id = @PhieuNhapId
            LIMIT 1;
            """;

        return await connection.QueryFirstOrDefaultAsync<PhieuNhapHang>(
            sql,
            new { PhieuNhapId = phieuNhapId });
    }
}
