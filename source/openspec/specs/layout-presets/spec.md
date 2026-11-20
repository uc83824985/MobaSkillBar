# layout-presets Specification

## Purpose
TBD - created by archiving change add-screen-region-overlay. Update Purpose after archive.
## Requirements
### Requirement: Manage named layouts
The application SHALL let users create, rename, switch, and delete named layouts. A layout SHALL store its bar position and the sources, positions, order, and appearance of all tiles. The layout selector SHALL display a confirmed name change immediately.

#### Scenario: Switch between game layouts
- **WHEN** the user leaves one layout and selects a different layout
- **THEN** the bar SHALL display the newly selected layout with its saved settings

#### Scenario: Rename the current layout
- **WHEN** the user confirms a new name for the current layout
- **THEN** the selector SHALL show the new name in its current value and list while keeping the same layout selected

### Requirement: Restore the last active layout
The application SHALL save layout metadata in `config/layouts.json` under the repository root for source builds or beside the executable for standalone distributions and restore the last active layout at startup. If the bar position is off-screen, it SHALL be moved into a visible work area.

#### Scenario: Restart after personalizing the HUD
- **WHEN** the user reopens the application after editing a layout
- **THEN** the application SHALL reopen that layout with its saved tile names, sources, order, position, scale, opacity, contrast, and saturation

