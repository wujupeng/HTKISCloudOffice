namespace HTKISCloudOffice.Application.DTOs;

public class AuthResult
{
    public bool success { get; init; }
    public string token { get; init; } = string.Empty;
    public string auto_login_token { get; init; } = string.Empty;
    public DateTime token_expires_at { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;
    public UserDto? user { get; init; }

    public static AuthResult Ok(string token, string auto_login_token, DateTime expires_at, UserDto user) => new()
    {
        success = true, token = token, auto_login_token = auto_login_token,
        token_expires_at = expires_at, user = user
    };

    public static AuthResult Fail(string error_code, string error_message) => new()
    {
        success = false, error_code = error_code, error_message = error_message
    };
}