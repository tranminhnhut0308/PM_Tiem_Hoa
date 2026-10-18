using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
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
            // Seed dữ liệu mẫu ban đầu
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

        // Gán số thứ tự hiển thị
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
                sp.don_vi_tinh AS DonViTinh
            FROM chi_tiet_phieu_tra_hang ct
            JOIN san_pham sp ON ct.san_pham_id = sp.id
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
}
