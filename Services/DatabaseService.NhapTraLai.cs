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
    private bool? _hasBangPhieuTraHang;

    /// <summary>
    /// Đảm bảo bảng phieu_tra_hang và chi_tiet_phieu_tra_hang tồn tại.
    /// Nếu bảng trống, tự động nạp mẫu vài phiếu để người dùng kiểm tra giao diện.
    /// </summary>
    public async Task EnsureBangPhieuTraHangAsync()
    {
        if (_hasBangPhieuTraHang == true)
            return;

        await using var connection = await OpenAsync();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS phieu_tra_hang (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_phieu_tra VARCHAR(50) NOT NULL UNIQUE,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                khach_hang_id INT UNSIGNED NULL,
                hoa_don_id INT UNSIGNED NULL,
                ma_hoa_don VARCHAR(50) NULL,
                thoi_gian_tra DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                kho_nhan VARCHAR(100) NOT NULL DEFAULT 'Kho Tổng (Kho mặc định)',
                tong_so_luong DECIMAL(15,3) NOT NULL DEFAULT 0,
                tong_tien_tra DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu TEXT NULL,
                trang_thai VARCHAR(30) NOT NULL DEFAULT 'HOAN_THANH',
                nguoi_tao_id INT UNSIGNED NULL,
                ten_nguoi_tao VARCHAR(100) NULL,
                ngay_tao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS chi_tiet_phieu_tra_hang (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                phieu_tra_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                so_luong DECIMAL(15,3) NOT NULL DEFAULT 1,
                don_gia DECIMAL(15,2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                ly_do_tra VARCHAR(255) NULL,
                INDEX idx_phieu_tra (phieu_tra_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        await connection.ExecuteAsync(createTableSql);

        // Kiểm tra xem bảng đã có dữ liệu chưa
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM phieu_tra_hang;");
        if (count == 0)
        {
            var sampleSql = """
                INSERT INTO phieu_tra_hang (ma_phieu_tra, chi_nhanh_id, khach_hang_id, ma_hoa_don, thoi_gian_tra, kho_nhan, tong_so_luong, tong_tien_tra, ghi_chu, trang_thai, ten_nguoi_tao)
                VALUES 
                ('TL261015-001', 1, 1, 'HD261014-001', DATE_SUB(NOW(), INTERVAL 2 DAY), 'Kho Tổng (Kho mặc định)', 5, 250000, 'Khách đổi mẫu hoa bó', 'HOAN_THANH', 'Quản trị viên'),
                ('TL261016-002', 1, 2, 'HD261015-003', DATE_SUB(NOW(), INTERVAL 1 DAY), 'Kho Tổng (Kho mặc định)', 2, 180000, 'Bình cắm hoa bị nứt viền', 'HOAN_THANH', 'Quản trị viên'),
                ('TL261017-003', 1, NULL, 'HD261017-002', NOW(), 'Kho Tổng (Kho mặc định)', 1, 75000, 'Khách trả lại giỏ mây phụ kiện', 'HOAN_THANH', 'Quản trị viên');
            """;
            try
            {
                await connection.ExecuteAsync(sampleSql);
            }
            catch { }
        }

        _hasBangPhieuTraHang = true;
    }

    /// <summary>
    /// Phát sinh mã phiếu trả hàng tiếp theo theo định dạng TLyyMMdd-NNN.
    /// </summary>
    public async Task<string> TaoMaPhieuTraMoiAsync()
    {
        await EnsureBangPhieuTraHangAsync();
        await using var connection = await OpenAsync();

        var prefix = $"TL{DateTime.Now:yyMMdd}-";
        const string sql = """
            SELECT ma_phieu_tra 
            FROM phieu_tra_hang 
            WHERE ma_phieu_tra LIKE CONCAT(@Prefix, '%')
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
    /// Lấy danh sách phiếu nhập trả lại có phân trang và bộ lọc tìm kiếm.
    /// </summary>
    public async Task<(List<PhieuTraHang> Items, int TotalCount)> GetPhieuTraHangPagedAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? trangThai,
        int page,
        int pageSize)
    {
        await EnsureBangPhieuTraHangAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pth.ma_phieu_tra LIKE @Keyword OR kh.ten_khach_hang LIKE @Keyword OR kh.so_dien_thoai LIKE @Keyword OR pth.ma_hoa_don LIKE @Keyword OR pth.ghi_chu LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pth.thoi_gian_tra >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pth.thoi_gian_tra <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pth.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $"""
            SELECT COUNT(*) 
            FROM phieu_tra_hang pth
            LEFT JOIN khach_hang kh ON pth.khach_hang_id = kh.id
            {whereClause};
        """;

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = Math.Max(0, (page - 1) * pageSize);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var querySql = $"""
            SELECT 
                pth.id AS Id,
                pth.ma_phieu_tra AS MaPhieuTra,
                pth.chi_nhanh_id AS ChiNhanhId,
                pth.khach_hang_id AS KhachHangId,
                pth.hoa_don_id AS HoaDonId,
                pth.ma_hoa_don AS MaHoaDon,
                pth.thoi_gian_tra AS ThoiGianTra,
                pth.kho_nhan AS KhoNhan,
                pth.tong_so_luong AS TongSoLuong,
                pth.tong_tien_tra AS TongTienTra,
                pth.ghi_chu AS GhiChu,
                pth.trang_thai AS TrangThai,
                pth.nguoi_tao_id AS NguoiTaoId,
                pth.ten_nguoi_tao AS TenNguoiTao,
                pth.ngay_tao AS NgayTao,
                kh.ten_khach_hang AS TenKhachHang,
                kh.ma_khach_hang AS MaKhachHang,
                kh.so_dien_thoai AS SoDienThoaiKhach
            FROM phieu_tra_hang pth
            LEFT JOIN khach_hang kh ON pth.khach_hang_id = kh.id
            {whereClause}
            ORDER BY pth.id DESC
            LIMIT @PageSize OFFSET @Offset;
        """;

        var rows = (await connection.QueryAsync<PhieuTraHang>(querySql, parameters)).ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].SoThuTu = offset + i + 1;
        }

        return (rows, totalCount);
    }

    /// <summary>
    /// Thống kê tổng số phiếu, tổng tiền hoàn trả và tổng số sản phẩm trả lại theo bộ lọc.
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongTienTra, decimal TongSoLuongTra)> GetThongKePhieuTraHangAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? trangThai)
    {
        await EnsureBangPhieuTraHangAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pth.ma_phieu_tra LIKE @Keyword OR kh.ten_khach_hang LIKE @Keyword OR kh.so_dien_thoai LIKE @Keyword OR pth.ma_hoa_don LIKE @Keyword OR pth.ghi_chu LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pth.thoi_gian_tra >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pth.thoi_gian_tra <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pth.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT 
                COUNT(*) AS TongSoPhieu,
                COALESCE(SUM(pth.tong_tien_tra), 0) AS TongTienTra,
                COALESCE(SUM(pth.tong_so_luong), 0) AS TongSoLuongTra
            FROM phieu_tra_hang pth
            LEFT JOIN khach_hang kh ON pth.khach_hang_id = kh.id
            {whereClause};
        """;

        var stat = await connection.QueryFirstOrDefaultAsync<(int TongSoPhieu, decimal TongTienTra, decimal TongSoLuongTra)>(sql, parameters);
        return stat;
    }

    /// <summary>
    /// Lấy chi tiết các mặt hàng trong một phiếu trả hàng.
    /// </summary>
    public async Task<List<ChiTietPhieuTraHang>> GetChiTietPhieuTraHangAsync(uint phieuTraId)
    {
        await EnsureBangPhieuTraHangAsync();
        await using var connection = await OpenAsync();

        const string sql = """
            SELECT 
                ct.id AS Id,
                ct.phieu_tra_id AS PhieuTraId,
                ct.san_pham_id AS SanPhamId,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                ct.thanh_tien AS ThanhTien,
                ct.ly_do_tra AS LyDoTra,
                sp.ma_san_pham AS MaSanPham,
                sp.ten_san_pham AS TenSanPham,
                COALESCE(dvt.ten_don_vi, '') AS DonViTinh
            FROM chi_tiet_phieu_tra_hang ct
            JOIN san_pham sp ON ct.san_pham_id = sp.id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            WHERE ct.phieu_tra_id = @PhieuTraId
            ORDER BY ct.id ASC;
        """;

        var items = (await connection.QueryAsync<ChiTietPhieuTraHang>(sql, new { PhieuTraId = phieuTraId })).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].SoThuTu = i + 1;
        }

        return items;
    }

    /// <summary>
    /// Tìm kiếm gợi ý các hóa đơn gốc bán ra để lập phiếu trả hàng.
    /// </summary>
    public async Task<List<HoaDonGocGoiY>> TimKiemHoaDonGocAsync(string? keyword, uint? khachHangId = null, int limit = 10)
    {
        await using var connection = await OpenAsync();

        var conditions = new List<string> { "hd.trang_thai_hoa_don = 'HOAN_THANH'" };
        var p = new DynamicParameters();

        if (khachHangId.HasValue && khachHangId.Value > 0)
        {
            conditions.Add("hd.khach_hang_id = @KhachHangId");
            p.Add("KhachHangId", khachHangId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = $"%{keyword.Trim()}%";
            conditions.Add("(hd.ma_hoa_don LIKE @Kw OR kh.ten_khach_hang LIKE @Kw OR kh.so_dien_thoai LIKE @Kw)");
            p.Add("Kw", kw);
        }

        var whereClause = string.Join(" AND ", conditions);
        p.Add("Limit", limit);

        var sql = $"""
            SELECT 
                hd.id AS Id,
                hd.ma_hoa_don AS MaHoaDon,
                hd.thoi_gian_ban AS ThoiGianBan,
                hd.khach_hang_id AS KhachHangId,
                COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
                hd.khach_can_tra AS KhachCanTra,
                hd.khach_da_thanh_toan AS KhachDaThanhToan
            FROM hoa_don_ban_hang hd
            LEFT JOIN khach_hang kh ON hd.khach_hang_id = kh.id
            WHERE {whereClause}
            ORDER BY hd.thoi_gian_ban DESC
            LIMIT @Limit;
        """;

        return (await connection.QueryAsync<HoaDonGocGoiY>(sql, p)).ToList();
    }

    /// <summary>
    /// Lấy danh sách chi tiết các mặt hàng trong hóa đơn gốc cùng với số lượng đã trả trước đó.
    /// </summary>
    public async Task<List<ChiTietPhieuTraHang>> GetChiTietHoaDonChoTraHangAsync(uint hoaDonId)
    {
        await EnsureBangPhieuTraHangAsync();
        await using var connection = await OpenAsync();

        const string sql = """
            SELECT 
                ct.san_pham_id AS SanPhamId,
                ct.ma_san_pham AS MaSanPham,
                ct.ten_san_pham AS TenSanPham,
                COALESCE(ct.don_vi_tinh, '') AS DonViTinh,
                ct.so_luong AS SoLuongMua,
                ct.don_gia AS DonGia,
                COALESCE((
                    SELECT SUM(cp.so_luong)
                    FROM chi_tiet_phieu_tra_hang cp
                    JOIN phieu_tra_hang pth ON cp.phieu_tra_id = pth.id
                    WHERE pth.hoa_don_id = @HoaDonId
                      AND cp.san_pham_id = ct.san_pham_id
                      AND pth.trang_thai != 'DA_HUY'
                ), 0) AS DaTra
            FROM chi_tiet_hoa_don_ban_hang ct
            WHERE ct.hoa_don_id = @HoaDonId
            ORDER BY ct.id ASC;
        """;

        var rows = (await connection.QueryAsync(sql, new { HoaDonId = hoaDonId })).ToList();
        var result = new List<ChiTietPhieuTraHang>();
        var index = 1;

        foreach (var r in rows)
        {
            var soLuongMua = (decimal)r.SoLuongMua;
            var daTra = (decimal)r.DaTra;
            var conLai = Math.Max(0, soLuongMua - daTra);

            var item = new ChiTietPhieuTraHang
            {
                SoThuTu = index++,
                SanPhamId = (uint)r.SanPhamId,
                MaSanPham = (string)r.MaSanPham,
                TenSanPham = (string)r.TenSanPham,
                DonViTinh = (string)r.DonViTinh,
                DonGia = (decimal)r.DonGia,
                SoLuongMua = soLuongMua,
                DaTra = daTra,
                SoLuong = conLai > 0 ? (conLai >= 1 ? 1 : conLai) : 0,
                LyDoTra = "Khách đổi trả hàng theo hóa đơn"
            };
            item.ThanhTien = Math.Round(item.SoLuong * item.DonGia, 2);
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Lưu phiếu nhập trả lại hàng: Ghi phiếu trả, chi tiết trả, cập nhật tồn kho (+),
    /// hoàn tiền (ghi sổ quỹ hoặc trừ nợ khách hàng) trong MỘT giao dịch duy nhất.
    /// </summary>
    public async Task<string> LuuPhieuTraHangAsync(
        PhieuTraHang phieu,
        List<ChiTietPhieuTraHang> chiTiets,
        string phuongThucHoanTien)
    {
        if (chiTiets.Count == 0)
            throw new InvalidOperationException("Phiếu trả hàng phải có ít nhất một mặt hàng.");

        foreach (var item in chiTiets)
        {
            if (item.SoLuong <= 0)
                throw new InvalidOperationException($"Sản phẩm \"{item.TenSanPham}\" có số lượng trả không hợp lệ (phải > 0).");
            if (item.HasHoaDonGoc && item.SoLuong > item.SoLuongToiDa)
                throw new InvalidOperationException($"Sản phẩm \"{item.TenSanPham}\" chỉ còn được trả tối đa {item.SoLuongToiDa:N0}.");
        }

        await EnsureBangPhieuTraHangAsync();
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;

        try
        {
            // 1. Sinh mã phiếu trả hàng nếu chưa có
            if (string.IsNullOrWhiteSpace(phieu.MaPhieuTra))
            {
                var prefix = $"TL{DateTime.Now:yyMMdd}-";
                const string checkCodeSql = """
                    SELECT ma_phieu_tra 
                    FROM phieu_tra_hang 
                    WHERE ma_phieu_tra LIKE CONCAT(@Prefix, '%')
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
                phieu.MaPhieuTra = $"{prefix}{nextNumber:D3}";
            }

            // Tính tổng
            phieu.TongSoLuong = chiTiets.Sum(c => c.SoLuong);
            phieu.TongTienTra = chiTiets.Sum(c => c.ThanhTien);
            var now = phieu.ThoiGianTra != default ? phieu.ThoiGianTra : DateTime.Now;
            var userId = Session.CurrentUser?.Id;
            var userName = Session.CurrentUser?.HoTen ?? "Quản trị viên";
            var branchId = phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. Thêm phiếu trả hàng
            const string insertPhieuSql = """
                INSERT INTO phieu_tra_hang
                (
                    ma_phieu_tra, chi_nhanh_id, khach_hang_id, hoa_don_id, ma_hoa_don,
                    thoi_gian_tra, kho_nhan, tong_so_luong, tong_tien_tra, ghi_chu,
                    trang_thai, nguoi_tao_id, ten_nguoi_tao, ngay_tao
                )
                VALUES
                (
                    @MaPhieuTra, @ChiNhanhId, @KhachHangId, @HoaDonId, @MaHoaDon,
                    @ThoiGianTra, @KhoNhan, @TongSoLuong, @TongTienTra, @GhiChu,
                    'HOAN_THANH', @NguoiTaoId, @TenNguoiTao, @NgayTao
                );
                SELECT LAST_INSERT_ID();
            """;

            var phieuTraId = await connection.ExecuteScalarAsync<uint>(insertPhieuSql, new
            {
                phieu.MaPhieuTra,
                ChiNhanhId = branchId,
                phieu.KhachHangId,
                phieu.HoaDonId,
                phieu.MaHoaDon,
                ThoiGianTra = now,
                phieu.KhoNhan,
                phieu.TongSoLuong,
                phieu.TongTienTra,
                phieu.GhiChu,
                NguoiTaoId = userId,
                TenNguoiTao = userName,
                NgayTao = now
            }, transaction);

            // 3. Thêm chi tiết phiếu trả hàng
            const string insertChiTietSql = """
                INSERT INTO chi_tiet_phieu_tra_hang
                (phieu_tra_id, san_pham_id, so_luong, don_gia, thanh_tien, ly_do_tra)
                VALUES
                (@PhieuTraId, @SanPhamId, @SoLuong, @DonGia, @ThanhTien, @LyDoTra);
            """;

            const string updateTonKhoSql = """
                UPDATE san_pham 
                SET ton_kho_excel = ton_kho_excel + @SoLuong,
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
            """;

            foreach (var item in chiTiets)
            {
                await connection.ExecuteAsync(insertChiTietSql, new
                {
                    PhieuTraId = phieuTraId,
                    item.SanPhamId,
                    item.SoLuong,
                    item.DonGia,
                    item.ThanhTien,
                    item.LyDoTra
                }, transaction);

                // Cập nhật tăng tồn kho
                await connection.ExecuteAsync(updateTonKhoSql, new
                {
                    item.SoLuong,
                    item.SanPhamId
                }, transaction);
            }

            // 4. Xử lý hoàn tiền / trừ công nợ
            if (phuongThucHoanTien == "Trừ công nợ" && phieu.KhachHangId.HasValue && phieu.KhachHangId.Value > 0)
            {
                // Giảm công nợ khách hàng (Ghi có)
                const string currentDebtSql = """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_khach_hang
                    WHERE khach_hang_id = @KhachHangId
                    FOR UPDATE;
                """;

                var currentDebt = await connection.ExecuteScalarAsync<decimal>(
                    currentDebtSql,
                    new { KhachHangId = phieu.KhachHangId.Value },
                    transaction);

                var newDebtBalance = currentDebt - phieu.TongTienTra;

                const string debtSql = """
                    INSERT INTO lich_su_cong_no_khach_hang
                    (
                        khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                        ma_chung_tu, ghi_no, ghi_co, so_du,
                        thoi_gian, nguoi_thuc_hien_id, ghi_chu
                    )
                    VALUES
                    (
                        @KhachHangId, @ChiNhanhId, 'TRA_HANG', 'PHIEU_TRA', @MaChungTu,
                        0, @GhiCo, @SoDu, @ThoiGian, @NguoiThucHienId, @GhiChu
                    );
                """;

                await connection.ExecuteAsync(debtSql, new
                {
                    KhachHangId = phieu.KhachHangId.Value,
                    ChiNhanhId = branchId,
                    MaChungTu = phieu.MaPhieuTra,
                    GhiCo = phieu.TongTienTra,
                    SoDu = newDebtBalance,
                    ThoiGian = now,
                    NguoiThucHienId = userId,
                    GhiChu = $"Giảm trừ công nợ do trả hàng theo phiếu {phieu.MaPhieuTra}"
                }, transaction);
            }
            else
            {
                // Hoàn tiền mặt hoặc chuyển khoản -> Tạo phiếu chi trong sổ quỹ nếu bảng phieu_chi tồn tại
                try
                {
                    var hasPhieuChi = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi';",
                        transaction: transaction) > 0;

                    if (hasPhieuChi)
                    {
                        var prefixChi = $"PC{DateTime.Now:yyMMdd}-";
                        const string lastChiSql = """
                            SELECT ma_phieu_chi 
                            FROM phieu_chi 
                            WHERE ma_phieu_chi LIKE CONCAT(@Prefix, '%')
                            ORDER BY id DESC 
                            LIMIT 1 
                            FOR UPDATE;
                        """;
                        var lastChiCode = await connection.QueryFirstOrDefaultAsync<string>(lastChiSql, new { Prefix = prefixChi }, transaction);
                        var nextChiNum = 1;
                        if (!string.IsNullOrWhiteSpace(lastChiCode) && lastChiCode.Length >= prefixChi.Length)
                        {
                            if (int.TryParse(lastChiCode.Substring(prefixChi.Length), out var cn))
                                nextChiNum = cn + 1;
                        }
                        var maPhieuChi = $"{prefixChi}{nextChiNum:D3}";

                        var phuongThucChi = phuongThucHoanTien == "Chuyển khoản" ? "Chuyển khoản" : "Tiền mặt";
                        var tenNguoiNhan = !string.IsNullOrWhiteSpace(phieu.TenKhachHang) ? phieu.TenKhachHang : "Khách lẻ";

                        const string insertChiSql = """
                            INSERT INTO phieu_chi
                            (
                                ma_phieu_chi, chi_nhanh_id, so_tien, phuong_thuc,
                                noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                                nguoi_thuc_hien_id, trang_thai
                            )
                            VALUES
                            (
                                @MaPhieuChi, @ChiNhanhId, @SoTien, @PhuongThuc,
                                @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGianChi,
                                @NguoiThucHienId, 'HOAN_THANH'
                            );
                        """;

                        await connection.ExecuteAsync(insertChiSql, new
                        {
                            MaPhieuChi = maPhieuChi,
                            ChiNhanhId = branchId,
                            SoTien = phieu.TongTienTra,
                            PhuongThuc = phuongThucChi,
                            NoiDung = $"Chi tiền hoàn trả hàng cho {tenNguoiNhan} (Phiếu trả: {phieu.MaPhieuTra})",
                            NguoiNopNhan = tenNguoiNhan,
                            GhiChu = phieu.GhiChu ?? "",
                            ThoiGianChi = now,
                            NguoiThucHienId = userId
                        }, transaction);
                    }
                }
                catch (Exception exChi)
                {
                    SafeLog.WriteError("Không tạo được phiếu chi tự động cho phiếu trả hàng", exChi);
                }
            }

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Nhập trả lại",
                "PHIEU_TRA",
                phieuTraId,
                phieu.MaPhieuTra,
                $"Lập phiếu nhập trả lại {phieu.MaPhieuTra} cho {phieu.KhachHangHienThi} ({phieu.TongSoLuong:N0} SP, {phieu.TongTienTra:N0} đ)",
                phieu.TongTienTra);

            return phieu.MaPhieuTra;
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch { }
            }
            SafeLog.WriteError("Lỗi khi lưu phiếu trả hàng", ex);
            throw;
        }
    }
}
