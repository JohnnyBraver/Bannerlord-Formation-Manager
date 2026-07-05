using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.Core;

namespace FormationManager.Patches
{
    internal static class MissionGuards
    {
        public static bool CanAttachMissionBehavior(Mission? mission)
        {
            if (mission == null)
                return false;

            return mission.CombatType == Mission.MissionCombatType.Combat
                && IsBattleOrDeploymentStartup(mission.Mode);
        }

        public static bool IsSupportedRegularBattleMission(Mission? mission)
        {
            if (mission == null)
                return false;

            return mission.CombatType == Mission.MissionCombatType.Combat
                && IsBattleOrDeployment(mission.Mode)
                && mission.PlayerTeam != null
                && HasSupportedRegularBattleBehavior(mission);
        }

        public static bool IsCurrentMissionSupported()
        {
            return IsSupportedRegularBattleMission(Mission.Current);
        }

        private static bool IsBattleOrDeploymentStartup(MissionMode mode)
        {
            return mode == MissionMode.StartUp
                || mode == MissionMode.Deployment
                || mode == MissionMode.Battle;
        }

        private static bool IsBattleOrDeployment(MissionMode mode)
        {
            return mode == MissionMode.Deployment
                || mode == MissionMode.Battle;
        }

        private static bool HasSupportedRegularBattleBehavior(Mission mission)
        {
            return mission.HasMissionBehavior<BattleDeploymentHandler>();
        }
    }
}
