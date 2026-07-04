using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FormationManager.Data;

namespace FormationManager.Patches
{
    internal sealed class MissionTroopReassignmentBehavior : MissionBehavior
    {
        private const float ReassignmentIntervalSeconds = 3f;

        private float _elapsedSinceScan;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            if (MissionGuards.IsCurrentMissionNaval())
                return;

            _elapsedSinceScan += dt;
            if (_elapsedSinceScan < ReassignmentIntervalSeconds)
                return;

            _elapsedSinceScan = 0f;
            ReassignPlayerTeamAgents("periodic scan");
        }

        public override void OnAgentDismount(Agent agent)
        {
            if (MissionGuards.IsCurrentMissionNaval())
                return;

            ReassignAgent(agent, "dismount");
        }

        public override void OnAgentMount(Agent agent)
        {
            if (MissionGuards.IsCurrentMissionNaval())
                return;

            ReassignAgent(agent, "mount");
        }

        private static void ReassignPlayerTeamAgents(string reason)
        {
            var mission = Mission.Current;
            var team = mission?.PlayerTeam;
            if (team == null)
                return;

            foreach (var agent in team.ActiveAgents)
            {
                ReassignAgent(agent, reason);
            }
        }

        private static void ReassignAgent(Agent agent, string reason)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.AutoReassignmentEnabled)
                return;

            if (!IsPlayerTroop(agent))
                return;

            var character = agent.Character;
            if (character == null)
                return;

            int targetFormationIndex = GetTargetFormationIndex(agent, character, settings);
            if (targetFormationIndex < 0)
                return;

            MoveToFormation(agent, targetFormationIndex, reason);
        }

        private static int GetTargetFormationIndex(Agent agent, BasicCharacterObject character, Settings settings)
        {
            var nativeClass = character.DefaultFormationClass;

            if (settings.BacklineReassignmentEnabled && IsRangedClass(nativeClass) && IsOutOfAmmo(agent))
            {
                return ToFormationIndex(agent.HasMount
                    ? settings.MountedBacklineFormation
                    : settings.BacklineFormation);
            }

            if (!agent.HasMount)
            {
                if (IsMeleeCavalry(nativeClass))
                    return ToFormationIndex(settings.InfantryFormation);

                if (nativeClass == FormationClass.HorseArcher)
                    return ToFormationIndex(settings.ArcherFormation);
            }

            int assignedIndex = FormationAssignmentStore.GetAssignment(character.StringId);
            if (assignedIndex >= 0 && assignedIndex <= 7)
                return assignedIndex;

            return GetDefaultFormationIndex(agent, nativeClass, settings);
        }

        private static int GetDefaultFormationIndex(Agent agent, FormationClass nativeClass, Settings settings)
        {
            if (IsInfantryClass(nativeClass))
                return ToFormationIndex(settings.InfantryFormation);

            if (nativeClass == FormationClass.Ranged)
                return ToFormationIndex(settings.ArcherFormation);

            if (IsMeleeCavalry(nativeClass))
                return ToFormationIndex(agent.HasMount ? settings.CavalryFormation : settings.InfantryFormation);

            if (nativeClass == FormationClass.HorseArcher)
                return ToFormationIndex(agent.HasMount ? settings.CavalryFormation : settings.ArcherFormation);

            return -1;
        }

        private static bool IsPlayerTroop(Agent agent)
        {
            if (agent == null || !agent.IsHuman || agent.IsMainAgent)
                return false;

            var team = agent.Team;
            return team != null && team.IsPlayerTeam;
        }

        private static bool IsOutOfAmmo(Agent agent)
        {
            var equipment = agent.Equipment;
            return equipment != null && !equipment.ContainsNonConsumableRangedWeaponWithAmmo();
        }

        private static bool IsInfantryClass(FormationClass formationClass)
        {
            return formationClass == FormationClass.Infantry
                || formationClass == FormationClass.HeavyInfantry
                || formationClass == FormationClass.Skirmisher;
        }

        private static bool IsRangedClass(FormationClass formationClass)
        {
            return formationClass == FormationClass.Ranged
                || formationClass == FormationClass.HorseArcher;
        }

        private static bool IsMeleeCavalry(FormationClass formationClass)
        {
            return formationClass == FormationClass.Cavalry
                || formationClass == FormationClass.HeavyCavalry
                || formationClass == FormationClass.LightCavalry;
        }

        private static int ToFormationIndex(int configuredFormation)
        {
            if (configuredFormation < 1)
                return 0;

            if (configuredFormation > 8)
                return 7;

            return configuredFormation - 1;
        }

        private static void MoveToFormation(Agent agent, int formationIndex, string reason)
        {
            var team = agent.Team;
            var character = agent.Character;
            if (team == null || character == null)
                return;

            var targetFormation = team.GetFormation((FormationClass)formationIndex);
            if (targetFormation == null || agent.Formation == targetFormation)
                return;

            agent.Formation = targetFormation;
            Logger.Log($"[MissionTroopReassignmentBehavior] Moved {character.StringId} to formation {formationIndex + 1} after {reason} (native class: {character.DefaultFormationClass}, has mount: {agent.HasMount}).");
        }
    }
}
