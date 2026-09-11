namespace WispUI.Appearance;

/// <summary>
/// Implemented by every HUD element. This interface, and nothing else, is what gives an
/// element copy and paste: nobody writes clipboard code for a new element, they implement
/// three members and the shared header block does the rest.
/// </summary>
internal interface IAppearanceOwner
{
    /// <summary>The element's name, as shown in the "from:" line of the paste panel.</summary>
    string DisplayName { get; }

    /// <summary>Which parts of an appearance this element actually has.</summary>
    AppearanceFields SupportedFields { get; }

    AppearanceBlock GetAppearance();

    /// <summary>
    /// Takes the parts named in <paramref name="mask"/> from <paramref name="source"/> and
    /// leaves everything else alone. The mask has already been narrowed to what both sides
    /// support, so an implementation only has to read the flags it knows.
    /// </summary>
    void ApplyAppearance(AppearanceBlock source, AppearanceFields mask);
}
