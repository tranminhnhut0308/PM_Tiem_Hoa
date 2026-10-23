using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private static bool _daDamBaoBangCongNoKy;

    /// <summary>
    /// Đảm bảo bảng cong_no_khach_hang_ky tồn tại để lưu vết và chốt số liệu công nợ theo từng kỳ kế toán.
    /// </summary>
    public async Task EnsureBangCongNoKyAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        if (_daDamBaoBangCongNoKy) return;

        try
        {
            // Bảng kỳ và các cột mở rộng (ngày kết kỳ, người kết kỳ...) do module kho quản lý.
            await EnsureBangKyKhoAsync(connection, transaction);

            const string createSql = """
            CREATE TABLE IF NOT EXISTS cong_no_khach_hang_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ky_ke_toan_id INT UNSIGNED NOT NULL,
                khach_hang_id INT UNSIGNED NOT NULL,
                no_dau_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                phat_sinh_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                dieu_chinh_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                da_thu_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                no_cuoi_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                so_ky_no INT NOT NULL DEFAULT 1,
                muc_do_uu_tien VARCHAR(20) NOT NULL DEFAULT 'TRUNG_BINH',
                ghi_chu TEXT NULL,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_ky_khach (ky_ke_toan_id, khach_hang_id),
                INDEX idx_cnk_ky (ky_ke_toan_id),
                INDEX idx_cnk_khach (khach_hang_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(createSql, transaction: transaction);

            await AddColumnIfNotExistsAsync(connection, "cong_no_khach_hang_ky",
                "dieu_chinh_trong_ky", "DECIMAL(15,2) NOT NULL DEFAULT 0", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_cong_no_dau_ky",
                "loai_doi_tac", "VARCHAR(20) NOT NULL DEFAULT ''", transaction);
            await AddColumnIfNotExistsAsync(connection, "chi_tiet_cong_no_dau_ky",
                "ngay_dau_ky", "DATE NULL", transaction);

            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan",
                "ly_do_mo_lai", "TEXT NULL", transaction);
            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan",
                "nguoi_mo_lai_id", "INT UNSIGNED NULL", transaction);
            await AddColumnIfNotExistsAsync(connection, "ky_ke_toan",
                "ngay_mo_lai", "DATETIME NULL", transaction);

            // Bảng điều chỉnh công nợ (một số database cũ chưa có).
            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS dieu_chinh_cong_no (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_dieu_chinh VARCHAR(80) NOT NULL,
                khach_hang_id INT UNSIGNED NULL,
                nha_cung_cap_id INT UNSIGNED NULL,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                so_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
                loai_dieu_chinh VARCHAR(30) NOT NULL,
                ly_do TEXT NULL,
                nguoi_thuc_hien_id INT UNSIGNED NULL,
                thoi_gian DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY uk_ma_dieu_chinh (ma_dieu_chinh),
                INDEX idx_dc_khach (khach_hang_id),
                INDEX idx_dc_thoi_gian (thoi_gian)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """, transaction: transaction);

            // Bảng cấu hình ngưỡng ưu tiên nhắc nợ (idempotent).
            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS cau_hinh_cong_no (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                khoa VARCHAR(80) NOT NULL,
                gia_tri DECIMAL(15,2) NOT NULL DEFAULT 0,
                mo_ta VARCHAR(255) NULL,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_cau_hinh_cong_no (khoa)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """, transaction: transaction);

            await connection.ExecuteAsync("""
            INSERT INTO cau_hinh_cong_no (khoa, gia_tri, mo_ta)
            VALUES
                ('uu_tien_cao_so_ky', 3, 'Số kỳ nợ liên tiếp để xếp mức ưu tiên cao'),
                ('uu_tien_cao_so_tien', 5000000, 'Số tiền nợ để xếp mức ưu tiên cao'),
                ('uu_tien_trung_binh_so_ky', 2, 'Số kỳ nợ liên tiếp để xếp mức ưu tiên trung bình'),
                ('uu_tien_trung_binh_so_tien', 2000000, 'Số tiền nợ để xếp mức ưu tiên trung bình')
            ON DUPLICATE KEY UPDATE mo_ta = VALUES(mo_ta);
            """, transaction: transaction);

            _daDamBaoBangCongNoKy = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không tạo được bảng cong_no_khach_hang_ky", ex);
        }
    }

    /// <summary>
    /// Lấy danh sách công nợ khách hàng theo kỳ kế toán kinh doanh.
    /// Hỗ trợ tìm kiếm, lọc theo số kỳ nợ, mức độ ưu tiên nhắc nợ, số tiền nợ,
    /// chỉ khách còn nợ và sắp xếp linh hoạt.
    /// </summary>
    public async Task<IReadOnlyList<KhachHangKyCongNoItem>> GetCongNoTheoKyAsync(
        uint kyId,
        string? search = null,
        int? soKyFilter = null,
        string? uuTienFilter = null,
        decimal? minNoFilter = null,
        decimal? maxNoFilter = null,
        bool onlyDebt = false,
        string? sortMode = null)
    {
        if (kyId == 0)
            return Array.Empty<KhachHangKyCongNoItem>();

        await using var connection = await OpenAsync();
        await EnsureBangCongNoKyAsync(connection);

        return await GetCongNoTheoKyCoreAsync(
            connection,
            null,
            kyId,
            search,
            soKyFilter,
            uuTienFilter,
            minNoFilter,
            maxNoFilter,
            onlyDebt,
            sortMode);
    }

    private async Task<IReadOnlyList<KhachHangKyCongNoItem>> GetCongNoTheoKyCoreAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        uint kyId,
        string? search,
        int? soKyFilter,
        string? uuTienFilter,
        decimal? minNoFilter,
        decimal? maxNoFilter,
        bool onlyDebt,
        string? sortMode)
    {
        // 1. Lấy thông tin kỳ đang chọn
        const string sqlKy = """
        SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, 
               ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
               trang_thai_khoa_so AS TrangThaiKhoaSo, ngay_ket_ky AS NgayKetKy
        FROM ky_ke_toan
        WHERE id = @KyId;
        """;
        var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
            sqlKy, new { KyId = kyId }, transaction);
        if (ky is null)
            return Array.Empty<KhachHangKyCongNoItem>();

        var cauHinh = await LoadCauHinhCongNoAsync(connection, transaction);
        var list = new List<KhachHangKyCongNoItem>();

        // 2. Nếu kỳ đã khóa sổ (đã kết kỳ): đọc trực tiếp từ bảng cong_no_khach_hang_ky
        if (ky.TrangThaiKhoaSo)
        {
            const string sqlLocked = """
            SELECT 
                cnk.id AS Id,
                cnk.ky_ke_toan_id AS KyKeToanId,
                cnk.khach_hang_id AS KhachHangId,
                kh.ma_khach_hang AS MaKhachHang,
                kh.ten_khach_hang AS TenKhachHang,
                COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(kh.dia_chi, '') AS DiaChi,
                COALESCE(nkh.ten_nhom, 'Khách lẻ/Chưa nhóm') AS NhomKhachHang,
                cnk.no_dau_ky AS NoDauKy,
                cnk.phat_sinh_trong_ky AS PhatSinhTrongKy,
                COALESCE(cnk.dieu_chinh_trong_ky, 0) AS DieuChinhTrongKy,
                cnk.da_thu_trong_ky AS DaThuTrongKy,
                cnk.no_cuoi_ky AS NoCuoiKy,
                cnk.so_ky_no AS SoKyNo,
                cnk.muc_do_uu_tien AS MucDoUuTien,
                cnk.ghi_chu AS GhiChu
            FROM cong_no_khach_hang_ky cnk
            INNER JOIN khach_hang kh ON kh.id = cnk.khach_hang_id
            LEFT JOIN nhom_khach_hang nkh ON nkh.id = kh.nhom_khach_hang_id
            WHERE cnk.ky_ke_toan_id = @KyId
              AND kh.trang_thai = 1
            ORDER BY 
                CASE cnk.muc_do_uu_tien WHEN 'CAO' THEN 1 WHEN 'TRUNG_BINH' THEN 2 ELSE 3 END,
                cnk.so_ky_no DESC,
                cnk.no_cuoi_ky DESC;
            """;

            var rows = (await connection.QueryAsync<KhachHangKyCongNoItem>(
                sqlLocked, new { KyId = kyId }, transaction)).AsList();
            list.AddRange(rows);
        }
        else
        {
            // 3. Nếu kỳ đang mở: Tính toán realtime số liệu
            // 3.1. Tìm kỳ trước đó để kế thừa tồn nợ cuối
            const string sqlKyTruoc = """
            SELECT id AS Id, ngay_bat_dau_ky AS NgayBatDauKy, trang_thai_khoa_so AS TrangThaiKhoaSo
            FROM ky_ke_toan
            WHERE id <> @KyId AND ngay_bat_dau_ky < @NgayBatDau
            ORDER BY ngay_bat_dau_ky DESC
            LIMIT 1;
            """;
            var kyTruoc = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                sqlKyTruoc, new { KyId = kyId, NgayBatDau = ky.NgayBatDauKy }, transaction);

            // Đọc các bản ghi đã lưu sẵn trong kỳ này (nếu đã từng lưu hoặc điều chỉnh)
            var daLuuDict = new Dictionary<uint, KhachHangKyCongNoItem>();
            try
            {
                var daLuu = await connection.QueryAsync<KhachHangKyCongNoItem>(
                    "SELECT * FROM cong_no_khach_hang_ky WHERE ky_ke_toan_id = @KyId;",
                    new { KyId = kyId }, transaction);
                foreach (var dl in daLuu)
                    daLuuDict[dl.KhachHangId] = dl;
            }
            catch { }

            // Đọc nợ cuối kỳ trước nếu có
            var noCuoiKyTruocDict = new Dictionary<uint, (decimal NoCuoi, int SoKyNo)>();
            if (kyTruoc != null)
            {
                var rowsKyTruoc = await connection.QueryAsync<(uint KhachHangId, decimal NoCuoiKy, int SoKyNo)>(
                    "SELECT khach_hang_id, no_cuoi_ky, so_ky_no FROM cong_no_khach_hang_ky WHERE ky_ke_toan_id = @KyTruocId;",
                    new { KyTruocId = kyTruoc.Id }, transaction);
                foreach (var r in rowsKyTruoc)
                    noCuoiKyTruocDict[r.KhachHangId] = (r.NoCuoiKy, r.SoKyNo);
            }

            // Đọc công nợ ban đầu (chi_tiet_cong_no_dau_ky)
            var noBanDauDict = new Dictionary<uint, decimal>();
            try
            {
                // Có thể có nhiều lần khai báo nợ đầu kỳ khác ngày cho cùng khách hàng.
                // Lấy bản ghi mới nhất tính đến cuối kỳ để tránh cộng trùng.
                var rowsDauKy = await connection.QueryAsync<(uint DoiTacId, decimal SoTien)>(
                    """
                    SELECT doi_tac_id, so_tien
                    FROM chi_tiet_cong_no_dau_ky
                    WHERE loai_doi_tac = 'KHACH_HANG'
                      AND (ngay_dau_ky IS NULL OR ngay_dau_ky <= @DenNgay)
                    ORDER BY ngay_dau_ky ASC, id ASC;
                    """,
                    new { DenNgay = ky.NgayKetThucKy.Date },
                    transaction);
                foreach (var r in rowsDauKy)
                    noBanDauDict[r.DoiTacId] = r.SoTien;
            }
            catch { }

            // Đọc phát sinh tăng nợ (hóa đơn bán chịu / ghi nợ) trong khoảng thời gian của kỳ
            var tuNgay = ky.NgayBatDauKy.Date;
            var denNgay = ky.NgayKetThucKy.Date.AddDays(1).AddTicks(-1);

            var phatSinhDict = new Dictionary<uint, decimal>();
            var daThuDict = new Dictionary<uint, decimal>();
            var dieuChinhDict = new Dictionary<uint, decimal>();

            // Lấy từ sổ cái lịch sử công nợ. Các bút toán điều chỉnh được tính
            // riêng từ bảng dieu_chinh_cong_no để không bị cộng hai lần.
            const string sqlLichSu = """
            SELECT khach_hang_id AS KhachHangId,
                   SUM(CASE
                       WHEN ghi_no > 0
                        AND loai_giao_dich NOT IN ('NO_DAU_KY', 'DIEU_CHINH', 'DIEU_CHINH_TANG', 'DIEU_CHINH_GIAM')
                       THEN ghi_no ELSE 0
                   END) AS TongPhatSinh,
                   SUM(CASE
                       WHEN loai_giao_dich NOT IN ('DIEU_CHINH', 'DIEU_CHINH_TANG', 'DIEU_CHINH_GIAM')
                       THEN ghi_co ELSE 0
                   END) AS TongDaThu
            FROM lich_su_cong_no_khach_hang
            WHERE thoi_gian >= @TuNgay AND thoi_gian <= @DenNgay
            GROUP BY khach_hang_id;
            """;
            var lsRows = await connection.QueryAsync<(uint KhachHangId, decimal TongPhatSinh, decimal TongDaThu)>(
                sqlLichSu, new { TuNgay = tuNgay, DenNgay = denNgay }, transaction);
            foreach (var r in lsRows)
            {
                phatSinhDict[r.KhachHangId] = r.TongPhatSinh;
                daThuDict[r.KhachHangId] = r.TongDaThu;
            }

            try
            {
                const string sqlDieuChinh = """
                SELECT khach_hang_id AS KhachHangId,
                       SUM(CASE
                           WHEN UPPER(loai_dieu_chinh) IN ('DIEU_CHINH_GIAM', 'GIAM_NO', 'GIAM')
                           THEN -so_tien
                           ELSE so_tien
                       END) AS TongDieuChinh
                FROM dieu_chinh_cong_no
                WHERE khach_hang_id IS NOT NULL
                  AND thoi_gian >= @TuNgay AND thoi_gian <= @DenNgay
                GROUP BY khach_hang_id;
                """;
                var dcRows = await connection.QueryAsync<(uint KhachHangId, decimal TongDieuChinh)>(
                    sqlDieuChinh, new { TuNgay = tuNgay, DenNgay = denNgay }, transaction);
                foreach (var r in dcRows)
                    dieuChinhDict[r.KhachHangId] = r.TongDieuChinh;
            }
            catch { }

            // Lấy danh sách khách hàng hoạt động
            const string sqlKhach = """
            SELECT 
                kh.id AS Id,
                kh.ma_khach_hang AS MaKhachHang,
                kh.ten_khach_hang AS TenKhachHang,
                COALESCE(kh.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(kh.dia_chi, '') AS DiaChi,
                COALESCE(nkh.ten_nhom, 'Khách lẻ/Chưa nhóm') AS NhomKhachHang,
                kh.no_can_thu_hien_tai AS NoKiotViet
            FROM khach_hang kh
            LEFT JOIN nhom_khach_hang nkh ON nkh.id = kh.nhom_khach_hang_id
            WHERE kh.trang_thai = 1;
            """;
            var allKhach = (await connection.QueryAsync<dynamic>(sqlKhach, transaction: transaction)).AsList();

            foreach (var kh in allKhach)
            {
                uint khId = (uint)kh.Id;

                // Xác định nợ đầu kỳ
                decimal noDau = 0m;
                int soKy = 1;

                if (daLuuDict.TryGetValue(khId, out var dlItem))
                {
                    noDau = dlItem.NoDauKy;
                    soKy = dlItem.SoKyNo;
                }
                else if (noCuoiKyTruocDict.TryGetValue(khId, out var ktItem))
                {
                    noDau = ktItem.NoCuoi;
                    soKy = noDau > 0 ? (ktItem.SoKyNo + 1) : 1;
                }
                else if (noBanDauDict.TryGetValue(khId, out var nbVal))
                {
                    noDau = nbVal;
                    soKy = 1;
                }
                else
                {
                    // Fallback số nợ ban đầu từ file Excel KiotViet nếu có
                    decimal noKv = 0;
                    try { noKv = Convert.ToDecimal(kh.NoKiotViet ?? 0); } catch { }
                    noDau = noKv;
                    soKy = 1;
                }

                decimal phatSinh = phatSinhDict.GetValueOrDefault(khId, 0m);
                decimal daThu = daThuDict.GetValueOrDefault(khId, 0m);
                decimal dieuChinh = dieuChinhDict.GetValueOrDefault(khId, 0m);
                decimal noCuoi = Math.Max(0m, noDau + phatSinh + dieuChinh - daThu);

                // Chỉ đưa vào danh sách theo dõi công nợ nếu có nợ đầu, có phát sinh, hoặc còn nợ
                if (noDau > 0 || phatSinh > 0 || daThu > 0 || dieuChinh != 0 || noCuoi > 0)
                {
                    var item = new KhachHangKyCongNoItem
                    {
                        Id = dlItem?.Id ?? 0,
                        KyKeToanId = kyId,
                        KhachHangId = khId,
                        MaKhachHang = kh.MaKhachHang ?? "",
                        TenKhachHang = kh.TenKhachHang ?? "",
                        SoDienThoai = kh.SoDienThoai ?? "",
                        DiaChi = kh.DiaChi ?? "",
                        NhomKhachHang = kh.NhomKhachHang ?? "",
                        NoDauKy = noDau,
                        PhatSinhTrongKy = phatSinh,
                        DieuChinhTrongKy = dieuChinh,
                        DaThuTrongKy = daThu,
                        SoKyNo = soKy,
                        GhiChu = dlItem?.GhiChu
                    };

                    item.DanhGiaLaiMucDoUuTien(cauHinh);
                    list.Add(item);
                }
            }

            // Sắp xếp ưu tiên
            list = list.OrderBy(x => x.MucDoUuTien switch { "CAO" => 1, "TRUNG_BINH" => 2, _ => 3 })
                       .ThenByDescending(x => x.SoKyNo)
                       .ThenByDescending(x => x.NoCuoiKy)
                       .ToList();
        }

        // 4. Áp dụng các bộ lọc
        var query = list.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(x =>
                x.MaKhachHang.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.TenKhachHang.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.SoDienThoai.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        if (soKyFilter.HasValue && soKyFilter.Value > 0)
        {
            if (soKyFilter.Value >= 3)
                query = query.Where(x => x.SoKyNo >= 3);
            else
                query = query.Where(x => x.SoKyNo == soKyFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(uuTienFilter) && uuTienFilter != "ALL")
        {
            query = query.Where(x => string.Equals(x.MucDoUuTien, uuTienFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (minNoFilter.HasValue && minNoFilter.Value > 0)
        {
            query = query.Where(x => x.NoCuoiKy >= minNoFilter.Value);
        }

        if (maxNoFilter.HasValue && maxNoFilter.Value > 0)
        {
            query = query.Where(x => x.NoCuoiKy <= maxNoFilter.Value);
        }

        if (onlyDebt)
        {
            query = query.Where(x => x.NoCuoiKy > 0);
        }

        query = (sortMode ?? "uu_tien").Trim().ToLowerInvariant() switch
        {
            "no_giam" => query.OrderByDescending(x => x.NoCuoiKy)
                              .ThenByDescending(x => x.SoKyNo),
            "no_tang" => query.OrderBy(x => x.NoCuoiKy)
                              .ThenByDescending(x => x.SoKyNo),
            "so_ky" => query.OrderByDescending(x => x.SoKyNo)
                            .ThenByDescending(x => x.NoCuoiKy),
            "ten" => query.OrderBy(x => x.TenKhachHang, StringComparer.CurrentCultureIgnoreCase),
            _ => query.OrderBy(x => x.MucDoUuTien switch { "CAO" => 1, "TRUNG_BINH" => 2, _ => 3 })
                      .ThenByDescending(x => x.SoKyNo)
                      .ThenByDescending(x => x.NoCuoiKy)
        };

        return query.ToList();
    }

    /// <summary>
    /// Đọc ngưỡng ưu tiên nhắc nợ từ bảng cấu hình, fallback về giá trị mặc định.
    /// </summary>
    private static async Task<CongNoCauHinh> LoadCauHinhCongNoAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction)
    {
        var cfg = CongNoCauHinh.MacDinh;
        try
        {
            var rows = await connection.QueryAsync<(string Khoa, decimal GiaTri)>(
                "SELECT khoa, gia_tri FROM cau_hinh_cong_no;", transaction: transaction);
            foreach (var row in rows)
            {
                switch (row.Khoa)
                {
                    case "uu_tien_cao_so_ky":
                        cfg.SoKyUuTienCao = Math.Max(1, (int)row.GiaTri);
                        break;
                    case "uu_tien_cao_so_tien":
                        cfg.SoTienUuTienCao = row.GiaTri;
                        break;
                    case "uu_tien_trung_binh_so_ky":
                        cfg.SoKyUuTienTrungBinh = Math.Max(1, (int)row.GiaTri);
                        break;
                    case "uu_tien_trung_binh_so_tien":
                        cfg.SoTienUuTienTrungBinh = row.GiaTri;
                        break;
                }
            }
        }
        catch { }
        return cfg;
    }

    /// <summary>
    /// Chốt số liệu công nợ cuối kỳ (Kết kỳ) và khóa sổ kỳ này.
    /// Tự động kết chuyển nợ cuối kỳ này sang làm nợ đầu kỳ tiếp theo.
    /// </summary>
    public async Task<int> KetKyCongNoAsync(uint kyId)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.CustomersDebtClose,
            "Bạn không có quyền kết kỳ công nợ khách hàng.");

        if (kyId == 0)
            throw new ArgumentException("Mã kỳ không hợp lệ.", nameof(kyId));

        await using var connection = await OpenAsync();
        await EnsureBangCongNoKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Kiểm tra kỳ
            const string sqlKy = "SELECT * FROM ky_ke_toan WHERE id = @Id FOR UPDATE;";
            var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(sqlKy, new { Id = kyId }, transaction);
            if (ky is null)
                throw new InvalidOperationException("Không tìm thấy kỳ kế toán.");

            if (ky.TrangThaiKhoaSo)
                throw new InvalidOperationException($"Kỳ \"{ky.TenKy}\" đã được kết kỳ trước đó.");

            // 2. Tính toán và lưu chốt số liệu các khách nợ trong kỳ hiện tại.
            // Dùng chung connection/transaction để số liệu chốt là một ảnh chụp nhất quán.
            var danhSachKhach = await GetCongNoTheoKyCoreAsync(
                connection,
                transaction,
                kyId,
                null,
                null,
                null,
                null,
                null,
                false,
                "uu_tien");

            var cauHinh = await LoadCauHinhCongNoAsync(connection, transaction);

            const string upsertSql = """
            INSERT INTO cong_no_khach_hang_ky
            (
                ky_ke_toan_id, khach_hang_id, no_dau_ky, phat_sinh_trong_ky,
                dieu_chinh_trong_ky, da_thu_trong_ky, no_cuoi_ky, so_ky_no,
                muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyKeToanId, @KhachHangId, @NoDauKy, @PhatSinhTrongKy,
                @DieuChinhTrongKy, @DaThuTrongKy, @NoCuoiKy, @SoKyNo,
                @MucDoUuTien, @GhiChu
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDauKy,
                phat_sinh_trong_ky = @PhatSinhTrongKy,
                dieu_chinh_trong_ky = @DieuChinhTrongKy,
                da_thu_trong_ky = @DaThuTrongKy,
                no_cuoi_ky = @NoCuoiKy,
                so_ky_no = @SoKyNo,
                muc_do_uu_tien = @MucDoUuTien,
                ghi_chu = @GhiChu,
                ngay_cap_nhat = NOW();
            """;

            foreach (var item in danhSachKhach)
            {
                await connection.ExecuteAsync(upsertSql, new
                {
                    KyKeToanId = kyId,
                    KhachHangId = item.KhachHangId,
                    item.NoDauKy,
                    item.PhatSinhTrongKy,
                    item.DieuChinhTrongKy,
                    item.DaThuTrongKy,
                    NoCuoiKy = item.NoCuoiKy,
                    item.SoKyNo,
                    item.MucDoUuTien,
                    item.GhiChu
                }, transaction);
            }

            // Xóa các dòng cũ không còn phát sinh trong kỳ (ví dụ hóa đơn bị hủy
            // sau khi mở lại kỳ), tránh giữ lại số liệu chốt lỗi thời.
            if (danhSachKhach.Count == 0)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM cong_no_khach_hang_ky WHERE ky_ke_toan_id = @KyId;",
                    new { KyId = kyId }, transaction);
            }
            else
            {
                await connection.ExecuteAsync(
                    """
                    DELETE FROM cong_no_khach_hang_ky
                    WHERE ky_ke_toan_id = @KyId
                      AND khach_hang_id NOT IN @KhachHangIds;
                    """,
                    new
                    {
                        KyId = kyId,
                        KhachHangIds = danhSachKhach.Select(x => x.KhachHangId).ToArray()
                    }, transaction);
            }

            // 3. Khóa sổ kỳ hiện tại
            const string lockSql = """
            UPDATE ky_ke_toan
            SET trang_thai_khoa_so = 1,
                ngay_ket_ky = NOW(),
                nguoi_ket_ky_id = @UserId
            WHERE id = @Id;
            """;
            await connection.ExecuteAsync(lockSql, new { Id = kyId, UserId = Session.CurrentUser?.Id }, transaction);

            // 4. Tìm hoặc tự động tạo kỳ tiếp theo để kế thừa công nợ
            const string findNextSql = """
            SELECT id, trang_thai_khoa_so FROM ky_ke_toan
            WHERE ngay_bat_dau_ky > @NgayBatDau
            ORDER BY ngay_bat_dau_ky ASC 
            LIMIT 1
            FOR UPDATE;
            """;
            var nextKy = await connection.QueryFirstOrDefaultAsync<(uint Id, bool TrangThaiKhoaSo)>(
                findNextSql, new { NgayBatDau = ky.NgayBatDauKy }, transaction);

            if (nextKy.Id > 0 && nextKy.TrangThaiKhoaSo)
            {
                throw new InvalidOperationException(
                    "Kỳ kế tiếp đã được khóa sổ. Vui lòng mở lại kỳ kế tiếp trước khi kết kỳ này.");
            }

            var nextKyId = nextKy.Id > 0 ? nextKy.Id : (uint?)null;

            if (!nextKyId.HasValue || nextKyId.Value == 0)
            {
                // Tự sinh kỳ mới cho tháng kế tiếp
                var thangMoi = ky.NgayBatDauKy.AddMonths(1);
                var ngayDauThangMoi = new DateTime(thangMoi.Year, thangMoi.Month, 1);
                var ngayCuoiThangMoi = ngayDauThangMoi.AddMonths(1).AddDays(-1);
                var maKyMoi = $"KY_{thangMoi:yyyy_MM}";
                var tenKyMoi = $"Kỳ tháng {thangMoi:MM/yyyy}";

                const string taoKySql = """
                INSERT INTO ky_ke_toan (ma_ky, ten_ky, ngay_bat_dau_ky, ngay_ket_thuc_ky, trang_thai_khoa_so, chi_nhanh_id)
                VALUES (@MaKy, @TenKy, @NgayBatDau, @NgayKetThuc, 0, @ChiNhanhId);
                SELECT LAST_INSERT_ID();
                """;

                nextKyId = await connection.ExecuteScalarAsync<uint>(taoKySql, new
                {
                    MaKy = maKyMoi,
                    TenKy = tenKyMoi,
                    NgayBatDau = ngayDauThangMoi,
                    NgayKetThuc = ngayCuoiThangMoi,
                    ChiNhanhId = ky.ChiNhanhId > 0 ? ky.ChiNhanhId : 1
                }, transaction);
            }

            // 5. Kết chuyển số dư cuối kỳ sang làm đầu kỳ mới cho các khách còn nợ.
            // Upsert chỉ cập nhật nợ đầu kỳ, giữ nguyên phát sinh/đã thu nếu kỳ sau
            // đã có giao dịch (trường hợp kết kỳ muộn hoặc mở lại rồi kết lại).
            const string carrySql = """
            INSERT INTO cong_no_khach_hang_ky
            (
                ky_ke_toan_id, khach_hang_id, no_dau_ky, phat_sinh_trong_ky,
                dieu_chinh_trong_ky, da_thu_trong_ky, no_cuoi_ky, so_ky_no,
                muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyKeToanId, @KhachHangId, @NoDauKy, 0, 0, 0, @NoDauKy, @SoKyNo,
                @MucDoUuTien, @GhiChu
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = VALUES(no_dau_ky),
                no_cuoi_ky = GREATEST(0,
                    VALUES(no_dau_ky) + phat_sinh_trong_ky
                    + COALESCE(dieu_chinh_trong_ky, 0) - da_thu_trong_ky),
                so_ky_no = VALUES(so_ky_no),
                muc_do_uu_tien = VALUES(muc_do_uu_tien),
                ghi_chu = VALUES(ghi_chu),
                ngay_cap_nhat = NOW();
            """;

            var khachConNo = danhSachKhach.Where(k => k.NoCuoiKy > 0).ToList();
            foreach (var kn in khachConNo)
            {
                var nextSoKy = kn.SoKyNo + 1;
                var nextUuTien = cauHinh.DanhGiaMucDo(nextSoKy, kn.NoCuoiKy);

                await connection.ExecuteAsync(carrySql, new
                {
                    KyKeToanId = nextKyId.Value,
                    KhachHangId = kn.KhachHangId,
                    NoDauKy = kn.NoCuoiKy,
                    SoKyNo = nextSoKy,
                    MucDoUuTien = nextUuTien,
                    GhiChu = $"Kế thừa nợ từ {ky.TenKy}"
                }, transaction);
            }

            // Xóa/đưa về 0 phần kết chuyển cũ ở kỳ sau nếu sau khi mở lại kỳ,
            // khách hàng không còn nợ. Chỉ đụng tới nợ đầu kỳ, không xóa giao dịch.
            var conNoIds = khachConNo.Select(x => x.KhachHangId).ToArray();
            if (conNoIds.Length == 0)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE cong_no_khach_hang_ky
                    SET no_dau_ky = 0,
                        no_cuoi_ky = GREATEST(0, phat_sinh_trong_ky
                            + COALESCE(dieu_chinh_trong_ky, 0) - da_thu_trong_ky),
                        ngay_cap_nhat = NOW()
                    WHERE ky_ke_toan_id = @KyId AND no_dau_ky <> 0;
                    """,
                    new { KyId = nextKyId.Value }, transaction);
            }
            else
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE cong_no_khach_hang_ky
                    SET no_dau_ky = 0,
                        no_cuoi_ky = GREATEST(0, phat_sinh_trong_ky
                            + COALESCE(dieu_chinh_trong_ky, 0) - da_thu_trong_ky),
                        ngay_cap_nhat = NOW()
                    WHERE ky_ke_toan_id = @KyId
                      AND no_dau_ky <> 0
                      AND khach_hang_id NOT IN @ConNoIds;
                    """,
                    new { KyId = nextKyId.Value, ConNoIds = conNoIds }, transaction);
            }

            await connection.ExecuteAsync(
                """
                DELETE FROM cong_no_khach_hang_ky
                WHERE ky_ke_toan_id = @KyId
                  AND no_dau_ky = 0 AND phat_sinh_trong_ky = 0
                  AND COALESCE(dieu_chinh_trong_ky, 0) = 0
                  AND da_thu_trong_ky = 0 AND no_cuoi_ky = 0;
                """,
                new { KyId = nextKyId.Value }, transaction);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Kết kỳ công nợ",
                "KY_KE_TOAN",
                kyId,
                ky.MaKy,
                $"Kết kỳ công nợ {ky.TenKy}: Chốt nợ cho {danhSachKhach.Count} khách hàng, kết chuyển {khachConNo.Count} khách còn nợ sang kỳ mới.",
                danhSachKhach.Sum(x => x.NoCuoiKy));

            return khachConNo.Count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi KetKyCongNoAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Mở lại kỳ công nợ đã khóa sổ (dành cho người có quyền kết kỳ công nợ).
    /// Lưu lại lý do/người mở để phục vụ kiểm toán.
    /// </summary>
    public async Task<bool> MoLaiKyCongNoAsync(uint kyId, string? lyDo = null)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.CustomersDebtClose,
            "Bạn không có quyền mở lại kỳ công nợ khách hàng.");

        if (kyId == 0)
            throw new ArgumentException("Mã kỳ không hợp lệ.", nameof(kyId));

        await using var connection = await OpenAsync();
        await EnsureBangCongNoKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                "SELECT * FROM ky_ke_toan WHERE id = @Id FOR UPDATE;",
                new { Id = kyId }, transaction);

            if (ky is null)
                throw new InvalidOperationException("Không tìm thấy kỳ kế toán.");

            if (!ky.TrangThaiKhoaSo)
                throw new InvalidOperationException($"Kỳ \"{ky.TenKy}\" chưa được kết kỳ.");

            var nextKy = await connection.QueryFirstOrDefaultAsync<(uint Id, bool TrangThaiKhoaSo)>(
                """
                SELECT id, trang_thai_khoa_so FROM ky_ke_toan
                WHERE ngay_bat_dau_ky > @NgayBatDau
                ORDER BY ngay_bat_dau_ky ASC
                LIMIT 1
                FOR UPDATE;
                """,
                new { NgayBatDau = ky.NgayBatDauKy }, transaction);

            if (nextKy.Id > 0 && nextKy.TrangThaiKhoaSo)
            {
                throw new InvalidOperationException(
                    "Kỳ kế tiếp đã được khóa sổ. Vui lòng mở lại kỳ kế tiếp trước.");
            }

            var reason = string.IsNullOrWhiteSpace(lyDo)
                ? "Mở lại để điều chỉnh số liệu công nợ"
                : lyDo.Trim();

            const string sql = """
            UPDATE ky_ke_toan
            SET trang_thai_khoa_so = 0,
                ngay_ket_ky = NULL,
                nguoi_ket_ky_id = NULL,
                ly_do_mo_lai = @LyDo,
                nguoi_mo_lai_id = @UserId,
                ngay_mo_lai = NOW()
            WHERE id = @KyId;
            """;
            var rows = await connection.ExecuteAsync(sql, new
            {
                KyId = kyId,
                LyDo = reason,
                UserId = Session.CurrentUser?.Id
            }, transaction);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Mở lại kỳ công nợ",
                "KY_KE_TOAN",
                kyId,
                ky.MaKy,
                $"Mở lại kỳ công nợ {ky.TenKy}. Lý do: {reason}",
                0m);

            return rows > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi MoLaiKyCongNoAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Nạp lại công nợ cuối kỳ trước sang làm nợ đầu kỳ này.
    /// </summary>
    public async Task<int> LoadLaiNoKyTruocAsync(uint currentKyId, uint priorKyId)
    {
        await using var connection = await OpenAsync();
        await EnsureBangCongNoKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Lấy nợ cuối của kỳ trước
            const string sqlPrior = """
            SELECT khach_hang_id, no_cuoi_ky, so_ky_no 
            FROM cong_no_khach_hang_ky 
            WHERE ky_ke_toan_id = @PriorKyId AND no_cuoi_ky > 0;
            """;
            var priorDebtors = (await connection.QueryAsync<(uint KhachHangId, decimal NoCuoiKy, int SoKyNo)>(
                sqlPrior, new { PriorKyId = priorKyId }, transaction)).AsList();

            var cauHinh = await LoadCauHinhCongNoAsync(connection, transaction);

            const string upsertSql = """
            INSERT INTO cong_no_khach_hang_ky
            (
                ky_ke_toan_id, khach_hang_id, no_dau_ky, phat_sinh_trong_ky,
                dieu_chinh_trong_ky, da_thu_trong_ky, no_cuoi_ky, so_ky_no,
                muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyId, @KhachHangId, @NoDauKy, 0, 0, 0, @NoDauKy, @SoKyNo,
                @MucDoUuTien, 'Đồng bộ từ kỳ trước'
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = VALUES(no_dau_ky),
                no_cuoi_ky = GREATEST(0,
                    VALUES(no_dau_ky) + phat_sinh_trong_ky
                    + COALESCE(dieu_chinh_trong_ky, 0) - da_thu_trong_ky),
                so_ky_no = VALUES(so_ky_no),
                muc_do_uu_tien = VALUES(muc_do_uu_tien),
                ghi_chu = VALUES(ghi_chu),
                ngay_cap_nhat = NOW();
            """;

            int updated = 0;
            foreach (var d in priorDebtors)
            {
                var soKy = d.SoKyNo + 1;
                var uuTien = cauHinh.DanhGiaMucDo(soKy, d.NoCuoiKy);

                await connection.ExecuteAsync(upsertSql, new
                {
                    KyId = currentKyId,
                    KhachHangId = d.KhachHangId,
                    NoDauKy = d.NoCuoiKy,
                    SoKyNo = soKy,
                    MucDoUuTien = uuTien
                }, transaction);

                updated++;
            }

            await transaction.CommitAsync();
            return updated;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi LoadLaiNoKyTruocAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Cập nhật ghi chú nhắc nợ hoặc thay đổi mức độ ưu tiên cho một khách hàng trong kỳ.
    /// </summary>
    public async Task<bool> LuuGhiChuNhacNoAsync(uint kyId, uint khachHangId, string? ghiChu, string? mucDoUuTien)
    {
        await using var connection = await OpenAsync();
        await EnsureBangCongNoKyAsync(connection);

        const string sql = """
        UPDATE cong_no_khach_hang_ky
        SET ghi_chu = @GhiChu,
            muc_do_uu_tien = COALESCE(@MucDoUuTien, muc_do_uu_tien),
            ngay_cap_nhat = NOW()
        WHERE ky_ke_toan_id = @KyId AND khach_hang_id = @KhachHangId;
        """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            KyId = kyId,
            KhachHangId = khachHangId,
            GhiChu = ghiChu,
            MucDoUuTien = mucDoUuTien
        });

        return affected > 0;
    }
}
