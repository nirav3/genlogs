## Purpose

Lets the portal's frontend find out which carriers move the most trucks between an origin and
destination city, computed from seeded lane/detection data that mocks a real carrier-tracking data
warehouse rather than a hardcoded lookup table.

## Requirements

### Requirement: Lookup carriers by origin/destination
The system SHALL provide an endpoint that accepts an origin city and a destination city and returns
the list of carriers serving that lane, each with a name and trucks-per-day figure, ordered by
trucks-per-day descending.

#### Scenario: Known lane — New York City to Washington, DC
- **WHEN** the request's origin matches "New York City" (or "NYC") and destination matches
  "Washington, DC" (or "Washington DC")
- **THEN** the system returns Knight-Swift Transport Services (10), J.B. Hunt Transport Services Inc (7),
  and YRC Worldwide (5), in that order

#### Scenario: Known lane — San Francisco to Los Angeles
- **WHEN** the request's origin matches "San Francisco" and destination matches "Los Angeles"
- **THEN** the system returns XPO Logistics (9), Schneider (6), and Landstar Systems (2), in that order

#### Scenario: Unmatched lane falls back to the default carrier list
- **WHEN** the request's origin/destination pair does not match a known lane (in either direction)
- **THEN** the system returns UPS Inc. (11) and FedEx Corp (9), in that order

#### Scenario: City name matching is case- and whitespace-insensitive
- **WHEN** the origin or destination differs from a known lane's city name only by letter case,
  surrounding whitespace, or a trailing state/country qualifier (e.g. "new york city", " NYC ")
- **THEN** the system still matches the known lane rather than falling back to the default list

### Requirement: Trucks-per-day is a computed rolling average, not a stored constant
The system SHALL derive each carrier's trucks-per-day figure by counting distinct vehicles detected per
day over a recent lookback window and averaging across that window, rather than returning a fixed
constant value.

#### Scenario: Deterministic result for unchanged underlying data
- **WHEN** the same lane is looked up more than once without the underlying detection data changing
- **THEN** the system returns the same trucks-per-day figures each time

### Requirement: Reject invalid lookup requests
The system SHALL validate that both origin and destination are present and are non-empty strings
before performing a lookup, and SHALL reject requests that fail validation without performing a lookup.

#### Scenario: Missing origin or destination
- **WHEN** a lookup request omits `origin`, omits `destination`, or sends an empty/whitespace-only
  value for either field
- **THEN** the system responds with an error indicating which field is invalid and does not return a
  carrier list

#### Scenario: Malformed field types
- **WHEN** `origin` or `destination` is not a string (e.g. a number, object, or array)
- **THEN** the system responds with a validation error and does not return a carrier list

### Requirement: Lookup requires a valid bearer credential
The system SHALL require a valid Google ID token (see the `auth` capability) as a bearer credential on
the lookup endpoint and SHALL reject unauthenticated or invalid-credential requests before performing a
lookup.

#### Scenario: Request without a bearer credential
- **WHEN** a lookup request is made with no bearer credential attached
- **THEN** the system responds with an authentication error and does not return a carrier list

#### Scenario: Request with an expired or invalid bearer credential
- **WHEN** a lookup request carries a bearer credential that is malformed, expired, or fails signature
  verification
- **THEN** the system responds with an authentication error and does not return a carrier list
