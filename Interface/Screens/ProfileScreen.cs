using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Data;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The profiles: which sets of settings exist, who each one is for, and how one travels to
/// somebody else.
/// <para>
/// 🔴 The list takes the full width and its rows carry more than one control, which is the
/// grammar the bindings tab established and the second place it earns its exception. A
/// profile is not a setting — it is a row of a list, with a name, who it is for and what can
/// be done to it, and half of that row would say nothing (§3.1).
/// </para>
/// <para>
/// The two groups under it are ordinary: they are about the <em>selected</em> profile, one
/// answering who it is for and one answering how to send it. Three groups on one screen, no
/// scrolling.
/// </para>
/// </summary>
internal sealed class ProfileScreen
{
    private const string IdListGroup = "##wisp-profile-list";
    private const string IdScopeGroup = "##wisp-profile-scope";
    private const string IdShareGroup = "##wisp-profile-share";
    private const string IdActive = "##wisp-profile-active";
    private const string IdName = "##wisp-profile-name";
    private const string IdRename = "##wisp-profile-rename";
    private const string IdRemove = "##wisp-profile-remove";
    private const string IdAdd = "##wisp-profile-add";
    private const string IdAutomatic = "##wisp-profile-automatic";
    private const string IdKind = "##wisp-profile-kind";
    private const string IdRole = "##wisp-profile-role";
    private const string IdJob = "##wisp-profile-job";
    private const string IdJobAdd = "##wisp-profile-job-add";
    private const string IdJobRemove = "##wisp-profile-job-remove";
    private const string IdCopy = "##wisp-profile-copy";
    private const string IdPaste = "##wisp-profile-paste";
    private const string IdPastePanel = "##wisp-profile-paste-panel";
    private const string IdPastePartFrames = "##wisp-profile-part-frames";
    private const string IdPastePartScope = "##wisp-profile-part-scope";
    private const string IdPasteTargetNew = "##wisp-profile-into-new";
    private const string IdPasteTargetHere = "##wisp-profile-into-here";
    private const string IdPasteApply = "##wisp-profile-paste-apply";

    private readonly Configuration m_config;

    private readonly ArrowSelector<ProfileScopeKind> m_kind;
    private readonly ArrowSelector<JobRole> m_role;
    private readonly ArrowSelector<int> m_jobPicker;

    /// <summary>Which row is being renamed, or -1. State in the screen, never in the widget.</summary>
    private int m_renaming = -1;
    private string m_renameText = string.Empty;
    private bool m_renameFocus;

    /// <summary>What the last Copy said, shown until something else is done.</summary>
    private string m_note = string.Empty;

    /// <summary>The pasted profile, held while the panel asks what to take of it.</summary>
    private Profile? m_pasted;
    private ProfileParts m_pasteParts = ProfileParts.All;
    private bool m_pasteOverCurrent;
    private bool m_pasteOpen;

    private int m_jobChoice;

    /// <summary>Where the paste panel opens: under the button, worked out when it is drawn.</summary>
    private Vector2 m_pasteAnchor;

    private static readonly ProfileScopeKind[] ScopeKinds =
    {
        ProfileScopeKind.Role,
        ProfileScopeKind.Jobs,
    };

    private static readonly JobRole[] Roles = { JobRole.Tank, JobRole.Healer, JobRole.Dps };

    public ProfileScreen(Configuration config)
    {
        m_config = config;

        m_kind = new ArrowSelector<ProfileScopeKind>(
            IdKind,
            ScopeKinds,
            new ArrowSelectorOptions<ProfileScopeKind> { Label = KindLabel, ShowCounter = false });

        m_role = new ArrowSelector<JobRole>(
            IdRole,
            Roles,
            new ArrowSelectorOptions<JobRole> { Label = RoleLabel, ShowCounter = false });

        m_jobPicker = new ArrowSelector<int>(
            IdJob,
            JobIndices(),
            new ArrowSelectorOptions<int>
            {
                Label = static i => JobList.At(i).Name,
                EnablePopupList = true,
                ShowCounter = false,
            });
    }

    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float y = origin.Y;

