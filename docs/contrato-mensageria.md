# Contrato de mensageria

Contrato único entre o `car-repair-saga-orchestrator` e os serviços `car-repair-os-service`, `car-repair-billing-service` e `car-repair-production-service`. Este arquivo é a fonte da verdade; os outros repositórios têm uma cópia em `docs/contrato-mensageria.md`. Qualquer mudança entra aqui primeiro e depois é replicada nos quatro repositórios.

## Visão geral

A saga é **orquestrada**. Os serviços publicam **eventos** sobre o que aconteceu no seu domínio; a saga consome esses eventos, decide o próximo passo e publica **comandos** para o serviço responsável. Nenhum serviço conversa diretamente com outro por mensageria, e nenhum acessa o banco de outro.

```
os-service ──ServiceOrderOpened──▶ saga ──CreateBudgetCommand──▶ billing
billing ──BudgetCreated / BudgetApproved──▶ saga ──ProcessPaymentCommand──▶ billing
billing ──PaymentApproved──▶ saga ──CreateWorkOrderCommand──▶ production
production ──WorkCompleted──▶ saga ──CloseServiceOrderCommand──▶ os-service
```

## Exchanges e filas

| Exchange | Tipo | Quem publica | Routing key |
|---|---|---|---|
| `car-repair.events` | topic, durável | os, billing, production | nome do evento (`ServiceOrderOpened`) |
| `car-repair.commands` | topic, durável | saga | nome do comando (`CreateBudgetCommand`) |

- A saga consome a fila `saga-orchestrator.events`, ligada a `car-repair.events` com uma binding por evento que ela trata.
- Cada serviço consome uma fila por comando, com o nome `<serviço>.<Comando>` (ex.: `billing-service.CreateBudgetCommand`), ligada a `car-repair.commands` pela routing key do comando.
- Falhas no consumo de comando seguem o retry do próprio serviço: fila `<fila>.retry` (exchange `car-repair.commands.retry`, com TTL) e, esgotadas as tentativas, `<fila>.dlq` (exchange `car-repair.commands.dlx`).

## Envelope

Toda mensagem, evento ou comando, é um JSON em camelCase com este formato:

```json
{
  "messageId": "6f1c2b0e-0c4e-4a43-9a51-2f0d0f6b1a10",
  "type": "ServiceOrderOpened",
  "correlationId": "3d9c4f8a-1b2e-4c6d-8e7f-001122334455",
  "occurredAtUtc": "2026-11-14T15:30:00Z",
  "payload": { }
}
```

| Campo | Regra |
|---|---|
| `messageId` | GUID novo por mensagem. Serve para log e idempotência. |
| `type` | Nome do evento ou comando, igual à routing key. |
| `correlationId` | **Sempre o id da ordem de serviço** (GUID no formato `D`). É o que liga todas as mensagens da mesma saga. Quem publica um evento precisa saber a qual OS ele pertence. |
| `occurredAtUtc` | Data/hora UTC em ISO 8601. |
| `payload` | Objeto JSON. Nunca string com JSON dentro. |

Propriedades AMQP: `content-type: application/json`, `delivery-mode: 2` (persistente), `message-id` = `messageId`, `correlation-id` = `correlationId`, `type` = `type`.

## Eventos (serviços → saga)

| Evento | Publicado por | Quando | Payload |
|---|---|---|---|
| `ServiceOrderOpened` | os | OS criada | `serviceOrderId`, `customerId`, `customerName`, `vehicleId`, `items[]` (`description`, `unitPrice`, `quantity`), `totalAmount` |
| `ServiceOrderClosed` | os | OS encerrada | `serviceOrderId` |
| `ServiceOrderCancelled` | os | OS cancelada | `serviceOrderId` |
| `BudgetCreated` | billing | orçamento gerado | `budgetId`, `totalAmount` |
| `BudgetApproved` | billing | cliente aprovou o orçamento | `budgetId`, `totalAmount`, `simulatePaymentFailure` |
| `BudgetRejected` | billing | cliente recusou o orçamento | `budgetId`, `reason` |
| `BudgetCancelled` | billing | orçamento cancelado por compensação | `budgetId`, `refundedPaymentIds[]` |
| `PaymentApproved` | billing | pagamento aprovado no gateway | `paymentId`, `budgetId`, `amount` |
| `PaymentRejected` | billing | pagamento recusado no gateway | `paymentId`, `budgetId`, `reason` |
| `PaymentRefunded` | billing | pagamento estornado por compensação | `paymentId`, `budgetId`, `amount` |
| `WorkOrderCreated` | production | ordem de execução entrou na fila | `workOrderId` |
| `WorkStarted` | production | execução começou | `workOrderId` |
| `WorkCompleted` | production | execução terminou | `workOrderId` |
| `WorkFailed` | production | execução falhou | `workOrderId`, `reason` |

