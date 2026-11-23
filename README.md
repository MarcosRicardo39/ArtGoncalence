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

Os testes xUnit verificam as invariantes do domínio. O teste de integração usa um banco PostgreSQL descartável quando `ARTGONCALENSE_TEST_POSTGRES` está definida.

## PostgreSQL local

Crie um banco local vazio para desenvolvimento e outro dedicado a testes. Configure cada conexão na sessão do PowerShell usando as credenciais locais. Execute os comandos a partir da raiz.

```powershell
$env:ConnectionStrings__ArtGoncalense = Read-Host 'Conexão PostgreSQL de desenvolvimento' -MaskInput
dotnet tool restore
dotnet ef database update --project backend/src/ArtGoncalense.Infrastructure --startup-project backend/src/ArtGoncalense.API

$env:ARTGONCALENSE_TEST_POSTGRES = Read-Host 'Conexão PostgreSQL do banco descartável de teste' -MaskInput
dotnet test backend/ArtGoncalense.sln
```

O teste aplica a migration no banco indicado e desfaz os dados de teste ao encerrar a transação. A API aplica migrations somente pelo comando explícito acima.
