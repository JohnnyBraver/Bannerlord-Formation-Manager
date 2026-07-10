using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace FormationManager.Data
{
    /// <summary>
    /// The troop roles used by PartyManager's recruitment filter. Keeping the same
    /// classification here lets both mods describe a troop in the same way without
    /// taking a hard assembly dependency on PartyManager.
    /// </summary>
    internal enum PartyManagerRole
    {
        FrontlineInfantry,
        ShockInfantry,
        Skirmisher,
        FootArcher,
        Crossbowman,
        MeleeCavalry,
        HorseArcher,
        PikeInfantry
    }

    internal static class PartyManagerRoleClassifier
    {
        public static PartyManagerRole Classify(BasicCharacterObject troop)
        {
            if (troop == null)
                return PartyManagerRole.FrontlineInfantry;

            bool hasBow = HasWeaponOfClassInMostLoadouts(troop, WeaponClass.Bow);
            bool hasCrossbow = HasWeaponOfClassInMostLoadouts(troop, WeaponClass.Crossbow);

            if (troop.IsMounted)
                return hasBow || hasCrossbow
                    ? PartyManagerRole.HorseArcher
                    : PartyManagerRole.MeleeCavalry;

            if (hasBow)
                return PartyManagerRole.FootArcher;

            if (hasCrossbow)
                return PartyManagerRole.Crossbowman;

            if (HasPikeInMostLoadouts(troop))
                return PartyManagerRole.PikeInfantry;

            if (HasShieldInMostLoadouts(troop))
                return PartyManagerRole.FrontlineInfantry;

            if (HasLargeSwingableInMostLoadouts(troop))
                return PartyManagerRole.ShockInfantry;

            if (HasJavelinInMostLoadouts(troop))
                return PartyManagerRole.Skirmisher;

            return PartyManagerRole.FrontlineInfantry;
        }

        private static IEnumerable<Equipment> GetBattleEquipments(BasicCharacterObject troop)
        {
            bool yieldedAny = false;
            var battleEquipments = troop.BattleEquipments;
            if (battleEquipments != null)
            {
                foreach (var equipment in battleEquipments)
                {
                    if (equipment == null)
                        continue;

                    yieldedAny = true;
                    yield return equipment;
                }
            }

            if (!yieldedAny && troop.Equipment != null)
                yield return troop.Equipment;
        }

        private static bool AppearsInMostLoadouts(BasicCharacterObject troop, Func<Equipment, bool> predicate)
        {
            int loadoutCount = 0;
            int matchedCount = 0;

            foreach (var equipment in GetBattleEquipments(troop))
            {
                loadoutCount++;
                if (predicate(equipment))
                    matchedCount++;
            }

            return loadoutCount > 0 && matchedCount > loadoutCount / 2;
        }

        private static bool HasWeaponOfClassInMostLoadouts(BasicCharacterObject troop, params WeaponClass[] classes)
        {
            return AppearsInMostLoadouts(troop, equipment => HasWeaponOfClass(equipment, classes));
        }

        private static bool HasWeaponOfClass(Equipment equipment, params WeaponClass[] classes)
        {
            for (int i = 0; i < 4; i++)
            {
                var weapon = equipment[i].Item?.PrimaryWeapon;
                if (weapon != null && Array.IndexOf(classes, weapon.WeaponClass) >= 0)
                    return true;
            }

            return false;
        }

        private static bool HasShieldInMostLoadouts(BasicCharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, equipment =>
            {
                for (int i = 0; i < 4; i++)
                {
                    if (equipment[i].Item?.PrimaryWeapon?.IsShield == true)
                        return true;
                }

                return false;
            });
        }

        private static bool HasJavelinInMostLoadouts(BasicCharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, equipment =>
            {
                for (int i = 0; i < 4; i++)
                {
                    var item = equipment[i].Item;
                    if (item == null)
                        continue;

                    if (item.PrimaryWeapon?.WeaponClass == WeaponClass.Javelin ||
                        (item.StringId?.IndexOf("javelin", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                        return true;
                }

                return false;
            });
        }

        private static bool HasPikeInMostLoadouts(BasicCharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, equipment =>
            {
                for (int i = 0; i < 4; i++)
                {
                    var item = equipment[i].Item;
                    if (item == null)
                        continue;

                    string itemId = item.StringId ?? string.Empty;
                    string itemName = item.Name?.ToString() ?? string.Empty;
                    string weaponClass = item.PrimaryWeapon?.WeaponClass.ToString() ?? string.Empty;
                    if (weaponClass == "Pike" ||
                        itemId.IndexOf("pike", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        itemName.IndexOf("pike", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            });
        }

        private static bool HasLargeSwingableInMostLoadouts(BasicCharacterObject troop)
        {
            return AppearsInMostLoadouts(troop, equipment =>
            {
                for (int i = 0; i < 4; i++)
                {
                    var weapon = equipment[i].Item?.PrimaryWeapon;
                    if (weapon == null)
                        continue;

                    switch (weapon.WeaponClass)
                    {
                        case WeaponClass.TwoHandedSword:
                        case WeaponClass.TwoHandedAxe:
                        case WeaponClass.TwoHandedMace:
                            return true;
                        case WeaponClass.TwoHandedPolearm:
                        case WeaponClass.LowGripPolearm:
                            if (weapon.SwingDamage > 0)
                                return true;
                            break;
                    }
                }

                return false;
            });
        }
    }
}
