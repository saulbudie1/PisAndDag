using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag
{
    public static partial class PADUtils
    {
        public static bool FinalExtraUpdate(this Projectile proj) => proj.numUpdates == -1;
        public static void ForceNetUpdate(this Projectile proj, bool ignoreCurrentNetSpam = true)
        {
            proj.netUpdate = true;
            if (proj.netSpam >= 10 || ignoreCurrentNetSpam)
                proj.netSpam = 0;
        }
        public static Vector2 SafeDirectionTo(this Entity entity, Vector2 destination, Vector2? fallback = null)
        {
            // Fall back to zero by default. default(Vector2) could be used in the parameter definition, but
            // this is more clear.
            if (!fallback.HasValue)
                fallback = Vector2.Zero;

            return (destination - entity.Center).SafeNormalize(fallback.Value);
        }

        public static readonly Color[] ExoPalette = new Color[]
        {
            new Color(250, 255, 112),
            new Color(211, 235, 108),
            new Color(166, 240, 105),
            new Color(105, 240, 220),
            new Color(64, 130, 145),
            new Color(145, 96, 145),
            new Color(242, 112, 73),
            new Color(199, 62, 62),
        };
        public static void DrawLineBetter(this SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float width)
		{
			// Draw nothing if the start and end are equal, to prevent division by 0 problems.
			if (start == end)
				return;

			start -= Main.screenPosition;
			end -= Main.screenPosition;

			Texture2D line = ModContent.Request<Texture2D>("PisAndDag/Content/ExtraTextures/Line").Value;
			float rotation = (end - start).ToRotation();
			Vector2 scale = new Vector2(Vector2.Distance(start, end) / line.Width, width);

			spriteBatch.Draw(line, start, null, color, rotation, line.Size() * Vector2.UnitY * 0.5f, scale, SpriteEffects.None, 0f);
		}
        public static Color MulticolorLerp(float increment, params Color[] colors)
        {
            increment %= 0.999f;
            int currentColorIndex = (int)(increment * colors.Length);
            Color currentColor = colors[currentColorIndex];
            Color nextColor = colors[(currentColorIndex + 1) % colors.Length];
            return Color.Lerp(currentColor, nextColor, increment * colors.Length % 1f);
        }
    }
    
    }