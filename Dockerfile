# ─────────────────────────────────────────────────────────────────────────────
# Stage 1: build & publish the .NET 10 console app
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# restore with the project file first (layer caching)
COPY Rag.Indexer.csproj .
RUN dotnet restore

# copy everything else and publish
COPY . .
RUN dotnet publish -c Release -o /app

# ─────────────────────────────────────────────────────────────────────────────
# Stage 2: runtime image – tiny, only contains the published binaries
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The indexer accepts a folder path as argument; mount the repo being indexed
# as a volume at /repo (default) or override with --volume
VOLUME ["/repo"]

ENTRYPOINT ["dotnet", "Rag.Indexer.dll"]