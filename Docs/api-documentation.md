# Documentação da API — ArtGoncalense

## Estado
As rotas de contas, perfis, criação de ilustração e moderação estão implementadas. Os demais caminhos de [specifications.md](specifications.md) são contratos propostos.

## Convenções
- Base: `/api` para recursos de negócio; `/health` para saúde.
- Transporte HTTPS e JSON; upload via `multipart/form-data`.
- Campos de negócio em português. Campos padronizados de Problem Details preservados.
- Documentar autenticação, respostas e exemplos no OpenAPI/Scalar ou Swagger.
- Paginação com `page` e `pageSize`, limite máximo configurável.
- Erros padronizados em Problem Details com `traceId`, sem dados sensíveis.

## Contas

Obtenha os cookies com `GET /api/contas/csrf` e envie o valor do cookie legível `XSRF-TOKEN` no cabeçalho `X-XSRF-TOKEN` nas requisições que alteram estado, incluindo `POST` e `PUT`. Renove o token CSRF após mudanças de sessão, inclusive login e logout. O cookie de autenticação é enviado pelo navegador.

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

## Perfis de artista

`GET /api/artistas/{id}` é público e retorna `id`, `nomePublico` e `bio`, que pode ser `null`. Retorna `404` para perfis inexistentes, suspensos ou de usuários sem o papel de artista.

`PUT /api/artistas/me` usa o identificador do artista autenticado. Envie `nomePublico` obrigatório, com até 200 caracteres, e `bio` opcional, com até 2.000 caracteres. Uma biografia ausente, nula ou em branco limpa o valor. Propriedades adicionais resultam em `400`. A atualização bem-sucedida retorna `204`. Avatar depende da definição de imagens e storage, e links de apoio serão tratados na T14.

## Publicação de ilustração

`POST /api/ilustracoes` exige sessão de artista com e-mail confirmado e cabeçalho CSRF. Envie `multipart/form-data` com `titulo` de até 200 caracteres, `descricao` opcional de até 2.000, `categoria` igual a `ORIGINAL` ou `FANART` e um arquivo em `imagem`. São aceitos JPEG, PNG e WebP estáticos de até 10.000.000 bytes e 4.096 px por lado.

Resposta `201 Created`:
```json
{
  "id": "uuid-da-ilustracao",
  "titulo": "Meu desenho",
  "categoria": "ORIGINAL",
  "status": "PENDENTE"
}
```

`Location` identifica a ilustração criada. A leitura pública depende da etapa da galeria. Pendentes e aprovadas ocupam as cinco vagas por artista. A API retorna `409` quando todas estão ocupadas, `413` para tamanho acima do limite e `415` quando a mídia não corresponde a um formato aceito. Até quatro uploads são processados simultaneamente por instância. O excesso recebe `429` e pode tentar novamente.

## Moderação

As rotas abaixo exigem sessão de Moderador ou Admin.

- `GET /api/moderacao/pendentes?page=1&pageSize=20` retorna `page`, `pageSize`, `total` e `itens`. Cada item contém `id`, `artistaId`, `titulo`, `descricao`, `categoria`, `status`, `criadoEm` e `imagemUrl`. O máximo de `pageSize` é 100.
- `GET /api/moderacao/{id}/imagem` retorna WebP sem cache para ilustrações pendentes ou aprovadas. A API retorna `404` para imagem indisponível.
- `POST /api/moderacao/{id}/decisao` exige CSRF e JSON com `acao` (`APROVAR`, `REJEITAR` ou `OCULTAR`) e `justificativa` obrigatória de até 2.000 caracteres. Retorna `204` para uma decisão aplicada, `404` para ilustração inexistente e `409` quando o estado atual impede a ação.

Aprovar ou rejeitar exige estado pendente. Ocultar exige estado aprovado. O servidor registra o estado resultante, a ação, a justificativa, o autor e a data da decisão na mesma transação.
