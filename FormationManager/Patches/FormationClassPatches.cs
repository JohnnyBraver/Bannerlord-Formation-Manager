using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using FormationManager.Data;

namespace FormationManager.Patches
{
    // We disable the LogicalClass and PhysicalClass patches to avoid the "class lie".
    // Agents will be evaluated by their true native classes.

    /// <summary>
    /// Patches the RefreshFormation method on the OOB formation item VM.
    /// Dynamically activates custom cards using the native classes of the assigned troops.
    /// </summary>
    [HarmonyPatch(typeof(OrderOfBattleFormationItemVM), "RefreshFormation", new Type[] { typeof(Formation), typeof(DeploymentFormationClass), typeof(bool) })]
    internal static class RefreshFormationPatch
    {
        [HarmonyPrefix]
        private static void Prefix(OrderOfBattleFormationItemVM __instance, Formation formation, ref DeploymentFormationClass overriddenClass, ref bool mustExist)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (MissionGuards.IsCurrentMissionNaval())
                return;

            if (formation == null)
                return;

            int idx = formation.Index;
            bool hasTroops = HasTroopsAssigned(idx);

            if (hasTroops)
            {
                DeploymentFormationClass targetClass = GetCustomAssignmentClass(idx);
                if (targetClass != DeploymentFormationClass.Unset)
                {
                    overriddenClass = targetClass;
                    mustExist = true; // Force card activation
                    Logger.Log($"[RefreshFormationPatch] Prefix overrode formation {idx} to {overriddenClass}");
                }
            }
        }

        [HarmonyPostfix]
        private static void Postfix(OrderOfBattleFormationItemVM __instance, Formation formation)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (MissionGuards.IsCurrentMissionNaval())
                return;

            if (formation == null)
                return;

            int idx = formation.Index;
            var selector = __instance.FormationClassSelector;
            if (selector == null)
                return;

