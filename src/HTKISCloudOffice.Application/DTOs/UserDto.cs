namespace HTKISCloudOffice.Application.DTOs;

public class UserDto
{
    public Guid user_id { get; init; }
    public string username { get; init; } = string.Empty;
    public string display_name { get; init; } = string.Empty;
    public string? department { get; init; }
    public bool is_active { get; init; }
    public List<string> roles { get; init; } = new();
    public string? bound_vm_id { get; init; }
}