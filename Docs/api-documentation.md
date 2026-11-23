# Documentação da API — ArtGoncalense

## Estado
A API ainda não foi implementada. Os caminhos de [specifications.md](specifications.md) são contratos propostos e podem mudar após a resolução das decisões aplicáveis.

## Convenções
- Base: `/api` para recursos de negócio; `/health` para saúde.
- Transporte HTTPS e JSON; upload via `multipart/form-data`.
- Campos de negócio em português. Campos padronizados de Problem Details preservados.
- Documentar autenticação, respostas e exemplos no OpenAPI/Scalar ou Swagger.
- Paginação com `page` e `pageSize`, limite máximo configurável.
- Erros padronizados em Problem Details com `traceId`, sem dados sensíveis.

## Exemplo ilustrativo de publicação
Requisição autenticada `POST /api/ilustracoes` com `multipart/form-data` contendo `titulo`, `descricao`, `categoria` e `imagem`.

Resposta planejada `201 Created`:
```json
{
  "id": "uuid-da-ilustracao",
  "titulo": "Meu desenho",
  "categoria": "ORIGINAL",
  "status": "PENDENTE"
}
```

O exemplo não é um endpoint em execução. O formato exato de IDs, nomes de campos e respostas será fechado antes da implementação.
