# Documentação da API — ArtGoncalense

## Estado
As rotas de contas, perfis, ilustrações, moderação, denúncias, privacidade e saúde estão implementadas. A aplicação serve a descrição OpenAPI em `GET /openapi/v1.json` no ambiente de desenvolvimento.

## Convenções
- Base: `/api` para recursos de negócio; `/health` para saúde.
- Transporte HTTPS e JSON, com upload via `multipart/form-data` e respostas de imagem em WebP.
- Campos de negócio em português. Campos padronizados de Problem Details preservados.
- A descrição OpenAPI lista rotas e esquemas. Regras de autenticação, respostas e exemplos constam neste documento.
- Paginação com `page` e `pageSize`, com limite de 100 itens por página nas rotas paginadas.
- Erros padronizados em Problem Details com `traceId`, sem dados sensíveis.

## Contas

Obtenha os cookies com `GET /api/contas/csrf` e envie o valor do cookie legível `XSRF-TOKEN` no cabeçalho `X-XSRF-TOKEN` nas requisições que alteram estado, incluindo `POST` e `PUT`. Renove o token CSRF após mudanças de sessão, inclusive login e logout. O cookie de autenticação é enviado pelo navegador.

| Operação em `/api/contas` | Campos JSON |
|---|---|
| `registrar` | `email`, `senha`, `nomePublico`, `maiorDeIdade` (`true` obrigatório) |
| `login` | `email`, `senha` |
| `confirmar-email` | `email`, `codigo` |
| `reenviar-confirmacao` | `email` |
| `solicitar-recuperacao` | `email` |
| `redefinir-senha` | `email`, `codigo`, `senha` |
| `sair` | Corpo dispensável |

Para cadastro e redefinição, a senha aceita de 15 a 128 caracteres, sem classes de caracteres obrigatórias. Solicitações válidas de cadastro, reenvio e recuperação retornam `202` com a mesma mensagem, inclusive quando o envio é suprimido. Ao exceder o limite por IP a API retorna `429`. Em ambiente sem transporte de e-mail, as três operações de envio retornam `503`.

O login retorna `200` com cookie de sessão ou `401` com mensagem genérica. `GET /api/contas/sessao` retorna `id` e `emailConfirmado` para a sessão autenticada. Confirmação de e-mail, redefinição de senha e saída retornam `204` quando bem-sucedidas.

## Perfis de artista

`GET /api/artistas/{id}` é público e retorna `id`, `nomePublico` e `bio`, que pode ser `null`. Se houver link ativo e válido, inclui `apoio` com `provedor` e `url`. Retorna `404` para perfis inexistentes, suspensos ou de usuários sem o papel de artista.

`PUT /api/artistas/me` usa o identificador do artista autenticado. Envie `nomePublico` obrigatório, com até 200 caracteres, e `bio` opcional, com até 2.000 caracteres. Uma biografia ausente, nula ou em branco limpa o valor. Propriedades adicionais resultam em `400`. A atualização bem-sucedida retorna `204`. Avatar depende da integração de imagens ao perfil.

`PUT /api/artistas/me/apoio` usa o mesmo titular e exige CSRF. Envie JSON com `url` como um link direto `https://ko-fi.com/seuperfil` ou como `null` para remover. A regra aceita um identificador alfanumérico ASCII com sublinhados, de até 64 caracteres. Subdomínios, portas explícitas, parâmetros, fragmentos, trechos escapados e caminhos adicionais são recusados. A API valida o formato localmente e retorna `204` para a atualização, ou `400` para dados inválidos.

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

`Location` identifica a ilustração criada. A leitura pública fica disponível após aprovação. Pendentes e aprovadas ocupam as cinco vagas por artista. A API retorna `409` quando todas estão ocupadas, `413` para tamanho acima do limite e `415` quando a mídia não corresponde a um formato aceito. Até quatro uploads são processados simultaneamente por instância. O excesso recebe `429` e pode tentar novamente.

## Galeria pública

`GET /api/ilustracoes?page=1&pageSize=20` retorna `page`, `pageSize`, `total` e `itens`. O máximo de `pageSize` é 100. A ordem é da publicação mais recente para a mais antiga, com desempate por identificador.

