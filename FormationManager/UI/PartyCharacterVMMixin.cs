using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using FormationManager.Data;

namespace FormationManager.UI
{
    /// <summary>
    /// Extends each troop row in the party screen with a formation assignment badge.
    /// Properties here bind directly into the injected widget from PartyTroopTupleFormationBadgePatch.
    /// - Click  : cycles none → F1 → F2 → … → F8 → none
    /// - Right-click: clears assignment immediately
    /// </summary>
    [ViewModelMixin(nameof(PartyCharacterVM.RefreshValues))]
    internal sealed class PartyCharacterVMMixin : BaseViewModelMixin<PartyCharacterVM>
    {
        private static readonly string[] Labels = { "\u2014", "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };

        private string _formationLabel = "\u2014";
        private string _secondaryFormationLabel = "+";
        private bool _isFormationBadgeVisible;
        private bool _isSecondaryFormationBadgeVisible;

        private static int _instantiationCount = 0;

        public PartyCharacterVMMixin(PartyCharacterVM vm) : base(vm)
        {
            _instantiationCount++;
            try
            {
                string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                string path = System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "FormationManager_VMDebug.txt");
                System.IO.File.WriteAllText(path, $"Mixin instantiated. Count: {_instantiationCount}. Last Troop: {vm?.Character?.Name?.ToString() ?? "null"}");
            }
            catch {}
            Refresh();
        }

        // ── Bindable properties ────────────────────────────────────────────────

        [DataSourceProperty]
        public string FormationLabel
        {
            get => _formationLabel;
            set
            {
                if (_formationLabel == value) return;
                _formationLabel = value;
                OnPropertyChanged(nameof(FormationLabel));
            }
        }

        [DataSourceProperty]
        public bool IsFormationBadgeVisible
        {
            get => _isFormationBadgeVisible;
            set
            {
                if (_isFormationBadgeVisible == value) return;
                _isFormationBadgeVisible = value;
                OnPropertyChanged(nameof(IsFormationBadgeVisible));
            }
        }

        [DataSourceProperty]
        public string SecondaryFormationLabel
        {
            get => _secondaryFormationLabel;
            set
            {
                if (_secondaryFormationLabel == value) return;
                _secondaryFormationLabel = value;
                OnPropertyChanged(nameof(SecondaryFormationLabel));
            }
        }

        [DataSourceProperty]
        public bool IsSecondaryFormationBadgeVisible
        {
            get => _isSecondaryFormationBadgeVisible;
            set
            {
                if (_isSecondaryFormationBadgeVisible == value) return;
                _isSecondaryFormationBadgeVisible = value;
                OnPropertyChanged(nameof(IsSecondaryFormationBadgeVisible));
            }
        }

        // ── Commands (void methods — bound via Command.Click in the widget XML) ──

        [DataSourceMethod]
        public void ExecuteCycleFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;

            int current = FormationAssignmentStore.GetAssignment(character.StringId);
            int next = current + 1;
            if (next > 7) next = -1; // wrap back to none

            try
            {
                string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                string path = System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "FormationManager_ClickDebug.txt");
                System.IO.File.WriteAllText(path, $"Cycle clicked! Troop: {character.Name}, Current Assignment: {current}, Next Assignment: {next}");
            }
            catch {}

            if (next < 0)
                FormationAssignmentStore.ClearAssignment(character.StringId);
            else
                FormationAssignmentStore.SetAssignment(character.StringId, next);

            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteClearFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;

            try
            {
                string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                string path = System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "FormationManager_ClickDebug.txt");
                System.IO.File.WriteAllText(path, $"Clear clicked! Troop: {character.Name}");
            }
            catch {}

            FormationAssignmentStore.ClearAssignment(character.StringId);
            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteCycleSecondaryFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;

            int primary = FormationAssignmentStore.GetAssignment(character.StringId);
            if (primary < 0)
            {
                FormationAssignmentStore.SetAssignment(character.StringId, 0);
                FormationAssignmentStore.Save();
                Refresh();
                return;
            }

            int current = FormationAssignmentStore.GetSecondaryAssignment(character.StringId);
            int next = current;
            do
            {
                next++;
                if (next > 7)
                {
                    next = -1;
                    break;
                }
            }
            while (next == primary);

            if (next < 0)
                FormationAssignmentStore.ClearSecondaryAssignment(character.StringId);
            else
                FormationAssignmentStore.SetSecondaryAssignment(character.StringId, next);

            FormationAssignmentStore.Save();
            Refresh();
        }

        [DataSourceMethod]
        public void ExecuteClearSecondaryFormation()
        {
            var character = ViewModel?.Character;
            if (character == null) return;

            FormationAssignmentStore.ClearSecondaryAssignment(character.StringId);
            FormationAssignmentStore.Save();
            Refresh();
        }


        // ── Internal refresh ──────────────────────────────────────────────────

        private void Refresh()
        {
            var settings = Settings.Instance;
            bool modEnabled = settings?.ModEnabled ?? true;

            // Badge is only shown for regular (non-hero) player-side troops when mod is on
            IsFormationBadgeVisible = modEnabled && ViewModel != null && !ViewModel.IsMainHero && !ViewModel.IsPrisoner;

            if (ViewModel?.Character == null || !modEnabled)
            {
                FormationLabel = "\u2014";
                SecondaryFormationLabel = "+";
                IsSecondaryFormationBadgeVisible = false;
                return;
            }

            int idx = FormationAssignmentStore.GetAssignment(ViewModel.Character.StringId);
            FormationLabel = (idx >= 0 && idx <= 7) ? Labels[idx + 1] : "\u2014";

            int secondaryIdx = FormationAssignmentStore.GetSecondaryAssignment(ViewModel.Character.StringId);
            SecondaryFormationLabel = (secondaryIdx >= 0 && secondaryIdx <= 7) ? Labels[secondaryIdx + 1] : "+";
            IsSecondaryFormationBadgeVisible = IsFormationBadgeVisible && idx >= 0;
        }

        public override void OnRefresh() => Refresh();
    }
}
