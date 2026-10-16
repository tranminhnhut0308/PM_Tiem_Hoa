namespace ShopHoa.Models;

/// <summary>
/// Một phương thức thanh toán cho hộp thoại xác nhận thanh toán bán hàng.
/// <see cref="Ma"/> là mã lưu vào cột <c>phieu_chi.phuong_thuc</c>.
/// </summary>
public sealed class PhuongThucOption
{
    public string Ma { get; init; } = "";

    public string Ten { get; init; } = "";

    public override string ToString() => Ten;
}
