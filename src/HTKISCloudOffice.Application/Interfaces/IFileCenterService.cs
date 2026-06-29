using HTKISCloudOffice.Application.DTOs;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IFileCenterService
{
    Task<List<FileDriveDto>> GetDrivesForUserAsync(string user_id);
    Task<FileListResult> ListFilesAsync(string user_id, string drive_id, string? path);
    Task<FileUploadResult> UploadFileAsync(string user_id, string drive_id, string path, string file_name, Stream file_stream, long file_size, string ip_address);
    Task<FileDownloadResult> DownloadFileAsync(string user_id, string drive_id, string file_path);
    Task<FilePreviewResult> PreviewFileAsync(string user_id, string drive_id, string file_path);
    Task<FileOperationResult> DeleteFileAsync(string user_id, string drive_id, string file_path, string ip_address);
    Task<DirectoryCreateResult> CreateDirectoryAsync(string user_id, string drive_id, string path, string dir_name);
}