## Purpose

Defines the observable navigation structure, the getting-started wizard on
the home page, and the room-centric setup surface (anchor placement map,
inline chip registration, per-room session list) that an operator relies on
to configure the webapp end to end.

## ADDED Requirements

### Requirement: Operator navigation is limited to three primary items plus a debug group

The top navigation bar SHALL expose exactly three operator-facing links —
`Sessions`, `Rooms`, and `Chips` — as primary items, and SHALL group
`Trial`, `Raw measurements`, and `Position results` under a single
right-aligned `Debug` dropdown. No other top-level nav items SHALL appear.
The brand link ("Positioning") SHALL continue to point at the home page.

#### Scenario: Primary nav shows three operator items

- **WHEN** any authenticated page is rendered
- **THEN** the navbar displays `Sessions`, `Rooms`, and `Chips` as primary
  links, in that order
- **AND** it does not display standalone links for `Live`, `Room layouts`,
  `Layout chips`, `Trial`, `Raw measurements`, or `Position results`

#### Scenario: Debug items live under a dropdown

- **WHEN** the navbar is rendered
- **THEN** a `Debug` dropdown is available on the right side of the navbar
- **AND** opening it reveals links to `Trial`, `Raw measurements`, and
  `Position results` (global list)
- **AND** each of those links resolves to its existing route unchanged

### Requirement: Home page is a data-aware getting-started wizard

The home page SHALL present a getting-started checklist whose steps reflect
the current state of the database: (1) at least one chip registered,
(2) at least one room created, (3) at least one anchor placed with X and Y
coordinates, (4) at least one session started. Each completed step SHALL be
marked as done. When all four steps are complete the wizard SHALL be
collapsed by default and the home page SHALL surface an operational summary
(most recent active session, or a call to action to start one). A
"Show setup steps" affordance SHALL remain available to re-expand the
wizard.

#### Scenario: Fresh install shows the full wizard

- **WHEN** no chips, rooms, anchors-with-coordinates, or sessions exist
- **THEN** the home page displays all four steps unchecked
- **AND** step 1 links to the chip registration page
- **AND** step 2 links to the room creation page

#### Scenario: Partial progress is reflected

- **WHEN** at least one chip and at least one room exist, but no anchor has
  X and Y coordinates and no session has been started
- **THEN** steps 1 and 2 are shown as complete and steps 3 and 4 as
  incomplete
- **AND** the next actionable step is visually emphasised

#### Scenario: Fully-configured install collapses the wizard

- **WHEN** at least one chip, one room, one anchor with coordinates, and
  one session all exist
- **THEN** the wizard is collapsed by default
- **AND** the home page surfaces the most recent active session (if any) or
  a call to action to start a new session
- **AND** a control is present that re-expands the wizard on demand

### Requirement: Room detail page is the anchor placement cockpit

The room detail page (`SessionConfigs/Details/{id}`) SHALL render the
interactive positioning map — the same drag-to-place map that the live
session page uses — showing the room's floor plan (if uploaded), all
anchors placed on it, and the world coordinate grid. Operators SHALL be
able to drag any anchor to reposition it, with the new coordinates
persisted server-side without a page reload. Tags belonging to the room
SHALL be listed but not placed on the map, because tags are positioned
live.

#### Scenario: Room detail shows the placement map

- **WHEN** the operator opens a room detail page
- **THEN** an interactive map is rendered
- **AND** every `SessionConfigChip` with `Role = Anchor` and non-null X/Y
  is shown as a draggable marker at its world coordinates
- **AND** if the room has a floor plan configured it is rendered as a
  background overlay at its configured origin, scale, rotation, and
  opacity

#### Scenario: Dragging an anchor persists new coordinates

- **WHEN** the operator drags an anchor marker to a new position on the
  room detail map
- **THEN** the new X/Y coordinates are persisted server-side
- **AND** on a subsequent page load the anchor renders at the new
  coordinates
- **AND** no page reload is required for the drag to take effect

#### Scenario: Live view continues to use the same map behavior

- **WHEN** the operator opens the live view of a session
- **THEN** the same map component (with the same drag, pan, zoom, and
  persist behavior) is rendered
- **AND** in addition the live view displays tag position updates, the
  results table, the routing-selected anchor highlight, and the
  activate/deactivate control

### Requirement: Chips can be added to a room from the room page

