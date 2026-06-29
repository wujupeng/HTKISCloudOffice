using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Clients;
using HTKISCloudOffice.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class SambaFileClientTests
{
    private readonly SambaFileConfig _config;
    private readonly Mock<ILogger<SambaFileClient>> _logger;
    private readonly SambaFileClient _client;
    private readonly string _test_root;

    public SambaFileClientTests()
    {
        _test_root = Path.Combine(Path.GetTempPath(), $"samba-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_test_root);
        _config = new SambaFileConfig { mount_root = _test_root };
        _logger = new Mock<ILogger<SambaFileClient>>();
        _client = new SambaFileClient(_config, _logger.Object);
    }

    [Fact]
    public async Task ListDirectoryAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.ListDirectoryAsync("../../etc"));
    }

    [Fact]
    public async Task ListDirectoryAsync_EncodedPathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.ListDirectoryAsync("%2e%2e/etc"));
    }

    [Fact]
    public async Task ListDirectoryAsync_DoubleEncodedPathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.ListDirectoryAsync("%252e%252e/etc"));
    }

    [Fact]
    public async Task UploadFileAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.UploadFileAsync("../../etc", "test.txt", stream));
    }

    [Fact]
    public async Task DeleteFileAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.DeleteFileAsync("../../etc/passwd"));
    }

    [Fact]
    public async Task CreateDirectoryAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.CreateDirectoryAsync("../../etc", "evil"));
    }

    [Fact]
    public async Task DownloadFileAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _client.DownloadFileAsync("../../etc/passwd"));
    }

    [Fact]
    public async Task ListDirectoryAsync_NormalPath_ReturnsEntries()
    {
        Directory.CreateDirectory(Path.Combine(_test_root, "testdir"));
        File.WriteAllText(Path.Combine(_test_root, "testfile.txt"), "hello");

        var result = await _client.ListDirectoryAsync("");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.name == "testdir" && f.is_directory);
        Assert.Contains(result, f => f.name == "testfile.txt" && !f.is_directory);
    }

    [Fact]
    public async Task UploadFileAsync_SanitizesFileName()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test"));
        var result = await _client.UploadFileAsync("", "file:name?.txt", stream);

        Assert.Equal("file_name_.txt", result.name);
        Assert.True(File.Exists(Path.Combine(_test_root, "file_name_.txt")));
    }

    [Fact]
    public async Task UploadFileAsync_NormalUpload_ReturnsFileInfo()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello world"));
        stream.Position = 0;
        var result = await _client.UploadFileAsync("", "test.txt", stream);

        Assert.Equal("test.txt", result.name);
        Assert.False(result.is_directory);
        Assert.Equal(11, result.size);
    }

    [Fact]
    public async Task DownloadFileAsync_NormalDownload_ReturnsStream()
    {
        File.WriteAllText(Path.Combine(_test_root, "dltest.txt"), "download me");

        var stream = await _client.DownloadFileAsync("dltest.txt");

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("download me", content);
    }

    [Fact]
    public async Task DeleteFileAsync_NormalDelete_RemovesFile()
    {
        var path = Path.Combine(_test_root, "delme.txt");
        File.WriteAllText(path, "delete me");

        await _client.DeleteFileAsync("delme.txt");

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task CreateDirectoryAsync_NormalCreate_CreatesDir()
    {
        var result = await _client.CreateDirectoryAsync("", "newdir");

        Assert.Equal("newdir", result.name);
        Assert.True(result.is_directory);
        Assert.True(Directory.Exists(Path.Combine(_test_root, "newdir")));
    }

    [Fact]
    public async Task GetFileInfoAsync_File_ReturnsFileInfo()
    {
        File.WriteAllText(Path.Combine(_test_root, "info.txt"), "info");

        var result = await _client.GetFileInfoAsync("info.txt");

        Assert.NotNull(result);
        Assert.Equal("info.txt", result!.name);
        Assert.False(result.is_directory);
    }

    [Fact]
    public async Task GetFileInfoAsync_NonExistent_ReturnsNull()
    {
        var result = await _client.GetFileInfoAsync("nonexistent.txt");
        Assert.Null(result);
    }
}