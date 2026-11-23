# Especificações — ArtGoncalense

## 1. Referências e estado
Base: [requirements.md](requirements.md) e [architecture.md](architecture.md). As rotas de contas, perfis, criação e consulta pública de ilustrações, moderação e denúncias estão implementadas. As demais seguem propostas. JSON em português e erros no formato Problem Details. O provedor de e-mail para ambiente público depende da DA-05.

## 2. Fluxo de cadastro
1. Visitante informa e-mail, senha e nome público.
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
| GET | `/api/ilustracoes?page=1&pageSize=20` | Público | 200, apenas aprovadas |
| GET | `/api/ilustracoes/{id}` | Público | 200 para ilustração aprovada com artista disponível, ou 404 |
| GET | `/api/ilustracoes/{id}/imagem` | Público | 200 com WebP da ilustração aprovada, ou 404 |
| POST | `/api/ilustracoes` | Artista confirmado | 201 com status PENDENTE |
| DELETE | `/api/ilustracoes/{id}` | Titular ou moderador | 204 |
| POST | `/api/ilustracoes/{id}/denuncias` | Público, com CSRF e limite por IP | 202, 404 para ilustração inexistente ou fora do estado aprovado, 429 acima do limite |
| GET | `/api/moderacao/pendentes` | Moderador ou administrador | 200 com fila paginada |
| GET | `/api/moderacao/{id}/imagem` | Moderador ou administrador | 200 com WebP privado de ilustração pendente ou aprovada, ou 404 |
| POST | `/api/moderacao/{id}/decisao` | Moderador ou administrador, com CSRF | 204 em caso de sucesso, 409 se o estado não permitir a decisão |
| GET | `/api/moderacao/denuncias` | Moderador ou administrador | 200 com fila privada paginada de denúncias recebidas |
| POST | `/api/moderacao/denuncias/{id}/encerrar` | Moderador ou administrador, com CSRF | 204 em caso de sucesso, 404 para denúncia inexistente, 409 se já encerrada |
| GET | `/health` | Operacional | 200/503 |

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
