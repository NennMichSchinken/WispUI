namespace WispUI.Appearance;

/// <summary>
/// One buffer for the whole suite, like the format painter in a design tool: copy the look
/// off one element, tick what to carry, paste it onto another.
/// <para>
/// It lives in memory only — a copy does not survive a reload, and does not need to. What
/// it holds is an <see cref="AppearanceBlock"/>, which is flat and serialisable, so a
/// shareable code or a saved preset can be added later without touching this class.
/// </para>
/// </summary>
internal sealed class AppearanceClipboard
{
    private AppearanceBlock? m_buffer;
    private string m_sourceName = string.Empty;
    private AppearanceFields m_sourceFields;

    // Exactly one step of undo, held for the element that was pasted onto. Not an undo
    // system — just the way back out of the one action that overwrites without asking.
    private IAppearanceOwner? m_undoTarget;
    private AppearanceBlock? m_undoBefore;
    private AppearanceFields m_undoMask;

    public bool HasContent => m_buffer is not null;

    /// <summary>The element the buffer was taken from, for the paste panel's "from:" line.</summary>
    public string SourceName => m_sourceName;

    /// <summary>What the source element had. A paste can never carry more than this.</summary>
    public AppearanceFields SourceFields => m_sourceFields;

    public void Copy(IAppearanceOwner owner)
    {
        m_buffer = owner.GetAppearance();
        m_sourceName = owner.DisplayName;
        m_sourceFields = owner.SupportedFields;
    }

    /// <summary>
    /// What a paste onto <paramref name="target"/> would actually change: the ticks, cut
    /// down to what the source had and what the target has. The panel asks first so it can
    /// show which parts will not come across, instead of leaving the user with a paste that
    /// silently did nothing.
    /// </summary>
    public AppearanceFields EffectiveMask(IAppearanceOwner target, AppearanceFields wanted) =>
        wanted & m_sourceFields & target.SupportedFields;

    /// <summary>
    /// Applies the buffer. Returns what was actually carried over, which is
    /// <see cref="AppearanceFields.None"/> if the ticks and the two elements had nothing in
    /// common — in that case nothing is written and no undo is armed.
    /// </summary>
    public AppearanceFields Paste(IAppearanceOwner target, AppearanceFields wanted)
    {
        if (m_buffer is null)
        {
            return AppearanceFields.None;
        }

        AppearanceFields mask = this.EffectiveMask(target, wanted);
        if (mask == AppearanceFields.None)
        {
            return AppearanceFields.None;
        }

        // Taken before the write, so undo puts back exactly the parts this paste touched.
        m_undoBefore = target.GetAppearance();
        m_undoTarget = target;
        m_undoMask = mask;

        target.ApplyAppearance(m_buffer, mask);
        return mask;
    }

    /// <summary>True while the step back is still on offer for this element.</summary>
    public bool CanUndo(IAppearanceOwner target) => m_undoTarget == target && m_undoBefore is not null;

    public void Undo(IAppearanceOwner target)
    {
        if (!this.CanUndo(target) || m_undoBefore is null)
        {
            return;
        }

        target.ApplyAppearance(m_undoBefore, m_undoMask);
        this.ForgetUndo();
    }

    /// <summary>
    /// Drops the step back. Called when the user leaves the screen they pasted on: an undo
    /// button that outlives the thing it would undo is worse than none.
    /// </summary>
    public void ForgetUndo()
    {
        m_undoTarget = null;
        m_undoBefore = null;
        m_undoMask = AppearanceFields.None;
    }
}
