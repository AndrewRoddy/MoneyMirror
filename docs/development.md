# Development

Stack: ASP.NET Core / Blazor (C#), EF Core + PostgreSQL, NVIDIA Nemotron,
a multimodal vision model, and external market / BLS labor data. Single modular
monolith, one database, one implicit user — no auth or multi-tenancy.

## Configuration

AI, BLS, and eBay Browse API settings live under `Ai:Nemotron`,
`Ai:VisionModel`, `Ai:Claude`, `Bls`, and `Ebay` (`ClientId`, `ClientSecret`,
`AuthUrl`, `SearchUrl`, `MarketplaceId`, and `CacheDurationHours`).
`appsettings.json` ships empty secret placeholders — never commit real keys.
`Ai:Claude` is a fallback only: `FallbackLlmService` uses it when Nemotron is
unreachable or errors out, so it's optional in dev but required for prod
resilience. `Ai:Claude:WorkspaceId` is only needed for API keys that are not
already scoped to a single workspace. Set them locally:

```sh
dotnet user-secrets set "Ai:Nemotron:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:VisionModel:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:Claude:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:Claude:WorkspaceId" "<your-workspace-id>"
dotnet user-secrets set "Bls:ApiKey" "<your-key>"
dotnet user-secrets set "Ebay:ClientId" "<your-ebay-client-id>"
dotnet user-secrets set "Ebay:ClientSecret" "<your-ebay-client-secret>"
```

Or use env vars: `Ai__Nemotron__ApiKey`, `Ai__VisionModel__ApiKey`,
`Ai__Claude__ApiKey`, `Ai__Claude__WorkspaceId`, `Bls__ApiKey`,
`Ebay__ClientId`, `Ebay__ClientSecret`.

Physical asset valuations authenticate to eBay's Browse API with an
OAuth2 client-credentials app token (cached in memory for its ~2-hour
lifetime), then search for up to 50 listings filtered to used/refurbished
condition and calculate a median from USD-priced results. eBay's own
condition filter does not reliably exclude new items, so results are
re-checked client-side against each listing's `conditionId`. No usable
listings produce a null value and an explicit low-confidence explanation.
If eBay itself fails - missing credentials, network error, rate limit -
the valuation falls back to an LLM price guess (`AiEstimatedValuationService`)
rather than failing the request outright. The valuation returns each
comparable's price, source, title, and condition in `AssetValuation.Evidence`;
that evidence is persisted and displayed alongside each valuation in
inventory history. Successful results, including empty results, are cached
for 30 days in `App_Data/ebay-search-cache`, which is on the persistent
Docker app-data volume. Repeated or concurrent searches for the same item
reuse that result.

The default `AuthUrl`/`SearchUrl` point at eBay's **sandbox** environment,
whose inventory is seeded test data - it answers real requests but rarely
has comps for a real product name. Swap both URLs to the production hosts
(`api.ebay.com` instead of `api.sandbox.ebay.com`) once the app has
production-approved keys from the [eBay Developer Program](https://developer.ebay.com/).

## Run with Docker

```sh
Copy-Item .env.example .env
docker compose up --build
```

App: `http://localhost:8000` (change `APP_PORT` in `.env` if needed). More detail
on Postgres volumes and host-local `dotnet run`: [backend/postgresql_setup.md](../backend/postgresql_setup.md).

| Environment variable | .NET key |
| --- | --- |
| `POSTGRES_*` | `ConnectionStrings:DefaultConnection` |
| `NEMOTRON_API_KEY` | `Ai:Nemotron:ApiKey` |
| `VISION_API_KEY` | `Ai:VisionModel:ApiKey` |
| `ANTHROPIC_API_KEY` | `Ai:Claude:ApiKey` |
| `ANTHROPIC_WORKSPACE_ID` | `Ai:Claude:WorkspaceId` |
| `BLS_API_KEY` | `Bls:ApiKey` |
| `EBAY_CLIENT_ID` | `Ebay:ClientId` |
| `EBAY_CLIENT_SECRET` | `Ebay:ClientSecret` |

`docker compose down` stops the stack; add `-v` to wipe DB and image volumes.

## Format and check

```sh
dotnet tool restore
npm ci
npm run format
npm run check
```

CSharpier formats C#; Prettier formats CSS.

## Tests

```sh
dotnet test tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj
npm run test:video
```

Video-scan tests use mocks and cover selection, limits, and cleanup — not live
codec or provider quality. Before release, try a real 10–30s room pan on desktop
and phone.

### PR checks

PRs run **Format** and **Test**. Locally:

```sh
dotnet restore tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj
dotnet format MoneyMirror.csproj --no-restore --verify-no-changes
dotnet format tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj --no-restore --verify-no-changes
dotnet test tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj --no-restore --configuration Release
```
