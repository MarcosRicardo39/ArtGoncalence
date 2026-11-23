# ArtGoncalense

Plataforma de publicação de ilustrações e fanarts. A [documentação SDD](Docs/README.md) reúne requisitos, decisões e andamento.

## Ambiente local

Use o SDK .NET de `global.json`, o Node.js de `frontend/.nvmrc` e o npm indicado em `frontend/package.json`.

A partir da raiz, compile e inicie a API:

```powershell
dotnet restore backend/ArtGoncalense.sln
dotnet build backend/ArtGoncalense.sln --no-restore
dotnet run --project backend/src/ArtGoncalense.API --launch-profile https
```

A API disponibiliza `/openapi/v1.json` em desenvolvimento. Para usar o perfil HTTPS no navegador, configure a confiança no certificado local com `dotnet dev-certs https --trust`.

Em outro terminal, instale as dependências travadas e inicie o Angular:

```powershell
cd frontend
npm ci
npm start
```

## Verificações

```powershell
dotnet test backend/ArtGoncalense.sln
cd frontend
npm run build
npm test -- --watch=false
```

Os testes xUnit verificam as invariantes do modelo de domínio. A aplicação funciona localmente sem PostgreSQL, armazenamento de imagens ou provedor de e-mail nesta etapa.
