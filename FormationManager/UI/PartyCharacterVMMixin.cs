using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using FormationManager.Data;
using FormationManager.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using TroopClassifier;

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
        private static readonly List<WeakReference<PartyCharacterVMMixin>> ActiveMixins = new();
        private static bool _autosaveHintShown;
        private readonly string[] _targetTexts = new string[8];
        private readonly string[] _assignedTexts = new string[8];
        private readonly string[] _weightTexts = new string[8];
        private readonly bool[] _editorFormationActive = new bool[8];
        private readonly AdvancedFormationPlanDraft _draft = new();
        private string _formationLabel = "—";
        private string _secondaryFormationLabel = "+";
        private bool _isFormationBadgeVisible;
        private bool _isSecondaryFormationBadgeVisible;
        private int _selectedCustomFormationIndex = -1;

        public PartyCharacterVMMixin(PartyCharacterVM vm) : base(vm)
        {
            Instances.Remove(vm);
            Instances.Add(vm, this);
            ActiveMixins.RemoveAll(reference => !reference.TryGetTarget(out var existing) || ReferenceEquals(existing, this));
            ActiveMixins.Add(new WeakReference<PartyCharacterVMMixin>(this));
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

        [DataSourceProperty] public bool IsRolePlanBadgeVisible => IsPlayerHeroRow && FormationAssignmentStore.HasAnyRoleAssignments;
        [DataSourceProperty] public bool IsRolePlanEditorVisible => IsPlayerHeroRow && (ViewModel?.IsSelected ?? false);
        [DataSourceProperty] public string LightInfantryRoleFormation1Label => GetRoleFormationLabel(TroopRole.LightInfantry, 0);
        [DataSourceProperty] public string LightInfantryRoleFormation2Label => GetRoleFormationLabel(TroopRole.LightInfantry, 1);
        [DataSourceProperty] public string LightInfantryRoleFormation3Label => GetRoleFormationLabel(TroopRole.LightInfantry, 2);
        [DataSourceProperty] public string LightInfantryRoleFormation4Label => GetRoleFormationLabel(TroopRole.LightInfantry, 3);
        [DataSourceProperty] public string LightInfantryRoleFormation5Label => GetRoleFormationLabel(TroopRole.LightInfantry, 4);
        [DataSourceProperty] public string LightInfantryRoleFormation6Label => GetRoleFormationLabel(TroopRole.LightInfantry, 5);
        [DataSourceProperty] public string LightInfantryRoleFormation7Label => GetRoleFormationLabel(TroopRole.LightInfantry, 6);
        [DataSourceProperty] public string LightInfantryRoleFormation8Label => GetRoleFormationLabel(TroopRole.LightInfantry, 7);
        [DataSourceProperty] public string ShieldInfantryRoleFormation1Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 0);
        [DataSourceProperty] public string ShieldInfantryRoleFormation2Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 1);
        [DataSourceProperty] public string ShieldInfantryRoleFormation3Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 2);
        [DataSourceProperty] public string ShieldInfantryRoleFormation4Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 3);
        [DataSourceProperty] public string ShieldInfantryRoleFormation5Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 4);
        [DataSourceProperty] public string ShieldInfantryRoleFormation6Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 5);
        [DataSourceProperty] public string ShieldInfantryRoleFormation7Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 6);
        [DataSourceProperty] public string ShieldInfantryRoleFormation8Label => GetRoleFormationLabel(TroopRole.ShieldInfantry, 7);
        [DataSourceProperty] public string ShockInfantryRoleFormation1Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 0);
        [DataSourceProperty] public string ShockInfantryRoleFormation2Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 1);
        [DataSourceProperty] public string ShockInfantryRoleFormation3Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 2);
        [DataSourceProperty] public string ShockInfantryRoleFormation4Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 3);
        [DataSourceProperty] public string ShockInfantryRoleFormation5Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 4);
        [DataSourceProperty] public string ShockInfantryRoleFormation6Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 5);
        [DataSourceProperty] public string ShockInfantryRoleFormation7Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 6);
        [DataSourceProperty] public string ShockInfantryRoleFormation8Label => GetRoleFormationLabel(TroopRole.ShockInfantry, 7);
        [DataSourceProperty] public string PikeInfantryRoleFormation1Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 0);
        [DataSourceProperty] public string PikeInfantryRoleFormation2Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 1);
        [DataSourceProperty] public string PikeInfantryRoleFormation3Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 2);
        [DataSourceProperty] public string PikeInfantryRoleFormation4Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 3);
        [DataSourceProperty] public string PikeInfantryRoleFormation5Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 4);
        [DataSourceProperty] public string PikeInfantryRoleFormation6Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 5);
        [DataSourceProperty] public string PikeInfantryRoleFormation7Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 6);
        [DataSourceProperty] public string PikeInfantryRoleFormation8Label => GetRoleFormationLabel(TroopRole.PikeInfantry, 7);
        [DataSourceProperty] public string SkirmisherRoleFormation1Label => GetRoleFormationLabel(TroopRole.Skirmisher, 0);
        [DataSourceProperty] public string SkirmisherRoleFormation2Label => GetRoleFormationLabel(TroopRole.Skirmisher, 1);
        [DataSourceProperty] public string SkirmisherRoleFormation3Label => GetRoleFormationLabel(TroopRole.Skirmisher, 2);
        [DataSourceProperty] public string SkirmisherRoleFormation4Label => GetRoleFormationLabel(TroopRole.Skirmisher, 3);
        [DataSourceProperty] public string SkirmisherRoleFormation5Label => GetRoleFormationLabel(TroopRole.Skirmisher, 4);
        [DataSourceProperty] public string SkirmisherRoleFormation6Label => GetRoleFormationLabel(TroopRole.Skirmisher, 5);
        [DataSourceProperty] public string SkirmisherRoleFormation7Label => GetRoleFormationLabel(TroopRole.Skirmisher, 6);
        [DataSourceProperty] public string SkirmisherRoleFormation8Label => GetRoleFormationLabel(TroopRole.Skirmisher, 7);
        [DataSourceProperty] public string FootArcherRoleFormation1Label => GetRoleFormationLabel(TroopRole.FootArcher, 0);
        [DataSourceProperty] public string FootArcherRoleFormation2Label => GetRoleFormationLabel(TroopRole.FootArcher, 1);
        [DataSourceProperty] public string FootArcherRoleFormation3Label => GetRoleFormationLabel(TroopRole.FootArcher, 2);
        [DataSourceProperty] public string FootArcherRoleFormation4Label => GetRoleFormationLabel(TroopRole.FootArcher, 3);
        [DataSourceProperty] public string FootArcherRoleFormation5Label => GetRoleFormationLabel(TroopRole.FootArcher, 4);
        [DataSourceProperty] public string FootArcherRoleFormation6Label => GetRoleFormationLabel(TroopRole.FootArcher, 5);
        [DataSourceProperty] public string FootArcherRoleFormation7Label => GetRoleFormationLabel(TroopRole.FootArcher, 6);
        [DataSourceProperty] public string FootArcherRoleFormation8Label => GetRoleFormationLabel(TroopRole.FootArcher, 7);
        [DataSourceProperty] public string CrossbowmanRoleFormation1Label => GetRoleFormationLabel(TroopRole.Crossbowman, 0);
        [DataSourceProperty] public string CrossbowmanRoleFormation2Label => GetRoleFormationLabel(TroopRole.Crossbowman, 1);
        [DataSourceProperty] public string CrossbowmanRoleFormation3Label => GetRoleFormationLabel(TroopRole.Crossbowman, 2);
        [DataSourceProperty] public string CrossbowmanRoleFormation4Label => GetRoleFormationLabel(TroopRole.Crossbowman, 3);
        [DataSourceProperty] public string CrossbowmanRoleFormation5Label => GetRoleFormationLabel(TroopRole.Crossbowman, 4);
        [DataSourceProperty] public string CrossbowmanRoleFormation6Label => GetRoleFormationLabel(TroopRole.Crossbowman, 5);
        [DataSourceProperty] public string CrossbowmanRoleFormation7Label => GetRoleFormationLabel(TroopRole.Crossbowman, 6);
        [DataSourceProperty] public string CrossbowmanRoleFormation8Label => GetRoleFormationLabel(TroopRole.Crossbowman, 7);
        [DataSourceProperty] public string MeleeCavalryRoleFormation1Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 0);
        [DataSourceProperty] public string MeleeCavalryRoleFormation2Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 1);
        [DataSourceProperty] public string MeleeCavalryRoleFormation3Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 2);
        [DataSourceProperty] public string MeleeCavalryRoleFormation4Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 3);
        [DataSourceProperty] public string MeleeCavalryRoleFormation5Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 4);
        [DataSourceProperty] public string MeleeCavalryRoleFormation6Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 5);
        [DataSourceProperty] public string MeleeCavalryRoleFormation7Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 6);
        [DataSourceProperty] public string MeleeCavalryRoleFormation8Label => GetRoleFormationLabel(TroopRole.MeleeCavalry, 7);
        [DataSourceProperty] public string HorseArcherRoleFormation1Label => GetRoleFormationLabel(TroopRole.HorseArcher, 0);
        [DataSourceProperty] public string HorseArcherRoleFormation2Label => GetRoleFormationLabel(TroopRole.HorseArcher, 1);
        [DataSourceProperty] public string HorseArcherRoleFormation3Label => GetRoleFormationLabel(TroopRole.HorseArcher, 2);
        [DataSourceProperty] public string HorseArcherRoleFormation4Label => GetRoleFormationLabel(TroopRole.HorseArcher, 3);
        [DataSourceProperty] public string HorseArcherRoleFormation5Label => GetRoleFormationLabel(TroopRole.HorseArcher, 4);
        [DataSourceProperty] public string HorseArcherRoleFormation6Label => GetRoleFormationLabel(TroopRole.HorseArcher, 5);
        [DataSourceProperty] public string HorseArcherRoleFormation7Label => GetRoleFormationLabel(TroopRole.HorseArcher, 6);
        [DataSourceProperty] public string HorseArcherRoleFormation8Label => GetRoleFormationLabel(TroopRole.HorseArcher, 7);

        [DataSourceProperty] public bool IsCustomEditorVisible => IsFormationBadgeVisible;
        [DataSourceProperty] public bool IsFormationEditorVisible => IsFormationBadgeVisible &&
            (Settings.Instance?.ShowAdvancedFormationEditor ?? true) && (ViewModel?.IsSelected ?? false);
        [DataSourceProperty] public bool HasAdvancedFormationPlan => AdvancedPlansEnabled && ViewModel?.Character != null &&
            FormationAssignmentStore.TryGetDeploymentPlan(ViewModel.Character.StringId, out _);
        [DataSourceProperty] public bool CanRestoreAdvancedFormationPlan => AdvancedPlansEnabled && ViewModel?.Character != null &&
            FormationAssignmentStore.CanRestoreDeploymentPlan(ViewModel.Character.StringId);
        [DataSourceProperty] public bool HasUnsavedAdvancedFormationChanges => _draft.IsDirty;
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
        [DataSourceProperty] public bool IsFormation1Active => _editorFormationActive[0];
        [DataSourceProperty] public bool IsFormation2Active => _editorFormationActive[1];
        [DataSourceProperty] public bool IsFormation3Active => _editorFormationActive[2];
        [DataSourceProperty] public bool IsFormation4Active => _editorFormationActive[3];
        [DataSourceProperty] public bool IsFormation5Active => _editorFormationActive[4];
        [DataSourceProperty] public bool IsFormation6Active => _editorFormationActive[5];
        [DataSourceProperty] public bool IsFormation7Active => _editorFormationActive[6];
        [DataSourceProperty] public bool IsFormation8Active => _editorFormationActive[7];
        [DataSourceProperty] public string Formation1ButtonLabel => GetFormationButtonLabel(0, "I");
        [DataSourceProperty] public string Formation2ButtonLabel => GetFormationButtonLabel(1, "II");
        [DataSourceProperty] public string Formation3ButtonLabel => GetFormationButtonLabel(2, "III");
        [DataSourceProperty] public string Formation4ButtonLabel => GetFormationButtonLabel(3, "IV");
        [DataSourceProperty] public string Formation5ButtonLabel => GetFormationButtonLabel(4, "V");
        [DataSourceProperty] public string Formation6ButtonLabel => GetFormationButtonLabel(5, "VI");
        [DataSourceProperty] public string Formation7ButtonLabel => GetFormationButtonLabel(6, "VII");
        [DataSourceProperty] public string Formation8ButtonLabel => GetFormationButtonLabel(7, "VIII");
        [DataSourceProperty] public string Formation1TargetText => _targetTexts[0];
        [DataSourceProperty] public string Formation2TargetText => _targetTexts[1];
        [DataSourceProperty] public string Formation3TargetText => _targetTexts[2];
        [DataSourceProperty] public string Formation4TargetText => _targetTexts[3];
        [DataSourceProperty] public string Formation5TargetText => _targetTexts[4];
        [DataSourceProperty] public string Formation6TargetText => _targetTexts[5];
        [DataSourceProperty] public string Formation7TargetText => _targetTexts[6];
        [DataSourceProperty] public string Formation8TargetText => _targetTexts[7];
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

        [DataSourceProperty] public string SelectedFormationLabel => _selectedCustomFormationIndex < 0
            ? "Select an active formation, or click + Add formation"
            : $"Formation {Labels[_selectedCustomFormationIndex + 1]} — click its numeral again to remove";
        [DataSourceProperty] public string SelectedFormationTargetText => $"Target: {SelectedFormationTarget}";
        [DataSourceProperty] public string SelectedFormationWeightText => $"Weight: {SelectedFormationWeight}";
        [DataSourceProperty] public int SelectedFormationTargetMax => GetSelectedTargetMaximum();

        [DataSourceProperty]
        public int SelectedFormationTarget
        {
            get => _selectedCustomFormationIndex < 0 ? 0 : _draft.GetTarget(_selectedCustomFormationIndex);
            set => SetSelectedTarget(value);
        }

        [DataSourceProperty]
        public int SelectedFormationWeight
        {
            get => _selectedCustomFormationIndex < 0 ? 0 : _draft.GetWeight(_selectedCustomFormationIndex);
            set => SetSelectedWeight(value);
        }

        [DataSourceMethod]
        public void ExecuteCycleFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (AdvancedPlansEnabled && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
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

        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation1() => ToggleRoleFormation(TroopRole.LightInfantry, 0);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation2() => ToggleRoleFormation(TroopRole.LightInfantry, 1);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation3() => ToggleRoleFormation(TroopRole.LightInfantry, 2);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation4() => ToggleRoleFormation(TroopRole.LightInfantry, 3);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation5() => ToggleRoleFormation(TroopRole.LightInfantry, 4);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation6() => ToggleRoleFormation(TroopRole.LightInfantry, 5);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation7() => ToggleRoleFormation(TroopRole.LightInfantry, 6);
        [DataSourceMethod] public void ExecuteToggleLightInfantryRoleFormation8() => ToggleRoleFormation(TroopRole.LightInfantry, 7);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation1() => ToggleRoleFormation(TroopRole.ShieldInfantry, 0);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation2() => ToggleRoleFormation(TroopRole.ShieldInfantry, 1);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation3() => ToggleRoleFormation(TroopRole.ShieldInfantry, 2);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation4() => ToggleRoleFormation(TroopRole.ShieldInfantry, 3);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation5() => ToggleRoleFormation(TroopRole.ShieldInfantry, 4);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation6() => ToggleRoleFormation(TroopRole.ShieldInfantry, 5);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation7() => ToggleRoleFormation(TroopRole.ShieldInfantry, 6);
        [DataSourceMethod] public void ExecuteToggleShieldInfantryRoleFormation8() => ToggleRoleFormation(TroopRole.ShieldInfantry, 7);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation1() => ToggleRoleFormation(TroopRole.ShockInfantry, 0);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation2() => ToggleRoleFormation(TroopRole.ShockInfantry, 1);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation3() => ToggleRoleFormation(TroopRole.ShockInfantry, 2);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation4() => ToggleRoleFormation(TroopRole.ShockInfantry, 3);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation5() => ToggleRoleFormation(TroopRole.ShockInfantry, 4);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation6() => ToggleRoleFormation(TroopRole.ShockInfantry, 5);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation7() => ToggleRoleFormation(TroopRole.ShockInfantry, 6);
        [DataSourceMethod] public void ExecuteToggleShockInfantryRoleFormation8() => ToggleRoleFormation(TroopRole.ShockInfantry, 7);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation1() => ToggleRoleFormation(TroopRole.PikeInfantry, 0);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation2() => ToggleRoleFormation(TroopRole.PikeInfantry, 1);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation3() => ToggleRoleFormation(TroopRole.PikeInfantry, 2);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation4() => ToggleRoleFormation(TroopRole.PikeInfantry, 3);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation5() => ToggleRoleFormation(TroopRole.PikeInfantry, 4);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation6() => ToggleRoleFormation(TroopRole.PikeInfantry, 5);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation7() => ToggleRoleFormation(TroopRole.PikeInfantry, 6);
        [DataSourceMethod] public void ExecuteTogglePikeInfantryRoleFormation8() => ToggleRoleFormation(TroopRole.PikeInfantry, 7);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation1() => ToggleRoleFormation(TroopRole.Skirmisher, 0);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation2() => ToggleRoleFormation(TroopRole.Skirmisher, 1);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation3() => ToggleRoleFormation(TroopRole.Skirmisher, 2);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation4() => ToggleRoleFormation(TroopRole.Skirmisher, 3);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation5() => ToggleRoleFormation(TroopRole.Skirmisher, 4);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation6() => ToggleRoleFormation(TroopRole.Skirmisher, 5);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation7() => ToggleRoleFormation(TroopRole.Skirmisher, 6);
        [DataSourceMethod] public void ExecuteToggleSkirmisherRoleFormation8() => ToggleRoleFormation(TroopRole.Skirmisher, 7);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation1() => ToggleRoleFormation(TroopRole.FootArcher, 0);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation2() => ToggleRoleFormation(TroopRole.FootArcher, 1);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation3() => ToggleRoleFormation(TroopRole.FootArcher, 2);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation4() => ToggleRoleFormation(TroopRole.FootArcher, 3);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation5() => ToggleRoleFormation(TroopRole.FootArcher, 4);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation6() => ToggleRoleFormation(TroopRole.FootArcher, 5);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation7() => ToggleRoleFormation(TroopRole.FootArcher, 6);
        [DataSourceMethod] public void ExecuteToggleFootArcherRoleFormation8() => ToggleRoleFormation(TroopRole.FootArcher, 7);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation1() => ToggleRoleFormation(TroopRole.Crossbowman, 0);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation2() => ToggleRoleFormation(TroopRole.Crossbowman, 1);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation3() => ToggleRoleFormation(TroopRole.Crossbowman, 2);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation4() => ToggleRoleFormation(TroopRole.Crossbowman, 3);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation5() => ToggleRoleFormation(TroopRole.Crossbowman, 4);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation6() => ToggleRoleFormation(TroopRole.Crossbowman, 5);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation7() => ToggleRoleFormation(TroopRole.Crossbowman, 6);
        [DataSourceMethod] public void ExecuteToggleCrossbowmanRoleFormation8() => ToggleRoleFormation(TroopRole.Crossbowman, 7);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation1() => ToggleRoleFormation(TroopRole.MeleeCavalry, 0);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation2() => ToggleRoleFormation(TroopRole.MeleeCavalry, 1);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation3() => ToggleRoleFormation(TroopRole.MeleeCavalry, 2);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation4() => ToggleRoleFormation(TroopRole.MeleeCavalry, 3);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation5() => ToggleRoleFormation(TroopRole.MeleeCavalry, 4);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation6() => ToggleRoleFormation(TroopRole.MeleeCavalry, 5);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation7() => ToggleRoleFormation(TroopRole.MeleeCavalry, 6);
        [DataSourceMethod] public void ExecuteToggleMeleeCavalryRoleFormation8() => ToggleRoleFormation(TroopRole.MeleeCavalry, 7);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation1() => ToggleRoleFormation(TroopRole.HorseArcher, 0);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation2() => ToggleRoleFormation(TroopRole.HorseArcher, 1);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation3() => ToggleRoleFormation(TroopRole.HorseArcher, 2);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation4() => ToggleRoleFormation(TroopRole.HorseArcher, 3);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation5() => ToggleRoleFormation(TroopRole.HorseArcher, 4);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation6() => ToggleRoleFormation(TroopRole.HorseArcher, 5);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation7() => ToggleRoleFormation(TroopRole.HorseArcher, 6);
        [DataSourceMethod] public void ExecuteToggleHorseArcherRoleFormation8() => ToggleRoleFormation(TroopRole.HorseArcher, 7);

        private bool IsPlayerHeroRow => ViewModel?.Character is CharacterObject character &&
                                        character.HeroObject != null && ReferenceEquals(character.HeroObject, Hero.MainHero);

        private static string GetRoleFormationLabel(TroopRole role, int formationIndex)
            => FormationAssignmentStore.GetRoleAssignments(role).Contains(formationIndex)
                ? Labels[formationIndex + 1]
                : "+";

        private void ToggleRoleFormation(TroopRole role, int formationIndex)
        {
            var assignments = FormationAssignmentStore.GetRoleAssignments(role).ToList();
            if (!assignments.Remove(formationIndex))
                assignments.Add(formationIndex);

            FormationAssignmentStore.SetRoleAssignments(role, assignments);
            FormationAssignmentStore.Save();
            RefreshRolePlanBindings();
        }

        private void RefreshRolePlanBindings()
        {
            OnPropertyChanged(nameof(IsRolePlanBadgeVisible));
            foreach (string role in new[] { "LightInfantry", "ShieldInfantry", "ShockInfantry", "PikeInfantry", "Skirmisher", "FootArcher", "Crossbowman", "MeleeCavalry", "HorseArcher" })
            {
                for (int formation = 1; formation <= 8; formation++)
                    OnPropertyChanged($"{role}RoleFormation{formation}Label");
            }
        }

        [DataSourceMethod]
        public void ExecuteClearFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (AdvancedPlansEnabled && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
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
            if (AdvancedPlansEnabled && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
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
            if (AdvancedPlansEnabled && FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out _))
                FormationAssignmentStore.ClearDeploymentPlan(character.StringId);
            else
                FormationAssignmentStore.ClearSecondaryAssignment(character.StringId);
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteClearAdvancedFormationPlan()
        {
            if (ViewModel?.Character == null) return;
            EnsureDraftInitialized();
            _draft.Clear();
            _selectedCustomFormationIndex = -1;
            MarkDraftChanged();
            RefreshEditorValues();
        }

        [DataSourceMethod]
        public void ExecuteRestoreAdvancedFormationPlan()
        {
            var character = ViewModel?.Character;
            if (character == null) return;
            if (!FormationAssignmentStore.TryGetArchivedDeploymentPlanCopy(character.StringId, out var plan))
                return;

            _draft.Load(plan!);
            _selectedCustomFormationIndex = -1;
            MarkDraftChanged();
            RefreshEditorValues();
        }

        [DataSourceMethod]
        public void ExecuteSaveAdvancedFormationPlan()
        {
            var character = ViewModel?.Character;
            if (character == null || !_draft.IsInitialized || !_draft.IsDirty) return;

            if (_draft.IsClearRequested)
            {
                FormationAssignmentStore.ClearDeploymentPlan(character.StringId);
            }
            else
            {
                FormationAssignmentStore.SaveCustomPlan(
                    character.StringId,
                    _draft.Targets,
                    _draft.Weights);
            }
            FormationAssignmentStore.Save();
            _draft.MarkSaved();
            OnPropertyChanged(nameof(HasUnsavedAdvancedFormationChanges));
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
            if (!_editorFormationActive[formationIndex])
            {
                SelectOrActivateFormation(formationIndex);
                return;
            }
            if (_selectedCustomFormationIndex == formationIndex)
            {
                DeactivateFormation(formationIndex);
                return;
            }
            _selectedCustomFormationIndex = formationIndex;
            NotifyEditorSelectionChanged();
        }

        private string GetFormationButtonLabel(int formationIndex, string numeral)
            => !_editorFormationActive[formationIndex] ? "+" : _selectedCustomFormationIndex == formationIndex ? "×" : numeral;

        private void SetSelectedTarget(int value)
        {
            if (_selectedCustomFormationIndex < 0) return;
            EnsureDraftInitialized();
            if (!_draft.SetTarget(_selectedCustomFormationIndex, value, GetReadyTroopCount())) return;
            MarkDraftChanged();
            RefreshEditorValues();
        }

        private void SetSelectedWeight(int value)
        {
            if (_selectedCustomFormationIndex < 0) return;
            EnsureDraftInitialized();
            if (!_draft.SetWeight(_selectedCustomFormationIndex, value)) return;
            MarkDraftChanged();
            RefreshEditorValues();
        }

        private int GetSelectedTargetMaximum()
        {
            if (_selectedCustomFormationIndex < 0) return 0;
            return _draft.GetTargetMaximum(_selectedCustomFormationIndex, GetReadyTroopCount());
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

        [DataSourceMethod] public void ExecuteSelectOrActivateFormation1() => SelectOrActivateFormation(0);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation2() => SelectOrActivateFormation(1);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation3() => SelectOrActivateFormation(2);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation4() => SelectOrActivateFormation(3);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation5() => SelectOrActivateFormation(4);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation6() => SelectOrActivateFormation(5);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation7() => SelectOrActivateFormation(6);
        [DataSourceMethod] public void ExecuteSelectOrActivateFormation8() => SelectOrActivateFormation(7);
        [DataSourceMethod] public void ExecuteDeactivateFormation1() => DeactivateFormation(0);
        [DataSourceMethod] public void ExecuteDeactivateFormation2() => DeactivateFormation(1);
        [DataSourceMethod] public void ExecuteDeactivateFormation3() => DeactivateFormation(2);
        [DataSourceMethod] public void ExecuteDeactivateFormation4() => DeactivateFormation(3);
        [DataSourceMethod] public void ExecuteDeactivateFormation5() => DeactivateFormation(4);
        [DataSourceMethod] public void ExecuteDeactivateFormation6() => DeactivateFormation(5);
        [DataSourceMethod] public void ExecuteDeactivateFormation7() => DeactivateFormation(6);
        [DataSourceMethod] public void ExecuteDeactivateFormation8() => DeactivateFormation(7);

        private void SelectOrActivateFormation(int formationIndex)
        {
            if (ViewModel?.Character == null) return;
            EnsureDraftInitialized();
            if (!_editorFormationActive[formationIndex])
            {
                _draft.Activate(formationIndex);
                MarkDraftChanged();
                RefreshEditorValues();
            }
            _selectedCustomFormationIndex = formationIndex;
            NotifyEditorSelectionChanged();
        }

        private void DeactivateFormation(int formationIndex)
        {
            if (ViewModel?.Character == null || !_editorFormationActive[formationIndex]) return;
            EnsureDraftInitialized();
            _draft.Deactivate(formationIndex);
            MarkDraftChanged();
            if (_selectedCustomFormationIndex == formationIndex) _selectedCustomFormationIndex = -1;
            RefreshEditorValues();
        }

        private int[] GetBaselineFormationIndices()
        {
            var character = ViewModel?.Character;
            if (character == null) return Array.Empty<int>();
            int[] simpleAssignments = FormationAssignmentStore.GetAssignments(character.StringId);
            return simpleAssignments.Length > 0
                ? simpleAssignments
                : new[] { FormationAssignmentResolver.GetDefaultFormationIndex(character, Settings.Instance) };
        }

        private int[] GetEditorActiveFormationIndices()
        {
            EnsureDraftInitialized();
            return Enumerable.Range(0, 8).Where(_draft.IsActive).ToArray();
        }

        private void EnsureDraftInitialized()
        {
            if (_draft.IsInitialized)
                return;

            var character = ViewModel?.Character;
            TroopDeploymentPlan? plan = null;
            bool hasArchivedPlan = false;
            if (character != null)
            {
                FormationAssignmentStore.TryGetDeploymentPlan(character.StringId, out plan);
                hasArchivedPlan = FormationAssignmentStore.CanRestoreDeploymentPlan(character.StringId);
            }
            _draft.Initialize(plan, GetBaselineFormationIndices(), hasArchivedPlan);
            OnPropertyChanged(nameof(HasUnsavedAdvancedFormationChanges));
        }

        private void MarkDraftChanged()
        {
            OnPropertyChanged(nameof(HasUnsavedAdvancedFormationChanges));

            if (Settings.Instance?.AutosaveAdvancedFormationPlans ?? false)
            {
                ExecuteSaveAdvancedFormationPlan();
            }
            else if (!_autosaveHintShown)
            {
                _autosaveHintShown = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[Formation Manager] Changes are drafts. Enable 'Autosave Advanced Formation Plans' in Settings > Multi-Formation Splits to save each edit immediately."));
            }
        }

        private void ResetDraft()
        {
            _draft.Reset();
            OnPropertyChanged(nameof(HasUnsavedAdvancedFormationChanges));
        }

        private static void ShowCustomPlanHint()
            => InformationManager.DisplayMessage(new InformationMessage("[Formation Manager] Advanced formation allocation: expand this troop row to edit it or clear it."));

        private static bool AdvancedPlansEnabled => Settings.Instance?.ShowAdvancedFormationEditor ?? true;

        private void Refresh()
        {
            bool modEnabled = Settings.Instance?.ModEnabled ?? true;
            bool isEligibleTroop = TroopControlScope.IsEligibleTroop(ViewModel?.Character);
            IsFormationBadgeVisible = modEnabled && ViewModel != null && !ViewModel.IsPrisoner && isEligibleTroop;
            if (ViewModel?.Character == null || !modEnabled || !isEligibleTroop)
            {
                FormationLabel = "—";
                SecondaryFormationLabel = "+";
                IsSecondaryFormationBadgeVisible = false;
                _selectedCustomFormationIndex = -1;
                RefreshEditorValues();
                return;
            }

            string troopId = ViewModel.Character.StringId;
            bool hasDeploymentPlan = AdvancedPlansEnabled && FormationAssignmentStore.TryGetDeploymentPlan(troopId, out _);
            // Until an advanced draft is actually changed and saved, the simple
            // assignment remains its source of truth. Refreshing here prevents a
            // previously opened editor from holding an obsolete copied baseline.
            if (!hasDeploymentPlan && !_draft.IsDirty)
                ResetDraft();

            int primary = FormationAssignmentStore.GetAssignment(troopId);
            int secondary = FormationAssignmentStore.GetSecondaryAssignment(troopId);
            FormationLabel = hasDeploymentPlan ? "A" : primary is >= 0 and <= 7 ? Labels[primary + 1] : "—";
            SecondaryFormationLabel = secondary is >= 0 and <= 7 ? Labels[secondary + 1] : "+";
            IsSecondaryFormationBadgeVisible = IsFormationBadgeVisible && !hasDeploymentPlan && primary >= 0;
            OnPropertyChanged(nameof(HasAdvancedFormationPlan));
            OnPropertyChanged(nameof(CanRestoreAdvancedFormationPlan));
            RefreshEditorValues();
        }

        private void RefreshEditorValues()
        {
            var character = ViewModel?.Character;
            EnsureDraftInitialized();
            var allocation = character == null
                ? new System.Collections.Generic.Dictionary<int, int>()
                : FormationAssignmentResolver.GetAllocatedCustomPlanCounts(
                    character,
                    GetReadyTroopCount(),
                    _draft.Targets,
                    _draft.Weights,
                    Settings.Instance);

            int[] activeFormationIndices = GetEditorActiveFormationIndices();
            for (int i = 0; i < 8; i++)
            {
                _editorFormationActive[i] = activeFormationIndices.Contains(i);
                int assigned = allocation.TryGetValue(i, out int count) ? count : 0;
                int target = _draft.GetTarget(i);
                int weight = _draft.GetWeight(i);
                _targetTexts[i] = _editorFormationActive[i] ? $"Target {target}" : string.Empty;
                _assignedTexts[i] = _editorFormationActive[i] ? $"Assigned {assigned}" : "+ Add formation";
                _weightTexts[i] = _editorFormationActive[i] ? $"Weight {weight}" : string.Empty;
                OnPropertyChanged($"IsFormation{i + 1}Active");
                OnPropertyChanged($"Formation{i + 1}ButtonLabel");
                OnPropertyChanged($"Formation{i + 1}TargetText");
                OnPropertyChanged($"Formation{i + 1}AssignedText");
                OnPropertyChanged($"Formation{i + 1}WeightText");
            }
            if (_selectedCustomFormationIndex >= 0 && !_editorFormationActive[_selectedCustomFormationIndex])
                _selectedCustomFormationIndex = -1;
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
            // Bannerlord only invokes ExecuteSetSelected on the newly selected row,
            // while it silently clears every other row. Refresh all live mixins so
            // sibling editor panels follow the same one-open-row rule as vanilla.
            ActiveMixins.RemoveAll(reference => !reference.TryGetTarget(out _));
            foreach (var reference in ActiveMixins)
            {
                if (reference.TryGetTarget(out var mixin))
                    mixin.HandleSelectionChanged();
            }
        }

        private void HandleSelectionChanged()
        {
            if (!(ViewModel?.IsSelected ?? false))
                _selectedCustomFormationIndex = -1;
            OnPropertyChanged(nameof(IsFormationEditorVisible));
            OnPropertyChanged(nameof(IsFormationEditorSliderVisible));
            OnPropertyChanged(nameof(IsRolePlanEditorVisible));
            RefreshRolePlanBindings();
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
            ActiveMixins.RemoveAll(reference => !reference.TryGetTarget(out var mixin) || ReferenceEquals(mixin, this));
        }
    }
}
