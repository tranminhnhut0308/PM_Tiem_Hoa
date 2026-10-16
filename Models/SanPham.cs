using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;

namespace ShopHoa.Models;

public sealed partial class SanPham : ObservableObject
{
    public bool IsSelected { get; set; }
    public uint Id { get; set; }
    public string MaSanPham { get; set; } = "";
    public string? MaVach { get; set; }
    public string TenSanPham { get; set; } = "";
    public uint? NhomSanPhamId { get; set; }

    [ObservableProperty]
    private uint? donViTinhId;

    public uint? ThuongHieuId { get; set; }

    [ObservableProperty]
    private decimal giaBan;

    public decimal GiaVon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HetHang))]
    private decimal tonKho;

    public decimal TonToiThieu { get; set; }
    public decimal TonToiDa { get; set; }
    public decimal KhachDat { get; set; }
    public DateTime? NgayTao { get; set; }
    public string? DuKienHetHang { get; set; }

    [ObservableProperty]
    private string? donViTinh;

    public string? NhomSanPham { get; set; }
    public string? LoaiHang { get; set; }
    public string? NhomHangBaCap { get; set; }

    [ObservableProperty]
    private decimal tonKhoExcel;

    public string? ViTriKho { get; set; }
    public bool DangKinhDoanh { get; set; }
    public bool DuocBanTrucTiep { get; set; }
    public string? MoTa { get; set; }
    public string? GhiChu { get; set; }

    // =========================================================
    // CỘT LẤY TỪ FILE EXCEL DANH SÁCH HÀNG HÓA
    // Các cột này có trong bảng san_pham nhưng trước đây đọc vào rồi bị bỏ,
    // nên dữ liệu nhập từ file không đầy đủ. Không cột nào trong nhóm này
    // được coi là bắt buộc: đều có thể để NULL/rỗng mà dòng vẫn hợp lệ.
    // =========================================================

    /// <summary>Cột "Mã ĐVT Cơ bản" của file Excel.</summary>
    public string? MaDvtCoBan { get; set; }

    /// <summary>Cột "Quy đổi" của file Excel (mặc định 1).</summary>
    public decimal QuyDoi { get; set; } = 1m;

    /// <summary>Cột "Thuộc tính".</summary>
    public string? ThuocTinh { get; set; }

    /// <summary>Cột "Mã HH Liên quan".</summary>
    public string? MaHhLienQuan { get; set; }

    /// <summary>Cột "Trọng lượng".</summary>
    public decimal? TrongLuong { get; set; }

    /// <summary>Cột "Hàng thành phần".</summary>
    public string? HangThanhPhan { get; set; }

    /// <summary>Cột "Mẫu ghi chú".</summary>
    public string? MauGhiChu { get; set; }

    /// <summary>Tên thương hiệu lấy từ file; sẽ tra thành <see cref="ThuongHieuId"/> khi nhập.</summary>
    public string? ThuongHieu { get; set; }

    /// <summary>Tên đơn vị tính lấy từ cột "ĐVT"; sẽ tra thành <see cref="DonViTinhId"/> khi nhập.</summary>
    public string? TenDonViTinh { get; set; }

    /// <summary>Số dòng trong file Excel, dùng để báo lỗi cho người dùng.</summary>
    public int SoDongExcel { get; set; }

    [ObservableProperty]
    private string? hinhAnh;

    [ObservableProperty]
    private Bitmap? hinhAnhBitmap;

    // =========================================================
    // TRẠNG THÁI CHỈNH GIÁ NHANH NGAY TRÊN DANH SÁCH
    // (chỉ phục vụ giao diện, không lưu xuống database)
    // =========================================================

    /// <summary>Dòng đang mở ô nhập giá bán nhanh.</summary>
    [ObservableProperty]
    private bool dangChinhGia;

    /// <summary>Nội dung đang gõ trong ô giá bán nhanh (số, không có dấu phân cách).</summary>
    [ObservableProperty]
    private string giaBanDangNhap = "0";

    /// <summary>Lỗi hiển thị ngay dưới ô giá bán nhanh (rỗng = không lỗi).</summary>
    [ObservableProperty]
    private string? loiGiaBanNhanh;

    /// <summary>Đang ghi xuống database, khóa nút ✓ để tránh bấm hai lần.</summary>
    [ObservableProperty]
    private bool dangLuGiaBan;

    /// <summary>Sao chép các trường giao diện khi đóng ô chỉnh giá nhanh.</summary>
    public void DongGiaBanNhanh()
    {
        DangChinhGia = false;
        GiaBanDangNhap = "0";
        LoiGiaBanNhanh = null;
        DangLuGiaBan = false;
    }

    // Thêm thuộc tính kiểm tra hết hàng
    public bool HetHang => TonKho <= 0;
}