# Changelog - Formation Manager

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
