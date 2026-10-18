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
    private bool? _hasBangPhieuXuatTraNcc;

    /// <summary>
    /// Đảm bảo bảng phieu_xuat_tra_ncc và chi_tiet_phieu_xuat_tra_ncc tồn tại.
    /// Nếu bảng trống, nạp mẫu vài phiếu để người dùng kiểm tra ngay giao diện.
    /// </summary>
    public async Task EnsureBangPhieuXuatTraNccAsync()
    {
        if (_hasBangPhieuXuatTraNcc == true)
            return;

        await using var connection = await OpenAsync();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS phieu_xuat_tra_ncc (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_phieu_xuat_tra VARCHAR(50) NOT NULL UNIQUE,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                nha_cung_cap_id INT UNSIGNED NULL,
                ten_nha_cung_cap VARCHAR(255) NULL,
                ma_nha_cung_cap VARCHAR(50) NULL,
                so_dien_thoai_ncc VARCHAR(50) NULL,
                phieu_nhap_goc_id INT UNSIGNED NULL,
                ma_phieu_nhap_goc VARCHAR(50) NULL,
                thoi_gian_xuat_tra DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                kho_xuat VARCHAR(100) NOT NULL DEFAULT 'Kho Tổng (Kho mặc định)',
                tong_so_luong DECIMAL(15,3) NOT NULL DEFAULT 0,
                tong_tien_tra DECIMAL(15,2) NOT NULL DEFAULT 0,
                ghi_chu TEXT NULL,
                trang_thai VARCHAR(30) NOT NULL DEFAULT 'HOAN_THANH',
                nguoi_tao_id INT UNSIGNED NULL,
                ten_nguoi_tao VARCHAR(100) NULL,
                ngay_tao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS chi_tiet_phieu_xuat_tra_ncc (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                phieu_xuat_tra_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                so_luong DECIMAL(15,3) NOT NULL DEFAULT 1,
                don_gia DECIMAL(15,2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                ly_do_xuat_tra VARCHAR(255) NULL,
                INDEX idx_phieu_xuat_tra (phieu_xuat_tra_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        await connection.ExecuteAsync(createTableSql);

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM phieu_xuat_tra_ncc;");
        if (count == 0)
        {
            var sampleSql = """
                INSERT INTO phieu_xuat_tra_ncc 
                (ma_phieu_xuat_tra, chi_nhanh_id, ten_nha_cung_cap, ma_nha_cung_cap, so_dien_thoai_ncc, ma_phieu_nhap_goc, thoi_gian_xuat_tra, kho_xuat, tong_so_luong, tong_tien_tra, ghi_chu, trang_thai, ten_nguoi_tao)
                VALUES
                ('XT261014-001', 1, 'Vườn Hoa Đà Lạt Hasfarm', 'NCC001', '0912345678', 'PN261013-001', DATE_SUB(NOW(), INTERVAL 3 DAY), 'Kho Tổng (Kho mặc định)', 20, 1600000, 'Hoa cúc Calimero bị dập nát do vận chuyển', 'HOAN_THANH', 'Quản trị viên'),
                ('XT261016-002', 1, 'Nhà Vườn Hoa Tươi Mê Linh', 'NCC002', '0987654321', 'PN261015-002', DATE_SUB(NOW(), INTERVAL 1 DAY), 'Kho Tổng (Kho mặc định)', 15, 1250000, 'Hoa hồng đỏ cánh ngắn không đúng quy cách', 'HOAN_THANH', 'Quản trị viên'),
                ('XT261017-003', 1, 'Công ty Phụ liệu Hoa Sài Gòn', 'NCC003', '0903112233', 'PN261017-001', NOW(), 'Kho Tổng (Kho mặc định)', 10, 450000, 'Giỏ gỗ cắm hoa bị nứt đế', 'HOAN_THANH', 'Quản trị viên');
            """;
            try
            {
                await connection.ExecuteAsync(sampleSql);
            }
            catch { }
        }

        _hasBangPhieuXuatTraNcc = true;
    }

    /// <summary>
    /// Phát sinh mã phiếu xuất hàng trả lại NCC tiếp theo theo định dạng XTyyMMdd-NNN.
    /// </summary>
    public async Task<string> TaoMaPhieuXuatTraMoiAsync()
    {
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        var prefix = $"XT{DateTime.Now:yyMMdd}-";
        const string sql = """
            SELECT ma_phieu_xuat_tra 
            FROM phieu_xuat_tra_ncc 
            WHERE ma_phieu_xuat_tra LIKE CONCAT(@Prefix, '%')
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
    /// Lấy danh sách phiếu xuất hàng trả lại NCC có phân trang và bộ lọc tìm kiếm.
    /// </summary>
    public async Task<(List<PhieuXuatTraHang> Items, int TotalCount)> GetPhieuXuatTraPagedAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? trangThai,
        int page,
        int pageSize)
    {
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pxt.ma_phieu_xuat_tra LIKE @Keyword OR pxt.ten_nha_cung_cap LIKE @Keyword OR pxt.ma_nha_cung_cap LIKE @Keyword OR pxt.ma_phieu_nhap_goc LIKE @Keyword OR pxt.ghi_chu LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pxt.thoi_gian_xuat_tra >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pxt.thoi_gian_xuat_tra <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pxt.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $"""
            SELECT COUNT(*) 
            FROM phieu_xuat_tra_ncc pxt
            {whereClause};
        """;

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = Math.Max(0, (page - 1) * pageSize);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var querySql = $"""
            SELECT 
                pxt.id AS Id,
                pxt.ma_phieu_xuat_tra AS MaPhieuXuatTra,
                pxt.chi_nhanh_id AS ChiNhanhId,
                pxt.nha_cung_cap_id AS NhaCungCapId,
                pxt.ten_nha_cung_cap AS TenNhaCungCap,
                pxt.ma_nha_cung_cap AS MaNhaCungCap,
                pxt.so_dien_thoai_ncc AS SoDienThoaiNcc,
                pxt.phieu_nhap_goc_id AS PhieuNhapGocId,
                pxt.ma_phieu_nhap_goc AS MaPhieuNhapGoc,
                pxt.thoi_gian_xuat_tra AS ThoiGianXuatTra,
                pxt.kho_xuat AS KhoXuat,
                pxt.tong_so_luong AS TongSoLuong,
                pxt.tong_tien_tra AS TongTienTra,
                pxt.ghi_chu AS GhiChu,
                pxt.trang_thai AS TrangThai,
                pxt.nguoi_tao_id AS NguoiTaoId,
                pxt.ten_nguoi_tao AS TenNguoiTao,
                pxt.ngay_tao AS NgayTao
            FROM phieu_xuat_tra_ncc pxt
            {whereClause}
            ORDER BY pxt.id DESC
            LIMIT @PageSize OFFSET @Offset;
        """;

        var rows = (await connection.QueryAsync<PhieuXuatTraHang>(querySql, parameters)).ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].SoThuTu = offset + i + 1;
        }

        return (rows, totalCount);
    }

    /// <summary>
    /// Thống kê tổng số phiếu, tổng tiền xuất trả NCC và tổng số lượng sản phẩm xuất trả theo bộ lọc.
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongTienTra, decimal TongSoLuongTra)> GetThongKePhieuXuatTraAsync(
        string? search,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? trangThai)
    {
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";
            conditions.Add("(pxt.ma_phieu_xuat_tra LIKE @Keyword OR pxt.ten_nha_cung_cap LIKE @Keyword OR pxt.ma_nha_cung_cap LIKE @Keyword OR pxt.ma_phieu_nhap_goc LIKE @Keyword OR pxt.ghi_chu LIKE @Keyword)");
            parameters.Add("Keyword", keyword);
        }

        if (tuNgay.HasValue)
        {
            conditions.Add("pxt.thoi_gian_xuat_tra >= @TuNgay");
            parameters.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            conditions.Add("pxt.thoi_gian_xuat_tra <= @DenNgay");
            parameters.Add("DenNgay", denNgay.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TAT_CA")
        {
            conditions.Add("pxt.trang_thai = @TrangThai");
            parameters.Add("TrangThai", trangThai);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT 
                COUNT(*) AS TongSoPhieu,
                COALESCE(SUM(pxt.tong_tien_tra), 0) AS TongTienTra,
                COALESCE(SUM(pxt.tong_so_luong), 0) AS TongSoLuongTra
            FROM phieu_xuat_tra_ncc pxt
            {whereClause};
        """;

        var stat = await connection.QueryFirstOrDefaultAsync<(int TongSoPhieu, decimal TongTienTra, decimal TongSoLuongTra)>(sql, parameters);
        return stat;
    }

    /// <summary>
    /// Lấy chi tiết các mặt hàng trong một phiếu xuất trả hàng NCC.
    /// </summary>
    public async Task<List<ChiTietPhieuXuatTraHang>> GetChiTietPhieuXuatTraAsync(uint phieuXuatTraId)
    {
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        const string sql = """
            SELECT 
                ct.id AS Id,
                ct.phieu_xuat_tra_id AS PhieuXuatTraId,
                ct.san_pham_id AS SanPhamId,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                ct.thanh_tien AS ThanhTien,
                ct.ly_do_xuat_tra AS LyDoXuatTra,
                sp.ma_san_pham AS MaSanPham,
                sp.ten_san_pham AS TenSanPham,
                COALESCE(sp.don_vi_tinh, '') AS DonViTinh,
                COALESCE(sp.ton_kho_excel, 0) AS TonKho
            FROM chi_tiet_phieu_xuat_tra_ncc ct
            JOIN san_pham sp ON ct.san_pham_id = sp.id
            WHERE ct.phieu_xuat_tra_id = @PhieuXuatTraId
            ORDER BY ct.id ASC;
        """;

        var items = (await connection.QueryAsync<ChiTietPhieuXuatTraHang>(sql, new { PhieuXuatTraId = phieuXuatTraId })).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].SoThuTu = i + 1;
        }

        return items;
    }

    /// <summary>
    /// Tìm kiếm phiếu nhập gốc để người dùng chọn liên kết khi lập phiếu xuất trả NCC.
    /// </summary>
    public async Task<List<PhieuNhapGocGoiY>> TimKiemPhieuNhapGocChoXuatTraAsync(
        string? keyword,
        uint? nhaCungCapId = null,
        int limit = 15)
    {
        await using var connection = await OpenAsync();

        var conditions = new List<string> { "p.trang_thai NOT IN ('HUY', 'LUU_TAM')" };
        var parameters = new DynamicParameters();

        if (nhaCungCapId.HasValue && nhaCungCapId.Value > 0)
        {
            conditions.Add("p.nha_cung_cap_id = @NccId");
            parameters.Add("NccId", nhaCungCapId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = $"%{keyword.Trim()}%";
            conditions.Add("(p.ma_phieu_nhap LIKE @Keyword OR ncc.ten_nha_cung_cap LIKE @Keyword OR ncc.ma_nha_cung_cap LIKE @Keyword OR ncc.so_dien_thoai LIKE @Keyword)");
            parameters.Add("Keyword", kw);
        }

        parameters.Add("Limit", limit);

        var sql = $"""
            SELECT 
                p.id AS Id,
                p.ma_phieu_nhap AS MaPhieuNhap,
                p.thoi_gian_nhap AS ThoiGianNhap,
                p.nha_cung_cap_id AS NhaCungCapId,
                ncc.ten_nha_cung_cap AS TenNhaCungCap,
                ncc.ma_nha_cung_cap AS MaNhaCungCap,
                ncc.so_dien_thoai AS SoDienThoaiNcc,
                COALESCE(p.tong_tien_nhap, 0) AS TongTienNhap
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap ncc ON p.nha_cung_cap_id = ncc.id
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY p.id DESC
            LIMIT @Limit;
        """;

        return (await connection.QueryAsync<PhieuNhapGocGoiY>(sql, parameters)).ToList();
    }

    /// <summary>
    /// Lấy danh sách chi tiết các mặt hàng trong phiếu nhập gốc kèm số lượng đã xuất trả trước đó.
    /// Dùng để đổ thẳng mặt hàng vào bảng chi tiết xuất trả.
    /// </summary>
    public async Task<List<ChiTietPhieuXuatTraHang>> GetChiTietPhieuNhapChoXuatTraAsync(uint phieuNhapId)
    {
        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();

        const string sql = """
            SELECT 
                ct.san_pham_id AS SanPhamId,
                ct.ma_san_pham AS MaSanPham,
                ct.ten_san_pham AS TenSanPham,
                COALESCE(sp.don_vi_tinh, '') AS DonViTinh,
                COALESCE(sp.ton_kho_excel, 0) AS TonKho,
                ct.so_luong AS SoLuongMua,
                ct.don_gia_nhap AS DonGia,
                COALESCE(da_tra.TongDaTra, 0) AS DaXuatTra
            FROM chi_tiet_phieu_nhap_hang ct
            JOIN san_pham sp ON ct.san_pham_id = sp.id
            LEFT JOIN (
                SELECT cxt.san_pham_id, SUM(cxt.so_luong) AS TongDaTra
                FROM chi_tiet_phieu_xuat_tra_ncc cxt
                JOIN phieu_xuat_tra_ncc pxt ON cxt.phieu_xuat_tra_id = pxt.id
                WHERE pxt.phieu_nhap_goc_id = @PhieuNhapId
                  AND pxt.trang_thai = 'HOAN_THANH'
                GROUP BY cxt.san_pham_id
            ) da_tra ON ct.san_pham_id = da_tra.san_pham_id
            WHERE ct.phieu_nhap_id = @PhieuNhapId
            ORDER BY ct.id ASC;
        """;

        var rows = await connection.QueryAsync(sql, new { PhieuNhapId = phieuNhapId });
        var result = new List<ChiTietPhieuXuatTraHang>();
        var index = 1;

        foreach (var r in rows)
        {
            var soLuongMua = (decimal)r.SoLuongMua;
            var daTra = (decimal)r.DaXuatTra;
            var conLai = Math.Max(0, soLuongMua - daTra);

            var item = new ChiTietPhieuXuatTraHang
            {
                SoThuTu = index++,
                SanPhamId = (uint)r.SanPhamId,
                MaSanPham = (string)r.MaSanPham,
                TenSanPham = (string)r.TenSanPham,
                DonViTinh = (string)r.DonViTinh,
                TonKho = (decimal)r.TonKho,
                DonGia = (decimal)r.DonGia,
                SoLuongMua = soLuongMua,
                DaXuatTra = daTra,
                SoLuong = conLai > 0 ? (conLai >= 1 ? 1 : conLai) : 0,
                LyDoXuatTra = "Trả hàng Nhà cung cấp theo phiếu nhập"
            };
            item.ThanhTien = Math.Round(item.SoLuong * item.DonGia, 2);
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Lưu phiếu xuất hàng trả lại Nhà cung cấp trong MỘT giao dịch ACID nguyên tử:
    /// 1. Ghi phiếu xuất trả (phieu_xuat_tra_ncc).
    /// 2. Ghi chi tiết xuất trả (chi_tiet_phieu_xuat_tra_ncc).
    /// 3. Giảm tồn kho sản phẩm (ton_kho_excel).
    /// 4. Nếu phương thức "Trừ công nợ": ghi Có sổ cái công nợ NCC (giảm nợ phải trả).
    /// 5. Nếu phương thức "Tiền mặt" / "Chuyển khoản": tạo phiếu Thu trong sổ quỹ (NCC hoàn tiền về quỹ).
    /// </summary>
    public async Task<string> LuuPhieuXuatTraHangAsync(
        PhieuXuatTraHang phieu,
        List<ChiTietPhieuXuatTraHang> chiTiets,
        string phuongThucHoanTien)
    {
        if (chiTiets.Count == 0)
            throw new InvalidOperationException("Phiếu xuất trả NCC phải có ít nhất một mặt hàng.");

        foreach (var item in chiTiets)
        {
            if (item.SoLuong <= 0)
                throw new InvalidOperationException($"Mặt hàng \"{item.TenSanPham}\" có số lượng trả không hợp lệ (phải > 0).");
            if (item.HasPhieuNhapGoc && item.SoLuong > item.SoLuongToiDa)
                throw new InvalidOperationException($"Mặt hàng \"{item.TenSanPham}\" chỉ còn được trả tối đa {item.SoLuongToiDa:N0}.");
        }

        await EnsureBangPhieuXuatTraNccAsync();
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Sinh mã phiếu nếu chưa có
            if (string.IsNullOrWhiteSpace(phieu.MaPhieuXuatTra))
            {
                var prefix = $"XT{DateTime.Now:yyMMdd}-";
                const string checkCodeSql = """
                    SELECT ma_phieu_xuat_tra 
                    FROM phieu_xuat_tra_ncc 
                    WHERE ma_phieu_xuat_tra LIKE CONCAT(@Prefix, '%')
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
                phieu.MaPhieuXuatTra = $"{prefix}{nextNumber:D3}";
            }

            // Tính tổng
            phieu.TongSoLuong = chiTiets.Sum(c => c.SoLuong);
            phieu.TongTienTra = chiTiets.Sum(c => c.ThanhTien);
            var now = phieu.ThoiGianXuatTra != default ? phieu.ThoiGianXuatTra : DateTime.Now;
            var userId = Session.CurrentUser?.Id;
            var userName = Session.CurrentUser?.HoTen ?? "Quản trị viên";
            var branchId = phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. Thêm phiếu xuất trả
            const string insertPhieuSql = """
                INSERT INTO phieu_xuat_tra_ncc
                (
                    ma_phieu_xuat_tra, chi_nhanh_id, nha_cung_cap_id, ten_nha_cung_cap,
                    ma_nha_cung_cap, so_dien_thoai_ncc, phieu_nhap_goc_id, ma_phieu_nhap_goc,
                    thoi_gian_xuat_tra, kho_xuat, tong_so_luong, tong_tien_tra, ghi_chu,
                    trang_thai, nguoi_tao_id, ten_nguoi_tao, ngay_tao
                )
                VALUES
                (
                    @MaPhieuXuatTra, @ChiNhanhId, @NhaCungCapId, @TenNhaCungCap,
                    @MaNhaCungCap, @SoDienThoaiNcc, @PhieuNhapGocId, @MaPhieuNhapGoc,
                    @ThoiGianXuatTra, @KhoXuat, @TongSoLuong, @TongTienTra, @GhiChu,
                    'HOAN_THANH', @NguoiTaoId, @TenNguoiTao, @NgayTao
                );
                SELECT LAST_INSERT_ID();
            """;

            var phieuXuatTraId = await connection.ExecuteScalarAsync<uint>(insertPhieuSql, new
            {
                phieu.MaPhieuXuatTra,
                ChiNhanhId = branchId,
                phieu.NhaCungCapId,
                phieu.TenNhaCungCap,
                phieu.MaNhaCungCap,
                phieu.SoDienThoaiNcc,
                phieu.PhieuNhapGocId,
                phieu.MaPhieuNhapGoc,
                ThoiGianXuatTra = now,
                phieu.KhoXuat,
                phieu.TongSoLuong,
                phieu.TongTienTra,
                phieu.GhiChu,
                NguoiTaoId = userId,
                TenNguoiTao = userName,
                NgayTao = now
            }, transaction);

            // 3. Thêm chi tiết & Giảm tồn kho
            const string insertChiTietSql = """
                INSERT INTO chi_tiet_phieu_xuat_tra_ncc
                (phieu_xuat_tra_id, san_pham_id, so_luong, don_gia, thanh_tien, ly_do_xuat_tra)
                VALUES
                (@PhieuXuatTraId, @SanPhamId, @SoLuong, @DonGia, @ThanhTien, @LyDoXuatTra);
            """;

            const string updateTonKhoSql = """
                UPDATE san_pham 
                SET ton_kho_excel = GREATEST(0, COALESCE(ton_kho_excel, 0) - @SoLuong),
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
            """;

            foreach (var item in chiTiets)
            {
                await connection.ExecuteAsync(insertChiTietSql, new
                {
                    PhieuXuatTraId = phieuXuatTraId,
                    item.SanPhamId,
                    item.SoLuong,
                    item.DonGia,
                    item.ThanhTien,
                    item.LyDoXuatTra
                }, transaction);

                // Giảm tồn kho sản phẩm
                await connection.ExecuteAsync(updateTonKhoSql, new
                {
                    item.SoLuong,
                    item.SanPhamId
                }, transaction);
            }

            // 4. Xử lý công nợ hoặc hoàn tiền
            if (phuongThucHoanTien == "Trừ công nợ" && phieu.NhaCungCapId.HasValue && phieu.NhaCungCapId.Value > 0)
            {
                // Ghi có sổ cái công nợ NCC -> giảm nợ phải trả
                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    phieu.NhaCungCapId.Value,
                    branchId,
                    "XUAT_TRA_NCC",
                    phieu.MaPhieuXuatTra,
                    0,
                    phieu.TongTienTra,
                    now,
                    userId,
                    $"Giảm trừ công nợ do trả hàng NCC theo phiếu {phieu.MaPhieuXuatTra}");
            }
            else
            {
                // Hoàn tiền mặt hoặc chuyển khoản -> Tạo phiếu thu trong sổ quỹ nếu bảng phieu_thu tồn tại
                try
                {
                    var hasPhieuThu = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_thu';",
                        transaction: transaction) > 0;

                    if (hasPhieuThu)
                    {
                        var prefixThu = $"PT{DateTime.Now:yyMMdd}-";
                        const string lastThuSql = """
                            SELECT ma_phieu_thu 
                            FROM phieu_thu 
                            WHERE ma_phieu_thu LIKE CONCAT(@Prefix, '%')
                            ORDER BY id DESC 
                            LIMIT 1 
                            FOR UPDATE;
                        """;
                        var lastThuCode = await connection.QueryFirstOrDefaultAsync<string>(lastThuSql, new { Prefix = prefixThu }, transaction);
                        var nextThuNum = 1;
                        if (!string.IsNullOrWhiteSpace(lastThuCode) && lastThuCode.Length >= prefixThu.Length)
                        {
                            if (int.TryParse(lastThuCode.Substring(prefixThu.Length), out var cn))
                                nextThuNum = cn + 1;
                        }
                        var maPhieuThu = $"{prefixThu}{nextThuNum:D3}";

                        var phuongThucThu = phuongThucHoanTien == "Chuyển khoản" ? "Chuyển khoản" : "Tiền mặt";
                        var tenNcc = !string.IsNullOrWhiteSpace(phieu.TenNhaCungCap) ? phieu.TenNhaCungCap : "Nhà cung cấp";

                        const string insertThuSql = """
                            INSERT INTO phieu_thu
                            (
                                ma_phieu_thu, chi_nhanh_id, so_tien, phuong_thuc,
                                noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_thu,
                                nguoi_thuc_hien_id, trang_thai
                            )
                            VALUES
                            (
                                @MaPhieuThu, @ChiNhanhId, @SoTien, @PhuongThuc,
                                @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGianThu,
                                @NguoiThucHienId, 'HOAN_THANH'
                            );
                        """;

                        await connection.ExecuteAsync(insertThuSql, new
                        {
                            MaPhieuThu = maPhieuThu,
                            ChiNhanhId = branchId,
                            SoTien = phieu.TongTienTra,
                            PhuongThuc = phuongThucThu,
                            NoiDung = $"Thu tiền hoàn trả từ NCC {tenNcc} (Phiếu trả: {phieu.MaPhieuXuatTra})",
                            NguoiNopNhan = tenNcc,
                            GhiChu = phieu.GhiChu ?? "",
                            ThoiGianThu = now,
                            NguoiThucHienId = userId
                        }, transaction);
                    }
                }
                catch (Exception exThu)
                {
                    SafeLog.WriteError("Không tạo được phiếu thu tự động cho phiếu xuất trả NCC", exThu);
                }
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Xuất trả NCC",
                "PHIEU_XUAT_TRA",
                phieuXuatTraId,
                phieu.MaPhieuXuatTra,
                $"Xuất hàng trả lại NCC {phieu.TenNhaCungCap ?? "NCC"} - Phiếu {phieu.MaPhieuXuatTra} (Tổng tiền: {phieu.TongTienTra:N0} đ)",
                phieu.TongTienTra);

            return phieu.MaPhieuXuatTra;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi lưu phiếu xuất trả NCC", ex);
            throw;
        }
    }
}
