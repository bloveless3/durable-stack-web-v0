# Problem: validation-failed

Problem type URL:

- `https://docs.durablestack.com/problems/validation-failed`

Returned when telemetry batch payload validation fails.

## Status

- `400 Bad Request`

## Codes

- `validation_failed`

## Response shape

Validation details are in the `errors` object where keys map to request fields.

Example key:

- `events[0].payloadJson`

## Notes

- `correlationId` is included in response extensions.
