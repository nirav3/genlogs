## Purpose

Lets a user prove their identity via Google Sign-In and use that proof directly as the credential that
gates access to the rest of the API, without the system persisting any user record or minting its own
credential.

## Requirements

### Requirement: Protected endpoints require a valid Google ID token
The system SHALL require every request to an endpoint that needs authentication to carry a Google-issued
ID token as a bearer credential, and SHALL verify that token's signature, audience (this application's
OAuth client ID), and expiry before granting access.

#### Scenario: Valid Google ID token
- **WHEN** a request carries a Google ID token that is correctly signed, unexpired, and issued for this
  application's OAuth client ID
- **THEN** the system grants access to the requested endpoint

#### Scenario: Token fails verification
- **WHEN** a request carries an ID token that is expired, malformed, has an invalid signature, or is
  issued for a different OAuth client ID
- **THEN** the system rejects the request with an authentication error and does not grant access

#### Scenario: Missing token
- **WHEN** a request to a protected endpoint carries no bearer token at all
- **THEN** the system rejects the request with an authentication error and does not grant access

### Requirement: No user data is persisted
The system SHALL NOT persist any user profile, token, or session record to disk or a database as part of
verifying a user's identity.

#### Scenario: Repeated sign-in produces no stored record
- **WHEN** the same user authenticates multiple times (each time obtaining a fresh Google ID token)
- **THEN** the system verifies each request independently without creating or updating any stored user
  record
