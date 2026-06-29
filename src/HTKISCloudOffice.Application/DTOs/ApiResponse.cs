namespace HTKISCloudOffice.Application.DTOs;

public class ApiResponse<T>
{
    public bool success { get; init; }
    public T? data { get; init; }
    public string? error_code { get; init; }
    public string? error_message { get; init; }
    public string request_id { get; init; } = Guid.NewGuid().ToString();
    public DateTime timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data) => new() { success = true, data = data };

    public static ApiResponse<T> Fail(string error_code, string error_message) => new()
    {
        success = false,
        error_code = error_code,
        error_message = error_message
    };
}