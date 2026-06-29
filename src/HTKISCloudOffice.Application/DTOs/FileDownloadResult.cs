namespace HTKISCloudOffice.Application.DTOs;

public class FileDownloadResult
{
    public bool success { get; init; }
    public Stream? content_stream { get; init; }
    public string file_name { get; init; } = string.Empty;
    public string content_type { get; init; } = string.Empty;
    public long size { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static FileDownloadResult Ok(Stream stream, string file_name, string content_type, long size) => new()
    {
        success = true,
        content_stream = stream,
        file_name = file_name,
        content_type = content_type,
        size = size
    };

    public static FileDownloadResult Fail(string error_code, string error_message) => new()
    {
        success = false,
        error_code = error_code,
        error_message = error_message
    };
}