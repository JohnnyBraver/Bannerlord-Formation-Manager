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
            if (!MissionGuards.IsCurrentMissionSupported())
                return;

            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.AutoReassignmentEnabled || !settings.BacklineReassignmentEnabled)
                return;

            _elapsedSinceScan += dt;
            if (_elapsedSinceScan < ReassignmentIntervalSeconds)
                return;

            _elapsedSinceScan = 0f;
            ReassignPlayerTeamAgents("periodic scan");
        }

        public override void OnAgentDismount(Agent agent)
        {
            if (!MissionGuards.IsCurrentMissionSupported())
                return;

            ReassignAgent(agent, "dismount");
        }

        public override void OnAgentMount(Agent agent)
        {
            if (!MissionGuards.IsCurrentMissionSupported())
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
                if (!CanSafelyMoveAgent(agent, team))
                    continue;

                ReassignAgent(agent, reason);
            }
        }

        private static void ReassignAgent(Agent agent, string reason)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.AutoReassignmentEnabled)
                return;

            if (!IsManagedTroop(agent, settings))
                return;

            var character = agent.Character;
            if (character == null)
                return;

            int targetFormationIndex = GetTargetFormationIndex(agent, character, settings, reason);
            if (targetFormationIndex < 0)
                return;

            MoveToFormation(agent, targetFormationIndex, reason);
        }

        private static int GetTargetFormationIndex(Agent agent, BasicCharacterObject character, Settings settings, string reason)
        {
            var nativeClass = character.DefaultFormationClass;

            if (settings.BacklineReassignmentEnabled && IsRangedClass(nativeClass) && IsOutOfAmmo(agent))
            {
                return FormationAssignmentResolver.ToFormationIndex(agent.HasMount
                    ? settings.MountedBacklineFormation
                    : settings.BacklineFormation);
            }

            // The periodic scan exists solely for the out-of-ammo rule. It must not
            // pull troops back to their defaults and undo an OOB choice a few seconds
            // after the player begins the battle.
            if (reason == "periodic scan")
                return -1;

            if (!agent.HasMount)
            {
                if (IsMeleeCavalry(nativeClass))
                    return FormationAssignmentResolver.ToFormationIndex(settings.InfantryFormation);

                if (nativeClass == FormationClass.HorseArcher)
                    return FormationAssignmentResolver.ToFormationIndex(settings.ArcherFormation);
            }

            return FormationAssignmentResolver.ResolveFormationIndex(agent, character, settings);
        }

        private static bool IsManagedTroop(Agent agent, Settings settings)
            => TroopControlScope.ShouldManage(agent, settings);

        private static bool CanSafelyMoveAgent(Agent agent, Team expectedTeam)
        {
            if (agent == null)
                return false;

            if (agent.Mission != Mission.Current)
                return false;

            if (!agent.IsActive() || agent.State != AgentState.Active)
                return false;

            if (!agent.IsHuman || agent.IsMount || agent.IsMainAgent)
                return false;

            if (agent.Team != expectedTeam || !expectedTeam.IsPlayerTeam)
                return false;

            if (agent.Detachment != null || agent.IsDetachedFromFormation)
                return false;

            return true;
        }

        private static bool IsOutOfAmmo(Agent agent)
        {
            var equipment = agent.Equipment;
            return equipment != null && !equipment.ContainsNonConsumableRangedWeaponWithAmmo();
        }

        private static bool IsRangedClass(FormationClass formationClass)
            => formationClass == FormationClass.Ranged || formationClass == FormationClass.HorseArcher;

        private static bool IsMeleeCavalry(FormationClass formationClass)
            => formationClass == FormationClass.Cavalry || formationClass == FormationClass.HeavyCavalry ||
               formationClass == FormationClass.LightCavalry;

        private static void MoveToFormation(Agent agent, int formationIndex, string reason)
        {
            var mission = Mission.Current;
            var team = agent.Team;
            var character = agent.Character;
            if (mission == null || team == null || character == null)
                return;

            if (!CanSafelyMoveAgent(agent, team))
                return;

            var targetFormation = team.GetFormation((FormationClass)formationIndex);
            if (targetFormation == null || targetFormation.Team != team || agent.Formation == targetFormation)
                return;

            try
            {
                agent.Formation = targetFormation;
                Logger.Log($"[MissionTroopReassignmentBehavior] Moved {character.StringId} to formation {formationIndex + 1} after {reason} (native class: {character.DefaultFormationClass}, has mount: {agent.HasMount}).");
            }
            catch (System.Exception ex)
            {
                Logger.Log($"[MissionTroopReassignmentBehavior] Skipped moving {character.StringId} to formation {formationIndex + 1} after {reason}; Bannerlord rejected the move. AgentState={agent.State}, IsActive={agent.IsActive()}, IsDetached={agent.IsDetachedFromFormation}, HasDetachment={agent.Detachment != null}. Error={ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
