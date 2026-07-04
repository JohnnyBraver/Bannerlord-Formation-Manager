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
