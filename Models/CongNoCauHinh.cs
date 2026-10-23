namespace ShopHoa.Models;

/// <summary>
/// Ngưỡng cấu hình dùng để tự động đánh giá mức độ ưu tiên nhắc nợ.
/// Giá trị được lưu trong bảng cau_hinh_cong_no để không phải hard-code.
/// </summary>
public sealed class CongNoCauHinh
{
    public int SoKyUuTienCao { get; set; } = 3;

    public decimal SoTienUuTienCao { get; set; } = 5_000_000m;

    public int SoKyUuTienTrungBinh { get; set; } = 2;

    public decimal SoTienUuTienTrungBinh { get; set; } = 2_000_000m;

    public static CongNoCauHinh MacDinh => new();

    public string DanhGiaMucDo(int soKyNo, decimal soTienNo)
    {
        if (soKyNo >= SoKyUuTienCao || soTienNo >= SoTienUuTienCao)
            return "CAO";

        if (soKyNo >= SoKyUuTienTrungBinh || soTienNo >= SoTienUuTienTrungBinh)
            return "TRUNG_BINH";

        return "THAP";
    }
}
