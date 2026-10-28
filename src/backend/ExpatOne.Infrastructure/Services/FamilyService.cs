using System.Net.Mail;
using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ExpatOne.Infrastructure.Services;

public partial class FamilyService(ExpatOneDbContext db)
{
    public async Task<FamilyUserDto> SearchAsync(Guid userId, string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 256 || !MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new ArgumentException("Enter a valid, complete email address.");
        var matches = await db.Users.Where(x => x.IsActive && x.Id != userId && x.Email.ToLower() == normalized)
            .Take(2).ToListAsync();
        if (matches.Count != 1) throw new KeyNotFoundException();
        var u = matches[0];
        return new(u.Id, u.Email, u.DisplayName);
    }
    private IQueryable<FamilyRequest> Requests => db.FamilyRequests
        .Include(x => x.Requester).Include(x => x.Recipient).Include(x => x.Status).Include(x => x.Grants);
    private static FamilyRequestDto Map(FamilyRequest r, Guid userId) => new(r.Id,
        new(r.RequesterUserId, r.Requester.Email, r.Requester.DisplayName),
        new(r.RecipientUserId, r.Recipient.Email, r.Recipient.DisplayName),
        r.StatusId, r.StatusId switch { 1 => "Pending", 2 => "Accepted", 3 => "Rejected", 4 => "Cancelled", 5 => "Revoked", _ => "Unknown" }, r.RecipientUserId == userId, r.Revision, r.CreatedAt,
        r.RespondedAt, r.Grants.Select(g => g.DocumentId).ToList(), r.AllowExpiryReminders, r.AccessExpiresAt,
        r.StatusId == FamilyRequestStates.Accepted && r.AccessExpiresAt.HasValue && r.AccessExpiresAt <= DateTime.UtcNow, r.Relationship, r.AllowVisaApplicationUse);
    public async Task<List<FamilyRequestDto>> ListAsync(Guid userId) =>
        (await Requests.AsNoTracking().Where(x => x.RequesterUserId == userId || x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync()).Select(x => Map(x, userId)).ToList();
    public async Task<FamilyRequestDto> SendAsync(Guid userId, string email, string relationship = "Other")
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "Spouse", "Daughter", "Son", "Father", "Mother", "Brother", "Sister", "Friend", "Colleague", "Guardian", "Other" };
        if (!allowed.Contains(relationship)) throw new ArgumentException("Invalid relationship.");
        var target = await SearchAsync(userId, email);
        if (await db.FamilyRequests.AnyAsync(x => ((x.RequesterUserId == userId && x.RecipientUserId == target.Id) ||
             (x.RequesterUserId == target.Id && x.RecipientUserId == userId)) &&
            (x.StatusId == FamilyRequestStates.Pending || x.StatusId == FamilyRequestStates.Accepted)))
            throw new InvalidOperationException("A pending or accepted request already exists.");
        var request = new FamilyRequest { RequesterUserId = userId, RecipientUserId = target.Id, Relationship = relationship };
        db.FamilyRequests.Add(request);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new InvalidOperationException("Request could not be created. Refresh and try again."); }
        return Map(await Requests.SingleAsync(x => x.Id == request.Id), userId);
    }
    private async Task<FamilyRequest> FindAsync(Guid userId, Guid id, Guid revision, bool recipientOnly = false)
    {
        var r = await Requests.SingleOrDefaultAsync(x => x.Id == id &&
            (x.RecipientUserId == userId || (!recipientOnly && x.RequesterUserId == userId)))
            ?? throw new KeyNotFoundException();
        if (r.Revision != revision) throw new InvalidOperationException("This request changed. Refresh and try again.");
        return r;
    }
    private async Task ReplaceGrantsAsync(FamilyRequest r, List<Guid> ids)
    {
        var selected = ids.Distinct().ToHashSet();
        var owned = await db.Documents.Where(d => selected.Contains(d.Id) && d.UserId == r.RecipientUserId &&
            d.Status != DocumentStatus.PendingUpload).Select(d => d.Id).ToListAsync();
        if (owned.Count != selected.Count) throw new ArgumentException("Only your own completed documents can be shared.");
        foreach (var grant in r.Grants.Where(g => !selected.Contains(g.DocumentId)).ToList())
            db.FamilyDocumentGrants.Remove(grant);
        var existing = r.Grants.Select(g => g.DocumentId).ToHashSet();
        foreach (var id in selected.Except(existing))
            r.Grants.Add(new FamilyDocumentGrant { FamilyRequestId = r.Id, DocumentId = id });
    }
    private async Task<FamilyRequestDto> SaveAsync(FamilyRequest r, Guid userId)
    {
        r.Revision = Guid.NewGuid();
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("This request changed. Refresh and try again."); }
        return Map(await Requests.AsNoTracking().SingleAsync(x => x.Id == r.Id), userId);
    }
    private static DateTime? ValidateAccessExpiry(DateTimeOffset? expiry)
    {
        if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Sharing expiry must be in the future.");
        return expiry?.UtcDateTime;
    }
    public async Task<FamilyRequestDto> RespondAsync(Guid userId, Guid id, FamilyDecisionDto dto)
    {
        var r = await FindAsync(userId, id, dto.Revision, true);
        if (r.StatusId != FamilyRequestStates.Pending) throw new InvalidOperationException("Request is no longer pending.");
        var expiry = dto.Accept ? ValidateAccessExpiry(dto.AccessExpiresAt) : null;
        if (dto.Accept) await ReplaceGrantsAsync(r, dto.DocumentIds);
        r.AccessExpiresAt = expiry;
        r.AllowExpiryReminders = dto.Accept && dto.AllowExpiryReminders == true;
        r.AllowVisaApplicationUse = dto.Accept && dto.AllowVisaApplicationUse == true;
        r.StatusId = dto.Accept ? FamilyRequestStates.Accepted : FamilyRequestStates.Rejected;
        r.RespondedAt = DateTime.UtcNow;
        return await SaveAsync(r, userId);
    }
    public async Task<FamilyRequestDto> UpdateGrantsAsync(Guid userId, Guid id, FamilyGrantsDto dto)
    {
        var r = await FindAsync(userId, id, dto.Revision, true);
        if (r.StatusId != FamilyRequestStates.Accepted) throw new InvalidOperationException("Request is not accepted.");
        var changeExpiry = dto.UpdateAccessExpiry || dto.AccessExpiresAt.HasValue;
        var expiry = changeExpiry ? ValidateAccessExpiry(dto.AccessExpiresAt) : r.AccessExpiresAt;
        await ReplaceGrantsAsync(r, dto.DocumentIds);
        r.AccessExpiresAt = expiry;
        if (dto.AllowExpiryReminders.HasValue) r.AllowExpiryReminders = dto.AllowExpiryReminders.Value;
        if (dto.AllowVisaApplicationUse.HasValue) r.AllowVisaApplicationUse = dto.AllowVisaApplicationUse.Value;
        return await SaveAsync(r, userId);
    }
    public async Task<FamilyRequestDto> EndAsync(Guid userId, Guid id, Guid revision)
    {
        var r = await FindAsync(userId, id, revision);
        if (r.StatusId == FamilyRequestStates.Pending && r.RequesterUserId == userId)
            r.StatusId = FamilyRequestStates.Cancelled;
        else if (r.StatusId == FamilyRequestStates.Accepted) r.StatusId = FamilyRequestStates.Revoked;
        else throw new InvalidOperationException("This request cannot be cancelled or revoked.");
        r.AllowExpiryReminders = false;
        db.FamilyDocumentGrants.RemoveRange(r.Grants);
        return await SaveAsync(r, userId);
    }
}
