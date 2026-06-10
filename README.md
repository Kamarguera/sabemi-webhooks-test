# Sabemi Webhooks — Technical Assessment

Sistema de processamento de notificações de pagamento via Webhook, desenvolvido como avaliação técnica para a L2.

## Arquitetura

```
┌─────────────────┐     POST /webhooks/pagamento      ┌──────────────────────┐
│   Banco Parceiro │ ─────────────────────────────────► │  .NET 10 Backend API  │
└─────────────────┘                                    └──────────┬───────────┘
                                                                  │
                                                    ┌─────────────▼─────────────┐
                                                    │        SQLite DB           │
                                                    │  ┌─────────────────────┐  │
                                                    │  │  webhook_events_raw  │  │
                                                    │  ├─────────────────────┤  │
                                                    │  │   contract_status    │  │
                                                    │  └─────────────────────┘  │
                                                    └───────────────────────────┘
                                                                  │
                                                    ┌─────────────▼─────────────┐
                                                    │     React Dashboard        │
                                                    │  Filtros | Status | Polling│
                                                    └───────────────────────────┘
```

## Funcionalidades

### Backend (.NET 10 + SQLite)
- **POST /webhooks/pagamento** — recebe notificações com `id_transacao`, `id_contrato`, `valor`, `data_pagamento`, `status`
- **Segurança** — validação de `X-Api-Key` no header da requisição
- **Idempotência** — o mesmo `id_transacao` nunca é processado duas vezes
- **Persistência** — dois modelos: `WebhookEvent` (log de eventos brutos) e `ContractStatus` (status consolidado por contrato)
- **Resiliência** — endpoint responde imediatamente com `202 Accepted`; o processamento pesado ocorre em background via `Task.Run` com delay simulado de 2s
- **GET /webhooks/pagamentos** — listagem com filtros por `status` e `idContrato`

### Frontend (React + Vite)
- Dashboard com tabela de pagamentos recebidos
- Filtro por status (Sucesso/Erro) e por ID do Contrato
- Alerta visual para eventos com erro (linha em vermelho)
- Atualização automática a cada 5 segundos (polling)

## Como rodar

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org)

### Backend

```bash
cd SabemiWebhooks
dotnet run
# API disponível em http://localhost:5261
```

### Frontend

```bash
cd sabemi-frontend
npm install
npm run dev
# Dashboard disponível em http://localhost:5173
```

### Testando o endpoint

```bash
# Enviar webhook
curl -X POST http://localhost:5261/webhooks/pagamento \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: sabemi-secret-key" \
  -d '{"idTransacao":"TX001","idContrato":"CT100","valor":1500.00,"dataPagamento":"2026-06-10T00:00:00","status":"Sucesso"}'

# Testar idempotência (mesmo TX001 — deve retornar "Duplicado — ignorado.")
curl -X POST http://localhost:5261/webhooks/pagamento \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: sabemi-secret-key" \
  -d '{"idTransacao":"TX001","idContrato":"CT100","valor":1500.00,"dataPagamento":"2026-06-10T00:00:00","status":"Sucesso"}'

# Sem ApiKey — deve retornar 401
curl -X POST http://localhost:5261/webhooks/pagamento \
  -H "Content-Type: application/json" \
  -d '{"idTransacao":"TX002","idContrato":"CT100","valor":500.00,"dataPagamento":"2026-06-10T00:00:00","status":"Sucesso"}'
```

## Decisões técnicas

| Decisão | Justificativa |
|---------|--------------|
| SQLite em vez de PostgreSQL | Facilita execução local sem infraestrutura adicional; substituível por PostgreSQL/SQL Server via uma linha no `appsettings.json` |
| `Task.Run` para background | Simples e eficaz para simular processamento pesado sem overhead de filas; em produção usaria `IHostedService` + Channel |
| EF Core Minimal API | Reduz boilerplate mantendo a clareza do código |
| Polling no frontend | Simplicidade para o escopo do teste; substituível por SignalR para real-time em produção |

## Estrutura do projeto

```
sabemi-webhooks-test/
├── SabemiWebhooks/          # Backend .NET
│   ├── Program.cs           # Endpoints + Models + DbContext
│   ├── appsettings.json     # Connection string + ApiKey
│   └── SabemiWebhooks.csproj
└── sabemi-frontend/         # Frontend React
    ├── src/
    │   └── App.jsx          # Dashboard principal
    └── package.json
```

---

Desenvolvido por **Augusto Cesar Camargo** — [LinkedIn](https://www.linkedin.com/in/accamargo/) | [GitHub](https://github.com/Kamarguera)
