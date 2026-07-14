using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using FormationManager.Data;

namespace FormationManager
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        private bool _showAdvancedFormationEditor = true;

        // New MCM identity intentionally starts a clean v0.5 profile. The role
        // vocabulary and its defaults changed enough that loading v0.4 options
        // would be misleading.
        public override string Id => "FormationManager_v0_5";
        public override string DisplayName => "Stop Shuffling, You Fools - Formation Manager";
        public override string FolderName => "FormationManager";
        public override string FormatType => "json";

        [SettingPropertyBool(
            "Enable Formation Manager",
            RequireRestart = false,
            HintText = "When enabled, troops are assigned to formations according to your configured rules. Disable to restore vanilla behaviour.",
            Order = 0)]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Enable Troop Role Defaults",
            RequireRestart = false,
            HintText = "Use role-based troop classification to choose initial formation slots. Explicit troop assignments always take priority, and OOB changes remain editable for the current battle.",
            Order = 0)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public bool UsePartyManagerRoleDefaults { get; set; } = true;

        [SettingPropertyBool(
            "Use Spawned Equipment Classification",
            RequireRestart = false,
            HintText = "Classify each troop from the equipment it actually rolled for this battle. Enabled by default. Turn this off to use the troop's standard equipment sets, as Formation Manager did before v0.5.",
            Order = 1)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public bool UseSpawnedEquipmentClassification { get; set; } = true;

        [SettingPropertyInteger(
            "Light Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for unshielded, non-specialist foot melee troops.",
            Order = 2)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int LightInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Shield Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for foot troops carrying a shield.",
            Order = 3)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int ShieldInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Shock Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for two-handed and offensive-polearm infantry.",
            Order = 4)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int ShockInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Pike Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for anti-cavalry pike infantry.",
            Order = 5)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int PikeInfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Skirmisher Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for foot troops whose primary role is throwing weapons.",
            Order = 6)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int SkirmisherFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Foot Archer Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for bow-using foot archers and slingers.",
            Order = 7)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int FootArcherFormation { get; set; } = 2;

        [SettingPropertyInteger(
            "Crossbowman Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for crossbow-using foot archers.",
            Order = 8)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int CrossbowmanFormation { get; set; } = 2;

        [SettingPropertyInteger(
            "Melee Cavalry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for mounted melee troops.",
            Order = 9)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int MeleeCavalryRoleFormation { get; set; } = 3;

        [SettingPropertyInteger(
            "Horse Archer Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Default slot for mounted bow, crossbow, or sling troops.",
            Order = 10)]
        [SettingPropertyGroup("Troop Role Defaults", GroupOrder = 1)]
        public int HorseArcherRoleFormation { get; set; } = 4;

        [SettingPropertyBool(
            "Enable Mid-Battle Reassignment",
            RequireRestart = false,
            HintText = "When enabled, troops can be moved between configured formations during battle as their mounted or ammo state changes.",
            Order = 0)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public bool AutoReassignmentEnabled { get; set; } = true;

        [SettingPropertyInteger(
            "Infantry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for infantry and dismounted melee cavalry.",
            Order = 1)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public int InfantryFormation { get; set; } = 1;

        [SettingPropertyInteger(
            "Archer Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for active ranged troops and dismounted horse archers.",
            Order = 2)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public int ArcherFormation { get; set; } = 2;

        [SettingPropertyInteger(
            "Cavalry Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for active mounted troops.",
            Order = 3)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public int CavalryFormation { get; set; } = 3;

        [SettingPropertyInteger(
            "Backline Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for foot ranged troops that are out of ammo. Used only when backline reassignment is enabled.",
            Order = 4)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public int BacklineFormation { get; set; } = 8;

        [SettingPropertyInteger(
            "Mounted Backline Formation",
            1, 8,
            "0",
            RequireRestart = false,
            HintText = "Formation slot (1-8) for mounted ranged troops that are out of ammo. Used only when backline reassignment is enabled.",
            Order = 5)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public int MountedBacklineFormation { get; set; } = 8;

        [SettingPropertyBool(
            "Enable Out-of-Ammo Backline Reassignment",
            RequireRestart = false,
            HintText = "When enabled, ranged troops with no ammo move to the configured backline formations. Enabled by default.",
            Order = 6)]
        [SettingPropertyGroup("Mid-Battle Reassignment", GroupOrder = 3)]
        public bool BacklineReassignmentEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Prioritize Weights in Small Stacks",
            RequireRestart = false,
            HintText = "When there are too few troops to satisfy every custom target, distribute by weight first. Disabled by default: give one troop to each active formation from I to VIII before distributing the remainder.",
            Order = 0)]
        [SettingPropertyGroup("Multi-Formation Splits", GroupOrder = 2)]
        public bool PrioritizeWeightsInSmallStacks { get; set; } = false;

        [SettingPropertyBool(
            "Enable Advanced Formation Plans",
            RequireRestart = false,
            HintText = "Show and apply advanced Target/Weight plans. Turning this off completely hides the advanced panel and archives every active plan. Turning it back on exposes the editor, but archived plans remain inactive until restored individually.",
            Order = 1)]
        [SettingPropertyGroup("Multi-Formation Splits", GroupOrder = 2)]
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
        [SettingPropertyGroup("Multi-Formation Splits", GroupOrder = 2)]
        public bool AutosaveAdvancedFormationPlans { get; set; } = false;

        [SettingPropertyBool(
            "Lock Managed OOB Sliders",
            RequireRestart = false,
            HintText = "Lock Bannerlord's native weight sliders on formation cards seeded by Formation Manager. Turn this off to manually rebalance those cards in the Order of Battle screen.",
            Order = 3)]
        [SettingPropertyGroup("Multi-Formation Splits", GroupOrder = 2)]
        public bool LockManagedOobSliders { get; set; } = true;

        [SettingPropertyBool(
            "Manage Allied Infantry (Opt-In)",
            RequireRestart = false,
            HintText = "Allow Formation Manager to place allied infantry into your formations. Disabled by default: only troops from your own party are managed.",
            Order = 0)]
        [SettingPropertyGroup("Allied Troop Control", GroupOrder = 4)]
        public bool ManageAlliedInfantry { get; set; } = false;

        [SettingPropertyBool(
            "Manage Allied Ranged Troops (Opt-In)",
            RequireRestart = false,
            HintText = "Allow Formation Manager to place allied foot archers and crossbowmen into your formations. Disabled by default.",
            Order = 1)]
        [SettingPropertyGroup("Allied Troop Control", GroupOrder = 4)]
        public bool ManageAlliedRanged { get; set; } = false;

        [SettingPropertyBool(
            "Manage Allied Cavalry (Opt-In)",
            RequireRestart = false,
            HintText = "Allow Formation Manager to place allied melee cavalry into your formations. Disabled by default.",
            Order = 2)]
        [SettingPropertyGroup("Allied Troop Control", GroupOrder = 4)]
        public bool ManageAlliedCavalry { get; set; } = false;

        [SettingPropertyBool(
            "Manage Allied Horse Archers (Opt-In)",
            RequireRestart = false,
            HintText = "Allow Formation Manager to place allied mounted archers into your formations. Disabled by default.",
            Order = 3)]
        [SettingPropertyGroup("Allied Troop Control", GroupOrder = 4)]
        public bool ManageAlliedHorseArchers { get; set; } = false;
    }
}
