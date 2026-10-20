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
            const string createSql = """
            CREATE TABLE IF NOT EXISTS cong_no_khach_hang_ky (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ky_ke_toan_id INT UNSIGNED NOT NULL,
                khach_hang_id INT UNSIGNED NOT NULL,
                no_dau_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
                phat_sinh_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0,
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
            _daDamBaoBangCongNoKy = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không tạo được bảng cong_no_khach_hang_ky", ex);
        }
    }

    /// <summary>
    /// Lấy danh sách công nợ khách hàng theo kỳ kế toán kinh doanh.
    /// Hỗ trợ tìm kiếm, lọc theo số kỳ nợ, mức độ ưu tiên nhắc nợ và số tiền nợ.
    /// </summary>
    public async Task<IReadOnlyList<KhachHangKyCongNoItem>> GetCongNoTheoKyAsync(
        uint kyId,
        string? search = null,
        int? soKyFilter = null,
        string? uuTienFilter = null,
        decimal? minNoFilter = null,
        decimal? maxNoFilter = null)
    {
        if (kyId == 0)
            return Array.Empty<KhachHangKyCongNoItem>();

        await using var connection = await OpenAsync();
        await EnsureBangCongNoKyAsync(connection);

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
            return Array.Empty<KhachHangKyCongNoItem>();

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

            var rows = (await connection.QueryAsync<KhachHangKyCongNoItem>(sqlLocked, new { KyId = kyId })).AsList();
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
            var daLuuDict = new Dictionary<uint, KhachHangKyCongNoItem>();
            try
            {
                var daLuu = await connection.QueryAsync<KhachHangKyCongNoItem>(
                    "SELECT * FROM cong_no_khach_hang_ky WHERE ky_ke_toan_id = @KyId;",
                    new { KyId = kyId });
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
                    new { KyTruocId = kyTruoc.Id });
                foreach (var r in rowsKyTruoc)
                    noCuoiKyTruocDict[r.KhachHangId] = (r.NoCuoiKy, r.SoKyNo);
            }

            // Đọc công nợ ban đầu (chi_tiet_cong_no_dau_ky)
            var noBanDauDict = new Dictionary<uint, decimal>();
            try
            {
                var rowsDauKy = await connection.QueryAsync<(uint DoiTacId, decimal SoTien)>(
                    "SELECT doi_tac_id, so_tien FROM chi_tiet_cong_no_dau_ky WHERE loai_doi_tac = 'KHACH_HANG';");
                foreach (var r in rowsDauKy)
                    noBanDauDict[r.DoiTacId] = r.SoTien;
            }
            catch { }

            // Đọc phát sinh tăng nợ (hóa đơn bán chịu / ghi nợ) trong khoảng thời gian của kỳ
            var tuNgay = ky.NgayBatDauKy.Date;
            var denNgay = ky.NgayKetThucKy.Date.AddDays(1).AddTicks(-1);

            var phatSinhDict = new Dictionary<uint, decimal>();
            var daThuDict = new Dictionary<uint, decimal>();

            // Lấy từ sổ cái lịch sử công nợ
            const string sqlLichSu = """
            SELECT khach_hang_id AS KhachHangId,
                   SUM(CASE WHEN ghi_no > 0 AND loai_giao_dich <> 'NO_DAU_KY' THEN ghi_no ELSE 0 END) AS TongPhatSinh,
                   SUM(ghi_co) AS TongDaThu
            FROM lich_su_cong_no_khach_hang
            WHERE thoi_gian >= @TuNgay AND thoi_gian <= @DenNgay
            GROUP BY khach_hang_id;
            """;
            var lsRows = await connection.QueryAsync<(uint KhachHangId, decimal TongPhatSinh, decimal TongDaThu)>(
                sqlLichSu, new { TuNgay = tuNgay, DenNgay = denNgay });
            foreach (var r in lsRows)
            {
                phatSinhDict[r.KhachHangId] = r.TongPhatSinh;
                daThuDict[r.KhachHangId] = r.TongDaThu;
            }

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
            var allKhach = (await connection.QueryAsync<dynamic>(sqlKhach)).AsList();

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
                decimal noCuoi = Math.Max(0m, noDau + phatSinh - daThu);

                // Chỉ đưa vào danh sách theo dõi công nợ nếu có nợ đầu, có phát sinh, hoặc còn nợ
                if (noDau > 0 || phatSinh > 0 || daThu > 0 || noCuoi > 0)
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
                        DaThuTrongKy = daThu,
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

        return query.ToList();
    }

    /// <summary>
    /// Chốt số liệu công nợ cuối kỳ (Kết kỳ) và khóa sổ kỳ này.
    /// Tự động kết chuyển nợ cuối kỳ này sang làm nợ đầu kỳ tiếp theo.
    /// </summary>
    public async Task<int> KetKyCongNoAsync(uint kyId)
    {
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

            // 2. Tính toán và lưu chốt số liệu các khách nợ trong kỳ hiện tại
            var danhSachKhach = await GetCongNoTheoKyAsync(kyId);

            const string upsertSql = """
            INSERT INTO cong_no_khach_hang_ky
            (
                ky_ke_toan_id, khach_hang_id, no_dau_ky, phat_sinh_trong_ky,
                da_thu_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyKeToanId, @KhachHangId, @NoDauKy, @PhatSinhTrongKy,
                @DaThuTrongKy, @NoCuoiKy, @SoKyNo, @MucDoUuTien, @GhiChu
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDauKy,
                phat_sinh_trong_ky = @PhatSinhTrongKy,
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
                    item.DaThuTrongKy,
                    NoCuoiKy = item.NoCuoiKy,
                    item.SoKyNo,
                    item.MucDoUuTien,
                    item.GhiChu
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

            // 5. Kết chuyển số dư cuối kỳ sang làm đầu kỳ mới cho các khách còn nợ
            var khachConNo = danhSachKhach.Where(k => k.NoCuoiKy > 0).ToList();
            foreach (var kn in khachConNo)
            {
                var nextSoKy = kn.SoKyNo + 1;
                var nextUuTien = (nextSoKy >= 3 || kn.NoCuoiKy >= 5_000_000m) ? "CAO" :
                                 (nextSoKy == 2 || kn.NoCuoiKy >= 2_000_000m) ? "TRUNG_BINH" : "THAP";

                await connection.ExecuteAsync(upsertSql, new
                {
                    KyKeToanId = nextKyId.Value,
                    KhachHangId = kn.KhachHangId,
                    NoDauKy = kn.NoCuoiKy,
                    PhatSinhTrongKy = 0m,
                    DaThuTrongKy = 0m,
                    NoCuoiKy = kn.NoCuoiKy,
                    SoKyNo = nextSoKy,
                    MucDoUuTien = nextUuTien,
                    GhiChu = $"Kế thừa nợ từ {ky.TenKy}"
                }, transaction);
            }

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
    /// Mở lại kỳ công nợ đã khóa sổ (dành cho quản trị viên khi cần hiệu chỉnh lại số liệu).
    /// </summary>
    public async Task<bool> MoLaiKyCongNoAsync(uint kyId)
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

            const string upsertSql = """
            INSERT INTO cong_no_khach_hang_ky
            (
                ky_ke_toan_id, khach_hang_id, no_dau_ky, phat_sinh_trong_ky,
                da_thu_trong_ky, no_cuoi_ky, so_ky_no, muc_do_uu_tien, ghi_chu
            )
            VALUES
            (
                @KyId, @KhachHangId, @NoDauKy, 0, 0, @NoDauKy, @SoKyNo, @MucDoUuTien, 'Đồng bộ từ kỳ trước'
            )
            ON DUPLICATE KEY UPDATE
                no_dau_ky = @NoDauKy,
                no_cuoi_ky = @NoDauKy + phat_sinh_trong_ky - da_thu_trong_ky,
                so_ky_no = @SoKyNo,
                muc_do_uu_tien = @MucDoUuTien,
                ngay_cap_nhat = NOW();
            """;

            int updated = 0;
            foreach (var d in priorDebtors)
            {
                var soKy = d.SoKyNo + 1;
                var uuTien = (soKy >= 3 || d.NoCuoiKy >= 5_000_000m) ? "CAO" :
                             (soKy == 2 || d.NoCuoiKy >= 2_000_000m) ? "TRUNG_BINH" : "THAP";

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
