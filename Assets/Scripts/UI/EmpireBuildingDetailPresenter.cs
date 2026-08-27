using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Empire Building Detail popup V1. Overlay on the live Empire screen (does not destroy it).
    /// Guild Hall is non-upgrade. Prison/Embassy use pending-server framing. Duration stays OPEN.
    /// </summary>
    public class EmpireBuildingDetailPresenter : MonoBehaviour
    {
        public const string CanvasName = "EmpireBuildingDetailCanvas";

        private GameObject _canvasObj;
        private Action _onClose;
        private Action<EmpireBuildingKind> _onV1Upgrade;
        private EmpireBuildingKind _kind;
        private Text _statusText;
        private GameObject _upgradeButtonRoot;

        public GameObject CanvasObjectForTests => _canvasObj;
        public EmpireBuildingKind KindForTests => _kind;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public bool UpgradeButtonActiveForTests =>
            _upgradeButtonRoot != null && _upgradeButtonRoot.activeSelf;

        public void Initialize(EmpireBuildingKind kind, Action onClose,
            Action<EmpireBuildingKind> onV1Upgrade = null)
        {
            _kind = kind;
            _onClose = onClose;
            _onV1Upgrade = onV1Upgrade;
            BuildUI();
        }

        public void OpenBuildingForTests(EmpireBuildingKind kind)
        {
            Initialize(kind, _onClose, _onV1Upgrade);
        }

        public EmpireBuildingDetailUpgradeResult PressUpgradeForTests() => OnUpgradePressed();

        private void BuildUI()
        {
            TeardownUI();
            // Do not CleanupStaleMetagameCanvases — EmpireCanvas must stay underneath.

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 40;

            GameObject dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(dim.GetComponent<RectTransform>());
            Image dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            GameObject panel = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvasObj.transform, false);
            panel.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.08f, 0.96f);
            SetNorm(panel.GetComponent<RectTransform>(), 0.06f, 0.08f, 0.94f, 0.92f);

            BuildHeader(panel.transform);
            BuildBody(panel.transform);
        }

        private void BuildHeader(Transform panel)
        {
            GameObject header = new GameObject("DetailHeader", typeof(RectTransform));
            header.transform.SetParent(panel, false);
            SetNorm(header.GetComponent<RectTransform>(), 0.02f, 0.88f, 0.98f, 0.98f);

            CreateBarButton(header.transform, "Btn_Return", "RETURN TO EMPIRE", new Vector2(16f, 0f), 240f, Close);
            CreateBarButton(header.transform, "Btn_Close", "X", new Vector2(-16f, 0f), 64f, Close, fromRight: true);

            Text title = UISharedFoundation.CreateText(header.transform, "Title", "BUILDING DETAIL",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.88f, 0.62f), true,
                new Vector2(520f, 40f));
            title.fontSize = 28;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.85f);
        }

        /// <summary>Resource paths (no extension) for kinds that have a real isometric render.
        /// Kinds not in this map render with text only - no placeholder image is shown.</summary>
        private static readonly System.Collections.Generic.Dictionary<EmpireBuildingKind, string> ArtResourcePaths =
            new System.Collections.Generic.Dictionary<EmpireBuildingKind, string>
            {
                { EmpireBuildingKind.Storage, "UI/EmpireBuildingDetailV1/Buildings/storage_isometric_v1" },
                { EmpireBuildingKind.TrainingGrounds, "UI/EmpireBuildingDetailV1/Buildings/training_grounds_isometric_v1" },
                { EmpireBuildingKind.Quarry, "UI/EmpireBuildingDetailV1/Buildings/quarry_isometric_v1" },
                { EmpireBuildingKind.Academy, "UI/EmpireBuildingDetailV1/Buildings/academy_isometric_v1" },
                { EmpireBuildingKind.TreeOfKnowledge, "UI/EmpireBuildingDetailV1/Buildings/tree_of_knowledge_isometric_v1" },
            };

        public bool HasArtForTests(EmpireBuildingKind kind) => ArtResourcePaths.ContainsKey(kind);

        /// <summary>Shared with EmpirePresenter's structure-strip tiles so the same 5 real isometric
        /// renders show up as thumbnails at the entry point, not just inside this detail popup -
        /// single source of truth for the path map, no duplicated dictionary.</summary>
        public static string ArtResourcePathFor(EmpireBuildingKind kind) =>
            ArtResourcePaths.TryGetValue(kind, out string path) ? path : null;

        private void BuildBody(Transform panel)
        {
            EmpireBuildingDefinition def = EmpireBuildingRoster.Get(_kind);
            PlayerProfile profile = SaveManager.SaveData;

            if (ArtResourcePaths.TryGetValue(_kind, out string artPath))
            {
                Sprite art = Resources.Load<Sprite>(artPath);
                if (art != null)
                {
                    GameObject artObj = new GameObject("BuildingArt", typeof(RectTransform), typeof(Image));
                    artObj.transform.SetParent(panel, false);
                    Image artImg = artObj.GetComponent<Image>();
                    artImg.sprite = art;
                    artImg.preserveAspect = true;
                    artImg.raycastTarget = false;
                    SetNorm(artImg.rectTransform, 0.64f, 0.20f, 0.97f, 0.49f);
                }
                else
                {
                    Debug.LogWarning($"[EmpireBuildingDetail] Failed to load building art sprite '{artPath}'.");
                }
            }

            Text name = UISharedFoundation.CreateText(panel, "BuildingName",
                $"BUILDING {def.DisplayName.ToUpperInvariant()}", UITextRole.Title, TextAnchor.MiddleLeft,
                new Color(0.95f, 0.9f, 0.79f), true, new Vector2(700f, 36f));
            SetNorm(name.rectTransform, 0.04f, 0.78f, 0.62f, 0.86f);

            Text level = UISharedFoundation.CreateText(panel, "BuildingLevel",
                EmpireBuildingDetailCopy.FormatLevelLine(_kind, profile), UITextRole.Body,
                TextAnchor.MiddleLeft, new Color(0.75f, 0.88f, 0.7f), true, new Vector2(400f, 32f));
            SetNorm(level.rectTransform, 0.04f, 0.75f, 0.62f, 0.80f);

            Text purpose = UISharedFoundation.CreateText(panel, "BuildingPurpose",
                def.Phase1Function, UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.85f, 0.82f, 0.72f), true, new Vector2(900f, 90f));
            // Font floor fix (register, 2026-08-27, scoped exception - EmpireBuildingDetailPresenter
            // is not owned by this seat, this one element only): 18 -> 22. Band is ~81.6px real
            // height (0.09 fraction of a ~907px panel), real headroom for this element's content.
            purpose.fontSize = 22;
            purpose.horizontalOverflow = HorizontalWrapMode.Wrap;
            purpose.verticalOverflow = VerticalWrapMode.Overflow;
            SetNorm(purpose.rectTransform, 0.04f, 0.66f, 0.62f, 0.75f);

            Text current = UISharedFoundation.CreateText(panel, "CurrentBenefit",
                EmpireBuildingDetailCopy.FormatCurrentBenefit(_kind, profile), UITextRole.Body,
                TextAnchor.UpperLeft, new Color(0.92f, 0.88f, 0.78f), true, new Vector2(900f, 70f));
            SetNorm(current.rectTransform, 0.04f, 0.57f, 0.62f, 0.66f);

            Text next = UISharedFoundation.CreateText(panel, "NextBenefit",
                EmpireBuildingDetailCopy.FormatNextBenefit(_kind), UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.7f, 0.85f, 0.68f), true, new Vector2(900f, 50f));
            SetNorm(next.rectTransform, 0.04f, 0.49f, 0.62f, 0.57f);

            Text cost = UISharedFoundation.CreateText(panel, "UpgradeCost",
                EmpireBuildingDetailCopy.FormatUpgradeCostLine(_kind, profile), UITextRole.Body,
                TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.55f), true, new Vector2(900f, 50f));
            SetNorm(cost.rectTransform, 0.04f, 0.40f, 0.62f, 0.49f);

            Text duration = UISharedFoundation.CreateText(panel, "Duration",
                EmpireBuildingDetailCopy.FormatDurationLine(profile, _kind), UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.8f, 0.78f, 0.65f), true, new Vector2(900f, 48f));
            SetNorm(duration.rectTransform, 0.04f, 0.31f, 0.62f, 0.40f);

            GameObject req = new GameObject("Btn_ViewRequirements", typeof(RectTransform), typeof(Image), typeof(Button));
            req.transform.SetParent(panel, false);
            Image reqImg = req.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(req.GetComponent<Button>(), reqImg,
                new Color(0.18f, 0.22f, 0.26f));
            req.GetComponent<Button>().onClick.AddListener(() =>
                SetStatus(EmpireCastleInterlock.StatusNote));
            SetNorm(req.GetComponent<RectTransform>(), 0.04f, 0.23f, 0.36f, 0.30f);
            UISharedFoundation.CreateText(req.transform, "Text", "VIEW REQUIREMENTS >", UITextRole.Caption,
                TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(280f, 28f));

            Text variant = UISharedFoundation.CreateText(panel, "VariantFraming",
                EmpireBuildingDetailCopy.FormatVariantFraming(_kind), UITextRole.Body, TextAnchor.UpperLeft,
                new Color(0.95f, 0.55f, 0.45f), true, new Vector2(520f, 120f));
            SetNorm(variant.rectTransform, 0.66f, 0.50f, 0.97f, 0.86f);

            _upgradeButtonRoot = new GameObject("Btn_Upgrade", typeof(RectTransform), typeof(Image), typeof(Button));
            _upgradeButtonRoot.transform.SetParent(panel, false);
            Image upImg = _upgradeButtonRoot.GetComponent<Image>();
            HomeV3UiLibrary.ApplyPrimaryActionButton(_upgradeButtonRoot.GetComponent<Button>(), upImg);
            _upgradeButtonRoot.GetComponent<Button>().onClick.AddListener(() => OnUpgradePressed());
            SetNorm(_upgradeButtonRoot.GetComponent<RectTransform>(), 0.28f, 0.06f, 0.72f, 0.18f);
            UISharedFoundation.CreateText(_upgradeButtonRoot.transform, "Text", "UPGRADE", UITextRole.Display,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));
            _upgradeButtonRoot.SetActive(def.HasUpgradeLadder);

            _statusText = UISharedFoundation.CreateText(panel, "DetailStatus", def.ServerStatus,
                UITextRole.Caption, TextAnchor.MiddleLeft, new Color(0.85f, 0.8f, 0.65f), true,
                new Vector2(1400f, 32f));
            SetNorm(_statusText.rectTransform, 0.04f, 0.01f, 0.96f, 0.06f);
        }

        private EmpireBuildingDetailUpgradeResult OnUpgradePressed()
        {
            EmpireBuildingDefinition def = EmpireBuildingRoster.Get(_kind);
            var result = new EmpireBuildingDetailUpgradeResult();
            if (!def.HasUpgradeLadder)
            {
                result.Status = EmpireBuildingDetailUpgradeStatus.NonUpgradeBuilding;
                result.Message = EmpireBuildingDetailCopy.FormatVariantFraming(_kind);
                SetStatus(result.Message);
                return result;
            }

            if (_kind == EmpireBuildingKind.Castle || _kind == EmpireBuildingKind.Barracks ||
                _kind == EmpireBuildingKind.Gate)
            {
                _onV1Upgrade?.Invoke(_kind);
                result.Status = EmpireBuildingDetailUpgradeStatus.StartedV1;
                result.Message = "v1 Gold upgrade uses the existing Empire construction queue.";
                SetStatus(result.Message);
                return result;
            }

            result.Status = EmpireBuildingDetailUpgradeStatus.V2PersistNotWired;
            result.Message =
                "v2 Materials upgrade is not persisted yet (frozen save — no per-building v2 levels / Materials field).";
            SetStatus(result.Message);
            return result;
        }

        private void Close()
        {
            TeardownUI();
            _onClose?.Invoke();
        }

        private static void CreateBarButton(Transform parent, string name, string label, Vector2 anchoredPos,
            float width, Action onClick, bool fromRight = false)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNavTileButton(btnObj.GetComponent<Button>(), img);
            img.color = new Color(0.28f, 0.2f, 0.16f);
            btnObj.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            if (fromRight)
            {
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
            }

            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(width, 48f);
            UISharedFoundation.CreateText(btnObj.transform, "Text", label, UITextRole.Caption, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(width - 16f, 36f));
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
