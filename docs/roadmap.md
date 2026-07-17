# Deferred ideas

## Three levels of formation control

Formation Manager should remain useful at three progressively more detailed
levels, without requiring every player to use the more complex ones:

1. **Simple assignment:** assign a troop stack to one formation, or use the
   existing two-way split.
2. **Advanced formation plans:** set per-stack targets and surplus weights
   across several formations.
3. **Expert loadout rules:** optionally assign individual deployment agents
   according to their actual weapons and equipment rolls.

## Formation banner assignments

Allow a player to choose a banner for each formation independently of its
captain. The selected banner should be saved with the formation plan and
reapplied during Order of Battle/mission setup, so Bannerlord can select an
eligible ordinary troop as the banner bearer.

This must use only valid banner items and should provide an explicit "no
banner" choice. It must not require a captain or change that captain's
equipment; captain-equipped banners should remain compatible with the native
behaviour when no explicit formation banner is selected.

## Per-agent loadout formation rules

The expert layer would be a separate opt-in advanced rules section for players
who want to manage a troop stack at the individual-agent level during
deployment. Once Bannerlord creates agents for the OOB, inspect each soldier's
equipped weapons and assign them by their actual loadout rather than only by
troop template or Party Manager role.

Possible filters include one-handed weapon with shield, one-handed weapon
without shield, two-handed weapon, polearm, throwing weapon, foot bow,
crossbow, melee cavalry, and horse archer. This would allow a mixed troop such
as Battanian Peasants to split dynamically into one-handed and two-handed
formations.

Weapon damage type should also be available as an expert filter: cut, blunt,
and pierce. For example, Mamluke Palace Guards carrying axes could be separated
from guards carrying maces, allowing the player to direct cut-damage troops
toward lightly armoured targets and blunt-damage troops toward armoured elites.

These rules must remain opt-in and separate from the standard troop-stack and
role-based settings: equipment rolls belong to individual mission agents and
can differ between battles, so the rule can be persistent but its exact agent
allocation is necessarily recalculated for every deployment.

## Smarter manual formation transfers

Bannerlord's native OOB transfer controls move a chosen number of units from a
class pool. In a mixed formation that can transfer the wrong troop types and
leave an arrangement that cannot be cleanly untangled with the same controls.

Add a manual transfer tool that operates on known agents instead of anonymous
counts. The player should be able to choose a source and destination formation,
then move a selected troop stack, role, or future loadout filter. It should show
the exact affected troops before applying the transfer and leave unrelated
agents in the mixed source formation untouched.

This belongs to the pre-battle/OOB layer: it has the complete roster and can
make deliberate whole-formation decisions. Mid-battle reassignment remains an
optional, narrow agent-level system for state changes such as mounting,
dismounting, and out-of-ammo backline rules.
