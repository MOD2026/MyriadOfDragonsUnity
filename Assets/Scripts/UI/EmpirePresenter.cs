using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>Dedicated Empire screen — construction upgrades live here, not on Home.</summary>
    public class EmpirePresenter : MonoBehaviour
    {
        private GameObject _canvasObj;
        private Action _onBackToHome;
        private Text _empireStatusText;
        private Text _empireMessageText;
        private Text _avatarSummaryText;
        private Text _goldPillText;
        private Text _projectDetailText;
        private GameObject _empireCollectButtonRoot;
        private Action _onOpenAvatar;

        public void Initialize(Action onBackToHome)
        {
            Initialize(onBackToHome, onOpenAvatar: null);
        }

        public void Initialize(Action onBackToHome, Action onOpenAvatar)
        {
            _onBackToHome = onBackToHome;
            _onOpenAvatar = onOpenAvatar;
            BuildUI();
            RefreshPanel();
        }

        public void BuildUIForTests() => BuildUI();

        public void RefreshPanelForTests() => RefreshPanel();

        public GameObject CanvasObjectForTests => _canvasObj;

        public void OpenExpeditionForTests() => OpenExpedition();

        public void OpenBuildingDetailForTests(EmpireBuildingKind kind) => OpenBuildingDetail(kind);

        private void OpenExpedition()
        {
            TeardownUI();
            EmpireExpeditionPresenter expedition = gameObject.GetComponent<EmpireExpeditionPresenter>();
            if (expedition == null) expedition = gameObject.AddComponent<EmpireExpeditionPresenter>();

            expedition.Initialize(
                onBack: () =>
                {
                    if (Application.isPlaying) Destroy(expedition);
                    else DestroyImmediate(expedition);
                    BuildUI();
                    RefreshPanel();
                },
                guildBonusQuery: UnavailableGuildExpeditionBonusQuery.Instance);
        }

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas("EmpireCanvas", new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;

            UISharedFoundation.CreateFullscreenBackground(_canvasObj.transform, "UI/Backdrops/Zihan_City_NO NAMES", new Color(0.12f, 0.11f, 0.16f));

            BuildHeader();
            BuildConstructionPanel();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("EmpireHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.raycastTarget = false;
            if (!HomeV3UiLibrary.TryApplyHeaderFrame(topBg))
                topBg.color = new Color(0.06f, 0.06f, 0.1f, 0.92f);
            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 100f);

            GameObject backBtn = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtn.transform.SetParent(topBar.transform, false);
            Image backImg = backBtn.GetComponent<Image>();
            backImg.color = new Color(0.3f, 0.2f, 0.2f);
            Button back = backBtn.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(back, backImg);
            back.targetGraphic = backImg;
            back.onClick.AddListener(() =>
            {
                TeardownUI();
                _onBackToHome?.Invoke();
            });
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 60f);
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 50f));

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "EMPIRE", UITextRole.Display,
                TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.82f), true, new Vector2(220f, 60f));
            title.fontSize = 32;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(0f, 0.5f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(210f, 0f);
            titleRect.sizeDelta = new Vector2(220f, 60f);

            _avatarSummaryText = UISharedFoundation.CreateText(topBar.transform, "AvatarSummary", "Avatar L1",
                UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#B8A68F"), true, new Vector2(360f, 36f));
            _avatarSummaryText.fontSize = 18;
            RectTransform avatarRect = _avatarSummaryText.rectTransform;
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(440f, 0f);
            avatarRect.sizeDelta = new Vector2(360f, 36f);

            if (_onOpenAvatar != null)
            {
                // Visible HomeV3 nav affordance (was invisible hit-only).
                GameObject avatarBtnObj = new GameObject("OpenAvatarButton", typeof(RectTransform), typeof(Image), typeof(Button));
                avatarBtnObj.transform.SetParent(topBar.transform, false);
                Image avatarBtnImg = avatarBtnObj.GetComponent<Image>();
                avatarBtnImg.color = HexColor("#1A3A4A");
                Button avatarBtn = avatarBtnObj.GetComponent<Button>();
                HomeV3UiLibrary.ApplyNavTileButton(avatarBtn, avatarBtnImg);
                avatarBtn.onClick.AddListener(() =>
                {
                    TeardownUI();
                    _onOpenAvatar.Invoke();
                });
                RectTransform hitRect = avatarBtnObj.GetComponent<RectTransform>();
                hitRect.anchorMin = new Vector2(0f, 0.5f);
                hitRect.anchorMax = new Vector2(0f, 0.5f);
                hitRect.pivot = new Vector2(0f, 0.5f);
                hitRect.anchoredPosition = new Vector2(820f, 0f);
                hitRect.sizeDelta = new Vector2(140f, 52f);
                UISharedFoundation.CreateText(avatarBtnObj.transform, "ActionLabel", "AVATAR",
                    UITextRole.Body, TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(120f, 36f));
            }

            // Empire Expedition (structure-locked farm loop) — opens the rotation shell.
            GameObject expeditionBtnObj = new GameObject("OpenExpeditionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            expeditionBtnObj.transform.SetParent(topBar.transform, false);
            Image expeditionImg = expeditionBtnObj.GetComponent<Image>();
            expeditionImg.color = HexColor("#1A3A4A");
            Button expeditionBtn = expeditionBtnObj.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(expeditionBtn, expeditionImg);
            expeditionBtn.onClick.AddListener(OpenExpedition);
            RectTransform expeditionRect = expeditionBtnObj.GetComponent<RectTransform>();
            expeditionRect.anchorMin = new Vector2(0f, 0.5f);
            expeditionRect.anchorMax = new Vector2(0f, 0.5f);
            expeditionRect.pivot = new Vector2(0f, 0.5f);
            expeditionRect.anchoredPosition = new Vector2(_onOpenAvatar != null ? 980f : 820f, 0f);
            expeditionRect.sizeDelta = new Vector2(170f, 52f);
            UISharedFoundation.CreateText(expeditionBtnObj.transform, "ActionLabel", "EXPEDITION",
                UITextRole.Body, TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(150f, 36f));

            GameObject resourceGroup = new GameObject("ResourceGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            resourceGroup.transform.SetParent(topBar.transform, false);
            RectTransform resRect = resourceGroup.GetComponent<RectTransform>();
            resRect.anchorMin = new Vector2(1f, 0.5f);
            resRect.anchorMax = new Vector2(1f, 0.5f);
            resRect.pivot = new Vector2(1f, 0.5f);
            resRect.anchoredPosition = new Vector2(-30f, 0f);
            resRect.sizeDelta = new Vector2(220f, 60f);
            HorizontalLayoutGroup hlg = resourceGroup.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.spacing = 12f;
            hlg.childControlWidth = false;

            PlayerProfile profile = SaveManager.SaveData;
            int gold = profile != null ? profile.gold : 0;
            _goldPillText = HomeV3UiLibrary.CreateResourcePill(resourceGroup.transform, "home_resource_gold_pill_v3", "Gold", $"{gold}", 190f);
            RefreshAvatarSummary(profile);
        }

        private void BuildConstructionPanel()
        {
            GameObject empireRoot = new GameObject("EmpireConstructionRoot", typeof(RectTransform), typeof(Image));
            empireRoot.transform.SetParent(_canvasObj.transform, false);
            Image empireBg = empireRoot.GetComponent<Image>();
            empireBg.color = HexColor("#1E2630", 0.95f);
            empireBg.raycastTarget = false;
            RectTransform empireRect = empireRoot.GetComponent<RectTransform>();
            // Fill the working area under the 100px header (y≈0.907–1.0). The old 0.12–0.82 band
            // left unused strips above and below, and status/collect overlapped the Gate row.
            empireRect.anchorMin = new Vector2(0.03f, 0.03f);
            empireRect.anchorMax = new Vector2(0.97f, 0.88f);
            empireRect.offsetMin = Vector2.zero;
            empireRect.offsetMax = Vector2.zero;

            Text subtitle = UISharedFoundation.CreateText(empireRoot.transform, "EmpireSubtitle",
                "Castle · Barracks · Gate", UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#B8A68F"),
                true, new Vector2(800f, 36f));
            subtitle.fontSize = 20;
            SetNormalizedRect(subtitle.rectTransform, 0.03f, 0.93f, 0.50f, 0.99f);

            BuildVariantStrip(empireRoot.transform);
            CreateBuildingRow(empireRoot.transform, "CastleRow", "UpgradeCastleButton",
                EmpireBuildingKind.Castle, 0.64f, 0.84f, OnUpgradeCastle);
            CreateBuildingRow(empireRoot.transform, "BarracksRow", "UpgradeBarracksButton",
                EmpireBuildingKind.Barracks, 0.43f, 0.62f, OnUpgradeBarracks);
            CreateBuildingRow(empireRoot.transform, "GateRow", "UpgradeGateButton",
                EmpireBuildingKind.Gate, 0.22f, 0.41f, OnUpgradeGate);

            _empireStatusText = UISharedFoundation.CreateText(empireRoot.transform, "EmpireStatus", "",
                UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#B8A68F"), true, new Vector2(1200f, 48f));
            _empireStatusText.fontSize = 18;
            SetNormalizedRect(_empireStatusText.rectTransform, 0.03f, 0.12f, 0.68f, 0.20f);

            _projectDetailText = UISharedFoundation.CreateText(empireRoot.transform, "ActiveProjectDetail", "",
                UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#F2E5C9"), true, new Vector2(900f, 40f));
            _projectDetailText.fontSize = 16;
            SetNormalizedRect(_projectDetailText.rectTransform, 0.03f, 0.05f, 0.68f, 0.11f);

            _empireMessageText = UISharedFoundation.CreateText(empireRoot.transform, "EmpireMessage", "",
                UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#E8A87C"), true, new Vector2(500f, 48f));
            _empireMessageText.fontSize = 16;
            SetNormalizedRect(_empireMessageText.rectTransform, 0.03f, 0.00f, 0.68f, 0.05f);

            _empireCollectButtonRoot = CreateActionButton(empireRoot.transform, "CollectConstructionButton",
                "COLLECT UPGRADE", 0.71f, 0.04f, 0.97f, 0.20f, OnCollectConstruction);
        }

        private void RefreshPanel()
        {
            if (_empireStatusText == null || _canvasObj == null) return;

            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null)
            {
                _empireStatusText.text = "Empire data unavailable.";
                if (_empireCollectButtonRoot != null) _empireCollectButtonRoot.SetActive(false);
                return;
            }

            profile.ApplyDataToEmpire();
            RefreshAvatarSummary(profile);
            if (_goldPillText != null)
                _goldPillText.text = $"{profile.gold}";

            int deckSlots = profile.Empire.DeckSlotCount;
            int gateChapter = PlayerEmpireData.GetHighestCampaignChapterAllowed(profile.gateLevel);
            int castleResourceBonus = PlayerEmpireData.CastleResourceBonusForLevel(profile.castleLevel);
            int castleHealthBonus = PlayerEmpireData.CastleHealthBonusForLevel(profile.castleLevel);
            // Live totals after ApplyDataToEmpire (Block AA display-only — same readers battle uses).
            int liveResourceCap = profile.Empire.ResourceCap;
            int liveStartHp = profile.Empire.StartingAvatarHealth;

            EmpireConstructionState construction = profile.empireConstruction;
            EmpireConstructionService.AdvanceIfDue(profile);
            construction = profile.empireConstruction;

            bool building = construction != null && construction.status == EmpireConstructionStatus.Building;
            bool ready = construction != null && construction.status == EmpireConstructionStatus.ReadyToCollect;
            bool projectActive = building || ready;
            EmpireBuildingId? queuedBuilding = projectActive ? construction.buildingId : (EmpireBuildingId?)null;

            Text castleRow = _canvasObj.transform.Find("EmpireConstructionRoot/CastleRow/RowSummary")?.GetComponent<Text>();
            Text barracksRow = _canvasObj.transform.Find("EmpireConstructionRoot/BarracksRow/RowSummary")?.GetComponent<Text>();
            Text gateRow = _canvasObj.transform.Find("EmpireConstructionRoot/GateRow/RowSummary")?.GetComponent<Text>();

            if (castleRow != null)
            {
                castleRow.text = FormatCastleRow(
                    profile.castleLevel, castleResourceBonus, castleHealthBonus,
                    liveResourceCap, liveStartHp, queuedBuilding == EmpireBuildingId.Castle);
            }

            int barracksTarget = PlayerEmpireData.NextPaidBarracksMilestone(profile.barracksLevel);
            int barracksCost = barracksTarget == 0 ? 0 : PlayerEmpireData.GoldCostForBarracksUpgrade(profile.barracksLevel, barracksTarget);
            if (barracksRow != null)
            {
                barracksRow.text = FormatBarracksRow(
                    profile.barracksLevel, barracksTarget, deckSlots, barracksCost,
                    queuedBuilding == EmpireBuildingId.Barracks);
            }

            int gateTarget = PlayerEmpireData.NextPaidGateMilestone(profile.gateLevel);
            int gateCost = gateTarget == 0 ? 0 : PlayerEmpireData.GoldCostForGateUpgrade(profile.gateLevel, gateTarget);
            if (gateRow != null)
            {
                gateRow.text = FormatGateRowWithPreview(
                    profile.gateLevel, gateChapter, gateTarget, gateCost,
                    queuedBuilding == EmpireBuildingId.Gate);
            }

            if (ready)
            {
                _empireStatusText.text =
                    $"QUEUE · {construction.buildingId} → L{construction.targetLevel} · DONE — Collect to apply ({construction.costGold:N0} Gold / {construction.costMaterials:N0} Materials already spent).";
            }
            else if (building)
            {
                string remaining = EmpireConstructionTimer.FormatRemaining(
                    construction.endsAtUtcMs, EmpireConstructionTimer.UtcNowMs);
                _empireStatusText.text =
                    $"QUEUE · {construction.buildingId} → L{construction.targetLevel} · Building · {remaining} left ({construction.costGold:N0} Gold / {construction.costMaterials:N0} Materials spent).";
            }
            else
            {
                _empireStatusText.text =
                    "QUEUE · empty · One project at a time. Upgrade charges Gold + Materials and builds on the locked 30min-14d timer - then Collect.";
            }

            if (_projectDetailText != null)
            {
                if (ready)
                {
                    _projectDetailText.text =
                        $"Building now: {construction.buildingId} · Target L{construction.targetLevel} · Status: Ready to Collect";
                }
                else if (building)
                {
                    string remaining = EmpireConstructionTimer.FormatRemaining(
                        construction.endsAtUtcMs, EmpireConstructionTimer.UtcNowMs);
                    int durationSec = EmpireConstructionTimer.DurationSecondsForTargetLevel(construction.targetLevel);
                    _projectDetailText.text =
                        $"Building now: {construction.buildingId} · Target L{construction.targetLevel} · Timer {EmpireConstructionTimer.FormatDuration(durationSec)} · Remaining {remaining}";
                }
                else
                {
                    _projectDetailText.text =
                        "No active project. Next-tier payoffs are shown on each row before you spend Gold + Materials.";
                }
            }

            if (_empireCollectButtonRoot != null)
                _empireCollectButtonRoot.SetActive(ready);

            // Active project (Building or Ready) blocks starting another upgrade.
            SetUpgradeButtonsInteractable(!projectActive);
        }

        /// <summary>Castle row: current live Cap/Start HP + next-level delta before Gold commit.</summary>
        private static string FormatCastleRow(
            int castleLevel, int castleResourceBonus, int castleHealthBonus,
            int liveCap, int liveStartHp, bool isQueued)
        {
            string queueMark = isQueued ? "▶ QUEUE · " : string.Empty;
            string current =
                $"Progression spine, capacity milestones\n{queueMark}Castle L{castleLevel} · Cap {liveCap} · Start HP {liveStartHp} (+{castleResourceBonus} Cap / +{castleHealthBonus} HP from Castle)";

            if (castleLevel >= PlayerEmpireData.MaxCastleLevel)
                return $"{current} · MAX";

            int nextLevel = castleLevel + 1;
            int cost = PlayerEmpireData.GoldCostForCastleUpgrade(castleLevel);
            int nextResBonus = PlayerEmpireData.CastleResourceBonusForLevel(nextLevel);
            int nextHpBonus = PlayerEmpireData.CastleHealthBonusForLevel(nextLevel);
            int dCap = nextResBonus - castleResourceBonus;
            int dHp = nextHpBonus - castleHealthBonus;
            string deltaCap = dCap > 0 ? $"+{dCap} Cap" : "Cap unchanged";
            string deltaHp = dHp > 0 ? $"+{dHp} Start HP" : "Start HP unchanged";
            return $"{current} → Next L{nextLevel}: {deltaCap}, {deltaHp} · {cost:N0} Gold";
        }

        /// <summary>Barracks row: current Deck Slots + next milestone slot count before Gold commit.</summary>
        private static string FormatBarracksRow(
            int barracksLevel, int targetMilestone, int currentDeckSlots, int goldCost, bool isQueued)
        {
            string queueMark = isQueued ? "▶ QUEUE · " : string.Empty;
            string current = $"Recruits soldiers; deck-slot/Resource-regen/replenishment\n{queueMark}Barracks L{barracksLevel} · {currentDeckSlots} Deck Slots";

            if (targetMilestone == 0)
                return $"{current} · MAX";

            int nextSlots = PlayerEmpireData.DeckSlotsForBarracksLevel(targetMilestone);
            string slotDelta = nextSlots > currentDeckSlots
                ? $"{currentDeckSlots} → {nextSlots} Deck Slots"
                : $"{nextSlots} Deck Slots (unchanged)";
            return $"{current} → Next L{targetMilestone}: {slotDelta} · {goldCost:N0} Gold";
        }

        /// <summary>Gate row: Campaign chapter open now + next milestone chapter unlock before Gold commit.</summary>
        private static string FormatGateRowWithPreview(
            int gateLevel, int highestChapterAllowed, int nextGateMilestone, int gateUpgradeGold, bool isQueued)
        {
            string queueMark = isQueued ? "▶ QUEUE · " : string.Empty;
            string baseSummary = FormatGateRowSummary(gateLevel, highestChapterAllowed, nextGateMilestone, gateUpgradeGold);
            if (nextGateMilestone == 0)
                return queueMark + baseSummary;

            int chapterAtNext = PlayerEmpireData.GetHighestCampaignChapterAllowed(nextGateMilestone);
            string nextPreview = chapterAtNext > highestChapterAllowed
                ? $" · Next L{nextGateMilestone} unlocks through Ch{chapterAtNext}"
                : $" · Next L{nextGateMilestone} (Campaign still Ch{highestChapterAllowed})";
            return queueMark + baseSummary + nextPreview;
        }

        private void SetUpgradeButtonsInteractable(bool interactable)
        {
            if (_canvasObj == null) return;
            foreach (string path in new[]
                     {
                         "EmpireConstructionRoot/CastleRow/UpgradeCastleButton",
                         "EmpireConstructionRoot/BarracksRow/UpgradeBarracksButton",
                         "EmpireConstructionRoot/GateRow/UpgradeGateButton",
                     })
            {
                Button button = _canvasObj.transform.Find(path)?.GetComponent<Button>();
                if (button != null) button.interactable = interactable;
            }
        }

        private void RefreshAvatarSummary(PlayerProfile profile)
        {
            if (_avatarSummaryText == null) return;
            if (profile == null)
            {
                _avatarSummaryText.text = "Avatar unavailable";
                return;
            }

            profile.ApplyDataToEmpire();
            _avatarSummaryText.text =
                $"Avatar L{Mathf.Max(1, profile.avatarLevel)} · Cap {profile.Empire.ResourceCap} · Start HP {profile.Empire.StartingAvatarHealth}";
        }

        private void OnUpgradeCastle() => TryStartUpgrade(EmpireBuildingId.Castle);
        private void OnUpgradeBarracks() => TryStartUpgrade(EmpireBuildingId.Barracks);
        private void OnUpgradeGate() => TryStartUpgrade(EmpireBuildingId.Gate);

        /// <summary>
        /// Gate row clarity (Block Z): show which Campaign chapter is open now, and — without
        /// inventing Gate-level thresholds — prompt upgrading to open the next chapter when
        /// <see cref="PlayerEmpireData.GetHighestCampaignChapterAllowed"/> is below max (1–10).
        /// Chapter ceiling mirrors PlayerEmpireData GateLevelForChapter (read-only; do not invent 11).
        /// </summary>
        public const int MaxCampaignChapterForGateCopy = 10;

        public static string FormatGateRowSummary(int gateLevel, int highestChapterAllowed, int nextGateMilestone, int gateUpgradeGold)
        {
            int openChapter = Mathf.Clamp(highestChapterAllowed, 1, MaxCampaignChapterForGateCopy);
            string openCopy = $"Campaign open: Ch{openChapter}";

            string nextChapterCopy = string.Empty;
            if (openChapter < MaxCampaignChapterForGateCopy)
            {
                int nextChapter = openChapter + 1;
                if (!PlayerEmpireData.IsCampaignChapterAllowedByGate(gateLevel, nextChapter))
                    nextChapterCopy = $" · Upgrade Gate to open Ch{nextChapter}";
            }

            const string purpose = "World-map defence, protected-loot floor\n";
            if (nextGateMilestone == 0)
                return $"{purpose}Gate L{gateLevel} · {openCopy}{nextChapterCopy} · MAX";

            return $"{purpose}Gate L{gateLevel} → L{nextGateMilestone} · {openCopy}{nextChapterCopy} · {gateUpgradeGold:N0} Gold";
        }

        private void TryStartUpgrade(EmpireBuildingId building)
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (profile == null) return;

            string projectId = $"empire-{building.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
            if (!EmpireConstructionService.TryStart(profile, building, projectId, out string error))
            {
                SetMessage(error ?? "Upgrade could not start.");
                RefreshPanel();
                return;
            }

            SaveManager.Save();
            int durationSec = EmpireConstructionTimer.DurationSecondsForTargetLevel(
                profile.empireConstruction.targetLevel);
            SetMessage($"{building} queued - building for {EmpireConstructionTimer.FormatDuration(durationSec)}.");
            RefreshPanel();
        }

        private void OnCollectConstruction()
        {
            PlayerProfile profile = SaveManager.SaveData;
            if (profile?.empireConstruction == null) return;

            EmpireBuildingId finished = profile.empireConstruction.buildingId;
            int target = profile.empireConstruction.targetLevel;

            if (!EmpireConstructionService.TryClaimComplete(profile, profile.empireConstruction.projectId))
            {
                SetMessage("Nothing ready to collect.");
                RefreshPanel();
                return;
            }

            SaveManager.Save();
            SetMessage($"{finished} collected — now L{target}. Queue empty.");
            RefreshPanel();
        }

        private void SetMessage(string message)
        {
            if (_empireMessageText != null)
                _empireMessageText.text = message ?? string.Empty;
        }

        private void OpenBuildingDetail(EmpireBuildingKind kind)
        {
            EmpireBuildingDetailPresenter detail = gameObject.GetComponent<EmpireBuildingDetailPresenter>();
            if (detail == null) detail = gameObject.AddComponent<EmpireBuildingDetailPresenter>();

            detail.Initialize(
                kind,
                onClose: () =>
                {
                    if (Application.isPlaying) Destroy(detail);
                    else DestroyImmediate(detail);
                },
                onV1Upgrade: requested =>
                {
                    if (requested == EmpireBuildingKind.Castle) OnUpgradeCastle();
                    else if (requested == EmpireBuildingKind.Barracks) OnUpgradeBarracks();
                    else if (requested == EmpireBuildingKind.Gate) OnUpgradeGate();
                    RefreshPanel();
                });
        }

        private void OpenGuildHallEntry()
        {
            // Close any open building-detail overlay first so only one popup is live.
            EmpireBuildingDetailPresenter existingDetail = gameObject.GetComponent<EmpireBuildingDetailPresenter>();
            if (existingDetail != null)
            {
                if (Application.isPlaying) Destroy(existingDetail);
                else DestroyImmediate(existingDetail);
            }

            GuildHallEntryPresenter entry = gameObject.GetComponent<GuildHallEntryPresenter>();
            if (entry == null) entry = gameObject.AddComponent<GuildHallEntryPresenter>();

            entry.Initialize(() =>
            {
                if (Application.isPlaying) Destroy(entry);
                else DestroyImmediate(entry);
            });
        }

        private void BuildVariantStrip(Transform parent)
        {
            GameObject strip = new GameObject("VariantStrip", typeof(RectTransform));
            strip.transform.SetParent(parent, false);
            SetNormalizedRect(strip.GetComponent<RectTransform>(), 0.03f, 0.855f, 0.97f, 0.925f);

            CreateVariantChip(strip.transform, "Chip_GuildHall", "GUILD HALL", EmpireBuildingKind.GuildHall, 0.00f, 0.32f,
                onClickOverride: OpenGuildHallEntry);
            CreateVariantChip(strip.transform, "Chip_Prison", "PRISON", EmpireBuildingKind.Prison, 0.34f, 0.66f);
            CreateVariantChip(strip.transform, "Chip_Embassy", "EMBASSY", EmpireBuildingKind.Embassy, 0.68f, 1f);
        }

        private void CreateVariantChip(Transform parent, string name, string label, EmpireBuildingKind kind,
            float left, float right, System.Action onClickOverride = null)
        {
            GameObject chip = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            chip.transform.SetParent(parent, false);
            Image img = chip.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(chip.GetComponent<Button>(), img, HexColor("#1A3A4A"));
            EmpireBuildingKind captured = kind;
            if (onClickOverride != null)
                chip.GetComponent<Button>().onClick.AddListener(() => onClickOverride());
            else
                chip.GetComponent<Button>().onClick.AddListener(() => OpenBuildingDetail(captured));
            SetNormalizedRect(chip.GetComponent<RectTransform>(), left, 0.08f, right, 0.92f);
            UISharedFoundation.CreateText(chip.transform, "Label", label, UITextRole.Caption, TextAnchor.MiddleCenter,
                HexColor("#F2E5C9"), true, new Vector2(200f, 28f));
        }

        private void CreateBuildingRow(Transform parent, string rowName, string buttonName,
            EmpireBuildingKind kind, float bottom, float top, UnityEngine.Events.UnityAction onUpgrade)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            Image rowBg = row.GetComponent<Image>();
            rowBg.color = HexColor("#141A22", 0.92f);
            rowBg.raycastTarget = true;
            Button rowButton = row.GetComponent<Button>();
            rowButton.targetGraphic = rowBg;
            EmpireBuildingKind captured = kind;
            rowButton.onClick.AddListener(() => OpenBuildingDetail(captured));
            SetNormalizedRect(row.GetComponent<RectTransform>(), 0.03f, bottom, 0.97f, top);

            Text rowText = UISharedFoundation.CreateText(row.transform, "RowSummary", "",
                UITextRole.Body, TextAnchor.MiddleLeft, HexColor("#F2E5C9"), true, new Vector2(900f, 80f));
            rowText.fontSize = 20;
            rowText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rowText.verticalOverflow = VerticalWrapMode.Overflow;
            rowText.raycastTarget = false;
            SetNormalizedRect(rowText.rectTransform, 0.02f, 0.08f, 0.72f, 0.92f);

            CreateActionButton(row.transform, buttonName, "UPGRADE", 0.74f, 0.1f, 0.98f, 0.9f, onUpgrade);
        }

        private static GameObject CreateActionButton(Transform parent, string name, string label,
            float left, float bottom, float right, float top, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonRoot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(parent, false);
            Image bg = buttonRoot.GetComponent<Image>();
            bg.color = HexColor("#1A3A4A");
            Button button = buttonRoot.GetComponent<Button>();
            HomeV3UiLibrary.ApplyNavTileButton(button, bg);
            button.onClick.AddListener(onClick);
            SetNormalizedRect(buttonRoot.GetComponent<RectTransform>(), left, bottom, right, top);

            Text buttonLabel = UISharedFoundation.CreateText(buttonRoot.transform, "ActionLabel", label,
                UITextRole.Display, TextAnchor.MiddleCenter, HexColor("#F2E5C9"), true, new Vector2(220f, 36f));
            buttonLabel.fontSize = 16;
            buttonLabel.fontStyle = FontStyle.Bold;
            SetNormalizedRect(buttonLabel.rectTransform, 0.05f, 0.05f, 0.95f, 0.95f);
            return buttonRoot;
        }

        private static void SetNormalizedRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                c.a = alpha;
                return c;
            }

            return Color.white;
        }

        public void TeardownUI()
        {
            EmpireBuildingDetailPresenter detail = GetComponent<EmpireBuildingDetailPresenter>();
            if (detail != null)
            {
                detail.TeardownUI();
                if (Application.isPlaying) Destroy(detail);
                else DestroyImmediate(detail);
            }

            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
