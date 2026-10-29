# Localization resources

The three UTF-8 JSON dictionaries (`zh-TW`, `en-US`, `zh-CN`) are embedded by `build.ps1` and accessed through `L.Get(key)`. They contain the same 422 non-empty keys. The executable does not need loose JSON files next to it.

- `Text000`–`Text236` are stable keys migrated from the existing application. Keep them stable while other UI work is reviewed.
- New interaction strings use descriptive keys such as `ReviewUninstall`, `EmptySearch` and `SelectionCount`.
- Add a key to all three dictionaries, then use `L.Get` in the UI or core-generated message. Preserve placeholders (`{0}` etc.), paths, version values and technical identifiers.
- `L.Get` falls back to Traditional Chinese for a missing or empty translation. Unknown keys display `[key]` so omissions are visible. An unknown culture falls back to `zh-TW`.
- `L.Validate()` rejects missing, extra or empty translations during `build.ps1 -Test`. All language choices and preference persistence are exercised by `ExperienceTests`.
- To add another language, add its dictionary, register the culture in `L.Languages` and `build.ps1`, and extend both visible language selectors and integration tests.
- Display author-supplied names through ModNames; do not machine-translate author names or descriptions. Display source categories through `L.Text`; source priority compares stable resource identity across languages.
- Existing diagnostic classification uses an invariant Traditional Chinese comparison view. This view must never be returned as the displayed exception; preserve `ex.Message` and localize only the manager-generated text and remedy.
- Activity entries store module IDs and resolve available localized names at display time. Exact resource-key messages and operation titles relocalize; composite technical details retain their event-time text. Native Windows file pickers and operating-system error details follow Windows.

The light workflow adds group selection, category, archive, Workshop-scope, cancellation and comparison keys. `tools/verify-localization.ps1` checks key parity, placeholders and literal UI references before compilation. Test-only unknown-key fallback probes are excluded from literal-reference checking.

Four theme names, problem-center remedies, batch preflight summaries and metadata diagnostics use the same dictionaries. Module display names belong to the independent author metadata contract in docs/MOD-METADATA-v1.md.

The guide and incremental-workflow update adds readable step resources, import cancellation and truthful written/unchanged file counts. Manager-owned stored diagnostics relocalize known prefixes; technical paths and author-provided content remain unchanged.
