# Requisitos — ArtGoncalense

## 1. Objetivo e escopo
Plataforma web de comunidade de artistas brasileiros, com cadastro aberto, perfis públicos, publicação de ilustrações originais e fanarts, galeria e links externos de apoio voluntário. Hobby sem receita para a plataforma no MVP. Possibilidade futura de anúncios **fora do escopo**.

## 2. Papéis
- Visitante: navegar e visualizar publicações e perfis.
- Artista cadastrado: gerenciar perfil e suas próprias publicações.
- Moderador/administrador: analisar denúncias e publicações, suspender conteúdo e contas.

## 3. Requisitos funcionais
| ID | Requisito | Critério de aceite |
|---|---|---|
| RF-01 | Cadastro e autenticação | Cadastro com e-mail e senha, validação, confirmação de e-mail antes de publicar e login/logout funcionando. |
| RF-02 | Perfil do artista | Nome público, biografia, avatar opcional e link de apoio; edição apenas pelo titular. |
| RF-03 | Publicar ilustração | Artista verificado envia título, descrição, categoria original/fanart e imagem; publicação fica pendente de moderação. |
| RF-04 | Limites iniciais | Máximo de 5 publicações ativas por artista; imagem otimizada para no máximo 300 KB; rejeitar formato/tamanho inválido. |
| RF-05 | Galeria pública | Listagem paginada apenas de publicações aprovadas, com detalhes e perfil do artista. |
| RF-06 | Apoio voluntário | Botão leva a endereço externo controlado pelo artista; a plataforma não recebe, guarda ou repassa valores. |
| RF-07 | Moderação | Moderador aprova/rejeita, oculta conteúdo e registra justificativa e data. |
| RF-08 | Denúncias e direitos autorais | Visitante pode reportar conteúdo por formulário; há canal para notificações de titulares e revisão humana. |
| RF-09 | Exclusão e privacidade | Artista pode solicitar exclusão de conta e publicações; política de privacidade e canal de contato disponíveis. |
| RF-10 | Administração | Acesso restrito a fila de moderação e denúncias. |

## 4. Requisitos não funcionais
| ID | Requisito | Critério de aceite |
|---|---|---|
| RNF-01 | Stack | Angular, ASP.NET Core, EF Core, PostgreSQL; versões fixadas na implementação. |
| RNF-02 | Segurança | Autorização por papel e titularidade; senha com hash adequado; validação de entrada; rate limiting; sem segredos no Git. |
| RNF-03 | Imagens | Armazenamento externo persistente, validação de extensão/MIME/conteúdo e tamanho, sem confiar no nome do arquivo. |
| RNF-04 | API | HTTP JSON, erros padronizados Problem Details, operações assíncronas e documentação OpenAPI. |
| RNF-05 | Testes | Testes unitários e de integração para cadastro, autorização, limites e moderação. |
| RNF-06 | Custos | Priorizar cotas gratuitas; nenhuma ativação de cobrança automática ou plano pago sem aprovação. |
| RNF-07 | Privacidade | Coletar somente dados necessários, informar finalidade e retenção, proteger dados pessoais e oferecer canal de solicitações LGPD. |
| RNF-08 | Acessibilidade | Navegação por teclado, textos alternativos e layout responsivo. |

## 5. Fora do MVP
Comentários, curtidas, seguidores, chat, pagamentos internos, comissão, anúncios, app mobile nativo, IA/MCP, venda de prints e encomendas.

## 6. Questões pendentes
Ver [decisions.md](decisions.md). A escolha de cadastro aberto não autoriza disponibilizar o sistema sem controles de abuso, privacidade e moderação.
