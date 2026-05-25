# Problem: payload-too-large

Problem type URL:

- `https://docs.durablestack.com/problems/payload-too-large`

Returned when request body size exceeds allowed limits.

## Status

- `413 Payload Too Large`

## Notes

- Current ingest request limit is `1048576` bytes (1 MB).
- `correlationId` is included in response extensions.
