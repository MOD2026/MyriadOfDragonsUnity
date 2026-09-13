using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine.TestTools;

namespace MyriadOfDragons.Tests.PlayMode
{
    /// <summary>
    /// Beta safety net: permanent smoke/contract coverage for the two deployed backend modules
    /// (Friends, Bazaar) against the real nonprod-validation environment. Unlike
    /// FriendsLiveValidationPlayModeTests/FriendsLiveSevenCheckPlayModeTests/
    /// FriendsBackfillLiveExecutionPlayModeTests (each explicitly documented as a one-off capture
    /// for a specific task), this suite is meant to be re-run before every beta build/release as a
    /// standing regression gate - it re-verifies exactly the contract shape a real client depends
    /// on: success response shapes, the server's real documented error codes (not guessed ones -
    /// see CloudCode/Friends/FriendsOperations.cs and CloudCode/Bazaar/BazaarOperations.cs), and
    /// SendDailyGift's once-per-day idempotency (the exact rule PRODUCTIVE-TASK-BE-FRIENDS-LIVE-007
    /// first proved live).
    ///
    /// TargetEnvironmentName below is a real, checked runtime assertion, not a formality: this
    /// suite performs real writes (AddFriend/AcceptFriend/SendDailyGift/RemoveFriend) against
    /// whatever environment it targets. If a future edit ever changed the constant to "production"
    /// - the UGS default environment (see BuildEnvironmentGuard.cs, Assets/Editor/) - this
    /// assertion fails the whole suite immediately, before UnityServices.InitializeAsync or any
    /// network call, rather than silently exercising the real backend's real player-facing
    /// production data. The same assertion is checked again immediately before the artifact file
    /// is written (see WriteArtifact) as defense in depth for that specific side effect.
    ///
    /// REPEATABILITY (2026-09-13): the two Unity Authentication profiles this suite signs into are
    /// suffixed with a fresh GUID every run (_runId below), not a fixed "beta-smoke-a"/
    /// "beta-smoke-b" pair. A fixed pair meant every run - and every seat/CI run concurrently -
    /// resumed the SAME two backend accounts, so a prior run's leftover Pending/Accepted
    /// friendship (or a concurrent run's in-flight one) could corrupt this run's real writes; the
    /// first version of this suite hit exactly that with a same-process bug (a hardcoded id
    /// instead of the live signed-in one) that a fixed identity pair made much harder to diagnose,
    /// since the corruption persisted server-side across reruns. A fresh, never-reused identity
    /// pair per run makes every run start from a genuinely clean relationship graph with zero
    /// cross-run/cross-seat interference, and needs no cleanup step to achieve that guarantee -
    /// RemoveFriend at the end is real endpoint contract coverage, not a workaround.
    ///
    /// ARTIFACT (2026-09-13): each run writes a machine-readable JSON result file
    /// (beta_smoke_artifact.json, project root) so CI/a release checklist can consume pass/fail
    /// and error codes without scraping the Unity test-runner log. Deliberately narrow content -
    /// runId, environment, per-check endpoint/pass/errorCode/timestamp only. No player id, access
    /// token, service token, or any other credential/identity value is ever written to this file -
    /// this suite never even holds an AccessToken/ServiceToken itself (CloudCodeService/
    /// AuthenticationService keep those internal), and playerIdA/playerIdB are deliberately kept
    /// log-only (Debug.Log EVIDENCE lines) rather than promoted into the on-disk artifact, since
    /// the artifact is meant to be safe to archive/attach to a release checklist without review.
    /// </summary>
    public sealed class BetaBackendSmokeContractPlayModeTests
    {
        private const string TargetEnvironmentName = "nonprod-validation";
        private const string ForbiddenEnvironmentName = "production";
        private const string ArtifactFileName = "beta_smoke_artifact.json";

        [UnityTest]
        public IEnumerator RunFriendsAndBazaarSmokeContractSuite()
        {
            // Explicit environment assertion that rejects production - runs before anything else,
            // synchronously, so a misconfigured constant can never reach a live network call.
            Assert.AreNotEqual(ForbiddenEnvironmentName, TargetEnvironmentName,
                "Refusing to run: this suite performs real writes and must never target production.");

            var task = RunAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception ?? new Exception("Beta backend smoke/contract suite faulted with no exception.");
            }

