using System;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>Which kind of empty this is. The four behave differently and the category is what
    /// decides whether an action is offered at all - locked design, 2026-08-27.</summary>
    public enum EmptyStateKind
    {
        /// <summary>The player can do something about it right now (Friends, Guild, filters).</summary>
        Actionable,

        /// <summary>Nothing is wrong and nothing is owed - it simply has not happened yet
        /// (Mail with no mail, Battle Pass before a season).</summary>
        Waiting,

        /// <summary>Gated behind a condition the player can understand (Guild access, Shop section).</summary>
        Locked,

        /// <summary>Finished, and that is a good outcome (all quests claimed).</summary>
        Completed,
    }

    /// <summary>
    /// The one reusable empty-state region.
    ///
    /// AN EMPTY STATE IS A PLAY-STATE, NOT AN ERROR. It answers three things: what is absent, why,
    /// and what happens next. It must never invent fake activity, fake rewards, or decorative
    /// filler unrelated to the feature - a fabricated claimable reward in an empty list is a lie
    /// told to a player who is already disappointed.
    ///
    /// TWO RULES HERE ARE REFUSALS, and they are the ones most likely to be "helpfully" broken by a
    /// later caller:
    ///
    ///   1. NOT EVERY EMPTY STATE GETS AN ACTION, and a disabled button is not a solution - it is
    ///      the same dead end wearing a control's clothes. Waiting and Completed states REFUSE an
    ///      action outright: an empty mailbox gets "No messages" and a last-sync line, not a greyed
    ///      "Refresh". Passing an action to those kinds throws rather than quietly ignoring it, so
    ///      the mistake surfaces at the call site instead of shipping as a dead button.
    ///
    ///   2. COLLAPSE BEATS FILLER. When there is no truthful preview to show, the ranked answer is
    ///      to remove the region and reflow - not to stretch art across it. Landscape width is a
    ///      reason for columns, not for more decoration.
    ///
    /// NEW-PLAYER CASE, which is why this is ONE component rather than per-screen copies: a fresh
    /// account hits Friends, Mail, Guild, Collection filters and Battle Pass empty simultaneously.
    /// That is the first impression of the entire game. Five separate hero illustrations read as
    /// "systems are broken"; one shared visual language reads as "the game is waiting for
    /// progression". Compact kinds (Waiting) deliberately render smaller than Actionable ones.
    /// </summary>
    public static class UIEmptyState
    {
        public const string IllustrationNoFriends = "UI/EmptyStatesV1/empty_state_no_friends_v1";
        public const string IllustrationNoMail = "UI/EmptyStatesV1/empty_state_no_mail_v1";
        public const string IllustrationNoGuild = "UI/EmptyStatesV1/empty_state_no_guild_v1";
        public const string IllustrationCollectionFilter = "UI/EmptyStatesV1/empty_state_collection_filter_v1";
        public const string IllustrationAllQuestsClaimed = "UI/EmptyStatesV1/empty_state_all_quests_claimed_v1";
        public const string IllustrationBattlePassNotStarted = "UI/EmptyStatesV1/empty_state_battle_pass_not_started_v1";

        /// <summary>Proportions from the locked table, as fractions of the region. Deliberately
        /// relative: the spec calls for a responsive region, and fixed pixels would break the
        /// moment this is used in a narrow column instead of a full-width panel.</summary>
        private const float TitleTop = 0.70f;
        private const float TitleBottom = 0.58f;
        private const float BodyTop = 0.56f;
        private const float BodyBottom = 0.41f;
        private const float ActionTop = 0.34f;
        private const float ActionBottom = 0.20f;

        /// <summary>True when this kind may offer an action at all.</summary>
        public static bool AllowsAction(EmptyStateKind kind)
        {
            return kind == EmptyStateKind.Actionable || kind == EmptyStateKind.Locked;
        }

        /// <summary>
        /// Builds the empty state into <paramref name="region"/>.
        ///
        /// <paramref name="actionLabel"/>/<paramref name="onAction"/> are optional and only
        /// permitted for Actionable and Locked kinds. A Waiting or Completed state that is handed
        /// an action throws: silently dropping it would leave the caller believing a button exists,
        /// and honouring it would ship exactly the dead control the lock forbids.
        ///
        /// <paramref name="statusLine"/> carries the truthful "when will this matter" line - last
        /// sync time, next reset, unlock condition. It is what stops a Waiting state being a bare
        /// apology.
        /// </summary>
        public static void Build(
            RectTransform region,
            EmptyStateKind kind,
            string title,
            string explanation,
            string statusLine = null,
            string actionLabel = null,
            Action onAction = null,
            string illustrationPath = null)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (string.IsNullOrEmpty(title)) throw new ArgumentException("An empty state must say what is absent.", nameof(title));

            bool wantsAction = !string.IsNullOrEmpty(actionLabel) || onAction != null;
            if (wantsAction && !AllowsAction(kind))
            {
                throw new ArgumentException(
                    "A " + kind + " empty state must not offer an action (locked design, 2026-08-27). " +
                    "An empty mailbox gets 'No messages' plus a last-sync line, not a Refresh button, " +
                    "and 'all caught up' gets the next reset time, not a disabled Claim. Pass the " +
                    "explanation or statusLine instead.",
                    nameof(actionLabel));
            }

            bool hasIllustration = false;
            if (!string.IsNullOrEmpty(illustrationPath))
            {
                Sprite illuSprite = Resources.Load<Sprite>(illustrationPath);
                if (illuSprite != null)
                {
                    hasIllustration = true;
                    GameObject illuGo = new GameObject("EmptyState_Illustration", typeof(RectTransform), typeof(Image));
                    illuGo.transform.SetParent(region, false);
                    Image illuImg = illuGo.GetComponent<Image>();
                    illuImg.sprite = illuSprite;
                    illuImg.type = Image.Type.Simple;
                    illuImg.preserveAspect = true;
                    illuImg.color = Color.white;
                    illuImg.raycastTarget = false;
                    SetNorm(illuGo.GetComponent<RectTransform>(), 0.20f, 0.44f, 0.80f, 0.98f);
                }
            }

            float titleTop = hasIllustration ? 0.43f : TitleTop;
            float titleBottom = hasIllustration ? 0.31f : TitleBottom;
            float bodyTop = hasIllustration ? 0.30f : BodyTop;
            float bodyBottom = hasIllustration ? 0.17f : BodyBottom;
            float statusBottom = hasIllustration ? 0.04f : 0.08f;
            float statusTop = hasIllustration ? 0.15f : 0.17f;
            float actionBottom = hasIllustration ? 0.04f : ActionBottom;
            float actionTop = hasIllustration ? 0.16f : ActionTop;

            Text titleText = UISharedFoundation.CreateText(
                region, "EmptyState_Title", title, UITextRole.Title, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 70f));
            UIDesignTokens.Apply(titleText, UIDesignTokens.TypeTier.T5Section);
            SetNorm(titleText.rectTransform, 0.08f, titleBottom, 0.92f, titleTop);

            if (!string.IsNullOrEmpty(explanation))
            {
                Text body = UISharedFoundation.CreateText(
                    region, "EmptyState_Explanation", explanation, UITextRole.Body, TextAnchor.UpperCenter,
                    UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 90f));
                UIDesignTokens.Apply(body, UIDesignTokens.TypeTier.T3Body);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Truncate;
                SetNorm(body.rectTransform, 0.12f, bodyBottom, 0.88f, bodyTop);
            }

            if (!string.IsNullOrEmpty(statusLine))
            {
                // The "when will this matter" line. Small on purpose - it is orienting information,
                // not the headline, and a Waiting state whose status shouts is just an error page.
                Text status = UISharedFoundation.CreateText(
                    region, "EmptyState_Status", statusLine, UITextRole.Caption, TextAnchor.MiddleCenter,
                    UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 40f));
                UIDesignTokens.Apply(status, UIDesignTokens.TypeTier.T2Utility);
                SetNorm(status.rectTransform, 0.10f, statusBottom, 0.90f, statusTop);
            }

            if (!wantsAction) return;

            Button action = UISharedFoundation.CreateButton(
                region, "EmptyState_Action", actionLabel, new Vector2(360f, 96f),
                UIFrozenTokens.ColorAccentEmerald, () => onAction?.Invoke(), null, true);
            UIDesignTokens.Apply(action.GetComponentInChildren<Text>(), UIDesignTokens.TypeTier.T4Control);

            // Max ONE Tier-1 frame per empty state and this is it - the action is the hero, so
            // nothing else in this region may claim that tier.
            Image actionImage = action.GetComponent<Image>();
            if (actionImage != null)
            {
                actionImage.color = new Color(
                    actionImage.color.r, actionImage.color.g, actionImage.color.b,
                    UIDesignTokens.FillAlpha(UIDesignTokens.FrameTier.Tier1Hero));
            }

            SetNorm(action.GetComponent<RectTransform>(), 0.36f, actionBottom, 0.64f, actionTop);
        }

        /// <summary>
        /// The collapse answer, and the FIRST choice in the locked ranking - ahead of preview art.
        ///
        /// Removing a region beats pretending empty space is content. Callers reach for this when
        /// they have no truthful preview to show; the alternative everyone reaches for instead is
        /// stretching decoration across the gap, which the lock explicitly rejects.
        /// </summary>
        public static void Collapse(RectTransform region)
        {
            if (region == null) return;
            region.gameObject.SetActive(false);

            var layout = region.GetComponent<LayoutElement>();
            if (layout == null) layout = region.gameObject.AddComponent<LayoutElement>();

            // ignoreLayout alone leaves a gap in a layout group - the sibling spacing still
            // reserves the slot. Zeroing the sizes is what actually makes the neighbours reflow.
            layout.ignoreLayout = true;
            layout.preferredHeight = 0f;
            layout.minHeight = 0f;
        }

        private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
