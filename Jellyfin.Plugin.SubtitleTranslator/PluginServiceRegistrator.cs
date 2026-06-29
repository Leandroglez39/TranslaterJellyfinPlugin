using Jellyfin.Plugin.SubtitleTranslator.Translation;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SubtitleTranslator;

/// <summary>
/// Registers plugin services into Jellyfin's DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ITranslationService, OpenAiTranslationService>();
        serviceCollection.AddSingleton<SrtTranslator>();
    }
}
