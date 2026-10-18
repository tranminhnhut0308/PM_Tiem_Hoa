using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private bool? _hasBangPhieuXuatNoiBo;

    /// <summary>
    /// Đảm bảo bảng phieu_xuat_noi_bo và chi_tiet_phieu_xuat_noi_bo tồn tại trong CSDL.
    /// Nếu bảng trống, nạp dữ liệu mẫu để người dùng trải nghiệm ngay giao diện.
    /// </summary>
    public async Task EnsureBangPhieuXuatNoiBoAsync()
    {
        if (_hasBangPhieuXuatNoiBo == true)
            return;

        await using var connection = await OpenAsync();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS phieu_xuat_noi_bo (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_phieu_xuat_noi_bo VARCHAR(50) NOT NULL UNIQUE,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                loai_xuat_noi_bo VARCHAR(100) NOT NULL DEFAULT 'XUAT_CHUYEN_KHO',
                thoi_gian_xuat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                kho_xuat VARCHAR(100) NOT NULL DEFAULT 'Kho Tổng (Kho mặc định)',
                kho_nhan_hoac_bo_phan VARCHAR(255) NULL,
                tong_so_luong DECIMAL(15,3) NOT NULL DEFAULT 0,
                tong_gia_tri DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu TEXT NULL,
                trang_thai VARCHAR(30) NOT NULL DEFAULT 'HOAN_THANH',
                nguoi_tao_id INT UNSIGNED NULL,
                ten_nguoi_tao VARCHAR(100) NULL,
                ngay_tao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS chi_tiet_phieu_xuat_noi_bo (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                phieu_xuat_noi_bo_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                so_luong DECIMAL(15,3) NOT NULL DEFAULT 1,
                don_gia DECIMAL(15,2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu VARCHAR(255) NULL,
                INDEX idx_phieu_xuat_noi_bo (phieu_xuat_noi_bo_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        await connection.ExecuteAsync(createTableSql);

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM phieu_xuat_noi_bo;");
        if (count == 0)
        {
            var sampleSql = """
                INSERT INTO phieu_xuat_noi_bo
                (ma_phieu_xuat_noi_bo, chi_nhanh_id, loai_xuat_noi_bo, thoi_gian_xuat, kho_xuat, kho_nhan_hoac_bo_phan, tong_so_luong, tong_gia_tri, ghi_chu, trang_thai, ten_nguoi_tao)
                VALUES
                ('XNB261014-001', 1, 'XUAT_CHUYEN_KHO', DATE_SUB(NOW(), INTERVAL 4 DAY), 'Kho Tổng (Kho mặc định)', 'Chi nhánh 2 - Cầu Giấy', 80, 5400000, 'Điều chuyển hoa tươi và phụ kiện cắm hoa sang Chi nhánh 2', 'HOAN_THANH', 'Quản trị viên'),
                ('XNB261015-002', 1, 'XUAT_CAP_PHAT', DATE_SUB(NOW(), INTERVAL 3 DAY), 'Kho Tổng (Kho mặc định)', 'Bộ phận cắm hoa nghệ thuật', 70, 3850000, 'Cấp phát hoa hồng, cẩm chướng và phụ liệu cho quầy cắm mẫu sự kiện', 'HOAN_THANH', 'Quản trị viên'),
                ('XNB261016-003', 1, 'XUAT_SU_DUNG', DATE_SUB(NOW(), INTERVAL 1 DAY), 'Kho Tổng (Kho mặc định)', 'Bộ phận trang trí tiệm', 40, 2150000, 'Xuất hoa và bình gốm phục vụ trang trí sảnh tiệm chào mừng ngày 20/10', 'HOAN_THANH', 'Quản trị viên'),
                ('XNB261017-004', 1, 'XUAT_HUY_HONG', NOW(), 'Kho Tổng (Kho mặc định)', 'Kho xử lý hủy hỏng', 25, 1200000, 'Xuất tiêu hủy hoa dập nát do sự cố hệ thống tủ mát bảo quản', 'HOAN_THANH', 'Quản trị viên');
            """;
            try
            {
                await connection.ExecuteAsync(sampleSql);
            }
            catch { }
        }

        _hasBangPhieuXuatNoiBo = true;
    }

    /// <summary>
    /// Phát sinh mã phiếu xuất nội bộ mới theo định dạng XNByyMMdd-NNN.
    /// </summary>
    public async Task<string> TaoMaPhieuXuatNoiBoMoiAsync()
    {
        await EnsureBangPhieuXuatNoiBoAsync();
        await using var connection = await OpenAsync();

        var prefix = $"XNB{DateTime.Now:yyMMdd}-";
        const string sql = """
            SELECT ma_phieu_xuat_noi_bo 
            FROM phieu_xuat_noi_bo 
            WHERE ma_phieu_xuat_noi_bo LIKE CONCAT(@Prefix, '%')
            ORDER BY id DESC 
            LIMIT 1;
        """;

        var lastCode = await connection.QueryFirstOrDefaultAsync<string>(sql, new { Prefix = prefix });
        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastCode) && lastCode.Length >= prefix.Length)
        {
            var tail = lastCode.Substring(prefix.Length);
            if (int.TryParse(tail, out var n))
            {
                nextNumber = n + 1;
            }
        }

        return $"{prefix}{nextNumber:D3}";
    }

    /// <summary>
    /// Lấy danh sách phiếu xuất nội bộ có phân trang và bộ lọc tìm kiếm.
    /// </summary>
    public async Task<(List<PhieuXuatNoiBo> Items, int TotalCount)> GetPhieuXuatNoiBoPagedAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiXuatNoiBo,
        string? trangThai,
        int page,
        int pageSize)
    {
        await EnsureBangPhieuXuatNoiBoAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pxn.ma_phieu_xuat_noi_bo LIKE @Keyword OR pxn.kho_nhan_hoac_bo_phan LIKE @Keyword OR pxn.ghi_chu LIKE @Keyword OR pxn.ten_nguoi_tao LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pxn.thoi_gian_xuat >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pxn.thoi_gian_xuat <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(loaiXuatNoiBo) && loaiXuatNoiBo != "TAT_CA")
        {
            conditions.Add("pxn.loai_xuat_noi_bo = @LoaiXuatNoiBo");
            parameters.Add("LoaiXuatNoiBo", loaiXuatNoiBo);
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pxn.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $"""
            SELECT COUNT(*) 
            FROM phieu_xuat_noi_bo pxn
            {whereClause};
        """;

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = Math.Max(0, (page - 1) * pageSize);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var querySql = $"""
            SELECT 
                pxn.id AS Id,
                pxn.ma_phieu_xuat_noi_bo AS MaPhieuXuatNoiBo,
                pxn.chi_nhanh_id AS ChiNhanhId,
                pxn.loai_xuat_noi_bo AS LoaiXuatNoiBo,
                pxn.thoi_gian_xuat AS ThoiGianXuat,
                pxn.kho_xuat AS KhoXuat,
                pxn.kho_nhan_hoac_bo_phan AS KhoNhanHoacBoPhan,
                pxn.tong_so_luong AS TongSoLuong,
                pxn.tong_gia_tri AS TongGiaTri,
                pxn.ghi_chu AS GhiChu,
                pxn.trang_thai AS TrangThai,
                pxn.nguoi_tao_id AS NguoiTaoId,
                pxn.ten_nguoi_tao AS TenNguoiTao,
                pxn.ngay_tao AS NgayTao
            FROM phieu_xuat_noi_bo pxn
            {whereClause}
            ORDER BY pxn.id DESC
            LIMIT @PageSize OFFSET @Offset;
        """;

        var rows = (await connection.QueryAsync<PhieuXuatNoiBo>(querySql, parameters)).ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].SoThuTu = offset + i + 1;
        }

        return (rows, totalCount);
    }

    /// <summary>
    /// Thống kê tổng số phiếu, tổng giá trị xuất nội bộ và tổng số lượng sản phẩm xuất theo bộ lọc.
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongGiaTri, decimal TongSoLuong)> GetThongKePhieuXuatNoiBoAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiXuatNoiBo,
        string? trangThai)
    {
        await EnsureBangPhieuXuatNoiBoAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pxn.ma_phieu_xuat_noi_bo LIKE @Keyword OR pxn.kho_nhan_hoac_bo_phan LIKE @Keyword OR pxn.ghi_chu LIKE @Keyword OR pxn.ten_nguoi_tao LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pxn.thoi_gian_xuat >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pxn.thoi_gian_xuat <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(loaiXuatNoiBo) && loaiXuatNoiBo != "TAT_CA")
        {
            conditions.Add("pxn.loai_xuat_noi_bo = @LoaiXuatNoiBo");
            parameters.Add("LoaiXuatNoiBo", loaiXuatNoiBo);
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pxn.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT 
                COUNT(*) AS TongSoPhieu,
                COALESCE(SUM(pxn.tong_gia_tri), 0) AS TongGiaTri,
                COALESCE(SUM(pxn.tong_so_luong), 0) AS TongSoLuong
            FROM phieu_xuat_noi_bo pxn
            {whereClause};
        """;

        var stat = await connection.QueryFirstOrDefaultAsync<(int TongSoPhieu, decimal TongGiaTri, decimal TongSoLuong)>(sql, parameters);
        return stat;
    }

    /// <summary>
    /// Lấy chi tiết các mặt hàng trong một phiếu xuất nội bộ.
    /// </summary>
    public async Task<List<ChiTietPhieuXuatNoiBo>> GetChiTietPhieuXuatNoiBoAsync(uint phieuXuatNoiBoId)
    {
        await EnsureBangPhieuXuatNoiBoAsync();
        await using var connection = await OpenAsync();

        const string sql = """
            SELECT 
                ct.id AS Id,
                ct.phieu_xuat_noi_bo_id AS PhieuXuatNoiBoId,
                ct.san_pham_id AS SanPhamId,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                ct.thanh_tien AS ThanhTien,
                ct.ghi_chu AS GhiChu,
                sp.ma_san_pham AS MaSanPham,
                sp.ten_san_pham AS TenSanPham,
                COALESCE(sp.don_vi_tinh, '') AS DonViTinh,
                COALESCE(sp.ton_kho_excel, 0) AS TonKho
            FROM chi_tiet_phieu_xuat_noi_bo ct
            JOIN san_pham sp ON ct.san_pham_id = sp.id
            WHERE ct.phieu_xuat_noi_bo_id = @PhieuXuatNoiBoId
            ORDER BY ct.id ASC;
        """;

        var items = (await connection.QueryAsync<ChiTietPhieuXuatNoiBo>(sql, new { PhieuXuatNoiBoId = phieuXuatNoiBoId })).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].SoThuTu = i + 1;
        }

        return items;
    }

    /// <summary>
    /// Lưu phiếu xuất nội bộ mới cùng các dòng chi tiết theo giao dịch ACID.
    /// Tự động cập nhật giảm số lượng tồn kho (ton_kho_excel) trong bảng san_pham.
    /// </summary>
    public async Task<(bool Success, string Message, string MaPhieu, uint PhieuId)> LuuPhieuXuatNoiBoAsync(
        PhieuXuatNoiBo phieu,
        List<ChiTietPhieuXuatNoiBo> chiTiets)
    {
        if (chiTiets == null || chiTiets.Count == 0)
        {
            return (false, "Vui lòng chọn ít nhất một mặt hàng để lập phiếu xuất nội bộ.", "", 0);
        }

        foreach (var item in chiTiets)
        {
            if (item.SoLuong <= 0)
            {
                return (false, $"Số lượng xuất của hàng '{item.TenSanPham}' phải lớn hơn 0.", "", 0);
            }
            if (item.DonGia < 0)
            {
                return (false, $"Đơn giá của hàng '{item.TenSanPham}' không được âm.", "", 0);
            }
        }

        await EnsureBangPhieuXuatNoiBoAsync();
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Sinh mã phiếu nếu chưa có
            if (string.IsNullOrWhiteSpace(phieu.MaPhieuXuatNoiBo))
            {
                var prefix = $"XNB{DateTime.Now:yyMMdd}-";
                const string checkCodeSql = """
                    SELECT ma_phieu_xuat_noi_bo 
                    FROM phieu_xuat_noi_bo 
                    WHERE ma_phieu_xuat_noi_bo LIKE CONCAT(@Prefix, '%')
                    ORDER BY id DESC 
                    LIMIT 1 
                    FOR UPDATE;
                """;
                var lastCode = await connection.QueryFirstOrDefaultAsync<string>(checkCodeSql, new { Prefix = prefix }, transaction);
                var nextNumber = 1;
                if (!string.IsNullOrWhiteSpace(lastCode) && lastCode.Length >= prefix.Length)
                {
                    if (int.TryParse(lastCode.Substring(prefix.Length), out var n))
                        nextNumber = n + 1;
                }
                phieu.MaPhieuXuatNoiBo = $"{prefix}{nextNumber:D3}";
            }

            // Tính tổng
            phieu.TongSoLuong = chiTiets.Sum(c => c.SoLuong);
            phieu.TongGiaTri = chiTiets.Sum(c => c.ThanhTien);
            var now = phieu.ThoiGianXuat != default ? phieu.ThoiGianXuat : DateTime.Now;
            var userId = Session.CurrentUser?.Id;
            var userName = Session.CurrentUser?.HoTen ?? "Quản trị viên";
            var branchId = phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. Thêm phiếu xuất nội bộ
            const string insertPhieuSql = """
                INSERT INTO phieu_xuat_noi_bo
                (
                    ma_phieu_xuat_noi_bo, chi_nhanh_id, loai_xuat_noi_bo, thoi_gian_xuat,
                    kho_xuat, kho_nhan_hoac_bo_phan, tong_so_luong, tong_gia_tri,
                    ghi_chu, trang_thai, nguoi_tao_id, ten_nguoi_tao, ngay_tao
                )
                VALUES
                (
                    @MaPhieuXuatNoiBo, @ChiNhanhId, @LoaiXuatNoiBo, @ThoiGianXuat,
                    @KhoXuat, @KhoNhanHoacBoPhan, @TongSoLuong, @TongGiaTri,
                    @GhiChu, 'HOAN_THANH', @NguoiTaoId, @TenNguoiTao, @NgayTao
                );
                SELECT LAST_INSERT_ID();
            """;

            var phieuXuatNoiBoId = await connection.ExecuteScalarAsync<uint>(insertPhieuSql, new
            {
                phieu.MaPhieuXuatNoiBo,
                ChiNhanhId = branchId,
                phieu.LoaiXuatNoiBo,
                ThoiGianXuat = now,
                KhoXuat = string.IsNullOrWhiteSpace(phieu.KhoXuat) ? "Kho Tổng (Kho mặc định)" : phieu.KhoXuat,
                KhoNhanHoacBoPhan = phieu.KhoNhanHoacBoPhan,
                phieu.TongSoLuong,
                phieu.TongGiaTri,
                phieu.GhiChu,
                NguoiTaoId = userId,
                TenNguoiTao = userName,
                NgayTao = now
            }, transaction);

            // 3. Thêm chi tiết & Giảm tồn kho
            const string insertChiTietSql = """
                INSERT INTO chi_tiet_phieu_xuat_noi_bo
                (phieu_xuat_noi_bo_id, san_pham_id, so_luong, don_gia, thanh_tien, ghi_chu)
                VALUES
                (@PhieuXuatNoiBoId, @SanPhamId, @SoLuong, @DonGia, @ThanhTien, @GhiChu);
            """;

            const string updateTonKhoSql = """
                UPDATE san_pham 
                SET ton_kho_excel = GREATEST(0, COALESCE(ton_kho_excel, 0) - @SoLuong),
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
            """;

            foreach (var ct in chiTiets)
            {
                await connection.ExecuteAsync(insertChiTietSql, new
                {
                    PhieuXuatNoiBoId = phieuXuatNoiBoId,
                    ct.SanPhamId,
                    ct.SoLuong,
                    ct.DonGia,
                    ct.ThanhTien,
                    ct.GhiChu
                }, transaction);

                await connection.ExecuteAsync(updateTonKhoSql, new
                {
                    ct.SanPhamId,
                    ct.SoLuong
                }, transaction);
            }

            await transaction.CommitAsync();
            return (true, $"Lưu phiếu xuất nội bộ {phieu.MaPhieuXuatNoiBo} thành công!", phieu.MaPhieuXuatNoiBo, phieuXuatNoiBoId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi lưu phiếu xuất nội bộ", ex);
            return (false, "Lỗi khi lưu phiếu xuất nội bộ: " + ex.Message, "", 0);
        }
    }
}
