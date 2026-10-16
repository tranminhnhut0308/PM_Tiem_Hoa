namespace ShopHoa.Models;

public sealed class LoginResultExtended
{
    public NguoiDung? User { get; set; }
    public bool IsLocked { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
