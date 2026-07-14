using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FormationManager.Data;

namespace FormationManager.Patches
{
    /// <summary>
    /// Postfix patch on Mission.SpawnTroop.
    /// After an agent is spawned, move it to its explicit assignment or role default.
    /// This fires for both initial spawns (including OOB preview setup) and reinforcement waves.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "SpawnTroop")]
    internal static class MissionAgentSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            Mission __instance,
            IAgentOriginBase troopOrigin,
            bool isPlayerSide,
            Agent __result)
        {
            if (__result == null)
                return;

            if (__result.IsMainAgent)
                return;

            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (!FormationAssignmentResolver.HasCustomDefaults(settings))
                return;

            if (!MissionGuards.IsSupportedRegularBattleMission(__instance))
                return;

            if (!isPlayerSide)
                return;

            if (!TroopControlScope.ShouldManage(__result, settings))
                return;

            var character = troopOrigin.Troop;
            if (character == null)
                return;

            int assignedIndex = FormationAssignmentResolver.ResolveFormationIndex(__result, character, settings);
            if (assignedIndex < 0 || assignedIndex > 7)
                return;

            var team = __instance.PlayerTeam;
            if (team == null)
                return;

            var formation = team.GetFormation((FormationClass)assignedIndex);
            if (formation == null || formation.Team != team)
                return;

            try
            {
                __result.Formation = formation;
                Logger.Log($"[MissionAgentSpawnPatch] Moved agent {character.StringId} to formation {assignedIndex} (Name: {character.Name})");
            }
            catch (System.Exception ex)
            {
                Logger.Log($"[MissionAgentSpawnPatch] Could not move {character.StringId} to formation {assignedIndex}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
