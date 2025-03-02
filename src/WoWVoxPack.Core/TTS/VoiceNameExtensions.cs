namespace WoWVoxPack.TTS;

public static class VoiceNameExtensions
{
    public static string GetVoiceName(this VoiceName voiceName)
    {
        return voiceName switch
        {
            VoiceName.Default => "en-US-Standard-C",
            VoiceName.Aoede => "Aoede",
            VoiceName.Charon => "Charon",
            VoiceName.Fenrir => "Fenrir",
            VoiceName.Kore => "Kore",
            VoiceName.Leda => "Leda",
            VoiceName.Orus => "Orus",
            VoiceName.Puck => "Puck",
            VoiceName.Zephyr => "Zephyr",
            VoiceName.Casual_K => "en-US-Casual-K",
            VoiceName.Chirp_D => "en-US-Chirp-HD-D",
            VoiceName.Chirp_F => "en-US-Chirp-HD-F",
            VoiceName.Chirp_O => "en-US-Chirp-HD-O",
            VoiceName.Neural2_A => "en-US-Neural2-A",
            VoiceName.Neural2_C => "en-US-Neural2-C",
            VoiceName.Neural2_D => "en-US-Neural2-D",
            VoiceName.Neural2_E => "en-US-Neural2-E",
            VoiceName.Neural2_F => "en-US-Neural2-F",
            VoiceName.Neural2_G => "en-US-Neural2-G",
            VoiceName.Neural2_H => "en-US-Neural2-H",
            VoiceName.Neural2_I => "en-US-Neural2-I",
            VoiceName.Neural2_J => "en-US-Neural2-J",
            VoiceName.News_K => "en-US-News-K",
            VoiceName.News_L => "en-US-News-L",
            VoiceName.News_N => "en-US-News-N",
            VoiceName.Polyglot_1 => "en-US-Polyglot-1",
            VoiceName.Standard_A => "en-US-Standard-A",
            VoiceName.Standard_B => "en-US-Standard-B",
            VoiceName.Standard_C => "en-US-Standard-C",
            VoiceName.Standard_D => "en-US-Standard-D",
            VoiceName.Standard_E => "en-US-Standard-E",
            VoiceName.Standard_F => "en-US-Standard-F",
            VoiceName.Standard_G => "en-US-Standard-G",
            VoiceName.Standard_H => "en-US-Standard-H",
            VoiceName.Standard_I => "en-US-Standard-I",
            VoiceName.Standard_J => "en-US-Standard-J",
            VoiceName.Studio_O => "en-US-Studio-O",
            VoiceName.Studio_Q => "en-US-Studio-Q",
            VoiceName.Wavenet_A => "en-US-Wavenet-A",
            VoiceName.Wavenet_B => "en-US-Wavenet-B",
            VoiceName.Wavenet_C => "en-US-Wavenet-C",
            VoiceName.Wavenet_D => "en-US-Wavenet-D",
            VoiceName.Wavenet_E => "en-US-Wavenet-E",
            VoiceName.Wavenet_F => "en-US-Wavenet-F",
            VoiceName.Wavenet_G => "en-US-Wavenet-G",
            VoiceName.Wavenet_H => "en-US-Wavenet-H",
            VoiceName.Wavenet_I => "en-US-Wavenet-I",
            VoiceName.Wavenet_J => "en-US-Wavenet-J",
            _ => throw new ArgumentOutOfRangeException(nameof(voiceName), voiceName, null)
        };
    }
}
