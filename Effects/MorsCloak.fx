// Mors's cloak.
//
// COMPILE THIS BEFORE USE — same as LegendsShader: tModLoader does not build .fx
// files. Run it through fxcompiler or EasyXnb and put MorsCloak.xnb beside it. The
// mod loads without the sway (a still cloak) until the .xnb exists.
//
// A sprite shader, not a screen filter: MorsIdle draws the cloak layer alone with
// this applied, so coords run 0..1 over the cloak's own texture and the vertex
// colour is the world lighting at his feet.
//
// The cloth hangs from a line across the shoulders and everything below it is
// slid sideways. Three motions add up, and the weather decides the mix (MorsIdle
// works it out from Main.windSpeedCurrent and passes the numbers in):
//
//   RIPPLE  a wave travelling down the fall of the cloth, row by row. A still day
//           is only this. Grows with the square of the distance from the shoulders,
//           so the top is pinned and the hem is freest.
//   SWAY    the whole lower cloth leaning one way and then the other as one piece,
//           the way a branch moves. A breeze. Also square in the hang: it bends.
//   DRAG    a steady lean downwind. A windy day. The sway keeps going on top of it,
//           faster, and the ripple runs faster too.
//
// All of it is snapped to the 2px art grid, so a row moves by whole art pixels or
// not at all.

sampler uImage0 : register(s0);

float uTime;        // seconds
float2 uTexel;      // 1 / texture width, 1 / texture height
float uAnchorV;     // v of the shoulder line the cloth hangs from

float uRipple;      // ripple travel at the hem, texture pixels
float uRippleRate;  // multiplier on the ripple's speed
float uSway;        // sway travel at the hem, texture pixels
float uSwayRate;    // radians a second
float uDrag;        // steady lean at the hem, texture pixels, signed downwind

float4 CloakPass(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    // Which art row this is. Two texture rows to an art row.
    float row = floor(coords.y / (uTexel.y * 2.0));
    float v = (row + 0.5) * uTexel.y * 2.0;

    // 0 on the shoulder line, 1 at the bottom of the texture.
    float hang = saturate((v - uAnchorV) / (1.0 - uAnchorV));
    float bend = hang * hang;

    // Ripple: each row a little later in phase than the one above, so a bulge
    // visibly travels toward the hem.
    float phase = row * 0.45;
    float t = uTime * uRippleRate;
    float ripple = (sin(t * 2.1 + phase) * 0.7 + sin(t * 3.4 - phase * 1.7 + 1.3) * 0.3) * uRipple;

    // Sway: one slow lean for the whole cloth, with a smaller second beat so it is
    // not a metronome.
    float sway = (sin(uTime * uSwayRate) * 0.8 + sin(uTime * uSwayRate * 2.3 + 0.7) * 0.2) * uSway;

    float shift = (ripple + sway + uDrag) * bend;

    // Whole art pixels only.
    shift = floor(shift * 0.5 + 0.5) * 2.0;

    float2 uv = coords + float2(shift * uTexel.x, 0.0);

    // Off the edge of the texture is nothing, not a smear of the edge column.
    float inside = step(0.0, uv.x) * step(uv.x, 1.0);

    return tex2D(uImage0, uv) * color * inside;
}

technique Technique1
{
    pass CloakPass
    {
        PixelShader = compile ps_2_0 CloakPass();
    }
}
