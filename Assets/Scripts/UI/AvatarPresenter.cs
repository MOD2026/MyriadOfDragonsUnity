using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Dedicated Avatar screen (Block AC) — read-only live combat economy from Empire readers.
    /// Construction upgrades stay on <see cref="EmpirePresenter"/>.
    /// </summary>
    public class AvatarPresenter : MonoBehaviour
    {
        private GameObject _canvasObj;
        private Action _onBackToHome;
        private Action _onOpenEmpire;
        private Action _onOpenSpellLoadout;

        public void Initialize(Action onBackToHome, Action onOpenEmpire = null, Action onOpenSpellLoadout = null)
        {
            _onBackToHome = onBackToHome;
            _onOpenEmpire = onOpenEmpire;
            _onOpenSpellLoadout = onOpenSpellLoadout;
            BuildUI();
        }

        public GameObject CanvasObjectForTests => _canvasObj;

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            // Apply Reduced Motion (and audio) so InteractionStateController decorative loops honor Settings.
            PlayerSettingsService.ApplyFromProfile(SaveManager.SaveData);

            Canvas canvas = UISharedFoundation.CreateScreenCanvas("AvatarCanvas", new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;

            // docs/REVAMP_V2_APPROVAL_REGISTRY.md's `avatar_profile_states_8_distinct.png` row -
            // Zihan direct owner approval, 2026-09-12 (863f2115). Replaces the older
            // "Zihan_City_NO NAMES" placeholder backdrop; no test pins that sprite name, so no
            // locked expectation is broken. Falls back to the same flat colour as before.
            UISharedFoundation.CreateFullscreenBackground(_canvasObj.transform,
                "UI/RevampV2Approved/AvatarProfile/avatar_profile_states_v1", new Color(0.1f, 0.11f, 0.15f));

            BuildHeader();
            BuildBody();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("AvatarHeader", typeof(RectTransform), typeof(Image));
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
            back.onClick.AddListener(() =>
            {
                TeardownUI();
                _onBackToHome?.Invoke();
            });
            // Top-anchored, not vertically centered - same real overlap bug already fixed on
            // Empire (7185a4c/EmpirePresenter.cs) once the panel below got a real bordered
            // sprite: a center-anchored 60px-tall button in a 100px header only left 20px
            // clearance, which the flat-color panel never exposed since EmpireLayoutTests'
            // overlap check only flags Images with a real sprite.
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            // VS-UI-AVATAR-BACK-TARGET-006: 200x72, matching SettingsPresenter.CreateHeaderButton's
            // landscape tap-target standard. The prior 160x40 was smaller than the 160x60 that
            // change already rejected as unreliable, and at 40px the nav-tile frame sprite - sized
            // for a ~72px plate - rendered its top ornament flush against the screen edge. -14
            // keeps the whole 72px plate inside the 100px header with clearance at both ends.
            backRect.anchoredPosition = new Vector2(30f, -14f);
            backRect.sizeDelta = new Vector2(200f, 72f);
            // UITextRole.Body's shared default (16px, UIFrozenTokens.TypeBodySize) is under the
            // global 22px floor - explicit override here rather than waiting on that frozen
            // constant, which is a separate, larger decision (touches every screen, not just this
            // button). 56px box comfortably fits a 22px line.
            UISharedFoundation.AddLocalGradientScrim(backBtn.transform, Vector2.zero, new Vector2(200f, 72f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text backLabel = UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(180f, 56f));
            backLabel.fontSize = 22;
            backLabel.fontStyle = FontStyle.Bold;
            backBtn.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;

            UISharedFoundation.CreateText(topBar.transform, "Title", "AVATAR", UITextRole.Display, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.92f, 0.82f), true, new Vector2(800f, 60f)).fontSize = 32;
        }

        private void BuildBody()
        {
            PlayerProfile profile = SaveManager.SaveData;
            string name = profile != null ? profile.playerName : "Sovereign";
            int avatarLevel = 1;
            int cap = 0;
            int turn1 = 0;
            int startHp = 0;
            int deckSlots = 0;
            int castle = 1;
            int barracks = 1;
            int gate = 1;

            if (profile != null)
            {
                profile.ApplyDataToEmpire();
                avatarLevel = Mathf.Max(1, profile.avatarLevel);
                cap = profile.Empire.ResourceCap;
                turn1 = profile.Empire.Turn1Resource;
                startHp = profile.Empire.StartingAvatarHealth;
                deckSlots = profile.Empire.DeckSlotCount;
                castle = profile.castleLevel;
                barracks = profile.barracksLevel;
                gate = profile.gateLevel;
            }

            GameObject panel = new GameObject("AvatarBody", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvasObj.transform, false);
            Image panelBg = panel.GetComponent<Image>();
            panelBg.raycastTarget = false;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.03f, 0.04f);
            panelRect.anchorMax = new Vector2(0.97f, 0.87f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
            UISharedFoundation.ApplyFramedPanel(panelBg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);

            Text nameText = UISharedFoundation.CreateText(panel.transform, "AvatarName", name.ToUpperInvariant(),
                UITextRole.Display, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(700f, 48f));
            nameText.fontSize = 36;
            nameText.fontStyle = FontStyle.Bold;
            SetNorm(nameText.rectTransform, 0.04f, 0.84f, 0.96f, 0.96f);

            Text levelText = UISharedFoundation.CreateText(panel.transform, "AvatarLevel", $"Avatar Level {avatarLevel}",
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(700f, 36f));
            levelText.fontSize = 24;
            SetNorm(levelText.rectTransform, 0.04f, 0.74f, 0.96f, 0.84f);

            string combatBlock =
                $"Cap  {cap}\n" +
                $"Turn-1 Resource  {turn1}\n" +
                $"Start HP  {startHp}\n" +
                $"Deck Slots  {deckSlots}";
            Text combatText = UISharedFoundation.CreateText(panel.transform, "CombatStats", combatBlock,
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(700f, 160f));
            combatText.fontSize = 26;
            SetNorm(combatText.rectTransform, 0.04f, 0.36f, 0.96f, 0.72f);

            string buildings =
                $"Empire context (upgrade on Empire screen)\n" +
                $"Castle L{castle} · Barracks L{barracks} · Gate L{gate}";
            Text buildingText = UISharedFoundation.CreateText(panel.transform, "BuildingContext", buildings,
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(900f, 80f));
            buildingText.fontSize = 22;
            SetNorm(buildingText.rectTransform, 0.04f, 0.18f, 0.96f, 0.34f);

            if (_onOpenEmpire != null)
            {
                GameObject empireBtn = new GameObject("Btn_OpenEmpire", typeof(RectTransform), typeof(Image), typeof(Button));
                empireBtn.transform.SetParent(panel.transform, false);
                Image empireImg = empireBtn.GetComponent<Image>();
                empireImg.color = new Color(0.18f, 0.35f, 0.32f);
                Button empireButton = empireBtn.GetComponent<Button>();
                HomeV3UiLibrary.ApplyNavTileButton(empireButton, empireImg);
                empireButton.onClick.AddListener(() =>
                {
                    TeardownUI();
                    _onOpenEmpire.Invoke();
                });
                SetNorm(empireBtn.GetComponent<RectTransform>(), 0.04f, 0.04f, 0.36f, 0.14f);
                UISharedFoundation.CreateText(empireBtn.transform, "Label", "OPEN EMPIRE", UITextRole.Body,
                    TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(280f, 40f));
                empireBtn.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier2Section;
            }

            if (_onOpenSpellLoadout != null)
            {
                GameObject loadoutBtn = new GameObject("Btn_SpellLoadout", typeof(RectTransform), typeof(Image), typeof(Button));
                loadoutBtn.transform.SetParent(panel.transform, false);
                Image loadoutImg = loadoutBtn.GetComponent<Image>();
                loadoutImg.color = new Color(0.22f, 0.32f, 0.4f);
                Button loadoutButton = loadoutBtn.GetComponent<Button>();
                HomeV3UiLibrary.ApplyNavTileButton(loadoutButton, loadoutImg);
                loadoutButton.onClick.AddListener(() =>
                {
                    TeardownUI();
                    _onOpenSpellLoadout.Invoke();
                });
                SetNorm(loadoutBtn.GetComponent<RectTransform>(), 0.40f, 0.04f, 0.72f, 0.14f);
                UISharedFoundation.CreateText(loadoutBtn.transform, "Label", "SPELL LOADOUT", UITextRole.Body,
                    TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(280f, 40f));
                loadoutBtn.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier2Section;
            }
        }

        private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void TeardownUI()
        {
            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
