using UnityEngine;
using UnityEngine.UI;
using System;

namespace MyriadOfDragons.Story
{
    public class StoryOverlayPresenter : MonoBehaviour
    {
        private GameObject overlayCanvasObj;
        private Image leftPortraitImg;
        private Image rightPortraitImg;
        private Text speakerNameText;
        private Text dialogueContentText;

        private StorySequence currentSequence;
        private int currentLineIndex = 0;
        private Action onCompletedAction;

        /// <summary>Last sequence id handed to <see cref="PlaySequence"/> (EditMode + Play Mode).
        /// Cleared via <see cref="ResetTestHooks"/>.</summary>
        public static string LastPlayedSequenceIdForTests { get; private set; }

        public static void ResetTestHooks() => LastPlayedSequenceIdForTests = null;

        public static void PlaySequence(StorySequence sequence, Action onCompleted)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Count == 0)
            {
                onCompleted?.Invoke();
                return;
            }

            LastPlayedSequenceIdForTests = sequence.sequenceId;

            // EditMode cannot drive uGUI click-through overlays (and Destroy is deferred). Content
            // still registers via LastPlayedSequenceIdForTests; the callback completes immediately
            // so reward/unlock owners are never blocked by a phantom canvas.
            if (!Application.isPlaying)
            {
                onCompleted?.Invoke();
                return;
            }

