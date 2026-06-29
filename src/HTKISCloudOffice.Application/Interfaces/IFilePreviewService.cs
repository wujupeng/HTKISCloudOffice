using HTKISCloudOffice.Application.DTOs;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IFilePreviewService
{
    Task<FilePreviewResult> PreviewAsync(string file_path, string content_type);
}