        Chrome.BeginGroupRow();
        Chrome.GroupScope list = this.DrawList(origin.X, y, width, out float listHeight);
        y += Chrome.GroupFrame(list, listHeight) + Tokens.Metric.ColumnGutter;

        float column = Chrome.ColumnWidth(width);
        float rowTop = y;

        Chrome.BeginGroupRow();
        Chrome.GroupScope scope = this.DrawScope(Chrome.ColumnX(origin.X, width, 0), rowTop, column, out float scopeHeight);
        Chrome.GroupScope share = this.DrawShare(Chrome.ColumnX(origin.X, width, 1), rowTop, column, out float shareHeight);

        y = rowTop + Chrome.GroupFrameRow(scope, scopeHeight, share, shareHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    // --- the list -----------------------------------------------------------

    private Chrome.GroupScope DrawList(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdListGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupProfiles,
                Description = Strings.GroupProfilesHint,
            },
            x,
            y,
            width);

        List<Profile> items = m_config.Profiles.Items;
        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        float button = Chrome.MeasureButton(Strings.ProfileRemove);
        float renameWidth = Chrome.MeasureButton(Strings.ProfileRename);
        float gap = Tokens.Space.Md;

        float removeX = group.ContentX + group.ContentWidth - button;
        float renameX = removeX - gap - renameWidth;
        float scopeX = MathF.Round(group.ContentX + (group.ContentWidth * 0.42f));
        float nameWidth = scopeX - gap - group.ContentX - Tokens.Metric.ProfileDotColumn;

        int remove = -1;
        int activate = -1;

        for (int i = 0; i < items.Count; i++)
        {
            Profile profile = items[i];
            bool active = i == m_config.Profiles.Active;

            ImGui.PushID(i);
            Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

            if (Chrome.RadioDot(IdActive, group.ContentX, rowY, active, Strings.ProfileActiveTooltip) && !active)
            {
                activate = i;
            }

            float textX = group.ContentX + Tokens.Metric.ProfileDotColumn;

            if (m_renaming == i)
            {
                if (Chrome.NameField(IdName, textX, rowY, nameWidth, Profile.MaxNameLength, ref m_renameText, ref m_renameFocus))
                {
                    profile.Name = m_config.Profiles.FreeName(Trimmed(m_renameText, profile.Name));
                    m_renaming = -1;
                    m_config.MarkDirty();
                }
            }
            else
            {
                Ink.Draw(
                    ImGui.GetWindowDrawList(),
                    Ink.Role.Body,
                    new Vector2(textX, Chrome.CenterY(rowY, Chrome.RowHeight(), Ink.Role.Body)),
                    active ? Tokens.Col.GoldHi : Tokens.Col.Ink,
                    profile.Name);
            }

            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(scopeX, Chrome.CenterY(rowY, Chrome.RowHeight(), Ink.Role.Small)),
                Tokens.Col.InkFaint,
                this.ScopeSummary(profile, i));

            if (Chrome.Button(IdRename, Strings.ProfileRename, renameX, ButtonY(rowY), true))
            {
                m_renaming = i;
                m_renameText = profile.Name;
                m_renameFocus = true;
            }

            // 🔴 Only the fallback cannot go. It is where every unclaimed job lands, and a
            // list without one would need "no profile" to be a state somebody understands.
            bool removable = i > 0;

            if (Chrome.Button(IdRemove, Strings.ProfileRemove, removeX, ButtonY(rowY), removable, Strings.ProfileFallbackKeep))
            {
                remove = i;
            }

