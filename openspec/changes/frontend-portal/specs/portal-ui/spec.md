## Purpose

Lets a user pick an origin and destination city on a single page, see the fastest routes between them on
an embedded map, and see which carriers move the most trucks on that lane, sourced from the backend's
`carrier-lookup` capability behind its `auth` gate.

## ADDED Requirements

### Requirement: Search is gated on a signed-in user
The system SHALL require the user to be signed in with a Google identity before a lane search can be
submitted, and SHALL NOT send a carrier-lookup request on behalf of a signed-out user.

#### Scenario: Signed-out user cannot search
- **WHEN** the page loads and the user has not signed in
- **THEN** the search control is unavailable (disabled or otherwise blocked from submission) and no
  carrier-lookup request is sent

#### Scenario: Signing in unlocks search
- **WHEN** the user completes Google sign-in
- **THEN** the search control becomes available, provided an origin and destination are also selected

#### Scenario: Credential expires or is rejected mid-session
- **WHEN** a carrier-lookup request is rejected because the user's credential is missing, expired, or
  invalid
- **THEN** the system treats the user as signed out, prompts them to sign in again, and does not display a
  stale carrier list as if it were current

### Requirement: Origin and destination are matched against a map data provider
The system SHALL let the user enter an origin city and a destination city and SHALL resolve each entry to
a real place via a map data provider before treating it as valid input for a search.

#### Scenario: Unresolved city blocks search
- **WHEN** the user has typed into the origin or destination field but not selected a resolved place for
  it
- **THEN** the search control remains unavailable

#### Scenario: Both cities resolved
- **WHEN** the user has selected a resolved place for both origin and destination
- **THEN** the search control becomes available, provided the user is also signed in

### Requirement: Search submits a lane lookup and requests fastest routes
The system SHALL, on search submission with a signed-in user and two resolved cities, request the ranked
carrier list for that origin/destination pair and request routing between the two resolved places.

#### Scenario: Successful search
- **WHEN** the user submits a search with a resolved origin, a resolved destination, and a valid
  credential
- **THEN** the system requests the carrier list for that lane and requests routes between the two places,
  and shows a loading state until both are resolved

### Requirement: Map shows up to the 3 fastest routes
The system SHALL render an embedded map showing up to three alternative routes between the searched
origin and destination, ordered fastest first when route timing is available.

#### Scenario: Multiple routes available
- **WHEN** routing between the two resolved places yields three or more route options
- **THEN** the map displays exactly three routes

#### Scenario: Fewer than three routes available
- **WHEN** routing between the two resolved places yields fewer than three route options
- **THEN** the map displays all available routes without treating the shortfall as an error

#### Scenario: No route available
- **WHEN** no route exists between the two resolved places
- **THEN** the system shows that no route was found and does not render a stale or unrelated map

### Requirement: Carrier list reflects the lookup response as returned
The system SHALL render the carrier list from a successful lookup response in the order the response
provides it, without re-sorting or filtering entries client-side.

#### Scenario: Carriers returned
- **WHEN** a lookup response contains one or more carriers
- **THEN** the system renders each carrier's name and trucks-per-day figure, in the response's order

#### Scenario: No carriers returned
- **WHEN** a lookup response contains an empty carrier list
- **THEN** the system shows an explicit empty-result state rather than a blank list or an error

### Requirement: Lookup failures are shown distinctly and safely
The system SHALL distinguish, in what it shows the user, between a validation failure, an authentication
failure, a rate-limit rejection, and a server/network failure when a carrier-lookup request does not
succeed, and SHALL NOT display raw response bodies from the API as user-facing text.

#### Scenario: Validation failure
- **WHEN** a carrier-lookup request is rejected as invalid
- **THEN** the system shows a message indicating the input needs correction, without exposing the raw
  error payload

#### Scenario: Authentication failure
- **WHEN** a carrier-lookup request is rejected because the credential is missing, expired, or invalid
- **THEN** the system prompts the user to sign in again rather than showing a generic failure

#### Scenario: Rate-limited
- **WHEN** a carrier-lookup request is rejected for exceeding the rate limit
- **THEN** the system shows a message indicating the user should retry shortly, distinct from a validation
  or authentication failure

#### Scenario: Server or network failure
- **WHEN** a carrier-lookup request fails due to a server error or the request cannot reach the backend
  at all
- **THEN** the system shows a generic failure message and does not display a stale or partial carrier
  list as if it succeeded
