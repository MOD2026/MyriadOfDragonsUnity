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
                case Cc10SystemId.WorldMap: AddWorldMap(vm, client, s); break;
                case Cc10SystemId.NpcSpots: AddSpots(vm, s); break;
                case Cc10SystemId.GuildTerritory: AddContest(vm, client); break;
                case Cc10SystemId.IndividualResearch: AddResearch(vm, client, s, Cc10ResearchScope.Individual); break;
                case Cc10SystemId.GuildResearch: AddResearch(vm, client, s, Cc10ResearchScope.Guild); break;
                case Cc10SystemId.Minigame: AddMinigame(vm, client, s); break;
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
            // Server-derived (never client-computed): the caller's own current active-mission cap
            // and how many of their missions are actually Active right now.
            int activeCount = s.missions.Count(m => m.status == Cc10MissionStatus.Active);
            string slots = " - " + activeCount + "/" + t.activeMissionSlots + " active missions";

            if (project != null)
            {
                vm.Rows.Add(new Cc10Row
                {
                    Title = "Tavern  Lv " + t.level + " -> " + project.toLevel,
                    Detail = project.goldCost + " Gold + " + project.materialsCost + " Materials" + slots,
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
                Detail = (max ? "Fully upgraded" : "Start the next upgrade") + slots,
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
                var row = new Cc10Row { Title = MissionTitle(m.type) + PoolSuffix(m.npcPool), EntityKey = m.missionId };
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
            // Slot-full preview uses only two server-provided numbers - no per-level cap table is
            // reimplemented here; the server alone enforces the real limit (MISSION_SLOTS_FULL).
            int activeCount = s.missions.Count(m => m.status == Cc10MissionStatus.Active);
            int activeSlots = s.tavern != null ? s.tavern.activeMissionSlots : 0;
            bool slotsFull = activeSlots > 0 && activeCount >= activeSlots;
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
                    ActionEnabled = !slotsFull,
                    DisabledReason = Cc10Copy.MissionSlotsFull,
                };
                row.Payload["missionType"] = rule.Type;
                row.Payload["spotId"] = spot.spotId;
                vm.Rows.Add(row);
            }
        }

        /// <summary>Display-only suffix naming the mission's deterministically-selected NPC pool
        /// (server-computed from the mission's own EncounterSeed) - never client-chosen.</summary>
        private static string PoolSuffix(Cc10NpcPoolDto pool) =>
            pool != null && !string.IsNullOrEmpty(pool.poolName) ? " (" + pool.poolName + ")" : string.Empty;

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

        private static void AddNodes(Cc10SectionVm vm, Cc10FrontierSnapshot s, HashSet<string> ownFromSnapshot = null)
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
                // Server-authoritative layout key only - never computed or invented here; a future
                // map renderer places this row purely from these two numbers.
                row.Payload["layoutX"] = n.layoutX;
                row.Payload["layoutY"] = n.layoutY;

                if (n.owned || (ownFromSnapshot != null && ownFromSnapshot.Contains(n.nodeId)))
                {
                    row.Detail = "Owned" + (n.expanded ? " - expanded" : string.Empty);
                }
                else if (n.discovered)
                {
                    // Adjacency + phase gated ownership. The server enforces both; this preview
                    // only explains why the button is disabled, never decides it.
                    bool adjacentToOwned = s.nodes.Any(other => (other.owned || (ownFromSnapshot != null && ownFromSnapshot.Contains(other.nodeId)))
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

        /// <summary>Display-only color suffix from the server's opaque token ("GC01".."GC12"). A
        /// null/unknown token means no guild is enrolled/resolved (a neutral state, never a guessed color).</summary>
        private static string ColorSuffix(string colorToken) =>
            Cc10Rules.TryParseGuildColorToken(colorToken, out int index) ? " (color " + (index + 1) + ")" : string.Empty;

        private static string PhaseTokenTitle(string token)
        {
            switch (token)
            {
                case Cc10WorldMapPhaseToken.Tutorial: return "Home Outpost";
                case Cc10WorldMapPhaseToken.Outer: return "Outer Marches";
                case Cc10WorldMapPhaseToken.Inner: return "Inner Reach";
                case Cc10WorldMapPhaseToken.Central: return "Central Realm";
                default: return token;
            }
        }

        /// <summary>Banner for a World Map snapshot problem that leaves the last good snapshot on
        /// screen; empty when the last attempt was fine or never made.</summary>
        private static string WorldMapProblemBanner(Cc10FrontierClient client)
        {
            switch (client.LastWorldMapRefresh)
            {
                case Cc10WorldMapRefresh.SchemaRejected: return "Map data is from a newer version. Update the app to see the latest.";
                case Cc10WorldMapRefresh.Malformed: return "Map data couldn't be read. Showing the last good view.";
                default: return string.Empty;
            }
        }

        /// <summary>Central Realm contest rows, read ONLY from the canonical GetWorldMapSnapshot
        /// (the legacy GetFrontierState.contestDistricts list is never read - BE does not populate
        /// it). Everything is server-decided: the server sends no rows before Central unlock, with no
        /// active season, or while GuildTerritory is disabled, and this view invents none of them.
        /// A row shows a color only from the server's opaque token, never a guild identity.
        /// Enroll is offered on an Unclaimed row; whether this guild may enroll is decided by the
        /// server (NOT_ELIGIBLE etc. arrive as plain copy), not guessed here.</summary>
        private static void AddContest(Cc10SectionVm vm, Cc10FrontierClient client)
        {
            Cc10WorldMapSnapshotDto map = client.WorldMap;
            if (map == null)
            {
                vm.Banner = client.Connection == Cc10Connection.Online ? Cc10Copy.InFlight : Cc10Copy.Offline;
                vm.ReadOnly = true;
                return;
            }

            string problem = WorldMapProblemBanner(client);
            if (string.IsNullOrEmpty(vm.Banner) && !string.IsNullOrEmpty(problem)) vm.Banner = problem;

            if (Cc10WorldMapPhaseToken.Rank(map.unlockedPhase) < Cc10WorldMapPhaseToken.Rank(Cc10WorldMapPhaseToken.Central))
            {
                vm.Rows.Add(new Cc10Row { Title = "Central Realm contest", Detail = "Locked until you reach the Central Realm", EntityKey = "contest:locked" });
                return;
            }

            if (map.centralContest.Length == 0)
            {
                vm.Rows.Add(new Cc10Row { Title = "Central Realm contest", Detail = "No contest is running right now", EntityKey = "contest:none" });
                return;
            }

            foreach (Cc10WorldMapContestDto d in map.centralContest)
            {
                var row = new Cc10Row { Title = "Contest: " + d.districtId + " (season " + d.seasonId + ")", EntityKey = d.districtId };
                row.Payload["districtId"] = d.districtId;
                if (d.ownershipState == Cc10ContestOwnershipState.GuildOwned)
                {
                    row.Detail = "Owned by a guild" + ColorSuffix(d.guildColorToken);
                }
                else
                {
                    row.Detail = "Unclaimed";
                    row.ActionLabel = "Enroll";
                    row.Endpoint = Cc10Endpoints.EnrollContestDistrict;
                    row.Payload["seasonId"] = d.seasonId;
                    if (!string.IsNullOrEmpty(client.GuildId)) row.Payload["guildId"] = client.GuildId;
                    row.ActionEnabled = !string.IsNullOrEmpty(client.GuildId);
                    row.DisabledReason = "Join a guild to enter the contest";
                }
                vm.Rows.Add(row);
            }
        }

        /// <summary>World Map rows. When the canonical snapshot is held it drives the summary and the
        /// "your territory" list (private per-player occupancy, in the server's own order); the
        /// per-node Discover/Expand rows still come from the frontier snapshot, with a node counted
        /// owned if EITHER server read says so. No identity, base placement or wells anywhere.</summary>
        private static void AddWorldMap(Cc10SectionVm vm, Cc10FrontierClient client, Cc10FrontierSnapshot s)
        {
            Cc10WorldMapSnapshotDto map = client.WorldMap;
            HashSet<string> ownSet = null;
            if (map != null)
            {
                string problem = WorldMapProblemBanner(client);
                if (string.IsNullOrEmpty(vm.Banner) && !string.IsNullOrEmpty(problem)) vm.Banner = problem;

                vm.Rows.Add(new Cc10Row
                {
                    Title = "Map " + map.mapVersion,
                    Detail = PhaseTokenTitle(map.unlockedPhase) + " unlocked - territory v" + map.occupancyVersion,
                    EntityKey = "worldmap:summary",
                });

                ownSet = new HashSet<string>();
                foreach (Cc10WorldMapOwnNodeDto n in map.ownOccupiedNodes)
                {
                    ownSet.Add(n.nodeId);
                    vm.Rows.Add(new Cc10Row
                    {
                        Title = "Your territory: " + n.nodeId + " [" + PhaseTokenTitle(n.phaseId) + "]",
                        Detail = "Occupied",
                        EntityKey = "own:" + n.nodeId,
                    });
                }
            }

            AddPhases(vm, s);
            AddNodes(vm, s, ownSet);
        }

        /// <summary>Research rows: started/completed instances come straight from the server
        /// (<paramref name="s"/>.research); a catalog node not yet started has NO server instance
        /// at all (GetFrontierState only ever returns instances), so an "Available"/"Locked" row is
        /// built from the preview-only <see cref="Cc10Rules.ResearchCatalog"/> - purely to offer the
        /// Start button and explain a lock; the server independently re-validates cost, prerequisite,
        /// phase and top-three-guild eligibility on every real Start/Contribute call.</summary>
        private static void AddResearch(Cc10SectionVm vm, Cc10FrontierClient client, Cc10FrontierSnapshot s, string scope)
        {
            var started = new HashSet<string>();
            foreach (Cc10ResearchDto n in s.research)
            {
                if (n.scope != scope) continue;
                started.Add(n.nodeId);
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
                    else
                    {
                        // Cancel now genuinely restores the reserved Gold/Materials (BE 253834ab) -
                        // credited back via the receipt, never computed here.
                        row.ActionLabel = "Cancel";
                        row.Endpoint = scope == Cc10ResearchScope.Guild
                            ? Cc10Endpoints.CancelGuildResearch : Cc10Endpoints.CancelResearch;
                        row.ActionEnabled = true;
                    }
                }
                vm.Rows.Add(row);
            }

            foreach (Cc10Rules.ResearchCatalogRow cat in Cc10Rules.ResearchCatalog)
            {
                if (cat.Scope != scope || started.Contains(cat.NodeId)) continue;
                bool prereqsMet = cat.Prerequisites.All(p => s.research.Any(r => r.nodeId == p && r.status == Cc10ResearchStatus.Completed));
                var row = new Cc10Row { Title = cat.NodeId, EntityKey = cat.NodeId };
                row.Payload["nodeId"] = cat.NodeId;
                if (scope == Cc10ResearchScope.Guild && cat.RequiresTopThreeGuild)
                    row.DisabledReason = "Prerequisite not met, or your guild isn't currently top-3";
                else
                    row.DisabledReason = "Prerequisite not met";
                row.Detail = "Available - " + cat.Gold + " Gold + " + cat.Materials + " Materials";
                row.ActionLabel = scope == Cc10ResearchScope.Guild ? "Start (guild)" : "Start";
                row.Endpoint = scope == Cc10ResearchScope.Guild ? Cc10Endpoints.StartGuildResearch : Cc10Endpoints.StartResearch;
                row.ActionEnabled = prereqsMet; // server independently re-checks phase/top-three/cost
                vm.Rows.Add(row);
            }
        }

        /// <summary>Full published minigame state machine: Active/Submitted/Verified/Claimed/
        /// Failed/Expired/Abandoned. Submission itself (score/proof) is produced by the minigame's
        /// own play surface, not this shell - this row only offers Abandon while Active and Claim
        /// once Verified. Cost/reward stay 0 (CC10_ECONOMY_NEUTRAL_0_0) unless a receipt says
        /// otherwise; this layer never computes either.</summary>
        /// <summary>Vein Relay session row. Available and Active are BOTH real server statuses
        /// (BE 6e93d10d) - Available offers Start (a distinct call from Create), Active offers
        /// Abandon and shows the real 30-second countdown from StartedUtcMs. Actually PLAYING (the
        /// 12-round lane submission) is a separate gameplay surface this shell does not build - see
        /// Cc10FrontierClient.ExecuteAsync(SubmitMinigameResult) for the wire shape a future
        /// presenter uses; this row never computes or previews a score.</summary>
        private static void AddMinigame(Cc10SectionVm vm, Cc10FrontierClient client, Cc10FrontierSnapshot s)
        {
            Cc10MinigameSessionDto g = s.minigame;
            var row = new Cc10Row { Title = "Vein Relay (optional)", EntityKey = "minigame" };
            if (g == null)
            {
                row.Detail = "Doesn't affect progression";
                row.ActionLabel = "Play";
                row.Endpoint = Cc10Endpoints.CreateMinigameSession;
                row.ActionEnabled = true;
                vm.Rows.Add(row);
                return;
            }

            row.Payload["sessionId"] = g.sessionId;
            switch (g.status)
            {
                case Cc10MinigameStatus.Available:
                case Cc10MinigameStatus.Started: // wire-compat alias; server never produces this
                    row.Detail = "Ready - not started yet";
                    row.ActionLabel = "Start";
                    row.Endpoint = Cc10Endpoints.StartMinigameSession;
                    row.ActionEnabled = true;
                    break;
                case Cc10MinigameStatus.Active:
                    row.Detail = "In progress - " + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(g.expiresUtcMs));
                    row.ActionLabel = "Abandon";
                    row.Endpoint = Cc10Endpoints.AbandonMinigameSession;
                    row.ActionEnabled = true;
                    break;
                case Cc10MinigameStatus.Submitted: // wire-compat alias; server never produces this
                    row.Detail = "Result submitted - awaiting verification";
                    break;
                case Cc10MinigameStatus.Verified:
                    row.Detail = "Verified" + (g.verifiedScore.HasValue ? " - score " + g.verifiedScore.Value : string.Empty);
                    row.ActionLabel = "Claim";
                    row.Endpoint = Cc10Endpoints.ClaimMinigameResult;
                    row.ActionEnabled = true;
                    break;
                case Cc10MinigameStatus.Claimed:
                    row.Detail = "Claimed";
                    break;
                case Cc10MinigameStatus.Failed:
                    row.Detail = "Failed - nothing claimed";
                    row.ActionLabel = "Play again";
                    row.Endpoint = Cc10Endpoints.CreateMinigameSession;
                    row.ActionEnabled = true; // server enforces the 10-minute cooldown (MINIGAME_COOLING_DOWN), not this row
                    break;
                case Cc10MinigameStatus.Expired:
                    row.Detail = "Expired";
                    row.ActionLabel = "Play again";
                    row.Endpoint = Cc10Endpoints.CreateMinigameSession;
                    row.ActionEnabled = true;
                    break;
                case Cc10MinigameStatus.Abandoned:
                    row.Detail = "Abandoned";
                    row.ActionLabel = "Play again";
                    row.Endpoint = Cc10Endpoints.CreateMinigameSession;
                    row.ActionEnabled = true;
                    break;
                default:
                    row.Detail = g.status;
                    break;
            }
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
                    case Cc10CargoStatus.Intercepted:
                        row.Detail = "Intercepted by an NPC patrol" + (c.terminalReason != null ? " (" + c.terminalReason + ")" : string.Empty)
                            + " - the haul was lost, permanent progression untouched";
                        break;
                    default: // Abandoned / Expired / Failed - terminal, read-only
                        row.Detail = c.status + (c.terminalReason != null ? " (" + c.terminalReason + ")" : string.Empty);
                        break;
                }
                vm.Rows.Add(row);
            }
        }

        /// <summary>Guild territory contribution ledger, from a separately-loaded GetGuildState
        /// (<see cref="Cc10FrontierClient.GuildSnapshot"/> - the frontier snapshot itself has no
        /// per-guild territory rows). Contribution is a single command per member per window; the
        /// server decides eligibility/window/points, never this layer.</summary>
        public static Cc10SectionVm BuildGuildTerritory(Cc10FrontierClient client, string callerPseudonym)
        {
            var vm = new Cc10SectionVm { SystemId = Cc10SystemId.GuildTerritory, Title = "Territory" };
            if (client.GuildSnapshot == null)
            {
                vm.Banner = "No guild territory data loaded yet.";
                vm.ReadOnly = true;
                return vm;
            }

            // Independent of the main FrontierSnapshot load (GetGuildState is its own query); only
            // gates on live connectivity and the disabled-systems list when that snapshot exists.
            vm.ReadOnly = client.Connection != Cc10Connection.Online || client.IsSystemDisabled(Cc10SystemId.GuildTerritory);
            if (vm.ReadOnly) vm.Banner = client.Connection != Cc10Connection.Online ? Cc10Copy.Offline : Cc10Copy.SystemDisabled;
            foreach (Cc10TerritoryDto t in client.GuildSnapshot.territories)
            {
                bool alreadyContributed = t.contributedMembers != null && t.contributedMembers.Contains(callerPseudonym);
                var row = new Cc10Row
                {
                    Title = t.territoryId,
                    Detail = (string.IsNullOrEmpty(t.ownerGuildPseudonym) ? "Unclaimed" : "Held by " + t.ownerGuildPseudonym)
                        + " - window ends " + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(t.windowEndUtcMs)),
                    EntityKey = t.territoryId,
                    ActionLabel = "Contribute",
                    Endpoint = Cc10Endpoints.ContributeTerritory,
                    ActionEnabled = !alreadyContributed,
                    DisabledReason = "Already contributed this window",
                };
                row.Payload["territoryId"] = t.territoryId;
                if (vm.ReadOnly) { row.ActionEnabled = false; row.DisabledReason = vm.Banner; }
                vm.Rows.Add(row);
            }
            return vm;
        }

        /// <summary>Rendering only for a ranking view the host already fetched (a season id source
        /// - e.g. the active season from a Home/Season UI - is outside this shell's scope). Entries
        /// are shown in exactly the order the server returned them: tie order is the server's own
        /// deterministic rule (points desc, then earliest LastScoredUtcMs, then pseudonym), never
        /// re-sorted here.</summary>
        public static Cc10SectionVm BuildRanking(string systemId, Cc10RankingViewDto view)
        {
            var vm = new Cc10SectionVm { SystemId = systemId, Title = SectionTitle(systemId), ReadOnly = true };
            if (view == null)
            {
                vm.Banner = "Not available yet - no season selected.";
                return vm;
            }

            vm.Rows.Add(new Cc10Row { Title = "Season " + view.seasonId, Detail = view.state, EntityKey = "season:" + view.seasonId });
            foreach (Cc10RankingEntryDto e in view.entries)
                vm.Rows.Add(new Cc10Row { Title = "#" + e.rank + "  " + e.subjectPseudonym, Detail = e.points + " pts", EntityKey = "rank:" + e.rank });
            if (view.you != null)
                vm.Rows.Add(new Cc10Row { Title = "You: #" + view.you.rank, Detail = view.you.points + " pts", EntityKey = "rank:you" });
            return vm;
        }
    }
}
