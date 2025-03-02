namespace WoWVoxPack.TTS;

public class SoundFileContentEqualityComparer : IEqualityComparer<SoundFile>
{
    public static SoundFileContentEqualityComparer Default { get; } = new();

    public bool Equals(SoundFile? x, SoundFile? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null)
        {
            return false;
        }

        if (y is null)
        {
            return false;
        }

        return x.Text == y.Text && x.Ssml == y.Ssml && x.FileName == y.FileName;
    }

    public int GetHashCode(SoundFile obj)
    {
        return HashCode.Combine(obj.Text, obj.Ssml, obj.FileName);
    }
}
