using System.Collections.Generic;
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
        public Cc10System System;
        public string Title = string.Empty;
        public string Banner = string.Empty; // offline / paused notice, empty when live
        public bool ReadOnly;
        public List<Cc10Row> Rows = new List<Cc10Row>();
    }

    /// <summary>Pure mapping from authoritative state to what a screen shows and which commands it
    /// may offer. Contains no timers of its own, no reward maths beyond a display hint, and no writes.</summary>
    public static class Cc10ViewModels
    {
        public static string SectionTitle(Cc10System system)
        {
            switch (system)
            {
                case Cc10System.CardTraining: return "Training";
                case Cc10System.Tavern: return "Tavern";
                case Cc10System.Missions: return "Missions";
                case Cc10System.WorldMap: return "World Map";
                case Cc10System.NpcSpots: return "NPC Spots";
                case Cc10System.GuildTerritory: return "Territory";
                case Cc10System.IndividualResearch: return "Research";
                case Cc10System.GuildResearch: return "Guild Research";
                case Cc10System.IndividualRankings: return "Rankings";
                case Cc10System.GuildRankings: return "Guild Rankings";
                case Cc10System.Minigame: return "Minigame";
                default: return "Cargo";
            }
        }

        public static Cc10SectionVm Build(Cc10FrontierClient client, Cc10System system,
            IEnumerable<CardProgressionRecord> cards = null)
        {
            var vm = new Cc10SectionVm { System = system, Title = SectionTitle(system) };
            vm.ReadOnly = client.IsReadOnly(system);

            if (!client.HasState)
            {
                vm.Banner = client.Connection == Cc10Connection.Offline ? Cc10Copy.Offline : Cc10Copy.InFlight;
                vm.ReadOnly = true;
                return vm;
            }

            if (client.IsSystemDisabled(system)) vm.Banner = Cc10Copy.SystemDisabled;
            else if (client.Connection != Cc10Connection.Online) vm.Banner = Cc10Copy.Offline;

            Cc10Snapshot s = client.Snapshot;
            switch (system)
            {
                case Cc10System.CardTraining: AddCards(vm, cards); break;
                case Cc10System.Tavern: AddTavern(vm, s); break;
                case Cc10System.Missions: AddMissions(vm, client, s); break;
                case Cc10System.WorldMap: AddNodes(vm, s); break;
                case Cc10System.NpcSpots: AddSpots(vm, s); break;
                case Cc10System.GuildTerritory: AddTerritories(vm, client, s); break;
                case Cc10System.IndividualResearch: AddResearch(vm, client, s, "Individual"); break;
                case Cc10System.GuildResearch: AddResearch(vm, client, s, "Guild"); break;
                case Cc10System.IndividualRankings: AddRankings(vm, s, "Individual"); break;
                case Cc10System.GuildRankings: AddRankings(vm, s, "Guild"); break;
                case Cc10System.Minigame: AddMinigame(vm, s); break;
                case Cc10System.Cargo: AddCargo(vm, client, s); break;
            }

            // Read-only (offline / paused) disables every action but keeps the rows visible.
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
                    ActionLabel = "Train",
                    Endpoint = Cc10Endpoints.TrainCard,
                    EntityKey = rec.cardId,
                    ActionEnabled = !max && rec.trainingXp >= cost,
                    DisabledReason = max ? "Max level" : "Not enough Training XP",
                };
                row.Payload["cardId"] = rec.cardId;
                vm.Rows.Add(row);
            }
        }

        private static void AddTavern(Cc10SectionVm vm, Cc10Snapshot s)
        {
            Cc10TavernState t = s.tavern ?? new Cc10TavernState();
            bool max = t.level >= Cc10Rules.TavernMaxLevel;
            string detail = max ? "Fully upgraded"
                : t.upgradeActive ? "Upgrading..."
                : "Next: " + t.nextCostGold + " Gold + " + t.nextCostMaterials + " Materials";
            var row = new Cc10Row
            {
                Title = "Tavern  Lv " + t.level,
                Detail = detail,
                ActionLabel = "Upgrade",
                Endpoint = Cc10Endpoints.UpgradeTavern,
                EntityKey = "tavern",
                ActionEnabled = !max && !t.upgradeActive,
                DisabledReason = max ? "Fully upgraded" : "Upgrade in progress",
            };
            vm.Rows.Add(row);
        }

        private static void AddMissions(Cc10SectionVm vm, Cc10FrontierClient client, Cc10Snapshot s)
        {
            int used = s.projection != null ? s.projection.dailyGoldUsed : 0;
            foreach (Cc10MissionRow m in s.missions)
            {
                var row = new Cc10Row
                {
                    Title = MissionTitle(m.kind),
                    EntityKey = m.missionId,
                };
                row.Payload["missionId"] = m.missionId;
                string reward = m.goldReward + " Gold + " + m.materialReward + " Materials";
                switch (m.state)
                {
                    case Cc10StateMachine.Accepted:
                    case Cc10StateMachine.Scouting:
                    case Cc10StateMachine.Active:
                        row.Detail = m.state + " - " + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(m.completeUtcMs));
                        row.ActionLabel = "Abandon";
                        row.Endpoint = Cc10Endpoints.AbandonMission;
                        row.ActionEnabled = true;
                        break;
                    case Cc10StateMachine.Completed:
                        row.Detail = "Completed - " + reward;
                        if (Cc10Rules.ClaimWouldCrossGoldCap(used, m.goldReward))
                            row.Detail += " (" + Cc10Copy.CapReached + ")";
                        row.ActionLabel = "Claim";
                        row.Endpoint = Cc10Endpoints.ClaimMission;
                        row.ActionEnabled = true;
                        break;
                    case Cc10StateMachine.Claimed:
                    case Cc10StateMachine.Failed:
                    case Cc10StateMachine.Abandoned:
                    case Cc10StateMachine.Expired:
                        row.Detail = m.state;
                        break;
                    default: // Available
                        row.Detail = reward + " - " + m.staminaCost + " Stamina - " + m.durationMinutes + "m";
                        row.ActionLabel = "Start";
                        row.Endpoint = Cc10Endpoints.AssignMission;
                        row.ActionEnabled = m.dailyRemaining > 0;
                        row.DisabledReason = "No attempts left. Refreshes in "
                            + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(m.refreshUtcMs));
                        break;
                }
                vm.Rows.Add(row);
            }
        }

        private static string MissionTitle(string kind)
        {
            switch (kind)
            {
                case Cc10Rules.KindNpcPatrol: return "NPC Patrol";
                case Cc10Rules.KindRelicRescue: return "Relic Rescue";
                case Cc10Rules.KindVeinConvoy: return "Vein Convoy";
                default: return kind;
            }
        }

        private static void AddNodes(Cc10SectionVm vm, Cc10Snapshot s)
        {
            foreach (Cc10MapNode n in s.nodes)
            {
                var row = new Cc10Row
                {
                    Title = n.regionId + " / " + n.nodeId,
                    Detail = n.discovered ? "Travel is free" : "Undiscovered",
                    EntityKey = n.nodeId,
                };
                if (n.discovered)
                {
                    row.ActionLabel = "Travel";
                    row.Endpoint = Cc10Endpoints.TravelToNode;
                    row.ActionEnabled = true;
                    row.Payload["nodeId"] = n.nodeId;
                }
                vm.Rows.Add(row);
            }
        }

        private static void AddSpots(Cc10SectionVm vm, Cc10Snapshot s)
        {
            foreach (Cc10NpcSpot p in s.spots)
                vm.Rows.Add(new Cc10Row { Title = MissionTitle(p.kind), Detail = p.state, EntityKey = p.spotId });
        }

        private static void AddTerritories(Cc10SectionVm vm, Cc10FrontierClient client, Cc10Snapshot s)
        {
            foreach (Cc10Territory t in s.territories)
            {
                string owner = string.IsNullOrEmpty(t.ownerGuildId) ? "Unclaimed" : "Held";
                var row = new Cc10Row
                {
                    Title = t.territoryId,
                    Detail = owner + (t.contested ? " - Contested" : "") + " - window "
                        + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(t.windowEndUtcMs)),
                    ActionLabel = "Contribute",
                    Endpoint = Cc10Endpoints.ContributeTerritory,
                    EntityKey = t.territoryId,
                    ActionEnabled = true,
                };
                row.Payload["territoryId"] = t.territoryId;
                vm.Rows.Add(row);
            }
        }

        private static void AddResearch(Cc10SectionVm vm, Cc10FrontierClient client, Cc10Snapshot s, string scope)
        {
            foreach (Cc10ResearchNode n in s.research)
            {
                if (n.scope != scope) continue;
                var row = new Cc10Row { Title = n.nodeId, EntityKey = n.nodeId };
                row.Payload["nodeId"] = n.nodeId;
                switch (n.state)
                {
                    case "Available":
                        row.Detail = n.costGold + " Gold + " + n.costMaterials + " Materials";
                        row.ActionLabel = scope == "Guild" ? "Contribute" : "Start";
                        row.Endpoint = scope == "Guild" ? Cc10Endpoints.ContributeGuildResearch : Cc10Endpoints.StartResearch;
                        row.ActionEnabled = true;
                        break;
                    case "Active":
                        long left = client.Clock.RemainingMs(n.completeUtcMs);
                        row.Detail = "Researching - " + Cc10ServerClock.FormatRemaining(left);
                        if (left <= 0)
                        {
                            row.ActionLabel = "Complete";
                            row.Endpoint = Cc10Endpoints.CompleteResearch;
                            row.ActionEnabled = true;
                        }
                        else if (scope == "Individual")
                        {
                            row.ActionLabel = "Cancel";
                            row.Endpoint = Cc10Endpoints.CancelResearch;
                            row.ActionEnabled = true;
                        }
                        break;
                    case "Locked":
                        row.Detail = "Locked - requires " + string.Join(", ", n.prerequisiteIds ?? new string[0]);
                        break;
                    default:
                        row.Detail = n.state;
                        break;
                }
                vm.Rows.Add(row);
            }
        }

        private static void AddRankings(Cc10SectionVm vm, Cc10Snapshot s, string scope)
        {
            foreach (Cc10RankingBoard b in s.rankings)
            {
                if (b.scope != scope) continue;
                vm.Rows.Add(new Cc10Row { Title = "Season " + b.seasonId, Detail = b.phase, EntityKey = b.seasonId });
                foreach (Cc10RankEntry e in b.entries)
                    vm.Rows.Add(new Cc10Row { Title = "#" + e.rank + "  " + e.displayName, Detail = e.score + " pts", EntityKey = b.seasonId + ":" + e.rank });
            }
        }

        private static void AddMinigame(Cc10SectionVm vm, Cc10Snapshot s)
        {
            Cc10MinigameSession g = s.minigame ?? new Cc10MinigameSession();
            bool running = g.state == Cc10StateMachine.Active || g.state == Cc10StateMachine.Accepted;
            var row = new Cc10Row
            {
                Title = "Side Activity (optional)",
                Detail = running ? "In progress" : "Doesn't affect progression",
                ActionLabel = running ? string.Empty : "Play",
                Endpoint = Cc10Endpoints.StartMinigame,
                EntityKey = "minigame",
                ActionEnabled = !running,
            };
            vm.Rows.Add(row);
        }

        private static void AddCargo(Cc10SectionVm vm, Cc10FrontierClient client, Cc10Snapshot s)
        {
            foreach (Cc10CargoRecord c in s.cargo)
            {
                var row = new Cc10Row { Title = c.cargoId, EntityKey = c.cargoId };
                row.Payload["cargoId"] = c.cargoId;
                if (c.state == "Available")
                {
                    row.Detail = "Ready to accept";
                    row.ActionLabel = "Accept";
                    row.Endpoint = Cc10Endpoints.AcceptCargo;
                    row.ActionEnabled = true;
                }
                else if (Cc10StateMachine.IsClaimable(c.state))
                {
                    row.Detail = "Delivered";
                    row.ActionLabel = "Claim";
                    row.Endpoint = Cc10Endpoints.ClaimCargo;
                    row.ActionEnabled = true;
                }
                else
                {
                    row.Detail = c.state + (Cc10StateMachine.IsTerminal(c.state) ? ""
                        : " - " + Cc10ServerClock.FormatRemaining(client.Clock.RemainingMs(c.expiryUtcMs)));
                }
                vm.Rows.Add(row);
            }
        }
    }
}
