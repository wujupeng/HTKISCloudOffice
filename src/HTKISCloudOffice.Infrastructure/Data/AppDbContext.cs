using System.Text.Json;
using AppEntity = HTKISCloudOffice.Domain.Entities.Application;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> users => Set<User>();
    public DbSet<Role> roles => Set<Role>();
    public DbSet<UserRole> user_roles => Set<UserRole>();
    public DbSet<AppEntity> applications => Set<AppEntity>();
    public DbSet<AppAllowedRole> app_allowed_roles => Set<AppAllowedRole>();
    public DbSet<SharedDrive> shared_drives => Set<SharedDrive>();
    public DbSet<VmConfig> vm_configs => Set<VmConfig>();
    public DbSet<AuditLog> audit_logs => Set<AuditLog>();
    public DbSet<RefreshToken> refresh_tokens => Set<RefreshToken>();
    public DbSet<AppFavorite> app_favorites => Set<AppFavorite>();
    public DbSet<AppIcon> app_icons => Set<AppIcon>();
    public DbSet<DeviceBinding> device_bindings => Set<DeviceBinding>();
    public DbSet<ConnectionConfig> connection_configs => Set<ConnectionConfig>();
    public DbSet<ConnectionAllowedRole> connection_allowed_roles => Set<ConnectionAllowedRole>();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isRelational = Database.IsRelational();

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.user_id);
            entity.HasIndex(e => e.username).IsUnique();
            entity.HasIndex(e => e.department);
            entity.HasIndex(e => e.bound_vm_id);
            entity.HasIndex(e => e.is_active);
            entity.Property(e => e.username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.password_hash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.display_name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.department).HasMaxLength(100);
            entity.Property(e => e.bound_vm_id).HasMaxLength(50);
            entity.HasOne(e => e.BoundVm)
                .WithMany()
                .HasForeignKey(e => e.bound_vm_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.role_id);
            entity.HasIndex(e => e.role_name).IsUnique();
            entity.Property(e => e.role_name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.description).HasMaxLength(500);
            if (isRelational)
            {
                entity.Property(e => e.permissions)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, _jsonOptions),
                        v => JsonSerializer.Deserialize<List<PermissionEntry>>(v, _jsonOptions) ?? new());
            }
            else
            {
                entity.OwnsMany(e => e.permissions, pe => { });
            }
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.role_id });
            entity.HasIndex(e => e.role_id);
            entity.HasOne(e => e.User).WithMany(u => u.user_roles).HasForeignKey(e => e.user_id);
            entity.HasOne(e => e.Role).WithMany(r => r.user_roles).HasForeignKey(e => e.role_id);
        });

        modelBuilder.Entity<AppEntity>(entity =>
        {
            entity.HasKey(e => e.app_id);
            entity.HasIndex(e => e.category);
            entity.HasIndex(e => e.is_active);
            entity.HasIndex(e => e.sort_order);
            entity.Property(e => e.app_name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.description).HasMaxLength(500);
            if (isRelational)
            {
                entity.Property(e => e.app_type).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.category).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.launch_params).HasColumnType("jsonb").IsRequired();
            }
        });

        modelBuilder.Entity<AppAllowedRole>(entity =>
        {
            entity.HasKey(e => new { e.app_id, e.role_id });
            entity.HasOne(e => e.Application).WithMany(a => a.app_allowed_roles).HasForeignKey(e => e.app_id);
            entity.HasOne(e => e.Role).WithMany(r => r.app_allowed_roles).HasForeignKey(e => e.role_id);
        });

        modelBuilder.Entity<SharedDrive>(entity =>
        {
            entity.HasKey(e => e.drive_id);
            entity.HasIndex(e => e.drive_type);
            entity.HasIndex(e => e.drive_letter);
            entity.Property(e => e.drive_name).HasMaxLength(100).IsRequired();
            if (isRelational)
            {
                entity.Property(e => e.drive_type).HasConversion<string>().HasMaxLength(20);
            }
            entity.Property(e => e.samba_path).HasMaxLength(500).IsRequired();
            entity.Property(e => e.drive_letter).HasMaxLength(1).IsRequired();
            if (isRelational)
            {
                entity.Property(e => e.allowed_permissions)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, _jsonOptions),
                        v => JsonSerializer.Deserialize<List<DrivePermission>>(v, _jsonOptions) ?? new());
            }
            else
            {
                entity.OwnsMany(e => e.allowed_permissions, dp => { });
            }
        });

        modelBuilder.Entity<VmConfig>(entity =>
        {
            entity.HasKey(e => e.vm_id);
            entity.HasIndex(e => e.is_active);
            entity.Property(e => e.vm_id).HasMaxLength(50);
            entity.Property(e => e.vm_name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.hostname).HasMaxLength(255).IsRequired();
            entity.Property(e => e.rdp_username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.rdp_password_encrypted).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.log_id);
            entity.HasIndex(e => e.user_id);
            entity.HasIndex(e => e.action);
            entity.HasIndex(e => e.created_at);
            entity.HasIndex(e => new { e.resource_type, e.resource_id });
            if (isRelational)
            {
                entity.Property(e => e.action).HasConversion<string>().HasMaxLength(30);
            }
            entity.Property(e => e.resource_type).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ip_address).HasMaxLength(45).IsRequired();
            if (isRelational)
            {
                entity.Property(e => e.detail).HasColumnType("jsonb");
            }
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.token_id);
            entity.HasIndex(e => e.token_hash).IsUnique();
            entity.HasIndex(e => e.user_id);
            entity.HasIndex(e => e.expires_at);
            entity.Property(e => e.token_hash).HasMaxLength(255).IsRequired();
            entity.HasOne(e => e.User).WithMany(u => u.refresh_tokens).HasForeignKey(e => e.user_id);
        });

        modelBuilder.Entity<AppFavorite>(entity =>
        {
            entity.HasKey(e => e.favorite_id);
            entity.HasIndex(e => e.user_id);
            entity.HasIndex(e => new { e.user_id, e.app_id }).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.user_id).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Application).WithMany().HasForeignKey(e => e.app_id).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppIcon>(entity =>
        {
            entity.HasKey(e => e.icon_id);
            entity.HasIndex(e => e.icon_name);
            if (isRelational)
            {
                entity.Property(e => e.icon_type).HasConversion<string>().HasMaxLength(10);
            }
            entity.Property(e => e.icon_name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.icon_url).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<DeviceBinding>(entity =>
        {
            entity.HasKey(e => e.binding_id);
            entity.HasIndex(e => e.user_id).HasDatabaseName("idx_device_bindings_user");
            entity.HasIndex(e => e.device_id).HasDatabaseName("idx_device_bindings_device_id");
            entity.HasIndex(e => e.device_token_expires_at).HasDatabaseName("idx_device_bindings_token_expires");
            entity.HasIndex(e => e.is_active).HasDatabaseName("idx_device_bindings_is_active");
            entity.Property(e => e.device_id).HasMaxLength(255).IsRequired();
            entity.Property(e => e.device_name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.device_token).IsRequired();
            entity.Property(e => e.last_login_ip).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.user_id).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConnectionConfig>(entity =>
        {
            entity.HasKey(e => e.connection_id);
            entity.HasIndex(e => e.protocol).HasDatabaseName("idx_conn_configs_protocol");
            entity.HasIndex(e => e.is_active).HasDatabaseName("idx_conn_configs_is_active");
            entity.HasIndex(e => e.sort_order).HasDatabaseName("idx_conn_configs_sort");
            entity.Property(e => e.connection_name).HasMaxLength(100).IsRequired();
            if (isRelational)
            {
                entity.Property(e => e.protocol).HasConversion<string>().HasMaxLength(10);
                entity.Property(e => e.connection_params).HasColumnType("jsonb").HasDefaultValue("{}");
            }
            entity.Property(e => e.hostname).HasMaxLength(255).IsRequired();
            entity.Property(e => e.username).HasMaxLength(100);
            entity.Property(e => e.password_encrypted).HasMaxLength(500);
            entity.Property(e => e.remote_app_path).HasMaxLength(500);
        });

        modelBuilder.Entity<ConnectionAllowedRole>(entity =>
        {
            entity.HasKey(e => new { e.connection_id, e.role_id });
            entity.HasOne(e => e.ConnectionConfig).WithMany(c => c.connection_allowed_roles).HasForeignKey(e => e.connection_id);
            entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.role_id);
        });
    }
}
