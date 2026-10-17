using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using ShopHoa.Helpers;

namespace ShopHoa.Models;

/// <summary>
/// Mặt hàng trong bảng thiết lập giá bán riêng cho từng khách hàng.
/// Chứa thông tin sản phẩm, giá bán chung (niêm yết) và giá bán riêng đã thiết lập.
/// </summary>
public sealed partial class GiaBanKhachHangItem : ObservableObject
{
    public uint Id { get; set; }
    public uint KhachHangId { get; set; }
    public uint SanPhamId { get; set; }

    public string MaSanPham { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? DonViTinh { get; set; }
    public uint? DonViTinhId { get; set; }

    public string? NhomSanPham { get; set; }
    public uint? NhomSanPhamId { get; set; }

    public decimal GiaGoc { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChenhLech))]
    [NotifyPropertyChangedFor(nameof(PhanTramChenhLech))]
    [NotifyPropertyChangedFor(nameof(GiaRiengText))]
    [NotifyPropertyChangedFor(nameof(ChenhLechText))]
    [NotifyPropertyChangedFor(nameof(ChenhLechColor))]
    private decimal giaRieng;

    [ObservableProperty]
    private string? ghiChu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NgayApDungText))]
    private DateTime ngayApDung = DateTime.Now;

    public DateTime? NgayCapNhat { get; set; }

    public int Stt { get; set; }

    /// <summary>Số tiền chênh lệch = Giá riêng - Giá gốc (âm là rẻ hơn giá niêm yết).</summary>
    public decimal ChenhLech => GiaRieng - GiaGoc;

    /// <summary>Phần trăm chênh lệch so với giá gốc.</summary>
    public double PhanTramChenhLech => GiaGoc > 0 ? (double)((GiaRieng - GiaGoc) / GiaGoc * 100) : 0;

    public string GiaGocText => MoneyInput.Format(GiaGoc) + " đ";

    public string GiaRiengText => MoneyInput.Format(GiaRieng) + " đ";

    public string NgayApDungText => NgayApDung.ToString("dd/MM/yyyy");

    /// <summary>Chuỗi mô tả chênh lệch kèm phần trăm (ví dụ: -15.000 đ (-10%)).</summary>
    public string ChenhLechText
    {
        get
        {
            if (ChenhLech < 0)
                return $"-{MoneyInput.Format(Math.Abs(ChenhLech))} đ ({PhanTramChenhLech:0.#}%)";
            if (ChenhLech > 0)
                return $"+{MoneyInput.Format(ChenhLech)} đ (+{PhanTramChenhLech:0.#}%)";
            return "Bằng giá chung (0%)";
        }
    }

    /// <summary>Màu sắc hiển thị chênh lệch giá.</summary>
    public IBrush ChenhLechColor
    {
        get
        {
            if (ChenhLech < 0)
                return new SolidColorBrush(Color.Parse("#087F6E")); // Xanh ngọc (ưu đãi giảm giá)
            if (ChenhLech > 0)
                return new SolidColorBrush(Color.Parse("#D97706")); // Cam hổ phách (tăng giá)
            return new SolidColorBrush(Color.Parse("#61736E")); // Xám (bằng nhau)
        }
    }
}
