using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;

namespace ShopHoa.ViewModels;

public partial class MainWindowViewModel
{
    // ============================================================
    // PHÂN HỆ NHẬP HÀNG / XUẤT HÀNG HỢP NHẤT
    //
    // Trước đây có 6 mục menu rời rạc (Nhập hàng, Xuất hàng, Nhập trả lại,
    // Xuất trả NCC, Nhập khác, Xuất nội bộ). Nay gộp còn 2 trung tâm:
    //   • Nhập hàng : danh sách hợp nhất + chọn loại phiếu khi tạo đơn.
    //   • Xuất hàng : danh sách hợp nhất + chọn loại phiếu khi tạo đơn.
    // ============================================================

    /// <summary>Danh sách phiếu nhập hợp nhất (mọi loại phiếu nhập trong một bảng).</summary>
    public PhieuHopListViewModel PhieuNhapHop { get; }

    /// <summary>Danh sách phiếu xuất hợp nhất (mọi loại phiếu xuất trong một bảng).</summary>
    public PhieuHopListViewModel PhieuXuatHop { get; }

    // ---------------- Hub Nhập hàng ----------------

    /// <summary>True = đang xem tab "Danh sách phiếu nhập", False = tab "Tạo phiếu nhập".</summary>
    [ObservableProperty] private bool isPhieuNhapTabDanhSach = true;

    /// <summary>Tên loại phiếu nhập đang chọn (hiển thị trên ComboBox).</summary>
    [ObservableProperty] private string phieuNhapLoaiTen = "Nhập từ nhà cung cấp";

    /// <summary>Mã loại phiếu nhập đang chọn.</summary>
    [ObservableProperty] private string phieuNhapLoaiMa = PhieuHopLabels.NHAP_TU_NCC;

    public IReadOnlyList<string> PhieuNhapLoaiTenOptions { get; } =
        PhieuHopLabels.DanhSachLoaiNhap.Select(x => x.Ten).ToList();

    public bool IsPhieuNhapTabTaoPhieu => !IsPhieuNhapTabDanhSach;

    /// <summary>Loại phiếu nhập đang chọn là nhập từ nhà cung cấp (có công nợ NCC).</summary>
    public bool IsPhieuNhapLoaiNhaCungCap => PhieuNhapLoaiMa == PhieuHopLabels.NHAP_TU_NCC;

    /// <summary>Loại phiếu nhập đang chọn thuộc nhóm nội bộ (nhập khác).</summary>
    public bool IsPhieuNhapLoaiKhac => !IsPhieuNhapLoaiNhaCungCap;

    public string PhieuNhapLoaiMoTa => PhieuNhapLoaiMa switch
    {
        PhieuHopLabels.NHAP_TU_NCC =>
            "Nhập hàng từ nhà cung cấp: có liên kết công nợ, thanh toán và ghi sổ quỹ.",
        PhieuHopLabels.NHAP_NOI_BO =>
            "Điều chuyển hàng hóa, nguyên vật liệu từ chi nhánh hoặc bộ phận khác về kho.",
        PhieuHopLabels.NHAP_DIEU_CHINH =>
            "Cân bằng số liệu thực tế sau kiểm kê định kỳ hoặc chỉnh lệch kiểm kê.",
        PhieuHopLabels.NHAP_KHUYEN_MAI =>
            "Hàng tặng, mẫu dùng hoặc hộp biếu tới nhà cung cấp / đối tác (đơn giá 0đ).",
        PhieuHopLabels.NHAP_THU_HOI =>
            "Thu hồi giỏ hoa, bình gốm, hộp hoa và vật tư dùng sau khi kết thúc sự kiện.",
        _ => "Nhập hàng khác, không phát sinh công nợ với nhà cung cấp."
    };

    // ---------------- Hub Xuất hàng ----------------

    /// <summary>True = đang xem tab "Danh sách phiếu xuất", False = tab "Tạo phiếu xuất".</summary>
    [ObservableProperty] private bool isPhieuXuatTabDanhSach = true;

    /// <summary>Tên loại phiếu xuất đang chọn (hiển thị trên ComboBox).</summary>
    [ObservableProperty] private string phieuXuatLoaiTen = "Xuất hủy / hao hụt / hết hạn";

    /// <summary>Mã loại phiếu xuất đang chọn.</summary>
    [ObservableProperty] private string phieuXuatLoaiMa = PhieuHopLabels.XUAT_HAO_HUT;

    public IReadOnlyList<string> PhieuXuatLoaiTenOptions { get; } =
        PhieuHopLabels.DanhSachLoaiXuat.Select(x => x.Ten).ToList();

    public bool IsPhieuXuatTabTaoPhieu => !IsPhieuXuatTabDanhSach;

