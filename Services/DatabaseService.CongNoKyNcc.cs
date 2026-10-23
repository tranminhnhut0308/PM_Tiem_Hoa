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
    private static bool _daDamBaoBangCongNoNccKy;

    /// <summary>
    /// Đảm bảo bảng cong_no_ncc_ky tồn tại để lưu vết và chốt số liệu công nợ nhà cung cấp theo từng kỳ kế toán.
    /// </summary>
    public async Task EnsureBangCongNoNccKyAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        if (_daDamBaoBangCongNoNccKy) return;

        try
        {
            const string createSql = """
            CREATE TABLE IF NOT EXISTS cong_no_ncc_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ky_ke_toan_id INT UNSIGNED NOT NULL,
                nha_cung_cap_id INT UNSIGNED NOT NULL,
                no_dau_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                phat_sinh_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                da_tra_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                no_cuoi_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                so_ky_no INT NOT NULL DEFAULT 1,
                muc_do_uu_tien VARCHAR(20) NOT NULL DEFAULT 'TRUNG_BINH',
                ghi_chu TEXT NULL,
                ky_nguon_id INT UNSIGNED NULL,
                la_chuyen_ky TINYINT(1) NOT NULL DEFAULT 0,
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_ky_ncc (ky_ke_toan_id, nha_cung_cap_id),
                INDEX idx_cnk_ncc_ky (ky_ke_toan_id),
                INDEX idx_cnk_ncc_ncc (nha_cung_cap_id),
                INDEX idx_cnk_ncc_ky_nguon (ky_nguon_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(createSql, transaction: transaction);

            await ThemCotNeuThieuAsync(connection, transaction, "cong_no_ncc_ky", "ky_nguon_id",
                "ALTER TABLE cong_no_ncc_ky ADD COLUMN ky_nguon_id INT UNSIGNED NULL AFTER ghi_chu;");
            await ThemCotNeuThieuAsync(connection, transaction, "cong_no_ncc_ky", "la_chuyen_ky",
                "ALTER TABLE cong_no_ncc_ky ADD COLUMN la_chuyen_ky TINYINT(1) NOT NULL DEFAULT 0 AFTER ky_nguon_id;");

            _daDamBaoBangCongNoNccKy = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không tạo được bảng cong_no_ncc_ky", ex);
        }
    }

    private static async Task ThemCotNeuThieuAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string tableName,
        string columnName,
        string alterSql)
    {
        var exists = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
              AND column_name = @ColumnName;
            """,
            new { TableName = tableName, ColumnName = columnName },
            transaction);

        if (exists > 0) return;

        try
        {
            await connection.ExecuteAsync(alterSql, transaction: transaction);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError($"Không bổ sung được cột {tableName}.{columnName}", ex);
        }
    }

    /// <summary>
    /// Lấy danh sách công nợ nhà cung cấp theo kỳ kế toán kinh doanh.
    /// Hỗ trợ tìm kiếm, lọc theo số kỳ nợ, mức độ ưu tiên thanh toán và số tiền nợ.
    /// </summary>
    public async Task<IReadOnlyList<NhaCungCapKyCongNoItem>> GetCongNoNccTheoKyAsync(
        uint kyId,
        string? search = null,
        int? soKyFilter = null,
        string? uuTienFilter = null,
        decimal? minNoFilter = null,
        decimal? maxNoFilter = null)
    {
        if (kyId == 0)
            return Array.Empty<NhaCungCapKyCongNoItem>();

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await EnsureBangCongNoNccKyAsync(connection);

        return await GetCongNoNccTheoKyCoreAsync(
            connection,
            null,
            kyId,
            search,
            soKyFilter,
            uuTienFilter,
            minNoFilter,
            maxNoFilter);
    }

    private async Task<IReadOnlyList<NhaCungCapKyCongNoItem>> GetCongNoNccTheoKyCoreAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        uint kyId,
        string? search = null,
        int? soKyFilter = null,
        string? uuTienFilter = null,
        decimal? minNoFilter = null,
        decimal? maxNoFilter = null)
    {
        var sqlKy = """
        SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
               ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
               trang_thai_khoa_so AS TrangThaiKhoaSo,
               chi_nhanh_id AS ChiNhanhId
        FROM ky_ke_toan
        WHERE id = @KyId
        """;
        if (transaction is not null)
            sqlKy += " FOR UPDATE";

        var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(sqlKy, new { KyId = kyId }, transaction);
        if (ky is null)
            return Array.Empty<NhaCungCapKyCongNoItem>();

        var list = ky.TrangThaiKhoaSo
            ? await DocCongNoNccDaChotAsync(connection, transaction, ky)
            : await TinhCongNoNccTheoKyAsync(connection, transaction, ky);

        var query = list.Where(x => x.NoCuoiKy > 0);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(x =>
                x.MaNhaCungCap.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.TenNhaCungCap.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
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
            query = query.Where(x => x.NoCuoiKy >= minNoFilter.Value);

        if (maxNoFilter.HasValue && maxNoFilter.Value > 0)
            query = query.Where(x => x.NoCuoiKy <= maxNoFilter.Value);

        return query
            .OrderBy(x => x.MucDoUuTien switch { "CAO" => 1, "TRUNG_BINH" => 2, _ => 3 })
            .ThenByDescending(x => x.SoKyNo)
            .ThenByDescending(x => x.NoCuoiKy)
            .ToList();
    }

    private async Task<List<NhaCungCapKyCongNoItem>> DocCongNoNccDaChotAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        KyKeToan ky)
    {
        const string sql = """
        SELECT
            cnk.id AS Id,
            cnk.ky_ke_toan_id AS KyKeToanId,
            cnk.nha_cung_cap_id AS NhaCungCapId,
            ncc.ma_nha_cung_cap AS MaNhaCungCap,
            ncc.ten_nha_cung_cap AS TenNhaCungCap,
            COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
            COALESCE(ncc.email, '') AS Email,
            COALESCE(ncc.dia_chi, '') AS DiaChi,
            COALESCE(ncc.nguoi_lien_he, '') AS NguoiLienHe,
            COALESCE(nncc.ten_nhom, 'Mặc định') AS NhomNhaCungCap,
            cnk.no_dau_ky AS NoDauKy,
            cnk.phat_sinh_trong_ky AS PhatSinhTrongKy,
            cnk.da_tra_trong_ky AS DaTraTrongKy,
            cnk.so_ky_no AS SoKyNo,
            cnk.muc_do_uu_tien AS MucDoUuTien,
            cnk.ghi_chu AS GhiChu
        FROM cong_no_ncc_ky cnk
        INNER JOIN nha_cung_cap ncc ON ncc.id = cnk.nha_cung_cap_id
        LEFT JOIN nhom_nha_cung_cap nncc ON nncc.id = ncc.nhom_nha_cung_cap_id
        WHERE cnk.ky_ke_toan_id = @KyId
        ORDER BY
            CASE cnk.muc_do_uu_tien WHEN 'CAO' THEN 1 WHEN 'TRUNG_BINH' THEN 2 ELSE 3 END,
            cnk.so_ky_no DESC,
            cnk.no_cuoi_ky DESC;
        """;

        var rows = (await connection.QueryAsync<NhaCungCapKyCongNoItem>(
            sql, new { KyId = ky.Id }, transaction)).AsList();

        foreach (var row in rows)
            row.DanhGiaLaiMucDoUuTien();

        return rows.Where(x => x.NoCuoiKy > 0).ToList();
    }

    private async Task<List<NhaCungCapKyCongNoItem>> TinhCongNoNccTheoKyAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        KyKeToan ky)
    {
        var tuNgay = ky.NgayBatDauKy.Date;
        var denNgay = ky.NgayKetThucKy.Date.AddDays(1).AddSeconds(-1);

        const string sqlKyTruoc = """
        SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
               ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
               trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
        FROM ky_ke_toan
        WHERE id <> @KyId AND ngay_bat_dau_ky < @NgayBatDau
        ORDER BY ngay_bat_dau_ky DESC
        LIMIT 1;
        """;
        var kyTruoc = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
            sqlKyTruoc, new { KyId = ky.Id, NgayBatDau = ky.NgayBatDauKy }, transaction);

        var daLuu = new Dictionary<uint, NccCongNoSnapshotRow>();
        foreach (var row in await connection.QueryAsync<NccCongNoSnapshotRow>(
            """
            SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId,
                   no_dau_ky AS NoDauKy, phat_sinh_trong_ky AS PhatSinhTrongKy,
                   da_tra_trong_ky AS DaTraTrongKy, so_ky_no AS SoKyNo,
                   muc_do_uu_tien AS MucDoUuTien, ghi_chu AS GhiChu,
                   ky_nguon_id AS KyNguonId, la_chuyen_ky AS LaChuyenKy
            FROM cong_no_ncc_ky
            WHERE ky_ke_toan_id = @KyId;
            """,
            new { KyId = ky.Id }, transaction))
        {
            daLuu[row.NhaCungCapId] = row;
        }

        var noKyTruoc = new Dictionary<uint, NccCongNoSnapshotRow>();
        if (kyTruoc is not null)
        {
            foreach (var row in await connection.QueryAsync<NccCongNoSnapshotRow>(
                """
                SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId,
                       no_dau_ky AS NoDauKy, phat_sinh_trong_ky AS PhatSinhTrongKy,
                       da_tra_trong_ky AS DaTraTrongKy, so_ky_no AS SoKyNo,
                       muc_do_uu_tien AS MucDoUuTien, ghi_chu AS GhiChu,
                       ky_nguon_id AS KyNguonId, la_chuyen_ky AS LaChuyenKy
                FROM cong_no_ncc_ky
                WHERE ky_ke_toan_id = @KyTruocId;
                """,
                new { KyTruocId = kyTruoc.Id }, transaction))
            {
                noKyTruoc[row.NhaCungCapId] = row;
            }
        }

        var khaiBaoDauKy = new Dictionary<uint, NccDauKyRow>();
        try
        {
            foreach (var row in await connection.QueryAsync<NccDauKyRow>(
                """
                SELECT c.doi_tac_id AS DoiTacId, c.so_tien AS SoTien, c.ngay_dau_ky AS NgayDauKy
                FROM chi_tiet_cong_no_dau_ky c
                INNER JOIN
                (
                    SELECT doi_tac_id, MAX(ngay_dau_ky) AS NgayDauKy
                    FROM chi_tiet_cong_no_dau_ky
                    WHERE loai_doi_tac = 'NHA_CUNG_CAP' AND ngay_dau_ky <= @DenNgay
                    GROUP BY doi_tac_id
                ) x ON x.doi_tac_id = c.doi_tac_id AND x.NgayDauKy = c.ngay_dau_ky
                WHERE c.loai_doi_tac = 'NHA_CUNG_CAP';
                """,
                new { DenNgay = denNgay }, transaction))
            {
                khaiBaoDauKy[row.DoiTacId] = row;
            }
        }
        catch
        {
            // Bảng khai báo đầu kỳ có thể chưa tồn tại ở database cũ.
        }

        var truocKy = new Dictionary<uint, NccPhatSinhAggregateRow>();
        foreach (var row in await connection.QueryAsync<NccPhatSinhAggregateRow>(
            """
            SELECT nha_cung_cap_id AS NhaCungCapId,
                   SUM(CASE WHEN loai_giao_dich <> 'NO_DAU_KY' THEN ghi_no - ghi_co ELSE 0 END) AS TongPhatSinh,
                   SUM(CASE WHEN loai_giao_dich = 'NO_DAU_KY' THEN ghi_no ELSE 0 END) AS TongDaTra
            FROM lich_su_cong_no_nha_cung_cap
            WHERE thoi_gian < @TuNgay
            GROUP BY nha_cung_cap_id;
            """,
            new { TuNgay = tuNgay }, transaction))
        {
            truocKy[row.NhaCungCapId] = row;
        }

        var hoatDong = new Dictionary<uint, NccPhatSinhAggregateRow>();
        foreach (var row in await connection.QueryAsync<NccPhatSinhAggregateRow>(
            """
            SELECT nha_cung_cap_id AS NhaCungCapId,
                   SUM(ghi_no) AS TongPhatSinh,
                   SUM(ghi_co) AS TongDaTra
            FROM lich_su_cong_no_nha_cung_cap
            WHERE thoi_gian >= @TuNgay AND thoi_gian <= @DenNgay
              AND loai_giao_dich <> 'NO_DAU_KY'
            GROUP BY nha_cung_cap_id;
            """,
            new { TuNgay = tuNgay, DenNgay = denNgay }, transaction))
        {
            hoatDong[row.NhaCungCapId] = row;
        }

        if (hoatDong.Count == 0)
        {
            try
            {
                foreach (var row in await connection.QueryAsync<NccPhatSinhAggregateRow>(
                    """
                    SELECT nha_cung_cap_id AS NhaCungCapId,
                           SUM(tong_tien_nhap) AS TongPhatSinh,
                           SUM(da_thanh_toan) AS TongDaTra
                    FROM phieu_nhap_hang
                    WHERE thoi_gian_nhap >= @TuNgay AND thoi_gian_nhap <= @DenNgay
                      AND trang_thai NOT IN ('LUU_TAM', 'HUY', 'DA_HUY')
                      AND nha_cung_cap_id IS NOT NULL
                    GROUP BY nha_cung_cap_id;
                    """,
                    new { TuNgay = tuNgay, DenNgay = denNgay }, transaction))
                {
                    hoatDong[row.NhaCungCapId] = row;
                }
            }
            catch
            {
                // Sổ cái là nguồn chính; bảng phiếu nhập chỉ dùng để đối chiếu dữ liệu cũ.
            }
        }

        var allNcc = (await connection.QueryAsync<NccThongTinRow>(
            """
            SELECT
                ncc.id AS Id,
                ncc.ma_nha_cung_cap AS MaNhaCungCap,
                ncc.ten_nha_cung_cap AS TenNhaCungCap,
                COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(ncc.email, '') AS Email,
                COALESCE(ncc.dia_chi, '') AS DiaChi,
                COALESCE(ncc.nguoi_lien_he, '') AS NguoiLienHe,
                COALESCE(nncc.ten_nhom, 'Mặc định') AS NhomNhaCungCap,
                COALESCE(ncc.no_can_tra_hien_tai_excel, 0) AS NoExcel
            FROM nha_cung_cap ncc
            LEFT JOIN nhom_nha_cung_cap nncc ON nncc.id = ncc.nhom_nha_cung_cap_id;
            """, transaction: transaction)).AsList();

        var list = new List<NhaCungCapKyCongNoItem>();

        foreach (var ncc in allNcc)
        {
            decimal noDau = 0m;
            int soKy = 1;
            uint snapshotId = 0;
            string? ghiChu = null;

            if (daLuu.TryGetValue(ncc.Id, out var daLuuRow))
            {
                noDau = daLuuRow.NoDauKy;
                soKy = Math.Max(1, daLuuRow.SoKyNo);
                snapshotId = daLuuRow.Id;
                ghiChu = daLuuRow.GhiChu;
            }
            else if (noKyTruoc.TryGetValue(ncc.Id, out var truocRow))
            {
                noDau = truocRow.NoDauKy + truocRow.PhatSinhTrongKy - truocRow.DaTraTrongKy;
                noDau = Math.Max(0m, noDau);
                soKy = noDau > 0 ? Math.Max(2, truocRow.SoKyNo + 1) : 1;
            }
            else
            {
                var coKhaiBaoTrongKy = khaiBaoDauKy.TryGetValue(ncc.Id, out var khaiBao)
                    && khaiBao.NgayDauKy.Date >= tuNgay;

                if (kyTruoc?.TrangThaiKhoaSo == true && !coKhaiBaoTrongKy)
                {
                    // Kỳ trước đã chốt: snapshot là nguồn duy nhất. Không có dòng
                    // nghĩa là nhà cung cấp không còn nợ, không được dựng lại nợ cũ
                    // từ sổ cái lịch sử.
                    noDau = 0m;
                }
                else if (coKhaiBaoTrongKy)
                {
                    noDau = khaiBao!.SoTien;
                }
                else
                {
                    truocKy.TryGetValue(ncc.Id, out var rawTruoc);
                    var bienDongTruoc = rawTruoc?.TongPhatSinh ?? 0m;
                    var noDauLedgerTruoc = rawTruoc?.TongDaTra ?? 0m;

                    if (khaiBaoDauKy.TryGetValue(ncc.Id, out var khaiBaoCu))
                    {
                        noDau = noDauLedgerTruoc > 0
                            ? noDauLedgerTruoc + bienDongTruoc
                            : khaiBaoCu.SoTien + bienDongTruoc;
                    }
                    else
                    {
                        noDau = noDauLedgerTruoc + bienDongTruoc;
                    }

                    if (noDau == 0m && kyTruoc is null)
                        noDau = ncc.NoExcel;
                }

                noDau = Math.Max(0m, noDau);
                soKy = 1;
            }

            hoatDong.TryGetValue(ncc.Id, out var phatSinh);
            var phatSinhTrongKy = phatSinh?.TongPhatSinh ?? 0m;
            var daTraTrongKy = phatSinh?.TongDaTra ?? 0m;
            var noCuoiKy = Math.Max(0m, noDau + phatSinhTrongKy - daTraTrongKy);

            if (noDau == 0m && phatSinhTrongKy == 0m && daTraTrongKy == 0m && noCuoiKy == 0m)
                continue;

            var item = new NhaCungCapKyCongNoItem
            {
                Id = snapshotId,
                KyKeToanId = ky.Id,
                NhaCungCapId = ncc.Id,
                MaNhaCungCap = ncc.MaNhaCungCap ?? "",
                TenNhaCungCap = ncc.TenNhaCungCap ?? "",
                SoDienThoai = ncc.SoDienThoai ?? "",
                Email = ncc.Email ?? "",
                DiaChi = ncc.DiaChi ?? "",
                NguoiLienHe = ncc.NguoiLienHe ?? "",
                NhomNhaCungCap = ncc.NhomNhaCungCap ?? "",
                NoDauKy = noDau,
                PhatSinhTrongKy = phatSinhTrongKy,
                DaTraTrongKy = daTraTrongKy,
                SoKyNo = soKy,
                GhiChu = ghiChu
            };

            item.DanhGiaLaiMucDoUuTien();
            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// Chốt số liệu công nợ nhà cung cấp cuối kỳ (Kết kỳ).
    /// Tự động kết chuyển nợ cuối kỳ này sang làm nợ đầu kỳ tiếp theo.
    /// </summary>
    public async Task<int> KetKyCongNoNccAsync(uint kyId)
    {
        if (kyId == 0)
            throw new ArgumentException("Mã kỳ không hợp lệ.", nameof(kyId));

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await EnsureBangCongNoNccKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                "SELECT * FROM ky_ke_toan WHERE id = @Id FOR UPDATE;",
                new { Id = kyId },
                transaction);

            if (ky is null)
                throw new InvalidOperationException("Không tìm thấy kỳ kế toán.");

            if (ky.TrangThaiKhoaSo)
                throw new InvalidOperationException($"Kỳ \"{ky.TenKy}\" đã được kết kỳ trước đó.");

            var danhSachNcc = await TinhCongNoNccTheoKyAsync(connection, transaction, ky);

            var daLuuCu = new Dictionary<uint, NccCongNoSnapshotRow>();
            foreach (var row in await connection.QueryAsync<NccCongNoSnapshotRow>(
                """
                SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId,
                       no_dau_ky AS NoDauKy, phat_sinh_trong_ky AS PhatSinhTrongKy,
                       da_tra_trong_ky AS DaTraTrongKy, so_ky_no AS SoKyNo,
                       muc_do_uu_tien AS MucDoUuTien, ghi_chu AS GhiChu,
                       ky_nguon_id AS KyNguonId, la_chuyen_ky AS LaChuyenKy
                FROM cong_no_ncc_ky
                WHERE ky_ke_toan_id = @KyId;
                """,
                new { KyId = kyId }, transaction))
            {
                daLuuCu[row.NhaCungCapId] = row;
            }

            const string upsertSql = """
            INSERT INTO cong_no_ncc_ky
            (
                ky_ke_toan_id, nha_cung_cap_id, no_dau_ky, phat_sinh_trong_ky,
                da_tra_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu,
                ky_nguon_id, la_chuyen_ky
            )
            VALUES
            (
                @KyKeToanId, @NhaCungCapId, @NoDauKy, @PhatSinhTrongKy,
                @DaTraTrongKy, @NoCuoiKy, @SoKyNo, @MucDoUuTien, @GhiChu,
                @KyNguonId, @LaChuyenKy
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDauKy,
                phat_sinh_trong_ky = @PhatSinhTrongKy,
                da_tra_trong_ky = @DaTraTrongKy,
                no_cuoi_ky = @NoCuoiKy,
                so_ky_no = @SoKyNo,
                muc_do_uu_tien = @MucDoUuTien,
                ghi_chu = @GhiChu,
                ky_nguon_id = @KyNguonId,
                la_chuyen_ky = @LaChuyenKy,
                ngay_cap_nhat = NOW();
            """;

            foreach (var item in danhSachNcc)
            {
                await connection.ExecuteAsync(upsertSql, new
                {
                    KyKeToanId = kyId,
                    item.NhaCungCapId,
                    item.NoDauKy,
                    item.PhatSinhTrongKy,
                    item.DaTraTrongKy,
                    NoCuoiKy = item.NoCuoiKy,
                    item.SoKyNo,
                    item.MucDoUuTien,
                    GhiChu = daLuuCu.TryGetValue(item.NhaCungCapId, out var cu) && !string.IsNullOrWhiteSpace(cu.GhiChu)
                        ? cu.GhiChu
                        : item.GhiChu,
                    KyNguonId = daLuuCu.TryGetValue(item.NhaCungCapId, out var cuNguon) ? cuNguon.KyNguonId : null,
                    LaChuyenKy = daLuuCu.TryGetValue(item.NhaCungCapId, out var cuChuyen) && cuChuyen.LaChuyenKy
                }, transaction);
            }

            // Snapshot kết kỳ phải khớp tuyệt đối với danh sách vừa tính.
            // Các dòng còn sót từ lần kết kỳ trước (nếu kỳ từng bị mở lại) phải về 0
            // để không sinh nợ ảo khi đọc lại kỳ đã chốt.
            if (danhSachNcc.Count > 0)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE cong_no_ncc_ky
                    SET no_dau_ky = 0,
                        phat_sinh_trong_ky = 0,
                        da_tra_trong_ky = 0,
                        no_cuoi_ky = 0,
                        so_ky_no = 1,
                        ngay_cap_nhat = NOW()
                    WHERE ky_ke_toan_id = @KyId
                      AND nha_cung_cap_id NOT IN @NccIds;
                    """,
                    new { KyId = kyId, NccIds = danhSachNcc.Select(x => x.NhaCungCapId).ToList() },
                    transaction);
            }
            else
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE cong_no_ncc_ky
                    SET no_dau_ky = 0,
                        phat_sinh_trong_ky = 0,
                        da_tra_trong_ky = 0,
                        no_cuoi_ky = 0,
                        so_ky_no = 1,
                        ngay_cap_nhat = NOW()
                    WHERE ky_ke_toan_id = @KyId;
                    """,
                    new { KyId = kyId },
                    transaction);
            }

            await connection.ExecuteAsync(
                """
                UPDATE ky_ke_toan
                SET trang_thai_khoa_so = 1,
                    ngay_ket_ky = NOW(),
                    nguoi_ket_ky_id = @UserId
                WHERE id = @Id;
                """,
                new { Id = kyId, UserId = Session.CurrentUser?.Id },
                transaction);

            var nextKy = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                """
                SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                       ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                       trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
                FROM ky_ke_toan
                WHERE ngay_bat_dau_ky > @NgayBatDau
                ORDER BY ngay_bat_dau_ky ASC
                LIMIT 1
                FOR UPDATE;
                """,
                new { NgayBatDau = ky.NgayBatDauKy },
                transaction);

            if (nextKy is null)
            {
                var thangMoi = ky.NgayBatDauKy.AddMonths(1);
                var ngayDauThangMoi = new DateTime(thangMoi.Year, thangMoi.Month, 1);
                var ngayCuoiThangMoi = ngayDauThangMoi.AddMonths(1).AddDays(-1);
                var maKyMoi = $"KY_{thangMoi:yyyy_MM}";
                var tenKyMoi = $"Kỳ tháng {thangMoi:MM/yyyy}";

                var nextKyId = await connection.ExecuteScalarAsync<uint>(
                    """
                    INSERT INTO ky_ke_toan (ma_ky, ten_ky, ngay_bat_dau_ky, ngay_ket_thuc_ky, trang_thai_khoa_so, chi_nhanh_id)
                    VALUES (@MaKy, @TenKy, @NgayBatDau, @NgayKetThuc, 0, @ChiNhanhId);
                    SELECT LAST_INSERT_ID();
                    """,
                    new
                    {
                        MaKy = maKyMoi,
                        TenKy = tenKyMoi,
                        NgayBatDau = ngayDauThangMoi,
                        NgayKetThuc = ngayCuoiThangMoi,
                        ChiNhanhId = ky.ChiNhanhId > 0 ? ky.ChiNhanhId : 1
                    },
                    transaction);

                nextKy = new KyKeToan
                {
                    Id = nextKyId,
                    MaKy = maKyMoi,
                    TenKy = tenKyMoi,
                    NgayBatDauKy = ngayDauThangMoi,
                    NgayKetThucKy = ngayCuoiThangMoi,
                    TrangThaiKhoaSo = false,
                    ChiNhanhId = ky.ChiNhanhId > 0 ? ky.ChiNhanhId : 1
                };
            }

            if (nextKy.TrangThaiKhoaSo)
                throw new InvalidOperationException($"Kỳ tiếp theo \"{nextKy.TenKy}\" đã kết kỳ, không thể chuyển nợ vào.");

            var soNccChuyen = await ChuyenCongNoNccSangKyMoiAsync(
                connection, transaction, ky, nextKy.Id, danhSachNcc);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Kết kỳ công nợ NCC",
                "KY_KE_TOAN",
                kyId,
                ky.MaKy,
                $"Kết kỳ công nợ NCC {ky.TenKy}: Chốt nợ cho {danhSachNcc.Count} nhà cung cấp, kết chuyển {soNccChuyen} NCC còn nợ sang kỳ mới.",
                danhSachNcc.Sum(x => x.NoCuoiKy));

            return soNccChuyen;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi KetKyCongNoNccAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Mở lại kỳ công nợ đã khóa sổ. Chỉ cho phép khi kỳ sau chưa phát sinh
    /// giao dịch và chưa kết kỳ, sau đó xóa các dòng nợ đã kết chuyển.
    /// </summary>
    public async Task<bool> MoLaiKyCongNoNccAsync(uint kyId)
    {
        if (kyId == 0)
            throw new ArgumentException("Mã kỳ không hợp lệ.", nameof(kyId));

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await EnsureBangCongNoNccKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                "SELECT * FROM ky_ke_toan WHERE id = @Id FOR UPDATE;",
                new { Id = kyId },
                transaction);

            if (ky is null)
                throw new InvalidOperationException("Không tìm thấy kỳ kế toán.");

            if (!ky.TrangThaiKhoaSo)
                return true;

            var nextKy = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                """
                SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                       ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                       trang_thai_khoa_so AS TrangThaiKhoaSo
                FROM ky_ke_toan
                WHERE ngay_bat_dau_ky > @NgayBatDau
                ORDER BY ngay_bat_dau_ky ASC
                LIMIT 1
                FOR UPDATE;
                """,
                new { NgayBatDau = ky.NgayBatDauKy },
                transaction);

            if (nextKy is not null)
            {
                if (nextKy.TrangThaiKhoaSo)
                    throw new InvalidOperationException(
                        $"Kỳ sau \"{nextKy.TenKy}\" đã kết kỳ, không thể mở lại kỳ này.");

                var nextStart = nextKy.NgayBatDauKy.Date;
                var nextEnd = nextKy.NgayKetThucKy.Date.AddDays(1).AddSeconds(-1);

                var coDuLieuKhac = await connection.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*)
                    FROM cong_no_ncc_ky
                    WHERE ky_ke_toan_id = @NextKyId
                      AND (ky_nguon_id IS NULL OR ky_nguon_id <> @KyId)
                      AND (no_dau_ky <> 0 OR phat_sinh_trong_ky <> 0 OR da_tra_trong_ky <> 0);
                    """,
                    new { NextKyId = nextKy.Id, KyId = kyId },
                    transaction);

                if (coDuLieuKhac > 0)
                    throw new InvalidOperationException(
                        $"Kỳ sau \"{nextKy.TenKy}\" đã có dữ liệu công nợ, không thể mở lại kỳ này.");

                var coPhatSinh = await connection.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*)
                    FROM lich_su_cong_no_nha_cung_cap
                    WHERE thoi_gian >= @TuNgay AND thoi_gian <= @DenNgay
                      AND loai_giao_dich <> 'NO_DAU_KY'
                      AND nha_cung_cap_id IN
                      (
                          SELECT nha_cung_cap_id
                          FROM cong_no_ncc_ky
                          WHERE ky_ke_toan_id = @NextKyId
                            AND ky_nguon_id = @KyId
                            AND la_chuyen_ky = 1
                      );
                    """,
                    new { NextKyId = nextKy.Id, KyId = kyId, TuNgay = nextStart, DenNgay = nextEnd },
                    transaction);

                if (coPhatSinh > 0)
                    throw new InvalidOperationException(
                        $"Kỳ sau \"{nextKy.TenKy}\" đã phát sinh nhập hàng hoặc thanh toán, không thể mở lại kỳ này.");

                await connection.ExecuteAsync(
                    """
                    DELETE FROM cong_no_ncc_ky
                    WHERE ky_ke_toan_id = @NextKyId
                      AND ky_nguon_id = @KyId
                      AND la_chuyen_ky = 1;
                    """,
                    new { NextKyId = nextKy.Id, KyId = kyId },
                    transaction);
            }

            await connection.ExecuteAsync(
                """
                UPDATE ky_ke_toan
                SET trang_thai_khoa_so = 0,
                    ngay_ket_ky = NULL,
                    nguoi_ket_ky_id = NULL
                WHERE id = @KyId;
                """,
                new { KyId = kyId },
                transaction);

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi MoLaiKyCongNoNccAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Nạp lại công nợ cuối kỳ trước sang làm nợ đầu kỳ này cho nhà cung cấp.
    /// </summary>
    public async Task<int> LoadLaiNoKyTruocNccAsync(uint currentKyId, uint priorKyId)
    {
        if (currentKyId == 0 || priorKyId == 0 || currentKyId == priorKyId)
            throw new ArgumentException("Kỳ hiện tại hoặc kỳ trước không hợp lệ.");

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await EnsureBangCongNoNccKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var currentKy = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                "SELECT * FROM ky_ke_toan WHERE id = @Id FOR UPDATE;",
                new { Id = currentKyId },
                transaction);

            if (currentKy is null)
                throw new InvalidOperationException("Không tìm thấy kỳ hiện tại.");

            if (currentKy.TrangThaiKhoaSo)
                throw new InvalidOperationException("Kỳ hiện tại đã kết kỳ, không thể nạp lại công nợ.");

            var priorKy = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                "SELECT * FROM ky_ke_toan WHERE id = @Id FOR UPDATE;",
                new { Id = priorKyId },
                transaction);

            if (priorKy is null)
                throw new InvalidOperationException("Không tìm thấy kỳ trước.");

            if (priorKy.NgayBatDauKy >= currentKy.NgayBatDauKy)
                throw new InvalidOperationException("Kỳ trước phải bắt đầu trước kỳ hiện tại.");

            // Nạp lại là thay thế toàn bộ nợ đã kết chuyển trước đó, tránh
            // giữ lại dòng chuyển từ một kỳ nguồn khác.
            await connection.ExecuteAsync(
                """
                DELETE FROM cong_no_ncc_ky
                WHERE ky_ke_toan_id = @KyId AND la_chuyen_ky = 1;
                """,
                new { KyId = currentKyId },
                transaction);

            var nguon = await LayNguonChuyenKyAsync(connection, transaction, priorKy);
            var soNcc = await ChuyenCongNoNccSangKyMoiAsync(
                connection, transaction, priorKy, currentKy.Id, nguon);

            await transaction.CommitAsync();
            return soNcc;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi LoadLaiNoKyTruocNccAsync", ex);
            throw;
        }
    }

    private async Task<List<NhaCungCapKyCongNoItem>> LayNguonChuyenKyAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        KyKeToan kyNguon)
    {
        // Chỉ snapshot của kỳ đã khóa sổ mới là số liệu chốt. Kỳ đang mở có thể
        // có dòng snapshot (nợ đầu kỳ/thanh toán) nhưng phát sinh thực tế phải
        // được tính lại từ sổ cái.
        if (kyNguon.TrangThaiKhoaSo)
            return await DocCongNoNccDaChotAsync(connection, transaction, kyNguon);

        return await TinhCongNoNccTheoKyAsync(connection, transaction, kyNguon);
    }

    private async Task<int> ChuyenCongNoNccSangKyMoiAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        KyKeToan kyNguon,
        uint kyDichId,
        IReadOnlyList<NhaCungCapKyCongNoItem> nguon)
    {
        var nccConNo = nguon
            .Where(x => x.NoCuoiKy > 0)
            .GroupBy(x => x.NhaCungCapId)
            .Select(g => g.OrderByDescending(x => x.SoKyNo).First())
            .ToList();

        var dichRows = new Dictionary<uint, NccCongNoSnapshotRow>();
        foreach (var row in await connection.QueryAsync<NccCongNoSnapshotRow>(
            """
            SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId,
                   no_dau_ky AS NoDauKy, phat_sinh_trong_ky AS PhatSinhTrongKy,
                   da_tra_trong_ky AS DaTraTrongKy, so_ky_no AS SoKyNo,
                   muc_do_uu_tien AS MucDoUuTien, ghi_chu AS GhiChu,
                   ky_nguon_id AS KyNguonId, la_chuyen_ky AS LaChuyenKy
            FROM cong_no_ncc_ky
            WHERE ky_ke_toan_id = @KyId;
            """,
            new { KyId = kyDichId }, transaction))
        {
            dichRows[row.NhaCungCapId] = row;
        }

        const string insertSql = """
        INSERT INTO cong_no_ncc_ky
        (
            ky_ke_toan_id, nha_cung_cap_id, no_dau_ky, phat_sinh_trong_ky,
            da_tra_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu,
            ky_nguon_id, la_chuyen_ky
        )
        VALUES
        (
            @KyId, @NhaCungCapId, @NoDauKy, 0, 0, @NoDauKy, @SoKyNo,
            @MucDoUuTien, @GhiChu, @KyNguonId, 1
        )
        ON DUPLICATE KEY UPDATE
            no_dau_ky = @NoDauKy,
            no_cuoi_ky = GREATEST(0, @NoDauKy + phat_sinh_trong_ky - da_tra_trong_ky),
            so_ky_no = @SoKyNo,
            muc_do_uu_tien = @MucDoUuTien,
            ky_nguon_id = @KyNguonId,
            la_chuyen_ky = 1,
            ghi_chu = CASE
                WHEN ghi_chu IS NULL OR ghi_chu = '' THEN @GhiChu
                ELSE ghi_chu
            END,
            ngay_cap_nhat = NOW();
        """;

        foreach (var item in nccConNo)
        {
            var soKy = Math.Max(2, item.SoKyNo + 1);

            if (dichRows.TryGetValue(item.NhaCungCapId, out var dich))
                soKy = Math.Max(soKy, Math.Max(2, dich.SoKyNo));

            await connection.ExecuteAsync(insertSql, new
            {
                KyId = kyDichId,
                item.NhaCungCapId,
                NoDauKy = item.NoCuoiKy,
                SoKyNo = soKy,
                MucDoUuTien = TinhMucDoUuTien(soKy, item.NoCuoiKy),
                GhiChu = $"Kế thừa nợ từ {kyNguon.TenKy}",
                KyNguonId = kyNguon.Id
            }, transaction);
        }

        var nccConNoIds = nccConNo.Select(x => x.NhaCungCapId).ToList();

        if (nccConNoIds.Count > 0)
        {
            await connection.ExecuteAsync(
                """
                UPDATE cong_no_ncc_ky
                SET no_dau_ky = 0,
                    no_cuoi_ky = GREATEST(0, phat_sinh_trong_ky - da_tra_trong_ky),
                    so_ky_no = 1,
                    ngay_cap_nhat = NOW()
                WHERE ky_ke_toan_id = @KyId
                  AND ky_nguon_id = @KyNguonId
                  AND la_chuyen_ky = 1
                  AND nha_cung_cap_id NOT IN @NccIds;
                """,
                new { KyId = kyDichId, KyNguonId = kyNguon.Id, NccIds = nccConNoIds },
                transaction);

            await connection.ExecuteAsync(
                """
                DELETE FROM cong_no_ncc_ky
                WHERE ky_ke_toan_id = @KyId
                  AND ky_nguon_id = @KyNguonId
                  AND la_chuyen_ky = 1
                  AND no_dau_ky = 0
                  AND phat_sinh_trong_ky = 0
                  AND da_tra_trong_ky = 0
                  AND nha_cung_cap_id NOT IN @NccIds;
                """,
                new { KyId = kyDichId, KyNguonId = kyNguon.Id, NccIds = nccConNoIds },
                transaction);
        }
        else
        {
            await connection.ExecuteAsync(
                """
                UPDATE cong_no_ncc_ky
                SET no_dau_ky = 0,
                    no_cuoi_ky = GREATEST(0, phat_sinh_trong_ky - da_tra_trong_ky),
                    so_ky_no = 1,
                    ngay_cap_nhat = NOW()
                WHERE ky_ke_toan_id = @KyId
                  AND ky_nguon_id = @KyNguonId
                  AND la_chuyen_ky = 1;
                """,
                new { KyId = kyDichId, KyNguonId = kyNguon.Id },
                transaction);

            await connection.ExecuteAsync(
                """
                DELETE FROM cong_no_ncc_ky
                WHERE ky_ke_toan_id = @KyId
                  AND ky_nguon_id = @KyNguonId
                  AND la_chuyen_ky = 1
                  AND no_dau_ky = 0
                  AND phat_sinh_trong_ky = 0
                  AND da_tra_trong_ky = 0;
                """,
                new { KyId = kyDichId, KyNguonId = kyNguon.Id },
                transaction);
        }

        return nccConNo.Count;
    }

    private static string TinhMucDoUuTien(int soKyNo, decimal noCuoiKy)
    {
        if (soKyNo >= 3 || noCuoiKy >= 10_000_000m) return "CAO";
        if (soKyNo == 2 || noCuoiKy >= 5_000_000m) return "TRUNG_BINH";
        return "THAP";
    }

    /// <summary>
    /// Cập nhật nợ đầu kỳ của NCC vào snapshot kỳ đang mở để màn hình công nợ
    /// và bước kết kỳ sử dụng thống nhất một nguồn số liệu.
    /// </summary>
    private static async Task CapNhatNoDauKyNccVaoSnapshotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint nhaCungCapId,
        DateTime ngayDauKy,
        decimal soTien)
    {
        var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
            """
            SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                   ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                   trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
            FROM ky_ke_toan
            WHERE ngay_bat_dau_ky <= @Ngay
              AND ngay_ket_thuc_ky >= @Ngay
              AND trang_thai_khoa_so = 0
            ORDER BY ngay_bat_dau_ky DESC
            LIMIT 1
            FOR UPDATE;
            """,
            new { Ngay = ngayDauKy.Date },
            transaction);

        if (ky is null)
            return;

        var hienTai = await connection.QueryFirstOrDefaultAsync<NccCongNoSnapshotRow>(
            """
            SELECT id AS Id, nha_cung_cap_id AS NhaCungCapId,
                   no_dau_ky AS NoDauKy, phat_sinh_trong_ky AS PhatSinhTrongKy,
                   da_tra_trong_ky AS DaTraTrongKy, so_ky_no AS SoKyNo,
                   muc_do_uu_tien AS MucDoUuTien, ghi_chu AS GhiChu,
                   ky_nguon_id AS KyNguonId, la_chuyen_ky AS LaChuyenKy
            FROM cong_no_ncc_ky
            WHERE ky_ke_toan_id = @KyId AND nha_cung_cap_id = @NccId;
            """,
            new { KyId = ky.Id, NccId = nhaCungCapId },
            transaction);

        var soKy = hienTai is not null ? Math.Max(1, hienTai.SoKyNo) : 1;
        var noDau = Math.Max(0m, soTien);
        var noCuoi = Math.Max(0m, noDau + (hienTai?.PhatSinhTrongKy ?? 0m) - (hienTai?.DaTraTrongKy ?? 0m));

        await connection.ExecuteAsync(
            """
            INSERT INTO cong_no_ncc_ky
            (
                ky_ke_toan_id, nha_cung_cap_id, no_dau_ky, phat_sinh_trong_ky,
                da_tra_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu,
                ky_nguon_id, la_chuyen_ky
            )
            VALUES
            (
                @KyId, @NccId, @NoDau, 0, 0, @NoDau, @SoKy, @MucDo, @GhiChu,
                @KyNguonId, @LaChuyenKy
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDau,
                no_cuoi_ky = @NoCuoi,
                so_ky_no = @SoKy,
                muc_do_uu_tien = @MucDo,
                ngay_cap_nhat = NOW();
            """,
            new
            {
                KyId = ky.Id,
                NccId = nhaCungCapId,
                NoDau = noDau,
                NoCuoi = noCuoi,
                SoKy = soKy,
                MucDo = TinhMucDoUuTien(soKy, noCuoi),
                GhiChu = hienTai?.GhiChu,
                KyNguonId = hienTai?.KyNguonId,
                LaChuyenKy = hienTai?.LaChuyenKy ?? false
            },
            transaction);
    }

    /// <summary>
    /// Cập nhật ghi chú nhắc nợ hoặc thay đổi mức độ ưu tiên cho một nhà cung cấp trong kỳ.
    /// </summary>
    public async Task<bool> LuuGhiChuNhacNoNccAsync(uint kyId, uint nccId, string? ghiChu, string? mucDoUuTien)
    {
        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await EnsureBangCongNoNccKyAsync(connection);

        const string sql = """
        UPDATE cong_no_ncc_ky
        SET ghi_chu = @GhiChu,
            muc_do_uu_tien = COALESCE(@MucDoUuTien, muc_do_uu_tien),
            ngay_cap_nhat = NOW()
        WHERE ky_ke_toan_id = @KyId AND nha_cung_cap_id = @NccId;
        """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            KyId = kyId,
            NccId = nccId,
            GhiChu = ghiChu,
            MucDoUuTien = mucDoUuTien
        });

        return affected > 0;
    }

    /// <summary>
    /// Thanh toán công nợ cho nhà cung cấp:
    /// - Phân bổ trừ dần vào các phiếu nhập hàng còn nợ (cũ nhất trước).
    /// - Ghi vào sổ cái lịch sử công nợ nhà cung cấp (ghi_co).
    /// - Ghi phiếu chi trong sổ quỹ.
    /// </summary>
    public async Task<(decimal ConNoConLai, string? MaPhieuChi)> TraNoNhaCungCapAsync(
        uint nhaCungCapId,
        decimal soTien,
        string phuongThuc = "TIEN_MAT",
        string? ghiChu = null,
        uint kyId = 0,
        DateTime? thoiGianThanhToan = null)
    {
        if (nhaCungCapId == 0)
            throw new ArgumentException("Mã nhà cung cấp không hợp lệ.", nameof(nhaCungCapId));

        if (soTien <= 0)
            throw new ArgumentException("Số tiền thanh toán phải lớn hơn 0.", nameof(soTien));

        var coCotNguoiNopNhanGhiChu = await EnsurePhieuQuyExtraColumnsAsync();
        var coCotPhieuNhap = await EnsurePhieuQuyPhieuNhapColumnsAsync();

        await using var connection = await OpenAsync();
        await EnsureBangKyKhoAsync(connection);
        await EnsureBangCongNoNccKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var ncc = await connection.QueryFirstOrDefaultAsync<NccThongTinRow>(
                """
                SELECT id AS Id, ma_nha_cung_cap AS MaNhaCungCap, ten_nha_cung_cap AS TenNhaCungCap,
                       chi_nhanh_id AS ChiNhanhId
                FROM nha_cung_cap
                WHERE id = @Id
                LIMIT 1
                FOR UPDATE;
                """,
                new { Id = nhaCungCapId },
                transaction);

            if (ncc is null)
                throw new InvalidOperationException("Không tìm thấy nhà cung cấp.");

            var tenNcc = ncc.TenNhaCungCap ?? "";
            var branchId = ncc.ChiNhanhId is > 0 ? ncc.ChiNhanhId.Value : (Session.CurrentUser?.ChiNhanhId ?? 1);
            var userId = Session.CurrentUser?.Id;
            var phuongThucChuan = ChuanHoaPhuongThuc(phuongThuc);

            KyKeToan? ky = null;
            if (kyId > 0)
            {
                ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                    """
                    SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                           ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                           trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
                    FROM ky_ke_toan
                    WHERE id = @KyId
                    LIMIT 1
                    FOR UPDATE;
                    """,
                    new { KyId = kyId },
                    transaction);

                if (ky is null)
                    throw new InvalidOperationException("Không tìm thấy kỳ kế toán cần thanh toán.");

                if (ky.TrangThaiKhoaSo)
                    throw new InvalidOperationException($"Kỳ \"{ky.TenKy}\" đã kết kỳ, không thể thanh toán.");
            }
            else
            {
                var now = thoiGianThanhToan ?? DateTime.Now;
                ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(
                    """
                    SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy,
                           ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
                           trang_thai_khoa_so AS TrangThaiKhoaSo, chi_nhanh_id AS ChiNhanhId
                    FROM ky_ke_toan
                    WHERE ngay_bat_dau_ky <= @Ngay AND ngay_ket_thuc_ky >= @Ngay
                      AND trang_thai_khoa_so = 0
                    ORDER BY ngay_bat_dau_ky DESC
                    LIMIT 1
                    FOR UPDATE;
                    """,
                    new { Ngay = now.Date },
                    transaction);
            }

            if (ky is null)
                throw new InvalidOperationException("Không có kỳ đang mở để thanh toán công nợ nhà cung cấp.");

            var ngayThanhToan = thoiGianThanhToan ?? DateTime.Now;
            decimal conNoTrongKy;
            NhaCungCapKyCongNoItem? dongCongNo = null;

            if (ky is not null)
            {
                var dsKy = await GetCongNoNccTheoKyCoreAsync(connection, transaction, ky.Id);
                dongCongNo = dsKy.FirstOrDefault(x => x.NhaCungCapId == nhaCungCapId);
                conNoTrongKy = dongCongNo?.NoCuoiKy ?? 0m;

                if (conNoTrongKy <= 0)
                    throw new InvalidOperationException(
                        $"Nhà cung cấp \"{tenNcc}\" không còn nợ trong kỳ \"{ky.TenKy}\".");

                if (soTien > conNoTrongKy)
                    throw new InvalidOperationException(
                        $"Số tiền thanh toán ({soTien:N0} đ) vượt quá công nợ còn lại trong kỳ ({conNoTrongKy:N0} đ).");

                var batDau = ky.NgayBatDauKy.Date;
                var ketThuc = ky.NgayKetThucKy.Date.AddDays(1).AddSeconds(-1);
                if (ngayThanhToan < batDau) ngayThanhToan = batDau;
                if (ngayThanhToan > ketThuc) ngayThanhToan = ketThuc;
            }
            else
            {
                var baseline = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(no_can_tra_hien_tai, 0) FROM nha_cung_cap WHERE id = @Id;",
                    new { Id = nhaCungCapId },
                    transaction);
                var bienDong = await connection.ExecuteScalarAsync<decimal>(
                    """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_nha_cung_cap
                    WHERE nha_cung_cap_id = @Id
                    FOR UPDATE;
                    """,
                    new { Id = nhaCungCapId },
                    transaction);

                conNoTrongKy = Math.Max(0m, baseline + bienDong);
                if (conNoTrongKy <= 0)
                    throw new InvalidOperationException($"Nhà cung cấp \"{tenNcc}\" không còn nợ.");

                if (soTien > conNoTrongKy)
                    throw new InvalidOperationException(
                        $"Số tiền thanh toán ({soTien:N0} đ) vượt quá công nợ còn lại ({conNoTrongKy:N0} đ).");
            }

            var phieuList = (await connection.QueryAsync<PhieuNhapCongNoRow>(
                """
                SELECT id AS Id, chi_nhanh_id AS ChiNhanhId, ma_phieu_nhap AS MaPhieuNhap,
                       tong_tien_nhap AS TongTienNhap, da_thanh_toan AS DaThanhToan,
                       con_no AS ConNo, trang_thai AS TrangThai
                FROM phieu_nhap_hang
                WHERE nha_cung_cap_id = @NccId
                  AND con_no > 0
                  AND thoi_gian_nhap <= @NgayThanhToan
                  AND trang_thai NOT IN ('LUU_TAM', 'HUY', 'DA_HUY')
                ORDER BY thoi_gian_nhap ASC, id ASC
                FOR UPDATE;
                """,
                new { NccId = nhaCungCapId, NgayThanhToan = ngayThanhToan },
                transaction)).AsList();

            var nhomChiId = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");
            var conLai = soTien;
            var danhSachMaPhieuChi = new List<string>();

            foreach (var phieu in phieuList)
            {
                if (conLai <= 0) break;
                if (phieu.ConNo <= 0) continue;

                var traLanNay = Math.Min(conLai, phieu.ConNo);
                var daTraMoi = phieu.DaThanhToan + traLanNay;
                var conNoMoi = Math.Max(0m, phieu.TongTienNhap - daTraMoi);

                await connection.ExecuteAsync(
                    """
                    UPDATE phieu_nhap_hang
                    SET da_thanh_toan = @DaThanhToan,
                        con_no = @ConNo,
                        ngay_cap_nhat = @ThoiGian
                    WHERE id = @Id;
                    """,
                    new { DaThanhToan = daTraMoi, ConNo = conNoMoi, ThoiGian = ngayThanhToan, Id = phieu.Id },
                    transaction);

                var maThanhToan = await SinhMaThanhToanNhaCungCapTrongGiaoDichAsync(connection, transaction);
                await connection.ExecuteAsync(
                    """
                    INSERT INTO thanh_toan_nha_cung_cap
                    (ma_thanh_toan, nha_cung_cap_id, phieu_nhap_id, so_tien, phuong_thuc, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@MaThanhToan, @NhaCungCapId, @PhieuNhapId, @SoTien, @PhuongThuc, @ThoiGian, @NguoiThucHienId, @GhiChu);
                    """,
                    new
                    {
                        MaThanhToan = maThanhToan,
                        NhaCungCapId = nhaCungCapId,
                        PhieuNhapId = phieu.Id,
                        SoTien = traLanNay,
                        PhuongThuc = phuongThucChuan,
                        ThoiGian = ngayThanhToan,
                        NguoiThucHienId = userId,
                        GhiChu = BlankToNull(ghiChu)
                    },
                    transaction);

                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    nhaCungCapId,
                    phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : branchId,
                    "THANH_TOAN_NHAP",
                    phieu.MaPhieuNhap,
                    0,
                    traLanNay,
                    ngayThanhToan,
                    userId,
                    string.IsNullOrWhiteSpace(ghiChu)
                        ? $"Thanh toán {traLanNay:N0} đ cho phiếu {phieu.MaPhieuNhap}"
                        : $"Thanh toán {traLanNay:N0} đ cho phiếu {phieu.MaPhieuNhap}. {ghiChu.Trim()}");

                var maPhieuChi = await TaoPhieuChiThanhToanNccAsync(
                    connection,
                    transaction,
                    phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : branchId,
                    nhomChiId,
                    tenNcc,
                    traLanNay,
                    phuongThucChuan,
                    ngayThanhToan,
                    userId,
                    $"Thanh toán nợ nhà cung cấp {tenNcc} - phiếu {phieu.MaPhieuNhap}",
                    $"Chi trả công nợ cho nhà cung cấp {tenNcc} theo phiếu nhập {phieu.MaPhieuNhap}.",
                    coCotNguoiNopNhanGhiChu,
                    coCotPhieuNhap,
                    phieu.Id,
                    phieu.MaPhieuNhap);

                danhSachMaPhieuChi.Add(maPhieuChi);
                conLai -= traLanNay;
            }

            if (conLai > 0)
            {
                var maThanhToan = await SinhMaThanhToanNhaCungCapTrongGiaoDichAsync(connection, transaction);
                await connection.ExecuteAsync(
                    """
                    INSERT INTO thanh_toan_nha_cung_cap
                    (ma_thanh_toan, nha_cung_cap_id, phieu_nhap_id, so_tien, phuong_thuc, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                    VALUES
                    (@MaThanhToan, @NhaCungCapId, NULL, @SoTien, @PhuongThuc, @ThoiGian, @NguoiThucHienId, @GhiChu);
                    """,
                    new
                    {
                        MaThanhToan = maThanhToan,
                        NhaCungCapId = nhaCungCapId,
                        SoTien = conLai,
                        PhuongThuc = phuongThucChuan,
                        ThoiGian = ngayThanhToan,
                        NguoiThucHienId = userId,
                        GhiChu = BlankToNull(ghiChu)
                    },
                    transaction);

                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    nhaCungCapId,
                    branchId,
                    "THANH_TOAN_NCC",
                    maThanhToan,
                    0,
                    conLai,
                    ngayThanhToan,
                    userId,
                    string.IsNullOrWhiteSpace(ghiChu)
                        ? $"Thanh toán công nợ NCC {tenNcc}"
                        : $"Thanh toán công nợ NCC {tenNcc}. {ghiChu.Trim()}");

                var maPhieuChi = await TaoPhieuChiThanhToanNccAsync(
                    connection,
                    transaction,
                    branchId,
                    nhomChiId,
                    tenNcc,
                    conLai,
                    phuongThucChuan,
                    ngayThanhToan,
                    userId,
                    $"Thanh toán nợ nhà cung cấp {tenNcc}",
                    string.IsNullOrWhiteSpace(ghiChu)
                        ? $"Thanh toán công nợ NCC {tenNcc} ({conLai:N0} đ)"
                        : ghiChu.Trim(),
                    coCotNguoiNopNhanGhiChu,
                    coCotPhieuNhap,
                    null,
                    null);

                danhSachMaPhieuChi.Add(maPhieuChi);
                conLai = 0m;
            }

            if (ky is not null && dongCongNo is not null)
            {
                var daTraMoi = dongCongNo.DaTraTrongKy + soTien;
                var noCuoiMoi = Math.Max(0m, dongCongNo.NoDauKy + dongCongNo.PhatSinhTrongKy - daTraMoi);

                await connection.ExecuteAsync(
                    """
                    INSERT INTO cong_no_ncc_ky
                    (
                        ky_ke_toan_id, nha_cung_cap_id, no_dau_ky, phat_sinh_trong_ky,
                        da_tra_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu
                    )
                    VALUES
                    (
                        @KyId, @NccId, @NoDauKy, @PhatSinhTrongKy,
                        @DaTraTrongKy, @NoCuoiKy, @SoKyNo, @MucDo, @GhiChu
                    )
                    ON DUPLICATE KEY UPDATE
                        no_dau_ky = @NoDauKy,
                        phat_sinh_trong_ky = @PhatSinhTrongKy,
                        da_tra_trong_ky = @DaTraTrongKy,
                        no_cuoi_ky = @NoCuoiKy,
                        so_ky_no = @SoKyNo,
                        muc_do_uu_tien = @MucDo,
                        ngay_cap_nhat = NOW();
                    """,
                    new
                    {
                        KyId = ky.Id,
                        NccId = nhaCungCapId,
                        dongCongNo.NoDauKy,
                        dongCongNo.PhatSinhTrongKy,
                        DaTraTrongKy = daTraMoi,
                        NoCuoiKy = noCuoiMoi,
                        dongCongNo.SoKyNo,
                        MucDo = TinhMucDoUuTien(dongCongNo.SoKyNo, noCuoiMoi),
                        dongCongNo.GhiChu
                    },
                    transaction);
            }

            decimal conNoConLai;
            if (ky is not null && dongCongNo is not null)
            {
                conNoConLai = Math.Max(0m, dongCongNo.NoCuoiKy - soTien);
            }
            else
            {
                var baseline = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(no_can_tra_hien_tai, 0) FROM nha_cung_cap WHERE id = @Id;",
                    new { Id = nhaCungCapId },
                    transaction);
                var bienDong = await connection.ExecuteScalarAsync<decimal>(
                    """
                    SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
                    FROM lich_su_cong_no_nha_cung_cap
                    WHERE nha_cung_cap_id = @Id;
                    """,
                    new { Id = nhaCungCapId },
                    transaction);
                conNoConLai = Math.Max(0m, baseline + bienDong);
            }

            var maPhieuChiKetQua = danhSachMaPhieuChi.Count > 0
                ? string.Join(", ", danhSachMaPhieuChi)
                : null;

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Thanh toán công nợ NCC",
                "NHA_CUNG_CAP",
                nhaCungCapId,
                ncc.MaNhaCungCap ?? "",
                $"Thanh toán nợ cho nhà cung cấp {tenNcc}: {soTien:N0} đ qua phiếu chi {maPhieuChiKetQua}",
                soTien);

            return (conNoConLai, maPhieuChiKetQua);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi TraNoNhaCungCapAsync", ex);
            throw;
        }
    }

    private async Task<string> TaoPhieuChiThanhToanNccAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint chiNhanhId,
        uint? nhomChiId,
        string tenNcc,
        decimal soTien,
        string phuongThuc,
        DateTime thoiGian,
        uint? userId,
        string noiDung,
        string ghiChu,
        bool coCotExtra,
        bool coCotPhieuNhap,
        uint? phieuNhapId,
        string? maPhieuNhap)
    {
        var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);

        string sql;
        if (coCotPhieuNhap && coCotExtra)
        {
            sql = """
            INSERT INTO phieu_chi
            (
                ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                nguoi_thuc_hien_id, trang_thai, phieu_nhap_hang_id, ma_phieu_nhap
            )
            VALUES
            (
                @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                @NguoiTaoId, @TrangThai, @PhieuNhapId, @MaPhieuNhap
            );
            """;
        }
        else if (coCotExtra)
        {
            sql = """
            INSERT INTO phieu_chi
            (
                ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                nguoi_thuc_hien_id, trang_thai
            )
            VALUES
            (
                @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                @NguoiTaoId, @TrangThai
            );
            """;
        }
        else
        {
            sql = """
            INSERT INTO phieu_chi
            (
                ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai
            )
            VALUES
            (
                @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                @NoiDung, @ThoiGian, @NguoiTaoId, @TrangThai
            );
            """;
        }

        await connection.ExecuteAsync(sql, new
        {
            MaPhieu = maPhieuChi,
            ChiNhanhId = chiNhanhId,
            NhomChiPhiId = nhomChiId,
            SoTien = soTien,
            PhuongThuc = phuongThuc,
            NoiDung = noiDung,
            NguoiNopNhan = Truncate150(tenNcc),
            GhiChu = ghiChu,
            ThoiGian = thoiGian,
            NguoiTaoId = userId,
            TrangThai = TrangThaiPhieuHoanThanh,
            PhieuNhapId = phieuNhapId,
            MaPhieuNhap = maPhieuNhap
        }, transaction);

        return maPhieuChi;
    }

    private sealed class NccCongNoSnapshotRow
    {
        public uint Id { get; set; }
        public uint NhaCungCapId { get; set; }
        public decimal NoDauKy { get; set; }
        public decimal PhatSinhTrongKy { get; set; }
        public decimal DaTraTrongKy { get; set; }
        public int SoKyNo { get; set; }
        public string? MucDoUuTien { get; set; }
        public string? GhiChu { get; set; }
        public uint? KyNguonId { get; set; }
        public bool LaChuyenKy { get; set; }
    }

    private sealed class NccDauKyRow
    {
        public uint DoiTacId { get; set; }
        public decimal SoTien { get; set; }
        public DateTime NgayDauKy { get; set; }
    }

    private sealed class NccPhatSinhAggregateRow
    {
        public uint NhaCungCapId { get; set; }
        public decimal TongPhatSinh { get; set; }
        public decimal TongDaTra { get; set; }
    }

    private sealed class NccThongTinRow
    {
        public uint Id { get; set; }
        public string? MaNhaCungCap { get; set; }
        public string? TenNhaCungCap { get; set; }
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }
        public string? DiaChi { get; set; }
        public string? NguoiLienHe { get; set; }
        public string? NhomNhaCungCap { get; set; }
        public decimal NoExcel { get; set; }
        public uint? ChiNhanhId { get; set; }
    }
}
