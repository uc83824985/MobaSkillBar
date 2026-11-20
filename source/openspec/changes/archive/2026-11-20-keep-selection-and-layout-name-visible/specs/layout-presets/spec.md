## MODIFIED Requirements

### Requirement: Manage named layouts
The application SHALL let users create, rename, switch, and delete named layouts. A layout SHALL store its bar position and the sources, positions, order, and appearance of all tiles. The layout selector SHALL display a confirmed name change immediately.

#### Scenario: Switch between game layouts
- **WHEN** the user leaves one layout and selects a different layout
- **THEN** the bar SHALL display the newly selected layout with its saved settings

#### Scenario: Rename the current layout
- **WHEN** the user confirms a new name for the current layout
- **THEN** the selector SHALL show the new name in its current value and list while keeping the same layout selected
