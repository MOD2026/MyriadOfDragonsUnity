using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Story
{
    public enum SpeakerPosition
    {
        Left,
        Right
    }

    [Serializable]
    public class StorySpeaker
    {
        public string speakerId;
        public string displayName;
        public string portraitPath;
        public SpeakerPosition position;

        public StorySpeaker(string id, string name, string portrait, SpeakerPosition pos = SpeakerPosition.Left)
        {
            this.speakerId = id;
            this.displayName = name;
            this.portraitPath = portrait;
            this.position = pos;
        }
    }

    [Serializable]
    public class DialogueLine
    {
        public StorySpeaker speaker;
        public string text;

        public DialogueLine(StorySpeaker speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    [Serializable]
    public class StorySequence
    {
        public string sequenceId;
        public string title;
        public List<DialogueLine> lines = new List<DialogueLine>();

        public StorySequence(string id, string title)
        {
            this.sequenceId = id;
            this.title = title;
        }
    }
}