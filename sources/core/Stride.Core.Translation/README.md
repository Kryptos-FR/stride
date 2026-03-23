# Stride.Core.Translation

Localization and translation infrastructure for the Stride editor (Game Studio).

- `TranslationManager` — central service for string translation lookups
- `ITranslationProvider` — pluggable backend interface for translation sources
- Gettext-based `.po` / `.mo` file support
- Used internally by Game Studio UI for editor localization
