using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace WispUI.Core;

/// <summary>
/// Which parts of a profile an import takes. By module, because that is the unit somebody
/// thinks in — "I want their party frames, not their damage meter" (Florian, 2026-09-20).
/// </summary>
[Flags]
public enum ProfileParts
{
    None = 0,
    PartyFrames = 1 << 0,

    /// <summary>Who the profile is for: the role or the job list, and the automatic switch.</summary>
    Scope = 1 << 1,

    All = PartyFrames | Scope,
}

/// <summary>
/// A profile as a line of text somebody can paste into a chat window.
/// <para>
/// JSON, deflated, Base64, behind a prefix that says what it is and which shape it is in.
/// A few hundred characters rather than several thousand, which is the difference between
/// something that fits in a message and something nobody sends.
/// </para>
/// <para>
/// 🔴 A pasted code is UNTRUSTED INPUT and is treated as such throughout. Nothing here
/// reflects, resolves a type name, or builds a path: it deserialises into one known class
/// and every number in it then goes through the same <c>Sanitise</c> the configuration file
/// does. The two things a hostile code could otherwise do are run the reader out of memory
/// and hand the renderer a number no slider could produce, and the two guards below are
/// exactly those: a length cap before decompressing, and a cap on what comes out.
/// </para>
/// </summary>
internal static class ProfileCode
{
    /// <summary>
    /// What every code starts with. The digit is the shape, not the plugin version: it
    /// changes only when what is inside is arranged differently, so a code made today keeps
    /// working and one made by a future shape is refused with something to say rather than
    /// failing somewhere deeper.
    /// </summary>
    private const string Prefix = "WISP1:";

    /// <summary>
    /// The longest code that will be looked at, in characters. A profile runs to roughly
    /// six hundred; this is far above anything real and far below anything that costs
    /// time to reject.
    /// </summary>
    private const int MaxCodeLength = 16 * 1024;

    /// <summary>
    /// The most a code may expand to. The guard that matters: deflate compresses a
    /// megabyte of one repeated character into a few hundred bytes, so a short code can ask
    /// for as much memory as the reader is willing to give it. The reader is willing to
    /// give it this much.
    /// </summary>
    private const int MaxUnpackedBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        // Nothing indented and no field it does not know: a code is for sending, and a
        // property this build has never heard of is a newer shape's business, not ours.
        WriteIndented = false,
    };

    /// <summary>Turns a profile into the line somebody pastes to a friend.</summary>
    public static string Write(Profile profile)
    {
        // A copy, so the code carries what a profile carries and not whatever else is
        // hanging off the live object — see Profile.Copy for the list and what is left out.
        Payload payload = new()
        {
            Name = profile.Name,
            ScopeKind = profile.ScopeKind,
            Role = profile.Role,
            Jobs = profile.Jobs.ToArray(),
            Automatic = profile.Automatic,
            PartyFramesEnabled = profile.PartyFramesEnabled,
            PartyFrames = Profile.CloneConfig(profile.PartyFrames),
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, Options);

        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
        {
            deflate.Write(json, 0, json.Length);
        }

        return Prefix + Convert.ToBase64String(packed.ToArray());
    }

    /// <summary>
    /// Reads a code back, or says why it could not.
    /// <para>
    /// Every failure is caught here and answered with a sentence for the screen. A paste is
    /// the one place in the suite where the input is somebody else's, and the shapes it can
    /// arrive in are: not a code at all, a code truncated by a chat client, a code from a
    /// newer build, and something meant to break the reader. None of those may reach the
    /// player as a stack trace.
    /// </para>
    /// </summary>
    public static bool TryRead(string? code, out Profile profile, out string problem)
    {
        profile = new Profile();
        problem = string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            problem = Localization.Strings.ProfileCodeEmpty;
            return false;
        }

        code = code.Trim();

        if (code.Length > MaxCodeLength)
        {
            problem = Localization.Strings.ProfileCodeUnreadable;
            return false;
        }

        if (!code.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Told apart from unreadable on purpose: pasting the wrong thing entirely is
            // much the commonest way this goes wrong, and "that is not a WispUI code" is a
            // different thing to do about it than "that code is damaged".
            problem = code.StartsWith("WISP", StringComparison.OrdinalIgnoreCase)
                ? Localization.Strings.ProfileCodeNewer
                : Localization.Strings.ProfileCodeNotOurs;
            return false;
        }

        try
        {
            byte[] packed = Convert.FromBase64String(code[Prefix.Length..]);
            byte[]? json = Unpack(packed);

            if (json is null)
            {
                problem = Localization.Strings.ProfileCodeUnreadable;
                return false;
            }

            Payload? payload = JsonSerializer.Deserialize<Payload>(json, Options);

            if (payload is null)
            {
                problem = Localization.Strings.ProfileCodeUnreadable;
                return false;
            }

            profile = new Profile
            {
                Name = payload.Name ?? string.Empty,
                ScopeKind = payload.ScopeKind,
                Role = payload.Role,
                Automatic = payload.Automatic,
                PartyFramesEnabled = payload.PartyFramesEnabled,
                PartyFrames = payload.PartyFrames ?? new Configuration.PartyFramesConfig(),
            };

            if (payload.Jobs is not null)
            {
                profile.Jobs.AddRange(payload.Jobs);
            }

            // The same pass a file on disk gets. Everything above this line came from
            // somebody else; nothing below it has to wonder about that.
            profile.Sanitise();
            return true;
        }
        catch (Exception ex)
        {
            // Logged rather than shown. What went wrong inside a decoder is of no use to
            // the person holding the code, and the sentence they get says what to do.
            Services.Log.Debug(ex, "A pasted profile code could not be read.");
            problem = Localization.Strings.ProfileCodeUnreadable;
            return false;
        }
    }

    /// <summary>
    /// Inflates, refusing anything that keeps growing. Read in blocks and counted, because
    /// the only honest way to cap what comes out of a decompressor is to stop taking it.
    /// </summary>
    private static byte[]? Unpack(byte[] packed)
    {
        using var source = new MemoryStream(packed);
        using var deflate = new DeflateStream(source, CompressionMode.Decompress);
        using var grown = new MemoryStream();

        byte[] block = new byte[8192];
        int total = 0;

        while (true)
        {
            int read = deflate.Read(block, 0, block.Length);

            if (read <= 0)
            {
                break;
            }

            total += read;

            if (total > MaxUnpackedBytes)
            {
                return null;
            }

            grown.Write(block, 0, read);
        }

        return grown.ToArray();
    }

    /// <summary>
    /// What actually travels. Its own class rather than the profile itself, so the shape of
    /// a code is something we decide and change on purpose, and adding a field to a profile
    /// is never accidentally a change to the format friends are sending each other.
    /// </summary>
    private sealed class Payload
    {
        public string? Name { get; set; }

        public int ScopeKind { get; set; }

        public int Role { get; set; }

        public uint[]? Jobs { get; set; }

        public bool Automatic { get; set; }

        public bool PartyFramesEnabled { get; set; }

        public Configuration.PartyFramesConfig? PartyFrames { get; set; }
    }
}
