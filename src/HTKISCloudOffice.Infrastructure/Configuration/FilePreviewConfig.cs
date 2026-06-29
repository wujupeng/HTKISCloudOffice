namespace HTKISCloudOffice.Infrastructure.Configuration;

public class FilePreviewConfig
{
    public string libreoffice_path { get; init; } = "libreoffice";
    public string temp_dir { get; init; } = "/tmp/file-preview";
    public int cache_ttl_minutes { get; init; } = 60;
}