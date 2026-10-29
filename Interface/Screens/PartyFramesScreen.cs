using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Data;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The party list's settings: the marks WispUI lays on the game's own list, and the spells
/// that go to whoever the pointer is on.
/// <para>
/// 🔴 The game's list only (Florian, 2026-10-29). WispUI's own frames are gone — it plays
/// better on the game's list and stays closer to the game — and with them every tab that
/// sized, coloured or placed a frame of ours. The configuration still carries those fields
/// for the migration chain; nothing reads them.
/// </para>
/// </summary>
internal sealed class PartyFramesScreen
{
    private const string IdCleanseGroup = "##wisp-pf-cleansegroup";
    private const string IdCleanseWhenAble = "##wisp-pf-cleanseable";
    private const string IdCleanseColour = "##wisp-pf-cleansecolour";
    private const string IdCleanseOpacity = "##wisp-pf-cleanseopacity";
    private const string IdRaiseMarkGroup = "##wisp-pf-raisemarkgroup";
    private const string IdRaiseColour = "##wisp-pf-raisecolour";
    private const string IdRaiseOpacity = "##wisp-pf-raiseopacity";
    private const string IdJobGroup = "##wisp-pf-jobgroup";
    private const string IdMouseoverGroup = "##wisp-pf-mogroup";
    private const string IdMouseoverRow = "##wisp-pf-morow";
    private const string IdMouseoverOn = "##wisp-pf-moon";
    private const string IdMouseoverAdd = "##wisp-pf-moadd";
    private const string IdMouseoverRemove = "##wisp-pf-moremove";
    private const string IdBindingJob = "##wisp-pf-bindjob";
    private const string IdSpellAction = "##wisp-pf-spellaction";

    /// <summary>Opacity steps by a percent, which is what the readout beside it says.</summary>
    private const float OpacityStep = 0.01f;

    /// <summary>What the number at the end of an opacity row means when typed into: the percentage.</summary>
    private const float OpacityEditScale = 100f;

    private readonly Configuration m_config;
    private readonly ArrowSelector<JobEntry> m_jobSelector;
    private readonly ArrowSelector<ActionEntry> m_spellPicker;

    /// <summary>The spell the row being drawn holds, so the picker can keep offering it.</summary>
    private uint m_spellHeld;

    /// <summary>The row whose list should open on its first drawing, or -1 for none.</summary>
    private int m_spellOpenRow = -1;

    /// <summary>The mouseover rows being drawn, which is what "already taken" is measured against.</summary>
    private List<MouseoverSpell>? m_spellRows;

    /// <summary>Which job the bindings tab is showing.</summary>
    private int m_bindingJob = -1;

    /// <summary>The actions the picker offers, refilled when the job changes.</summary>
    private readonly List<ActionEntry> m_actionChoices = new();
    private uint m_choicesFor = uint.MaxValue;

    private string m_cleanseOpacityText = string.Empty;
    private int m_cleanseOpacityTextFor = -1;

    private string m_raiseOpacityText = string.Empty;
    private int m_raiseOpacityTextFor = -1;

    public PartyFramesScreen(Configuration config)
    {
        m_config = config;

        // Twenty-one jobs is well past what two arrows are for, so this one carries the popup
        // and its search — the same reasoning as the font list.
        m_jobSelector = new ArrowSelector<JobEntry>(
            IdBindingJob,
            JobList.All,
            new ArrowSelectorOptions<JobEntry>
            {
                Label = static job => job.Name,
                EnablePopupList = true,
                EnableSearch = true,
                ShowCounter = false,
            });

        // The spell list hides what it already holds: a second row casting the same spell is
        // nothing but a mistake.
        m_spellPicker = ActionPicker(IdSpellAction, m_actionChoices, Strings.MouseoverPick, this.SpellOnOffer);
    }

