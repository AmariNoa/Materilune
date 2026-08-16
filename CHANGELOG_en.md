# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.1.0-beta.3] - 2026-08-17

### Added

- Batch swap. One replacement serves as the example: the name pattern between its two materials is detected and applied to the panel's other materials at once. Every row is shown for approval first, rows that already hold a replacement start excluded, and applying is a single undo step.
- The material candidate popup gained an outline.

### Changed

- The UI stylesheets are now referenced by the UXML documents themselves (no visual change).

## [0.1.0-beta.2] - 2026-08-16

### Added

- A nested setup now shows what the setup enclosing it already replaces. A row with no replacement of its own shows the inherited one over its empty field, and a row that has both names the inherited one in its tooltip. The display is read-only.
- The window reports when the sibling order stops a nested setup from taking effect, and offers a button that puts the order right.

### Fixed

- A setup nested inside another one no longer loses to the outer one. Modular Avatar keeps the component it reaches last, so the Materilune object is moved to the front of its siblings. Where a prefab refuses the move, it goes as far as it can and says so; the prefab is never unpacked to force it.

## [0.1.0-beta.1] - 2026-08-16

The first public release.

### Added

- Setup command. It builds the Materilune hierarchy under the target object and places Modular Avatar's Material Swap.
- Presets. Several sets of replacements can be kept, with only one of them active at a time.
- Automatic swap entries. Entries are generated from the materials the target meshes use. The source material is read-only, and only entries with a replacement are written to Modular Avatar.
- The Materilune window, which edits the preset list, the whole-preset replacements, the target mesh tree and the selected mesh's replacements on one screen.
- A material candidate picker. Candidates are grouped into tabs by mode and shown with a preview.
- A window launch button drawn on the Hierarchy row. Its position is agreed with the buttons of other tools on the same row through a registry shared within the editor session.
- Component inspectors showing a summary of the settings and a button that opens the window, plus a warning on Renderers that Materilune manages.
- Japanese and English display languages.
- A preference for extra spacing beside the Hierarchy buttons, under `Preferences > AmariNoa > Hierarchy Buttons`.

### Known limitations

- The note stating that the language setting is shared across the whole Unity editor and every package is not implemented.
- The lock toggle that pins the Materilune window to one target object is not implemented.
- The VPM listing URL has not been settled.
- The public API has not been verified against Modular Avatar 1.13.x. Testing was done on 1.17.1.