    /// <summary>Loại phiếu xuất đang chọn là trả nhà cung cấp (dùng form riêng).</summary>
    public bool IsPhieuXuatLoaiTraNcc => PhieuXuatLoaiMa == PhieuHopLabels.XUAT_TRA_NCC;

    /// <summary>Loại phiếu xuất đang chọn thuộc nhóm xuất hàng thông thường.</summary>
    public bool IsPhieuXuatLoaiKhac => !IsPhieuXuatLoaiTraNcc;

    public string PhieuXuatLoaiMoTa => PhieuXuatLoaiMa switch
    {
        PhieuHopLabels.XUAT_HAO_HUT =>
            "Xuất hàng hỏng, hết hạn, hao hụt hoặc hủy đơn — không thu tiền.",
        PhieuHopLabels.XUAT_NOI_BO =>
            "Xuất hàng dùng cho nội bộ, trưng bày tiệm hoặc sự kiện.",
        PhieuHopLabels.XUAT_CHUYEN_KHO =>
            "Chuyển hàng sang chi nhánh hoặc kho khác trong hệ thống.",
        PhieuHopLabels.XUAT_TRA_NCC =>
            "Trả hàng cho nhà cung cấp kèm hoàn tiền, có ghi giảm công nợ NCC.",
        _ => "Xuất hàng khác, không phát sinh công nợ với nhà cung cấp."
    };

    // ============================================================
    // Khởi tạo
    // ============================================================

    private void KhoiTaoPhieuHop()
    {
        PhieuNhapHop.MoTaoPhieu = MoTaoPhieuNhapAsync;
        PhieuNhapHop.HienThongBao = HienThongBaoHopAsync;
        PhieuNhapHop.LayCuaSoChinh = () => OwnerWindow;
        PhieuNhapHop.ThanhToanPhieu = ThanhToanPhieuHopAsync;
        PhieuNhapHop.InPhieu = InPhieuHopAsync;

        PhieuXuatHop.MoTaoPhieu = MoTaoPhieuXuatAsync;
        PhieuXuatHop.HienThongBao = HienThongBaoHopAsync;
        PhieuXuatHop.LayCuaSoChinh = () => OwnerWindow;
    }

    /// <summary>Thanh toán công nợ cho một phiếu nhập từ NCC nằm trong danh sách hợp nhất.</summary>
    private async Task ThanhToanPhieuHopAsync(PhieuHopRow row)
    {
        var phieu = await _db.GetPhieuNhapHangTheoIdAsync(row.Id);

        if (phieu is null)
        {
            await HienThongBaoHopAsync("Thông báo", "Không tìm thấy phiếu nhập trong database.");
            return;
        }

        await ThanhToanPhieuNhapAsync(phieu);
        await PhieuNhapHop.LoadAsync();
    }

    /// <summary>In lại phiếu nhập từ NCC nằm trong danh sách hợp nhất.</summary>
    private async Task InPhieuHopAsync(PhieuHopRow row)
    {
        var phieu = await _db.GetPhieuNhapHangTheoIdAsync(row.Id);

        if (phieu is null)
        {
            await HienThongBaoHopAsync("Thông báo", "Không tìm thấy phiếu nhập trong database.");
            return;
        }

        await InPhieuNhapAsync(phieu);
    }

    private Task HienThongBaoHopAsync(string title, string message) =>
        ShowMessageAsync(title, message);

    // ============================================================
    // Điều hướng giữa 2 tab của mỗi hub
    // ============================================================

    [RelayCommand]
    public async Task MoTaoPhieuNhapAsync()
    {
        if (!await KiemTraQuyenAsync(PermissionKeys.PurchasesCreate, "Bạn không có quyền tạo phiếu nhập hàng."))
            return;

        IsPhieuNhapTabDanhSach = false;
        await ApDungLoaiPhieuNhapAsync();
    }

    [RelayCommand]
    public async Task MoDanhSachPhieuNhapHopAsync()
    {
        IsPhieuNhapTabDanhSach = true;
        await PhieuNhapHop.LoadAsync();
    }

    [RelayCommand]
    public async Task MoTaoPhieuXuatAsync()
    {
        IsPhieuXuatTabDanhSach = false;
        await ApDungLoaiPhieuXuatAsync();
    }

    [RelayCommand]
    public async Task MoDanhSachPhieuXuatHopAsync()
    {
        IsPhieuXuatTabDanhSach = true;
        await PhieuXuatHop.LoadAsync();
    }

    // ============================================================
    // Loại phiếu nhập
    // ============================================================

    partial void OnPhieuNhapLoaiTenChanged(string value)
    {
        var ma = ResolveLoaiNhap(value);
        if (ma == PhieuNhapLoaiMa)
            return;

        PhieuNhapLoaiMa = ma;
        OnPropertyChanged(nameof(IsPhieuNhapLoaiNhaCungCap));
        OnPropertyChanged(nameof(IsPhieuNhapLoaiKhac));
        OnPropertyChanged(nameof(PhieuNhapLoaiMoTa));

        if (!IsPhieuNhapTabDanhSach)
            _ = ApDungLoaiPhieuNhapAsync();
    }

