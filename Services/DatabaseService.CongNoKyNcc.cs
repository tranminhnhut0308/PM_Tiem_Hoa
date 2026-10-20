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
                ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_ky_ncc (ky_ke_toan_id, nha_cung_cap_id),
                INDEX idx_cnk_ncc_ky (ky_ke_toan_id),
                INDEX idx_cnk_ncc_ncc (nha_cung_cap_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(createSql, transaction: transaction);
            _daDamBaoBangCongNoNccKy = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không tạo được bảng cong_no_ncc_ky", ex);
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
        await EnsureBangCongNoNccKyAsync(connection);

        // 1. Lấy thông tin kỳ đang chọn
        const string sqlKy = """
        SELECT id AS Id, ma_ky AS MaKy, ten_ky AS TenKy, 
               ngay_bat_dau_ky AS NgayBatDauKy, ngay_ket_thuc_ky AS NgayKetThucKy,
               trang_thai_khoa_so AS TrangThaiKhoaSo, ngay_ket_ky AS NgayKetKy
        FROM ky_ke_toan
        WHERE id = @KyId;
        """;
        var ky = await connection.QueryFirstOrDefaultAsync<KyKeToan>(sqlKy, new { KyId = kyId });
        if (ky is null)
            return Array.Empty<NhaCungCapKyCongNoItem>();

        var list = new List<NhaCungCapKyCongNoItem>();

        // 2. Nếu kỳ đã khóa sổ (đã kết kỳ): đọc trực tiếp từ bảng cong_no_ncc_ky
        if (ky.TrangThaiKhoaSo)
        {
            const string sqlLocked = """
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
                cnk.no_cuoi_ky AS NoCuoiKy,
                cnk.so_ky_no AS SoKyNo,
                cnk.muc_do_uu_tien AS MucDoUuTien,
                cnk.ghi_chu AS GhiChu
            FROM cong_no_ncc_ky cnk
            INNER JOIN nha_cung_cap ncc ON ncc.id = cnk.nha_cung_cap_id
            LEFT JOIN nhom_nha_cung_cap nncc ON nncc.id = ncc.nhom_nha_cung_cap_id
            WHERE cnk.ky_ke_toan_id = @KyId
              AND ncc.trang_thai = 1
            ORDER BY 
                CASE cnk.muc_do_uu_tien WHEN 'CAO' THEN 1 WHEN 'TRUNG_BINH' THEN 2 ELSE 3 END,
                cnk.so_ky_no DESC,
                cnk.no_cuoi_ky DESC;
            """;

            var rows = (await connection.QueryAsync<NhaCungCapKyCongNoItem>(sqlLocked, new { KyId = kyId })).AsList();
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
                sqlKyTruoc, new { KyId = kyId, NgayBatDau = ky.NgayBatDauKy });

            // Đọc các bản ghi đã lưu sẵn trong kỳ này (nếu đã từng lưu hoặc điều chỉnh)
            var daLuuDict = new Dictionary<uint, NhaCungCapKyCongNoItem>();
            try
            {
                var daLuu = await connection.QueryAsync<NhaCungCapKyCongNoItem>(
                    "SELECT * FROM cong_no_ncc_ky WHERE ky_ke_toan_id = @KyId;",
                    new { KyId = kyId });
                foreach (var dl in daLuu)
                    daLuuDict[dl.NhaCungCapId] = dl;
            }
            catch { }

            // Đọc nợ cuối kỳ trước nếu có
            var noCuoiKyTruocDict = new Dictionary<uint, (decimal NoCuoi, int SoKyNo)>();
            if (kyTruoc != null)
            {
                var rowsKyTruoc = await connection.QueryAsync<(uint NhaCungCapId, decimal NoCuoiKy, int SoKyNo)>(
                    "SELECT nha_cung_cap_id, no_cuoi_ky, so_ky_no FROM cong_no_ncc_ky WHERE ky_ke_toan_id = @KyTruocId;",
                    new { KyTruocId = kyTruoc.Id });
                foreach (var r in rowsKyTruoc)
                    noCuoiKyTruocDict[r.NhaCungCapId] = (r.NoCuoiKy, r.SoKyNo);
            }

            // Đọc công nợ ban đầu (chi_tiet_cong_no_dau_ky loại NHA_CUNG_CAP)
            var noBanDauDict = new Dictionary<uint, decimal>();
            try
            {
                var rowsDauKy = await connection.QueryAsync<(uint DoiTacId, decimal SoTien)>(
                    "SELECT doi_tac_id, so_tien FROM chi_tiet_cong_no_dau_ky WHERE loai_doi_tac = 'NHA_CUNG_CAP';");
                foreach (var r in rowsDauKy)
                    noBanDauDict[r.DoiTacId] = r.SoTien;
            }
            catch { }

            // Đọc phát sinh tăng nợ (nhập hàng nợ) và đã trả trong khoảng thời gian của kỳ
            var tuNgay = ky.NgayBatDauKy.Date;
            var denNgay = ky.NgayKetThucKy.Date.AddDays(1).AddTicks(-1);

            var phatSinhDict = new Dictionary<uint, decimal>();
            var daTraDict = new Dictionary<uint, decimal>();

            // Lấy từ sổ cái lịch sử công nợ NCC
            try
            {
                const string sqlLichSu = """
                SELECT nha_cung_cap_id AS NhaCungCapId,
                       SUM(CASE WHEN ghi_no > 0 AND loai_giao_dich <> 'NO_DAU_KY' THEN ghi_no ELSE 0 END) AS TongPhatSinh,
                       SUM(ghi_co) AS TongDaTra
                FROM lich_su_cong_no_nha_cung_cap
                WHERE thoi_gian >= @TuNgay AND thoi_gian <= @DenNgay
                GROUP BY nha_cung_cap_id;
                """;
                var lsRows = await connection.QueryAsync<(uint NhaCungCapId, decimal TongPhatSinh, decimal TongDaTra)>(
                    sqlLichSu, new { TuNgay = tuNgay, DenNgay = denNgay });
                foreach (var r in lsRows)
                {
                    phatSinhDict[r.NhaCungCapId] = r.TongPhatSinh;
                    daTraDict[r.NhaCungCapId] = r.TongDaTra;
                }
            }
            catch { }

            // Đối chiếu thêm từ phieu_nhap_hang nếu sổ cái chưa có
            try
            {
                const string sqlPhieuNhap = """
                SELECT nha_cung_cap_id AS NhaCungCapId,
                       SUM(tong_tien_nhap) AS TongPhatSinh,
                       SUM(da_thanh_toan) AS TongDaTra
                FROM phieu_nhap_hang
                WHERE thoi_gian_nhap >= @TuNgay AND thoi_gian_nhap <= @DenNgay
                  AND trang_thai NOT IN ('LUU_TAM', 'HUY')
                  AND nha_cung_cap_id IS NOT NULL
                GROUP BY nha_cung_cap_id;
                """;
                var pnRows = await connection.QueryAsync<(uint NhaCungCapId, decimal TongPhatSinh, decimal TongDaTra)>(
                    sqlPhieuNhap, new { TuNgay = tuNgay, DenNgay = denNgay });
                foreach (var r in pnRows)
                {
                    if (!phatSinhDict.ContainsKey(r.NhaCungCapId) || phatSinhDict[r.NhaCungCapId] < r.TongPhatSinh)
                    {
                        phatSinhDict[r.NhaCungCapId] = r.TongPhatSinh;
                    }
                    if (!daTraDict.ContainsKey(r.NhaCungCapId) || daTraDict[r.NhaCungCapId] < r.TongDaTra)
                    {
                        daTraDict[r.NhaCungCapId] = r.TongDaTra;
                    }
                }
            }
            catch { }

            // Lấy danh sách nhà cung cấp đang hoạt động
            const string sqlNcc = """
            SELECT 
                ncc.id AS Id,
                ncc.ma_nha_cung_cap AS MaNhaCungCap,
                ncc.ten_nha_cung_cap AS TenNhaCungCap,
                COALESCE(ncc.so_dien_thoai, '') AS SoDienThoai,
                COALESCE(ncc.email, '') AS Email,
                COALESCE(ncc.dia_chi, '') AS DiaChi,
                COALESCE(ncc.nguoi_lien_he, '') AS NguoiLienHe,
                COALESCE(nncc.ten_nhom, 'Mặc định') AS NhomNhaCungCap,
                ncc.no_can_tra_hien_tai_excel AS NoExcel
            FROM nha_cung_cap ncc
            LEFT JOIN nhom_nha_cung_cap nncc ON nncc.id = ncc.nhom_nha_cung_cap_id
            WHERE ncc.trang_thai = 1;
            """;
            var allNcc = (await connection.QueryAsync<dynamic>(sqlNcc)).AsList();

            foreach (var ncc in allNcc)
            {
                uint nccId = (uint)ncc.Id;

                // Xác định nợ đầu kỳ
                decimal noDau = 0m;
                int soKy = 1;

                if (daLuuDict.TryGetValue(nccId, out var dlItem))
                {
                    noDau = dlItem.NoDauKy;
                    soKy = dlItem.SoKyNo;
                }
                else if (noCuoiKyTruocDict.TryGetValue(nccId, out var ktItem))
                {
                    noDau = ktItem.NoCuoi;
                    soKy = noDau > 0 ? (ktItem.SoKyNo + 1) : 1;
                }
                else if (noBanDauDict.TryGetValue(nccId, out var nbVal))
                {
                    noDau = nbVal;
                    soKy = 1;
                }
                else
                {
                    decimal noEx = 0;
                    try { noEx = Convert.ToDecimal(ncc.NoExcel ?? 0); } catch { }
                    noDau = noEx;
                    soKy = 1;
                }

                decimal phatSinh = phatSinhDict.GetValueOrDefault(nccId, 0m);
                decimal daTra = daTraDict.GetValueOrDefault(nccId, 0m);
                decimal noCuoi = Math.Max(0m, noDau + phatSinh - daTra);

                // Chỉ đưa vào danh sách theo dõi nếu có nợ đầu, có phát sinh, hoặc còn nợ
                if (noDau > 0 || phatSinh > 0 || daTra > 0 || noCuoi > 0)
                {
                    var item = new NhaCungCapKyCongNoItem
                    {
                        Id = dlItem?.Id ?? 0,
                        KyKeToanId = kyId,
                        NhaCungCapId = nccId,
                        MaNhaCungCap = ncc.MaNhaCungCap ?? "",
                        TenNhaCungCap = ncc.TenNhaCungCap ?? "",
                        SoDienThoai = ncc.SoDienThoai ?? "",
                        Email = ncc.Email ?? "",
                        DiaChi = ncc.DiaChi ?? "",
                        NguoiLienHe = ncc.NguoiLienHe ?? "",
                        NhomNhaCungCap = ncc.NhomNhaCungCap ?? "",
                        NoDauKy = noDau,
                        PhatSinhTrongKy = phatSinh,
                        DaTraTrongKy = daTra,
                        SoKyNo = soKy,
                        GhiChu = dlItem?.GhiChu
                    };

                    item.DanhGiaLaiMucDoUuTien();
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
        {
            query = query.Where(x => x.NoCuoiKy >= minNoFilter.Value);
        }

        if (maxNoFilter.HasValue && maxNoFilter.Value > 0)
        {
            query = query.Where(x => x.NoCuoiKy <= maxNoFilter.Value);
        }

        return query.ToList();
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
        await EnsureBangCongNoNccKyAsync(connection);
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

            // 2. Tính toán và lưu chốt số liệu các NCC nợ trong kỳ hiện tại
            var danhSachNcc = await GetCongNoNccTheoKyAsync(kyId);

            const string upsertSql = """
            INSERT INTO cong_no_ncc_ky
            (
                ky_ke_toan_id, nha_cung_cap_id, no_dau_ky, phat_sinh_trong_ky,
                da_tra_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyKeToanId, @NhaCungCapId, @NoDauKy, @PhatSinhTrongKy,
                @DaTraTrongKy, @NoCuoiKy, @SoKyNo, @MucDoUuTien, @GhiChu
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDauKy,
                phat_sinh_trong_ky = @PhatSinhTrongKy,
                da_tra_trong_ky = @DaTraTrongKy,
                no_cuoi_ky = @NoCuoiKy,
                so_ky_no = @SoKyNo,
                muc_do_uu_tien = @MucDoUuTien,
                ghi_chu = @GhiChu,
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
                    item.GhiChu
                }, transaction);
            }

            // 3. Khóa sổ kỳ hiện tại (nếu chưa khóa)
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
            SELECT id FROM ky_ke_toan 
            WHERE ngay_bat_dau_ky > @NgayBatDau 
            ORDER BY ngay_bat_dau_ky ASC 
            LIMIT 1;
            """;
            var nextKyId = await connection.ExecuteScalarAsync<uint?>(findNextSql, new { NgayBatDau = ky.NgayBatDauKy }, transaction);

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

            // 5. Kết chuyển số dư cuối kỳ sang làm đầu kỳ mới cho các NCC còn nợ
            var nccConNo = danhSachNcc.Where(k => k.NoCuoiKy > 0).ToList();
            foreach (var kn in nccConNo)
            {
                var nextSoKy = kn.SoKyNo + 1;
                var nextUuTien = (nextSoKy >= 3 || kn.NoCuoiKy >= 10_000_000m) ? "CAO" :
                                 (nextSoKy == 2 || kn.NoCuoiKy >= 5_000_000m) ? "TRUNG_BINH" : "THAP";

                await connection.ExecuteAsync(upsertSql, new
                {
                    KyKeToanId = nextKyId.Value,
                    kn.NhaCungCapId,
                    NoDauKy = kn.NoCuoiKy,
                    PhatSinhTrongKy = 0m,
                    DaTraTrongKy = 0m,
                    NoCuoiKy = kn.NoCuoiKy,
                    SoKyNo = nextSoKy,
                    MucDoUuTien = nextUuTien,
                    GhiChu = $"Kế thừa nợ từ {ky.TenKy}"
                }, transaction);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Kết kỳ công nợ NCC",
                "KY_KE_TOAN",
                kyId,
                ky.MaKy,
                $"Kết kỳ công nợ NCC {ky.TenKy}: Chốt nợ cho {danhSachNcc.Count} nhà cung cấp, kết chuyển {nccConNo.Count} NCC còn nợ sang kỳ mới.",
                danhSachNcc.Sum(x => x.NoCuoiKy));

            return nccConNo.Count;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi KetKyCongNoNccAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Mở lại kỳ công nợ đã khóa sổ.
    /// </summary>
    public async Task<bool> MoLaiKyCongNoNccAsync(uint kyId)
    {
        await using var connection = await OpenAsync();
        const string sql = """
        UPDATE ky_ke_toan
        SET trang_thai_khoa_so = 0,
            ngay_ket_ky = NULL,
            nguoi_ket_ky_id = NULL
        WHERE id = @KyId;
        """;
        var rows = await connection.ExecuteAsync(sql, new { KyId = kyId });
        return rows > 0;
    }

    /// <summary>
    /// Nạp lại công nợ cuối kỳ trước sang làm nợ đầu kỳ này cho nhà cung cấp.
    /// </summary>
    public async Task<int> LoadLaiNoKyTruocNccAsync(uint currentKyId, uint priorKyId)
    {
        await using var connection = await OpenAsync();
        await EnsureBangCongNoNccKyAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sqlPrior = """
            SELECT nha_cung_cap_id, no_cuoi_ky, so_ky_no 
            FROM cong_no_ncc_ky 
            WHERE ky_ke_toan_id = @PriorKyId AND no_cuoi_ky > 0;
            """;
            var priorDebtors = (await connection.QueryAsync<(uint NhaCungCapId, decimal NoCuoiKy, int SoKyNo)>(
                sqlPrior, new { PriorKyId = priorKyId }, transaction)).AsList();

            const string upsertSql = """
            INSERT INTO cong_no_ncc_ky
            (
                ky_ke_toan_id, nha_cung_cap_id, no_dau_ky, phat_sinh_trong_ky,
                da_tra_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyId, @NhaCungCapId, @NoDauKy, 0, 0, @NoDauKy, @SoKyNo, @MucDoUuTien, 'Đồng bộ từ kỳ trước'
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDauKy,
                no_cuoi_ky = @NoDauKy + phat_sinh_trong_ky - da_tra_trong_ky,
                so_ky_no = @SoKyNo,
                muc_do_uu_tien = @MucDoUuTien,
                ngay_cap_nhat = NOW();
            """;

            int updated = 0;
            foreach (var d in priorDebtors)
            {
                var soKy = d.SoKyNo + 1;
                var uuTien = (soKy >= 3 || d.NoCuoiKy >= 10_000_000m) ? "CAO" :
                             (soKy == 2 || d.NoCuoiKy >= 5_000_000m) ? "TRUNG_BINH" : "THAP";

                await connection.ExecuteAsync(upsertSql, new
                {
                    KyId = currentKyId,
                    d.NhaCungCapId,
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
            SafeLog.WriteError("Lỗi LoadLaiNoKyTruocNccAsync", ex);
            throw;
        }
    }

    /// <summary>
    /// Cập nhật ghi chú nhắc nợ hoặc thay đổi mức độ ưu tiên cho một nhà cung cấp trong kỳ.
    /// </summary>
    public async Task<bool> LuuGhiChuNhacNoNccAsync(uint kyId, uint nccId, string? ghiChu, string? mucDoUuTien)
    {
        await using var connection = await OpenAsync();
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
        string? ghiChu = null)
    {
        if (nhaCungCapId == 0)
            throw new ArgumentException("Mã nhà cung cấp không hợp lệ.", nameof(nhaCungCapId));

        if (soTien <= 0)
            throw new ArgumentException("Số tiền thanh toán phải lớn hơn 0.", nameof(soTien));

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Lấy thông tin nhà cung cấp
            const string sqlNcc = "SELECT id, ma_nha_cung_cap, ten_nha_cung_cap FROM nha_cung_cap WHERE id = @Id FOR UPDATE;";
            var ncc = await connection.QueryFirstOrDefaultAsync<dynamic>(sqlNcc, new { Id = nhaCungCapId }, transaction);
            if (ncc is null)
                throw new InvalidOperationException("Không tìm thấy nhà cung cấp.");

            string tenNcc = ncc.ten_nha_cung_cap ?? "";
            uint branchId = Session.CurrentUser?.ChiNhanhId ?? 1;
            uint? userId = Session.CurrentUser?.Id;

            // 2. Lấy danh sách phiếu nhập còn nợ (sắp xếp cũ nhất trước)
            const string sqlPhieu = """
            SELECT id, ma_phieu_nhap, tong_tien_nhap, da_thanh_toan, con_no
            FROM phieu_nhap_hang
            WHERE nha_cung_cap_id = @NccId AND con_no > 0 AND trang_thai NOT IN ('LUU_TAM', 'HUY')
            ORDER BY thoi_gian_nhap ASC, id ASC
            FOR UPDATE;
            """;
            var phieuList = (await connection.QueryAsync<dynamic>(sqlPhieu, new { NccId = nhaCungCapId }, transaction)).AsList();

            decimal conLaiCanPhanBo = soTien;
            foreach (var p in phieuList)
            {
                if (conLaiCanPhanBo <= 0)
                    break;

                decimal conNoPhieu = Convert.ToDecimal(p.con_no);
                decimal traChoPhieu = Math.Min(conLaiCanPhanBo, conNoPhieu);
                decimal daThanhToanMoi = Convert.ToDecimal(p.da_thanh_toan) + traChoPhieu;
                decimal conNoMoi = Math.Max(0m, conNoPhieu - traChoPhieu);

                const string updatePhieuSql = """
                UPDATE phieu_nhap_hang
                SET da_thanh_toan = @DaThanhToan,
                    con_no = @ConNo,
                    ngay_cap_nhat = NOW()
                WHERE id = @Id;
                """;
                await connection.ExecuteAsync(updatePhieuSql, new
                {
                    Id = (uint)p.id,
                    DaThanhToan = daThanhToanMoi,
                    ConNo = conNoMoi
                }, transaction);

                conLaiCanPhanBo -= traChoPhieu;
            }

            // 3. Sinh mã phiếu chi và ghi phiếu chi vào sổ quỹ
            string maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);
            uint? nhomChiId = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");

            var hasPhieuQuyExtra = await EnsurePhieuQuyExtraColumnsAsync();
            var hasPhieuQuyPn = await EnsurePhieuQuyPhieuNhapColumnsAsync();

            string noiDungChi = $"Thanh toán công nợ nhà cung cấp {tenNcc}";
            string ghiChuChi = string.IsNullOrWhiteSpace(ghiChu)
                ? $"Thanh toán công nợ NCC {tenNcc} ({soTien:N0} đ)"
                : ghiChu.Trim();

            string insertPhieuChiSql;
            if (hasPhieuQuyPn && hasPhieuQuyExtra)
            {
                insertPhieuChiSql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                    noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                    nguoi_thuc_hien_id, trang_thai
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                    @NoiDung, @NguoiNopNhan, @GhiChu, NOW(),
                    @NguoiTaoId, 'HOAN_THANH'
                );
                """;
            }
            else
            {
                insertPhieuChiSql = """
                INSERT INTO phieu_chi
                (
                    ma_phieu_chi, chi_nhanh_id, so_tien, phuong_thuc,
                    noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai
                )
                VALUES
                (
                    @MaPhieu, @ChiNhanhId, @SoTien, @PhuongThuc,
                    @NoiDung, NOW(), @NguoiTaoId, 'HOAN_THANH'
                );
                """;
            }

            await connection.ExecuteAsync(insertPhieuChiSql, new
            {
                MaPhieu = maPhieuChi,
                ChiNhanhId = branchId,
                NhomChiPhiId = nhomChiId,
                SoTien = soTien,
                PhuongThuc = ChuanHoaPhuongThuc(phuongThuc),
                NoiDung = noiDungChi,
                NguoiNopNhan = tenNcc,
                GhiChu = ghiChuChi,
                NguoiTaoId = userId
            }, transaction);

            // 4. Ghi một dòng vào sổ cái lich_su_cong_no_nha_cung_cap
            await GhiMotDongCongNoNhaCungCapAsync(
                connection,
                transaction,
                nhaCungCapId,
                branchId,
                "THANH_TOAN_NHAP",
                maPhieuChi,
                0,
                soTien,
                DateTime.Now,
                userId,
                ghiChuChi);

            // 5. Cập nhật bảng cong_no_ncc_ky nếu có kỳ đang mở
            const string updateKySql = """
            UPDATE cong_no_ncc_ky
            SET da_tra_trong_ky = da_tra_trong_ky + @SoTien,
                no_cuoi_ky = GREATEST(0, no_dau_ky + phat_sinh_trong_ky - (da_tra_trong_ky + @SoTien)),
                ngay_cap_nhat = NOW()
            WHERE nha_cung_cap_id = @NccId;
            """;
            await connection.ExecuteAsync(updateKySql, new { NccId = nhaCungCapId, SoTien = soTien }, transaction);

            // 6. Tính tổng nợ còn lại sau thanh toán
            const string sqlConNo = """
            SELECT COALESCE(SUM(con_no), 0)
            FROM phieu_nhap_hang
            WHERE nha_cung_cap_id = @NccId AND trang_thai NOT IN ('LUU_TAM', 'HUY');
            """;
            decimal conNoConLai = await connection.ExecuteScalarAsync<decimal>(sqlConNo, new { NccId = nhaCungCapId }, transaction);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Thanh toán công nợ NCC",
                "NHA_CUNG_CAP",
                nhaCungCapId,
                ncc.ma_nha_cung_cap ?? "",
                $"Thanh toán nợ cho nhà cung cấp {tenNcc}: {soTien:N0} đ qua phiếu chi {maPhieuChi}",
                soTien);

            return (conNoConLai, maPhieuChi);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi TraNoNhaCungCapAsync", ex);
            throw;
        }
    }
}
