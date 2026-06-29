# Subtitle Translator (Jellyfin plugin)

Jellyfin plugin that takes an embedded text subtitle track from a video,
translates it with OpenAI (latest nano model, `gpt-5.4-nano`, via the Responses
API) and offers it as a new selectable subtitle track.

## Project structure

```
Jellyfin.Plugin.SubtitleTranslator/
  Plugin.cs                         # Plugin entry point + config page registration
  PluginServiceRegistrator.cs       # DI registration
  Configuration/
    PluginConfiguration.cs          # Settings (API url/key, model, languages, batch)
    configPage.html                 # Admin config UI
  Providers/
    TranslateSubtitleProvider.cs    # ISubtitleProvider: extracts + translates embedded subs
  Subtitles/
    SrtSubtitle.cs                  # SRT parser/serializer
  Translation/
    ITranslationService.cs          # Engine abstraction
    OpenAiTranslationService.cs     # OpenAI Responses API client
    SubtitleTranslator.cs           # Batched SRT translation
manifest.json                       # Plugin repository manifest
build.yaml                          # Jellyfin build metadata
```

## Build

```pwsh
dotnet build Jellyfin.Plugin.SubtitleTranslator.sln -c Release
```

The compiled `Jellyfin.Plugin.SubtitleTranslator.dll` goes into the Jellyfin
plugins folder.

## Usage

### 1. Install

Copy `Jellyfin.Plugin.SubtitleTranslator.dll` into the server's plugins folder
and restart Jellyfin:

- Windows (direct install): `%UserProfile%\AppData\Local\jellyfin\plugins`
- Windows (tray install): `%ProgramData%\Jellyfin\Server\plugins`
- Linux: `/var/lib/jellyfin/plugins/`

After restart it appears under **Dashboard → Plugins**.

### 2. Configure

Open **Dashboard → Plugins → Subtitle Translator** and set:

- **OpenAI API key** — required (or leave empty and set the `OPENAI_API_KEY`
  environment variable on the server). Never commit it.
- **Model** — defaults to `gpt-5.4-nano`.
- **Source language** — `auto` or an ISO 639-1 code.
- **Target language** — e.g. `es`.
- **Cues per request** — batch size sent to OpenAI.

### 3. Translate a subtitle

For a movie/episode that has an embedded text subtitle track, open the item →
**…  → Subtitles → Search/Download**. The "Subtitle Translator (OpenAI)" provider
offers a translated track; selecting it extracts the embedded subtitle, sends it
to OpenAI in batches preserving timestamps, and adds the result as a new track.

> Internet: requires outbound HTTPS to `https://api.openai.com`. Translation is a
> paid OpenAI API call — costs scale with subtitle length. Use a self-hosted /
> local engine if you need offline operation.

## License

GPLv3 (required when linking against Jellyfin NuGet packages).
