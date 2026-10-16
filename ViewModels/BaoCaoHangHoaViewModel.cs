using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.ViewModels;

public enum BaoCaoHangHoaKieuHienThi { BieuDo, BaoCao }
public enum BaoCaoHangHoaMocQuanTam { DoanhThu, SoLuong, LoiNhuan }

/// <summary>
/// ViewModel cho màn "Báo cáo hàng hóa": KPI doanh thu / giá vốn / lợi nhuận,
/// top 10 biểu đồ ngang, doanh thu theo nhóm hàng và bảng chi tiết sắp xếp
/// được, tìm kiếm được, phân trang (tổng hợp tính trong SQL).
/// <para>
/// Giữ instance sống suốt phiên như <see cref="BaoCaoBanHangViewModel"/>;
/// không tải dữ liệu trong constructor — <see cref="MainWindowViewModel"/>
/// chỉ gọi <see cref="TaiLaiAsync"/> khi người dùng mở màn lần đầu.
/// </para>
/// </summary>
public partial class BaoCaoHangHoaViewModel : ObservableObject
{
    private const string TatCaLoaiHang = "Tất cả loại hàng";

    private readonly BaoCaoHangHoaService _service;

    /// <summary>Cờ: người dùng đã bấm sort cột thủ công thì bỏ áp "Mối quan tâm".</summary>
    private bool _sapXepThuCong;

    public BaoCaoHangHoaViewModel(DatabaseService db)
    {
        _service = new BaoCaoHangHoaService(db.TaoKetNoiAsync);

        KieuHienThi = BaoCaoHangHoaKieuHienThi.BieuDo;

        // Tuần này: từ thứ Hai đến hôm nay (pattern của BaoCaoBanHangViewModel).
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7;
        TuNgay = homNay.AddDays(-soNgayDaQua);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Tuần này";

        LoaiHangs.Add(TatCaLoaiHang);
        LoaiHangChon = TatCaLoaiHang;
        NhomChonIndex = 0;
        MocQuanTamChon = MocQuanTamOptions[0];
        MocQuanTam = BaoCaoHangHoaMocQuanTam.DoanhThu;
        GopHangHoa = false;
        TimKiem = string.Empty;
        TimKiemBang = string.Empty;
        SoDongTrang = 15;

        // Không tải dữ liệu trong constructor: MainWindowViewModel chỉ gọi
        // TaiLaiAsync() khi người dùng mở màn lần đầu, tránh truy vấn database
        // ngay khi khởi động ứng dụng.
    }

    // ============================================================
    // TRẠNG THÁI / CHẾ ĐỘ HIỂN THỊ
    // ============================================================

