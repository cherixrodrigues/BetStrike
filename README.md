# BetStrike

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2019-CC2927?logo=microsoftsqlserver)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)
![Kafka](https://img.shields.io/badge/Apache%20Kafka-7.4-231F20?logo=apachekafka)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?logo=rabbitmq)
![License](https://img.shields.io/badge/License-MIT-green)

Plataforma fictícia de apostas desportivas desenvolvida no âmbito da unidade curricular de **Integração de Sistemas** (UTAD). A aplicação demonstra integração de múltiplos serviços via mensageria assíncrona (Kafka e RabbitMQ), APIs REST em ASP.NET e orquestração com Docker Compose.

---

## Arquitetura

```
                        ┌──────────────────────────────────────────────────────┐
                        │                  Docker Network                       │
                        │                                                        │
  Utilizador            │   ┌─────────────────┐     ┌──────────────────────┐   │
  ──────────── HTTP ───►│──►│  BetStrikeWeb   │     │      Dashboard       │   │
                        │   │  nginx :8080    │     │   nginx :8081        │   │
                        │   └────────┬────────┘     └──────────────────────┘   │
                        │            │ REST                   ▲                  │
                        │            ▼                        │ Kafka SSE        │
                        │   ┌─────────────────┐              │                  │
                        │   │  BetStrikeAPI   │──RabbitMQ───►│                  │
                        │   │  ASP.NET :53386 │              │                  │
                        │   └────────┬────────┘              │                  │
                        │            │ SQL                    │                  │
                        │            ▼                        │                  │
                        │   ┌─────────────────┐     ┌────────┴───────────────┐  │
                        │   │   SQL Server    │     │  PlataformaResultados  │  │
                        │   │   :1433         │◄────│  ASP.NET :5100         │  │
                        │   │  Apostas        │     │  Kafka Producer        │  │
                        │   │  Pagamentos     │     └────────────────────────┘  │
                        │   │  Resultados     │                                  │
                        │   └─────────────────┘                                  │
                        │                                                        │
                        │   ┌──────────┐   ┌───────────┐   ┌────────────────┐  │
                        │   │ RabbitMQ │   │   Kafka   │   │   Zookeeper    │  │
                        │   │  :5672   │   │  :29092   │   │    :2181       │  │
                        │   └──────────┘   └───────────┘   └────────────────┘  │
                        └──────────────────────────────────────────────────────┘
```

---

## Componentes

| Serviço | Tecnologia | Porta | Descrição |
|---|---|---|---|
| **BetStrikeAPI** | ASP.NET / .NET 10 | `53386` | API REST principal — apostas, pagamentos, utilizadores |
| **PlataformaResultados** | ASP.NET / .NET 10 | `5100` | API REST de resultados desportivos com Kafka producer |
| **BetStrikeWeb** | HTML + nginx | `8080` | Frontend da plataforma de apostas |
| **Dashboard** | HTML + nginx | `8081` | Painel de analytics em tempo real |
| **SQL Server 2019** | MSSQL | `1433` | Bases de dados: Apostas, Pagamentos, Resultados |
| **RabbitMQ** | AMQP | `5672` / `15672` | Filas assíncronas de apostas e pagamentos |
| **Apache Kafka** | Confluent 7.4 | `29092` | Streaming de eventos de jogos (topic: `game-events`) |

---

## Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (com WSL2 no Windows)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (para desenvolvimento local)
- SQL Server Management Studio ou Azure Data Studio (opcional)

---

## Setup

### 1. Clonar o repositório

```bash
git clone https://github.com/cherixrodrigues/BetStrike.git
cd BetStrike
```

### 2. Configurar as connection strings (opcional)

As connection strings estão pré-configuradas no `docker-compose.yml` para ambiente Docker. Para desenvolvimento local, edite `appsettings.json` nos projetos `BetStrikeAPI_1` e `PlataformaResultados`:

```json
{
  "ConnectionStrings": {
    "ApostasConnection":    "Server=localhost,1433;Database=Apostas;User Id=sa;Password=SuaSenhaForte123!;TrustServerCertificate=True;",
    "PagamentosConnection": "Server=localhost,1433;Database=Pagamentos;User Id=sa;Password=SuaSenhaForte123!;TrustServerCertificate=True;",
    "ResultadosConnection": "Server=localhost,1433;Database=Resultados;User Id=sa;Password=SuaSenhaForte123!;TrustServerCertificate=True;"
  }
}
```

### 3. Iniciar todos os serviços

```bash
cd Desktop/BetStrike-parte2
docker-compose up --build
```

Após o arranque, aceder a:
- **Frontend:** http://localhost:8080
- **Dashboard:** http://localhost:8081
- **BetStrikeAPI Swagger:** http://localhost:53386/swagger
- **PlataformaResultados Swagger:** http://localhost:5100/swagger
- **RabbitMQ Management:** http://localhost:15672 (guest / guest)

### 4. Inicializar as bases de dados

Execute os scripts SQL na seguinte ordem via SSMS ou sqlcmd:

```bash
# Ligar ao SQL Server (após docker-compose up)
sqlcmd -S localhost,1433 -U sa -P SuaSenhaForte123! -i BaseDados/Apostas.sql
sqlcmd -S localhost,1433 -U sa -P SuaSenhaForte123! -i BaseDados/Pagamentos.sql
sqlcmd -S localhost,1433 -U sa -P SuaSenhaForte123! -i BaseDados/Resultados.sql
sqlcmd -S localhost,1433 -U sa -P SuaSenhaForte123! -i BaseDados/Gatilho.sql
```

---

## Endpoints principais

### PlataformaResultados (`localhost:5100`)

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/jogos/inserir` | Inserir novo jogo (publica em Kafka + sincroniza com BetStrikeAPI) |
| `PUT` | `/api/jogos/atualizar` | Atualizar estado/resultado de um jogo |
| `GET` | `/api/jogos/listar` | Listar jogos (filtros: `?data=` e `?estado=`) |
| `GET` | `/api/jogos/{codigo}` | Obter detalhes de um jogo específico |
| `DELETE` | `/api/jogos/remover/{codigo}` | Remover jogo |

**Formato de código de jogo:** `FUT-AAAA-JJNN` (ex: `FUT-2026-0101`)

### BetStrikeAPI (`localhost:53386`)

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/api/jogos/inserir` | Sincronizar jogo na BD Apostas |
| `POST` | `/api/jogos/atualizar` | Atualizar resultado na BD Apostas e liquidar apostas |
| `GET` | `/api/apostas` | Listar apostas |
| `POST` | `/api/apostas` | Colocar aposta |
| `GET` | `/api/pagamentos` | Consultar pagamentos |

---

## Estrutura de pastas

```
BetStrike/
├── BaseDados/
│   ├── Apostas.sql          # Script BD Apostas (tabelas, SPs, triggers)
│   ├── Pagamentos.sql       # Script BD Pagamentos
│   ├── Resultados.sql       # Script BD Resultados
│   └── Gatilho.sql          # Triggers de automação entre BDs
│
└── Desktop/BetStrike-parte2/
    ├── BetStrikeAPI_1/      # API REST principal (apostas + pagamentos)
    │   └── Dockerfile
    ├── PlataformaResultados/ # API REST de resultados + Kafka producer
    │   ├── Controllers/
    │   │   └── JogosController.cs
    │   ├── Models/
    │   │   └── Jogos.cs
    │   ├── Services/
    │   │   └── KafkaProducerService.cs
    │   ├── Program.cs
    │   └── Dockerfile
    ├── BetStrikeWeb/        # Frontend HTML servido por nginx
    │   ├── index.html
    │   ├── nginx.conf
    │   └── Dockerfile
    ├── Dashboard/           # Painel analytics em tempo real
    │   └── dashboard.html
    ├── docker-compose.yml   # Orquestração de todos os serviços
    └── BetStrike.slnx       # Solução Visual Studio
```

---

## Tecnologias

| Categoria | Tecnologia |
|---|---|
| Backend | ASP.NET Core / .NET 10 / C# 12 |
| Base de Dados | Microsoft SQL Server 2019, Stored Procedures, Triggers |
| Mensageria | Apache Kafka 7.4 (Confluent), RabbitMQ 3 |
| Frontend | HTML5, CSS3, JavaScript |
| Servidor Web | nginx:alpine |
| Contenção | Docker, Docker Compose |
| ORM / DB Access | Microsoft.Data.SqlClient (ADO.NET direto) |
| Kafka Client | Confluent.Kafka |

---

## Fluxo de dados

1. Um resultado desportivo chega à **PlataformaResultados** via REST.
2. A API persiste o resultado na BD **Resultados** via Stored Procedure.
3. Simultaneamente, publica um evento no tópico Kafka `game-events`.
4. A **BetStrikeAPI** é notificada e atualiza a BD **Apostas**, liquidando as apostas abertas.
5. Os pagamentos são processados assincronamente via **RabbitMQ** e registados na BD **Pagamentos**.
6. O **Dashboard** consome os eventos Kafka em tempo real e apresenta analytics.

---

## Licença

Distribuído sob a licença MIT. Ver [LICENSE](LICENSE) para mais detalhes.
