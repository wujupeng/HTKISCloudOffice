using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class AppCenterServiceTests
{
    private readonly Mock<IAppFavoriteRepository> _favorite_repo;
    private readonly Mock<IAppIconRepository> _icon_repo;
    private readonly Mock<IAppPortalService> _portal_svc;
    private readonly Mock<IPermissionService> _perm_svc;
    private readonly AppCenterService _service;

    public AppCenterServiceTests()
    {
        _favorite_repo = new Mock<IAppFavoriteRepository>();
        _icon_repo = new Mock<IAppIconRepository>();
        _portal_svc = new Mock<IAppPortalService>();
        _perm_svc = new Mock<IPermissionService>();
        _service = new AppCenterService(_favorite_repo.Object, _icon_repo.Object,
            _portal_svc.Object, _perm_svc.Object);
    }

    private static ApplicationDto CreateAppDto(Guid? id = null, string name = "WPS", AppCategory cat = AppCategory.Office)
        => new() { app_id = (id ?? Guid.NewGuid()).ToString(), app_name = name, category = cat, app_type = AppType.CloudDesktop };

    [Fact]
    public async Task GetAppCenterViewAsync_ReturnsFavoritesAndCategories()
    {
        var user_id = Guid.NewGuid().ToString();
        var fav_app_id = Guid.NewGuid();
        var other_app_id = Guid.NewGuid();

        _portal_svc.Setup(p => p.GetApplicationsForUserAsync(user_id))
            .ReturnsAsync(new List<ApplicationDto>
            {
                CreateAppDto(fav_app_id, "WPS", AppCategory.Office),
                CreateAppDto(other_app_id, "ERP", AppCategory.Business)
            });

        _favorite_repo.Setup(f => f.GetFavoriteAppIdsAsync(Guid.Parse(user_id)))
            .ReturnsAsync(new List<Guid> { fav_app_id });

        var result = await _service.GetAppCenterViewAsync(user_id);

        Assert.Single(result.favorites);
        Assert.Equal(fav_app_id.ToString(), result.favorites[0].app_id);
        Assert.Single(result.categories);
        Assert.Equal(AppCategory.Business, result.categories[0].category);
        Assert.Single(result.categories[0].applications);
    }

    [Fact]
    public async Task GetAppCenterViewAsync_NoFavorites_EmptyFavoritesList()
    {
        var user_id = Guid.NewGuid().ToString();
        _portal_svc.Setup(p => p.GetApplicationsForUserAsync(user_id))
            .ReturnsAsync(new List<ApplicationDto> { CreateAppDto(name: "ERP", cat: AppCategory.Business) });
        _favorite_repo.Setup(f => f.GetFavoriteAppIdsAsync(Guid.Parse(user_id)))
            .ReturnsAsync(new List<Guid>());

        var result = await _service.GetAppCenterViewAsync(user_id);

        Assert.Empty(result.favorites);
        Assert.Single(result.categories);
    }

    [Fact]
    public async Task GetAppCenterViewAsync_InvalidUserId_ReturnsEmpty()
    {
        var result = await _service.GetAppCenterViewAsync("not-a-guid");
        Assert.Empty(result.favorites);
        Assert.Empty(result.categories);
    }

    [Fact]
    public async Task SearchApplicationsAsync_MatchingKeyword_ReturnsMatches()
    {
        var user_id = Guid.NewGuid().ToString();
        _portal_svc.Setup(p => p.GetApplicationsForUserAsync(user_id))
            .ReturnsAsync(new List<ApplicationDto>
            {
                CreateAppDto(name: "WPS Office"),
                CreateAppDto(name: "ERP System"),
                CreateAppDto(name: "MES System")
            });

        var result = await _service.SearchApplicationsAsync(user_id, "wps");

        Assert.Single(result);
        Assert.Equal("WPS Office", result[0].app_name);
    }

    [Fact]
    public async Task SearchApplicationsAsync_NoMatch_ReturnsEmpty()
    {
        var user_id = Guid.NewGuid().ToString();
        _portal_svc.Setup(p => p.GetApplicationsForUserAsync(user_id))
            .ReturnsAsync(new List<ApplicationDto> { CreateAppDto(name: "WPS") });

        var result = await _service.SearchApplicationsAsync(user_id, "nonexistent");

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchApplicationsAsync_EmptyKeyword_ReturnsAll()
    {
        var user_id = Guid.NewGuid().ToString();
        _portal_svc.Setup(p => p.GetApplicationsForUserAsync(user_id))
            .ReturnsAsync(new List<ApplicationDto> { CreateAppDto(), CreateAppDto() });

        var result = await _service.SearchApplicationsAsync(user_id, "");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task AddFavoriteAsync_Normal_ReturnsSuccess()
    {
        var user_id = Guid.NewGuid();
        var app_id = Guid.NewGuid();
        _favorite_repo.Setup(f => f.IsFavoritedAsync(user_id, app_id)).ReturnsAsync(false);
        _favorite_repo.Setup(f => f.AddAsync(It.IsAny<AppFavorite>()))
            .ReturnsAsync((AppFavorite fav) => fav);

        var result = await _service.AddFavoriteAsync(user_id.ToString(), app_id.ToString());

        Assert.True(result.success);
    }

    [Fact]
    public async Task AddFavoriteAsync_AlreadyFavorited_ReturnsAlreadyFavorited()
    {
        var user_id = Guid.NewGuid();
        var app_id = Guid.NewGuid();
        _favorite_repo.Setup(f => f.IsFavoritedAsync(user_id, app_id)).ReturnsAsync(true);

        var result = await _service.AddFavoriteAsync(user_id.ToString(), app_id.ToString());

        Assert.False(result.success);
        Assert.Equal("ALREADY_FAVORITED", result.error_code);
    }

    [Fact]
    public async Task AddFavoriteAsync_InvalidId_ReturnsInvalidId()
    {
        var result = await _service.AddFavoriteAsync("bad", Guid.NewGuid().ToString());
        Assert.False(result.success);
        Assert.Equal("INVALID_ID", result.error_code);
    }

    [Fact]
    public async Task RemoveFavoriteAsync_Normal_ReturnsSuccess()
    {
        var user_id = Guid.NewGuid();
        var app_id = Guid.NewGuid();
        _favorite_repo.Setup(f => f.RemoveAsync(user_id, app_id)).ReturnsAsync(true);

        var result = await _service.RemoveFavoriteAsync(user_id.ToString(), app_id.ToString());

        Assert.True(result.success);
    }

    [Fact]
    public async Task RemoveFavoriteAsync_NotFavorited_ReturnsNotFavorited()
    {
        var user_id = Guid.NewGuid();
        var app_id = Guid.NewGuid();
        _favorite_repo.Setup(f => f.RemoveAsync(user_id, app_id)).ReturnsAsync(false);

        var result = await _service.RemoveFavoriteAsync(user_id.ToString(), app_id.ToString());

        Assert.False(result.success);
        Assert.Equal("NOT_FAVORITED", result.error_code);
    }

    [Fact]
    public async Task GetUserFavoritesAsync_ReturnsMappedDtos()
    {
        var user_id = Guid.NewGuid();
        var app = new Domain.Entities.Application
        {
            app_id = Guid.NewGuid(), app_name = "WPS", app_type = AppType.CloudDesktop,
            category = AppCategory.Office, icon_url = "/wps.png"
        };
        var favorites = new List<AppFavorite>
        {
            new() { user_id = user_id, app_id = app.app_id, Application = app }
        };

        _favorite_repo.Setup(f => f.GetByUserIdAsync(user_id)).ReturnsAsync(favorites);

        var result = await _service.GetUserFavoritesAsync(user_id.ToString());

        Assert.Single(result);
        Assert.Equal("WPS", result[0].app_name);
    }

    [Fact]
    public async Task GetAppIconsAsync_ReturnsMappedDtos()
    {
        var icons = new List<AppIcon>
        {
            new() { icon_id = Guid.NewGuid(), icon_name = "test", icon_type = AppIconType.Preset, icon_url = "/test.png" }
        };
        _icon_repo.Setup(i => i.GetAllAsync()).ReturnsAsync(icons);

        var result = await _service.GetAppIconsAsync();

        Assert.Single(result);
        Assert.Equal("test", result[0].icon_name);
        Assert.Equal("preset", result[0].icon_type);
    }

    [Fact]
    public async Task GetAppCenterViewAsync_FavoritesNotInCategories()
    {
        var user_id = Guid.NewGuid().ToString();
        var fav_id = Guid.NewGuid();
        var other_id = Guid.NewGuid();

        _portal_svc.Setup(p => p.GetApplicationsForUserAsync(user_id))
            .ReturnsAsync(new List<ApplicationDto>
            {
                CreateAppDto(fav_id, "WPS", AppCategory.Office),
                CreateAppDto(other_id, "ERP", AppCategory.Office)
            });
        _favorite_repo.Setup(f => f.GetFavoriteAppIdsAsync(Guid.Parse(user_id)))
            .ReturnsAsync(new List<Guid> { fav_id });

        var result = await _service.GetAppCenterViewAsync(user_id);

        Assert.Single(result.favorites);
        Assert.Equal(fav_id.ToString(), result.favorites[0].app_id);
        var cat_apps = result.categories.SelectMany(c => c.applications).ToList();
        Assert.DoesNotContain(cat_apps, a => a.app_id == fav_id.ToString());
        Assert.Contains(cat_apps, a => a.app_id == other_id.ToString());
    }
}