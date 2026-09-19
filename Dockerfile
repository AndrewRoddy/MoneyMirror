FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY nuget.config MoneyMirror.csproj global.json ./
RUN dotnet restore MoneyMirror.csproj

COPY . .
RUN dotnet publish MoneyMirror.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --property:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data/possession-images

ENTRYPOINT ["dotnet", "MoneyMirror.dll"]
