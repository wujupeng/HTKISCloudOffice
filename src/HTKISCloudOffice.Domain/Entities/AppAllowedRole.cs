namespace HTKISCloudOffice.Domain.Entities;

public class AppAllowedRole
{
    public Guid app_id { get; set; }
    public Guid role_id { get; set; }

    public Application Application { get; set; } = null!;
    public Role Role { get; set; } = null!;
}