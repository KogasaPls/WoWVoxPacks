namespace WoWVoxPack.AddOns;

public interface IAddOnService<T> : IAddOnService where T : AddOn
{
    new Task<T> BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default);

    async Task<AddOn> IAddOnService.BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken)
    {
        return await BuildAddOnAsync(outputDirectoryBase, cancellationToken).ConfigureAwait(false);
    }
}

public interface IAddOnService
{
    Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default);
}
