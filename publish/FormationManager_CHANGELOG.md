# Changelog - Formation Manager

## [v0.5.0] - 2026-07-14

### Added
- **Light Infantry Formation**: Configure a separate default slot for unshielded, non-specialist foot melee troops alongside the renamed **Shield Infantry Formation** setting.
- **Spawned Equipment Classification Toggle**: Role defaults use the shared classifier's spawned-equipment result by default. Turn it off to use standard troop equipment-set classification, preserving the pre-v0.5 behaviour.
- **Troop Classifier Dependency**: Formation Manager now uses the shared **Troop Classifier** module, establishing one role vocabulary and one set of weapon rules for compatible mods.

### Changed
- **Settings Reorganization**: Settings now follow the battle setup flow: general controls, role defaults, multi-formation splits, mid-battle reassignment, then allied troop control. Out-of-ammo backline reassignment is now enabled by default.

## [v0.4.1] - 2026-07-14

### Added
- **Managed OOB Slider Lock**: Optionally lock Bannerlord's native OOB weight sliders for cards seeded by Formation Manager. Enabled by default; turn it off to rebalance those cards manually.
- **Opt-In Allied Troop Control**: Allied infantry, ranged troops, cavalry, and horse archers are no longer placed in your formations by default. Each class can be enabled separately in the new **Allied Troop Control (Opt-In)** MCM section.

### Changed
- **Clear Pre-Battle and Mid-Battle Boundaries**: OOB uses a complete deployment plan for exact troop placement, while the optional mid-battle system remains limited to mount-state and out-of-ammo reactions. Both respect the same regular-troop eligibility rules.
- **Hero Boundaries**: Formation Manager no longer shows assignment controls for heroes or changes their formation. Bannerlord keeps manual captain and elite-formation placement intact.

### Fixed
- **Correct OOB Card Classes**: An OOB card class determined from the actual agents assigned to it is no longer overwritten by a broader roster fallback. This prevents modded infantry formations from being changed into cavalry cards.
- **Target-Only Surplus Splits**: Advanced plans with targets but no weights now use their target ratio for surplus troops instead of silently favouring a fallback formation.
- **Clean Empty OOB Cards**: Empty Formation Manager slots are cleared again after Bannerlord's native deployment pass, so they cannot retain or recreate stale native card types.

## [v0.4.0] - 2026-07-11

### Added
- **Advanced Formation Plans**: Create a saved per-troop plan across all eight formations, with direct **Target** counts and **Weight** values for surplus troops.
- **Expanded Party Editor**: Open a troop row to view its eight OOB-style formation cards, predicted assignment, target, and weight. Inactive formations can be added directly from the editor.
- **Draft, Save, Restore, and Autosave Controls**: Advanced changes remain a draft until saved by default. Clear and Restore are staged actions, and optional autosave commits every advanced edit immediately.
- **Advanced Plan Toggle**: Disable advanced plans from MCM to hide their editor and place active plans in a recoverable archive.
- **Native Mixed OOB Cards**: Supports Bannerlord's compatible infantry+ranged and cavalry+horse-archer formation cards.

### Changed
- **Precise OOB Placement**: Saved formation plans reapply exact troop-stack placements after Bannerlord completes its native class-pool setup, preserving configured splits instead of substituting other troops of the same class.
- **Simple-to-Advanced Baseline**: Until an advanced plan is saved, the editor follows the troop's current simple one/two-formation assignment. A saved advanced plan takes ownership and replaces the simple row controls with an `A` indicator.
- **Short-Stack Policy**: Custom plans can prioritize weights in under-strength stacks, or use the default one-per-active-formation distribution before weighted surplus.

## [v0.3.0] - 2026-07-10

### Added
- **Editable Order of Battle Defaults**: Your saved troop and role assignments now seed the OOB screen without locking it. Change card types and use the native weight controls for the battle at hand.
- **Troop Role Defaults**: Configure formation slots for frontline, shock, pike, skirmisher, archer, crossbow, cavalry, and horse-archer roles. The feature is enabled by default and can be turned off in MCM.
- **Two-Formation Stack Splits**: Party-screen troop badges now include an optional second slot, letting one troop stack start split across two formations.

### Changed
- **Clearer Role Settings**: Renamed the MCM section to “Troop Role Defaults” and placed its enable toggle at the top.

### Fixed
- **Balanced Odd Splits**: Multiple odd-numbered stacks assigned to the same pair of formations now share remainder troops between both sides instead of always favouring the first formation.
- **Correct OOB Card Types**: Role-based card typing now ignores the player character, so a mounted main hero cannot turn a shock-infantry formation into a cavalry card.
- **OOB Choice Preservation**: Periodic reassignment no longer pulls troops back to defaults after deployment; it only performs the optional out-of-ammo backline check.

## [v0.2.1] - 2026-07-05

### Changed
- **Regular Battle Whitelist**: Limited Formation Manager's custom assignment logic to regular battle missions with the vanilla battle deployment handler. Siege, naval, and unknown mission types now stay on vanilla formation behavior.

### Fixed
- **DLC-Safe Mission Guard**: Removed direct naval mission property checks so the mod does not require DLC-only mission APIs while deciding whether to run custom formation logic.

## [v0.2.0] - 2026-07-04

### Added
- **Mid-Battle Reassignment Controls**: Added a master MCM toggle plus configurable Infantry, Archer, Cavalry, Backline, and Mounted Backline formation slots. The default layout keeps infantry in I, archers in II, cavalry in III, and reserves VIII for optional backline use.
- **Mounted Archer State Handling**: Mounted archers now resolve across mounted/foot and ammo/no-ammo states. With ammo they route to cavalry while mounted and archers while dismounted; out-of-ammo backline routing is available as an explicit opt-in.
- **Bannerlord API Inspection Tools**: Added local build and Bannerlord API inspection wrappers so mission hooks and game symbols can be verified without relying on constrained PowerShell reflection.

### Changed
- **Mission Reassignment Model**: Replaced the narrow mount/dismount patch with a mission behavior that reacts immediately to mount-state changes and periodically audits active player troops for state drift.
- **Out-of-Ammo Backline Default**: Backline movement is disabled by default. Players who want depleted ranged troops pulled out of combat can enable the separate backline toggle.

### Fixed
- **Naval Battle Safety**: Formation Manager now detects naval and naval raid missions and leaves mission formation behavior to vanilla systems, avoiding custom assignment logic in battles the mod cannot safely validate locally.
- **Build Scratch Hygiene**: Added repository ignore rules and wrappers to keep local dotnet/MSBuild scratch files out of the working tree.
