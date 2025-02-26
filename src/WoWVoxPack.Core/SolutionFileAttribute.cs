namespace WoWVoxPack;

[AttributeUsage(AttributeTargets.Assembly)]
public class SolutionFileAttribute : Attribute
{
    public SolutionFileAttribute(string solutionFile)
    {
        SolutionFile = solutionFile;
    }

    public string SolutionFile { get; set; }
}
