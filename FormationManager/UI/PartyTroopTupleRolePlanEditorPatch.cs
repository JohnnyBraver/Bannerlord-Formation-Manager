using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace FormationManager.UI
{
    /// <summary>Shows campaign role fallbacks only when the player hero row is expanded.</summary>
    [PrefabExtension("PartyTroopTuple", "descendant::InventoryTupleExtensionControlsWidget[@Id='Extension']")]
    public class PartyTroopTupleRolePlanEditorPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionFileName(true)]
        public string MyXmlFile => "PartyTroopTupleRolePlanEditor";
    }
}
