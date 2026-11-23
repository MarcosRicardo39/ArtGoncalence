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

## CI/CD planejado
GitHub Actions: restore, build, testes e geração de artefatos; deploy somente após autorização e configuração segura de credenciais. Não inferir que o projeto anterior DocumentAI autoriza publicação deste novo projeto.
