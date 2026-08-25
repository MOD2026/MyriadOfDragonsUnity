using System;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>FRIENDS V1 art shell. Actions refuse while OpenValues stay OPEN.</summary>
    public class FriendsPresenter : MonoBehaviour
    {
        public const string CanvasName = "FriendsCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;

        public void Initialize(Action onBack)
        {
            _onBack = onBack;
            BuildUI();
        }


        public FriendsActionResult MessageForTests()
        {
            var r = FriendsOpenValues.TryMessage();
            SetStatus(r.Message);
            return r;
        }


        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            FriendsUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            BuildNav(); BuildList(); BuildProfile();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("FriendsHeader", typeof(RectTransform));
            topBar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.90f, 1f, 1f);

            GameObject backBtn = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtn.transform.SetParent(topBar.transform, false);
            Image backImg = backBtn.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNavTileButton(backBtn.GetComponent<Button>(), backImg);
            backImg.color = new Color(0.3f, 0.2f, 0.2f);
            backBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                TeardownUI();
                _onBack?.Invoke();
            });
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 56f);
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "FRIENDS",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            Text identity = UISharedFoundation.CreateText(topBar.transform, "SelfIdentity",
                MetagameShellProfileBinding.SelfIdentityLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                new Color(0.8f, 0.85f, 0.7f), true, new Vector2(280f, 28f));
            SetNorm(identity.rectTransform, 0.18f, 0.15f, 0.40f, 0.85f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", FriendsOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildNav()
        {
            GameObject rail = new GameObject("NavRail", typeof(RectTransform));
            rail.transform.SetParent(_canvasObj.transform, false);
            SetNorm(rail.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.14f, 0.88f);
            string[] tabs = { "Friends", "Requests", "Find" };
            float h = 1f / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                GameObject tab = new GameObject($"Nav_{tabs[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                tab.transform.SetParent(rail.transform, false);
                Image img = tab.GetComponent<Image>();
                HomeV3UiLibrary.ApplyNeutralActionButton(tab.GetComponent<Button>(), img, new Color(0.16f, 0.2f, 0.26f, 0.7f));
                tab.GetComponent<Button>().onClick.AddListener(() => SetStatus(FriendsOpenValues.TrySelectTab(idx).Message));
                SetNorm(tab.GetComponent<RectTransform>(), 0.05f, 1f - (i + 1) * h + 0.05f, 0.95f, 1f - i * h - 0.05f);
                UISharedFoundation.CreateText(tab.transform, "Text", tabs[i].ToUpperInvariant(), UITextRole.Caption,
                    TextAnchor.MiddleCenter, Color.white, true, new Vector2(120f, 28f));
            }
        }

        private void BuildList()
        {
            GameObject list = new GameObject("FriendsList", typeof(RectTransform));
            list.transform.SetParent(_canvasObj.transform, false);
            SetNorm(list.GetComponent<RectTransform>(), 0.15f, 0.08f, 0.68f, 0.88f);
            for (int i = 0; i < FriendsOpenValues.VisibleRowCount; i++)
            {
                int row = i;
                float h = 1f / FriendsOpenValues.VisibleRowCount;
                GameObject well = new GameObject($"FriendRow_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(list.transform, false);
                Image img = well.GetComponent<Image>();
                img.color = new Color(0.1f, 0.12f, 0.16f, 0.35f);
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SetStatus(FriendsOpenValues.TrySelectFriend(row).Message));
                SetNorm(well.GetComponent<RectTransform>(), 0.02f, 1f - (i + 1) * h + 0.02f, 0.98f, 1f - i * h - 0.02f);
                string rowLabel = i == 0
                    ? MetagameShellProfileBinding.SelfIdentityLine()
                    : (i == 1
                        ? "No friends list — relationship graph not live"
                        : MetagameShellProfileBinding.EmptyBackendLabel);
                UISharedFoundation.CreateText(well.transform, "DisplayName", rowLabel,
                    UITextRole.Caption, TextAnchor.MiddleLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(400f, 28f));
            }
        }

        private void BuildProfile()
        {
            GameObject drawer = new GameObject("ProfileDrawer", typeof(RectTransform));
            drawer.transform.SetParent(_canvasObj.transform, false);
            SetNorm(drawer.GetComponent<RectTransform>(), 0.70f, 0.08f, 0.95f, 0.88f);
            Text summary = UISharedFoundation.CreateText(drawer.transform, "PublicSummary",
                $"{MetagameShellProfileBinding.SelfIdentityLine()}\n{MetagameShellProfileBinding.WalletLine()}\n\n" +
                "Message/roster OPEN — no friends graph in Save.",
                UITextRole.Body, TextAnchor.UpperCenter,
                new Color(0.9f, 0.88f, 0.75f), true, new Vector2(360f, 160f));
            SetNorm(summary.rectTransform, 0.08f, 0.45f, 0.92f, 0.85f);
            GameObject msg = new GameObject("Btn_Message", typeof(RectTransform), typeof(Image), typeof(Button));
            msg.transform.SetParent(drawer.transform, false);
            Image mImg = msg.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(msg.GetComponent<Button>(), mImg, new Color(0.2f, 0.32f, 0.4f));
            msg.GetComponent<Button>().onClick.AddListener(() => SetStatus(FriendsOpenValues.TryMessage().Message));
            SetNorm(msg.GetComponent<RectTransform>(), 0.1f, 0.2f, 0.9f, 0.35f);
            UISharedFoundation.CreateText(msg.transform, "Text", "MESSAGE", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
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
