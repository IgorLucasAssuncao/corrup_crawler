using Microsoft.Playwright;

public class PlaywrightBrowserService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _semaphore = new(1, 1); // Garante thread-safety

    public async Task<string> RenderizarAsync(string url)
    {
        // Garante que o navegador seja iniciado apenas uma vez (Lazy Loading)
        await _semaphore.WaitAsync();
        try
        {
            _playwright ??= await Playwright.CreateAsync();
            _browser ??= await _playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        finally
        {
            _semaphore.Release();
        }

        // Abre uma nova "aba" (Page)
        var page = await _browser!.NewPageAsync();
        try
        {
            // Timeout curto para não travar o crawler se a página demorar
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
            return await page.ContentAsync();
        }
        finally
        {
            await page.CloseAsync(); // Fecha a aba, mas mantém o navegador aberto
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}