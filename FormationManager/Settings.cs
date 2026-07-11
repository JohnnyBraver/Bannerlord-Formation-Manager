using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using FormationManager.Data;

namespace FormationManager
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        private bool _showAdvancedFormationEditor = true;

        public override string Id => "FormationManager";
        public override string DisplayName => "Stop Shuffling, You Fools - Formation Manager";
        public override string FolderName => "FormationManager";
        public override string FormatType => "json";

        [SettingPropertyBool(
            "Enable Formation Manager",
            RequireRestart = false,
            HintText = "When enabled, troops are assigned to formations according to your configured rules. Disable to restore vanilla behaviour.",
            Order = 0)]
        [SettingPropertyGroup("General")]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Enable Troop Role Defaults",
            RequireRestart = false,
            HintText = "Use role-based troop classification to choose initial formation slots. Explicit troop assignments always take priority, and OOB changes remain editable for the current battle.",
            Order = 0)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public bool UsePartyManagerRoleDefaults { get; set; } = true;

        [SettingPropertyInteger(
            "Frontline Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for shield infantry and unclassified foot troops.",
            Order = 1)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int FrontlineInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Shock Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for two-handed and offensive-polearm infantry.",
            Order = 2)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int ShockInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Pike Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for anti-cavalry pike infantry.",
            Order = 3)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int PikeInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Skirmisher Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for foot troops whose primary role is throwing weapons.",
            Order = 4)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int SkirmisherFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Foot Archer Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for bow-using foot archers.",
            Order = 5)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int FootArcherFormation { get; set; } = 2;

        [SettingPropertyInteger(
            "Crossbowman Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for crossbow-using foot archers.",
            Order = 6)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int CrossbowmanFormation { get; set; } = 2;

        [SettingPropertyInteger(
            "Melee Cavalry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for mounted melee troops.",
            Order = 7)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int MeleeCavalryRoleFormation { get; set; } = 3;

        [SettingPropertyInteger(
            "Horse Archer Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for mounted bow or crossbow troops.",
            Order = 8)]
        [SettingPropertyGroup("Troop Role Defaults")]
        public int HorseArcherRoleFormation { get; set; } = 4;

        [SettingPropertyBool(
            "Enable Mid-Battle Reassignment",
            RequireRestart = false,
            HintText = "When enabled, troops can be moved between configured formations during battle as their mounted or ammo state changes.",
            Order = 0)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public bool AutoReassignmentEnabled { get; set; } = true;

        [SettingPropertyInteger(
            "Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for infantry and dismounted melee cavalry.",
            Order = 1)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public int InfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Archer Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for active ranged troops and dismounted horse archers.",
            Order = 2)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public int ArcherFormation { get; set; } = 2;

        [SettingPropertyInteger(
            "Cavalry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for active mounted troops.",
            Order = 3)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public int CavalryFormation { get; set; } = 3;

        [SettingPropertyInteger(
            "Backline Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for foot ranged troops that are out of ammo. Used only when backline reassignment is enabled.",
            Order = 4)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public int BacklineFormation { get; set; } = 8;

        [SettingPropertyInteger(
            "Mounted Backline Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for mounted ranged troops that are out of ammo. Used only when backline reassignment is enabled.",
            Order = 5)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public int MountedBacklineFormation { get; set; } = 8;

        [SettingPropertyBool(
            "Enable Out-of-Ammo Backline Reassignment",
            RequireRestart = false,
            HintText = "When enabled, ranged troops with no ammo move to the configured backline formations. Disabled by default.",
            Order = 6)]
        [SettingPropertyGroup("Mid-Battle Reassignment")]
        public bool BacklineReassignmentEnabled { get; set; } = false;

        [SettingPropertyBool(
            "Prioritize Weights in Small Stacks",
            RequireRestart = false,
            HintText = "When there are too few troops to satisfy every custom target, distribute by weight first. Disabled by default: give one troop to each active formation from I to VIII before distributing the remainder.",
            Order = 0)]
        [SettingPropertyGroup("Multi-Formation Splits")]
        public bool PrioritizeWeightsInSmallStacks { get; set; } = false;

        [SettingPropertyBool(
            "Enable Advanced Formation Plans",
            RequireRestart = false,
            HintText = "Show and apply advanced Target/Weight plans. Turning this off completely hides the advanced panel and archives every active plan. Turning it back on exposes the editor, but archived plans remain inactive until restored individually.",
            Order = 1)]
        [SettingPropertyGroup("Multi-Formation Splits")]
        public bool ShowAdvancedFormationEditor
        {
            get => _showAdvancedFormationEditor;
            set
            {
                if (_showAdvancedFormationEditor == value)
                    return;

                _showAdvancedFormationEditor = value;
                FormationAssignmentStore.SetAdvancedPlansEnabled(value);
            }
        }

        [SettingPropertyBool(
            "Autosave Advanced Formation Plans",
            RequireRestart = false,
            HintText = "Save advanced formation edits immediately when targets, weights, or active formations change. Disabled by default so edits remain a draft until the Save button is pressed.",
            Order = 2)]
        [SettingPropertyGroup("Multi-Formation Splits")]
        public bool AutosaveAdvancedFormationPlans { get; set; } = false;
    }
}
