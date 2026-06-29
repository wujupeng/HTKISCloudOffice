namespace HTKISCloudOffice.Application.DTOs;

public class TokenRefreshResult
{
    public bool success { get; init; }
    public string token { get; init; } = string.Empty;
    public DateTime token_expires_at { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static TokenRefreshResult Ok(string token, DateTime expires_at) => new()
    {
        success = true, token = token, token_expires_at = expires_at
    };

    public static TokenRefreshResult Fail(string error_code, string error_message) => new()
    {
        success = false, error_code = error_code, error_message = error_message
    };
}