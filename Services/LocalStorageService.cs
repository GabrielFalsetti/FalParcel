using Microsoft.JSInterop;

namespace Parcelly.Services;

public class LocalStorageService(IJSRuntime js)
{
    public async Task<string?> GetAsync(string key) =>
        await js.InvokeAsync<string?>("parcelly.get", key);

    public async Task SetAsync(string key, string value) =>
        await js.InvokeVoidAsync("parcelly.set", key, value);

    public async Task RemoveAsync(string key) =>
        await js.InvokeVoidAsync("parcelly.remove", key);

    public async Task DownloadTextAsync(string filename, string content, string mime) =>
        await js.InvokeVoidAsync("parcelly.downloadText", filename, content, mime);

    public async Task DownloadBytesAsync(string filename, byte[] bytes, string mime) =>
        await js.InvokeVoidAsync("parcelly.downloadBytes", filename, Convert.ToBase64String(bytes), mime);
}
