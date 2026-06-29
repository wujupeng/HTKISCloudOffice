using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/users").RequireAuthorization("SuperAdmin");

        admin.MapGet("/", async (int page, int page_size, string? department, bool? is_active, IUserRepository user_repo) =>
        {
            var (users, total) = await user_repo.ListAsync(page, page_size, department, is_active);
            var dtos = users.Select(u => new UserDto
            {
                user_id = u.user_id,
                username = u.username,
                display_name = u.display_name,
                department = u.department,
                is_active = u.is_active,
                roles = u.user_roles.Select(ur => ur.Role.role_name).ToList(),
                bound_vm_id = u.bound_vm_id
            }).ToList();
            return Results.Ok(ApiResponse<object>.Ok(new { users = dtos, total, page, page_size }));
        });

        admin.MapPost("/", async ([FromBody] CreateUserRequest request, IUserRepository user_repo) =>
        {
            var user = new Domain.Entities.User
            {
                username = request.username,
                password_hash = HashPassword(request.password),
                display_name = request.display_name,
                department = request.department
            };
            await user_repo.CreateAsync(user);
            return Results.Ok(ApiResponse<object>.Ok(new { user_id = user.user_id }));
        });

        admin.MapPut("/{user_id:guid}/vm", async (Guid user_id, [FromBody] BindVmRequest request, IUserRepository user_repo) =>
        {
            var user = await user_repo.GetByIdAsync(user_id);
            if (user == null) return Results.NotFound();
            user.bound_vm_id = request.vm_id;
            await user_repo.UpdateAsync(user);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });
    }

    private static string HashPassword(string password)
    {
        using var argon2 = new Konscious.Security.Cryptography.Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16),
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        return Convert.ToBase64String(argon2.GetBytes(32));
    }
}

public record CreateUserRequest(string username, string password, string display_name, string? department);
public record BindVmRequest(string? vm_id);