            var artifact = task.Result;
            foreach (var check in artifact.checks)
            {
                UnityEngine.Debug.Log($"[{(check.pass ? "PASS" : "FAIL")}] {check.name}{(string.IsNullOrEmpty(check.errorCode) ? "" : " - " + check.errorCode)}");
            }
            UnityEngine.Debug.Log($"===== {artifact.passedChecks}/{artifact.totalChecks} passed =====");

            string artifactPath = WriteArtifact(artifact);
            UnityEngine.Debug.Log($"EVIDENCE ARTIFACT_PATH={artifactPath}");

            Assert.AreEqual(artifact.totalChecks, artifact.passedChecks, $"{artifact.totalChecks - artifact.passedChecks} check(s) failed - see log above.");
        }

        private static async Task<SmokeRunArtifact> RunAsync()
        {
            var results = new List<CheckArtifactEntry>();

            var options = new InitializationOptions();
            options.SetEnvironmentName(TargetEnvironmentName);
            await UnityServices.InitializeAsync(options);

            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
            }

            // Fresh, never-reused profile pair per run - see the class doc comment
            // "REPEATABILITY" note. Two independently-resumable identities - see
            // FriendsLiveSevenCheckPlayModeTests for why SwitchProfile + non-clearing SignOut is
            // required for a real bidirectional two-account flow (Friends' AddFriend/AcceptFriend/
            // SendDailyGift all need a genuine counterpart, not just single-account error paths).
            // Unity Authentication profile names are capped at 30 chars (alphanumeric, '-', '_'
            // only) - "beta-smoke-a-" plus a full 32-hex-char GUID blows that limit (real,
            // observed failure: AuthenticationException "Invalid profile name"). 16 hex chars is
            // still effectively collision-free for this purpose and fits comfortably (7 + 16 = 23).
            string runId = Guid.NewGuid().ToString("N").Substring(0, 16);
            string profileA = "beta-a-" + runId;
            string profileB = "beta-b-" + runId;
            string startedUtc = DateTime.UtcNow.ToString("O");

            AuthenticationService.Instance.SwitchProfile(profileA);
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            string playerIdA = AuthenticationService.Instance.PlayerId;

            AuthenticationService.Instance.SignOut(false);
            AuthenticationService.Instance.SwitchProfile(profileB);
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            string playerIdB = AuthenticationService.Instance.PlayerId;

            UnityEngine.Debug.Log($"EVIDENCE runId={runId}");
            UnityEngine.Debug.Log($"EVIDENCE playerIdA={playerIdA}");
            UnityEngine.Debug.Log($"EVIDENCE playerIdB={playerIdB}");
            UnityEngine.Debug.Log($"EVIDENCE TIMESTAMP_UTC={startedUtc}");
            UnityEngine.Debug.Log($"EVIDENCE ENVIRONMENT={TargetEnvironmentName}");

            await RunFriendsChecksAsync(results, playerIdA, playerIdB, profileA, profileB);
            await RunBazaarChecksAsync(results);

            int passCount = 0;
            foreach (var check in results)
            {
                if (check.pass) passCount++;
            }

