namespace HTKISCloudOffice.Application.DTOs;

public class FileItemDto
{
    public string name { get; set; } = string.Empty;
    public string path { get; set; } = string.Empty;
    public bool is_directory { get; set; }
    public long size { get; set; }
    public DateTime last_modified { get; set; }
    public string extension { get; set; } = string.Empty;
    public string content_type { get; set; } = string.Empty;
}