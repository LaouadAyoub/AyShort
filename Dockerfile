FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ ./src/
RUN dotnet publish src/Adapters/In/WebApi/WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
# The public demo uses the existing in-memory cache instead of hosted Redis.
ENV Redis__Connection=""
EXPOSE 8080
USER app
# Railway supplies PORT; 8080 is the local Docker default.
ENTRYPOINT ["sh", "-c", "exec dotnet WebApi.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
