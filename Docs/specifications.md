# Especificações — ArtGoncalense

## 1. Referências e estado
Base: [requirements.md](requirements.md) e [architecture.md](architecture.md). As rotas de contas, perfis, criação e consulta pública de ilustrações, moderação e denúncias estão implementadas. As demais seguem propostas. JSON em português e erros no formato Problem Details. O provedor de e-mail para ambiente público depende da DA-05.

## 2. Fluxo de cadastro
1. Visitante informa e-mail, senha e nome público e declara ter 18 anos ou mais. A API rejeita cadastro sem a declaração afirmativa.
2. API valida formato, unicidade e política de senha.
3. Para e-mail novo, a conta é criada com confirmação pendente. Solicitações válidas recebem o mesmo 202 e corpo genérico, inclusive para e-mail já cadastrado.
4. Após confirmação, artista pode publicar; antes disso, acesso a publicação é negado.
5. Login inválido retorna erro genérico sem revelar existência de conta.

## 3. Fluxo de publicação
1. Artista autenticado envia título, descrição, categoria (`ORIGINAL` ou `FANART`) e imagem.
2. API usa o artista confirmado da sessão e valida o limite de cinco publicações pendentes ou aprovadas, além do tipo e tamanho real da imagem.
3. Arquivo é processado/otimizado, validado e enviado a armazenamento persistente.
4. Registro é salvo com status `PENDENTE`.
5. Moderador ou administrador aprova ou rejeita uma publicação pendente. Uma publicação aprovada pode ser ocultada. Cada decisão exige justificativa e registra autor, data e ação.
6. Apenas `APROVADA` aparece na galeria pública. `OCULTA` não aparece.

## 4. Contratos HTTP propostos
| Método | Rota | Acesso | Resultado principal |
|---|---|---|---|
| POST | `/api/contas/registrar` | Público | 202 para solicitação válida, inclusive para e-mail já cadastrado |
| POST | `/api/contas/login` | Público | 200 com cookie de sessão conforme DA-03 |
| GET | `/api/contas/csrf` | Público | 204 com cookies de proteção CSRF |
| GET | `/api/contas/sessao` | Usuário autenticado | 200 com `id` e `emailConfirmado`, ou 401 |
| POST | `/api/contas/sair` | Usuário autenticado | 204 |
| POST | `/api/contas/confirmar-email` | Público | 204 ou 400 genérico para código inválido ou expirado |
| POST | `/api/contas/reenviar-confirmacao` | Público | 202 genérico para solicitação válida |
| POST | `/api/contas/solicitar-recuperacao` | Público | 202 genérico para solicitação válida |
| POST | `/api/contas/redefinir-senha` | Público | 204 ou 400 genérico para código inválido ou expirado |
| GET | `/api/artistas/{id}` | Público | 200 com `id`, `nomePublico` e `bio`, ou 404 |
| PUT | `/api/artistas/me` | Artista titular, com CSRF | 204 em caso de sucesso |
| PUT | `/api/artistas/me/apoio` | Artista titular, com CSRF | 204 para cadastrar, substituir ou remover o link, 400 para URL inválida |
| GET | `/api/ilustracoes?page=1&pageSize=20` | Público | 200, apenas aprovadas |
| GET | `/api/ilustracoes/{id}` | Público | 200 para ilustração aprovada com artista disponível, ou 404 |
| GET | `/api/ilustracoes/{id}/imagem` | Público | 200 com WebP da ilustração aprovada, ou 404 |
| POST | `/api/ilustracoes` | Artista confirmado | 201 com status PENDENTE |
| POST | `/api/privacidade/exclusao` | Artista titular, com CSRF e confirmação de senha | 202 com protocolo. Enviar o ID para excluir uma ilustração ou omitir o ID para excluir a conta. |
| POST | `/api/ilustracoes/{id}/denuncias` | Público, com CSRF e limite por IP | 202, 404 para ilustração inexistente ou fora do estado aprovado, 429 acima do limite |
| GET | `/api/moderacao/pendentes` | Moderador ou administrador | 200 com fila paginada |
| GET | `/api/moderacao/{id}/imagem` | Moderador ou administrador | 200 com WebP privado de ilustração pendente ou aprovada, ou 404 |
| POST | `/api/moderacao/{id}/decisao` | Moderador ou administrador, com CSRF | 204 em caso de sucesso, 409 se o estado não permitir a decisão |
| GET | `/api/moderacao/denuncias` | Moderador ou administrador | 200 com fila privada paginada de denúncias recebidas |
| POST | `/api/moderacao/denuncias/{id}/encerrar` | Moderador ou administrador, com CSRF | 204 em caso de sucesso, 404 para denúncia inexistente, 409 se já encerrada |
| GET | `/api/privacidade` | Público | 200 com o endereço configurado para contato de privacidade |
| GET | `/api/privacidade/pedidos` | Administrador | 200 com a fila de exclusão |
| POST | `/api/privacidade/pedidos/{id}/processar` | Administrador, com CSRF | 204 ao concluir, 409 quando há registros sujeitos a conservação |
| POST | `/api/privacidade/contatos/limpar` | Administrador, com CSRF | 200 com o total de contatos removidos |
| PUT | `/api/privacidade/denuncias/{id}/conservacao` | Administrador, com CSRF | 204 ao registrar ou liberar a conservação |
| GET | `/health` | Público | 200 com conexão ao PostgreSQL disponível ou 503 se indisponível |

