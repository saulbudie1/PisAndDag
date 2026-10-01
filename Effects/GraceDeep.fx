// Beneath Grace: the screen filter under the Kingdom of Grace.
//
// COMPILE THIS BEFORE USE. tModLoader does not build .fx files during a mod build:
// run it through EasyXnb (Effects/Compiler/) and put the resulting GraceDeep.xnb in
// Effects/ beside this file. Until then the mod loads without it and logs one line;
// the water and the motes are there anyway, only the grade is not.
//
// The halls under the plain, where the light is candles and torches in a great deal of
// dark: the shadows sunk into a deep sapphire rather than into black, the middle a little
// quieter, and whatever is lit gone warm and gold, glowing past its edges. Moody, and still
// the kingdom it was.
//
//   SHADOW  the dark cooled toward uSecondaryColor, and faintly lit by it, so the black
//           is a blue you can see into; a slow haze of it drifts through the dark, fixed to
//           the world, not the screen.
//   MIDDLE  a little of the colour taken out, for the quiet.
//   LIGHT   lit things toward uColor, gold; richer, and the contrast raised a little.
//   GLOW    what is brightest glows past its edges in gold: three rings of eight taps.
//   EDGES   a heavier vignette than the plain's, blue in the corners.
//
// uOpacity is the game's own fade: the filter manager runs it 0 to 1 over a second when the
// biome is entered and back down when it is left, so the grade never snaps. uIntensity is
// the overall strength, set once at registration with the colours. Compiled for ps_3_0.

sampler uImage0 : register(s0);   // the frame

float3 uColor;            // the gold of the light
float3 uSecondaryColor;   // the blue of the dark
float2 uScreenPosition;   // the view's top-left, in the world
float2 uScreenResolution; // the view, in world pixels
float2 uZoom;
float uOpacity;
float uIntensity;
float uTime;              // seconds

// The glow off the brightest things: how much, and from how bright.
static const float Glow = 0.5;
static const float GlowFrom = 0.5;

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

float4 GraceDeepPass(float2 coords : TEXCOORD0) : COLOR0
{
    float4 frame = tex2D(uImage0, coords);
    float3 col = frame.rgb;
    float lum = Luma(col);

    // ---- the dark: cooled into sapphire and lit faintly by it
    float shadow = 1.0 - smoothstep(0.0, 0.34, lum);
    float3 cool = col * float3(0.80, 0.88, 1.10) + uSecondaryColor * 0.035;
    col = lerp(col, cool, shadow * 0.85);

    // A haze of it through the dark, in slow bands fixed to the world.
    float2 world = uScreenPosition + coords * uScreenResolution;
    float bands = 0.5 + 0.5 * sin(world.x * 0.0037 + world.y * 0.0021 + uTime * 0.23)
                            * sin(world.y * 0.0053 - world.x * 0.0011 - uTime * 0.17);
    col += uSecondaryColor * (0.05 * bands * shadow);

    // ---- the middle: a little quieter
    float mid = smoothstep(0.06, 0.36, lum) * (1.0 - smoothstep(0.42, 0.8, lum));
    float ml = Luma(col);
    col = lerp(col, ml.xxx, mid * 0.2);

    // ---- the light: toward gold, a little richer
    float high = smoothstep(0.32, 0.95, lum);
    float3 gold = col * (float3(0.62, 0.55, 0.42) + uColor * 0.5) + uColor * 0.025;
    col = lerp(col, gold, high * 0.7);
    float gl = Luma(col);
    col = lerp(gl.xxx, col, 1.0 + 0.18 * high);

    // Contrast, gently: a third of the way to an S curve.
    float3 c = saturate(col);
    col = lerp(col, c * c * (3.0 - 2.0 * c), 0.33);

    // ---- the glow off the brightest things: three rings of eight round the pixel
    float2 texel = 1.0 / (uScreenResolution * uZoom);
    float3 glow = Ring(coords, texel * 3.0)
                + Ring(coords, texel * 8.0) * 0.7
                + Ring(coords, texel * 15.0) * 0.45;

    col += glow / 17.2 * uColor * Glow;

    // ---- the edges: heavier than the plain's, and blue
    float2 off = coords - 0.5;
    float v = dot(off, off);
    col = col * (1.0 - v * 0.6) + uSecondaryColor * (0.025 * v);

    // uOpacity does the fade in and out; at 0 the frame passes through untouched.
    return float4(lerp(frame.rgb, saturate(col), uOpacity * uIntensity), frame.a);
}

technique Technique1
{
    pass GraceDeepPass
    {
        PixelShader = compile ps_3_0 GraceDeepPass();
    }
}
