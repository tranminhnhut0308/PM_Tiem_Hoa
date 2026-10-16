namespace ShopHoa.Models;

public sealed class LoginResult
{
    public NguoiDung? User { get; set; }
    public bool IsLocked { get; set; }
    public bool IsValid { get; set; }
}
