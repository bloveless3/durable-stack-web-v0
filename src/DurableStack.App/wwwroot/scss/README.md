# SCSS Workflow

`app.scss` is the source of truth for app styling.

Compile it into `wwwroot/scss/app.css` during development/build.

Tailwind scripts (from `src/DurableStack.App`):

```bash
npm run tailwind:watch
npm run tailwind:build
```

Notes:

- Tailwind scans Razor and JS files via `tailwind.config.js`.
- Keep layout styles and component classes in `app.scss` under `@layer components`.
- Keep page-specific styles in additional SCSS files and include the compiled CSS from each page using `@section HeadStyles` in the Razor view.

Visual Studio wiring:

- `compilerconfig.json` is included so your VS SCSS compiler can watch and compile `app.scss`.
- `DurableStack.App.csproj` also runs `npm run tailwind:build` before each build by default (`RunTailwindOnBuild=true`) so F5/startup profiles stay consistent.
- If you only want the VS compiler path on your machine, set `RunTailwindOnBuild=false` in a local project override.
