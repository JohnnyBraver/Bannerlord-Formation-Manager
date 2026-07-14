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

            // The native deployment pass can recreate an empty default card. The
            // post-deployment cleanup deliberately sends Unset through unchanged.
            if (OrderOfBattleDefaultSession.IsClearingEmptyCards)
                return;

            if (formation == null)
                return;

            // The initialization patch uses Bannerlord's dedicated combined card
            // values for Infantry+Ranged and Cavalry+HorseArcher. Do not replace
            // an explicit request—single-class included—with the first matching
            // roster class. The layout snapshot has already inspected the actual
            // assigned OOB agents, which is authoritative for modded troop types.
            if (overriddenClass != DeploymentFormationClass.Unset)
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
                if (!TroopControlScope.IsEligibleTroop(element.Character))
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
                    if (!TroopControlScope.IsEligibleTroop(element.Character)) continue;
                    if (element.Number <= element.WoundedNumber) continue;

                    int[] assignedIndices = FormationAssignmentResolver.GetEffectiveFormationIndices(element.Character, Settings.Instance);
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
            var settings = Settings.Instance;
            var assignedClasses = team.ActiveAgents
                .Where(agent => settings != null && TroopControlScope.ShouldManage(agent, settings) &&
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
        private static bool _isClearingEmptyCards;

        public static bool IsApplyingDefaults => _isApplyingDefaults && _mission == Mission.Current;
        public static bool IsClearingEmptyCards => IsApplyingDefaults && _isClearingEmptyCards;

        public static void Begin(Mission mission)
        {
            _mission = mission;
            _isApplyingDefaults = mission != null;
        }

        public static void Complete()
        {
            _isApplyingDefaults = false;
            _isClearingEmptyCards = false;
        }

        public static void ClearEmptyCards(Action clear)
        {
            _isClearingEmptyCards = true;
            try
            {
                clear();
            }
            finally
            {
                _isClearingEmptyCards = false;
            }
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

                var layout = OobFormationLayout.ForActiveAgents(team, settings);

                // 1. Establish default preview positions. These are only a starting
                // point: selecting classes or changing weights afterwards uses the
                // game's normal OOB controls and is never reapplied by this patch.
                OobPreviewAssignmentApplier.Apply(team, layout, settings, "initial plan");

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
                    else
                    {
                        // A previously selected native card must not remain active
                        // when the configured Formation Manager plan leaves this
                        // slot empty.
                        item.RefreshFormation(item.Formation, DeploymentFormationClass.Unset, false);
                    }
                }

                // 3. Show the default split in the card counts and weights.
                OobWeightDistributor.DistributeWeights(__instance, layout);

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
                OobPreviewAssignmentApplier.Apply(team, layout, settings, "after native class-pool distribution");

                // OnUnitDeployed may recreate a vanilla default card even when no
                // managed agent belongs in that formation. Clear those cards after
                // the final exact placement, bypassing our normal default seeding.
                OrderOfBattleDefaultSession.ClearEmptyCards(() =>
                {
                    foreach (var item in formationsList)
                    {
                        if (item.Formation == null || layout.GetFormationCount(item.Formation.Index) != 0)
                            continue;

                        item.RefreshFormation(item.Formation, DeploymentFormationClass.Unset, false);
                        Logger.Log($"[OrderOfBattleVMInitializePatch] Cleared empty formation {item.Formation.Index + 1} after native deployment.");
                    }
                });
                foreach (var item in formationsList)
                    item.OnSizeChanged();
                OobWeightDistributor.LockManagedSliders(__instance, layout);

                string sliderState = settings.LockManagedOobSliders && FormationAssignmentResolver.HasCustomDefaults(settings)
                    ? "managed native OOB sliders are locked"
                    : "native OOB sliders remain available";
                Logger.Log($"[OrderOfBattleVMInitializePatch] Defaults applied; {sliderState}.");
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

    }

}
