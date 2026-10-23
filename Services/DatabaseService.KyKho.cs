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
    private static bool _daKiemTraBangKyKho;

    /// <summary>
    /// Đảm bảo bảng ky_ke_toan và chi_tiet_hang_hoa_dau_ky có đầy đủ cấu trúc và các cột
    /// phục vụ quản lý tồn kho theo kỳ, điều chỉnh kiểm kê, giá vốn, giá bán và mở bán.
    /// </summary>
    public async Task EnsureBangKyKhoAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        if (_daKiemTraBangKyKho)
            return;

        try
        {
            // 1. Tạo bảng ky_ke_toan nếu chưa có
            const string createKyTableSql = """
            CREATE TABLE IF NOT EXISTS ky_ke_toan (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_ky VARCHAR(50) NOT NULL UNIQUE,
                ten_ky VARCHAR(150) NOT NULL,
                ngay_bat_dau_ky DATE NOT NULL,
                ngay_ket_thuc_ky DATE NOT NULL,
                trang_thai_khoa_so TINYINT(1) NOT NULL DEFAULT 0,
                ngay_ket_ky DATETIME NULL,
                nguoi_ket_ky_id INT UNSIGNED NULL,
                ghi_chu TEXT NULL,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_ky_ngay (ngay_bat_dau_ky, ngay_ket_thuc_ky),
                INDEX idx_ky_trangthai (trang_thai_khoa_so)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createKyTableSql, transaction: transaction);

            // Bổ sung các cột mở rộng cho ky_ke_toan nếu thiếu
            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan", "ngay_ket_ky", "DATETIME NULL", transaction);
            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan", "nguoi_ket_ky_id", "INT UNSIGNED NULL", transaction);
            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan", "ghi_chu", "TEXT NULL", transaction);
            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan", "chi_nhanh_id", "INT UNSIGNED NOT NULL DEFAULT 1", transaction);

            // 2. Tạo bảng chi_tiet_hang_hoa_dau_ky nếu chưa có
            const string createChiTietTableSql = """
            CREATE TABLE IF NOT EXISTS chi_tiet_hang_hoa_dau_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ky_ke_toan_id INT UNSIGNED NOT NULL,
                kho_hang_id INT UNSIGNED NOT NULL DEFAULT 1,
                hang_hoa_id INT UNSIGNED NOT NULL,
                so_luong DECIMAL(15,3) NOT NULL DEFAULT 0,
                so_luong_dieu_chinh DECIMAL(15,3) NOT NULL DEFAULT 0,
                so_luong_thuc_te DECIMAL(15,3) NOT NULL DEFAULT 0,
                gia_von DECIMAL(15,2) NOT NULL DEFAULT 0,
                gia_ban DECIMAL(15,2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                so_luong_nhap DECIMAL(15,3) NOT NULL DEFAULT 0,
                so_luong_xuat DECIMAL(15,3) NOT NULL DEFAULT 0,
                so_luong_cuoi_ky DECIMAL(15,3) NOT NULL DEFAULT 0,
                dang_mo_ban TINYINT(1) NOT NULL DEFAULT 1,
                noi_dung TEXT NULL,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_cthh_ky (ky_ke_toan_id),
                INDEX idx_cthh_hh (hang_hoa_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createChiTietTableSql, transaction: transaction);

            // Bổ sung các cột mở rộng cho chi_tiet_hang_hoa_dau_ky nếu thiếu
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "so_luong_dieu_chinh", "DECIMAL(15,3) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "so_luong_thuc_te", "DECIMAL(15,3) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "gia_ban", "DECIMAL(15,2) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "so_luong_nhap", "DECIMAL(15,3) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "so_luong_xuat", "DECIMAL(15,3) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "so_luong_cuoi_ky", "DECIMAL(15,3) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_hang_hoa_dau_ky", "dang_mo_ban", "TINYINT(1) NOT NULL DEFAULT 1", transaction);

            _daKiemTraBangKyKho = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khởi tạo cấu trúc bảng quản lý kỳ kho", ex);
        }
    }

    private static async Task AddColumnIfNotExistsAsync(
        MySqlConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        MySqlTransaction? transaction = null)
    {
        try
        {
            const string checkColumnSql = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @TableName
              AND COLUMN_NAME = @ColumnName;
            """;

            var count = await connection.ExecuteScalarAsync<int>(
                checkColumnSql,
                new { TableName = tableName, ColumnName = columnName },
                transaction: transaction);

            if (count == 0)
            {
                var alterSql = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition};";
                await connection.ExecuteAsync(alterSql, transaction: transaction);
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError($"Lỗi kiểm tra/thêm cột {columnName} vào bảng {tableName}", ex);
        }
    }

    /// <summary>
    /// Lấy toàn bộ danh sách các kỳ kế toán / kỳ kinh doanh trong hệ thống.
    /// Sắp xếp kỳ mới nhất lên đầu.
    /// </summary>
    public async Task<IReadOnlyList<KyKeToan>> GetDanhSachKyKeToanAsync()
    {
        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        const string sql = """
        SELECT
            id AS Id,
            ma_ky AS MaKy,
            ten_ky AS TenKy,
            ngay_bat_dau_ky AS NgayBatDauKy,
            ngay_ket_thuc_ky AS NgayKetThucKy,
            trang_thai_khoa_so AS TrangThaiKhoaSo,
            ngay_ket_ky AS NgayKetKy,
            nguoi_ket_ky_id AS NguoiKetKyId,
            ghi_chu AS GhiChu,
            chi_nhanh_id AS ChiNhanhId,
            ngay_tao AS NgayTao
        FROM ky_ke_toan
        ORDER BY ngay_bat_dau_ky DESC, id DESC;
        """;

        return (await connection.QueryAsync<KyKeToan>(sql)).AsList();
    }

    /// <summary>
    /// Lấy kỳ đang mở (active) hiện tại. Nếu chưa có kỳ nào mở hoặc database trống,
    /// tự động khởi tạo kỳ cho tháng hiện tại để hệ thống luôn có kỳ làm việc.
    /// </summary>
    public async Task<KyKeToan> GetKyHienTaiHoacTaoMoiAsync()
    {
        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        // 1. Thử lấy kỳ đang mở gần nhất
        const string sqlActive = """
        SELECT
            id AS Id,
            ma_ky AS MaKy,
            ten_ky AS TenKy,
            ngay_bat_dau_ky AS NgayBatDauKy,
            ngay_ket_thuc_ky AS NgayKetThucKy,
            trang_thai_khoa_so AS TrangThaiKhoaSo,
            ngay_ket_ky AS NgayKetKy,
            nguoi_ket_ky_id AS NguoiKetKyId,
            ghi_chu AS GhiChu,
            chi_nhanh_id AS ChiNhanhId,
            ngay_tao AS NgayTao
        FROM ky_ke_toan
        WHERE trang_thai_khoa_so = 0
        ORDER BY ngay_bat_dau_ky DESC, id DESC
        LIMIT 1;
        """;

        var activeKy = await connection.QueryFirstOrDefaultAsync<KyKeToan>(sqlActive);
        if (activeKy != null)
            return activeKy;

        // 2. Nếu không có kỳ nào đang mở, tự động tạo kỳ cho tháng hiện tại
        var today = DateTime.Today;
        var dauThang = new DateTime(today.Year, today.Month, 1);
        var cuoiThang = dauThang.AddMonths(1).AddDays(-1);
        var maKy = $"KY_{today:MM_yyyy}";
        var tenKy = $"Tháng {today.Month} Năm {today.Year}";

        // Kiểm tra xem mã kỳ này đã có chưa
        var existing = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, trang_thai_khoa_so AS TrangThaiKhoaSo FROM ky_ke_toan WHERE ma_ky = @MaKy LIMIT 1;",
            new { MaKy = maKy });

        if (existing != null)
            return existing;

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string insertSql = """
            INSERT INTO ky_ke_toan
            (ma_ky, ten_ky, ngay_bat_dau_ky, ngay_ket_thuc_ky, trang_thai_khoa_so, chi_nhanh_id)
            VALUES
            (@MaKy, @TenKy, @NgayBatDau, @NgayKetThuc, 0, 1);
            SELECT LAST_INSERT_ID();
            """;

            var newId = await connection.ExecuteScalarAsync<uint>(insertSql, new
            {
                MaKy = maKy,
                TenKy = tenKy,
                NgayBatDau = dauThang,
                NgayKetThuc = cuoiThang
            }, transaction);

            // Khởi tạo sẵn danh sách hàng hóa ban đầu vào kỳ mới này
            await KhoiTaoHangHoaChoKyMoiAsync(connection, newId, transaction);

            // Kỳ tự động cũng phải kế thừa công nợ NCC còn lại từ kỳ trước.
            var kyNguon = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                """
                SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                       ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                       trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
                FROM ky_ke_toan
                WHERE id <> @KyId AND ngay_bat_dau_ky < @NgayBatDau
                ORDER BY ngay_bat_dau_ky DESC
                LIMIT 1
                FOR UPDATE;
                """,
                new { KyId = newId, NgayBatDau = dauThang },
                transaction);

            if (kyNguon is not null)
            {
                await EnsureBangCongNoNccKyAsync(connection, transaction);
                var congNoNguon = await LayNguonChuyenKyAsync(connection, transaction, kyNguon);
                await ChuyenCongNoNccSangKyMoiAsync(
                    connection,
                    transaction,
                    kyNguon,
                    newId,
                    congNoNguon);
            }

            await transaction.CommitAsync();

            return new KyKeToan
            {
                Id = newId,
                MaKy = maKy,
                TenKy = tenKy,
                NgayBatDauKy = dauThang,
                NgayKetThucKy = cuoiThang,
                TrangThaiKhoaSo = false
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Tạo một kỳ kinh doanh mới. Hỗ trợ tự động kế thừa toàn bộ tồn cuối kỳ trước làm tồn đầu kỳ mới.
    /// </summary>
    public async Task<KyKeToan> TaoKyMoiAsync(
        string maKy,
        string tenKy,
        DateTime ngayBatDau,
        DateTime ngayKetThuc,
        bool keThuaTonKyTruoc,
        uint? kyTruocId,
        string? ghiChu = null)
    {
        if (string.IsNullOrWhiteSpace(maKy))
            throw new ArgumentException("Mã kỳ không được để trống.", nameof(maKy));

        if (string.IsNullOrWhiteSpace(tenKy))
            throw new ArgumentException("Tên kỳ không được để trống.", nameof(tenKy));

        if (ngayKetThuc < ngayBatDau)
            throw new ArgumentException("Ngày kết thúc không được trước ngày bắt đầu.", nameof(ngayKetThuc));

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Kiểm tra trùng mã kỳ
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ky_ke_toan WHERE ma_ky = @MaKy;",
                new { MaKy = maKy.Trim() },
                transaction: transaction) > 0;

            if (exists)
            {
                maKy = $"{maKy.Trim()}_{DateTime.Now:HHmmss}";
            }

            const string insertSql = """
            INSERT INTO ky_ke_toan
            (ma_ky, ten_ky, ngay_bat_dau_ky, ngay_ket_thuc_ky, trang_thai_khoa_so, ghi_chu, chi_nhanh_id)
            VALUES
            (@MaKy, @TenKy, @NgayBatDau, @NgayKetThuc, 0, @GhiChu, 1);
            SELECT LAST_INSERT_ID();
            """;

            var newKyId = await connection.ExecuteScalarAsync<uint>(insertSql, new
            {
                MaKy = maKy.Trim(),
                TenKy = tenKy.Trim(),
                NgayBatDau = ngayBatDau.Date,
                NgayKetThuc = ngayKetThuc.Date,
                GhiChu = ghiChu?.Trim()
            }, transaction: transaction);

            // Nạp hàng hóa và tồn kho ban đầu cho kỳ mới
            if (keThuaTonKyTruoc && kyTruocId.HasValue && kyTruocId.Value > 0)
            {
                await KeThuaTonKhoTuKyTruocAsync(connection, newKyId, kyTruocId.Value, transaction);
            }
            else
            {
                await KhoiTaoHangHoaChoKyMoiAsync(connection, newKyId, transaction);
            }

            // Kế thừa công nợ nhà cung cấp từ kỳ trước (nếu có) để không mất số dư
            // khi người dùng tạo kỳ mới từ màn hình quản lý kỳ.
            KyKeToan? kyNguon = null;
            if (kyTruocId.HasValue && kyTruocId.Value > 0 && kyTruocId.Value != newKyId)
            {
                kyNguon = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                    """
                    SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                           ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                           trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
                    FROM ky_ke_toan
                    WHERE id = @Id
                    LIMIT 1
                    FOR UPDATE;
                    """,
                    new { Id = kyTruocId.Value },
                    transaction);
            }

            if (kyNguon is null)
            {
                kyNguon = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                    """
                    SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                           ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                           trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
                    FROM ky_ke_toan
                    WHERE id <> @KyId AND ngay_bat_dau_ky < @NgayBatDau
                    ORDER BY ngay_bat_dau_ky DESC
                    LIMIT 1
                    FOR UPDATE;
                    """,
                    new { KyId = newKyId, NgayBatDau = ngayBatDau.Date },
                    transaction);
            }

            if (kyNguon is not null && kyNguon.NgayBatDauKy < ngayBatDau.Date)
            {
                await EnsureBangCongNoNccKyAsync(connection, transaction);
                var congNoNguon = await LayNguonChuyenKyAsync(connection, transaction, kyNguon);
                await ChuyenCongNoNccSangKyMoiAsync(
                    connection,
                    transaction,
                    kyNguon,
                    newKyId,
                    congNoNguon);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Tạo kỳ kho mới",
                "KY_KE_TOAN",
                newKyId,
                maKy,
                $"Khởi tạo kỳ kho: {tenKy} ({ngayBatDau:dd/MM/yyyy} - {ngayKetThuc:dd/MM/yyyy}). Kế thừa tồn: {keThuaTonKyTruoc}",
                0);

            return new KyKeToan
            {
                Id = newKyId,
                MaKy = maKy,
                TenKy = tenKy,
                NgayBatDauKy = ngayBatDau.Date,
                NgayKetThucKy = ngayKetThuc.Date,
                TrangThaiKhoaSo = false,
                GhiChu = ghiChu
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi TaoKyMoiAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Kế thừa tồn kho từ kỳ trước sang kỳ mới:
    /// Tồn cuối kỳ trước $\rightarrow$ Tồn đầu kỳ mới.
    /// Giữ nguyên giá vốn, giá bán và trạng thái mở bán gần nhất.
    /// </summary>
    private async Task KeThuaTonKhoTuKyTruocAsync(
        MySqlConnection connection,
        uint kyMoiId,
        uint kyTruocId,
        MySqlTransaction transaction)
    {
        // Lấy danh sách hàng hóa và tồn cuối kỳ của kỳ trước
        const string selectOldSql = """
        SELECT
            hang_hoa_id,
            kho_hang_id,
            COALESCE(
                NULLIF(so_luong_cuoi_ky, 0),
                GREATEST(0, (so_luong + so_luong_dieu_chinh) + so_luong_nhap - so_luong_xuat)
            ) AS TonCuoi,
            gia_von,
            gia_ban,
            dang_mo_ban
        FROM chi_tiet_hang_hoa_dau_ky
        WHERE ky_ke_toan_id = @KyTruocId;
        """;

        var oldItems = (await connection.QueryAsync<dynamic>(
            selectOldSql,
            new { KyTruocId = kyTruocId },
            transaction: transaction)).AsList();

        if (oldItems.Count > 0)
        {
            const string insertNewSql = """
            INSERT INTO chi_tiet_hang_hoa_dau_ky
            (
                ky_ke_toan_id, kho_hang_id, hang_hoa_id,
                so_luong, so_luong_dieu_chinh, so_luong_thuc_te,
                gia_von, gia_ban, thanh_tien,
                so_luong_nhap, so_luong_xuat, so_luong_cuoi_ky,
                dang_mo_ban, noi_dung
            )
            VALUES
            (
                @KyMoiId, @KhoHangId, @HangHoaId,
                @SoLuongDau, 0, @SoLuongDau,
                @GiaVon, @GiaBan, (@SoLuongDau * @GiaVon),
                0, 0, @SoLuongDau,
                @DangMoBan, 'Kế thừa tồn cuối kỳ trước'
            );
            """;

            foreach (var item in oldItems)
            {
                decimal tonCuoi = ToDecimal(item.TonCuoi);
                decimal giaVon = ToDecimal(item.gia_von);
                decimal giaBan = ToDecimal(item.gia_ban);
                bool moBan = item.dang_mo_ban != null && Convert.ToBoolean(item.dang_mo_ban);

                await connection.ExecuteAsync(insertNewSql, new
                {
                    KyMoiId = kyMoiId,
                    KhoHangId = (uint)item.kho_hang_id,
                    HangHoaId = (uint)item.hang_hoa_id,
                    SoLuongDau = tonCuoi,
                    GiaVon = giaVon,
                    GiaBan = giaBan,
                    DangMoBan = moBan ? 1 : 0
                }, transaction: transaction);
            }
        }
        else
        {
            // Nếu kỳ trước chưa có dữ liệu thì nạp từ danh mục sản phẩm chung
            await KhoiTaoHangHoaChoKyMoiAsync(connection, kyMoiId, transaction);
        }
    }

    /// <summary>
    /// Khởi tạo dữ liệu hàng hóa ban đầu cho một kỳ mới từ bảng san_pham.
    /// </summary>
    private async Task KhoiTaoHangHoaChoKyMoiAsync(
        MySqlConnection connection,
        uint kyId,
        MySqlTransaction? transaction)
    {
        const string sql = """
        INSERT INTO chi_tiet_hang_hoa_dau_ky
        (
            ky_ke_toan_id, kho_hang_id, hang_hoa_id,
            so_luong, so_luong_dieu_chinh, so_luong_thuc_te,
            gia_von, gia_ban, thanh_tien,
            so_luong_nhap, so_luong_xuat, so_luong_cuoi_ky,
            dang_mo_ban, noi_dung
        )
        SELECT
            @KyId,
            1,
            sp.id,
            COALESCE(tk.so_luong_ton, COALESCE(sp.ton_kho_excel, 0)),
            0,
            COALESCE(tk.so_luong_ton, COALESCE(sp.ton_kho_excel, 0)),
            COALESCE(sp.gia_von, 0),
            COALESCE(sp.gia_ban, 0),
            (COALESCE(tk.so_luong_ton, COALESCE(sp.ton_kho_excel, 0)) * COALESCE(sp.gia_von, 0)),
            0,
            0,
            COALESCE(tk.so_luong_ton, COALESCE(sp.ton_kho_excel, 0)),
            CASE WHEN sp.trang_thai = 1 THEN 1 ELSE 0 END,
            'Khởi tạo đầu kỳ'
        FROM san_pham sp
        LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id AND tk.chi_nhanh_id = 1
        WHERE NOT EXISTS (
            SELECT 1 FROM chi_tiet_hang_hoa_dau_ky ct
            WHERE ct.ky_ke_toan_id = @KyId AND ct.hang_hoa_id = sp.id
        );
        """;

        await connection.ExecuteAsync(sql, new { KyId = kyId }, transaction: transaction);
    }

    /// <summary>
    /// Kết kỳ: Chốt số liệu tồn cuối kỳ, tính toán tồn thực tế và khóa sổ kỳ này.
    /// </summary>
    public async Task<bool> KetKyAsync(uint kyId)
    {
        if (kyId == 0)
            throw new ArgumentException("Mã kỳ không hợp lệ.", nameof(kyId));

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Chốt tồn cuối kỳ cho tất cả mặt hàng trong kỳ:
            // Tồn cuối = (Tồn đầu + Điều chỉnh) + Nhập - Xuất
            const string updateChiTietSql = """
            UPDATE chi_tiet_hang_hoa_dau_ky
            SET so_luong_thuc_te = GREATEST(0, so_luong + so_luong_dieu_chinh),
                so_luong_cuoi_ky = GREATEST(0, (so_luong + so_luong_dieu_chinh) + so_luong_nhap - so_luong_xuat),
                thanh_tien = GREATEST(0, so_luong + so_luong_dieu_chinh) * gia_von,
                ngay_cap_nhat = NOW()
            WHERE ky_ke_toan_id = @KyId;
            """;
            await connection.ExecuteAsync(updateChiTietSql, new { KyId = kyId }, transaction: transaction);

            // 2. Khóa sổ kỳ kế toán
            const string lockKySql = """
            UPDATE ky_ke_toan
            SET trang_thai_khoa_so = 1,
                ngay_ket_ky = NOW(),
                nguoi_ket_ky_id = @UserId
            WHERE id = @KyId;
            """;
            await connection.ExecuteAsync(lockKySql, new
            {
                KyId = kyId,
                UserId = Session.CurrentUser?.Id
            }, transaction: transaction);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Kết kỳ kho",
                "KY_KE_TOAN",
                kyId,
                $"KY{kyId}",
                $"Chốt sổ kết kỳ thành công cho kỳ #{kyId}.",
                0);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi KetKyAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Mở lại kỳ đã kết sổ (cho phép chỉnh sửa khi cần thiết và có quyền quản trị).
    /// </summary>
    public async Task<bool> MoLaiKyAsync(uint kyId)
    {
        if (kyId == 0)
            throw new ArgumentException("Mã kỳ không hợp lệ.", nameof(kyId));

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        const string sql = """
        UPDATE ky_ke_toan
        SET trang_thai_khoa_so = 0,
            ngay_ket_ky = NULL
        WHERE id = @KyId;
        """;

        var rows = await connection.ExecuteAsync(sql, new { KyId = kyId });
        return rows > 0;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ hàng hóa trong một kỳ kinh doanh kèm số lượng tồn đầu,
    /// điều chỉnh hao hụt, nhập trong kỳ, xuất bán trong kỳ, tồn hiện tại và giá.
    /// </summary>
    public async Task<IReadOnlyList<HangHoaKyKhoItem>> GetHangHoaTheoKyAsync(
        uint kyId,
        string? search = null,
        uint? nhomId = null)
    {
        if (kyId == 0)
            return new List<HangHoaKyKhoItem>();

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        // 1. Kiểm tra kỳ có hàng hóa chưa. Nếu kỳ hoàn toàn trống, tự nạp từ danh mục sản phẩm
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM chi_tiet_hang_hoa_dau_ky WHERE ky_ke_toan_id = @KyId;",
            new { KyId = kyId });

        if (count == 0)
        {
            await KhoiTaoHangHoaChoKyMoiAsync(connection, kyId, null);
        }

        // 2. Lấy thông tin ngày bắt đầu và kết thúc của kỳ để tính nhập/xuất tự động
        var kyInfo = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
            "SELECT id AS Id, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyId });

        var tuNgay = kyInfo?.NgayBatDauKy ?? DateTime.Today.AddMonths(-1);
        var denNgay = (kyInfo?.NgayKetThucKy ?? DateTime.Today).AddDays(1).AddTicks(-1);

        // 3. Truy vấn danh sách hàng hóa trong kỳ
        var sql = """
        SELECT
            ct.id AS Id,
            ct.ky_ke_toan_id AS KyKeToanId,
            ct.kho_hang_id AS KhoHangId,
            ct.hang_hoa_id AS HangHoaId,
            sp.ma_san_pham AS MaHangHoa,
            COALESCE(NULLIF(TRIM(sp.ten_san_pham), ''), sp.ma_san_pham) AS TenHangHoa,
            COALESCE(dvt.ten_don_vi, 'Cành') AS DonViTinh,
            COALESCE(nsp.ten_nhom, sp.nhom_hang_3_cap_excel, 'Chưa phân nhóm') AS NhomHangHoa,
            sp.hinh_anh AS HinhAnh,
            ct.so_luong AS SoLuongDauKy,
            ct.so_luong_dieu_chinh AS SoLuongDieuChinh,
            COALESCE(NULLIF(ct.gia_von, 0), sp.gia_von, 0) AS GiaVonDauKy,
            COALESCE(NULLIF(ct.gia_ban, 0), sp.gia_ban, 0) AS GiaBanDauKy,
            ct.so_luong_nhap AS SoLuongNhap,
            ct.so_luong_xuat AS SoLuongXuat,
            ct.so_luong_cuoi_ky AS SoLuongCuoiKy,
            ct.dang_mo_ban AS DangMoBan,
            ct.noi_dung AS GhiChu
        FROM chi_tiet_hang_hoa_dau_ky ct
        INNER JOIN san_pham sp ON sp.id = ct.hang_hoa_id
        LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
        LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
        WHERE ct.ky_ke_toan_id = @KyId
        """;

        var param = new DynamicParameters();
        param.Add("KyId", kyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (sp.ma_san_pham LIKE @Search OR sp.ten_san_pham LIKE @Search)";
            param.Add("Search", $"%{search.Trim()}%");
        }

        if (nhomId.HasValue && nhomId.Value > 0)
        {
            if (nhomId.Value == 999999)
            {
                sql += " AND (sp.nhom_san_pham_id IS NULL OR sp.nhom_san_pham_id = 0)";
            }
            else
            {
                sql += " AND sp.nhom_san_pham_id = @NhomId";
                param.Add("NhomId", nhomId.Value);
            }
        }

        sql += " ORDER BY sp.ten_san_pham, sp.id DESC;";

        var list = (await connection.QueryAsync<HangHoaKyKhoItem>(sql, param)).AsList();

        // 4. Tính toán số lượng nhập và xuất phát sinh thực tế trong kỳ (nếu có các bảng liên quan)
        try
        {
            // Kiểm tra bảng chi_tiet_phieu_nhap_hang
            bool hasNhap = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_phieu_nhap_hang';") > 0;

            if (hasNhap)
            {
                const string nhapSql = """
                SELECT ctp.san_pham_id AS SanPhamId, COALESCE(SUM(ctp.so_luong), 0) AS TongNhap
                FROM chi_tiet_phieu_nhap_hang ctp
                INNER JOIN phieu_nhap_hang pnh ON pnh.id = ctp.phieu_nhap_id
                WHERE pnh.thoi_gian_nhap >= @TuNgay AND pnh.thoi_gian_nhap <= @DenNgay
                GROUP BY ctp.san_pham_id;
                """;

                var nhapDict = (await connection.QueryAsync<(uint SanPhamId, decimal TongNhap)>(
                    nhapSql, new { TuNgay = tuNgay, DenNgay = denNgay })).ToDictionary(x => x.SanPhamId, x => x.TongNhap);

                foreach (var item in list)
                {
                    if (nhapDict.TryGetValue(item.HangHoaId, out var slNhap))
                    {
                        item.SoLuongNhap = slNhap;
                    }
                }
            }

            // Kiểm tra bảng chi_tiet_hoa_don
            bool hasBan = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don';") > 0;

            if (hasBan)
            {
                const string xuatSql = """
                SELECT cthd.san_pham_id AS SanPhamId, COALESCE(SUM(cthd.so_luong), 0) AS TongXuat
                FROM chi_tiet_hoa_don cthd
                INNER JOIN hoa_don hd ON hd.id = cthd.hoa_don_id
                WHERE hd.ngay_tao >= @TuNgay AND hd.ngay_tao <= @DenNgay
                  AND (hd.trang_thai IS NULL OR hd.trang_thai != 'DA_HUY')
                GROUP BY cthd.san_pham_id;
                """;

                var xuatDict = (await connection.QueryAsync<(uint SanPhamId, decimal TongXuat)>(
                    xuatSql, new { TuNgay = tuNgay, DenNgay = denNgay })).ToDictionary(x => x.SanPhamId, x => x.TongXuat);

                foreach (var item in list)
                {
                    if (xuatDict.TryGetValue(item.HangHoaId, out var slXuat))
                    {
                        item.SoLuongXuat = slXuat;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tổng hợp số lượng nhập xuất trong kỳ", ex);
        }

        for (int i = 0; i < list.Count; i++)
        {
            list[i].Stt = i + 1;
        }

        return list;
    }

    /// <summary>
    /// Lưu toàn bộ điều chỉnh kiểm kê, giá vốn, giá bán và trạng thái mở bán của danh sách hàng hóa trong kỳ.
    /// Nếu kỳ đang mở: Tự động đồng bộ số tồn thực tế và giá vốn/bán sang bảng ton_kho và san_pham.
    /// </summary>
    public async Task<bool> LuuDieuChinhHangHoaKyAsync(uint kyId, IEnumerable<HangHoaKyKhoItem> items)
    {
        if (kyId == 0 || items is null)
            return false;

        var itemList = items.ToList();
        if (itemList.Count == 0)
            return true;

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Kiểm tra trạng thái kỳ
            var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                "SELECT id AS Id, trang_thai_khoa_so AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
                new { Id = kyId },
                transaction: transaction);

            bool isDangMo = ky != null && !ky.TrangThaiKhoaSo;

            const string updateCtSql = """
            UPDATE chi_tiet_hang_hoa_dau_ky
            SET so_luong = @SoLuongDauKy,
                so_luong_dieu_chinh = @SoLuongDieuChinh,
                so_luong_thuc_te = @SoLuongThucTe,
                gia_von = @GiaVon,
                gia_ban = @GiaBan,
                thanh_tien = @ThanhTien,
                dang_mo_ban = @DangMoBan,
                noi_dung = @GhiChu,
                ngay_cap_nhat = NOW()
            WHERE id = @Id;
            """;

            foreach (var item in itemList)
            {
                var slThucTe = item.SoLuongThucTeDauKy;
                var thanhTien = item.ThanhTienDauKy;

                await connection.ExecuteAsync(updateCtSql, new
                {
                    item.Id,
                    item.SoLuongDauKy,
                    item.SoLuongDieuChinh,
                    SoLuongThucTe = slThucTe,
                    GiaVon = item.GiaVonDauKy,
                    GiaBan = item.GiaBanDauKy,
                    ThanhTien = thanhTien,
                    DangMoBan = item.DangMoBan ? 1 : 0,
                    item.GhiChu
                }, transaction: transaction);

                // Nếu kỳ này đang mở, đồng bộ tồn kho hiện tại và giá bán/vốn vào ton_kho và san_pham
                if (isDangMo)
                {
                    var tonHienTai = item.TonHienTai;

                    // Đồng bộ bảng ton_kho
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO ton_kho (chi_nhanh_id, san_pham_id, so_luong_ton, gia_von_binh_quan, ngay_cap_nhat)
                        VALUES (1, @SanPhamId, @SoLuongTon, @GiaVon, NOW())
                        ON DUPLICATE KEY UPDATE
                            so_luong_ton = @SoLuongTon,
                            gia_von_binh_quan = @GiaVon,
                            ngay_cap_nhat = NOW();
                        """,
                        new
                        {
                            SanPhamId = item.HangHoaId,
                            SoLuongTon = tonHienTai,
                            GiaVon = item.GiaVonDauKy
                        },
                        transaction: transaction);

                    // Đồng bộ bảng san_pham
                    await connection.ExecuteAsync(
                        """
                        UPDATE san_pham
                        SET ton_kho_excel = @SoLuongTon,
                            gia_von = @GiaVon,
                            gia_ban = @GiaBan,
                            trang_thai = @TrangThai,
                            ngay_cap_nhat = NOW()
                        WHERE id = @SanPhamId;
                        """,
                        new
                        {
                            SanPhamId = item.HangHoaId,
                            SoLuongTon = tonHienTai,
                            GiaVon = item.GiaVonDauKy,
                            GiaBan = item.GiaBanDauKy,
                            TrangThai = item.DangMoBan ? 1 : 0
                        },
                        transaction: transaction);
                }
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Cập nhật kho theo kỳ",
                "CHI_TIET_HANG_HOA_DAU_KY",
                kyId,
                $"KY{kyId}",
                $"Cập nhật kiểm kê & định giá cho {itemList.Count} mặt hàng trong kỳ #{kyId}.",
                0);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi LuuDieuChinhHangHoaKyAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Thêm một mặt hàng từ Danh mục hàng hóa vào kỳ kinh doanh hiện tại.
    /// </summary>
    public async Task<bool> ThemHangVaoKyAsync(
        uint kyId,
        uint sanPhamId,
        decimal soLuongDauKy = 0,
        decimal giaVon = 0,
        decimal giaBan = 0,
        bool moBan = true)
    {
        if (kyId == 0 || sanPhamId == 0)
            return false;

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        // Lấy thông tin giá mặc định từ sản phẩm nếu truyền vào 0
        if (giaVon == 0 || giaBan == 0)
        {
            var sp = await connection.QueryFirstOrDefaultAsync<SanPham>(
                "SELECT gia_von AS GiaVon, gia_ban AS GiaBan FROM san_pham WHERE id = @Id;",
                new { Id = sanPhamId });
            if (sp != null)
            {
                if (giaVon == 0) giaVon = sp.GiaVon;
                if (giaBan == 0) giaBan = sp.GiaBan;
            }
        }

        const string sql = """
        INSERT INTO chi_tiet_hang_hoa_dau_ky
        (
            ky_ke_toan_id, kho_hang_id, hang_hoa_id,
            so_luong, so_luong_dieu_chinh, so_luong_thuc_te,
            gia_von, gia_ban, thanh_tien,
            so_luong_nhap, so_luong_xuat, so_luong_cuoi_ky,
            dang_mo_ban, noi_dung
        )
        VALUES
        (
            @KyId, 1, @SanPhamId,
            @SoLuong, 0, @SoLuong,
            @GiaVon, @GiaBan, (@SoLuong * @GiaVon),
            0, 0, @SoLuong,
            @DangMoBan, 'Thêm vào kỳ'
        )
        ON DUPLICATE KEY UPDATE
            dang_mo_ban = @DangMoBan,
            gia_von = @GiaVon,
            gia_ban = @GiaBan,
            ngay_cap_nhat = NOW();
        """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            KyId = kyId,
            SanPhamId = sanPhamId,
            SoLuong = soLuongDauKy,
            GiaVon = giaVon,
            GiaBan = giaBan,
            DangMoBan = moBan ? 1 : 0
        });

        return affected > 0;
    }

    /// <summary>
    /// Thêm danh sách nhiều sản phẩm từ Danh mục hàng hóa vào kỳ.
    /// </summary>
    public async Task<int> ThemNhieuHangVaoKyAsync(uint kyId, IEnumerable<uint> sanPhamIds)
    {
        var ids = sanPhamIds.Where(x => x > 0).Distinct().ToList();
        if (kyId == 0 || ids.Count == 0)
            return 0;

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        const string insertSql = """
        INSERT INTO chi_tiet_hang_hoa_dau_ky
        (
            ky_ke_toan_id, kho_hang_id, hang_hoa_id,
            so_luong, so_luong_dieu_chinh, so_luong_thuc_te,
            gia_von, gia_ban, thanh_tien,
            so_luong_nhap, so_luong_xuat, so_luong_cuoi_ky,
            dang_mo_ban, noi_dung
        )
        SELECT
            @KyId, 1, sp.id,
            0, 0, 0,
            COALESCE(sp.gia_von, 0), COALESCE(sp.gia_ban, 0), 0,
            0, 0, 0,
            1, 'Bổ sung vào kỳ'
        FROM san_pham sp
        WHERE sp.id = @SanPhamId
          AND NOT EXISTS (
              SELECT 1 FROM chi_tiet_hang_hoa_dau_ky ct
              WHERE ct.ky_ke_toan_id = @KyId AND ct.hang_hoa_id = sp.id
          );
        """;

        int totalAdded = 0;
        foreach (var id in ids)
        {
            var added = await connection.ExecuteAsync(insertSql, new { KyId = kyId, SanPhamId = id });
            totalAdded += added;
        }

        return totalAdded;
    }

    /// <summary>
    /// Xóa một mặt hàng khỏi kỳ kinh doanh nếu chưa phát sinh nhập/xuất trong kỳ.
    /// </summary>
    public async Task<bool> XoaHangKhoiKyAsync(uint chiTietId)
    {
        if (chiTietId == 0)
            return false;

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);

        var rows = await connection.ExecuteAsync(
            "DELETE FROM chi_tiet_hang_hoa_dau_ky WHERE id = @Id;",
            new { Id = chiTietId });

        return rows > 0;
    }

    /// <summary>
    /// Nạp lại tồn cuối kỳ trước vào kỳ hiện tại (chức năng đồng bộ lại khi kỳ trước có thay đổi).
    /// </summary>
    public async Task<int> LoadLaiTonKyTruocAsync(uint kyHienTaiId, uint kyTruocId)
    {
        if (kyHienTaiId == 0 || kyTruocId == 0)
            return 0;

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
            UPDATE chi_tiet_hang_hoa_dau_ky cur
            INNER JOIN (
                SELECT
                    hang_hoa_id,
                    COALESCE(
                        NULLIF(so_luong_cuoi_ky, 0),
                        GREATEST(0, (so_luong + so_luong_dieu_chinh) + so_luong_nhap - so_luong_xuat)
                    ) AS TonCuoiTruoc,
                    gia_von AS GiaVonTruoc,
                    gia_ban AS GiaBanTruoc
                FROM chi_tiet_hang_hoa_dau_ky
                WHERE ky_ke_toan_id = @KyTruocId
            ) prev ON prev.hang_hoa_id = cur.hang_hoa_id
            SET cur.so_luong = prev.TonCuoiTruoc,
                cur.so_luong_thuc_te = GREATEST(0, prev.TonCuoiTruoc + cur.so_luong_dieu_chinh),
                cur.thanh_tien = GREATEST(0, prev.TonCuoiTruoc + cur.so_luong_dieu_chinh) * cur.gia_von,
                cur.ngay_cap_nhat = NOW()
            WHERE cur.ky_ke_toan_id = @KyHienTaiId;
            """;

            var rows = await connection.ExecuteAsync(
                sql,
                new { KyHienTaiId = kyHienTaiId, KyTruocId = kyTruocId },
                transaction: transaction);

            await transaction.CommitAsync();
            return rows;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi LoadLaiTonKyTruocAsync", ex);
            throw;
        }
    }
}
