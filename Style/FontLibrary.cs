using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Interface.GameFonts;
using WispUI.Core;

namespace WispUI.Style;

/// <summary>
/// One face the HUD can be set to, whatever it came from.
/// <para>
/// The three sources are deliberately not three settings. A face out of the game files, a
/// face shipped with the plugin and a face the player dropped into a folder are the same
/// kind of choice to the person making it, so they are one list.
/// </para>
/// </summary>
internal sealed class FontChoice
{
    /// <summary>
    /// What it is called, in the list and in the saved configuration. The name is the key —
    /// not a position in the list — because the list grows and shrinks with the contents of a
    /// folder, and a stored index would quietly mean a different face the moment it did.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The game family, for a face read out of the game files.</summary>
    public GameFontFamily GameFamily { get; init; } = GameFontFamily.Axis;

    /// <summary>The file, for a face that comes from one. Null for a game face.</summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Whether this face can be rendered crisply at any size. True only for files: the game's
    /// faces are bitmaps that exist at the sizes the game ships them in and are stretched
    /// everywhere else, however they are asked for.
    /// </summary>
    public bool IsVector => this.FilePath is not null;
}

/// <summary>
/// Every face the HUD can be set to: the game's own, the two that ship with the plugin, and
/// whatever the player has put in their own font folder.
/// <para>
/// The folder is the answer to a question that would otherwise keep coming back — "can we add
/// this one font as well". Most fonts worth asking for cannot be shipped: they are free to
/// use and not free to redistribute, which is not the same thing and is the distinction a
/// plugin that wants to be published has to respect. A folder needs no licence from anybody,
/// because nothing is distributed (Florian, 2026-09-12, asking for Expressway).
/// </para>
/// </summary>
internal static class FontLibrary
{
    /// <summary>The name of the face everything falls back to, and the one shipped default.</summary>
    public const string DefaultName = "Axis";

    /// <summary>
    /// A ceiling on what the folder can contribute. Not a technical limit — a list nobody can
    /// walk is a worse answer than a full folder, and anyone with more than this many fonts is
    /// choosing between them somewhere other than here.
    /// </summary>
    private const int MaxUserFonts = 32;

    private static readonly List<FontChoice> Choices = new();
    private static FontChoice[] s_array = Array.Empty<FontChoice>();

    /// <summary>The faces, in the order the list walks them. Rebuilt only by <see cref="Refresh"/>.</summary>
    public static FontChoice[] All => s_array;

    /// <summary>Where a player puts a face of their own. Made if it is not there.</summary>
    public static string UserFolder =>
        Path.Combine(Services.PluginInterface.ConfigDirectory.FullName, "Fonts");

    /// <summary>
    /// Reads the folder and rebuilds the list. Called at load and when the player asks for it
    /// — never per frame, and never from the draw path: this touches the disk.
    /// </summary>
    public static void Refresh()
    {
        Choices.Clear();

        // The game's own, which need no file and are always there.
        Add("Axis", GameFontFamily.Axis);
        Add("Miedinger", GameFontFamily.MiedingerMid);
        Add("Trump Gothic", GameFontFamily.TrumpGothic);
        Add("Jupiter", GameFontFamily.Jupiter);

        // The two that ship with the plugin, if the build put them where it should.
        AddFile("Figtree", ShippedPath("Figtree-ExtraBold.ttf"));
        AddFile("DM Sans", ShippedPath("DMSans-Bold.ttf"));

        AddUserFonts();

        s_array = Choices.ToArray();
    }

    /// <summary>
    /// The face with this name, or the default when there is none — a font folder that lost a
    /// file must leave the frames lettered in something, not unlettered.
    /// </summary>
    public static FontChoice Find(string? name)
    {
        for (int i = 0; i < s_array.Length; i++)
        {
            if (string.Equals(s_array[i].Name, name, StringComparison.Ordinal))
            {
                return s_array[i];
            }
        }

        return s_array.Length > 0 ? s_array[0] : new FontChoice { Name = DefaultName };
    }

    /// <summary>Where this face sits in the list, or zero when it is no longer in it.</summary>
    public static int IndexOf(string? name)
    {
        for (int i = 0; i < s_array.Length; i++)
        {
            if (string.Equals(s_array[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>The name at this position, for a list that hands back a position.</summary>
    public static string NameAt(int index) =>
        index >= 0 && index < s_array.Length ? s_array[index].Name : DefaultName;

    private static void Add(string name, GameFontFamily family) =>
        Choices.Add(new FontChoice { Name = name, GameFamily = family });

    private static void AddFile(string name, string path)
    {
        if (!File.Exists(path))
        {
            Services.Log.Warning("A shipped font is missing and will not be offered: {Path}", path);
            return;
        }

        Choices.Add(new FontChoice { Name = name, FilePath = path });
    }

    /// <summary>
    /// Whatever the player put in their folder. Named after the file, because that is the name
    /// they know it by — and a name that is already taken is skipped rather than shadowing a
    /// face that is always there.
    /// </summary>
    private static void AddUserFonts()
    {
        string folder = UserFolder;

        try
        {
            if (!Directory.Exists(folder))
            {
                // Made even when empty, so that "put a font here" points somewhere real.
                Directory.CreateDirectory(folder);
                return;
            }

            int added = 0;

            foreach (string path in Directory.EnumerateFiles(folder))
            {
                if (added >= MaxUserFonts)
                {
                    Services.Log.Warning("More than {Max} fonts in {Folder}; the rest are ignored.", MaxUserFonts, folder);
                    break;
                }

                string extension = Path.GetExtension(path);

                if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);

                if (name.Length == 0 || Taken(name))
                {
                    continue;
                }

                Choices.Add(new FontChoice { Name = name, FilePath = path });
                added++;
            }

            if (added > 0)
            {
                Services.Log.Information("Found {Count} font(s) in {Folder}.", added, folder);
            }
        }
        catch (Exception ex)
        {
            // A folder that cannot be read costs the fonts in it and nothing else.
            Services.Log.Error(ex, "The font folder could not be read: {Folder}", folder);
        }
    }

    private static bool Taken(string name)
    {
        for (int i = 0; i < Choices.Count; i++)
        {
            if (string.Equals(Choices[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ShippedPath(string fileName)
    {
        string? dir = Services.PluginInterface.AssemblyLocation.DirectoryName;
        return dir is null ? fileName : Path.Combine(dir, "Fonts", fileName);
    }
}
