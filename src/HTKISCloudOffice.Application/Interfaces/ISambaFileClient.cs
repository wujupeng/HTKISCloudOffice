namespace HTKISCloudOffice.Application.Interfaces;

public interface ISambaFileClient
{
    Task<List<SambaFileInfo>> ListDirectoryAsync(string relative_path);
    Task<SambaFileInfo> UploadFileAsync(string relative_path, string file_name, Stream file_stream);
    Task<Stream> DownloadFileAsync(string relative_path);
    Task DeleteFileAsync(string relative_path);
    Task<SambaFileInfo> CreateDirectoryAsync(string parent_path, string dir_name);
    Task<SambaFileInfo?> GetFileInfoAsync(string relative_path);
}

public class SambaFileInfo
{
    public string name { get; set; } = string.Empty;
    public string path { get; set; } = string.Empty;
    public bool is_directory { get; set; }
    public long size { get; set; }
    public DateTime last_modified { get; set; }
}