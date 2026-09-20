# Money Mirror

Plenty of apps will tell you a net worth number. Very few will tell you where it
came from, and almost none teach you anything you can act on afterwards.

Money Mirror is built on the opposite bet. A number you can't explain is a number
you can't make decisions with, so every figure in this app arrives with its
reasoning attached: the comparable listings behind a valuation, the BLS series
behind a salary range, the skills behind an opportunity. When it isn't confident,
it says so.

It also counts more than your bank balance. The furniture in your apartment is
worth something. So is what the labor market will pay for what you know.

```
financial assets + physical assets - liabilities = estimated net worth
```

Market potential sits beside that number rather than inside it. Your earning
power is real, but it isn't cash, and folding it in would make the total a lie.

## What it does

**Financial.** Accounts, investments, and other assets tracked against loans,
credit, and liabilities. The arithmetic is ordinary and deliberately transparent.

**Physical assets.** Photograph something, or pan a short video around a room.
A vision model picks out the items, you tap to isolate one, and Nemotron
estimates what it would resell for and explains its reasoning. Anything you
confirm joins your inventory and rolls into net worth.

**Market potential.** Upload a resume. Nemotron reads it into a structured
profile, matches it to occupations, and pairs it with real Bureau of Labor
Statistics wage data. You get a compensation range, which of your skills the
market is asking for, which ones you're missing, and which better paying roles
are within reach of what you already have.

## The models

All three model slots are configured in [`appsettings.json`](appsettings.json)
under `Ai:Nemotron` and `Ai:VisionModel`.

| Job | Model | Runs on |
| --- | --- | --- |
| Reasoning, extraction, valuation, explanation | `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning` | NVIDIA NIM (`integrate.api.nvidia.com`) |
| Object detection in photos and video frames | `meta/llama-3.2-11b-vision-instruct` | NVIDIA NIM (`integrate.api.nvidia.com`) |
| Tap to segment item cutouts | MobileSAM (ONNX) | Your browser, WebGPU with a WASM fallback |

Both hosted models point at the same endpoint, so one NVIDIA API key covers them.

Every Nemotron job runs through `FallbackLlmService`, which retries the same
prompt against Anthropic Claude (`claude-sonnet-4-5`) whenever Nemotron is
unreachable or errors out. Claude is a backup, not a fourth job - it never
runs unless Nemotron already failed.

MobileSAM never leaves the client. You tap an object in the camera view and the
mask is computed on device, which keeps the interaction instant and means only
the crop you chose is ever uploaded.

## How Nemotron is used

Nemotron does five different jobs here, not one job five times:

| | What it's asked for |
| --- | --- |
| **Profile extraction** | Unstructured resume text into a typed profile: roles, dates, skills, education, publications |
| **Occupation matching** | That profile into candidate occupations, with the key skills each one wants |
| **Compensation estimation** | A structured salary estimate when BLS has no series for the role |
| **Asset valuation** | A resale price for a detected object, with written reasoning |
| **Explanation** | Plain language prose explaining what a computed range means and why it varies |

The design rule underneath all of it: Nemotron supplies judgment, C# does the
arithmetic. `FinancialNetWorthCalculator`, `SkillGapAnalyzer`,
`OpportunityFinder`, and the BLS aggregation are plain deterministic code. The
model is never asked to add up your net worth.

The explanation prompt is the clearest example. It hands Nemotron a range already
computed from real BLS observations and tells it, in so many words, not to
propose numbers of its own, only to explain the evidence it was given. That keeps
the prose educational instead of confidently invented.

## Quick start

You'll need [Docker](https://docs.docker.com/get-docker/) and an
[NVIDIA API key](https://build.nvidia.com/). The same key works for both hosted
models.

```sh
git clone https://github.com/AndrewRoddy/MoneyMirror.git
cd MoneyMirror
cp .env.example .env     # PowerShell: Copy-Item .env.example .env
```

Open `.env` and fill in `NEMOTRON_API_KEY` and `VISION_API_KEY`. `BLS_API_KEY` is
optional. A [free key](https://data.bls.gov/registrationEngine/) raises the rate
limit, and the app falls back to the unregistered limit without one. The Postgres
defaults are fine for local work.

```sh
docker compose up --build
```

That brings up the app and a Postgres container, applies migrations on first
boot, and serves the app at **http://localhost:8000** (set `APP_PORT` to move
it). `docker compose down` stops everything. Add `-v` if you also want the
database and uploaded images gone.

No keys? The app still runs and the financial pages work. The scan and resume
features will just report that the provider call failed.

## Things worth knowing

- **Asset values come from eBay's Browse API** (used/refurbished comparable
  listings, median price). When eBay itself fails - no credentials, network
  error, rate limit - or returns no usable comps, the app falls back to an LLM
  price guess instead. Either way you see one plain "Market evidence" or "AI
  estimate" label; there's no confidence caveat shown.
- **There is no login.** One implicit user, no auth, no multi-tenancy. It's built
  to run on your own machine, so don't put it on the open internet.
- **Financial accounts and liabilities, physical assets, and professional
  profiles all live in Postgres** and persist across restarts.
- **The vision model is not deterministic.** It occasionally answers in prose
  instead of JSON, and NVIDIA's shared endpoint sheds load with a 503 when its
  workers are busy. Both are handled, bad replies get re-prompted and transient
  failures retry with backoff, but a scan can still fail. Every error has a
  **Show provider response** button so you can see what actually came back.
- Postgres has no host port mapping. It sits on an internal Compose network,
  reachable by the app container and nothing else.

## Layout

```
Ai/               Provider clients: Nemotron, NVIDIA vision, retry handling
PhysicalAssets/   Detection, segmentation, valuation, image storage
HumanCapital/     Resume parsing, BLS wage data, skill gaps, opportunities
Financial/        Net worth math
Features/         Blazor pages and components
backend/Data/     EF Core context and migrations
wwwroot/js/       Browser side camera, video scanning, MobileSAM
tests/            xUnit tests, plus Node tests for the browser side scanning
```

A modular monolith: one project, one database, ASP.NET Core and Blazor Server on
.NET 9.

## Working on it

```sh
dotnet test tests/MoneyMirror.Tests/MoneyMirror.Tests.csproj
dotnet tool restore && npm ci
npm run format               # CSharpier for C#, Prettier for CSS
```

PRs run formatting and tests. Fuller detail on user secrets, running against a
host local Postgres, and the environment variable mapping lives in
[docs/development.md](docs/development.md).

## Status

Actively being built. The [project board](https://github.com/users/AndrewRoddy/projects/8)
has what's in flight.
