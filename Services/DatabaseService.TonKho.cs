using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    /// <summary>
    /// Cập nhật tồn kho của một sản phẩm tại một chi nhánh trong giao dịch hiện tại.
    /// Ghi nhận đủ ba nơi: ton_kho (nguồn thật), san_pham.ton_kho_excel (tổng hợp cũ)
    /// và lich_su_nhap_xuat_kho (dấu vết kiểm toán).
    /// </summary>
    private async Task<(decimal TonTruoc, decimal TonSau)> CapNhatTonKhoGiaoDichAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint chiNhanhId,
        uint sanPhamId,
        string? tenSanPham,
        decimal delta,
        decimal donGia,
        string loaiGiaoDich,
        string loaiChungTu,
        string? maChungTu,
        uint? nguoiThucHienId,
        DateTime thoiGian,
        string? ghiChu,
        bool choPhepAm = false,
        bool dongBoTonKhoExcel = true)
    {
        const string selectTonSql = """
            SELECT so_luong_ton, gia_von_binh_quan
            FROM ton_kho
            WHERE chi_nhanh_id = @ChiNhanhId
              AND san_pham_id = @SanPhamId
            FOR UPDATE;
        """;

        var banGhi = (await connection.QueryAsync(
            selectTonSql,
            new { ChiNhanhId = chiNhanhId, SanPhamId = sanPhamId },
            transaction)).ToList();

        decimal tonTruoc = 0;
        decimal giaVon = donGia;

        if (banGhi.FirstOrDefault() is IDictionary<string, object> row)
        {
            tonTruoc = ToDecimal(row.TryGetValue("so_luong_ton", out var soLuong) ? soLuong : null);
            giaVon = ToDecimal(row.TryGetValue("gia_von_binh_quan", out var giaVonCu) ? giaVonCu : null);
        }

        if (giaVon <= 0)
        {
            giaVon = await connection.ExecuteScalarAsync<decimal>(
                "SELECT COALESCE(gia_von, 0) FROM san_pham WHERE id = @SanPhamId;",
                new { SanPhamId = sanPhamId },
                transaction);
        }

        if (giaVon <= 0)
            giaVon = donGia;

        var tonSau = tonTruoc + delta;

        if (!choPhepAm && tonSau < 0)
        {
            var ten = string.IsNullOrWhiteSpace(tenSanPham) ? $"#{sanPhamId}" : tenSanPham;
            throw new InvalidOperationException(
                $"\"{ten}\" không đủ tồn kho để xuất (tồn hiện tại {tonTruoc:N0}, cần {Math.Abs(delta):N0}).");
        }

        const string upsertTonSql = """
            INSERT INTO ton_kho
            (
                chi_nhanh_id,
                san_pham_id,
                so_luong_ton,
                gia_von_binh_quan,
                ngay_cap_nhat
            )
            VALUES
            (
                @ChiNhanhId,
                @SanPhamId,
                @SoLuongTon,
                @GiaVon,
                @ThoiGian
            )
            ON DUPLICATE KEY UPDATE
                so_luong_ton = @SoLuongTon,
                gia_von_binh_quan = IF(@GiaVon > 0, @GiaVon, gia_von_binh_quan),
                ngay_cap_nhat = @ThoiGian;
        """;

        await connection.ExecuteAsync(
            upsertTonSql,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId,
                SoLuongTon = tonSau,
                GiaVon = giaVon,
                ThoiGian = thoiGian
            },
            transaction);

        if (dongBoTonKhoExcel)
        {
            const string syncExcelSql = """
                UPDATE san_pham
                SET ton_kho_excel = GREATEST(0, COALESCE(ton_kho_excel, 0) + @Delta),
                    ngay_cap_nhat = NOW()
                WHERE id = @SanPhamId;
            """;

            await connection.ExecuteAsync(
                syncExcelSql,
                new { Delta = delta, SanPhamId = sanPhamId },
                transaction);
        }

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
                @LoaiGiaoDich,
                @LoaiChungTu,
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

        await connection.ExecuteAsync(
            insertLichSuSql,
            new
            {
                ChiNhanhId = chiNhanhId,
                SanPhamId = sanPhamId,
                LoaiGiaoDich = loaiGiaoDich,
                LoaiChungTu = loaiChungTu,
                MaChungTu = maChungTu,
                SoLuong = delta,
                TonTruoc = tonTruoc,
                TonSau = tonSau,
                GiaVon = giaVon,
                NguoiThucHienId = nguoiThucHienId,
                ThoiGian = thoiGian,
                GhiChu = ghiChu
            },
            transaction);

        return (tonTruoc, tonSau);
    }
}
