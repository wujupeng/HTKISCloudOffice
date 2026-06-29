using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IAppPortalService
{
    Task<List<ApplicationDto>> GetApplicationsForUserAsync(string user_id);
    Task<ApplicationDto?> GetApplicationByIdAsync(string app_id);
    Task<LaunchResult> LaunchApplicationAsync(string user_id, string app_id);
    Task<List<ApplicationDto>> GetApplicationsByCategoryAsync(string user_id, AppCategory category);
}