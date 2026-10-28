# The Empress's pictures

Everything here is drawn in the game in place of the game's own picture (`EmpressTextures.cs`), so whatever is painted
here is what is seen: in the fight, in her scenes, on the map and in the bestiary. Keep each file's size and its frames
where they are; the code cuts frames by size.

Paint them as any picture is painted: soft edges, glows and half-clear pixels are fine. The game keeps its own pictures
premultiplied (each colour times its alpha) and a mod's are packed as painted, so the mod makes these premultiplied as
it loads them; a soft edge fades here as it does in the game.

Anything drawn smoothly and turned (the bow, the arrow) has a clear margin all round: paint that touches a picture's
edge is cut off there, in a hard line, as the picture is drawn.

| File | What it is | Frames |
|---|---|---|
| Body.png | her body, first half then second | 2 down, 166 x 176 each, her middle at 83, 88 |
| WingsBack.png | her wings | 11 down |
| WingsGlow.png | their glow (coloured by the game's own shader) | 11 down |
| ArmLeft.png, ArmRight.png | her arms (the screen's left and right) | 7 down, 174 x 172 each; frame 7 is cut for her posed arms |
| Streamers.png, Skirt.png | her second half's streamers and skirt of light | 8 down, and 1 |
| Head.png | her head on the map | 1 |
| Lacewing.png | the Prismatic Lacewing | as the game's |
| Lance.png, LanceLine.png, RainbowStreak.png, LastingRainbow.png, SplitShotCore.png, DeathAurora.png, SunDance.png | her shots | as the game's |
| Bow.png | her bow of light (Legends, second half): Eventide, enlarged two and a half times, to redraw; drawn in the fight at 0.6 of its size | 1, 90 x 195 (the bow 70 x 175, in a clear margin of 10) |
| Arrow.png | her arrows (Legends, second half): Daybreak's shape, in greys (tinted as they fly) | 1, 112 x 42 (the arrow 100 x 30, in a clear margin of 6) |
| Veil.png | where her veil's streaks may take her body away (Legends, second half): a mask over Body.png's two frames | same size as Body.png |
| BodyPhase2.png | her second half's own body under the veil (Legends): its second frame drawn in place of Body.png's second while the veil is on her; the red places are where the streaks always lie | same size as Body.png |
| HeadAndShoulderForPhase2.png | her head, hair and shoulders, laid back over BodyPhase2 so the veil never takes them | same size as Body.png |

## Bow.png

Drawn pointing right (its string side on the left), held at its grip, at 0.6 of its size: a pixel of it is 0.6 of a
pixel of the world (`EmpressBow.Size`; change that alone for another size in the fight). So it can be painted finer
than the world's own pixels. Its string is not painted: the game draws it as light from tip to tip. If the bow's shape
changes, these places in `EmpressBow.cs` want moving to match (picture pixels, from its top left, the margin counted):

| Place | Now |
|---|---|
| Grip (where her hand holds it, the middle of the riser) | 62.5, 97.5 |
| TopTip (where the string is tied at the top: the blue gem under the top ear) | 22.5, 48.75 |
| BottomTip (where the string is tied at the bottom) | 22.5, 146.25 |
| StringMiddle (the string at rest) | 22.5, 97.5 |
| Pull (how far back the string can be drawn, picture pixels: as far as leaves a nocked arrow's tip just clear of the bow) | 98 |

It is the game's Eventide picture enlarged two and a half times (each of its 2 x 2 blocks now 5 x 5), so it can be
redrawn with finer detail; in the fight it is one and a half times Eventide's own size, a little over half her height.
Keep the margin clear; the bow is turned over (top for bottom) when she aims to her left, so keep it a mirror image
top to bottom, or the string's two places want moving each their own way.

## Arrow.png

Pointing right. Its nock (where the string holds it) is at 12, 21, the middle of its sun of a head at 78, 21, and its
tip at 105 (`EmpressBow.cs`; `EmpressArrow.cs` keeps the arrow's own lengths: nock to tip 93). Daybreak's parts in
Daybreak's places: the pommel's diamond at the nock, the shaft, the round head with a ring in it and two horns swept
forward off it, the leaf of a blade and its needle of a tip. In greys and white: it is tinted in her colours as it
flies, and lit with a star at its head.

## Veil.png

Not drawn: wherever it is painted (any colour), the veil's moving streaks may take her body's pixels away; wherever it
is clear, they never do. Now: all of her below her face, in both frames.

## BodyPhase2.png and HeadAndShoulderForPhase2.png

Only the second frame of each is used. In BodyPhase2, paint pure red (255, 0, 0) where a streak must always lie: now a
bar across her chest and a square at her hips. The red is never seen (each red pixel is drawn in her own colour from
the nearest pixel above or below it until the veil takes it), and the two main streaks are held over the two places,
turning and drifting about them but always covering them (`EmpressVeil.cs`: the upper red place is the chest streak's,
the lower the hip one's). Everything of BodyPhase2 not under HeadAndShoulderForPhase2 may be taken by a streak;
everything under it never is. Without either file, or at another size, her second half is drawn from Body.png as
before.
