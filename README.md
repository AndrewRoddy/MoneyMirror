# Money Mirror

Pitt Money is a personal net-worth and "personal capital" application. It goes beyond a
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

## Configuration

The app reads AI provider settings from the `Ai:Nemotron` and `Ai:VisionModel`
configuration sections (`BaseUrl`, `Model`, `ApiKey`). `appsettings.json` ships
with empty `ApiKey` placeholders — never commit real keys there. Set them locally
with .NET user-secrets instead:

```
dotnet user-secrets set "Ai:Nemotron:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:VisionModel:ApiKey" "<your-key>"
```

or via environment variables (`Ai__Nemotron__ApiKey`, `Ai__VisionModel__ApiKey`).

## Development

Formatting is handled by [CSharpier](https://csharpier.com) (C#) and
[Prettier](https://prettier.io) (CSS). After cloning:

```
dotnet tool restore
npm ci
```

Then:

```
npm run format    # format C# and CSS
npm run check     # verify formatting without writing
```

### Styling

Colors follow the [University of Pittsburgh brand palette](https://brand.pitt.edu),
defined once in `wwwroot/theme.css` in Pitt's own three tiers:

| Tier | Colors |
| --- | --- |
| **Primary** | Royal Blue `#003594`, Gold `#FFB81C` |
| **Secondary** | New Sky `#DBEEFF`, Gray `#C8C9C7`, White `#FFFFFF`, Medium Blue `#00205B`, Bronze `#B87333`, Black `#000000` |
| **Accent** | Light Green `#00AD6E`, Infrared `#FF5B45`, Merlot `#770538`, Light Blue `#66B2E3` |

Per Pitt's guidelines: Royal and Gold dominate; secondary colors support them; accent
colors are used sparingly and **no more than four at once** (we use three, reserving
Light Blue for charts).

No color outside that palette appears anywhere, and there are no hand-picked in-between
shades — every tint is derived with `color-mix()` from an official value.

Components use the **role** tokens, never the raw palette and never a literal hex:
`--text`, `--text-muted`, `--surface`, `--border`, `--brand`, `--accent`, `--positive`,
`--negative`, and their `-text` / `-surface` variants. If no role fits, add one to
`theme.css`.

Two accessibility constraints are baked in: Gold is 1.7:1 on white and must never carry
small text (it's a fill, rule and highlight color — Bronze is the warm tone for type),
and every text role clears WCAG AA 4.5:1 against the background it actually sits on.

Scoped `.razor.css` files inherit the tokens from `:root` and can use them freely, but
must never declare their own `:root` block: Blazor rewrites scoped selectors with a
per-component attribute, so the declarations are silently dropped.

## Status

Actively being built. See the [project board](https://github.com/users/AndrewRoddy/projects/8)
for current progress and `docs/backlog-proposal.md` for the full issue backlog and scope
rationale.

