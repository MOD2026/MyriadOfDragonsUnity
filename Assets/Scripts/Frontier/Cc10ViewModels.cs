using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Frontier
{
    public sealed class Cc10Row
    {
        public string Title = string.Empty;
        public string Detail = string.Empty;
        public string ActionLabel = string.Empty; // empty = read-only row
        public bool ActionEnabled;
        public string DisabledReason = string.Empty;
        public string Endpoint = string.Empty;
        public Dictionary<string, object> Payload = new Dictionary<string, object>();
        public string EntityKey = string.Empty;
        public bool HasAction => !string.IsNullOrEmpty(ActionLabel);
    }

    public sealed class Cc10SectionVm
    {
        public string SystemId = string.Empty;
        public string Title = string.Empty;
        public string Banner = string.Empty; // offline / paused / unavailable notice, empty when live
        public bool ReadOnly;
        public List<Cc10Row> Rows = new List<Cc10Row>();
    }

    /// <summary>Pure mapping from the real FrontierSnapshotResult to what a screen shows and which
    /// commands it may offer. No timers of its own beyond the display clock, no reward maths, no
    /// writes, and no invented map phases/threat bands/guild-contest scoping - those concepts do
    /// not exist in the published contract (see Cc10Contracts.cs header).</summary>
    public static class Cc10ViewModels
    {
        public static string SectionTitle(string systemId)
        {
            switch (systemId)
            {
                case Cc10SystemId.Tavern: return "Tavern";
                case Cc10SystemId.Missions: return "Missions";
                case Cc10SystemId.WorldMap: return "World Map";
                case Cc10SystemId.NpcSpots: return "NPC Spots";
                case Cc10SystemId.GuildTerritory: return "Territory";
                case Cc10SystemId.IndividualResearch: return "Research";
                case Cc10SystemId.GuildResearch: return "Guild Research";
                case Cc10SystemId.IndividualRankings: return "Rankings";
                case Cc10SystemId.GuildRankings: return "Guild Rankings";
                case Cc10SystemId.Minigame: return "Minigame";
                default: return "Cargo";
            }
        }

        /// <summary>Systems GetFrontierState actually returns data for. GuildTerritory now has
        /// PARTIAL support: the Central Realm's three contest districts are queryable
        /// (FrontierSnapshotResult.ContestDistricts), but the per-guild TerritoryDto contribution
        /// ledger (Ashen Ridge/Ember Hollow/Iron Quarry) still has no query endpoint. GuildRankings
        /// and IndividualRankings still need a chosen season id the shell has no source for.</summary>
        public static bool HasQuerySupport(string systemId) =>
            systemId != Cc10SystemId.GuildRankings
            && systemId != Cc10SystemId.IndividualRankings;

        public static Cc10SectionVm Build(Cc10FrontierClient client, string systemId,
            IEnumerable<CardProgressionRecord> cards = null)
        {
            var vm = new Cc10SectionVm { SystemId = systemId, Title = SectionTitle(systemId) };
            vm.ReadOnly = client.IsReadOnly(systemId);

            if (!client.HasState)
            {
                vm.Banner = client.Connection == Cc10Connection.Offline ? Cc10Copy.Offline : Cc10Copy.InFlight;
                vm.ReadOnly = true;
                return vm;
            }

            if (!HasQuerySupport(systemId))
            {
                vm.Banner = "Not available yet - no server query for this view.";
                vm.ReadOnly = true;
                return vm;
            }

            if (client.IsSystemDisabled(systemId)) vm.Banner = Cc10Copy.SystemDisabled;
            else if (client.Connection != Cc10Connection.Online) vm.Banner = Cc10Copy.Offline;

            Cc10FrontierSnapshot s = client.Snapshot;
            switch (systemId)
            {
                case Cc10SystemId.Tavern: AddTavern(vm, s); break;
                case Cc10SystemId.Missions: AddMissions(vm, client, s); break;
                case Cc10SystemId.WorldMap: AddPhases(vm, s); AddNodes(vm, s); break;
                case Cc10SystemId.NpcSpots: AddSpots(vm, s); break;
                case Cc10SystemId.GuildTerritory: AddContestDistricts(vm, s); break;
                case Cc10SystemId.IndividualResearch: AddResearch(vm, client, s, Cc10ResearchScope.Individual); break;
                case Cc10SystemId.GuildResearch: AddResearch(vm, client, s, Cc10ResearchScope.Guild); break;
                case Cc10SystemId.Minigame: AddMinigame(vm, s); break;
                case Cc10SystemId.Cargo: AddCargo(vm, client, s); break;
            }

            if (systemId == "CardTraining") AddCards(vm, cards);

            if (vm.ReadOnly)
            {
                foreach (Cc10Row row in vm.Rows)
                {
                    if (!row.HasAction) continue;
                    row.ActionEnabled = false;
                    if (string.IsNullOrEmpty(row.DisabledReason)) row.DisabledReason = vm.Banner;
                }
            }
            return vm;
        }

        private static void AddCards(Cc10SectionVm vm, IEnumerable<CardProgressionRecord> cards)
        {
            if (cards == null) return;
            foreach (CardProgressionRecord rec in cards)
            {
                if (rec == null || string.IsNullOrEmpty(rec.cardId)) continue;
                bool max = rec.cardLevel >= Cc10Rules.CardLevelCap;
                int cost = Cc10Rules.CardTrainingCost(rec.cardLevel);
                var row = new Cc10Row
                {
                    Title = rec.cardId + "  Lv " + rec.cardLevel,
                    Detail = max ? "Max level" : "Training XP " + rec.trainingXp + " / " + cost,
                    EntityKey = rec.cardId,
                    ActionEnabled = false, // no CC10 endpoint owns card training; existing Collection flow does
                };
                vm.Rows.Add(row);
            }
        }

        private static void AddTavern(Cc10SectionVm vm, Cc10FrontierSnapshot s)
        {
            Cc10TavernDto t = s.tavern ?? new Cc10TavernDto();
            bool max = t.level >= Cc10Rules.TavernMaxLevel;
            var project = t.activeProject;

            if (project != null)
            {
                vm.Rows.Add(new Cc10Row
                {
                    Title = "Tavern  Lv " + t.level + " -> " + project.toLevel,
                    Detail = project.goldCost + " Gold + " + project.materialsCost + " Materials",
                    ActionLabel = "Complete",
                    Endpoint = Cc10Endpoints.CompleteTavernUpgrade,
                    EntityKey = "tavern",
                    ActionEnabled = true, // server rejects with NOT_READY if readyUtcMs hasn't passed
                });
                return;
            }

            vm.Rows.Add(new Cc10Row
            {
                Title = "Tavern  Lv " + t.level,
                Detail = max ? "Fully upgraded" : "Start the next upgrade",
                ActionLabel = "Upgrade",
                Endpoint = Cc10Endpoints.StartTavernUpgrade,
                EntityKey = "tavern",
                ActionEnabled = !max,
                DisabledReason = "Fully upgraded",
            });
        }

        private static void AddMissions(Cc10SectionVm vm, Cc10FrontierClient client, Cc10FrontierSnapshot s)
        {
            foreach (Cc10MissionDto m in s.missions)
            {
                var row = new Cc10Row { Title = MissionTitle(m.type), EntityKey = m.missionId };
                row.Payload["missionId"] = m.missionId;
                string reward = Cc10Rules.MissionGoldReward + " Gold + " + Cc10Rules.MissionMaterialReward + " Materials";
                switch (m.status)
                {
                    case Cc10MissionStatus.Active:
                        row.Detail = "Active - " + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(m.readyUtcMs));
                        row.ActionLabel = "Abandon";
                        row.Endpoint = Cc10Endpoints.AbandonMission;
                        row.ActionEnabled = true;
                        break;
                    case Cc10MissionStatus.Completed:
                        row.Detail = "Completed - " + reward;
                        row.ActionLabel = "Claim";
                        row.Endpoint = Cc10Endpoints.ClaimMission;
                        row.ActionEnabled = true;
                        break;
                    default: // Claimed / Failed / Abandoned / Expired - terminal, read-only
                        row.Detail = m.status;
                        break;
                }
                vm.Rows.Add(row);
            }

            // Assignable slots: one row per unlocked mission type with a free spot this UTC day,
            // derived from the spots list (server-owned; spots are mission sources, not faucets).
            foreach (Cc10Rules.MissionRule rule in Cc10Rules.MissionRules)
            {
                Cc10NpcSpotDto spot = s.spots.FirstOrDefault(sp => sp.missionType == rule.Type && sp.status == Cc10SpotStatus.Available);
                if (spot == null) continue;
                var row = new Cc10Row
                {
                    Title = MissionTitle(rule.Type) + " (assign)",
                    Detail = Cc10Rules.MissionGoldReward + " Gold + " + Cc10Rules.MissionMaterialReward
                        + " Materials - " + Cc10Rules.MissionStaminaCost + " Stamina - " + rule.Minutes + "m",
                    ActionLabel = "Start",
                    Endpoint = Cc10Endpoints.AssignMission,
                    EntityKey = "assign:" + spot.spotId,
                    ActionEnabled = true,
                };
                row.Payload["missionType"] = rule.Type;
                row.Payload["spotId"] = spot.spotId;
                vm.Rows.Add(row);
            }
        }

        private static string MissionTitle(string type)
        {
            switch (type)
            {
                case Cc10MissionType.NpcPatrol: return "NPC Patrol";
                case Cc10MissionType.RelicRescue: return "Relic Rescue";
                case Cc10MissionType.VeinConvoy: return "Vein Convoy";
                default: return type;
            }
        }

        /// <summary>Read-only phase ladder at the top of World Map (server-evaluated Tavern level +
        /// reported Campaign chapter; never computed client-side).</summary>
        private static void AddPhases(Cc10SectionVm vm, Cc10FrontierSnapshot s)
        {
            foreach (Cc10PhaseDto p in s.phases)
            {
                bool current = p.phase == s.phase;
                vm.Rows.Add(new Cc10Row
                {
                    Title = PhaseTitle(p.phase) + (current ? " (current)" : string.Empty),
                    Detail = p.unlocked
                        ? "Unlocked"
                        : "Locked - Tavern Lv " + p.requiredTavernLevel + (p.requiredCampaignChapter > 0
                            ? " + Campaign ch. " + p.requiredCampaignChapter : string.Empty),
                    EntityKey = "phase:" + p.phase,
                });
            }
        }

        private static string PhaseTitle(string phase)
        {
            switch (phase)
            {
                case Cc10MapPhase.HomeOutpost: return "Home Outpost";
                case Cc10MapPhase.OuterMarches: return "Outer Marches";
                case Cc10MapPhase.InnerReach: return "Inner Reach";
                case Cc10MapPhase.CentralRealm: return "Central Realm";
                default: return phase;
            }
        }

        private static void AddNodes(Cc10SectionVm vm, Cc10FrontierSnapshot s)
        {
            foreach (Cc10MapNodeDto n in s.nodes)
            {
                var row = new Cc10Row
                {
                    Title = n.regionId + " / " + n.nodeId + " [" + PhaseTitle(n.phase) + ", " + n.encounterBand + "]"
                        + (n.isContestDistrict ? " (contest district)" : string.Empty),
                    EntityKey = n.nodeId,
                };
                row.Payload["nodeId"] = n.nodeId;

                if (n.owned)
                {
                    row.Detail = "Owned" + (n.expanded ? " - expanded" : string.Empty);
                }
                else if (n.discovered)
                {
                    // Adjacency + phase gated ownership. The server enforces both; this preview
                    // only explains why the button is disabled, never decides it.
                    bool adjacentToOwned = s.nodes.Any(other => other.owned
                        && other.neighbors != null && other.neighbors.Contains(n.nodeId));
                    row.Detail = "Discovered, not yet owned";
                    row.ActionLabel = "Expand";
                    row.Endpoint = Cc10Endpoints.ExpandNode;
                    row.ActionEnabled = n.phaseUnlocked && adjacentToOwned;
                    row.DisabledReason = !n.phaseUnlocked ? "Phase not unlocked yet" : "Not adjacent to an owned location";
                }
                else
                {
                    bool adjacentToDiscovered = s.nodes.Any(other => other.discovered
                        && other.neighbors != null && other.neighbors.Contains(n.nodeId));
                    row.Detail = "Undiscovered";
                    row.ActionLabel = "Discover";
                    row.Endpoint = Cc10Endpoints.DiscoverNode;
                    row.ActionEnabled = adjacentToDiscovered;
                    row.DisabledReason = "Not adjacent to a discovered location yet";
                }
                vm.Rows.Add(row);
            }
        }

        /// <summary>NPC hotspots scattered across discovered nodes - read-only; a hotspot only ever
        /// binds to the mission the server already generated for it (spots are mission sources,
        /// never independent faucets).</summary>
        private static void AddSpots(Cc10SectionVm vm, Cc10FrontierSnapshot s)
        {
            foreach (Cc10NpcSpotDto p in s.spots)
                vm.Rows.Add(new Cc10Row
                {
                    Title = MissionTitle(p.missionType) + " hotspot @ " + p.nodeId,
                    Detail = p.status,
                    EntityKey = p.spotId,
                });
        }

        /// <summary>Central Realm's three fixed contest districts - a genuine cross-guild race.
        /// Enroll is offered only when the server says the caller's own guild is presently top-3
        /// eligible and no district is enrolled yet; resolution is operator-gated, never a player
        /// action, so it is shown as status only.</summary>
        private static void AddContestDistricts(Cc10SectionVm vm, Cc10FrontierSnapshot s)
        {
            foreach (Cc10ContestDistrictDto d in s.contestDistricts)
            {
                var row = new Cc10Row { Title = "Contest: " + d.districtId, EntityKey = d.districtId };
                row.Payload["districtId"] = d.districtId;
                switch (d.status)
                {
                    case Cc10ContestStatus.NotEnrolled:
                        row.Detail = d.callerGuildEligible ? "Eligible - top 3 this season" : "Not eligible this season";
                        row.ActionLabel = "Enroll";
                        row.Endpoint = Cc10Endpoints.EnrollContestDistrict;
                        row.ActionEnabled = d.callerGuildEligible;
                        row.DisabledReason = "Your guild is not currently top-3";
                        break;
                    case Cc10ContestStatus.Owned:
                        row.Detail = "Owned by " + (d.ownerGuildPseudonym ?? "another guild");
                        break;
                    default:
                        row.Detail = d.status + (d.enrolledGuildPseudonym != null ? " - " + d.enrolledGuildPseudonym : string.Empty);
                        break;
                }
                vm.Rows.Add(row);
            }
        }

        private static void AddResearch(Cc10SectionVm vm, Cc10FrontierClient client, Cc10FrontierSnapshot s, string scope)
        {
            foreach (Cc10ResearchDto n in s.research)
            {
                if (n.scope != scope) continue;
                var row = new Cc10Row { Title = n.nodeId, EntityKey = n.nodeId };
                row.Payload["nodeId"] = n.nodeId;
                if (n.status == Cc10ResearchStatus.Completed)
                {
                    row.Detail = "Completed";
                }
                else
                {
                    long left = client.Clock.RemainingMs(n.readyUtcMs);
                    row.Detail = "Researching - " + Cc10ServerClock.FormatRemaining(left);
                    if (left <= 0)
                    {
                        row.ActionLabel = "Complete";
                        row.Endpoint = scope == Cc10ResearchScope.Guild
                            ? Cc10Endpoints.CompleteGuildResearch : Cc10Endpoints.CompleteResearch;
                        row.ActionEnabled = true;
                    }
                    else if (scope == Cc10ResearchScope.Individual)
                    {
                        row.ActionLabel = "Cancel";
                        row.Endpoint = Cc10Endpoints.CancelResearch;
                        row.ActionEnabled = true;
                    }
                }
                vm.Rows.Add(row);
            }
        }

        private static void AddMinigame(Cc10SectionVm vm, Cc10FrontierSnapshot s)
        {
            Cc10MinigameSessionDto g = s.minigame;
            bool running = g != null && g.status == Cc10MinigameStatus.Created;
            var row = new Cc10Row
            {
                Title = "Side Activity (optional)",
                Detail = running ? "In progress" : "Doesn't affect progression",
                ActionLabel = running ? string.Empty : "Play",
                Endpoint = Cc10Endpoints.CreateMinigameSession,
                EntityKey = "minigame",
                ActionEnabled = !running,
            };
            vm.Rows.Add(row);
        }

        private static void AddCargo(Cc10SectionVm vm, Cc10FrontierClient client, Cc10FrontierSnapshot s)
        {
            foreach (Cc10CargoDto c in s.cargo)
            {
                var row = new Cc10Row { Title = c.cargoId, EntityKey = c.cargoId };
                row.Payload["cargoId"] = c.cargoId;
                switch (c.status)
                {
                    case Cc10CargoStatus.Accepted:
                        row.Detail = "Accepted - ready to scout";
                        row.ActionLabel = "Scout";
                        row.Endpoint = Cc10Endpoints.ScoutCargo;
                        row.ActionEnabled = true;
                        break;
                    case Cc10CargoStatus.Scouting:
                        row.Detail = "Scouting - route: " + string.Join(" -> ", c.routeNodeIds ?? System.Array.Empty<string>());
                        row.ActionLabel = "Dispatch";
                        row.Endpoint = Cc10Endpoints.DispatchCargo;
                        row.ActionEnabled = true;
                        break;
                    case Cc10CargoStatus.Active:
                        row.Detail = "En route - " + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(c.arriveUtcMs))
                            + (c.encounterRequired ? " (encounter required)" : "");
                        row.ActionLabel = "Deliver";
                        row.Endpoint = Cc10Endpoints.DeliverCargo;
                        row.ActionEnabled = !c.encounterRequired; // an encounter needs a Battle attestation first
                        row.DisabledReason = c.encounterRequired ? "Resolve the encounter first" : string.Empty;
                        break;
                    case Cc10CargoStatus.Delivered:
                        row.Detail = "Delivered - " + c.haulGold + " Gold + " + c.haulMaterials + " Materials (not yet claimed)";
                        row.ActionLabel = "Claim";
                        row.Endpoint = Cc10Endpoints.ClaimMission; // claim settles via the bound mission's claim
                        row.ActionEnabled = true;
                        break;
                    default:
                        row.Detail = c.status + (c.terminalReason != null ? " (" + c.terminalReason + ")" : "");
                        break;
                }
                vm.Rows.Add(row);
            }
        }
    }
}
