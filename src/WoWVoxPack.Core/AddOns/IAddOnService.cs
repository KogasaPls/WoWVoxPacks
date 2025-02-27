namespace WoWVoxPack.AddOns;

public interface IAddOnService<T> : IAddOnService where T : AddOn
{
    async Task<AddOn> IAddOnService.BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken)
    {
        return await BuildAddOnAsync(outputDirectoryBase, cancellationToken).ConfigureAwait(false);
    }

    new Task<T> BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default);
}

public interface IAddOnService
{
    Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default);
}
