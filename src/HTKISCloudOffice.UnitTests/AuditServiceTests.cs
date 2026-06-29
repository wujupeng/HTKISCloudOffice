using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class AuditServiceTests
{
    private readonly Mock<IAuditLogRepository> _audit_repo;
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        _audit_repo = new Mock<IAuditLogRepository>();
        _service = new AuditService(_audit_repo.Object);
    }

    [Fact]
    public async Task LogAsync_WritesAuditLog()
    {
        var entry = new AuditLogEntry
        {
            user_id = Guid.NewGuid(),
            action = AuditAction.Login,
            resource_type = "user",
            resource_id = Guid.NewGuid(),
            ip_address = "127.0.0.1"
        };

        await _service.LogAsync(entry);

        _audit_repo.Verify(r => r.LogAsync(It.Is<AuditLog>(l =>
            l.user_id == entry.user_id &&
            l.action == AuditAction.Login &&
            l.resource_type == "user" &&
            l.ip_address == "127.0.0.1")), Times.Once);
    }

    [Fact]
    public async Task LogLoginAsync_WritesLoginAudit()
    {
        var user_id = Guid.NewGuid();

        await _service.LogLoginAsync(user_id, "192.168.1.1", true);

        _audit_repo.Verify(r => r.LogAsync(It.Is<AuditLog>(l =>
            l.user_id == user_id &&
            l.action == AuditAction.Login &&
            l.ip_address == "192.168.1.1")), Times.Once);
    }

    [Fact]
    public async Task LogPermissionChangeAsync_WritesPermissionAudit()
    {
        var operator_id = Guid.NewGuid().ToString();
        var target_id = Guid.NewGuid().ToString();

        await _service.LogPermissionChangeAsync(operator_id, target_id, "Assigned roles", "10.0.0.1");

        _audit_repo.Verify(r => r.LogAsync(It.Is<AuditLog>(l =>
            l.action == AuditAction.PermissionChange &&
            l.detail == "Assigned roles" &&
            l.ip_address == "10.0.0.1")), Times.Once);
    }

    [Fact]
    public async Task LogAppLaunchAsync_WritesAppLaunchAudit()
    {
        var user_id = Guid.NewGuid();
        var app_id = Guid.NewGuid();

        await _service.LogAppLaunchAsync(user_id, app_id, "10.0.0.2");

        _audit_repo.Verify(r => r.LogAsync(It.Is<AuditLog>(l =>
            l.user_id == user_id &&
            l.action == AuditAction.AppLaunch &&
            l.resource_type == "application" &&
            l.resource_id == app_id)), Times.Once);
    }

    [Fact]
    public async Task LogFileDeleteAsync_WritesFileDeleteAudit()
    {
        var user_id = Guid.NewGuid();
        var drive_id = Guid.NewGuid();

        await _service.LogFileDeleteAsync(user_id, drive_id, "report.xlsx", "10.0.0.3");

        _audit_repo.Verify(r => r.LogAsync(It.Is<AuditLog>(l =>
            l.user_id == user_id &&
            l.action == AuditAction.FileDelete &&
            l.resource_type == "shared_drive" &&
            l.resource_id == drive_id)), Times.Once);
    }

    [Fact]
    public async Task QueryLogsAsync_ReturnsPagedResult()
    {
        var logs = new List<AuditLog>
        {
            new()
            {
                log_id = Guid.NewGuid(),
                user_id = Guid.NewGuid(),
                action = AuditAction.Login,
                resource_type = "user",
                ip_address = "127.0.0.1",
                created_at = DateTime.UtcNow
            }
        };

        _audit_repo.Setup(r => r.QueryAsync(null, null, null, null, 1, 50))
            .ReturnsAsync((logs, 1));

        var result = await _service.QueryLogsAsync(new AuditLogFilter());

        Assert.Single(result.items);
        Assert.Equal(1, result.total);
        Assert.Equal(AuditAction.Login, result.items[0].action);
    }

    [Fact]
    public async Task QueryLogsAsync_WithFilters_PassesFiltersToRepo()
    {
        var user_id = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        _audit_repo.Setup(r => r.QueryAsync(user_id, AuditAction.Login, start, end, 1, 20))
            .ReturnsAsync((new List<AuditLog>(), 0));

        var result = await _service.QueryLogsAsync(new AuditLogFilter
        {
            user_id = user_id,
            action = AuditAction.Login,
            start_time = start,
            end_time = end,
            page = 1,
            page_size = 20
        });

        Assert.Empty(result.items);
        Assert.Equal(0, result.total);
    }
}