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

    public IModel FoundryModel => _currentModel ?? throw new InvalidOperationException("Model is not loaded.");

    public async Task<IModel> GetModelAsync(CancellationToken cancellationToken = default)
    {
        if (_currentModel != null)
        {
            return _currentModel;
        }

        if (_manager == null || !FoundryLocalManager.IsInitialized)
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
        }

        _logger.LogInformation("Retrieving model with the alias '{Alias}' from catalog.", _options.ModelAlias);
        var catalog = await _manager.GetCatalogAsync(cancellationToken);

        _currentModel = await catalog.GetModelAsync(_options.ModelAlias, cancellationToken)
             ?? throw new InvalidOperationException($"Model with the alias '{_options.ModelAlias}' not found in catalog.");

        _logger.LogInformation("Model with the alias '{Alias}' found with ID '{Id}'.", _currentModel?.Alias, _currentModel?.Id);

        if (!await _currentModel!.IsCachedAsync(cancellationToken))
        {
            _logger.LogInformation("Model '{Alias}' is not cached. Starting download.", _currentModel?.Id);
            await DownloadModelAsync(cancellationToken: cancellationToken);
        }

        var currentEp = string.Empty;
        var currentPercent = 0;
        await _manager!.DownloadAndRegisterEpsAsync((epName, percent) =>
        {
            currentEp = epName;
            var percentValue = (int)percent;

            if (percentValue >= 100)
            {
                _logger.LogInformation("The execution provider '{EpName}' has been registered already.", epName);
            }
            else if (percentValue != currentPercent)
            {
                currentPercent = percentValue;
                _logger.LogInformation("Downloading the execution provider '{EpName}': {Percent}%", epName, Math.Round(percent, 2));
            }
        });

        return _currentModel!;
    }

    public async Task DownloadModelAsync(Action<float>? onProgress = null, CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        if (await model.IsCachedAsync(cancellationToken))
        {
            _logger.LogInformation("Model '{Alias}' is already downloaded and cached. Skipping download.", _currentModel?.Id);
            return;
        }

        var currentProgress = 0;
        await model.DownloadAsync(progress =>
        {
            onProgress?.Invoke(progress);

            if ((int)progress != currentProgress)
            {
                currentProgress = (int)progress;
                _logger.LogInformation("Downloading the model '{Alias}': {Progress}%", _currentModel?.Id, Math.Round(progress, 2));
            }
        });

        _logger.LogInformation("Model '{Alias}' download completed and cached.", _currentModel?.Id);
    }

    public async Task DeleteModelAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        var isDownloaded = await model.IsCachedAsync(cancellationToken);
        if (isDownloaded)
        {
            _logger.LogWarning("Deleting model '{Alias}' from cache.", _currentModel?.Id);
            await model.RemoveFromCacheAsync(cancellationToken);
        }
    }

    public async Task LoadModelAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        var isLoaded = await model.IsLoadedAsync(cancellationToken);
        if (!isLoaded)
        {
            _logger.LogInformation("Loading model '{Alias}' into memory.", _currentModel?.Id);
            await model.LoadAsync(cancellationToken);
        }
    }

    public async Task UnloadModelAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);

        var isLoaded = await model.IsLoadedAsync(cancellationToken);
        if (isLoaded)
        {
            _logger.LogWarning("Unloading model '{Alias}' from memory.", _currentModel?.Id);
            await model.UnloadAsync(cancellationToken);
            _currentModel = null;
        }
    }

    public async Task StartWebServiceWithModelAsync(CancellationToken cancellationToken = default)
    {
        if (_isWebServiceRunning)
        {
            _logger.LogTrace("Web service is already running with model '{Alias}'.", _currentModel?.Id);
            return;
        }

        await LoadModelAsync(cancellationToken);

        _logger.LogInformation("Starting web service with model '{Alias}'.", _currentModel?.Id);
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

        _logger.LogInformation("Stopping web service with model '{Alias}'.", _currentModel?.Id);
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