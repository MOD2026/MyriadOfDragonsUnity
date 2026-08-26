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
    /// Manual spell loadout picker. Slot count is tier-unlocked by Avatar level (4/5/6 —
    /// <see cref="SpellLoadoutAutoEquip.RequiredSlotCount"/>). At most one spell per
    /// <see cref="SpellEffect"/>; pool from <see cref="SpellLoadoutSelection.ResolveSelectablePool"/>.
    /// Writes <c>equippedSpellIds</c>.
    /// </summary>
    public class SpellLoadoutPickerPresenter : MonoBehaviour
    {
        public const string CanvasName = "SpellLoadoutPickerCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private readonly Dictionary<SpellEffect, string> _slotSelection =
            new Dictionary<SpellEffect, string>();
        private readonly List<Text> _slotLabels = new List<Text>();
        private List<AvatarSpell> _pool = new List<AvatarSpell>();
        private int _requiredSlots = SpellLoadoutAutoEquip.StartingSlotCount;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public IReadOnlyDictionary<SpellEffect, string> SlotSelectionForTests => _slotSelection;
        public int RequiredSlotsForTests => _requiredSlots;

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

            int avatarLevel = profile != null && profile.avatarLevel > 0 ? profile.avatarLevel : 1;
            _requiredSlots = SpellLoadoutSelection.RequiredSlotCount(avatarLevel);
            _pool = SpellLoadoutSelection.ResolveSelectablePool(profile);
            PrefillFromEquipped(profile);

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 14;

            Color shellFallback = new Color(0.07f, 0.08f, 0.11f, 1f);
            GameObject backing = new GameObject("BackgroundBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(backing.GetComponent<RectTransform>());
            Image backingImg = backing.GetComponent<Image>();
            backingImg.color = new Color(shellFallback.r, shellFallback.g, shellFallback.b, 1f);
            backingImg.raycastTarget = false;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            SpellLoadoutUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader(avatarLevel);
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
                if (_slotSelection.Count >= _requiredSlots && !_slotSelection.ContainsKey(spell.Effect))
                    break;
                _slotSelection[spell.Effect] = spell.Id;
            }
        }

        private void BuildHeader(int avatarLevel)
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title",
                $"SPELL LOADOUT ({_requiredSlots} SLOTS · L{avatarLevel})",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(820f, 48f));
            title.fontSize = 26;
            SetNorm(title.rectTransform, 0.18f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", string.Empty,
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(480f, 40f));
            SetNorm(_statusText.rectTransform, 0.70f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildSlotSummary()
        {
            _slotLabels.Clear();
            GameObject strip = new GameObject("SlotSummary", typeof(RectTransform));
            strip.transform.SetParent(_canvasObj.transform, false);
            SetNorm(strip.GetComponent<RectTransform>(), 0.04f, 0.78f, 0.96f, 0.88f);

            float w = 1f / _requiredSlots;
            for (int i = 0; i < _requiredSlots; i++)
            {
                GameObject cell = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(strip.transform, false);
                // Token color only, not ApplyFramedPanel: this strip's cells sit right at/under
                // the real ListRow art's minimum-height floor (128px) per the manifest - real
                // art would risk deforming rather than reading as an improvement.
                cell.GetComponent<Image>().color = UIFrozenTokens.ColorPanel;
                cell.GetComponent<Image>().raycastTarget = false;
                SetNorm(cell.GetComponent<RectTransform>(), i * w + 0.01f, 0.1f, (i + 1) * w - 0.01f, 0.9f);

                // School icon — filled from the selected spell's SpellSchool on refresh.
                SpellLoadoutUiLibrary.ApplySchoolIcon(cell.transform, "SchoolIcon", SpellSchool.Andras,
                    0.08f, 0.35f, 0.40f, 0.92f);
                Image schoolImg = cell.transform.Find("SchoolIcon")?.GetComponent<Image>();
                if (schoolImg != null) schoolImg.enabled = false;

                Text header = UISharedFoundation.CreateText(cell.transform, "EffectLabel", $"SLOT {i + 1}",
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.75f, 0.7f, 0.55f), true,
                    new Vector2(200f, 24f));
                header.fontSize = 13;
                header.raycastTarget = false;
                SetNorm(header.rectTransform, 0.42f, 0.55f, 0.95f, 0.95f);

                Text pick = UISharedFoundation.CreateText(cell.transform, "PickLabel", "(empty)",
                    UITextRole.Body, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true,
                    new Vector2(220f, 28f));
                pick.fontSize = 16;
                pick.raycastTarget = false;
                SetNorm(pick.rectTransform, 0.42f, 0.05f, 0.95f, 0.55f);
                _slotLabels.Add(pick);
            }

            RefreshSlotLabels();
        }

        private SpellEffect[] VisibleEffects() =>
            SpellLoadoutAutoEquip.FullEffectPriorityOrder
                .Where(e => SpellLoadoutSelection.SpellsForEffect(_pool, e).Count > 0)
                .ToArray();

        private void BuildEffectColumns()
        {
            GameObject columns = new GameObject("EffectColumns", typeof(RectTransform));
            columns.transform.SetParent(_canvasObj.transform, false);
            SetNorm(columns.GetComponent<RectTransform>(), 0.03f, 0.16f, 0.97f, 0.76f);

            SpellEffect[] order = VisibleEffects();
            if (order.Length == 0)
            {
                Text empty = UISharedFoundation.CreateText(columns.transform, "EmptyPool",
                    "No spells unlocked", UITextRole.Caption, TextAnchor.MiddleCenter,
                    new Color(0.7f, 0.55f, 0.45f), true, new Vector2(400f, 40f));
                SetNorm(empty.rectTransform, 0.2f, 0.4f, 0.8f, 0.6f);
                return;
            }

            // One row when ≤7 effect types; two rows when the unlocked pool spans more.
            int colsPerRow = order.Length <= 7 ? order.Length : (order.Length + 1) / 2;
            int rows = order.Length <= 7 ? 1 : 2;

            for (int i = 0; i < order.Length; i++)
            {
                SpellEffect effect = order[i];
                int row = i / colsPerRow;
                int col = i % colsPerRow;
                int colsThisRow = row == 0
                    ? Math.Min(colsPerRow, order.Length)
                    : order.Length - colsPerRow;
                float cellW = 1f / colsThisRow;
                float rowBottom = rows == 1 ? 0f : (row == 0 ? 0.52f : 0f);
                float rowTop = rows == 1 ? 1f : (row == 0 ? 1f : 0.48f);

                GameObject colGo = new GameObject($"Column_{effect}", typeof(RectTransform));
                colGo.transform.SetParent(columns.transform, false);
                SetNorm(colGo.GetComponent<RectTransform>(),
                    col * cellW + 0.006f, rowBottom,
                    (col + 1) * cellW - 0.006f, rowTop);

                Text effectHeader = UISharedFoundation.CreateText(colGo.transform, "EffectHeader",
                    SpellLoadoutSelection.EffectSlotLabel(effect).ToUpperInvariant(),
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.8f, 0.75f, 0.55f), true,
                    new Vector2(180f, 22f));
                effectHeader.fontSize = 12;
                SetNorm(effectHeader.rectTransform, 0.02f, 0.88f, 0.98f, 0.98f);

                List<AvatarSpell> spells = SpellLoadoutSelection.SpellsForEffect(_pool, effect);
                float rowH = 0.86f / Mathf.Max(spells.Count, 1);
                for (int s = 0; s < spells.Count; s++)
                {
                    AvatarSpell spell = spells[s];
                    string spellId = spell.Id;
                    GameObject btn = new GameObject($"Spell_{spell.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
                    btn.transform.SetParent(colGo.transform, false);
                    Image img = btn.GetComponent<Image>();
                    bool selected = _slotSelection.TryGetValue(effect, out string cur) && cur == spellId;
                    HomeV3UiLibrary.ApplyNeutralActionButton(btn.GetComponent<Button>(), img,
                        selected ? new Color(0.28f, 0.42f, 0.28f) : new Color(0.16f, 0.2f, 0.26f, 0.72f));
                    btn.GetComponent<Button>().onClick.AddListener(() => SelectSpell(spellId));
                    SetNorm(btn.GetComponent<RectTransform>(), 0.02f, 0.88f - (s + 1) * rowH + 0.01f,
                        0.98f, 0.88f - s * rowH - 0.01f);

                    SpellLoadoutUiLibrary.ApplySchoolIcon(btn.transform, "SchoolIcon", spell.School,
                        0.04f, 0.18f, 0.28f, 0.82f);

                    string label = $"{spell.Name}\nE{spell.EnergyCost} · Mag {spell.Magnitude}";
                    Text t = UISharedFoundation.CreateText(btn.transform, "Label", label,
                        UITextRole.Caption, TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 56f));
                    t.fontSize = 13;
                    t.raycastTarget = false;
                    SetNorm(t.rectTransform, 0.30f, 0.08f, 0.96f, 0.92f);
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

            if (_slotSelection.TryGetValue(spell.Effect, out string current) && current == spellId)
            {
                _slotSelection.Remove(spell.Effect);
            }
            else if (_slotSelection.ContainsKey(spell.Effect))
            {
                _slotSelection[spell.Effect] = spell.Id;
            }
            else if (_slotSelection.Count >= _requiredSlots)
            {
                SetStatus($"Loadout full ({_requiredSlots}/{_requiredSlots}). Deselect a spell or replace the same effect type.");
                return;
            }
            else
            {
                _slotSelection[spell.Effect] = spell.Id;
            }

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
            int avatarLevel = profile != null && profile.avatarLevel > 0 ? profile.avatarLevel : 1;
            int required = SpellLoadoutSelection.RequiredSlotCount(avatarLevel);

            if (_slotSelection.Count != required)
            {
                var incomplete = new SpellLoadoutApplyResult
                {
                    Status = SpellLoadoutApplyStatus.WrongCount,
                    Message = $"Pick exactly {required} spells (one per effect type). Currently {_slotSelection.Count}/{required}.",
                };
                SetStatus(incomplete.Message);
                return incomplete;
            }

            List<string> orderedIds = SpellLoadoutAutoEquip.FullEffectPriorityOrder
                .Where(effect => _slotSelection.ContainsKey(effect))
                .Select(effect => _slotSelection[effect])
                .ToList();

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

            List<(SpellEffect effect, string id)> ordered = SpellLoadoutAutoEquip.FullEffectPriorityOrder
                .Where(e => _slotSelection.ContainsKey(e))
                .Select(e => (e, _slotSelection[e]))
                .ToList();

            for (int i = 0; i < _slotLabels.Count; i++)
            {
                Text label = _slotLabels[i];
                if (label == null) continue;
                Transform slot = label.transform.parent;
                Image schoolImg = slot != null ? slot.Find("SchoolIcon")?.GetComponent<Image>() : null;

                if (i < ordered.Count && byId.TryGetValue(ordered[i].id, out AvatarSpell spell))
                {
                    label.text = $"{SpellLoadoutSelection.EffectSlotLabel(ordered[i].effect)}\n{spell.Name}";
                    if (schoolImg != null)
                    {
                        schoolImg.enabled = true;
                        schoolImg.sprite = SpellLoadoutUiLibrary.LoadSchool(spell.School);
                    }
                }
                else
                {
                    label.text = "(empty)";
                    if (schoolImg != null) schoolImg.enabled = false;
                }
            }
        }

        private void RefreshStatus()
        {
            int filled = _slotSelection.Count;
            if (!SpellLoadoutSelection.PoolHasEnoughDistinctEffects(_pool, _requiredSlots))
            {
                SetStatus($"Unlocked pool cannot yet fill {_requiredSlots} distinct effect types — progress further.");
                return;
            }

            if (filled < _requiredSlots)
                SetStatus($"Select {_requiredSlots} spells, one per effect type ({filled}/{_requiredSlots}).");
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
