using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FormationManager.Data;

namespace FormationManager.Patches
{
    /// <summary>
    /// Patches OrderController.RearrangeFormationsAccordingToFilters.
    /// Instead of completely bypassing troop redistribution, we filter out custom-assigned agents
    /// from the redistribution lists.
    /// This allows unassigned agents to still be dynamically rearranged via the UI sliders,
    /// while custom-assigned agents remain strictly locked to their assigned formations.
    /// </summary>
    [HarmonyPatch(typeof(OrderController), "RearrangeFormationsAccordingToFilters")]
    public static class OrderControllerPatches
    {
        [HarmonyPrefix]
        public static bool Prefix(
            Team team,
            List<ValueTuple<Formation, int, TroopTraitsMask, List<Agent>>> filters)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return true;

            if (team == null || !team.IsPlayerTeam)
                return true;

            if (!FormationAssignmentStore.HasAnyAssignments)
                return true;

            if (filters == null)
                return true;

            Logger.Log("[OrderControllerPatches] Filtering custom-assigned agents out of auto-distribution...");

            foreach (var filter in filters)
            {
                var agentList = filter.Item4;
                if (agentList == null) continue;

                // Loop backwards to safely remove elements while iterating
                for (int i = agentList.Count - 1; i >= 0; i--)
                {
                    var agent = agentList[i];
                    if (agent == null) continue;

                    var character = agent.Character;
                    if (character == null) continue;

                    int assignedIndex = FormationAssignmentStore.GetAssignment(character.StringId);
                    if (assignedIndex >= 0 && assignedIndex <= 7)
                    {
                        // Ensure the agent is physically in their assigned formation
                        var targetFormation = team.GetFormation((FormationClass)assignedIndex);
                        if (targetFormation != null && agent.Formation != targetFormation)
                        {
                            agent.Formation = targetFormation;
                        }

                        // Remove from the redistribution pool so sliders don't touch them
                        agentList.RemoveAt(i);
                    }
                }
            }

            // Return true to allow the original method to distribute the remaining UNASSIGNED agents
            return true;
        }
    }
}
