namespace HTKISCloudOffice.Domain.Entities;

public class RefreshToken
{
    public Guid token_id { get; set; } = Guid.NewGuid();
    public Guid user_id { get; set; }
    public string token_hash { get; set; } = string.Empty;
    public DateTime expires_at { get; set; }
    public bool is_revoked { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}