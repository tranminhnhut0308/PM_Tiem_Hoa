using Dapper;
using MySqlConnector;
using ShopHoa.Helpers;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Nguồn dữ liệu duy nhất cho thông tin tiệm.
/// <para>
/// Nguyên tắc: toàn bộ màn hình và mọi mẫu hóa đơn đều đọc qua service này.
/// Không màn nào tự gọi database riêng cho thông tin tiệm, không màn nào giữ
/// bản sao riêng rồi tự cập nhật, và không chỗ nào hard-code tên tiệm /
/// địa chỉ / số điện thoại. Sửa ở một chỗ là mọi hóa đơn sinh ra sau đó tự
/// lấy thông tin mới.
/// </para>
/// <para>
/// Bảng <c>thong_tin_tiem</c> chỉ có một bản ghi (id = 1). Lần lưu luôn UPDATE
/// đúng bản ghi đó nên không tích tụ bản ghi theo số lần bấm "Lưu".
/// </para>
/// </summary>
public sealed class StoreInfoService
{
    private readonly Func<Task<MySqlConnection>> _taoKetNoi;

    public StoreInfoService(Func<Task<MySqlConnection>> taoKetNoi)
    {
        _taoKetNoi = taoKetNoi;
    }

    // =========================================================
    // BẢNG LƯU TRỮ
    // =========================================================

    /// <summary>
    /// Bảo đảm bảng <c>thong_tin_tiem</c> tồn tại và có đúng một bản ghi.
    /// <para>
    /// Database cũ chưa có bảng này thì tự tạo, không bắt người dùng chạy
    /// script tay. Lỗi tạo bảng (không đủ quyền DDL, mất mạng) không làm sập
    /// ứng dụng: service trả về thông tin mặc định và ghi log, đọc/ghi sau đó
    /// sẽ báo lỗi rõ ràng nếu thật sự không truy cập được.
    /// </para>
    /// </summary>
    private async Task<bool> DamBaoBangAsync()
    {
        try
        {
            await using var connection = await _taoKetNoi();

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS thong_tin_tiem (
                    id             int(10) UNSIGNED NOT NULL,
                    ten_tiem       varchar(200) NOT NULL DEFAULT '',
                    dia_chi        varchar(300) NOT NULL DEFAULT '',
                    so_dien_thoai  varchar(30)  NOT NULL DEFAULT '',
                    email          varchar(120) NOT NULL DEFAULT '',
                    ma_so_thue     varchar(30)  NOT NULL DEFAULT '',
                    website        varchar(200) NOT NULL DEFAULT '',
                    duong_dan_logo varchar(500) NOT NULL DEFAULT '',
                    loi_cam_on     varchar(500) NOT NULL DEFAULT '',
                    ngay_cap_nhat  datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (id),
                    CONSTRAINT ck_thong_tin_tiem_id CHECK (id = 1)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);

            // INSERT IGNORE để bản ghi duy nhất luôn tồn tại: lần đầu chạy
            // ứng dụng hoặc lần sau đều không sinh thêm dòng.
            await connection.ExecuteAsync(
                "INSERT IGNORE INTO thong_tin_tiem (id, ten_tiem, loi_cam_on) "
                + "VALUES (1, 'VỰA HOA NHÀ HUY', 'Cảm ơn quý khách!');");

            return true;
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi tao bang thong_tin_tiem", ex);
            return false;
        }
    }

    // =========================================================
    // ĐỌC
    // =========================================================

    /// <summary>
    /// Đọc thông tin tiệm đang lưu. Không bao giờ trả null: khi bảng chưa có
    /// hoặc đọc lỗi thì trả về bản ghi mặc định để giao diện vẫn hiển thị
    /// được (không trắng màn, không crash).
    /// </summary>
    public async Task<ThongTinTiem> LayThongTinAsync()
    {
        await DamBaoBangAsync();

        try
        {
            await using var connection = await _taoKetNoi();

            const string sql = """
            SELECT
                id,
                ten_tiem       AS TenTiem,
                dia_chi        AS DiaChi,
                so_dien_thoai  AS SoDienThoai,
                email          AS Email,
                ma_so_thue     AS MaSoThue,
                website        AS Website,
                duong_dan_logo AS DuongDanLogo,
                loi_cam_on     AS LoiCamOn
            FROM thong_tin_tiem
            WHERE id = 1
            LIMIT 1;
            """;

            var info = await connection.QueryFirstOrDefaultAsync<ThongTinTiem>(sql);

            return info ?? new ThongTinTiem();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi doc thong tin tiem", ex);
            return new ThongTinTiem();
        }
    }

    /// <summary>
    /// Thông tin tiệm đã nạp sẵn logo, dùng cho các mẫu in nhanh (hóa đơn).
    /// </summary>
    public async Task<ThongTinTiem> LayThongTinKemLogoAsync()
    {
        var info = await LayThongTinAsync();
        NapLogo(info);
        return info;
    }

    // =========================================================
    // GHI
    // =========================================================

