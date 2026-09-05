// Legends screen filter.
//
// COMPILE THIS BEFORE USE. tModLoader does not build .fx files during a mod build --
// run it through fxcompiler or EasyXnb and put the resulting LegendsShader.xnb in the
// mod's Effects/ folder. The .fx itself is source only; the game never reads it.
//
// A screen filter receives the finished frame as one texture. Tiles, walls, sky, player
// and NPCs are already composited together by that point, with no depth or stencil left,
// so there is no way to ask whether a pixel is foreground or background. Brightness is
// the only cue that survives, and it is what this shader splits on.

sampler uImage0 : register(s0);

float3 uColor;           // the "behind" colour  -- red
float3 uSecondaryColor;  // the "solid" colour   -- black
float uIntensity;        // 0 = untouched frame, 1 = fully stylised

float4 LegendsPass(float2 coords : TEXCOORD0) : COLOR0
{
    float4 frame = tex2D(uImage0, coords);

    // Perceptual luminance, not a flat average -- green carries most of the apparent
    // brightness, so weighting the channels equally would misjudge foliage and sky.
    float lum = dot(frame.rgb, float3(0.299, 0.587, 0.114));

    // A soft step rather than a gradient, so the result reads as two tones rather than
    // a tinted photograph. Widen the gap between these numbers for a softer boundary.
    float split = smoothstep(0.22, 0.42, lum);

    // Keep some of the original luminance in the red, or the background collapses into
    // a flat plate and you lose the shapes of trees and clouds entirely.
    float3 behind = uColor * (0.35 + 0.65 * lum);

    float3 stylised = lerp(uSecondaryColor, behind, split);

    // uIntensity does the fade in and out; at 0 the frame passes through untouched.
    return float4(lerp(frame.rgb, stylised, uIntensity), frame.a);
}

technique Technique1
{
    pass LegendsPass
    {
        PixelShader = compile ps_2_0 LegendsPass();
    }
}
