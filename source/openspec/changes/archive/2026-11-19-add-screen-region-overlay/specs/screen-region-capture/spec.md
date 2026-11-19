## ADDED Requirements

### Requirement: Live screen region selection
The application SHALL let users select or replace the source rectangle of a tile within one display. Selection SHALL use physical screen pixels and cancellation SHALL preserve existing configuration.

#### Scenario: Select two skill slots
- **WHEN** the user selects two bottom HUD rectangles as separate tiles
- **THEN** each tile SHALL refresh from its own rectangle, including when the rectangles are on different displays

#### Scenario: Cancel replacing a source
- **WHEN** the user presses Escape during source selection
- **THEN** the application SHALL retain the previous source rectangle

### Requirement: Capture interruption and feedback
The application SHALL preserve configured regions during capture interruptions and SHALL prevent recursive overlay imagery.

#### Scenario: Source display reconnects
- **WHEN** a source display goes away and later returns with a valid source rectangle
- **THEN** the affected tile SHALL show an unavailable state and subsequently resume capture

#### Scenario: Overlay overlaps a source
- **WHEN** the floating bar overlaps a configured source
- **THEN** capture SHALL exclude the floating window or suspend the affected tile with an explanation if exclusion is unavailable
