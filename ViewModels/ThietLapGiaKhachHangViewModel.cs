using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

public sealed partial class ThietLapGiaKhachHangViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _searchCts;

    public KhachHang Customer { get; }
    public Func<Window?>? LayCuaSoChinh { get; set; }

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string searchKeyword = "";

    [ObservableProperty]
    private ProductOption? selectedNhomHang;

    [ObservableProperty]
    private GiaBanKhachHangItem? selectedGiaItem;

    [ObservableProperty]
    private string statusMessage = "";

    public ObservableCollection<GiaBanKhachHangItem> DanhSachGia { get; } = new();
    public ObservableCollection<ProductOption> NhomHangOptions { get; } = new();

    // Thông tin khách hàng hiển thị
    public string MaKhachHang => Customer.MaKhachHang;
    public string TenKhachHang => Customer.TenKhachHang;
    public string SoDienThoai => string.IsNullOrWhiteSpace(Customer.SoDienThoai) ? "Chưa có SĐT" : Customer.SoDienThoai;
    public string NhomKhachHang => string.IsNullOrWhiteSpace(Customer.NhomKhachHang) ? "Chưa phân nhóm" : Customer.NhomKhachHang;
    public string DiaChi => string.IsNullOrWhiteSpace(Customer.DiaChi) ? "Chưa có địa chỉ" : Customer.DiaChi;

    public int TongSoMatHang => DanhSachGia.Count;
    public int SoHangGiamGia => DanhSachGia.Count(x => x.ChenhLech < 0);
    public int SoHangTangGia => DanhSachGia.Count(x => x.ChenhLech > 0);
    public bool IsEmpty => DanhSachGia.Count == 0 && !IsLoading;

    public string TongSoMatHangText => $"{TongSoMatHang} mặt hàng thiết lập giá";
    public string SoHangGiamGiaText => $"{SoHangGiamGia} hàng ưu đãi giảm";
    public string SoHangTangGiaText => $"{SoHangTangGia} hàng tăng giá";

    public ThietLapGiaKhachHangViewModel(KhachHang customer)
    {
        Customer = customer;
        _ = KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        await LoadNhomHangOptionsAsync();
        await TaiDuLieuAsync();
    }

    private async Task LoadNhomHangOptionsAsync()
    {
        try
        {
            var list = await _db.GetNhomSanPhamAsync();
            NhomHangOptions.Clear();
            NhomHangOptions.Add(new ProductOption { Id = 0, Name = "Tất cả nhóm hàng" });
            NhomHangOptions.Add(new ProductOption { Id = 999999, Name = "-- Chưa phân nhóm --" });

            if (list != null)
            {
                foreach (var item in list)
                    NhomHangOptions.Add(item);
            }

            SelectedNhomHang = NhomHangOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi tải nhóm hàng thiết lập giá", ex);
        }
    }

    partial void OnSearchKeywordChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await TaiDuLieuAsync();
                });
            }
            catch (OperationCanceledException) { }
        });
    }

    partial void OnSelectedNhomHangChanged(ProductOption? value)
    {
        _ = TaiDuLieuAsync();
    }

    [RelayCommand]
    public async Task TaiDuLieuAsync()
    {
        if (Customer.Id == 0)
            return;

        IsLoading = true;
        StatusMessage = "Đang tải bảng giá khách hàng...";

        try
        {
            uint? nhomId = SelectedNhomHang != null && SelectedNhomHang.Id > 0 ? SelectedNhomHang.Id : null;
            var list = await _db.GetBangGiaKhachHangAsync(Customer.Id, SearchKeyword, nhomId);

            DanhSachGia.Clear();
            foreach (var item in list)
            {
                DanhSachGia.Add(item);
            }

            CapNhatThongKe();
            StatusMessage = DanhSachGia.Count == 0
                ? "Khách hàng chưa có mặt hàng nào được thiết lập giá riêng."
                : $"Đã tải {DanhSachGia.Count} mặt hàng có giá riêng.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi tải bảng giá: " + ex.Message;
            SafeLog.WriteError("Lỗi TaiDuLieuAsync ThietLapGia", ex);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private void CapNhatThongKe()
    {
        OnPropertyChanged(nameof(TongSoMatHang));
        OnPropertyChanged(nameof(SoHangGiamGia));
        OnPropertyChanged(nameof(SoHangTangGia));
        OnPropertyChanged(nameof(TongSoMatHangText));
        OnPropertyChanged(nameof(SoHangGiamGiaText));
        OnPropertyChanged(nameof(SoHangTangGiaText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task ThemGiaHangHoaAsync()
    {
        var owner = LayCuaSoChinh?.Invoke();
        if (owner is null) return;

        try
        {
            var dialog = new ThemSuaGiaKhachHangDialog(Customer);
            var result = await dialog.ShowDialog<bool>(owner);

            if (result)
            {
                await TaiDuLieuAsync();
                StatusMessage = "Đã lưu thiết lập giá riêng cho hàng hóa thành công!";
                await NotificationWindow.ShowSuccessAsync("Đã lưu thiết lập giá thành công!", owner: owner);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi thêm giá riêng: " + ex.Message;
            SafeLog.WriteError("Lỗi ThemGiaHangHoaAsync", ex);
        }
    }

    [RelayCommand]
    private async Task SuaGiaHangHoaAsync(GiaBanKhachHangItem? item)
    {
        if (item is null) return;
        var owner = LayCuaSoChinh?.Invoke();
        if (owner is null) return;

        try
        {
            var dialog = new ThemSuaGiaKhachHangDialog(Customer, item);
            var result = await dialog.ShowDialog<bool>(owner);

            if (result)
            {
                await TaiDuLieuAsync();
                StatusMessage = $"Đã cập nhật giá bán riêng cho {item.TenSanPham}.";
                await NotificationWindow.ShowSuccessAsync($"Đã cập nhật giá cho {item.TenSanPham}.", owner: owner);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi sửa giá: " + ex.Message;
            SafeLog.WriteError("Lỗi SuaGiaHangHoaAsync", ex);
        }
    }

    [RelayCommand]
    private async Task XoaGiaHangHoaAsync(GiaBanKhachHangItem? item)
    {
        if (item is null) return;
        var owner = LayCuaSoChinh?.Invoke();
        if (owner is null) return;

        var confirm = new ConfirmationWindow(
            "Xóa thiết lập giá",
            $"Bạn có chắc muốn xóa thiết lập giá riêng của mặt hàng \"{item.TenSanPham}\" cho khách hàng {Customer.TenKhachHang}?\nSau khi xóa, mặt hàng này sẽ quay về áp dụng giá bán chung ({item.GiaGocText}).",
            "Xóa thiết lập",
            "Hủy");

        var confirmed = await confirm.ShowDialog<bool>(owner);
        if (!confirmed) return;

        try
        {
            var success = await _db.XoaGiaBanKhachHangAsync(item.Id);
            if (success)
            {
                DanhSachGia.Remove(item);
                // Đánh lại số thứ tự
                for (int i = 0; i < DanhSachGia.Count; i++)
                    DanhSachGia[i].Stt = i + 1;

                CapNhatThongKe();
                StatusMessage = $"Đã xóa giá riêng cho {item.TenSanPham}.";
                await NotificationWindow.ShowSuccessAsync($"Đã đưa {item.TenSanPham} về giá bán chung.", owner: owner);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Không thể xóa giá riêng: " + ex.Message;
            SafeLog.WriteError("Lỗi XoaGiaHangHoaAsync", ex);
        }
    }

    [RelayCommand]
    private async Task LamMoiAsync()
    {
        SearchKeyword = "";
        SelectedNhomHang = NhomHangOptions.FirstOrDefault();
        await TaiDuLieuAsync();
    }
}