            var selectedItem = selector.SelectedItem;
            var selectedClass = selectedItem != null ? selectedItem.FormationClass.ToString() : "null";
            Logger.Log($"[RefreshFormationPatch] Postfix for formation {idx}: SelectedIndex={selector.SelectedIndex}, SelectedItem.FormationClass={selectedClass}");
        }

        private static bool HasTroopsAssigned(int formationIndex)
        {
            var mainParty = MobileParty.MainParty;
            if (mainParty?.MemberRoster == null)
                return false;

            for (int i = 0; i < mainParty.MemberRoster.Count; i++)
            {
                var element = mainParty.MemberRoster.GetElementCopyAtIndex(i);
                if (element.Character == null)
                    continue;

                int assignedIndex = FormationAssignmentStore.GetAssignment(element.Character.StringId);
                if (assignedIndex == formationIndex)
                {
                    if (element.Number > element.WoundedNumber)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static DeploymentFormationClass GetCustomAssignmentClass(int formationIndex)
        {
            var mainParty = MobileParty.MainParty;
            bool hasCustomTroops = false;

            if (mainParty?.MemberRoster != null)
            {
                for (int i = 0; i < mainParty.MemberRoster.Count; i++)
                {
                    var element = mainParty.MemberRoster.GetElementCopyAtIndex(i);
                    if (element.Character == null) continue;
                    if (element.Number <= element.WoundedNumber) continue;

                    int assignedIndex = FormationAssignmentStore.GetAssignment(element.Character.StringId);
                    if (assignedIndex == formationIndex)
                    {
                        var nativeClass = element.Character.DefaultFormationClass;
                        return MapToDeploymentClass(nativeClass);
                    }
                    if (assignedIndex >= 0)
                    {
                        hasCustomTroops = true;
                    }
                }
            }

            var mainHero = Hero.MainHero;
            if (mainHero != null)
            {
                int assignedIndex = FormationAssignmentStore.GetAssignment(mainHero.CharacterObject.StringId);
                if (assignedIndex == formationIndex)
                {
                    return MapToDeploymentClass(mainHero.CharacterObject.DefaultFormationClass);
                }
                if (assignedIndex >= 0)
                {
                    hasCustomTroops = true;
                }
            }

            // If the player has configured custom assignments, any slot that does NOT have custom assigned troops should be Unset.
            if (hasCustomTroops)
            {
                return DeploymentFormationClass.Unset;
            }

            // Fallback to default OOB slot classes ONLY if there are no custom assignments in the entire party (vanilla behavior)
            if (formationIndex == 0 || formationIndex == 4 || formationIndex == 5)
                return DeploymentFormationClass.Infantry;
            if (formationIndex == 1)
                return DeploymentFormationClass.Ranged;
            if (formationIndex == 2 || formationIndex == 6 || formationIndex == 7)
                return DeploymentFormationClass.Cavalry;
            if (formationIndex == 3)
                return DeploymentFormationClass.HorseArcher;

            return DeploymentFormationClass.Unset;
        }

        public static DeploymentFormationClass MapToDeploymentClass(FormationClass fc)
        {
            switch (fc)
            {
                case FormationClass.Infantry:
                case FormationClass.HeavyInfantry:
                case FormationClass.Skirmisher:
                    return DeploymentFormationClass.Infantry;
                case FormationClass.Ranged:
                    return DeploymentFormationClass.Ranged;
                case FormationClass.Cavalry:
                case FormationClass.LightCavalry:
                case FormationClass.HeavyCavalry:
                    return DeploymentFormationClass.Cavalry;
                case FormationClass.HorseArcher:
                    return DeploymentFormationClass.HorseArcher;
                default:
                    return DeploymentFormationClass.Unset;
            }
        }
    }

    /// <summary>
    /// Patches the SetInitialHeroFormations method on the OOB VM.
    /// Programmatically distributes card weights based on dynamic troop native classes.
    /// </summary>
    [HarmonyPatch(typeof(OrderOfBattleVM), "SetInitialHeroFormations")]
    internal static class SetInitialHeroFormationsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(OrderOfBattleVM __instance)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (MissionGuards.IsCurrentMissionNaval())
                return;

            Logger.Log("[SetInitialHeroFormationsPatch] Postfix: Distributing card weights...");
            WeightDistributor.DistributeWeights(__instance);
        }
    }

    /// <summary>
    /// Patches the Initialize method on the OrderOfBattleVM.
    /// Run at the very end of VM initialization to ensure that:
    /// 1. Preview agents are moved to their correct assigned formations (since the engine's internal setup resets them).
    /// 2. The dropdown selector and card classes list are forced to sync with the target classes.
    /// 3. Weights and counts are updated to reflect this final layout.
    /// </summary>
    [HarmonyPatch(typeof(OrderOfBattleVM), "Initialize")]
    internal static class OrderOfBattleVMInitializePatch
    {
        [HarmonyPostfix]
        private static void Postfix(OrderOfBattleVM __instance)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            if (MissionGuards.IsCurrentMissionNaval())
                return;

            Logger.Log("[OrderOfBattleVMInitializePatch] Postfix: Enforcing custom assignments on OOB cards and preview agents...");

            var mission = Mission.Current;
            if (mission == null)
            {
                Logger.Log("[OrderOfBattleVMInitializePatch] Mission is null!");
                return;
            }

            var team = mission.PlayerTeam;
            if (team == null)
            {
                Logger.Log("[OrderOfBattleVMInitializePatch] PlayerTeam is null!");
                return;
            }

            // 1. Move preview agents to their assigned formations
            foreach (var agent in team.ActiveAgents)
            {
                if (agent.Character == null) continue;

                int assignedIndex = FormationAssignmentStore.GetAssignment(agent.Character.StringId);
                if (assignedIndex >= 0 && assignedIndex <= 7)
                {
                    var targetFormation = team.GetFormation((FormationClass)assignedIndex);
                    if (targetFormation != null && agent.Formation != targetFormation)
                    {
                        agent.Formation = targetFormation;
                        Logger.Log($"[OrderOfBattleVMInitializePatch] Moved preview agent {agent.Character.StringId} to formation {assignedIndex} (Name: {agent.Character.Name})");
                    }
                }
            }

            // 2. Force the correct classes on the cards and class VMs
            var formationsList = __instance.FormationsFirstHalf.Concat(__instance.FormationsSecondHalf).ToList();
            foreach (var item in formationsList)
            {
                if (item.Formation == null) continue;
                int idx = item.Formation.Index;

                var targetClass = RefreshFormationPatch.GetCustomAssignmentClass(idx);
                if (targetClass != DeploymentFormationClass.Unset)
                {
                    Logger.Log($"[OrderOfBattleVMInitializePatch] Forcing formation {idx} card class to {targetClass}");
                    item.RefreshFormation(item.Formation, targetClass, true);

                    // Manually enforce the class VM backing property to match targetClass.
                    // This guarantees that the UI slider and backing class match exactly,
                    // bypassing any binding update glitches in the base game's selector event loop.
                    var targetNativeClass = MapToNativeClass(targetClass);
                    if (item.Classes != null && item.Classes.Count > 0)
                    {
                        item.Classes[0].Class = targetNativeClass;
                        if (item.Classes.Count > 1)
                        {
                            item.Classes[1].Class = FormationClass.Unset;
                        }
                    }
                }
            }

            // 3. Redistribute weights on final card classes
            WeightDistributor.DistributeWeights(__instance);

            // 4. Call OnSizeChanged on all cards to update counts
            foreach (var item in formationsList)
            {
                item.OnSizeChanged();
            }

            // 5. Refresh weights and UI via reflection
            try
            {
                AccessTools.Method(typeof(OrderOfBattleVM), "RefreshWeights").Invoke(__instance, null);
            }
            catch (Exception ex)
            {
                Logger.Log($"[OrderOfBattleVMInitializePatch] Failed to call RefreshWeights: {ex}");
            }
            __instance.OnUnitDeployed();

            Logger.Log("[OrderOfBattleVMInitializePatch] Postfix completed successfully.");
        }

        private static FormationClass MapToNativeClass(DeploymentFormationClass dfc)
        {
            switch (dfc)
            {
                case DeploymentFormationClass.Infantry:
                    return FormationClass.Infantry;
                case DeploymentFormationClass.Ranged:
                    return FormationClass.Ranged;
                case DeploymentFormationClass.Cavalry:
                    return FormationClass.Cavalry;
                case DeploymentFormationClass.HorseArcher:
                    return FormationClass.HorseArcher;
                default:
                    return FormationClass.Unset;
            }
        }
    }

    /// <summary>
    /// Shared helper to distribute OOB card weights based on troop counts in each formation.
    /// </summary>
    internal static class WeightDistributor
    {
        public static void DistributeWeights(OrderOfBattleVM VM)
        {
            DeploymentFormationClass[] cardClasses = new DeploymentFormationClass[8];
            for (int i = 0; i < 8; i++)
            {
                cardClasses[i] = RefreshFormationPatch.GetCustomAssignmentClass(i);
            }

            int[] classCounts = new int[8];

            var mainParty = MobileParty.MainParty;
            if (mainParty?.MemberRoster != null)
            {
                for (int i = 0; i < mainParty.MemberRoster.Count; i++)
                {
                    var element = mainParty.MemberRoster.GetElementCopyAtIndex(i);
                    if (element.Character == null) continue;
                    if (element.Number <= element.WoundedNumber) continue;

                    int assignedIndex = FormationAssignmentStore.GetAssignment(element.Character.StringId);
                    if (assignedIndex >= 0 && assignedIndex <= 7)
                    {
                        int count = element.Number - element.WoundedNumber;
                        classCounts[assignedIndex] += count;
                    }
                }
            }

            var mainHero = Hero.MainHero;
            if (mainHero != null)
            {
                int assignedIndex = FormationAssignmentStore.GetAssignment(mainHero.CharacterObject.StringId);
                if (assignedIndex >= 0 && assignedIndex <= 7)
                {
                    classCounts[assignedIndex] += 1;
                }
                else
                {
                    classCounts[2] += 1; // Default main hero to Cavalry slot
                }
            }

            int[] totalByClass = new int[7]; // DeploymentFormationClass has values 0 to 6
            for (int i = 0; i < 8; i++)
            {
                int classVal = (int)cardClasses[i];
                if (classVal >= 0 && classVal < 7)
                {
                    totalByClass[classVal] += classCounts[i];
                }
            }

            var formationsList = VM.FormationsFirstHalf.Concat(VM.FormationsSecondHalf).ToList();

            foreach (var item in formationsList)
            {
                if (item.Formation == null) continue;
                int idx = item.Formation.Index;
                var cardClass = cardClasses[idx];

                Logger.Log($"[WeightDistributor] Inspecting formation {idx} (CardClass={cardClass}, classCounts={classCounts[idx]})");

                for (int cIdx = 0; cIdx < item.Classes.Count; cIdx++)
                {
                    var classVM = item.Classes[cIdx];
                    Logger.Log($"[WeightDistributor]   - classVM[{cIdx}]: Class={classVM.Class}, IsUnset={classVM.IsUnset}, Weight={classVM.Weight}");

                    if (classVM.IsUnset) continue;

                    if (RefreshFormationPatch.MapToDeploymentClass(classVM.Class) == cardClass)
                    {
                        int total = totalByClass[(int)cardClass];
                        int targetWeight = 0;
                        if (total > 0)
                        {
                            targetWeight = (int)Math.Round((double)classCounts[idx] / total * 100);
                        }
                        classVM.Weight = targetWeight;
                        Logger.Log($"[WeightDistributor] Set formation {idx} class {classVM.Class} weight to {targetWeight}%");
                    }
                }
            }
        }
    }
}
