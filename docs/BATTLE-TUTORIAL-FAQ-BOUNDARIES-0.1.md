# Battle Tutorial / FAQ Mechanic Boundaries v0.1

Status: implementation note; no new mechanics or code authorized.

## Owner-approved explanatory content

- `Front: Gain +1 Attack per clash.`
- `Middle: Gain +1 Health per clash.`
- `Back: Gain +2 Energy per clash.`

These lines explain existing lane effects when the player places cards. They are content descriptions, not new rules or balance changes. CR must keep the existing numeric resolution unchanged.

- Combat is automatic and timer/tick-driven; the player’s active combat choice is spell casting when legal.
- Reinforcement is optional and only available in the authored tick-4/tick-8 windows.
- The tutorial placement instruction remains `Place a card in the Back lane.` when the step teaches placement only.
- The Perfect card sentence remains card-specific: `Perfect: On Play in the Back lane - draw a card (no bonus in Front/Middle).`
- No END TURN or Auto Battle claim is approved for the current combat flow.

## FAQ/tutorial boundaries still requiring owner/content confirmation

1. Whether the one-tap reinforcement explanation is shown in tutorial/FAQ or deferred until native captures prove the interaction.
2. Exact wording for fewer-than-three hotkeys and no-op states in localized tutorial surfaces.
3. Whether the global lane lines appear in tutorial onboarding or only in a general Battle reference panel.

Do not teach a fixed six-card reserve, guaranteed three hotkeys, seeded RNG, raw match seed, or unsupported opponent spellcasting. These are implementation/diagnostic details or unresolved contracts, not player lessons.

## Capture gate

ST may bind the approved lines after content signoff; VE must capture tutorial and Battle states at native mobile profiles and verify wrapping, contrast, and no clipping. This note is attached to ST-REVAMP-001 and BS-BATTLE-007; it does not authorize production changes.
