using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FormationManager.Data;

namespace FormationManager.Patches
{
    /// <summary>
    /// Postfix patch on Mission.SpawnTroop.
    /// After an agent is spawned, if the player has assigned their troop type to a specific
    /// formation, we immediately move the agent into that formation.
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

            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (MissionGuards.IsNavalMission(__instance))
                return;

            if (!isPlayerSide)
                return;

            var character = troopOrigin.Troop;
            if (character == null)
                return;

            int assignedIndex = FormationAssignmentStore.GetAssignment(character.StringId);
            if (assignedIndex < 0 || assignedIndex > 7)
                return;

            var team = __instance.PlayerTeam;
            if (team == null)
                return;

            var targetFormationClass = (FormationClass)assignedIndex;
            var formation = team.GetFormation(targetFormationClass);
            if (formation != null)
            {
                __result.Formation = formation;
                Logger.Log($"[MissionAgentSpawnPatch] Moved agent {character.StringId} to formation {assignedIndex} (Name: {character.Name})");
            }
        }
    }
}
