
using CorruptionTracker.Crawler.Models;
using Lucene.Net.Analysis.Br;
using Lucene.Net.Analysis.Pt;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace CorruptionTracker.Crawler.Services;

/// <summary>
/// Serviço de indexação em background.
/// Processa documentos pendentes (IndexadoEm == null), aplica análise léxica
/// (tokenização, remoção de stopwords, stemming PT-BR via Lucene) e constrói
/// o índice invertido com TF-IDF nas collections "vocabulario" e "postings".
/// </summary>
public class IndexBackgroundService : BackgroundService
{
    private readonly IMongoDatabase _db;
    private readonly ILogger<IndexBackgroundService> _logger;
    private readonly BrazilianAnalyzer _analyzer;

    // ══════════════════════════════════════════════════════════════
    //  HIPERPARÂMETROS
    // ══════════════════════════════════════════════════════════════

    /// <summary>Tamanho mínimo do token para ser indexado.</summary>
    private const int TokenMinLength = 3;

    /// <summary>Tamanho máximo do token para ser indexado.</summary>
    private const int TokenMaxLength = 40;

    /// <summary>Tamanho do lote para inserção de postings no MongoDB.</summary>
    private const int PostingsBatchSize = 10_000;

    /// <summary>Tamanho do lote para inserção de vocabulário no MongoDB.</summary>
    private const int VocabBatchSize = 5_000;

    /// <summary>Tamanho do lote para leitura de documentos do MongoDB.</summary>
    private const int DocReadBatchSize = 500;

    /// <summary>Intervalo entre ciclos de indexação.</summary>
    private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromMinutes(30);

    /// <summary>Delay inicial antes do primeiro ciclo (espera crawler popular).</summary>
    private static readonly TimeSpan DelayInicial = TimeSpan.FromSeconds(10);

    // ══════════════════════════════════════════════════════════════
    //  COLLECTIONS
    // ══════════════════════════════════════════════════════════════

    private IMongoCollection<DocumentoCrawlado> ColecaoDocs =>
        _db.GetCollection<DocumentoCrawlado>("documentos");

    private IMongoCollection<Vocabulario> ColecaoVocab =>
        _db.GetCollection<Vocabulario>("vocabulario");

    private IMongoCollection<PostingEntry> ColecaoPostings =>
        _db.GetCollection<PostingEntry>("postings");

    // ══════════════════════════════════════════════════════════════
    //  CONSTRUTOR
    // ══════════════════════════════════════════════════════════════

    public IndexBackgroundService(IMongoDatabase db, ILogger<IndexBackgroundService> logger)
    {
        _db = db;
        _logger = logger;

        // BrazilianAnalyzer já inclui:
        // 1. StandardTokenizer (tokenização)
        // 2. LowerCaseFilter (normalização)
        // 3. BrazilianStemFilter (stemming PT-BR)
        // 4. StopFilter com stopwords PT-BR (~200 palavras)
        _analyzer = new BrazilianAnalyzer(LuceneVersion.LUCENE_48);
    }

