# Problem: auth-failed

Problem type URL:

- `https://docs.durablestack.com/problems/auth-failed`

Returned when API authentication cannot be completed.

## Status

- `401 Unauthorized`

## Codes

- `missing_headers`
- `invalid_headers`
- `invalid_credentials`

## Notes

- `missing_headers` includes `requiredHeaders` in the response extensions.
- `correlationId` is always included for support diagnostics.
