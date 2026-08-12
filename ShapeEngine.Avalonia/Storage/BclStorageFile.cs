using System.Security;
using Avalonia.Platform.Storage;

namespace ShapeEngine.Avalonia.Storage;

/// <summary>An <see cref="IStorageFile"/> over a plain filesystem path.</summary>
internal sealed class BclStorageFile : IStorageBookmarkFile
{
    private Uri? path;

    public FileInfo FileInfo { get; }

    public BclStorageFile(FileInfo fileInfo) => FileInfo = fileInfo;

    public string Name => FileInfo.Name;

    public bool CanBookmark => true;

    public Uri Path => path ??= BuildPath();

    private Uri BuildPath()
    {
        try
        {
            if (FileInfo.Directory is not null)
            {
                return new UriBuilder { Scheme = Uri.UriSchemeFile, Host = String.Empty, Path = FileInfo.FullName }.Uri;
            }
        }
        catch (SecurityException)
        {
            // Fall through to the relative form below.
        }

        return new Uri(FileInfo.Name, UriKind.Relative);
    }

    public Task<StorageItemProperties> GetBasicPropertiesAsync()
        => Task.FromResult(FileInfo.Exists
            ? new StorageItemProperties((ulong)FileInfo.Length, FileInfo.CreationTimeUtc, FileInfo.LastAccessTimeUtc)
            : new StorageItemProperties());

    public Task<IStorageFolder?> GetParentAsync()
        => Task.FromResult<IStorageFolder?>(FileInfo.Directory is { } directory ? new BclStorageFolder(directory) : null);

    public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(FileInfo.OpenRead());

    public Task<Stream> OpenWriteAsync()
        => Task.FromResult<Stream>(new FileStream(FileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Write));

    public Task<string?> SaveBookmarkAsync()
        => Task.FromResult(FileInfo.Exists ? FileInfo.FullName : null);

    public Task ReleaseBookmarkAsync() => Task.CompletedTask;

    public Task DeleteAsync()
    {
        if (!FileInfo.Exists) throw new FileNotFoundException($"File not found: {FileInfo.FullName}");

        FileInfo.Delete();
        return Task.CompletedTask;
    }

    public Task<IStorageItem?> MoveAsync(IStorageFolder destination)
    {
        if (destination is not BclStorageFolder storageFolder) return Task.FromResult<IStorageItem?>(null);

        var newPath = System.IO.Path.Combine(storageFolder.DirectoryInfo.FullName, FileInfo.Name);
        FileInfo.MoveTo(newPath);

        return Task.FromResult<IStorageItem?>(new BclStorageFile(new FileInfo(newPath)));
    }

    public void Dispose() { }
}
