using FoundryLocal.SemanticKernel.Interfaces;
using FoundryLocal.SemanticKernel.Options;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoundryLocal.SemanticKernel.Implementations;

public class FoundryModelService(IOptions<FoundryLocalOptions> options, ILogger<FoundryModelService>? logger = null) : IFoundryModelService, IAsyncDisposable
{
    private readonly ILogger<FoundryModelService> _logger = logger ?? NullLogger<FoundryModelService>.Instance;
    private readonly FoundryLocalOptions _options = options.Value;
    private FoundryLocalManager? _manager;
    private IModel? _currentModel;
    private bool _isWebServiceRunning = false;

    public string ModelAlias => _options.ModelAlias;

    public async Task<IModel> GetModelAsync(CancellationToken cancellationToken = default)
    {
        if (_currentModel != null)
        {
            return _currentModel;
        }

        if (_manager == null)
        {
            _logger.LogInformation("Initializing FoundryLocalManager with app name '{AppName}' and log level '{LogLevel}'.", _options.AppName, _options.LogLevel);
            var config = new Configuration
            {
                AppName = string.IsNullOrEmpty(_options.AppName)
                    ? nameof(FoundryModelService)
                    : _options.AppName,
                LogLevel = _options.LogLevel,
                Web = new Configuration.WebService
                {
                    Urls = _options.WebServiceUrl
                }
            };

            await FoundryLocalManager.CreateAsync(config, _logger, cancellationToken);
            _manager = FoundryLocalManager.Instance;

            var currentEp = string.Empty;
            await _manager.DownloadAndRegisterEpsAsync((epName, percent) =>
            {
                if (epName != currentEp)
                {
                    currentEp = epName;
                }
                _logger.LogInformation("Downloading the execution provider '{EpName}': {Percent}%", epName, Math.Round(percent, 2));
            });
        }

        _logger.LogInformation("Retrieving model '{Alias}' from catalog.", ModelAlias);
        var catalog = await _manager.GetCatalogAsync(cancellationToken);

        _logger.LogInformation("Searching for model with alias or ID matching '{Alias}' in catalog.", ModelAlias);
        var models = await catalog.ListModelsAsync(cancellationToken);

        _currentModel = models.FirstOrDefault(m => m.Alias.Equals(ModelAlias, StringComparison.OrdinalIgnoreCase)
            || m.Id.Equals(ModelAlias, StringComparison.OrdinalIgnoreCase)
            || m.Variants.Any(v => v.Id.Equals(ModelAlias, StringComparison.OrdinalIgnoreCase)
                || v.Alias.Equals(ModelAlias, StringComparison.OrdinalIgnoreCase)))
             ?? throw new InvalidOperationException($"Model '{ModelAlias}' not found in catalog.");

        _logger.LogInformation("Model '{Alias}' found with ID '{Id}'.", ModelAlias, _currentModel.Id);

        if (!await _currentModel.IsCachedAsync(cancellationToken))
        {
            _logger.LogInformation("Model '{Alias}' is not cached. Starting download.", ModelAlias);
            await DownloadModelAsync(cancellationToken: cancellationToken);
        }

        return _currentModel;
    }

    public async Task DownloadModelAsync(Action<float>? onProgress = null, CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        if (await model.IsCachedAsync(cancellationToken))
        {
            _logger.LogInformation("Model '{Alias}' is already downloaded and cached. Skipping download.", ModelAlias);
            return;
        }

        await model.DownloadAsync(progress =>
        {
            onProgress?.Invoke(progress);
            _logger.LogInformation("Downloading the model '{Alias}': {Progress}%", ModelAlias, Math.Round(progress, 2));
        });

        _logger.LogInformation("Model '{Alias}' download completed and cached.", ModelAlias);
    }

    public async Task DeleteModelAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        var isDownloaded = await model.IsCachedAsync(cancellationToken);
        if (isDownloaded)
        {
            _logger.LogWarning("Deleting model '{Alias}' from cache.", ModelAlias);
            await model.RemoveFromCacheAsync(cancellationToken);
        }
    }

    public async Task LoadModelAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        var isLoaded = await model.IsLoadedAsync(cancellationToken);
        if (!isLoaded)
        {
            _logger.LogInformation("Loading model '{Alias}' into memory.", ModelAlias);
            await model.LoadAsync(cancellationToken);
        }
    }

    public async Task UnloadModelAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        var isLoaded = await model.IsLoadedAsync(cancellationToken);
        if (isLoaded)
        {
            _logger.LogWarning("Unloading model '{Alias}' from memory.", ModelAlias);
            await model.UnloadAsync(cancellationToken);
            _currentModel = null;
        }
    }

    public async Task StartWebServiceWithModelAsync(CancellationToken cancellationToken = default)
    {
        if (_isWebServiceRunning)
        {
            _logger.LogTrace("Web service is already running with model '{Alias}'.", ModelAlias);
            return;
        }

        await LoadModelAsync(cancellationToken);

        _logger.LogInformation("Starting web service with model '{Alias}'.", ModelAlias);
        await _manager!.StartWebServiceAsync();
        _isWebServiceRunning = true;
    }

    public async Task StopWebServiceWithModelAsync(CancellationToken cancellationToken = default)
    {
        if (!_isWebServiceRunning)
        {
            _logger.LogTrace("Web service is not running, so no need to stop it.");
            return;
        }

        _logger.LogInformation("Stopping web service with model '{Alias}'.", ModelAlias);
        await _manager!.StopWebServiceAsync();
        _isWebServiceRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isWebServiceRunning)
        {
            await StopWebServiceWithModelAsync();
        }

        if (_currentModel != null)
        {
            await _currentModel.UnloadAsync();
        }

        if (_manager != null)
        {
            _manager.Dispose();
            _manager = null;
        }
    }
}