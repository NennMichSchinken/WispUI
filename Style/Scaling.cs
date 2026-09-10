using Dalamud.Game.Config;
using WispUI.Core;

namespace WispUI.Style;

/// <summary>
/// Works out the interface scale. FFXIV has no virtual UI grid to snap to the way WoW does,
/// so the effect is built instead: one factor on every token, and whole-pixel rounding on
/// every value that reaches the screen (see <see cref="Tokens.Px"/>).
/// </summary>
internal static class Scaling
{
    /// <summary>Applies the scale the configuration asks for and rebuilds the fonts for it.</summary>
    public static void Apply(Configuration config)
    {
        float previous = Tokens.Scale;
        Tokens.SetScale(config.ScaleFollowsGame ? FromGame() : config.ManualScale);

        // Font handles carry a baked pixel size, so they only need rebuilding when the
        // scale actually moved — or the very first time round.
        if (!Fonts.Ready || previous != Tokens.Scale)
        {
            Fonts.Rebuild();
        }
    }

    private static float FromGame()
    {
        if (!Services.GameConfig.TryGet(SystemConfigOption.UiBaseScale, out uint raw))
        {
            Services.Log.Information("Could not read the game's UI base scale; using 1.00.");
            return 1f;
        }

        // Read as a percentage, which is how every setup we have seen reports it. Anything
        // outside a sane range is left alone rather than guessed at. The raw value is logged
        // so the first in-game run can confirm the reading instead of us assuming it.
        float derived = raw >= 50 && raw <= 400 ? raw / 100f : 1f;
        Services.Log.Information("Game UI base scale reported as {Raw}; using a factor of {Factor:0.00}.", raw, derived);
        return derived;
    }
}
