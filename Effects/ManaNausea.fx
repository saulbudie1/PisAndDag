// Mana Nausea screen filter.
//
// COMPILE THIS BEFORE USE. tModLoader does not build .fx files during a mod build:
// run it through EasyXnb (Effects/Compiler/) and put the resulting ManaNausea.xnb in
// Effects/ beside this file. Until then the mod loads without it and logs one line;
// the buffs still work, only the screen stays still.
//
// What too much mana does to the eyes. One pass for all three strengths; ManaNauseaScreen
// works out the numbers from the player's buffs every tick and passes them in.
//
//   SWIM    the picture slides about in slow waves, never quite repeating, and swells
//           and sinks about the player like breathing. How far it moves is uIntensity,
//           in screen heights.
//   REACH   where the swim is felt. Nothing inside uColor.x, all of it past uColor.y,
//           measured from the player: in screen heights round them (a circle), or, as
//           uSecondaryColor.y goes to 1, toward the edges of the screen (1 is the middle
//           of each edge). I is the edges only; II everything but a circle round the
//           player; III everything, the player too.
//   TURN    III: the whole picture turned about the middle of the screen by
//           uSecondaryColor.z, radians. ManaNauseaScreen eases it round to upside down
//           while the kaleidoscope hides it, and back.
//   SLICES  III: the kaleidoscope. The screen cut into eight equal slices about its
//           middle, every other one mirrored, all of them showing the same wedge of the
//           world; the slices turn by uDirection.x. uSecondaryColor.x is how much of it
//           there is: at 1 nothing else shows.
//   TRIP    III, run into over several seconds (uColor.z): the colours come apart, the
//           hues roll outward from the player in rings, everything too bright and too
//           saturated.
//
// Off the edge of the screen the picture is mirrored back in, so a turned or bent view
// never smears the edge pixels.
//
// uOpacity is the filter manager's own fade in and out. Compiled for ps_3_0: ps_2_0 runs
// out of instruction slots.

sampler uImage0 : register(s0);

float3 uColor;            // x: clear radius, y: full radius, z: trip 0..1
float3 uSecondaryColor;   // x: slices 0..1, y: edge metric 0..1, z: turn, radians
float2 uDirection;        // x: the slices' own turn, radians
float uOpacity;
float uIntensity;         // swim, screen heights
float uTime;              // seconds
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;   // the player's centre, world

static const float TwoPi = 6.2831853;

// Screen coordinates folded back into the screen by mirroring at every edge.
float2 Mirror(float2 uv)
{
    return 1.0 - abs(1.0 - abs(fmod(uv, 2.0)));
}

float2 Turn(float2 v, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

// The picture at uv, its red and blue pulled apart along split.
float3 Split(float2 uv, float2 split)
{
    return float3(tex2D(uImage0, Mirror(uv + split)).r,
                  tex2D(uImage0, Mirror(uv)).g,
                  tex2D(uImage0, Mirror(uv - split)).b);
}

// Colour turned about the grey axis by a (radians): the hue moves, the brightness stays.
float3 HueTurn(float3 col, float a)
{
    const float3 k = float3(0.57735, 0.57735, 0.57735);
    float c = cos(a);
    return col * c + cross(k, col) * sin(a) + k * dot(k, col) * (1.0 - c);
}

float4 NauseaPass(float2 coords : TEXCOORD0) : COLOR0
{
    float t = uTime;
    float aspect = uScreenResolution.x / uScreenResolution.y;
    float2 toAspect = float2(aspect, 1.0);
    float2 toScreen = float2(1.0 / aspect, 1.0);

    // The player on the screen, and this pixel's offset from them, in screen heights.
    float2 target = (uTargetPosition - uScreenPosition) / uScreenResolution;
    float2 d = (coords - target) * toAspect;
    float dist = length(d);
    float2 dir = d / max(dist, 0.0001);

    // Reach: a circle round the player, or the edges of the screen.
    float edge = length((coords - 0.5) * 2.0);
    float reach = smoothstep(uColor.x, uColor.y, lerp(dist, edge, uSecondaryColor.y));

    // Swim: three waves each way, at speeds that never line up, and the breath.
    float2 q = coords * toAspect;
    float2 swim;
    swim.x = sin(q.y * 7.0 + t * 1.3) * 0.55
           + sin((q.x + q.y) * 11.0 - t * 1.9) * 0.30
           + sin(q.y * 23.0 + t * 3.1) * 0.15;
    swim.y = sin(q.x * 6.0 - t * 1.1) * 0.55
           + sin((q.x - q.y) * 10.0 + t * 1.7) * 0.30
           + sin(q.x * 19.0 - t * 2.7) * 0.15;
    float breath = sin(t * 1.6) * 0.5 + sin(t * 0.61) * 0.25;
    float2 shift = (swim + dir * breath) * (uIntensity * uOpacity * reach);

    // Everything below turns about the middle of the screen, in screen heights from it.
    float2 m = (coords - 0.5) * toAspect;
    float turn = uSecondaryColor.z * uOpacity;

    // The view as it is, turned and swum.
    float2 direct = Turn(m, -turn) + shift;

    // The slices: eight about the middle, every other one mirrored, one wedge of the view in all.
    float slice = TwoPi / 8.0;
    float spin = uDirection.x;
    float a = atan2(m.y, m.x) - spin;
    float w = fmod(a + TwoPi * 64.0, 2.0 * slice);
    float folded = slice - abs(w - slice);
    float2 wedge = Turn(float2(cos(folded + spin), sin(folded + spin)) * length(m), -turn) + shift;

    float trip = uColor.z * uOpacity;
    float slices = uSecondaryColor.x * uOpacity;

    // The colours come apart along the line out from the player.
    float2 split = dir * (0.004 + 0.004 * sin(t * 2.3)) * trip * toScreen;

    float3 col = Split(0.5 + direct * toScreen, split);
    float3 kal = Split(0.5 + wedge * toScreen, split);
    col = lerp(col, kal, slices);

    // Hues roll outward from the player in rings.
    float wave = t * 0.9 - dist * 5.0 + sin(t * 0.37) * 1.5;
    col = HueTurn(col, trip * wave * 1.3);

    // Too bright, too saturated.
    float lum = dot(col, float3(0.299, 0.587, 0.114));
    col = lerp(lum.xxx, col, 1.0 + 0.9 * trip);
    float3 rainbow = 0.5 + 0.5 * cos(TwoPi * (wave * 0.25 + float3(0.0, 0.33, 0.67)));
    col = lerp(col, col * (0.7 + 0.9 * rainbow), 0.5 * trip);
    col *= 1.0 + 0.15 * trip * sin(t * 3.0 - dist * 9.0);

    return float4(col, tex2D(uImage0, coords).a);
}

technique Technique1
{
    pass NauseaPass
    {
        PixelShader = compile ps_3_0 NauseaPass();
    }
}
