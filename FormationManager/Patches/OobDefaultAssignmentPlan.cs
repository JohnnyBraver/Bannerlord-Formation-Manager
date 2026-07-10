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
        private readonly Dictionary<int, int> _agentFormationIndices = new();
        private readonly Dictionary<string, Dictionary<int, int>> _splitTargetCounts = new();

        public int GetFormationCount(int formationIndex)
            => formationIndex >= 0 && formationIndex < _formationCounts.Length ? _formationCounts[formationIndex] : 0;

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
            int[] targets = FormationAssignmentResolver.GetFormationIndices(character, settings);
            if (troopCount <= 0 || targets.Length == 0)
                return;

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
                _formationCounts[target] += countPerTarget;
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
                _formationCounts[target]++;
                totalsForThisSplit[target]++;
                allocatedCounts[target]++;
            }

            if (agents == null)
                return;

            int agentOffset = 0;
            foreach (int target in targets)
            {
                int assignedCount = allocatedCounts[target];
                for (int i = 0; i < assignedCount && agentOffset < agents.Count; i++)
                    _agentFormationIndices[agents[agentOffset++].Index] = target;
            }
        }
    }
}
