# API versioning notes

Reference material for the `dotnet-api-review` skill.

## What counts as a breaking change

| Change | Breaking |
|---|---|
| Adding an optional response field | No |
| Removing a response field | Yes |
| Making an optional request field required | Yes |
| Widening an accepted type | No |
| Narrowing an accepted type | Yes |
| Changing a success status code | Yes |
| Adding a new enum value the client must handle | Usually |

## Questions to ask about a versioned API

1. How does a client select a version — URL segment, header, or query string?
2. Is the previous version still served, and for how long?
3. Are contract types shared between versions? Shared types make an "isolated" change global.
4. Is there a test that pins the previous version's contract?
