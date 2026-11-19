## ADDED Requirements

### Requirement: Floating display and edit modes
The application SHALL provide a transparent topmost skill bar and a separate control window. Display mode SHALL pass mouse input through the bar. Edit mode SHALL let users move the bar, reorder or position tiles, and remove tiles.

#### Scenario: Move the HUD near the center of the screen
- **WHEN** the user moves the bar and arranges tiles in edit mode, then enters display mode
- **THEN** the bar SHALL show the arranged tiles at the chosen location and SHALL allow mouse clicks to reach the application underneath

### Requirement: Per-tile appearance
The application SHALL expose independent scale, opacity, and contrast controls for each tile. Scaling SHALL preserve the source aspect ratio.

#### Scenario: Reduce one tile's opacity and increase contrast
- **WHEN** the user changes a selected tile's opacity and contrast
- **THEN** the live tile SHALL reflect those adjustments while the other tiles retain their settings