    // ══════════════════════════════════════════════════════════════
    //  EXECUÇÃO PRINCIPAL
    // ══════════════════════════════════════════════════════════════

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "IndexBackgroundService iniciado. Delay inicial: {Delay}min, Intervalo: {Intervalo}min",
            DelayInicial.TotalMinutes, IntervaloEntreCiclos.TotalMinutes);

        await Task.Delay(DelayInicial, ct);
        await CriarIndicesMongoAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await IndexarPendentesAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ciclo de indexação");
            }

            await Task.Delay(IntervaloEntreCiclos, ct);
        }

        _analyzer.Dispose();
        _logger.LogInformation("IndexBackgroundService finalizado");
    }

    // ══════════════════════════════════════════════════════════════
    //  INDEXAÇÃO DE DOCUMENTOS PENDENTES
    // ══════════════════════════════════════════════════════════════

    private async Task IndexarPendentesAsync(CancellationToken ct)
    {
        // 1. Buscar documentos com IndexadoEm == null
        var filtro = Builders<DocumentoCrawlado>.Filter.Eq(d => d.IndexadoEm, null);
        var totalPendentes = await ColecaoDocs.CountDocumentsAsync(filtro, cancellationToken: ct);

        if (totalPendentes == 0)
        {
            _logger.LogDebug("Nenhum documento pendente de indexação");
            return;
        }

        _logger.LogInformation("══════ INDEXAÇÃO INICIADA: {N} documentos pendentes ══════",
            totalPendentes);
        var sw = Stopwatch.StartNew();

        // 2. Total de docs (para cálculo do IDF)
        var totalDocsCorpus = await ColecaoDocs.CountDocumentsAsync(
            FilterDefinition<DocumentoCrawlado>.Empty, cancellationToken: ct);

        // 3. Processar em lotes via cursor
        var cursor = await ColecaoDocs.FindAsync(filtro,
            new FindOptions<DocumentoCrawlado> { BatchSize = DocReadBatchSize }, ct);

        var termosAfetados = new HashSet<string>();
        var postingsBatch = new List<PostingEntry>(PostingsBatchSize);
        int docsProcessados = 0;
        long totalTokensGerados = 0;

        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var doc in cursor.Current)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(doc.Texto)) continue;

                // ┌─────────────────────────────────────────────┐
                // │  Limpar postings antigos (caso re-crawl)    │
                // └─────────────────────────────────────────────┘
                await RemoverPostingsAntigosAsync(doc.HashUrl, ct);

                // ┌─────────────────────────────────────────────┐
                // │  Análise Léxica: Tokenização + Stop + Stem  │
                // └─────────────────────────────────────────────┘
                var tokens = Tokenizar(doc.Texto);
                totalTokensGerados += tokens.Count;

                if (tokens.Count == 0)
                {
                    await MarcarComoIndexadoAsync(doc.HashUrl, ct);
                    continue;
                }

                // ┌─────────────────────────────────────────────┐
                // │  Contagem de frequências e posições          │
                // └─────────────────────────────────────────────┘
                var frequencias = ContarFrequencias(tokens);

                // ┌─────────────────────────────────────────────┐
                // │  Upsert no vocabulário + acumular postings   │
                // └─────────────────────────────────────────────┘
                foreach (var (termo, info) in frequencias)
                {
                    termosAfetados.Add(termo);

                    await ColecaoVocab.UpdateOneAsync(
                        Builders<Vocabulario>.Filter.Eq(v => v.Termo, termo),
                        Builders<Vocabulario>.Update
                            .Inc(v => v.Df, 1)
                            .Inc(v => v.Cf, info.Frequencia)
                            .SetOnInsert(v => v.Termo, termo),
                        new UpdateOptions { IsUpsert = true },
                        ct);

                    postingsBatch.Add(new PostingEntry
                    {
                        Termo = termo,
                        DocHash = doc.HashUrl,
                        Tf = info.Frequencia,
                        Posicoes = info.Posicoes
                    });
                }

                // ┌─────────────────────────────────────────────┐
                // │  Flush postings batch quando cheio           │
                // └─────────────────────────────────────────────┘
                if (postingsBatch.Count >= PostingsBatchSize)
                {
                    await InserirPostingsBatchAsync(postingsBatch, ct);
                    postingsBatch.Clear();
                }

                // ┌─────────────────────────────────────────────┐
                // │  Marcar documento como indexado              │
                // └─────────────────────────────────────────────┘
                await MarcarComoIndexadoAsync(doc.HashUrl, ct);
                docsProcessados++;

                if (docsProcessados % 100 == 0)
                    _logger.LogInformation("Progresso: {N}/{Total} docs processados",
                        docsProcessados, totalPendentes);
            }
        }

        // 4. Flush postings restantes
        if (postingsBatch.Count > 0)
            await InserirPostingsBatchAsync(postingsBatch, ct);

        // 5. Recalcular IDF e TF-IDF dos termos afetados
        await RecalcularIdfAsync(termosAfetados, totalDocsCorpus, ct);

        sw.Stop();
        _logger.LogInformation(
            "══════ INDEXAÇÃO COMPLETA ══════\n" +
            "  Documentos processados: {Docs}\n" +
            "  Tokens gerados: {Tokens}\n" +
            "  Termos únicos afetados: {Termos}\n" +
            "  Tempo total: {Tempo:F1}s\n" +
            "  Velocidade: {Vel:F0} docs/s",
            docsProcessados,
            totalTokensGerados,
            termosAfetados.Count,
            sw.Elapsed.TotalSeconds,
            docsProcessados / Math.Max(sw.Elapsed.TotalSeconds, 0.001));
    }

    // ══════════════════════════════════════════════════════════════
    //  ANÁLISE LÉXICA (Lucene BrazilianAnalyzer)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pipeline de análise léxica via Lucene BrazilianAnalyzer:
    /// 1. StandardTokenizer → separa por espaços/pontuação
    /// 2. LowerCaseFilter → normaliza para minúsculas
    /// 3. StopFilter → remove stopwords PT-BR (~200 palavras)
    /// 4. BrazilianStemFilter → aplica stemming PT-BR
    /// 
    /// Exemplo:
    ///   Input:  "As investigações sobre corrupção na Petrobras continuam em andamento"
    ///   Output: ["investig", "corrupt", "petrobras", "continu", "andam"]
    ///   
    ///   Removidos: "as" (stopword), "sobre" (stopword), "na" (stopword), "em" (stopword)
    ///   Stemmed: investigações→investig, corrupção→corrupt, continuam→continu
    /// </summary>
    private List<string> Tokenizar(string texto)
    {
        var tokens = new List<string>();

        using var reader = new StringReader(texto);
        using var tokenStream = _analyzer.GetTokenStream("content", reader);

        var termAttr = tokenStream.GetAttribute<ICharTermAttribute>();
        tokenStream.Reset();

        while (tokenStream.IncrementToken())
        {
            var token = termAttr.ToString();

            // Filtros adicionais pós-Lucene
            if (token.Length < TokenMinLength) continue;
            if (token.Length > TokenMaxLength) continue;
            if (token.All(char.IsDigit)) continue;      // Remove tokens puramente numéricos

            tokens.Add(token);
        }

        tokenStream.End();
        return tokens;
    }

    // ══════════════════════════════════════════════════════════════
    //  CONTAGEM DE FREQUÊNCIAS E POSIÇÕES
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Conta frequência e registra posições de cada termo no documento.
    /// Necessário para:
    /// - TF (Term Frequency) → cálculo do TF-IDF
    /// - Posições → busca por frase (proximity search)
    /// </summary>
    private static Dictionary<string, TermoLocal> ContarFrequencias(List<string> tokens)
    {
        var frequencias = new Dictionary<string, TermoLocal>();

        for (int posicao = 0; posicao < tokens.Count; posicao++)
        {
            var termo = tokens[posicao];

            if (!frequencias.TryGetValue(termo, out var info))
            {
                info = new TermoLocal();
                frequencias[termo] = info;
            }

            info.Frequencia++;
            info.Posicoes.Add(posicao);
        }

        return frequencias;
    }

    // ══════════════════════════════════════════════════════════════
    //  REMOÇÃO DE POSTINGS ANTIGOS (re-crawl)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Remove postings anteriores de um documento re-crawleado
    /// e decrementa os contadores no vocabulário.
    /// </summary>
    private async Task RemoverPostingsAntigosAsync(string docHash, CancellationToken ct)
    {
        var filtroPostings = Builders<PostingEntry>.Filter.Eq(p => p.DocHash, docHash);
        var postingsAntigos = await ColecaoPostings
            .Find(filtroPostings)
            .ToListAsync(ct);

        if (postingsAntigos.Count == 0) return;

        _logger.LogDebug("Removendo {N} postings antigos do doc {Hash}",
            postingsAntigos.Count, docHash[..8]);

        // Decrementar contadores no vocabulário
        var agrupado = postingsAntigos
            .GroupBy(p => p.Termo)
            .Select(g => new { Termo = g.Key, Df = 1, Cf = g.Sum(p => p.Tf) });

        foreach (var item in agrupado)
        {
            await ColecaoVocab.UpdateOneAsync(
                Builders<Vocabulario>.Filter.Eq(v => v.Termo, item.Termo),
                Builders<Vocabulario>.Update
                    .Inc(v => v.Df, -item.Df)
                    .Inc(v => v.Cf, -item.Cf),
                cancellationToken: ct);
        }

        // Remover postings
        await ColecaoPostings.DeleteManyAsync(filtroPostings, ct);
    }

    // ══════════════════════════════════════════════════════════════
    //  RECALCULAR IDF E TF-IDF
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Recalcula IDF = log(N/df) para cada termo afetado
    /// e atualiza TF-IDF = TF × IDF nos postings correspondentes.
    /// 
    /// IDF alto → termo raro → mais discriminativo
    /// IDF baixo → termo comum → menos relevante
    /// </summary>
    private async Task RecalcularIdfAsync(
        HashSet<string> termosAfetados, long totalDocs, CancellationToken ct)
    {
        _logger.LogInformation("Recalculando IDF para {N} termos...", termosAfetados.Count);
        int processados = 0;

        foreach (var termo in termosAfetados)
        {
            if (ct.IsCancellationRequested) break;

            var vocab = await ColecaoVocab
                .Find(v => v.Termo == termo)
                .FirstOrDefaultAsync(ct);

            if (vocab == null || vocab.Df <= 0) continue;

            // IDF = log(N / df)
            var novoIdf = Math.Log((double)totalDocs / vocab.Df);

            // Atualizar IDF no vocabulário
            await ColecaoVocab.UpdateOneAsync(
                v => v.Termo == termo,
                Builders<Vocabulario>.Update.Set(v => v.Idf, novoIdf),
                cancellationToken: ct);

            // Atualizar TF-IDF nos postings: TfIdf = Tf * IDF
            var pipelineUpdate = Builders<PostingEntry>.Update.Pipeline(
                PipelineDefinition<PostingEntry, PostingEntry>.Create(
                    new BsonDocument("$set", new BsonDocument("TfIdf",
                        new BsonDocument("$multiply", new BsonArray { "$Tf", novoIdf })))));

            await ColecaoPostings.UpdateManyAsync(
                Builders<PostingEntry>.Filter.Eq(p => p.Termo, termo),
                pipelineUpdate,
                cancellationToken: ct);

            processados++;
            if (processados % 1000 == 0)
                _logger.LogDebug("IDF recalculado: {N}/{Total}", processados, termosAfetados.Count);
        }

        // Limpar termos com df=0 (foram completamente removidos)
        await ColecaoVocab.DeleteManyAsync(
            Builders<Vocabulario>.Filter.Lte(v => v.Df, 0), ct);
    }

    // ══════════════════════════════════════════════════════════════
    //  PERSISTÊNCIA
    // ══════════════════════════════════════════════════════════════

    private async Task InserirPostingsBatchAsync(List<PostingEntry> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        await ColecaoPostings.InsertManyAsync(batch,
            new InsertManyOptions { IsOrdered = false }, ct);

        _logger.LogDebug("Inseridos {N} postings", batch.Count);
    }

    private async Task MarcarComoIndexadoAsync(string docHash, CancellationToken ct)
    {
        await ColecaoDocs.UpdateOneAsync(
            d => d.HashUrl == docHash,
            Builders<DocumentoCrawlado>.Update.Set(d => d.IndexadoEm, DateTime.UtcNow),
            cancellationToken: ct);
    }

    // ══════════════════════════════════════════════════════════════
    //  ÍNDICES MONGODB
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Cria índices para performance de busca e atualização.
    /// Executado uma única vez na inicialização.
    /// </summary>
    private async Task CriarIndicesMongoAsync(CancellationToken ct)
    {
        _logger.LogInformation("Verificando índices MongoDB...");

        // Busca por termo + ordenação por score
        await ColecaoPostings.Indexes.CreateOneAsync(
            new CreateIndexModel<PostingEntry>(
                Builders<PostingEntry>.IndexKeys
                    .Ascending(p => p.Termo)
                    .Descending(p => p.TfIdf),
                new CreateIndexOptions { Name = "idx_termo_tfidf" }),
            cancellationToken: ct);

        // Remoção/atualização de postings por documento
        await ColecaoPostings.Indexes.CreateOneAsync(
            new CreateIndexModel<PostingEntry>(
                Builders<PostingEntry>.IndexKeys
                    .Ascending(p => p.DocHash),
                new CreateIndexOptions { Name = "idx_dochash" }),
            cancellationToken: ct);

        // Busca de documentos pendentes de indexação
        await ColecaoDocs.Indexes.CreateOneAsync(
            new CreateIndexModel<DocumentoCrawlado>(
                Builders<DocumentoCrawlado>.IndexKeys
                    .Ascending(d => d.IndexadoEm),
                new CreateIndexOptions { Name = "idx_indexado_em" }),
            cancellationToken: ct);

        _logger.LogInformation("Índices MongoDB verificados/criados");
    }

    // ══════════════════════════════════════════════════════════════
    //  CLASSE AUXILIAR
    // ══════════════════════════════════════════════════════════════

    private sealed class TermoLocal
    {
        public int Frequencia { get; set; }
        public List<int> Posicoes { get; set; } = [];
    }
}