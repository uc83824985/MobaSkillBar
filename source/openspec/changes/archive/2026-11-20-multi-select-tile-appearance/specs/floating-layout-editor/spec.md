## ADDED Requirements

### Requirement: Multi-tile appearance editing
The application SHALL keep appearance controls visible for multiple selected tiles. Each property SHALL display the shared percentage or a mixed-value indicator. The application SHALL apply each edited property to all selected tiles and preserve the other properties.

#### Scenario: Inspect and edit selected tiles with mixed values
- **WHEN** tiles with the same scale and different contrast are selected
- **THEN** the details SHALL show the shared scale and a mixed indicator for contrast
- **WHEN** the user edits or resets contrast
- **THEN** the assigned contrast SHALL apply to all selected tiles and the shared contrast SHALL appear in the details

#### Scenario: Clear selection by clicking empty space
- **WHEN** the user clicks empty space in the tile list or in the floating editor
- **THEN** the selections in both views SHALL clear
