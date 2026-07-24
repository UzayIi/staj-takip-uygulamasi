using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Messaging;

public record SendStaffMessageCommand(
    Guid RecipientUserId,
    Guid OrganizationUnitId,
    string Subject,
    string Body,
    Guid? ParentMessageId = null);

public record EligibleRecipientDto(
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyList<Guid> SharedUnitIds,
    IReadOnlyList<string> SharedUnitNames);

public record StaffMessageListItemDto(
    Guid Id,
    Guid ThreadId,
    Guid OtherUserId,
    string OtherUserName,
    Guid OrganizationUnitId,
    string OrganizationUnitName,
    string Subject,
    DateTime SentAtUtc,
    bool IsRead,
    bool IsIncoming);

public record StaffMessageThreadDto(
    Guid ThreadId,
    string Subject,
    Guid OrganizationUnitId,
    string OrganizationUnitName,
    Guid OtherUserId,
    string OtherUserName,
    IReadOnlyList<StaffMessage> Messages);

public interface IStaffMessagingService
{
    Task<IReadOnlyList<EligibleRecipientDto>> GetEligibleRecipientsAsync(Guid senderUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<StaffMessage>> SendAsync(Guid senderUserId, SendStaffMessageCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> MarkReadAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ArchiveAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffMessageListItemDto>> ListInboxAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffMessageListItemDto>> ListSentAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffMessageListItemDto>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<StaffMessageThreadDto>> GetDetailsAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
}
