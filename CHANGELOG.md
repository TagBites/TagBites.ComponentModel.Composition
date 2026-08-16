# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- The license is now Apache-2.0, previously MIT.

## [1.2.1] - 2026-07-30

### Fixed

- `Register` and `Unregister(ExportComponent)` now raise `ExportCollectionChanged`. The event condition was inverted: a component registered through the public API never raised the event, while the internal export restore in `UnloadAssembly` raised it twice. `Unregister(Uri)` is unchanged.
- `ExportComponent.Instance` is thread safe. Two threads reading `Instance` at the same time could each create an own instance; now the first access creates the shared instance and later accesses return it.
- `UnloadAssembly` drops the bookkeeping of exports it restored. With `ExportDuplicateUriHandling.RemoveExisting`, a second load and unload of the same assembly restored the exports recorded during the first load, and the records were kept for the lifetime of the process together with the components they referenced.
- A manually created export validates its types. `ExportComponentDefinition` rejects a value type that is not assignable to the contract type, and `ExportComponent<T>` rejects a contract type other than `T`. Both cases used to pass and then fail with `InvalidCastException` inside `GetExports`.

### Changed

- `ContractDefinition.ContactName` and `ContractDefinition.ContactType` are renamed to `ContractName` and `ContractType`. The old properties remain as hidden `[Obsolete]` forwarders.
- `TryCreateManyExportInstances` is renamed to `CreateManyExportInstances` (both overloads); the method never had try semantics. The old methods remain as hidden `[Obsolete]` forwarders.
- `UnregisterCore` stays public but is hidden from IntelliSense. `Unregister(ExportComponent)` is the member to call; it now also raises `ExportCollectionChanged`.
- `AssemblyExportSettingsAttribute` shows up in IntelliSense. The attribute configures `ExportDuplicateUriHandling`, so hiding it kept the only way to set the mode out of sight.
- `Register` throws `InvalidOperationException` instead of `Exception` when an export with the same URI is already registered, and `ArgumentException` instead of `ArgumentNullException` when the component location is `null`.
- `PrepareCache` removes stale cache files of the same assembly after writing a new one. The old behavior kept a file for every past build, so the cache directory grew without limit.
- The package targets `netstandard2.0` only. The `net5.0` target carried the same code, the same public API and the same dependency, so a project on .NET 5 or later now resolves the `netstandard2.0` assets instead.

### Added

- Test project `TagBites.ComponentModel.Composition.Tests` (xUnit), run by the `build & test` workflow and before every publish.
- Documentation comments on the public members of `ExportComponentManager`.

## [1.2.0] - 2025-11-28

### Changed

- The cache file name includes the assembly module version id instead of file size and write time. The cache now invalidates exactly when the assembly content changes, independent of file metadata.
- The export map is a plain dictionary keyed by contract type and contract name, which simplifies lookup and registration paths.

## [1.1.0] - 2025-07-04

### Added

- Export override with the same URI. The `AssemblyExportSettings` assembly attribute selects the behavior for duplicate URIs via `ExportDuplicateUriHandling`: `SkipCurrent` (default, the new export is ignored), `OverrideExisting` (the new export wins URI lookups) or `RemoveExisting` (the existing export is removed and restored when the new assembly unloads).

## [1.0.2] - 2023-12-07

### Fixed

- A failed `LoadAssembly` call no longer leaves the manager in an inconsistent state. When an assembly cannot be processed completely, the manager unloads it and rethrows, so either all exports of the assembly are available or none.

## [1.0.1] - 2021-07-29

First stable release. Export container with lookup by contract type, contract name and URI; manual component registration with an instance provider for types without a default constructor; change notifications per contract type; assembly scan cache with caller-provided serialization.

[Unreleased]: https://github.com/TagBites/TagBites.ComponentModel.Composition/compare/1.2.1...HEAD
[1.2.1]: https://github.com/TagBites/TagBites.ComponentModel.Composition/compare/1.2.0...1.2.1
[1.2.0]: https://github.com/TagBites/TagBites.ComponentModel.Composition/compare/1.1.0...1.2.0
[1.1.0]: https://github.com/TagBites/TagBites.ComponentModel.Composition/compare/1.0.2...1.1.0
[1.0.2]: https://github.com/TagBites/TagBites.ComponentModel.Composition/compare/1.0.1...1.0.2
[1.0.1]: https://github.com/TagBites/TagBites.ComponentModel.Composition/releases/tag/1.0.1
