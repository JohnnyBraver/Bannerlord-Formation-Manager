using System;
using FormationManager.Data;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FormationManager.Patches
{
    /// <summary>Reapplies exact agents after native OOB class-pool distribution.</summary>
    internal static class OobPreviewAssignmentApplier
    {
        public static void Apply(Team team, OobDefaultAssignmentPlan assignmentPlan, Settings settings, string stage)
        {
            foreach (var agent in team.ActiveAgents)
            {
                var character = agent.Character;
                if (character == null || agent.IsMount || agent.IsMainAgent)
                    continue;

                int assignedIndex = assignmentPlan.GetFormationIndex(agent, character, settings);
                if (assignedIndex is < 0 or > 7)
                    continue;

                var targetFormation = team.GetFormation((FormationClass)assignedIndex);
                if (targetFormation == null || targetFormation.Team != team || agent.Formation == targetFormation)
                    continue;

                try
                {
                    agent.Formation = targetFormation;
                    Logger.Log($"[OobPreviewAssignmentApplier] Applied {stage}: {character.StringId} -> formation {assignedIndex + 1} (Name: {character.Name})");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[OobPreviewAssignmentApplier] Could not apply {stage} for {character.StringId}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }
}
