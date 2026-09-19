# Pitt Money

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

## Status

Actively being built. See the [project board](https://github.com/users/AndrewRoddy/projects/8)
for current progress and `docs/backlog-proposal.md` for the full issue backlog and scope
rationale.

