using System.Collections.Generic;
using UnityEngine;

namespace MyriadOfDragons.Story
{
    /// <summary>
    /// One line of story - a speaker and what they say. `portrait` names a sprite under
    /// Resources/UI/Portraits (e.g. "Paladin", "Orc_King"), or is left empty for narration.
    /// </summary>
    [System.Serializable]
    public class StoryBeat
    {
        public string speaker;
        public string portrait;
        public string text;

        /// <summary>
        /// Full-screen landscape artwork behind this beat, named relative to
        /// Resources/Story/Backgrounds (no extension). Empty keeps whatever the previous beat
        /// showed, so a run of dialogue in one location only names its background once.
        /// </summary>
        public string background;
    }

    /// <summary>A named run of beats, e.g. the game's opening.</summary>
    [System.Serializable]
    public class StoryChapter
    {
        public string id;
        public string title;
        public StoryBeat[] beats;
    }

    /// <summary>
    /// One tutorial instruction. `highlight` names the UI element it points at, matching the
    /// GameObject names GameBootstrap creates ("HandPanel", "PlayerPanel", "SpellBar",
    /// "PrimaryActionPanel"), so a future tutorial overlay can find its target without a second
    /// naming scheme to keep in sync.
    /// </summary>
    [System.Serializable]
    public class TutorialStep
    {
        public string id;
        public string instruction;
        public string highlight;
    }

    [System.Serializable]
    public class StoryChapterList
    {
        public StoryChapter[] chapters;
    }

    [System.Serializable]
    public class TutorialStepList
    {
        public TutorialStep[] steps;
    }

    /// <summary>
    /// Loads story and tutorial content from Resources/Data/Story.
    ///
    /// Scaffolding: this reads and exposes the content, and nothing more. There is no UI to show
    /// a chapter, no entry point from GameBootstrap, and - crucially - no save system anywhere in
    /// this project, so "which chapters has this player already seen" cannot currently be
    /// answered across sessions. See README_FOR_CONTRIBUTORS.md in this folder.
    ///
    /// Deliberately a plain class, not a MonoBehaviour: nothing here needs a frame update or a
    /// scene presence, and a plain class can be exercised directly by the EditMode test suite.
    /// </summary>
    public class StoryDatabase
    {
        public const string ChaptersResourcePath = "Data/Story/story_chapters";
        public const string TutorialResourcePath = "Data/Story/tutorial_steps";

        public List<StoryChapter> Chapters { get; private set; } = new List<StoryChapter>();
        public List<TutorialStep> TutorialSteps { get; private set; } = new List<TutorialStep>();

        /// <summary>
        /// Reads both files. Missing or malformed content leaves the corresponding list empty
        /// rather than throwing - story is optional content, and a missing chapter file should
        /// never stop the game from booting into a playable battle.
        /// </summary>
        public void Load()
        {
            Chapters = LoadList<StoryChapterList, StoryChapter>(
                ChaptersResourcePath, wrapper => wrapper.chapters);
            TutorialSteps = LoadList<TutorialStepList, TutorialStep>(
                TutorialResourcePath, wrapper => wrapper.steps);
        }

        public StoryChapter GetChapter(string id) => Chapters.Find(c => c.id == id);

        private static List<TItem> LoadList<TWrapper, TItem>(
            string resourcePath, System.Func<TWrapper, TItem[]> selectItems)
        {
            var results = new List<TItem>();

            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) return results;

            // JsonUtility can't parse a top-level JSON array, hence the wrapper object - the
            // same pattern card_data.json already uses successfully.
            TWrapper wrapper = JsonUtility.FromJson<TWrapper>(asset.text);
            TItem[] items = wrapper == null ? null : selectItems(wrapper);
            if (items != null) results.AddRange(items);

            return results;
        }
    }
}
