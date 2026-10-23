using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    /// <summary>
    /// Lấy danh sách các chứng từ còn nợ (Hóa đơn hoặc Phiếu nhập) của một đối tác.
    /// </summary>
    public async Task<IReadOnlyList<ChungTuNoOption>> GetChungTuNoCuaDoiTacAsync(string loaiDoiTac, uint doiTacId)
    {
        var result = new List<ChungTuNoOption>();
        if (doiTacId == 0)
            return result;

        loaiDoiTac = (loaiDoiTac ?? "KHACH_HANG").Trim().ToUpperInvariant();
        await using var connection = await TaoKetNoiAsync();

        if (loaiDoiTac == "KHACH_HANG")
        {
            const string sql = """
            SELECT 
                ma_hoa_don          AS MaChungTu,
                'Hóa đơn bán hàng'  AS LoaiChungTu,
                thoi_gian_ban       AS NgayPhatSinh,
                khach_can_tra       AS TongTien,
                khach_da_thanh_toan AS DaThanhToan,
                con_no              AS ConNo,
                ghi_chu             AS GhiChu
            FROM hoa_don_ban_hang
            WHERE khach_hang_id = @DoiTacId
              AND con_no > 0
              AND trang_thai_hoa_don = 'HOAN_THANH'
            ORDER BY thoi_gian_ban ASC, id ASC;
            """;

            var items = await connection.QueryAsync<ChungTuNoOption>(sql, new { DoiTacId = doiTacId });
            result.AddRange(items);
        }
        else
        {
            const string sql = """
            SELECT 
                ma_phieu_nhap       AS MaChungTu,
                'Phiếu nhập hàng'   AS LoaiChungTu,
                thoi_gian_nhap      AS NgayPhatSinh,
                tong_tien_nhap      AS TongTien,
                da_thanh_toan       AS DaThanhToan,
                con_no              AS ConNo,
                ghi_chu             AS GhiChu
            FROM phieu_nhap_hang
            WHERE nha_cung_cap_id = @DoiTacId
              AND con_no > 0
              AND trang_thai != 'DA_HUY'
            ORDER BY thoi_gian_nhap ASC, id ASC;
            """;

            var items = await connection.QueryAsync<ChungTuNoOption>(sql, new { DoiTacId = doiTacId });
            result.AddRange(items);
        }

        return result;
    }

    /// <summary>
    /// Thanh toán công nợ cho nhà cung cấp: phân bổ số tiền thanh toán lần lượt vào các phiếu nhập còn nợ
    /// (phiếu cũ nhất trước). Nếu còn tiền thừa thì ghi nhận vào lịch sử công nợ và sổ quỹ.
    /// Tất cả trong một Transaction an toàn.
    /// </summary>
    public async Task<int> ThanhToanNoNhaCungCapTheoDanhSachAsync(
        uint nhaCungCapId,
        decimal soTien,
        string? phuongThucThanhToan = null,
        DateTime? thoiGianThanhToan = null,
        string? ghiChu = null)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.SuppliersDebtPay,
            "Bạn không có quyền thanh toán công nợ nhà cung cấp.");

        if (nhaCungCapId == 0)
            throw new InvalidOperationException("Thiếu nhà cung cấp cần thanh toán.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền thanh toán phải lớn hơn 0.");

        var now = thoiGianThanhToan ?? DateTime.Now;
        var userId = Session.CurrentUser?.Id;
        var phuongThuc = ChuanHoaPhuongThuc(phuongThucThanhToan);

        var coCotNguoiNopNhanGhiChu = await EnsurePhieuQuyExtraColumnsAsync();
        var coCotPhieuNhap = await EnsurePhieuQuyPhieuNhapColumnsAsync();

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var daCommit = false;
        try
        {
            // 1. Khoá nhà cung cấp
            const string nccSql = """
            SELECT 
                id                 AS Id,
                ten_nha_cung_cap   AS TenNhaCungCap,
                chi_nhanh_id       AS ChiNhanhId
            FROM nha_cung_cap
            WHERE id = @Id
            LIMIT 1
            FOR UPDATE;
            """;

            var ncc = await connection.QueryFirstOrDefaultAsync<dynamic>(nccSql, new { Id = nhaCungCapId }, transaction);
            if (ncc is null)
                throw new InvalidOperationException($"Không tìm thấy nhà cung cấp (id {nhaCungCapId}) trong database.");

            string? tenNcc = ncc.TenNhaCungCap;
            uint chiNhanhId = ncc.ChiNhanhId ?? (Session.CurrentUser?.ChiNhanhId ?? 1);

            // 2. Lấy các phiếu nhập còn nợ, cũ nhất trước
            const string phieuSql = """
            SELECT
                id                 AS Id,
                chi_nhanh_id       AS ChiNhanhId,
                ma_phieu_nhap      AS MaPhieuNhap,
                tong_tien_nhap     AS TongTienNhap,
                da_thanh_toan      AS DaThanhToan,
                con_no             AS ConNo,
                trang_thai         AS TrangThai
            FROM phieu_nhap_hang
            WHERE nha_cung_cap_id = @NhaCungCapId
              AND con_no > 0
              AND trang_thai != 'DA_HUY'
            ORDER BY thoi_gian_nhap ASC, id ASC
            FOR UPDATE;
            """;

            var phieuList = (await connection.QueryAsync<PhieuNhapCongNoRow>(
                phieuSql,
                new { NhaCungCapId = nhaCungCapId },
                transaction)).AsList();

            var conLai = soTien;
            var soPhieuDaThanhToan = 0;

            // Lấy id nhóm chi phí CHI_NHA_CUNG_CAP
            var idNhomChiNhaCungCap = await LayIdNhomChiPhiTheoMaAsync(connection, transaction, "CHI_NHA_CUNG_CAP");

            foreach (var p in phieuList)
            {
                if (conLai <= 0)
                    break;

                if (p.ConNo <= 0)
                    continue;

                var traLanNay = Math.Min(conLai, p.ConNo);
                var daTraMoi = p.DaThanhToan + traLanNay;
                var conNoMoi = Math.Max(0m, p.TongTienNhap - daTraMoi);

                // Cập nhật phiếu nhập
                await connection.ExecuteAsync(
                    """
                    UPDATE phieu_nhap_hang
                    SET da_thanh_toan = @DaThanhToan,
                        con_no        = @ConNo,
                        ngay_cap_nhat = @ThoiGian
                    WHERE id = @Id;
                    """,
                    new { DaThanhToan = daTraMoi, ConNo = conNoMoi, ThoiGian = now, Id = p.Id },
                    transaction);

                // Ghi lịch sử thanh toán nhà cung cấp
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
                        PhieuNhapId = p.Id,
                        SoTien = traLanNay,
                        PhuongThuc = phuongThuc,
                        ThoiGian = now,
                        NguoiThucHienId = userId,
                        GhiChu = BlankToNull(ghiChu)
                    },
                    transaction);

                // Ghi sổ cái công nợ nhà cung cấp
                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    nhaCungCapId,
                    p.ChiNhanhId,
                    "THANH_TOAN_NHAP",
                    p.MaPhieuNhap,
                    0,
                    traLanNay,
                    now,
                    userId,
                    string.IsNullOrWhiteSpace(ghiChu)
                        ? $"Thanh toán {traLanNay:N0} đ cho phiếu {p.MaPhieuNhap}"
                        : $"Thanh toán {traLanNay:N0} đ cho phiếu {p.MaPhieuNhap}. {ghiChu.Trim()}");

                // Ghi phiếu chi sổ quỹ
                var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);
                var noiDung = $"Thanh toán nợ nhà cung cấp {tenNcc} - phiếu {p.MaPhieuNhap}";
                var ghiChuPhieu = $"Chi trả công nợ cho nhà cung cấp {tenNcc} theo phiếu nhập {p.MaPhieuNhap}.";

                string insertPhieuChiSql = coCotPhieuNhap && coCotNguoiNopNhanGhiChu
                    ? """
                      INSERT INTO phieu_chi
                      (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                       noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                       nguoi_thuc_hien_id, trang_thai, phieu_nhap_hang_id, ma_phieu_nhap)
                      VALUES
                      (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                       @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                       @NguoiTaoId, @TrangThai, @PhieuNhapId, @MaPhieuNhap);
                      """
                    : coCotNguoiNopNhanGhiChu
                    ? """
                      INSERT INTO phieu_chi
                      (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                       noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                       nguoi_thuc_hien_id, trang_thai)
                      VALUES
                      (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                       @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                       @NguoiTaoId, @TrangThai);
                      """
                    : """
                      INSERT INTO phieu_chi
                      (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                       noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai)
                      VALUES
                      (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                       @NoiDung, @ThoiGian, @NguoiTaoId, @TrangThai);
                      """;

                await connection.ExecuteAsync(
                    insertPhieuChiSql,
                    new
                    {
                        MaPhieu = maPhieuChi,
                        ChiNhanhId = p.ChiNhanhId,
                        NhomChiPhiId = idNhomChiNhaCungCap,
                        SoTien = traLanNay,
                        PhuongThuc = phuongThuc,
                        NoiDung = noiDung,
                        NguoiNopNhan = Truncate150(tenNcc),
                        GhiChu = ghiChuPhieu,
                        ThoiGian = now,
                        NguoiTaoId = userId,
                        TrangThai = TrangThaiPhieuHoanThanh,
                        PhieuNhapId = p.Id,
                        MaPhieuNhap = p.MaPhieuNhap
                    },
                    transaction);

                conLai -= traLanNay;
                soPhieuDaThanhToan++;
            }

            // Nếu số tiền thanh toán còn dư (hoặc NCC không có phiếu nhập nào nhưng có nợ đầu kỳ)
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
                        PhuongThuc = phuongThuc,
                        ThoiGian = now,
                        NguoiThucHienId = userId,
                        GhiChu = BlankToNull(ghiChu)
                    },
                    transaction);

                await GhiMotDongCongNoNhaCungCapAsync(
                    connection,
                    transaction,
                    nhaCungCapId,
                    chiNhanhId,
                    "THANH_TOAN_NCC",
                    maThanhToan,
                    0,
                    conLai,
                    now,
                    userId,
                    string.IsNullOrWhiteSpace(ghiChu)
                        ? $"Thanh toán công nợ NCC {tenNcc}"
                        : $"Thanh toán công nợ NCC {tenNcc}. {ghiChu.Trim()}");

                var maPhieuChi = await SinhMaPhieuChiTrongGiaoDichAsync(connection, transaction);
                var noiDung = $"Thanh toán nợ nhà cung cấp {tenNcc}";
                var ghiChuPhieu = string.IsNullOrWhiteSpace(ghiChu) ? noiDung : $"{noiDung}. {ghiChu.Trim()}";

                var insertPhieuSql = coCotNguoiNopNhanGhiChu
                    ? """
                      INSERT INTO phieu_chi
                      (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                       noi_dung, nguoi_nop_nhan, ghi_chu, thoi_gian_chi,
                       nguoi_thuc_hien_id, trang_thai)
                      VALUES
                      (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                       @NoiDung, @NguoiNopNhan, @GhiChu, @ThoiGian,
                       @NguoiTaoId, @TrangThai);
                      """
                    : """
                      INSERT INTO phieu_chi
                      (ma_phieu_chi, chi_nhanh_id, nhom_chi_phi_id, so_tien, phuong_thuc,
                       noi_dung, thoi_gian_chi, nguoi_thuc_hien_id, trang_thai)
                      VALUES
                      (@MaPhieu, @ChiNhanhId, @NhomChiPhiId, @SoTien, @PhuongThuc,
                       @NoiDung, @ThoiGian, @NguoiTaoId, @TrangThai);
                      """;

                await connection.ExecuteAsync(
                    insertPhieuSql,
                    new
                    {
                        MaPhieu = maPhieuChi,
                        ChiNhanhId = chiNhanhId,
                        NhomChiPhiId = idNhomChiNhaCungCap,
                        SoTien = conLai,
                        PhuongThuc = phuongThuc,
                        NoiDung = noiDung,
                        NguoiNopNhan = Truncate150(tenNcc),
                        GhiChu = ghiChuPhieu,
                        ThoiGian = now,
                        NguoiTaoId = userId,
                        TrangThai = TrangThaiPhieuHoanThanh
                    },
                    transaction);
            }

            await transaction.CommitAsync();
            daCommit = true;

            _ = LogSubAccountActivityAsync(
                "Thanh toán nợ NCC",
                "NHA_CUNG_CAP",
                nhaCungCapId,
                null,
                $"Thanh toán nợ NCC {(tenNcc ?? nhaCungCapId.ToString())}: {soTien:N0} đ",
                soTien);

            return soPhieuDaThanhToan;
        }
        catch (Exception ex)
        {
            if (!daCommit)
            {
                try { await transaction.RollbackAsync(); }
                catch (Exception rbEx) { SafeLog.WriteError("Không rollback được giao dịch trả nợ NCC", rbEx); }
            }
            SafeLog.WriteError("Lỗi thanh toán nợ nhà cung cấp theo danh sách", ex);
            throw;
        }
    }

    /// <summary>
    /// Thực hiện thanh toán công nợ chung (điều phối nghiệp vụ Khách hàng hoặc Nhà cung cấp).
    /// </summary>
    public async Task<bool> ThucHienThanhToanCongNoAsync(ThanhToanNoYeuCau yeuCau)
    {
        if (yeuCau is null)
            throw new ArgumentNullException(nameof(yeuCau));

        if (yeuCau.DoiTacId == 0)
            throw new InvalidOperationException("Vui lòng chọn đối tác cần thanh toán công nợ.");

        if (yeuCau.SoTien <= 0)
            throw new InvalidOperationException("Số tiền thanh toán phải lớn hơn 0.");

        var loaiDoiTac = (yeuCau.LoaiDoiTac ?? "KHACH_HANG").Trim().ToUpperInvariant();

        if (loaiDoiTac == "KHACH_HANG")
        {
            if (!string.IsNullOrWhiteSpace(yeuCau.MaChungTu) && !yeuCau.MaChungTu.StartsWith("["))
            {
                // Thanh toán theo đích danh một hóa đơn
                await ThuNoHoaDonAsync(yeuCau.MaChungTu, yeuCau.SoTien, yeuCau.PhuongThuc, yeuCau.GhiChu);
            }
            else
            {
                // Thanh toán theo khách hàng (phân bổ từ hóa đơn cũ nhất)
                await ThuNoKhachHangAsync(yeuCau.DoiTacId, yeuCau.SoTien, yeuCau.PhuongThuc, yeuCau.GhiChu);
            }
            return true;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(yeuCau.MaChungTu) && !yeuCau.MaChungTu.StartsWith("["))
            {
                // Thanh toán theo đích danh một phiếu nhập
                await ThanhToanPhieuNhapAsync(
                    yeuCau.MaChungTu,
                    yeuCau.SoTien,
                    yeuCau.PhuongThuc,
                    yeuCau.NgayThanhToan,
                    yeuCau.GhiChu);
            }
            else
            {
                // Thanh toán theo nhà cung cấp (phân bổ từ phiếu cũ nhất)
                await ThanhToanNoNhaCungCapTheoDanhSachAsync(
                    yeuCau.DoiTacId,
                    yeuCau.SoTien,
                    yeuCau.PhuongThuc,
                    yeuCau.NgayThanhToan,
                    yeuCau.GhiChu);
            }
            return true;
        }
    }

    /// <summary>
    /// Tìm kiếm và tra cứu công nợ có phân trang và tính số liệu tổng hợp (Giao diện 48).
    /// Hợp nhất dữ liệu công nợ Khách hàng (Hóa đơn) và Nhà cung cấp (Phiếu nhập).
    /// </summary>
    public async Task<(IReadOnlyList<CongNoItemRow> Items, int TotalCount, CongNoThongKe ThongKe)> TimKiemCongNoPagedAsync(
        CongNoFilterCriteria criteria)
    {
        criteria ??= new CongNoFilterCriteria();
        var loaiDoiTac = (criteria.LoaiDoiTac ?? "TAT_CA").Trim().ToUpperInvariant();
        var trangThai = (criteria.TrangThaiThanhToan ?? "TAT_CA").Trim().ToUpperInvariant();
        var tuKhoa = string.IsNullOrWhiteSpace(criteria.TuKhoa) ? null : criteria.TuKhoa.Trim();

        await using var connection = await TaoKetNoiAsync();

        // Xây dựng câu truy vấn hợp nhất (UNION ALL) từ Khách hàng và Nhà cung cấp
        var unionParts = new List<string>();

        if (loaiDoiTac == "TAT_CA" || loaiDoiTac == "KHACH_HANG")
        {
            var khWhere = new StringBuilder("h.trang_thai_hoa_don = 'HOAN_THANH'");

            if (trangThai == "CON_NO")
                khWhere.Append(" AND h.con_no > 0");
            else if (trangThai == "DA_THANH_TOAN")
                khWhere.Append(" AND h.con_no <= 0");

            if (criteria.TuNgay.HasValue)
                khWhere.Append(" AND h.thoi_gian_ban >= @TuNgay");

            if (criteria.DenNgay.HasValue)
                khWhere.Append(" AND h.thoi_gian_ban <= @DenNgay");

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                khWhere.Append("""
                 AND (
                    h.ma_hoa_don LIKE @SearchLike
                    OR k.ma_khach_hang LIKE @SearchLike
                    OR k.ten_khach_hang LIKE @SearchLike
                    OR k.so_dien_thoai LIKE @SearchLike
                 )
                """);
            }

            unionParts.Add($"""
            SELECT 
                'KHACH_HANG'            AS LoaiDoiTac,
                'Phải thu'              AS LoaiCongNo,
                h.khach_hang_id         AS DoiTacId,
                COALESCE(k.ma_khach_hang, CONCAT('KH', LPAD(h.khach_hang_id, 4, '0'))) AS MaDoiTac,
                COALESCE(k.ten_khach_hang, 'Khách lẻ') AS TenDoiTac,
                k.so_dien_thoai         AS SoDienThoai,
                h.ma_hoa_don            AS MaChungTu,
                'Hóa đơn'               AS LoaiChungTu,
                h.thoi_gian_ban         AS NgayPhatSinh,
                h.khach_can_tra         AS TongTien,
                h.khach_da_thanh_toan   AS DaThanhToan,
                h.con_no                AS ConNo
            FROM hoa_don_ban_hang h
            LEFT JOIN khach_hang k ON k.id = h.khach_hang_id
            WHERE {khWhere}
            """);
        }

        if (loaiDoiTac == "TAT_CA" || loaiDoiTac == "NHA_CUNG_CAP")
        {
            var nccWhere = new StringBuilder("p.trang_thai != 'DA_HUY'");

            if (trangThai == "CON_NO")
                nccWhere.Append(" AND p.con_no > 0");
            else if (trangThai == "DA_THANH_TOAN")
                nccWhere.Append(" AND p.con_no <= 0");

            if (criteria.TuNgay.HasValue)
                nccWhere.Append(" AND p.thoi_gian_nhap >= @TuNgay");

            if (criteria.DenNgay.HasValue)
                nccWhere.Append(" AND p.thoi_gian_nhap <= @DenNgay");

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                nccWhere.Append("""
                 AND (
                    p.ma_phieu_nhap LIKE @SearchLike
                    OR n.ma_nha_cung_cap LIKE @SearchLike
                    OR n.ten_nha_cung_cap LIKE @SearchLike
                    OR n.so_dien_thoai LIKE @SearchLike
                 )
                """);
            }

            unionParts.Add($"""
            SELECT 
                'NHA_CUNG_CAP'          AS LoaiDoiTac,
                'Phải trả'              AS LoaiCongNo,
                p.nha_cung_cap_id       AS DoiTacId,
                COALESCE(n.ma_nha_cung_cap, CONCAT('NCC', LPAD(p.nha_cung_cap_id, 4, '0'))) AS MaDoiTac,
                COALESCE(n.ten_nha_cung_cap, 'Chưa rõ NCC') AS TenDoiTac,
                n.so_dien_thoai         AS SoDienThoai,
                p.ma_phieu_nhap         AS MaChungTu,
                'Phiếu nhập'            AS LoaiChungTu,
                p.thoi_gian_nhap        AS NgayPhatSinh,
                p.tong_tien_nhap        AS TongTien,
                p.da_thanh_toan         AS DaThanhToan,
                p.con_no                AS ConNo
            FROM phieu_nhap_hang p
            LEFT JOIN nha_cung_cap n ON n.id = p.nha_cung_cap_id
            WHERE {nccWhere}
            """);
        }

        if (unionParts.Count == 0)
        {
            return (Array.Empty<CongNoItemRow>(), 0, new CongNoThongKe());
        }

        var fullUnion = string.Join("\nUNION ALL\n", unionParts);

        var queryParams = new
        {
            SearchLike = $"%{tuKhoa}%",
            TuNgay = criteria.TuNgay?.Date,
            DenNgay = criteria.DenNgay?.Date.AddDays(1).AddTicks(-1),
            Offset = Math.Max(0, (criteria.Page - 1) * criteria.PageSize),
            Limit = Math.Max(1, criteria.PageSize)
        };

        // 1. Tính tổng số lượng và thống kê tổng hợp (KPI)
        var countAndSummarySql = $"""
        SELECT 
            COUNT(*)                                                                AS TotalCount,
            COALESCE(SUM(CASE WHEN LoaiDoiTac = 'KHACH_HANG' THEN ConNo ELSE 0 END), 0) AS TongNoPhaiThu,
            COALESCE(SUM(CASE WHEN LoaiDoiTac = 'NHA_CUNG_CAP' THEN ConNo ELSE 0 END), 0) AS TongNoPhaiTra,
            COALESCE(SUM(DaThanhToan), 0)                                           AS TongDaThanhToan
        FROM (
            {fullUnion}
        ) AS T;
        """;

        var summaryRow = await connection.QueryFirstOrDefaultAsync<dynamic>(countAndSummarySql, queryParams);

        int totalCount = summaryRow != null ? (int)(summaryRow.TotalCount ?? 0) : 0;
        var thongKe = new CongNoThongKe
        {
            TongNoPhaiThu = summaryRow != null ? (decimal)(summaryRow.TongNoPhaiThu ?? 0m) : 0m,
            TongNoPhaiTra = summaryRow != null ? (decimal)(summaryRow.TongNoPhaiTra ?? 0m) : 0m,
            TongDaThanhToan = summaryRow != null ? (decimal)(summaryRow.TongDaThanhToan ?? 0m) : 0m
        };

        // 2. Lấy dữ liệu của trang hiện tại
        var pagedSql = $"""
        SELECT *
        FROM (
            {fullUnion}
        ) AS T
        ORDER BY NgayPhatSinh DESC
        LIMIT @Limit OFFSET @Offset;
        """;

        var rows = (await connection.QueryAsync<CongNoItemRow>(pagedSql, queryParams)).ToList();

        var stt = Math.Max(0, (criteria.Page - 1) * criteria.PageSize);
        foreach (var r in rows)
        {
            r.SoThuTu = ++stt;
        }

        return (rows, totalCount, thongKe);
    }

    /// <summary>
    /// Lấy sổ cái công nợ của một khách hàng, mới nhất trước.
    /// </summary>
    public async Task<IReadOnlyList<LichSuCongNoKhachHang>> GetLichSuCongNoKhachHangAsync(
        uint khachHangId,
        int limit = 300)
    {
        var safeLimit = limit < 1 ? 300 : Math.Min(limit, 2000);

        const string sql = """
        SELECT
            ls.id AS Id,
            ls.loai_giao_dich AS LoaiGiaoDich,
            ls.loai_chung_tu AS LoaiChungTu,
            ls.ma_chung_tu AS MaChungTu,
            ls.ghi_no AS GhiNo,
            ls.ghi_co AS GhiCo,
            ls.so_du AS SoDu,
            ls.thoi_gian AS ThoiGian,
            ls.ghi_chu AS GhiChu
        FROM lich_su_cong_no_khach_hang ls
        WHERE ls.khach_hang_id = @KhachHangId
        ORDER BY ls.thoi_gian DESC, ls.id DESC
        LIMIT @Limit;
        """;

        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<LichSuCongNoKhachHang>(
            sql, new { KhachHangId = khachHangId, Limit = safeLimit })).AsList();
    }

    /// <summary>
    /// Ghi nhận điều chỉnh công nợ khách hàng (tăng hoặc giảm) vào bảng điều chỉnh
    /// và sổ cái công nợ trong cùng một giao dịch.
    /// </summary>
    public async Task<bool> LuuDieuChinhCongNoAsync(
        uint khachHangId,
        decimal soTien,
        bool tangNo,
        string lyDo)
    {
        Quyen.RequirePermission(
            Session.CurrentUser,
            PermissionKeys.CustomersDebtPay,
            "Bạn không có quyền điều chỉnh công nợ khách hàng.");

        if (khachHangId == 0)
            throw new InvalidOperationException("Thiếu khách hàng cần điều chỉnh công nợ.");

        if (soTien <= 0)
            throw new InvalidOperationException("Số tiền điều chỉnh phải lớn hơn 0.");

        if (string.IsNullOrWhiteSpace(lyDo))
            throw new InvalidOperationException("Vui lòng nhập lý do điều chỉnh công nợ.");

        var now = DateTime.Now;
        var userId = Session.CurrentUser?.Id;
        var coCotKiotViet = await DamBaoCotDuLieuKiotVietAsync();
        var maDieuChinh = $"DC_{now:yyyyMMddHHmmssfff}_{khachHangId}";
        var loaiDieuChinh = tangNo ? "DIEU_CHINH_TANG" : "DIEU_CHINH_GIAM";
        var ghiNo = tangNo ? soTien : 0m;
        var ghiCo = tangNo ? 0m : soTien;

        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string khachSql = """
            SELECT id, chi_nhanh_id
            FROM khach_hang
            WHERE id = @Id
            LIMIT 1
            FOR UPDATE;
            """;
            var khach = await connection.QueryFirstOrDefaultAsync<dynamic>(
                khachSql, new { Id = khachHangId }, transaction);

            if (khach is null)
                throw new InvalidOperationException("Không tìm thấy khách hàng cần điều chỉnh công nợ.");

            uint chiNhanhId = (uint?)khach.chi_nhanh_id
                ?? Session.CurrentUser?.ChiNhanhId
                ?? 1;

            if (!tangNo)
            {
                var bieuThucNo = TaoBieuThucTienKhachHang(false, "kh.id", coCotKiotViet).NoHienTai;
                var noHienTai = await connection.ExecuteScalarAsync<decimal>(
                    $"SELECT {bieuThucNo} FROM khach_hang kh WHERE kh.id = @Id;",
                    new { Id = khachHangId },
                    transaction);

                if (soTien - noHienTai > 0.009m)
                {
                    throw new InvalidOperationException(
                        $"Số tiền giảm ({soTien:N0} đ) vượt quá dư nợ hiện tại ({noHienTai:N0} đ).");
                }
            }

            await connection.ExecuteAsync("""
            INSERT INTO dieu_chinh_cong_no
            (
                ma_dieu_chinh, khach_hang_id, chi_nhanh_id, so_tien,
                loai_dieu_chinh, ly_do, nguoi_thuc_hien_id, thoi_gian
            )
            VALUES
            (
                @MaDieuChinh, @KhachHangId, @ChiNhanhId, @SoTien,
                @LoaiDieuChinh, @LyDo, @NguoiThucHienId, @ThoiGian
            );
            """,
            new
            {
                MaDieuChinh = maDieuChinh,
                KhachHangId = khachHangId,
                ChiNhanhId = chiNhanhId,
                SoTien = soTien,
                LoaiDieuChinh = loaiDieuChinh,
                LyDo = lyDo.Trim(),
                NguoiThucHienId = userId,
                ThoiGian = now
            }, transaction);

            const string soDuSql = """
            SELECT COALESCE(SUM(ghi_no - ghi_co), 0)
            FROM lich_su_cong_no_khach_hang
            WHERE khach_hang_id = @KhachHangId
            FOR UPDATE;
            """;
            var soDuCu = await connection.ExecuteScalarAsync<decimal>(
                soDuSql, new { KhachHangId = khachHangId }, transaction);

            await connection.ExecuteAsync("""
            INSERT INTO lich_su_cong_no_khach_hang
            (
                khach_hang_id, chi_nhanh_id, loai_giao_dich, loai_chung_tu,
                ma_chung_tu, ghi_no, ghi_co, so_du,
                thoi_gian, nguoi_thuc_hien_id, ghi_chu
            )
            VALUES
            (
                @KhachHangId, @ChiNhanhId, @LoaiGiaoDich, 'DIEU_CHINH',
                @MaChungTu, @GhiNo, @GhiCo, @SoDu,
                @ThoiGian, @NguoiThucHienId, @GhiChu
            );
            """,
            new
            {
                KhachHangId = khachHangId,
                ChiNhanhId = chiNhanhId,
                LoaiGiaoDich = loaiDieuChinh,
                MaChungTu = maDieuChinh,
                GhiNo = ghiNo,
                GhiCo = ghiCo,
                SoDu = soDuCu + ghiNo - ghiCo,
                ThoiGian = now,
                NguoiThucHienId = userId,
                GhiChu = lyDo.Trim()
            }, transaction);

            await transaction.CommitAsync();

            _ = LogSubAccountActivityAsync(
                "Điều chỉnh công nợ",
                "KHACH_HANG",
                khachHangId,
                maDieuChinh,
                $"{(tangNo ? "Tăng" : "Giảm")} công nợ {soTien:N0} đ. Lý do: {lyDo.Trim()}",
                soTien);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            SafeLog.WriteError("Lỗi LuuDieuChinhCongNoAsync", ex);
            throw;
        }
    }
}
