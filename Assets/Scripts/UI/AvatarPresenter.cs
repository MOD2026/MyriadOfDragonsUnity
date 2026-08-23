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

        public void Initialize(Action onBackToHome, Action onOpenEmpire = null)
        {
            _onBackToHome = onBackToHome;
            _onOpenEmpire = onOpenEmpire;
            BuildUI();
        }

        public GameObject CanvasObjectForTests => _canvasObj;

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas("AvatarCanvas", new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;

            UISharedFoundation.CreateFullscreenBackground(_canvasObj.transform, "UI/Backdrops/Zihan_City_NO NAMES",
                new Color(0.1f, 0.11f, 0.15f));

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
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 60f);
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(140f, 50f));

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
            panelBg.color = new Color(0.12f, 0.14f, 0.19f, 0.94f);
            panelBg.raycastTarget = false;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.18f);
            panelRect.anchorMax = new Vector2(0.88f, 0.82f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            GameObject crestObj = new GameObject("Crest", typeof(RectTransform), typeof(Image));
            crestObj.transform.SetParent(panel.transform, false);
            Image crest = crestObj.GetComponent<Image>();
            crest.sprite = HomeV3UiLibrary.Load("home_identity_crest_v3");
            crest.preserveAspect = true;
            crest.raycastTarget = false;
            crest.color = crest.sprite != null ? Color.white : new Color(0.3f, 0.35f, 0.42f);
            RectTransform crestRect = crestObj.GetComponent<RectTransform>();
            crestRect.anchorMin = new Vector2(0.08f, 0.35f);
            crestRect.anchorMax = new Vector2(0.32f, 0.88f);
            crestRect.offsetMin = Vector2.zero;
            crestRect.offsetMax = Vector2.zero;

            Text nameText = UISharedFoundation.CreateText(panel.transform, "AvatarName", name.ToUpperInvariant(),
                UITextRole.Display, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(700f, 48f));
            nameText.fontSize = 34;
            nameText.fontStyle = FontStyle.Bold;
            SetNorm(nameText.rectTransform, 0.36f, 0.78f, 0.96f, 0.92f);

            Text levelText = UISharedFoundation.CreateText(panel.transform, "AvatarLevel", $"Avatar Level {avatarLevel}",
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(700f, 36f));
            levelText.fontSize = 22;
            SetNorm(levelText.rectTransform, 0.36f, 0.68f, 0.96f, 0.78f);

            string combatBlock =
                $"Cap  {cap}\n" +
                $"Turn-1 Resource  {turn1}\n" +
                $"Start HP  {startHp}\n" +
                $"Deck Slots  {deckSlots}";
            Text combatText = UISharedFoundation.CreateText(panel.transform, "CombatStats", combatBlock,
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(700f, 160f));
            combatText.fontSize = 22;
            SetNorm(combatText.rectTransform, 0.36f, 0.38f, 0.96f, 0.66f);

            string buildings =
                $"Empire context (upgrade on Empire screen)\n" +
                $"Castle L{castle} · Barracks L{barracks} · Gate L{gate}";
            Text buildingText = UISharedFoundation.CreateText(panel.transform, "BuildingContext", buildings,
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.72f, 0.66f, 0.56f), true, new Vector2(900f, 80f));
            buildingText.fontSize = 18;
            SetNorm(buildingText.rectTransform, 0.08f, 0.18f, 0.96f, 0.34f);

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
                SetNorm(empireBtn.GetComponent<RectTransform>(), 0.36f, 0.04f, 0.72f, 0.14f);
                UISharedFoundation.CreateText(empireBtn.transform, "Label", "OPEN EMPIRE", UITextRole.Body,
                    TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(280f, 40f));
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
