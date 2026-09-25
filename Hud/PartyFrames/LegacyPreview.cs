using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Data;
using WispUI.Style;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// The preview for Legacy mode: a stand-in of the game's own party list, rows under each
/// other, with the marks WispUI lays on it.
/// <para>
/// ⚠️ A stand-in, like the cast bar's was: in Legacy the list is the game's, and a game window
/// cannot be drawn into a settings window. What it shows is what Legacy is about — which row
/// is marked, in which colour, how strongly — with the job icons the game itself uses. The
/// exact look of a mark on the real list is the game's art (spec: Legacy step 2).
/// </para>
/// </summary>
internal static class LegacyPreview
{
    public static Vector2 Size(int count) =>
        new(Tokens.Metric.LegacyRowWidth, (Tokens.Metric.LegacyRowHeight * count) + (Tokens.Metric.LegacyRowGap * (count - 1)));

    /// <summary>Draws the rows. Allocates nothing: every text is a stand-in string that already exists.</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 origin, PartySnapshot preview, Configuration.PartyFramesConfig cfg)
    {
        PartyMemberSnapshot[] members = preview.Members;
        float width = Tokens.Metric.LegacyRowWidth;
        float height = Tokens.Metric.LegacyRowHeight;
        float icon = Tokens.Metric.LegacyIcon;
        float pad = Tokens.Metric.LegacyRowPad;
        float bar = Tokens.Metric.LegacyBarHeight;

        for (int i = 0; i < preview.Count; i++)
        {
            ref PartyMemberSnapshot member = ref members[i];
            Vector2 min = new(origin.X, MathF.Round(origin.Y + (i * (height + Tokens.Metric.LegacyRowGap))));
            Vector2 max = new(min.X + width, min.Y + height);

            // The job icon, the game's own picture in the game's own framed set.
            float iconY = MathF.Round(min.Y + ((height - icon) * 0.5f));
            Vector2 iconMin = new(min.X + pad, iconY);
            ImTextureID texture = Icons.Handle(Jobs.IconId(member.JobId, true));

            if (!texture.IsNull)
            {
                dl.AddImage(texture, iconMin, iconMin + new Vector2(icon, icon));
            }

            // Name over a thin bar, the way the game lays a row out.
            float textX = iconMin.X + icon + pad;
            float barY = MathF.Round(max.Y - pad - bar);
            Ink.Draw(dl, Ink.Role.Small, new Vector2(textX, MathF.Round(barY - Ink.LineHeight(Ink.Role.Small) - Tokens.Metric.LegacyNameGap)), Tokens.Col.HudInk, member.Name);

            Vector2 barMin = new(textX, barY);
            Vector2 barMax = new(max.X - pad, barY + bar);
            dl.AddRectFilled(barMin, barMax, Tokens.Col.LegacyTrack);
            float fraction = member.MaxHp == 0u ? 0f : (float)member.Hp / member.MaxHp;
            dl.AddRectFilled(barMin, new Vector2(MathF.Round(barMin.X + ((barMax.X - barMin.X) * fraction)), barMax.Y), Tokens.Col.LegacyHealth);

            // The mark — variant D (Florian, 2026-09-25): an outline round the whole row and a
            // light wash inside it. Raise over cleanse, because "somebody is already on it"
            // is the reason to stop looking at the row.
            bool raising = member.RaiseRemaining > 0f || member.RaiseIsLanded;

            if (raising && cfg.ShowRaiseMark)
            {
                DrawMark(dl, min, max, cfg.RaiseColour, cfg.RaiseOpacity);
            }
            else if (member.HasDispellable && cfg.ShowCleanseMark)
            {
                DrawMark(dl, min, max, cfg.CleanseColour, cfg.CleanseOpacity);
            }
        }
    }

    private static void DrawMark(ImDrawListPtr dl, Vector2 min, Vector2 max, uint colour, float strength)
    {
        float radius = Tokens.Metric.LegacyMarkRadius;
        dl.AddRectFilled(min, max, Tokens.Col.Faded(colour, Tokens.Metric.LegacyWash * strength), radius);

        float half = Tokens.Metric.LegacyMarkEdge * 0.5f;
        Vector2 inset = new(half, half);
        dl.AddRect(min + inset, max - inset, colour, radius, ImDrawFlags.RoundCornersAll, Tokens.Metric.LegacyMarkEdge);
    }
}
