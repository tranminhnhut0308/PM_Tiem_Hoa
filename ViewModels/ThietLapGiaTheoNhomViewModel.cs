using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;
using ShopHoa.Views;

namespace ShopHoa.ViewModels;

/// <summary>
/// Mặt hàng trong danh sách thiết lập giá bán riêng hàng loạt.
/// </summary>
public sealed partial class SanPhamGiaRiengItem : ObservableObject
{
    public uint SanPhamId { get; set; }
    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string DonViTinh { get; set; } = "Cái";
    public decimal GiaBan { get; set; }
    public decimal GiaVon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GiaRiengText))]
    [NotifyPropertyChangedFor(nameof(ChenhLech))]
    [NotifyPropertyChangedFor(nameof(PhanTramChenhLech))]
    [NotifyPropertyChangedFor(nameof(ChenhLechText))]
    [NotifyPropertyChangedFor(nameof(ChenhLechColor))]
    [NotifyPropertyChangedFor(nameof(IsGiamGia))]
    [NotifyPropertyChangedFor(nameof(IsTangGia))]
    [NotifyPropertyChangedFor(nameof(IsThapHonGiaVon))]
    [NotifyPropertyChangedFor(nameof(ChenhLechGiaVonText))]
    private decimal giaRieng;

    [ObservableProperty]
    private string? ghiChu;

    public int Stt { get; set; }

    public string GiaBanText => MoneyInput.Format(GiaBan) + " đ";
    public string GiaVonText => MoneyInput.Format(GiaVon) + " đ";
    public string GiaRiengText => MoneyInput.Format(GiaRieng) + " đ";

    public decimal ChenhLech => GiaRieng - GiaBan;
    public double PhanTramChenhLech => GiaBan > 0 ? (double)((GiaRieng - GiaBan) / GiaBan * 100) : 0;

    public bool IsGiamGia => ChenhLech < 0;
    public bool IsTangGia => ChenhLech > 0;

    /// <summary>Cảnh báo giá riêng thấp hơn giá vốn gốc (nguy cơ bán dưới giá vốn).</summary>
    public bool IsThapHonGiaVon => GiaRieng > 0 && GiaRieng < GiaVon;
    public decimal ChenhLechGiaVon => GiaRieng - GiaVon;
    public string ChenhLechGiaVonText => IsThapHonGiaVon ? $"⚠️ Lỗ {MoneyInput.Format(Math.Abs(ChenhLechGiaVon))} đ/SP" : "";

    public string ChenhLechText
    {
        get
        {
            if (ChenhLech < 0)
                return $"-{MoneyInput.Format(Math.Abs(ChenhLech))} đ ({PhanTramChenhLech:0.#}%)";
            if (ChenhLech > 0)
                return $"+{MoneyInput.Format(ChenhLech)} đ (+{PhanTramChenhLech:0.#}%)";
            return "Bằng giá niêm yết (0%)";
        }
    }

    public IBrush ChenhLechColor
    {
        get
        {
            if (ChenhLech < 0)
                return new SolidColorBrush(Color.Parse("#087F6E")); // Ưu đãi giảm
            if (ChenhLech > 0)
                return new SolidColorBrush(Color.Parse("#D97706")); // Tăng giá
            return new SolidColorBrush(Color.Parse("#61736E")); // Bằng nhau
        }
    }
}

