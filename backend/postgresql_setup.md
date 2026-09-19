# how to set it up

## Download PostgreSQL
https://www.postgresql.org/download/

Setup the server

## Connection String
You will need to setup your own secret connection string in order to connect to the db from the built app:
`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=----;Database=------;Username=-------;Password=-------"`

## Create pittmoney db

Create a local database named `pittmoney`:

```sql
CREATE DATABASE pittmoney;
```