    partial void OnPhieuNhapLoaiMaChanged(string value)
    {
        OnPropertyChanged(nameof(IsPhieuNhapLoaiNhaCungCap));
        OnPropertyChanged(nameof(IsPhieuNhapLoaiKhac));
        OnPropertyChanged(nameof(PhieuNhapLoaiMoTa));
    }

    private static string ResolveLoaiNhap(string? tenHienThi)
    {
        if (string.IsNullOrWhiteSpace(tenHienThi))
            return PhieuHopLabels.NHAP_TU_NCC;

        foreach (var (ma, ten) in PhieuHopLabels.DanhSachLoaiNhap)
        {
            if (ten == tenHienThi)
                return ma;
        }

        return PhieuHopLabels.NHAP_TU_NCC;
    }

    /// <summary>
    /// Đồng bộ form nhập khác với loại phiếu đang chọn ở hub rồi nạp lại form.
    /// </summary>
    private async Task ApDungLoaiPhieuNhapAsync()
    {
        if (IsPhieuNhapLoaiNhaCungCap)
        {
            IsThemPhieuNhapKhacVisible = false;
            await MoPhieuNhapHangAsync();
            return;
        }

        IsNhapHangFormVisible = false;
        PhieuNhapKhacLoai = PhieuNhapLoaiMa;
        OnPropertyChanged(nameof(SelectedPhieuNhapKhacLoaiFormHienThi));
        OnPropertyChanged(nameof(PhieuNhapKhacLoaiGiaiThich));

        IsThemPhieuNhapKhacVisible = true;
        await LamMoiFormThemPhieuNhapKhacAsync();
    }

    // ============================================================
    // Loại phiếu xuất
    // ============================================================

    partial void OnPhieuXuatLoaiTenChanged(string value)
    {
        var ma = ResolveLoaiXuat(value);
        if (ma == PhieuXuatLoaiMa)
            return;

        PhieuXuatLoaiMa = ma;
        OnPropertyChanged(nameof(IsPhieuXuatLoaiTraNcc));
        OnPropertyChanged(nameof(IsPhieuXuatLoaiKhac));
        OnPropertyChanged(nameof(PhieuXuatLoaiMoTa));

        if (!IsPhieuXuatTabDanhSach)
            _ = ApDungLoaiPhieuXuatAsync();
    }

    partial void OnPhieuXuatLoaiMaChanged(string value)
    {
        OnPropertyChanged(nameof(IsPhieuXuatLoaiTraNcc));
        OnPropertyChanged(nameof(IsPhieuXuatLoaiKhac));
        OnPropertyChanged(nameof(PhieuXuatLoaiMoTa));
    }

    private static string ResolveLoaiXuat(string? tenHienThi)
    {
        if (string.IsNullOrWhiteSpace(tenHienThi))
            return PhieuHopLabels.XUAT_HAO_HUT;

        foreach (var (ma, ten) in PhieuHopLabels.DanhSachLoaiXuat)
        {
            if (ten == tenHienThi)
                return ma;
        }

        return PhieuHopLabels.XUAT_HAO_HUT;
    }

    /// <summary>
    /// Đồng bộ form xuất hàng với loại phiếu đang chọn ở hub rồi nạp lại form.
    /// </summary>
    private async Task ApDungLoaiPhieuXuatAsync()
    {
        if (IsPhieuXuatLoaiTraNcc)
        {
            await MoFormThemPhieuXuatTraAsync();
            return;
        }

        IsThemPhieuXuatTraVisible = false;
        await KhoiTaoPhieuXuatMoiAsync();
        PhieuXuatLoaiXuat = ResolveTenLoaiXuatForm(PhieuXuatLoaiMa);
    }

    /// <summary>
    /// Ánh xạ mã loại phiếu của hub sang nhãn mà form xuất hàng đang dùng.
    /// </summary>
    private static readonly (string Ma, string Ten)[] PhieuXuatLoaiFormOptions =
    [
        (PhieuHopLabels.XUAT_HAO_HUT, "Xuất hủy / Hao hụt / Hết hạn"),
        (PhieuHopLabels.XUAT_NOI_BO, "Xuất sử dụng nội bộ / Trưng bày"),
        (PhieuHopLabels.XUAT_CHUYEN_KHO, "Xuất chuyển kho / chi nhánh"),
        (PhieuHopLabels.XUAT_KHAC, "Xuất khác")
    ];

    private static string ResolveTenLoaiXuatForm(string ma)
    {
        foreach (var (maHienThi, ten) in PhieuXuatLoaiFormOptions)
        {
            if (maHienThi == ma)
                return ten;
        }

        return PhieuXuatLoaiFormOptions[0].Ten;
    }
}
