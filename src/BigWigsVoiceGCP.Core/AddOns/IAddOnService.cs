namespace BigWigsVoiceGCP.AddOns;

public interface IAddOnService
{
    Task BuildAddOnAsync(string outputDirectory, CancellationToken cancellationToken = default);
}
