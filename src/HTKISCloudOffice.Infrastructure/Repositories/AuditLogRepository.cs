using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(AuditLog entry)
    {
        _context.audit_logs.Add(entry);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<AuditLog> items, int total)> QueryAsync(
        Guid? user_id = null, AuditAction? action = null,
        DateTime? start_time = null, DateTime? end_time = null,
        int page = 1, int page_size = 50)
    {
        var query = _context.audit_logs.AsQueryable();

        if (user_id.HasValue) query = query.Where(l => l.user_id == user_id.Value);
        if (action.HasValue) query = query.Where(l => l.action == action.Value);
        if (start_time.HasValue) query = query.Where(l => l.created_at >= start_time.Value);
        if (end_time.HasValue) query = query.Where(l => l.created_at <= end_time.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.created_at)
            .Skip((page - 1) * page_size)
            .Take(page_size)
            .ToListAsync();

        return (items, total);
    }
}
