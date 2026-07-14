using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FormationManager.Patches
{
    /// <summary>
    /// Limits Formation Manager to regular troops from the player's party unless
    /// the user has deliberately opted into a matching allied troop class.
    /// </summary>
    internal static class TroopControlScope
    {
        public static bool IsEligibleTroop(BasicCharacterObject? character)
            => character != null && !(character is CharacterObject campaignCharacter && campaignCharacter.HeroObject != null);

        public static bool ShouldManage(Agent? agent, Settings settings)
        {
            if (agent == null || !agent.IsHuman || agent.IsMount || agent.IsMainAgent || !IsEligibleTroop(agent.Character))
                return false;

            if (agent.Team == null || !agent.Team.IsPlayerTeam)
                return false;

            if (IsFromMainParty(agent))
                return true;

            return IsAlliedClassEnabled(agent.Character!.DefaultFormationClass, settings);
        }

        private static bool IsAlliedClassEnabled(FormationClass formationClass, Settings settings)
        {
            switch (formationClass)
            {
                case FormationClass.Ranged:
                    return settings.ManageAlliedRanged;
                case FormationClass.HorseArcher:
                    return settings.ManageAlliedHorseArchers;
                case FormationClass.Cavalry:
                case FormationClass.LightCavalry:
                case FormationClass.HeavyCavalry:
                    return settings.ManageAlliedCavalry;
                default:
                    return settings.ManageAlliedInfantry;
            }
        }

        private static bool IsFromMainParty(Agent agent)
        {
            object? origin = agent.Origin;
            object? originParty = origin == null
                ? null
                : AccessTools.Property(origin.GetType(), "Party")?.GetValue(origin);
            return originParty != null && ReferenceEquals(originParty, MobileParty.MainParty?.Party);
        }
    }
}
