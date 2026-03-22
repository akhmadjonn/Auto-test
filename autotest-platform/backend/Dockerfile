FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.sln .
COPY nuget.config .
COPY src/AutoTest.Domain/AutoTest.Domain.csproj src/AutoTest.Domain/
COPY src/AutoTest.Application/AutoTest.Application.csproj src/AutoTest.Application/
COPY src/AutoTest.Infrastructure/AutoTest.Infrastructure.csproj src/AutoTest.Infrastructure/
COPY src/AutoTest.Api/AutoTest.Api.csproj src/AutoTest.Api/

RUN dotnet restore src/AutoTest.Api/AutoTest.Api.csproj

COPY . .
RUN dotnet publish src/AutoTest.Api/AutoTest.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENV TZ=Asia/Tashkent
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AutoTest.Api.dll"]
