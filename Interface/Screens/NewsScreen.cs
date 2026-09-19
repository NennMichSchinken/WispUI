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
/// two-column grid and no settings rows: a release is a heading and a list of sentences,
/// and dressing that as settings would make it look like something to configure.
/// </para>
/// <para>
/// 🔴 Grouped by kind, not labelled per line. The first build put New, Changed or Fixed in
/// front of every sentence, which cost a column down the left and read as an untidy list
/// (Florian, 2026-09-20). Three headings say the same thing once each, and the eye can
/// tell in a second whether a release brings something or repairs something.
/// </para>
/// <para>
/// Every line sits on its own surface with a few pixels between. Sentences directly on the
/// page ran together; a surface makes each one a thing you can look at, and it gives the
/// hover somewhere to happen.
/// </para>
/// </summary>
internal sealed class NewsScreen
{
    private const string IdRow = "##wisp-news-row";

    /// <summary>The order the sections appear in: what it brings, then what it changes, then what it repairs.</summary>
    private static readonly NewsKind[] Sections = { NewsKind.New, NewsKind.Changed, NewsKind.Fixed };

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
                y += Tokens.Metric.NewsReleaseGap;
                Chrome.Rule(dl, origin.X, origin.X + width, y);
                y += Tokens.Metric.NewsReleaseGap;
            }

            y = DrawHead(dl, origin.X, y, width, in release);

            ImGui.PushID(r);

            for (int s = 0; s < Sections.Length; s++)
            {
                y = this.DrawSection(dl, origin.X, y, width, in release, Sections[s], s);
            }

            ImGui.PopID();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>The version, the date beside it, and the one line saying what the release was.</summary>
    private static float DrawHead(ImDrawListPtr dl, float x, float y, float width, in NewsRelease release)
    {
        _ = width;

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
        return y + Ink.LineHeight(Ink.Role.Body);
    }

    /// <summary>
    /// One kind's heading and its lines, or nothing at all when a release brought none of
    /// that kind. An empty "Fixed" under a release that fixed nothing is a heading that
    /// makes the reader look for something that is not there.
    /// </summary>
    private float DrawSection(
        ImDrawListPtr dl,
        float x,
        float y,
        float width,
        in NewsRelease release,
        NewsKind kind,
        int section)
    {
        int count = 0;

        for (int i = 0; i < release.Entries.Length; i++)
        {
            if (release.Entries[i].Kind == kind)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return y;
        }

        y += Tokens.Metric.NewsSectionGap;

        // The word, and a hairline running from it to the right edge. The line is what
        // makes a heading a heading rather than just a shorter sentence above the others.
        string label = KindLabel(kind);
        float labelWidth = MathF.Round(Ink.Measure(Ink.Role.Small, label).X);

        Ink.Draw(dl, Ink.Role.Small, new Vector2(x, y), KindColour(kind), label);
        Chrome.Hairline(
            dl,
            x + labelWidth + Tokens.Space.Md,
            x + width,
            MathF.Round(y + (Ink.LineHeight(Ink.Role.Small) * 0.5f)),
            Tokens.Col.RowDivider);

        y += Ink.LineHeight(Ink.Role.Small) + Tokens.Space.Md;

        ImGui.PushID(section);
        int row = 0;

        for (int i = 0; i < release.Entries.Length; i++)
        {
            if (release.Entries[i].Kind != kind)
            {
                continue;
            }

            y = this.DrawEntry(dl, x, y, width, release.Entries[i], row++);
        }

        ImGui.PopID();
        return y;
    }

    /// <summary>
    /// One line on its own surface: the sentence, and — only where the line has somewhere
    /// to go — a chevron against the right edge.
    /// </summary>
    private float DrawEntry(ImDrawListPtr dl, float x, float y, float width, NewsEntry entry, int index)
    {
        float padX = Tokens.Metric.NewsRowPaddingX;
        float padY = Tokens.Metric.NewsRowPaddingY;
        float chevron = entry.Jumps ? Tokens.Metric.NewsChevron + padX : 0f;
        float textX = x + padX;
        float textWidth = width - (padX * 2f) - chevron;

        // Measured first, because a sentence wraps and the row is as tall as it turned out.
        float textHeight = MathF.Max(
            Ink.MeasureWrapped(Ink.Role.Body, entry.Text, textWidth).Y,
            Ink.LineHeight(Ink.Role.Body));

        float height = MathF.Round(textHeight + (padY * 2f));

        ImGui.PushID(index);
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(IdRow, new Vector2(width, height));

        // 🔴 Both asked here, before anything is drawn. Drawing a text is an ImGui item
        // too, and IsItemClicked further down would be asking about the sentence rather
        // than about the row — which is exactly how the news card came to do nothing at
        // all (2026-09-20).
        bool hovered = entry.Jumps && ImGui.IsItemHovered();
        bool clicked = hovered && ImGui.IsItemClicked();

        Chrome.ShowHand(hovered);
        ImGui.PopID();

        if (clicked)
        {
            this.Clicked = entry;
        }

        dl.AddRectFilled(
            new Vector2(x, y),
            new Vector2(x + width, y + height),
            hovered ? Tokens.Col.RowHover : Tokens.Col.RowRest,
            Tokens.Radius.Control);

        Ink.DrawWrapped(
            Ink.Role.Body,
            new Vector2(textX, MathF.Round(y + padY)),
            textWidth,
            hovered ? Tokens.Col.Ink : Tokens.Col.InkSoft,
            entry.Text);

        if (entry.Jumps)
        {
            // A chevron and nothing else. Where it leads is on screen a click later, and
            // naming the path here would put a breadcrumb on every second line.
            float size = Tokens.Metric.NewsChevron;
            float cx = x + width - padX - size;
            float cy = MathF.Round(y + (height * 0.5f));
            uint ink = hovered ? Tokens.Col.GoldHi : Tokens.Col.InkFaint;

            dl.AddTriangleFilled(
                new Vector2(cx, cy - (size * 0.45f)),
                new Vector2(cx + (size * 0.55f), cy),
                new Vector2(cx, cy + (size * 0.45f)),
                ink);
        }

        return y + height + Tokens.Metric.NewsRowGap;
    }

    private static string KindLabel(NewsKind kind) => kind switch
    {
        NewsKind.New => Strings.NewsNew,
        NewsKind.Changed => Strings.NewsChanged,
        _ => Strings.NewsFixed,
    };

    /// <summary>
    /// Only one of the three is the accent. New is what somebody opened the screen for;
    /// changed and fixed are the quiet half of a release and are coloured like it.
    /// </summary>
    private static uint KindColour(NewsKind kind) => kind switch
    {
        NewsKind.New => Tokens.Col.Gold,
        _ => Tokens.Col.InkFaint,
    };
}
