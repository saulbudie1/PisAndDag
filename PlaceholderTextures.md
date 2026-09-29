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
| `Content/NPCs/Legends/WallTentacle.png` | The Wall of Flesh's tentacles (Legends), a segment | 16x52 | Two variants of one segment, one above the other, each 16x26: 8x13 art at 2x. Drawn along the tentacle every 10 px, its length along x, and thinned toward the tip. In the Wall's own colours. |
| `Content/NPCs/Legends/WallTentacleTip.png` | The Wall of Flesh's tentacles (Legends), the tip | 24x18 | 12x9 art at 2x, pointing right: the flesh (x 0 to 15) narrowing to a raw lip and three bone hooks. Drawn from (2, 9), so the flesh starts at the end of the segments. |
| `Content/NPCs/Legends/SkeletronHead.png` | Skeletron in Legends, the skull while his jaw is open (the breath) | 114x132 | Vanilla's head (NPC_35, frame 0) with the jaw cut away at row 94 and the mouth behind it filled dark. One frame on vanilla's frame size, so it draws at vanilla's placement; keep it 114x132 and the cut where the jaw's top is. |
| `Content/NPCs/Legends/SkeletronJaw.png` | Skeletron in Legends, his jaw | 114x132 | The jaw alone (rows 94 down of NPC_35, frame 0), in place on the same canvas as the head; dropped by up to 16 px when he opens it. |
| `Content/Items/Summoner/JointArmsOfTheDefiled.png` | Joint Arms of the Defiled (whip), the item | 32x32 | One frame: the lash coiled, the bone joint of the handle bottom left, the tail end bottom right. |
| `Content/Projectiles/Summoner/JointArmsOfTheDefiledProj.png` | Joint Arms of the Defiled, the swung whip | 16x104 | Five pieces one above the other, each pointing down, 2 empty rows between them: the handle at y 0 (24 tall, held at (8, 8), the bone joint above that), three links at y 26, 46 and 66 (18 tall, drawn from (8, 9): the first, middle and last third of the lash, thinning), the tail end at y 86 (18 tall, drawn from (8, 5), its opening at the bottom). Keep every piece centred on x 8. The rectangles and points are in `JointArmsOfTheDefiledProj.cs` (Frame, Origin). |
| `Content/Buffs/ManyHands.png` | Many Hands buff icon | 32x32 | Buff icons are always 32x32. |
