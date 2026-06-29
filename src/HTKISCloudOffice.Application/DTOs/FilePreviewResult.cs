namespace HTKISCloudOffice.Application.DTOs;

public class FilePreviewResult
{
    public bool success { get; init; }
    public string preview_type { get; init; } = string.Empty;
    public Stream? content_stream { get; init; }
    public string? text_content { get; init; }
    public string? content_type { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static FilePreviewResult Pdf(Stream stream) => new()
    {
        success = true,
        preview_type = "pdf",
        content_stream = stream,
        content_type = "application/pdf"
    };

    public static FilePreviewResult Image(Stream stream, string content_type) => new()
    {
        success = true,
        preview_type = "image",
        content_stream = stream,
        content_type = content_type
    };

    public static FilePreviewResult Text(string text) => new()
    {
        success = true,
        preview_type = "text",
        text_content = text,
        content_type = "text/plain"
    };

    public static FilePreviewResult NotSupported() => new()
    {
        success = false,
        preview_type = "none",
        error_code = "PREVIEW_NOT_SUPPORTED",
        error_message = "This file format is not supported for preview"
    };

    public static FilePreviewResult Fail(string error_code, string error_message) => new()
    {
        success = false,
        preview_type = "none",
        error_code = error_code,
        error_message = error_message
    };
}