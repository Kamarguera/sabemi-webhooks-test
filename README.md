# Sabemi Webhooks — Technical Assessment

Sistema de processamento de notificações de pagamento via Webhook, desenvolvido como avaliação técnica para a L2.

[![CI](https://github.com/Kamarguera/sabemi-webhooks-test/actions/workflows/ci.yml/badge.svg)](https://github.com/Kamarguera/sabemi-webhooks-test/actions/workflows/ci.yml)

## Arquitetura

```
┌─────────────────┐     POST /webhooks/pagamento      ┌──────────────────────┐
│  Banco Parceiro  │ ─────────────────────────────────► │  .NET 10 Backend API  │
└─────────────────┘        (X-Api-Key)                 └──────────┬───────────┘
                                                                  │
                                              Channel (fila em memória)
                                                                  │
                                                    ┌─────────────▼─────────────┐
                                                    │  Worker em background      │
                                                    │  (IHostedService)          │
                                                    └─────────────┬─────────────┘
                                                                  │
                                                    ┌─────────────▼─────────────┐
                                                    │   PostgreSQL (Docker)      │
                                                    │  ┌─────────────────────┐  │
                                                    │  │  WebhookEvents (raw) │  │
                                                    │  ├─────────────────────┤  │
                                                    │  │   ContractStatus     │  │
                                                    │  └─────────────────────┘  │
                                                    └─────────────┬─────────────┘
                                                                  │
                                                    ┌─────────────▼─────────────┐
                                                    │     React Dashboard        │
                                                    │  Filtros | Status | Polling│
                                                    └───────────────────────────┘
```

### Estrutura do backend (camadas)

```
SabemiWebhooks/
├── Program.cs                       # Composition root (DI, provider do banco, pipeline)
├── Endpoints/
│   └── WebhookEndpoints.cs          # Rotas HTTP (apresentação)
├── Services/
│   ├── PaymentService.cs            # Regra de negócio: validação, idempotência, persistência
│   ├── PaymentQueue.cs              # Fila em memória (Channel) entre endpoint e worker
│   └── PaymentProcessingWorker.cs   # IHostedService: processamento pesado em background
├── Security/
│   └── ApiKeyFilter.cs              # Validação do header X-Api-Key (endpoint filter)
├── Data/
│   └── AppDbContext.cs              # EF Core: mapeamento e índice único de idempotência
├── Models/                          # Entidades (WebhookEvent, ContractStatus)
└── Contracts/                       # DTOs de entrada (PaymentRequest)

SabemiWebhooks.Tests/                # Testes de integração (xUnit + WebApplicationFactory)
```

## Funcionalidades

### Backend (.NET 10 + EF Core)
- **POST /webhooks/pagamento** — recebe notificações com `id_transacao`, `id_contrato`, `valor`, `data_pagamento`, `status`
- **Segurança** — validação de `X-Api-Key` via endpoint filter
- **Idempotência** — dupla proteção: verificação na aplicação + **índice único no banco** (garante consistência mesmo sob requisições concorrentes)
- **Validação** — payload inválido (campos vazios, valor ≤ 0) retorna `400 Bad Request`
- **Persistência** — `WebhookEvents` (log de eventos brutos) e `ContractStatus` (status consolidado por contrato)
- **Resiliência** — endpoint responde `202 Accepted` imediatamente; o processamento pesado (delay simulado de 2s) ocorre em background via `Channel` + `IHostedService`, com shutdown gracioso e registro de falhas no próprio evento
- **GET /webhooks/pagamentos** — listagem com filtros por `status` e `idContrato`

### Frontend (React + Vite)
- Dashboard com tabela de pagamentos recebidos
- Filtro por status (Sucesso/Erro) e por ID do Contrato
- Alerta visual para eventos com erro (linha vermelha + ⚠️) e para falha de conexão com a API
- Atualização automática a cada 5 segundos (polling)

## Como rodar

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org)
- [Docker](https://www.docker.com/) (para o PostgreSQL) — *opcional, veja fallback SQLite abaixo*

### 1. Banco de dados (PostgreSQL via Docker)

```bash
docker compose up -d
```

### 2. Backend

```bash
cd SabemiWebhooks
dotnet run
```
API disponível em `http://localhost:5000`

> **Sem Docker?** Altere `"Database": { "Provider": "Sqlite" }` no `appsettings.json` e rode normalmente — o banco vira um arquivo local, sem nenhuma infraestrutura.

### 3. Frontend

```bash
cd sabemi-frontend
npm install
npm run dev
```
Dashboard disponível em `http://localhost:5173`

## Testes

```bash
dotnet test
```

6 testes de integração sobem a API real em memória (WebApplicationFactory) e cobrem:
- Autenticação (401 sem ApiKey)
- Recebimento (202 Accepted)
- Idempotência (duplicado não grava duas vezes)
- Validação (400 para payload inválido)
- Filtros do GET (status e contrato)
- Processamento em background (evento marcado como processado pelo worker)

## Testando o endpoint manualmente

**Enviar webhook (Sucesso):**
```
curl -X POST http://localhost:5000/webhooks/pagamento -H "Content-Type: application/json" -H "X-Api-Key: sabemi-secret-key" -d "{\"idTransacao\":\"TX001\",\"idContrato\":\"CT100\",\"valor\":1500.00,\"dataPagamento\":\"2026-06-10T00:00:00\",\"status\":\"Sucesso\"}"
```

**Enviar webhook (Erro):**
```
curl -X POST http://localhost:5000/webhooks/pagamento -H "Content-Type: application/json" -H "X-Api-Key: sabemi-secret-key" -d "{\"idTransacao\":\"TX002\",\"idContrato\":\"CT200\",\"valor\":800.00,\"dataPagamento\":\"2026-06-10T00:00:00\",\"status\":\"Erro\"}"
```

**Testar idempotência (mesmo TX001 — deve retornar "Duplicado — ignorado."):**
```
curl -X POST http://localhost:5000/webhooks/pagamento -H "Content-Type: application/json" -H "X-Api-Key: sabemi-secret-key" -d "{\"idTransacao\":\"TX001\",\"idContrato\":\"CT100\",\"valor\":1500.00,\"dataPagamento\":\"2026-06-10T00:00:00\",\"status\":\"Sucesso\"}"
```

**Testar sem ApiKey (deve retornar 401):**
```
curl -X POST http://localhost:5000/webhooks/pagamento -H "Content-Type: application/json" -d "{\"idTransacao\":\"TX003\",\"idContrato\":\"CT100\",\"valor\":500.00,\"dataPagamento\":\"2026-06-10T00:00:00\",\"status\":\"Sucesso\"}"
```

**Listar todos os pagamentos:**
```
curl http://localhost:5000/webhooks/pagamentos
```

**Filtrar por status:**
```
curl http://localhost:5000/webhooks/pagamentos?status=Sucesso
```

**Filtrar por contrato:**
```
curl http://localhost:5000/webhooks/pagamentos?idContrato=CT100
```

## Decisões técnicas

| Decisão | Justificativa |
|---------|--------------|
| PostgreSQL via Docker (padrão) + SQLite (fallback) | Atende à especificação com infraestrutura reproduzível em um comando; o fallback SQLite permite rodar sem Docker. A troca é só configuração — o EF Core abstrai o provider |
| `Channel` + `IHostedService` para background | Fila em memória com shutdown gracioso e worker dedicado — padrão recomendado para processamento assíncrono in-process. Em produção distribuída, evoluiria para RabbitMQ/Kafka |
| Idempotência em duas camadas | O check na aplicação é o caminho rápido; o índice único no banco garante consistência sob concorrência (a corrida é tratada capturando `DbUpdateException`) |
| Minimal API em camadas (Endpoints/Services/Data) | Separação de responsabilidades sem o boilerplate de controllers; a regra de negócio fica testável e isolada do HTTP |
| Testes de integração com WebApplicationFactory | Testam o sistema real de ponta a ponta (HTTP → fila → worker → banco) em vez de mocks |
| Polling no frontend | Simplicidade para o escopo do teste; substituível por SignalR para real-time em produção |

---

Desenvolvido por **Augusto Cesar Camargo** — [LinkedIn](https://www.linkedin.com/in/accamargo/) | [GitHub](https://github.com/Kamarguera)
