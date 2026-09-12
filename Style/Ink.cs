using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace WispUI.Style;

/// <summary>
/// Text output by font role. Nothing asks for a font by name or a size in pixels —
/// it asks for <see cref="Role.Title"/>, <see cref="Role.Body"/> or <see cref="Role.Small"/>.
/// <para>
/// This exists for one performance reason as much as for tidiness. Dalamud's
/// <c>IFontHandle.Push()</c> takes a font lock and hands it to a deferred-dispose queue,
/// so it allocates on every call. Pushing per widget would mean dozens of allocations per
/// frame, which is exactly what the draw-path rules forbid. So the handles are pushed
/// exactly once per frame in <see cref="BeginFrame"/>; the lock Dalamud took stays valid
/// until the frame ends, which lets everything after that draw and measure through a plain
/// pointer with no allocation at all.
/// </para>
/// </summary>
internal static class Ink
{
    /// <summary>
    /// Eight entries, not four: the window's four steps in Axis, then the same four steps in
    /// whatever face the HUD was set to. When the HUD is in Axis too — the default — the
    /// second four are copies of the first, so nothing extra is locked and nothing that draws
    /// has to ask which case it is in.
    /// </summary>
    private static readonly ImFontPtr[] Fonts = new ImFontPtr[8];
    private static readonly float[] Sizes = new float[8];

    /// <summary>Where the HUD's copy of the four steps starts.</summary>
    private const int HudBase = 4;

    internal enum Role
    {
        ScreenTitle = 0,
        Title = 1,
        Body = 2,
        Small = 3,
    }

    /// <summary>
    /// Takes this frame's font locks. Call once at the top of a window's draw, before
    /// anything measures or writes text.
    /// </summary>
    public static void BeginFrame()
    {
        Capture((int)Role.ScreenTitle, Style.Fonts.ScreenTitle);
        Capture((int)Role.Title, Style.Fonts.Title);
        Capture((int)Role.Body, Style.Fonts.Body);
        Capture((int)Role.Small, Style.Fonts.Small);

        for (int step = 0; step < HudBase; step++)
        {
            Dalamud.Interface.ManagedFontAtlas.IFontHandle? hud = Style.Fonts.HudStep(step);

            if (hud is null)
            {
                // The HUD writes in Axis, so it writes through the window's own handle. No
                // second lock, which is the whole reason the default face costs nothing.
                Fonts[HudBase + step] = Fonts[step];
                Sizes[HudBase + step] = Sizes[step];
                continue;
            }

            Capture(HudBase + step, hud);
        }
    }

    /// <summary>The height of one line in this role.</summary>
    public static float LineHeight(Role role) => Sizes[(int)role];

    /// <summary>Measures a string in the given role. Allocation free.</summary>
    public static Vector2 Measure(Role role, string text)
    {
        Push(role);
        Vector2 size = ImGui.CalcTextSize(text);
        Pop(role);
        return size;
    }

    /// <summary>Writes a string at an absolute screen position.</summary>
    public static void Draw(ImDrawListPtr dl, Role role, Vector2 pos, uint colour, string text)
    {
        int i = (int)role;
        if (Fonts[i].IsNull)
        {
            dl.AddText(pos, colour, text);
            return;
        }

        dl.AddText(Fonts[i], Sizes[i], pos, colour, text);
    }

    /// <summary>
    /// Writes a string with whatever was chosen to carry it, for text that lies over the game
    /// rather than over a panel of ours.
    /// </summary>
    public static void DrawEdged(ImDrawListPtr dl, Role role, Vector2 pos, uint colour, string text, TextEdge edge)
    {
        float offset = Style.Tokens.Metric.HudTextShadow;
        uint dark = Style.Tokens.Col.HudTextShadow;

        switch (edge)
        {
            case TextEdge.Shadow:
                Draw(dl, role, new Vector2(pos.X + offset, pos.Y + offset), dark, text);
                break;

            case TextEdge.Outline:
                // Four, not eight. At one pixel a ring of eight puts its diagonals on pixels
                // the four have already darkened, so the extra four cost a draw each per
                // string and change nothing on screen.
                Draw(dl, role, new Vector2(pos.X - offset, pos.Y), dark, text);
                Draw(dl, role, new Vector2(pos.X + offset, pos.Y), dark, text);
                Draw(dl, role, new Vector2(pos.X, pos.Y - offset), dark, text);
                Draw(dl, role, new Vector2(pos.X, pos.Y + offset), dark, text);
                break;
        }

        Draw(dl, role, pos, colour, text);
    }

