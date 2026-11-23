# Arquitetura — ArtGoncalense

## 1. Princípios
Monólito modular com API ASP.NET Core e front-end Angular separados. Fluxo de negócio: Angular → Controller → Service → Repository → EF Core DbContext → PostgreSQL. Registro de dependências no Program.cs não determina a ordem de execução das chamadas.

## 2. Estrutura proposta
```text
ArtGoncalense/
  frontend/                    # Angular
  backend/
    ArtGoncalense.sln
    src/
      ArtGoncalense.API/             # Controllers, DTOs, DI, autenticação, OpenAPI
      ArtGoncalense.Application/     # Casos de uso, serviços, interfaces
      ArtGoncalense.Domain/          # Entidades, regras e invariantes
      ArtGoncalense.Infrastructure/  # EF Core, repositórios, storage, e-mail
    tests/
      ArtGoncalense.Tests/           # xUnit
  Docs/                        # Estes documentos SDD
```

## 3. Dependências
- Domain não referencia outros projetos da solução.
- Application referencia Domain.
- Infrastructure referencia Application e Domain.
- API referencia Application e Infrastructure apenas para composição e configuração.
- Tests referencia os projetos sob teste.
- Sem referências circulares; sem lógica de negócio nos Controllers.

## 4. Componentes
- Identidade: ASP.NET Core Identity com papéis Artista/Moderador/Admin e sessão por cookie, conforme a DA-03. Operações que alteram estado exigem proteção CSRF.
- Persistência: PostgreSQL via EF Core e migrations versionadas.
- Imagens: Supabase Storage ou serviço compatível; arquivos fora do banco; banco guarda metadados e caminho.
- Apoio: URL externa cadastrada e validada; nenhuma transação financeira intermediada.
- Moderação: publicação com estado Pendente/Aprovada/Rejeitada/Oculta.
- E-mail: serviço externo para verificação, com modo local de desenvolvimento.

## 5. Modelo inicial
- Usuario: Id, Email, NomePublico, Bio, AvatarPath, Papel, Status, CriadoEm.
- LinkApoio: Id, UsuarioId, Url, Ativo.
- Ilustracao: Id, UsuarioId, Titulo, Descricao, Categoria, NomeArquivo, CaminhoStorage, MimeType, TamanhoBytes, Status, CriadoEm, AtualizadoEm.
- Denuncia: Id, IlustracaoId, Contato, Motivo, Descricao, Status, CriadoEm.
- AcaoModeracao: Id, IlustracaoId, ModeradorId, Acao, Justificativa, CriadoEm.

Na T03, os identificadores internos usam `Guid` e as datas usam `DateTimeOffset`. Ilustrações começam pendentes, com a data de atualização igual à de criação. A ação de moderação exige identificadores da ilustração e do moderador e uma justificativa. Biografia, avatar e contato de denúncia admitem ausência no modelo. O status de denúncia é armazenado como texto no domínio até a definição do fluxo da T10.

Na T04, o contexto persiste instantes em UTC, aplica integridade por chaves estrangeiras e restringe exclusões em cascata até a definição dos fluxos de exclusão. O índice de e-mail garante unicidade do valor armazenado. A normalização de identidade será tratada na T05. A API recebe a conexão por configuração e aplica migrations por comando explícito.

## 6. Segurança e fluxo
1. API autentica o usuário e identifica seu papel.
2. Controller valida o contrato HTTP e chama Application.
3. Application verifica titularidade, limites e regras.
4. Infrastructure persiste dados e arquivos com controle de falhas.
5. API devolve status HTTP e resposta sem expor dados sensíveis.

## 7. Integrações e limites
O armazenamento local da T07 usa um diretório privado configurado para desenvolvimento. O nome final é gerado pelo servidor e a gravação só fica disponível após mover o arquivo temporário, sem sobrescrita. A operação de reconciliação remove temporários e objetos sem referência de ilustração ou avatar com mais de 24 horas. Na integração da T08, compensar falhas de persistência confirmadas com exclusão idempotente e usar a reconciliação para interrupções ou resultados de commit incertos.

Não armazenar imagens no disco efêmero do servidor. Não expor credenciais de storage no Angular. Usar HTTPS e CORS restrito às origens aprovadas. Validar o provedor persistente e as cotas na T18.
