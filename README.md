# Subtitle Translator (Jellyfin plugin)

Jellyfin plugin that translates existing subtitle tracks into another language
using a configurable translation engine (LibreTranslate, DeepL or Google).

> Status: project scaffold only. No translation logic implemented yet.

## Project structure

```
Jellyfin.Plugin.SubtitleTranslator/
  Plugin.cs                         # Plugin entry point + config page registration
  PluginServiceRegistrator.cs       # DI registration
  Configuration/
    PluginConfiguration.cs          # Settings (engine, API url/key, languages)
    configPage.html                 # Admin config UI
  Providers/
    TranslateSubtitleProvider.cs    # ISubtitleProvider (stub)
  Translation/
    ITranslationService.cs          # Engine abstraction
    LibreTranslateService.cs        # LibreTranslate impl (stub)
manifest.json                       # Plugin repository manifest
build.yaml                          # Jellyfin build metadata
```

## Build

```pwsh
dotnet build Jellyfin.Plugin.SubtitleTranslator.sln -c Release
```

The compiled `Jellyfin.Plugin.SubtitleTranslator.dll` goes into the Jellyfin
`plugins/SubtitleTranslator` folder.

## License

GPLv3 (required when linking against Jellyfin NuGet packages).
