# Placeholder textures

Every texture in the mod that is standing in until real art is drawn. When you replace one,
keep the same path and file name, keep the size (or change the matching `Item.width` /
`Item.height` in its `.cs`), keep a tile sheet's frame layout, and delete its row here.

| Texture | What uses it | Size | Notes |
|---|---|---|---|
| `Content/Items/Misc/EssenceOfTheFouled.png` | Essence of the Fouled (crafting material) | 16x22 | One frame. |
| `Content/Items/Misc/AmalgaermisBag.png` | Treasure Bag (Amalgaermis) | 26x28 | One frame. |
| `Content/Items/Misc/MakeshiftAltar.png` | Makeshift Altar, the item | 46x34 | One frame. The picture in the inventory and on the ground. |
| `Content/Tiles/MakeshiftAltar.png` | Makeshift Altar, the placed tile | 54x38 | 3 frames across, 2 down. Each frame is 16 px wide with 2 px of empty padding after it. The top row is 16 px tall, the bottom row 18 px (it sits 2 px into the floor, like vanilla's Demon Altar). Frame x: 0, 18, 36. Frame y: 0, 18. |
| `Content/Items/LoreItem/ReliquaryKey.png` | Reliquary Key | 18x30 | Marked as a placeholder in `ReliquaryKey.cs`. |
| `Content/Buffs/ManaNausea1.png`, `ManaNausea2.png`, `ManaNausea3.png` | Mana Nausea I, II, III buff icons | 32x32 | Buff icons are always 32x32. |
| `Content/Buffs/ManaPoisoning1.png`, `ManaPoisoning2.png` | Mana Poisoning I, II buff icons | 32x32 | Buff icons are always 32x32. |
| `Content/Items/Mage/Staves/TreeBranchStaff.png` | Tree Branch Staff | 40x40 | One frame: 20x20 art at 2x, on the diagonal, head up and right, like vanilla staffs. Cold Call holds the staff's head to the mouth by the point where its branches part, (28, 12) on this sprite: `HeadOnSprite` in `Content/Projectiles/Magic/ColdCallHold.cs`, as a fraction of the size. Move it too if the new head is elsewhere. |
| `Content/Items/Mage/Spell/ColdCall.png` | Cold Call (spell) | 60x60 | The incantation page with a frost glyph on it: there is no spell page template yet. |
| `Content/Projectiles/Magic/ColdCallIcicle.png` | Cold Call's icicle | 10x26 | One frame, tip up. The hitbox centres on (5, 9) of the sprite: `Pivot` in `ColdCallIcicle.cs`. |
| `Content/Items/Mage/Incantation/ForestNurtureMe.png` | Forest, Nurture Me (incantation) | 60x60 | The incantation page with a sprout in a ring on it. |
| `Content/Buffs/Nurtured.png` | Nurtured buff icon | 32x32 | Buff icons are always 32x32. |
| `Content/Items/Mage/Spell/AquaticFirearms.png` | Aquatic Firearms (spell) | 60x60 | The incantation page with a Conquest glyph on it, a curling wave and three shots, in the element's golds: there is no spell page template yet. |
| `Content/Items/Mage/Incantation/ChlorophyteArmMe.png` | Chlorophyte, Arm Me (incantation) | 60x60 | The incantation page with a chlorophyte round sprouting two leaves on it. |
| `Content/Buffs/Armed.png` | Armed buff icon | 32x32 | Buff icons are always 32x32. 16x16 art at 2x. |
