# Shop Floor Tracker

A digital traveler for a sheet metal fabrication shop, built as an MES-style
desktop app in C#. It replaces the paper traveler that follows a job across
the floor: work orders, routings through each operation, and operator
sign-offs with timestamps.

> **Status: in progress.** The data model, database migrations, and
> work order release logic are built and tested. The traveler screens,
> inspection, material tracking, and traceability report are next.

## Why I built this

I spent 13 years in metal fabrication manufacturing: as a welder, a leadman,
and then as a detail draftsman and CNC programmer. Every job on the floor
moved with a paper traveler. This project models that process the way it
actually worked, with decisions based on what I saw on the shop floor.

## Tech stack

- **C# / .NET 10**
- **WPF** desktop UI
- **Entity Framework Core** with **SQLite**
- **xUnit** for automated tests

## What works today

- **Inventory:** parts, storage locations, and stock levels by location
- **Shop floor data model:** operators, operations, part routings, work orders,
  job operations, and operator sign-offs
- **Database migrations** that add the shop floor tables without touching
  existing inventory data
- **Work order release:** releasing a job copies the part's routing onto the
  job's traveler and marks it Released
- **Automated tests** that run against a temporary in-memory database

## Example routing

A formed bracket moves through these operations:

| Seq | Operation |
|----:|-----------|
| 10 | Shear/Cut |
| 20 | Punch |
| 30 | Brake |
| 40 | Fit-up |
| 50 | Weld/Cleanup |
| 60 | Inspect |

Routings are stored as data, not hardcoded, so each part can have its own.
A tube part can skip Punch and Brake, and operations can be added or split
without code changes.

## Design decisions

- **Routing snapshots.** Releasing a job copies the routing onto that job.
  Changing a part's routing later never rewrites the history of jobs already
  on the floor.
- **One traveler per job.** A work order follows the job and covers a
  quantity of parts, matching how travelers were used on the floor.
- **Permanent history.** Sign-offs are added as new records, never
  overwritten, so there's always a record of who did what and when.
- **Protected records.** Delete rules block removing operators, operations,
  parts, or jobs that still have history attached.
- **Readable data.** Statuses are stored as text (`Released`, not `1`), so
  the database is easy to query and report on directly.
- **Steps numbered by 10s,** a common routing convention, so a step can be
  added between two others without renumbering.

## Roadmap

- [x] Inventory: parts, locations, and stock levels
- [x] Shop floor data model and migration
- [x] Work order release with tests
- [ ] Operator start and complete sign-offs, enforcing operation order
- [ ] Traveler screen for releasing jobs and signing off operations
- [ ] Inspection pass/fail with defect notes and rework
- [ ] Material lots and heat numbers, issued from inventory to jobs
- [ ] Traceability report per job, written in SQL
- [ ] Demo data and screenshots

## Getting started

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/okcowboy80/shop-floor-tracker.git
cd shop-floor-tracker
dotnet build
dotnet test
dotnet run --project InventoryTracker
```

The app creates its SQLite database on first run at
`%LOCALAPPDATA%\InventoryTracker\inventory.db` and applies any pending
migrations at startup.

## Project structure

```text
InventoryTracker/          WPF app
  Data/                    InventoryContext (EF Core database setup and rules)
  Models/                  Entity classes and status enums
  Services/                Shop floor business rules
  Migrations/              EF Core database migrations
InventoryTracker.Tests/    xUnit tests using an in-memory SQLite database
```