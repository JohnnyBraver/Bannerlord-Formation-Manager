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
    /// Immutable-in-practice snapshot of the managed OOB layout. It is built once
    /// from the active deployment agents and then shared by preview placement,
    /// card-class selection, weights, and managed-slider locks.
    /// </summary>
    internal sealed class OobFormationLayout
    {
        private readonly int[] _formationCounts = new int[8];
        private readonly int[,] _formationClassCounts = new int[8, 7];
        private readonly Dictionary<int, int> _agentFormationIndices = new();
        private readonly Dictionary<string, Dictionary<int, int>> _remainderTotals = new();

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
            if (agent != null && _agentFormationIndices.TryGetValue(agent.Index, out int plannedFormationIndex))
                return plannedFormationIndex;

            return FormationAssignmentResolver.ResolveFormationIndex(agent, character, settings);
        }

        public static OobFormationLayout ForActiveAgents(Team? team, Settings? settings)
        {
            var layout = new OobFormationLayout();
            if (team == null)
                return layout;

            var groups = team.ActiveAgents
                .Where(agent => TroopControlScope.ShouldManage(agent, settings!))
                .GroupBy(agent => agent.Character.StringId)
                .OrderBy(group => group.Key);

            foreach (var group in groups)
            {
                var agents = group.OrderBy(agent => agent.Index).ToList();
                if (agents.Count > 0)
                    layout.AddStack(agents[0].Character, agents.Count, agents, settings);
            }

            return layout;
        }

        private void AddStack(BasicCharacterObject character, int troopCount, List<Agent> agents, Settings? settings)
        {
            // Role defaults deliberately use each spawned agent instead of the
            // troop template. One troop stack may contain different rolled kits.
            // Explicit troop plans remain stack-level and take precedence.
            if (FormationAssignmentResolver.UsesAgentRoleDefaults(character, settings))
            {
                foreach (Agent agent in agents)
                {
                    int formationIndex = FormationAssignmentResolver.GetDefaultFormationIndex(agent, character, settings);
                    _agentFormationIndices[agent.Index] = formationIndex;
                    _formationCounts[formationIndex]++;

                    int classIndex = (int)RefreshFormationPatch.MapToDeploymentClass(agent);
                    if (classIndex >= 0 && classIndex < 7)
                        _formationClassCounts[formationIndex, classIndex]++;
                }

                return;
            }

            Dictionary<int, int> allocatedCounts;
            if (FormationAssignmentResolver.TryGetEvenSplitTargets(character, out int[] evenTargets, settings))
            {
                allocatedCounts = FormationPlanAllocator.AllocateEvenly(
                    evenTargets,
                    troopCount,
                    CreateRemainderStrategy(evenTargets.Select(index => $"{index}").ToArray()));
            }
            else if (FormationAssignmentResolver.TryGetWeightedCustomSurplus(character, troopCount, out _, out var weights, out _, settings))
            {
                allocatedCounts = FormationAssignmentResolver.GetAllocatedFormationCounts(
                    character,
                    troopCount,
                    settings,
                    CreateRemainderStrategy(weights.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}").ToArray()));
            }
            else
            {
                allocatedCounts = FormationAssignmentResolver.GetAllocatedFormationCounts(character, troopCount, settings);
            }

            DeploymentFormationClass deploymentClass = RefreshFormationPatch.MapToDeploymentClass(character);
            foreach (var allocation in allocatedCounts)
            {
                _formationCounts[allocation.Key] += allocation.Value;
                int classIndex = (int)deploymentClass;
                if (classIndex >= 0 && classIndex < 7)
                    _formationClassCounts[allocation.Key, classIndex] += allocation.Value;
            }

            int agentOffset = 0;
            foreach (var allocation in allocatedCounts.OrderBy(pair => pair.Key))
            {
                for (int i = 0; i < allocation.Value && agentOffset < agents.Count; i++)
                    _agentFormationIndices[agents[agentOffset++].Index] = allocation.Key;
            }
        }

        private IFormationRemainderStrategy CreateRemainderStrategy(IEnumerable<string> signatureParts)
        {
            string key = string.Join(",", signatureParts);
            if (!_remainderTotals.TryGetValue(key, out var totals))
            {
                totals = new Dictionary<int, int>();
                _remainderTotals.Add(key, totals);
            }

            return new BalancedRemainderStrategy(totals);
        }

        private sealed class BalancedRemainderStrategy : IFormationRemainderStrategy
        {
            private readonly Dictionary<int, int> _totals;

            public BalancedRemainderStrategy(Dictionary<int, int> totals)
            {
                _totals = totals;
            }

            public void RecordBaseAllocation(int formationIndex, int count)
                => Add(formationIndex, count);

            public int SelectRecipient(IReadOnlyDictionary<int, int> remainders, ISet<int> excludedRecipients)
                => remainders
                    .Where(pair => !excludedRecipients.Contains(pair.Key))
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => _totals.TryGetValue(pair.Key, out int total) ? total : 0)
                    .ThenBy(pair => pair.Key)
                    .First().Key;

            public void RecordRemainderAllocation(int formationIndex)
                => Add(formationIndex, 1);

            private void Add(int formationIndex, int count)
            {
                if (count <= 0)
                    return;
                _totals[formationIndex] = _totals.TryGetValue(formationIndex, out int existing) ? existing + count : count;
            }
        }
    }
}
