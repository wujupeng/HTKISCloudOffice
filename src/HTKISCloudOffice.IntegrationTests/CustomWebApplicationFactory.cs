using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Configuration;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Text;

namespace HTKISCloudOffice.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IGuacamoleApiClient> GuacamoleMock { get; } = new();
    public Mock<IFilePreviewService> FilePreviewMock { get; } = new();

    public IServiceScope CreateScope() => Services.CreateScope();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AppDbContext));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
            });

            services.Replace(ServiceDescriptor.Scoped(_ => GuacamoleMock.Object));
            services.Replace(ServiceDescriptor.Scoped(_ => FilePreviewMock.Object));

            services.Replace(ServiceDescriptor.Singleton(new AesEncryptionConfig
            {
                key = "O8OqiXs09/412OMPBi/2EtNchIEcqxTNhA9mgSaUz3o="
            }));

            services.Replace(ServiceDescriptor.Singleton(new SambaFileConfig
            {
                mount_root = Path.Combine(Path.GetTempPath(), $"samba-inttest-{Guid.NewGuid():N}")
            }));

            services.Replace(ServiceDescriptor.Singleton(new FilePreviewConfig
            {
                libreoffice_path = "/usr/bin/libreoffice",
                temp_dir = Path.Combine(Path.GetTempPath(), "file-preview"),
                cache_ttl_minutes = 60
            }));

            services.PostConfigure<JwtBearerOptions>("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "htkis-cloud-office",
                    ValidAudience = "htkis-cloud-office-users",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("HTKIS_CLOUD_OFFICE_JWT_SECRET_KEY_2024_SECURE_RANDOM_32CHARS"))
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                };
            });
        });
    }
}