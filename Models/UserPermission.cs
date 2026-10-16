namespace ShopHoa.Models;

public sealed class UserPermission
{
    public uint Id { get; set; }
    public uint NguoiDungId { get; set; }
    public uint QuyenId { get; set; }
    public bool ChoPhep { get; set; } = true;
    public uint? NguoiCapNhatId { get; set; }
    public DateTime? NgayCapNhat { get; set; }
}
