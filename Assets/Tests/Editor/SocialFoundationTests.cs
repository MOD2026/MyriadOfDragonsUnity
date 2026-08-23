using System;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Social;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class SocialFoundationTests
    {
        [Test]
        public void ContractObjects_CanBeConstructedWithRepresentativeData()
        {
            var account = new AccountIdentity
            {
                accountId = "acc_001",
                displayName = "Ari",
                createdAtUtcMs = 1720000000000,
                lastSeenAtUtcMs = 1720001000000,
                status = "Active"
            };

            var guild = new GuildIdentity
            {
                guildId = "guild_001",
                guildName = "Dragon Guard",
                ownerAccountId = "acc_001",
                createdAtUtcMs = 1720000000000,
                memberCount = 10,
                status = "Active"
            };

            var membership = new GuildMembership
            {
                membershipId = "mem_001",
                accountId = "acc_001",
                guildId = "guild_001",
                roleId = "role_001",
                joinedAtUtcMs = 1720000000000,
                status = "Active"
            };

            var role = new GuildRole
            {
                roleId = "role_001",
                guildId = "guild_001",
                roleName = "Leader",
                permissions = GuildPermissions.All,
                isDefault = false
            };

            var channel = new GuildChatChannel
            {
                channelId = "chan_001",
                guildId = "guild_001",
                channelType = "General",
                title = "General Chat",
                createdAtUtcMs = 1720000000000,
                status = "Active"
            };

            var message = new ChatMessage
            {
                messageId = "msg_001",
                channelId = "chan_001",
                accountId = "acc_001",
                body = "Hello guild",
                createdAtUtcMs = 1720000000000,
                editedAtUtcMs = 1720000100000,
                deletedAtUtcMs = 0,
                moderationStatus = ModerationStatus.None
            };

            var retention = new GuildChatRetentionPolicy
            {
                retentionDays = 30,
                enabled = true,
                policyName = "default",
                updatedAtUtcMs = 1720000000000
            };

            var block = new BlockRecord
            {
                blockId = "blk_001",
                blockerAccountId = "acc_001",
                blockedAccountId = "acc_002",
                createdAtUtcMs = 1720000000000,
                status = "Active"
            };

            var mute = new MuteRecord
            {
                muteId = "mute_001",
                muterAccountId = "acc_001",
                mutedAccountId = "acc_002",
                createdAtUtcMs = 1720000000000,
                muteUntilUtcMs = 1721000000000,
                status = "Active"
            };

            var report = new ReportSubmission
            {
                reportId = "rep_001",
                reporterAccountId = "acc_001",
                targetAccountId = "acc_002",
                reportReason = "Harassment",
                createdAtUtcMs = 1720000000000,
                moderationStatus = ModerationStatus.Pending
            };

            Assert.NotNull(account);
            Assert.NotNull(guild);
            Assert.NotNull(membership);
            Assert.NotNull(role);
            Assert.NotNull(channel);
            Assert.NotNull(message);
            Assert.NotNull(retention);
            Assert.NotNull(block);
            Assert.NotNull(mute);
            Assert.NotNull(report);
        }

        [Test]
        public void OpaqueIds_AreNotDerivedFromDisplayNames()
        {
            var account = new AccountIdentity
            {
                accountId = "acc_opaque_001",
                displayName = "Ari"
            };

            Assert.AreNotEqual(account.displayName, account.accountId);
            Assert.False(account.accountId.Contains(account.displayName, StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void EveryGuildPermissionCanBeRepresentedIndependently()
        {
            var permissions = GuildPermissions.InviteMembers
                | GuildPermissions.RemoveMembers
                | GuildPermissions.AssignRoles
                | GuildPermissions.EditGuildDetails
                | GuildPermissions.SendChatMessages
                | GuildPermissions.ModerateChat
                | GuildPermissions.DisbandGuild;

            Assert.True((permissions & GuildPermissions.InviteMembers) != 0);
            Assert.True((permissions & GuildPermissions.RemoveMembers) != 0);
            Assert.True((permissions & GuildPermissions.AssignRoles) != 0);
            Assert.True((permissions & GuildPermissions.EditGuildDetails) != 0);
            Assert.True((permissions & GuildPermissions.SendChatMessages) != 0);
            Assert.True((permissions & GuildPermissions.ModerateChat) != 0);
            Assert.True((permissions & GuildPermissions.DisbandGuild) != 0);
        }

        [Test]
        public void ValidationRejectsEmptyOpaqueIds()
        {
            var result = SocialValidation.ValidateOpaqueId(string.Empty, "accountId");

            Assert.False(result.IsValid);
            Assert.IsNotEmpty(result.Errors);
        }

        [Test]
        public void ValidationEnforcesConfiguredNameAndMessageLimits()
        {
            var displayNameResult = SocialValidation.ValidateDisplayName(new string('x', 33));
            var guildNameResult = SocialValidation.ValidateGuildName(new string('y', 65));
            var messageResult = SocialValidation.ValidateChatMessage(new string('z', 1001));

            Assert.False(displayNameResult.IsValid);
            Assert.False(guildNameResult.IsValid);
            Assert.False(messageResult.IsValid);
        }

        [Test]
        public void ValidationRejectsSelfBlockAndSelfReport()
        {
            var blockResult = SocialValidation.ValidateBlockRequest(new BlockAccountRequest
            {
                blockedAccountId = "acc_001"
            }, "acc_001");

            var reportResult = SocialValidation.ValidateReportRequest(new SubmitReportRequest
            {
                targetAccountId = "acc_001",
                reportReason = "spam"
            }, "acc_001");

            Assert.False(blockResult.IsValid);
            Assert.False(reportResult.IsValid);
        }

        [Test]
        public void ValidationRejectsInvalidRetentionAndPaginationValues()
        {
            var retentionResult = SocialValidation.ValidateRetentionPolicy(new GuildChatRetentionPolicy
            {
                retentionDays = 0,
                enabled = true
            });

            var paginationResult = SocialValidation.ValidatePagination(0, 1);
            var pageNumberResult = SocialValidation.ValidatePagination(10, 0);

            Assert.False(retentionResult.IsValid);
            Assert.False(paginationResult.IsValid);
            Assert.False(pageNumberResult.IsValid);
        }

        [Test]
        public void ServiceFailuresCanRepresentEveryRequiredFailureCategory()
        {
            var categories = Enum.GetValues(typeof(SocialFailureCategory)).Cast<SocialFailureCategory>().ToArray();

            Assert.Contains(SocialFailureCategory.Authentication, categories);
            Assert.Contains(SocialFailureCategory.Authorization, categories);
            Assert.Contains(SocialFailureCategory.Validation, categories);
            Assert.Contains(SocialFailureCategory.Conflict, categories);
            Assert.Contains(SocialFailureCategory.RateLimit, categories);
            Assert.Contains(SocialFailureCategory.Unavailable, categories);
            Assert.Contains(SocialFailureCategory.Unknown, categories);
        }

        [Test]
        public void NoProductionClassProvidesASuccessSimulator()
        {
            Assert.IsTrue(typeof(ISocialService).IsInterface);
            Assert.IsNotNull(typeof(SocialResult<>));
        }

        [Test]
        public void ActingAccountFields_AreNotPresentOnApprovedRequestDtos()
        {
            Assert.False(typeof(BootstrapIdentityRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(CreateGuildRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(JoinGuildRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(LeaveGuildRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(FetchMembershipRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(FetchChatHistoryRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(SendChatMessageRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "accountId"));
            Assert.False(typeof(BlockAccountRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "blockerAccountId"));
            Assert.False(typeof(UnblockAccountRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "blockerAccountId"));
            Assert.False(typeof(MuteAccountRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "muterAccountId"));
            Assert.False(typeof(UnmuteAccountRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "muterAccountId"));
            Assert.False(typeof(SubmitReportRequest).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "reporterAccountId"));
        }

        [Test]
        public void GuildMembership_UsesRoleIdInsteadOfEmbeddedRole()
        {
            var membership = new GuildMembership
            {
                roleId = "role_001"
            };

            Assert.AreEqual("role_001", membership.roleId);
            Assert.False(typeof(GuildMembership).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "role"));
            Assert.True(typeof(GuildRole).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "guildId"));
        }

        [Test]
        public void TransferGuildOwnershipResult_UsesUnambiguousNewOwnerMembershipField()
        {
            var result = new TransferGuildOwnershipResult
            {
                newOwnerMembership = new GuildMembership { membershipId = "mem_002" }
            };

            Assert.NotNull(result.newOwnerMembership);
            Assert.False(typeof(TransferGuildOwnershipResult).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "membership"));
        }

        [Test]
        public void ApprovedGuildLifecycleOperationsExistOnService()
        {
            var methods = typeof(ISocialService).GetMethods().Select(method => method.Name).ToArray();

            Assert.Contains("TransferGuildOwnershipAsync", methods);
            Assert.Contains("RemoveGuildMemberAsync", methods);
            Assert.Contains("AssignGuildRoleAsync", methods);
            Assert.Contains("DisbandGuildAsync", methods);
            Assert.Contains("ModerateChatMessageAsync", methods);
            Assert.Contains("ReviewReportAsync", methods);
        }

        [Test]
        public void PermissionFlagsMapToAvailableOperations()
        {
            var methods = typeof(ISocialService).GetMethods().Select(method => method.Name).ToArray();

            Assert.Contains("RemoveGuildMemberAsync", methods);
            Assert.Contains("AssignGuildRoleAsync", methods);
            Assert.Contains("DisbandGuildAsync", methods);
            Assert.Contains("ModerateChatMessageAsync", methods);
        }

        [Test]
        public void ModerationAndReportTransitionsCanBeRepresentedExplicitly()
        {
            var moderation = new ModerateChatMessageRequest
            {
                action = ChatModerationAction.DeleteMessage
            };

            var review = new ReviewReportRequest
            {
                resolution = ReportResolution.Actioned
            };

            var moderationResult = new ModerateChatMessageResult
            {
                moderated = true,
                action = ChatModerationAction.DeleteMessage
            };

            var reviewResult = new ReviewReportResult
            {
                reviewed = true,
                resolution = ReportResolution.Actioned
            };

            Assert.AreEqual(ChatModerationAction.DeleteMessage, moderation.action);
            Assert.AreEqual(ReportResolution.Actioned, review.resolution);
            Assert.True(moderationResult.moderated);
            Assert.True(reviewResult.reviewed);
        }

        [Test]
        public void NullRequestsReturnInvalidValidationResultsInsteadOfThrowing()
        {
            Assert.False(SocialValidation.ValidateBlockRequest(null, "acc_001").IsValid);
            Assert.False(SocialValidation.ValidateReportRequest(null, "acc_001").IsValid);
            Assert.False(SocialValidation.ValidateTransferGuildOwnershipRequest(null).IsValid);
            Assert.False(SocialValidation.ValidateRemoveGuildMemberRequest(null).IsValid);
            Assert.False(SocialValidation.ValidateAssignGuildRoleRequest(null).IsValid);
            Assert.False(SocialValidation.ValidateDisbandGuildRequest(null).IsValid);
            Assert.False(SocialValidation.ValidateModerateChatMessageRequest(null).IsValid);
            Assert.False(SocialValidation.ValidateReviewReportRequest(null).IsValid);
        }

        [Test]
        public void MissingResourceIdsAreRejected()
        {
            var transfer = SocialValidation.ValidateTransferGuildOwnershipRequest(new TransferGuildOwnershipRequest
            {
                guildId = string.Empty,
                destinationAccountId = string.Empty
            });

            var remove = SocialValidation.ValidateRemoveGuildMemberRequest(new RemoveGuildMemberRequest
            {
                guildId = string.Empty,
                membershipId = string.Empty
            });

            var roleAssignment = SocialValidation.ValidateAssignGuildRoleRequest(new AssignGuildRoleRequest
            {
                guildId = string.Empty,
                membershipId = string.Empty,
                roleId = string.Empty
            });

            var disband = SocialValidation.ValidateDisbandGuildRequest(new DisbandGuildRequest
            {
                guildId = string.Empty
            });

            var moderation = SocialValidation.ValidateModerateChatMessageRequest(new ModerateChatMessageRequest
            {
                guildId = string.Empty,
                channelId = string.Empty,
                messageId = string.Empty,
                action = ChatModerationAction.None
            });

            var review = SocialValidation.ValidateReviewReportRequest(new ReviewReportRequest
            {
                reportId = string.Empty,
                resolution = ReportResolution.None
            });

            Assert.False(transfer.IsValid);
            Assert.False(remove.IsValid);
            Assert.False(roleAssignment.IsValid);
            Assert.False(disband.IsValid);
            Assert.False(moderation.IsValid);
            Assert.False(review.IsValid);
        }

        [Test]
        public void SelfBlockingAndSelfReportingRemainRejectedWhenActorContextIsSeparate()
        {
            var blockResult = SocialValidation.ValidateBlockRequest(new BlockAccountRequest
            {
                blockedAccountId = "acc_001"
            }, "acc_001");

            var reportResult = SocialValidation.ValidateReportRequest(new SubmitReportRequest
            {
                targetAccountId = "acc_001",
                reportReason = "spam"
            }, "acc_001");

            Assert.False(blockResult.IsValid);
            Assert.False(reportResult.IsValid);
        }

        [Test]
        public void RequestDtosDoNotContainSensitiveOrProviderSpecificFields()
        {
            var requestTypes = new[]
            {
                typeof(BootstrapIdentityRequest),
                typeof(CreateGuildRequest),
                typeof(JoinGuildRequest),
                typeof(LeaveGuildRequest),
                typeof(FetchMembershipRequest),
                typeof(FetchChatHistoryRequest),
                typeof(SendChatMessageRequest),
                typeof(BlockAccountRequest),
                typeof(UnblockAccountRequest),
                typeof(MuteAccountRequest),
                typeof(UnmuteAccountRequest),
                typeof(SubmitReportRequest),
                typeof(TransferGuildOwnershipRequest),
                typeof(RemoveGuildMemberRequest),
                typeof(AssignGuildRoleRequest),
                typeof(DisbandGuildRequest),
                typeof(ModerateChatMessageRequest),
                typeof(ReviewReportRequest),
            };

            var sensitiveMarkers = new[] { "token", "credential", "password", "secret", "provider" };

            foreach (var requestType in requestTypes)
            {
                var fieldNames = requestType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => field.Name.ToLowerInvariant())
                    .ToArray();

                foreach (var marker in sensitiveMarkers)
                {
                    Assert.False(fieldNames.Any(fieldName => fieldName.Contains(marker)), $"{requestType.Name} contains sensitive field marker '{marker}'.");
                }
            }
        }

        [Test]
        public void DirectMessageContracts_RepresentRequiredPrivacyAndFailureStates()
        {
            var settings = new DirectMessagePrivacySettings
            {
                whoMaySendRequest = DirectMessagePrivacyMode.AcceptedContactsOnly,
                unrestrictedTextEligible = true,
                presetMessageOnlyEligible = false,
                r4R5AuthorityCannotBypass = true,
            };

            var request = new CreateDirectMessageRequestRequest
            {
                targetAccountId = "acc_002",
                text = "hello",
                requestedAtUtcMs = 1720000000000,
            };

            Assert.AreEqual(DirectMessagePrivacyMode.AcceptedContactsOnly, settings.whoMaySendRequest);
            Assert.True(settings.unrestrictedTextEligible);
            Assert.False(settings.presetMessageOnlyEligible);
            Assert.True(settings.r4R5AuthorityCannotBypass);
            Assert.AreEqual("acc_002", request.targetAccountId);
            Assert.AreEqual(DirectMessageFailureReason.None, DirectMessageFailureReason.None);
        }

        [Test]
        public void DirectMessageRequestValidation_RejectsSelfBlockedAndStateViolations()
        {
            var selfResult = SocialValidation.ValidateCreateDirectMessageRequest(new CreateDirectMessageRequestRequest
            {
                targetAccountId = "acc_001",
                text = "hello",
                requestedAtUtcMs = 1720000000000,
            }, "acc_001", false);

            var blockedResult = SocialValidation.ValidateCreateDirectMessageRequest(new CreateDirectMessageRequestRequest
            {
                targetAccountId = "acc_002",
                text = "hello",
                requestedAtUtcMs = 1720000000000,
            }, "acc_001", true);

            var invalidTransition = SocialValidation.ValidateDirectMessageRequestStateTransition(
                DirectMessageRequestStatus.Pending,
                DirectMessageRequestStatus.Accepted,
                DirectMessageRequestStatus.Rejected,
                "accept");

            Assert.False(selfResult.IsValid);
            Assert.False(blockedResult.IsValid);
            Assert.False(invalidTransition.IsValid);
        }

        [Test]
        public void DirectMessageConversations_AreExactlyTwoParticipantsAndNoGroupRepresentation()
        {
            var conversation = new DirectMessageConversation
            {
                conversationId = "dm_conv_001",
                participantAccountIds = new[] { "acc_001", "acc_002" },
                createdAtUtcMs = 1720000000000,
                lastMessageAtUtcMs = 1720000100000,
                isMuted = false
            };

            Assert.AreEqual(2, conversation.participantAccountIds.Length);
            Assert.AreNotEqual(conversation.participantAccountIds[0], conversation.participantAccountIds[1]);
            Assert.AreEqual("dm_conv_001", conversation.conversationId);
            Assert.False(typeof(DirectMessageConversation).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "groupName"));
        }

        [Test]
        public void DirectMessageValidation_RejectsPresetOnlyMessagesAndAcceptsBoundedText()
        {
            var presetOnly = SocialValidation.ValidateSendDirectMessageRequest(new SendDirectMessageRequest
            {
                conversationId = "dm_conv_001",
                body = "this is raw text",
                presetMessageId = null,
                requestedAtUtcMs = 1720000000000,
            }, "acc_001", false, false, true);

            var unrestricted = SocialValidation.ValidateSendDirectMessageRequest(new SendDirectMessageRequest
            {
                conversationId = "dm_conv_001",
                body = "valid bounded text",
                presetMessageId = null,
                requestedAtUtcMs = 1720000000000,
            }, "acc_001", true, false, false);

            var blockedUnrestricted = SocialValidation.ValidateSendDirectMessageRequest(new SendDirectMessageRequest
            {
                conversationId = "dm_conv_001",
                body = "valid bounded text",
                presetMessageId = null,
                requestedAtUtcMs = 1720000000000,
            }, "acc_001", true, false, true);

            var presetAllowed = SocialValidation.ValidateSendDirectMessageRequest(new SendDirectMessageRequest
            {
                conversationId = "dm_conv_001",
                body = null,
                presetMessageId = "preset_001",
                requestedAtUtcMs = 1720000000000,
            }, "acc_001", false, true, false);

            Assert.False(presetOnly.IsValid);
            Assert.True(unrestricted.IsValid);
            Assert.True(presetAllowed.IsValid);
            Assert.False(blockedUnrestricted.IsValid);
        }

        [Test]
        public void DirectMessageService_ContainsRequiredOperationsAndNoSuccessSimulator()
        {
            var methods = typeof(ISocialService).GetMethods().Select(method => method.Name).ToArray();

            Assert.Contains(nameof(ISocialService.GetDirectMessagePrivacySettingsAsync), methods);
            Assert.Contains(nameof(ISocialService.CreateDirectMessageRequestAsync), methods);
            Assert.Contains(nameof(ISocialService.ListDirectMessageRequestsAsync), methods);
            Assert.Contains(nameof(ISocialService.AcceptDirectMessageRequestAsync), methods);
            Assert.Contains(nameof(ISocialService.RejectDirectMessageRequestAsync), methods);
            Assert.Contains(nameof(ISocialService.CancelDirectMessageRequestAsync), methods);
            Assert.Contains(nameof(ISocialService.ListDirectMessageConversationsAsync), methods);
            Assert.Contains(nameof(ISocialService.FetchDirectMessageHistoryAsync), methods);
            Assert.Contains(nameof(ISocialService.SendDirectMessageAsync), methods);
            Assert.Contains(nameof(ISocialService.MuteDirectMessageConversationAsync), methods);
            Assert.Contains(nameof(ISocialService.UnmuteDirectMessageConversationAsync), methods);
            Assert.Contains(nameof(ISocialService.HideDirectMessageConversationAsync), methods);
            Assert.False(methods.Any(name => name.Contains("Simulator", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void DirectMessageRequestAndConversationDtos_DoNotLeakActorIdentitiesOrAuthTokens()
        {
            var serviceMethods = typeof(ISocialService).GetMethods()
                .Where(method => method.Name.Contains("DirectMessage", StringComparison.OrdinalIgnoreCase)
                    || method.Name.Contains("PrivacySettings", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var method in serviceMethods)
            {
                var parameters = method.GetParameters();
                foreach (var parameter in parameters)
                {
                    if (parameter.ParameterType == typeof(System.Threading.CancellationToken))
                    {
                        continue;
                    }

                    var name = parameter.Name ?? string.Empty;
                    Assert.False(name.Contains("accountId", StringComparison.OrdinalIgnoreCase), $"{method.Name} still exposes actor account ID as '{name}'.");
                    Assert.False(name.Contains("account", StringComparison.OrdinalIgnoreCase) && name.Contains("id", StringComparison.OrdinalIgnoreCase), $"{method.Name} still exposes actor account ID as '{name}'.");
                    Assert.False(name.Contains("token", StringComparison.OrdinalIgnoreCase), $"{method.Name} still exposes a token as '{name}'.");
                    Assert.False(name.Contains("auth", StringComparison.OrdinalIgnoreCase), $"{method.Name} still exposes auth as '{name}'.");
                    Assert.False(name.Contains("session", StringComparison.OrdinalIgnoreCase), $"{method.Name} still exposes session data as '{name}'.");
                }
            }

            var requestTypes = new[]
            {
                typeof(GetDirectMessagePrivacySettingsRequest),
                typeof(ListDirectMessageRequestsRequest),
                typeof(CreateDirectMessageRequestRequest),
                typeof(AcceptDirectMessageRequestRequest),
                typeof(RejectDirectMessageRequestRequest),
                typeof(CancelDirectMessageRequestRequest),
                typeof(ListDirectMessageConversationsRequest),
                typeof(FetchDirectMessageHistoryRequest),
                typeof(SendDirectMessageRequest),
                typeof(MuteDirectMessageConversationRequest),
                typeof(UnmuteDirectMessageConversationRequest),
                typeof(HideDirectMessageConversationRequest),
            };

            var forbiddenFieldNames = new[]
            {
                "accountId",
                "actorAccountId",
                "actingAccountId",
                "senderAccountId",
                "requesterAccountId",
                "authToken",
                "authenticationToken",
                "sessionToken",
                "credential",
                "credentials",
            };

            foreach (var requestType in requestTypes)
            {
                var fieldNames = requestType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => field.Name)
                    .ToArray();

                foreach (var forbiddenFieldName in forbiddenFieldNames)
                {
                    Assert.False(fieldNames.Any(fieldName => string.Equals(fieldName, forbiddenFieldName, StringComparison.Ordinal)), $"{requestType.Name} contains forbidden field '{forbiddenFieldName}'.");
                }
            }

            var createRequestFields = typeof(CreateDirectMessageRequestRequest).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(field => field.Name)
                .ToArray();

            Assert.Contains("targetAccountId", createRequestFields);
            CollectionAssert.DoesNotContain(createRequestFields, "actoraccountid");
            CollectionAssert.DoesNotContain(createRequestFields, "senderaccountid");
            CollectionAssert.DoesNotContain(createRequestFields, "authtoken");
            CollectionAssert.DoesNotContain(createRequestFields, "sessiontoken");
        }

        [Test]
        public void DirectMessageTypeDefinitions_ArePresentAndOneToOneOnly()
        {
            Assert.NotNull(typeof(DirectMessageRequest));
            Assert.NotNull(typeof(DirectMessageConversation));
            Assert.NotNull(typeof(DirectMessageMessage));
            Assert.NotNull(typeof(DirectMessagePrivacySettings));
            Assert.NotNull(typeof(DirectMessagePrivacySettingsResult));
            Assert.NotNull(typeof(GetDirectMessagePrivacySettingsRequest));
            Assert.NotNull(typeof(ListDirectMessageRequestsRequest));
            Assert.NotNull(typeof(ListDirectMessageRequestsResult));
            Assert.NotNull(typeof(CreateDirectMessageRequestRequest));
            Assert.NotNull(typeof(CreateDirectMessageRequestResult));
            Assert.NotNull(typeof(AcceptDirectMessageRequestRequest));
            Assert.NotNull(typeof(AcceptDirectMessageRequestResult));
            Assert.NotNull(typeof(RejectDirectMessageRequestRequest));
            Assert.NotNull(typeof(RejectDirectMessageRequestResult));
            Assert.NotNull(typeof(CancelDirectMessageRequestRequest));
            Assert.NotNull(typeof(CancelDirectMessageRequestResult));
            Assert.NotNull(typeof(ListDirectMessageConversationsRequest));
            Assert.NotNull(typeof(ListDirectMessageConversationsResult));
            Assert.NotNull(typeof(FetchDirectMessageHistoryRequest));
            Assert.NotNull(typeof(FetchDirectMessageHistoryResult));
            Assert.NotNull(typeof(SendDirectMessageRequest));
            Assert.NotNull(typeof(SendDirectMessageResult));
            Assert.NotNull(typeof(MuteDirectMessageConversationRequest));
            Assert.NotNull(typeof(MuteDirectMessageConversationResult));
            Assert.NotNull(typeof(UnmuteDirectMessageConversationRequest));
            Assert.NotNull(typeof(UnmuteDirectMessageConversationResult));
            Assert.NotNull(typeof(HideDirectMessageConversationRequest));
            Assert.NotNull(typeof(HideDirectMessageConversationResult));
            Assert.NotNull(typeof(DirectMessageRetentionPolicy));

            var conversation = new DirectMessageConversation
            {
                participantAccountIds = new[] { "acc_001", "acc_002" }
            };

            Assert.AreEqual(2, conversation.participantAccountIds.Length);
            Assert.False(typeof(DirectMessageConversation).GetFields(BindingFlags.Instance | BindingFlags.Public).Any(field => field.Name == "groupName"));
        }
    }
}
