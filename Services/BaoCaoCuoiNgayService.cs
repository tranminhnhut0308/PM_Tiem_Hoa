using Dapper;
using MySqlConnector;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Các truy vấn cho màn hình "Báo cáo cuối ngày".
/// <para>
/// Nguyên tắc (đồng nhất với <see cref="DashboardService"/>):
/// mọi phép tính COUNT/SUM/GROUP BY/ORDER BY/LIMIT đều để database thực hiện,
/// không kéo toàn bộ bảng về RAM rồi tính ở client, không hard-code số liệu,
/// không tạo bảng mới.
/// </para>
/// <para>
/// Quy tắc doanh thu — dùng chung với màn Hóa đơn và màn Tổng quan:
/// chỉ tính hóa đơn <c>trang_thai_hoa_don = 'HOAN_THANH'</c>;
/// doanh thu = <c>khach_can_tra</c>; đã thu = <c>khach_da_thanh_toan</c>;
/// công nợ = <c>con_no</c>; tiền thối = <c>thu_khac</c>.
/// Tiền khách đưa (<c>khach_da_thanh_toan + thu_khac</c>) không bao giờ được
/// tính là doanh thu.
/// </para>
/// <para>
/// Mọi truy vấn đều lọc theo khoảng nửa mở
/// <c>thoi_gian &gt;= @DauNgay AND thoi_gian &lt; @NgayKeTiep</c> nên không bị
/// sai khi cột thời gian có giờ/phút/giây.
/// </para>
/// <para>
/// Quy tắc sổ quỹ — dùng chung với <c>DatabaseService.GetSoQuyTongHopTheoLocAsync</c>:
/// thu khi mã phiếu bắt đầu bằng "PT", chi khi bắt đầu bằng "PC",
/// chỉ tính phiếu <c>trang_thai = 'HOAN_THANH'</c>.
/// </para>
/// </summary>
public sealed class BaoCaoCuoiNgayService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public BaoCaoCuoiNgayService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    /// <summary>Số giờ trong một ngày để dựng trục biểu đồ đủ 24 cột.</summary>
    private const int SoGioTrongNgay = 24;

    /// <summary>Số dòng tối đa cho các bảng xếp hạng trong báo cáo.</summary>
    private const int GioiHanTop = 10;

    /// <summary>
    /// Mốc thời gian cho một ngày: từ 00:00:00 ngày đó tới 00:00:00 ngày kế tiếp.
    /// Dùng khoảng nửa mở để không bỏ sót hay lặp bản ghi có đủ giờ/phút/giây.
    /// </summary>
    public static (DateTime DauNgay, DateTime NgayKeTiep) TinhMocNgay(DateTime ngay)
        => (ngay.Date, ngay.Date.AddDays(1));

    /// <summary>
    /// Tải toàn bộ số liệu của một ngày. Các nhóm truy vấn độc lập được chạy
    /// song song, mỗi nhóm chỉ lấy đúng số dòng cần hiển thị.
    /// </summary>
    public async Task<BaoCaoCuoiNgayDuLieu> TaiBaoCaoAsync(
        DateTime ngay,
        CancellationToken token = default)
    {
        var (dauNgay, ngayKeTiep) = TinhMocNgay(ngay);

        var duLieu = new BaoCaoCuoiNgayDuLieu
        {
            Ngay = dauNgay,
            NgayKeTiep = ngayKeTiep
        };

        var taskTongKet = TaiTongKetBanHangAsync(dauNgay, ngayKeTiep, token);
        var taskPhuongThuc = TaiDoanhThuTheoPhuongThucAsync(dauNgay, ngayKeTiep, token);
        var taskTopSanPham = TaiTopSanPhamAsync(dauNgay, ngayKeTiep, token);
        var taskKhachHang = TaiKhachHangTrongNgayAsync(dauNgay, ngayKeTiep, token);
        var taskCongNo = TaiCongNoPhatSinhTrongNgayAsync(dauNgay, ngayKeTiep, token);
        var taskSoQuy = TaiThuChiTrongNgayAsync(dauNgay, ngayKeTiep, token);
        var taskNhapHang = TaiThongKeNhapHangAsync(dauNgay, ngayKeTiep, token);

        await Task.WhenAll(taskTongKet, taskPhuongThuc, taskTopSanPham,
            taskKhachHang, taskCongNo, taskSoQuy, taskNhapHang);

        duLieu.TongKetBanHang = await taskTongKet;
        duLieu.PhuongThucThanhToan = await taskPhuongThuc;
        duLieu.TopSanPham = await taskTopSanPham;

        var (khachTongHop, topKhach) = await taskKhachHang;
        duLieu.KhachHangTongHop = khachTongHop;
        duLieu.TopKhachHang = topKhach;

        var (congNoTongHop, congNoRows) = await taskCongNo;
        duLieu.CongNoTongHop = congNoTongHop;
        duLieu.CongNoPhatSinh = congNoRows;

        duLieu.DoanhThuTheoGio = await TaiDoanhThuTheoGioAsync(dauNgay, ngayKeTiep, token);

        var (soQuyTongHop, thuChi) = await taskSoQuy;
        duLieu.SoQuyTongHop = soQuyTongHop;
        duLieu.ThuChi = thuChi;

        duLieu.NhapHang = await taskNhapHang;

        return duLieu;
    }

    // ============================================================
    // 1. TỔNG KẾT BÁN HÀNG
    // ============================================================

    /// <summary>
    /// Tổng kết bán hàng trong ngày — 1 câu COUNT/SUM duy nhất.
    /// <c>thanh_toan_da_thanh_toan</c> / <c>thanh_toan_con_no</c> đếm số hóa đơn
    /// theo trạng thái thanh toán, không cần truy vấn thêm lần nữa.
    /// </summary>
    private async Task<BaoCaoTongKetBanHang> TaiTongKetBanHangAsync(
        DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            COUNT(*)                                          AS SoHoaDon,
            COALESCE(SUM(hd.tong_tien_hang), 0)               AS TongTienHang,
            COALESCE(SUM(hd.giam_gia), 0)                     AS GiamGia,
            COALESCE(SUM(hd.khach_can_tra), 0)                AS DoanhThu,
            COALESCE(SUM(hd.khach_da_thanh_toan), 0)          AS DaThu,
            COALESCE(SUM(hd.con_no), 0)                       AS ConNo,
            COALESCE(SUM(hd.thu_khac), 0)                     AS TienThua,
            COALESCE(SUM(hd.trang_thai_thanh_toan = 'DA_THANH_TOAN'), 0) AS SoHoaDonDaThanhToan,
            COALESCE(SUM(hd.trang_thai_thanh_toan <> 'DA_THANH_TOAN'), 0) AS SoHoaDonConCongNo
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @NgayKeTiep;
        """;

        var row = await connection.QueryFirstOrDefaultAsync<TongKetRow>(
            new CommandDefinition(sql,
                new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep },
                cancellationToken: token));

        return new BaoCaoTongKetBanHang
        {
            SoHoaDon = row?.SoHoaDon ?? 0,
            TongTienHang = row?.TongTienHang ?? 0m,
            GiamGia = row?.GiamGia ?? 0m,
            DoanhThu = row?.DoanhThu ?? 0m,
            DaThu = row?.DaThu ?? 0m,
            ConNo = row?.ConNo ?? 0m,
            TienThua = row?.TienThua ?? 0m,
            SoHoaDonDaThanhToan = row?.SoHoaDonDaThanhToan ?? 0,
            SoHoaDonConCongNo = row?.SoHoaDonConCongNo ?? 0
        };
    }

    private sealed class TongKetRow
    {
        public int SoHoaDon { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal DoanhThu { get; set; }
        public decimal DaThu { get; set; }
        public decimal ConNo { get; set; }
        public decimal TienThua { get; set; }
        public int SoHoaDonDaThanhToan { get; set; }
        public int SoHoaDonConCongNo { get; set; }
    }

    // ============================================================
    // 2. DOANH THU THEO GIỜ
    // ============================================================

    /// <summary>
    /// Doanh thu theo giờ. SQL chỉ trả về các giờ thực sự có hóa đơn; phần trục
    /// 24 giờ được dựng ở C# từ kết quả đó, không cần đọc thêm dữ liệu.
    /// </summary>
    private async Task<List<BaoCaoDiemGio>> TaiDoanhThuTheoGioAsync(
        DateTime dauNgay,
        DateTime ngayKeTiep,
        CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            HOUR(hd.thoi_gian_ban)       AS Gio,
            COALESCE(SUM(hd.khach_can_tra), 0) AS DoanhThu,
            COUNT(*)                     AS SoHoaDon
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @NgayKeTiep
        GROUP BY HOUR(hd.thoi_gian_ban);
        """;

        var hangGio = (await connection.QueryAsync<(int Gio, decimal DoanhThu, int SoHoaDon)>(
            new CommandDefinition(sql,
                new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep },
                cancellationToken: token))).AsList();

        var theoGio = new Dictionary<int, (decimal DoanhThu, int SoHoaDon)>();

        foreach (var r in hangGio)
        {
            theoGio[r.Gio] = (r.DoanhThu, r.SoHoaDon);
        }

        // Trục đủ 24 giờ: giờ không có hóa đơn vẫn giữ để biểu đồ không bị "nhảy".
        var diem = new List<BaoCaoDiemGio>(SoGioTrongNgay);

        for (var gio = 0; gio < SoGioTrongNgay; gio++)
        {
            var co = theoGio.TryGetValue(gio, out var gt);

            diem.Add(new BaoCaoDiemGio
            {
                Gio = gio,
                DoanhThu = co ? gt.DoanhThu : 0m,
                SoHoaDon = co ? gt.SoHoaDon : 0
            });
        }

        var maxDoanhThu = diem.Count == 0 ? 0m : diem.Max(p => p.DoanhThu);

        foreach (var p in diem)
        {
            p.TyLeChieuCao = maxDoanhThu <= 0 ? 0 : (double)(p.DoanhThu / maxDoanhThu * 100m);
        }

        return diem;
    }

    // ============================================================
    // 3. DOANH THU THEO PHƯƠNG THỨC THANH TOÁN
    // ============================================================

    /// <summary>
    /// Số tiền thu trong ngày theo phương thức thanh toán.
    /// <para>
    /// <b>Nguồn và giới hạn:</b> đọc từ <c>phieu_chi.phuong_thuc</c> của sổ quỹ.
    /// Bảng <c>thanh_toan</c> tuy có cột <c>phuong_thuc</c> và có khoá
    /// <c>hoa_don_id</c> nhưng đang rỗng (0 dòng) và không mã nào của ứng dụng
    /// truy vấn tới, nên dùng nó thì báo cáo sẽ trống dù có bán hàng thật.
    /// </para>
    /// <para>
    /// Số ở đây là <b>dòng tiền sổ quỹ</b>, không phải doanh thu hóa đơn. Cột
    /// <c>SoTienTuBanHang</c> tách riêng phần gắn hóa đơn để người dùng đối
    /// chiếu, tuyệt đối không cộng vào doanh thu.
    /// </para>
    /// <para>
    /// Các dòng lấy từ dữ liệu thực tế (GROUP BY trên chính database), nên nếu
    /// database chỉ có "TIEN_MAT" thì bảng chỉ hiện một dòng — không tự thêm
    /// dòng QR/ví điện tử giả.
    /// </para>
    /// </summary>
    private async Task<List<BaoCaoPhuongThucRow>> TaiDoanhThuTheoPhuongThucAsync(
        DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            CASE
                WHEN pc.phuong_thuc IS NULL OR TRIM(pc.phuong_thuc) = '' THEN 'KHONG_ROI'
                ELSE UPPER(TRIM(pc.phuong_thuc))
            END                      AS PhuongThuc,
            COUNT(*)                 AS SoGiaoDich,
            COALESCE(SUM(pc.so_tien), 0) AS SoTien,
            COALESCE(SUM(
                CASE WHEN pc.hoa_don_ban_hang_id IS NOT NULL THEN pc.so_tien ELSE 0 END
            ), 0)                  AS SoTienTuBanHang
        FROM phieu_chi pc
        WHERE pc.ma_phieu_chi LIKE 'PT%'
          AND pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi >= @DauNgay
          AND pc.thoi_gian_chi < @NgayKeTiep
        GROUP BY PhuongThuc
        ORDER BY SoTien DESC;
        """;

        var rows = (await connection.QueryAsync<(
            string PhuongThuc,
            int SoGiaoDich,
            decimal SoTien,
            decimal SoTienTuBanHang)>(
            new CommandDefinition(sql,
                new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep },
                cancellationToken: token))).AsList();

        var tong = rows.Sum(r => r.SoTien);

        return rows.Select(r => new BaoCaoPhuongThucRow
        {
            PhuongThuc = r.PhuongThuc,
            SoGiaoDich = r.SoGiaoDich,
            SoTien = r.SoTien,
            SoTienTuBanHang = r.SoTienTuBanHang,
            TyLePhanTram = tong <= 0 ? 0 : (double)(r.SoTien / tong * 100m)
        }).ToList();
    }

    // ============================================================
    // 4. TOP SẢN PHẨM BÁN CHẠY
    // ============================================================

    /// <summary>
    /// Top sản phẩm bán chạy trong ngày. GROUP BY ở tầng database và LIMIT 10 nên
    /// không kéo toàn bộ chi tiết hóa đơn về RAM, dù bảng chi tiết có hàng trăm
    /// nghìn dòng.
    /// </summary>
    private async Task<List<BaoCaoSanPhamRow>> TaiTopSanPhamAsync(
        DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            COALESCE(NULLIF(TRIM(ct.ten_san_pham), ''), 'Sản phẩm đã xoá') AS Ten,
            COALESCE(NULLIF(TRIM(ct.ma_san_pham), ''), '-')                  AS MaSanPham,
            COALESCE(SUM(ct.so_luong), 0)                                     AS SoLuong,
            COALESCE(SUM(ct.thanh_tien), 0)                                   AS DoanhThu
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd
            ON hd.id = ct.hoa_don_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @NgayKeTiep
        GROUP BY ct.san_pham_id, Ten, MaSanPham
        ORDER BY DoanhThu DESC, SoLuong DESC, Ten ASC
        LIMIT 10;
        """;

        var rows = (await connection.QueryAsync<(string Ten, string MaSanPham, decimal SoLuong, decimal DoanhThu)>(
            new CommandDefinition(sql,
                new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep },
                cancellationToken: token))).AsList();

        var tong = rows.Sum(r => r.DoanhThu);

        var kq = new List<BaoCaoSanPhamRow>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            kq.Add(new BaoCaoSanPhamRow
            {
                ThuTu = i + 1,
                Ten = rows[i].Ten,
                MaSanPham = rows[i].MaSanPham,
                SoLuong = rows[i].SoLuong,
                DoanhThu = rows[i].DoanhThu,
                TyLePhanTram = tong <= 0 ? 0 : (double)(rows[i].DoanhThu / tong * 100m)
            });
        }

        return kq;
    }

    // ============================================================
    // 5. KHÁCH HÀNG TRONG NGÀY
    // ============================================================