## 5. Validação e erros
- 400: campos ou arquivo inválidos.
- 401: não autenticado.
- 403: sem permissão ou e-mail não confirmado.
- 404: recurso inexistente ou não público.
- 409: conflito de unicidade, estado ou limite de publicações, quando aplicável. Cadastro público válido segue a resposta uniforme definida acima.
- 413: imagem acima do limite aceito.
- 415: formato de mídia não aceito.
- 429: excesso de tentativas.
- 500/503: falhas internas ou indisponibilidade.
- Não revelar detalhes internos, tokens, endereços privados ou stack traces.

## 6. Critérios de aceite
| ID | Verificação |
|---|---|
| CA-01 | Cadastro válido e confirmação de e-mail habilitam publicação; cadastro inválido não cria conta. |
| CA-02 | Artista não pode editar/excluir publicação de outro artista. |
| CA-03 | Sexta publicação ativa é rejeitada, inclusive sob requisições simultâneas. |
| CA-04 | Arquivo inválido ou acima do limite é rejeitado sem publicação órfã. |
| CA-05 | Publicação pendente/rejeitada/oculta não aparece em galeria pública. |
| CA-06 | Link de apoio abre destino externo válido, sem dinheiro passar pela plataforma. |
| CA-07 | Denúncia chega à moderação sem expor dados do denunciante ao público. |
| CA-08 | Logs, respostas e Git não contêm segredos ou dados pessoais desnecessários. |
| CA-09 | API e front-end funcionam em dispositivos móveis. |

## 7. Casos de teste essenciais
CT-01 cadastro válido; CT-02 e-mail duplicado; CT-03 senha inválida; CT-04 login inválido; CT-05 publicação sem confirmação; CT-06 upload válido; CT-07 formato falso/arquivo inválido; CT-08 imagem acima do limite; CT-09 sexta imagem; CT-10 disputa simultânea pelo limite; CT-11 acesso cruzado a recursos; CT-12 moderação aprova; CT-13 moderação rejeita; CT-14 ocultação após denúncia; CT-15 paginação; CT-16 URL externa maliciosa; CT-17 exclusão e limpeza de storage; CT-18 rate limiting; CT-19 health check.

| Cenários | Suíte xUnit |
|---|---|
| CT-01 a CT-04 | `ContasTests.cs` verifica cadastro, duplicidade, senha inválida e login inválido. |
| CT-05 a CT-10 | `PublicacoesTests.cs` verifica confirmação, upload válido, rejeição por tipo e tamanho, limite de cinco e concorrência. |
| CT-11 | `ApoioTests.cs` e `PrivacidadeTests.cs` verificam titularidade e autorização administrativa. |
| CT-12 e CT-13 | `ModeracaoTests.cs` verifica aprovação e rejeição com trilha de auditoria. |
| CT-14 | `DenunciasTests.cs` verifica denúncia, encerramento, ocultação e retirada da imagem pública. |
| CT-15 | `GaleriaTests.cs` verifica a paginação e a visibilidade das ilustrações. |
| CT-16 | `ApoioTests.cs` verifica a validação dos destinos de links externos. |
| CT-17 | `PrivacidadeTests.cs` verifica exclusão e remoção de arquivos armazenados. |
| CT-18 | `ContasTests.cs` e `DenunciasTests.cs` verificam bloqueios e respostas de limite. |
| CT-19 | `SaudeTests.cs` verifica `200` com banco acessível e `503` com banco indisponível. |
