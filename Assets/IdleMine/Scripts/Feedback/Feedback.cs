namespace IdleMine
{
    /// <summary>
    /// One call for "this happened": plays the sound and the matching haptic. Strengths scale with how much
    /// the moment matters: a button tick, a light tap per dig, a solid thump for purchases and new layers,
    /// a heavy hit for keystones, Motherlodes and ascending, and the system success/warning patterns for
    /// rewards and "can't do that".
    /// </summary>
    public static class Feedback
    {
        public static void Play(Sfx sfx, float volume = 1f)
        {
            AudioManager.Play(sfx, volume);
            Haptics.Play(HapticFor(sfx));
        }

        static Haptic HapticFor(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click: return Haptic.Selection;
                case Sfx.Dig: return Haptic.Light;
                case Sfx.Crit: return Haptic.Rigid;
                case Sfx.Coins: return Haptic.Light;
                case Sfx.Purchase: return Haptic.Medium;
                case Sfx.Keystone: return Haptic.Heavy;
                case Sfx.Deny: return Haptic.Warning;
                case Sfx.Breakthrough: return Haptic.Medium;
                case Sfx.Motherlode: return Haptic.Heavy;
                case Sfx.Reward: return Haptic.Success;
                case Sfx.Cart: return Haptic.Soft;
                case Sfx.Ascend: return Haptic.Heavy;
                default: return Haptic.Light;
            }
        }
    }
}
