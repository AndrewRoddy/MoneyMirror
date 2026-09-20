# Development

Stack: ASP.NET Core / Blazor (C#), EF Core + PostgreSQL, NVIDIA Nemotron,
a multimodal vision model, and external market / BLS labor data. Single modular
monolith, one database, one implicit user — no auth or multi-tenancy.

## Configuration

AI, BLS, and eBay Browse API settings live under `Ai:Nemotron`,
`Ai:VisionModel`, `Bls`, and `Ebay` (`ClientId`, `ClientSecret`, `BaseUrl`, and
`MarketplaceId`). `appsettings.json` ships empty secret placeholders — never
commit real keys. Set them locally:

```sh
dotnet user-secrets set "Ai:Nemotron:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:VisionModel:ApiKey" "<your-key>"
dotnet user-secrets set "Bls:ApiKey" "<your-key>"
dotnet user-secrets set "Ebay:ClientId" "<your-app-id>"
dotnet user-secrets set "Ebay:ClientSecret" "<your-app-secret>"
```

Or use env vars: `Ai__Nemotron__ApiKey`, `Ai__VisionModel__ApiKey`,
`Bls__ApiKey`, `Ebay__ClientId`, `Ebay__ClientSecret`.

Physical asset valuations query up to 20 used eBay US listings and calculate a
median from usable USD prices. No usable listings produce a null value and an
explicit low-confidence explanation. The valuation returns each comparable's
price, source, title, and condition in `AssetValuation.Evidence`; that evidence
is persisted and displayed alongside each valuation in inventory history.

A free BLS v2 key from [data.bls.gov/registrationEngine](https://data.bls.gov/registrationEngine/)
raises rate limits; the app works without one at the unregistered limit.

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
