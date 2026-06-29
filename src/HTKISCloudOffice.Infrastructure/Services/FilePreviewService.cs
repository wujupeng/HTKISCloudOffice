using System.Security.Cryptography;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Infrastructure.Services;

public class FilePreviewService : IFilePreviewService
{
    private readonly FilePreviewConfig _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FilePreviewService> _logger;
    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase) { ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".csv", ".json", ".xml", ".log", ".md", ".ini", ".conf", ".yaml", ".yml" };

    public FilePreviewService(FilePreviewConfig config, IMemoryCache cache, ILogger<FilePreviewService> logger)
    {
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public async Task<FilePreviewResult> PreviewAsync(string file_path, string content_type)
    {
        var ext = Path.GetExtension(file_path);
        var file_info = new FileInfo(file_path);

        if (!file_info.Exists)
            return FilePreviewResult.Fail("FILE_NOT_FOUND", "File not found");

        var cache_key = GetCacheKey(file_path, file_info.LastWriteTimeUtc);

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await GetCachedOrLoadPdfAsync(cache_key, file_path);
        }

        if (OfficeExtensions.Contains(ext))
        {
            return await ConvertOfficeToPdfAsync(cache_key, file_path);
        }

        if (ImageExtensions.Contains(ext))
        {
            return await GetImageAsync(file_path, content_type);
        }

        if (TextExtensions.Contains(ext))
        {
            return await GetTextAsync(file_path);
        }

        return FilePreviewResult.NotSupported();
    }

    private async Task<FilePreviewResult> GetCachedOrLoadPdfAsync(string cache_key, string file_path)
    {
        if (_cache.TryGetValue(cache_key, out string? cached_path) && cached_path != null && File.Exists(cached_path))
        {
            var stream = new MemoryStream(await File.ReadAllBytesAsync(cached_path));
            return FilePreviewResult.Pdf(stream);
        }

        var pdf_stream = new MemoryStream(await File.ReadAllBytesAsync(file_path));
        return FilePreviewResult.Pdf(pdf_stream);
    }

    private async Task<FilePreviewResult> ConvertOfficeToPdfAsync(string cache_key, string file_path)
    {
        if (_cache.TryGetValue(cache_key, out string? cached_path) && cached_path != null && File.Exists(cached_path))
        {
            var stream = new MemoryStream(await File.ReadAllBytesAsync(cached_path));
            return FilePreviewResult.Pdf(stream);
        }

        try
        {
            if (!Directory.Exists(_config.temp_dir))
                Directory.CreateDirectory(_config.temp_dir);

            var start_info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _config.libreoffice_path,
                Arguments = $"--headless --convert-to pdf --outdir {_config.temp_dir} {file_path}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(start_info);
            if (process == null)
                return FilePreviewResult.Fail("CONVERSION_FAILED", "Failed to start LibreOffice");

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("LibreOffice conversion failed: {Error}", error);
                return FilePreviewResult.Fail("CONVERSION_FAILED", "LibreOffice conversion failed");
            }

            var pdf_name = Path.GetFileNameWithoutExtension(file_path) + ".pdf";
            var pdf_path = Path.Combine(_config.temp_dir, pdf_name);

            if (!File.Exists(pdf_path))
                return FilePreviewResult.Fail("CONVERSION_FAILED", "Converted PDF not found");

            _cache.Set(cache_key, pdf_path, TimeSpan.FromMinutes(_config.cache_ttl_minutes));

            var stream = new MemoryStream(await File.ReadAllBytesAsync(pdf_path));
            return FilePreviewResult.Pdf(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LibreOffice conversion error");
            return FilePreviewResult.Fail("CONVERSION_ERROR", "LibreOffice is not available or conversion failed");
        }
    }

    private async Task<FilePreviewResult> GetImageAsync(string file_path, string content_type)
    {
        var stream = new MemoryStream(await File.ReadAllBytesAsync(file_path));
        return FilePreviewResult.Image(stream, content_type);
    }

    private async Task<FilePreviewResult> GetTextAsync(string file_path)
    {
        try
        {
            var text = await File.ReadAllTextAsync(file_path);
            return FilePreviewResult.Text(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text file: {Path}", file_path);
            return FilePreviewResult.Fail("READ_FAILED", "Failed to read text file");
        }
    }

    private static string GetCacheKey(string file_path, DateTime last_modified)
    {
        var raw = $"{file_path}:{last_modified:O}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}