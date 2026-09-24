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