            return new SmokeRunArtifact
            {
                runId = runId,
                environment = TargetEnvironmentName,
                startedUtc = startedUtc,
                finishedUtc = DateTime.UtcNow.ToString("O"),
                totalChecks = results.Count,
                passedChecks = passCount,
                checks = results,
            };
        }

        // ---------- Friends: success shapes, error codes, daily-gift idempotency ----------

        private static async Task RunFriendsChecksAsync(List<CheckArtifactEntry> results, string playerIdA, string playerIdB, string profileA, string profileB)
        {
            var gateway = new UnityCloudCodeFriendsGateway();

            // Error-code contract: blank/self/nonexistent targets, matching FriendsOperations.cs's
            // real validation order (INVALID_REQUEST checked before self-target). Uses the
            // CURRENTLY signed-in player's own id for the self-target case - passing a hardcoded
            // playerIdA while signed in as B would silently perform a real AddFriend(B->A) instead
            // of exercising the self-target rejection, corrupting the relationship state for every
            // check that follows (the bug this suite's first version actually hit).
            var blankTarget = await gateway.AddFriendAsync("", CancellationToken.None);
            LogRaw("Friends.AddFriend(blank)", blankTarget);
            AddCheck(results, "Friends: AddFriend(blank target) -> INVALID_REQUEST", "Friends.AddFriend",
                blankTarget?.errorCode == "INVALID_REQUEST", blankTarget?.errorCode);

            var selfTarget = await gateway.AddFriendAsync(AuthenticationService.Instance.PlayerId, CancellationToken.None);
            LogRaw("Friends.AddFriend(self)", selfTarget);
            AddCheck(results, "Friends: AddFriend(self target) -> SELF_TARGET_NOT_ALLOWED", "Friends.AddFriend",
                selfTarget?.errorCode == "SELF_TARGET_NOT_ALLOWED", selfTarget?.errorCode);

            string nonexistentTarget = "beta-smoke-nonexistent-" + Guid.NewGuid().ToString("N");
            var acceptNonexistent = await gateway.AcceptFriendAsync(nonexistentTarget, CancellationToken.None);
            LogRaw("Friends.AcceptFriend(nonexistent)", acceptNonexistent);
            AddCheck(results, "Friends: AcceptFriend(nonexistent) -> REQUEST_NOT_FOUND", "Friends.AcceptFriend",
                acceptNonexistent?.errorCode == "REQUEST_NOT_FOUND", acceptNonexistent?.errorCode);

            var declineNonexistent = await gateway.DeclineFriendAsync(nonexistentTarget, CancellationToken.None);
            LogRaw("Friends.DeclineFriend(nonexistent)", declineNonexistent);
            AddCheck(results, "Friends: DeclineFriend(nonexistent) -> REQUEST_NOT_FOUND", "Friends.DeclineFriend",
                declineNonexistent?.errorCode == "REQUEST_NOT_FOUND", declineNonexistent?.errorCode);

            // Success shape + daily-gift idempotency: real two-account round trip. A and B are a
            // fresh identity pair for this run (see class doc comment), so there is no leftover
            // relationship state to clean up first.
            await SwitchToAsync(playerIdA, playerIdB, profileA, profileB, isA: true);
            var add = await gateway.AddFriendAsync(playerIdB, CancellationToken.None);
            LogRaw("Friends.AddFriend(A->B)", add);
            AddCheck(results, "Friends: AddFriend(A->B) success shape (success=true, status=Pending)", "Friends.AddFriend",
                add != null && add.success && add.status == "Pending", add?.errorCode);

            await SwitchToAsync(playerIdA, playerIdB, profileA, profileB, isA: false);
            var accept = await gateway.AcceptFriendAsync(playerIdA, CancellationToken.None);
            LogRaw("Friends.AcceptFriend(B accepts A)", accept);
            AddCheck(results, "Friends: AcceptFriend(B accepts A) success shape (success=true, status=Accepted)", "Friends.AcceptFriend",
                accept != null && accept.success && accept.status == "Accepted", accept?.errorCode);

            var list = await gateway.ListFriendsAsync(CancellationToken.None);
            LogRawList("Friends.ListFriends(B)", list);
            bool listShapeOk = list != null && list.success && list.friends != null && list.friends.Count > 0 && !string.IsNullOrEmpty(list.friends[0].counterpartAliasId);
            AddCheck(results, "Friends: ListFriends success shape (non-empty alias, no raw account id)", "Friends.ListFriends",
                listShapeOk, list?.errorCode);

            await SwitchToAsync(playerIdA, playerIdB, profileA, profileB, isA: true);
            var gift1 = await gateway.SendDailyGiftAsync(playerIdB, CancellationToken.None);
            LogRaw("Friends.SendDailyGift(A->B) #1", gift1);
            AddCheck(results, "Friends: SendDailyGift #1 today succeeds", "Friends.SendDailyGift",
                gift1 != null && gift1.success, gift1?.errorCode);

            var gift2 = await gateway.SendDailyGiftAsync(playerIdB, CancellationToken.None);
            LogRaw("Friends.SendDailyGift(A->B) #2", gift2);
            AddCheck(results, "Friends: SendDailyGift #2 same day -> GIFT_ALREADY_SENT_TODAY (idempotency)", "Friends.SendDailyGift",
                gift2 != null && !gift2.success && gift2.errorCode == "GIFT_ALREADY_SENT_TODAY", gift2?.errorCode);

            // Cleanup so a repeated suite run doesn't accumulate stale friendships.
            var remove = await gateway.RemoveFriendAsync(playerIdB, CancellationToken.None);
            LogRaw("Friends.RemoveFriend(cleanup)", remove);
            AddCheck(results, "Friends: RemoveFriend cleanup succeeds", "Friends.RemoveFriend",
                remove != null && remove.success, remove?.errorCode);
        }

        private static async Task SwitchToAsync(string playerIdA, string playerIdB, string profileA, string profileB, bool isA)
        {
            string target = isA ? playerIdA : playerIdB;
            if (AuthenticationService.Instance.PlayerId == target)
            {
                return;
            }

            AuthenticationService.Instance.SignOut(false);
            AuthenticationService.Instance.SwitchProfile(isA ? profileA : profileB);
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (AuthenticationService.Instance.PlayerId != target)
            {
                throw new InvalidOperationException(
                    $"Profile switch did not resume the expected identity: expected={target} actual={AuthenticationService.Instance.PlayerId}");
            }
        }

        // ---------- Bazaar: success shapes, error codes ----------

        private static async Task RunBazaarChecksAsync(List<CheckArtifactEntry> results)
        {
            var gateway = new UnityCloudCodeBazaarGateway();

            var wallet = await gateway.GetWalletAsync(CancellationToken.None);
            UnityEngine.Debug.Log($"EVIDENCE RAW Bazaar.GetBazaarWallet = {{\"balanceCredits\":{wallet?.balanceCredits},\"errorCode\":\"{wallet?.errorCode}\"}}");
            AddCheck(results, "Bazaar: GetBazaarWallet success shape (no error)", "Bazaar.GetBazaarWallet",
                wallet != null && string.IsNullOrEmpty(wallet.errorCode), wallet?.errorCode);

            var query = await gateway.QueryListingsAsync(10, null, CancellationToken.None);
            UnityEngine.Debug.Log($"EVIDENCE RAW Bazaar.QueryBazaarListings = {{\"success\":{query?.success.ToString().ToLowerInvariant()},\"listingCount\":{query?.listings?.Count},\"errorCode\":\"{query?.errorCode}\"}}");
            AddCheck(results, "Bazaar: QueryBazaarListings success shape (success=true)", "Bazaar.QueryBazaarListings",
                query != null && query.success, query?.errorCode);

            string fakeInstanceId = "beta-smoke-instance-" + Guid.NewGuid().ToString("N");
            var list = await gateway.ListItemAsync(fakeInstanceId, 100, CancellationToken.None);
            UnityEngine.Debug.Log($"EVIDENCE RAW Bazaar.ListBazaarItem(fakeInstance) = {{\"success\":{list?.success.ToString().ToLowerInvariant()},\"errorCode\":\"{list?.errorCode}\"}}");
            AddCheck(results, "Bazaar: ListBazaarItem(nonexistent instance) -> INSTANCE_NOT_FOUND", "Bazaar.ListBazaarItem",
                list != null && list.errorCode == "INSTANCE_NOT_FOUND", list?.errorCode);

            string fakeListingId = "beta-smoke-listing-" + Guid.NewGuid().ToString("N");
            var cancel = await gateway.CancelListingAsync(fakeListingId, CancellationToken.None);
            UnityEngine.Debug.Log($"EVIDENCE RAW Bazaar.CancelBazaarListing(fakeListing) = {{\"success\":{cancel?.success.ToString().ToLowerInvariant()},\"errorCode\":\"{cancel?.errorCode}\"}}");
            AddCheck(results, "Bazaar: CancelBazaarListing(nonexistent listing) -> LISTING_NOT_AVAILABLE", "Bazaar.CancelBazaarListing",
                cancel != null && cancel.errorCode == "LISTING_NOT_AVAILABLE", cancel?.errorCode);

            var buy = await gateway.BuyItemAsync(fakeListingId, "beta-smoke-idem-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
            UnityEngine.Debug.Log($"EVIDENCE RAW Bazaar.BuyBazaarItem(fakeListing) = {{\"success\":{buy?.success.ToString().ToLowerInvariant()},\"errorCode\":\"{buy?.errorCode}\"}}");
            AddCheck(results, "Bazaar: BuyBazaarItem(nonexistent listing) -> LISTING_NOT_AVAILABLE", "Bazaar.BuyBazaarItem",
                buy != null && buy.errorCode == "LISTING_NOT_AVAILABLE", buy?.errorCode);
        }

        private static void AddCheck(List<CheckArtifactEntry> results, string name, string endpoint, bool pass, string errorCode)
        {
            results.Add(new CheckArtifactEntry
            {
                name = name,
                endpoint = endpoint,
                pass = pass,
                errorCode = errorCode ?? "",
                timestampUtc = DateTime.UtcNow.ToString("O"),
            });
        }

        /// <summary>Writes the run's result to a machine-readable JSON file at the project root
        /// (next to results.xml/run.log, which tools/run_editmode_tests.ps1 already produces
        /// there) so CI/a release checklist can consume pass/fail and error codes without scraping
        /// the Unity test-runner log. Re-checks the production-refusal condition immediately
        /// before writing, as defense in depth for this specific side effect, and returns the path
        /// actually written (or null if the write itself failed - a failed artifact write must
        /// never turn a real pass/fail result into a false test failure, so it degrades to a
        /// logged warning instead of throwing).</summary>
        private static string WriteArtifact(SmokeRunArtifact artifact)
        {
            if (artifact.environment == ForbiddenEnvironmentName)
            {
                UnityEngine.Debug.LogError("Refusing to write the smoke-run artifact: environment resolved to production.");
                return null;
            }

            try
            {
                string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string path = Path.Combine(projectRoot ?? ".", ArtifactFileName);
                string json = UnityEngine.JsonUtility.ToJson(artifact, prettyPrint: true);
                File.WriteAllText(path, json);
                return path;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Could not write the smoke-run artifact (non-fatal, does not affect the pass/fail result above): {exception.GetType().Name}: {exception.Message}");
                return null;
            }
        }

        private static void LogRaw(string label, FriendGatewayResult result) =>
            UnityEngine.Debug.Log($"EVIDENCE RAW {label} = {{\"success\":{result?.success.ToString().ToLowerInvariant()},\"status\":\"{result?.status}\",\"errorCode\":\"{result?.errorCode}\"}}");

        private static void LogRaw(string label, GiftGatewayResult result) =>
            UnityEngine.Debug.Log($"EVIDENCE RAW {label} = {{\"success\":{result?.success.ToString().ToLowerInvariant()},\"errorCode\":\"{result?.errorCode}\"}}");

        private static void LogRawList(string label, ListFriendsGatewayResult result)
        {
            string aliasOfFirst = result?.friends != null && result.friends.Count > 0 ? result.friends[0].counterpartAliasId : null;
            UnityEngine.Debug.Log($"EVIDENCE RAW {label} = {{\"success\":{result?.success.ToString().ToLowerInvariant()},\"friendCount\":{result?.friends?.Count},\"firstAlias\":\"{aliasOfFirst}\",\"errorCode\":\"{result?.errorCode}\"}}");
        }

        [Serializable]
        private sealed class CheckArtifactEntry
        {
            public string name;
            public string endpoint;
            public bool pass;
            public string errorCode;
            public string timestampUtc;
        }

        [Serializable]
        private sealed class SmokeRunArtifact
        {
            public string runId;
            public string environment;
            public string startedUtc;
            public string finishedUtc;
            public int totalChecks;
            public int passedChecks;
            public List<CheckArtifactEntry> checks = new List<CheckArtifactEntry>();
        }
    }
}
