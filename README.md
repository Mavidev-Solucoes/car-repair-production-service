# car-repair-production-service
Esse projeto faz parte do Tech Challenge do curso de Arquitetura de Soluções da FIAP

## Responsabilidades e limites do microsserviço

O **Production Service** gerencia as ordens de execução associadas a uma ordem de serviço: criação, consulta, início, conclusão, falha e histórico de status da execução. A abertura da OS e as decisões financeiras pertencem aos serviços de OS e Billing.

## Arquitetura e persistência

- **API:** controllers REST em `src/Api/Controllers`; Swagger/OpenAPI em ambiente de desenvolvimento.
- **Application:** comandos e consultas com MediatR, validações e interfaces de persistência.
- **Domain:** entidade `ServiceOrderJob` e transições de estado de execução.
- **Infrastructure:** `MongoDB.Driver`, `MongoDbContext`, documentos, mapeamentos, repositório e integração RabbitMQ.
- **Banco próprio NoSQL:** MongoDB `production-mongodb` (banco `carRepairProductionDb`), independente dos bancos PostgreSQL de OS, Billing e Saga.
- **Integridade dos documentos:** índice único em `ServiceOrderId`, índice de consulta por data de criação e atualização com checagem de versão para impedir sobrescrita concorrente.

Os arquivos de implementação estão em [`src/Infrastructure/Persistence`](src/Infrastructure/Persistence) e a configuração Kubernetes em [`k8s/base`](k8s/base). Cada microsserviço utiliza apenas as credenciais de seu próprio banco.

## Participação na Saga e mensageria

O Production Service **consome** `CreateWorkOrderCommand` enviado pelo orquestrador via RabbitMQ e **publica** eventos sobre a execução, incluindo `WorkOrderCreated`, `WorkStarted`, `WorkCompleted` e `WorkFailed`. O orquestrador utiliza `WorkCompleted` para solicitar o fechamento da OS e `WorkFailed` para iniciar as compensações de Billing e OS.

As mensagens utilizam o `correlationId` da ordem de serviço. Consulte o [contrato de mensageria](docs/contrato-mensageria.md) para exchanges, routing keys, payloads e tratamento de falhas.

## Endpoints e testes

- `POST /api/work-orders`, `GET /api/work-orders` e `GET /api/work-orders/{workOrderId}`.
- `POST /api/work-orders/{workOrderId}/start`, `/complete` e `/fail`.
- Testes em `tests/UnitTests`, `tests/IntegrationTests` e `tests/InfrastructureTests`, incluindo testes do repositório MongoDB e mensageria.

## Evidência de testes e cobertura — FIAP Fase 4

- **Cobertura de linhas (assemblies de produção):** **95,41%** (mínimo exigido: 80%), medida no CI da branch `main` em **15/11/2026**.
- **Evidência:** [execução bem-sucedida do GitHub Actions CI — testes, cobertura, SonarCloud e Docker](https://github.com/Mavidev-Solucoes/car-repair-production-service/actions/runs/43690148460).
- **Fluxo distribuído:** [E2E Kubernetes com os quatro serviços e três cenários da Saga](https://github.com/Mavidev-Solucoes/car-repair-k8s-infra/actions/runs/43935058397).

A cobertura indicada é o resultado de uma execução identificada. O workflow `.github/workflows/ci.yml` verifica o limite mínimo de 80%.


## GitHub Actions CI - Secrets para SonarCloud

Para habilitar a análise no SonarCloud dentro do pipeline de CI, configure os seguintes secrets no repositório:

- `SONAR_TOKEN`: token de acesso do SonarCloud
- `SONAR_PROJECT_KEY`: chave do projeto no SonarCloud
- `SONAR_ORGANIZATION`: organização no SonarCloud

Sem esses secrets, o pipeline continua executando restore, build, testes, cobertura e validação de build Docker, mas pula a etapa de análise SonarCloud.

## GitHub Actions CD - Secrets para publicação no GHCR

Para publicar a imagem Docker no GitHub Container Registry (GHCR), o workflow de CD utiliza:

- `GITHUB_TOKEN` (automático do GitHub Actions): usado para autenticação no GHCR.

Não é necessário criar secrets adicionais para esse fluxo, desde que o workflow tenha permissão `packages: write`.

## Observabilidade (New Relic)

A imagem traz o agente .NET do New Relic com o profiler desligado; no Kubernetes o ConfigMap liga o agente (`NEW_RELIC_APP_NAME=car-repair-production-service`) e a license vem do Secret opcional `newrelic-license`.

O trace distribuído atravessa o RabbitMQ: `MessageTracing` grava `traceparent`/`tracestate` (W3C) e os headers do New Relic em cada mensagem publicada e, no consumo, abre a transação `RabbitMQ/<tipo>` que continua o trace de quem publicou, com o atributo `correlationId` (id da OS). Detalhes e consultas NRQL no README do `car-repair-k8s-infra`, seção "Observabilidade: New Relic APM e trace distribuído".
