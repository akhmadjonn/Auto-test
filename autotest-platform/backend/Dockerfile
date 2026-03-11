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
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AutoTest.Api.dll"]
