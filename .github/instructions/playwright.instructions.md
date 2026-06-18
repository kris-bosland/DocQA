---
applyTo: "DocQA.Tests.Browser/**"
---

# Playwright Browser Test Guidance

## Host setup
- Prefer a real hosted app fixture that starts the server and browser together
- Serve the published client output from `DocQA.Client` rather than reconstructing assets by hand
- Use the published `wwwroot` as the browser web root so hashed `_framework` assets resolve exactly as built
- If the browser host needs test-only configuration, override `appsettings.json` in the copied web root instead of editing source files

## Static assets
- Keep the published `index.html` intact unless there is a clear, verified reason to rewrite it
- If a framework asset returns a blank response or 404, check the file is present in the publish output before changing test code
- Map `.dat` to `application/octet-stream` in the browser host static-file options so Blazor ICU globalization assets can load
- When adding new asset types, verify the browser network log before assuming the issue is in Playwright

## Troubleshooting
- If the page loads but the UI never appears, inspect console errors and failed network responses first
- A `SyntaxError` or `Unexpected token '<'` usually means an HTML response is being served where JavaScript was expected
- If the runtime fails while downloading `_framework` files, compare the request path with the published `wwwroot` layout
- For 404s on Blazor runtime assets, verify the host is serving the published output root, not the build root
- Confirm the SPA root is reachable by waiting for a visible UI element such as the main table or empty state text

## Test design
- Use `BrowserCollection` to disable parallelization for browser tests that share a single server fixture
- Keep the fixture deterministic: in-memory SQLite, stub Claude service, fixed localhost base URL
- Favor end-to-end user flows over implementation detail assertions
- Keep temporary diagnostics out of committed tests once the host is stable

## CI
- Install Playwright browsers before running browser tests in CI
- Cache the Playwright browser directory when the pipeline supports it
- Run browser tests only after the client and server projects have been built for the same configuration
- If browser tests fail in CI but pass locally, compare the published client output and the browser cache state first
