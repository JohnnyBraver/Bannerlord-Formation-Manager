using System;
using System.Collections.Generic;
using System.Linq;

namespace FormationManager.Data
{
    /// <summary>
    /// Chooses recipients for fractional allocation remainders. OOB supplies a
    /// balancing strategy; UI and mission callers use the deterministic default.
    /// </summary>
    internal interface IFormationRemainderStrategy
    {
        void RecordBaseAllocation(int formationIndex, int count);
        int SelectRecipient(IReadOnlyDictionary<int, int> remainders, ISet<int> excludedRecipients);
        void RecordRemainderAllocation(int formationIndex);
    }

    /// <summary>Single source of truth for target-and-weight formation allocation.</summary>
    internal static class FormationPlanAllocator
    {
        public static Dictionary<int, int> AllocateEvenly(
            IEnumerable<int> formationIndices,
            int troopCount,
            IFormationRemainderStrategy? remainderStrategy = null)
        {
            var targets = formationIndices.Where(FormationPlanNormalizer.IsValidFormationIndex)
                .Distinct().OrderBy(index => index).ToArray();
            var allocation = new Dictionary<int, int>();
            if (troopCount <= 0 || targets.Length == 0)
                return allocation;

            int countPerFormation = troopCount / targets.Length;
            foreach (int target in targets)
            {
                AddCount(allocation, target, countPerFormation);
                remainderStrategy?.RecordBaseAllocation(target, countPerFormation);
            }

            var remainders = targets.ToDictionary(index => index, _ => 0);
            var excluded = new HashSet<int>();
            for (int remaining = troopCount % targets.Length; remaining > 0; remaining--)
            {
                int target = SelectRecipient(remainders, excluded, remainderStrategy);
                AddCount(allocation, target, 1);
                remainderStrategy?.RecordRemainderAllocation(target);
                excluded.Add(target);
            }

            return allocation;
        }

        public static Dictionary<int, int> AllocateCustom(
            TroopDeploymentPlan plan,
            int troopCount,
            int fallbackIndex,
            bool prioritizeWeightsInSmallStacks,
            IFormationRemainderStrategy? remainderStrategy = null)
        {
            var allocation = new Dictionary<int, int>();
            if (troopCount <= 0)
                return allocation;

            var targets = PositiveEntries(plan.FormationTargets);
            var weights = PositiveEntries(plan.FormationWeights);
            var activeIndices = targets.Keys.Concat(weights.Keys).Distinct().OrderBy(index => index).ToArray();
            if (activeIndices.Length == 0)
            {
                allocation[fallbackIndex] = troopCount;
                return allocation;
            }

            int targetTotal = targets.Values.Sum();
            if (targetTotal == 0 && weights.Count > 0 && !prioritizeWeightsInSmallStacks)
            {
                int remaining = GiveOneToEachActive(allocation, activeIndices, troopCount);
                AllocateProportionally(allocation, weights, remaining, remainderStrategy);
                return allocation;
            }

            if (troopCount >= targetTotal)
            {
                foreach (var pair in targets)
                    allocation[pair.Key] = pair.Value;

                int surplus = troopCount - targetTotal;
                AllocateProportionally(allocation, weights.Count > 0 ? weights : targets, surplus, remainderStrategy);
                return allocation;
            }

            if (prioritizeWeightsInSmallStacks)
            {
                AllocateProportionally(allocation, weights.Count > 0 ? weights : targets, troopCount, remainderStrategy);
                return allocation;
            }

            int remainingAfterMinimums = GiveOneToEachActive(allocation, activeIndices, troopCount);
            AllocateProportionally(allocation, weights.Count > 0 ? weights : targets, remainingAfterMinimums, remainderStrategy);
            return allocation;
        }

        private static Dictionary<int, int> PositiveEntries(IReadOnlyDictionary<int, int> source)
            => source.Where(pair => FormationPlanNormalizer.IsValidFormationIndex(pair.Key) && pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

        private static int GiveOneToEachActive(Dictionary<int, int> allocation, IEnumerable<int> activeIndices, int troopCount)
        {
            int remaining = troopCount;
            foreach (int index in activeIndices)
            {
                if (remaining == 0)
                    break;
                AddCount(allocation, index, 1);
                remaining--;
            }
            return remaining;
        }

        private static void AllocateProportionally(
            Dictionary<int, int> allocation,
            IReadOnlyDictionary<int, int> values,
            int troopCount,
            IFormationRemainderStrategy? remainderStrategy)
        {
            if (troopCount <= 0 || values.Count == 0)
                return;

            int total = values.Values.Sum(value => Math.Max(0, value));
            if (total <= 0)
                return;

            var remainders = new Dictionary<int, int>();
            int allocated = 0;
            foreach (var pair in values.OrderBy(pair => pair.Key))
            {
                int numerator = troopCount * pair.Value;
                int count = numerator / total;
                AddCount(allocation, pair.Key, count);
                remainderStrategy?.RecordBaseAllocation(pair.Key, count);
                allocated += count;
                remainders[pair.Key] = numerator % total;
            }

            var excluded = new HashSet<int>();
            while (allocated < troopCount)
            {
                int target = SelectRecipient(remainders, excluded, remainderStrategy);
                AddCount(allocation, target, 1);
                remainderStrategy?.RecordRemainderAllocation(target);
                excluded.Add(target);
                allocated++;
            }
        }

        private static int SelectRecipient(
            IReadOnlyDictionary<int, int> remainders,
            ISet<int> excludedRecipients,
            IFormationRemainderStrategy? remainderStrategy)
        {
            int target = remainderStrategy?.SelectRecipient(remainders, excludedRecipients) ?? -1;
            if (remainders.ContainsKey(target) && !excludedRecipients.Contains(target))
                return target;

            return remainders
                .Where(pair => !excludedRecipients.Contains(pair.Key))
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .First().Key;
        }

        private static void AddCount(Dictionary<int, int> allocation, int formationIndex, int count)
        {
            if (count <= 0)
                return;

            allocation[formationIndex] = allocation.TryGetValue(formationIndex, out int existing) ? existing + count : count;
        }
    }
}