    [ObservableProperty] private BaoCaoHangHoaKieuHienThi kieuHienThi;
    [ObservableProperty] private BaoCaoHangHoaMocQuanTam mocQuanTam;
    [ObservableProperty] private bool dangTai;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoLoi))]
    private string thongBaoLoi = string.Empty;

    /// <summary>RadioButton "Biểu đồ" bind TwoWay vào chế độ hiển thị.</summary>
    public bool HienThiBieuDo
    {
        get => KieuHienThi == BaoCaoHangHoaKieuHienThi.BieuDo;
        set { if (value) KieuHienThi = BaoCaoHangHoaKieuHienThi.BieuDo; }
    }

    /// <summary>RadioButton "Báo cáo" bind TwoWay vào chế độ hiển thị.</summary>
    public bool HienThiBaoCao
    {
        get => KieuHienThi == BaoCaoHangHoaKieuHienThi.BaoCao;
        set { if (value) KieuHienThi = BaoCaoHangHoaKieuHienThi.BaoCao; }
    }

    partial void OnKieuHienThiChanged(BaoCaoHangHoaKieuHienThi value)
    {
        OnPropertyChanged(nameof(HienThiBieuDo));
        OnPropertyChanged(nameof(HienThiBaoCao));
    }

    // ============================================================
    // BỘ LỌC THỜI GIAN
    // ============================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime tuNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    private DateTime denNgay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KhoangHienThi))]
    [NotifyPropertyChangedFor(nameof(DangChonTuanNay))]
    [NotifyPropertyChangedFor(nameof(DangChonTuyChinh))]
    private string khoangThoiGian = "Tuần này";

    [ObservableProperty] private bool tuyChinh;

    public bool DangChonTuanNay => KhoangThoiGian == "Tuần này";
    public bool DangChonTuyChinh => KhoangThoiGian == "Tùy chọn";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? tuNgayChon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoKhoangTuyChinh))]
    private DateTimeOffset? denNgayChon;

    public bool CoKhoangTuyChinh => TuNgayChon is not null && DenNgayChon is not null;

    public string KhoangHienThi => $"Khoảng thời gian: {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";

    // ============================================================
    // BỘ LỌC HÀNG HÓA (chờ nút "Áp dụng")
    // ============================================================

    [ObservableProperty] private bool gopHangHoa;

    [ObservableProperty] private string timKiem = string.Empty;

    [ObservableProperty] private string? loaiHangChon;

    [ObservableProperty] private int nhomChonIndex;

    [ObservableProperty] private string? mocQuanTamChon;

    /// <summary>Tất cả loại hàng (khớp option đầu của <see cref="LoaiHangs"/>).</summary>
    public ObservableCollection<string> LoaiHangs { get; } = new();

    public ObservableCollection<BaoCaoHangHoaNhomOption> NhomHangOptions { get; } = new();

    public ObservableCollection<string> MocQuanTamOptions { get; } =
        new() { "Doanh thu", "Số lượng bán", "Lợi nhuận" };

    // ============================================================
    // DỮ LIỆU
    // ============================================================

    [ObservableProperty] private BaoCaoHangHoaDuLieu duLieu = new();

    public BaoCaoHangHoaTongHop TongHop => DuLieu.TongHop;

    public ObservableCollection<BaoCaoHangHoaTop> TopDoanhThuItems => new(DuLieu.TopDoanhThu);
    public ObservableCollection<BaoCaoHangHoaTop> TopSoLuongItems => new(DuLieu.TopSoLuong);
    public ObservableCollection<BaoCaoHangHoaNhom> NhomHangItems => new(DuLieu.NhomHang);

    // ============================================================
    // CỜ RỖNG / CÓ DỮ LIỆU
    // ============================================================

    public bool CoLoi => !string.IsNullOrWhiteSpace(ThongBaoLoi);
    public bool CoTopDoanhThu => DuLieu.TopDoanhThu.Count > 0;
    public bool CoTopSoLuong => DuLieu.TopSoLuong.Count > 0;
    public bool CoNhomHang => DuLieu.NhomHang.Count > 0;
    public bool CoChiTiet => TongSoDong > 0;

    private void ThongBaoCoGiaTri()
    {
        OnPropertyChanged(nameof(TongHop));
        OnPropertyChanged(nameof(TopDoanhThuItems));
        OnPropertyChanged(nameof(TopSoLuongItems));
        OnPropertyChanged(nameof(NhomHangItems));
        OnPropertyChanged(nameof(CoTopDoanhThu));
        OnPropertyChanged(nameof(CoTopSoLuong));
        OnPropertyChanged(nameof(CoNhomHang));
        CapNhatBang();
    }

    // ============================================================
    // BẢNG CHI TIẾT: TÌM KIẾM / SẮP XẾP / PHÂN TRANG
    // ============================================================

    /// <summary>Tìm kiếm tức thì trong bảng (lọc trong bộ nhớ trên kết quả đã tổng hợp).</summary>
    [ObservableProperty] private string timKiemBang = string.Empty;

    [ObservableProperty] private string sapXepKho = "DoanhThu";
    [ObservableProperty] private bool sapXepTang;
    [ObservableProperty] private int trang = 1;
    [ObservableProperty] private int soDongTrang = 15;

    public ObservableCollection<int> SoDongTrangOptions { get; } = new() { 10, 15, 25, 50 };

    /// <summary>Các dòng thuộc trang đang xem (STT tính tiếp tục giữa các trang).</summary>
    public System.Collections.Generic.IEnumerable<BaoCaoHangHoaChiTiet> ChiTietTrang
    {
        get
        {
            var list = LocVaSapXep();
            int offset = (Trang - 1) * SoDongTrang;
            var trang = list.Skip(offset).Take(SoDongTrang).ToList();
            for (int i = 0; i < trang.Count; i++)
                trang[i].Stt = offset + i + 1;
            return trang;
        }
    }

    public int TongSoDong => LocVaSapXep().Count;

    public int TongSoTrang =>
        SoDongTrang <= 0 ? 0 : (int)Math.Ceiling(TongSoDong / (double)SoDongTrang);

    /// <summary>Ví dụ: "1 - 15 trong 367 mã hàng".</summary>
    public string VanBanPhanTrang
    {
        get
        {
            if (TongSoDong == 0)
                return "0 mã hàng";

            var tu = (Trang - 1) * SoDongTrang + 1;
            var den = Math.Min(tu + SoDongTrang - 1, TongSoDong);
            return $"{tu} - {den} trong {TongSoDong} mã hàng";
        }
    }

    public bool CanTrangTruoc => Trang > 1;
    public bool CanTrangSau => Trang < TongSoTrang;

    /// <summary>Dấu ▲/▼ cho từng cột — chỉ cột đang sắp xếp mới có dấu.</summary>
    public string DanhDauMa => DanhDau("MaHang");
    public string DanhDauTen => DanhDau("TenHang");
    public string DanhDauNhom => DanhDau("NhomHang");
    public string DanhDauSoLuong => DanhDau("SoLuongBan");
    public string DanhDauDoanhThu => DanhDau("DoanhThu");
    public string DanhDauGiaVon => DanhDau("GiaVon");
    public string DanhDauLoiNhuan => DanhDau("LoiNhuan");

    private string DanhDau(string cot) => SapXepKho == cot ? (SapXepTang ? "▲" : "▼") : string.Empty;

    private System.Collections.Generic.List<BaoCaoHangHoaChiTiet> LocVaSapXep()
    {
        var q = DuLieu.ChiTiet.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(TimKiemBang))
        {
            var tu = TimKiemBang.Trim();
            q = q.Where(r =>
                (r.TenHang?.IndexOf(tu, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (r.MaHang?.IndexOf(tu, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (r.NhomHang?.IndexOf(tu, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }

        q = SapXepKho switch
        {
            "MaHang" => SapXepTang ? q.OrderBy(r => r.MaHang) : q.OrderByDescending(r => r.MaHang),
            "TenHang" => SapXepTang ? q.OrderBy(r => r.TenHang) : q.OrderByDescending(r => r.TenHang),
            "NhomHang" => SapXepTang ? q.OrderBy(r => r.NhomHang) : q.OrderByDescending(r => r.NhomHang),
            "SoLuongBan" => SapXepTang ? q.OrderBy(r => r.SoLuongBan) : q.OrderByDescending(r => r.SoLuongBan),
            "GiaVon" => SapXepTang ? q.OrderBy(r => r.GiaVon) : q.OrderByDescending(r => r.GiaVon),
            "LoiNhuan" => SapXepTang ? q.OrderBy(r => r.LoiNhuan) : q.OrderByDescending(r => r.LoiNhuan),
            _ => SapXepTang ? q.OrderBy(r => r.DoanhThu) : q.OrderByDescending(r => r.DoanhThu)
        };

        return q.ToList();
    }

    private void CapNhatBang()
    {
        int tongTrang = TongSoTrang;
        if (tongTrang > 0 && Trang > tongTrang)
        {
            // Trang vượt quá dữ liệu (bớt bộ lọc/search) — lùi về trang cuối.
            Trang = tongTrang;
            return; // OnTrangChanged sẽ gọi lại CapNhatBang lần nữa.
        }

        OnPropertyChanged(nameof(ChiTietTrang));
        OnPropertyChanged(nameof(TongSoDong));
        OnPropertyChanged(nameof(TongSoTrang));
        OnPropertyChanged(nameof(VanBanPhanTrang));
        OnPropertyChanged(nameof(CanTrangTruoc));
        OnPropertyChanged(nameof(CanTrangSau));
        OnPropertyChanged(nameof(CoChiTiet));
    }

    partial void OnTrangChanged(int value) => CapNhatBang();

    partial void OnSoDongTrangChanged(int value)
    {
        if (Trang != 1)
            Trang = 1;
        else
            CapNhatBang();
    }

    partial void OnTimKiemBangChanged(string value)
    {
        if (Trang != 1)
            Trang = 1;
        else
            CapNhatBang();
    }

    partial void OnSapXepKhoChanged(string value) => ThongBaoDauCot();
    partial void OnSapXepTangChanged(bool value) => ThongBaoDauCot();

    private void ThongBaoDauCot()
    {
        OnPropertyChanged(nameof(DanhDauMa));
        OnPropertyChanged(nameof(DanhDauTen));
        OnPropertyChanged(nameof(DanhDauNhom));
        OnPropertyChanged(nameof(DanhDauSoLuong));
        OnPropertyChanged(nameof(DanhDauDoanhThu));
        OnPropertyChanged(nameof(DanhDauGiaVon));
        OnPropertyChanged(nameof(DanhDauLoiNhuan));
    }

    [RelayCommand]
    private void SapXep(string cot)
    {
        _sapXepThuCong = true;

        if (SapXepKho == cot)
            SapXepTang = !SapXepTang;
        else
        {
            SapXepKho = cot;
            // Cột chữ sắp tăng dần, cột số sắp giảm dần cho đúng intuition.
            SapXepTang = cot is "MaHang" or "TenHang" or "NhomHang";
        }

        if (Trang != 1)
            Trang = 1;
        else
            CapNhatBang();
    }

    [RelayCommand] private void TrangDau() { Trang = 1; }
    [RelayCommand] private void TrangTruoc() { if (Trang > 1) Trang--; }
    [RelayCommand] private void TrangSau() { if (Trang < TongSoTrang) Trang++; }
    [RelayCommand] private void TrangCuoi() { Trang = Math.Max(1, TongSoTrang); }

    // ============================================================
    // TẢI DỮ LIỆU
    // ============================================================

    [RelayCommand]
    public async Task TaiLaiAsync()
    {
        await TaiDuLieuAsync();
    }

    private async Task TaiDuLieuAsync()
    {
        try
        {
            DangTai = true;
            ThongBaoLoi = string.Empty;

            if (DenNgay < TuNgay)
                DenNgay = TuNgay;

            // Danh sách loại hàng / nhóm hàng chỉ cần nạp một lần cho phiên.
            if (NhomHangOptions.Count == 0)
            {
                var loais = await _service.TaiLoaiHangAsync(default);
                foreach (var l in loais)
                    if (!LoaiHangs.Contains(l))
                        LoaiHangs.Add(l);

                var nhoms = await _service.TaiNhomHangAsync(default);
                foreach (var n in nhoms)
                    NhomHangOptions.Add(n);
            }

            DuLieu = await _service.TaiDuLieuAsync(
                TuNgay,
                DenNgay,
                GopHangHoa,
                LocLoaiHang(),
                LocNhomId(),
                TimKiem,
                default);

            ApSapXepMacDinh();
            ThongBaoCoGiaTri();
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không thể tải báo cáo hàng hóa: " + ex.Message;
        }
        finally
        {
            DangTai = false;
        }
    }

    /// <summary>Giá trị ComboBox "Loại hàng" → tham số SQL (null = không lọc).</summary>
    private string? LocLoaiHang()
    {
        var v = LoaiHangChon?.Trim();
        if (string.IsNullOrEmpty(v) || v.StartsWith("Tất cả", StringComparison.Ordinal))
            return null;
        return v;
    }

    /// <summary>Chỉ số ComboBox "Nhóm hàng" → id (-1 tất cả, 0 chưa phân nhóm, &gt;0 id thật).</summary>
    private int LocNhomId()
    {
        if (NhomChonIndex < 0 || NhomChonIndex >= NhomHangOptions.Count)
            return -1;
        return NhomHangOptions[NhomChonIndex].Id;
    }

    /// <summary>Sắp xếp mặc định theo "Mối quan tâm" (trừ khi người dùng đã bấm sort cột).</summary>
    private void ApSapXepMacDinh()
    {
        if (_sapXepThuCong)
            return;

        (SapXepKho, SapXepTang) = MocQuanTam switch
        {
            BaoCaoHangHoaMocQuanTam.SoLuong => ("SoLuongBan", false),
            BaoCaoHangHoaMocQuanTam.LoiNhuan => ("LoiNhuan", false),
            _ => ("DoanhThu", false)
        };
    }

    // ============================================================
    // LỆNH BỘ LỌC
    // ============================================================

    [RelayCommand]
    private async Task VeTuanNayAsync()
    {
        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7;
        TuNgay = homNay.AddDays(-soNgayDaQua);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Tuần này";
        TuyChinh = false;

        await TaiDuLieuAsync();
    }

    /// <summary>Bật vùng chọn ngày tùy chỉnh — ngày chỉ hiệu lực khi bấm "Áp dụng".</summary>
    [RelayCommand]
    private void ChonTuyChinh()
    {
        KhoangThoiGian = "Tùy chọn";
        TuyChinh = true;
    }

    /// <summary>Áp dụng toàn bộ bộ lọc (ngày tùy chỉnh + gộp + loại/nhóm + tìm kiếm + mối quan tâm).</summary>
    [RelayCommand]
    private async Task ApDungAsync()
    {
        if (KhoangThoiGian == "Tùy chọn" && TuNgayChon is not null && DenNgayChon is not null)
        {
            var tu = TuNgayChon.Value.Date;
            var den = DenNgayChon.Value.Date;
            if (den < tu)
                (tu, den) = (den, tu);
            TuNgay = tu;
            DenNgay = den;
        }

        MocQuanTam = MocQuanTamChon switch
        {
            "Số lượng bán" => BaoCaoHangHoaMocQuanTam.SoLuong,
            "Lợi nhuận" => BaoCaoHangHoaMocQuanTam.LoiNhuan,
            _ => BaoCaoHangHoaMocQuanTam.DoanhThu
        };

        // Áp dụng bộ lọc mới thì quay lại sắp xếp mặc định theo "Mối quan tâm".
        _sapXepThuCong = false;

        await TaiDuLieuAsync();
    }

    /// <summary>Xóa toàn bộ bộ lọc, đưa màn về trạng thái mặc định (tuần này).</summary>
    [RelayCommand]
    private async Task XoaBoLocAsync()
    {
        GopHangHoa = false;
        TimKiem = string.Empty;
        TimKiemBang = string.Empty;
        LoaiHangChon = TatCaLoaiHang;
        NhomChonIndex = 0;
        MocQuanTamChon = MocQuanTamOptions[0];
        MocQuanTam = BaoCaoHangHoaMocQuanTam.DoanhThu;

        var homNay = DateTime.Today;
        var soNgayDaQua = ((int)homNay.DayOfWeek + 6) % 7;
        TuNgay = homNay.AddDays(-soNgayDaQua);
        DenNgay = homNay;
        TuNgayChon = new DateTimeOffset(TuNgay);
        DenNgayChon = new DateTimeOffset(DenNgay);
        KhoangThoiGian = "Tuần này";
        TuyChinh = false;

        _sapXepThuCong = false;
        SapXepKho = "DoanhThu";
        SapXepTang = false;

        if (Trang != 1)
            Trang = 1;

        await TaiDuLieuAsync();
    }

    // ============================================================
    // LỆNH KHÁC
    // ============================================================

    [RelayCommand]
    private void ChonBieuDo() => KieuHienThi = BaoCaoHangHoaKieuHienThi.BieuDo;

    [RelayCommand]
    private void ChonBaoCao() => KieuHienThi = BaoCaoHangHoaKieuHienThi.BaoCao;

    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        try
        {
            var cuaSo = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt
                ? lt.MainWindow
                : null;

            if (cuaSo is null)
                return;

            using (var wb = BaoCaoHangHoaExcel.TaoWorkbook(DuLieu))
            {
                var file = await cuaSo.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Xuất Excel báo cáo hàng hóa",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }
                    },
                    SuggestedFileName = DuLieu.TongHop.TenFileXuat()
                });

                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    wb.SaveAs(stream);
                }
            }
        }
        catch (Exception ex)
        {
            ThongBaoLoi = "Không xuất được file Excel: " + ex.Message;
        }
    }
}
