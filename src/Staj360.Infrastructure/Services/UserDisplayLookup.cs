using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;

namespace Staj360.Infrastructure.Services;

public class UserDisplayLookup : IUserDisplayLookup
{
    private readonly AppDbContext _db;

    public UserDisplayLookup(AppDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, UserDisplayInfo>> GetByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, UserDisplayInfo>();

        return await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserDisplayInfo(u.Id, u.FullName, u.Email ?? string.Empty))
            .ToDictionaryAsync(u => u.UserId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> SearchUserIdsAsync(string search, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            return Array.Empty<Guid>();

        var term = search.Trim();
        return await _db.Users.AsNoTracking()
            .Where(u => u.FullName.Contains(term) || (u.Email != null && u.Email.Contains(term)))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
