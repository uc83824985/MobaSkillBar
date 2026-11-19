# floating-layout-editor Specification

## Purpose
TBD - created by archiving change add-screen-region-overlay. Update Purpose after archive.
## Requirements
### Requirement: Floating display and edit modes
The application SHALL provide a transparent topmost skill bar and a separate control window. Display mode SHALL pass mouse input through the bar. Edit mode SHALL let users move the bar, reorder or position tiles, rename tiles, and remove tiles. Mode changes SHALL preserve the on-screen position and size of each tile.

#### Scenario: Move the HUD near the center of the screen
- **WHEN** the user moves the bar and arranges tiles in edit mode, then enters display mode
- **THEN** the bar SHALL show each tile at its edited screen rectangle and SHALL allow mouse clicks to reach the application underneath

#### Scenario: Return to edit mode
- **WHEN** the user enters edit mode from display mode
- **THEN** the application SHALL show the editing controls without moving or resizing any tile

### Requirement: Per-tile appearance
The application SHALL expose independent scale, opacity, contrast, and saturation controls for each tile. Each control SHALL accept typed percentages and offer a per-property reset. Zero saturation SHALL produce grayscale. Scaling SHALL preserve the source aspect ratio and map source pixels one to one at 100% on the destination display.

#### Scenario: Reduce one tile's opacity and increase contrast
- **WHEN** the user changes a selected tile's opacity and contrast
- **THEN** the live tile SHALL reflect those adjustments while the other tiles retain their settings

### Requirement: Direct tile selection and resizing
Edit mode SHALL allow direct selection by clicking a tile. The selected tile SHALL have a visible bounding outline with corner and side handles. Dragging the tile body SHALL move it and dragging a handle SHALL resize it proportionally. Changes SHALL be reflected in the control window and saved in the active layout.

#### Scenario: Select and move multiple tiles
- **WHEN** the user drags from empty bar space or holds the left button on a tile before dragging
- **THEN** the editor SHALL select the tiles intersecting the marquee and show their selection in the bar and control window
- **WHEN** the user drags any selected tile
- **THEN** the selected tiles SHALL move by the same offset, preserving relative positions at the canvas boundaries

#### Scenario: Move and scale a selected tile
- **WHEN** the user clicks a tile, drags its body, then drags one of its bounding box handles
- **THEN** the application SHALL move and scale that tile while preserving its aspect ratio and leaving the other tiles at their existing positions and sizes

