// Kingdom of Grace screen filter.
//
// COMPILE THIS BEFORE USE. tModLoader does not build .fx files during a mod build:
// run it through EasyXnb (Effects/Compiler/) and put the resulting Grace.xnb in
// Effects/ beside this file. Until then the mod loads without it and logs one line;
// the motes still hang in the sun, only the light itself is not drawn.
//
// The plain in the light of a long golden afternoon, as the high plateaus of the old
// kingdoms are remembered: everything warmed toward amber, the brightest things gone to
// gold and glowing, the shadows lifted into a warm brown, and the sun laid over it all as
// it actually falls.
//
//   GRADE   the blue of the sky turned to a hazy gold; warmer overall, highlights toward
//           gold, shadows lifted into brown, a little more colour. Full in the day, less
//           than half of it at night.
//   SUN     uImage1 is how much sun reaches each tile on screen, and through what (GraceLight
//           works it out, a texel a tile: stone stops it, stained glass colours it, and a
//           window lets it into the room behind). Where it reaches, the air glows with it,
//           in soft bands along its way that drift, and what it falls on is warmed and
//           brightened. Where something shades it there is no glow, so a hall is crossed by
//           shafts from its windows and a tower throws a long shadow.
//   GLOW    the brightest things, gold and sunlit marble, glow a little past their edges.
//   GLARE   a warm bloom where vanilla draws the sun.
//
// uOpacity is the game's own fade: the filter manager runs it 0 to 1 over a second when the
// biome is entered and back down when it is left, so the light never snaps.
// uIntensity is the overall strength and is set once at registration. Compiled for ps_3_0.

sampler uImage0 : register(s0);   // the frame
sampler uImage1 : register(s1);   // the sun, a texel a tile

float3 uColor;            // the sun's colour
float3 uSecondaryColor;   // x, y: where vanilla draws the sun, as a share of the screen; z: day, 0 to 1
float2 uDirection;        // toward the sun, a unit vector, y down
float2 uImageOffset;      // the sun map's top-left, in world pixels from the view's top-left
float2 uImageSize1;       // the sun map's size, in texels (tiles)
float2 uScreenResolution; // the view, in world pixels
float2 uZoom;
float uOpacity;
float uIntensity;
float uTime;              // seconds

// How much the sunlit air glows, and how much warmer and brighter sunlit things are.
static const float Haze = 0.16;
static const float Shine = 0.30;

// The glow off the brightest things: how much, and from how bright.
static const float Glow = 0.45;
static const float GlowFrom = 0.68;

float Luma(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

// What this point gives off: how far past GlowFrom it is, in its own colour.
float3 Bright(float2 uv)
{
    float3 c = tex2D(uImage0, uv).rgb;
    return max(0.0, Luma(c) - GlowFrom) * c;
}

// What the eight points round coords at this far (in screen texels, each way) give off.
float3 Ring(float2 coords, float2 r)
{
    float d = 0.7071068;
    return Bright(coords + float2(r.x, 0.0))
         + Bright(coords - float2(r.x, 0.0))
         + Bright(coords + float2(0.0, r.y))
         + Bright(coords - float2(0.0, r.y))
         + Bright(coords + float2(r.x, r.y) * d)
         + Bright(coords - float2(r.x, r.y) * d)
         + Bright(coords + float2(r.x, -r.y) * d)
         + Bright(coords - float2(r.x, -r.y) * d);
}

// The sun at this point of the view, in world pixels from its top-left.
float3 SunAt(float2 world)
{
    float2 uv = (world - uImageOffset) / (uImageSize1 * 16.0);
    return tex2D(uImage1, uv).rgb;
}

float4 GracePass(float2 coords : TEXCOORD0) : COLOR0
{
    float4 frame = tex2D(uImage0, coords);
    float3 col = frame.rgb;
    float day = uSecondaryColor.z;

    // ---- the grade
    float grade = lerp(0.4, 1.0, day);

    // The sky, and whatever shows it (glass): bright blue goes to the gold of a hazy afternoon.
    float lum = Luma(col);
    float skyness = saturate((col.b - max(col.r, col.g)) * 4.0) * saturate(lum * 1.6);
    float3 golden = float3(1.0, 0.80, 0.50) * (0.5 + 0.4 * lum);
    col = lerp(col, golden, skyness * 0.6 * grade);

    // Warmer, the highlights toward gold, the shadows lifted into brown, a little more colour.
    lum = Luma(col);
    float3 warm = col * float3(1.05, 1.0, 0.9);
    float high = smoothstep(0.45, 1.0, lum);
    warm = lerp(warm, warm * float3(1.08, 0.98, 0.78) + float3(0.02, 0.01, 0.0), high * 0.4);
    warm += float3(0.045, 0.028, 0.010) * (1.0 - smoothstep(0.0, 0.32, lum));
    float wl = Luma(warm);
    warm = lerp(wl.xxx, warm, 1.1);
    col = lerp(col, warm, grade);

    // ---- the sun
    float2 world = coords * uScreenResolution;
    float3 sun = SunAt(world) * uColor * day;

    // In the air, in bands along the way it comes that drift slowly across.
    float2 across = float2(-uDirection.y, uDirection.x);
    float q = dot(world, across);
    float bands = 0.78 + 0.12 * sin(q * 0.021 + uTime * 0.35) + 0.10 * sin(q * 0.053 - uTime * 0.6);
    float3 air = sun * (Haze * bands);
    col = 1.0 - (1.0 - col) * (1.0 - air);

    // On what it falls on.
    col *= 1.0 + sun * Shine;

    // ---- the glow off the brightest things: two rings of eight round the pixel
    float2 texel = 1.0 / (uScreenResolution * uZoom);
    float3 glow = Ring(coords, texel * 3.0) + Ring(coords, texel * 7.0) * 0.6;

    col += glow / 12.8 * float3(1.0, 0.82, 0.5) * Glow * lerp(0.5, 1.0, day);

    // ---- the sun's glare
    float2 aspect = float2(uScreenResolution.x / uScreenResolution.y, 1.0);
    float2 toSun = (coords - uSecondaryColor.xy) * aspect;
    col += uColor * exp(-dot(toSun, toSun) * 5.0) * 0.18 * day;

    // ---- a warm vignette
    float2 off = coords - 0.5;
    float v = dot(off, off);
    col = col * (1.0 - v * 0.35) + float3(0.02, 0.01, 0.0) * v;

    // uOpacity does the fade in and out; at 0 the frame passes through untouched.
    return float4(lerp(frame.rgb, saturate(col), uOpacity * uIntensity), frame.a);
}

technique Technique1
{
    pass GracePass
    {
        PixelShader = compile ps_3_0 GracePass();
    }
}
