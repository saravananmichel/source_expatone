using System.Security.Cryptography;
using System.Text;
using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace ExpatOne.Infrastructure.Services;

public partial class FamilyService
{
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Fingerprint(Document d) => Hash($"{d.Id:N}|{d.S3ObjectKey}|{d.ExpiryDate?.ToUniversalTime():O}");

    public Task<List<FamilyDashboardDocumentDto>> DashboardAsync(Guid userId) => DashboardAtAsync(userId, DateTime.UtcNow);

    // Explicit clock enables deterministic boundary tests. Calendar days are UTC throughout.
    internal async Task<List<FamilyDashboardDocumentDto>> DashboardAtAsync(Guid userId, DateTime now)
    {
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.PendingUpload &&
                (d.UserId == userId || db.FamilyDocumentGrants.Any(g => g.DocumentId == d.Id &&
                    g.Request.StatusId == FamilyRequestStates.Accepted &&
                    (g.Request.AccessExpiresAt == null || g.Request.AccessExpiresAt > now) && g.Request.RequesterUserId == userId &&
                    g.Request.RecipientUserId == d.UserId)))
            .Include(d => d.User).Include(d => d.DocumentType)
            .Select(d => new {
                Document = d,
                Confirmation = db.FamilyExpiryConfirmations.Where(c => c.DocumentId == d.Id).Select(c => c.Fingerprint).FirstOrDefault(),
                RemindersAllowed = d.UserId == userId || db.FamilyDocumentGrants.Any(g => g.DocumentId == d.Id &&
                    g.Request.StatusId == FamilyRequestStates.Accepted &&
                    (g.Request.AccessExpiresAt == null || g.Request.AccessExpiresAt > now) && g.Request.RequesterUserId == userId &&
                    g.Request.RecipientUserId == d.UserId && g.Request.AllowExpiryReminders)
            }).ToListAsync();
        return rows.Select(row => {
            var d = row.Document;
            int? days = d.ExpiryDate.HasValue ? (d.ExpiryDate.Value.Date - now.Date).Days : null;
            var fingerprint = Fingerprint(d);
            return new FamilyDashboardDocumentDto(d.Id, d.DocumentName, d.DocumentType.Name,
                new(d.UserId, d.User.Email, d.User.DisplayName), d.UserId == userId, d.ExpiryDate, days,
                days is null ? "No expiry date" : days < 0 ? "Expired" : days <= 90 ? "Due soon" : "Valid",
                d.ExpiryDate.HasValue && row.Confirmation == fingerprint, row.RemindersAllowed, fingerprint);
        }).OrderBy(d => d.DaysRemaining ?? int.MaxValue).ThenBy(d => d.Name).ToList();
    }

    public async Task ConfirmExpiryAsync(Guid userId, Guid documentId, ConfirmFamilyExpiryDto dto)
    {
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId &&
            d.UserId == userId && d.Status != DocumentStatus.PendingUpload) ?? throw new KeyNotFoundException();
        if (!document.ExpiryDate.HasValue) throw new ArgumentException("This document has no expiry date to confirm.");
        var fingerprint = Fingerprint(document);
        if (fingerprint != dto.Fingerprint) throw new InvalidOperationException("Document changed. Refresh and review its expiry date again.");
        var confirmation = await db.FamilyExpiryConfirmations.FindAsync(documentId);
        if (confirmation is null)
            db.FamilyExpiryConfirmations.Add(new FamilyExpiryConfirmation { DocumentId = documentId,
                Fingerprint = fingerprint, ConfirmedAt = DateTime.UtcNow });
        else { confirmation.Fingerprint = fingerprint; confirmation.ConfirmedAt = DateTime.UtcNow; }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new InvalidOperationException("Confirmation changed. Refresh and try again."); }
        // If another operation changes the date/file concurrently, the fingerprint no longer matches;
        // both dashboard and alerts then require a fresh confirmation.
    }

    public Task<List<FamilyAlertDto>> AlertsAsync(Guid userId) => AlertsAtAsync(userId, DateTime.UtcNow);

    internal async Task<List<FamilyAlertDto>> AlertsAtAsync(Guid userId, DateTime now)
    {
        var alerts = new List<FamilyAlertDto>();
        foreach (var d in await DashboardAtAsync(userId, now))
        {
            if (!d.RemindersAllowed || !d.ExpiryConfirmed || d.DaysRemaining is null or > 90) continue;
            var days = d.DaysRemaining.Value;
            // Only the current milestone is shown: do not backfill three alerts on first use.
            var milestone = days < 0 ? "expired" : days == 0 ? "today" : days <= 7 ? "7" : days <= 30 ? "30" : "90";
            var timing = days < 0 ? $"Expired {-days} day(s) ago" : days == 0 ? "Expires today" : $"Expires in {days} day(s)";
            alerts.Add(new(Hash($"expiry|{userId:N}|{d.Id:N}|{d.Fingerprint}|{milestone}"), "expiry", d.Name,
                $"{d.Owner.DisplayName ?? d.Owner.Email}: {timing}.", d.Id, null, d.ExpiryDate));
        }
        foreach (var r in await ListAsync(userId))
        {
            // No historical document details are stored in request alerts.
            var other = r.IsIncoming ? r.Requester : r.Recipient;
            var expired = r.StatusId == FamilyRequestStates.Accepted && r.AccessExpiresAt.HasValue && r.AccessExpiresAt <= now;
            var alertStatus = expired ? "Expired" : r.Status;
            string? message = alertStatus switch {
                "Expired" => $"Temporary document access between you and {other.Email} has expired. The owner can renew it in Family.",
                "Pending" when r.IsIncoming => $"{other.Email} requested access. Choose documents in Family to accept.",
                "Accepted" when !r.IsIncoming => $"{other.Email} accepted your request. Only selected documents are shared.",
                "Rejected" when !r.IsIncoming => $"{other.Email} declined your request.",
                "Cancelled" when r.IsIncoming => $"{other.Email} cancelled their request.",
                "Revoked" => $"Document access between you and {other.Email} was revoked.",
                _ => null
            };
            if (message != null) alerts.Add(new(Hash(expired ? $"request-expired|{userId:N}|{r.Id:N}|{r.AccessExpiresAt:O}" : $"request|{userId:N}|{r.Id:N}|{r.StatusId}"), "request",
                $"Family request: {alertStatus}", message, null, r.Id, null));
        }
        var dismissed = (await db.FamilyAlertDismissals.AsNoTracking().Where(d => d.UserId == userId)
            .Select(d => d.AlertId).ToListAsync()).ToHashSet();
        return alerts.Where(a => !dismissed.Contains(a.Id)).ToList();
    }

    public async Task DismissAlertAsync(Guid userId, string alertId)
    {
        if (alertId.Length != 64) throw new KeyNotFoundException();
        if (await db.FamilyAlertDismissals.AnyAsync(d => d.UserId == userId && d.AlertId == alertId)) return;
        if (!(await AlertsAsync(userId)).Any(a => a.Id == alertId)) throw new KeyNotFoundException();
        var receipt = new FamilyAlertDismissal { UserId = userId, AlertId = alertId, DismissedAt = DateTime.UtcNow };
        db.FamilyAlertDismissals.Add(receipt);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Entry(receipt).State = EntityState.Detached;
            if (!await db.FamilyAlertDismissals.AnyAsync(d => d.UserId == userId && d.AlertId == alertId)) throw;
        }
    }
}
