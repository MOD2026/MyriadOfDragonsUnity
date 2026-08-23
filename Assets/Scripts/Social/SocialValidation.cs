using System;
using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Social
{
    public static class SocialValidation
    {
        public const int MaxDisplayNameLength = 32;
        public const int MaxGuildNameLength = 64;
        public const int MaxChatMessageLength = 1000;
        public const int MaxDirectMessageLength = 2000;
        public const int MinRetentionDays = 1;
        public const int MaxRetentionDays = 3650;
        public const int MinPageSize = 1;
        public const int MaxPageSize = 100;
        public const int MinPageNumber = 1;

        public static ValidationResult ValidateOpaqueId(string value, string fieldName)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{fieldName} must not be empty.");
            }
            else if (value.Trim() != value || value.Contains(" "))
            {
                errors.Add($"{fieldName} must be a trimmed opaque identifier.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateDisplayName(string displayName)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add("Display name must not be empty.");
            }
            else if (displayName.Length > MaxDisplayNameLength)
            {
                errors.Add($"Display name must be at most {MaxDisplayNameLength} characters.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateGuildName(string guildName)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(guildName))
            {
                errors.Add("Guild name must not be empty.");
            }
            else if (guildName.Length > MaxGuildNameLength)
            {
                errors.Add($"Guild name must be at most {MaxGuildNameLength} characters.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateChatMessage(string message)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(message))
            {
                errors.Add("Chat message must not be empty.");
            }
            else if (message.Length > MaxChatMessageLength)
            {
                errors.Add($"Chat message must be at most {MaxChatMessageLength} characters.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidatePagination(int pageSize, int pageNumber)
        {
            var errors = new List<string>();
            if (pageSize < MinPageSize || pageSize > MaxPageSize)
            {
                errors.Add($"Page size must be between {MinPageSize} and {MaxPageSize}.");
            }

            if (pageNumber < MinPageNumber)
            {
                errors.Add($"Page number must be at least {MinPageNumber}.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateTimestamp(long value, string fieldName)
        {
            var errors = new List<string>();
            if (value <= 0)
            {
                errors.Add($"{fieldName} must be a positive Unix UTC millisecond timestamp.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateRequest<T>(T request, string fieldName) where T : class
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add($"{fieldName} must not be null.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateCreateDirectMessageRequest(CreateDirectMessageRequestRequest request, string actorAccountId, bool isBlocked)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(actorAccountId, "actorAccountId").Errors);
                errors.AddRange(ValidateOpaqueId(request.targetAccountId, "targetAccountId").Errors);

                if (string.Equals(actorAccountId, request.targetAccountId, StringComparison.Ordinal))
                {
                    errors.Add("Self-message requests are not allowed.");
                }

                if (isBlocked)
                {
                    errors.Add("Blocked users cannot create direct-message requests.");
                }

                if (string.IsNullOrWhiteSpace(request.text) && string.IsNullOrWhiteSpace(request.targetAccountId))
                {
                    errors.Add("Direct-message request text or target account is required.");
                }

                if (!string.IsNullOrWhiteSpace(request.text) && request.text.Length > MaxDirectMessageLength)
                {
                    errors.Add($"Direct-message request text must be at most {MaxDirectMessageLength} characters.");
                }
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateDirectMessageRequestStateTransition(
            DirectMessageRequestStatus currentStatus,
            DirectMessageRequestStatus allowedStatus,
            DirectMessageRequestStatus targetStatus,
            string actionName)
        {
            var errors = new List<string>();

            if (targetStatus == DirectMessageRequestStatus.None)
            {
                errors.Add($"{actionName} must specify a concrete target status.");
                return new ValidationResult(errors);
            }

            if (targetStatus != allowedStatus)
            {
                errors.Add($"Invalid request-state transition from {currentStatus} to {targetStatus} for {actionName}.");
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateSendDirectMessageRequest(
            SendDirectMessageRequest request,
            string actorAccountId,
            bool isUnrestrictedEligible,
            bool isPresetMessageOnlyEligible,
            bool isBlocked)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(actorAccountId, "actorAccountId").Errors);
                errors.AddRange(ValidateOpaqueId(request.conversationId, "conversationId").Errors);

                if (isBlocked)
                {
                    errors.Add("Blocked users cannot send direct messages.");
                }

                bool hasText = !string.IsNullOrWhiteSpace(request.body);
                bool hasPreset = !string.IsNullOrWhiteSpace(request.presetMessageId);

                if (!isUnrestrictedEligible && !isPresetMessageOnlyEligible)
                {
                    errors.Add("This account is not eligible to send direct messages.");
                }

                if (isPresetMessageOnlyEligible && !hasPreset && hasText)
                {
                    errors.Add("This account may only send approved preset messages.");
                }

                if (!isPresetMessageOnlyEligible && !isUnrestrictedEligible && hasText)
                {
                    errors.Add("This account cannot send arbitrary direct-message text.");
                }

                if (isUnrestrictedEligible && hasText && request.body.Length > MaxDirectMessageLength)
                {
                    errors.Add($"Direct message text must be at most {MaxDirectMessageLength} characters.");
                }

                if (isPresetMessageOnlyEligible && hasPreset && string.IsNullOrWhiteSpace(request.presetMessageId))
                {
                    errors.Add("Preset-message IDs must be non-empty when provided.");
                }

                if (!hasText && !hasPreset)
                {
                    errors.Add("Direct messages require either text or an approved preset-message ID.");
                }

                if (hasText && hasPreset)
                {
                    errors.Add("A direct message cannot include both arbitrary text and a preset-message ID.");
                }
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateBlockRequest(BlockAccountRequest request, string actorAccountId)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(actorAccountId, "actorAccountId").Errors);
                errors.AddRange(ValidateOpaqueId(request.blockedAccountId, "blockedAccountId").Errors);

                if (string.Equals(actorAccountId, request.blockedAccountId, StringComparison.Ordinal))
                {
                    errors.Add("Self-blocking is not allowed.");
                }
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateReportRequest(SubmitReportRequest request, string actorAccountId)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(actorAccountId, "actorAccountId").Errors);
                errors.AddRange(ValidateOpaqueId(request.targetAccountId, "targetAccountId").Errors);

                if (string.Equals(actorAccountId, request.targetAccountId, StringComparison.Ordinal))
                {
                    errors.Add("Self-reporting is not allowed.");
                }

                if (string.IsNullOrWhiteSpace(request.reportReason))
                {
                    errors.Add("Report reason must not be empty.");
                }
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateTransferGuildOwnershipRequest(TransferGuildOwnershipRequest request)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(request.guildId, "guildId").Errors);
                errors.AddRange(ValidateOpaqueId(request.destinationAccountId, "destinationAccountId").Errors);
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateRemoveGuildMemberRequest(RemoveGuildMemberRequest request)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(request.guildId, "guildId").Errors);
                errors.AddRange(ValidateOpaqueId(request.membershipId, "membershipId").Errors);
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateAssignGuildRoleRequest(AssignGuildRoleRequest request)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(request.guildId, "guildId").Errors);
                errors.AddRange(ValidateOpaqueId(request.membershipId, "membershipId").Errors);
                errors.AddRange(ValidateOpaqueId(request.roleId, "roleId").Errors);
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateDisbandGuildRequest(DisbandGuildRequest request)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(request.guildId, "guildId").Errors);
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateModerateChatMessageRequest(ModerateChatMessageRequest request)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(request.guildId, "guildId").Errors);
                errors.AddRange(ValidateOpaqueId(request.channelId, "channelId").Errors);
                errors.AddRange(ValidateOpaqueId(request.messageId, "messageId").Errors);

                if (request.action == default(ChatModerationAction) || request.action == ChatModerationAction.None)
                {
                    errors.Add("Moderation action must be a concrete typed action.");
                }
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateReviewReportRequest(ReviewReportRequest request)
        {
            var errors = new List<string>();
            errors.AddRange(ValidateRequest(request, nameof(request)).Errors);

            if (request != null)
            {
                errors.AddRange(ValidateOpaqueId(request.reportId, "reportId").Errors);

                if (request.resolution == default(ReportResolution) || request.resolution == ReportResolution.None)
                {
                    errors.Add("Resolution must be a concrete typed resolution.");
                }
            }

            return new ValidationResult(errors);
        }

        public static ValidationResult ValidateRetentionPolicy(GuildChatRetentionPolicy policy)
        {
            var errors = new List<string>();
            if (policy == null)
            {
                errors.Add("Retention policy must not be null.");
                return new ValidationResult(errors);
            }

            if (!policy.enabled)
            {
                return new ValidationResult(errors);
            }

            if (policy.retentionDays < MinRetentionDays || policy.retentionDays > MaxRetentionDays)
            {
                errors.Add($"Retention days must be between {MinRetentionDays} and {MaxRetentionDays}.");
            }

            return new ValidationResult(errors);
        }
    }

    public class ValidationResult
    {
        public ValidationResult(IEnumerable<string> errors)
        {
            Errors = (errors ?? Array.Empty<string>()).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
        }

        public List<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }
}
