# layout-presets Specification

## Purpose
TBD - created by archiving change add-screen-region-overlay. Update Purpose after archive.
## Requirements
### Requirement: Manage named layouts
The application SHALL let users create, rename, switch, and delete named layouts. A layout SHALL store its bar position and the sources, positions, order, and appearance of all tiles.

#### Scenario: Switch between game layouts
- **WHEN** the user leaves one layout and selects a different layout
- **THEN** the bar SHALL display the newly selected layout with its saved settings

### Requirement: Restore the last active layout
The application SHALL save layout metadata locally and restore the last active layout at startup. If the bar position is off-screen, it SHALL be moved into a visible work area.

#### Scenario: Restart after personalizing the HUD
- **WHEN** the user reopens the application after editing a layout
- **THEN** the application SHALL reopen that layout with its saved sources, order, position, scale, opacity, and contrast

