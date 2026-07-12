using System;
using System.Linq;
using FormationManager.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace FormationManager.Patches
{
    /// <summary>Seeds native OOB class weights and optional slider locks from the resolved plan.</summary>
    internal static class OobWeightDistributor
    {
        public static void DistributeWeights(OrderOfBattleVM viewModel)
        {
            var settings = Settings.Instance;
            if (!FormationAssignmentResolver.HasCustomDefaults(settings))
                return;

            var assignmentPlan = OobDefaultAssignmentPlan.ForPlayerRoster(settings);
            int[,] classCounts = GetClassCounts(assignmentPlan);
            AddMainHero(classCounts, settings);
            int[] totalByClass = GetTotals(classCounts);

            foreach (var item in viewModel.FormationsFirstHalf.Concat(viewModel.FormationsSecondHalf))
            {
                if (item.Formation == null)
                    continue;

                int formationIndex = item.Formation.Index;
                foreach (var classViewModel in item.Classes.Where(classViewModel => !classViewModel.IsUnset))
                    ApplyWeightAndLock(classViewModel, formationIndex, classCounts, totalByClass, settings);
            }
        }

        private static int[,] GetClassCounts(OobDefaultAssignmentPlan assignmentPlan)
        {
            int[,] classCounts = new int[8, 7];
            for (int formationIndex = 0; formationIndex < 8; formationIndex++)
            for (int classIndex = 0; classIndex < 7; classIndex++)
                classCounts[formationIndex, classIndex] = assignmentPlan.GetFormationClassCount(
                    formationIndex, (DeploymentFormationClass)classIndex);
            return classCounts;
        }

        private static void AddMainHero(int[,] classCounts, Settings? settings)
        {
            var mainHero = Hero.MainHero;
            if (mainHero == null)
                return;

            int[] assignedIndices = FormationAssignmentStore.GetAssignments(mainHero.CharacterObject.StringId);
            DeploymentFormationClass heroClass = RefreshFormationPatch.MapToDeploymentClass(mainHero.CharacterObject.DefaultFormationClass);
            if (assignedIndices.Length > 0)
            {
                for (int formationIndex = 0; formationIndex < 8; formationIndex++)
                {
                    int assignedCount = FormationAssignmentResolver.GetAssignedCountForFormation(
                        mainHero.CharacterObject, 1, formationIndex, settings);
                    if ((int)heroClass is >= 0 and < 7)
                        classCounts[formationIndex, (int)heroClass] += assignedCount;
                }
            }
            else
            {
                classCounts[2, (int)DeploymentFormationClass.Cavalry] += 1;
            }
        }

        private static int[] GetTotals(int[,] classCounts)
        {
            int[] totals = new int[7];
            for (int formationIndex = 0; formationIndex < 8; formationIndex++)
            for (int classIndex = 0; classIndex < 7; classIndex++)
                totals[classIndex] += classCounts[formationIndex, classIndex];
            return totals;
        }

        private static void ApplyWeightAndLock(
            OrderOfBattleFormationClassVM classViewModel,
            int formationIndex,
            int[,] classCounts,
            int[] totalByClass,
            Settings? settings)
        {
            DeploymentFormationClass deploymentClass = RefreshFormationPatch.MapToDeploymentClass(classViewModel.Class);
            int classIndex = (int)deploymentClass;
            if (classIndex is < 0 or >= 7)
                return;

            int targetWeight = totalByClass[classIndex] > 0
                ? (int)Math.Round((double)classCounts[formationIndex, classIndex] / totalByClass[classIndex] * 100)
                : 0;
            classViewModel.Weight = targetWeight;

            if (settings?.LockManagedOobSliders == true && classCounts[formationIndex, classIndex] > 0)
                classViewModel.SetWeightAdjustmentLock(true);
        }
    }
}
