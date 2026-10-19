using Dapper;
using ShopHoa.Models;

namespace ShopHoa.Services;

public sealed partial class DatabaseService
{
    /// <summary>Dòng kết quả của <see cref="GetSoQuyPhieuChiThongKeAsync"/>.</summary>
    private sealed class SoQuyPhieuChiThongKeRow
    {
        public long TongSoPhieu { get; set; }

        public decimal TongTienChi { get; set; }

        public long SoPhieuDaHuy { get; set; }
    }

    /// <summary>
    /// 3 thẻ thống kê của màn Danh sách phiếu chi: tổng số phiếu chi, tổng tiền
    /// chi (chỉ cộng phiếu chưa hủy) và số phiếu đã hủy.
    /// <para>
    /// Dùng đúng bộ tham số lọc của danh sách đang hiển thị nên 3 thẻ luôn khớp
    /// với bảng bên dưới. Điều kiện WHERE lấy lại của sổ quỹ
    /// (<see cref="TaoDieuKienSoQuy"/>), cộng thêm ràng buộc mã tiền tố "PC" để
    /// thống kê không bao giờ dính phiếu thu.
    /// </para>
    /// <para>
    /// Cột số đếm trả về dạng BIGINT rồi cast về int ở C#: Dapper không tự
    /// chuyển Int64 sang int nên khai decimal/long sẽ không bị lỗi ánh xạ.
    /// </para>
    /// </summary>
    public async Task<(int TongSoPhieu, decimal TongTienChi, int SoPhieuDaHuy)> GetSoQuyPhieuChiThongKeAsync(
        SoQuyBoLoc loc)
    {
        loc ??= new SoQuyBoLoc();

        // Gắn loại thu/chi cho phiếu cũ trước khi thống kê, giống hệt danh sách.
        await EnsureSoQuyNhomChiPhiChoPhieuCuAsync();

        var coCotMoi = await EnsurePhieuQuyExtraColumnsAsync();

        await using var connection = await OpenAsync();

        var dieuKien = TaoDieuKienSoQuy(apDungKhoangThoiGian: true, apDungKhoangTien: true, coCotMoi);
        var thamSo = TaoThamSoSoQuy(loc, apDungKhoangTien: true);

        var sql = $"""
        SELECT
            COUNT(*) AS TongSoPhieu,
            COALESCE(SUM(CASE WHEN pc.trang_thai = @TrangThaiHoanThanh THEN pc.so_tien ELSE 0 END), 0) AS TongTienChi,
            COALESCE(SUM(CASE WHEN pc.trang_thai = @TrangThaiHuy THEN 1 ELSE 0 END), 0) AS SoPhieuDaHuy
        {SoQuyFrom}
        WHERE {dieuKien}
          AND pc.ma_phieu_chi LIKE 'PC%';
        """;

        var parameters = GopThamSo(thamSo, 0, 0);
        parameters["TrangThaiHoanThanh"] = TrangThaiPhieuHoanThanh;
        parameters["TrangThaiHuy"] = TrangThaiPhieuHuy;

        var row = await connection.QueryFirstOrDefaultAsync<SoQuyPhieuChiThongKeRow>(sql, parameters);

        return (
            (int)(row?.TongSoPhieu ?? 0L),
            row?.TongTienChi ?? 0m,
            (int)(row?.SoPhieuDaHuy ?? 0L));
    }
}
