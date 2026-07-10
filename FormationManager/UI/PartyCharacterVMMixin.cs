using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using FormationManager.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;

namespace FormationManager.UI
{
    /// <summary>
    /// Adds quick formation controls to a party row and supplies the dedicated
    /// target-and-weight editor that appears in its native expanded section.
    /// </summary>
    [ViewModelMixin(nameof(PartyCharacterVM.RefreshValues))]
    internal sealed class PartyCharacterVMMixin : BaseViewModelMixin<PartyCharacterVM>
    {
        private static readonly string[] Labels = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        private static readonly ConditionalWeakTable<PartyCharacterVM, PartyCharacterVMMixin> Instances = new();
        private readonly string[] _assignedTexts = new string[8];
        private readonly string[] _weightTexts = new string[8];
        private string _formationLabel = "—";
        private string _secondaryFormationLabel = "+";
        private bool _isFormationBadgeVisible;
        private bool _isSecondaryFormationBadgeVisible;
        private int _selectedCustomFormationIndex = -1;

        public PartyCharacterVMMixin(PartyCharacterVM vm) : base(vm)
        {
            Instances.Remove(vm);
            Instances.Add(vm, this);
            vm.PropertyChanged += OnViewModelPropertyChanged;
            Refresh();
        }

        [DataSourceProperty]
        public string FormationLabel
        {
            get => _formationLabel;
            set { if (_formationLabel != value) { _formationLabel = value; OnPropertyChanged(nameof(FormationLabel)); } }
        }

        [DataSourceProperty]
        public bool IsFormationBadgeVisible
        {
            get => _isFormationBadgeVisible;
            set { if (_isFormationBadgeVisible != value) { _isFormationBadgeVisible = value; OnPropertyChanged(nameof(IsFormationBadgeVisible)); } }
        }

        [DataSourceProperty]
        public string SecondaryFormationLabel
        {
            get => _secondaryFormationLabel;
            set { if (_secondaryFormationLabel != value) { _secondaryFormationLabel = value; OnPropertyChanged(nameof(SecondaryFormationLabel)); } }
        }

        [DataSourceProperty]
        public bool IsSecondaryFormationBadgeVisible
        {
            get => _isSecondaryFormationBadgeVisible;
            set { if (_isSecondaryFormationBadgeVisible != value) { _isSecondaryFormationBadgeVisible = value; OnPropertyChanged(nameof(IsSecondaryFormationBadgeVisible)); } }
        }

        [DataSourceProperty] public bool IsCustomEditorVisible => IsFormationBadgeVisible;
        [DataSourceProperty] public bool IsFormationEditorVisible => IsFormationBadgeVisible && (ViewModel?.IsSelected ?? false);
        [DataSourceProperty] public bool IsFormationEditorSliderVisible => IsFormationEditorVisible && _selectedCustomFormationIndex >= 0;
        [DataSourceProperty] public bool IsCustomFormationSelected => _selectedCustomFormationIndex >= 0;
        [DataSourceProperty] public bool IsFormation1Selected => _selectedCustomFormationIndex == 0;
        [DataSourceProperty] public bool IsFormation2Selected => _selectedCustomFormationIndex == 1;
        [DataSourceProperty] public bool IsFormation3Selected => _selectedCustomFormationIndex == 2;
        [DataSourceProperty] public bool IsFormation4Selected => _selectedCustomFormationIndex == 3;
        [DataSourceProperty] public bool IsFormation5Selected => _selectedCustomFormationIndex == 4;
        [DataSourceProperty] public bool IsFormation6Selected => _selectedCustomFormationIndex == 5;
        [DataSourceProperty] public bool IsFormation7Selected => _selectedCustomFormationIndex == 6;
        [DataSourceProperty] public bool IsFormation8Selected => _selectedCustomFormationIndex == 7;
        [DataSourceProperty] public string Formation1AssignedText => _assignedTexts[0];
        [DataSourceProperty] public string Formation2AssignedText => _assignedTexts[1];
        [DataSourceProperty] public string Formation3AssignedText => _assignedTexts[2];
        [DataSourceProperty] public string Formation4AssignedText => _assignedTexts[3];
        [DataSourceProperty] public string Formation5AssignedText => _assignedTexts[4];
        [DataSourceProperty] public string Formation6AssignedText => _assignedTexts[5];
        [DataSourceProperty] public string Formation7AssignedText => _assignedTexts[6];
        [DataSourceProperty] public string Formation8AssignedText => _assignedTexts[7];
        [DataSourceProperty] public string Formation1WeightText => _weightTexts[0];
        [DataSourceProperty] public string Formation2WeightText => _weightTexts[1];
        [DataSourceProperty] public string Formation3WeightText => _weightTexts[2];
        [DataSourceProperty] public string Formation4WeightText => _weightTexts[3];
        [DataSourceProperty] public string Formation5WeightText => _weightTexts[4];
        [DataSourceProperty] public string Formation6WeightText => _weightTexts[5];
        [DataSourceProperty] public string Formation7WeightText => _weightTexts[6];
        [DataSourceProperty] public string Formation8WeightText => _weightTexts[7];

