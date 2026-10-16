using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Các truy vấn thống kê cho màn hình "Tổng quan".
/// <para>
/// Nguyên tắc: mọi phép tính (COUNT / SUM / GROUP BY / ORDER BY / LIMIT) đều để
/// database thực hiện. Không SELECT toàn bộ bảng về RAM rồi tính ở client, và
/// không hard-code số liệu.
/// </para>
/// <para>
/// Quy tắc doanh thu (đồng nhất với màn Hóa đơn):
/// chỉ tính hóa đơn <c>trang_thai_hoa_don = 'HOAN_THANH'</c>, doanh thu là
/// <c>khach_can_tra</c> (không phải "tiền khách đưa"), tiền thu là
/// <c>khach_da_thanh_toan</c> (không phải <c>tien_khach_dua</c>), nợ là <c>con_no</c>.
/// Nhờ vậy hóa đơn 500.000 với tiền thừa 100.000 vẫn ra doanh thu 500.000.
/// </para>
/// </summary>
public sealed class DashboardService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public DashboardService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    /// <summary>
    /// Khoảng thời gian được tính ở C# rồi truyền mốc <c>TuNgay</c>/<c>DenNgay</c>
    /// (khoảng nửa vờng, luôn kín) xuống SQL. Nhờ vậy chỉ có đúng một dạng điều kiện
    /// thời gian duy nhất cho mọi truy vấn, dễ kiểm chứng và không phụ thuộc
    /// múi giờ của database.
    /// </summary>
    public static (DateTime TuNgay, DateTime DenNgay) TinhKhoang(
        string khoangThoiGian,
        DateTime? tuNgayTuChon,
        DateTime? denNgayTuChon,
        DateTime homNay)
    {
        var dauNgay = homNay.Date;

        return khoangThoiGian switch
        {
            TongQuanKhoangThoiGian.BayNgay => (dauNgay.AddDays(-6), dauNgay.AddDays(1).AddTicks(-1)),

            TongQuanKhoangThoiGian.ThangNay => (
                new DateTime(homNay.Year, homNay.Month, 1),
                dauNgay.AddDays(1).AddTicks(-1)),

            TongQuanKhoangThoiGian.ThangTruoc => (
                new DateTime(homNay.Year, homNay.Month, 1).AddMonths(-1),
                new DateTime(homNay.Year, homNay.Month, 1).AddTicks(-1)),

            TongQuanKhoangThoiGian.NamNay => (
                new DateTime(homNay.Year, 1, 1),
                dauNgay.AddDays(1).AddTicks(-1)),

            TongQuanKhoangThoiGian.TuDien => (
                (tuNgayTuChon ?? dauNgay).Date,
                (denNgayTuChon ?? dauNgay).Date.AddDays(1).AddTicks(-1)),

            // Mặc định = Hôm nay
            _ => (dauNgay, dauNgay.AddDays(1).AddTicks(-1))
        };
    }

    /// <summary>
    /// Biểu thức SQL dùng để cắt bucket theo ngày/giờ/thứ. Chỉ dùng hàm có sẵn
    /// của MariaDB để không phụ thuộc tuỳ chọn đặc biệt.
    /// </summary>
    private static string BieuThucBucket(string chieu)
    {
        return chieu switch
        {
            // Theo giờ: gom theo 24 khung giờ trong ngày (0..23) để phân tích giờ cao điểm.
            TongQuanChieuDoanhThu.TheoGio =>
                "CAST(HOUR(hd.thoi_gian_ban) AS CHAR)",

            // Theo thứ: gom theo thứ trong tuần (0 = Thứ 2 ... 6 = Chủ nhật).
            TongQuanChieuDoanhThu.TheoThu =>
                "CAST(WEEKDAY(hd.thoi_gian_ban) AS CHAR)",

            _ => "DATE_FORMAT(hd.thoi_gian_ban, '%Y-%m-%d')"
        };
    }

    private static string TenCotTongTien() => "TongDoanhThu";

    /// <summary>
    /// Tải toàn bộ số liệu Dashboard. Các nhóm truy vấn độc lập được chạy song
    /// song để giảm thời gian chờ, mỗi nhóm chỉ lấy đúng số dòng cần dùng.
    /// </summary>
    public async Task<TongQuanDuLieu> TaiDuLieuAsync(
        string khoangThoiGian,
        string chieuDoanhThu,
        string xepHangTopHang,
        DateTime? tuNgayTuChon,
        DateTime? denNgayTuChon,
        DateTime homNay,
        bool laAdmin = false,
        CancellationToken token = default)
    {
        var (tuNgay, denNgay) = TinhKhoang(khoangThoiGian, tuNgayTuChon, denNgayTuChon, homNay);

        var ketQua = new TongQuanDuLieu
        {
            ThoiGianHienThi = $"{TongQuanDuLieu.NgayHienThi(tuNgay)} – {TongQuanDuLieu.NgayHienThi(denNgay)}"
        };

        // Các nhóm độc lập: chạy song song, tất cả đều lọc theo đúng khoảng [tuNgay, denNgay] đang chọn.
        var taskBanHangTheoKy = TaiSoLieuBanHangHomNayAsync(tuNgay, denNgay, token);
        var taskBieuDo = TaiBieuDoDoanhThuAsync(chieuDoanhThu, tuNgay, denNgay, token);
        var taskTopHang = TaiTopHangBanChayAsync(xepHangTopHang, tuNgay, denNgay, token);
        var taskTopKhach = TaiTopKhachHangAsync(tuNgay, denNgay, token);
        var taskHoatDongSub = laAdmin ? TaiHoatDongTaiKhoanConAsync(token) : Task.FromResult(new List<TongQuanHoatDongTaiKhoanCon>());

        await Task.WhenAll(taskBanHangTheoKy, taskBieuDo, taskTopHang, taskTopKhach, taskHoatDongSub);

        var banHang = await taskBanHangTheoKy;
        var (tongDoanhThu, tongSoHoaDon, bieuDo) = await taskBieuDo;
        var topHang = await taskTopHang;
        var topKhach = await taskTopKhach;
        var hoatDongSub = await taskHoatDongSub;

        ketQua.DoanhThuHomNay = banHang.DoanhThu;
        ketQua.SoHoaDonHomNay = banHang.SoHoaDon;
        ketQua.DaThuHomNay = banHang.DaThu;
        ketQua.ConNoHomNay = banHang.ConNo;
        ketQua.SoDonHangHomNay = banHang.SoHoaDon;

        ketQua.TongDoanhThuKyChon = tongDoanhThu;
        ketQua.TongSoHoaDonKyChon = tongSoHoaDon;
        ketQua.BieuDoDoanhThu = bieuDo;
        DienPhanTichTheoChieu(ketQua, chieuDoanhThu, tuNgay, denNgay, tongDoanhThu, tongSoHoaDon, bieuDo);

        ketQua.HoatDongTaiKhoanCon = hoatDongSub;
        ketQua.TopHangBanChay = topHang;
        ketQua.TopKhachHang = topKhach;

        // Thống kê kho không phụ thuộc bộ lọc thời gian nên tải sau, giữ cho
        // các task đầu chạy cùng lúc.
        ketQua.ThongKeKho = await TaiThongKeKhoAsync(token);

        return ketQua;
    }

    /// <summary>5 thẻ kết quả bán hàng theo khoảng thời gian đang chọn — 1 câu COUNT/SUM duy nhất.</summary>
    private async Task<(decimal DoanhThu, int SoHoaDon, decimal DaThu, decimal ConNo)> TaiSoLieuBanHangHomNayAsync(
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(hd.khach_da_thanh_toan), 0) AS DaThu,
            COALESCE(SUM(hd.con_no), 0) AS ConNo
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay;
        """;

        var row = await connection.QuerySingleAsync<(decimal DoanhThu, int SoHoaDon, decimal DaThu, decimal ConNo)>(
            new CommandDefinition(
                sql,
                new
                {
                    TuNgay = tuNgay,
                    DenNgay = denNgay
                },
                cancellationToken: token));

        return row;
    }

    /// <summary>
    /// Biểu đồ doanh thu: GROUP BY theo bucket trong DB (theo ngày / khung giờ / thứ trong tuần),
    /// sau đó dựng đầy đủ trục thời gian để người dùng thấy rõ sự phân bổ và so sánh giữa các mốc.
    /// </summary>
    private async Task<(decimal TongDoanhThu, int TongSoHoaDon, List<TongQuanDiemDoanhThu> Diem)> TaiBieuDoDoanhThuAsync(
        string chieu,
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        var bucket = BieuThucBucket(chieu);

        var gioiHanDiem = chieu switch
        {
            TongQuanChieuDoanhThu.TheoGio => 24,
            TongQuanChieuDoanhThu.TheoThu => 7,
            _ => 366
        };

        var sql = $"""
        SELECT
            {bucket} AS Bucket,
            COALESCE(SUM(hd.khach_can_tra), 0) AS {TenCotTongTien()},
            COUNT(*) AS SoHoaDon,
            MAX(hd.thoi_gian_ban) AS ThoiGianLonNhat
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay
        GROUP BY Bucket
        ORDER BY Bucket
        LIMIT {gioiHanDiem};
        """;

        var hangDuLieu = (
            await connection.QueryAsync<(
                string Bucket,
                decimal TongDoanhThu,
                int SoHoaDon,
                DateTime ThoiGianLonNhat)>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TuNgay = tuNgay,
                        DenNgay = denNgay
                    },
                    cancellationToken: token))
        ).AsList();

        var tongDoanhThu = hangDuLieu.Sum(r => r.TongDoanhThu);
        var tongSoHoaDon = hangDuLieu.Sum(r => r.SoHoaDon);

        var diem = new List<TongQuanDiemDoanhThu>();

        if (chieu == TongQuanChieuDoanhThu.TheoGio)
        {
            // Gom theo 24 khung giờ trong ngày (00:00 -> 23:00)
            var mapGio = new Dictionary<int, (decimal DoanhThu, int SoHoaDon)>();
            foreach (var r in hangDuLieu)
            {
                var gio = int.TryParse(r.Bucket, out var parsedGio)
                    ? parsedGio
                    : r.ThoiGianLonNhat.Hour;
                if (gio is >= 0 and <= 23)
                    mapGio[gio] = (r.TongDoanhThu, r.SoHoaDon);
            }

            for (var gio = 0; gio < 24; gio++)
            {
                mapGio.TryGetValue(gio, out var val);
                diem.Add(new TongQuanDiemDoanhThu
                {
                    Nhan = $"{gio:D2}:00",
                    NhanDayDu = $"Khung giờ {gio:D2}:00 – {gio:D2}:59 ({TongQuanDuLieu.NgayHienThi(tuNgay)} – {TongQuanDuLieu.NgayHienThi(denNgay)})",
                    DoanhThu = val.DoanhThu,
                    SoHoaDon = val.SoHoaDon,
                    BeRongCotTuyChinh = 46
                });
            }
        }
        else if (chieu == TongQuanChieuDoanhThu.TheoThu)
        {
            // Gom theo 7 thứ trong tuần (0 = Thứ 2 ... 6 = Chủ nhật)
            var mapThu = new Dictionary<int, (decimal DoanhThu, int SoHoaDon)>();
            foreach (var r in hangDuLieu)
            {
                var thu = int.TryParse(r.Bucket, out var parsedThu)
                    ? parsedThu
                    : ((int)r.ThoiGianLonNhat.DayOfWeek + 6) % 7;
                if (thu is >= 0 and <= 6)
                    mapThu[thu] = (r.TongDoanhThu, r.SoHoaDon);
            }

            for (var thu = 0; thu < 7; thu++)
            {
                mapThu.TryGetValue(thu, out var val);
                var tenThu = TenThuTrongTuan((DayOfWeek)((thu + 1) % 7));
                diem.Add(new TongQuanDiemDoanhThu
                {
                    Nhan = tenThu,
                    NhanDayDu = $"{tenThu} (trong kỳ {TongQuanDuLieu.NgayHienThi(tuNgay)} – {TongQuanDuLieu.NgayHienThi(denNgay)})",
                    DoanhThu = val.DoanhThu,
                    SoHoaDon = val.SoHoaDon,
                    BeRongCotTuyChinh = 82
                });
            }
        }
        else
        {
            // Theo ngày: hiển thị đầy đủ các ngày trong khoảng (tối đa 62 ngày liên tục),
            // nếu khoảng dài hơn (như Năm nay) thì hiển thị các ngày có phát sinh + ngày đầu/cuối kỳ.
            var mapNgay = new Dictionary<DateTime, (decimal DoanhThu, int SoHoaDon)>();
            foreach (var r in hangDuLieu)
            {
                var ngay = DateTime.TryParse(r.Bucket, out var parsedNgay)
                    ? parsedNgay.Date
                    : r.ThoiGianLonNhat.Date;
                mapNgay[ngay] = (r.TongDoanhThu, r.SoHoaDon);
            }

            var soNgay = Math.Max(1, (int)(denNgay.Date - tuNgay.Date).TotalDays + 1);
            if (soNgay <= 62)
            {
                var beRong = soNgay == 1 ? 110 : (soNgay <= 7 ? 74 : (soNgay <= 16 ? 56 : 46));
                for (var d = tuNgay.Date; d <= denNgay.Date; d = d.AddDays(1))
                {
                    mapNgay.TryGetValue(d, out var val);
                    var tenThu = TenThuTrongTuan(d.DayOfWeek);
                    diem.Add(new TongQuanDiemDoanhThu
                    {
                        Nhan = soNgay == 1 ? $"{d:dd/MM/yyyy}" : d.ToString("dd/MM"),
                        NhanDayDu = $"{tenThu}, ngày {d:dd/MM/yyyy}",
                        DoanhThu = val.DoanhThu,
                        SoHoaDon = val.SoHoaDon,
                        BeRongCotTuyChinh = beRong
                    });
                }
            }
            else
            {
                var danhSachNgay = mapNgay.Keys.OrderBy(d => d).ToList();
                if (danhSachNgay.Count == 0)
                {
                    danhSachNgay.Add(tuNgay.Date);
                    if (denNgay.Date != tuNgay.Date)
                        danhSachNgay.Add(denNgay.Date);
                }

                var beRong = danhSachNgay.Count <= 7 ? 74 : (danhSachNgay.Count <= 16 ? 56 : 48);
                foreach (var d in danhSachNgay)
                {
                    mapNgay.TryGetValue(d, out var val);
                    var tenThu = TenThuTrongTuan(d.DayOfWeek);
                    diem.Add(new TongQuanDiemDoanhThu
                    {
                        Nhan = d.ToString("dd/MM"),
                        NhanDayDu = $"{tenThu}, ngày {d:dd/MM/yyyy}",
                        DoanhThu = val.DoanhThu,
                        SoHoaDon = val.SoHoaDon,
                        BeRongCotTuyChinh = beRong
                    });
                }
            }
        }

        var maxDoanhThu = diem.Count == 0 ? 0m : diem.Max(r => r.DoanhThu);
        var tyLeBeRong = diem.Count == 0 ? 0d : 100d / diem.Count;

        foreach (var item in diem)
        {
            item.TyLeChieuCao = maxDoanhThu <= 0 ? 0 : (double)(item.DoanhThu / maxDoanhThu * 100m);
            item.TyLeBeRong = tyLeBeRong;
            item.LaDiemCaoNhat = maxDoanhThu > 0 && item.DoanhThu == maxDoanhThu;
        }

        return (tongDoanhThu, tongSoHoaDon, diem);
    }

    private static void DienPhanTichTheoChieu(
        TongQuanDuLieu ketQua,
        string chieu,
        DateTime tuNgay,
        DateTime denNgay,
        decimal tongDoanhThu,
        int tongSoHoaDon,
        List<TongQuanDiemDoanhThu> bieuDo)
    {
        var diemCoDon = bieuDo.Where(d => d.CoDuLieu).ToList();
        var diemMax = diemCoDon.OrderByDescending(d => d.DoanhThu).ThenByDescending(d => d.SoHoaDon).FirstOrDefault();
        var tbMoiHoaDon = tongSoHoaDon > 0 ? tongDoanhThu / tongSoHoaDon : 0m;

        if (chieu == TongQuanChieuDoanhThu.TheoGio)
        {
            ketQua.TieuDeDiemCaoNhat = "KHUNG GIỜ CAO ĐIỂM";
            if (diemMax is not null)
            {
                ketQua.GiaTriDiemCaoNhat = $"{diemMax.Nhan} – {diemMax.Nhan[..2]}:59";
                ketQua.PhuDeDiemCaoNhat = $"{diemMax.DoanhThuHienThi} • {diemMax.SoHoaDonHienThi}";
            }
            else
            {
                ketQua.GiaTriDiemCaoNhat = "Chưa có phát sinh";
                ketQua.PhuDeDiemCaoNhat = "0 đ • 0 hóa đơn";
            }

            var tbGioCoDon = diemCoDon.Count > 0 ? tongDoanhThu / diemCoDon.Count : 0m;
            ketQua.TieuDeTrungBinhChieu = "TB KHUNG GIỜ CÓ ĐƠN";
            ketQua.GiaTriTrungBinhChieu = $"{tbGioCoDon:N0} đ";
            ketQua.PhuDeTrungBinhChieu = $"TB mỗi hóa đơn: {tbMoiHoaDon:N0} đ";

            ketQua.TieuDeMatDoChieu = "KHUNG GIỜ HOẠT ĐỘNG";
            ketQua.GiaTriMatDoChieu = $"{diemCoDon.Count} / 24 giờ";
            ketQua.PhuDeMatDoChieu = diemCoDon.Count > 0
                ? $"Có giao dịch trong {diemCoDon.Count} khung giờ"
                : "Chưa có khung giờ nào phát sinh đơn";
        }
        else if (chieu == TongQuanChieuDoanhThu.TheoThu)
        {
            ketQua.TieuDeDiemCaoNhat = "THỨ BÁN CHẠY NHẤT";
            if (diemMax is not null)
            {
                ketQua.GiaTriDiemCaoNhat = diemMax.Nhan;
                ketQua.PhuDeDiemCaoNhat = $"{diemMax.DoanhThuHienThi} • {diemMax.SoHoaDonHienThi}";
            }
            else
            {
                ketQua.GiaTriDiemCaoNhat = "Chưa có phát sinh";
                ketQua.PhuDeDiemCaoNhat = "0 đ • 0 hóa đơn";
            }

            var tbThuCoDon = diemCoDon.Count > 0 ? tongDoanhThu / diemCoDon.Count : 0m;
            ketQua.TieuDeTrungBinhChieu = "TB MỖI THỨ CÓ ĐƠN";
            ketQua.GiaTriTrungBinhChieu = $"{tbThuCoDon:N0} đ";
            ketQua.PhuDeTrungBinhChieu = $"TB mỗi hóa đơn: {tbMoiHoaDon:N0} đ";

            ketQua.TieuDeMatDoChieu = "THỨ CÓ GIAO DỊCH";
            ketQua.GiaTriMatDoChieu = $"{diemCoDon.Count} / 7 thứ";
            ketQua.PhuDeMatDoChieu = diemCoDon.Count > 0
                ? $"Phát sinh đơn vào { string.Join(", ", diemCoDon.Select(d => d.Nhan)) }"
                : "Chưa có ngày thứ nào phát sinh đơn";
        }
        else
        {
            var soNgayTrongKy = Math.Max(1, (int)(denNgay.Date - tuNgay.Date).TotalDays + 1);
            ketQua.TieuDeDiemCaoNhat = "NGÀY CAO ĐIỂM NHẤT";
            if (diemMax is not null)
            {
                ketQua.GiaTriDiemCaoNhat = diemMax.NhanDayDu.Replace("Ngày ", "");
                ketQua.PhuDeDiemCaoNhat = $"{diemMax.DoanhThuHienThi} • {diemMax.SoHoaDonHienThi}";
            }
            else
            {
                ketQua.GiaTriDiemCaoNhat = "Chưa có phát sinh";
                ketQua.PhuDeDiemCaoNhat = "0 đ • 0 hóa đơn";
            }

            var tbMoiNgay = tongDoanhThu / soNgayTrongKy;
            var tbNgayCoDon = diemCoDon.Count > 0 ? tongDoanhThu / diemCoDon.Count : 0m;
            ketQua.TieuDeTrungBinhChieu = "TRUNG BÌNH MỖI NGÀY";
            ketQua.GiaTriTrungBinhChieu = $"{tbMoiNgay:N0} đ";
            ketQua.PhuDeTrungBinhChieu = $"TB ngày có đơn: {tbNgayCoDon:N0} đ";

            ketQua.TieuDeMatDoChieu = "SỐ NGÀY CÓ ĐƠN";
            ketQua.GiaTriMatDoChieu = $"{diemCoDon.Count} / {soNgayTrongKy} ngày";
            ketQua.PhuDeMatDoChieu = $"TB mỗi hóa đơn: {tbMoiHoaDon:N0} đ";
        }
    }

    private static string TenThuTrongTuan(DayOfWeek thu) => thu switch
    {
        DayOfWeek.Monday => "Thứ 2",
        DayOfWeek.Tuesday => "Thứ 3",
        DayOfWeek.Wednesday => "Thứ 4",
        DayOfWeek.Thursday => "Thứ 5",
        DayOfWeek.Friday => "Thứ 6",
        DayOfWeek.Saturday => "Thứ 7",
        _ => "Chủ nhật"
    };

    /// <summary>
    /// Lịch sử hoạt động của các tài khoản con (nhân viên, thu ngân, quản lý...).
    /// Chỉ tải khi tài khoản đang đăng nhập là Quản trị viên (Admin).
    /// Kết hợp bảng nhat_ky_hoat_dong với các giao dịch bán hàng, nhập kho, thu chi do tài khoản con thực hiện.
    /// </summary>
    public async Task<List<TongQuanHoatDongTaiKhoanCon>> TaiHoatDongTaiKhoanConAsync(CancellationToken token)
    {
        try
        {
            await using var connection = await _taoKetNoi();

            // Đảm bảo bảng nhat_ky_hoat_dong luôn tồn tại trước khi truy vấn
            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS `nhat_ky_hoat_dong` (
              `id` BIGINT(20) UNSIGNED NOT NULL AUTO_INCREMENT,
              `nguoi_dung_id` INT(10) UNSIGNED DEFAULT NULL,
              `ten_dang_nhap` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `ho_ten` VARCHAR(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `vai_tro` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `hanh_dong` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL,
              `doi_tuong` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `doi_tuong_id` INT(10) UNSIGNED DEFAULT NULL,
              `ma_chung_tu` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `noi_dung` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `gia_tri` DECIMAL(15,2) DEFAULT NULL,
              `dia_chi_ip` VARCHAR(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
              `thoi_gian` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
              PRIMARY KEY (`id`),
              KEY `idx_nhat_ky_nguoi_dung` (`nguoi_dung_id`),
              KEY `idx_nhat_ky_thoi_gian` (`thoi_gian`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """);

            const string sql = """
            SELECT * FROM (
                -- 1. Nhật ký hoạt động chi tiết từ bảng nhat_ky_hoat_dong
                SELECT
                    nk.id AS Id,
                    nk.nguoi_dung_id AS NguoiDungId,
                    COALESCE(nk.ten_dang_nhap, nd.ten_dang_nhap, '') AS TenDangNhap,
                    COALESCE(nk.ho_ten, nd.ho_ten, nk.ten_dang_nhap, 'Nhân viên') AS HoTen,
                    COALESCE(nk.vai_tro, (SELECT vt.ten_vai_tro FROM nguoi_dung_vai_tro ndvt JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id WHERE ndvt.nguoi_dung_id = nk.nguoi_dung_id LIMIT 1), 'Nhân viên') AS VaiTro,
                    nk.hanh_dong AS HanhDong,
                    nk.doi_tuong AS DoiTuong,
                    nk.ma_chung_tu AS MaChungTu,
                    nk.noi_dung AS NoiDung,
                    nk.gia_tri AS GiaTri,
                    nk.thoi_gian AS ThoiGian
                FROM nhat_ky_hoat_dong nk
                LEFT JOIN nguoi_dung nd ON nk.nguoi_dung_id = nd.id
                WHERE LOWER(COALESCE(nk.ten_dang_nhap, nd.ten_dang_nhap, '')) != 'admin'
                  AND (nk.vai_tro IS NULL OR LOWER(nk.vai_tro) NOT LIKE '%quan_tri%')

                UNION ALL

                -- 2. Hóa đơn bán hàng từ tài khoản nhân viên
                SELECT
                    CAST(hd.id AS UNSIGNED) AS Id,
                    hd.nguoi_ban_id AS NguoiDungId,
                    nd.ten_dang_nhap AS TenDangNhap,
                    COALESCE(nd.ho_ten, nd.ten_dang_nhap) AS HoTen,
                    COALESCE((SELECT vt.ten_vai_tro FROM nguoi_dung_vai_tro ndvt JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id WHERE ndvt.nguoi_dung_id = nd.id LIMIT 1), 'Thu ngân') AS VaiTro,
                    'Bán hàng' AS HanhDong,
                    'HOA_DON' AS DoiTuong,
                    hd.ma_hoa_don AS MaChungTu,
                    CASE
                        WHEN hd.trang_thai_hoa_don = 'HUY' THEN CONCAT('Hủy đơn bán ', hd.ma_hoa_don)
                        WHEN COALESCE(kh.ten_khach_hang, '') != '' THEN CONCAT('Bán hàng cho ', kh.ten_khach_hang)
                        ELSE 'Bán hàng cho khách lẻ'
                    END AS NoiDung,
                    hd.khach_can_tra AS GiaTri,
                    hd.thoi_gian_ban AS ThoiGian
                FROM hoa_don_ban_hang hd
                INNER JOIN nguoi_dung nd ON hd.nguoi_ban_id = nd.id
                LEFT JOIN khach_hang kh ON hd.khach_hang_id = kh.id
                WHERE LOWER(nd.ten_dang_nhap) != 'admin'
                  AND nd.id NOT IN (SELECT ndvt2.nguoi_dung_id FROM nguoi_dung_vai_tro ndvt2 JOIN vai_tro vt2 ON vt2.id = ndvt2.vai_tro_id WHERE vt2.ma_vai_tro = 'QUAN_TRI')
                  AND hd.ma_hoa_don NOT IN (SELECT COALESCE(ma_chung_tu, '') FROM nhat_ky_hoat_dong WHERE ma_chung_tu IS NOT NULL)

                UNION ALL

                -- 3. Phiếu nhập hàng từ tài khoản nhân viên
                SELECT
                    CAST(pn.id AS UNSIGNED) AS Id,
                    pn.nguoi_nhap_id AS NguoiDungId,
                    nd.ten_dang_nhap AS TenDangNhap,
                    COALESCE(nd.ho_ten, nd.ten_dang_nhap) AS HoTen,
                    COALESCE((SELECT vt.ten_vai_tro FROM nguoi_dung_vai_tro ndvt JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id WHERE ndvt.nguoi_dung_id = nd.id LIMIT 1), 'Kho') AS VaiTro,
                    'Nhập hàng' AS HanhDong,
                    'PHIEU_NHAP' AS DoiTuong,
                    pn.ma_phieu_nhap AS MaChungTu,
                    CASE
                        WHEN pn.trang_thai = 'HUY' THEN CONCAT('Hủy phiếu nhập ', pn.ma_phieu_nhap)
                        WHEN COALESCE(ncc.ten_nha_cung_cap, '') != '' THEN CONCAT('Nhập hàng từ ', ncc.ten_nha_cung_cap)
                        ELSE 'Nhập kho hàng hóa'
                    END AS NoiDung,
                    pn.tong_tien_nhap AS GiaTri,
                    pn.thoi_gian_nhap AS ThoiGian
                FROM phieu_nhap_hang pn
                INNER JOIN nguoi_dung nd ON pn.nguoi_nhap_id = nd.id
                LEFT JOIN nha_cung_cap ncc ON pn.nha_cung_cap_id = ncc.id
                WHERE LOWER(nd.ten_dang_nhap) != 'admin'
                  AND nd.id NOT IN (SELECT ndvt2.nguoi_dung_id FROM nguoi_dung_vai_tro ndvt2 JOIN vai_tro vt2 ON vt2.id = ndvt2.vai_tro_id WHERE vt2.ma_vai_tro = 'QUAN_TRI')
                  AND pn.ma_phieu_nhap NOT IN (SELECT COALESCE(ma_chung_tu, '') FROM nhat_ky_hoat_dong WHERE ma_chung_tu IS NOT NULL)

                UNION ALL

                -- 4. Phiếu sổ quỹ từ tài khoản nhân viên
                SELECT
                    CAST(pc.id AS UNSIGNED) AS Id,
                    pc.nguoi_thuc_hien_id AS NguoiDungId,
                    nd.ten_dang_nhap AS TenDangNhap,
                    COALESCE(nd.ho_ten, nd.ten_dang_nhap) AS HoTen,
                    COALESCE((SELECT vt.ten_vai_tro FROM nguoi_dung_vai_tro ndvt JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id WHERE ndvt.nguoi_dung_id = nd.id LIMIT 1), 'Nhân viên') AS VaiTro,
                    CASE 
                        WHEN pc.trang_thai = 'HUY' THEN 'Hủy sổ quỹ'
                        WHEN pc.hoa_don_ban_hang_id IS NOT NULL THEN 'Thu tiền'
                        WHEN pc.so_tien >= 0 THEN 'Thu tiền'
                        ELSE 'Chi tiền'
                    END AS HanhDong,
                    'SO_QUY' AS DoiTuong,
                    pc.ma_phieu_chi AS MaChungTu,
                    COALESCE(NULLIF(TRIM(pc.noi_dung), ''), 'Giao dịch thu chi sổ quỹ') AS NoiDung,
                    pc.so_tien AS GiaTri,
                    pc.thoi_gian_chi AS ThoiGian
                FROM phieu_chi pc
                INNER JOIN nguoi_dung nd ON pc.nguoi_thuc_hien_id = nd.id
                WHERE LOWER(nd.ten_dang_nhap) != 'admin'
                  AND nd.id NOT IN (SELECT ndvt2.nguoi_dung_id FROM nguoi_dung_vai_tro ndvt2 JOIN vai_tro vt2 ON vt2.id = ndvt2.vai_tro_id WHERE vt2.ma_vai_tro = 'QUAN_TRI')
                  AND pc.ma_phieu_chi NOT IN (SELECT COALESCE(ma_chung_tu, '') FROM nhat_ky_hoat_dong WHERE ma_chung_tu IS NOT NULL)
            ) AS hoat_dong_sub
            ORDER BY ThoiGian DESC
            LIMIT 100;
            """;

            return (
                await connection.QueryAsync<TongQuanHoatDongTaiKhoanCon>(
                    new CommandDefinition(sql, cancellationToken: token))
            ).AsList();
        }
        catch (Exception ex)
        {
            Helpers.SafeLog.WriteError("TaiHoatDongTaiKhoanConAsync error", ex);
            return [];
        }
    }

    /// <summary>
    /// Top 10 hàng bán chạy — GROUP BY ở tầng database, chỉ trả về đúng 10 dòng.
    /// </summary>
    private async Task<List<TongQuanHangTop>> TaiTopHangBanChayAsync(
        string xepHang,
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        var theoDoanhThu = xepHang == TongQuanXepHang.TheoDoanhThu;

        // Lưu ý: cotSapXep đã là biểu thức SUM(...) hoàn chỉnh nên không bọc SUM
        // thêm lần nữa, nếu không MariaDB báo "Invalid use of group function".
        var cotSapXep = theoDoanhThu ? "SUM(ct.thanh_tien)" : "SUM(ct.so_luong)";

        var sql = $"""
        SELECT
            ct.san_pham_id AS Id,
            COALESCE(NULLIF(TRIM(ct.ten_san_pham), ''), 'Sản phẩm đã xoá') AS Ten,
            COALESCE(NULLIF(TRIM(ct.ma_san_pham), ''), '-') AS TenPhu,
            COALESCE(SUM(ct.so_luong), 0) AS SoLuong,
            COALESCE(SUM(ct.thanh_tien), 0) AS DoanhThu,
            COALESCE({cotSapXep}, 0) AS GiaTriXepHang
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd
            ON hd.id = ct.hoa_don_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay
        GROUP BY ct.san_pham_id, Ten, TenPhu
        ORDER BY {cotSapXep} DESC, ct.ten_san_pham ASC
        LIMIT 10;
        """;

        var hangDuLieu = (
            await connection.QueryAsync<(uint Id, string Ten, string TenPhu, decimal SoLuong, decimal DoanhThu, decimal GiaTriXepHang)>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TuNgay = tuNgay,
                        DenNgay = denNgay
                    },
                    cancellationToken: token))
        ).AsList();

        var maxGiaTri = hangDuLieu.Count == 0 ? 0m : hangDuLieu.Max(r => r.GiaTriXepHang);

        var kq = new List<TongQuanHangTop>(hangDuLieu.Count);

        for (var i = 0; i < hangDuLieu.Count; i++)
        {
            var r = hangDuLieu[i];

            kq.Add(new TongQuanHangTop
            {
                ThuTu = i + 1,
                Ten = r.Ten,
                TenPhu = r.TenPhu,
                SoLuong = r.SoLuong,
                DoanhThu = r.DoanhThu,
                TyLePhanTram = maxGiaTri <= 0 ? 0 : (double)(r.GiaTriXepHang / maxGiaTri * 100m)
            });
        }

        return kq;
    }

    /// <summary>
    /// Top 10 khách hàng. Hóa đơn không gắn khách hàng ("khách lẻ") gộp chung
    /// thành một nhóm đúng như tên hiển thị sẵn có của phần mềm, không tạo
    /// khách hàng ảo trong database.
    /// </summary>
    private async Task<List<TongQuanHangTop>> TaiTopKhachHangAsync(
        DateTime tuNgay,
        DateTime denNgay,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            COALESCE(kh.id, 0) AS Id,
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS Ten,
            COALESCE(kh.ma_khach_hang, 'Khách lẻ') AS TenPhu,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @TuNgay
          AND hd.thoi_gian_ban <= @DenNgay
        GROUP BY kh.id, Ten, TenPhu
        ORDER BY DoanhThu DESC, Ten ASC
        LIMIT 10;
        """;

        var hangDuLieu = (
            await connection.QueryAsync<(uint Id, string Ten, string TenPhu, int SoHoaDon, decimal DoanhThu)>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TuNgay = tuNgay,
                        DenNgay = denNgay
                    },
                    cancellationToken: token))
        ).AsList();

        var maxDoanhThu = hangDuLieu.Count == 0 ? 0m : hangDuLieu.Max(r => r.DoanhThu);

        var kq = new List<TongQuanHangTop>(hangDuLieu.Count);

        for (var i = 0; i < hangDuLieu.Count; i++)
        {
            var r = hangDuLieu[i];

            kq.Add(new TongQuanHangTop
            {
                ThuTu = i + 1,
                Ten = r.Ten,
                TenPhu = $"{r.SoHoaDon:N0} hóa đơn",
                SoLuong = r.SoHoaDon,
                DoanhThu = r.DoanhThu,
                TyLePhanTram = maxDoanhThu <= 0 ? 0 : (double)(r.DoanhThu / maxDoanhThu * 100m)
            });
        }

        return kq;
    }

    /// <summary>
    /// Thống kê kho. "Sắp hết"/"hết hàng" lấy đúng quy tắc đang dùng ở tab Hàng hóa:
    /// tồn &lt;= 0 là hết hàng, tồn &lt;= ngưỡng tồn tối thiểu là sắp hết.
    /// Giá trị tồn kho lấy từ <c>ton_kho.so_luong_ton * gia_von_binh_quan</c>.
    /// </summary>
    private async Task<TongQuanThongKeKho> TaiThongKeKhoAsync(CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            (SELECT COUNT(*) FROM san_pham WHERE trang_thai = 1) AS TongSoHangHoa,
            (SELECT COUNT(*)
             FROM ton_kho tk
             INNER JOIN san_pham sp ON sp.id = tk.san_pham_id
             WHERE tk.so_luong_ton > 0
               AND tk.so_luong_ton <= sp.ton_toi_thieu) AS HangSapHet,
            (SELECT COUNT(*)
             FROM ton_kho tk
             INNER JOIN san_pham sp ON sp.id = tk.san_pham_id
             WHERE tk.so_luong_ton <= 0) AS HangHetHang,
            (SELECT COUNT(*) FROM khach_hang WHERE trang_thai = 1) AS TongSoKhachHang,
            (SELECT COUNT(*) FROM nha_cung_cap WHERE trang_thai = 1) AS TongSoNhaCungCap,
            (SELECT COALESCE(SUM(tk.so_luong_ton * tk.gia_von_binh_quan), 0)
             FROM ton_kho tk) AS GiaTriTonKho;
        """;

        return await connection.QuerySingleAsync<TongQuanThongKeKho>(
            new CommandDefinition(sql, cancellationToken: token));
    }
}