            GameObject host = new GameObject("StoryOverlayHost");
            var presenter = host.AddComponent<StoryOverlayPresenter>();
            presenter.Initialize(sequence, onCompleted);
        }

        private void Initialize(StorySequence sequence, Action onCompleted)
        {
            this.currentSequence = sequence;
            this.onCompletedAction = onCompleted;
            this.currentLineIndex = 0;

            BuildUI();
            ShowCurrentLine();
        }

        private void BuildUI()
        {
            overlayCanvasObj = new GameObject("StoryCanvas");
            overlayCanvasObj.transform.SetParent(transform, false);

            Canvas canvas = overlayCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = overlayCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = MyriadOfDragons.UI.UISharedFoundation.MatchWidthOrHeight;

            overlayCanvasObj.AddComponent<GraphicRaycaster>();

            // Dim Backdrop
            GameObject bgObj = new GameObject("StoryBackdrop");
            bgObj.transform.SetParent(overlayCanvasObj.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Left NPC Avatar Portrait
            GameObject leftPortObj = new GameObject("Portrait_Left");
            leftPortObj.transform.SetParent(overlayCanvasObj.transform, false);
            leftPortraitImg = leftPortObj.AddComponent<Image>();
            RectTransform leftRect = leftPortObj.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0.18f, 0.25f);
            leftRect.anchorMax = new Vector2(0.18f, 0.25f);
            leftRect.pivot = new Vector2(0.5f, 0f);
            leftRect.sizeDelta = new Vector2(320, 440);

            // Right NPC Avatar Portrait
            GameObject rightPortObj = new GameObject("Portrait_Right");
            rightPortObj.transform.SetParent(overlayCanvasObj.transform, false);
            rightPortraitImg = rightPortObj.AddComponent<Image>();
            RectTransform rightRect = rightPortObj.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.82f, 0.25f);
            rightRect.anchorMax = new Vector2(0.82f, 0.25f);
            rightRect.pivot = new Vector2(0.5f, 0f);
            rightRect.sizeDelta = new Vector2(320, 440);

            // Dialogue Container Box
            GameObject panelObj = new GameObject("DialoguePanel");
            panelObj.transform.SetParent(overlayCanvasObj.transform, false);
            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.14f, 0.95f);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0, 40);
            panelRect.sizeDelta = new Vector2(1400, 260);

            Button advanceBtn = panelObj.AddComponent<Button>();
            advanceBtn.onClick.AddListener(AdvanceDialogue);

            // Speaker Name Header
            GameObject nameObj = new GameObject("SpeakerNameText");
            nameObj.transform.SetParent(panelObj.transform, false);
            speakerNameText = nameObj.AddComponent<Text>();
            speakerNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            speakerNameText.fontSize = 28;
            speakerNameText.fontStyle = FontStyle.Bold;
            speakerNameText.color = new Color(1f, 0.85f, 0.3f);

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(0, 1);
            nameRect.pivot = new Vector2(0, 1);
            nameRect.anchoredPosition = new Vector2(30, -20);
            nameRect.sizeDelta = new Vector2(600, 40);

            // Dialogue Body Text
            GameObject contentObj = new GameObject("DialogueContentText");
            contentObj.transform.SetParent(panelObj.transform, false);
            dialogueContentText = contentObj.AddComponent<Text>();
            dialogueContentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dialogueContentText.fontSize = 22;
            dialogueContentText.color = Color.white;
            dialogueContentText.supportRichText = true;

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.anchoredPosition = new Vector2(30, -70);
            contentRect.sizeDelta = new Vector2(-60, 160);

            // Skip Button
            GameObject skipObj = new GameObject("Btn_Skip");
            skipObj.transform.SetParent(overlayCanvasObj.transform, false);
            Image skipImg = skipObj.AddComponent<Image>();
            skipImg.color = new Color(0.25f, 0.25f, 0.3f, 0.85f);

            Button skipBtn = skipObj.AddComponent<Button>();
            skipBtn.onClick.AddListener(EndSequence);

            RectTransform skipRect = skipObj.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1, 1);
            skipRect.anchorMax = new Vector2(1, 1);
            skipRect.pivot = new Vector2(1, 1);
            skipRect.anchoredPosition = new Vector2(-40, -40);
            skipRect.sizeDelta = new Vector2(120, 45);

            GameObject skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);
            Text skipText = skipTextObj.AddComponent<Text>();
            skipText.text = "SKIP >";
            skipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skipText.fontSize = 20;
            skipText.alignment = TextAnchor.MiddleCenter;
            skipText.color = Color.white;

            RectTransform skipTextRect = skipTextObj.GetComponent<RectTransform>();
            skipTextRect.anchorMin = Vector2.zero;
            skipTextRect.anchorMax = Vector2.one;
            skipTextRect.sizeDelta = Vector2.zero;
        }

        private void ShowCurrentLine()
        {
            if (currentLineIndex >= currentSequence.lines.Count)
            {
                EndSequence();
                return;
            }

            var line = currentSequence.lines[currentLineIndex];
            speakerNameText.text = line.speaker != null ? line.speaker.displayName : "???";
            dialogueContentText.text = line.text;

            UpdatePortrait(leftPortraitImg, line.speaker, SpeakerPosition.Left);
            UpdatePortrait(rightPortraitImg, line.speaker, SpeakerPosition.Right);
        }

        private void UpdatePortrait(Image portraitImg, StorySpeaker speaker, SpeakerPosition targetPosition)
        {
            if (speaker == null || speaker.position != targetPosition)
            {
                portraitImg.gameObject.SetActive(false);
                return;
            }

            portraitImg.gameObject.SetActive(true);
            Sprite portraitSprite = string.IsNullOrEmpty(speaker.portraitPath) ? null : Resources.Load<Sprite>(speaker.portraitPath);

            if (portraitSprite != null)
            {
                portraitImg.sprite = portraitSprite;
                portraitImg.color = Color.white;
            }
            else
            {
                // Fallback NPC placeholder avatar if sprite resource is not imported yet
                portraitImg.sprite = null;
                portraitImg.color = targetPosition == SpeakerPosition.Left
                    ? new Color(0.2f, 0.4f, 0.8f, 0.85f)
                    : new Color(0.8f, 0.3f, 0.2f, 0.85f);
            }
        }

        private void AdvanceDialogue()
        {
            currentLineIndex++;
            ShowCurrentLine();
        }

        private void EndSequence()
        {
            Action callback = onCompletedAction;
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
            callback?.Invoke();
        }
    }
}