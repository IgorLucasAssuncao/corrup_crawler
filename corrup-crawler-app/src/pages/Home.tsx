import { useState } from "react";
import { SearchBar } from "../components/SearchBar";
import { SearchResultCard } from "../components/SearchResultCard";
import { SearchFilters } from "../components/SearchFilters";
import { RecentsNewsGrid } from "../components/RecentsNewsGrid";
import { Loading } from "../components/Loading";
import { Paginations } from "../components/Paginations";
import { useSearch } from "../hooks/useSearch";
import { useRecentDocuments } from "../hooks/UseRecentDocuments";
import { type SearchFilter } from "../types/SearchResult";

const FILTROS_PADRAO: SearchFilters = {
    dataInicio: "",
    dataFim: "",
    ordenacao: "Relevancia",
};

export function Home() {
    const [term, setTerm] = useState("");
    const [pagina, setPagina] = useState(1);
    const [filtros, setFiltros] = useState<SearchFilters>(FILTROS_PADRAO);

    const recentes = useRecentDocuments(12);
    const busca = useSearch(term, pagina, filtros);

    const termosDestaque = busca.data?.resultados[0]?.termosEncontrados ?? [];
    const exibirHome = !term;

    function handleSearch(novaTerm: string) {
        setTerm(novaTerm);
        setPagina(1);
    }

    function handleFiltros(novosFiltros: FiltrosBusca) {
        setFiltros(novosFiltros);
        setPagina(1);
    }

    return (
        <div className="container">
            <h1 className="main-title">
                Vigia <span className="brasilTitle">Brasil</span> 🕵️
            </h1>

            <SearchBar onSearch={handleSearch} />

            {/* ── Estado: home (sem busca ativa) ── */}
            {exibirHome && (
                <>
                    {recentes.isLoading && <Loading />}
                    {recentes.isError && (
                        <div className="error">Não foi possível carregar as notícias recentes.</div>
                    )}
                    {recentes.data && recentes.data.length > 0 && (
                        <RecentNewsGrid documents={recentes.data} />
                    )}
                </>
            )}

            {/* ── Estado: resultados de busca ── */}
            {!exibirHome && (
                <>
                    <SearchFilters filtros={filtros} onChange={handleFiltros} />

                    {busca.isLoading && <Loading />}

                    {busca.isError && (
                        <div className="error">
                            Erro ao buscar resultados. Verifique se a API está rodando.
                        </div>
                    )}

                    {busca.data && busca.data.total > 0 && (
                        <div className="results-count">
                            {busca.data.total} resultado{busca.data.total !== 1 ? "s" : ""} encontrado
                            {busca.data.total !== 1 ? "s" : ""} — página {busca.data.pagina} de {busca.data.totalPaginas}
                        </div>
                    )}

                    {busca.data && busca.data.total === 0 && !busca.isLoading && (
                        <div className="results-count">
                            Nenhum resultado encontrado para "<strong>{term}</strong>".
                        </div>
                    )}

                    <div className="results-container">
                        {busca.data?.resultados.map((result) => (
                            <SearchResultCard
                                key={result.id}
                                result={result}
                                termosDestaque={termosDestaque}
                            />
                        ))}
                    </div>

                    {busca.data && busca.data.totalPaginas > 1 && (
                        <Paginations
                            paginaAtual={pagina}
                            totalPaginas={busca.data.totalPaginas}
                            onMudar={setPagina}
                        />
                    )}
                </>
            )}
        </div>
    );
}