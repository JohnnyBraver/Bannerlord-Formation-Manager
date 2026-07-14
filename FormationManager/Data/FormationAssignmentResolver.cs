using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TroopClassifier;

namespace FormationManager.Data
{
    /// <summary>
    /// Resolves a troop's persistent explicit assignment, role default, or native
    /// formation default in that order. Explicit assignments may contain two slots
    /// so one roster stack can be split evenly before the player fine-tunes it in OOB.
    /// </summary>
    internal static class FormationAssignmentResolver
    {
        public static bool HasCustomDefaults(Settings? settings)
            => FormationAssignmentStore.HasAnyAssignments || HasConfiguredDefaultFormations(settings);

        /// <summary>
        /// Returns only formations explicitly active in the troop's plan. For a
        /// custom advanced plan this deliberately excludes its role/default
        /// fallback, which is used only when an individual reinforcement arrives
        /// without the OOB stack context.
        /// </summary>
        public static int[] GetActivePlanFormationIndices(BasicCharacterObject character, Settings? settings)
        {
            if (character == null)
                return Array.Empty<int>();

            if (AdvancedPlansEnabled(settings) && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var deploymentPlan))
            {
                if (deploymentPlan!.Mode == TroopDeploymentPlanMode.Even)
                    return deploymentPlan.FormationIndices.ToArray();

                return deploymentPlan.FormationTargets.Keys
                    .Concat(deploymentPlan.FormationWeights.Keys)
                    .Where(FormationPlanNormalizer.IsValidFormationIndex)
                    .Distinct()
                    .OrderBy(index => index)
                    .ToArray();
            }

            int[] explicitAssignments = FormationAssignmentStore.GetAssignments(character.StringId);
            if (explicitAssignments.Length > 0)
                return explicitAssignments;

            return Array.Empty<int>();
        }

        /// <summary>
        /// Returns the actual managed slots for card classification. Unlike an
        /// advanced custom plan's fallback, configured role/class defaults are a
        /// direct placement and are therefore included when no plan is active.
        /// </summary>
        public static int[] GetEffectiveFormationIndices(BasicCharacterObject character, Settings? settings)
        {
            int[] activeIndices = GetActivePlanFormationIndices(character, settings);
            return activeIndices.Length > 0 ? activeIndices : new[] { GetDefaultFormationIndex(character, settings) };
        }

        /// <summary>Compatibility alias for callers that need effective slots.</summary>
        public static int[] GetFormationIndices(BasicCharacterObject character, Settings? settings)
            => GetEffectiveFormationIndices(character, settings);

        public static int ResolveFormationIndex(Agent? agent, BasicCharacterObject character, Settings? settings)
        {
            if (TryGetEvenSplitTargets(character, out int[] evenTargets, settings))
            {
                if (agent == null)
                    return evenTargets[0];

                int evenAgentIndex = agent.Index == int.MinValue ? 0 : Math.Abs(agent.Index);
                return evenTargets[evenAgentIndex % evenTargets.Length];
            }

            if (AdvancedPlansEnabled(settings) && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var customPlan) &&
                customPlan!.Mode == TroopDeploymentPlanMode.Custom)
            {
                // The OOB plan applies exact custom counts once the deployment agents
                // exist. Reinforcements without that roster context use the role default.
                return GetDefaultFormationIndex(agent, character, settings);
            }

            int[] formationIndices = GetActivePlanFormationIndices(character, settings);
            if (formationIndices.Length == 0)
                return GetDefaultFormationIndex(agent, character, settings);

            if (formationIndices.Length == 1 || agent == null)
                return formationIndices[0];

            int agentIndex = agent.Index;
            if (agentIndex == int.MinValue)
                agentIndex = 0;

