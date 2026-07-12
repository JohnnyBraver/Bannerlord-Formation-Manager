using System;
using System.Collections.Generic;
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

            if (!MissionGuards.IsCurrentMissionSupported())
                return;

            // The role and troop assignments create the initial OOB layout only.
            // Once the screen is visible, the player's type selector and weight
            // sliders must be allowed to call the unmodified native flow.
            if (!OrderOfBattleDefaultSession.IsApplyingDefaults)
                return;

            if (formation == null)
                return;

            // The initialization patch uses Bannerlord's dedicated combined card
            // values for Infantry+Ranged and Cavalry+HorseArcher. Do not replace
            // those explicit requests with the first matching roster class.
            if (overriddenClass == DeploymentFormationClass.InfantryAndRanged ||
                overriddenClass == DeploymentFormationClass.CavalryAndHorseArcher)
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

            if (!MissionGuards.IsCurrentMissionSupported())
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
            if (!FormationAssignmentResolver.HasCustomDefaults(Settings.Instance))
                return false;

            var mainParty = MobileParty.MainParty;
            if (mainParty?.MemberRoster == null)
                return false;

            for (int i = 0; i < mainParty.MemberRoster.Count; i++)
            {
                var element = mainParty.MemberRoster.GetElementCopyAtIndex(i);
                if (element.Character == null)
                    continue;
                if (element.Character == Hero.MainHero?.CharacterObject)
                    continue;

                int readyCount = element.Number - element.WoundedNumber;
                if (FormationAssignmentResolver.GetAssignedCountForFormation(
                    element.Character,
                    readyCount,
                    formationIndex,
                    Settings.Instance) > 0)
                    return true;
            }

            return false;
        }

        public static DeploymentFormationClass GetCustomAssignmentClass(int formationIndex)
        {
            if (!FormationAssignmentResolver.HasCustomDefaults(Settings.Instance))
                return GetVanillaDefaultClass(formationIndex);

            var mainParty = MobileParty.MainParty;
            bool hasCustomTroops = false;

            if (mainParty?.MemberRoster != null)
            {
                for (int i = 0; i < mainParty.MemberRoster.Count; i++)
                {
                    var element = mainParty.MemberRoster.GetElementCopyAtIndex(i);
                    if (element.Character == null) continue;
                    if (element.Character == Hero.MainHero?.CharacterObject) continue;
                    if (element.Number <= element.WoundedNumber) continue;

                    int[] assignedIndices = FormationAssignmentResolver.GetFormationIndices(element.Character, Settings.Instance);
                    if (assignedIndices.Contains(formationIndex))
                    {
                        var deploymentClass = MapToDeploymentClass(element.Character);
                        Logger.Log($"[RefreshFormationPatch] Formation {formationIndex + 1} card class resolved from {element.Character.StringId}: {deploymentClass}");
                        return deploymentClass;
                    }
                    if (assignedIndices.Length > 0)
                    {
                        hasCustomTroops = true;
                    }
                }
            }

            var mainHero = Hero.MainHero;
            if (mainHero != null)
            {
                // Role defaults are deliberately for regular troop stacks. The main
                // hero keeps the game's normal OOB handling unless explicitly saved.
                int[] assignedIndices = FormationAssignmentStore.GetAssignments(mainHero.CharacterObject.StringId);
                if (assignedIndices.Contains(formationIndex))
                {
                    return MapToDeploymentClass(mainHero.CharacterObject.DefaultFormationClass);
                }
                if (assignedIndices.Length > 0)
                {
                    hasCustomTroops = true;
                }
            }

            // If the player has configured role or troop defaults, any unused slot
            // is unset until the player creates it through the native OOB controls.
            if (hasCustomTroops)
            {
                return DeploymentFormationClass.Unset;
            }

            return GetVanillaDefaultClass(formationIndex);
        }

        /// <summary>
        /// Returns the native two-class card layout for the agents already placed
        /// in a formation. Bannerlord supports foot (infantry+ranged) and mounted
        /// (cavalry+horse archer) mixed cards; cross-family mixes retain the legacy
        /// single-class fallback until their semantics are designed separately.
        /// </summary>
        public static DeploymentFormationClass[] GetCompatibleFormationClasses(int formationIndex, Team team)
        {
            var assignedClasses = team.ActiveAgents
                .Where(agent => agent != null && agent.IsHuman && !agent.IsMount && !agent.IsMainAgent &&
                                agent.Formation != null && agent.Formation.Index == formationIndex && agent.Character != null)
                .Select(agent => MapToDeploymentClass(agent.Character))
                .Where(deploymentClass => deploymentClass != DeploymentFormationClass.Unset)
                .Distinct()
                .ToHashSet();

            if (assignedClasses.Count == 0)
                return new[] { GetCustomAssignmentClass(formationIndex) };

            if (assignedClasses.All(deploymentClass => deploymentClass == DeploymentFormationClass.Infantry || deploymentClass == DeploymentFormationClass.Ranged))
            {
                return new[] { DeploymentFormationClass.Infantry, DeploymentFormationClass.Ranged }
                    .Where(assignedClasses.Contains)
                    .ToArray();
            }

            if (assignedClasses.All(deploymentClass => deploymentClass == DeploymentFormationClass.Cavalry || deploymentClass == DeploymentFormationClass.HorseArcher))
            {
                return new[] { DeploymentFormationClass.Cavalry, DeploymentFormationClass.HorseArcher }
                    .Where(assignedClasses.Contains)
                    .ToArray();
            }

            var fallback = GetCustomAssignmentClass(formationIndex);
            Logger.Log($"[RefreshFormationPatch] Formation {formationIndex + 1} has an unsupported cross-family class mix; retaining single-class fallback {fallback}.");
            return new[] { fallback };
        }

        private static DeploymentFormationClass GetVanillaDefaultClass(int formationIndex)
        {
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

        public static DeploymentFormationClass MapToDeploymentClass(BasicCharacterObject character)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.UsePartyManagerRoleDefaults)
                return MapToDeploymentClass(character.DefaultFormationClass);

            switch (PartyManagerRoleClassifier.Classify(character))
            {
                case PartyManagerRole.FootArcher:
                case PartyManagerRole.Crossbowman:
                    return DeploymentFormationClass.Ranged;
                case PartyManagerRole.MeleeCavalry:
                    return DeploymentFormationClass.Cavalry;
                case PartyManagerRole.HorseArcher:
                    return DeploymentFormationClass.HorseArcher;
                default:
                    return DeploymentFormationClass.Infantry;
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

            if (!MissionGuards.IsCurrentMissionSupported())
                return;

            // WeightDistributor runs later from OrderOfBattleVM.Initialize, after
            // the exact agent plan has been prepared. Doing it here lets native OOB
            // redistribute troops by class before we can restore per-stack splits.
            Logger.Log("[SetInitialHeroFormationsPatch] Deferring card weights until exact OOB placement is complete.");
        }
    }

    /// <summary>
    /// Tracks the short initial-layout phase of an OOB screen. The same native UI
    /// remains completely editable after this phase finishes.
    /// </summary>
    internal static class OrderOfBattleDefaultSession
    {
        private static Mission? _mission;
        private static bool _isApplyingDefaults;

        public static bool IsApplyingDefaults => _isApplyingDefaults && _mission == Mission.Current;

        public static void Begin(Mission mission)
        {
            _mission = mission;
            _isApplyingDefaults = mission != null;
        }

        public static void Complete()
        {
            _isApplyingDefaults = false;
        }
    }

    /// <summary>
    /// Creates the initial role/explicit-assignment layout before handing all class
    /// and weight controls back to the native OOB screen.
    /// </summary>
    [HarmonyPatch(typeof(OrderOfBattleVM), "Initialize")]
    internal static class OrderOfBattleVMInitializePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Mission __0)
        {
            OrderOfBattleDefaultSession.Begin(__0);
        }

        [HarmonyPostfix]
        private static void Postfix(OrderOfBattleVM __instance)
        {
            try
            {
                var settings = Settings.Instance;
                if (settings == null || !settings.ModEnabled || !MissionGuards.IsCurrentMissionSupported())
                    return;

                if (!FormationAssignmentResolver.HasCustomDefaults(settings))
                    return;

                Logger.Log("[OrderOfBattleVMInitializePatch] Applying role and troop assignment defaults to OOB...");

                var mission = Mission.Current;
                var team = mission?.PlayerTeam;
                if (team == null)
                {
                    Logger.Log("[OrderOfBattleVMInitializePatch] PlayerTeam is null!");
                    return;
                }

                var assignmentPlan = OobDefaultAssignmentPlan.ForActiveAgents(team, settings);

                // 1. Establish default preview positions. These are only a starting
                // point: selecting classes or changing weights afterwards uses the
                // game's normal OOB controls and is never reapplied by this patch.
                ApplyExactPreviewAssignments(team, assignmentPlan, settings, "initial plan");

                // 2. Seed only the initial card types. OrderOfBattleDefaultSession
                // remains active while RefreshFormation runs, then is completed in
                // finally so later player interaction is unmodified.
                var formationsList = __instance.FormationsFirstHalf.Concat(__instance.FormationsSecondHalf).ToList();
                // RefreshFormation can immediately redistribute native-class pools.
                // Snapshot every intended card type first so an earlier card cannot
                // make a later mixed card appear to contain only its remaining type.
                var intendedCardClasses = formationsList
                    .Where(item => item.Formation != null)
                    .ToDictionary(
                        item => item.Formation.Index,
                        item => RefreshFormationPatch.GetCompatibleFormationClasses(item.Formation.Index, team)
                            .Where(deploymentClass => deploymentClass != DeploymentFormationClass.Unset)
                            .ToArray());
                foreach (var item in formationsList)
                {
                    if (item.Formation == null) continue;
                    int idx = item.Formation.Index;

                    var targetClasses = intendedCardClasses[idx];
                    if (targetClasses.Length > 0)
                    {
                        item.RefreshFormation(item.Formation, ToNativeCardClass(targetClasses), true);
                        if (item.Classes != null && item.Classes.Count > 0)
                        {
                            // The mixed card types must be created through
                            // RefreshFormation's dedicated enum values. Directly
                            // setting two class slots is not enough for native OOB
                            // to recognise Infantry+Ranged or Cavalry+HorseArcher.
                            if (targetClasses.Length == 1)
                            {
                                item.Classes[0].Class = MapToNativeClass(targetClasses[0]);
                                if (item.Classes.Count > 1)
                                    item.Classes[1].Class = FormationClass.Unset;
                            }
                        }
                    }
                }

                // 3. Show the default split in the card counts and weights.
                WeightDistributor.DistributeWeights(__instance);

                foreach (var item in formationsList)
                    item.OnSizeChanged();

                try
                {
                    AccessTools.Method(typeof(OrderOfBattleVM), "RefreshWeights").Invoke(__instance, null);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[OrderOfBattleVMInitializePatch] Failed to call RefreshWeights: {ex}");
                }
                __instance.OnUnitDeployed();

                // Native OOB applies its class-pool weights in OnUnitDeployed.
                // Reapply the plan afterwards so the selected agents—not merely
                // an equivalent number of their native class—remain in each slot.
                ApplyExactPreviewAssignments(team, assignmentPlan, settings, "after native class-pool distribution");
                foreach (var item in formationsList)
                    item.OnSizeChanged();

                Logger.Log("[OrderOfBattleVMInitializePatch] Defaults applied; native OOB controls are now unlocked.");
            }
            finally
            {
                OrderOfBattleDefaultSession.Complete();
            }
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            // A fault in native OOB initialization must not leave the default-only
            // guard enabled for a later screen refresh in the same mission.
            OrderOfBattleDefaultSession.Complete();
            return __exception;
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

        private static DeploymentFormationClass ToNativeCardClass(DeploymentFormationClass[] classes)
        {
            bool hasInfantry = classes.Contains(DeploymentFormationClass.Infantry);
            bool hasRanged = classes.Contains(DeploymentFormationClass.Ranged);
            if (hasInfantry && hasRanged)
                return DeploymentFormationClass.InfantryAndRanged;

            bool hasCavalry = classes.Contains(DeploymentFormationClass.Cavalry);
            bool hasHorseArcher = classes.Contains(DeploymentFormationClass.HorseArcher);
            if (hasCavalry && hasHorseArcher)
                return DeploymentFormationClass.CavalryAndHorseArcher;

            return classes[0];
        }

        private static void ApplyExactPreviewAssignments(Team team, OobDefaultAssignmentPlan assignmentPlan, Settings settings, string stage)
        {
            foreach (var agent in team.ActiveAgents)
            {
                var character = agent.Character;
                if (character == null || agent.IsMount || agent.IsMainAgent)
                    continue;

                int assignedIndex = assignmentPlan.GetFormationIndex(agent, character, settings);
                if (assignedIndex < 0 || assignedIndex > 7)
                    continue;

                var targetFormation = team.GetFormation((FormationClass)assignedIndex);
                if (targetFormation == null || targetFormation.Team != team || agent.Formation == targetFormation)
                    continue;

                try
                {
                    agent.Formation = targetFormation;
                    Logger.Log($"[OrderOfBattleVMInitializePatch] Applied {stage}: {character.StringId} -> formation {assignedIndex + 1} (Name: {character.Name})");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[OrderOfBattleVMInitializePatch] Could not apply {stage} for {character.StringId}: {ex.GetType().Name}: {ex.Message}");
                }
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
            var settings = Settings.Instance;
            if (!FormationAssignmentResolver.HasCustomDefaults(settings))
                return;

            var assignmentPlan = OobDefaultAssignmentPlan.ForPlayerRoster(settings);
            int[,] classCounts = new int[8, 7]; // formation index, DeploymentFormationClass
            for (int formationIndex = 0; formationIndex < 8; formationIndex++)
            {
                for (int classIndex = 0; classIndex < 7; classIndex++)
                    classCounts[formationIndex, classIndex] = assignmentPlan.GetFormationClassCount(
                        formationIndex,
                        (DeploymentFormationClass)classIndex);
            }

            var mainHero = Hero.MainHero;
            if (mainHero != null)
            {
                int[] assignedIndices = FormationAssignmentStore.GetAssignments(mainHero.CharacterObject.StringId);
                DeploymentFormationClass heroClass = RefreshFormationPatch.MapToDeploymentClass(mainHero.CharacterObject.DefaultFormationClass);
                if (assignedIndices.Length > 0)
                {
                    for (int formationIndex = 0; formationIndex < 8; formationIndex++)
                    {
                        int assignedCount = FormationAssignmentResolver.GetAssignedCountForFormation(
                            mainHero.CharacterObject,
                            1,
                            formationIndex,
                            settings);
                        if ((int)heroClass >= 0 && (int)heroClass < 7)
                            classCounts[formationIndex, (int)heroClass] += assignedCount;
                    }
                }
                else
                {
                    classCounts[2, (int)DeploymentFormationClass.Cavalry] += 1; // Default main hero to Cavalry slot
                }
            }

            int[] totalByClass = new int[7]; // DeploymentFormationClass has values 0 to 6
            for (int formationIndex = 0; formationIndex < 8; formationIndex++)
            {
                for (int classIndex = 0; classIndex < 7; classIndex++)
                    totalByClass[classIndex] += classCounts[formationIndex, classIndex];
            }

            var formationsList = VM.FormationsFirstHalf.Concat(VM.FormationsSecondHalf).ToList();

            foreach (var item in formationsList)
            {
                if (item.Formation == null) continue;
                int idx = item.Formation.Index;
                Logger.Log($"[WeightDistributor] Inspecting formation {idx}.");

                for (int cIdx = 0; cIdx < item.Classes.Count; cIdx++)
                {
                    var classVM = item.Classes[cIdx];
                    Logger.Log($"[WeightDistributor]   - classVM[{cIdx}]: Class={classVM.Class}, IsUnset={classVM.IsUnset}, Weight={classVM.Weight}");

                    if (classVM.IsUnset) continue;

                    var deploymentClass = RefreshFormationPatch.MapToDeploymentClass(classVM.Class);
                    int classIndex = (int)deploymentClass;
                    if (classIndex >= 0 && classIndex < 7)
                    {
                        int total = totalByClass[classIndex];
                        int targetWeight = 0;
                        if (total > 0)
                        {
                            targetWeight = (int)Math.Round((double)classCounts[idx, classIndex] / total * 100);
                        }
                        classVM.Weight = targetWeight;

                        // This is Bannerlord's native slider lock: it keeps the
                        // class card and its other OOB controls usable while
                        // preventing an accidental weight-slider drag from
                        // undoing Formation Manager's initial distribution.
                        // Only lock a class that this mod actually seeded.
                        if (settings?.LockManagedOobSliders == true && classCounts[idx, classIndex] > 0)
                            classVM.SetWeightAdjustmentLock(true);

                        Logger.Log($"[WeightDistributor] Set formation {idx} class {classVM.Class} weight to {targetWeight}%");
                    }
                }
            }
        }
    }
}
