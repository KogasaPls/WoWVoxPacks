namespace WoWVoxPack.AddOns;

public interface IAddOnService
{
    Task BuildAddOnAsync(string outputDirectory, CancellationToken cancellationToken = default);
}
