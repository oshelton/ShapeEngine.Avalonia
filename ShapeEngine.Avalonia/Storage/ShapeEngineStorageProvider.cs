using Avalonia.Platform.Storage;

namespace ShapeEngine.Avalonia.Storage;

/// <summary>
/// An <see cref="IStorageProvider"/> that resolves paths and well-known folders through the BCL.
/// </summary>
/// <remarks>
/// raylib has no native file pickers, so the interactive Open/Save methods are unsupported. The
/// non-interactive half - bookmarks, paths and well-known folders - is what controls need to work with
/// files a game hands them.
/// </remarks>
internal sealed class ShapeEngineStorageProvider : IStorageProvider
{
    public bool CanOpen => false;

    public bool CanSave => false;

    public bool CanPickFolder => false;

    public Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
        => Task.FromResult<IReadOnlyList<IStorageFile>>([]);

    public Task<OpenFilePickerResult> OpenFilePickerWithResultAsync(FilePickerOpenOptions options)
        => Task.FromResult(new OpenFilePickerResult { Files = [] });

    public Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
        => Task.FromResult<IStorageFile?>(null);

    public Task<SaveFilePickerResult> SaveFilePickerWithResultAsync(FilePickerSaveOptions options)
        => Task.FromResult(new SaveFilePickerResult());

    public Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
        => Task.FromResult<IReadOnlyList<IStorageFolder>>([]);

    public Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark)
        => Task.FromResult(TryGetFileFromPath(bookmark) as IStorageBookmarkFile);

    public Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark)
        => Task.FromResult(TryGetFolderFromPath(bookmark) as IStorageBookmarkFolder);

    public Task<IStorageFile?> TryGetFileFromPathAsync(Uri filePath)
        => Task.FromResult(filePath.IsAbsoluteUri && filePath.IsFile
            ? TryGetFileFromPath(filePath.LocalPath)
            : null);

    public Task<IStorageFolder?> TryGetFolderFromPathAsync(Uri folderPath)
        => Task.FromResult(folderPath.IsAbsoluteUri && folderPath.IsFile
            ? TryGetFolderFromPath(folderPath.LocalPath)
            : null);

    public Task<IStorageFolder?> TryGetWellKnownFolderAsync(WellKnownFolder wellKnownFolder)
    {
        var folder = wellKnownFolder switch
        {
            WellKnownFolder.Desktop => Environment.SpecialFolder.DesktopDirectory,
            WellKnownFolder.Documents => Environment.SpecialFolder.MyDocuments,
            WellKnownFolder.Downloads => (Environment.SpecialFolder?)null,
            WellKnownFolder.Music => Environment.SpecialFolder.MyMusic,
            WellKnownFolder.Pictures => Environment.SpecialFolder.MyPictures,
            WellKnownFolder.Videos => Environment.SpecialFolder.MyVideos,
            _ => null
        };

        // Downloads has no SpecialFolder entry on Windows; fall back to the conventional location.
        var path = folder is { } specialFolder
            ? Environment.GetFolderPath(specialFolder)
            : wellKnownFolder == WellKnownFolder.Downloads
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                : null;

        return Task.FromResult(String.IsNullOrEmpty(path) ? null : TryGetFolderFromPath(path));
    }

    private static IStorageFile? TryGetFileFromPath(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? new BclStorageFile(file) : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IStorageFolder? TryGetFolderFromPath(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.Exists ? new BclStorageFolder(directory) : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
