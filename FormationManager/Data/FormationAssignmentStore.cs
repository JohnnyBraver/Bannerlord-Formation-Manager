using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;
using TroopClassifier;

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
        private static Dictionary<string, TroopDeploymentPlan> _archivedDeploymentPlans = new();
        private static Dictionary<string, TroopDeploymentPlan> _pausedDeploymentPlans = new();
        private static Dictionary<string, RoleAssignment> _roleAssignments = new();
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
            _archivedDeploymentPlans = new Dictionary<string, TroopDeploymentPlan>();
            _pausedDeploymentPlans = new Dictionary<string, TroopDeploymentPlan>();
            _roleAssignments = new Dictionary<string, RoleAssignment>();
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
                if (data?.ArchivedDeploymentPlans != null)
                    _archivedDeploymentPlans = data.ArchivedDeploymentPlans;
                if (data?.PausedDeploymentPlans != null)
                    _pausedDeploymentPlans = data.PausedDeploymentPlans;
                if (data?.RoleAssignments != null)
                    _roleAssignments = data.RoleAssignments;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[FormationManager] Failed to load assignment data: {ex.Message}",
                    new Color(0.9f, 0.3f, 0.3f)));
            }

            SetAdvancedPlansEnabled(Settings.Instance?.ShowAdvancedFormationEditor ?? true);
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
                    DeploymentPlans = _deploymentPlans,
                    ArchivedDeploymentPlans = _archivedDeploymentPlans,
                    PausedDeploymentPlans = _pausedDeploymentPlans,
                    RoleAssignments = _roleAssignments
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
            if (_secondaryAssignments.TryGetValue(troopId, out int secondaryIndex) && secondaryIndex == formationIndex)
                _secondaryAssignments.Remove(troopId);
            _isDirty = true;
        }

        public static void ClearAssignment(string troopId)
        {
            if (_assignments.Remove(troopId) | _secondaryAssignments.Remove(troopId))
                _isDirty = true;
        }

        /// <summary>Returns one or two explicit target slots, or an empty array when no override is set.</summary>
        public static int[] GetAssignments(string troopId)
        {
            bool hasPrimary = _assignments.TryGetValue(troopId, out int primaryIndex) && FormationPlanNormalizer.IsValidFormationIndex(primaryIndex);
            bool hasSecondary = _secondaryAssignments.TryGetValue(troopId, out int secondaryIndex) &&
                                FormationPlanNormalizer.IsValidFormationIndex(secondaryIndex) && (!hasPrimary || secondaryIndex != primaryIndex);

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
            if (!FormationPlanNormalizer.IsValidFormationIndex(formationIndex))
            {
                ClearSecondaryAssignment(troopId);
                return;
            }

            if (!_assignments.TryGetValue(troopId, out int primaryIndex) || !FormationPlanNormalizer.IsValidFormationIndex(primaryIndex))
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
            _isDirty = true;
        }

        public static void ClearSecondaryAssignment(string troopId)
        {
            if (_secondaryAssignments.Remove(troopId))
                _isDirty = true;
        }

        /// <summary>Returns the saved one- or two-formation fallback for a role.</summary>
        public static int[] GetRoleAssignments(TroopRole role)
        {
            if (!_roleAssignments.TryGetValue(role.ToString(), out var assignment))
                return Array.Empty<int>();

            bool hasPrimary = FormationPlanNormalizer.IsValidFormationIndex(assignment.PrimaryFormation);
            bool hasSecondary = FormationPlanNormalizer.IsValidFormationIndex(assignment.SecondaryFormation) &&
                                assignment.SecondaryFormation != assignment.PrimaryFormation;
            if (hasPrimary && hasSecondary) return new[] { assignment.PrimaryFormation, assignment.SecondaryFormation };
            if (hasPrimary) return new[] { assignment.PrimaryFormation };
            if (hasSecondary) return new[] { assignment.SecondaryFormation };
            return Array.Empty<int>();
        }

        public static bool HasAnyRoleAssignments => _roleAssignments.Keys.Any(key =>
            Enum.TryParse(key, out TroopRole role) && GetRoleAssignments(role).Length > 0);

        public static void SetRoleAssignment(TroopRole role, int formationIndex)
        {
            if (!FormationPlanNormalizer.IsValidFormationIndex(formationIndex))
            {
                ClearRoleAssignment(role);
                return;
            }

            var assignment = GetOrCreateRoleAssignment(role);
            assignment.PrimaryFormation = formationIndex;
            if (assignment.SecondaryFormation == formationIndex) assignment.SecondaryFormation = -1;
            _isDirty = true;
        }

        public static void SetSecondaryRoleAssignment(TroopRole role, int formationIndex)
        {
            var assignment = GetOrCreateRoleAssignment(role);
            if (!FormationPlanNormalizer.IsValidFormationIndex(formationIndex) || formationIndex == assignment.PrimaryFormation)
                assignment.SecondaryFormation = -1;
            else
                assignment.SecondaryFormation = formationIndex;
            _isDirty = true;
        }

        public static void ClearRoleAssignment(TroopRole role)
        {
            if (_roleAssignments.Remove(role.ToString())) _isDirty = true;
        }

        public static void ClearSecondaryRoleAssignment(TroopRole role)
        {
            if (_roleAssignments.TryGetValue(role.ToString(), out var assignment) && assignment.SecondaryFormation >= 0)
            {
                assignment.SecondaryFormation = -1;
                _isDirty = true;
            }
        }

        private static RoleAssignment GetOrCreateRoleAssignment(TroopRole role)
        {
            string key = role.ToString();
            if (!_roleAssignments.TryGetValue(key, out var assignment))
            {
                assignment = new RoleAssignment();
                _roleAssignments[key] = assignment;
            }
            return assignment;
        }

        public static bool TryGetDeploymentPlan(string troopId, out TroopDeploymentPlan? plan)
        {
            if (_deploymentPlans.TryGetValue(troopId, out var storedPlan))
            {
                if (FormationPlanNormalizer.Normalize(storedPlan))
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

        /// <summary>Atomically replaces the saved custom plan with an editor draft.</summary>
        public static void SaveCustomPlan(string troopId, IReadOnlyDictionary<int, int> targets, IReadOnlyDictionary<int, int> weights)
        {
            var plan = TroopDeploymentPlan.CreateCustom();
            foreach (var pair in targets.Where(pair => FormationPlanNormalizer.IsValidFormationIndex(pair.Key) && pair.Value > 0))
                plan.FormationTargets[pair.Key] = pair.Value;
            foreach (var pair in weights.Where(pair => FormationPlanNormalizer.IsValidFormationIndex(pair.Key) && pair.Value > 0))
                plan.FormationWeights[pair.Key] = Math.Min(100, pair.Value);

            if (plan.IsValid())
                _deploymentPlans[troopId] = plan;
            else
                _deploymentPlans.Remove(troopId);

            _archivedDeploymentPlans.Remove(troopId);
            _pausedDeploymentPlans.Remove(troopId);
            _isDirty = true;
        }

        public static void ClearDeploymentPlan(string troopId)
        {
            if (_deploymentPlans.TryGetValue(troopId, out var plan))
            {
                _archivedDeploymentPlans[troopId] = plan;
                _deploymentPlans.Remove(troopId);
                _isDirty = true;
            }
        }

        public static bool CanRestoreDeploymentPlan(string troopId)
            => TryGetArchivedDeploymentPlan(troopId, out var plan) && plan.IsValid();

        /// <summary>Returns a detached copy for the editor to stage before saving.</summary>
        public static bool TryGetArchivedDeploymentPlanCopy(string troopId, out TroopDeploymentPlan? planCopy)
        {
            planCopy = null;
            if (!TryGetArchivedDeploymentPlan(troopId, out var plan))
                return false;

            FormationPlanNormalizer.Normalize(plan);
            if (!plan.IsValid())
                return false;

            planCopy = FormationPlanNormalizer.Clone(plan);
            return true;
        }

        public static bool RestoreDeploymentPlan(string troopId)
        {
            if (_deploymentPlans.ContainsKey(troopId) || !TryGetArchivedDeploymentPlan(troopId, out var plan))
                return false;

            FormationPlanNormalizer.Normalize(plan);
            if (!plan.IsValid())
            {
                _archivedDeploymentPlans.Remove(troopId);
                _pausedDeploymentPlans.Remove(troopId);
                _isDirty = true;
                return false;
            }

            _deploymentPlans[troopId] = plan;
            _archivedDeploymentPlans.Remove(troopId);
            _pausedDeploymentPlans.Remove(troopId);
            _isDirty = true;
            return true;
        }

        private static bool TryGetArchivedDeploymentPlan(string troopId, out TroopDeploymentPlan plan)
        {
            if (_archivedDeploymentPlans.TryGetValue(troopId, out plan!))
                return true;

            return _pausedDeploymentPlans.TryGetValue(troopId, out plan!);
        }

        /// <summary>
        /// Moves active advanced plans into a separate persisted pause archive when
        /// the feature is disabled. Enabling the feature only makes the editor
        /// available again; each paused plan must be restored explicitly.
        /// This is intentionally separate from the per-troop Clear/Restore archive.
        /// </summary>
        public static void SetAdvancedPlansEnabled(bool enabled)
        {
            bool changed = false;
            if (!enabled)
            {
                foreach (var pair in _deploymentPlans.ToList())
                {
                    _pausedDeploymentPlans[pair.Key] = pair.Value;
                    _deploymentPlans.Remove(pair.Key);
                    changed = true;
                }
            }
            if (changed)
                _isDirty = true;
        }

        public static bool HasAnyAssignments => _assignments.Count > 0 || _secondaryAssignments.Count > 0 || _deploymentPlans.Count > 0;

        private class StorageModel
        {
            public Dictionary<string, int> Assignments { get; set; } = new();
            public Dictionary<string, int> SecondaryAssignments { get; set; } = new();
            public Dictionary<string, TroopDeploymentPlan> DeploymentPlans { get; set; } = new();
            public Dictionary<string, TroopDeploymentPlan> ArchivedDeploymentPlans { get; set; } = new();
            public Dictionary<string, TroopDeploymentPlan> PausedDeploymentPlans { get; set; } = new();
            public Dictionary<string, RoleAssignment> RoleAssignments { get; set; } = new();
        }

        public class RoleAssignment
        {
            public int PrimaryFormation { get; set; } = -1;
            public int SecondaryFormation { get; set; } = -1;
        }
    }

}
