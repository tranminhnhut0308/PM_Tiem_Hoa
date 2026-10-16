using System.Globalization;

namespace ShopHoa.Models;

/// <summary>Dòng tổng hợp của báo cáo hàng hóa trong khoảng thời gian đang xem.</summary>
public sealed class BaoCaoHangHoaTongHop
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }

    /// <summary>Số mã hàng bán ra (đếm distinct theo cách gộp đang chọn).</summary>
    public int SoMaHang { get; set; }

    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }

    /// <summary>Tổng giá vốn = SUM(gia_von đơn vị × số lượng).</summary>
    public decimal GiaVon { get; set; }

    public decimal LoiNhuan => DoanhThu - GiaVon;

    /// <summary>Tỉ lệ lợi nhuận trên doanh thu (0..100).</summary>
    public double TyLeLoiNhuan => DoanhThu <= 0 ? 0 : (double)(LoiNhuan / DoanhThu * 100m);

    public string SoMaHangHienThi => SoMaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanHienThi => LoiNhuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeLoiNhuanHienThi => TyLeLoiNhuan.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";

    public string TenFileXuat() => $"BaoCaoHangHoa_{TuNgay:yyyyMMdd}_{DenNgay:yyyyMMdd}.xlsx";
}

/// <summary>Dòng top 10 (doanh thu hoặc số lượng) — có thanh ngang bind thẳng từ model.</summary>
public sealed class BaoCaoHangHoaTop
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }

    /// <summary>Chiều dài thanh so với dòng cao nhất (0..100).</summary>
    public double TyLeBar { get; set; }

    /// <summary>Bề rộng tối đa của thanh (px) — XAML lấy hằng này làm mốc.</summary>
    public const double BeRongToiDa = 260;

    /// <summary>Bề rộng thanh (px) để XAML bind thẳng, không cần converter.</summary>
    public double RongBar => TyLeBar <= 0 ? 0 : Math.Max(6, TyLeBar / 100d * BeRongToiDa);

    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLeBarHienThi => TyLeBar.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}

/// <summary>Dòng doanh thu theo nhóm hàng.</summary>
public sealed class BaoCaoHangHoaNhom
{
    public string TenNhom { get; set; } = string.Empty;
    public int SoMaHang { get; set; }
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }

    /// <summary>Độ lớn so với nhóm cao nhất (0..100) — dùng cho thanh bar.</summary>
    public double TyLeBar { get; set; }

    /// <summary>Tỉ trọng doanh thu của nhóm này trong tổng kỳ (0..100).</summary>
    public double TyLePhanTram { get; set; }

    public const double BeRongToiDa = 260;

    public double RongBar => TyLeBar <= 0 ? 0 : Math.Max(6, TyLeBar / 100d * BeRongToiDa);

    public string SoMaHangHienThi => SoMaHang.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string TyLePhanTramHienThi => TyLePhanTram.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%";
}

/// <summary>Dòng bảng chi tiết (một mã hàng trong khoảng đang xem).</summary>
public sealed class BaoCaoHangHoaChiTiet
{
    public int Stt { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public string NhomHang { get; set; } = string.Empty;
    public decimal SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal GiaVon { get; set; }

    public decimal LoiNhuan => DoanhThu - GiaVon;

    /// <summary>Lợi nhuận không âm — bind vào class "ln" của XAML (màu xanh).</summary>
    public bool LoiNhuanDuong => LoiNhuan >= 0;

    /// <summary>Lợi nhuận âm — bind vào class "lo" của XAML (màu cảnh báo).</summary>
    public bool LoiNhuanAm => LoiNhuan < 0;

    public string SoLuongBanHienThi => SoLuongBan.ToString("N3", CultureInfo.GetCultureInfo("vi-VN"));
    public string DoanhThuHienThi => DoanhThu.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string GiaVonHienThi => GiaVon.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
    public string LoiNhuanHienThi => LoiNhuan.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}

/// <summary>Tùy chọn nhóm hàng cho bộ lọc bên trái (gồm "Tất cả" và "Chưa phân nhóm").</summary>
public sealed class BaoCaoHangHoaNhomOption
{
    /// <summary>-1 = tất cả, 0 = sản phẩm chưa gán nhóm, &gt; 0 = id nhóm thật.</summary>
    public int Id { get; set; } = -1;

    public string TenNhom { get; set; } = string.Empty;

    public string TenHienThi { get; set; } = string.Empty;
}

/// <summary>Toàn bộ dữ liệu màn "Báo cáo hàng hóa" cho một lần tải.</summary>
public sealed class BaoCaoHangHoaDuLieu
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public BaoCaoHangHoaTongHop TongHop { get; set; } = new();
    public List<BaoCaoHangHoaTop> TopDoanhThu { get; set; } = new();
    public List<BaoCaoHangHoaTop> TopSoLuong { get; set; } = new();
    public List<BaoCaoHangHoaNhom> NhomHang { get; set; } = new();
    public List<BaoCaoHangHoaChiTiet> ChiTiet { get; set; } = new();
}
