namespace HTKISCloudOffice.Domain.Entities;

public class UserRole
{
    public Guid user_id { get; set; }
    public Guid role_id { get; set; }
    public DateTime assigned_at { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}