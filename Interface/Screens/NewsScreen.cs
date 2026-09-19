using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Data;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// What is new, release by release, with the lines that have a setting behind them
/// clickable.
/// <para>
/// 🔴 Not a settings screen and deliberately not shaped like one. There are no groups, no
/// two-column grid and no rows: a release is a heading and a list of sentences, and dressing
/// that as settings would make it look like something to configure.
/// </para>
/// <para>
/// Reached from the card in the navigation footer rather than from a nav row of its own.
/// The nav list is short on purpose, and a permanent entry for something read once per
/// release would be the first thing to make it long.
/// </para>
/// </summary>
internal sealed class NewsScreen
{
    private const string IdRow = "##wisp-news-row";

    /// <summary>Which line was clicked this frame, handed to the window that can act on it.</summary>
    public NewsEntry? Clicked { get; private set; }

    public void Draw(float width)
    {
        this.Clicked = null;

        Vector2 origin = ImGui.GetCursorScreenPos();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float y = origin.Y;

        for (int r = 0; r < News.Releases.Length; r++)
        {
            ref readonly NewsRelease release = ref News.Releases[r];

            if (r > 0)
            {
                y += Tokens.Space.Lg;
                Chrome.Rule(dl, origin.X, origin.X + width, y);
                y += Tokens.Space.Lg;
            }

            y = DrawHead(dl, origin.X, y, width, in release);

            ImGui.PushID(r);

            for (int e = 0; e < release.Entries.Length; e++)
            {
                y = this.DrawEntry(dl, origin.X, y, width, release.Entries[e], e);
            }

            ImGui.PopID();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>The version, and the date beside it in the quiet colour.</summary>
    private static float DrawHead(ImDrawListPtr dl, float x, float y, float width, in NewsRelease release)
    {
        Ink.Draw(dl, Ink.Role.ScreenTitle, new Vector2(x, y), Tokens.Col.Heading, release.Version);

        float versionWidth = MathF.Round(Ink.Measure(Ink.Role.ScreenTitle, release.Version).X);
        float dateY = MathF.Round(y + Ink.LineHeight(Ink.Role.ScreenTitle) - Ink.LineHeight(Ink.Role.Small));
        Ink.Draw(
            dl,
            Ink.Role.Small,
            new Vector2(x + versionWidth + Tokens.Space.Md, dateY),
            Tokens.Col.InkFaint,
            release.Date);

        y += Ink.LineHeight(Ink.Role.ScreenTitle) + Tokens.Space.Xs;

        Ink.Draw(dl, Ink.Role.Body, new Vector2(x, y), Tokens.Col.Ink, release.Summary);
        return y + Ink.LineHeight(Ink.Role.Body) + Tokens.Space.Lg;
    }

    /// <summary>
    /// One line: the kind on the left in its own colour, the sentence beside it, and — only
    /// where the line has somewhere to go — a chevron against the right edge.
    /// </summary>
    private float DrawEntry(ImDrawListPtr dl, float x, float y, float width, NewsEntry entry, int index)
    {
        float kindWidth = Tokens.Metric.NewsKindColumn;
        float chevron = entry.Jumps ? Tokens.Metric.NewsChevron + Tokens.Space.Md : 0f;
        float textX = x + kindWidth;
        float textWidth = width - kindWidth - chevron;

        // Measured first, because a sentence wraps and the row is as tall as it turned out.
        float textHeight = Ink.MeasureWrapped(Ink.Role.Body, entry.Text, textWidth).Y;
        float height = MathF.Max(textHeight, Ink.LineHeight(Ink.Role.Body)) + (Tokens.Space.Sm * 2f);

        ImGui.PushID(index);
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(IdRow, new Vector2(width, height));
        bool hovered = entry.Jumps && ImGui.IsItemHovered();
        Chrome.ShowHand(hovered);

        if (hovered && ImGui.IsItemClicked())
        {
            this.Clicked = entry;
        }

        ImGui.PopID();

        if (hovered)
        {
            dl.AddRectFilled(
                new Vector2(x - Tokens.Space.Sm, y),
                new Vector2(x + width + Tokens.Space.Sm, y + height),
                Tokens.Col.RowHover,
                Tokens.Radius.Small);
        }

        float textY = y + Tokens.Space.Sm;

        Ink.Draw(dl, Ink.Role.Small, new Vector2(x, textY + Tokens.Px(2f)), KindColour(entry.Kind), KindLabel(entry.Kind));

        Ink.DrawWrapped(
            Ink.Role.Body,
            new Vector2(textX, textY),
            textWidth,
            hovered ? Tokens.Col.Ink : Tokens.Col.InkSoft,
            entry.Text);

        if (entry.Jumps)
        {
            // A chevron and nothing else. The path it leads to is on screen a click later,
            // and naming it here would put a breadcrumb on every second line.
            float size = Tokens.Metric.NewsChevron;
            float cx = x + width - size;
            float cy = MathF.Round(y + (height * 0.5f));
            uint ink = hovered ? Tokens.Col.GoldHi : Tokens.Col.InkFaint;

            dl.AddTriangleFilled(
                new Vector2(cx, cy - (size * 0.45f)),
                new Vector2(cx + (size * 0.55f), cy),
                new Vector2(cx, cy + (size * 0.45f)),
                ink);
        }

        return y + height;
    }

    private static string KindLabel(NewsKind kind) => kind switch
    {
        NewsKind.New => Strings.NewsNew,
        NewsKind.Changed => Strings.NewsChanged,
        _ => Strings.NewsFixed,
    };

    /// <summary>
    /// Three colours, and only one of them is the accent. New is what somebody came to read;
    /// changed and fixed are the quiet half of a release and are coloured like it.
    /// </summary>
    private static uint KindColour(NewsKind kind) => kind switch
    {
        NewsKind.New => Tokens.Col.Gold,
        NewsKind.Changed => Tokens.Col.Heading,
        _ => Tokens.Col.InkFaint,
    };
}
