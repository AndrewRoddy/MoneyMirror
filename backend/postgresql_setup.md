# how to set it up

## Download PostgreSQL
https://www.postgresql.org/download/

Setup the server

## Connection String
You will need to setup your own secret connection string in order to connect to the db from the built app:
`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=----;Database=------;Username=-------;Password=-------"`

## Create MoneyMirror db

Create a local database named `MoneyMirror`:

```sql
CREATE DATABASE MoneyMirror;
```