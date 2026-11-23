# Plano de implementação — ArtGoncalense

## 1. Regras de execução
**Estado: T01 a T03 concluídas. T04 a T20 pendentes.** Executar somente tarefas autorizadas; marcar concluída apenas após evidência. Não fazer deploy ou criar recursos externos por conta própria. Referências: requirements.md, architecture.md e specifications.md.

| ID | Tarefa | Dependências | Critério de conclusão |
|---|---|---|---|
| T01 | Confirmar decisões de produto e segurança | Nenhuma | DA-01 a DA-07 resolvidas ou explicitamente adiadas. |
| T02 | Preparar repositório e solution | T01 | Angular, .NET, xUnit, .gitignore e README criados; build limpo. |
| T03 | Criar Domain e modelo inicial | T02 | Entidades, enums, invariantes e testes básicos. |
| T04 | Configurar EF Core/PostgreSQL | T03 | DbContext, mapeamentos, migration e teste com banco local. |
| T05 | Criar autenticação e confirmação de e-mail | T04 | Cadastro, login, autorização e testes negativos. |
| T06 | Implementar perfil de artista | T05 | Leitura pública e edição somente pelo titular. |
| T07 | Integrar storage e processamento de imagens | T06 | Upload validado, otimização, limpeza de órfãos e testes. |
| T08 | Implementar publicação e limite de 5 | T07 | Estado PENDENTE, concorrência controlada e testes. |
| T09 | Implementar moderação | T08 | Aprovar/rejeitar/ocultar, trilha de auditoria e permissões. |
| T10 | Implementar denúncias | T09 | Canal funcional, antiabuso e tratamento de notificações. |
| T11 | Criar front-end de cadastro/perfil | T05,T06 | Fluxos completos e responsivos. |
| T12 | Criar galeria e envio pelo Angular | T08,T11 | Galeria paginada e upload com mensagens de erro. |
| T13 | Criar área de moderação no Angular | T09,T10,T12 | Acesso restrito e ações funcionais. |
| T14 | Integrar links de apoio | T06,T12 | URL validada e link externo seguro. |
| T15 | Privacidade, acessibilidade e proteção contra abuso | T10,T13,T14 | Revisão de políticas, segurança e testes. |
| T16 | Testes de integração e revisão de API | T15 | xUnit e cenários CT-01 a CT-19 aprovados. |
| T17 | Validar localmente | T16 | Build/test, fluxos manuais e evidências documentadas. |
| T18 | Planejar deploy gratuito | T17 | Cotas, limitações, backup e riscos aprovados; sem cobrança automática. |
| T19 | Publicar ambiente de teste | T18 e autorização explícita | Front, API, banco e storage configurados com HTTPS. |
| T20 | Validar publicação e abrir cadastro | T19 e autorização explícita | Smoke tests, moderação, privacidade, alertas e rollback verificados. |

## 2. Registro de execução
Ao concluir cada tarefa, anotar data, arquivos alterados, testes executados, resultado e decisões atualizadas. Se um requisito mudar, atualizar primeiro os documentos SDD afetados e reavaliar dependências.

- **2026-11-23 — T01.** Aprovação registrada em [decisions.md](decisions.md). Nome atualizado nos oito documentos e contrato de cadastro alinhado à resposta uniforme. Revisão documental das DA-01 a DA-07. Build e testes de software não se aplicam.
- **2026-11-23 — T02.** Git local inicializado. Solução .NET com API, Domain, Application, Infrastructure e projeto xUnit. Frontend Angular com lockfile, README e regras de ignore. SDK .NET 10.0.404, Angular 22.3.0, Node.js 24.21.0 e npm 11.19.0. Build .NET sem avisos e build Angular aprovados. A API respondeu ao `GET /openapi/v1.json` local. Dois testes Angular passaram. `dotnet test` executado com zero casos xUnit, previstos a partir da T03. Auditorias NuGet (incluindo transitivas) e npm sem vulnerabilidades conhecidas nas fontes consultadas.
- **2026-11-23 — T03.** Criados os modelos de usuário, link de apoio, ilustração, denúncia e ação de moderação, com enums e invariantes locais. Build .NET com zero avisos e erros. `dotnet test backend/ArtGoncalense.sln --no-build` aprovou 14 casos. A revisão confirmou estado inicial pendente para ilustrações, rejeição de identificadores de vínculo vazios e exigência de autoria e justificativa na ação de moderação. Autorização e políticas de publicação serão verificadas nas suas etapas.
