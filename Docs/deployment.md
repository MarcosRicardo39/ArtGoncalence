# Implantação — ArtGoncalense

## Estado
**Planejamento somente. Nenhum recurso externo criado, custo aprovado ou deploy executado.**

## Proposta para ambiente de teste

Em 25/11/2026, a opção recomendada para um piloto fechado é um serviço web Render Free no workspace Hobby, com o Angular compilado e a API ASP.NET Core entregues pelo mesmo contêiner, e um projeto Supabase Free para PostgreSQL e armazenamento privado. Servir o Angular e `/api` na mesma origem HTTPS preserva o modelo de cookies e CSRF da prévia. O frontend e o backend continuam sendo projetos separados. Usar o subdomínio incluído pelo Render e um repositório GitHub com acesso mínimo, caso autorizado.

| Componente | Cota publicada | Impacto no piloto |
|---|---|---|
| Render Free | 0,1 CPU, 512 MB de RAM, 750 horas por workspace por mês e 5 GB de tráfego de saída no Hobby. Serviço dorme após 15 minutos sem tráfego e pode levar cerca de um minuto para voltar. | Medir memória do processamento de imagens no contêiner e começar com um upload por vez. Com 5 GB, cerca de 16 mil respostas de 300 kB consumiriam todo o tráfego, antes de contabilizar o Angular e a API. Sem meio de pagamento, o excesso suspende o serviço. [Computação](https://render.com/docs/compute-plans), [instâncias gratuitas](https://render.com/docs/free) e [tráfego](https://render.com/docs/outbound-bandwidth). |
| Supabase Free | 500 MB de banco por projeto, 1 GB de arquivos, 5 GB de saída e 5 GB de saída em cache. Pausa após uma semana de inatividade. Backups automáticos e recuperação pontual ficam fora do plano. | Usar bucket privado e download pela API após verificar visibilidade. O limite teórico de 1 GB comporta cerca de 3.300 imagens de 300 kB, antes da margem operacional. A API usa TLS com certificado validado. Se a ligação direta por IPv6 estiver indisponível, usar o pooler em modo sessão. [Preços](https://supabase.com/pricing), [conexões PostgreSQL](https://supabase.com/docs/guides/database/connecting-to-postgres) e [buckets privados](https://supabase.com/docs/guides/storage/buckets/fundamentals). |
| E-mail transacional | Brevo Free inclui até 300 envios/dia. O Resend limita o domínio de teste ao próprio endereço da conta. | Avaliar Brevo pela API HTTPS para destinatários de teste previamente autorizados. Confirmar remetente, entrega e ausência de cobrança antes de ativar. O Gmail de privacidade é um contato. Para envio público, validar domínio autenticado e definir o remetente em etapa própria. [Cota Brevo](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan), [autenticação do domínio](https://help.brevo.com/hc/en-us/articles/12163873383186-Authenticate-your-domain-with-Brevo-Brevo-code-DKIM-DMARC) e [restrição Resend](https://resend.com/docs/knowledge-base/403-error-resend-dev-domain). |

Cloudflare Pages permanece uma opção para os arquivos estáticos. Com uma API em outro host, exigiria configurar e validar um proxy na mesma origem. O PostgreSQL gratuito do Render tem prazo de expiração de 30 dias. Cloudflare R2 tem franquia gratuita de armazenamento, com cobrança por excedente, e o Supabase já oferece o armazenamento necessário ao piloto. Se a combinação recomendada exigir cartão, saldo pago ou exceder as cotas, manter a validação na prévia local e reapresentar alternativas. [Limites do Render](https://render.com/docs/free) e [preços do R2](https://developers.cloudflare.com/r2/pricing/).

## Preparação e critérios de aceite da T19

1. Confirmar as cotas e a ausência de meio de pagamento no painel dos provedores. Provisionar apenas após autorização explícita. Manter o ambiente limitado aos participantes do teste, usar dados fictícios e deixar o cadastro público fechado. Validar o bloqueio também na URL direta do backend.
2. Preparar empacotamento Angular + ASP.NET Core para Linux, usando a porta do ambiente e fallback do Angular apenas para rotas da interface. Servir `/health` pelo backend. Validar inicialização a frio, timeout e consumo de memória com imagens próximas aos limites de entrada.
3. Configurar e validar os adaptadores de storage privado e e-mail HTTPS. Confirmar gravação, leitura, exclusão e reconciliação, com credenciais apenas no servidor e teste de e-mail limitado aos participantes. Em desenvolvimento, manter armazenamento e correio locais.
4. Persistir e proteger as chaves de Data Protection fora do disco efêmero. Verificar sessão, confirmação e recuperação de senha após reinício. Aplicar cabeçalhos encaminhados apenas de proxies confiáveis e testar a origem do IP usado pelos limites. Rever cookies e CSRF sob HTTPS, assim como as permissões de artista, moderador e administrador.
5. Aplicar migrations explicitamente com credencial separada da aplicação. Antes de cada migração, exportar o PostgreSQL e copiar os objetos privados com inventário de chaves. Fazer exportações periódicas durante o piloto e guardar tudo em local criptografado com acesso restrito. Testar restauração isolada do dump e dos objetos, verificando que pedidos de exclusão já concluídos sejam respeitados antes de reabrir o ambiente. A exportação do banco não contém os arquivos de Storage. [Backups Supabase](https://supabase.com/docs/guides/platform/backups).
6. Acompanhar os painéis de uso. Como margem inicial do piloto, revisar consumo ao atingir 70% de banda, armazenamento ou cota de e-mail e pausar novos envios a partir de 85% se não houver margem para concluir o teste. Se um deploy falhar, retomar a versão anterior compatível com o esquema, verificar `/health` e repetir os fluxos de sessão, galeria e exclusão antes de liberar o acesso aos participantes.

## Empacotamento do piloto

O `Dockerfile` compila o Angular e a API e executa o contêiner na porta 10000 com usuário sem privilégios. O contêiner inclui os arquivos do Angular em `wwwroot`. Em modo piloto, o acesso à interface e à API exige HTTPS e credencial de entrada, além da sessão da aplicação nas operações autenticadas. A credencial de entrada só deve ser compartilhada com os participantes. `GET /health` fica disponível ao monitor do provedor.

Configurar `Piloto__Senha` no painel de segredos com um valor aleatório independente. O usuário da credencial de entrada é `piloto`. Quando TLS terminar no proxy, configurar os endereços de origem confiáveis em `Piloto__ProxiesConfiaveis__0` e índices seguintes. Validar a origem real do proxy antes de disponibilizar o piloto. Sem HTTPS reconhecido, a aplicação recusa o acesso à interface e à API.

Para publicar, validar as integrações com os provedores e o proxy confiável. Validar o plano sem meio de pagamento em cada painel antes de criar recursos.

### Configuração das integrações no Render

| Variável | Valor a configurar |
|---|---|
| `Imagens__Supabase__Url` | URL HTTPS do projeto, sem caminho adicional. |
| `Imagens__Supabase__Chave` | Chave secreta de servidor `sb_secret_…` de um projeto dedicado ao piloto. Manter apenas no painel do Render. |
| `Contas__Brevo__Chave` | Chave de API Brevo com permissão de envio transacional. |
| `Contas__Brevo__Remetente` | Endereço de remetente verificado no Brevo. |
| `Contas__Brevo__Destinatarios__0` | Primeiro endereço autorizado a receber e-mails no piloto. Use índices seguintes para outros participantes. |

Usar o bucket privado `ilustracoes`. A API lê os objetos pela rota autenticada do Storage e verifica a visibilidade da publicação antes de entregá-los. O adaptador grava WebP otimizado com nome aleatório e envia a exclusão ao Storage. A reconciliação local continua restrita ao armazenamento em disco. No piloto, verificar objetos sem referência antes da abertura e definir a rotina de reconciliação sem remover objetos recentes.

O transporte Brevo envia códigos de confirmação e redefinição somente para a lista do piloto e mantém a resposta de cadastro uniforme. Testar a entrega e a recuperação de senha com uma conta autorizada e confirmar a privacidade do bucket com uma tentativa de leitura sem credencial. [Chaves do Supabase](https://supabase.com/docs/guides/getting-started/api-keys) e [API transacional Brevo](https://developers.brevo.com/reference/send-transac-email).

### Chaves de sessão do piloto

Aplicar a migração `ChavesProtecaoPiloto` antes de iniciar o contêiner. A tabela `ChavesProtecao` usa RLS sem políticas públicas. O app grava as chaves de Data Protection no PostgreSQL, criptografadas com um certificado RSA de uso exclusivo do piloto. Configurar um PKCS#12 com chave privada, codificado em Base64, em `Piloto__CertificadoProtecao` no painel de segredos do Render.

Usar o mesmo certificado em novos deploys e manter uma cópia protegida separada do backup do banco. Uma troca sem acesso ao certificado anterior invalida as sessões e os códigos emitidos antes da troca. Testar login e confirmação após reinício e confirmar que a coluna XML contém material criptografado. [Configuração de Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

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
