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

O Angular serve a interface em `https://localhost:4200` e encaminha `/api` para `https://localhost:7218` em desenvolvimento. Use HTTPS para que o navegador aceite os cookies `Secure`.

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

Os testes aplicam migrations no banco indicado. Use um banco descartável, pois os testes HTTP de conta criam registros fictícios. Para o banco de desenvolvimento, aplique as migrations com o comando explícito acima.

## Autenticação local

Execute a API no perfil HTTPS de desenvolvimento. Antes de iniciá-la, configure `$env:Contas__DiretorioEmailLocal = Join-Path ([System.IO.Path]::GetTempPath()) 'artgoncalense-email'`. O diretório deve ser privado e fora do repositório. Os arquivos JSON contêm códigos temporários e devem ser tratados como credenciais. Use somente endereços fictícios e remova os arquivos ao encerrar os testes manuais.

Os fluxos e os cabeçalhos estão na [documentação da API](Docs/api-documentation.md). Os limites de autenticação podem ser ajustados por variáveis `Contas__Limites__LoginPorIp`, `Contas__Limites__CorreioPorIp`, `Contas__Limites__EnviosPorEmail` e `Contas__Limites__FalhasPorConta`.

Na interface Angular, abra `/conta/cadastro`, use um e-mail fictício e copie o campo `codigo` da mensagem de confirmação gerada no diretório local para `/conta/confirmar-email`. Depois de entrar, `/conta/perfil` permite editar nome e biografia e abrir o perfil público. Para recuperação de senha, use o código da mensagem com finalidade `recuperacao`.

A página inicial exibe a galeria pública. Com e-mail confirmado, abra `/publicar` para enviar uma ilustração à moderação. Depois da aprovação, ela aparece na galeria com um link para o perfil do artista.

Uma sessão de Moderador ou Admin abre `/moderacao` após o login. O painel tem filas de ilustrações pendentes e denúncias, além da aba de obras aprovadas visíveis na galeria. As ações de revisão exigem justificativa e usam as permissões da API.

## Imagens de desenvolvimento

Configure `Imagens__DiretorioLocal` com um caminho absoluto para um diretório privado e persistente fora do repositório. Com a API em desenvolvimento, um artista com e-mail confirmado pode enviar uma imagem para `POST /api/ilustracoes` conforme a [documentação da API](Docs/api-documentation.md).

As [amostras de ilustrações](backend/tests/ArtGoncalense.Tests/Fixtures/README.md) exercitam o processamento em três estilos. Os testes usam diretório descartável e dados fictícios.
