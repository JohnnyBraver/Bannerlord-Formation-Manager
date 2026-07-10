using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace FormationManager.UI
{
    /// <summary>
    /// Adds a dedicated editor beside the specialised native extension widget.
    /// The native widget accepts its own built-in children but does not render
    /// arbitrary injected ones; this sibling is owned by PartyTroopTupleButtonWidget
    /// and retains the row's PartyCharacterVM data context.
    /// </summary>
    [PrefabExtension("PartyTroopTuple", "descendant::InventoryTupleExtensionControlsWidget[@Id='Extension']")]
    public class PartyTroopTupleCustomSplitEditorPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionFileName(true)]
        public string MyXmlFile => "PartyTroopTupleCustomSplitEditor";
    }

}
