# Money Mirror

Money Mirror is a personal net-worth and "personal capital" application. It goes beyond a
conventional net-worth tracker by also inventorying the things you own and the market value
of your skills and experience, so you can see a fuller picture of what you're actually worth.

## What it does

**Conventional financial net worth**
Manually track bank accounts, savings, investments, and other financial assets alongside
loans, credit, and other liabilities.

**Physical asset inventory and valuation**
Photograph your possessions (electronics, instruments, furniture, tools, collectibles,
vehicles, etc.) and the app:
- uses a vision model to detect the objects in the photo and identify the likely product/model
- lets you confirm or correct the identification
- pulls real market/used-market comparables and estimates current resale value from that
  evidence (not an invented number)
- adds the item to your physical inventory, tracked with its valuation and valuation date,
  and rolls it into your net worth

**Human capital / market potential**
Upload a resume and the app extracts a structured professional profile (education,
certifications, skills, experience, projects) using NVIDIA Nemotron, matches it against
real labor-market data, and produces a grounded estimated compensation range, relevant
occupations, and career-growth opportunities. This is shown separately as **Market
Potential** — it is never added into net worth as a dollar figure.

**Net worth formula**

```
financial assets + physical assets - liabilities = estimated net worth
```

Market Potential is displayed alongside net worth, not folded into it.

## Stack

