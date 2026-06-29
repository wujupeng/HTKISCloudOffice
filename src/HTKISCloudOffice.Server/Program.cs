using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Infrastructure.Clients;
using HTKISCloudOffice.Infrastructure.Configuration;
using HTKISCloudOffice.Infrastructure.Data;
using HTKISCloudOffice.Infrastructure.Repositories;
using HTKISCloudOffice.Infrastructure.Services;
using HTKISCloudOffice.Server.Endpoints;
using HTKISCloudOffice.Server.Hubs;
using HTKISCloudOffice.Server.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/htkis-portal-.log", rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("HTKIS Cloud Office Portal starting...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddSignalR();
    builder.Services.AddHttpClient<IGuacamoleApiClient, GuacamoleApiClient>();

    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer(options =>
        {
            var config = builder.Configuration.GetSection("Jwt");
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["issuer"],
                ValidAudience = config["audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(config["secret_key"]!))
            };
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("SuperAdmin", policy =>
            policy.RequireRole("super_admin"));
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddSingleton(builder.Configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig());
    builder.Services.AddSingleton(builder.Configuration.GetSection("Guacamole").Get<GuacamoleConfig>() ?? new GuacamoleConfig());
    builder.Services.AddSingleton(builder.Configuration.GetSection("AesEncryption").Get<AesEncryptionConfig>() ?? new AesEncryptionConfig());
    builder.Services.AddSingleton(builder.Configuration.GetSection("Samba").Get<SambaConfig>() ?? new SambaConfig());
    builder.Services.AddSingleton(builder.Configuration.GetSection("SambaFile").Get<SambaFileConfig>() ?? new SambaFileConfig());
    builder.Services.AddSingleton(builder.Configuration.GetSection("FilePreview").Get<FilePreviewConfig>() ?? new FilePreviewConfig());

    builder.Services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();
    builder.Services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
    builder.Services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IRoleRepository, RoleRepository>();
    builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
    builder.Services.AddScoped<ISharedDriveRepository, SharedDriveRepository>();
    builder.Services.AddScoped<IVmConfigRepository, VmConfigRepository>();
    builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    builder.Services.AddScoped<IAppFavoriteRepository, AppFavoriteRepository>();
    builder.Services.AddScoped<IAppIconRepository, AppIconRepository>();
    builder.Services.AddScoped<IDeviceBindingRepository, DeviceBindingRepository>();
    builder.Services.AddScoped<IConnectionConfigRepository, ConnectionConfigRepository>();

    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAppPortalService, AppPortalService>();
    builder.Services.AddScoped<IAppCenterService, AppCenterService>();
    builder.Services.AddScoped<IDesktopService, DesktopService>();
    builder.Services.AddScoped<IFileShareService, FileShareService>();
    builder.Services.AddScoped<IDeviceAuthService, DeviceAuthService>();
    builder.Services.AddScoped<IConnectionService, ConnectionService>();
    builder.Services.AddScoped<ISambaFileClient, SambaFileClient>();
    builder.Services.AddScoped<IFilePreviewService, FilePreviewService>();
    builder.Services.AddScoped<IFileCenterService, FileCenterService>();
    builder.Services.AddSingleton<ISambaConfigManager, SambaConfigManager>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<RequestIdMiddleware>();
    app.UseMiddleware<TokenAutoRefreshMiddleware>();

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapBlazorHub();
    app.MapHub<DesktopHub>("/hubs/desktop");

    app.MapAuthEndpoints();
    app.MapDeviceAuthEndpoints();
    app.MapAdminDeviceEndpoints();
    app.MapAppPortalEndpoints();
    app.MapAppCenterEndpoints();
    app.MapAdminIconEndpoints();
    app.MapDesktopEndpoints();
    app.MapConnectionEndpoints();
    app.MapAdminConnectionEndpoints();
    app.MapFileShareEndpoints();
    app.MapFileCenterEndpoints();
    app.MapPermissionEndpoints();
    app.MapAuditLogEndpoints();
    app.MapAdminUserEndpoints();
    app.MapAdminAppEndpoints();
    app.MapAdminDriveEndpoints();
    app.MapHealthEndpoints();

    app.MapFallbackToPage("/_Host");

    using (var scope = app.Services.CreateScope())
    {
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}

finally
{
    Log.CloseAndFlush();
}

public partial class Program { }