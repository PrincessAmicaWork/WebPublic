using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Lagerverwaltung.Web.Services;

public record CurrentUserInfo(
    bool IsAuthenticated,
    string Email,
    string Name);

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string Email { get; }
    string Name { get; }

    Task<CurrentUserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetCurrentUserGroupIdsAsync(CancellationToken cancellationToken = default);
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
    }

    private ClaimsPrincipal? HttpUser => _httpContextAccessor.HttpContext?.User;

    // Synchronous properties are kept for existing UI code.
    // They use HttpContext only because Blazor Server event callbacks do not always
    // have a reliable HttpContext, especially after publish behind IIS/reverse proxy.
    // New submit/business logic should use GetCurrentUserAsync().
    public bool IsAuthenticated => ReadUser(HttpUser).IsAuthenticated;

    public string Email => ReadUser(HttpUser).Email;

    public string Name => ReadUser(HttpUser).Name;

    public async Task<CurrentUserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        // In interactive Blazor Server, AuthenticationStateProvider is the reliable
        // source during button clicks / SignalR circuit events.
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var circuitUser = ReadUser(authState.User);

        if (circuitUser.IsAuthenticated)
            return circuitUser;

        // Fallback for normal HTTP endpoints and prerendering.
        return ReadUser(HttpUser);
    }

    public async Task<IReadOnlySet<string>> GetCurrentUserGroupIdsAsync(CancellationToken cancellationToken = default)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var circuitGroups = ReadGroupIds(authState.User);

        if (circuitGroups.Count > 0)
            return circuitGroups;

        return ReadGroupIds(HttpUser);
    }

    private static CurrentUserInfo ReadUser(ClaimsPrincipal? user)
    {
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        var email =
            user?.FindFirst("preferred_username")?.Value
            ?? user?.FindFirst("email")?.Value
            ?? user?.FindFirst(ClaimTypes.Email)?.Value
            ?? user?.FindFirst("upn")?.Value
            ?? user?.Identity?.Name
            ?? "";

        var name =
            user?.FindFirst("name")?.Value
            ?? user?.FindFirst(ClaimTypes.Name)?.Value
            ?? user?.Identity?.Name
            ?? email
            ?? "";

        return new CurrentUserInfo(
            isAuthenticated,
            email.Trim(),
            name.Trim());
    }

    private static IReadOnlySet<string> ReadGroupIds(ClaimsPrincipal? user)
    {
        return user?.FindAll("groups")
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