            ImGui.PopID();
            rowY += pitch;
        }

        // After the loop. Taking a row out while walking the list is how a row gets skipped.
        if (remove >= 0)
        {
            this.Remove(remove);
        }
        else if (activate >= 0)
        {
            m_config.UseProfile(activate);
            m_renaming = -1;
            m_note = string.Empty;
        }

        rowY = this.DrawAdd(group, rowY);

        float used = rowY - group.ContentY;
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    private float DrawAdd(in Chrome.GroupScope group, float rowY)
    {
        Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

        bool room = m_config.Profiles.Items.Count < ProfileSet.MaxProfiles;

        if (Chrome.Button(IdAdd, Strings.ProfileAdd, group.ContentX, ButtonY(rowY), room, Strings.ProfileFull))
        {
            this.Add();
        }

        return rowY + Chrome.RowPitch();
    }

    /// <summary>
    /// A new profile starts as a copy of what is on screen, not as the defaults.
    /// <para>
    /// Somebody adding a second profile has already built the first one. Starting from
    /// nothing would mean rebuilding it to change the one thing they wanted different, and
    /// "start from the defaults" is what the Defaults button in the header is for.
    /// </para>
    /// </summary>
    private void Add()
    {
        if (m_config.Profiles.Items.Count >= ProfileSet.MaxProfiles)
        {
            return;
        }

        // What is on screen belongs to the profile that is on, first: otherwise the copy
        // would be of whatever that profile held at its last switch.
        m_config.StoreIntoActiveProfile();

        Profile made = m_config.Profiles.Current.Clone();
        made.Name = m_config.Profiles.FreeName(Strings.ProfileNewName);
        made.ScopeKind = (int)ProfileScopeKind.Jobs;
        made.Jobs.Clear();

        m_config.Profiles.Items.Add(made);
        m_config.UseProfile(m_config.Profiles.Items.Count - 1);

        m_renaming = m_config.Profiles.Items.Count - 1;
        m_renameText = made.Name;
        m_renameFocus = true;
        m_config.MarkDirty();
    }

    private void Remove(int index)
    {
        if (index <= 0 || index >= m_config.Profiles.Items.Count)
        {
            return;
        }

        int active = m_config.Profiles.Active;
        m_config.Profiles.Items.RemoveAt(index);
        m_renaming = -1;

        if (active == index)
        {
            // The one that went was the one on screen, so the live settings are still its.
            // Applied rather than switched to: switching would store those settings into
            // the fallback on the way past, and deleting one profile would overwrite
            // another.
            m_config.Profiles.Active = 0;
            m_config.ApplyActiveProfile();
        }
        else if (active > index)
        {
            m_config.Profiles.Active = active - 1;
        }

        m_config.MarkDirty();
    }

    // --- who it is for ------------------------------------------------------

    private Chrome.GroupScope DrawScope(float x, float y, float width, out float contentHeight)
    {
        Profile profile = m_config.Profiles.Current;
        bool fallback = m_config.Profiles.Active == 0;

        Chrome.GroupScope group = Chrome.BeginGroup(
            IdScopeGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupProfileScope,
                Description = profile.Name,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // 🔴 The fallback answers the question by being first in the list, so there is
        // nothing to set. Saying so beats three greyed-out rows nobody can act on.
        if (fallback)
        {
            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(group.ContentX, rowY + Tokens.Space.Sm),
                Tokens.Col.InkFaint,
                Strings.ProfileFallbackKeep);

            float only = Chrome.RowHeight();
            Chrome.EndGroupContent(group, only);
            contentHeight = only;
            return group;
        }

        if (Chrome.OptionRow(
                IdAutomatic,
                Strings.ProfileAutomatic,
                group.ContentX,
                rowY,
                group.ContentWidth,
                profile.Automatic,
                Chrome.OptionControl.Tick,
                Strings.ProfileAutomaticTooltip))
        {
            profile.Automatic = !profile.Automatic;
            m_config.MarkDirty();
        }

        rowY += pitch;

        int kind = Math.Max(0, Array.IndexOf(ScopeKinds, (ProfileScopeKind)profile.ScopeKind));

        if (m_kind.Draw(
                ref kind,
                Chrome.Row(Strings.ProfileAppliesTo, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            profile.ScopeKind = (int)ScopeKinds[kind];
            m_config.MarkDirty();
        }

        rowY += pitch;

        if ((ProfileScopeKind)profile.ScopeKind == ProfileScopeKind.Role)
        {
            int role = Math.Max(0, Array.IndexOf(Roles, (JobRole)profile.Role));

            if (m_role.Draw(
                    ref role,
                    Chrome.Row(Strings.ProfileRole, group.ContentX, rowY, group.ContentWidth, true, Strings.ProfileScopeBeaten),
                    rowY,
                    Chrome.ControlWidth()))
            {
                profile.Role = (int)Roles[role];
                m_config.MarkDirty();
            }

            rowY += pitch;
        }
        else
        {
            rowY = this.DrawJobs(group, profile, rowY, pitch);
        }

        float used = rowY - group.ContentY;
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The jobs a profile names, one per row, and the picker that adds another. A list,
    /// like the bindings, for the same reason: it is a set somebody builds rather than a
    /// value they pick.
    /// </summary>
    private float DrawJobs(in Chrome.GroupScope group, Profile profile, float rowY, float pitch)
    {
        float trash = Tokens.Metric.TitleButton;
        float trashX = group.ContentX + group.ContentWidth - trash;
        int remove = -1;

        for (int i = 0; i < profile.Jobs.Count; i++)
        {
            int at = JobList.IndexOf(profile.Jobs[i]);

            if (at < 0)
            {
                continue;
            }

            ImGui.PushID(i);
            Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Body,
                new Vector2(group.ContentX, Chrome.CenterY(rowY, Chrome.RowHeight(), Ink.Role.Body)),
                Tokens.Col.Ink,
                JobList.At(at).Name);

            if (Chrome.CloseButton(IdJobRemove, trashX, MathF.Round(rowY + ((Chrome.RowHeight() - trash) * 0.5f))))
            {
                remove = i;
            }

            ImGui.PopID();
            rowY += pitch;
        }

        if (remove >= 0)
        {
            profile.Jobs.RemoveAt(remove);
            m_config.MarkDirty();
        }

        if (profile.Jobs.Count == 0)
        {
            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(group.ContentX, rowY + Tokens.Space.Sm),
                Tokens.Col.InkFaint,
                Strings.ProfileJobsNone);

            rowY += Chrome.RowPitch();
        }

        Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

        // The picker adds rather than sets: picking a job puts it on the list and the
        // picker goes back to offering, which is what "add a job" means.
        int choice = m_jobChoice;

        if (m_jobPicker.Draw(
                ref choice,
                Chrome.Row(Strings.ProfileJobAdd, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_jobChoice = choice;
            uint id = JobList.At(choice).Id;

            if (!profile.Jobs.Contains(id))
            {
                profile.Jobs.Add(id);
                m_config.MarkDirty();
            }
        }

        return rowY + pitch;
    }

    // --- sharing ------------------------------------------------------------

    private Chrome.GroupScope DrawShare(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdShareGroup,
            new Chrome.GroupHead { Title = Strings.GroupProfileShare },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        float copyWidth = Chrome.MeasureButton(Strings.ProfileCopyCode);
        float copyX = group.ContentX + group.ContentWidth - copyWidth;

        Chrome.Row(Strings.ProfileThisOne, group.ContentX, rowY, group.ContentWidth, false);

        if (Chrome.Button(IdCopy, Strings.ProfileCopyCode, copyX, ButtonY(rowY), true))
        {
            this.CopyCurrent();
        }

        rowY += pitch;

        float pasteWidth = Chrome.MeasureButton(Strings.ProfilePaste);
        float pasteX = group.ContentX + group.ContentWidth - pasteWidth;

        Chrome.Row(Strings.ProfileFromCode, group.ContentX, rowY, group.ContentWidth, true);

        if (Chrome.Button(IdPaste, Strings.ProfilePaste, pasteX, ButtonY(rowY), true))
        {
            this.ReadClipboard();
        }

        // Where the panel hangs, remembered now: it belongs under the button that opened
        // it, and by the time the popup is drawn the cursor has moved on.
        m_pasteAnchor = new Vector2(
            group.ContentX + group.ContentWidth - Tokens.Metric.PastePanelWidth,
            ButtonY(rowY) + Tokens.Metric.ButtonHeight + Tokens.Metric.PopupGap);

        rowY += pitch;

        // One line under the two buttons: what the last thing done said, or what pasting
        // will ask. It never shouts — a copy that worked is not news, it is a receipt.
        Ink.Draw(
            ImGui.GetWindowDrawList(),
            Ink.Role.Small,
            new Vector2(group.ContentX, rowY + Tokens.Space.Sm),
            m_note.Length > 0 ? Tokens.Col.Heading : Tokens.Col.InkFaint,
            m_note.Length > 0 ? m_note : Strings.ProfilePasteHint);

        rowY += Chrome.RowHeight();

        float used = rowY - group.ContentY;
        Chrome.EndGroupContent(group, used);

        if (m_pasteOpen)
        {
            ImGui.OpenPopup(IdPastePanel);
            m_pasteOpen = false;
        }

        this.DrawPastePanel(group);
        contentHeight = used;
        return group;
    }

    private void CopyCurrent()
    {
        // What is on screen belongs to the profile before it is written out, or the code
        // carries whatever that profile held at its last switch.
        m_config.StoreIntoActiveProfile();

        try
        {
            ImGui.SetClipboardText(ProfileCode.Write(m_config.Profiles.Current));
            m_note = Strings.ProfileCopied;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "The clipboard would not take the profile code.");
            m_note = Strings.ProfileCodeUnreadable;
        }
    }

    private void ReadClipboard()
    {
        string clip;

        try
        {
            clip = ImGui.GetClipboardText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "The clipboard could not be read.");
            m_note = Strings.ProfileCodeEmpty;
            return;
        }

        if (!ProfileCode.TryRead(clip, out Profile read, out string problem))
        {
            m_pasted = null;
            m_note = problem;
            return;
        }

        m_pasted = read;
        m_pasteParts = ProfileParts.All;
        m_pasteOverCurrent = false;
        m_pasteOpen = true;
        m_note = string.Empty;
    }

    /// <summary>
    /// What a pasted code brings, and where it goes. The same shape as the appearance paste
    /// panel — tick what to take, one button that does it — because it is the same question
    /// and a second answer to it would be a second thing to learn.
    /// <para>
    /// Where it goes is asked here rather than assumed: a new profile by default, because
    /// nothing of the player's is then at risk, and over the current one for somebody who
    /// meant to replace it (Florian, 2026-09-20).
    /// </para>
    /// </summary>
    private void DrawPastePanel(in Chrome.GroupScope group)
    {
        if (m_pasted is null)
        {
            return;
        }

        float width = Tokens.Metric.PastePanelWidth;
        float pad = Tokens.Metric.PastePanelPadding;
        float tick = Chrome.CheckBoxHeight();
        float line = Tokens.Line(1f);
        float small = Ink.LineHeight(Ink.Role.Small);

        float height = (pad * 2f)
            + small + Tokens.Space.Md
            + (tick * 2f) + Tokens.Metric.FieldGap
            + Tokens.Space.Lg + line + Tokens.Space.Lg
            + small + Tokens.Space.Sm
            + (tick * 2f) + Tokens.Metric.FieldGap
            + Tokens.Space.Lg + line + Tokens.Space.Lg
            + Tokens.Metric.ButtonHeight;

        ImGui.SetNextWindowPos(m_pasteAnchor);
        ImGui.SetNextWindowSize(new Vector2(width, height));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, Tokens.Radius.Control);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, line);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tokens.Col.PopupBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Tokens.Col.PopupEdge);

        if (ImGui.BeginPopup(IdPastePanel))
        {
            if (Chrome.ClosePopupRequested)
            {
                ImGui.CloseCurrentPopup();
            }

            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetWindowPos();
            float x = origin.X + pad;
            float cursorY = origin.Y + pad;
            float right = origin.X + width - pad;

            Ink.Draw(dl, Ink.Role.Small, new Vector2(x, cursorY), Tokens.Col.InkFaint, Strings.ProfilePasteTitle);
            Ink.Draw(
                dl,
                Ink.Role.Small,
                new Vector2(x, cursorY + small),
                Tokens.Col.Heading,
                m_pasted.Name);
            cursorY += small + Tokens.Space.Md;

            if (Chrome.CheckBox(
                    IdPastePartFrames,
                    Strings.ProfilePartPartyFrames,
                    x,
                    cursorY,
                    (m_pasteParts & ProfileParts.PartyFrames) != 0))
            {
                m_pasteParts ^= ProfileParts.PartyFrames;
            }

            cursorY += tick + Tokens.Metric.FieldGap;

            if (Chrome.CheckBox(
                    IdPastePartScope,
                    Strings.ProfilePartScope,
                    x,
                    cursorY,
                    (m_pasteParts & ProfileParts.Scope) != 0))
            {
                m_pasteParts ^= ProfileParts.Scope;
            }

            cursorY += tick + Tokens.Space.Lg;
            Chrome.Hairline(dl, x, right, cursorY, Tokens.Col.RowDivider);
            cursorY += Tokens.Space.Lg;

            Ink.Draw(dl, Ink.Role.Small, new Vector2(x, cursorY), Tokens.Col.InkFaint, Strings.ProfilePasteInto);
            cursorY += small + Tokens.Space.Sm;

            if (Chrome.CheckBox(IdPasteTargetNew, Strings.ProfilePasteAsNew, x, cursorY, !m_pasteOverCurrent))
            {
                m_pasteOverCurrent = false;
            }

            cursorY += tick + Tokens.Metric.FieldGap;

            // Over the fallback is allowed: it is a profile like any other once it is the
            // one on screen, and refusing would mean somebody with one profile could not
            // take a friend's at all.
            if (Chrome.CheckBox(IdPasteTargetHere, Strings.ProfilePasteOverCurrent, x, cursorY, m_pasteOverCurrent))
            {
                m_pasteOverCurrent = true;
            }

            cursorY += tick + Tokens.Space.Lg;
            Chrome.Hairline(dl, x, right, cursorY, Tokens.Col.RowDivider);
            cursorY += Tokens.Space.Lg;

            bool any = m_pasteParts != ProfileParts.None;
            float applyWidth = Chrome.MeasureButton(Strings.ProfilePasteApply);

            if (Chrome.Button(
                    IdPasteApply,
                    Strings.ProfilePasteApply,
                    right - applyWidth,
                    cursorY,
                    any,
                    Strings.ProfilePasteNothing,
                    true))
            {
                this.ApplyPaste();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        else
        {
            // Closed by clicking away. The code is dropped rather than kept waiting: a
            // panel that reopens with somebody else's profile still in it is a surprise.
            m_pasted = null;
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    private void ApplyPaste()
    {
        if (m_pasted is null || m_pasteParts == ProfileParts.None)
        {
            return;
        }

        Profile source = m_pasted;
        m_pasted = null;

        if (!m_pasteOverCurrent)
        {
            if (m_config.Profiles.Items.Count >= ProfileSet.MaxProfiles)
            {
                m_note = Strings.ProfileFull;
                return;
            }

            // A new profile starts as a copy of the one on screen, then takes what was
            // ticked. That way an unticked part is what the player already had rather than
            // a default they never chose.
            m_config.StoreIntoActiveProfile();
            Profile made = m_config.Profiles.Current.Clone();
            made.Name = m_config.Profiles.FreeName(source.Name);

            Take(source, made, m_pasteParts);

            m_config.Profiles.Items.Add(made);
            m_config.UseProfile(m_config.Profiles.Items.Count - 1);
        }
        else
        {
            Profile into = m_config.Profiles.Current;
            Take(source, into, m_pasteParts);

            // Straight onto the screen, because this profile IS the screen.
            if ((m_pasteParts & ProfileParts.PartyFrames) != 0)
            {
                m_config.PartyFramesEnabled = into.PartyFramesEnabled;
                Profile.Copy(into.PartyFrames, m_config.PartyFrames);
            }
        }

        m_note = string.Empty;
        m_config.MarkDirty();
    }

    /// <summary>
    /// Moves the ticked parts across. The fallback never takes a scope, whatever was
    /// ticked: it is the profile that catches what nothing claimed, and that is decided by
    /// its place in the list rather than by anything it holds.
    /// </summary>
    private static void Take(Profile from, Profile into, ProfileParts parts)
    {
        if ((parts & ProfileParts.PartyFrames) != 0)
        {
            into.PartyFramesEnabled = from.PartyFramesEnabled;
            Profile.Copy(from.PartyFrames, into.PartyFrames);
        }

        if ((parts & ProfileParts.Scope) != 0 && into.ScopeKind != (int)ProfileScopeKind.Fallback)
        {
            into.ScopeKind = from.ScopeKind;
            into.Role = from.Role;
            into.Jobs.Clear();
            into.Jobs.AddRange(from.Jobs);
            into.Automatic = from.Automatic;
        }
    }

    // --- small things -------------------------------------------------------

    private static float ButtonY(float rowY) =>
        MathF.Round(rowY + ((Chrome.RowHeight() - Tokens.Metric.ButtonHeight) * 0.5f));

    private static string Trimmed(string typed, string fallback)
    {
        typed = typed.Trim();
        return typed.Length > 0 ? typed : fallback;
    }

    /// <summary>
    /// What a row says about who its profile is for, in as few words as it takes. Built
    /// when it changes, not per frame: the strings are cached against what they describe.
    /// </summary>
    private string ScopeSummary(Profile profile, int index)
    {
        if (index == 0)
        {
            return Strings.ProfileFallbackScope;
        }

        if (!profile.Automatic)
        {
            return Strings.ProfileManualOnly;
        }

        if ((ProfileScopeKind)profile.ScopeKind == ProfileScopeKind.Role)
        {
            return RoleLabel((JobRole)profile.Role);
        }

        return m_jobNames.For(profile);
    }

    private readonly JobNameCache m_jobNames = new();

    /// <summary>
    /// The job names of a profile as one string, rebuilt only when that profile's list
    /// changes. Joining names allocates, and this is read once per row per frame.
    /// </summary>
    private sealed class JobNameCache
    {
        private readonly Dictionary<Profile, (int Count, uint First, string Text)> m_by = new();

        public string For(Profile profile)
        {
            List<uint> jobs = profile.Jobs;
            uint first = jobs.Count > 0 ? jobs[0] : 0u;

            // Count and first id together: adding, removing and swapping a job all move one
            // of the two, and comparing the whole list would cost what caching saves.
            if (m_by.TryGetValue(profile, out (int Count, uint First, string Text) known)
                && known.Count == jobs.Count
                && known.First == first)
            {
                return known.Text;
            }

            // Profiles that were removed stay in here holding a string nothing will ask
            // for again. Bounded rather than tracked: adding and removing profiles all
            // afternoon is the only way to grow it, and starting over costs one rebuild
            // per row on the next frame.
            if (m_by.Count > ProfileSet.MaxProfiles * 2)
            {
                m_by.Clear();
            }

            string text = Build(jobs);
            m_by[profile] = (jobs.Count, first, text);
            return text;
        }

        private static string Build(List<uint> jobs)
        {
            if (jobs.Count == 0)
            {
                return Strings.ProfileJobsNone;
            }

            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < jobs.Count; i++)
            {
                int at = JobList.IndexOf(jobs[i]);

                if (at < 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(JobList.At(at).Name);
            }

            return sb.ToString();
        }
    }

    private static string KindLabel(ProfileScopeKind kind) => kind switch
    {
        ProfileScopeKind.Role => Strings.ProfileScopeRole,
        _ => Strings.ProfileScopeJobs,
    };

    private static string RoleLabel(JobRole role) => role switch
    {
        JobRole.Tank => Strings.RoleTank,
        JobRole.Healer => Strings.RoleHealer,
        _ => Strings.RoleDps,
    };

    private static int[] JobIndices()
    {
        var all = new int[JobList.Count];

        for (int i = 0; i < all.Length; i++)
        {
            all[i] = i;
        }

        return all;
    }
}
