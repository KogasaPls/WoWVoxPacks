namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Formats Callouts' colour-wrapped, voice-prefixed LibSharedMedia display key.</summary>
public static class CalloutsLsmKey
{
    public static string Format(string voice, string displayName) =>
        $"|cffff7f3fWVP {voice}: {displayName}|r";
}