Cada item e o detalhe em `GET /api/ilustracoes/{id}` incluem `id`, `titulo`, `descricao`, `categoria`, `criadoEm`, `artista` com `id` e `nomePublico`, e `imagemUrl`. Apenas ilustrações aprovadas de artistas disponíveis são públicas. A rota de imagem retorna WebP com `Cache-Control: no-store`. Detalhes e imagem retornam `404` quando a publicação não está visível.

## Moderação

As rotas abaixo exigem sessão de Moderador ou Admin.

- `GET /api/moderacao/pendentes?page=1&pageSize=20` retorna `page`, `pageSize`, `total` e `itens`. Cada item contém `id`, `artistaId`, `titulo`, `descricao`, `categoria`, `status`, `criadoEm` e `imagemUrl`. O máximo de `pageSize` é 100.
- `GET /api/moderacao/{id}/imagem` retorna WebP sem cache para ilustrações pendentes ou aprovadas. A API retorna `404` para imagem indisponível.
- `POST /api/moderacao/{id}/decisao` exige CSRF e JSON com `acao` (`APROVAR`, `REJEITAR` ou `OCULTAR`) e `justificativa` obrigatória de até 2.000 caracteres. Retorna `204` para uma decisão aplicada, `404` para ilustração inexistente e `409` quando o estado atual impede a ação.

Aprovar ou rejeitar exige estado pendente. Ocultar exige estado aprovado. O servidor registra o estado resultante, a ação, a justificativa, o autor e a data da decisão na mesma transação.

## Denúncias

`POST /api/ilustracoes/{id}/denuncias` aceita solicitações de visitantes com CSRF para ilustrações aprovadas. Envie JSON com `motivo` (`DIREITOS_AUTORAIS` ou `OUTRO`), `descricao` obrigatória de até 2.000 caracteres e `contato` como e-mail de até 256 caracteres. O contato é obrigatório em notificações de direitos autorais. A resposta de sucesso é `202` sem corpo. O limite soma as solicitações por IP em todos os alvos, até cinco a cada 15 minutos.

`GET /api/moderacao/denuncias?page=1&pageSize=20` retorna `page`, `pageSize`, `total` e `itens`. Os itens são denúncias recebidas e incluem `id`, `ilustracaoId`, `motivo`, `descricao`, `contato` e `criadoEm`. O máximo de `pageSize` é 100. A fila exige sessão de Moderador ou Admin e é servida sem cache.

`POST /api/moderacao/denuncias/{id}/encerrar` exige o mesmo papel, CSRF e `justificativa` obrigatória de até 2.000 caracteres. A API grava o estado `ENCERRADA`, a justificativa, o autor e a data. A visibilidade da ilustração continua sob controle das decisões de moderação.

## Privacidade

- `GET /api/privacidade` retorna `email`, que é nulo quando o endereço do canal ainda não foi configurado.
- `POST /api/privacidade/exclusao` exige sessão de artista, CSRF e JSON com `senha` e `ilustracaoId` opcional. Omitir o ID solicita exclusão de toda a conta. O retorno `202` traz o protocolo no campo `id`. Senha incorreta retorna `403`. Uma obra indisponível para o artista retorna `404`.
- `GET /api/privacidade/pedidos` exige Admin e lista até 100 pedidos pendentes, dos mais antigos aos mais recentes, com `id`, `titularId`, `ilustracaoId`, `solicitadoEm` e `limiteEm`.
- `POST /api/privacidade/pedidos/{id}/processar` exige Admin e CSRF. Retorna `204` após concluir, inclusive em uma repetição, ou `409` se houver denúncia em aberto ou registros a conservar.
- `POST /api/privacidade/contatos/limpar` exige Admin e CSRF e retorna a contagem `removidos` de contatos de denúncias encerradas há pelo menos 90 dias, descontadas as exceções registradas.
- `PUT /api/privacidade/denuncias/{id}/conservacao` exige Admin e CSRF. Enviar `motivo` com até 500 caracteres registra a justificativa. Enviar `motivo: null` libera o contato para os prazos normais.

## Saúde

`GET /health` verifica a conexão da aplicação com o PostgreSQL sem exigir sessão. Retorna `200` com `Healthy` ou `503` com `Unhealthy`, em texto simples. O corpo omite detalhes de diagnóstico.
