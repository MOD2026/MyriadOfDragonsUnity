# Tutorial Opening Flashback V1

Status: forward-production concept and implementation handoff; no production changes authorized.

## Purpose and guardrails

Add a brief cosmetic cold open before the existing Chapter 1 tutorial. The supplied image `G:\My Drive\card game\Drawing\Vuk kostic\najbolji 27.jpg` is the approved background plate (5946x3848): a wide painterly battlefield with a central pale titan-like figure, dragons, foreground soldiers, smoke/light, and a baked Myriad of Dragons logo at bottom-right. The logo remains static as part of the plate; do not redraw, animate, replace, or add a second logo. The flashback presents a Titan-versus-Dragon clash as a fragment of history or vision, not omniscient proof. It establishes stakes and mystery without resolving the conflict or changing gameplay, rewards, currencies, Save data, combat resolution, or navigation. The existing five-beat callbacks remain unchanged.

## Timing and beat order

Total cold open: 2.5-4.0 seconds, one-shot, landscape 16:9. Then enter the existing five-beat flow.

1. 0.0-0.5s, Black and ember: near-black frame and low ember pulse. No text or UI.
2. 0.5-2.2s, Flash of scale: restrained pan/zoom/parallax across the supplied Titan/Dragon battlefield plate; keep the baked logo inside the crop and avoid overlays.
3. 2.2-3.2s, Collision memory: brief smoke/light movement across the central clash; do not show a definitive winner or add lore claims.
4. 3.2-4.0s, Handoff: settle the plate and invoke the existing Chapter 1 tutorial entry, followed by starter-card reveal, Formation, first controlled battle, and Return to Empire.

## Sound and music

Use the existing Questing cue for the cold open and transition. Add only short wind, ember, and low impact SFX if already available; otherwise mark them as missing assets. No copyrighted music and no new voiceover are required.

## Skip, Reduced Motion, replay, and resume

Skip is available throughout the 2.5-4.0 second cold open and immediately invokes the existing tutorial-entry callback; it does not fabricate a battle result or reward. Reduced Motion skips the cold open immediately, matching the approved Option A behavior for the existing opening. Replay uses the existing tutorial replay entry only. Reconnect/resume uses existing authoritative tutorial checkpoints; if the cold open was completed, resume at the current checkpoint and do not replay it automatically.

## CR implementation handoff

Attach the cold-open presenter to the existing Chapter 1 tutorial entry seam, before the current opening animation. Required callbacks are handoff labels only: `FlashbackStarted`, `FlashbackCompleted` (natural), `FlashbackSkipped`, then the unchanged existing tutorial entry. Do not alter `OpeningCompleted`, starter reveal, Formation, `BattleEntered`, `ResultShown`, or `ReturnToEmpireRequested`. Use video or layered-still playback according to CR performance evidence; no new navigation or Save schema. Keep any runtime copy in a separate localization-safe layer, never over the baked logo.

## Assets and acceptance

Required reference: supplied JPG background plate with baked logo. Reuse existing Battle/Empire background, ember, smoke, and blue-light assets where verified. Missing only if absent: one neutral wind SFX, one ember pulse SFX, and optional transparent smoke/light overlays. AD must approve 16:9 crop and logo-safe placement; VE/CR must verify skip equivalence, reduced-motion immediate bypass, callback ordering, replay teardown, and no tutorial outcome spoiler. No separate logo asset is required.
