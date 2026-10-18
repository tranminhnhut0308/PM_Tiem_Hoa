using System.Data;
using System.Globalization;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    private bool _hasBangPhieuXuatHang;

    /// <summary>
    /// Đảm bảo bảng phieu_xuat_hang và chi_tiet_phieu_xuat_hang tồn tại trong cơ sở dữ liệu.
    /// </summary>
    public async Task EnsureBangPhieuXuatHangAsync(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        if (_hasBangPhieuXuatHang)
            return;

        try
        {
            const string sqlHeader = """
            CREATE TABLE IF NOT EXISTS phieu_xuat_hang (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                ma_phieu_xuat VARCHAR(50) NOT NULL UNIQUE,
                chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
                kho_xuat VARCHAR(100) DEFAULT 'Kho Tổng (Kho mặc định)',
                loai_xuat VARCHAR(50) NOT NULL DEFAULT 'XUAT_HUY',
                doi_tuong_nhan VARCHAR(150) NULL,
                khach_hang_id INT UNSIGNED NULL,
                nguoi_xuat_id INT UNSIGNED NULL,
                ten_nhan_vien VARCHAR(100) NULL,
                thoi_gian_xuat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                tong_so_luong DECIMAL(15, 2) NOT NULL DEFAULT 0,
                tong_tien DECIMAL(15, 2) NOT NULL DEFAULT 0,
                ghi_chu TEXT NULL,
                trang_thai VARCHAR(30) NOT NULL DEFAULT 'DA_XUAT_KHO',
                ngay_tao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ngay_cap_nhat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_px_ma (ma_phieu_xuat),
                INDEX idx_px_ngay (thoi_gian_xuat)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            const string sqlDetail = """
            CREATE TABLE IF NOT EXISTS chi_tiet_phieu_xuat_hang (
                id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                phieu_xuat_id INT UNSIGNED NOT NULL,
                san_pham_id INT UNSIGNED NOT NULL,
                ma_san_pham VARCHAR(50) NOT NULL,
                ten_san_pham VARCHAR(255) NOT NULL,
                don_vi_tinh VARCHAR(50) NULL,
                ton_kho_hien_tai DECIMAL(15, 2) NOT NULL DEFAULT 0,
                so_luong DECIMAL(15, 2) NOT NULL DEFAULT 1,
                don_gia DECIMAL(15, 2) NOT NULL DEFAULT 0,
                thanh_tien DECIMAL(15, 2) NOT NULL DEFAULT 0,
                ghi_chu VARCHAR(255) NULL,
                INDEX idx_ctpx_px (phieu_xuat_id),
                INDEX idx_ctpx_sp (san_pham_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

            await connection.ExecuteAsync(sqlHeader, transaction: transaction);
            await connection.ExecuteAsync(sqlDetail, transaction: transaction);
            _hasBangPhieuXuatHang = true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Không thể đảm bảo bảng phieu_xuat_hang", ex);
        }
    }

    /// <summary>
    /// Sinh mã phiếu xuất kế tiếp trong ngày, dạng PXyyMMdd-NNN (VD: PX261018-001).
    /// Đảm bảo không bị trùng lặp ngay cả khi có nhiều giao dịch đồng thời.
    /// </summary>
    public async Task<string> TaoMaPhieuXuatMoiAsync()
    {
        await using var connection = await OpenAsync();
        await EnsureBangPhieuXuatHangAsync(connection);

        var prefix = $"PX{DateTime.Now:yyMMdd}";

        const string sql = """
        SELECT COALESCE(
            MAX(CAST(SUBSTRING(ma_phieu_xuat, CHAR_LENGTH(@Prefix) + 2) AS UNSIGNED)),
            0
        )
        FROM phieu_xuat_hang
        WHERE ma_phieu_xuat LIKE CONCAT(@Prefix, '-%')
          AND ma_phieu_xuat REGEXP CONCAT('^', @Prefix, '-[0-9]+$');
        """;

        var maxNumber = await connection.ExecuteScalarAsync<decimal>(sql, new { Prefix = prefix });
        var nextNumber = Convert.ToInt64(Math.Clamp(maxNumber, 0, 999_999_999)) + 1;

        for (var i = 0; i < 100; i++)
        {
            var candidate = $"{prefix}-{nextNumber:D3}";
            var exists = await connection.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM phieu_xuat_hang WHERE ma_phieu_xuat = @Ma LIMIT 1;",
                new { Ma = candidate });

            if (exists is null)
                return candidate;

            nextNumber++;
        }

        return $"{prefix}-{nextNumber:D3}";
    }

    /// <summary>
    /// Lấy số lượng tồn kho khả dụng hiện tại của một sản phẩm tại chi nhánh.
    /// </summary>
    public async Task<decimal> LayTonKhoSanPhamAsync(uint sanPhamId, uint chiNhanhId = 1)
    {
        try
        {
            await using var connection = await OpenAsync();
            const string sql = """
            SELECT COALESCE(so_luong_ton, 0)
            FROM ton_kho
            WHERE san_pham_id = @SanPhamId AND chi_nhanh_id = @ChiNhanhId
            LIMIT 1;
            """;

            return await connection.ExecuteScalarAsync<decimal>(sql, new { SanPhamId = sanPhamId, ChiNhanhId = chiNhanhId });
        }
        catch
        {
            return 0m;
        }
    }

    /// <summary>
    /// Lưu phiếu xuất hàng: cập nhật tồn kho (trừ kho), ghi nhận lịch sử xuất kho,
    /// tạo chi tiết phiếu và lưu thông tin phiếu xuất trong MỘT giao dịch duy nhất.
    /// </summary>
    public async Task<uint> LuuPhieuXuatHangAsync(
        PhieuXuatHang phieu,
        IReadOnlyList<ChiTietPhieuXuatHang> items)
    {
        if (phieu is null)
            throw new ArgumentNullException(nameof(phieu));

        var dongHopLe = items.Where(x => x.SanPham.Id > 0 && x.SoLuong > 0).ToList();
        if (dongHopLe.Count == 0)
            throw new InvalidOperationException("Phiếu xuất phải có ít nhất một mặt hàng với số lượng lớn hơn 0.");

        if (string.IsNullOrWhiteSpace(phieu.MaPhieuXuat))
            phieu.MaPhieuXuat = await TaoMaPhieuXuatMoiAsync();

        phieu.MaPhieuXuat = phieu.MaPhieuXuat.Trim();
        var branchId = phieu.ChiNhanhId > 0 ? phieu.ChiNhanhId : (Session.CurrentUser?.ChiNhanhId ?? 1);
        phieu.ChiNhanhId = branchId;
        phieu.NguoiXuatId ??= Session.CurrentUser?.Id;
        phieu.TenNhanVien = Session.CurrentUser?.HoTen ?? phieu.TenNhanVien ?? "Quản trị viên";
        phieu.TongSoLuong = dongHopLe.Sum(x => x.SoLuong);
        phieu.TongTien = dongHopLe.Sum(x => x.ThanhTien);
        phieu.TrangThai = "DA_XUAT_KHO";

        await using var connection = await OpenAsync();
        await EnsureBangPhieuXuatHangAsync(connection);
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 0. Kiểm tra trùng mã phiếu xuất
            var exists = await connection.ExecuteScalarAsync<int?>(
                "SELECT id FROM phieu_xuat_hang WHERE ma_phieu_xuat = @Ma LIMIT 1 FOR UPDATE;",
                new { Ma = phieu.MaPhieuXuat },
                transaction);

            if (exists.HasValue && exists.Value > 0)
                throw new InvalidOperationException($"Mã phiếu xuất '{phieu.MaPhieuXuat}' đã tồn tại trong hệ thống.");

            // 1. Kiểm tra tồn kho khả dụng cho từng dòng hàng
            const string selectTonSql = """
            SELECT so_luong_ton, gia_von_binh_quan
            FROM ton_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId
            FOR UPDATE;
            """;

            var stockSnapshot = new List<(ChiTietPhieuXuatHang Item, decimal TonTruoc, decimal TonSau, decimal GiaVon)>();

            foreach (var item in dongHopLe)
            {
                var tonHienTai = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    selectTonSql,
                    new { ChiNhanhId = branchId, SanPhamId = item.SanPham.Id },
                    transaction);

                decimal tonTruoc = 0;
                decimal giaVon = item.DonGia;

                if (tonHienTai != null)
                {
                    tonTruoc = ToDecimal(tonHienTai.so_luong_ton);
                    if (giaVon <= 0)
                        giaVon = ToDecimal(tonHienTai.gia_von_binh_quan);
                }

                // Không cho xuất vượt tồn kho
                if (item.SoLuong > tonTruoc)
                {
                    throw new InvalidOperationException(
                        $"Hàng hóa \"{item.TenSanPham}\" ({item.MaSanPham}) chỉ còn tồn kho {tonTruoc:N0}, không đủ để xuất {item.SoLuong:N0}!");
                }

                var tonSau = tonTruoc - item.SoLuong;
                stockSnapshot.Add((item, tonTruoc, tonSau, giaVon));
            }

            // 2. Thêm đầu phiếu xuất
            const string insertHeaderSql = """
            INSERT INTO phieu_xuat_hang
            (
                ma_phieu_xuat,
                chi_nhanh_id,
                kho_xuat,
                loai_xuat,
                doi_tuong_nhan,
                khach_hang_id,
                nguoi_xuat_id,
                ten_nhan_vien,
                thoi_gian_xuat,
                tong_so_luong,
                tong_tien,
                ghi_chu,
                trang_thai,
                ngay_tao,
                ngay_cap_nhat
            )
            VALUES
            (
                @MaPhieuXuat,
                @ChiNhanhId,
                @KhoXuat,
                @LoaiXuat,
                @DoiTuongNhan,
                @KhachHangId,
                @NguoiXuatId,
                @TenNhanVien,
                @ThoiGianXuat,
                @TongSoLuong,
                @TongTien,
                @GhiChu,
                @TrangThai,
                NOW(),
                NOW()
            );
            SELECT LAST_INSERT_ID();
            """;

            phieu.Id = await connection.ExecuteScalarAsync<uint>(
                insertHeaderSql,
                new
                {
                    phieu.MaPhieuXuat,
                    phieu.ChiNhanhId,
                    phieu.KhoXuat,
                    phieu.LoaiXuat,
                    DoiTuongNhan = BlankToNull(phieu.DoiTuongNhan),
                    phieu.KhachHangId,
                    phieu.NguoiXuatId,
                    phieu.TenNhanVien,
                    phieu.ThoiGianXuat,
                    phieu.TongSoLuong,
                    phieu.TongTien,
                    GhiChu = BlankToNull(phieu.GhiChu),
                    phieu.TrangThai
                },
                transaction);

            // 3. Thêm chi tiết phiếu xuất
            const string insertDetailSql = """
            INSERT INTO chi_tiet_phieu_xuat_hang
            (
                phieu_xuat_id,
                san_pham_id,
                ma_san_pham,
                ten_san_pham,
                don_vi_tinh,
                ton_kho_hien_tai,
                so_luong,
                don_gia,
                thanh_tien,
                ghi_chu
            )
            VALUES
            (
                @PhieuXuatId,
                @SanPhamId,
                @MaSanPham,
                @TenSanPham,
                @DonViTinh,
                @TonKhoHienTai,
                @SoLuong,
                @DonGia,
                @ThanhTien,
                @GhiChu
            );
            """;

            const string updateTonSql = """
            UPDATE ton_kho
            SET so_luong_ton  = @SoLuongTon,
                ngay_cap_nhat = NOW()
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId;
            """;

            const string insertLichSuSql = """
            INSERT INTO lich_su_nhap_xuat_kho
            (
                chi_nhanh_id,
                san_pham_id,
                loai_giao_dich,
                loai_chung_tu,
                ma_chung_tu,
                so_luong,
                ton_truoc,
                ton_sau,
                gia_von,
                nguoi_thuc_hien_id,
                thoi_gian,
                ghi_chu
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                'XUAT',
                'PHIEU_XUAT_HANG',
                @MaChungTu,
                @SoLuong,
                @TonTruoc,
                @TonSau,
                @GiaVon,
                @NguoiThucHienId,
                @ThoiGian,
                @GhiChu
            );
            """;

            foreach (var (item, tonTruoc, tonSau, giaVon) in stockSnapshot)
            {
                await connection.ExecuteAsync(
                    insertDetailSql,
                    new
                    {
                        PhieuXuatId = phieu.Id,
                        SanPhamId = item.SanPham.Id,
                        MaSanPham = item.MaSanPham,
                        TenSanPham = item.TenSanPham,
                        DonViTinh = item.DonViTinh,
                        TonKhoHienTai = tonTruoc,
                        item.SoLuong,
                        item.DonGia,
                        item.ThanhTien,
                        GhiChu = BlankToNull(item.GhiChu)
                    },
                    transaction);

                // Cập nhật tồn kho (trừ kho)
                await connection.ExecuteAsync(
                    updateTonSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = item.SanPham.Id,
                        SoLuongTon = tonSau
                    },
                    transaction);

                // Ghi nhận lịch sử biến động kho
                await connection.ExecuteAsync(
                    insertLichSuSql,
                    new
                    {
                        ChiNhanhId = branchId,
                        SanPhamId = item.SanPham.Id,
                        MaChungTu = phieu.MaPhieuXuat,
                        item.SoLuong,
                        TonTruoc = tonTruoc,
                        TonSau = tonSau,
                        GiaVon = giaVon,
                        NguoiThucHienId = phieu.NguoiXuatId,
                        ThoiGian = phieu.ThoiGianXuat,
                        GhiChu = $"Xuất kho ({phieu.LoaiXuatHienThi}) - Phiếu {phieu.MaPhieuXuat}"
                    },
                    transaction);
            }

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Xuất kho",
                "PHIEU_XUAT",
                phieu.Id,
                phieu.MaPhieuXuat,
                $"Tạo phiếu xuất kho {phieu.MaPhieuXuat} ({phieu.LoaiXuatHienThi}, Tổng {phieu.TongSoLuong:N0} SP, Giá trị: {phieu.TongTien:N0} đ)",
                phieu.TongTien);

            return phieu.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ================================================================
    // DANH SÁCH PHIẾU BÁN LẺ (HÓA ĐƠN) - PHÂN TRANG VÀ THỐNG KÊ
    // ================================================================

    public sealed class ThongKeHoaDonBanLe
    {
        public int TongSoHoaDon { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal TongGiamGia { get; set; }
        public decimal TongDoanhThu { get; set; }
        public decimal TongDaThu { get; set; }
        public decimal TongCongNo { get; set; }
    }

    /// <summary>
    /// Đọc danh sách phiếu bán lẻ (hóa đơn) có phân trang, lọc nâng cao theo khoảng ngày,
    /// khách hàng, nhân viên, trạng thái thanh toán và phương thức thanh toán.
    /// Kèm 4 chỉ số thống kê tổng hợp trực tiếp từ database.
    /// </summary>
    public async Task<(IReadOnlyList<HoaDon> Items, ThongKeHoaDonBanLe ThongKe)>
        GetHoaDonPagedAsync(
            string? search = null,
            DateTime? tuNgay = null,
            DateTime? denNgay = null,
            string? trangThaiLoc = null,
            string? phuongThuc = null,
            int page = 1,
            int pageSize = 20)
    {
        await using var connection = await OpenAsync();

        var safePageSize = pageSize is < 5 or > 200 ? 20 : pageSize;
        var safePage = Math.Max(1, page);
        var offset = (safePage - 1) * safePageSize;

        var keyword = (search ?? "").Trim();
        var status = (trangThaiLoc ?? "").Trim().ToUpperInvariant();
        var method = (phuongThuc ?? "").Trim().ToUpperInvariant();

        var whereClause = new List<string>();
        var param = new DynamicParameters();

        param.Add("Search", keyword);
        param.Add("PageSize", safePageSize);
        param.Add("Offset", offset);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            whereClause.Add("""
            (
                hd.ma_hoa_don LIKE CONCAT('%', @Search, '%')
                OR kh.ma_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.ten_khach_hang LIKE CONCAT('%', @Search, '%')
                OR kh.so_dien_thoai LIKE CONCAT('%', @Search, '%')
                OR COALESCE(hd.so_dien_thoai, '') LIKE CONCAT('%', @Search, '%')
                OR COALESCE(nd.ho_ten, '') LIKE CONCAT('%', @Search, '%')
            )
            """);
        }

        if (tuNgay.HasValue)
        {
            whereClause.Add("hd.thoi_gian_ban >= @TuNgay");
            param.Add("TuNgay", tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            whereClause.Add("hd.thoi_gian_ban < @DenNgay");
            param.Add("DenNgay", denNgay.Value.Date.AddDays(1));
        }

        if (status == "PAID")
        {
            whereClause.Add("(hd.con_no <= 0 AND hd.trang_thai_thanh_toan != 'HUY')");
        }
        else if (status == "DEBT")
        {
            whereClause.Add("(hd.con_no > 0 AND hd.trang_thai_thanh_toan != 'HUY')");
        }
        else if (status == "CANCEL" || status == "HUY")
        {
            whereClause.Add("hd.trang_thai_thanh_toan = 'HUY'");
        }

        var whereSql = whereClause.Count > 0 ? "WHERE " + string.Join(" AND ", whereClause) : "";

        // Query thống kê
        var statsSql = $"""
        SELECT
            COUNT(*) AS TongSoHoaDon,
            COALESCE(SUM(hd.tong_tien_hang), 0) AS TongTienHang,
            COALESCE(SUM(hd.giam_gia), 0) AS TongGiamGia,
            COALESCE(SUM(hd.khach_can_tra), 0) AS TongDoanhThu,
            COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS TongDaThu,
            COALESCE(SUM(hd.con_no), 0) AS TongCongNo
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        LEFT JOIN nguoi_dung nd ON nd.id = hd.nguoi_ban_id
        {whereSql};
        """;

        var thongKe = await connection.QueryFirstOrDefaultAsync<ThongKeHoaDonBanLe>(statsSql, param)
                      ?? new ThongKeHoaDonBanLe();

        if (thongKe.TongSoHoaDon == 0)
        {
            return (Array.Empty<HoaDon>(), thongKe);
        }

        // Query danh sách dòng theo phân trang
        var pageSql = $"""
        SELECT
            hd.id AS Id,
            hd.ma_hoa_don AS MaHoaDon,
            hd.thoi_gian_ban AS ThoiGianBan,
            COALESCE(kh.ma_khach_hang, '') AS MaKhachHang,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
            COALESCE(nd.ho_ten, '') AS TenNhanVien,
            COALESCE(hd.so_dien_thoai, kh.so_dien_thoai, '') AS SoDienThoai,
            hd.tong_tien_hang AS TongTienHang,
            hd.giam_gia AS GiamGia,
            hd.thu_khac AS ThuKhac,
            hd.khach_can_tra AS KhachCanTra,
            hd.khach_da_thanh_toan AS KhachDaThanhToan,
            hd.con_no AS ConNo,
            hd.trang_thai_thanh_toan AS TrangThaiThanhToan,
            hd.ghi_chu AS GhiChu
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        LEFT JOIN nguoi_dung nd ON nd.id = hd.nguoi_ban_id
        {whereSql}
        ORDER BY hd.thoi_gian_ban DESC, hd.id DESC
        LIMIT @PageSize OFFSET @Offset;
        """;

        var items = (await connection.QueryAsync<HoaDon>(pageSql, param)).AsList();

        for (var i = 0; i < items.Count; i++)
        {
            items[i].SoThuTu = offset + i + 1;
        }

        return (items, thongKe);
    }

    /// <summary>
    /// Hủy một hóa đơn bán lẻ: hoàn lại số lượng tồn kho sản phẩm, hủy phiếu thu liên quan,
    /// và cập nhật trạng thái hóa đơn thành 'HUY' trong MỘT giao dịch.
    /// </summary>
    public async Task<bool> HuyHoaDonBanLeAsync(uint hoaDonId, string? lyDo = null)
    {
        Quyen.RequirePermission(Session.CurrentUser, PermissionKeys.SalesCancel, "Bạn không có quyền hủy hóa đơn.");

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Kiểm tra trạng thái hiện tại
            const string selectHdSql = """
            SELECT id, ma_hoa_don, chi_nhanh_id, khach_hang_id, khach_can_tra, con_no, trang_thai_thanh_toan
            FROM hoa_don_ban_hang
            WHERE id = @Id
            LIMIT 1
            FOR UPDATE;
            """;

            var hd = await connection.QueryFirstOrDefaultAsync<dynamic>(selectHdSql, new { Id = hoaDonId }, transaction);
            if (hd is null)
                throw new InvalidOperationException("Không tìm thấy hóa đơn cần hủy.");

            if ((string)hd.trang_thai_thanh_toan == "HUY")
                throw new InvalidOperationException("Hóa đơn này đã được hủy trước đó.");

            string maHoaDon = hd.ma_hoa_don;
            uint chiNhanhId = (uint)hd.chi_nhanh_id;
            uint? khachHangId = hd.khach_hang_id != null ? (uint)hd.khach_hang_id : null;
            decimal conNo = ToDecimal(hd.con_no);

            // 2. Đọc chi tiết sản phẩm để hoàn lại tồn kho
            const string selectItemsSql = """
            SELECT san_pham_id, so_luong, don_gia
            FROM chi_tiet_hoa_don_ban_hang
            WHERE hoa_don_id = @HoaDonId;
            """;

            var items = (await connection.QueryAsync<dynamic>(selectItemsSql, new { HoaDonId = hoaDonId }, transaction)).AsList();

            const string selectTonSql = "SELECT so_luong_ton FROM ton_kho WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId FOR UPDATE;";
            const string updateTonSql = "UPDATE ton_kho SET so_luong_ton = @SoLuongTon, ngay_cap_nhat = NOW() WHERE chi_nhanh_id = @ChiNhanhId AND san_pham_id = @SanPhamId;";
            const string insertLichSuSql = """
            INSERT INTO lich_su_nhap_xuat_kho (chi_nhanh_id, san_pham_id, loai_giao_dich, loai_chung_tu, ma_chung_tu, so_luong, ton_truoc, ton_sau, gia_von, nguoi_thuc_hien_id, thoi_gian, ghi_chu)
            VALUES (@ChiNhanhId, @SanPhamId, 'NHAP', 'HUY_HOA_DON', @MaChungTu, @SoLuong, @TonTruoc, @TonSau, @GiaVon, @NguoiThucHienId, NOW(), @GhiChu);
            """;

            var userId = Session.CurrentUser?.Id;

            foreach (var item in items)
            {
                uint spId = (uint)item.san_pham_id;
                decimal soLuong = ToDecimal(item.so_luong);
                decimal donGia = ToDecimal(item.don_gia);

                var tonHienTai = await connection.ExecuteScalarAsync<decimal?>(selectTonSql, new { ChiNhanhId = chiNhanhId, SanPhamId = spId }, transaction) ?? 0m;
                var tonSau = tonHienTai + soLuong;

                await connection.ExecuteAsync(updateTonSql, new { ChiNhanhId = chiNhanhId, SanPhamId = spId, SoLuongTon = tonSau }, transaction);
                await connection.ExecuteAsync(insertLichSuSql, new
                {
                    ChiNhanhId = chiNhanhId,
                    SanPhamId = spId,
                    MaChungTu = maHoaDon,
                    SoLuong = soLuong,
                    TonTruoc = tonHienTai,
                    TonSau = tonSau,
                    GiaVon = donGia,
                    NguoiThucHienId = userId,
                    GhiChu = $"Hoàn trả kho do hủy hóa đơn {maHoaDon}. Lý do: {lyDo ?? "Hủy đơn"}"
                }, transaction);
            }

            // 3. Nếu có công nợ của khách hàng, ghi giảm công nợ
            if (khachHangId.HasValue && khachHangId.Value > 0 && conNo > 0)
            {
                var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(SUM(ghi_no - ghi_co), 0) FROM lich_su_cong_no_khach_hang WHERE khach_hang_id = @KhachHangId FOR UPDATE;",
                    new { KhachHangId = khachHangId.Value },
                    transaction);

                const string debtSql = """
                INSERT INTO lich_su_cong_no_khach_hang (khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu, ma_chung_tu, ghi_no, ghi_co, so_du, thoi_gian, nguoi_thuc_hien_id, ghi_chu)
                VALUES (@KhachHangId, @ChiNhanhId, 'HUY_HOA_DON', 'HOA_DON', @MaChungTu, 0, @GhiCo, @SoDu, NOW(), @NguoiThucHienId, @GhiChu);
                """;

                await connection.ExecuteAsync(debtSql, new
                {
                    KhachHangId = khachHangId.Value,
                    ChiNhanhId = chiNhanhId,
                    MaChungTu = maHoaDon,
                    GhiCo = conNo,
                    SoDu = soDuCu - conNo,
                    NguoiThucHienId = userId,
                    GhiChu = $"Giảm công nợ do hủy hóa đơn {maHoaDon}"
                }, transaction);
            }

            // 4. Hủy phiếu thu trong sổ quỹ nếu có
            try
            {
                await connection.ExecuteAsync(
                    "UPDATE phieu_chi SET trang_thai = 'HUY', ghi_chu = CONCAT(COALESCE(ghi_chu, ''), ' [ĐÃ HỦY THEO HÓA ĐƠN]') WHERE hoa_don_ban_hang_id = @Id OR ma_hoa_don = @Ma;",
                    new { Id = hoaDonId, Ma = maHoaDon },
                    transaction);
            }
            catch { }

            // 5. Cập nhật trạng thái hóa đơn
            const string updateHdSql = """
            UPDATE hoa_don_ban_hang
            SET trang_thai_thanh_toan = 'HUY',
                ghi_chu = CONCAT(COALESCE(ghi_chu, ''), ' [HỦY: ', @LyDo, ']'),
                ngay_cap_nhat = NOW()
            WHERE id = @Id;
            """;

            await connection.ExecuteAsync(updateHdSql, new { Id = hoaDonId, LyDo = lyDo ?? "Người dùng hủy" }, transaction);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Hóa đơn",
                "HOA_DON",
                hoaDonId,
                maHoaDon,
                $"Hủy hóa đơn {maHoaDon}. Lý do: {lyDo ?? "Hủy hóa đơn"}",
                0m);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
