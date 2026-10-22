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
    private bool? _hasBangPhieuNhapKhac;

    /// <summary>
    /// Đảm bảo bảng phieu_nhap_khac và chi_tiet_phieu_nhap_khac tồn tại.
    /// Nếu bảng trống, nạp mẫu vài phiếu để người dùng kiểm tra ngay giao diện.
    /// </summary>
    public async Task EnsureBangPhieuNhapKhacAsync()
    {
        if (_hasBangPhieuNhapKhac == true)
            return;

        await using var connection = await OpenAsync();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS phieu_nhap_khac (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_phieu_nhap_khac VARCHAR(50) NOT NULL UNIQUE,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                loai_nhap_khac VARCHAR(100) NOT NULL DEFAULT 'NHAP_KHAC',
                thoi_gian_nhap DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                kho_nhan VARCHAR(100) NOT NULL DEFAULT 'Kho Tổng (Kho mặc định)',
                doi_tuong_lien_quan VARCHAR(255) NULL,
                tong_so_luong DECIMAL(15,3) NOT NULL DEFAULT 0,
                tong_gia_tri DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu TEXT NULL,
                trang_thai VARCHAR(30) NOT NULL DEFAULT 'HOAN_THANH',
                nguoi_tao_id INT UNSIGNED NULL,
                ten_nguoi_tao VARCHAR(100) NULL,
                ngay_tao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS chi_tiet_phieu_nhap_khac (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                phieu_nhap_khac_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                so_luong DECIMAL(15,3) NOT NULL DEFAULT 1,
                don_gia DECIMAL(15,2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu VARCHAR(255) NULL,
                INDEX idx_phieu_nhap_khac (phieu_nhap_khac_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        await connection.ExecuteAsync(createTableSql);

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM phieu_nhap_khac;");
        if (count == 0)
        {
            var sampleSql = """
                INSERT INTO phieu_nhap_khac
                (ma_phieu_nhap_khac, chi_nhanh_id, loai_nhap_khac, thoi_gian_nhap, kho_nhan, doi_tuong_lien_quan, tong_so_luong, tong_gia_tri, ghi_chu, trang_thai, ten_nguoi_tao)
                VALUES
                ('NK261014-001', 1, 'NHAP_NOI_BO', DATE_SUB(NOW(), INTERVAL 4 DAY), 'Kho Tổng (Kho mặc định)', 'Chi nhánh 2 - Cầu Giấy', 50, 4200000, 'Điều chuyển hoa và phụ kiện từ Chi nhánh 2 về Kho Tổng', 'HOAN_THANH', 'Quản trị viên'),
                ('NK261015-002', 1, 'NHAP_DIEU_CHINH', DATE_SUB(NOW(), INTERVAL 2 DAY), 'Kho Tổng (Kho mặc định)', 'Bộ phận kiểm kê', 15, 1200000, 'Cân bằng số liệu thực tế sau kiểm kê định kỳ tháng 10', 'HOAN_THANH', 'Quản trị viên'),
                ('NK261016-003', 1, 'NHAP_KHUYEN_MAI', DATE_SUB(NOW(), INTERVAL 1 DAY), 'Kho Tổng (Kho mặc định)', 'Công ty Hoa Tươi Hasfarm', 25, 0, 'Hàng mẫu tặng kèm trong chương trình tri ân đại lý hoa', 'HOAN_THANH', 'Quản trị viên'),
                ('NK261017-004', 1, 'NHAP_THU_HOI', NOW(), 'Kho Tổng (Kho mặc định)', 'Bộ phận sự kiện tiệc cưới Metropole', 30, 2400000, 'Thu hồi giỏ hoa và bình gốm sứ sau khi kết thúc sự kiện', 'HOAN_THANH', 'Quản trị viên');
            """;
            try
            {
                await connection.ExecuteAsync(sampleSql);
            }
            catch { }
        }

        _hasBangPhieuNhapKhac = true;
    }

    /// <summary>
    /// Phát sinh mã phiếu nhập khác tiếp theo theo định dạng NKyyMMdd-NNN.
    /// </summary>
    public async Task<string> TaoMaPhieuNhapKhacMoiAsync()
    {
        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();

        var prefix = $"NK{DateTime.Now:yyMMdd}-";
        const string sql = """
            SELECT ma_phieu_nhap_khac 
            FROM phieu_nhap_khac 
            WHERE ma_phieu_nhap_khac LIKE CONCAT(@Prefix, '%')
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
    /// Lấy danh sách phiếu nhập khác có phân trang và bộ lọc tìm kiếm.
    /// </summary>
    public async Task<(List<PhieuNhapKhac> Items, int TotalCount)> GetPhieuNhapKhacPagedAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiNhapKhac,
        string? trangThai,
        int page,
        int pageSize)
    {
        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pnk.ma_phieu_nhap_khac LIKE @Keyword OR pnk.doi_tuong_lien_quan LIKE @Keyword OR pnk.ghi_chu LIKE @Keyword OR pnk.ten_nguoi_tao LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pnk.thoi_gian_nhap >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pnk.thoi_gian_nhap <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(loaiNhapKhac) && loaiNhapKhac != "TAT_CA")
        {
            conditions.Add("pnk.loai_nhap_khac = @LoaiNhapKhac");
            parameters.Add("LoaiNhapKhac", loaiNhapKhac);
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pnk.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $"""
            SELECT COUNT(*) 
            FROM phieu_nhap_khac pnk
            {whereClause};
        """;

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = Math.Max(0, (page - 1) * pageSize);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var querySql = $"""
            SELECT 
                pnk.id AS Id,
                pnk.ma_phieu_nhap_khac AS MaPhieuNhapKhac,
                pnk.chi_nhanh_id AS ChiNhanhId,
                pnk.loai_nhap_khac AS LoaiNhapKhac,
                pnk.thoi_gian_nhap AS ThoiGianNhap,
                pnk.kho_nhan AS KhoNhan,
                pnk.doi_tuong_lien_quan AS DoiTuongLienQuan,
                pnk.tong_so_luong AS TongSoLuong,
                pnk.tong_gia_tri AS TongGiaTri,
                pnk.ghi_chu AS GhiChu,
                pnk.trang_thai AS TrangThai,
                pnk.nguoi_tao_id AS NguoiTaoId,
                pnk.ten_nguoi_tao AS TenNguoiTao,
                pnk.ngay_tao AS NgayTao
            FROM phieu_nhap_khac pnk
            {whereClause}
            ORDER BY pnk.id DESC
            LIMIT @PageSize OFFSET @Offset;
        """;

        var rows = (await connection.QueryAsync<PhieuNhapKhac>(querySql, parameters)).ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].SoThuTu = offset + i + 1;
        }

        return (rows, totalCount);
    }

    /// <summary>
    /// Thống kê tổng số phiếu, tổng giá trị nhập khác và tổng số lượng sản phẩm nhập theo bộ lọc.
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongGiaTri, decimal TongSoLuong)> GetThongKePhieuNhapKhacAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? loaiNhapKhac,
        string? trangThai)
    {
        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pnk.ma_phieu_nhap_khac LIKE @Keyword OR pnk.doi_tuong_lien_quan LIKE @Keyword OR pnk.ghi_chu LIKE @Keyword OR pnk.ten_nguoi_tao LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pnk.thoi_gian_nhap >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pnk.thoi_gian_nhap <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(loaiNhapKhac) && loaiNhapKhac != "TAT_CA")
        {
            conditions.Add("pnk.loai_nhap_khac = @LoaiNhapKhac");
            parameters.Add("LoaiNhapKhac", loaiNhapKhac);
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pnk.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT 
                COUNT(*) AS TongSoPhieu,
                COALESCE(SUM(pnk.tong_gia_tri), 0) AS TongGiaTri,
                COALESCE(SUM(pnk.tong_so_luong), 0) AS TongSoLuong
            FROM phieu_nhap_khac pnk
            {whereClause};
        """;

        var stat = await connection.QueryFirstOrDefaultAsync<(int TongSoPhieu, decimal TongGiaTri, decimal TongSoLuong)>(sql, parameters);
        return stat;
    }

    /// <summary>
    /// Lấy chi tiết các mặt hàng trong một phiếu nhập khác.
    /// </summary>
    public async Task<List<ChiTietPhieuNhapKhac>> GetChiTietPhieuNhapKhacAsync(uint phieuNhapKhacId)
    {
        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();

        const string sql = """
            SELECT 
                ct.id AS Id,
                ct.phieu_nhap_khac_id AS PhieuNhapKhacId,
                ct.san_pham_id AS SanPhamId,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                ct.thanh_tien AS ThanhTien,
                ct.ghi_chu AS GhiChu,
                sp.ma_san_pham AS MaSanPham,
                sp.ten_san_pham AS TenSanPham,
                COALESCE(dvt.ten_don_vi, '') AS DonViTinh,
                COALESCE(sp.ton_kho_excel, 0) AS TonKho
            FROM chi_tiet_phieu_nhap_khac ct
            JOIN san_pham sp ON ct.san_pham_id = sp.id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            WHERE ct.phieu_nhap_khac_id = @PhieuNhapKhacId
            ORDER BY ct.id ASC;
        """;

        var items = (await connection.QueryAsync<ChiTietPhieuNhapKhac>(sql, new { PhieuNhapKhacId = phieuNhapKhacId })).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].SoThuTu = i + 1;
        }

        return items;
    }

    /// <summary>
    /// Lưu phiếu nhập khác mới cùng các dòng chi tiết theo giao dịch ACID.
    /// Tự động cập nhật tăng số lượng tồn kho (ton_kho_excel) trong bảng san_pham.
    /// Không tạo công nợ và không ghi sổ quỹ (vì là nghiệp vụ nội bộ/kiểm kê/khuyến mãi/thu hồi).
    /// </summary>
    public async Task<(bool Success, string Message, string MaPhieu, uint PhieuId)> LuuPhieuNhapKhacAsync(
        PhieuNhapKhac phieu,
        List<ChiTietPhieuNhapKhac> chiTiets)
    {
        if (chiTiets == null || chiTiets.Count == 0)
        {
            return (false, "Vui lòng chọn ít nhất một mặt hàng để lập phiếu nhập khác.", "", 0);
        }

        foreach (var item in chiTiets)
        {
            if (item.SoLuong <= 0)
            {
                return (false, $"Số lượng nhập của hàng '{item.TenSanPham}' phải lớn hơn 0.", "", 0);
            }
            if (item.DonGia < 0)
            {
                return (false, $"Đơn giá nhập của hàng '{item.TenSanPham}' không được âm.", "", 0);
            }
        }

        await EnsureBangPhieuNhapKhacAsync();
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Sinh mã phiếu nếu chưa có
            if (string.IsNullOrWhiteSpace(phieu.MaPhieuNhapKhac))
            {
                var prefix = $"NK{DateTime.Now:yyMMdd}-";
                const string checkCodeSql = """
                    SELECT ma_phieu_nhap_khac 
                    FROM phieu_nhap_khac 
                    WHERE ma_phieu_nhap_khac LIKE CONCAT(@Prefix, '%')
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
                phieu.MaPhieuNhapKhac = $"{prefix}{nextNumber:D3}";
            }

            // Tính tổng
            phieu.TongSoLuong = chiTiets.Sum(c => c.SoLuong);
            phieu.TongGiaTri = chiTiets.Sum(c => c.ThanhTien);
            var now = phieu.ThoiGianNhap != default ? phieu.ThoiGianNhap : DateTime.Now;
            var userId = Session.CurrentUser?.Id;
            var userName = Session.CurrentUser?.HoTen ?? "Quản trị viên";
            var branchId = phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. Thêm phiếu nhập khác
            const string insertPhieuSql = """
                INSERT INTO phieu_nhap_khac
                (
                    ma_phieu_nhap_khac, chi_nhanh_id, loai_nhap_khac, thoi_gian_nhap,
                    kho_nhan, doi_tuong_lien_quan, tong_so_luong, tong_gia_tri,
                    ghi_chu, trang_thai, nguoi_tao_id, ten_nguoi_tao, ngay_tao
                )
                VALUES
                (
                    @MaPhieuNhapKhac, @ChiNhanhId, @LoaiNhapKhac, @ThoiGianNhap,
                    @KhoNhan, @DoiTuongLienQuan, @TongSoLuong, @TongGiaTri,
                    @GhiChu, 'HOAN_THANH', @NguoiTaoId, @TenNguoiTao, @NgayTao
                );
                SELECT LAST_INSERT_ID();
            """;

            var phieuNhapKhacId = await connection.ExecuteScalarAsync<uint>(insertPhieuSql, new
            {
                phieu.MaPhieuNhapKhac,
                ChiNhanhId = branchId,
                phieu.LoaiNhapKhac,
                ThoiGianNhap = now,
                KhoNhan = string.IsNullOrWhiteSpace(phieu.KhoNhan) ? "Kho Tổng (Kho mặc định)" : phieu.KhoNhan,
                phieu.DoiTuongLienQuan,
                phieu.TongSoLuong,
                phieu.TongGiaTri,
                phieu.GhiChu,
                NguoiTaoId = userId,
                TenNguoiTao = userName,
                NgayTao = now
            }, transaction);

            // 3. Thêm chi tiết & Tăng tồn kho
            const string insertChiTietSql = """
                INSERT INTO chi_tiet_phieu_nhap_khac
                (phieu_nhap_khac_id, san_pham_id, so_luong, don_gia, thanh_tien, ghi_chu)
                VALUES
                (@PhieuNhapKhacId, @SanPhamId, @SoLuong, @DonGia, @ThanhTien, @GhiChu);
            """;

            const string updateTonKhoSql = """
                UPDATE san_pham 
                SET ton_kho_excel = COALESCE(ton_kho_excel, 0) + @SoLuong,
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
            """;

            foreach (var ct in chiTiets)
            {
                await connection.ExecuteAsync(insertChiTietSql, new
                {
                    PhieuNhapKhacId = phieuNhapKhacId,
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
            return (true, $"Lưu phiếu nhập khác {phieu.MaPhieuNhapKhac} thành công!", phieu.MaPhieuNhapKhac, phieuNhapKhacId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi lưu phiếu nhập khác", ex);
            return (false, "Lỗi khi lưu phiếu nhập khác: " + ex.Message, "", 0);
        }
    }
}
