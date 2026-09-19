# PostgreSQL setup

## Run the full application with Docker

From the repository root, copy the example environment file, set a local
database password, and start both services:

```powershell
Copy-Item .env.example .env
# Edit .env before starting the stack.
docker compose up --build
```

The web app is available at `http://localhost:8000` by default. Compose passes
the Postgres variables into the ASP.NET Core container as its
`ConnectionStrings:DefaultConnection` configuration value. The container uses
`Host=postgres`, which is the Compose service name.

PostgreSQL has no host port mapping. It is attached only to Compose's internal
`database` network, so it is reachable by the app container but not directly
from the host. The app applies the existing EF Core migrations when it starts.

The Compose defaults are:

- Host from the app container: `postgres`
- Port: `5432`
- Database: `MoneyMirror`
- Username: `postgres`
- Password: the value of `POSTGRES_PASSWORD` in `.env`

The data is stored in the `pitt-money_postgres_data` Docker volume, and uploaded
possession images are stored in the `pitt-money-app_data` volume.

To use different credentials, set `POSTGRES_DB`, `POSTGRES_USER`, and
`POSTGRES_PASSWORD` in `.env`. Set `NEMOTRON_API_KEY` and `VISION_API_KEY` there
when using the AI-backed features. These values become environment-backed .NET
configuration; they are not copied into the image.

## Run the app locally with a Docker Postgres

If you prefer to run `dotnet run` on the host, the database must be published
for host access. The normal full-stack Compose setup deliberately does not do
this. Start the database with a temporary override or set the same connection
string in .NET user-secrets after starting a separately published Postgres:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=MoneyMirror;Username=postgres;Password=postgres"
```

Then run the app normally:

```powershell
dotnet run
```

## Stop the stack

```powershell
docker compose down
```

To remove the container and its persisted data, use `docker compose down -v`.
