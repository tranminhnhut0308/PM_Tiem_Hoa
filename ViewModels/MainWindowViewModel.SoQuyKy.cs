using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

/// <summary>
/// Phần ViewModel của "kỳ" trên màn Sổ quỹ.
///
/// Sổ quỹ được hiểu là bảng theo dõi dòng tiền của một kỳ: bắt đầu bằng số
/// dư quỹ đầu kỳ, cộng dồn phiếu thu, trừ dần phiếu chi và cho ra tồn quỹ.
/// Vì vậy màn hình phải trả lời rõ ba câu hỏi cho kỳ đang xem:
/// <list type="bullet">
///   <item>Kỳ này là kỳ nào (từ ngày nào đến ngày nào)?</item>
///   <item>Số dư quỹ đầu kỳ của kỳ đã được khai báo chưa?</item>
///   <item>Tồn quỹ sau kỳ là bao nhiêu?</item>
/// </list>
///
/// Khởi tạo quỹ đầu kỳ là thao tác khai báo số tiền có sẵn trong quỹ tại
/// ngày bắt đầu kỳ (có thể khai 0 nếu quỹ mở vốn). Số này được cộng vào
/// "quỹ đầu kỳ" khi tính tồn quỹ, nên khai sai sẽ làm lệch cả báo cáo.
/// </summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Mã quỹ mặc định khi chưa có dữ liệu nào trong database.</summary>
    private const string SoQuyQuyMacDinh = "TIEN_MAT";

    /// <summary>
    /// Danh sách quỹ dùng cho ô chọn khi khởi tạo quỹ đầu kỳ.
    /// Lấy từ những quỹ đang thực tế có giao dịch; nếu chưa có giao dịch nào
    /// thì đưa sẵn các quỹ thông dụng để người dùng vẫn khởi tạo được.
    /// </summary>
    public ObservableCollection<SoQuyLuaChon> SoQuyKhoiTaoQuyOptions { get; } = new();

    /// <summary>true khi người dùng được phép khởi tạo quỹ đầu kỳ.</summary>
    public bool CanKhoiTaoQuyDauKy =>
        Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookInit)
        || Quyen.HasPermission(Session.CurrentUser, PermissionKeys.CashBookOpeningBalance)
        || Quyen.IsAdmin(Session.CurrentUser);

    /// <summary>Đang mở bảng "khởi tạo quỹ đầu kỳ" ngay trên màn Sổ quỹ.</summary>
    [ObservableProperty] private bool isSoQuyKhoiTaoQuyDauKyVisible;

    /// <summary>Ngày đầu kỳ sẽ khai báo (mặc định là ngày bắt đầu của kỳ đang xem).</summary>
    [ObservableProperty] private DateTime soQuyKhoiTaoNgayDauKy = DateTime.Today;

    /// <summary>Quỹ sẽ khai báo số dư đầu kỳ.</summary>
    [ObservableProperty] private SoQuyLuaChon? soQuyKhoiTaoQuy;

    /// <summary>Số tiền đầu kỳ (dạng chuỗi để người dùng gõ được).</summary>
    [ObservableProperty] private string soQuyKhoiTaoSoTienText = "0";

    /// <summary>Ghi chú cho lần khai báo này.</summary>
    [ObservableProperty] private string soQuyKhoiTaoNoiDung = "";

    /// <summary>Tên kỳ đang xem, ví dụ "Tháng 10/2026".</summary>
    public string SoQuyKyTenHienThi { get; private set; } = "Toàn thời gian";

    /// <summary>Khoảng thời gian của kỳ đang xem, ví dụ "01/10/2026 – 31/10/2026".</summary>
    public string SoQuyKyKhoangThoiGianHienThi { get; private set; } = "";

    /// <summary>Ngày bắt đầu của kỳ đang xem (null = xem toàn thời gian).</summary>
    public DateTime? SoQuyKyTuNgay { get; private set; }

    /// <summary>Ngày kết thúc của kỳ đang xem (null = xem toàn thời gian).</summary>
    public DateTime? SoQuyKyDenNgay { get; private set; }

    /// <summary>
    /// Số dư đầu kỳ đã khai báo cho kỳ đang xem (đã định dạng).
    /// Rỗng khi kỳ đó chưa có khai báo nào.
    /// </summary>
    public string SoQuyKyQuyDauKyDaKhaiBaoText { get; private set; } = "";

    /// <summary>Số tiền đầu kỳ đã khai báo cho kỳ đang xem.</summary>
    public decimal SoQuyKyQuyDauKyDaKhaiBao { get; private set; }

    /// <summary>Số bản ghi đầu kỳ đã khai báo cho kỳ đang xem.</summary>
    public int SoQuyKySoKhaiBao { get; private set; }

    /// <summary>true khi kỳ đang xem chưa có khai báo quỹ đầu kỳ nào.</summary>
    public bool SoQuyKyChuaKhoiTao => SoQuyKySoKhaiBao == 0;

    /// <summary>
    /// Câu mở đầu của bảng khởi tạo, nhắc đúng kỳ đang xem.
    /// </summary>
    public string SoQuyKhoiTaoTieuDe => SoQuyKyTuNgay.HasValue
        ? $"Khởi tạo quỹ đầu kỳ · {SoQuyKyTenHienThi}"
        : "Khởi tạo quỹ đầu kỳ";

    /// <summary>
    /// Nạp lại thông tin kỳ (nhãn kỳ + số dư đầu kỳ đã khai báo) sau mỗi lần
    /// bộ lọc thay đổi. Không ném lỗi ra ngoài vì đây chỉ là thông tin bổ trợ.
    /// </summary>
    private async Task CapNhatThongTinKyAsync()
    {
        var loc = SoQuyLocHienTai;

        // DenNgay trong bộ lọc là mốc độc quyền (không bao gồm), nên trừ 1 ngày
        // để lấy đúng ngày cuối của kỳ.
        SoQuyKyTuNgay = loc.TuNgay?.Date;
        SoQuyKyDenNgay = loc.DenNgay?.Date.AddDays(-1);

        CapNhatNhanKy();

        OnPropertyChanged(nameof(SoQuyKyTenHienThi));
        OnPropertyChanged(nameof(SoQuyKyKhoangThoiGianHienThi));
        OnPropertyChanged(nameof(SoQuyKhoiTaoTieuDe));

        if (!SoQuyKyTuNgay.HasValue)
        {
            // Xem toàn thời gian: kỳ bắt đầu từ khai báo đầu tiên nên coi như
            // đã có khai báo nếu bảng đầu kỳ có bất kỳ bản ghi nào.
            var tatCa = await _db.GetSoQuyDauKyListAsync(loc.PhuongThuc);

            SoQuyKySoKhaiBao = tatCa.Count;
            SoQuyKyQuyDauKyDaKhaiBao = tatCa.Sum(k => k.SoTien);
            SoQuyKyQuyDauKyDaKhaiBaoText = SoQuyKySoKhaiBao > 0
                ? MoneyInput.Format(SoQuyKyQuyDauKyDaKhaiBao) + " đ"
                : "";

            OnPropertyChanged(nameof(SoQuyKyChuaKhoiTao));
            OnPropertyChanged(nameof(SoQuyKyQuyDauKyDaKhaiBao));
            OnPropertyChanged(nameof(SoQuyKyQuyDauKyDaKhaiBaoText));

            return;
        }

        try
        {
            var khaiBao = await _db.GetSoQuyDauKyTheoKyAsync(
                SoQuyKyTuNgay.Value,
                loc.PhuongThuc);

            SoQuyKySoKhaiBao = khaiBao.Count;
            SoQuyKyQuyDauKyDaKhaiBao = khaiBao.Sum(k => k.SoTien);

            // Đã khai báo dù số tiền bằng 0 vẫn là "đã khởi tạo", nên vẫn hiện
            // số tiền để người dùng thấy trạng thái của kỳ.
            SoQuyKyQuyDauKyDaKhaiBaoText = SoQuyKySoKhaiBao > 0
                ? MoneyInput.Format(SoQuyKyQuyDauKyDaKhaiBao) + " đ"
                : "";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải thông tin kỳ sổ quỹ", ex);

            SoQuyKyQuyDauKyDaKhaiBao = 0m;
            SoQuyKyQuyDauKyDaKhaiBaoText = "";
            SoQuyKySoKhaiBao = 0;
        }

        OnPropertyChanged(nameof(SoQuyKyChuaKhoiTao));
        OnPropertyChanged(nameof(SoQuyKyQuyDauKyDaKhaiBao));
        OnPropertyChanged(nameof(SoQuyKyQuyDauKyDaKhaiBaoText));
    }

    /// <summary>Dựng nhãn "kỳ đang xem" từ khoảng thời gian đang lọc.</summary>
    private void CapNhatNhanKy()
    {
        if (!SoQuyKyTuNgay.HasValue || !SoQuyKyDenNgay.HasValue)
        {
            SoQuyKyTenHienThi = "Toàn thời gian";
            SoQuyKyKhoangThoiGianHienThi = "Từ lúc bắt đầu sử dụng phần mềm đến hiện tại";

            return;
        }

        var tu = SoQuyKyTuNgay.Value;
        var den = SoQuyKyDenNgay.Value;

        if (tu.Year == den.Year && tu.Month == den.Month)
        {
            SoQuyKyTenHienThi = $"Tháng {tu.Month:00}/{tu.Year}";
        }
        else if (tu.Year == den.Year)
        {
            SoQuyKyTenHienThi = $"{tu:dd/MM} – {den:dd/MM/yyyy}";
        }
        else
        {
            SoQuyKyTenHienThi = $"{tu:dd/MM/yyyy} – {den:dd/MM/yyyy}";
        }

        SoQuyKyKhoangThoiGianHienThi = $"{tu:dd/MM/yyyy} – {den:dd/MM/yyyy}";
    }

    /// <summary>
    /// Nạp danh sách quỹ cho ô chọn khi khởi tạo. Bổ sung các quỹ thông dụng
    /// nếu database chưa có quỹ nào (cửa hàng mới dùng phần mềm).
    /// </summary>
    private async Task TaiDanhSachQuyKhoiTaoAsync()
    {
        try
        {
            var quy = await _db.GetSoQuyQuyOptionsAsync();

            SoQuyKhoiTaoQuyOptions.Clear();

            foreach (var item in quy)
            {
                if (SoQuyKhoiTaoQuyOptions.Any(o => o.GiaTri == item.GiaTri))
                    continue;

                SoQuyKhoiTaoQuyOptions.Add(item);
            }

            if (!SoQuyKhoiTaoQuyOptions.Any(o => o.GiaTri == SoQuyQuyMacDinh))
            {
                SoQuyKhoiTaoQuyOptions.Insert(
                    0,
                    new SoQuyLuaChon(SoQuyQuyMacDinh, "💵 Tiền mặt"));
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải danh sách quỹ khởi tạo sổ quỹ", ex);
        }

        OnPropertyChanged(nameof(SoQuyKhoiTaoQuy));
    }

    /// <summary>
    /// Mở bảng khởi tạo quỹ đầu kỳ cho kỳ đang xem. Số tiền được điền sẵn
    /// theo khai báo đã có (nếu có) để người dùng chỉ cần sửa, không cần gõ lại.
    /// </summary>
    [RelayCommand]
    private async Task MoKhoiTaoQuyDauKyAsync()
    {
        if (!await KiemTraQuyenAsync(
                PermissionKeys.CashBookInit,
                "Bạn không có quyền khởi tạo quỹ đầu kỳ."))
        {
            return;
        }

        await TaiDanhSachQuyKhoiTaoAsync();

        SoQuyKhoiTaoNgayDauKy = SoQuyKyTuNgay ?? DateTime.Today;
        SoQuyKhoiTaoNoiDung = "";

        // Mặc định chọn đúng quỹ đang lọc trên bảng Sổ quỹ.
        var maQuy = (SoQuyLocQuy?.GiaTri ?? "").Trim().ToUpperInvariant();

        SoQuyKhoiTaoQuy = SoQuyKhoiTaoQuyOptions.FirstOrDefault(o =>
                           string.Equals(o.GiaTri, maQuy, StringComparison.OrdinalIgnoreCase))
                         ?? SoQuyKhoiTaoQuyOptions.FirstOrDefault();

        SoQuyKhoiTaoSoTienText = "0";

        // Điền sẵn số tiền đã khai báo cho đúng mốc ngày + quỹ này.
        try
        {
            var daKhaiBao = await _db.GetSoQuyDauKyTheoKyAsync(
                SoQuyKhoiTaoNgayDauKy,
                SoQuyKhoiTaoQuy?.GiaTri);

            if (daKhaiBao.Count > 0)
            {
                SoQuyKhoiTaoSoTienText = MoneyInput.ToRawText(daKhaiBao[0].SoTien);
                SoQuyKhoiTaoNoiDung = daKhaiBao[0].NoiDung ?? "";
            }
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi đọc số dư đầu kỳ khi khởi tạo", ex);
        }

        IsSoQuyKhoiTaoQuyDauKyVisible = true;

        OnPropertyChanged(nameof(SoQuyKhoiTaoTieuDe));
    }

    /// <summary>Đóng bảng khởi tạo quỹ đầu kỳ.</summary>
    [RelayCommand]
    private void DongKhoiTaoQuyDauKy() => IsSoQuyKhoiTaoQuyDauKyVisible = false;

    /// <summary>
    /// Lưu số dư quỹ đầu kỳ cho kỳ đang xem. Khai được 0 cũng hợp lệ: đó là
    /// trạng thái "quỹ mở vốn từ đầu kỳ" để người dùng theo dõi thu chi
    /// của kỳ hiện tại.
    /// </summary>
    [RelayCommand]
    private async Task LuuKhoiTaoQuyDauKyAsync()
    {
        if (SoQuyKhoiTaoQuy is null)
        {
            StatusMessage = "Vui lòng chọn quỹ cần khởi tạo số dư đầu kỳ.";

            return;
        }

        if (!MoneyInput.TryParse(SoQuyKhoiTaoSoTienText, batBuoc: false, out var soTien, out var loi))
        {
            StatusMessage = loi;

            return;
        }

        try
        {
            var item = new SoQuyDauKy
            {
                NgayDauKy = SoQuyKhoiTaoNgayDauKy.Date,
                PhuongThuc = SoQuyKhoiTaoQuy.GiaTri,
                SoTien = soTien,
                NoiDung = (SoQuyKhoiTaoNoiDung ?? "").Trim(),
                ChiNhanhId = Session.CurrentUser?.ChiNhanhId ?? 1
            };

            await _db.LuuSoQuyDauKyAsync(item);

            IsSoQuyKhoiTaoQuyDauKyVisible = false;

            await TaiLaiSoQuyAsync();

            StatusMessage = soTien > 0m
                ? $"Đã khởi tạo quỹ đầu kỳ {SoQuyKhoiTaoQuy.HienThi} với số dư {MoneyInput.Format(soTien)} đ."
                : $"Đã khởi tạo quỹ đầu kỳ {SoQuyKhoiTaoQuy.HienThi} với số dư 0 đ.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khởi tạo quỹ đầu kỳ", ex);

            StatusMessage = "Không khởi tạo được quỹ đầu kỳ: " + ex.Message;
        }
    }

    /// <summary>
    /// Xoá khai báo quỹ đầu kỳ của kỳ đang xem (dùng khi khai nhầm ngày hoặc
    /// muốn quay lại trạng thái chưa khởi tạo).
    /// </summary>
    [RelayCommand]
    private async Task XoaKhoiTaoQuyDauKyAsync()
    {
        if (SoQuyKhoiTaoQuy is null)
        {
            StatusMessage = "Vui lòng chọn quỹ cần xoá số dư đầu kỳ.";

            return;
        }

        var xacNhan = await ShowConfirmationAsync(
            "Xoá số dư đầu kỳ",
            $"Xoá số dư đầu kỳ đã khai báo cho quỹ {SoQuyKhoiTaoQuy.HienThi} "
            + $"ngày {SoQuyKhoiTaoNgayDauKy:dd/MM/yyyy}?\n\n"
            + "Số dư này sẽ không còn được cộng vào quỹ đầu kỳ của kỳ.",
            "Xoá số dư",
            "Giữ nguyên");

        if (!xacNhan)
            return;

        try
        {
            var daXoa = await _db.XoaSoQuyDauKyTheoKyAsync(
                SoQuyKhoiTaoNgayDauKy,
                SoQuyKhoiTaoQuy.GiaTri);

            if (!daXoa)
            {
                StatusMessage = "Không tìm thấy số dư đầu kỳ cần xoá.";

                return;
            }

            IsSoQuyKhoiTaoQuyDauKyVisible = false;

            await TaiLaiSoQuyAsync();

            StatusMessage = "Đã xoá số dư đầu kỳ của kỳ.";
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi xoá số dư đầu kỳ", ex);

            StatusMessage = "Không xoá được số dư đầu kỳ: " + ex.Message;
        }
    }

    partial void OnSoQuyKhoiTaoNgayDauKyChanged(DateTime value)
    {
        OnPropertyChanged(nameof(SoQuyKhoiTaoTieuDe));
    }
}
