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
    private static readonly ImFontPtr[] Fonts = new ImFontPtr[3];
    private static readonly float[] Sizes = new float[3];

    internal enum Role
    {
        Title = 0,
        Body = 1,
        Small = 2,
    }

    /// <summary>
    /// Takes this frame's font locks. Call once at the top of a window's draw, before
    /// anything measures or writes text.
    /// </summary>
    public static void BeginFrame()
    {
        Capture(Role.Title, Style.Fonts.Title);
        Capture(Role.Body, Style.Fonts.Body);
        Capture(Role.Small, Style.Fonts.Small);
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

    private static void Capture(Role role, Dalamud.Interface.ManagedFontAtlas.IFontHandle handle)
    {
        int i = (int)role;
        using (handle.Push())
        {
            Fonts[i] = ImGui.GetFont();
            Sizes[i] = ImGui.GetFontSize();
        }
    }
}
