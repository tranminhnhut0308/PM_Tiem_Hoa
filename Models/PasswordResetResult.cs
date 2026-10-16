namespace ShopHoa.Models;

public sealed class PasswordResetResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NewPassword { get; set; }
}
