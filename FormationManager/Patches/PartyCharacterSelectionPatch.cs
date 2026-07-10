using HarmonyLib;
using FormationManager.UI;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace FormationManager.Patches
{
    /// <summary>Refreshes the sibling editor after Bannerlord opens or closes a troop row.</summary>
    [HarmonyPatch(typeof(PartyCharacterVM), "ExecuteSetSelected")]
    internal static class PartyCharacterSelectionPatch
    {
        private static void Postfix(PartyCharacterVM __instance)
            => PartyCharacterVMMixin.NotifySelectionChanged(__instance);
    }
}
