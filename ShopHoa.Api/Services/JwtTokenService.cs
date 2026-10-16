using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShopHoa.Api.Models;

namespace ShopHoa.Api.Services;

public interface IJwtTokenService
{
    (string Token, DateTime Expiration) GenerateToken(UserDto user, IEnumerable<string> permissions);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime Expiration) GenerateToken(UserDto user, IEnumerable<string> permissions)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["Key"]
            ?? throw new InvalidOperationException("Cấu hình Jwt:Key không tồn tại.");
        var issuer = jwtSettings["Issuer"] ?? "ShopHoaApi";
        var audience = jwtSettings["Audience"] ?? "ShopHoaApp";
        var expireHours = int.TryParse(jwtSettings["ExpireHours"], out var hours) ? hours : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiration = DateTime.UtcNow.AddHours(expireHours);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.TenDangNhap),
            new(ClaimTypes.GivenName, user.HoTen),
            new("MaNguoiDung", user.MaNguoiDung)
        };

        if (!string.IsNullOrWhiteSpace(user.MaVaiTro))
        {
            foreach (var role in user.MaVaiTro.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        foreach (var perm in permissions)
        {
            if (!string.IsNullOrWhiteSpace(perm))
            {
                claims.Add(new Claim("Permission", perm.Trim()));
            }
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }
}
