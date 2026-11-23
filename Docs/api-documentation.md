# Documentação da API — ArtGoncalense

## Estado
As rotas de contas da T05 estão implementadas. Os demais caminhos de [specifications.md](specifications.md) são contratos propostos.

## Convenções
- Base: `/api` para recursos de negócio; `/health` para saúde.
- Transporte HTTPS e JSON; upload via `multipart/form-data`.
- Campos de negócio em português. Campos padronizados de Problem Details preservados.
- Documentar autenticação, respostas e exemplos no OpenAPI/Scalar ou Swagger.
- Paginação com `page` e `pageSize`, limite máximo configurável.
- Erros padronizados em Problem Details com `traceId`, sem dados sensíveis.

## Contas

Obtenha os cookies com `GET /api/contas/csrf` e envie o valor do cookie legível `XSRF-TOKEN` no cabeçalho `X-XSRF-TOKEN` em cada `POST`. Renove o token CSRF após mudanças de sessão, inclusive login e logout. O cookie de autenticação é enviado pelo navegador.

| Operação em `/api/contas` | Campos JSON |
|---|---|
| `registrar` | `email`, `senha`, `nomePublico` |
| `login` | `email`, `senha` |
| `confirmar-email` | `email`, `codigo` |
| `reenviar-confirmacao` | `email` |
| `solicitar-recuperacao` | `email` |
| `redefinir-senha` | `email`, `codigo`, `senha` |
| `sair` | Corpo dispensável |

Para cadastro e redefinição, a senha aceita de 15 a 128 caracteres, sem classes de caracteres obrigatórias. Solicitações válidas de cadastro, reenvio e recuperação retornam `202` com a mesma mensagem, inclusive quando o envio é suprimido. Ao exceder o limite por IP a API retorna `429`. Em ambiente sem transporte de e-mail, as três operações de envio retornam `503`.

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