public sealed partial class ThietLapGiaTheoNhomViewModel : ObservableObject
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _productSearchCts;

    public Func<Window?>? LayCuaSoChinh { get; set; }
    public Action? YeuCauDong { get; set; }

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "";

    // 1. Khách hàng
    [ObservableProperty] private string customerSearch = "";
    [ObservableProperty] private NhomKhachHang? selectedNhomKhachHang;
    [ObservableProperty] private bool hasCustomerSuggestions;

    public ObservableCollection<KhachHang> DanhSachKhachHang { get; } = new();
    public ObservableCollection<NhomKhachHang> NhomKhachHangOptions { get; } = new();
    public ObservableCollection<KhachHang> CustomerSuggestions { get; } = new();

    // 2. Sản phẩm & Bảng giá
    [ObservableProperty] private string productSearch = "";
    [ObservableProperty] private ProductOption? selectedNhomSanPham;
    [ObservableProperty] private bool hasProductSuggestions;

    public ObservableCollection<SanPhamGiaRiengItem> DanhSachSanPham { get; } = new();
    public ObservableCollection<ProductOption> NhomSanPhamOptions { get; } = new();
    public ObservableCollection<SanPham> ProductSuggestions { get; } = new();

    // 3. Công cụ tính giá nhanh & Áp dụng
    [ObservableProperty] private string batchCalcValue = "10";
    [ObservableProperty] private DateTime? ngayApDung = DateTime.Today;
    [ObservableProperty] private string ghiChuChung = "";

    // Thống kê tóm tắt
    public int TongSoKhachHang => DanhSachKhachHang.Count;
    public int TongSoSanPham => DanhSachSanPham.Count;
    public int SoHangGiamGia => DanhSachSanPham.Count(x => x.IsGiamGia);
    public int SoHangThapHonGiaVon => DanhSachSanPham.Count(x => x.IsThapHonGiaVon);

    public string TongSoKhachHangText => $"{TongSoKhachHang} khách hàng";
    public string TongSoSanPhamText => $"{TongSoSanPham} sản phẩm";
    public string CanhBaoGiaVonText => SoHangThapHonGiaVon > 0
        ? $"⚠️ Có {SoHangThapHonGiaVon} sản phẩm có giá riêng thấp hơn giá vốn!"
        : "";
    public bool CoCanhBaoGiaVon => SoHangThapHonGiaVon > 0;

    public ThietLapGiaTheoNhomViewModel(IEnumerable<KhachHang>? initialCustomers = null)
    {
        if (initialCustomers != null)
        {
            foreach (var kh in initialCustomers)
            {
                if (kh.Id > 0 && !DanhSachKhachHang.Any(k => k.Id == kh.Id))
                    DanhSachKhachHang.Add(kh);
            }
        }

        _ = KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        IsLoading = true;
        try
        {
            await LoadNhomKhachHangAsync();
            await LoadNhomSanPhamAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadNhomKhachHangAsync()
    {
        try
        {
            var list = await _db.GetNhomKhachHangAsync();
            NhomKhachHangOptions.Clear();
            foreach (var item in list)
                NhomKhachHangOptions.Add(item);

            SelectedNhomKhachHang = NhomKhachHangOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("LoadNhomKhachHangAsync error", ex);
        }
    }

    private async Task LoadNhomSanPhamAsync()
    {
        try
        {
            var list = await _db.GetNhomSanPhamAsync();
            NhomSanPhamOptions.Clear();
            NhomSanPhamOptions.Add(new ProductOption { Id = 0, Name = "Tất cả nhóm hàng" });
            if (list != null)
            {
                foreach (var item in list)
                    NhomSanPhamOptions.Add(item);
            }
            SelectedNhomSanPham = NhomSanPhamOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("LoadNhomSanPhamAsync error", ex);
        }
    }

    // =========================================================
    // KHÁCH HÀNG: THÊM / XÓA
    // =========================================================

    partial void OnCustomerSearchChanged(string value)
    {
        _customerSearchCts?.Cancel();
        _customerSearchCts?.Dispose();
        _customerSearchCts = new CancellationTokenSource();
        var token = _customerSearchCts.Token;

        var keyword = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(keyword))
        {
            CustomerSuggestions.Clear();
            HasCustomerSuggestions = false;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;

                var list = await _db.GetKhachHangAsync(keyword);
                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CustomerSuggestions.Clear();
                    foreach (var item in list.Take(15))
                        CustomerSuggestions.Add(item);

                    HasCustomerSuggestions = CustomerSuggestions.Count > 0;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SafeLog.WriteError("TimKiemKhachHang error", ex);
            }
        });
    }

    [RelayCommand]
    private void ChonKhachHangGoiY(KhachHang? kh)
    {
        if (kh == null) return;
        ThemKhachHangVaoDanhSach(kh);
        CustomerSearch = "";
        CustomerSuggestions.Clear();
        HasCustomerSuggestions = false;
    }

    [RelayCommand]
    private async Task ThemNhomKhachHangAsync()
    {
        if (SelectedNhomKhachHang == null || SelectedNhomKhachHang.Id == 0)
        {
            StatusMessage = "Vui lòng chọn một nhóm khách hàng.";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Đang tải khách hàng nhóm \"{SelectedNhomKhachHang.TenNhom}\"...";

        try
        {
            var list = await _db.GetKhachHangTheoNhomAsync(SelectedNhomKhachHang.Id);
            var addedCount = 0;

            foreach (var kh in list)
            {
                if (!DanhSachKhachHang.Any(k => k.Id == kh.Id))
                {
                    DanhSachKhachHang.Add(kh);
                    addedCount++;
                }
            }

            CapNhatThongKe();
            StatusMessage = $"Đã thêm {addedCount} khách hàng từ nhóm \"{SelectedNhomKhachHang.TenNhom}\".";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi nạp khách hàng theo nhóm: " + ex.Message;
            SafeLog.WriteError("ThemNhomKhachHangAsync error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ThemKhachHangVaoDanhSach(KhachHang kh)
    {
        if (kh == null || kh.Id == 0) return;

        if (DanhSachKhachHang.Any(k => k.Id == kh.Id))
        {
            StatusMessage = $"Khách hàng \"{kh.TenKhachHang}\" đã có trong danh sách.";
            return;
        }

        DanhSachKhachHang.Add(kh);
        CapNhatThongKe();
        StatusMessage = $"Đã thêm khách hàng \"{kh.TenKhachHang}\".";
    }

    [RelayCommand]
    private void XoaKhachHang(KhachHang? kh)
    {
        if (kh == null) return;
        DanhSachKhachHang.Remove(kh);
        CapNhatThongKe();
    }

    [RelayCommand]
    private void XoaTatCaKhachHang()
    {
        DanhSachKhachHang.Clear();
        CapNhatThongKe();
        StatusMessage = "Đã xóa toàn bộ khách hàng khỏi danh sách.";
    }

    // =========================================================
    // SẢN PHẨM: THÊM / XÓA
    // =========================================================

    partial void OnProductSearchChanged(string value)
    {
        _productSearchCts?.Cancel();
        _productSearchCts?.Dispose();
        _productSearchCts = new CancellationTokenSource();
        var token = _productSearchCts.Token;

        var keyword = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(keyword))
        {
            ProductSuggestions.Clear();
            HasProductSuggestions = false;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;

                var (items, _) = await _db.GetSanPhamPagedAsync(keyword, 1, 20);
                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ProductSuggestions.Clear();
                    foreach (var item in items)
                        ProductSuggestions.Add(item);

                    HasProductSuggestions = ProductSuggestions.Count > 0;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SafeLog.WriteError("TimKiemSanPham error", ex);
            }
        });
    }

    [RelayCommand]
    private void ChonSanPhamGoiY(SanPham? sp)
    {
        if (sp == null) return;
        ThemSanPhamVaoDanhSach(sp);
        ProductSearch = "";
        ProductSuggestions.Clear();
        HasProductSuggestions = false;
    }

    [RelayCommand]
    private async Task ThemTatCaSanPhamNhomAsync()
    {
        IsLoading = true;
        StatusMessage = "Đang nạp danh sách sản phẩm...";

        try
        {
            var (products, _) = await _db.GetSanPhamPagedAsync(null, 1, 1000);
            var nhomId = SelectedNhomSanPham?.Id ?? 0;

            if (nhomId > 0)
            {
                products = products.Where(p => p.NhomSanPhamId == nhomId).ToList();
            }

            var addedCount = 0;
            foreach (var sp in products)
            {
                if (!DanhSachSanPham.Any(item => item.SanPhamId == sp.Id))
                {
                    DanhSachSanPham.Add(new SanPhamGiaRiengItem
                    {
                        Stt = DanhSachSanPham.Count + 1,
                        SanPhamId = sp.Id,
                        MaSanPham = sp.MaSanPham,
                        TenSanPham = sp.TenSanPham,
                        DonViTinh = sp.DonViTinh ?? "Cái",
                        GiaBan = sp.GiaBan,
                        GiaVon = sp.GiaVon,
                        GiaRieng = sp.GiaBan, // Mặc định bằng giá niêm yết
                        GhiChu = GhiChuChung
                    });
                    addedCount++;
                }
            }

            CapNhatSttSanPham();
            CapNhatThongKe();
            StatusMessage = $"Đã thêm {addedCount} sản phẩm vào bảng thiết lập giá.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi nạp sản phẩm: " + ex.Message;
            SafeLog.WriteError("ThemTatCaSanPhamNhomAsync error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ThemSanPhamVaoDanhSach(SanPham sp)
    {
        if (sp == null || sp.Id == 0) return;

        if (DanhSachSanPham.Any(x => x.SanPhamId == sp.Id))
        {
            StatusMessage = $"Sản phẩm \"{sp.TenSanPham}\" đã có trong danh sách.";
            return;
        }

        DanhSachSanPham.Add(new SanPhamGiaRiengItem
        {
            Stt = DanhSachSanPham.Count + 1,
            SanPhamId = sp.Id,
            MaSanPham = sp.MaSanPham,
            TenSanPham = sp.TenSanPham,
            DonViTinh = sp.DonViTinh ?? "Cái",
            GiaBan = sp.GiaBan,
            GiaVon = sp.GiaVon,
            GiaRieng = sp.GiaBan,
            GhiChu = GhiChuChung
        });

        CapNhatSttSanPham();
        CapNhatThongKe();
        StatusMessage = $"Đã thêm sản phẩm \"{sp.TenSanPham}\".";
    }

    [RelayCommand]
    private void XoaSanPham(SanPhamGiaRiengItem? item)
    {
        if (item == null) return;
        DanhSachSanPham.Remove(item);
        CapNhatSttSanPham();
        CapNhatThongKe();
    }

    [RelayCommand]
    private void XoaTatCaSanPham()
    {
        DanhSachSanPham.Clear();
        CapNhatThongKe();
        StatusMessage = "Đã xóa toàn bộ sản phẩm khỏi danh sách.";
    }

    private void CapNhatSttSanPham()
    {
        for (int i = 0; i < DanhSachSanPham.Count; i++)
            DanhSachSanPham[i].Stt = i + 1;
    }

    // =========================================================
    // CÔNG CỤ TÍNH GIÁ NHANH HÀNG LOẠT
    // =========================================================

    [RelayCommand]
    private void ApDungGiamPhanTram()
    {
        if (!double.TryParse(BatchCalcValue, out var pt) || pt < 0 || pt > 100)
        {
            StatusMessage = "Vui lòng nhập phần trăm giảm hợp lệ (từ 0 đến 100).";
            return;
        }

        foreach (var item in DanhSachSanPham)
        {
            var giaMoi = item.GiaBan * (decimal)(1 - pt / 100);
            item.GiaRieng = Math.Max(0, Math.Round(giaMoi / 1000m) * 1000m);
        }

        CapNhatThongKe();
        StatusMessage = $"Đã áp dụng giảm {pt:0.#}% cho tất cả {DanhSachSanPham.Count} sản phẩm.";
    }

    [RelayCommand]
    private void ApDungGiamSoTien()
    {
        if (!MoneyInput.TryParse(BatchCalcValue, true, out var soTien, out _) || soTien < 0)
        {
            StatusMessage = "Vui lòng nhập số tiền giảm hợp lệ.";
            return;
        }

        foreach (var item in DanhSachSanPham)
        {
            item.GiaRieng = Math.Max(0, item.GiaBan - soTien);
        }

        CapNhatThongKe();
        StatusMessage = $"Đã áp dụng giảm {soTien:N0} đ cho tất cả {DanhSachSanPham.Count} sản phẩm.";
    }

    [RelayCommand]
    private void ApDungGiaVonCongLai()
    {
        if (!double.TryParse(BatchCalcValue, out var pt) || pt < 0)
        {
            StatusMessage = "Vui lòng nhập % tỷ lệ lãi hợp lệ.";
            return;
        }

        foreach (var item in DanhSachSanPham)
        {
            var giaMoi = item.GiaVon * (decimal)(1 + pt / 100);
            item.GiaRieng = Math.Max(0, Math.Round(giaMoi / 1000m) * 1000m);
        }

        CapNhatThongKe();
        StatusMessage = $"Đã áp dụng Giá vốn + {pt:0.#}% cho tất cả {DanhSachSanPham.Count} sản phẩm.";
    }

    [RelayCommand]
    private void KhoiPhucGiaNiemYet()
    {
        foreach (var item in DanhSachSanPham)
        {
            item.GiaRieng = item.GiaBan;
        }

        CapNhatThongKe();
        StatusMessage = "Đã khôi phục giá riêng bằng giá niêm yết cho toàn bộ sản phẩm.";
    }

    private void CapNhatThongKe()
    {
        OnPropertyChanged(nameof(TongSoKhachHang));
        OnPropertyChanged(nameof(TongSoSanPham));
        OnPropertyChanged(nameof(SoHangGiamGia));
        OnPropertyChanged(nameof(SoHangThapHonGiaVon));
        OnPropertyChanged(nameof(TongSoKhachHangText));
        OnPropertyChanged(nameof(TongSoSanPhamText));
        OnPropertyChanged(nameof(CanhBaoGiaVonText));
        OnPropertyChanged(nameof(CoCanhBaoGiaVon));
    }

    // =========================================================
    // LƯU DỮ LIỆU HÀNG LOẠT
    // =========================================================

    [RelayCommand]
    private async Task LuuThietLapGiaAsync()
    {
        var owner = LayCuaSoChinh?.Invoke();

        if (DanhSachKhachHang.Count == 0)
        {
            await NotificationWindow.ShowWarningAsync(
                "Vui lòng chọn ít nhất một khách hàng để áp dụng bảng giá.",
                "Chưa chọn khách hàng",
                owner);
            return;
        }

        if (DanhSachSanPham.Count == 0)
        {
            await NotificationWindow.ShowWarningAsync(
                "Vui lòng chọn ít nhất một sản phẩm cần thiết lập giá bán riêng.",
                "Chưa chọn sản phẩm",
                owner);
            return;
        }

        // Cảnh báo nếu có mặt hàng bán dưới giá vốn
        if (SoHangThapHonGiaVon > 0 && owner != null)
        {
            var confirm = new ConfirmationWindow(
                "Cảnh báo bán dưới giá vốn",
                $"Phát hiện {SoHangThapHonGiaVon} sản phẩm có giá bán riêng thấp hơn giá vốn gốc (nguy cơ bán lỗ).\n\nBạn có chắc chắn muốn lưu và áp dụng bảng giá này cho {DanhSachKhachHang.Count} khách hàng không?",
                "Vẫn lưu",
                "Kiểm tra lại");

            var ok = await confirm.ShowDialog<bool>(owner);
            if (!ok) return;
        }

        IsLoading = true;
        StatusMessage = $"Đang lưu thiết lập giá cho {DanhSachKhachHang.Count} khách hàng và {DanhSachSanPham.Count} sản phẩm...";

        try
        {
            var khachIds = DanhSachKhachHang.Select(k => k.Id).ToList();
            var danhSachGia = DanhSachSanPham.Select(sp => (
                SanPhamId: sp.SanPhamId,
                GiaRieng: sp.GiaRieng,
                GhiChu: string.IsNullOrWhiteSpace(sp.GhiChu) ? GhiChuChung : sp.GhiChu,
                NgayApDung: NgayApDung
            )).ToList();

            var totalSaved = await _db.LuuGiaBanKhachHangHangLoatAsync(khachIds, danhSachGia);

            await NotificationWindow.ShowSuccessAsync(
                $"Đã thiết lập giá bán riêng thành công!\n• Áp dụng cho: {DanhSachKhachHang.Count} khách hàng\n• Số mặt hàng: {DanhSachSanPham.Count} sản phẩm\n• Tổng số bản ghi giá: {totalSaved:N0}",
                "Thiết lập giá thành công",
                owner);

            YeuCauDong?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = "Lỗi khi lưu bảng giá: " + ex.Message;
            await NotificationWindow.ShowErrorAsync(
                "Không thể lưu thiết lập giá riêng: " + ex.Message,
                "Lỗi lưu dữ liệu",
                owner);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