            return formationIndices[Math.Abs(agentIndex) % formationIndices.Length];
        }

        public static int GetDefaultFormationIndex(BasicCharacterObject character, Settings? settings)
            => GetDefaultFormationIndex(null, character, settings);

        public static int GetDefaultFormationIndex(Agent? agent, BasicCharacterObject character, Settings? settings)
        {
            if (character == null)
                return 0;

            if (TryGetConfiguredDefaultFormationIndex(agent, character, settings, out int formationIndex))
                return formationIndex;

            return GetVanillaFormationIndex(character.DefaultFormationClass);
        }

        public static int GetAssignedCountForFormation(BasicCharacterObject character, int troopCount, int formationIndex, Settings? settings)
        {
            return GetAllocatedFormationCounts(character, troopCount, settings)
                .TryGetValue(formationIndex, out int assignedCount)
                ? assignedCount
                : 0;
        }

        public static bool TryGetEvenSplitTargets(BasicCharacterObject character, out int[] formationIndices, Settings? settings = null)
        {
            formationIndices = Array.Empty<int>();
            if (character == null)
                return false;

            if (AdvancedPlansEnabled(settings) && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var deploymentPlan) &&
                deploymentPlan!.Mode == TroopDeploymentPlanMode.Even)
            {
                formationIndices = deploymentPlan.FormationIndices.ToArray();
                return formationIndices.Length >= 2;
            }

            int[] legacyAssignments = FormationAssignmentStore.GetAssignments(character.StringId);
            if (legacyAssignments.Length < 2)
                return false;

            formationIndices = legacyAssignments;
            return true;
        }

        /// <summary>
        /// Exposes the target-filled portion of a custom plan to the OOB planner so
        /// it can balance weighted rounding across equivalent odd-sized stacks.
        /// </summary>
        public static bool TryGetWeightedCustomSurplus(
            BasicCharacterObject character,
            int troopCount,
            out Dictionary<int, int> targets,
            out Dictionary<int, int> weights,
            out int surplus,
            Settings? settings = null)
        {
            targets = new Dictionary<int, int>();
            weights = new Dictionary<int, int>();
            surplus = 0;
            if (character == null || !AdvancedPlansEnabled(settings) || !FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var plan) ||
                plan!.Mode != TroopDeploymentPlanMode.Custom)
                return false;

            foreach (var pair in plan.FormationTargets.Where(pair => pair.Key >= 0 && pair.Key <= 7 && pair.Value > 0))
                targets[pair.Key] = pair.Value;
            foreach (var pair in plan.FormationWeights.Where(pair => pair.Key >= 0 && pair.Key <= 7 && pair.Value > 0))
                weights[pair.Key] = pair.Value;

            int targetTotal = targets.Values.Sum();
            surplus = troopCount - targetTotal;
            return surplus > 0 && weights.Count > 0;
        }

        public static Dictionary<int, int> GetAllocatedFormationCounts(
            BasicCharacterObject character,
            int troopCount,
            Settings? settings,
            IFormationRemainderStrategy? remainderStrategy = null)
        {
            var allocation = new Dictionary<int, int>();
            if (character == null || troopCount <= 0)
                return allocation;

            if (TryGetEvenSplitTargets(character, out int[] evenTargets, settings))
            {
                return FormationPlanAllocator.AllocateEvenly(evenTargets, troopCount, remainderStrategy);
            }

            if (AdvancedPlansEnabled(settings) && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var deploymentPlan) &&
                deploymentPlan!.Mode == TroopDeploymentPlanMode.Custom)
            {
                return FormationPlanAllocator.AllocateCustom(
                    deploymentPlan,
                    troopCount,
                    GetDefaultFormationIndex(character, settings),
                    settings?.PrioritizeWeightsInSmallStacks ?? false,
                    remainderStrategy);
            }

            int[] explicitAssignments = FormationAssignmentStore.GetAssignments(character.StringId);
            if (explicitAssignments.Length == 1)
            {
                allocation[explicitAssignments[0]] = troopCount;
                return allocation;
            }

            allocation[GetDefaultFormationIndex(character, settings)] = troopCount;
            return allocation;
        }

        public static Dictionary<int, int> GetAllocatedCustomPlanCounts(
            BasicCharacterObject character,
            int troopCount,
            IReadOnlyDictionary<int, int> targets,
            IReadOnlyDictionary<int, int> weights,
            Settings? settings)
        {
            var allocation = new Dictionary<int, int>();
            if (character == null || troopCount <= 0)
                return allocation;

            var plan = TroopDeploymentPlan.CreateCustom();
            foreach (var pair in targets)
                plan.FormationTargets[pair.Key] = pair.Value;
            foreach (var pair in weights)
                plan.FormationWeights[pair.Key] = pair.Value;
            return FormationPlanAllocator.AllocateCustom(
                plan,
                troopCount,
                GetDefaultFormationIndex(character, settings),
                settings?.PrioritizeWeightsInSmallStacks ?? false);
        }

        private static bool AdvancedPlansEnabled(Settings? settings)
            => settings?.ShowAdvancedFormationEditor ?? Settings.Instance?.ShowAdvancedFormationEditor ?? true;

        private static bool HasConfiguredDefaultFormations(Settings? settings)
        {
            if (settings == null)
                return false;

            if (settings.UsePartyManagerRoleDefaults)
            {
                return IsValidConfiguredFormation(settings.LightInfantryFormation) ||
                       IsValidConfiguredFormation(settings.ShieldInfantryFormation) ||
                       IsValidConfiguredFormation(settings.ShockInfantryFormation) ||
                       IsValidConfiguredFormation(settings.PikeInfantryFormation) ||
                       IsValidConfiguredFormation(settings.SkirmisherFormation) ||
                       IsValidConfiguredFormation(settings.FootArcherFormation) ||
                       IsValidConfiguredFormation(settings.CrossbowmanFormation) ||
                       IsValidConfiguredFormation(settings.MeleeCavalryRoleFormation) ||
                       IsValidConfiguredFormation(settings.HorseArcherRoleFormation);
            }

            return IsValidConfiguredFormation(settings.InfantryFormation) ||
                   IsValidConfiguredFormation(settings.ArcherFormation) ||
                   IsValidConfiguredFormation(settings.CavalryFormation);
        }

        internal static bool UsesAgentRoleDefaults(BasicCharacterObject character, Settings? settings)
        {
            if (character == null || settings?.UsePartyManagerRoleDefaults != true || settings.UseSpawnedEquipmentClassification != true)
                return false;

            return GetActivePlanFormationIndices(character, settings).Length == 0;
        }

        private static bool TryGetConfiguredDefaultFormationIndex(Agent? agent, BasicCharacterObject character, Settings? settings, out int formationIndex)
        {
            formationIndex = 0;
            if (settings == null)
                return false;

            int configuredFormation = settings.UsePartyManagerRoleDefaults
                ? GetRoleFormation(settings.UseSpawnedEquipmentClassification && agent != null
                    ? TroopRoleClassifier.Classify(agent)
                    : TroopRoleClassifier.Classify(character), settings)
                : GetNativeClassFormation(character.DefaultFormationClass, settings);
            if (!IsValidConfiguredFormation(configuredFormation))
                return false;

            formationIndex = configuredFormation - 1;
            return true;
        }

        private static bool IsValidConfiguredFormation(int configuredFormation)
            => configuredFormation >= 1 && configuredFormation <= 8;

        private static int GetVanillaFormationIndex(FormationClass formationClass)
        {
            switch (formationClass)
            {
                case FormationClass.Ranged:
                    return 1;
                case FormationClass.Cavalry:
                case FormationClass.LightCavalry:
                case FormationClass.HeavyCavalry:
                    return 2;
                case FormationClass.HorseArcher:
                    return 3;
                default:
                    return 0;
            }
        }

        private static int GetRoleFormation(TroopRole role, Settings settings)
        {
            switch (role)
            {
                case TroopRole.LightInfantry:
                    return settings.LightInfantryFormation;
                case TroopRole.ShieldInfantry:
                    return settings.ShieldInfantryFormation;
                case TroopRole.ShockInfantry:
                    return settings.ShockInfantryFormation;
                case TroopRole.Skirmisher:
                    return settings.SkirmisherFormation;
                case TroopRole.FootArcher:
                    return settings.FootArcherFormation;
                case TroopRole.Crossbowman:
                    return settings.CrossbowmanFormation;
                case TroopRole.MeleeCavalry:
                    return settings.MeleeCavalryRoleFormation;
                case TroopRole.HorseArcher:
                    return settings.HorseArcherRoleFormation;
                case TroopRole.PikeInfantry:
                    return settings.PikeInfantryFormation;
                default:
                    return settings.InfantryFormation;
            }
        }

        private static int GetNativeClassFormation(FormationClass formationClass, Settings? settings)
        {
            switch (formationClass)
            {
                case FormationClass.Ranged:
                    return settings?.ArcherFormation ?? 0;
                case FormationClass.HorseArcher:
                    return settings?.CavalryFormation ?? 0;
                case FormationClass.Cavalry:
                case FormationClass.LightCavalry:
                case FormationClass.HeavyCavalry:
                    return settings?.CavalryFormation ?? 0;
                default:
                    return settings?.InfantryFormation ?? 0;
            }
        }

        public static int ToFormationIndex(int configuredFormation)
        {
            if (configuredFormation < 1)
                return 0;

            if (configuredFormation > 8)
                return 7;

            return configuredFormation - 1;
        }
    }
}