        [DataSourceProperty] public string SelectedFormationLabel => _selectedCustomFormationIndex < 0 ? "Select a formation card to edit it" : $"Formation {Labels[_selectedCustomFormationIndex + 1]}";
        [DataSourceProperty] public string SelectedFormationTargetText => $"Target: {SelectedFormationTarget}";
        [DataSourceProperty] public string SelectedFormationWeightText => $"Weight: {SelectedFormationWeight}";
        [DataSourceProperty] public int SelectedFormationTargetMax => GetSelectedTargetMaximum();

        [DataSourceProperty]
        public int SelectedFormationTarget
        {
            get => GetSelectedValue(FormationAssignmentStore.GetCustomTarget);
            set => SetSelectedTarget(value);
        }

        [DataSourceProperty]
        public int SelectedFormationWeight
        {
            get => GetSelectedValue(FormationAssignmentStore.GetCustomWeight);
            set => SetSelectedWeight(value);
        }

        [DataSourceMethod]
        public void ExecuteCycleFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
            {
                ShowCustomPlanHint();
                return;
            }

            int next = FormationAssignmentStore.GetAssignment(character.StringId) + 1;
            if (next > 7) next = -1;
            if (next < 0) FormationAssignmentStore.ClearAssignment(character.StringId);
            else FormationAssignmentStore.SetAssignment(character.StringId, next);
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteClearFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
            {
                ShowCustomPlanHint();
                return;
            }
            FormationAssignmentStore.ClearAssignment(character.StringId);
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteCycleSecondaryFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
            {
                FormationAssignmentStore.ClearDeploymentPlan(character.StringId);
                FormationAssignmentStore.Save();
                Refresh();
                return;
            }

            int primary = FormationAssignmentStore.GetAssignment(character.StringId);
            if (primary < 0)
            {
                FormationAssignmentStore.SetAssignment(character.StringId, 0);
            }
            else
            {
                int next = FormationAssignmentStore.GetSecondaryAssignment(character.StringId);
                do { next++; if (next > 7) { next = -1; break; } } while (next == primary);
                if (next < 0) FormationAssignmentStore.ClearSecondaryAssignment(character.StringId);
                else FormationAssignmentStore.SetSecondaryAssignment(character.StringId, next);
            }
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteClearSecondaryFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
                FormationAssignmentStore.ClearDeploymentPlan(character.StringId);
            else
                FormationAssignmentStore.ClearSecondaryAssignment(character.StringId);
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteEnableEvenSplit()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            FormationAssignmentStore.SetEvenSplit(character.StringId, Enumerable.Range(0, 8));
            _selectedCustomFormationIndex = -1;
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod] public void ExecuteSelectCustomFormation1() => SelectCustomFormation(0);
        [DataSourceMethod] public void ExecuteSelectCustomFormation2() => SelectCustomFormation(1);
        [DataSourceMethod] public void ExecuteSelectCustomFormation3() => SelectCustomFormation(2);
        [DataSourceMethod] public void ExecuteSelectCustomFormation4() => SelectCustomFormation(3);
        [DataSourceMethod] public void ExecuteSelectCustomFormation5() => SelectCustomFormation(4);
        [DataSourceMethod] public void ExecuteSelectCustomFormation6() => SelectCustomFormation(5);
        [DataSourceMethod] public void ExecuteSelectCustomFormation7() => SelectCustomFormation(6);
        [DataSourceMethod] public void ExecuteSelectCustomFormation8() => SelectCustomFormation(7);

        private void SelectCustomFormation(int formationIndex)
        {
            _selectedCustomFormationIndex = formationIndex;
            NotifyEditorSelectionChanged();
        }

        private void SetSelectedTarget(int value)
        {
            var character = ViewModel?.Character;
            if (character == null || _selectedCustomFormationIndex < 0) return;
            FormationAssignmentStore.SetCustomTarget(character.StringId, _selectedCustomFormationIndex, value, GetReadyTroopCount());
            FormationAssignmentStore.Save();
            RefreshEditorValues();
        }

        private void SetSelectedWeight(int value)
        {
            var character = ViewModel?.Character;
            if (character == null || _selectedCustomFormationIndex < 0) return;
            FormationAssignmentStore.SetCustomWeight(character.StringId, _selectedCustomFormationIndex, value);
            FormationAssignmentStore.Save();
            RefreshEditorValues();
        }

        private int GetSelectedValue(Func<string, int, int> getter)
        {
            var character = ViewModel?.Character;
            return character == null || _selectedCustomFormationIndex < 0 ? 0 : getter(character.StringId, _selectedCustomFormationIndex);
        }

