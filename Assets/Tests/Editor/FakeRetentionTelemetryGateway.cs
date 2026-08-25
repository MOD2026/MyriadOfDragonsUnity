using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;

namespace MyriadOfDragons.Tests
{
    /// <summary>Shared fake IRetentionTelemetryGateway (same pattern as FakeBazaarGateway/
    /// FakeChatSocialGateway - Task.FromResult, benign real success) - used by every presenter's
    /// own telemetry-wiring test (BattlePass/DailyLoginQuests/EmpireExpedition shell tests) so
    /// they don't each duplicate an identical fake. Records every sent event for assertions.</summary>
    internal sealed class FakeRetentionTelemetryGateway : IRetentionTelemetryGateway
    {
        public readonly List<RetentionTelemetryEvent> SentEvents = new List<RetentionTelemetryEvent>();

        public Task<RetentionTelemetryGatewayResult> SendEventAsync(RetentionTelemetryEvent evt, CancellationToken cancellationToken)
        {
            SentEvents.Add(evt);
            return Task.FromResult(new RetentionTelemetryGatewayResult { success = true });
        }
    }
}
