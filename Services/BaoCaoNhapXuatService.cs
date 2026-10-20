using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dịch vụ dữ liệu cho:
/// - Giao diện 63: Báo cáo tổng hợp nhập xuất (Xuất - Nhập - Tồn kho)
/// - Giao diện 64: Báo cáo chi tiết nhập/xuất (Thẻ kho / Sổ chi tiết hàng hóa)
/// </summary>
public sealed class BaoCaoNhapXuatService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;
    private const string MauHeaderBg = "#087F6E";
    private const string DinhDangTien = "#,##0\" đ\"";
    private const string DinhDangSo = "#,##0";

    public BaoCaoNhapXuatService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    public static (DateTime TuNgay, DateTime DenKeTiep) TinhKhoang(DateTime tuNgay, DateTime denNgay)
    {
        var tu = tuNgay.Date;
        var denKeTiep = denNgay.Date.AddDays(1);
        return (tu, denKeTiep);
    }

    #region TẢI DANH MỤC LỌC

    /// <summary>
    /// Danh sách nhóm hàng cho ComboBox lọc.
    /// </summary>
    public async Task<List<BaoCaoNhapXuatNhomOption>> TaiNhomHangAsync(CancellationToken token = default)
    {
        await using var conn = await _taoKetNoi();
        var rs = await conn.QueryAsync<(uint Id, string TenNhom)>(new CommandDefinition(
            @"SELECT id, ten_nhom FROM nhom_san_pham WHERE trang_thai = 1 ORDER BY ten_nhom;",
            cancellationToken: token));

        var list = new List<BaoCaoNhapXuatNhomOption>
        {
            new() { Id = -1, TenNhom = "", TenHienThi = "Tất cả nhóm hàng" }
        };

        foreach (var (id, ten) in rs)
        {
            list.Add(new BaoCaoNhapXuatNhomOption { Id = (int)id, TenNhom = ten, TenHienThi = ten });
        }

        list.Add(new BaoCaoNhapXuatNhomOption { Id = 0, TenNhom = "", TenHienThi = "Chưa phân nhóm" });
        return list;
    }

    /// <summary>
    /// Danh sách sản phẩm cho ComboBox chọn mặt hàng (dùng cho Thẻ kho / Chi tiết).
    /// </summary>
    public async Task<List<BaoCaoNhapXuatSanPhamOption>> TaiDanhSachSanPhamOptionAsync(
        int nhomId = -1,
        string? timKiem = null,
        CancellationToken token = default)
    {
        await using var conn = await _taoKetNoi();
        var sql = new StringBuilder("SELECT id, ma_san_pham, ten_san_pham FROM san_pham WHERE 1=1");
        var p = new DynamicParameters();

        if (nhomId > 0)
        {
            sql.Append(" AND nhom_san_pham_id = @NhomId");
            p.Add("NhomId", nhomId);
        }
        else if (nhomId == 0)
        {
            sql.Append(" AND (nhom_san_pham_id IS NULL OR nhom_san_pham_id = 0)");
        }

        if (!string.IsNullOrWhiteSpace(timKiem))
        {
            sql.Append(" AND (ma_san_pham LIKE @Search OR ten_san_pham LIKE @Search)");
            p.Add("Search", $"%{timKiem.Trim()}%");
        }

        sql.Append(" ORDER BY ten_san_pham LIMIT 300;");

        var rows = await conn.QueryAsync<(uint Id, string Ma, string Ten)>(
            new CommandDefinition(sql.ToString(), p, cancellationToken: token));

        var result = new List<BaoCaoNhapXuatSanPhamOption>
        {
            new() { Id = 0, MaSanPham = "", TenSanPham = "Tất cả mặt hàng" }
        };

        foreach (var (id, ma, ten) in rows)
        {
            result.Add(new BaoCaoNhapXuatSanPhamOption
            {
                Id = id,
                MaSanPham = ma ?? "",
                TenSanPham = ten ?? ""
            });
        }

        return result;
    }

    private static async Task<bool> KiemTraBangTonTaiAsync(MySqlConnection conn, string tableName)
    {
        try
        {
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @Name;",
                new { Name = tableName });
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region GIAO DIỆN 63 – BÁO CÁO TỔNG HỢP NHẬP XUẤT

    /// <summary>
    /// Tải dữ liệu Báo cáo tổng hợp xuất nhập tồn theo sản phẩm trong khoảng thời gian.
    /// Tự động tổng hợp chính xác theo quy tắc kế toán: Tồn cuối = Tồn đầu + Tổng nhập - Tổng xuất.
    /// </summary>
    public async Task<BaoCaoTongHopNhapXuatDuLieu> TaiTongHopNhapXuatAsync(
        DateTime tuNgay,
        DateTime denNgay,
        int nhomId = -1,
        string? timKiem = null,
        bool chiHienCoPhatSinhHoacTon = true,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);
        await using var conn = await _taoKetNoi();

        bool hasBangTraHang = await KiemTraBangTonTaiAsync(conn, "phieu_tra_hang");
        bool hasBangNhapKhac = await KiemTraBangTonTaiAsync(conn, "phieu_nhap_khac");
        bool hasBangXuatNoiBo = await KiemTraBangTonTaiAsync(conn, "phieu_xuat_noi_bo");
        bool hasBangXuatTraNcc = await KiemTraBangTonTaiAsync(conn, "phieu_xuat_tra_ncc");

        // 1. Lấy danh sách sản phẩm theo bộ lọc nhóm & tìm kiếm
        var sqlSp = new StringBuilder("""
            SELECT
                sp.id                                                    AS SanPhamId,
                sp.ma_san_pham                                           AS MaSanPham,
                sp.ten_san_pham                                          AS TenSanPham,
                COALESCE(nsp.ten_nhom, 'Chưa phân nhóm')                AS TenNhom,
                COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái')          AS DonViTinh,
                COALESCE(sp.gia_von, 0)                                  AS GiaVon,
                COALESCE(tk.so_luong_ton, sp.ton_kho_excel, 0)           AS TonHienTai
            FROM san_pham sp
            LEFT JOIN nhom_san_pham nsp ON nsp.id = sp.nhom_san_pham_id
            LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
            LEFT JOIN ton_kho tk ON tk.san_pham_id = sp.id
            WHERE 1=1
        """);

        var pSp = new DynamicParameters();

        if (nhomId > 0)
        {
            sqlSp.Append(" AND sp.nhom_san_pham_id = @NhomId");
            pSp.Add("NhomId", nhomId);
        }
        else if (nhomId == 0)
        {
            sqlSp.Append(" AND (sp.nhom_san_pham_id IS NULL OR sp.nhom_san_pham_id = 0)");
        }

        if (!string.IsNullOrWhiteSpace(timKiem))
        {
            sqlSp.Append(" AND (sp.ma_san_pham LIKE @Search OR sp.ten_san_pham LIKE @Search)");
            pSp.Add("Search", $"%{timKiem.Trim()}%");
        }

        sqlSp.Append(" ORDER BY nsp.ten_nhom, sp.ten_san_pham;");

        var dsSanPham = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlSp.ToString(), pSp, cancellationToken: token))).AsList();

        // 2. Tổng hợp Nhập mua hàng trong kỳ và sau TuNgay
        const string sqlNhapMua = """
            SELECT
                ctpn.san_pham_id AS SanPhamId,
                COALESCE(SUM(CASE WHEN pn.thoi_gian_nhap >= @TuNgay AND pn.thoi_gian_nhap < @DenKeTiep THEN ctpn.so_luong ELSE 0 END), 0) AS NhapTrongKy,
                COALESCE(SUM(CASE WHEN pn.thoi_gian_nhap >= @TuNgay AND pn.thoi_gian_nhap < @DenKeTiep THEN ctpn.thanh_tien ELSE 0 END), 0) AS GiaTriNhapTrongKy,
                COALESCE(SUM(CASE WHEN pn.thoi_gian_nhap >= @TuNgay THEN ctpn.so_luong ELSE 0 END), 0) AS NhapSauTuNgay
            FROM chi_tiet_phieu_nhap_hang ctpn
            INNER JOIN phieu_nhap_hang pn ON pn.id = ctpn.phieu_nhap_hang_id
            WHERE pn.trang_thai != 'HUY' AND pn.thoi_gian_nhap >= @TuNgay
            GROUP BY ctpn.san_pham_id;
        """;

        var nhapMuaDict = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlNhapMua, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)))
            .ToDictionary(
                r => (uint)Convert.ToUInt32(r.SanPhamId),
                r => (
                    NhapTrongKy: Convert.ToDecimal(r.NhapTrongKy),
                    GiaTriTrongKy: Convert.ToDecimal(r.GiaTriNhapTrongKy),
                    NhapSau: Convert.ToDecimal(r.NhapSauTuNgay)
                ));

        // 3. Tổng hợp Bán hàng trong kỳ và sau TuNgay
        const string sqlXuatBan = """
            SELECT
                cthd.san_pham_id AS SanPhamId,
                COALESCE(SUM(CASE WHEN hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep THEN cthd.so_luong ELSE 0 END), 0) AS XuatTrongKy,
                COALESCE(SUM(CASE WHEN hd.thoi_gian_ban >= @TuNgay AND hd.thoi_gian_ban < @DenKeTiep THEN cthd.so_luong * COALESCE(cthd.gia_von, 0) ELSE 0 END), 0) AS GiaTriXuatTrongKy,
                COALESCE(SUM(CASE WHEN hd.thoi_gian_ban >= @TuNgay THEN cthd.so_luong ELSE 0 END), 0) AS XuatSauTuNgay
            FROM chi_tiet_hoa_don_ban_hang cthd
            INNER JOIN hoa_don_ban_hang hd ON hd.id = cthd.hoa_don_id
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH' AND hd.thoi_gian_ban >= @TuNgay
            GROUP BY cthd.san_pham_id;
        """;

        var xuatBanDict = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlXuatBan, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token)))
            .ToDictionary(
                r => (uint)Convert.ToUInt32(r.SanPhamId),
                r => (
                    XuatTrongKy: Convert.ToDecimal(r.XuatTrongKy),
                    GiaTriTrongKy: Convert.ToDecimal(r.GiaTriXuatTrongKy),
                    XuatSau: Convert.ToDecimal(r.XuatSauTuNgay)
                ));

        // 4. Tổng hợp Khách trả hàng (nếu bảng tồn tại)
        var nhapTraLaiDict = new Dictionary<uint, (decimal NhapTrongKy, decimal GiaTriTrongKy, decimal NhapSau)>();
        if (hasBangTraHang)
        {
            try
            {
                const string sqlTraHang = """
                    SELECT
                        ctpt.san_pham_id AS SanPhamId,
                        COALESCE(SUM(CASE WHEN pt.thoi_gian_tra >= @TuNgay AND pt.thoi_gian_tra < @DenKeTiep THEN ctpt.so_luong ELSE 0 END), 0) AS NhapTrongKy,
                        COALESCE(SUM(CASE WHEN pt.thoi_gian_tra >= @TuNgay AND pt.thoi_gian_tra < @DenKeTiep THEN ctpt.thanh_tien ELSE 0 END), 0) AS GiaTriTrongKy,
                        COALESCE(SUM(CASE WHEN pt.thoi_gian_tra >= @TuNgay THEN ctpt.so_luong ELSE 0 END), 0) AS NhapSauTuNgay
                    FROM chi_tiet_phieu_tra_hang ctpt
                    INNER JOIN phieu_tra_hang pt ON pt.id = ctpt.phieu_tra_hang_id
                    WHERE pt.trang_thai = 'HOAN_THANH' AND pt.thoi_gian_tra >= @TuNgay
                    GROUP BY ctpt.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlTraHang, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    nhapTraLaiDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToDecimal(r.NhapTrongKy),
                        Convert.ToDecimal(r.GiaTriTrongKy),
                        Convert.ToDecimal(r.NhapSauTuNgay)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_tra_hang trong TaiTongHopNhapXuatAsync", ex);
            }
        }

        // 5. Tổng hợp Nhập khác (nếu bảng tồn tại)
        var nhapKhacDict = new Dictionary<uint, (decimal NhapTrongKy, decimal GiaTriTrongKy, decimal NhapSau)>();
        if (hasBangNhapKhac)
        {
            try
            {
                const string sqlNhapKhac = """
                    SELECT
                        ct.san_pham_id AS SanPhamId,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep THEN ct.so_luong ELSE 0 END), 0) AS NhapTrongKy,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep THEN ct.so_luong * COALESCE(ct.don_gia, 0) ELSE 0 END), 0) AS GiaTriTrongKy,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay THEN ct.so_luong ELSE 0 END), 0) AS NhapSauTuNgay
                    FROM chi_tiet_phieu_nhap_khac ct
                    INNER JOIN phieu_nhap_khac p ON p.id = ct.phieu_nhap_khac_id
                    WHERE p.thoi_gian >= @TuNgay
                    GROUP BY ct.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlNhapKhac, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    nhapKhacDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToDecimal(r.NhapTrongKy),
                        Convert.ToDecimal(r.GiaTriTrongKy),
                        Convert.ToDecimal(r.NhapSauTuNgay)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_nhap_khac", ex);
            }
        }

        // 6. Tổng hợp Xuất trả NCC (nếu bảng tồn tại)
        var xuatTraNccDict = new Dictionary<uint, (decimal XuatTrongKy, decimal GiaTriTrongKy, decimal XuatSau)>();
        if (hasBangXuatTraNcc)
        {
            try
            {
                const string sqlXuatTraNcc = """
                    SELECT
                        ct.san_pham_id AS SanPhamId,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep THEN ct.so_luong ELSE 0 END), 0) AS XuatTrongKy,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep THEN ct.so_luong * COALESCE(ct.don_gia, 0) ELSE 0 END), 0) AS GiaTriTrongKy,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay THEN ct.so_luong ELSE 0 END), 0) AS XuatSauTuNgay
                    FROM chi_tiet_phieu_xuat_tra_ncc ct
                    INNER JOIN phieu_xuat_tra_ncc p ON p.id = ct.phieu_xuat_tra_ncc_id
                    WHERE p.thoi_gian >= @TuNgay
                    GROUP BY ct.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlXuatTraNcc, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    xuatTraNccDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToDecimal(r.XuatTrongKy),
                        Convert.ToDecimal(r.GiaTriTrongKy),
                        Convert.ToDecimal(r.XuatSauTuNgay)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_xuat_tra_ncc", ex);
            }
        }

        // 7. Tổng hợp Xuất nội bộ (nếu bảng tồn tại)
        var xuatNoiBoDict = new Dictionary<uint, (decimal XuatTrongKy, decimal GiaTriTrongKy, decimal XuatSau)>();
        if (hasBangXuatNoiBo)
        {
            try
            {
                const string sqlXuatNoiBo = """
                    SELECT
                        ct.san_pham_id AS SanPhamId,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep THEN ct.so_luong ELSE 0 END), 0) AS XuatTrongKy,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay AND p.thoi_gian < @DenKeTiep THEN ct.so_luong * COALESCE(ct.don_gia, 0) ELSE 0 END), 0) AS GiaTriTrongKy,
                        COALESCE(SUM(CASE WHEN p.thoi_gian >= @TuNgay THEN ct.so_luong ELSE 0 END), 0) AS XuatSauTuNgay
                    FROM chi_tiet_phieu_xuat_noi_bo ct
                    INNER JOIN phieu_xuat_noi_bo p ON p.id = ct.phieu_xuat_noi_bo_id
                    WHERE p.thoi_gian >= @TuNgay
                    GROUP BY ct.san_pham_id;
                """;

                var rows = await conn.QueryAsync<dynamic>(
                    new CommandDefinition(sqlXuatNoiBo, new { TuNgay = tu, DenKeTiep = denKeTiep }, cancellationToken: token));

                foreach (var r in rows)
                {
                    xuatNoiBoDict[(uint)Convert.ToUInt32(r.SanPhamId)] = (
                        Convert.ToDecimal(r.XuatTrongKy),
                        Convert.ToDecimal(r.GiaTriTrongKy),
                        Convert.ToDecimal(r.XuatSauTuNgay)
                    );
                }
            }
            catch (Exception ex)
            {
                SafeLog.WriteError("Lỗi đọc phieu_xuat_noi_bo", ex);
            }
        }

        // 8. Ghép nối tính toán từng sản phẩm
        var items = new List<BaoCaoTongHopNhapXuatItem>();
        int stt = 1;

        foreach (var sp in dsSanPham)
        {
            uint id = Convert.ToUInt32(sp.SanPhamId);
            string ma = Convert.ToString(sp.MaSanPham) ?? "";
            string ten = Convert.ToString(sp.TenSanPham) ?? "";
            string tenNhom = Convert.ToString(sp.TenNhom) ?? "";
            string dvt = Convert.ToString(sp.DonViTinh) ?? "";
            decimal giaVon = Convert.ToDecimal(sp.GiaVon);
            decimal tonHienTai = Convert.ToDecimal(sp.TonHienTai);

            // Nhập
            nhapMuaDict.TryGetValue(id, out var nm);
            nhapTraLaiDict.TryGetValue(id, out var ntl);
            nhapKhacDict.TryGetValue(id, out var nk);

            decimal nhapMua = nm.NhapTrongKy;
            decimal gtNhapMua = nm.GiaTriTrongKy;

            decimal nhapTraLai = ntl.NhapTrongKy;
            decimal gtNhapTraLai = ntl.GiaTriTrongKy;

            decimal nhapKhac = nk.NhapTrongKy;
            decimal gtNhapKhac = nk.GiaTriTrongKy;

            decimal tongNhap = nhapMua + nhapTraLai + nhapKhac;
            decimal tongGtNhap = gtNhapMua + gtNhapTraLai + gtNhapKhac;

            decimal tongNhapSauTuNgay = nm.NhapSau + ntl.NhapSau + nk.NhapSau;

            // Xuất
            xuatBanDict.TryGetValue(id, out var xb);
            xuatTraNccDict.TryGetValue(id, out var xt);
            xuatNoiBoDict.TryGetValue(id, out var xnb);

            decimal xuatBan = xb.XuatTrongKy;
            decimal gtXuatBan = xb.GiaTriTrongKy > 0 ? xb.GiaTriTrongKy : xuatBan * giaVon;

            decimal xuatTraNcc = xt.XuatTrongKy;
            decimal gtXuatTraNcc = xt.GiaTriTrongKy > 0 ? xt.GiaTriTrongKy : xuatTraNcc * giaVon;

            decimal xuatNoiBo = xnb.XuatTrongKy;
            decimal gtXuatNoiBo = xnb.GiaTriTrongKy > 0 ? xnb.GiaTriTrongKy : xuatNoiBo * giaVon;

            decimal tongXuat = xuatBan + xuatTraNcc + xuatNoiBo;
            decimal tongGtXuat = gtXuatBan + gtXuatTraNcc + gtXuatNoiBo;

            decimal tongXuatSauTuNgay = xb.XuatSau + xt.XuatSau + xnb.XuatSau;

            // Tồn đầu kỳ = Tồn hiện tại - Toàn bộ phát sinh nhập sau TuNgay + Toàn bộ phát sinh xuất sau TuNgay
            decimal tonDauKy = tonHienTai - tongNhapSauTuNgay + tongXuatSauTuNgay;
            decimal gtDauKy = tonDauKy * giaVon;

            // Tồn cuối kỳ = Tồn đầu kỳ + Tổng nhập trong kỳ - Tổng xuất trong kỳ
            decimal tonCuoiKy = tonDauKy + tongNhap - tongXuat;
            decimal gtCuoiKy = tonCuoiKy * giaVon;

            // Bộ lọc chỉ hiện hàng có phát sinh hoặc còn tồn
            if (chiHienCoPhatSinhHoacTon)
            {
                if (tonDauKy == 0 && tongNhap == 0 && tongXuat == 0 && tonCuoiKy == 0)
                {
                    continue;
                }
            }

            items.Add(new BaoCaoTongHopNhapXuatItem
            {
                Stt = stt++,
                SanPhamId = id,
                MaSanPham = ma,
                TenSanPham = ten,
                TenNhom = tenNhom,
                DonViTinh = dvt,

                TonDauKy = tonDauKy,
                GiaTriDauKy = gtDauKy,

                NhapMua = nhapMua,
                GiaTriNhapMua = gtNhapMua,
                NhapTraLai = nhapTraLai,
                GiaTriNhapTraLai = gtNhapTraLai,
                NhapKhac = nhapKhac,
                GiaTriNhapKhac = gtNhapKhac,
                TongNhap = tongNhap,
                GiaTriNhap = tongGtNhap,

                XuatBan = xuatBan,
                GiaTriXuatBan = gtXuatBan,
                XuatTraNcc = xuatTraNcc,
                GiaTriXuatTraNcc = gtXuatTraNcc,
                XuatNoiBo = xuatNoiBo,
                GiaTriXuatNoiBo = gtXuatNoiBo,
                TongXuat = tongXuat,
                GiaTriXuat = tongGtXuat,

                TonCuoiKy = tonCuoiKy,
                GiaVon = giaVon,
                GiaTriTonCuoi = gtCuoiKy
            });
        }

        return new BaoCaoTongHopNhapXuatDuLieu
        {
            TuNgay = tu,
            DenNgay = denKeTiep.AddDays(-1),
            TongSoMatHang = items.Count,
            TongTonDau = items.Sum(i => i.TonDauKy),
            TongGiaTriDau = items.Sum(i => i.GiaTriDauKy),
            TongNhap = items.Sum(i => i.TongNhap),
            TongGiaTriNhap = items.Sum(i => i.GiaTriNhap),
            TongXuat = items.Sum(i => i.TongXuat),
            TongGiaTriXuat = items.Sum(i => i.GiaTriXuat),
            TongTonCuoi = items.Sum(i => i.TonCuoiKy),
            TongGiaTriTonCuoi = items.Sum(i => i.GiaTriTonCuoi),
            Items = items
        };
    }

    #endregion

    #region GIAO DIỆN 64 – BÁO CÁO CHI TIẾT NHẬP/XUẤT (THẺ KHO)

    /// <summary>
    /// Tải dữ liệu Báo cáo chi tiết các giao dịch nhập xuất (Thẻ kho) có phân trang.
    /// </summary>
    public async Task<BaoCaoChiTietNhapXuatDuLieu> TaiChiTietNhapXuatPagedAsync(
        DateTime tuNgay,
        DateTime denNgay,
        string? loaiChungTuLoc = null,
        uint? sanPhamId = null,
        int nhomId = -1,
        string? timKiem = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken token = default)
    {
        var (tu, denKeTiep) = TinhKhoang(tuNgay, denNgay);
        await using var conn = await _taoKetNoi();

        bool hasBangTraHang = await KiemTraBangTonTaiAsync(conn, "phieu_tra_hang");
        bool hasBangNhapKhac = await KiemTraBangTonTaiAsync(conn, "phieu_nhap_khac");
        bool hasBangXuatNoiBo = await KiemTraBangTonTaiAsync(conn, "phieu_xuat_noi_bo");
        bool hasBangXuatTraNcc = await KiemTraBangTonTaiAsync(conn, "phieu_xuat_tra_ncc");

        var queries = new List<string>();

        // 1. Nhập mua hàng
        if (string.IsNullOrWhiteSpace(loaiChungTuLoc) || loaiChungTuLoc == "TAT_CA" || loaiChungTuLoc == "NHAP_HANG")
        {
            queries.Add("""
                SELECT
                    pn.thoi_gian_nhap                       AS ThoiGian,
                    pn.ma_phieu_nhap                        AS MaChungTu,
                    'NHAP'                                  AS LoaiGiaoDich,
                    'NHAP_HANG'                             AS LoaiChungTu,
                    '📥 Nhập mua hàng'                     AS LoaiChungTuHienThi,
                    '#087F6E'                               AS MauLoaiChungTu,
                    '#E7F8F0'                               AS MauNenLoaiChungTu,
                    sp.id                                   AS SanPhamId,
                    sp.ma_san_pham                          AS MaSanPham,
                    sp.ten_san_pham                         AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái') AS DonViTinh,
                    COALESCE(ncc.ten_nha_cung_cap, 'Nhà cung cấp') AS DoiTac,
                    ctpn.so_luong                           AS SoLuongNhap,
                    ctpn.don_gia                            AS DonGiaNhap,
                    ctpn.thanh_tien                         AS ThanhTienNhap,
                    NULL                                    AS SoLuongXuat,
                    NULL                                    AS DonGiaXuat,
                    NULL                                    AS ThanhTienXuat,
                    COALESCE(pn.ghi_chu, '')                AS GhiChu,
                    sp.nhom_san_pham_id                     AS NhomSanPhamId
                FROM chi_tiet_phieu_nhap_hang ctpn
                INNER JOIN phieu_nhap_hang pn ON pn.id = ctpn.phieu_nhap_hang_id
                INNER JOIN san_pham sp ON sp.id = ctpn.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                LEFT JOIN nha_cung_cap ncc ON ncc.id = pn.nha_cung_cap_id
                WHERE pn.trang_thai != 'HUY'
                  AND pn.thoi_gian_nhap >= @TuNgay
                  AND pn.thoi_gian_nhap < @DenKeTiep
            """);
        }

        // 2. Bán hàng
        if (string.IsNullOrWhiteSpace(loaiChungTuLoc) || loaiChungTuLoc == "TAT_CA" || loaiChungTuLoc == "BAN_HANG")
        {
            queries.Add("""
                SELECT
                    hd.thoi_gian_ban                        AS ThoiGian,
                    hd.ma_hoa_don                           AS MaChungTu,
                    'XUAT'                                  AS LoaiGiaoDich,
                    'BAN_HANG'                              AS LoaiChungTu,
                    '📤 Bán lẻ'                             AS LoaiChungTuHienThi,
                    '#D92D20'                               AS MauLoaiChungTu,
                    '#FEF3F2'                               AS MauNenLoaiChungTu,
                    sp.id                                   AS SanPhamId,
                    sp.ma_san_pham                          AS MaSanPham,
                    sp.ten_san_pham                         AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái') AS DonViTinh,
                    COALESCE(hd.ten_khach_hang, 'Khách lẻ') AS DoiTac,
                    NULL                                    AS SoLuongNhap,
                    NULL                                    AS DonGiaNhap,
                    NULL                                    AS ThanhTienNhap,
                    cthd.so_luong                           AS SoLuongXuat,
                    cthd.gia_ban                            AS DonGiaXuat,
                    cthd.thanh_tien                         AS ThanhTienXuat,
                    COALESCE(hd.ghi_chu, '')                AS GhiChu,
                    sp.nhom_san_pham_id                     AS NhomSanPhamId
                FROM chi_tiet_hoa_don_ban_hang cthd
                INNER JOIN hoa_don_ban_hang hd ON hd.id = cthd.hoa_don_id
                INNER JOIN san_pham sp ON sp.id = cthd.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
                  AND hd.thoi_gian_ban >= @TuNgay
                  AND hd.thoi_gian_ban < @DenKeTiep
            """);
        }

        // 3. Khách trả hàng
        if (hasBangTraHang && (string.IsNullOrWhiteSpace(loaiChungTuLoc) || loaiChungTuLoc == "TAT_CA" || loaiChungTuLoc == "KHACH_TRA"))
        {
            queries.Add("""
                SELECT
                    pt.thoi_gian_tra                        AS ThoiGian,
                    pt.ma_phieu_tra                         AS MaChungTu,
                    'NHAP'                                  AS LoaiGiaoDich,
                    'KHACH_TRA'                             AS LoaiChungTu,
                    '↩ Khách trả hàng'                     AS LoaiChungTuHienThi,
                    '#1877F2'                               AS MauLoaiChungTu,
                    '#EAF1FF'                               AS MauNenLoaiChungTu,
                    sp.id                                   AS SanPhamId,
                    sp.ma_san_pham                          AS MaSanPham,
                    sp.ten_san_pham                         AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái') AS DonViTinh,
                    COALESCE(kh.ten_khach_hang, 'Khách hàng') AS DoiTac,
                    ctpt.so_luong                           AS SoLuongNhap,
                    ctpt.don_gia_tra                        AS DonGiaNhap,
                    ctpt.thanh_tien                         AS ThanhTienNhap,
                    NULL                                    AS SoLuongXuat,
                    NULL                                    AS DonGiaXuat,
                    NULL                                    AS ThanhTienXuat,
                    COALESCE(pt.ghi_chu, '')                AS GhiChu,
                    sp.nhom_san_pham_id                     AS NhomSanPhamId
                FROM chi_tiet_phieu_tra_hang ctpt
                INNER JOIN phieu_tra_hang pt ON pt.id = ctpt.phieu_tra_hang_id
                INNER JOIN san_pham sp ON sp.id = ctpt.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                LEFT JOIN khach_hang kh ON kh.id = pt.khach_hang_id
                WHERE pt.trang_thai = 'HOAN_THANH'
                  AND pt.thoi_gian_tra >= @TuNgay
                  AND pt.thoi_gian_tra < @DenKeTiep
            """);
        }

        // 4. Nhập khác
        if (hasBangNhapKhac && (string.IsNullOrWhiteSpace(loaiChungTuLoc) || loaiChungTuLoc == "TAT_CA" || loaiChungTuLoc == "NHAP_KHAC"))
        {
            queries.Add("""
                SELECT
                    pnk.thoi_gian                           AS ThoiGian,
                    pnk.ma_phieu                            AS MaChungTu,
                    'NHAP'                                  AS LoaiGiaoDich,
                    'NHAP_KHAC'                             AS LoaiChungTu,
                    '📥 Nhập khác'                          AS LoaiChungTuHienThi,
                    '#099250'                               AS MauLoaiChungTu,
                    '#EDFPF6'                               AS MauNenLoaiChungTu,
                    sp.id                                   AS SanPhamId,
                    sp.ma_san_pham                          AS MaSanPham,
                    sp.ten_san_pham                         AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái') AS DonViTinh,
                    'Nhập kho khác'                         AS DoiTac,
                    ctnk.so_luong                           AS SoLuongNhap,
                    COALESCE(ctnk.don_gia, 0)               AS DonGiaNhap,
                    ctnk.so_luong * COALESCE(ctnk.don_gia, 0) AS ThanhTienNhap,
                    NULL                                    AS SoLuongXuat,
                    NULL                                    AS DonGiaXuat,
                    NULL                                    AS ThanhTienXuat,
                    COALESCE(pnk.ghi_chu, '')               AS GhiChu,
                    sp.nhom_san_pham_id                     AS NhomSanPhamId
                FROM chi_tiet_phieu_nhap_khac ctnk
                INNER JOIN phieu_nhap_khac pnk ON pnk.id = ctnk.phieu_nhap_khac_id
                INNER JOIN san_pham sp ON sp.id = ctnk.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE pnk.thoi_gian >= @TuNgay
                  AND pnk.thoi_gian < @DenKeTiep
            """);
        }

        // 5. Xuất trả NCC
        if (hasBangXuatTraNcc && (string.IsNullOrWhiteSpace(loaiChungTuLoc) || loaiChungTuLoc == "TAT_CA" || loaiChungTuLoc == "TRA_NCC"))
        {
            queries.Add("""
                SELECT
                    pxt.thoi_gian                           AS ThoiGian,
                    pxt.ma_phieu                            AS MaChungTu,
                    'XUAT'                                  AS LoaiGiaoDich,
                    'TRA_NCC'                               AS LoaiChungTu,
                    '🔄 Xuất trả NCC'                       AS LoaiChungTuHienThi,
                    '#7A5AF8'                               AS MauLoaiChungTu,
                    '#F4F3FF'                               AS MauNenLoaiChungTu,
                    sp.id                                   AS SanPhamId,
                    sp.ma_san_pham                          AS MaSanPham,
                    sp.ten_san_pham                         AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái') AS DonViTinh,
                    'Nhà cung cấp'                          AS DoiTac,
                    NULL                                    AS SoLuongNhap,
                    NULL                                    AS DonGiaNhap,
                    NULL                                    AS ThanhTienNhap,
                    ctxt.so_luong                           AS SoLuongXuat,
                    COALESCE(ctxt.don_gia, 0)               AS DonGiaXuat,
                    ctxt.so_luong * COALESCE(ctxt.don_gia, 0) AS ThanhTienXuat,
                    COALESCE(pxt.ghi_chu, '')               AS GhiChu,
                    sp.nhom_san_pham_id                     AS NhomSanPhamId
                FROM chi_tiet_phieu_xuat_tra_ncc ctxt
                INNER JOIN phieu_xuat_tra_ncc pxt ON pxt.id = ctxt.phieu_xuat_tra_ncc_id
                INNER JOIN san_pham sp ON sp.id = ctxt.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE pxt.thoi_gian >= @TuNgay
                  AND pxt.thoi_gian < @DenKeTiep
            """);
        }

        // 6. Xuất nội bộ
        if (hasBangXuatNoiBo && (string.IsNullOrWhiteSpace(loaiChungTuLoc) || loaiChungTuLoc == "TAT_CA" || loaiChungTuLoc == "XUAT_NOI_BO"))
        {
            queries.Add("""
                SELECT
                    pxnb.thoi_gian                          AS ThoiGian,
                    pxnb.ma_phieu                           AS MaChungTu,
                    'XUAT'                                  AS LoaiGiaoDich,
                    'XUAT_NOI_BO'                           AS LoaiChungTu,
                    '📦 Xuất nội bộ'                        AS LoaiChungTuHienThi,
                    '#F79009'                               AS MauLoaiChungTu,
                    '#FFFAEB'                               AS MauNenLoaiChungTu,
                    sp.id                                   AS SanPhamId,
                    sp.ma_san_pham                          AS MaSanPham,
                    sp.ten_san_pham                         AS TenSanPham,
                    COALESCE(dvt.ten_don_vi, sp.don_vi_tinh, 'Cái') AS DonViTinh,
                    'Nội bộ / Hủy'                          AS DoiTac,
                    NULL                                    AS SoLuongNhap,
                    NULL                                    AS DonGiaNhap,
                    NULL                                    AS ThanhTienNhap,
                    ctxnb.so_luong                          AS SoLuongXuat,
                    COALESCE(ctxnb.don_gia, 0)              AS DonGiaXuat,
                    ctxnb.so_luong * COALESCE(ctxnb.don_gia, 0) AS ThanhTienXuat,
                    COALESCE(pxnb.ghi_chu, '')              AS GhiChu,
                    sp.nhom_san_pham_id                     AS NhomSanPhamId
                FROM chi_tiet_phieu_xuat_noi_bo ctxnb
                INNER JOIN phieu_xuat_noi_bo pxnb ON pxnb.id = ctxnb.phieu_xuat_noi_bo_id
                INNER JOIN san_pham sp ON sp.id = ctxnb.san_pham_id
                LEFT JOIN don_vi_tinh dvt ON dvt.id = sp.don_vi_tinh_id
                WHERE pxnb.thoi_gian >= @TuNgay
                  AND pxnb.thoi_gian < @DenKeTiep
            """);
        }

        if (queries.Count == 0)
        {
            return new BaoCaoChiTietNhapXuatDuLieu
            {
                TuNgay = tu,
                DenNgay = denKeTiep.AddDays(-1),
                Page = 1,
                PageSize = pageSize
            };
        }

        var fullUnion = string.Join("\nUNION ALL\n", queries);

        var filterSql = new StringBuilder("SELECT * FROM (" + fullUnion + ") AS t WHERE 1=1");
        var p = new DynamicParameters();
        p.Add("TuNgay", tu);
        p.Add("DenKeTiep", denKeTiep);

        if (sanPhamId.HasValue && sanPhamId.Value > 0)
        {
            filterSql.Append(" AND t.SanPhamId = @SanPhamId");
            p.Add("SanPhamId", sanPhamId.Value);
        }

        if (nhomId > 0)
        {
            filterSql.Append(" AND t.NhomSanPhamId = @NhomId");
            p.Add("NhomId", nhomId);
        }
        else if (nhomId == 0)
        {
            filterSql.Append(" AND (t.NhomSanPhamId IS NULL OR t.NhomSanPhamId = 0)");
        }

        if (!string.IsNullOrWhiteSpace(timKiem))
        {
            filterSql.Append(" AND (t.MaChungTu LIKE @Search OR t.MaSanPham LIKE @Search OR t.TenSanPham LIKE @Search OR t.DoiTac LIKE @Search)");
            p.Add("Search", $"%{timKiem.Trim()}%");
        }

        // Lấy tất cả các dòng khớp bộ lọc để tính KPI và chạy số dư lũy kế
        var sortSql = filterSql.ToString() + " ORDER BY t.ThoiGian ASC, t.MaChungTu ASC;";

        var allRows = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(sortSql, p, cancellationToken: token))).AsList();

        int totalCount = allRows.Count;
        decimal tongNhapSl = 0;
        decimal tongNhapTien = 0;
        decimal tongXuatSl = 0;
        decimal tongXuatTien = 0;

        // Nếu lọc 1 mặt hàng, tính số tồn đầu kỳ trước TuNgay để làm mốc chạy lũy kế thẻ kho
        decimal runningTon = 0;
        if (sanPhamId.HasValue && sanPhamId.Value > 0)
        {
            // Lấy tồn đầu kỳ của sản phẩm này tại mốc TuNgay
            var summarySp = await TaiTongHopNhapXuatAsync(tu, denKeTiep.AddDays(-1), -1, null, false, token);
            var spItem = summarySp.Items.FirstOrDefault(x => x.SanPhamId == sanPhamId.Value);
            runningTon = spItem?.TonDauKy ?? 0;
        }

        var allItems = new List<BaoCaoChiTietNhapXuatItem>();

        for (int i = 0; i < allRows.Count; i++)
        {
            var r = allRows[i];
            decimal? slNhap = r.SoLuongNhap != null ? Convert.ToDecimal(r.SoLuongNhap) : null;
            decimal? dgNhap = r.DonGiaNhap != null ? Convert.ToDecimal(r.DonGiaNhap) : null;
            decimal? ttNhap = r.ThanhTienNhap != null ? Convert.ToDecimal(r.ThanhTienNhap) : null;

            decimal? slXuat = r.SoLuongXuat != null ? Convert.ToDecimal(r.SoLuongXuat) : null;
            decimal? dgXuat = r.DonGiaXuat != null ? Convert.ToDecimal(r.DonGiaXuat) : null;
            decimal? ttXuat = r.ThanhTienXuat != null ? Convert.ToDecimal(r.ThanhTienXuat) : null;

            if (slNhap.HasValue) tongNhapSl += slNhap.Value;
            if (ttNhap.HasValue) tongNhapTien += ttNhap.Value;
            if (slXuat.HasValue) tongXuatSl += slXuat.Value;
            if (ttXuat.HasValue) tongXuatTien += ttXuat.Value;

            decimal? tonSau = null;
            if (sanPhamId.HasValue && sanPhamId.Value > 0)
            {
                runningTon += (slNhap ?? 0) - (slXuat ?? 0);
                tonSau = runningTon;
            }

            allItems.Add(new BaoCaoChiTietNhapXuatItem
            {
                Stt = i + 1,
                ThoiGian = Convert.ToDateTime(r.ThoiGian),
                MaChungTu = Convert.ToString(r.MaChungTu) ?? "",
                LoaiGiaoDich = Convert.ToString(r.LoaiGiaoDich) ?? "",
                LoaiChungTu = Convert.ToString(r.LoaiChungTu) ?? "",
                LoaiChungTuHienThi = Convert.ToString(r.LoaiChungTuHienThi) ?? "",
                MauLoaiChungTu = Convert.ToString(r.MauLoaiChungTu) ?? "#087F6E",
                MauNenLoaiChungTu = Convert.ToString(r.MauNenLoaiChungTu) ?? "#E7F8F0",
                SanPhamId = Convert.ToUInt32(r.SanPhamId),
                MaSanPham = Convert.ToString(r.MaSanPham) ?? "",
                TenSanPham = Convert.ToString(r.TenSanPham) ?? "",
                DonViTinh = Convert.ToString(r.DonViTinh) ?? "",
                DoiTac = Convert.ToString(r.DoiTac) ?? "",
                SoLuongNhap = slNhap,
                DonGiaNhap = dgNhap,
                ThanhTienNhap = ttNhap,
                SoLuongXuat = slXuat,
                DonGiaXuat = dgXuat,
                ThanhTienXuat = ttXuat,
                TonSau = tonSau,
                GhiChu = Convert.ToString(r.GhiChu) ?? ""
            });
        }

        // Khi hiển thị chung toàn hệ thống, xếp thời gian mới nhất lên đầu; nếu là thẻ kho 1 mặt hàng thì giữ thứ tự thời gian tăng dần
        if (!sanPhamId.HasValue || sanPhamId.Value == 0)
        {
            allItems.Reverse();
            for (int i = 0; i < allItems.Count; i++)
            {
                allItems[i].Stt = i + 1;
            }
        }

        // Phân trang
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new BaoCaoChiTietNhapXuatDuLieu
        {
            TuNgay = tu,
            DenNgay = denKeTiep.AddDays(-1),
            TongSoGiaoDich = totalCount,
            TongSoLuongNhap = tongNhapSl,
            TongThanhTienNhap = tongNhapTien,
            TongSoLuongXuat = tongXuatSl,
            TongThanhTienXuat = tongXuatTien,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = pagedItems
        };
    }

    #endregion

    #region XUẤT EXCEL (CLOSEDXML)

    /// <summary>
    /// Xuất Excel Báo cáo tổng hợp nhập xuất (Giao diện 63).
    /// </summary>
    public static XLWorkbook TaoWorkbookTongHopNhapXuat(BaoCaoTongHopNhapXuatDuLieu data)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Tổng hợp nhập xuất");

        // Tiêu đề
        ws.Cell(1, 1).Value = "BÁO CÁO TỔNG HỢP NHẬP XUẤT";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#164B40");

        ws.Cell(2, 1).Value = $"Thời gian: Từ {data.TuNgayHienThi} đến {data.DenNgayHienThi}";
        ws.Cell(2, 1).Style.Font.Italic = true;

        ws.Cell(3, 1).Value = $"Ngày xuất báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(3, 1).Style.Font.FontSize = 10;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#667085");

        // Header bảng (2 tầng)
        // Hàng 5: Nhóm cha
        // Hàng 6: Cột con
        int rHeader1 = 5;
        int rHeader2 = 6;

        ws.Cell(rHeader1, 1).Value = "STT";
        ws.Range(rHeader1, 1, rHeader2, 1).Merge();

        ws.Cell(rHeader1, 2).Value = "Mã hàng";
        ws.Range(rHeader1, 2, rHeader2, 2).Merge();

        ws.Cell(rHeader1, 3).Value = "Tên hàng hóa";
        ws.Range(rHeader1, 3, rHeader2, 3).Merge();

        ws.Cell(rHeader1, 4).Value = "Nhóm hàng";
        ws.Range(rHeader1, 4, rHeader2, 4).Merge();

        ws.Cell(rHeader1, 5).Value = "ĐVT";
        ws.Range(rHeader1, 5, rHeader2, 5).Merge();

        // Tồn đầu kỳ
        ws.Cell(rHeader1, 6).Value = "TỒN ĐẦU KỲ";
        ws.Range(rHeader1, 6, rHeader1, 7).Merge();
        ws.Cell(rHeader2, 6).Value = "SL Tồn đầu";
        ws.Cell(rHeader2, 7).Value = "Giá trị đầu";

        // Nhập trong kỳ
        ws.Cell(rHeader1, 8).Value = "NHẬP TRONG KỲ";
        ws.Range(rHeader1, 8, rHeader1, 12).Merge();
        ws.Cell(rHeader2, 8).Value = "Nhập mua";
        ws.Cell(rHeader2, 9).Value = "Khách trả";
        ws.Cell(rHeader2, 10).Value = "Nhập khác";
        ws.Cell(rHeader2, 11).Value = "Tổng nhập";
        ws.Cell(rHeader2, 12).Value = "Giá trị nhập";

        // Xuất trong kỳ
        ws.Cell(rHeader1, 13).Value = "XUẤT TRONG KỲ";
        ws.Range(rHeader1, 13, rHeader1, 17).Merge();
        ws.Cell(rHeader2, 13).Value = "Xuất bán";
        ws.Cell(rHeader2, 14).Value = "Trả NCC";
        ws.Cell(rHeader2, 15).Value = "Xuất NB/Hủy";
        ws.Cell(rHeader2, 16).Value = "Tổng xuất";
        ws.Cell(rHeader2, 17).Value = "Giá trị xuất";

        // Tồn cuối kỳ
        ws.Cell(rHeader1, 18).Value = "TỒN CUỐI KỲ";
        ws.Range(rHeader1, 18, rHeader1, 20).Merge();
        ws.Cell(rHeader2, 18).Value = "SL Tồn cuối";
        ws.Cell(rHeader2, 19).Value = "Đơn giá vốn";
        ws.Cell(rHeader2, 20).Value = "Giá trị cuối";

        // Định dạng header
        var headerRange = ws.Range(rHeader1, 1, rHeader2, 20);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Đổ dữ liệu
        int r = rHeader2;
        foreach (var item in data.Items)
        {
            r++;
            ws.Cell(r, 1).Value = item.Stt;
            ws.Cell(r, 2).Value = item.MaSanPham;
            ws.Cell(r, 3).Value = item.TenSanPham;
            ws.Cell(r, 4).Value = item.TenNhom;
            ws.Cell(r, 5).Value = item.DonViTinh;

            ws.Cell(r, 6).Value = item.TonDauKy;
            ws.Cell(r, 6).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 7).Value = item.GiaTriDauKy;
            ws.Cell(r, 7).Style.NumberFormat.Format = DinhDangTien;

            ws.Cell(r, 8).Value = item.NhapMua;
            ws.Cell(r, 8).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 9).Value = item.NhapTraLai;
            ws.Cell(r, 9).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 10).Value = item.NhapKhac;
            ws.Cell(r, 10).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 11).Value = item.TongNhap;
            ws.Cell(r, 11).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(r, 11).Style.Font.Bold = true;

            ws.Cell(r, 12).Value = item.GiaTriNhap;
            ws.Cell(r, 12).Style.NumberFormat.Format = DinhDangTien;

            ws.Cell(r, 13).Value = item.XuatBan;
            ws.Cell(r, 13).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 14).Value = item.XuatTraNcc;
            ws.Cell(r, 14).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 15).Value = item.XuatNoiBo;
            ws.Cell(r, 15).Style.NumberFormat.Format = DinhDangSo;

            ws.Cell(r, 16).Value = item.TongXuat;
            ws.Cell(r, 16).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(r, 16).Style.Font.Bold = true;

            ws.Cell(r, 17).Value = item.GiaTriXuat;
            ws.Cell(r, 17).Style.NumberFormat.Format = DinhDangTien;

            ws.Cell(r, 18).Value = item.TonCuoiKy;
            ws.Cell(r, 18).Style.NumberFormat.Format = DinhDangSo;
            ws.Cell(r, 18).Style.Font.Bold = true;

            ws.Cell(r, 19).Value = item.GiaVon;
            ws.Cell(r, 19).Style.NumberFormat.Format = DinhDangTien;

            ws.Cell(r, 20).Value = item.GiaTriTonCuoi;
            ws.Cell(r, 20).Style.NumberFormat.Format = DinhDangTien;
            ws.Cell(r, 20).Style.Font.Bold = true;

            if (r % 2 == 1)
            {
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FBFA");
            }
        }

        // Dòng tổng cộng
        r++;
        ws.Cell(r, 1).Value = "TỔNG CỘNG";
        ws.Range(r, 1, r, 5).Merge();
        ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(r, 6).Value = data.TongTonDau;
        ws.Cell(r, 6).Style.NumberFormat.Format = DinhDangSo;

        ws.Cell(r, 7).Value = data.TongGiaTriDau;
        ws.Cell(r, 7).Style.NumberFormat.Format = DinhDangTien;

        ws.Cell(r, 11).Value = data.TongNhap;
        ws.Cell(r, 11).Style.NumberFormat.Format = DinhDangSo;

        ws.Cell(r, 12).Value = data.TongGiaTriNhap;
        ws.Cell(r, 12).Style.NumberFormat.Format = DinhDangTien;

        ws.Cell(r, 16).Value = data.TongXuat;
        ws.Cell(r, 16).Style.NumberFormat.Format = DinhDangSo;

        ws.Cell(r, 17).Value = data.TongGiaTriXuat;
        ws.Cell(r, 17).Style.NumberFormat.Format = DinhDangTien;

        ws.Cell(r, 18).Value = data.TongTonCuoi;
        ws.Cell(r, 18).Style.NumberFormat.Format = DinhDangSo;

        ws.Cell(r, 20).Value = data.TongGiaTriTonCuoi;
        ws.Cell(r, 20).Style.NumberFormat.Format = DinhDangTien;

        var totalRowRange = ws.Range(r, 1, r, 20);
        totalRowRange.Style.Font.Bold = true;
        totalRowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2ECE8");

        var dataRange = ws.Range(rHeader1, 1, r, 20);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Columns().AdjustToContents();
        return wb;
    }

    /// <summary>
    /// Xuất Excel Báo cáo chi tiết nhập xuất (Thẻ kho - Giao diện 64).
    /// </summary>
    public static XLWorkbook TaoWorkbookChiTietNhapXuat(BaoCaoChiTietNhapXuatDuLieu data)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Chi tiết nhập xuất");

        // Tiêu đề
        ws.Cell(1, 1).Value = "BÁO CÁO CHI TIẾT NHẬP XUẤT (THẺ KHO)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#164B40");

        ws.Cell(2, 1).Value = $"Thời gian: Từ {data.TuNgayHienThi} đến {data.DenNgayHienThi} • Tổng cộng: {data.TongSoGiaoDich:N0} chứng từ/giao dịch";
        ws.Cell(2, 1).Style.Font.Italic = true;

        ws.Cell(3, 1).Value = $"Ngày xuất báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(3, 1).Style.Font.FontSize = 10;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#667085");

        // Header bảng
        int r = 5;
        string[] headers =
        [
            "STT", "Thời gian", "Mã chứng từ", "Loại giao dịch", "Mã hàng", "Tên hàng hóa",
            "ĐVT", "Đối tác", "SL Nhập", "Đơn giá nhập", "Thành tiền nhập",
            "SL Xuất", "Đơn giá xuất", "Thành tiền xuất", "Tồn sau GD", "Ghi chú"
        ];

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(r, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MauHeaderBg);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        foreach (var item in data.Items)
        {
            r++;
            ws.Cell(r, 1).Value = item.Stt;
            ws.Cell(r, 2).Value = item.ThoiGianHienThi;
            ws.Cell(r, 3).Value = item.MaChungTu;
            ws.Cell(r, 4).Value = item.LoaiChungTuHienThi;
            ws.Cell(r, 5).Value = item.MaSanPham;
            ws.Cell(r, 6).Value = item.TenSanPham;
            ws.Cell(r, 7).Value = item.DonViTinh;
            ws.Cell(r, 8).Value = item.DoiTac;

            if (item.SoLuongNhap.HasValue)
            {
                ws.Cell(r, 9).Value = item.SoLuongNhap.Value;
                ws.Cell(r, 9).Style.NumberFormat.Format = DinhDangSo;
            }

            if (item.DonGiaNhap.HasValue)
            {
                ws.Cell(r, 10).Value = item.DonGiaNhap.Value;
                ws.Cell(r, 10).Style.NumberFormat.Format = DinhDangTien;
            }

            if (item.ThanhTienNhap.HasValue)
            {
                ws.Cell(r, 11).Value = item.ThanhTienNhap.Value;
                ws.Cell(r, 11).Style.NumberFormat.Format = DinhDangTien;
            }

            if (item.SoLuongXuat.HasValue)
            {
                ws.Cell(r, 12).Value = item.SoLuongXuat.Value;
                ws.Cell(r, 12).Style.NumberFormat.Format = DinhDangSo;
            }

            if (item.DonGiaXuat.HasValue)
            {
                ws.Cell(r, 13).Value = item.DonGiaXuat.Value;
                ws.Cell(r, 13).Style.NumberFormat.Format = DinhDangTien;
            }

            if (item.ThanhTienXuat.HasValue)
            {
                ws.Cell(r, 14).Value = item.ThanhTienXuat.Value;
                ws.Cell(r, 14).Style.NumberFormat.Format = DinhDangTien;
            }

            if (item.TonSau.HasValue)
            {
                ws.Cell(r, 15).Value = item.TonSau.Value;
                ws.Cell(r, 15).Style.NumberFormat.Format = DinhDangSo;
                ws.Cell(r, 15).Style.Font.Bold = true;
            }

            ws.Cell(r, 16).Value = item.GhiChu;

            if (r % 2 == 1)
            {
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FBFA");
            }
        }

        // Dòng tổng cộng
        r++;
        ws.Cell(r, 1).Value = "TỔNG CỘNG";
        ws.Range(r, 1, r, 8).Merge();
        ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(r, 9).Value = data.TongSoLuongNhap;
        ws.Cell(r, 9).Style.NumberFormat.Format = DinhDangSo;

        ws.Cell(r, 11).Value = data.TongThanhTienNhap;
        ws.Cell(r, 11).Style.NumberFormat.Format = DinhDangTien;

        ws.Cell(r, 12).Value = data.TongSoLuongXuat;
        ws.Cell(r, 12).Style.NumberFormat.Format = DinhDangSo;

        ws.Cell(r, 14).Value = data.TongThanhTienXuat;
        ws.Cell(r, 14).Style.NumberFormat.Format = DinhDangTien;

        var totalRowRange = ws.Range(r, 1, r, headers.Length);
        totalRowRange.Style.Font.Bold = true;
        totalRowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2ECE8");

        var dataRange = ws.Range(5, 1, r, headers.Length);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Columns().AdjustToContents();
        return wb;
    }

    #endregion
}
