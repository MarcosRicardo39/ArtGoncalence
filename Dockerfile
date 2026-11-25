FROM node:24-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/ArtGoncalense.API/ArtGoncalense.API.csproj \
    --configuration Release --output /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /publish/ ./
COPY --from=frontend /src/frontend/dist/art-goncalense/browser/ ./wwwroot/
ENV ASPNETCORE_HTTP_PORTS=10000 Piloto__Ativo=true
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "ArtGoncalense.API.dll"]
