namespace Staj360.Application.Abstractions;

public record UserDisplayInfo(Guid UserId, string FullName, string Email);

/// <summary>
/// Identity kullanıcılarının görüntüleme bilgilerine Application katmanından erişim.
/// Domain Identity'ye bağımlı değildir; implementasyon Infrastructure'dadır.
/// </summary>
public interface IUserDisplayLookup
{
    Task<IReadOnlyDictionary<Guid, UserDisplayInfo>> GetByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>Ad Soyad veya e-posta içinde arama; eşleşen kullanıcı Id listesi.</summary>
    Task<IReadOnlyList<Guid>> SearchUserIdsAsync(string search, CancellationToken cancellationToken = default);
}
