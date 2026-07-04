using HarmonyLib;
using TaleWorlds.MountAndBlade;
using FormationManager.Data;

namespace FormationManager.Patches
{
    [HarmonyPatch(typeof(OrderController), "RearrangeFormationsAccordingToFilters")]
    public static class OrderControllerPatches
    {
        [HarmonyPrefix]
        public static bool Prefix(Team team)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return true;

            if (MissionGuards.IsCurrentMissionNaval())
                return true;

            if (team != null && team.IsPlayerTeam)
            {
                if (FormationAssignmentStore.HasAnyAssignments)
                {
                    Logger.Log("[OrderControllerPatches] Bypassing auto-distribution for player team due to custom assignments.");
                    return false; // Skip the original distribution!
                }
            }
            return true;
        }
    }
}
