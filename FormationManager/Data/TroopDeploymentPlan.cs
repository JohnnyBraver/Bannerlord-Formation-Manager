using System.Collections.Generic;
using System.Linq;

namespace FormationManager.Data
{
    /// <summary>
    /// Persisted target-and-weight allocation for one troop type. FormationCounts
    /// remains solely to migrate pre-v0.4 saved plans.
    /// </summary>
    public sealed class TroopDeploymentPlan
    {
        public TroopDeploymentPlanMode Mode { get; set; }
        public List<int> FormationIndices { get; set; } = new();
        public Dictionary<int, int> FormationCounts { get; set; } = new();
        public Dictionary<int, int> FormationTargets { get; set; } = new();
        public Dictionary<int, int> FormationWeights { get; set; } = new();

        public static TroopDeploymentPlan CreateEven(IEnumerable<int> formationIndices)
            => new()
            {
                Mode = TroopDeploymentPlanMode.Even,
                FormationIndices = formationIndices.Distinct().OrderBy(index => index).ToList()
            };

        public static TroopDeploymentPlan CreateCustom()
            => new() { Mode = TroopDeploymentPlanMode.Custom };

        public bool IsValid()
            => Mode == TroopDeploymentPlanMode.Even
                ? FormationIndices.Count >= 2 && FormationIndices.All(FormationPlanNormalizer.IsValidFormationIndex)
                : FormationTargets.Any(pair => FormationPlanNormalizer.IsValidFormationIndex(pair.Key) && pair.Value > 0) ||
                  FormationWeights.Any(pair => FormationPlanNormalizer.IsValidFormationIndex(pair.Key) && pair.Value > 0);
    }

    public enum TroopDeploymentPlanMode
    {
        Even,
        Custom
    }

    /// <summary>Centralizes saved-plan validation, migration, and safe editor copies.</summary>
    internal static class FormationPlanNormalizer
    {
        public static bool IsValidFormationIndex(int formationIndex)
            => formationIndex >= 0 && formationIndex <= 7;

        /// <summary>Migrates pre-target custom count plans in place on first use.</summary>
        public static bool Normalize(TroopDeploymentPlan plan)
        {
            if (plan.Mode != TroopDeploymentPlanMode.Custom)
                return false;

            bool changed = false;
            plan.FormationTargets ??= new Dictionary<int, int>();
            plan.FormationWeights ??= new Dictionary<int, int>();
            plan.FormationCounts ??= new Dictionary<int, int>();

            foreach (var pair in plan.FormationCounts.Where(pair => IsValidFormationIndex(pair.Key) && pair.Value > 0))
            {
                if (!plan.FormationTargets.ContainsKey(pair.Key))
                {
                    plan.FormationTargets[pair.Key] = pair.Value;
                    changed = true;
                }
            }

            if (plan.FormationCounts.Count > 0)
            {
                plan.FormationCounts.Clear();
                changed = true;
            }

            foreach (int key in plan.FormationTargets.Keys.Where(key => !IsValidFormationIndex(key) || plan.FormationTargets[key] <= 0).ToList())
            {
                plan.FormationTargets.Remove(key);
                changed = true;
            }
            foreach (int key in plan.FormationWeights.Keys.Where(key => !IsValidFormationIndex(key) || plan.FormationWeights[key] <= 0).ToList())
            {
                plan.FormationWeights.Remove(key);
                changed = true;
            }

            return changed;
        }

        public static TroopDeploymentPlan Clone(TroopDeploymentPlan plan)
            => new()
            {
                Mode = plan.Mode,
                FormationIndices = plan.FormationIndices.ToList(),
                FormationCounts = plan.FormationCounts.ToDictionary(pair => pair.Key, pair => pair.Value),
                FormationTargets = plan.FormationTargets.ToDictionary(pair => pair.Key, pair => pair.Value),
                FormationWeights = plan.FormationWeights.ToDictionary(pair => pair.Key, pair => pair.Value)
            };
    }
}
