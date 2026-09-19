# PostgreSQL setup

## Start PostgreSQL with Docker

From the repository root, start the development database:

```powershell
docker compose up -d postgres
```

The Compose service uses these defaults:

- Host: `localhost`
- Port: `5432`
- Database: `MoneyMirror`
- Username: `postgres`
- Password: `postgres`

The data is stored in the `pitt-money_postgres_data` Docker volume, so restarting
the container does not remove the database.

To use different credentials, set `POSTGRES_DB`, `POSTGRES_USER`, and
`POSTGRES_PASSWORD` in a local `.env` file before starting the service.

## Configure the application

The application reads its connection string from user secrets. With the Compose
defaults, configure it once from the repository root:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=MoneyMirror;Username=postgres;Password=postgres"
```

Then run the app normally:

```powershell
dotnet run
```

When the app starts in Development, it applies the existing EF Core migrations
automatically.

## Stop PostgreSQL

```powershell
docker compose stop postgres
```

To remove the container and its persisted data, use `docker compose down -v`.
