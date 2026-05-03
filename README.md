# Glory Cafe

Online ordering and admin platform for **Glory Cafe** (Brisbane, QLD).

## Stack

- **Backend:** ASP.NET Core 9 — Clean Architecture (Domain / Application / Infrastructure / API)
- **Frontend:** React + Vite + TypeScript (mobile-first)
- **Database:** PostgreSQL 18
- **Payments:** Stripe (test mode)
- **Real-time admin:** SignalR
- **Auth (admin):** JWT

## Repository layout

```
GloryCafe/
├── GloryCafeAPI/        Backend solution (.NET)
│   ├── src/
│   │   ├── GloryCafe.Domain/
│   │   ├── GloryCafe.Application/
│   │   ├── GloryCafe.Infrastructure/
│   │   └── GloryCafe.API/
│   └── tests/
├── glory-cafe-web/      Frontend (React + Vite)
├── docs/                Setup and architecture notes
│   └── SETUP.md
├── .gitignore
└── README.md
```

## Branching

- `main`  — stable
- `develop` — active development

## Documentation

Each phase is documented step by step in `docs/`:

- [`docs/SETUP.md`](docs/SETUP.md) — Phase 1: backend skeleton and Clean Architecture
- [`docs/PHASE2.md`](docs/PHASE2.md) — Phase 2: domain entities and first migration
