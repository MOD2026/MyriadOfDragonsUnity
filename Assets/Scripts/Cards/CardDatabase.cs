using System.Collections.Generic;
using UnityEngine;

namespace MyriadOfDragons.Cards
{
    /// <summary>
    /// Loads Assets/Resources/Data/card_data.json once and hands out Card objects and their
    /// art. Mirrors the role of CardDatabase.gd in the earlier Godot prototype, rebuilt here
    /// against the v2 integer-stat model instead of the old multiplier formulas.
    /// </summary>
    public class CardDatabase : MonoBehaviour
    {
        public static CardDatabase Instance { get; private set; }

        public IReadOnlyList<Card> AllCards => _allCards;

        private readonly List<Card> _allCards = new List<Card>();
        private readonly Dictionary<string, Card> _cardsById = new Dictionary<string, Card>();
        private readonly Dictionary<string, Sprite> _artCache = new Dictionary<string, Sprite>();

        private bool _initialized;

        private void Awake() => Initialize();

        /// <summary>
        /// Explicitly (re-)runs singleton setup and card loading. Awake() calls this
        /// automatically at runtime, but Unity never invokes Awake for a plain MonoBehaviour
        /// added via AddComponent while the Editor isn't in Play Mode - which is exactly the
        /// situation in EditMode tests - so callers that need a populated database outside of
        /// Play Mode must call this directly instead of relying on the lifecycle callback.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            if (Instance != null && Instance != this)
            {
                // Destroy() is illegal (and silently a no-op) outside Play Mode - EditMode tests
                // that create a second CardDatabase while Instance already points to one would
                // previously return here with this object never loaded (_initialized stays
                // false, AllCards stays empty) while the caller's own local reference to *this*
                // object is what they actually query - a real leak across test fixtures, not
                // just a duplicate-singleton no-op. DestroyImmediate is safe and immediate in
                // both modes for a component reachable from Initialize() (project non-negotiable).
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            LoadCards();
            _initialized = true;
        }

        private void LoadCards()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/card_data");
            if (json == null)
            {
                Debug.LogError("CardDatabase: could not find Resources/Data/card_data.json");
                return;
            }

            // JsonUtility can't parse a bare top-level array, so wrap it before parsing.
            string wrapped = "{\"cards\":" + json.text + "}";
            CardDataList parsed = JsonUtility.FromJson<CardDataList>(wrapped);
            if (parsed?.cards == null)
            {
                Debug.LogError("CardDatabase: card_data.json failed to parse");
                return;
            }

            foreach (CardData entry in parsed.cards)
            {
                Card card = Card.FromData(entry);
                _allCards.Add(card);
                _cardsById[card.Id] = card;
            }
        }

        public Card GetCard(string id)
        {
            return _cardsById.TryGetValue(id, out Card card) ? card : null;
        }

        /// <summary>
        /// Loads (and caches) a card's art as a Sprite. Art ships as raw imported textures in
        /// Resources/CardArt/ - set each one's Texture Type to "Sprite (2D and UI)" in the
        /// Unity importer the first time this project opens (not done automatically without
        /// the Editor installed to process it).
        /// </summary>
        public Sprite GetArt(Card card)
        {
            if (card == null) return null;

            if (_artCache.TryGetValue(card.Id, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(card.ResourcePath());
            if (sprite == null)
            {
                Debug.LogWarning($"CardDatabase: no sprite found at Resources/{card.ResourcePath()}");
            }

            _artCache[card.Id] = sprite;
            return sprite;
        }
    }
}
