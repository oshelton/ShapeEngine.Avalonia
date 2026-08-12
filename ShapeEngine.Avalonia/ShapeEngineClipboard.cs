using Avalonia.Input;
using Avalonia.Input.Platform;
using Raylib_cs;

namespace ShapeEngine.Avalonia;

/// <summary>
/// An <see cref="IClipboard"/> backed by raylib's clipboard functions. Plain text only, which is what
/// the built-in text controls need; anything richer is dropped.
/// </summary>
internal sealed class ShapeEngineClipboard : IClipboard
{
    public Task ClearAsync()
    {
        Raylib.SetClipboardText(String.Empty);
        return Task.CompletedTask;
    }

    public Task SetDataAsync(IAsyncDataTransfer? dataTransfer)
    {
        Raylib.SetClipboardText(TryGetText(dataTransfer) ?? String.Empty);
        return Task.CompletedTask;
    }

    public Task FlushAsync() => Task.CompletedTask;

    public Task<IAsyncDataTransfer?> TryGetDataAsync()
    {
        var text = Raylib.GetClipboardText_();

        if (String.IsNullOrEmpty(text)) return Task.FromResult<IAsyncDataTransfer?>(null);

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateText(text));

        return Task.FromResult<IAsyncDataTransfer?>(dataTransfer);
    }

    public Task<IAsyncDataTransfer?> TryGetInProcessDataAsync() => Task.FromResult<IAsyncDataTransfer?>(null);

    private static string? TryGetText(IAsyncDataTransfer? dataTransfer)
    {
        if (dataTransfer is null) return null;

        foreach (var item in dataTransfer.Items)
        {
            foreach (var format in item.Formats)
            {
                if (format.Equals(DataFormat.Text) && item is IDataTransferItem syncItem)
                {
                    return syncItem.TryGetRaw(DataFormat.Text) as string;
                }
            }
        }

        return null;
    }
}
