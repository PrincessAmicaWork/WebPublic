using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Services;

public interface ICurrentSiteService
{
    Task<Site?> GetCurrentSiteAsync(CancellationToken cancellationToken = default);
    Task<int?> GetCurrentSiteIdAsync(CancellationToken cancellationToken = default);
    Task<List<Site>> GetAvailableSitesAsync(CancellationToken cancellationToken = default);
    Task<Site?> GetSuggestedSiteFromGroupsAsync(CancellationToken cancellationToken = default);
    Task SetCurrentSiteAsync(int siteId, CancellationToken cancellationToken = default);
    Task<bool> CurrentSiteUsesStorageAsync(CancellationToken cancellationToken = default);
    event Action? OnSiteChanged;
}

public class CurrentSiteService : ICurrentSiteService
{
    private const string DefaultCulture = "de-CH";
    public event Action? OnSiteChanged;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUserService;

    public CurrentSiteService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentUserService currentUserService)
    {
        _dbFactory = dbFactory;
        _currentUserService = currentUserService;
    }

    public async Task<Site?> GetCurrentSiteAsync(CancellationToken cancellationToken = default)
    {
        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Email))
            return null;

        var emailNormalized = NormalizeEmail(user.Email);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var preference = await db.UserPreferences
            .AsNoTracking()
            .Include(x => x.LastSelectedSite)
            .FirstOrDefaultAsync(
                x => x.UserEmailNormalized == emailNormalized,
                cancellationToken);

        var site = preference?.LastSelectedSite;

        if (site is null || site.IsActiveValue != 1)
        {
            site = await db.Sites
                .AsNoTracking()
                .Where(s => s.IsActiveValue == 1)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await UserCanAccessSiteAsync(db, emailNormalized, site.Id, cancellationToken)
            ? site
            : null;
    }

    public async Task<int?> GetCurrentSiteIdAsync(CancellationToken cancellationToken = default)
    {
        return (await GetCurrentSiteAsync(cancellationToken))?.Id;
    }

    public async Task<List<Site>> GetAvailableSitesAsync(CancellationToken cancellationToken = default)
    {
        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var emailNormalized = NormalizeEmail(user.Email);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var activeSitesQuery = db.Sites
            .AsNoTracking()
            .Where(s => s.IsActiveValue == 1);

        if (!string.IsNullOrWhiteSpace(emailNormalized))
        {
            var accessRows = await db.UserSiteAccesses
                .AsNoTracking()
                .Where(a =>
                    a.UserEmailNormalized == emailNormalized &&
                    a.IsActiveValue == 1 &&
                    a.CanOrderValue == 1)
                .Select(a => a.SiteId)
                .ToListAsync(cancellationToken);

            // Phase 1 behavior: if no access rows exist yet, every authenticated user can choose every active site.
            if (accessRows.Count > 0)
                activeSitesQuery = activeSitesQuery.Where(s => accessRows.Contains(s.Id));
        }

        return await activeSitesQuery
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Site?> GetSuggestedSiteFromGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groupIds = await _currentUserService.GetCurrentUserGroupIdsAsync(cancellationToken);
        if (groupIds.Count == 0)
            return null;

        var groupIdList = groupIds.ToList();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var matches = await db.Sites
            .AsNoTracking()
            .Where(s =>
                s.IsActiveValue == 1 &&
                s.EntraGroupObjectId != null &&
                s.EntraGroupObjectId.Trim() != "" &&
                groupIdList.Contains(s.EntraGroupObjectId))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        // If the same Entra group is configured for multiple sites, do not guess.
        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task SetCurrentSiteAsync(int siteId, CancellationToken cancellationToken = default)
    {
        if (siteId <= 0)
            throw new ArgumentOutOfRangeException(nameof(siteId));

        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.Email))
            throw new InvalidOperationException("No authenticated user was found.");

        var email = user.Email.Trim();
        var emailNormalized = NormalizeEmail(email);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var siteExists = await db.Sites.AnyAsync(
            s => s.Id == siteId && s.IsActiveValue == 1,
            cancellationToken);

        if (!siteExists)
            throw new InvalidOperationException("The selected site does not exist or is inactive.");

        if (!await UserCanAccessSiteAsync(db, emailNormalized, siteId, cancellationToken))
            throw new InvalidOperationException("The current user is not allowed to use the selected site.");

        var preference = await db.UserPreferences
            .FirstOrDefaultAsync(
                x => x.UserEmailNormalized == emailNormalized,
                cancellationToken);

        if (preference is null)
        {
            db.UserPreferences.Add(new UserPreference
            {
                UserEmail = email,
                UserEmailNormalized = emailNormalized,
                LastSelectedSiteId = siteId,
                PreferredCulture = DefaultCulture,
                UpdatedAt = DateTime.Now
            });
        }
        else
        {
            preference.UserEmail = email;
            preference.UserEmailNormalized = emailNormalized;
            preference.LastSelectedSiteId = siteId;
            preference.UpdatedAt = DateTime.Now;
        }

        await db.SaveChangesAsync(cancellationToken);
        OnSiteChanged?.Invoke();
    }

    public async Task<bool> CurrentSiteUsesStorageAsync(CancellationToken cancellationToken = default)
    {
        var site = await GetCurrentSiteAsync(cancellationToken);
        return site is not null && site.StockPolicy != AppStockPolicy.NeverUseStorage;
    }

    private static async Task<bool> UserCanAccessSiteAsync(
        AppDbContext db,
        string emailNormalized,
        int siteId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(emailNormalized))
            return false;

        var hasAnyAccessRows = await db.UserSiteAccesses
            .AsNoTracking()
            .AnyAsync(a =>
                a.UserEmailNormalized == emailNormalized &&
                a.IsActiveValue == 1,
                cancellationToken);

        // Phase 1 behavior: no rows means open site choice for all authenticated users.
        if (!hasAnyAccessRows)
            return true;

        return await db.UserSiteAccesses
            .AsNoTracking()
            .AnyAsync(a =>
                a.UserEmailNormalized == emailNormalized &&
                a.SiteId == siteId &&
                a.IsActiveValue == 1 &&
                a.CanOrderValue == 1,
                cancellationToken);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? "";
    }
}
