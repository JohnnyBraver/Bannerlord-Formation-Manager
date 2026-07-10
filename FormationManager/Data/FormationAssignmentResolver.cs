using System;
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

            int[] explicitAssignments = FormationAssignmentStore.GetAssignments(character.StringId);
            if (explicitAssignments.Length > 0)
                return explicitAssignments;

            return new[] { GetDefaultFormationIndex(character, settings) };
        }

        public static int ResolveFormationIndex(Agent? agent, BasicCharacterObject character, Settings? settings)
        {
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
            if (troopCount <= 0)
                return 0;

            int[] formationIndices = GetFormationIndices(character, settings);
            if (formationIndices.Length == 0)
                return 0;

            int matchingSlots = 0;
            int firstMatchingSlot = -1;
            for (int i = 0; i < formationIndices.Length; i++)
            {
                if (formationIndices[i] != formationIndex)
                    continue;

                matchingSlots++;
                if (firstMatchingSlot < 0)
                    firstMatchingSlot = i;
            }

            if (matchingSlots == 0)
                return 0;

            int assignedCount = troopCount * matchingSlots / formationIndices.Length;
            int remainder = troopCount % formationIndices.Length;
            if (firstMatchingSlot >= 0 && firstMatchingSlot < remainder)
                assignedCount++;

            return assignedCount;
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
