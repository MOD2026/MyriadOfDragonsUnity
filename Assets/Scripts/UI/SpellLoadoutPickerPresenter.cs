using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Manual 4-spell loadout picker. One slot per <see cref="SpellEffect"/>; pool from
    /// <see cref="SpellLoadoutSelection.ResolveSelectablePool"/>. Writes equippedSpellIds.
    /// </summary>
    public class SpellLoadoutPickerPresenter : MonoBehaviour
    {
        public const string CanvasName = "SpellLoadoutPickerCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private readonly Dictionary<SpellEffect, string> _slotSelection =
            new Dictionary<SpellEffect, string>();
        private readonly Dictionary<SpellEffect, Text> _slotLabels =
            new Dictionary<SpellEffect, Text>();
        private List<AvatarSpell> _pool = new List<AvatarSpell>();

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public IReadOnlyDictionary<SpellEffect, string> SlotSelectionForTests => _slotSelection;

        public void Initialize(Action onBack)
        {
            _onBack = onBack;
            BuildUI();
        }

        public SpellLoadoutApplyResult ConfirmForTests() => ConfirmSelection();

        public void SelectSpellForTests(string spellId) => SelectSpell(spellId);

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            PlayerProfile profile = SaveManager.SaveData;
            if (profile != null)
                SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            _pool = SpellLoadoutSelection.ResolveSelectablePool(profile);
            PrefillFromEquipped(profile);

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 14;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            Image bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.07f, 0.08f, 0.11f, 1f);
            bgImg.raycastTarget = false;

            BuildHeader();
            BuildSlotSummary();
            BuildEffectColumns();
            BuildConfirmBar();
            RefreshStatus();
        }

        private void PrefillFromEquipped(PlayerProfile profile)
        {
            _slotSelection.Clear();
            if (profile?.equippedSpellIds == null) return;

            Dictionary<string, AvatarSpell> poolById = _pool
                .Where(s => !string.IsNullOrEmpty(s.Id))
                .ToDictionary(s => s.Id);

            foreach (string id in profile.equippedSpellIds)
            {
                if (string.IsNullOrEmpty(id) || !poolById.TryGetValue(id, out AvatarSpell spell))
                    continue;
                _slotSelection[spell.Effect] = spell.Id;
            }
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("LoadoutHeader", typeof(RectTransform));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "SPELL LOADOUT",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(720f, 48f));
            title.fontSize = 28;
            SetNorm(title.rectTransform, 0.22f, 0.15f, 0.78f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", string.Empty,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(480f, 40f));
            SetNorm(_statusText.rectTransform, 0.70f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildSlotSummary()
        {
            GameObject strip = new GameObject("SlotSummary", typeof(RectTransform));
            strip.transform.SetParent(_canvasObj.transform, false);
            SetNorm(strip.GetComponent<RectTransform>(), 0.04f, 0.78f, 0.96f, 0.88f);

            SpellEffect[] order = SpellLoadoutAutoEquip.EquipSlotOrder;
            float w = 1f / order.Length;
            for (int i = 0; i < order.Length; i++)
            {
                SpellEffect effect = order[i];
                GameObject cell = new GameObject($"Slot_{effect}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(strip.transform, false);
                cell.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.2f, 0.9f);
                cell.GetComponent<Image>().raycastTarget = false;
                SetNorm(cell.GetComponent<RectTransform>(), i * w + 0.01f, 0.1f, (i + 1) * w - 0.01f, 0.9f);

                Text header = UISharedFoundation.CreateText(cell.transform, "EffectLabel",
                    SpellLoadoutSelection.EffectSlotLabel(effect).ToUpperInvariant(),
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.75f, 0.7f, 0.55f), true,
                    new Vector2(200f, 24f));
                header.fontSize = 14;
                SetNorm(header.rectTransform, 0.05f, 0.55f, 0.95f, 0.95f);

                Text pick = UISharedFoundation.CreateText(cell.transform, "PickLabel", "(empty)",
                    UITextRole.Body, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true,
                    new Vector2(220f, 28f));
                pick.fontSize = 18;
                SetNorm(pick.rectTransform, 0.05f, 0.05f, 0.95f, 0.55f);
                _slotLabels[effect] = pick;
            }

            RefreshSlotLabels();
        }

        private void BuildEffectColumns()
        {
            GameObject columns = new GameObject("EffectColumns", typeof(RectTransform));
            columns.transform.SetParent(_canvasObj.transform, false);
            SetNorm(columns.GetComponent<RectTransform>(), 0.03f, 0.16f, 0.97f, 0.76f);

            SpellEffect[] order = SpellLoadoutAutoEquip.EquipSlotOrder;
            float w = 1f / order.Length;
            for (int i = 0; i < order.Length; i++)
            {
                SpellEffect effect = order[i];
                GameObject col = new GameObject($"Column_{effect}", typeof(RectTransform));
                col.transform.SetParent(columns.transform, false);
                SetNorm(col.GetComponent<RectTransform>(), i * w + 0.008f, 0f, (i + 1) * w - 0.008f, 1f);

                List<AvatarSpell> spells = SpellLoadoutSelection.SpellsForEffect(_pool, effect);
                if (spells.Count == 0)
                {
                    Text empty = UISharedFoundation.CreateText(col.transform, "EmptyPool",
                        "None unlocked", UITextRole.Caption, TextAnchor.MiddleCenter,
                        new Color(0.7f, 0.55f, 0.45f), true, new Vector2(200f, 40f));
                    SetNorm(empty.rectTransform, 0.05f, 0.4f, 0.95f, 0.6f);
                    continue;
                }

                float rowH = 1f / Mathf.Max(spells.Count, 1);
                for (int s = 0; s < spells.Count; s++)
                {
                    AvatarSpell spell = spells[s];
                    string spellId = spell.Id;
                    GameObject btn = new GameObject($"Spell_{spell.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                    btn.transform.SetParent(col.transform, false);
                    Image img = btn.GetComponent<Image>();
                    bool selected = _slotSelection.TryGetValue(effect, out string cur) && cur == spellId;
                    HomeV3UiLibrary.ApplyNeutralActionButton(btn.GetComponent<Button>(), img,
                        selected ? new Color(0.28f, 0.42f, 0.28f) : new Color(0.16f, 0.2f, 0.26f, 0.92f));
                    btn.GetComponent<Button>().onClick.AddListener(() => SelectSpell(spellId));
                    SetNorm(btn.GetComponent<RectTransform>(), 0.02f, 1f - (s + 1) * rowH + 0.02f,
                        0.98f, 1f - s * rowH - 0.02f);

                    string label = $"{spell.Name}\nE{spell.EnergyCost} · Mag {spell.Magnitude}";
                    Text t = UISharedFoundation.CreateText(btn.transform, "Label", label,
                        UITextRole.Caption, TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 56f));
                    t.fontSize = 15;
                    SetNorm(t.rectTransform, 0.04f, 0.08f, 0.96f, 0.92f);
                }
            }
        }

        private void BuildConfirmBar()
        {
            GameObject bar = new GameObject("ConfirmBar", typeof(RectTransform));
            bar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(bar.GetComponent<RectTransform>(), 0.25f, 0.03f, 0.75f, 0.13f);

            GameObject confirm = new GameObject("Btn_ConfirmLoadout", typeof(RectTransform), typeof(Image), typeof(Button));
            confirm.transform.SetParent(bar.transform, false);
            Image img = confirm.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(confirm.GetComponent<Button>(), img, new Color(0.2f, 0.4f, 0.3f));
            confirm.GetComponent<Button>().onClick.AddListener(() => ConfirmSelection());
            SetNorm(confirm.GetComponent<RectTransform>(), 0.1f, 0.15f, 0.9f, 0.85f);
            UISharedFoundation.CreateText(confirm.transform, "Text", "CONFIRM LOADOUT", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(360f, 40f));
        }

        private void SelectSpell(string spellId)
        {
            AvatarSpell spell = _pool.FirstOrDefault(s => s.Id == spellId);
            if (spell == null)
            {
                SetStatus($"Not selectable: {spellId}");
                return;
            }

            _slotSelection[spell.Effect] = spell.Id;
            RebuildColumnsKeepingSelection();
            RefreshSlotLabels();
            RefreshStatus();
        }

        private void RebuildColumnsKeepingSelection()
        {
            Transform columns = _canvasObj != null ? _canvasObj.transform.Find("EffectColumns") : null;
            if (columns != null)
            {
                if (Application.isPlaying) Destroy(columns.gameObject);
                else DestroyImmediate(columns.gameObject);
            }
            BuildEffectColumns();
        }

        private SpellLoadoutApplyResult ConfirmSelection()
        {
            PlayerProfile profile = SaveManager.SaveData;
            var orderedIds = new List<string>(SpellLoadoutSelection.RequiredSlotCount);
            foreach (SpellEffect effect in SpellLoadoutAutoEquip.EquipSlotOrder)
            {
                if (!_slotSelection.TryGetValue(effect, out string id) || string.IsNullOrEmpty(id))
                {
                    var incomplete = new SpellLoadoutApplyResult
                    {
                        Status = SpellLoadoutApplyStatus.MissingEffect,
                        Message = $"Pick a {SpellLoadoutSelection.EffectSlotLabel(effect)} spell.",
                    };
                    SetStatus(incomplete.Message);
                    return incomplete;
                }
                orderedIds.Add(id);
            }

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile, orderedIds);
            SetStatus(result.Message);
            if (result.Status == SpellLoadoutApplyStatus.Applied)
                SaveManager.Save();
            return result;
        }

        private void RefreshSlotLabels()
        {
            Dictionary<string, AvatarSpell> byId = _pool
                .Where(s => !string.IsNullOrEmpty(s.Id))
                .ToDictionary(s => s.Id);

            foreach (SpellEffect effect in SpellLoadoutAutoEquip.EquipSlotOrder)
            {
                if (!_slotLabels.TryGetValue(effect, out Text label) || label == null) continue;
                if (_slotSelection.TryGetValue(effect, out string id) && byId.TryGetValue(id, out AvatarSpell spell))
                    label.text = spell.Name;
                else
                    label.text = "(empty)";
            }
        }

        private void RefreshStatus()
        {
            int filled = _slotSelection.Count;
            if (!SpellLoadoutSelection.PoolCoversAllSlots(_pool))
            {
                SetStatus("Unlocked pool is missing an effect type — progress further to fill all four slots.");
                return;
            }

            if (filled < SpellLoadoutSelection.RequiredSlotCount)
                SetStatus($"Select one spell per type ({filled}/{SpellLoadoutSelection.RequiredSlotCount}).");
            else
                SetStatus("Ready — confirm to save loadout.");
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
            _slotLabels.Clear();
            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
