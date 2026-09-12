using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace WispUI.Style;

/// <summary>
/// Text output. The window asks by role — <see cref="Role.Title"/>, <see cref="Role.Body"/>,
/// <see cref="Role.Small"/> — and the HUD asks by pixel size.
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
    private static readonly ImFontPtr[] Fonts = new ImFontPtr[4];
    private static readonly float[] Sizes = new float[4];

    /// <summary>
    /// The HUD's own faces, one per size in use, mirroring what <see cref="Style.Fonts"/>
    /// holds. Separate from the four above because the HUD's sizes are whatever the player
    /// set, not four fixed steps.
    /// </summary>
    private static readonly ImFontPtr[] HudFonts = new ImFontPtr[4];
    private static readonly float[] HudPx = new float[4];
    private static int s_hudCount;

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
        Capture(0, Style.Fonts.ScreenTitle);
        Capture(1, Style.Fonts.Title);
        Capture(2, Style.Fonts.Body);
        Capture(3, Style.Fonts.Small);

        s_hudCount = 0;

        for (int i = 0; i < Style.Fonts.HudCount && i < HudFonts.Length; i++)
        {
            Dalamud.Interface.ManagedFontAtlas.IFontHandle? handle = Style.Fonts.HudHandleAt(i);

            // 🔴 Available, not just non-null. A handle is returned the moment it is asked for
            // and is only backed by a real face once the atlas has been rebuilt, which is a
            // frame or more later. Pushing it before then hands back the default face instead
            // — silently, so the HUD draws in a font nobody chose and nothing says why.
            if (handle is null || !handle.Available)
            {
                continue;
            }

            using (handle.Push())
            {
                HudFonts[s_hudCount] = ImGui.GetFont();
                HudPx[s_hudCount] = Style.Fonts.HudSizeAt(i);
            }

            s_hudCount++;
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

    // --- Text at a size the player chose --------------------------------------
    // The window's own text picks a role and gets that role's native size. A HUD element
    // cannot work that way: how large a name on a party frame should be depends on how tall
    // the frame is, and only the person looking at it knows.
    //
    // So the HUD's handles are built for exactly the sizes in use (Style.Fonts.SyncHud), and
    // a draw finds the one that matches. With a vector face that is a glyph rasterised for
    // this size and nothing else — sharp at any size. With one of the game's bitmap faces it
    // is still the nearest size the game ships, resampled, because that is all a bitmap can
    // ever be (Florian, 2026-09-12).

    /// <summary>The roles a free size can fall back to when the HUD holds no handle at all.</summary>
    private static readonly Role[] Scalable = { Role.Small, Role.Body, Role.Title, Role.ScreenTitle };

    /// <summary>
    /// Which window role to scale from, for the case where the HUD has no handles yet — the
    /// first frames after a load, and any frame where a font file was missing.
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
    /// Which held HUD face to write this size in: the one built closest to it, which is
    /// normally the one built for exactly it. Returns -1 when the HUD holds none.
    /// </summary>
    private static int HudIndex(float pixels)
    {
        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < s_hudCount; i++)
        {
            if (HudFonts[i].IsNull)
            {
                continue;
            }

            float distance = MathF.Abs(HudPx[i] - pixels);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>How wide a string is at a chosen pixel size, in the face the HUD writes in.</summary>
    public static float MeasureWidth(float pixels, string text)
    {
        int i = HudIndex(pixels);
        ImFontPtr font = i >= 0 ? HudFonts[i] : Fonts[(int)RoleFor(pixels)];
        float native = i >= 0 ? HudPx[i] : Sizes[(int)RoleFor(pixels)];

        if (native <= 0f || font.IsNull)
        {
            return 0f;
        }

        // Measured in the face the text will actually be drawn in. A wide face takes a
        // different width from a narrow one at the same size, so measuring anything else here
        // would put every name slightly out of place.
        ImGui.PushFont(font);
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
        float offset = EdgeWidth(pixels);

        switch (edge)
        {
            case TextEdge.Shadow:
                DrawScaled(dl, pixels, Snap(pos, offset * 2f, offset * 2f), Style.Tokens.Col.HudTextShadowFar, text);
                DrawScaled(dl, pixels, Snap(pos, offset, offset), Style.Tokens.Col.HudTextShadow, text);
                break;

            case TextEdge.Outline:
                uint dark = Style.Tokens.Col.HudTextOutline;

                // The four sides, then the four corners. At one pixel the corners land where
                // the sides already are and cost nothing but four draws — but the width grows
                // with the text now, and at two pixels and up a ring of four leaves the
                // diagonals open, which is exactly the fraying that was reported (Florian,
                // 2026-09-12).
                DrawScaled(dl, pixels, Snap(pos, -offset, 0f), dark, text);
                DrawScaled(dl, pixels, Snap(pos, offset, 0f), dark, text);
                DrawScaled(dl, pixels, Snap(pos, 0f, -offset), dark, text);
                DrawScaled(dl, pixels, Snap(pos, 0f, offset), dark, text);

                if (offset > 1f)
                {
                    DrawScaled(dl, pixels, Snap(pos, -offset, -offset), dark, text);
                    DrawScaled(dl, pixels, Snap(pos, offset, -offset), dark, text);
                    DrawScaled(dl, pixels, Snap(pos, -offset, offset), dark, text);
                    DrawScaled(dl, pixels, Snap(pos, offset, offset), dark, text);
                }

                break;
        }

        DrawScaled(dl, pixels, pos, colour, text);
    }

    /// <summary>
    /// Writes a string at a chosen pixel size in the <em>interface</em> face — Axis — whatever
    /// the HUD has been set to.
    /// <para>
    /// 🔴 For what the plugin says, as opposed to what the frame shows. A name, a health
    /// figure and a party number belong to the frame and follow the player's chosen face; a
    /// status the plugin reports does not, and in a serif face it would read as part of the
    /// design rather than as a message (Florian, 2026-09-12). Axis is also what the game
    /// itself states conditions in.
    /// </para>
    /// </summary>
    public static void DrawNote(ImDrawListPtr dl, float pixels, Vector2 pos, uint colour, string text, TextEdge edge)
    {
        float offset = EdgeWidth(pixels);

        if (edge == TextEdge.Shadow)
        {
            DrawInterface(dl, pixels, Snap(pos, offset, offset), Style.Tokens.Col.HudTextShadow, text);
        }

        DrawInterface(dl, pixels, pos, colour, text);
    }

    /// <summary>How wide a string is at this size in the interface face.</summary>
    public static float MeasureNote(float pixels, string text)
    {
        int role = (int)RoleFor(pixels);
        float native = Sizes[role];

        if (native <= 0f || Fonts[role].IsNull)
        {
            return 0f;
        }

        ImGui.PushFont(Fonts[role]);
        float width = ImGui.CalcTextSize(text).X;
        ImGui.PopFont();

        return width * (pixels / native);
    }

    private static void DrawInterface(ImDrawListPtr dl, float pixels, Vector2 pos, uint colour, string text)
    {
        int role = (int)RoleFor(pixels);

        if (Fonts[role].IsNull)
        {
            dl.AddText(pos, colour, text);
            return;
        }

        dl.AddText(Fonts[role], pixels, pos, colour, text);
    }

    /// <summary>Writes a string at a chosen pixel size, in the face the HUD was set to.</summary>
    public static void DrawScaled(ImDrawListPtr dl, float pixels, Vector2 pos, uint colour, string text)
    {
        int i = HudIndex(pixels);

        if (i >= 0)
        {
            dl.AddText(HudFonts[i], pixels, pos, colour, text);
            return;
        }

        int role = (int)RoleFor(pixels);

        if (Fonts[role].IsNull)
        {
            dl.AddText(pos, colour, text);
            return;
        }

        dl.AddText(Fonts[role], pixels, pos, colour, text);
    }

    /// <summary>
    /// How thick the edge under a text of this size is, in whole pixels.
    /// <para>
    /// 🔴 It follows the text rather than being one pixel for everything. One pixel is right
    /// at sixteen and invisible at forty, which is what a fixed width always ends up being at
    /// one end of a range the user can set (Florian, 2026-09-12). Whole pixels because half a
    /// one is what made the outline look soft: a glyph drawn at a fractional offset is
    /// resampled across two pixel columns and comes back grey.
    /// </para>
    /// </summary>
    private static float EdgeWidth(float pixels)
    {
        float scaled = Style.Tokens.Metric.HudTextShadow;

        // 🔴 The step is late on purpose. It used to thicken at sixteen pixels, which put it
        // inside the range people actually set a name to — so nudging a size across that line
        // visibly jumped the outline, and the thicker line read as clumsy at sizes that did
        // not need it (Florian, 2026-09-12). At twenty-eight and up the text is large enough
        // that a single pixel genuinely disappears, and nobody is fine-tuning there.
        return pixels < 28f ? scaled : scaled * 2f;
    }

    /// <summary>
    /// A position nudged by whole pixels. The nudge is rounded, not the result: the text's own
    /// position is where the layout put it, and moving it to a pixel boundary here would move
    /// the letters rather than the edge under them.
    /// </summary>
    private static Vector2 Snap(Vector2 pos, float dx, float dy) =>
        new(pos.X + MathF.Round(dx), pos.Y + MathF.Round(dy));

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
