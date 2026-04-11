# CorruptionTracker Crawler

Rastreador web que identifica e persiste documentos relacionados a corrupção em fontes jornalísticas e órgãos públicos brasileiros. Construído com .NET 10, Abot2, AngleSharp e MongoDB, orquestrado com Aspire AppHost.

## Requisitos

- Windows 10/11 com WSL2 habilitado
- .NET 10 SDK
- Podman Desktop (ou Docker)
- MongoDB 6.0+
- 8GB RAM mínimo

## Setup Rápido

### 1. Habilitar WSL2

```powershell
# PowerShell (Admin)
wsl --install
wsl --set-default-version 2
wsl --update
```

### 2. Instalar Podman

Baixe o instalador em [podman-desktop.io](https://podman-desktop.io)

Valide:
```bash
podman --version
```

### 3. Subir MongoDB

```bash
podman run -d \
  --name mongo \
  -p 27017:27017 \
  -e MONGO_INITDB_ROOT_USERNAME=admin \
  -e MONGO_INITDB_ROOT_PASSWORD=password \
  mongo:latest
```

### 4. Instalar .NET 10 (WSL2)

```bash
# Dentro do WSL2
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
echo "export PATH=$HOME/.dotnet:$PATH" >> ~/.bashrc
source ~/.bashrc
dotnet --version
```

### 5. Clonar e Rodar

```bash
git clone https://github.com/IgorLucasAssuncao/corrup_crawler.git
cd corruption_tracker
```

## Como Rodar

### Opção 1: AppHost (Recomendado)

Orquestra MongoDB + Crawler automaticamente:

```bash
cd corruption_tracker.AppHost
dotnet run
```

Acesse o dashboard em `http://localhost:18888`

### Opção 2: Local com MongoDB

```bash
# Garantir MongoDB rodando
podman ps | grep mongo

# Configurar connection string em CorruptionTracker.Crawler/appsettings.json
{
  "ConnectionStrings": {
    "mongodb": "mongodb://admin:password@localhost:27017/corruption_tracker?authSource=admin"
  }
}

# Rodar
cd CorruptionTracker.Crawler
dotnet run
```

## Arquitetura

### Estrutura

```
corruption_tracker/
├── CorruptionTracker.Crawler/
│   ├── Services/Crawler.cs          # BackgroundService principal
│   ├── Models/DocumentoCrawlado.cs  # Modelo MongoDB
│   ├── Program.cs
│   └── appsettings.json
├── CorruptionTracker.ServiceDefaults/
├── corruption_tracker.AppHost/
└── README.md
```

### Fluxo

AppHost → MongoDB (Podman) → Crawler (BackgroundService)

ExecuteAsync:
- RastrearSeedAsync (10 seeds em paralelo via Task.WhenAll)
- PoliteWebCrawler (respeita robots.txt)
- ProcessarPaginaAsync (extrai e pontuação)
- AngleSharp (parse HTML: h1, p, article)
- CalcularPontuacao (keywords com peso: 1-3 pontos)
- MongoDB Upsert (threshold: 2 pontos mínimo)
- Aguarda 24h e repete

### Seeds Monitoradas

1. G1 - https://g1.globo.com/
2. Folha de S.Paulo - https://www.folha.uol.com.br/
3. Estadão - https://www.estadao.com.br/
4. Agência Pública - https://apublica.org
5. The Intercept Brasil - https://theintercept.com/
6. Portal da Transparência - https://www.portaldatransparencia.gov.br
7. CGU - https://www.cgu.gov.br/
8. MPF - https://www.mpf.mp.br/
9. JusBrasil - https://www.jusbrasil.com.br/
10. Transparência Internacional - https://transparenciainternacional.org.br

### Palavras-Chave

| Palavra-chave | Pontos |
|---|---|
| corrupção, propina, peculato, lavagem de dinheiro | 3 |
| improbidade, superfaturamento | 2 |
| fraude, suborno, investigação, indiciado | 1 |

Threshold: 2 pontos mínimos para persistência

### Stack

- .NET 10 / C# 14.0
- Abot2 2.0.70 (Web crawler)
- AngleSharp 1.4.0 (HTML parsing)
- MongoDB.Driver 3.7.1 (Persistência)
- Aspire (Orquestração)

## Configuração

### Parâmetros do Crawler (Services/Crawler.cs)

```csharp
var config = new CrawlConfiguration
{
    MaxPagesToCrawl = 100_000,
    MaxCrawlDepth = 4,
    IsRespectRobotsDotTextEnabled = true,
    MinCrawlDelayPerDomainMilliSeconds = 1500,
    MaxConcurrentThreads = 10,
    UserAgentString = "CorruptionRI-Bot/1.0 (Academic)",
    CrawlTimeoutSeconds = 300
};
```

## Troubleshooting

### Podman não inicia

```bash
podman machine start
```

### MongoDB não conecta

```bash
podman ps | grep mongo
podman logs mongo
```

Validar string de conexão: `mongodb://admin:password@localhost:27017/?authSource=admin`

### Porta 27017 ocupada

```bash
podman stop mongo
podman rm mongo
# Reiniciar com porta diferente (-p 27018:27017)
```

### AppHost não inicia

```bash
dotnet workload restore
dotnet workload install aspire
cd corruption_tracker.AppHost
dotnet clean && dotnet restore && dotnet run
```

### Crawler não salva documentos

- MongoDB está rodando? `podman ps`
- Connection string correta em appsettings.json?
- Threshold (2 pontos) muito alto?
- robots.txt do site permite rastreamento?

Debug: aumentar log level para "Debug"

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Alto consumo de memória

```csharp
MaxConcurrentThreads = 5,      // Reduza de 10
MaxPagesToCrawl = 10_000,      // Reduza de 100_000
MaxCrawlDepth = 2,             // Reduza de 4
```

## O Que Pode Estar Faltando

### Crítico

- Health Check endpoint `/health` para MongoDB
- Graceful Shutdown com `IHostApplicationLifetime`
- CI/CD com GitHub Actions
- Docker Hub: publicar imagem

### Importante

- Unit Tests para `CalcularPontuacao()` e `ExtrairConteudo()`
- Retry policy com Polly
- Seeds e keywords em arquivo de configuração externo
- Índices MongoDB em `Url` e `ColetadoEm`
- Error Notifications (Slack, email)

### Futuro

- Web API com GET `/documentos`
- Dashboard UI
- Elasticsearch para full-text search
- Proxy rotation
- Webhook Notifications

## Monitoramento

### Aspire Dashboard

```
http://localhost:18888
```

Logs, métricas, status de serviços

### MongoDB Compass

Conecte em `mongodb://admin:password@localhost:27017/?authSource=admin`

Queries úteis:

```javascript
// Total de documentos
db.documentos.countDocuments()

// Top 10 por relevância
db.documentos.find().sort({ PontuacaoRelevancia: -1 }).limit(10)

// Score médio
db.documentos.aggregate([
  { $group: { _id: null, media: { $avg: "$PontuacaoRelevancia" } } }
])
```

## Referências

- [Abot2](https://github.com/sjdirect/abot2)
- [AngleSharp](https://anglesharp.com)
- [MongoDB.Driver](https://www.mongodb.com/docs/drivers/csharp/)
- [.NET 10](https://github.com/dotnet/release-notes)
- [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [WSL2](https://learn.microsoft.com/en-us/windows/wsl/install)
- [Podman](https://podman.io/docs/installation)

## Licença

MIT License

---

Igor Lucas Assunção | [GitHub](https://github.com/IgorLucasAssuncao)
