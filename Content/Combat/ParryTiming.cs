namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// How long a parry takes, in ticks: the wind before it can turn anything aside, the
    /// moment it can, and the time after in which it cannot and whoever made it can do
    /// nothing else. A weapon that parries says which of these it has (ParryPlayer.Begin).
    /// </summary>
    public readonly struct ParryTiming
    {
        /// <summary>Ticks from the press before it turns anything.</summary>
        public readonly int Startup;

        /// <summary>Ticks it turns things for.</summary>
        public readonly int Active;

        /// <summary>Ticks after that before anything else can be done.</summary>
        public readonly int Recovery;

        public ParryTiming(int startup, int active, int recovery)
        {
            Startup = startup < 0 ? 0 : startup;
            Active = active < 1 ? 1 : active;
            Recovery = recovery < 0 ? 0 : recovery;
        }

        /// <summary>The whole of it.</summary>
        public int Total => Startup + Active + Recovery;

        /// <summary>
        /// Elden Ring's Parry on a curved sword: 3 frames before it, 4 of it, 14 after, at 30
        /// frames a second (eldenring.wiki.gg, Parrying). Twice that in ticks: a tenth of a
        /// second in, 0.13 seconds of it, 0.47 seconds after.
        /// </summary>
        public static readonly ParryTiming CurvedSword = new ParryTiming(6, 8, 28);
    }
}
