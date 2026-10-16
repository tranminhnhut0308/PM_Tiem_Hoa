namespace ShopHoa.Models;

public sealed class ResetPasswordDto
{
    public uint UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
    public uint ActorId { get; set; }
}
