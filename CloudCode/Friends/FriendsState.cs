using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MyriadOfDragons.CloudCode.Friends;

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
}

/// <summary>One relationship between two accounts, stored once under a shared, pair-derived key
/// (see <see cref="CloudSaveFriendsStore"/>) rather than duplicated per-account - a request and its
/// eventual acceptance/decline/removal must be a single source of truth both sides observe, the
/// same reasoning Bazaar's listings use for buyer/seller. RequesterAccountId/RecipientAccountId
/// record who actually sent the original request, independent of the shared key's own (arbitrary,
/// canonical) ordering - direction still matters for "only the recipient may accept/decline" and
/// for attributing each side's own daily gift.</summary>
public sealed class FriendshipRecord
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    [JsonProperty("requesterAccountId")]
    public string RequesterAccountId { get; set; } = string.Empty;
    [JsonProperty("recipientAccountId")]
    public string RecipientAccountId { get; set; } = string.Empty;
    [JsonProperty("status")]
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    [JsonProperty("createdUtcMs")]
    public long CreatedUtcMs { get; set; }
    [JsonProperty("respondedUtcMs")]
    public long RespondedUtcMs { get; set; }
    /// <summary>UTC-day-bucketed (see FriendsOperations.IsSameUtcDay) last-gift timestamp,
    /// attributed to whichever account actually sent it - kept as two fields rather than one
    /// dictionary because there are only ever exactly two possible senders for a given pair.</summary>
    [JsonProperty("requesterLastGiftUtcMs")]
    public long RequesterLastGiftUtcMs { get; set; }
    [JsonProperty("recipientLastGiftUtcMs")]
    public long RecipientLastGiftUtcMs { get; set; }

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

/// <summary>One account's own index of every counterpart it has a <see cref="FriendshipRecord"/>
/// with (pending in either direction, or accepted) - player-scoped, not shared, because "which
/// pairs touch me" is inherently per-account. Needed because Cloud Save Custom Items (where the
/// shared records themselves live) has no query/filter capability - see CloudSaveFriendsStore's own
/// doc comment, identical reasoning to Bazaar's own listing index.</summary>
public sealed class FriendsIndexState
{
    [JsonProperty("counterpartAccountIds")]
    public List<string> CounterpartAccountIds { get; set; } = new();

    [JsonIgnore]
    public string? WriteLock { get; set; }
}

public sealed class FriendRequest
{
    public string TargetAccountId { get; set; } = string.Empty;
}

public sealed class FriendResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("status")]
    [JsonConverter(typeof(StringEnumConverter))]
    public FriendshipStatus? Status { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class ListFriendsRequest
{
}

public sealed class FriendSummary
{
    [JsonProperty("counterpartAccountId")]
    public string CounterpartAccountId { get; set; } = string.Empty;
    [JsonProperty("status")]
    [JsonConverter(typeof(StringEnumConverter))]
    public FriendshipStatus Status { get; set; }
    /// <summary>True when the caller is the one who sent this still-pending request (so the UI
    /// shows "Requested" rather than "Respond"); meaningless once Accepted.</summary>
    [JsonProperty("isOutgoingRequest")]
    public bool IsOutgoingRequest { get; set; }
    [JsonProperty("canGiftToday")]
    public bool CanGiftToday { get; set; }
    [JsonProperty("createdUtcMs")]
    public long CreatedUtcMs { get; set; }
}

public sealed class ListFriendsResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("friends")]
    public List<FriendSummary> Friends { get; set; } = new();
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class GiftResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; set; }
}

public sealed class FriendsStorageException : Exception
{
    public FriendsStorageException(string errorCode, Exception? innerException = null)
        : base("Friends storage failed.", innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public interface IFriendsClock
{
    long UtcNowMs { get; }
}

public sealed class SystemFriendsClock : IFriendsClock
{
    public long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
