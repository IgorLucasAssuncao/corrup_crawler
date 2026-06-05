namespace CorruptionTracker.Api.Services;

/// <summary>
/// Static dictionaries for contextual analysis of corruption-related documents.
/// </summary>
public static class ContextDictionary
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Notable Brazilian politicians known for corruption scandals.
    /// </summary>
    public static readonly HashSet<string> Politicians = new(Comparer)
    {
        "lula", "luís inácio", "inácio lula",
        "bolsonaro", "jair bolsonaro",
        "cunha", "eduardo cunha",
        "cabral", "sergio cabral",
        "cláudio castro", "castro",
        "ciro nogueira",
        "aécio neves", "aécio",
        "joesley", "batista",
        "marcelo odebrecht", "odebrecht",
        "dilma", "dilma rousseff",
        "temer", "michel temer",
        "collor", "fernando collor",
        "sarney", "josé sarney",
        "paulão", "paulo",
        "valdemiro santiago",
        "dario raul",
        "palocci", "antonio palocci",
        "vaccari", "joão vaccari",
        "geddel", "geddel vieira",
        "guido mantega",
        "silval barbosa",
        "sérgio cabral", // variations
        "deputado", "senador", "governador", "ministro", "ex-presidente", "presidente",
        "vice-presidente", "vicepresident"
    };

    /// <summary>
    /// Major corruption operations and scandals.
    /// </summary>
    public static readonly HashSet<string> Operations = new(Comparer)
    {
        "lava jato", "lava-jato", "lavajato", "operação lava jato",
        "mensalão",
        "petrolão",
        "operação zelotes", "zelotes",
        "carne fraca", "operação carne fraca",
        "pf deflagra", "polícia federal",
        "operação",
        "encruzilhada",
        "captura",
        "lava toga",
        "vaza jato"
    };

    /// <summary>
    /// Government institutions (legitimate anti-corruption bodies).
    /// </summary>
    public static readonly HashSet<string> Institutions = new(Comparer)
    {
        "stf", "supremo tribunal federal",
        "stj", "superior tribunal de justiça",
        "tcu", "tribunal de contas",
        "cgu", "controladoria geral",
        "mpf", "ministério público federal",
        "tse", "tribunal eleitoral",
        "petrobras",
        "eletrobras",
        "bndes",
        "senado", "senado federal",
        "câmara", "câmara dos deputados",
        "congresso", "congresso nacional",
        "planalto",
        "carf", "conselho administrativo de recursos",
        "receita federal",
        "coaf", "conselho de atividades financeiras"
    };

    /// <summary>
    /// Terms that create ambiguity (e.g., "lava jato" could mean a car wash).
    /// When these appear with political context, document is NOT marked ambiguous.
    /// </summary>
    public static readonly HashSet<string> NonPoliticalContext = new(Comparer)
    {
        "tiros", "baleado", "assalto", "lava a jato",
        "lava-rápido", "estabelecimento", "cliente do lava",
        "dono do lava", "atropelamento", "incêndio",
        "morto", "assassinato", "ciúmes", "crime",
        "roubo", "furto", "morte"
    };

    /// <summary>
    /// Corruption keywords with weight (3=critical, 2=moderate, 1=basic).
    /// Title appearances are boosted 3x.
    /// </summary>
    public static readonly Dictionary<string, int> CorruptionKeywords = new(Comparer)
{
// ═══════════════════════════════════════════════════════════
// CRITICAL — Corrupção direta (peso 3)
// ═══════════════════════════════════════════════════════════
{ "corrupção", 3 },
{ "corrupto", 3 },
{ "corrupta", 3 },
{ "corruptos", 3 },
{ "corruptas", 3 },
{ "corromper", 3 },
{ "corrompido", 3 },
{ "propina", 3 },
{ "propinas", 3 },
{ "peculato", 3 },
{ "peculatos", 3 },
{ "lavagem de dinheiro", 3 },
{ "lavagem", 3 },
{ "lavar dinheiro", 3 },
{ "caixa dois", 3 },
{ "caixa-dois", 3 },
{ "desvio de verba", 3 },
{ "desvio de verbas", 3 },
{ "desvio de dinheiro", 3 },
{ "desvio de recursos", 3 },
{ "desvio", 3 },
{ "desviar", 3 },
{ "desviado", 3 },
{ "desviados", 3 },
{ "malversação", 3 },
{ "malversar", 3 },
{ "rachadinha", 3 },
{ "rachadinhas", 3 },
{ "mensalão", 3 },
{ "petrolão", 3 },
{ "lava jato", 3 },
{ "lava-jato", 3 },
{ "operação lava jato", 3 },
{ "esquema de corrupção", 3 },
{ "rede de corrupção", 3 },

// ═══════════════════════════════════════════════════════════
// CRITICAL — Crimes financeiros graves (peso 3)
// ═══════════════════════════════════════════════════════════
{ "evasão de divisas", 3 },
{ "evasão fiscal", 3 },
{ "sonegação", 3 },
{ "sonegação fiscal", 3 },
{ "sonegar", 3 },
{ "fraude bancária", 3 },
{ "lavagem de capitais", 3 },
{ "branqueamento de capitais", 3 },
{ "doleiro", 3 },
{ "doleiros", 3 },
{ "offshore", 3 },
{ "offshores", 3 },
{ "paraíso fiscal", 3 },
{ "conta secreta", 3 },
{ "conta no exterior", 3 },

// ═══════════════════════════════════════════════════════════
// MODERATE — Improbidade e crimes administrativos (peso 2)
// ═══════════════════════════════════════════════════════════
{ "improbidade", 2 },
{ "improbidade administrativa", 2 },
{ "ímprobo", 2 },
{ "superfaturamento", 2 },
{ "superfaturado", 2 },
{ "superfaturada", 2 },
{ "sobrepreço", 2 },
{ "delação premiada", 2 },
{ "delação", 2 },
{ "acordo de delação", 2 },
{ "colaboração premiada", 2 },
{ "enriquecimento ilícito", 2 },
{ "enriquecimento sem causa", 2 },
{ "organização criminosa", 2 },
{ "organizações criminosas", 2 },
{ "esquema", 2 },
{ "esquemas", 2 },
{ "esquema criminoso", 2 },
{ "licitação fraudulenta", 2 },
{ "fraude em licitação", 2 },
{ "cartel", 2 },
{ "cartelização", 2 },
{ "fraude", 2 },
{ "fraudes", 2 },
{ "fraudar", 2 },
{ "fraudado", 2 },
{ "fraudulento", 2 },
{ "fraudulenta", 2 },
{ "tráfico de influência", 2 },
{ "uso indevido", 2 },
{ "abuso de poder", 2 },
{ "abuso de autoridade", 2 },
{ "prevaricação", 2 },
{ "concussão", 2 },
{ "advocacia administrativa", 2 },
{ "estelionato", 2 },
{ "apropriação indébita", 2 },
{ "lobby ilegal", 2 },
{ "conflito de interesses", 2 },

// ═══════════════════════════════════════════════════════════
// MODERATE — Operações e ações policiais (peso 2)
// ═══════════════════════════════════════════════════════════
{ "operação policial", 2 },
{ "operação deflagrada", 2 },
{ "polícia federal", 2 },
{ "pf deflagra", 2 },
{ "mandado de busca", 2 },
{ "mandado de prisão", 2 },
{ "busca e apreensão", 2 },
{ "apreensão", 2 },
{ "interceptação telefônica", 2 },
{ "quebra de sigilo", 2 },
{ "denúncia anônima", 2 },
{ "operação", 2 },

// ═══════════════════════════════════════════════════════════
// BASIC — Investigação e processo (peso 1)
// ═══════════════════════════════════════════════════════════
{ "suborno", 1 },
{ "subornar", 1 },
{ "subornado", 1 },
{ "investigação", 1 },
{ "investigações", 1 },
{ "investigado", 1 },
{ "investigada", 1 },
{ "investigados", 1 },
{ "indiciado", 1 },
{ "indiciada", 1 },
{ "indiciamento", 1 },
{ "denúncia", 1 },
{ "denúncias", 1 },
{ "denunciar", 1 },
{ "denunciado", 1 },
{ "denunciante", 1 },
{ "preso", 1 },
{ "presa", 1 },
{ "presos", 1 },
{ "prisão", 1 },
{ "prisão preventiva", 1 },
{ "prisão temporária", 1 },
{ "condenado", 1 },
{ "condenada", 1 },
{ "condenação", 1 },
{ "absolvido", 1 },
{ "delator", 1 },
{ "delatores", 1 },
{ "delatar", 1 },
{ "réu", 1 },
{ "ré", 1 },
{ "réus", 1 },
{ "acusado", 1 },
{ "acusada", 1 },
{ "acusação", 1 },
{ "processo", 1 },
{ "processado", 1 },
{ "ação penal", 1 },
{ "inquérito", 1 },
{ "inquérito policial", 1 },
{ "ministério público", 1 },
{ "procurador", 1 },
{ "procuradoria", 1 },
{ "juiz", 1 },
{ "juíza", 1 },
{ "magistrado", 1 },
{ "sentença", 1 },
{ "habeas corpus", 1 },
{ "tribunal", 1 },
{ "supremo", 1 },
{ "stf", 1 },
{ "stj", 1 },
{ "trf", 1 },

// ═══════════════════════════════════════════════════════════
// BASIC — Contexto político e governamental (peso 1)
// ═══════════════════════════════════════════════════════════
{ "político", 1 },
{ "políticos", 1 },
{ "política", 1 },
{ "políticas", 1 },
{ "deputado", 1 },
{ "deputada", 1 },
{ "deputados", 1 },
{ "senador", 1 },
{ "senadora", 1 },
{ "senadores", 1 },
{ "governador", 1 },
{ "governadora", 1 },
{ "ex-governador", 1 },
{ "prefeito", 1 },
{ "prefeita", 1 },
{ "ex-prefeito", 1 },
{ "ministro", 1 },
{ "ministra", 1 },
{ "ex-ministro", 1 },
{ "presidente", 1 },
{ "ex-presidente", 1 },
{ "vereador", 1 },
{ "vereadora", 1 },
{ "secretário", 1 },
{ "ex-secretário", 1 },
{ "servidor público", 1 },
{ "agente público", 1 },
{ "funcionário público", 1 },
{ "partido", 1 },
{ "partidos", 1 },
{ "campanha eleitoral", 1 },
{ "doação eleitoral", 1 },
{ "financiamento de campanha", 1 },

// ═══════════════════════════════════════════════════════════
// BASIC — Termos de escândalo e mídia (peso 1)
// ═══════════════════════════════════════════════════════════
{ "escândalo", 1 },
{ "escândalos", 1 },
{ "irregularidade", 1 },
{ "irregularidades", 1 },
{ "ilegalidade", 1 },
{ "ilegal", 1 },
{ "ilícito", 1 },
{ "ilícita", 1 },
{ "ilícitos", 1 },
{ "crime", 1 },
{ "crimes", 1 },
{ "criminoso", 1 },
{ "criminosa", 1 },
{ "suspeita", 1 },
{ "suspeito", 1 },
{ "suspeitos", 1 },
{ "alvo", 1 },
{ "investigar", 1 },
{ "apurar", 1 },
{ "apuração", 1 },
{ "afastamento", 1 },
{ "afastado", 1 },
{ "cassação", 1 },
{ "cassado", 1 },
{ "impeachment", 1 },
{ "impedimento", 1 }
};

    /// <summary>
    /// Domain trust multipliers (1.0 = neutral, > 1.0 = boost, &lt; 1.0 = penalize).
    /// </summary>
    public static readonly Dictionary<string, double> DomainTrust = new(Comparer)
    {
        // Official sources (highest trust)
        { "cgu.gov.br", 1.7 },
        { "mpf.mp.br", 1.7 },
        { "tcu.gov.br", 1.7 },
        { "stf.jus.br", 1.7 },
        { "stj.jus.br", 1.7 },
        { "portaldatransparencia.gov.br", 1.7 },
        { "tse.jus.br", 1.6 },
        { "receita.fazenda.gov.br", 1.6 },

        // Investigative journalism
        { "apublica.org", 1.6 },
        { "theintercept.com", 1.6 },

        // Major news outlets
        { "g1.globo.com", 1.5 },
        { "folha.uol.com.br", 1.5 },
        { "estadao.com.br", 1.5 },
        { "oglobo.com.br", 1.4 },
        { "conjur.com.br", 1.3 },
        { "jusbrasil.com.br", 1.2 },

        // Penalized (low trust / aggregators)
        { "bing.com", 0.6 },
        { "google.com", 0.7 }
    };
}