    /// <summary>
    /// Lưu thông tin tiệm. Bản ghi đã có thì UPDATE, chưa có thì INSERT — luôn
    /// một bản ghi duy nhất, không sinh dòng mới mỗi lần bấm "Lưu".
    /// </summary>
    public async Task LuuThongTinAsync(ThongTinTiem info)
    {
        await DamBaoBangAsync();

        await using var connection = await _taoKetNoi();

        // ON DUPLICATE KEY UPDATE xử lý cả hai nhánh trong một câu lệnh: bản ghi
        // có sẵn thì cập nhật tại chỗ, chưa có thì chèn, đều không tạo dòng thứ hai.
        const string sql = """
        INSERT INTO thong_tin_tiem
            (id, ten_tiem, dia_chi, so_dien_thoai, email, ma_so_thue, website, duong_dan_logo, loi_cam_on)
        VALUES
            (@Id, @TenTiem, @DiaChi, @SoDienThoai, @Email, @MaSoThue, @Website, @DuongDanLogo, @LoiCamOn)
        ON DUPLICATE KEY UPDATE
            ten_tiem       = VALUES(ten_tiem),
            dia_chi        = VALUES(dia_chi),
            so_dien_thoai  = VALUES(so_dien_thoai),
            email          = VALUES(email),
            ma_so_thue     = VALUES(ma_so_thue),
            website        = VALUES(website),
            duong_dan_logo = VALUES(duong_dan_logo),
            loi_cam_on     = VALUES(loi_cam_on),
            ngay_cap_nhat  = CURRENT_TIMESTAMP;
        """;

        await connection.ExecuteAsync(sql, new
        {
            Id = ThongTinTiem.IdMacDinh,
            TenTiem = (info.TenTiem ?? "").Trim(),
            DiaChi = (info.DiaChi ?? "").Trim(),
            SoDienThoai = (info.SoDienThoai ?? "").Trim(),
            Email = (info.Email ?? "").Trim(),
            MaSoThue = (info.MaSoThue ?? "").Trim(),
            Website = (info.Website ?? "").Trim(),
            DuongDanLogo = (info.DuongDanLogo ?? "").Trim(),
            LoiCamOn = (info.LoiCamOn ?? "").Trim()
        });
    }

    // =========================================================
    // LOGO
    // =========================================================

    /// <summary>
    /// Nạp logo từ đường dẫn đã lưu vào đối tượng. File không tồn tại hoặc không
    /// đọc được thì để trống — hóa đơn vẫn in bình thường, chỉ không có logo.
    /// </summary>
    public void NapLogo(ThongTinTiem info)
    {
        info.LogoBitmap = null;

        if (string.IsNullOrWhiteSpace(info.DuongDanLogo))
            return;

        try
        {
            if (File.Exists(info.DuongDanLogo))
                info.LogoBitmap = new Avalonia.Media.Imaging.Bitmap(info.DuongDanLogo);
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Loi nap logo tiem", ex);
            info.LogoBitmap = null;
        }
    }

    /// <summary>
    /// Chép file logo người dùng chọn vào thư mục dữ liệu ứng dụng rồi trả về
    /// đường dẫn đó.
    /// <para>
    /// Lưu bản sao trong thư mục ứng dụng thay vì giữ đường dẫn tới file gốc:
    /// người dùng có thể di chuyển/xoá file sau khi chọn, hoặc chạy phần mềm
    /// trên máy khác — bản sao vẫn còn nên hóa đơn không bị mất logo.
    /// </para>
    /// </summary>
    public async Task<string> LuuLogoAsync(string duongDanFileGoc)
    {
        if (string.IsNullOrWhiteSpace(duongDanFileGoc) || !File.Exists(duongDanFileGoc))
            throw new FileNotFoundException("File logo không tồn tại.", duongDanFileGoc);

        var thuMuc = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShopHoa",
            "logo");

        Directory.CreateDirectory(thuMuc);

        // Giữ đuôi file gốc (png/jpg/...) để phần mềm đoán đúng kiểu ảnh.
        var duoi = Path.GetExtension(duongDanFileGoc);

        if (string.IsNullOrWhiteSpace(duoi) || duoi.Length > 10)
            duoi = ".png";

        // Tên cố định: ứng dụng chỉ dùng một logo tiệm, không tích tụ file rác
        // mỗi lần người dùng chọn logo khác.
        var dichDen = Path.Combine(thuMuc, "logo_tiem" + duoi.ToLowerInvariant());

        // Đã là bản sao trong thư mục ứng dụng thì không cần chép lại: mở
        // chính file đó để ghi đè sẽ làm hỏng dữ liệu.
        if (string.Equals(
                Path.GetFullPath(duongDanFileGoc),
                Path.GetFullPath(dichDen),
                StringComparison.OrdinalIgnoreCase))
        {
            return dichDen;
        }

        await using (var input = File.OpenRead(duongDanFileGoc))
        await using (var output = File.Create(dichDen))
        {
            await input.CopyToAsync(output);
        }

        return dichDen;
    }
}