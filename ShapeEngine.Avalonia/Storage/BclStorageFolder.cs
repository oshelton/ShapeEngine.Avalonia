using System.Security;
using Avalonia.Platform.Storage;

namespace ShapeEngine.Avalonia.Storage;

/// <summary>An <see cref="IStorageFolder"/> over a plain filesystem directory.</summary>
internal sealed class BclStorageFolder : IStorageBookmarkFolder
{
    private Uri? path;

    public DirectoryInfo DirectoryInfo { get; }

    public BclStorageFolder(DirectoryInfo directoryInfo) => DirectoryInfo = directoryInfo;

    public string Name => DirectoryInfo.Name;

    public bool CanBookmark => true;

    public Uri Path => path ??= BuildPath();

    private Uri BuildPath()
    {
        try
        {
            return new UriBuilder { Scheme = Uri.UriSchemeFile, Host = String.Empty, Path = DirectoryInfo.FullName }.Uri;
        }
        catch (SecurityException)
        {
            return new Uri(DirectoryInfo.Name, UriKind.Relative);
        }
    }

    public Task<StorageItemProperties> GetBasicPropertiesAsync()
        => Task.FromResult(DirectoryInfo.Exists
            ? new StorageItemProperties(null, DirectoryInfo.CreationTimeUtc, DirectoryInfo.LastAccessTimeUtc)
            : new StorageItemProperties());

    public Task<IStorageFolder?> GetParentAsync()
        => Task.FromResult<IStorageFolder?>(DirectoryInfo.Parent is { } parent ? new BclStorageFolder(parent) : null);

    public async IAsyncEnumerable<IStorageItem> GetItemsAsync()
    {
        if (!DirectoryInfo.Exists) yield break;

        foreach (var item in DirectoryInfo.EnumerateFileSystemInfos())
        {
            if (item is FileInfo file) yield return new BclStorageFile(file);
            else if (item is DirectoryInfo directory) yield return new BclStorageFolder(directory);
        }

        await Task.CompletedTask;
    }

    public Task<string?> SaveBookmarkAsync()
        => Task.FromResult(DirectoryInfo.Exists ? DirectoryInfo.FullName : null);

    public Task ReleaseBookmarkAsync() => Task.CompletedTask;

    public Task DeleteAsync()
    {
        if (!DirectoryInfo.Exists) throw new DirectoryNotFoundException($"Directory not found: {DirectoryInfo.FullName}");

        DirectoryInfo.Delete(recursive: true);
        return Task.CompletedTask;
    }

    public Task<IStorageItem?> MoveAsync(IStorageFolder destination)
    {
        if (destination is not BclStorageFolder storageFolder) return Task.FromResult<IStorageItem?>(null);

        var newPath = System.IO.Path.Combine(storageFolder.DirectoryInfo.FullName, DirectoryInfo.Name);
        DirectoryInfo.MoveTo(newPath);

        return Task.FromResult<IStorageItem?>(new BclStorageFolder(new DirectoryInfo(newPath)));
    }

    public Task<IStorageFile?> CreateFileAsync(string name)
    {
        var newFile = new FileInfo(System.IO.Path.Combine(DirectoryInfo.FullName, name));
        using (newFile.Create()) { }

        return Task.FromResult<IStorageFile?>(new BclStorageFile(newFile));
    }

    public Task<IStorageFolder?> CreateFolderAsync(string name)
        => Task.FromResult<IStorageFolder?>(new BclStorageFolder(DirectoryInfo.CreateSubdirectory(name)));

    public Task<IStorageFile?> GetFileAsync(string name)
    {
        var file = new FileInfo(System.IO.Path.Combine(DirectoryInfo.FullName, name));
        return Task.FromResult<IStorageFile?>(file.Exists ? new BclStorageFile(file) : null);
    }

    public Task<IStorageFolder?> GetFolderAsync(string name)
    {
        var directory = new DirectoryInfo(System.IO.Path.Combine(DirectoryInfo.FullName, name));
        return Task.FromResult<IStorageFolder?>(directory.Exists ? new BclStorageFolder(directory) : null);
    }

    public void Dispose() { }
}
