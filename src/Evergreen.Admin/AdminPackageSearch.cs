using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine;
using UniGetUI.PackageEngine.Interfaces;

namespace Evergreen.Admin;

public sealed record AdminPackageSearchResult(
    string Name,
    string Id,
    string Version,
    string ManagerName,
    string ManagerDisplayName,
    string Source,
    Uri? IconUri);

public interface IAdminPackageSearch
{
    IReadOnlyList<string> ManagerNames { get; }
    Task<IReadOnlyList<AdminPackageSearchResult>> SearchAsync(
        string query,
        string? managerName,
        CancellationToken cancellationToken);
}

internal sealed class AdminPackageSearch : IAdminPackageSearch
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public IReadOnlyList<string> ManagerNames => PEInterface.Managers
        .Select(manager => manager.Name)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public async Task<IReadOnlyList<AdminPackageSearchResult>> SearchAsync(
        string query,
        string? managerName,
        CancellationToken cancellationToken)
    {
        string safeQuery = CoreTools.EnsureSafeQueryString(query.Trim());
        if (safeQuery.Length == 0) return [];
        await EnsureInitializedAsync(cancellationToken);
        IPackageManager[] managers = PEInterface.Managers
            .Where(manager => manager.IsReady()
                && (string.IsNullOrEmpty(managerName)
                    || string.Equals(manager.Name, managerName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var searches = managers.Select(manager => Task.Run(
            () => manager.FindPackages(safeQuery).Take(100).Select(ToResult).ToArray(),
            cancellationToken));
        AdminPackageSearchResult[][] results = await Task.WhenAll(searches);
        cancellationToken.ThrowIfCancellationRequested();
        return results.SelectMany(result => result)
            .DistinctBy(result => (result.ManagerName.ToUpperInvariant(), result.Source.ToUpperInvariant(), result.Id.ToUpperInvariant()))
            .OrderBy(result => string.Equals(result.Id, safeQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(result => result.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(250)
            .ToArray();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await Task.WhenAll(PEInterface.Managers.Select(manager => Task.Run(manager.Initialize, cancellationToken)));
            _initialized = true;
        }
        finally { _initializationLock.Release(); }
    }

    private static AdminPackageSearchResult ToResult(IPackage package)
    {
        Uri? iconUri = null;
        try { iconUri = package.GetIconUrlIfAny(); }
        catch { /* A missing source icon must not hide an otherwise valid package. */ }
        return new(
            package.Name,
            package.Id,
            package.HasConcreteVersion ? package.VersionString : "",
            package.Manager.Name,
            package.Manager.DisplayName,
            package.Source.Name,
            iconUri);
    }
}
