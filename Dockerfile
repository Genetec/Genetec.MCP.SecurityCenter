FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Genetec.Mcp.SecurityCenter/Genetec.Mcp.SecurityCenter.csproj src/Genetec.Mcp.SecurityCenter/
RUN dotnet restore src/Genetec.Mcp.SecurityCenter/Genetec.Mcp.SecurityCenter.csproj

COPY src/Genetec.Mcp.SecurityCenter/ src/Genetec.Mcp.SecurityCenter/
COPY LICENSE LICENSE
COPY skills/ skills/
COPY privilegeTemplates/ privilegeTemplates/
RUN dotnet publish src/Genetec.Mcp.SecurityCenter/Genetec.Mcp.SecurityCenter.csproj \
    -c Release -o /app/publish --no-restore \
    && rm -f /app/publish/appsettings.example.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Docker seeds an empty configuration volume with this JSON and its permissions.
RUN install -d -m 0700 -o 1654 -g 1654 /app/config /app/config/keys /app/logs \
    && mv /app/appsettings.json /app/config/appsettings.json \
    && chown 1654:1654 /app /app/config/appsettings.json \
    && chmod 0700 /app \
    && chmod 0600 /app/config/appsettings.json
USER 1654:1654

ENV APPSETTINGS_PATH=/app/config/appsettings.json
# The ASP.NET runtime image sets an HTTP port by default. Clear it so the
# HTTP transport binds only the configured HTTPS endpoint.
ENV ASPNETCORE_HTTP_PORTS=
EXPOSE 5000
ENTRYPOINT ["dotnet", "Genetec.Mcp.SecurityCenter.dll"]
