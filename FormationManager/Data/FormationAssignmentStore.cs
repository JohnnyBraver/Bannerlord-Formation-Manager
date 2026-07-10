using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace FormationManager.Data
{
    /// <summary>
    /// Persists troop-type-to-formation assignments as an external JSON file per campaign hero.
    /// Key = CharacterObject.StringId. The original primary assignment is retained
    /// for backward-compatible save files; a second optional assignment splits a
    /// troop stack evenly between two formation slots.
    /// </summary>
    public static class FormationAssignmentStore
    {
        private static string? _currentHeroId;
        private static Dictionary<string, int> _assignments = new();
        private static Dictionary<string, int> _secondaryAssignments = new();
        private static Dictionary<string, TroopDeploymentPlan> _deploymentPlans = new();
        private static bool _isDirty;

        private static string GetConfigDir()
        {
            // Documents\Mount and Blade II Bannerlord\Configs\FormationManager\
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "FormationManager");
        }

        private static string GetFilePath(string heroId)
            => Path.Combine(GetConfigDir(), $"{heroId}.json");

        public static void Load(string heroId)
        {
            _currentHeroId = heroId;
            _assignments = new Dictionary<string, int>();
            _secondaryAssignments = new Dictionary<string, int>();
            _deploymentPlans = new Dictionary<string, TroopDeploymentPlan>();
            _isDirty = false;

            string path = GetFilePath(heroId);
            if (!File.Exists(path))
                return;

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<StorageModel>(json);
                if (data?.Assignments != null)
                    _assignments = data.Assignments;
                if (data?.SecondaryAssignments != null)
                    _secondaryAssignments = data.SecondaryAssignments;
                if (data?.DeploymentPlans != null)
                    _deploymentPlans = data.DeploymentPlans;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[FormationManager] Failed to load assignment data: {ex.Message}",
                    new Color(0.9f, 0.3f, 0.3f)));
            }
        }

        public static void Save()
        {
            if (_currentHeroId == null || !_isDirty)
                return;

            try
            {
                string dir = GetConfigDir();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var model = new StorageModel
                {
                    Assignments = _assignments,
                    SecondaryAssignments = _secondaryAssignments,
                    DeploymentPlans = _deploymentPlans
                };
                string json = JsonConvert.SerializeObject(model, Formatting.Indented);
                File.WriteAllText(GetFilePath(_currentHeroId), json);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[FormationManager] Failed to save assignment data: {ex.Message}",
                    new Color(0.9f, 0.3f, 0.3f)));
            }
        }

        /// <summary>Returns the assigned formation index (0-7), or -1 if none is set.</summary>
        public static int GetAssignment(string troopId)
            => _assignments.TryGetValue(troopId, out int idx) ? idx : -1;

        public static void SetAssignment(string troopId, int formationIndex)
        {
            if (formationIndex < 0)
            {
                ClearAssignment(troopId);
                return;
            }

            _assignments[troopId] = formationIndex;
            _deploymentPlans.Remove(troopId);
            if (_secondaryAssignments.TryGetValue(troopId, out int secondaryIndex) && secondaryIndex == formationIndex)
                _secondaryAssignments.Remove(troopId);
            _isDirty = true;
        }

        public static void ClearAssignment(string troopId)
        {
            if (_assignments.Remove(troopId) | _secondaryAssignments.Remove(troopId) | _deploymentPlans.Remove(troopId))
                _isDirty = true;
        }

        /// <summary>Returns one or two explicit target slots, or an empty array when no override is set.</summary>
        public static int[] GetAssignments(string troopId)
        {
            bool hasPrimary = _assignments.TryGetValue(troopId, out int primaryIndex) && IsValidFormationIndex(primaryIndex);
            bool hasSecondary = _secondaryAssignments.TryGetValue(troopId, out int secondaryIndex) &&
                                IsValidFormationIndex(secondaryIndex) && (!hasPrimary || secondaryIndex != primaryIndex);

            if (hasPrimary && hasSecondary)
                return new[] { primaryIndex, secondaryIndex };
            if (hasPrimary)
                return new[] { primaryIndex };
            if (hasSecondary)
                return new[] { secondaryIndex };

            return Array.Empty<int>();
        }

        public static int GetSecondaryAssignment(string troopId)
            => _secondaryAssignments.TryGetValue(troopId, out int idx) ? idx : -1;

        public static void SetSecondaryAssignment(string troopId, int formationIndex)
        {
            if (!IsValidFormationIndex(formationIndex))
            {
                ClearSecondaryAssignment(troopId);
                return;
            }

            if (!_assignments.TryGetValue(troopId, out int primaryIndex) || !IsValidFormationIndex(primaryIndex))
            {
                // A secondary assignment has no meaning until a primary slot exists.
                SetAssignment(troopId, formationIndex);
                return;
            }

            if (primaryIndex == formationIndex)
            {
                ClearSecondaryAssignment(troopId);
                return;
            }

            _secondaryAssignments[troopId] = formationIndex;
            _deploymentPlans.Remove(troopId);
            _isDirty = true;
        }

        public static void ClearSecondaryAssignment(string troopId)
        {
            if (_secondaryAssignments.Remove(troopId))
                _isDirty = true;
        }

        public static bool TryGetDeploymentPlan(string troopId, out TroopDeploymentPlan? plan)
        {
            if (_deploymentPlans.TryGetValue(troopId, out var storedPlan))
            {
                if (NormalizeCustomPlan(storedPlan))
                    _isDirty = true;

                if (storedPlan.IsValid())
                {
                    plan = storedPlan;
                    return true;
                }
            }

            plan = null;
            return false;
        }

        public static void SetEvenSplit(string troopId, IEnumerable<int> formationIndices)
        {
            var indices = formationIndices.Where(IsValidFormationIndex).Distinct().OrderBy(index => index).ToList();
            if (indices.Count < 2)
            {
                ClearDeploymentPlan(troopId);
                return;
            }

            _assignments.Remove(troopId);
            _secondaryAssignments.Remove(troopId);
            _deploymentPlans[troopId] = TroopDeploymentPlan.CreateEven(indices);
            _isDirty = true;
        }

        public static int GetCustomTarget(string troopId, int formationIndex)
        {
            return TryGetDeploymentPlan(troopId, out var plan) && plan!.Mode == TroopDeploymentPlanMode.Custom &&
                   plan.FormationTargets.TryGetValue(formationIndex, out int target)
                ? target
                : 0;
        }

        public static int GetCustomWeight(string troopId, int formationIndex)
        {
            return TryGetDeploymentPlan(troopId, out var plan) && plan!.Mode == TroopDeploymentPlanMode.Custom &&
                   plan.FormationWeights.TryGetValue(formationIndex, out int weight)
                ? weight
                : 0;
        }

        /// <summary>
        /// Changes a formation target. The total of all targets is limited to the
        /// ready troops currently in this stack, so a plan cannot be overfilled while
        /// it is edited in the party screen.
        /// </summary>
        public static void SetCustomTarget(string troopId, int formationIndex, int target, int readyTroopCount)
        {
            if (!IsValidFormationIndex(formationIndex))
                return;

            var plan = GetOrCreateCustomPlan(troopId);
            int otherTargets = plan.FormationTargets
                .Where(pair => pair.Key != formationIndex && IsValidFormationIndex(pair.Key))
                .Sum(pair => Math.Max(0, pair.Value));
            int maxTarget = Math.Max(0, Math.Max(0, readyTroopCount) - otherTargets);
            int next = Math.Max(0, Math.Min(target, maxTarget));
            SetPlanValue(plan.FormationTargets, formationIndex, next);
            FinishCustomPlanChange(troopId, plan);
        }

        /// <summary>Changes the surplus-distribution weight (0-100) for one formation.</summary>
        public static void SetCustomWeight(string troopId, int formationIndex, int weight)
        {
            if (!IsValidFormationIndex(formationIndex))
                return;

            var plan = GetOrCreateCustomPlan(troopId);
            SetPlanValue(plan.FormationWeights, formationIndex, Math.Max(0, Math.Min(100, weight)));
            FinishCustomPlanChange(troopId, plan);
        }

        public static void ClearDeploymentPlan(string troopId)
        {
            if (_deploymentPlans.Remove(troopId))
                _isDirty = true;
        }

        public static bool HasAnyAssignments => _assignments.Count > 0 || _secondaryAssignments.Count > 0 || _deploymentPlans.Count > 0;

        private static bool IsValidFormationIndex(int formationIndex)
            => formationIndex >= 0 && formationIndex <= 7;

        private static TroopDeploymentPlan GetOrCreateCustomPlan(string troopId)
        {
            TroopDeploymentPlan plan;
            if (TryGetDeploymentPlan(troopId, out var existingPlan) && existingPlan!.Mode == TroopDeploymentPlanMode.Custom)
            {
                plan = existingPlan;
            }
            else if (TryGetDeploymentPlan(troopId, out existingPlan) && existingPlan!.Mode == TroopDeploymentPlanMode.Even)
            {
                // Editing EV creates a real custom plan while preserving its equal
                // distribution intent. The normal EV path remains dynamically balanced.
                plan = TroopDeploymentPlan.CreateCustom();
                foreach (int index in existingPlan.FormationIndices.Where(IsValidFormationIndex).Distinct())
                    plan.FormationWeights[index] = 1;
                _deploymentPlans[troopId] = plan;
            }
            else
            {
                plan = TroopDeploymentPlan.CreateCustom();
                _deploymentPlans[troopId] = plan;
            }

            _assignments.Remove(troopId);
            _secondaryAssignments.Remove(troopId);
            return plan;
        }

        private static void FinishCustomPlanChange(string troopId, TroopDeploymentPlan plan)
        {
            NormalizeCustomPlan(plan);
            if (!plan.IsValid())
                _deploymentPlans.Remove(troopId);
            _isDirty = true;
        }

        private static void SetPlanValue(Dictionary<int, int> values, int formationIndex, int value)
        {
            if (value <= 0)
                values.Remove(formationIndex);
            else
                values[formationIndex] = value;
        }

        /// <summary>Migrates pre-target custom count plans in-place on first use.</summary>
        private static bool NormalizeCustomPlan(TroopDeploymentPlan plan)
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

        private class StorageModel
        {
            public Dictionary<string, int> Assignments { get; set; } = new();
            public Dictionary<string, int> SecondaryAssignments { get; set; } = new();
            public Dictionary<string, TroopDeploymentPlan> DeploymentPlans { get; set; } = new();
        }
    }

    public enum TroopDeploymentPlanMode
    {
        Even,
        Custom
    }

    public sealed class TroopDeploymentPlan
    {
        public TroopDeploymentPlanMode Mode { get; set; }
        public List<int> FormationIndices { get; set; } = new();
        public Dictionary<int, int> FormationCounts { get; set; } = new();
        // FormationCounts is intentionally retained for JSON migration from v0.3.0.
        public Dictionary<int, int> FormationTargets { get; set; } = new();
        public Dictionary<int, int> FormationWeights { get; set; } = new();

        public static TroopDeploymentPlan CreateEven(IEnumerable<int> formationIndices)
        {
            return new TroopDeploymentPlan
            {
                Mode = TroopDeploymentPlanMode.Even,
                FormationIndices = formationIndices.Distinct().OrderBy(index => index).ToList()
            };
        }

        public static TroopDeploymentPlan CreateCustom()
        {
            return new TroopDeploymentPlan { Mode = TroopDeploymentPlanMode.Custom };
        }

        public bool IsValid()
        {
            return Mode == TroopDeploymentPlanMode.Even
                ? FormationIndices.Count >= 2 && FormationIndices.All(index => index >= 0 && index <= 7)
                : FormationTargets.Any(pair => pair.Key >= 0 && pair.Key <= 7 && pair.Value > 0) ||
                  FormationWeights.Any(pair => pair.Key >= 0 && pair.Key <= 7 && pair.Value > 0);
        }
    }
}
