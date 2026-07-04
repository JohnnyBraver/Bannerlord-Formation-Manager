using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace FormationManager
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
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
    }
}
