namespace WoWVoxPack.AddOns;

public interface IAddOnService
{
    Task BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default);
}
