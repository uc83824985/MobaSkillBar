# screen-region-capture Specification

## Purpose
TBD - created by archiving change add-screen-region-overlay. Update Purpose after archive.
## Requirements
### Requirement: Live screen region selection
The application SHALL let users select or replace the source rectangle of a tile within one display. Selection SHALL use physical screen pixels and cancellation SHALL preserve existing configuration. During selection the application SHALL magnify pixels around the cursor, show the selection border in that view, and support single-pixel cursor movement using WASD or the arrow keys.

#### Scenario: Select two skill slots
- **WHEN** the user selects two bottom HUD rectangles as separate tiles
- **THEN** each tile SHALL refresh from its own rectangle, including when the rectangles are on different displays

#### Scenario: Cancel replacing a source
- **WHEN** the user presses Escape or right-clicks during source selection
- **THEN** the application SHALL retain the previous source rectangle

#### Scenario: Align a selection to a pixel edge
- **WHEN** the user presses W, A, S, D, or the corresponding arrow key before or during a drag
- **THEN** the cursor SHALL move one physical pixel up, left, down, or right within the display, and the magnifier and selection SHALL follow the cursor

### Requirement: Capture interruption and feedback
The application SHALL preserve configured regions during capture interruptions and SHALL prevent recursive overlay imagery.

#### Scenario: Source display reconnects
- **WHEN** a source display goes away and later returns with a valid source rectangle
- **THEN** the affected tile SHALL show an unavailable state and subsequently resume capture

#### Scenario: Overlay overlaps a source
- **WHEN** the floating bar overlaps a configured source
- **THEN** capture SHALL exclude the floating window or suspend the affected tile with an explanation if exclusion is unavailable

### Requirement: Batched preview readback
The application SHALL share a captured frame across regions on the same display. Nearby regions SHALL share a bounded rectangular readback, with each tile mapped to its own rectangle. The preview SHALL target up to 60 updates per second and consume completed readbacks without waiting for the GPU.

#### Scenario: Preview adjacent skill slots
- **WHEN** four to six nearby skill slots are captured on a single display
- **THEN** the application SHALL read their shared bounding box once per frame and display each tile's own pixel rectangle from that frame

#### Scenario: Preview widely separated regions
- **WHEN** combining regions would cause excessive readback of intervening pixels
- **THEN** the application SHALL use separate bounding boxes and preserve the mapping of each tile

