using TaleWorlds.MountAndBlade;

namespace FormationManager.Patches
{
    internal static class MissionGuards
    {
        public static bool IsNavalMission(Mission? mission)
        {
            return mission != null && (mission.IsNavalBattle || mission.IsNavalRaidBattle);
        }

        public static bool IsCurrentMissionNaval()
        {
            return IsNavalMission(Mission.Current);
        }
    }
}
