using System.Collections.Generic;
using UnityEngine;

namespace MyriadOfDragons.Combat
{
    /// <summary>
    /// Cue id / visual tier -> Resources path. This is the whole point of the binding layer: when
    /// real audio and particle assets finally exist, someone drops files at these paths and the
    /// game picks them up with NO code change. Until then every lookup simply resolves to null and
    /// the sinks below no-op.
    ///
    /// Plain static data + lookups, no MonoBehaviour, so the mapping itself is fully EditMode
    /// testable - the tests assert that every cue CombatPresentation can emit has a binding, which
    /// is the failure mode that would otherwise only show up as silence in a build.
    /// </summary>
    public static class CombatPresentationAssetMap
    {
        public const string AudioRoot = "Audio/Combat/";
        public const string ParticleRoot = "VFX/Combat/";

        /// <summary>Cue id -> file name under <see cref="AudioRoot"/> (no extension).</summary>
        private static readonly Dictionary<string, string> AudioPaths = new Dictionary<string, string>
        {
            { CombatPresentation.CueCommit, "combat_commit" },
            { CombatPresentation.CueCast, "combat_cast" },
            { CombatPresentation.CueImpact, "combat_impact" },
            { CombatPresentation.CueSoftResolve, "combat_resolve_soft" },
            { CombatPresentation.CueAvatarStrikeReleaseImpact, "avatarstrike_release_impact" },
            // NOTE the "release" segment. This binding said "avatarstrike_stinger" while its sibling
            // said "avatarstrike_release_impact" and every delivered file is avatarstrike_release_*.
            // The inconsistency was written when no audio existed to check against, and stayed
            // invisible because the sink is null-tolerant BY DESIGN - a wrong binding produces
            // silence, which is indistinguishable from "no audio shipped yet". Only a load test
            // surfaces it.
            { CombatPresentation.CueAvatarStrikeStinger, "avatarstrike_release_stinger" },
        };

        /// <summary>Every cue id this map knows about. Tests compare it against the cue constants
        /// CombatPresentation actually emits, so a new cue cannot be added without a binding.</summary>
        public static IEnumerable<string> KnownAudioCueIds => AudioPaths.Keys;

        public static bool HasAudioBinding(string cueId) =>
            !string.IsNullOrEmpty(cueId) && AudioPaths.ContainsKey(cueId);

        /// <summary>Full Resources path for a cue, or null when the cue is unknown.</summary>
        /// <summary>Every cue id that has an audio binding. Exposed so a test can enumerate the
        /// REAL map instead of a hand-copied list that silently drifts when a cue is added.</summary>
        public static IEnumerable<string> BoundAudioCueIdsForTests => AudioPaths.Keys;

        public static string AudioPathFor(string cueId) =>
            HasAudioBinding(cueId) ? AudioRoot + AudioPaths[cueId] : null;

        /// <summary>
        /// Particle path for a palette+tier pair. Palette drives the school colour set and tier
        /// drives visual weight - deliberately NOT damage magnitude, so a balance retune can never
        /// force visual reauthoring (locked rule).
        /// </summary>
        public static string ParticlePathFor(CombatPresentationPalette palette, CombatPresentationVisualTier tier)
        {
            if (palette == CombatPresentationPalette.None || tier == CombatPresentationVisualTier.None) return null;
            return ParticleRoot + palette.ToString().ToLowerInvariant() + "_" + tier.ToString().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Resources-backed audio sink. Resolves a cue id to a clip and plays it through an injected
    /// AudioSource. Every step is null-tolerant on purpose: no SFX assets exist yet, and a missing
    /// clip must be silence, never an exception in the middle of combat.
    ///
    /// The MonoBehaviour supplies only the AudioSource; all resolution logic is here and testable.
    /// </summary>
    public sealed class ResourcesCombatAudioSink : ICombatAudioSink
    {
        private readonly AudioSource _source;
        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        /// <summary>Cue ids that were requested but had no asset behind them. Surfacing this beats
        /// silent silence - it is how a missing asset gets noticed before a build ships.</summary>
        public readonly List<string> UnresolvedCueIds = new List<string>();

        public ResourcesCombatAudioSink(AudioSource source) => _source = source;

        public void Play(string audioCueId)
        {
            AudioClip clip = Resolve(audioCueId);
            if (clip == null)
            {
                if (!string.IsNullOrEmpty(audioCueId) && !UnresolvedCueIds.Contains(audioCueId))
                    UnresolvedCueIds.Add(audioCueId);
                return;
            }
            if (_source != null) _source.PlayOneShot(clip);
        }

        public AudioClip Resolve(string audioCueId)
        {
            if (string.IsNullOrEmpty(audioCueId)) return null;
            if (_cache.TryGetValue(audioCueId, out AudioClip cached)) return cached;

            string path = CombatPresentationAssetMap.AudioPathFor(audioCueId);
            AudioClip clip = string.IsNullOrEmpty(path) ? null : Resources.Load<AudioClip>(path);
            _cache[audioCueId] = clip;
            return clip;
        }
    }

    /// <summary>
    /// Resources-backed particle sink. Resolves palette+tier to a prefab and instantiates it at the
    /// given lane. Null-tolerant for the same reason as the audio sink - no VFX assets exist yet.
    /// The hybrid ParticleSystem/flipbook decision lives inside the prefab, not here, so this layer
    /// never needs to change when that choice is made per-effect.
    /// </summary>
    public sealed class ResourcesCombatParticleSink : ICombatParticleSink
    {
        private readonly Transform _parent;
        private readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        public readonly List<string> UnresolvedParticlePaths = new List<string>();

        /// <summary>Instantiated effects, so a caller (or a test) can clean them up deterministically.</summary>
        public readonly List<GameObject> Spawned = new List<GameObject>();

        public ResourcesCombatParticleSink(Transform parent) => _parent = parent;

        public void Emit(CombatPresentationPalette palette, CombatPresentationVisualTier tier, int laneIndex)
        {
            GameObject prefab = Resolve(palette, tier);
            if (prefab == null) return;

            GameObject instance = Object.Instantiate(prefab, _parent);
            instance.name = prefab.name + "_lane" + laneIndex;
            Spawned.Add(instance);
        }

        public GameObject Resolve(CombatPresentationPalette palette, CombatPresentationVisualTier tier)
        {
            string path = CombatPresentationAssetMap.ParticlePathFor(palette, tier);
            if (string.IsNullOrEmpty(path)) return null;
            if (_cache.TryGetValue(path, out GameObject cached)) return cached;

            GameObject prefab = Resources.Load<GameObject>(path);
            _cache[path] = prefab;
            if (prefab == null && !UnresolvedParticlePaths.Contains(path)) UnresolvedParticlePaths.Add(path);
            return prefab;
        }
    }
}