    // --- Text at a size the user chose ---------------------------------------
    // The window's own text picks a role and gets that role's native size, which is the only
    // way a bitmap face is ever perfectly sharp. A HUD element is the one place where that is
    // not enough: how large a name on a party frame should be depends on how tall the frame
    // is, and only the person looking at it knows.
    //
    // So a HUD text asks for a size in pixels, and the nearest role is scaled to it. At the
    // native sizes it is exactly as sharp as the window; in between it softens a little, and
    // that is the honest trade for letting the size be chosen at all.

    /// <summary>The roles a free size can be scaled from, smallest first.</summary>
    private static readonly Role[] Scalable = { Role.Small, Role.Body, Role.Title, Role.ScreenTitle };

    /// <summary>
    /// Which role to scale from for a given pixel size: the smallest one that is at least as
    /// large as what was asked for. Scaling a bitmap face down keeps its edges, scaling it up
    /// softens them, so the source is never the smaller of the two unless there is nothing
    /// bigger to take.
    /// </summary>
    public static Role RoleFor(float pixels)
    {
        for (int i = 0; i < Scalable.Length; i++)
        {
            if (Sizes[(int)Scalable[i]] >= pixels)
            {
                return Scalable[i];
            }
        }

        return Role.ScreenTitle;
    }

    /// <summary>
    /// How wide a string is at a chosen pixel size. ImGui scales a font linearly, so this is
    /// the measured width times the ratio rather than a second measurement.
    /// </summary>
    public static float MeasureWidth(float pixels, string text)
    {
        // Measured in the HUD's own face, not the window's. A name is laid out against the
        // width it will actually take, and a wide face takes a different width from a narrow
        // one at the same size — measuring the window's Axis here would put every name in
        // MiedingerMid slightly out of place.
        int i = HudBase + (int)RoleFor(pixels);
        float native = Sizes[i];

        if (native <= 0f || Fonts[i].IsNull)
        {
            return 0f;
        }

        ImGui.PushFont(Fonts[i]);
        float width = ImGui.CalcTextSize(text).X;
        ImGui.PopFont();

        return width * (pixels / native);
    }

    /// <summary>Writes a string at a chosen pixel size, with whatever was chosen to carry it.</summary>
    public static void DrawScaledEdged(
        ImDrawListPtr dl,
        float pixels,
        Vector2 pos,
        uint colour,
        string text,
        TextEdge edge)
    {
        float offset = Style.Tokens.Metric.HudTextShadow;
        uint dark = Style.Tokens.Col.HudTextShadow;

        switch (edge)
        {
            case TextEdge.Shadow:
                DrawScaled(dl, pixels, new Vector2(pos.X + offset, pos.Y + offset), dark, text);
                break;

            case TextEdge.Outline:
                DrawScaled(dl, pixels, new Vector2(pos.X - offset, pos.Y), dark, text);
                DrawScaled(dl, pixels, new Vector2(pos.X + offset, pos.Y), dark, text);
                DrawScaled(dl, pixels, new Vector2(pos.X, pos.Y - offset), dark, text);
                DrawScaled(dl, pixels, new Vector2(pos.X, pos.Y + offset), dark, text);
                break;
        }

        DrawScaled(dl, pixels, pos, colour, text);
    }

    /// <summary>Writes a string at a chosen pixel size, in the face the HUD was set to.</summary>
    public static void DrawScaled(ImDrawListPtr dl, float pixels, Vector2 pos, uint colour, string text)
    {
        int i = HudBase + (int)RoleFor(pixels);
        if (Fonts[i].IsNull)
        {
            dl.AddText(pos, colour, text);
            return;
        }

        dl.AddText(Fonts[i], pixels, pos, colour, text);
    }

    /// <summary>
    /// Pushes a role onto the ImGui font stack for code that uses the normal widget flow
    /// instead of a draw list. Allocation free, because the lock was already taken this frame.
    /// Always pair it with <see cref="Pop"/>.
    /// </summary>
    public static void Push(Role role)
    {
        int i = (int)role;
        if (!Fonts[i].IsNull)
        {
            ImGui.PushFont(Fonts[i]);
        }
    }

    /// <summary>Undoes a <see cref="Push"/>. Safe to call even if the push was skipped.</summary>
    public static void Pop(Role role)
    {
        if (!Fonts[(int)role].IsNull)
        {
            ImGui.PopFont();
        }
    }

    private static void Capture(int slot, Dalamud.Interface.ManagedFontAtlas.IFontHandle handle)
    {
        using (handle.Push())
        {
            Fonts[slot] = ImGui.GetFont();
            Sizes[slot] = ImGui.GetFontSize();
        }
    }
}