The room detail page SHALL provide an inline flow to add either an anchor
or a tag to the room. The flow SHALL allow selecting an existing chip
from the device pool or registering a brand-new chip (name + device
identifier) in the same interaction. On submission the flow SHALL create a
`SessionConfigChip` record with the chosen role, optionally with initial
coordinates for anchors, and the resulting anchor or tag SHALL appear on
the room's map or tag list without navigating away.

#### Scenario: Add an anchor using an existing unassigned chip

- **WHEN** the operator opens the "Add anchor" flow on a room page and
  selects an existing chip that is not yet in this room
- **THEN** a `SessionConfigChip` row is created linking that chip to this
  room with `Role = Anchor`
- **AND** the new anchor appears on the map at its supplied coordinates
  (or without a position if none was supplied)

#### Scenario: Register a new chip while adding an anchor

- **WHEN** the operator supplies a new chip name and device identifier
  inside the "Add anchor" flow and submits
- **THEN** a new `Chip` is created
- **AND** a `SessionConfigChip` row is created linking the new chip to
  this room with `Role = Anchor`
- **AND** both records are visible without navigating to the chips page

#### Scenario: Tags are added without fixed coordinates

- **WHEN** the operator uses the "Add tag" flow
- **THEN** the coordinate inputs are hidden
- **AND** submitting creates a `SessionConfigChip` with `Role = Tag` and
  no X/Y/Z coordinates

### Requirement: Room detail page lists the room's sessions

The room detail page SHALL list the sessions that were run against that
room, most recent first, with links to open the live view (for active
sessions) or view results (for finished ones). It SHALL also provide a
control to start a new session against that room.

#### Scenario: Room shows its session history

- **WHEN** a room has one active and one finished session
- **THEN** the room detail page lists both, most recent first
- **AND** the active session offers a link to its live view
- **AND** the finished session offers a link to its results

#### Scenario: Room offers a start-session action

- **WHEN** the operator is on a room detail page
- **THEN** a control to start a new session on this room is present
- **AND** activating it results in a new session bound to this room

### Requirement: Chips index shows current room placements

The chips index page SHALL display, for each chip, every room the chip is
currently a member of, together with the chip's role in each of those
rooms. Chips that are not a member of any room SHALL be identifiable as
unassigned. The many-to-many relationship between chips and rooms SHALL
be preserved: a single chip MAY belong to more than one room at the same
time and each membership MAY have its own role.

#### Scenario: Placed chip lists every room membership

- **WHEN** a chip is a member of two rooms, once as anchor and once as
  tag
- **THEN** the chips index shows both memberships for that chip with
  their respective roles

#### Scenario: Unassigned chip is identifiable

- **WHEN** a chip has no `SessionConfigChip` rows
- **THEN** the chips index marks it as unassigned

### Requirement: The Layout chips top-level page is retired from the operator surface

The nav SHALL NOT expose `Layout chips` as a top-level item, and the
`/SessionConfigChips` index route SHALL redirect operators to the rooms
index. The individual Create, Edit, Delete, and Details actions on
`SessionConfigChip` SHALL remain reachable — they are invoked by the
room-detail dialogs and by direct navigation — but they SHALL NOT be the
primary entry point for placing chips into a room.

#### Scenario: /SessionConfigChips redirects to rooms

- **WHEN** an operator navigates directly to `/SessionConfigChips` or
  `/SessionConfigChips/Index`
- **THEN** they are redirected to `/SessionConfigs` (the rooms index)

#### Scenario: Per-row actions on SessionConfigChip remain reachable

- **WHEN** an operator or the room-detail dialog invokes
  `/SessionConfigChips/Create`, `/SessionConfigChips/Edit/{id}`,
  `/SessionConfigChips/Delete/{id}`, or `/SessionConfigChips/Details/{id}`
- **THEN** the request succeeds and behaves as it does today

### Requirement: Existing routes and APIs continue to resolve

No route removals, controller removals, or API removals SHALL be part of
this change. All URLs that resolved before this change SHALL continue to
resolve, including the debug pages under their existing paths, so that
external bookmarks, scripts, and the ingestion pipeline remain
functional.

#### Scenario: Debug routes still work

- **WHEN** any of `/RawMeasurements`, `/PositionResults`, or `/Home/Trial`
  is requested
- **THEN** the page renders exactly as it did before this change

#### Scenario: Anchor drag API is unchanged

- **WHEN** any client (live view, room detail map, or an external caller)
  sends `PATCH /api/SessionConfigChips/{id}/coords` with a JSON body
  `{ "x": <number>, "y": <number> }`
- **THEN** the endpoint updates the anchor coordinates as it does today
- **AND** the response contract is unchanged
