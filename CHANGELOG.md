# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-05-14

### Changed

- Renamed the "Overwrite Only If Needed" mode to **"Smart Overwrite"** for clarity.
  - This mode corrects scales in both directions (expand and shrink) while protecting values that the user has manually set beyond the calculated target.

### Fixed

- Fixed a bug in the overwrite condition logic (`ShouldOverwrite`) where objects with a calculated target scale of exactly `1.0` were skipped even in "Always Overwrite" mode.
- Fixed the protection logic for the `target == 1.0` case. Since `1.0` has no directional bias, any manually set value is now correctly preserved (except in "Always Overwrite" mode).

---

## [1.0.0] - 2026-05-14

### Added

- Initial release of the Lightmap Scaler Tool.
- Implemented **Manual Batch Setting** to bulk-change lightmap scales with name filters.
- Implemented **Auto UV Scale Correction** to automatically calculate and apply optimal scales based on mesh surface area.
- Distributed as separate standalone scripts for English (`LightmapScalerTool_EN.cs`) and Japanese (`LightmapScalerTool_JP.cs`).
