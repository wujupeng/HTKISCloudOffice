namespace HTKISCloudOffice.Domain.Entities;

public class AppFavorite
{
    public Guid favorite_id { get; set; } = Guid.NewGuid();
    public Guid user_id { get; set; } = Guid.Empty;
    public Guid app_id { get; set; } = Guid.Empty;
    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Application Application { get; set; } = null!;
}