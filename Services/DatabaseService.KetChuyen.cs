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
    #region Quản lý kỳ kế toán & Bảng số liệu

    /// <summary>
    /// Đảm bảo bảng ky_ke_toan và các bảng số liệu đầu kỳ tồn tại trong cơ sở dữ liệu.
    /// </summary>
    public async Task EnsureBangKyKeToanVaKetChuyenAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        try
        {
            const string createKySql = """
            CREATE TABLE IF NOT EXISTS ky_ke_toan (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_ky VARCHAR(50) NOT NULL UNIQUE,
                ten_ky VARCHAR(100) NOT NULL,
                ngay_bat_dau_ky DATE NOT NULL,
                ngay_ket_thuc_ky DATE NOT NULL,
                trang_thai_khoa_so TINYINT(1) NOT NULL DEFAULT 0,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_ky_ngay (ngay_bat_dau_ky, ngay_ket_thuc_ky)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createKySql, transaction: transaction);

            const string createChiTietHangSql = """
            CREATE TABLE IF NOT EXISTS chi_tiet_hang_hoa_dau_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                kho_hang_id INT UNSIGNED NOT NULL DEFAULT 1,
                hang_hoa_id INT UNSIGNED NOT NULL,
                ky_ke_toan_id INT UNSIGNED NOT NULL,
                so_luong DECIMAL(15,2) NOT NULL DEFAULT 0,
                gia_von DECIMAL(15,2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                noi_dung TEXT NULL,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_kho_hang_ky (kho_hang_id, hang_hoa_id, ky_ke_toan_id),
                INDEX idx_cthh_ky (ky_ke_toan_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createChiTietHangSql, transaction: transaction);

            const string createChiTietCongNoSql = """
            CREATE TABLE IF NOT EXISTS chi_tiet_cong_no_dau_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                loai_doi_tac VARCHAR(20) NOT NULL COMMENT 'KHACH_HANG hoặc NHA_CUNG_CAP',
                doi_tac_id INT UNSIGNED NOT NULL,
                ma_doi_tac VARCHAR(50) NULL,
                ten_doi_tac VARCHAR(255) NULL,
                so_dien_thoai VARCHAR(50) NULL,
                so_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                ngay_dau_ky DATE NOT NULL,
                ngay_hen_tra DATE NULL,
                noi_dung TEXT NULL,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                ky_ke_toan_id INT UNSIGNED NULL,
                nguoi_tao_id INT UNSIGNED NULL,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_doitac_dauky (loai_doi_tac, doi_tac_id, ngay_dau_ky),
                INDEX idx_ctcn_ky (ky_ke_toan_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;
            await connection.ExecuteAsync(createChiTietCongNoSql, transaction: transaction);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("EnsureBangKyKeToanVaKetChuyenAsync", ex);
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả các kỳ kế toán hiện có trong cơ sở dữ liệu.
    /// Nếu chưa có kỳ nào, tự động tạo kỳ tháng hiện tại và kỳ tháng tiếp theo để người dùng làm việc.
    /// </summary>
    public async Task<IReadOnlyList<KyKeToanItem>> GetDanhSachKyKeToanItemAsync()
    {
        await using var connection = await OpenAsync();
        await EnsureBangKyKeToanVaKetChuyenAsync(connection);

        const string sql = """
        SELECT 
            id                  AS Id,
            ma_ky               AS MaKy,
            ten_ky              AS TenKy,
            ngay_bat_dau_ky     AS NgayBatDauKy,
            ngay_ket_thuc_ky    AS NgayKetThucKy,
            (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo
        FROM ky_ke_toan
        ORDER BY ngay_bat_dau_ky DESC;
        """;

        var list = (await connection.QueryAsync<KyKeToanItem>(sql)).ToList();

        // Nếu bảng kỳ kế toán chưa có hoặc chỉ có 1 kỳ, tự động sinh thêm kỳ hiện tại và kỳ sau
        if (list.Count < 2)
        {
            var now = DateTime.Today;
            await TaoHoacLayKyKeToanTheoThangAsync(now, connection);
            await TaoHoacLayKyKeToanTheoThangAsync(now.AddMonths(1), connection);
            await TaoHoacLayKyKeToanTheoThangAsync(now.AddMonths(-1), connection);

            list = (await connection.QueryAsync<KyKeToanItem>(sql)).ToList();
        }

        return list;
    }

    /// <summary>
    /// Tìm hoặc tạo mới một kỳ kế toán theo tháng năm chỉ định.
    /// </summary>
    public async Task<KyKeToanItem> TaoHoacLayKyKeToanTheoThangAsync(DateTime thangNam, MySqlConnection? existingConnection = null)
    {
        var conn = existingConnection ?? await OpenAsync();
        bool disposeConn = existingConnection == null;

        try
        {
            var maKy = $"KY_{thangNam:MM_yyyy}";
            const string findSql = """
            SELECT 
                id                  AS Id,
                ma_ky               AS MaKy,
                ten_ky              AS TenKy,
                ngay_bat_dau_ky     AS NgayBatDauKy,
                ngay_ket_thuc_ky    AS NgayKetThucKy,
                (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo
            FROM ky_ke_toan
            WHERE ma_ky = @MaKy
            LIMIT 1;
            """;

            var existing = await conn.QueryFirstOrDefaultAsync<KyKeToanItem>(findSql, new { MaKy = maKy });
            if (existing != null)
                return existing;

            var batDau = new DateTime(thangNam.Year, thangNam.Month, 1);
            var ketThuc = batDau.AddMonths(1).AddDays(-1);
            var tenKy = $"Tháng {thangNam.Month} Năm {thangNam.Year}";

            const string insertSql = """
            INSERT INTO ky_ke_toan (ma_ky, ten_ky, ngay_bat_dau_ky, ngay_ket_thuc_ky, trang_thai_khoa_so)
            VALUES (@MaKy, @TenKy, @BatDau, @KetThuc, 0)
            ON DUPLICATE KEY UPDATE ten_ky = @TenKy;
            SELECT LAST_INSERT_ID();
            """;

            var newId = await conn.ExecuteScalarAsync<uint>(insertSql, new
            {
                MaKy = maKy,
                TenKy = tenKy,
                BatDau = batDau,
                KetThuc = ketThuc
            });

            if (newId == 0)
            {
                existing = await conn.QueryFirstOrDefaultAsync<KyKeToanItem>(findSql, new { MaKy = maKy });
                if (existing != null) return existing;
            }

            return new KyKeToanItem
            {
                Id = newId,
                MaKy = maKy,
                TenKy = tenKy,
                NgayBatDauKy = batDau,
                NgayKetThucKy = ketThuc,
                TrangThaiKhoaSo = false
            };
        }
        finally
        {
            if (disposeConn)
                await conn.DisposeAsync();
        }
    }

    #endregion

    #region Nghiệp vụ Giao diện 53 – Kết chuyển hàng hóa

    /// <summary>
    /// Kiểm tra và xem trước dữ liệu kết chuyển tồn kho từ Kỳ nguồn sang Kỳ đích.
    /// Đọc dữ liệu tồn kho thật từ CSDL, kiểm tra trạng thái khóa sổ và dữ liệu đã có ở kỳ đích.
    /// </summary>
    public async Task<KetChuyenHangHoaPreview> PreviewKetChuyenHangHoaAsync(
        uint kyNguonId,
        uint kyDichId,
        uint? khoHangId = null)
    {
        await using var connection = await OpenAsync();
        await EnsureBangKyKeToanVaKetChuyenAsync(connection);

        var preview = new KetChuyenHangHoaPreview();

        // 1. Kiểm tra kỳ nguồn và kỳ đích
        var kyNguon = await connection.QueryFirstOrDefaultAsync<KyKeToanItem>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyNguonId });

        var kyDich = await connection.QueryFirstOrDefaultAsync<KyKeToanItem>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyDichId });

        preview.KyNguon = kyNguon;
        preview.KyDich = kyDich;

        if (kyDich != null && kyDich.TrangThaiKhoaSo)
        {
            preview.KyDichDaKhoaSo = true;
            preview.CanhBao = $"⚠️ Kỳ đích [{kyDich.TenKy}] hiện đang ĐÃ KHÓA SỔ. Hệ thống không cho phép kết chuyển số liệu vào kỳ đã khóa!";
            return preview;
        }

        // 2. Kiểm tra xem kỳ đích đã có bản ghi tồn đầu kỳ chưa
        var soBanGhiDauKyDich = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM chi_tiet_hang_hoa_dau_ky WHERE ky_ke_toan_id = @KyDichId;",
            new { KyDichId = kyDichId });

        preview.DaCoDuLieuDauKyDich = soBanGhiDauKyDich > 0;
        preview.SoBanGhiDauKyHienCo = soBanGhiDauKyDich;

        if (preview.DaCoDuLieuDauKyDich)
        {
            preview.CanhBao = $"ℹ️ Kỳ đích [{kyDich?.TenKy}] đã có {soBanGhiDauKyDich} mặt hàng tồn đầu kỳ. Thực hiện kết chuyển sẽ ghi đè và cập nhật lại chính xác số liệu mới nhất, đảm bảo KHÔNG làm nhân đôi số tồn.";
        }

        // 3. Đọc dữ liệu tồn kho hiện tại từ CSDL
        // Tương thích cả bảng ton_kho và san_pham
        bool hasTonKho = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ton_kho';") > 0;

        List<KetChuyenHangHoaItem> items;

        if (hasTonKho)
        {
            var khoFilter = (khoHangId.HasValue && khoHangId.Value > 0)
                ? "AND tk.chi_nhanh_id = @KhoId"
                : "";

            var sql = $"""
            SELECT 
                sp.id                                AS SanPhamId,
                sp.ma_san_pham                       AS MaSanPham,
                sp.ten_san_pham                      AS TenSanPham,
                COALESCE(dvt.ten_don_vi_tinh, '')    AS DonViTinh,
                COALESCE(tk.chi_nhanh_id, 1)         AS KhoHangId,
                COALESCE(cn.ten_chi_nhanh, 'Kho Tổng') AS TenKho,
                COALESCE(tk.so_luong_ton, 0)         AS SoLuongTon,
                COALESCE(tk.gia_von_binh_quan, sp.gia_von, 0) AS GiaVon,
                ROUND(COALESCE(tk.so_luong_ton, 0) * COALESCE(tk.gia_von_binh_quan, sp.gia_von, 0), 2) AS ThanhTien
            FROM san_pham sp
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id {khoFilter}
            LEFT JOIN chi_nhanh cn ON cn.id = tk.chi_nhanh_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            WHERE sp.dang_kinh_doanh = 1
            ORDER BY sp.ma_san_pham ASC;
            """;

            items = (await connection.QueryAsync<KetChuyenHangHoaItem>(sql, new { KhoId = khoHangId })).ToList();
        }
        else
        {
            const string sqlSp = """
            SELECT 
                sp.id                                AS SanPhamId,
                sp.ma_san_pham                       AS MaSanPham,
                sp.ten_san_pham                      AS TenSanPham,
                COALESCE(dvt.ten_don_vi_tinh, '')    AS DonViTinh,
                1                                    AS KhoHangId,
                'Kho Tổng (Mặc định)'                AS TenKho,
                COALESCE(sp.ton_kho_excel, 0)        AS SoLuongTon,
                COALESCE(sp.gia_von, 0)              AS GiaVon,
                ROUND(COALESCE(sp.ton_kho_excel, 0) * COALESCE(sp.gia_von, 0), 2) AS ThanhTien
            FROM san_pham sp
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            WHERE sp.dang_kinh_doanh = 1
            ORDER BY sp.ma_san_pham ASC;
            """;

            items = (await connection.QueryAsync<KetChuyenHangHoaItem>(sqlSp)).ToList();
        }

        // Đánh số thứ tự
        int stt = 0;
        foreach (var item in items)
        {
            item.SoThuTu = ++stt;
        }

        preview.Items = items;
        preview.TongSoMatHang = items.Count;
        preview.TongSoLuong = items.Sum(i => i.SoLuongTon);
        preview.TongGiaTri = items.Sum(i => i.ThanhTien);

        return preview;
    }

    /// <summary>
    /// Thực hiện kết chuyển tồn kho hàng hóa từ Kỳ nguồn sang Kỳ đích.
    /// Thực hiện trong Transaction an toàn, cập nhật chi_tiet_hang_hoa_dau_ky,
    /// chống cộng dồn hai lần nếu người dùng chạy lại nhiều lần, không làm hỏng chứng từ cũ.
    /// </summary>
    public async Task<KetChuyenKetQua> ThucHienKetChuyenHangHoaAsync(
        uint kyNguonId,
        uint kyDichId,
        uint? khoHangId = null,
        string? ghiChu = null)
    {
        if (kyNguonId == kyDichId)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = "Kỳ nguồn và Kỳ đích không được trùng nhau. Vui lòng chọn 2 kỳ kế toán khác nhau."
            };
        }

        await using var connection = await OpenAsync();
        await EnsureBangKyKeToanVaKetChuyenAsync(connection);

        // Kiểm tra trạng thái kỳ đích
        var kyDich = await connection.QueryFirstOrDefaultAsync<KyKeToanItem>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyDichId });

        if (kyDich == null)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = "Không tìm thấy kỳ kế toán đích."
            };
        }

        if (kyDich.TrangThaiKhoaSo)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = $"Kỳ đích [{kyDich.TenKy}] đã khóa sổ. Không thể kết chuyển vào kỳ đã khóa."
            };
        }

        // Lấy danh sách hàng hóa cần kết chuyển
        var preview = await PreviewKetChuyenHangHoaAsync(kyNguonId, kyDichId, khoHangId);
        if (preview.Items.Count == 0)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = "Không có mặt hàng nào để kết chuyển."
            };
        }

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var noiDung = string.IsNullOrWhiteSpace(ghiChu)
                ? $"Kết chuyển tồn kho từ kỳ {preview.KyNguon?.TenKy} sang kỳ {kyDich.TenKy}"
                : ghiChu.Trim();

            const string upsertSql = """
            INSERT INTO chi_tiet_hang_hoa_dau_ky
            (kho_hang_id, hang_hoa_id, ky_ke_toan_id, so_luong, gia_von, thanh_tien, noi_dung)
            VALUES
            (@KhoHangId, @HangHoaId, @KyKeToanId, @SoLuong, @GiaVon, @ThanhTien, @NoiDung)
            ON DUPLICATE KEY UPDATE
                so_luong = @SoLuong,
                gia_von = @GiaVon,
                thanh_tien = @ThanhTien,
                noi_dung = @NoiDung,
                ngay_cap_nhat = NOW();
            """;

            foreach (var item in preview.Items)
            {
                await connection.ExecuteAsync(upsertSql, new
                {
                    KhoHangId = item.KhoHangId,
                    HangHoaId = item.SanPhamId,
                    KyKeToanId = kyDichId,
                    SoLuong = item.SoLuongTon,
                    GiaVon = item.GiaVon,
                    ThanhTien = item.ThanhTien,
                    NoiDung = noiDung
                }, transaction: transaction);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Kết chuyển hàng hóa",
                "KY_KE_TOAN",
                kyDichId,
                kyDich.MaKy,
                $"Kết chuyển {preview.TongSoMatHang} mặt hàng từ {preview.KyNguon?.TenKy} sang {kyDich.TenKy}. Tổng tồn: {preview.TongSoLuong:N0}, Tổng giá trị: {preview.TongGiaTri:N0} đ. {noiDung}",
                preview.TongGiaTri);

            return new KetChuyenKetQua
            {
                ThanhCong = true,
                ThongBao = $"Kết chuyển hàng hóa thành công sang kỳ [{kyDich.TenKy}]!\n"
                         + $"• Tổng số mặt hàng: {preview.TongSoMatHang:N0}\n"
                         + $"• Tổng số lượng tồn: {preview.TongSoLuong:N0}\n"
                         + $"• Tổng giá trị tồn kho: {preview.TongGiaTri:N0} đ\n"
                         + (preview.DaCoDuLieuDauKyDich ? "• Dữ liệu đầu kỳ đích đã được cập nhật đồng bộ chính xác." : ""),
                SoBanGhi = preview.TongSoMatHang,
                TongGiaTri = preview.TongGiaTri,
                MaKyDich = kyDich.MaKy,
                TenKyDich = kyDich.TenKy
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi ThucHienKetChuyenHangHoaAsync", ex);
            throw;
        }
    }

    #endregion

    #region Nghiệp vụ Giao diện 54 – Kết chuyển công nợ

    /// <summary>
    /// Kiểm tra và xem trước dữ liệu kết chuyển công nợ (Khách hàng & Nhà cung cấp)
    /// từ Kỳ nguồn sang Kỳ đích. Tính toán số dư công nợ còn nợ từ CSDL thật.
    /// </summary>
    public async Task<KetChuyenCongNoPreview> PreviewKetChuyenCongNoAsync(
        uint kyNguonId,
        uint kyDichId,
        string loaiDoiTac = "TAT_CA")
    {
        await using var connection = await OpenAsync();
        await EnsureBangKyKeToanVaKetChuyenAsync(connection);

        var preview = new KetChuyenCongNoPreview();
        loaiDoiTac = (loaiDoiTac ?? "TAT_CA").Trim().ToUpperInvariant();

        // 1. Kiểm tra kỳ
        var kyNguon = await connection.QueryFirstOrDefaultAsync<KyKeToanItem>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyNguonId });

        var kyDich = await connection.QueryFirstOrDefaultAsync<KyKeToanItem>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyDichId });

        preview.KyNguon = kyNguon;
        preview.KyDich = kyDich;

        if (kyDich != null && kyDich.TrangThaiKhoaSo)
        {
            preview.KyDichDaKhoaSo = true;
            preview.CanhBao = $"⚠️ Kỳ đích [{kyDich.TenKy}] hiện đang ĐÃ KHÓA SỔ. Hệ thống không cho phép kết chuyển số liệu công nợ vào kỳ đã khóa!";
            return preview;
        }

        // 2. Kiểm tra xem kỳ đích đã có công nợ đầu kỳ chưa
        var soBanGhiDauKyDich = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM chi_tiet_cong_no_dau_ky WHERE ky_ke_toan_id = @KyDichId;",
            new { KyDichId = kyDichId });

        preview.DaCoDuLieuDauKyDich = soBanGhiDauKyDich > 0;
        preview.SoBanGhiDauKyHienCo = soBanGhiDauKyDich;

        if (preview.DaCoDuLieuDauKyDich)
        {
            preview.CanhBao = $"ℹ️ Kỳ đích [{kyDich?.TenKy}] đã có {soBanGhiDauKyDich} đối tác có số dư công nợ đầu kỳ. Kết chuyển sẽ ghi đè và cập nhật lại số dư công nợ mới nhất, tuyệt đối KHÔNG làm nhân đôi số nợ.";
        }

        var items = new List<KetChuyenCongNoItem>();

        // 3. Lấy công nợ khách hàng (phải thu) nếu chọn TAT_CA hoặc KHACH_HANG
        if (loaiDoiTac == "TAT_CA" || loaiDoiTac == "KHACH_HANG")
        {
            const string sqlKh = """
            SELECT 
                'KHACH_HANG'                         AS LoaiDoiTac,
                kh.id                                AS DoiTacId,
                kh.ma_khach_hang                     AS MaDoiTac,
                kh.ten_khach_hang                    AS TenDoiTac,
                kh.so_dien_thoai                     AS SoDienThoai,
                COALESCE(ls.SoDu, kh.no_can_thu_hien_tai, 0) AS SoDuNoCuoiKy
            FROM khach_hang kh
            LEFT JOIN (
                SELECT khach_hang_id, SUM(ghi_no - ghi_co) AS SoDu
                FROM lich_su_cong_no_khach_hang
                GROUP BY khach_hang_id
            ) ls ON ls.khach_hang_id = kh.id
            WHERE COALESCE(ls.SoDu, kh.no_can_thu_hien_tai, 0) > 0
            ORDER BY kh.ma_khach_hang ASC;
            """;

            var khItems = await connection.QueryAsync<KetChuyenCongNoItem>(sqlKh);
            items.AddRange(khItems);
        }

        // 4. Lấy công nợ nhà cung cấp (phải trả) nếu chọn TAT_CA hoặc NHA_CUNG_CAP
        if (loaiDoiTac == "TAT_CA" || loaiDoiTac == "NHA_CUNG_CAP")
        {
            const string sqlNcc = """
            SELECT 
                'NHA_CUNG_CAP'                       AS LoaiDoiTac,
                ncc.id                               AS DoiTacId,
                ncc.ma_nha_cung_cap                  AS MaDoiTac,
                ncc.ten_nha_cung_cap                 AS TenDoiTac,
                ncc.so_dien_thoai                    AS SoDienThoai,
                COALESCE(ls.SoDu, ncc.no_can_tra_hien_tai, 0) AS SoDuNoCuoiKy
            FROM nha_cung_cap ncc
            LEFT JOIN (
                SELECT nha_cung_cap_id, SUM(ghi_no - ghi_co) AS SoDu
                FROM lich_su_cong_no_nha_cung_cap
                GROUP BY nha_cung_cap_id
            ) ls ON ls.nha_cung_cap_id = ncc.id
            WHERE COALESCE(ls.SoDu, ncc.no_can_tra_hien_tai, 0) > 0
            ORDER BY ncc.ma_nha_cung_cap ASC;
            """;

            var nccItems = await connection.QueryAsync<KetChuyenCongNoItem>(sqlNcc);
            items.AddRange(nccItems);
        }

        // Đánh số thứ tự
        int stt = 0;
        foreach (var item in items)
        {
            item.SoThuTu = ++stt;
        }

        preview.Items = items;
        preview.TongSoDoiTac = items.Count;
        preview.SoKhachHang = items.Count(i => i.LoaiDoiTac == "KHACH_HANG");
        preview.SoNhaCungCap = items.Count(i => i.LoaiDoiTac == "NHA_CUNG_CAP");
        preview.TongNoPhaiThu = items.Where(i => i.LoaiDoiTac == "KHACH_HANG").Sum(i => i.SoDuNoCuoiKy);
        preview.TongNoPhaiTra = items.Where(i => i.LoaiDoiTac == "NHA_CUNG_CAP").Sum(i => i.SoDuNoCuoiKy);

        return preview;
    }

    /// <summary>
    /// Thực hiện kết chuyển công nợ sang Kỳ đích.
    /// Tái sử dụng bảng chi_tiet_cong_no_dau_ky trong Transaction an toàn,
    /// chống nhân đôi công nợ, không ghi thêm phiếu giả và không sửa đổi chứng từ bán/nhập gốc.
    /// </summary>
    public async Task<KetChuyenKetQua> ThucHienKetChuyenCongNoAsync(
        uint kyNguonId,
        uint kyDichId,
        string loaiDoiTac = "TAT_CA",
        string? ghiChu = null)
    {
        if (kyNguonId == kyDichId)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = "Kỳ nguồn và Kỳ đích không được trùng nhau. Vui lòng chọn 2 kỳ kế toán khác nhau."
            };
        }

        await using var connection = await OpenAsync();
        await EnsureBangKyKeToanVaKetChuyenAsync(connection);

        // Kiểm tra kỳ đích
        var kyDich = await connection.QueryFirstOrDefaultAsync<KyKeToanItem>(
            "SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy, (trang_thai_khoa_so = 1) AS TrangThaiKhoaSo FROM ky_ke_toan WHERE id = @Id;",
            new { Id = kyDichId });

        if (kyDich == null)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = "Không tìm thấy kỳ kế toán đích."
            };
        }

        if (kyDich.TrangThaiKhoaSo)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = $"Kỳ đích [{kyDich.TenKy}] đã khóa sổ. Không thể kết chuyển công nợ vào kỳ đã khóa."
            };
        }

        // Lấy danh sách đối tác còn nợ
        var preview = await PreviewKetChuyenCongNoAsync(kyNguonId, kyDichId, loaiDoiTac);
        if (preview.Items.Count == 0)
        {
            return new KetChuyenKetQua
            {
                ThanhCong = false,
                ThongBao = "Không có đối tác nào còn nợ để kết chuyển."
            };
        }

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var ngayDauKyDich = kyDich.NgayBatDauKy.Date;
            var branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
            var userId = Session.CurrentUser?.Id;
            var noiDung = string.IsNullOrWhiteSpace(ghiChu)
                ? $"Kết chuyển công nợ từ kỳ {preview.KyNguon?.TenKy} sang kỳ {kyDich.TenKy}"
                : ghiChu.Trim();

            const string upsertSql = """
            INSERT INTO chi_tiet_cong_no_dau_ky
            (
                loai_doi_tac, doi_tac_id, ma_doi_tac, ten_doi_tac, so_dien_thoai,
                so_tien, ngay_dau_ky, noi_dung, chi_nhanh_id, ky_ke_toan_id, nguoi_tao_id
            )
            VALUES
            (
                @LoaiDoiTac, @DoiTacId, @MaDoiTac, @TenDoiTac, @SoDienThoai,
                @SoTien, @NgayDauKy, @NoiDung, @ChiNhanhId, @KyKeToanId, @NguoiTaoId
            )
            ON DUPLICATE KEY UPDATE
                so_tien = @SoTien,
                noi_dung = @NoiDung,
                ky_ke_toan_id = @KyKeToanId,
                nguoi_tao_id = @NguoiTaoId,
                ngay_cap_nhat = NOW();
            """;

            foreach (var item in preview.Items)
            {
                await connection.ExecuteAsync(upsertSql, new
                {
                    LoaiDoiTac = item.LoaiDoiTac,
                    DoiTacId = item.DoiTacId,
                    MaDoiTac = item.MaDoiTac,
                    TenDoiTac = item.TenDoiTac,
                    SoDienThoai = item.SoDienThoai,
                    SoTien = item.SoDuNoCuoiKy,
                    NgayDauKy = ngayDauKyDich,
                    NoiDung = noiDung,
                    ChiNhanhId = branchId,
                    KyKeToanId = kyDichId,
                    NguoiTaoId = userId
                }, transaction: transaction);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Kết chuyển công nợ",
                "KY_KE_TOAN",
                kyDichId,
                kyDich.MaKy,
                $"Kết chuyển công nợ {preview.TongSoDoiTac} đối tác sang {kyDich.TenKy}. Phải thu: {preview.TongNoPhaiThu:N0} đ, Phải trả: {preview.TongNoPhaiTra:N0} đ. {noiDung}",
                preview.TongNoPhaiThu + preview.TongNoPhaiTra);

            return new KetChuyenKetQua
            {
                ThanhCong = true,
                ThongBao = $"Kết chuyển công nợ thành công sang kỳ [{kyDich.TenKy}]!\n"
                         + $"• Tổng đối tác: {preview.TongSoDoiTac:N0} (Khách hàng: {preview.SoKhachHang}, Nhà cung cấp: {preview.SoNhaCungCap})\n"
                         + $"• Tổng nợ phải thu: {preview.TongNoPhaiThu:N0} đ\n"
                         + $"• Tổng nợ phải trả: {preview.TongNoPhaiTra:N0} đ\n"
                         + $"• Công nợ ròng: {preview.TongConNoRong:N0} đ\n"
                         + (preview.DaCoDuLieuDauKyDich ? "• Dữ liệu công nợ đầu kỳ đích đã được cập nhật đồng bộ chính xác." : ""),
                SoBanGhi = preview.TongSoDoiTac,
                TongGiaTri = preview.TongNoPhaiThu + preview.TongNoPhaiTra,
                MaKyDich = kyDich.MaKy,
                TenKyDich = kyDich.TenKy
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi ThucHienKetChuyenCongNoAsync", ex);
            throw;
        }
    }

    #endregion
}
