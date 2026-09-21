// Wasted Mile screen filter.
//
// COMPILE THIS BEFORE USE. tModLoader does not build .fx files during a mod build:
// run it through EasyXnb (Effects/Compiler/) and put the resulting WastedMile.xnb in
// Effects/ beside this file. Until then the mod loads without it and logs one line.
//
// Cold, dim, empty. Most of the colour goes; what is left is pushed toward a grey
// blue in the shadows and a dead, faintly brown grey in the light. The contrast is
// flattened so nothing is bright, and the edges of the screen fall off. Nothing
// moves and nothing is added over the frame.
//
// uOpacity is the game's own fade: the filter manager runs it 0 to 1 over a second
// when the biome is entered and back down when it is left, so the grade never snaps.
// uIntensity is the overall strength and is set once at registration.

sampler uImage0 : register(s0);

float uOpacity;
float uIntensity;

// How much colour survives. 0 keeps it all, 1 is grey.
static const float Drain = 0.70;

// The two ends of the grade. Shadows go here: cold, slightly blue...
static const float3 Shadow = float3(0.09, 0.10, 0.13);
// ...and highlights go here: grey with the last of the brown in it.
static const float3 Light = float3(0.66, 0.63, 0.57);

// How far the frame is pulled toward the grade.
static const float Grade = 0.50;

// Contrast taken out, and the ceiling: nothing gets brighter than this.
static const float Flatten = 0.88;
static const float Ceiling = 0.92;

// Corner darkening. 0 is off.
static const float Vignette = 0.60;

float4 MilePass(float2 coords : TEXCOORD0) : COLOR0
{
    float4 frame = tex2D(uImage0, coords);

    float lum = dot(frame.rgb, float3(0.299, 0.587, 0.114));

    // Drain the colour.
    float3 col = lerp(frame.rgb, lum.xxx, Drain);

    // Grade by brightness, then pull toward it.
    float3 graded = lerp(Shadow, Light, lum);
    col = lerp(col, graded, Grade);

    // Flatten, then cap.
    col = min(col * Flatten, Ceiling);

    // Vignette. coords are 0..1 across the screen; distance from the centre squared.
    float2 off = coords - 0.5;
    col *= 1.0 - dot(off, off) * Vignette;

    // uOpacity does the fade in and out; at 0 the frame passes through untouched.
    return float4(lerp(frame.rgb, col, uOpacity * uIntensity), frame.a);
}

technique Technique1
{
    pass MilePass
    {
        PixelShader = compile ps_2_0 MilePass();
    }
}