- ASP.NET Core / Blazor Web App (C#)
- Entity Framework Core + PostgreSQL
- NVIDIA Nemotron for extraction, reasoning, and explanation
- A multimodal vision model for physical asset recognition
- External market/search data for asset valuation and labor-market data for compensation
  estimates

Single modular monolith, single database, single implicit user/profile — no accounts,
auth, or multi-tenancy.

## Short-video possession scans

On **Physical Assets**, choose or record a video, then select **Find items in video**.
The initial implementation accepts clips up to **30 seconds and 100 MiB** in MP4,
WebM, MOV, or M4V containers. Codec support depends on the browser; try an H.264
MP4 if a phone recording cannot be decoded. Camera capture is a browser-dependent
file-picker hint, not a built-in recording interface.

- The browser samples up to six evenly spaced JPEG frames (maximum 1280 pixels
  on the longest edge). The full video never leaves the device; these stills are
  sent to the configured vision provider. Detection time/cost can be up to six
  image requests per scan, processed sequentially.
- Review the sampled frames by timestamp and click an item's **bounding box**
  or checkbox. These are approximate rectangles, not pixel-level segmentation
  masks. Items without reliable coordinates remain selectable with an explicit
  whole-frame image warning.
- Nearby detections with matching labels/identification in adjacent frames share
  a selection. This is a conservative overlap heuristic, not reliable object
  tracking: camera movement can produce duplicates, and identical objects can be
  confused. Select repeats only once; use **This is a different item** to split
  an incorrect association. At most 20 detections per frame enter review.
- **Review selected items** uploads only the selected crops (or the warned-about
  full-frame fallback). Review identification, edit brand/model, estimate value,
  and explicitly save or merge using the existing inventory workflow. Reviewing
  alone does not create inventory records. Current valuation remains an AI
  estimate, not an evidence-based market appraisal.

Selected images use the existing possession-image store and can remain there if
review is abandoned; this feature does not introduce automatic image retention
cleanup. Raw video/frame object URLs are released on replacement or navigation.
No new model, database migration, video service, or server-side FFmpeg is required.

### Video scan verification

Run the component/repository tests and browser-module logic tests:

```sh
dotnet test tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj
npm run test:video
```

The automated tests use synthetic detections, simulated browser/canvas APIs, and
SQLite. They verify selection, separate image references, explicit persistence,
sampling/cropping arithmetic, limits, cancellation, retry, and URL cleanup. They
do **not** establish actual browser codec support, visual box alignment, or live
provider detection quality. Before release, check a real 10–30 second room pan
on desktop and phone: switch frames, select boxes/checkboxes, split a repeated
item, confirm its crop, save it, and open the inventory detail. Also try an
overlong clip, an unsupported codec, cancellation, and replacing the clip.

## Configuration

The app reads AI provider settings from the `Ai:Nemotron` and `Ai:VisionModel`
configuration sections (`BaseUrl`, `Model`, `ApiKey`), and BLS wage-data settings
from the `Bls` section (`BaseUrl`, `ApiKey`). `appsettings.json` ships with empty
`ApiKey` placeholders — never commit real keys there. Set them locally with .NET
user-secrets instead:

```
dotnet user-secrets set "Ai:Nemotron:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:VisionModel:ApiKey" "<your-key>"
dotnet user-secrets set "Bls:ApiKey" "<your-key>"
```

or via environment variables (`Ai__Nemotron__ApiKey`, `Ai__VisionModel__ApiKey`,
`Bls__ApiKey`). A BLS v2 API key is free to register for at
[data.bls.gov/registrationEngine](https://data.bls.gov/registrationEngine/) and
raises BLS's rate limit from 25 to 500 queries/day (and from 25 to 50 series
per request, 10 to 20 years per query); the app works without a key at the
lower unregistered limit, needed for real (non-AI-estimated) compensation
data (HC5).

## Run the full stack with Docker

Docker Compose runs both the ASP.NET Core app and PostgreSQL. Create a local
environment file and start the stack:

```
Copy-Item .env.example .env
docker compose up --build
```

The app is available at `http://localhost:8000` by default. Change `APP_PORT`
in `.env` to use another host port. 

Compose passes environment variables using ASP.NET Core's double-underscore
configuration syntax. This keeps secrets out of the image and lets environment
variables override the empty placeholders in `appsettings.json`:

| Environment variable | .NET configuration key |
| --- | --- |
| `POSTGRES_*` | `ConnectionStrings:DefaultConnection` |
| `NEMOTRON_API_KEY` | `Ai:Nemotron:ApiKey` |
| `VISION_API_KEY` | `Ai:VisionModel:ApiKey` |
| `BLS_API_KEY` | `Bls:ApiKey` |

Stop the stack with `docker compose down`. Add `-v` only when you also want to
delete the persisted database and uploaded-image volumes.

## Development

Formatting is handled by [CSharpier](https://csharpier.com) (C#) and
[Prettier](https://prettier.io) (CSS). After cloning:

```
dotnet tool restore
npm ci
```

Then:

```
npm run format
npm run check
```

## Status

### Market valuation evidence

`MarketValuationCalculator.Calculate(comps, valuationDate)` provides the PA6
evidence-based calculation. It ignores non-positive prices and missing sources,
removes identical records, and computes the median of the remaining USD prices
(rounded to cents, midpoint away from zero). Matching comparable items/conditions
and converting currencies are the retrieval provider's responsibility.

No usable listings returns `EstimatedValueUsd = null`, empty evidence, and an
explicit low-confidence explanation. One or two usable listings returns their
median with a low-confidence warning. Three listings is only the minimum sample
threshold; it does not guarantee market accuracy. AI estimates are always marked
low confidence. Scan review hides save/merge actions for unavailable values, and
revaluation preserves the existing history when no new value is available.

The registered service remains the AI placeholder from #138. Connecting real
retrieval to this calculator and storing/displaying structured comps is tracked
in [#234](https://github.com/AndrewRoddy/MoneyMirror/issues/234), following #65/#66
and the eBay client in #179. Calculator and rendered-component tests exercise
empty, invalid, sparse, and sufficient evidence without external credentials.

Actively being built. See the [project board](https://github.com/users/AndrewRoddy/projects/8)
for current progress and `docs/backlog-proposal.md` for the full issue backlog and scope
rationale.

## Pull-request checks

Every pull request runs the **Format** and **Test** GitHub Actions checks. Repository
branch protection must require both checks before a pull request can merge. To run the
same checks locally:

```
dotnet restore tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj
dotnet format MoneyMirror.csproj --no-restore --verify-no-changes
dotnet format tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj --no-restore --verify-no-changes
dotnet test tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj --no-restore --configuration Release
```
