namespace ShopHoa.Models;

public sealed class RolePermission
{
    public uint Id { get; set; }
    public uint VaiTroId { get; set; }
    public uint QuyenId { get; set; }
    public uint? NguoiCapNhatId { get; set; }
    public DateTime? NgayCapNhat { get; set; }
}
