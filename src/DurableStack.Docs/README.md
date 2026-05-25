# DurableStack Docs

This project is the source of truth for docs content that will be served from:

- `https://docs.durablestack.com`

The docs site implementation can evolve later (static site generator, custom app, etc.),
but markdown content should live here now so API behavior and docs stay in sync.

This project now uses DocFX for local build/serve and static site generation.

## Local commands

Run from repository root:

```bash
dotnet tool restore
dotnet docfx src/DurableStack.Docs/docfx.json
dotnet docfx src/DurableStack.Docs/docfx.json --serve
```

- Build output is generated in `src/DurableStack.Docs/_site/`.
- Default local site URL is shown by DocFX when using `--serve`.

## Content layout

- `docfx.json` - DocFX build configuration
- `content/api/` - API endpoint docs and integration examples
- `content/problems/` - Problem type pages referenced in API `ProblemDetails.type`
- `content/toc.yml` - top-level navigation

## Authoring guidance

- Keep docs version-aware when behavior changes.
- Prefer concrete request/response examples.
- Keep problem pages aligned with error `code` values used by the API.