    /// <summary>
    /// The Base tab: the two marks WispUI lays on the game's own list, and nothing else —
    /// the list itself is the game's to set up (Florian, 2026-09-25).
    /// </summary>
    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);
        float y = origin.Y;

        Chrome.BeginGroupRow();
        Chrome.GroupScope cleanse = this.DrawCleanse(Chrome.ColumnX(origin.X, width, 0), y, column, out float cleanseHeight);
        Chrome.GroupScope raiseMark = this.DrawRaiseMark(Chrome.ColumnX(origin.X, width, 1), y, column, out float raiseMarkHeight);
        y += Chrome.GroupFrameRow(cleanse, cleanseHeight, raiseMark, raiseMarkHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>
    /// The Bindings tab: which job is being set up, then the two lists that say where a
    /// button and a key press land.
    /// <para>
    /// The job is its own panel at the top (Florian, 2026-09-19). It used to be the first row
    /// of the bindings list, which was honest while it governed one list and became a lie the
    /// moment it governed two — a setting that belongs to everything below it cannot live
    /// inside one of the things below it.
    /// </para>
    /// <para>
    /// 🔴 There is no group called "Mouse" any more. It held three switches that had nothing
    /// to do with each other beyond involving a pointer, which is the subject-shaped cut §3.1
    /// warns about. Two of them moved to the list each one serves — the hover ring to the
    /// buttons, the mouseover target to the spells — and the third became the list itself.
    /// A switch is better off beside what it affects than in a panel named after a device.
    /// </para>
    /// </summary>
    public void DrawBindings(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float y = origin.Y;

        // Resolved once for the whole tab: both lists below are the same job's.
        if (m_bindingJob < 0)
        {
            m_bindingJob = Math.Max(0, JobList.IndexOf(Services.Objects.LocalPlayer?.ClassJob.RowId ?? 0u));
        }

        Chrome.BeginGroupRow();
        Chrome.GroupScope job = this.DrawJobPanel(origin.X, y, width, out float jobHeight);
        y += Chrome.GroupFrame(job, jobHeight) + Tokens.Metric.ColumnGutter;

        JobEntry entry = JobList.At(m_bindingJob);
        this.SyncActionChoices(entry.Id);

        // 🔴 The full width, and rows that carry more than one control. The grammar everywhere
        // else — one setting, one control, half the width — is what keeps a settings screen
        // readable, and a mouseover row is not a setting: it is a spell, its switch and a way
        // to remove it (Florian, 2026-09-12, pointing at LumenUI's own bindings screen).
        //
        // There were mouse bindings above it until 2026-09-25 — a mouse button that used an
        // action on a frame. Gone (Florian): a click selects and a right click opens the menu,
        // fixed, and nothing else needs the mouse.
        Chrome.BeginGroupRow();
        Chrome.GroupScope over = this.DrawMouseoverList(origin.X, y, width, entry, out float overHeight);
        y += Chrome.GroupFrame(over, overHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>
    /// Which job the rest of the tab is about. One row, across the full width, because what
    /// it governs is the full width.
    /// </summary>
    private Chrome.GroupScope DrawJobPanel(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdJobGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupJob,
                Description = Strings.GroupJobHint,
            },
            x,
            y,
            width);

        int job = m_bindingJob;

        if (m_jobSelector.Draw(
                ref job,
                Chrome.Row(Strings.BindingJob, group.ContentX, group.ContentY, group.ContentWidth, false, Strings.BindingJobTooltip),
                group.ContentY,
                Chrome.ControlWidth()))
        {
            m_bindingJob = job;
        }

        float used = Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// Which spells go to whoever the pointer is on, for this job.
    /// <para>
    /// Built as a list rather than as a switch (Florian, 2026-09-19). One switch for every
    /// action a job owns asked the wrong question: "can this be used on them" is true of
    /// every heal, shield and raise a healer has, and wanting a regen on the pointer is not
    /// wanting a raise there. The list asks it once per spell, which is how it comes up.
    /// </para>
    /// <para>
    /// There is no switch over the list either, because the list already is one. Nothing
    /// named, or everything switched off, and the hook is not installed at all — so the one
    /// feature in the suite that changes what a key press does costs exactly nothing until
    /// somebody asks for it.
    /// </para>
    /// </summary>
    private Chrome.GroupScope DrawMouseoverList(float x, float y, float width, JobEntry job, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdMouseoverGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupMouseover,
                Description = Strings.GroupMouseoverHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // Read, not edited. An entry is made when a spell is added and not a moment earlier:
        // for this list an empty entry and no entry mean the same thing, so opening the tab
        // on twenty jobs would otherwise write twenty empty lists nobody asked for.
        List<MouseoverSpell> spells = m_config.PartyFrames.Mouseover.For(job.Id);
        rowY = this.DrawMouseoverRows(group, spells, job, rowY, pitch);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// One row per spell: what it is, whether it answers, and a way to take it out.
    /// <para>
    /// The same shape as a binding row minus the key, because there is no key to name — the
    /// key is whatever you already have the spell on. That absence is the whole difference
    /// between the two lists and is better shown by a missing column than explained.
    /// </para>
    /// </summary>
    private float DrawMouseoverRows(
        Chrome.GroupScope group,
        List<MouseoverSpell> spells,
        JobEntry job,
        float rowY,
        float pitch)
    {
        ActionEntry[] actions = ActionList.For(job.Id);

        // What the picker measures "already taken" against, for as long as this list is the
        // one being drawn.
        m_spellRows = spells;

        float toggleWidth = Tokens.Px(30f);
        float trash = Tokens.Metric.TitleButton;
        float gap = Tokens.Space.Md;

        float trashX = group.ContentX + group.ContentWidth - trash;
        float toggleX = trashX - gap - toggleWidth;
        float nameWidth = toggleX - gap - group.ContentX;

        int remove = -1;

        // 🔴 Its own id scope, and not only for tidiness: every row of every list is the same
        // few controls, and the id is the only thing telling one row's popup from another's.
        ImGui.PushID(IdMouseoverRow);

        for (int i = 0; i < spells.Count; i++)
        {
            MouseoverSpell spell = spells[i];

            ImGui.PushID(i);
            Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

            // What this row holds, for the length of its own drawing. The picker asks it
            // while the list is open, so that a row keeps offering its own spell while the
            // others are hidden — otherwise a row could not show what it already is.
            m_spellHeld = spell.ActionId;

            // The row a press of "Add spell" made, which did not exist yet when the press
            // happened. Opening it here saves the click that only says "yes, the row I just
            // asked for" (Florian, 2026-09-19).
            if (i == m_spellOpenRow)
            {
                m_spellOpenRow = -1;
                m_spellPicker.RequestOpen();
            }

            int pick = this.ActionIndex(spell.ActionId);

            if (m_spellPicker.Draw(ref pick, group.ContentX, rowY, nameWidth)
                && pick >= 0 && pick < m_actionChoices.Count)
            {
                spell.ActionId = m_actionChoices[pick].Id;
                m_config.MarkDirty();
            }

            if (Chrome.BindingToggle(IdMouseoverOn, toggleX, rowY, spell.Enabled))
            {
                spell.Enabled = !spell.Enabled;
                m_config.MarkDirty();
            }

            float trashY = MathF.Round(rowY + ((Chrome.RowHeight() - trash) * 0.5f));

            if (Chrome.CloseButton(IdMouseoverRemove, trashX, trashY))
            {
                remove = i;
            }

            ImGui.PopID();
            rowY += pitch;
        }

        ImGui.PopID();

        // Whatever is left has gone stale — a row taken away before it was ever drawn. Only
        // ever cleared here, which is before the button below can ask for a new one.
        m_spellOpenRow = -1;

        // After the loop, never inside it: taking a row out while walking the list is how a
        // row gets skipped and an index ends up pointing at the wrong spell.
        if (remove >= 0)
        {
            spells.RemoveAt(remove);
            m_config.MarkDirty();
        }

        return this.DrawAddSpell(group, job, actions, rowY);
    }

    /// <summary>
    /// The button that adds a spell, or the line that says this job has nothing to add.
    /// <para>
    /// The one place the job's entry is created, which is why it takes the job rather than
    /// the list it was drawn from: a job nobody has added a spell to is handed a shared empty
    /// list for reading, and that list must never be written to.
    /// </para>
    /// </summary>
    private float DrawAddSpell(
        Chrome.GroupScope group,
        JobEntry job,
        ActionEntry[] actions,
        float rowY)
    {
        if (actions.Length == 0)
        {
            Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(group.ContentX, rowY + Tokens.Space.Sm),
                Tokens.Col.InkFaint,
                Strings.BindingNoActions);

            return rowY + Chrome.RowPitch();
        }

        Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

        if (Chrome.PillButton(IdMouseoverAdd, Strings.MouseoverAdd, group.ContentX, rowY))
        {
            // Empty, and the row says so. A new row started on the job's first action, which
            // read as a choice somebody had made and had to be undone before it could be made
            // (Florian, 2026-09-19).
            List<MouseoverSpell> rows = m_config.PartyFrames.Mouseover.Edit(job.Id);

            // Noted before the row is added, so it is the index the new row will have.
            m_spellOpenRow = rows.Count;
            rows.Add(new MouseoverSpell());
            m_config.MarkDirty();
        }

        return rowY + Chrome.RowPitch();
    }

    /// <summary>
    /// The picker a row of a list gets: a job's actions, no arrows, no box, the action's own
    /// icon beside its name, and a placeholder for the row nobody has filled in yet.
    /// <para>
    /// The list it reads is the shared one, refilled in place when the job changes, so both
    /// pickers always offer the job on screen.
    /// </para>
    /// </summary>
    private static ArrowSelector<ActionEntry> ActionPicker(
        string id,
        IReadOnlyList<ActionEntry> choices,
        string placeholder,
        Func<ActionEntry, bool>? available) =>
        new(
            id,
            choices,
            new ArrowSelectorOptions<ActionEntry>
            {
                Label = static action => action.Name,
                EnablePopupList = true,
                EnableSearch = true,
                ShowCounter = false,
                Placeholder = placeholder,
                Available = available,

                // What the game says the action does, on hover — in the open list and on the
                // row once it is chosen. Read at the moment it is wanted and kept.
                Describe = static action => ActionList.Describe(action.Id),

                // No arrows and no box. Stepping through a job's whole action list one at a
                // time is not a way anybody would use it, and a field drawn round the name
                // would make the row read as two settings rather than one binding (Florian,
                // 2026-09-12).
                HideArrows = true,
                Flat = true,

                // The action's own icon, which is how a spell is recognised before its name is
                // read. Icons.Handle caches the lookup and hands back a null handle for
                // anything not loaded, which the selector simply does not draw.
                // Square, because an action icon is. The default preview strip is wide and
                // short for bar fills, and an icon stretched into it comes out smeared.
                PreviewSize = Tokens.Px(22f, 22f),

                DrawPreview = static (dl, action, min, max) =>
                {
                    ImTextureID icon = Icons.Handle(action.Icon);

                    if (!icon.IsNull)
                    {
                        dl.AddImage(icon, min, max);
                    }
                },
            });

    /// <summary>
    /// Whether an action is still worth offering in the mouseover list: one nobody has taken,
    /// or the one the row being drawn already holds.
    /// <para>
    /// The second half is not a nicety. Without it a row would hide its own spell from its
    /// own list, so opening it would show a list that does not contain what the row says.
    /// </para>
    /// </summary>
    private bool SpellOnOffer(ActionEntry action)
    {
        if (action.Id == m_spellHeld)
        {
            return true;
        }

        List<MouseoverSpell>? rows = m_spellRows;

        if (rows is null)
        {
            return true;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].ActionId == action.Id)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Where an action sits in the picker's list, or -1 for a row that has not been filled
    /// in — a fresh one, or one whose action this job does not have.
    /// <para>
    /// It used to answer zero for both, which put the job's first action in front of somebody
    /// who had picked nothing (Florian, 2026-09-19). The second case is the same mistake
    /// quietly: a row that has lost its action is better off saying so than showing whatever
    /// happens to be at the top of the list.
    /// </para>
    /// </summary>
    private int ActionIndex(uint actionId)
    {
        if (actionId == 0u)
        {
            return -1;
        }

        for (int i = 0; i < m_actionChoices.Count; i++)
        {
            if (m_actionChoices[i].Id == actionId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Refills the action list the picker holds, when the job it is showing has changed.
    /// <para>
    /// The same list object is kept and its contents replaced, because a selector holds the
    /// reference it was built with. Refilled only on a change, so this costs a comparison on
    /// every frame but the one where the job was switched.
    /// </para>
    /// </summary>
    private void SyncActionChoices(uint jobId)
    {
        if (m_choicesFor == jobId)
        {
            return;
        }

        m_choicesFor = jobId;
        m_actionChoices.Clear();
        m_actionChoices.AddRange(ActionList.For(jobId));
    }

    /// <summary>
    /// The mark on a row of the game's list whose member carries an effect you could take
    /// off. One shape on the game's list (variant D, Florian 2026-09-25): colour and strength.
    /// </summary>
    private Chrome.GroupScope DrawCleanse(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdCleanseGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupCleanse,
                Description = Strings.GroupCleanseHint,
                Toggle = m_config.PartyFrames.ShowCleanseMark,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowCleanseMark = !m_config.PartyFrames.ShowCleanseMark;
            m_config.MarkDirty();
        }

        float rowY = group.ContentY;

        uint colour = m_config.PartyFrames.CleanseColour;
        if (Chrome.ColourRow(
                IdCleanseColour,
                Strings.CleanseColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref colour,
                false,
                null))
        {
            m_config.PartyFrames.CleanseColour = colour;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        float cleanseOpacity = m_config.PartyFrames.CleanseOpacity;
        Chrome.SliderResult cleanseFill = Chrome.Slider(
            IdCleanseOpacity,
            Strings.MarkOpacity,
            this.CleanseOpacityCaption(cleanseOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cleanseOpacity,
            0f,
            1f,
            null,
            Strings.MarkOpacityTooltip,
            true,
            OpacityStep,
            OpacityEditScale);

        if (cleanseFill.Changed)
        {
            m_config.PartyFrames.CleanseOpacity = cleanseFill.Value;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        if (Chrome.OptionRow(
                IdCleanseWhenAble,
                Strings.CleanseWhenAble,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.CleanseOnlyWhenAble,
                Chrome.OptionControl.Tick,
                Strings.CleanseWhenAbleTooltip,
                true,
                true))
        {
            m_config.PartyFrames.CleanseOnlyWhenAble = !m_config.PartyFrames.CleanseOnlyWhenAble;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The mark that says somebody is already being picked up — the same shape as the cleanse
    /// mark, saying the opposite thing: "you can stop looking at this row".
    /// </summary>
    private Chrome.GroupScope DrawRaiseMark(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdRaiseMarkGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupRaiseMark,
                Description = Strings.GroupRaiseMarkHint,
                Toggle = m_config.PartyFrames.ShowRaiseMark,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowRaiseMark = !m_config.PartyFrames.ShowRaiseMark;
            m_config.MarkDirty();
        }

        float rowY = group.ContentY;

        uint colour = m_config.PartyFrames.RaiseColour;
        if (Chrome.ColourRow(
                IdRaiseColour,
                Strings.RaiseColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref colour,
                false,
                null))
        {
            m_config.PartyFrames.RaiseColour = colour;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        float raiseOpacity = m_config.PartyFrames.RaiseOpacity;
        Chrome.SliderResult raiseFill = Chrome.Slider(
            IdRaiseOpacity,
            Strings.MarkOpacity,
            this.RaiseOpacityCaption(raiseOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            raiseOpacity,
            0f,
            1f,
            null,
            Strings.MarkOpacityTooltip,
            true,
            OpacityStep,
            OpacityEditScale);

        if (raiseFill.Changed)
        {
            m_config.PartyFrames.RaiseOpacity = raiseFill.Value;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    private static string PercentCaption(float value, ref int cachedFor, ref string cached)
    {
        int percent = (int)MathF.Round(value * 100f);

        if (percent != cachedFor)
        {
            cachedFor = percent;
            cached = percent.ToString(CultureInfo.InvariantCulture) + " %";
        }

        return cached;
    }

    private string CleanseOpacityCaption(float opacity) =>
        PercentCaption(opacity, ref m_cleanseOpacityTextFor, ref m_cleanseOpacityText);

    private string RaiseOpacityCaption(float opacity) =>
        PercentCaption(opacity, ref m_raiseOpacityTextFor, ref m_raiseOpacityText);
}
