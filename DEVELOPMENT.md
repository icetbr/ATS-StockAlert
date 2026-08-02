# Development Notes

- Use `E:\dnspy\export\Assembly-CSharp` as the primary reference for Against the Storm game code and UI behavior. Check the exported game implementation before guessing at game systems, tooltip layout, recipe availability, services, or panel behavior.
- Keep `TradeRouteDiagnostics/` out of GitHub and mod packages. It is local diagnostic scratch code.
- `dist/` is used for Thunderstore packaging and generated archives. Do not stage generated package outputs unless intentionally changing tracked package metadata.
- Latest released version is `1.2.6` on `main`. For releases, update `StockAlertInfo.cs`, `CHANGELOG.md`, `dist/thunderstore/manifest.json`, rebuild, refresh `dist/thunderstore/StockAlert.dll`, `README.md`, and `CHANGELOG.md`, then create `dist/StockAlert-Thunderstore-<version>.zip`.
- When releasing a new version, add a new changelog section above the previous released version. Do not rename or overwrite the previous release entry; the new section should only describe changes since that previous release.
- After a version is released, subsequent changes are for the next new version unless the user explicitly says to amend or revise the released version.
- The game has many useful public methods/properties even when services must be reached through reflection. Prefer game-exposed counters/state first, then reflection only to access service collections such as buildings, farmfields, or storages.
- For game UI/tooltips, inspect the original tooltip classes and components before tuning layout. The correct tooltip behavior often depends on using the game's existing tooltip model rather than only changing text width.
- World indicators should usually anchor to existing game UI anchors such as `ToRotate/UI/NoWorkersIcon` when available. If no anchor exists, fall back to renderer bounds.
