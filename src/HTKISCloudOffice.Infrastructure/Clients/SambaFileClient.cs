using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Infrastructure.Clients;

public class SambaFileClient : ISambaFileClient
{
    private readonly SambaFileConfig _config;
    private readonly ILogger<SambaFileClient> _logger;
    private static readonly char[] InvalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    public SambaFileClient(SambaFileConfig config, ILogger<SambaFileClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<List<SambaFileInfo>> ListDirectoryAsync(string relative_path)
    {
        ValidatePath(relative_path);
        var full_path = GetFullPath(relative_path);

        if (!Directory.Exists(full_path))
            throw new DirectoryNotFoundException($"Directory not found: {relative_path}");

        var result = new List<SambaFileInfo>();
        var dir_info = new DirectoryInfo(full_path);

        foreach (var dir in dir_info.GetDirectories())
        {
            result.Add(new SambaFileInfo
            {
                name = dir.Name,
                path = GetRelativePath(dir.FullName),
                is_directory = true,
                size = 0,
                last_modified = dir.LastWriteTimeUtc
            });
        }

        foreach (var file in dir_info.GetFiles())
        {
            result.Add(new SambaFileInfo
            {
                name = file.Name,
                path = GetRelativePath(file.FullName),
                is_directory = false,
                size = file.Length,
                last_modified = file.LastWriteTimeUtc
            });
        }

        return await Task.FromResult(result);
    }

    public async Task<SambaFileInfo> UploadFileAsync(string relative_path, string file_name, Stream file_stream)
    {
        ValidatePath(relative_path);
        file_name = SanitizeFileName(file_name);
        var full_path = GetFullPath(relative_path);

        if (!Directory.Exists(full_path))
            Directory.CreateDirectory(full_path);

        var file_path = Path.Combine(full_path, file_name);

        using (var fs = new FileStream(file_path, FileMode.Create, FileAccess.Write))
        {
            await file_stream.CopyToAsync(fs);
            await fs.FlushAsync();
        }

        var file_info = new FileInfo(file_path);
        return new SambaFileInfo
        {
            name = file_info.Name,
            path = GetRelativePath(file_info.FullName),
            is_directory = false,
            size = file_info.Length,
            last_modified = file_info.LastWriteTimeUtc
        };
    }

    public async Task<Stream> DownloadFileAsync(string relative_path)
    {
        ValidatePath(relative_path);
        var full_path = GetFullPath(relative_path);

        if (!File.Exists(full_path))
            throw new FileNotFoundException($"File not found: {relative_path}");

        var memory_stream = new MemoryStream();
        using var file_stream = new FileStream(full_path, FileMode.Open, FileAccess.Read);
        await file_stream.CopyToAsync(memory_stream);
        memory_stream.Position = 0;
        return memory_stream;
    }

    public async Task DeleteFileAsync(string relative_path)
    {
        ValidatePath(relative_path);
        var full_path = GetFullPath(relative_path);

        if (File.Exists(full_path))
        {
            await Task.Run(() => File.Delete(full_path));
            return;
        }

        if (Directory.Exists(full_path))
        {
            if (Directory.EnumerateFileSystemEntries(full_path).Any())
                throw new InvalidOperationException("Cannot delete non-empty directory");

            await Task.Run(() => Directory.Delete(full_path));
            return;
        }

        throw new FileNotFoundException($"File or directory not found: {relative_path}");
    }

    public async Task<SambaFileInfo> CreateDirectoryAsync(string parent_path, string dir_name)
    {
        ValidatePath(parent_path);
        dir_name = SanitizeFileName(dir_name);
        var full_path = Path.Combine(GetFullPath(parent_path), dir_name);

        if (Directory.Exists(full_path))
            throw new InvalidOperationException($"Directory already exists: {dir_name}");

        Directory.CreateDirectory(full_path);
        var dir_info = new DirectoryInfo(full_path);

        return await Task.FromResult(new SambaFileInfo
        {
            name = dir_info.Name,
            path = GetRelativePath(dir_info.FullName),
            is_directory = true,
            size = 0,
            last_modified = dir_info.LastWriteTimeUtc
        });
    }

    public async Task<SambaFileInfo?> GetFileInfoAsync(string relative_path)
    {
        ValidatePath(relative_path);
        var full_path = GetFullPath(relative_path);

        if (File.Exists(full_path))
        {
            var file_info = new FileInfo(full_path);
            return new SambaFileInfo
            {
                name = file_info.Name,
                path = GetRelativePath(file_info.FullName),
                is_directory = false,
                size = file_info.Length,
                last_modified = file_info.LastWriteTimeUtc
            };
        }

        if (Directory.Exists(full_path))
        {
            var dir_info = new DirectoryInfo(full_path);
            return new SambaFileInfo
            {
                name = dir_info.Name,
                path = GetRelativePath(dir_info.FullName),
                is_directory = true,
                size = 0,
                last_modified = dir_info.LastWriteTimeUtc
            };
        }

        return await Task.FromResult<SambaFileInfo?>(null);
    }

    private string GetFullPath(string relative_path)
    {
        var normalized = relative_path.TrimStart('/', '\\');
        return Path.GetFullPath(Path.Combine(_config.mount_root, normalized));
    }

    private string GetRelativePath(string full_path)
    {
        var root = Path.GetFullPath(_config.mount_root);
        if (full_path.StartsWith(root))
            return full_path[root.Length..].TrimStart('/', '\\');
        return full_path;
    }

    private void ValidatePath(string path)
    {
        if (path.Contains(".."))
            throw new UnauthorizedAccessException("Path traversal detected");

        var decoded = path;
        string prev;
        do
        {
            prev = decoded;
            decoded = Uri.UnescapeDataString(decoded);
        } while (decoded != prev);

        if (decoded.Contains(".."))
            throw new UnauthorizedAccessException("Path traversal detected");

        var full_check = Path.GetFullPath(Path.Combine(_config.mount_root, decoded.TrimStart('/', '\\')));
        var root = Path.GetFullPath(_config.mount_root);
        if (!full_check.StartsWith(root))
            throw new UnauthorizedAccessException("Path traversal detected");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in InvalidChars)
            name = name.Replace(c, '_');
        return name.Trim();
    }
}