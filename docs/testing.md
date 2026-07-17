# Testing and Inspection

Use the repository wrappers instead of raw `dotnet` commands. They keep temporary files under `.tmp/dotnet-temp`, disable MSBuild node reuse, and avoid deploying into the live Bannerlord module folder unless explicitly requested.

## Build

```powershell
& .\scripts\build.ps1
```

Deploy into the local Bannerlord `Modules\FormationManager` folder:

```powershell
& .\scripts\build.ps1 -Deploy
```

Run with restore after package or project changes:

```powershell
& .\scripts\build.ps1 -Restore
```

## Inspect Bannerlord APIs

The inspector is a tiny compiled helper because direct PowerShell reflection can be blocked by constrained language mode.

```powershell
& .\scripts\inspect-bannerlord.ps1 -Type TaleWorlds.MountAndBlade.Mission -Member OnAgentDismount
& .\scripts\inspect-bannerlord.ps1 -Type TaleWorlds.Core.FormationClass
& .\scripts\inspect-bannerlord.ps1 -Type TaleWorlds.MountAndBlade.Formation -Member RidingOrder
```

Set `BANNERLORD_GAME_BIN` to point at another install if needed.

## Party-screen UI performance

### 2026-07-17 role-plan editor A/B test

The original role-plan editor was injected into every `PartyTroopTuple`, even
though only the player hero could reveal it. `IsVisible="false"` hides a
Gauntlet widget but does not stop its construction. The static editor therefore
created 84 buttons, 104 text widgets, and 238 bindings for *every* troop row.

For the A/B build, the `PartyTroopTupleRolePlanEditor` prefab patch is compiled
out with `ROLE_PLAN_EDITOR` undefined, and role-plan binding refreshes are
disabled. In a party with 23 live row mixins, the in-mod selection handler
recorded 24 samples with a 15.69–34.33 ms range and an 18.85 ms average. The
player reported the corresponding UI response as nearly instant.

The timer writes lines such as the following to
`Documents\Mount and Blade II Bannerlord\Configs\FormationManager\log.txt`:

```text
[PartySelectionTiming] row=main_hero mixins=23 roleEditorEnabled=False handlerMs=16.29
```

The timer measures Formation Manager's handler only, not Bannerlord's complete
frame time. The A/B result still establishes that the hidden role-editor widget
tree was the dominant perceived cost.

The second half of the A/B test re-enabled the same prefab and binding refresh
path. With the same 23 live row mixins, 12 samples measured 1016.16–1040.61 ms
with a 1023.71 ms average. This is roughly 54 times the disabled-panel handler
average and matches the player-visible one-second stall. The problem is not
role allocation logic: it is the static widget tree and the binding notifications
sent to that tree for every troop row.

Before re-enabling the feature, render role controls from a data-driven list
that is populated only for the player hero (or from a dedicated party-screen
panel). Do not place a large static control grid in the prefab shared by every
troop row. Also replace the all-row selection refresh with updates to only the
previously selected and newly selected rows.
