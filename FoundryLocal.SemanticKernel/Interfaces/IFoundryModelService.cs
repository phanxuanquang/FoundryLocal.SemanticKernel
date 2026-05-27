using Microsoft.AI.Foundry.Local;

namespace FoundryLocal.SemanticKernel.Interfaces;

public interface IFoundryModelService
{
    IModel FoundryModel { get; }
    Task<IModel> GetModelAsync(CancellationToken cancellationToken = default);
    Task DownloadModelAsync(Action<float>? onProgress = null, CancellationToken cancellationToken = default);
    Task DeleteModelAsync(CancellationToken cancellationToken = default);
    Task LoadModelAsync(CancellationToken cancellationToken = default);
    Task UnloadModelAsync(CancellationToken cancellationToken = default);
    Task StartWebServiceWithModelAsync(CancellationToken cancellationToken = default);
    Task StopWebServiceWithModelAsync(CancellationToken cancellationToken = default);
}