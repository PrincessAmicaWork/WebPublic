using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Services;

// NEW: UI-friendly DTO for the Vorgesetzte autocomplete.
public record ApproverOption(string Name, string Email);

// NEW: Import DTO. Use this for CSV/SCIM/manual import later.
public record ApproverImportRow(string DisplayName, string Email);

// NEW: Gives you a clear result after replacing the approver list from an import.
public record ApproverImportResult(int Added, int Updated, int Deactivated);

public interface IApproverService
{
    Task<List<ApproverOption>> GetActiveApproversAsync();
    Task<ApproverOption?> FindActiveByEmailAsync(string email);
    Task<bool> IsActiveApproverAsync(string email);
    Task<ApproverImportResult> ReplaceFromImportAsync(IEnumerable<ApproverImportRow> rows);
}

public class ApproverService : IApproverService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ApproverService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<ApproverOption>> GetActiveApproversAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // NEW: RequestDialog uses this instead of a hard-coded in-code supervisor list.
        return await db.Approvers
            .AsNoTracking()
            .Where(a => a.IsActiveValue == 1)
            .OrderBy(a => a.DisplayName)
            .Select(a => new ApproverOption(a.DisplayName, a.Email))
            .ToListAsync();
    }

    public async Task<ApproverOption?> FindActiveByEmailAsync(string email)
    {
        var normalized = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // NEW: Used by EquipmentService to validate the selected supervisor server-side.
        return await db.Approvers
            .AsNoTracking()
            .Where(a => a.IsActiveValue == 1 && a.EmailNormalized == normalized)
            .Select(a => new ApproverOption(a.DisplayName, a.Email))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsActiveApproverAsync(string email)
    {
        var normalized = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Approvers
            .AsNoTracking()
            .AnyAsync(a => a.IsActiveValue == 1 && a.EmailNormalized == normalized);
    }

    public async Task<ApproverImportResult> ReplaceFromImportAsync(IEnumerable<ApproverImportRow> rows)
    {
        // NEW: Import behavior:
        // - rows in the import become active
        // - existing approvers missing from the import become inactive
        // - no hard delete, because old requests should still show old names/emails
        var incoming = rows
            .Where(r =>
                !string.IsNullOrWhiteSpace(r.DisplayName) &&
                !string.IsNullOrWhiteSpace(r.Email))
            .Select(r => new
            {
                DisplayName = r.DisplayName.Trim(),
                Email = r.Email.Trim(),
                EmailNormalized = NormalizeEmail(r.Email)
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.EmailNormalized))
            .GroupBy(r => r.EmailNormalized)
            .Select(g => g.First())
            .ToDictionary(x => x.EmailNormalized, x => x);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.Approvers.ToListAsync();

        var added = 0;
        var updated = 0;
        var deactivated = 0;
        var now = DateTime.Now;

        foreach (var approver in existing)
        {
            var normalized = NormalizeEmail(approver.Email);

            if (incoming.TryGetValue(normalized, out var row))
            {
                if (approver.DisplayName != row.DisplayName ||
                    approver.Email != row.Email ||
                    approver.EmailNormalized != row.EmailNormalized ||
                    !approver.IsActive)
                {
                    approver.DisplayName = row.DisplayName;
                    approver.Email = row.Email;
                    approver.EmailNormalized = row.EmailNormalized;
                    approver.IsActive = true;
                    approver.Source = "CSV";
                    approver.LastSyncedAt = now;
                    updated++;
                }

                incoming.Remove(normalized);
            }
            else if (approver.IsActive)
            {
                approver.IsActive = false;
                approver.LastSyncedAt = now;
                deactivated++;
            }
        }

        foreach (var row in incoming.Values)
        {
            db.Approvers.Add(new Approver
            {
                DisplayName = row.DisplayName,
                Email = row.Email,
                EmailNormalized = row.EmailNormalized,
                IsActive = true,
                Source = "CSV",
                CreatedAt = now,
                UpdatedAt = now,
                LastSyncedAt = now
            });

            added++;
        }

        await db.SaveChangesAsync();

        return new ApproverImportResult(added, updated, deactivated);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? "";
    }
}