/// <summary>
    /// Khách hàng trong ngày + bảng Top khách.
    /// <para>
    /// "Khách mới" là khách có lần mua hoàn thành đầu tiên rơi đúng vào ngày đang
    /// xem; "khách quay lại" là khách đã có hóa đơn trước ngày đó rồi mua tiếp trong
    /// ngày. Hai số này đếm theo <b>số khách</b> chứ không phải số hóa đơn: SQL
    /// gom theo <c>khach_hang_id</c> trước rồi mới đếm, nên khách mua 3 hóa đơn
    /// trong ngày vẫn chỉ tính một lần.
    /// </para>
    /// <para>
    /// Chỉ khách có hồ sơ (<c>khach_hang_id IS NOT NULL</c>) mới xếp vào mới /
    /// quay lại. Hóa đơn không gắn hồ sơ gộp thành một dòng "Khách lẻ" ở bảng
    /// Top khách và không thuộc nhóm nào trong hai thẻ đếm.
    /// </para>
    /// </summary>
    private async Task<(BaoCaoKhachHangTongHop TongHop, List<BaoCaoKhachHangRow> Top)>
        TaiKhachHangTrongNgayAsync(DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        // Gom theo khách trước, đếm sau: tránh một khách mua nhiều hóa đơn trong
        // ngày bị tính thành nhiều "khách mới".
        const string sqlTongHop = """
        SELECT
            COUNT(*) AS SoKhachDaMua,
            COALESCE(SUM(CASE WHEN DaMuaTruocDo = 0 THEN 1 ELSE 0 END), 0) AS SoKhachMoi,
            COALESCE(SUM(CASE WHEN DaMuaTruocDo > 0 THEN 1 ELSE 0 END), 0) AS SoKhachQuayLai,
            COALESCE(SUM(TongGiaTriMua), 0) AS TongGiaTriMua
        FROM (
            SELECT
                hd.khach_hang_id,
                COALESCE(SUM(hd.khach_can_tra), 0) AS TongGiaTriMua,
                (
                    SELECT COUNT(*)
                    FROM hoa_don_ban_hang truoc
                    WHERE truoc.khach_hang_id = hd.khach_hang_id
                      AND truoc.trang_thai_hoa_don = 'HOAN_THANH'
                      AND truoc.thoi_gian_ban < @DauNgay
                ) AS DaMuaTruocDo
            FROM hoa_don_ban_hang hd
            WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
              AND hd.thoi_gian_ban >= @DauNgay
              AND hd.thoi_gian_ban < @NgayKeTiep
              AND hd.khach_hang_id IS NOT NULL
            GROUP BY hd.khach_hang_id
        ) AS khach_trong_ngay;
        """;

        var thamSo = new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep };

        var tongHopRow = await connection.QueryFirstOrDefaultAsync<KhachTongHopRow>(
            new CommandDefinition(sqlTongHop, thamSo, cancellationToken: token));

        const string sqlTop = """
        SELECT
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS Ten,
            COALESCE(NULLIF(TRIM(kh.ma_khach_hang), ''), '')          AS MaKhachHang,
            COUNT(*)                        AS SoHoaDon,
            COALESCE(SUM(hd.khach_can_tra), 0)         AS TongMua,
            COALESCE(SUM(hd.khach_da_thanh_toan), 0)   AS DaThanhToan,
            COALESCE(SUM(hd.con_no), 0)                 AS ConNo
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @NgayKeTiep
        GROUP BY hd.khach_hang_id,
                 COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ'),
                 COALESCE(NULLIF(TRIM(kh.ma_khach_hang), ''), '')
        ORDER BY TongMua DESC, SoHoaDon DESC, Ten ASC
        LIMIT 10;
        """;

        var hangDuLieu = (await connection.QueryAsync<(string Ten, string MaKhachHang, int SoHoaDon, decimal TongMua, decimal DaThanhToan, decimal ConNo)>(
            new CommandDefinition(sqlTop, thamSo, cancellationToken: token))).AsList();

        var tong = hangDuLieu.Sum(r => r.TongMua);

        var top = new List<BaoCaoKhachHangRow>(hangDuLieu.Count);

        for (var i = 0; i < hangDuLieu.Count; i++)
        {
            top.Add(new BaoCaoKhachHangRow
            {
                ThuTu = i + 1,
                Ten = hangDuLieu[i].Ten,
                MaKhachHang = hangDuLieu[i].MaKhachHang,
                SoHoaDon = hangDuLieu[i].SoHoaDon,
                TongMua = hangDuLieu[i].TongMua,
                DaThanhToan = hangDuLieu[i].DaThanhToan,
                ConNo = hangDuLieu[i].ConNo,
                TyLePhanTram = tong <= 0 ? 0 : (double)(hangDuLieu[i].TongMua / tong * 100m)
            });
        }

        return (new BaoCaoKhachHangTongHop
        {
            SoKhachDaMua = tongHopRow?.SoKhachDaMua ?? 0,
            SoKhachMoi = tongHopRow?.SoKhachMoi ?? 0,
            SoKhachQuayLai = tongHopRow?.SoKhachQuayLai ?? 0,
            TongGiaTriMua = tongHopRow?.TongGiaTriMua ?? 0m
        }, top);
    }

    private sealed class KhachTongHopRow
    {
        public int SoKhachDaMua { get; set; }
        public int SoKhachMoi { get; set; }
        public int SoKhachQuayLai { get; set; }
        public decimal TongGiaTriMua { get; set; }
    }

    // ============================================================
    // 6. CÔNG NỢ PHÁT SINH TRONG NGÀY
    // ============================================================

    /// <summary>
    /// Công nợ phát sinh trong ngày: tổng hợp + bảng chi tiết từng hóa đơn,
    /// lấy trực tiếp <c>khach_can_tra</c> / <c>khach_da_thanh_toan</c> /
    /// <c>con_no</c> nên khớp với màn Hóa đơn.
    /// </summary>
    private async Task<(BaoCaoCongNoTongHop TongHop, List<BaoCaoCongNoRow> Rows)>
        TaiCongNoPhatSinhTrongNgayAsync(DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sqlTongHop = """
        SELECT
            COUNT(*)                                   AS SoHoaDon,
            COALESCE(SUM(hd.khach_can_tra), 0)         AS TongCongNoPhatSinh,
            COALESCE(SUM(hd.khach_da_thanh_toan), 0)   AS TongDaTra,
            COALESCE(SUM(hd.con_no), 0)                 AS TongConNo,
            COALESCE(SUM(hd.trang_thai_thanh_toan <> 'DA_THANH_TOAN'), 0) AS SoHoaDonConNo
        FROM hoa_don_ban_hang hd
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @NgayKeTiep;
        """;

        var thamSo = new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep };

        var tongHopRow = await connection.QueryFirstOrDefaultAsync<CongNoTongHopRow>(
            new CommandDefinition(sqlTongHop, thamSo, cancellationToken: token));

        const string sqlRows = """
        SELECT
            COALESCE(NULLIF(TRIM(kh.ten_khach_hang), ''), 'Khách lẻ') AS TenKhachHang,
            hd.ma_hoa_don                                  AS MaHoaDon,
            hd.thoi_gian_ban                               AS ThoiGian,
            hd.tong_tien_hang                              AS TongTienHang,
            hd.giam_gia                                    AS GiamGia,
            hd.thu_khac                                    AS ThuKhac,
            hd.khach_can_tra                               AS DoanhThu,
            hd.khach_da_thanh_toan                         AS DaTra,
            hd.con_no                                      AS ConNo,
            COALESCE(hd.trang_thai_thanh_toan, '')          AS TrangThai
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= @DauNgay
          AND hd.thoi_gian_ban < @NgayKeTiep
        ORDER BY hd.thoi_gian_ban DESC, hd.ma_hoa_don DESC;
        """;

        var duLieu = (await connection.QueryAsync<CongNoRow>(
            new CommandDefinition(sqlRows, thamSo, cancellationToken: token))).AsList();

        var rows = duLieu.Select(r => new BaoCaoCongNoRow
        {
            TenKhachHang = r.TenKhachHang,
            MaHoaDon = r.MaHoaDon,
            ThoiGian = r.ThoiGian,
            TongTienHang = r.TongTienHang,
            GiamGia = r.GiamGia,
            ThuKhac = r.ThuKhac,
            DoanhThu = r.DoanhThu,
            DaTra = r.DaTra,
            ConNo = r.ConNo,
            TrangThai = r.TrangThai
        }).ToList();

        return (new BaoCaoCongNoTongHop
        {
            SoHoaDon = tongHopRow?.SoHoaDon ?? 0,
            TongCongNoPhatSinh = tongHopRow?.TongCongNoPhatSinh ?? 0m,
            TongDaTra = tongHopRow?.TongDaTra ?? 0m,
            TongConNo = tongHopRow?.TongConNo ?? 0m,
            SoHoaDonConNo = tongHopRow?.SoHoaDonConNo ?? 0
        }, rows);
    }

    private sealed class CongNoTongHopRow
    {
        public int SoHoaDon { get; set; }
        public decimal TongCongNoPhatSinh { get; set; }
        public decimal TongDaTra { get; set; }
        public decimal TongConNo { get; set; }
        public int SoHoaDonConNo { get; set; }
    }

    private sealed class CongNoRow
    {
        public string TenKhachHang { get; set; } = "";
        public string MaHoaDon { get; set; } = "";
        public DateTime ThoiGian { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal ThuKhac { get; set; }
        public decimal DoanhThu { get; set; }
        public decimal DaTra { get; set; }
        public decimal ConNo { get; set; }
        public string TrangThai { get; set; } = "";
    }

    // ============================================================
    // 7. SỔ QUỸ CUỐI NGÀY
    // ============================================================

    /// <summary>
    /// Thu - chi trong ngày từ bảng <c>phieu_chi</c> của sổ quỹ hiện có.
    /// Quy tắc thu/chi giống hệt màn Sổ quỹ: mã phiếu "PT%" là thu, "PC%" là
    /// chi, chỉ tính phiếu hoàn thành.
    /// </summary>
    private async Task<(BaoCaoSoQuyTongHop TongHop, List<BaoCaoThuChiRow> Rows)>
        TaiThuChiTrongNgayAsync(DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sqlTongHop = """
        SELECT
            COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN pc.so_tien ELSE 0 END), 0) AS TongThu,
            COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PC%' THEN pc.so_tien ELSE 0 END), 0) AS TongChi,
            COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN 1 ELSE 0 END), 0)         AS SoPhieuThu,
            COALESCE(SUM(CASE WHEN pc.ma_phieu_chi LIKE 'PC%' THEN 1 ELSE 0 END), 0)         AS SoPhieuChi
        FROM phieu_chi pc
        WHERE pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi >= @DauNgay
          AND pc.thoi_gian_chi < @NgayKeTiep;
        """;

        var thamSo = new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep };

        var tongHopRow = await connection.QueryFirstOrDefaultAsync<SoQuyTongHopRow>(
            new CommandDefinition(sqlTongHop, thamSo, cancellationToken: token));

        const string sqlRows = """
        SELECT
            pc.thoi_gian_chi AS ThoiGian,
            CASE WHEN pc.ma_phieu_chi LIKE 'PT%' THEN 'THU' ELSE 'CHI' END AS Loai,
            COALESCE(
                NULLIF(TRIM(pc.noi_dung), ''),
                CASE
                    WHEN pc.hoa_don_ban_hang_id IS NOT NULL THEN 'Thu tiền bán hàng'
                    WHEN pc.ma_phieu_chi LIKE 'PT%' THEN 'Ghi nhận thu'
                    ELSE 'Ghi nhận chi'
                END
            ) AS NoiDung,
            pc.ma_phieu_chi AS MaChungTu,
            COALESCE(pc.phuong_thuc, '') AS PhuongThuc,
            ABS(pc.so_tien) AS SoTien
        FROM phieu_chi pc
        WHERE pc.trang_thai = 'HOAN_THANH'
          AND pc.thoi_gian_chi >= @DauNgay
          AND pc.thoi_gian_chi < @NgayKeTiep
        ORDER BY pc.thoi_gian_chi DESC, pc.ma_phieu_chi DESC;
        """;

        var rows = (await connection.QueryAsync<ThuChiRow>(
            new CommandDefinition(sqlRows, thamSo, cancellationToken: token)))
            .Select(r => new BaoCaoThuChiRow
            {
                ThoiGian = r.ThoiGian,
                Loai = r.Loai,
                NoiDung = r.NoiDung,
                MaChungTu = r.MaChungTu,
                PhuongThuc = r.PhuongThuc,
                SoTien = r.SoTien
            }).ToList();

        var tongThu = tongHopRow?.TongThu ?? 0m;
        var tongChi = tongHopRow?.TongChi ?? 0m;

        return (new BaoCaoSoQuyTongHop
        {
            TongThu = tongThu,
            TongChi = tongChi,
            // Số dư phát sinh trong ngày = tổng thu - tổng chi.
            ChenhLech = tongThu - tongChi,
            SoPhieuThu = tongHopRow?.SoPhieuThu ?? 0,
            SoPhieuChi = tongHopRow?.SoPhieuChi ?? 0
        }, rows);
    }

    private sealed class SoQuyTongHopRow
    {
        public decimal TongThu { get; set; }
        public decimal TongChi { get; set; }
        public int SoPhieuThu { get; set; }
        public int SoPhieuChi { get; set; }
    }

    private sealed class ThuChiRow
    {
        public DateTime ThoiGian { get; set; }
        public string Loai { get; set; } = "";
        public string NoiDung { get; set; } = "";
        public string MaChungTu { get; set; } = "";
        public string PhuongThuc { get; set; } = "";
        public decimal SoTien { get; set; }
    }

    // ============================================================
    // 8. NHẬP HÀNG TRONG NGÀY
    // ============================================================

    /// <summary>
    /// Thống kê nhập hàng trong ngày. Tổng tiền nhập là <c>tong_tien_nhap</c>,
    /// đã trả là <c>da_thanh_toan</c>, còn nợ là <c>con_no</c> — cùng nguồn với
    /// màn Nhà cung cấp, nên báo cáo khớp với công nợ nhà cung cấp đang hiển thị.
    /// </summary>
    private async Task<BaoCaoNhapHangTongHop> TaiThongKeNhapHangAsync(
        DateTime dauNgay, DateTime ngayKeTiep, CancellationToken token)
    {
        await using var connection = await _taoKetNoi();

        const string sql = """
        SELECT
            COUNT(*)                              AS SoPhieu,
            COALESCE(SUM(pn.tong_tien_nhap), 0)    AS TongTienNhap,
            COALESCE(SUM(pn.da_thanh_toan), 0)     AS DaThanhToan,
            COALESCE(SUM(pn.con_no), 0)            AS ConNo
        FROM phieu_nhap_hang pn
        WHERE pn.trang_thai <> 'HUY'
          AND pn.thoi_gian_nhap >= @DauNgay
          AND pn.thoi_gian_nhap < @NgayKeTiep;
        """;

        var row = await connection.QueryFirstOrDefaultAsync<NhapHangRow>(
            new CommandDefinition(sql,
                new { DauNgay = dauNgay, NgayKeTiep = ngayKeTiep },
                cancellationToken: token));

        return new BaoCaoNhapHangTongHop
        {
            SoPhieu = row?.SoPhieu ?? 0,
            TongTienNhap = row?.TongTienNhap ?? 0m,
            DaThanhToan = row?.DaThanhToan ?? 0m,
            ConNo = row?.ConNo ?? 0m
        };
    }

    private sealed class NhapHangRow
    {
        public int SoPhieu { get; set; }
        public decimal TongTienNhap { get; set; }
        public decimal DaThanhToan { get; set; }
        public decimal ConNo { get; set; }
    }
}