Os eventos do billing também levam `serviceOrderId` no payload. Eventos que a saga não trata (`ServiceOrderClosed`, `ServiceOrderCancelled`, `BudgetCancelled`, `PaymentCreated`, `PaymentRefunded`, `WorkOrderCreated`, `WorkStarted`) são publicados para rastreio e observabilidade; a saga não cria binding para eles.

## Comandos (saga → serviços)

O payload de todo comando é o **contexto da saga**: a junção dos payloads de todos os eventos recebidos até ali, em que a chave mais recente vence. Assim cada serviço recebe tudo o que precisa sem consultar outro serviço. Por exemplo, o `CreateWorkOrderCommand` chega com `items` (vindo de `ServiceOrderOpened`), `budgetId` e `paymentId`.

| Comando | Consumido por | O que faz | Campos que usa |
|---|---|---|---|
| `CreateBudgetCommand` | billing | cria o orçamento da OS | `customerName`, `items[]` |
| `ProcessPaymentCommand` | billing | cobra o total do orçamento no gateway | `budgetId`, `simulatePaymentFailure` |
| `CreateWorkOrderCommand` | production | cria a ordem de execução | `items[]` |
| `CloseServiceOrderCommand` | os | encerra a OS | (só o `correlationId`) |
| `CancelServiceOrderCommand` | os | cancela a OS (compensação) | (só o `correlationId`) |
| `CancelBudgetCommand` | billing | cancela o orçamento e estorna pagamentos aprovados (compensação) | `budgetId` (se ausente, busca pela OS) |

## Fluxo e compensações

| Estado da saga | Evento | Próximo estado | Comando(s) |
|---|---|---|---|
| `Started` | `ServiceOrderOpened` | `WaitingBudget` | `CreateBudgetCommand` |
| `WaitingBudget` | `BudgetCreated` | `WaitingBudgetApproval` | — |
| `WaitingBudgetApproval` | `BudgetApproved` | `WaitingPayment` | `ProcessPaymentCommand` |
| `WaitingPayment` | `PaymentApproved` | `WaitingProduction` | `CreateWorkOrderCommand` |
| `WaitingProduction` | `WorkCompleted` | `Completed` | `CloseServiceOrderCommand` |
| `WaitingBudgetApproval` | `BudgetRejected` | `Compensating` → `Failed` | `CancelServiceOrderCommand` |
| `WaitingPayment` | `PaymentRejected` | `Compensating` → `Failed` | `CancelBudgetCommand`, `CancelServiceOrderCommand` |
| `WaitingProduction` | `WorkFailed` | `Compensating` → `Failed` | `CancelBudgetCommand`, `CancelServiceOrderCommand` |

## Regras para quem consome

- **Idempotência:** a mesma mensagem pode chegar mais de uma vez. Repetir um comando já aplicado não pode duplicar efeito (ex.: `CreateBudgetCommand` para uma OS que já tem orçamento não cria outro; `CancelServiceOrderCommand` para OS já cancelada é ignorado).
- **Ack só depois de persistir.** Erro transitório vai para retry; mensagem inválida (sem `correlationId`, payload sem campo obrigatório) vai direto para a DLQ.
- **Ações de sistema:** comandos vindos da saga não têm usuário. Regras de domínio que exigem usuário (como "só o responsável pode cancelar") precisam aceitar o ator de sistema nesses casos.
- **Simulação de falha:** `POST /budgets/{id}/approve` no billing aceita `{ "simulatePaymentFailure": true }`. O valor vai no `BudgetApproved`, entra no contexto da saga e faz o gateway recusar o pagamento, o que dispara a compensação. Serve para demonstrar o rollback.
- **Campos desconhecidos são ignorados**, para que um lado possa evoluir o payload sem quebrar o outro.
