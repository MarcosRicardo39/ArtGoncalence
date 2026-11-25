# Implantação — ArtGoncalense

## Estado
**Planejamento somente. Nenhum recurso externo criado, custo aprovado ou deploy executado.**

## Opção inicial a validar
- Código: GitHub.
- Front-end Angular: Cloudflare Pages.
- API ASP.NET Core em contêiner: Render (verificar suporte e limites vigentes antes de usar).
- PostgreSQL e arquivos: Supabase (verificar cotas, política de inatividade, limites e condições atuais).
- E-mail transacional: escolher fornecedor com plano e cotas adequados antes de habilitar cadastro real.

## Regras obrigatórias
1. Confirmar planos gratuitos atuais diretamente nos provedores antes de provisionar.
2. Não cadastrar cartão, habilitar faturamento, upgrade automático ou recurso pago sem aprovação explícita.
3. Usar variáveis de ambiente/secret store para credenciais e configurações.
4. Nunca guardar imagens em disco efêmero do contêiner.
5. Aplicar migrations de forma controlada, com estratégia de backup e rollback.
6. Restringir CORS, exigir HTTPS e verificar URLs de retorno da autenticação.
7. Definir retenção, exclusão e backup de imagens e dados pessoais.
8. Configurar limites de requisição/upload, logs sem dados sensíveis e monitoramento de cotas.
9. Testar registro, confirmação de e-mail, publicação, moderação, exclusão e link de apoio em ambiente de teste.
10. Se o plano gratuito não for adequado, suspender a publicação e apresentar alternativas; nunca migrar automaticamente para pago.

Para a sessão da T05, validar o acesso do navegador ao frontend e a `/api` pela mesma origem HTTPS. Preservar o IP do cliente somente por meio de proxies confiáveis e proteger as chaves de Data Protection usadas pelos cookies e códigos. Os limites por IP e por endereço de e-mail operam na memória de cada instância. Validar o comportamento após reinícios e entre instâncias antes da abertura pública.

## CI/CD planejado
GitHub Actions: restore, build, testes e geração de artefatos; deploy somente após autorização e configuração segura de credenciais. Não inferir que o projeto anterior DocumentAI autoriza publicação deste novo projeto.

## Operação de privacidade

Antes da abertura pública, definir a pessoa responsável e configurar um endereço monitorado em `Privacidade__EmailContato`. Conferir a política na página `/privacidade`. Usar somente dados fictícios para validar este fluxo localmente.

Revisar diariamente a fila administrativa e processar os pedidos até o prazo de 30 dias. Tratar os conflitos de conservação com a pessoa responsável, resolver denúncias abertas e documentar a justificativa antes de liberar registros. Executar diariamente a limpeza de contatos vencidos pela API administrativa. Definir retenção e restauração de backups de modo que dados excluídos não voltem à galeria após uma recuperação. Rotina e responsáveis devem estar definidos antes da abertura pública.
