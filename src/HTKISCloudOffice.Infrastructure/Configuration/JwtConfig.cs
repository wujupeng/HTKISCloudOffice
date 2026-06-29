namespace HTKISCloudOffice.Infrastructure.Configuration;

public class JwtConfig
{
    public string secret_key { get; init; } = string.Empty;
    public string issuer { get; init; } = string.Empty;
    public string audience { get; init; } = string.Empty;
    public int token_expiration_hours { get; init; } = 8;
    public int auto_login_token_expiration_days { get; init; } = 30;
}