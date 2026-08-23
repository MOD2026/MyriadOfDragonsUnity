using System;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Social;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class SocialIdentityBootstrapTests
    {
        [Test]
        public async Task BootstrapIdentity_RestoresCachedIdentity()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => Task.FromResult(new IdentityBootstrapSnapshot
            {
                PlayerId = "player_cached_001",
                RestoredCachedSession = true
            })));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.False(result.Data.created);
            Assert.NotNull(result.Data.account);
            Assert.AreEqual("player_cached_001", result.Data.account.accountId);
        }

        [Test]
        public async Task BootstrapIdentity_CreatesAnonymousIdentityWhenNoCacheExists()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => Task.FromResult(new IdentityBootstrapSnapshot
            {
                PlayerId = "player_anon_001",
                RestoredCachedSession = false
            })));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.True(result.Data.created);
            Assert.AreEqual("player_anon_001", result.Data.account.accountId);
        }

        [Test]
        public async Task BootstrapIdentity_RejectsMissingReturnedPlayerIdWithoutFabricatingALocalFallback()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => Task.FromResult(new IdentityBootstrapSnapshot
            {
                PlayerId = string.Empty,
                RestoredCachedSession = false
            })));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.AreEqual(SocialFailureCategory.Validation, result.Failure.Category);
            Assert.IsNull(result.Data);
        }

        [Test]
        public async Task BootstrapIdentity_MapsAuthenticationFailures()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => throw new IdentityBootstrapException(
                SocialFailureCategory.Authentication,
                "Authentication failed.",
                "UNITY_AUTH_AUTHENTICATION_FAILURE")));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.AreEqual(SocialFailureCategory.Authentication, result.Failure.Category);
        }

        [Test]
        public async Task BootstrapIdentity_MapsUnavailableServiceFailures()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => throw new IdentityBootstrapException(
                SocialFailureCategory.Unavailable,
                "Authentication service unavailable.",
                "UNITY_AUTH_SERVICE_UNAVAILABLE")));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.AreEqual(SocialFailureCategory.Unavailable, result.Failure.Category);
        }

        [Test]
        public async Task BootstrapIdentity_MapsSynchronousUnexpectedFailuresWithoutRawExceptionText()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => throw new InvalidOperationException("raw synchronous secret")));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.AreEqual(SocialFailureCategory.Unknown, result.Failure.Category);
            Assert.AreEqual("Unexpected identity bootstrap failure.", result.Failure.Message);
            Assert.AreEqual("UNITY_AUTH_UNEXPECTED_FAILURE", result.Failure.Detail);
            Assert.IsNull(result.Data);
            StringAssert.DoesNotContain("raw synchronous secret", result.Failure.Message);
            StringAssert.DoesNotContain("raw synchronous secret", result.Failure.Detail);
        }

        [Test]
        public void BootstrapIdentity_HonorsCancellation()
        {
            var gateway = new FakeIdentityBootstrapGateway(async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                return new IdentityBootstrapSnapshot
                {
                    PlayerId = "player_cancelled_001",
                    RestoredCachedSession = false
                };
            });

            var service = new UnityAuthenticationSocialService(gateway);
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Task<SocialResult<BootstrapIdentityResult>> task = service.BootstrapIdentityAsync(new BootstrapIdentityRequest
                {
                    displayName = "Ari",
                    requestedAtUtcMs = 1720000000000
                }, cancellationTokenSource.Token);

                cancellationTokenSource.Cancel();

                Assert.CatchAsync<OperationCanceledException>(async () => await task);
            }
        }

        [Test]
        public async Task BootstrapIdentity_SharedBootstrapIsIdempotentAcrossRepeatedAndConcurrentCalls()
        {
            var completion = new TaskCompletionSource<IdentityBootstrapSnapshot>();
            var gateway = new FakeIdentityBootstrapGateway(_ => completion.Task);
            var service = new UnityAuthenticationSocialService(gateway);
            var request = new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            };

            Task<SocialResult<BootstrapIdentityResult>> first = service.BootstrapIdentityAsync(request, CancellationToken.None);
            Task<SocialResult<BootstrapIdentityResult>> second = service.BootstrapIdentityAsync(request, CancellationToken.None);

            Assert.AreEqual(1, gateway.CallCount);

            completion.SetResult(new IdentityBootstrapSnapshot
            {
                PlayerId = "player_shared_001",
                RestoredCachedSession = true
            });

            SocialResult<BootstrapIdentityResult>[] results = await Task.WhenAll(first, second);

            Assert.True(results[0].Success);
            Assert.True(results[1].Success);
            Assert.AreEqual("player_shared_001", results[0].Data.account.accountId);
            Assert.AreEqual("player_shared_001", results[1].Data.account.accountId);

            SocialResult<BootstrapIdentityResult> repeated = await service.BootstrapIdentityAsync(request, CancellationToken.None);
            Assert.AreEqual(1, gateway.CallCount);
            Assert.True(repeated.Success);
            Assert.AreEqual("player_shared_001", repeated.Data.account.accountId);
        }

        [Test]
        public async Task BootstrapIdentity_DoesNotSynthesizeALocalIdentityWhenGatewayReturnsAnError()
        {
            var service = new UnityAuthenticationSocialService(new FakeIdentityBootstrapGateway(_ => throw new IdentityBootstrapException(
                SocialFailureCategory.Unavailable,
                "Authentication service unavailable.",
                "UNITY_AUTH_SERVICE_UNAVAILABLE")));

            SocialResult<BootstrapIdentityResult> result = await service.BootstrapIdentityAsync(new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.AreEqual(SocialFailureCategory.Unavailable, result.Failure.Category);
            Assert.IsNull(result.Data);
        }

        [Test]
        public void UnityAuthenticationFailureClassifier_ClassifiesAuthenticationFailures()
        {
            var exception = new Exception("Authentication token is invalid.", new Exception("raw-secret-value"));

            IdentityBootstrapException classified = UnityAuthenticationFailureClassifier.Classify(exception);

            Assert.AreEqual(SocialFailureCategory.Authentication, classified.Category);
            Assert.AreEqual("UNITY_AUTH_AUTHENTICATION_FAILURE", classified.Detail);
            Assert.AreNotEqual(exception.Message, classified.Detail);
        }

        [Test]
        public void UnityAuthenticationFailureClassifier_ClassifiesUnavailableAndTimeoutFailures()
        {
            var timeoutException = new Exception("Network timeout while calling service.");
            var unavailableException = new Exception("ServiceUnavailable from backend.");

            IdentityBootstrapException timeoutClassified = UnityAuthenticationFailureClassifier.Classify(timeoutException);
            IdentityBootstrapException unavailableClassified = UnityAuthenticationFailureClassifier.Classify(unavailableException);

            Assert.AreEqual(SocialFailureCategory.Unavailable, timeoutClassified.Category);
            Assert.AreEqual("UNITY_AUTH_SERVICE_UNAVAILABLE", timeoutClassified.Detail);
            Assert.AreEqual(SocialFailureCategory.Unavailable, unavailableClassified.Category);
            Assert.AreEqual("UNITY_AUTH_SERVICE_UNAVAILABLE", unavailableClassified.Detail);
        }

        [Test]
        public void UnityAuthenticationFailureClassifier_ClassifiesValidationFailures()
        {
            var exception = new Exception("InvalidParameter supplied for sign in.");

            IdentityBootstrapException classified = UnityAuthenticationFailureClassifier.Classify(exception);

            Assert.AreEqual(SocialFailureCategory.Validation, classified.Category);
            Assert.AreEqual("UNITY_AUTH_VALIDATION_FAILURE", classified.Detail);
        }

        [Test]
        public void UnityAuthenticationFailureClassifier_UsesUnknownFallbackForUnrecognizedFailures()
        {
            var exception = new Exception("Completely unexpected SDK response.");

            IdentityBootstrapException classified = UnityAuthenticationFailureClassifier.Classify(exception);

            Assert.AreEqual(SocialFailureCategory.Unknown, classified.Category);
            Assert.AreEqual("UNITY_AUTH_UNEXPECTED_FAILURE", classified.Detail);
        }

        [Test]
        public async Task BootstrapIdentity_ConcurrentCallersShareSameFailedBootstrapOperation()
        {
            var completion = new TaskCompletionSource<IdentityBootstrapSnapshot>();
            var gateway = new FakeIdentityBootstrapGateway(_ => completion.Task);
            var service = new UnityAuthenticationSocialService(gateway);
            var request = new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            };

            Task<SocialResult<BootstrapIdentityResult>> first = service.BootstrapIdentityAsync(request, CancellationToken.None);
            Task<SocialResult<BootstrapIdentityResult>> second = service.BootstrapIdentityAsync(request, CancellationToken.None);

            Assert.AreEqual(1, gateway.CallCount);

            completion.SetException(new IdentityBootstrapException(
                SocialFailureCategory.Unavailable,
                "Unity authentication is unavailable.",
                "UNITY_AUTH_SERVICE_UNAVAILABLE"));

            SocialResult<BootstrapIdentityResult>[] results = await Task.WhenAll(first, second);

            Assert.False(results[0].Success);
            Assert.False(results[1].Success);
            Assert.AreEqual(SocialFailureCategory.Unavailable, results[0].Failure.Category);
            Assert.AreEqual(SocialFailureCategory.Unavailable, results[1].Failure.Category);
            Assert.AreEqual(1, gateway.CallCount);
        }

        [Test]
        public async Task BootstrapIdentity_RetriesSuccessfullyAfterFailedBootstrap()
        {
            int attempts = 0;
            var gateway = new FakeIdentityBootstrapGateway(_ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new IdentityBootstrapException(
                        SocialFailureCategory.Unavailable,
                        "Unity authentication is unavailable.",
                        "UNITY_AUTH_SERVICE_UNAVAILABLE");
                }

                return Task.FromResult(new IdentityBootstrapSnapshot
                {
                    PlayerId = "player_after_retry_001",
                    RestoredCachedSession = false
                });
            });

            var service = new UnityAuthenticationSocialService(gateway);
            var request = new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            };

            SocialResult<BootstrapIdentityResult> first = await service.BootstrapIdentityAsync(request, CancellationToken.None);
            SocialResult<BootstrapIdentityResult> second = await service.BootstrapIdentityAsync(request, CancellationToken.None);

            Assert.False(first.Success);
            Assert.AreEqual(SocialFailureCategory.Unavailable, first.Failure.Category);
            Assert.True(second.Success);
            Assert.AreEqual("player_after_retry_001", second.Data.account.accountId);
            Assert.AreEqual(2, gateway.CallCount);
        }

        [Test]
        public async Task BootstrapIdentity_CancelingOneCallerDoesNotCancelOrCorruptAnotherSharedBootstrap()
        {
            var completion = new TaskCompletionSource<IdentityBootstrapSnapshot>();
            var gateway = new FakeIdentityBootstrapGateway(_ => completion.Task);
            var service = new UnityAuthenticationSocialService(gateway);
            var request = new BootstrapIdentityRequest
            {
                displayName = "Ari",
                requestedAtUtcMs = 1720000000000
            };

            using (var cancelledCaller = new CancellationTokenSource())
            {
                Task<SocialResult<BootstrapIdentityResult>> first = service.BootstrapIdentityAsync(request, cancelledCaller.Token);
                Task<SocialResult<BootstrapIdentityResult>> second = service.BootstrapIdentityAsync(request, CancellationToken.None);

                Assert.AreEqual(1, gateway.CallCount);

                cancelledCaller.Cancel();

                completion.SetResult(new IdentityBootstrapSnapshot
                {
                    PlayerId = "player_shared_after_cancel_001",
                    RestoredCachedSession = true
                });

                Assert.CatchAsync<OperationCanceledException>(async () => await first);

                SocialResult<BootstrapIdentityResult> secondResult = await second;
                Assert.True(secondResult.Success);
                Assert.AreEqual("player_shared_after_cancel_001", secondResult.Data.account.accountId);

                SocialResult<BootstrapIdentityResult> repeated = await service.BootstrapIdentityAsync(request, CancellationToken.None);
                Assert.True(repeated.Success);
                Assert.AreEqual("player_shared_after_cancel_001", repeated.Data.account.accountId);
                Assert.AreEqual(1, gateway.CallCount);
            }
        }

        private sealed class FakeIdentityBootstrapGateway : IIdentityBootstrapGateway
        {
            private readonly Func<CancellationToken, Task<IdentityBootstrapSnapshot>> _bootstrap;

            public FakeIdentityBootstrapGateway(Func<CancellationToken, Task<IdentityBootstrapSnapshot>> bootstrap)
            {
                _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            }

            public int CallCount { get; private set; }

            public Task<IdentityBootstrapSnapshot> BootstrapAsync(CancellationToken cancellationToken)
            {
                CallCount++;
                return _bootstrap(cancellationToken);
            }
        }
    }
}