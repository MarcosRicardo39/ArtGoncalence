# Decisões — ArtGoncalense

As decisões DA-01 a DA-07 foram aprovadas ou explicitamente adiadas em 23/11/2026. Resolver os pontos adiados antes das tarefas dependentes.

| ID | Decisão | Proposta | Estado |
|---|---|---|---|
| DA-01 | Nome público e identidade visual | ArtGoncalense é o nome definitivo | Identidade visual adiada até antes da T11 |
| DA-02 | Versões e nomenclatura | .NET 10 LTS, Angular 22 e Node.js 24 compatível. Campos de negócio em português e campos de Problem Details preservados | Aprovada. Fixar versões exatas na T02 |
| DA-03 | Identidade, autenticação e confirmação | ASP.NET Core Identity com cookie de sessão `HttpOnly`, `Secure` e `SameSite=Lax`; proteção CSRF nas operações que alteram estado e logout por `POST`. Confirmar e-mail antes de publicar. Cadastro válido recebe `202` genérico, inclusive para e-mail já cadastrado. | Cookie aprovado. Na T18, validar frontend e `/api` na mesma origem pública, dentro do orçamento. |
| DA-04 | Conteúdo, direitos autorais e denúncias | Moderação manual por Moderador ou Admin. Pendente pode ser aprovada ou rejeitada. Aprovada pode ser ocultada. Cada decisão exige justificativa e auditoria. A reabertura de rejeitadas ou ocultas fica adiada. | Fluxo operacional da T09 aprovado para validação local. Regras de denúncia antes da T10. Política pública de conteúdo e direitos autorais pendente de revisão jurídica. |
| DA-05 | E-mail e prevenção de spam | Modo local em desenvolvimento. Login: 20 solicitações por IP em 15 min e bloqueio de 15 min após 5 falhas consecutivas na conta. Cadastro, reenvio e solicitação de recuperação: 5 por IP por operação em 15 min e até 3 envios por endereço em uma hora, somados. | Limites aprovados. Provedor e cotas antes de qualquer teste público de cadastro. Validar IP real atrás do proxy e limites no ambiente de publicação. |
| DA-06 | Imagens e limite de publicações | JPEG, PNG e WebP estáticos, até 10.000.000 bytes e 4.096 px por lado. Saída WebP até 300.000 bytes, com proporção e transparência preservadas, sem metadados, lado maior até 1.600 px e qualidade mínima 75. O redimensionamento de originais maiores para em 1.024 px no lado maior. Até cinco publicações pendentes ou aprovadas por artista. Rejeitadas e ocultas liberam a vaga. | Processamento via Magick.NET, armazenamento local privado e estados que ocupam vaga aprovados. Validar provedor externo na T18. |
| DA-07 | Privacidade, idade mínima, retenção e exclusão | Usar dados fictícios em desenvolvimento | Definir política antes da T15 e de qualquer cadastro real |
| DA-08 | Links de apoio | URLs HTTPS de provedores externos; não armazenar chave Pix diretamente no MVP | Pendente |
| DA-09 | Publicação e custos | Usar serviços gratuitos apenas após verificar limites vigentes | Pendente |

## Observações
Fanart gratuita ou com doações não recebe automaticamente autorização de titulares de direitos autorais. Política e moderação reduzem riscos, mas não os eliminam. Caso se permita uso por menores de idade, exigências adicionais de proteção de dados e segurança deverão ser avaliadas antes da abertura pública.
