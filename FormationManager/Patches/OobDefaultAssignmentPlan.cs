using System.Collections.Generic;
using System.Linq;
using FormationManager.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FormationManager.Patches
{
    /// <summary>
    /// Produces a balanced initial split for the OOB. Remainders from several odd
    /// stacks are shared: each remainder goes to the currently smaller target slot
    /// instead of always favouring the first configured formation.
    /// </summary>
    internal sealed class OobDefaultAssignmentPlan
    {
        private readonly int[] _formationCounts = new int[8];
        private readonly int[,] _formationClassCounts = new int[8, 7];
        private readonly Dictionary<int, int> _agentFormationIndices = new();
        private readonly Dictionary<string, Dictionary<int, int>> _splitTargetCounts = new();
        private readonly Dictionary<string, Dictionary<int, int>> _weightedTargetCounts = new();

        public int GetFormationCount(int formationIndex)
            => formationIndex >= 0 && formationIndex < _formationCounts.Length ? _formationCounts[formationIndex] : 0;

        public int GetFormationClassCount(int formationIndex, DeploymentFormationClass deploymentClass)
        {
            int classIndex = (int)deploymentClass;
            return formationIndex >= 0 && formationIndex < 8 && classIndex >= 0 && classIndex < 7
                ? _formationClassCounts[formationIndex, classIndex]
                : 0;
        }

        public int GetFormationIndex(Agent? agent, BasicCharacterObject character, Settings? settings)
        {
            if (agent != null && _agentFormationIndices.TryGetValue(agent.Index, out int formationIndex))
                return formationIndex;

            return FormationAssignmentResolver.ResolveFormationIndex(agent, character, settings);
        }

        public static OobDefaultAssignmentPlan ForPlayerRoster(Settings? settings)
        {
            var plan = new OobDefaultAssignmentPlan();
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null)
                return plan;

            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character == null)
                    continue;
                if (element.Character == Hero.MainHero?.CharacterObject)
                    continue;

                int readyCount = element.Number - element.WoundedNumber;
                if (readyCount > 0)
                    plan.AddStack(element.Character, readyCount, null, settings);
            }

            return plan;
        }

        public static OobDefaultAssignmentPlan ForActiveAgents(Team? team, Settings? settings)
        {
            var plan = new OobDefaultAssignmentPlan();
            if (team == null)
                return plan;

            var groups = team.ActiveAgents
                .Where(agent => agent != null && agent.IsHuman && !agent.IsMount && !agent.IsMainAgent && agent.Character != null)
                .GroupBy(agent => agent.Character.StringId)
                .OrderBy(group => group.Key);

            foreach (var group in groups)
            {
                var agents = group.OrderBy(agent => agent.Index).ToList();
                if (agents.Count == 0)
                    continue;

                plan.AddStack(agents[0].Character, agents.Count, agents, settings);
            }

            return plan;
        }

        private void AddStack(BasicCharacterObject character, int troopCount, List<Agent>? agents, Settings? settings)
        {
            if (troopCount <= 0)
                return;

            Dictionary<int, int> allocatedCounts;
            if (FormationAssignmentResolver.TryGetEvenSplitTargets(character, out int[] evenTargets, settings))
                allocatedCounts = AllocateEvenSplit(evenTargets, troopCount);
            else if (FormationAssignmentResolver.TryGetWeightedCustomSurplus(character, troopCount, out var targets, out var weights, out int surplus, settings))
                allocatedCounts = AllocateWeightedCustomSplit(
                    targets,
                    weights,
                    surplus,
                    targets.Count == 0 && !(settings?.PrioritizeWeightsInSmallStacks ?? false));
            else
                allocatedCounts = FormationAssignmentResolver.GetAllocatedFormationCounts(character, troopCount, settings);

            if (allocatedCounts.Count == 0)
                return;

            DeploymentFormationClass deploymentClass = RefreshFormationPatch.MapToDeploymentClass(character);
            foreach (var allocation in allocatedCounts)
            {
                _formationCounts[allocation.Key] += allocation.Value;
                int classIndex = (int)deploymentClass;
                if (classIndex >= 0 && classIndex < 7)
                    _formationClassCounts[allocation.Key, classIndex] += allocation.Value;
            }

            if (agents == null)
                return;

            int agentOffset = 0;
            foreach (var allocation in allocatedCounts.OrderBy(pair => pair.Key))
            {
                for (int i = 0; i < allocation.Value && agentOffset < agents.Count; i++)
                    _agentFormationIndices[agents[agentOffset++].Index] = allocation.Key;
            }
        }

        private Dictionary<int, int> AllocateEvenSplit(int[] targets, int troopCount)
        {
            var allocatedCounts = new Dictionary<int, int>();
            string splitKey = string.Join(",", targets.OrderBy(index => index));
            if (!_splitTargetCounts.TryGetValue(splitKey, out var totalsForThisSplit))
            {
                totalsForThisSplit = targets.ToDictionary(index => index, _ => 0);
                _splitTargetCounts.Add(splitKey, totalsForThisSplit);
            }

            int countPerTarget = troopCount / targets.Length;
            foreach (int target in targets)
            {
                totalsForThisSplit[target] += countPerTarget;
                allocatedCounts[target] = countPerTarget;
            }

            int remainder = troopCount % targets.Length;
            for (int i = 0; i < remainder; i++)
            {
                int target = targets
                    .OrderBy(index => totalsForThisSplit[index])
                    .ThenBy(index => index)
                    .First();
                totalsForThisSplit[target]++;
                allocatedCounts[target]++;
            }

            return allocatedCounts;
        }

        private Dictionary<int, int> AllocateWeightedCustomSplit(
            IReadOnlyDictionary<int, int> targets,
            IReadOnlyDictionary<int, int> weights,
            int surplus,
            bool applyMinimums)
        {
            var allocatedCounts = targets.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (applyMinimums)
            {
                foreach (int index in weights.Keys.OrderBy(index => index))
                {
                    if (surplus == 0)
                        return allocatedCounts;
                    allocatedCounts[index] = allocatedCounts.TryGetValue(index, out int current) ? current + 1 : 1;
                    surplus--;
                }
            }

            if (surplus == 0)
                return allocatedCounts;

            string splitKey = string.Join(",", weights.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
            if (!_weightedTargetCounts.TryGetValue(splitKey, out var totalsForThisSplit))
            {
                totalsForThisSplit = weights.Keys.ToDictionary(index => index, _ => 0);
                _weightedTargetCounts.Add(splitKey, totalsForThisSplit);
            }

            int totalWeight = weights.Values.Sum();
            int allocated = 0;
            var remainders = new Dictionary<int, int>();
            foreach (var pair in weights)
            {
                int numerator = surplus * pair.Value;
                int count = numerator / totalWeight;
                allocatedCounts[pair.Key] = allocatedCounts.TryGetValue(pair.Key, out int current) ? current + count : count;
                totalsForThisSplit[pair.Key] += count;
                allocated += count;
                remainders[pair.Key] = numerator % totalWeight;
            }

            while (allocated < surplus)
            {
                int target = weights.Keys
                    .OrderByDescending(index => remainders[index])
                    .ThenBy(index => totalsForThisSplit[index])
                    .ThenBy(index => index)
                    .First();
                allocatedCounts[target] = allocatedCounts.TryGetValue(target, out int current) ? current + 1 : 1;
                totalsForThisSplit[target]++;
                // Do not select the same largest-remainder slot twice unless all
                // candidates have been considered for this stack.
                remainders[target] = -1;
                allocated++;
            }

            return allocatedCounts;
        }
    }
}