        private int GetSelectedTargetMaximum()
        {
            var character = ViewModel?.Character;
            if (character == null || _selectedCustomFormationIndex < 0) return 0;
            int otherTargets = Enumerable.Range(0, 8).Where(index => index != _selectedCustomFormationIndex)
                .Sum(index => FormationAssignmentStore.GetCustomTarget(character.StringId, index));
            return Math.Max(0, GetReadyTroopCount() - otherTargets);
        }

        private int GetReadyTroopCount()
        {
            var character = ViewModel?.Character;
            var roster = MobileParty.MainParty?.MemberRoster;
            if (character == null || roster == null) return 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character == character)
                    return Math.Max(0, element.Number - element.WoundedNumber);
            }
            return 0;
        }

        private static void ShowCustomPlanHint()
            => InformationManager.DisplayMessage(new InformationMessage("[Formation Manager] Custom formation allocation: expand this troop row to edit it. Press X to clear it."));

        private void Refresh()
        {
            bool modEnabled = Settings.Instance?.ModEnabled ?? true;
            IsFormationBadgeVisible = modEnabled && ViewModel != null && !ViewModel.IsMainHero && !ViewModel.IsPrisoner;
            if (ViewModel?.Character == null || !modEnabled)
            {
                FormationLabel = "—";
                SecondaryFormationLabel = "+";
                IsSecondaryFormationBadgeVisible = false;
                _selectedCustomFormationIndex = -1;
                RefreshEditorValues();
                return;
            }

            string troopId = ViewModel.Character.StringId;
            bool hasDeploymentPlan = FormationAssignmentStore.TryGetDeploymentPlan(troopId, out _);
            int primary = FormationAssignmentStore.GetAssignment(troopId);
            int secondary = FormationAssignmentStore.GetSecondaryAssignment(troopId);
            FormationLabel = hasDeploymentPlan ? "C" : primary is >= 0 and <= 7 ? Labels[primary + 1] : "—";
            SecondaryFormationLabel = hasDeploymentPlan ? "X" : secondary is >= 0 and <= 7 ? Labels[secondary + 1] : "+";
            IsSecondaryFormationBadgeVisible = IsFormationBadgeVisible && (primary >= 0 || hasDeploymentPlan);
            RefreshEditorValues();
        }

        private void RefreshEditorValues()
        {
            var character = ViewModel?.Character;
            var allocation = character == null
                ? new System.Collections.Generic.Dictionary<int, int>()
                : FormationAssignmentResolver.GetAllocatedFormationCounts(character, GetReadyTroopCount(), Settings.Instance);

            for (int i = 0; i < 8; i++)
            {
                int assigned = allocation.TryGetValue(i, out int count) ? count : 0;
                int weight = character == null ? 0 : FormationAssignmentStore.GetCustomWeight(character.StringId, i);
                _assignedTexts[i] = $"Assigned {assigned}";
                _weightTexts[i] = $"Weight {weight}";
                OnPropertyChanged($"Formation{i + 1}AssignedText");
                OnPropertyChanged($"Formation{i + 1}WeightText");
            }
            NotifyEditorSelectionChanged();
        }

        private void NotifyEditorSelectionChanged()
        {
            OnPropertyChanged(nameof(IsCustomFormationSelected));
            for (int i = 1; i <= 8; i++) OnPropertyChanged($"IsFormation{i}Selected");
            OnPropertyChanged(nameof(SelectedFormationLabel));
            OnPropertyChanged(nameof(SelectedFormationTarget));
            OnPropertyChanged(nameof(SelectedFormationTargetMax));
            OnPropertyChanged(nameof(SelectedFormationTargetText));
            OnPropertyChanged(nameof(SelectedFormationWeight));
            OnPropertyChanged(nameof(SelectedFormationWeightText));
            OnPropertyChanged(nameof(IsCustomEditorVisible));
            OnPropertyChanged(nameof(IsFormationEditorVisible));
            OnPropertyChanged(nameof(IsFormationEditorSliderVisible));
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PartyCharacterVM.IsSelected))
                HandleSelectionChanged();
        }

        internal static void NotifySelectionChanged(PartyCharacterVM viewModel)
        {
            if (Instances.TryGetValue(viewModel, out var mixin))
                mixin.HandleSelectionChanged();
        }

        private void HandleSelectionChanged()
        {
            if (!(ViewModel?.IsSelected ?? false))
                _selectedCustomFormationIndex = -1;
            OnPropertyChanged(nameof(IsFormationEditorVisible));
            OnPropertyChanged(nameof(IsFormationEditorSliderVisible));
            NotifyEditorSelectionChanged();
        }

        public override void OnRefresh() => Refresh();

        public override void OnFinalize()
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                Instances.Remove(ViewModel);
            }
        }
    }
}
