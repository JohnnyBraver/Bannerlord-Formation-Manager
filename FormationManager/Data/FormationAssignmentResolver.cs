using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

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
            => (settings?.UsePartyManagerRoleDefaults ?? false) || FormationAssignmentStore.HasAnyAssignments;

        public static int[] GetFormationIndices(BasicCharacterObject character, Settings? settings)
        {
            if (character == null)
                return Array.Empty<int>();

            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var deploymentPlan))
            {
                if (deploymentPlan!.Mode == TroopDeploymentPlanMode.Even)
                    return deploymentPlan.FormationIndices.ToArray();

                int fallbackIndex = GetDefaultFormationIndex(character, settings);
                return deploymentPlan.FormationTargets.Keys
                    .Concat(deploymentPlan.FormationWeights.Keys)
                    .Append(fallbackIndex)
                    .Distinct()
                    .OrderBy(index => index)
                    .ToArray();
            }

            int[] explicitAssignments = FormationAssignmentStore.GetAssignments(character.StringId);
            if (explicitAssignments.Length > 0)
                return explicitAssignments;

            return new[] { GetDefaultFormationIndex(character, settings) };
        }

        public static int ResolveFormationIndex(Agent? agent, BasicCharacterObject character, Settings? settings)
        {
            if (TryGetEvenSplitTargets(character, out int[] evenTargets))
            {
                if (agent == null)
                    return evenTargets[0];

                int evenAgentIndex = agent.Index == int.MinValue ? 0 : Math.Abs(agent.Index);
                return evenTargets[evenAgentIndex % evenTargets.Length];
            }

            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var customPlan) &&
                customPlan!.Mode == TroopDeploymentPlanMode.Custom)
            {
                // The OOB plan applies exact custom counts once the deployment agents
                // exist. Reinforcements without that roster context use the role default.
                return GetDefaultFormationIndex(character, settings);
            }

            int[] formationIndices = GetFormationIndices(character, settings);
            if (formationIndices.Length == 0)
                return -1;

            if (formationIndices.Length == 1 || agent == null)
                return formationIndices[0];

            int agentIndex = agent.Index;
            if (agentIndex == int.MinValue)
                agentIndex = 0;

            return formationIndices[Math.Abs(agentIndex) % formationIndices.Length];
        }

        public static int GetDefaultFormationIndex(BasicCharacterObject character, Settings? settings)
        {
            if (character == null)
                return 0;

            if (settings != null && settings.UsePartyManagerRoleDefaults)
                return ToFormationIndex(GetRoleFormation(PartyManagerRoleClassifier.Classify(character), settings));

            return ToFormationIndex(GetNativeClassFormation(character.DefaultFormationClass, settings));
        }

        public static int GetAssignedCountForFormation(BasicCharacterObject character, int troopCount, int formationIndex, Settings? settings)
        {
            return GetAllocatedFormationCounts(character, troopCount, settings)
                .TryGetValue(formationIndex, out int assignedCount)
                ? assignedCount
                : 0;
        }

        public static bool TryGetEvenSplitTargets(BasicCharacterObject character, out int[] formationIndices)
        {
            formationIndices = Array.Empty<int>();
            if (character == null)
                return false;

            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var deploymentPlan) &&
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
            out int surplus)
        {
            targets = new Dictionary<int, int>();
            weights = new Dictionary<int, int>();
            surplus = 0;
            if (character == null || !FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var plan) ||
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

        public static Dictionary<int, int> GetAllocatedFormationCounts(BasicCharacterObject character, int troopCount, Settings? settings)
        {
            var allocation = new Dictionary<int, int>();
            if (character == null || troopCount <= 0)
                return allocation;

            if (TryGetEvenSplitTargets(character, out int[] evenTargets))
            {
                AllocateEvenly(allocation, evenTargets, troopCount);
                return allocation;
            }

            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out var deploymentPlan) &&
                deploymentPlan!.Mode == TroopDeploymentPlanMode.Custom)
            {
                AllocateCustomPlan(allocation, deploymentPlan, troopCount, GetDefaultFormationIndex(character, settings), settings?.PrioritizeWeightsInSmallStacks ?? false);
                return allocation;
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

        private static void AllocateEvenly(Dictionary<int, int> allocation, int[] formationIndices, int troopCount)
        {
            int countPerFormation = troopCount / formationIndices.Length;
            int remainder = troopCount % formationIndices.Length;
            for (int i = 0; i < formationIndices.Length; i++)
                allocation[formationIndices[i]] = countPerFormation + (i < remainder ? 1 : 0);
        }

        private static void AllocateCustomPlan(Dictionary<int, int> allocation, TroopDeploymentPlan plan, int troopCount, int fallbackIndex, bool prioritizeWeightsInSmallStacks)
        {
            var targets = plan.FormationTargets.Where(pair => pair.Key >= 0 && pair.Key <= 7 && pair.Value > 0)
                .OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            var weights = plan.FormationWeights.Where(pair => pair.Key >= 0 && pair.Key <= 7 && pair.Value > 0)
                .OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            var activeIndices = targets.Keys.Concat(weights.Keys).Distinct().OrderBy(index => index).ToArray();
            if (activeIndices.Length == 0)
            {
                allocation[fallbackIndex] = troopCount;
                return;
            }

            int targetTotal = targets.Values.Sum();
            // A weight-only plan has no targets to make it "short" against, but
            // it is still a short stack when fewer troops are available than its
            // active formations. Apply the same default minimum rule here so
            // 50/1/1/1/1/1/1/1 with ten troops becomes 3/1/1/1/1/1/1/1.
            if (targetTotal == 0 && weights.Count > 0 && !prioritizeWeightsInSmallStacks)
            {
                int surplusAfterMinimums = troopCount;
                foreach (int index in activeIndices)
                {
                    if (surplusAfterMinimums == 0)
                        break;
                    AddCount(allocation, index, 1);
                    surplusAfterMinimums--;
                }
                AllocateProportionally(allocation, weights, surplusAfterMinimums);
                return;
            }

            if (troopCount >= targetTotal)
            {
                foreach (var pair in targets)
                    allocation[pair.Key] = pair.Value;

                int surplus = troopCount - targetTotal;
                if (weights.Count > 0)
                    AllocateProportionally(allocation, weights, surplus);
                else
                    AddCount(allocation, fallbackIndex, surplus);
                return;
            }

            if (prioritizeWeightsInSmallStacks)
            {
                AllocateProportionally(allocation, weights.Count > 0 ? weights : targets, troopCount);
                return;
            }

            int remaining = troopCount;
            foreach (int index in activeIndices)
            {
                if (remaining == 0)
                    break;
                AddCount(allocation, index, 1);
                remaining--;
            }

            if (remaining > 0)
                AllocateProportionally(allocation, weights.Count > 0 ? weights : targets, remaining);
        }

        private static void AllocateProportionally(Dictionary<int, int> allocation, IReadOnlyDictionary<int, int> values, int troopCount)
        {
            if (troopCount <= 0 || values.Count == 0)
                return;

            int total = values.Values.Sum(value => Math.Max(0, value));
            if (total <= 0)
                return;

            int allocated = 0;
            var remainders = new List<(int FormationIndex, int Remainder)>();
            foreach (var pair in values.OrderBy(pair => pair.Key))
            {
                int numerator = troopCount * pair.Value;
                int count = numerator / total;
                AddCount(allocation, pair.Key, count);
                allocated += count;
                remainders.Add((pair.Key, numerator % total));
            }

            foreach (var remainder in remainders.OrderByDescending(item => item.Remainder).ThenBy(item => item.FormationIndex))
            {
                if (allocated >= troopCount)
                    break;
                AddCount(allocation, remainder.FormationIndex, 1);
                allocated++;
            }
        }

        private static void AddCount(Dictionary<int, int> allocation, int formationIndex, int count)
        {
            if (count <= 0)
                return;

            allocation[formationIndex] = allocation.TryGetValue(formationIndex, out int existing) ? existing + count : count;
        }

        private static int GetRoleFormation(PartyManagerRole role, Settings settings)
        {
            switch (role)
            {
                case PartyManagerRole.FrontlineInfantry:
                    return settings.FrontlineInfantryFormation;
                case PartyManagerRole.ShockInfantry:
                    return settings.ShockInfantryFormation;
                case PartyManagerRole.Skirmisher:
                    return settings.SkirmisherFormation;
                case PartyManagerRole.FootArcher:
                    return settings.FootArcherFormation;
                case PartyManagerRole.Crossbowman:
                    return settings.CrossbowmanFormation;
                case PartyManagerRole.MeleeCavalry:
                    return settings.MeleeCavalryRoleFormation;
                case PartyManagerRole.HorseArcher:
                    return settings.HorseArcherRoleFormation;
                case PartyManagerRole.PikeInfantry:
                    return settings.PikeInfantryFormation;
                default:
                    return settings.InfantryFormation;
            }
        }

        private static int GetNativeClassFormation(FormationClass formationClass, Settings? settings)
        {
            if (settings == null)
                return 1;

            switch (formationClass)
            {
                case FormationClass.Ranged:
                    return settings.ArcherFormation;
                case FormationClass.HorseArcher:
                    return settings.CavalryFormation;
                case FormationClass.Cavalry:
                case FormationClass.LightCavalry:
                case FormationClass.HeavyCavalry:
                    return settings.CavalryFormation;
                default:
                    return settings.InfantryFormation;
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
