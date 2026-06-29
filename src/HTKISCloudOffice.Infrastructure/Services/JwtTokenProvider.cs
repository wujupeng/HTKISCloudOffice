using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HTKISCloudOffice.Infrastructure.Services;

public class JwtTokenProvider : IJwtTokenProvider
{
    private readonly JwtConfig _config;

    public JwtTokenProvider(JwtConfig config)
    {
        _config = config;
    }

    public string GenerateToken(Guid user_id, string username, List<string> roles, int? expiration_hours = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.secret_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user_id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new("roles", string.Join(",", roles)),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var expires = DateTime.UtcNow.AddHours(expiration_hours ?? _config.token_expiration_hours);

        var token = new JwtSecurityToken(
            issuer: _config.issuer,
            audience: _config.audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateAutoLoginToken(Guid user_id, string device_id)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.secret_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user_id.ToString()),
            new("device_id", device_id),
            new("token_type", "auto_login"),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var expires = DateTime.UtcNow.AddDays(_config.auto_login_token_expiration_days);

        var token = new JwtSecurityToken(
            issuer: _config.issuer,
            audience: _config.audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.secret_key));
        var handler = new JwtSecurityTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _config.issuer,
            ValidAudience = _config.audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        return handler.ValidateToken(token, parameters, out _);
    }

    public DateTime GetTokenExpiration(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.ValidTo;
    }
}