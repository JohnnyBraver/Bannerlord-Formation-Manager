using System;
using System.Collections.Generic;
using System.Linq;
using FormationManager.Data;

namespace FormationManager.UI
{
    /// <summary>
    /// UI-independent editable state for one troop's advanced formation plan.
    /// The mixin owns display and commands; this type owns plan editing rules.
    /// </summary>
    internal sealed class AdvancedFormationPlanDraft
    {
        private readonly int[] _targets = new int[8];
        private readonly int[] _weights = new int[8];

        public bool IsInitialized { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsClearRequested { get; private set; }

        public int GetTarget(int formationIndex) => _targets[formationIndex];
        public int GetWeight(int formationIndex) => _weights[formationIndex];
        public bool IsActive(int formationIndex) => _targets[formationIndex] > 0 || _weights[formationIndex] > 0;
        public IReadOnlyDictionary<int, int> Targets => Enumerable.Range(0, 8).ToDictionary(index => index, GetTarget);
        public IReadOnlyDictionary<int, int> Weights => Enumerable.Range(0, 8).ToDictionary(index => index, GetWeight);

        public void Initialize(TroopDeploymentPlan? plan, IEnumerable<int> baselineFormationIndices, bool hasArchivedPlan)
        {
            if (IsInitialized)
                return;

            ClearValues();
            if (plan != null)
                LoadValues(plan);
            else if (!hasArchivedPlan)
                foreach (int index in baselineFormationIndices.Where(IsValidFormationIndex).Distinct())
                    _targets[index] = 1;

            IsInitialized = true;
            IsDirty = false;
            IsClearRequested = false;
        }

        public void Load(TroopDeploymentPlan plan)
        {
            ClearValues();
            LoadValues(plan);
            IsInitialized = true;
            IsDirty = true;
            IsClearRequested = false;
        }

        public void Reset()
        {
            ClearValues();
            IsInitialized = false;
            IsDirty = false;
            IsClearRequested = false;
        }

        public void Clear()
        {
            ClearValues();
            IsClearRequested = true;
            IsDirty = true;
        }

        public bool Activate(int formationIndex)
        {
            if (!IsValidFormationIndex(formationIndex) || IsActive(formationIndex))
                return false;

            _targets[formationIndex] = 1;
            IsClearRequested = false;
            IsDirty = true;
            return true;
        }

        public bool Deactivate(int formationIndex)
        {
            if (!IsValidFormationIndex(formationIndex) || !IsActive(formationIndex))
                return false;

            _targets[formationIndex] = 0;
            _weights[formationIndex] = 0;
            UpdateClearRequested();
            IsDirty = true;
            return true;
        }

        public bool SetTarget(int formationIndex, int value, int readyTroopCount)
        {
            if (!IsValidFormationIndex(formationIndex))
                return false;

            int otherTargets = Enumerable.Range(0, 8).Where(index => index != formationIndex).Sum(index => _targets[index]);
            int next = Math.Max(0, Math.Min(value, Math.Max(0, readyTroopCount - otherTargets)));
            if (_targets[formationIndex] == next)
                return false;

            _targets[formationIndex] = next;
            UpdateClearRequested();
            IsDirty = true;
            return true;
        }

        public bool SetWeight(int formationIndex, int value)
        {
            if (!IsValidFormationIndex(formationIndex))
                return false;

            int next = Math.Max(0, Math.Min(100, value));
            if (_weights[formationIndex] == next)
                return false;

            _weights[formationIndex] = next;
            UpdateClearRequested();
            IsDirty = true;
            return true;
        }

        public int GetTargetMaximum(int formationIndex, int readyTroopCount)
            => !IsValidFormationIndex(formationIndex)
                ? 0
                : Math.Max(0, readyTroopCount - Enumerable.Range(0, 8)
                    .Where(index => index != formationIndex).Sum(index => _targets[index]));

        public void MarkSaved()
        {
            IsDirty = false;
            IsClearRequested = false;
        }

        private void LoadValues(TroopDeploymentPlan plan)
        {
            if (plan.Mode == TroopDeploymentPlanMode.Custom)
            {
                foreach (var pair in plan.FormationTargets.Where(pair => IsValidFormationIndex(pair.Key)))
                    _targets[pair.Key] = pair.Value;
                foreach (var pair in plan.FormationWeights.Where(pair => IsValidFormationIndex(pair.Key)))
                    _weights[pair.Key] = pair.Value;
            }
            else
            {
                foreach (int index in plan.FormationIndices.Where(IsValidFormationIndex))
                    _weights[index] = 1;
            }
        }

        private void UpdateClearRequested()
            => IsClearRequested = !_targets.Any(value => value > 0) && !_weights.Any(value => value > 0);

        private void ClearValues()
        {
            Array.Clear(_targets, 0, _targets.Length);
            Array.Clear(_weights, 0, _weights.Length);
        }

        private static bool IsValidFormationIndex(int formationIndex)
            => formationIndex >= 0 && formationIndex <= 7;
    }
}
