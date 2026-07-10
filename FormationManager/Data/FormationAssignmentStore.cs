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
                    SecondaryAssignments = _secondaryAssignments
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
            _isDirty = true;
        }

        public static void ClearSecondaryAssignment(string troopId)
        {
            if (_secondaryAssignments.Remove(troopId))
                _isDirty = true;
        }

        public static bool HasAnyAssignments => _assignments.Count > 0 || _secondaryAssignments.Count > 0;

        private static bool IsValidFormationIndex(int formationIndex)
            => formationIndex >= 0 && formationIndex <= 7;

        private class StorageModel
        {
            public Dictionary<string, int> Assignments { get; set; } = new();
            public Dictionary<string, int> SecondaryAssignments { get; set; } = new();
        }
    }
}
