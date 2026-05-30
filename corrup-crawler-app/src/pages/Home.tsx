import { useState } from "react";
import { SearchBar } from "../components/SearchBar";
import { SearchResultCard } from "../components/SearchResultCard";
import { SearchFilters } from "../components/SearchFilters";
import { RecentNewsGrid } from "../components/RecentsNewsGrid";
import { Loading } from "../components/Loading";
import { Paginations } from "../components/Paginations";
import { useSearch } from "../hooks/useSearch";
import { useRecentDocuments } from "../hooks/UseRecentDocuments";
import { type SearchFilters as SearchFiltersType } from "../types/SearchResult";

const DEFAULT_FILTERS: SearchFiltersType = {
    startDate: "",
    endDate: "",
    sortBy: "Relevance",
};

export function Home() {
    const [term, setTerm] = useState("");
    const [page, setPage] = useState(1);
    const [filters, setFilters] = useState<SearchFiltersType>(DEFAULT_FILTERS);

    const recentDocs = useRecentDocuments(12);
    const searchQuery = useSearch(term, page, filters);

    const highlightTerms = searchQuery.data?.results[0]?.matchedTerms ?? [];
    const showHome = !term;

    function handleSearch(newTerm: string) {
        setTerm(newTerm);
        setPage(1);
    }

    function handleFilters(newFilters: SearchFiltersType) {
        setFilters(newFilters);
        setPage(1);
    }

    return (
        <div className="container">
            <h1 className="main-title">
                Vigia <span className="brasilTitle">Brasil</span> 🕵️
            </h1>

            <SearchBar onSearch={handleSearch} />

            {/* ── Home state (no active search) ── */}
            {showHome && (
                <>
                    {recentDocs.isLoading && <Loading />}
                    {recentDocs.isError && (
                        <div className="error">Could not load recent news.</div>
                    )}
                    {recentDocs.data && recentDocs.data.length > 0 && (
                        <RecentNewsGrid documents={recentDocs.data} />
                    )}
                </>
            )}

            {/* ── Search results state ── */}
            {!showHome && (
                <>
                    <SearchFilters filters={filters} onChange={handleFilters} />

                    {searchQuery.isLoading && <Loading />}

                    {searchQuery.isError && (
                        <div className="error">
                            Error fetching results. Make sure the API is running.
                        </div>
                    )}

                    {searchQuery.data && searchQuery.data.total > 0 && (
                        <div className="results-count">
                            {searchQuery.data.total} result{searchQuery.data.total !== 1 ? "s" : ""} found
                            {" "}— page {searchQuery.data.page} of {searchQuery.data.totalPages}
                        </div>
                    )}

                    {searchQuery.data && searchQuery.data.total === 0 && !searchQuery.isLoading && (
                        <div className="results-count">
                            No results found for "<strong>{term}</strong>".
                        </div>
                    )}

                    <div className="results-container">
                        {searchQuery.data?.results.map((result) => (
                            <SearchResultCard
                                key={result.id}
                                result={result}
                                highlightTerms={highlightTerms}
                            />
                        ))}
                    </div>

                    {searchQuery.data && searchQuery.data.totalPages > 1 && (
                        <Paginations
                            paginaAtual={page}
                            totalPaginas={searchQuery.data.totalPages}
                            onMudar={setPage}
                        />
                    )}
                </>
            )}
        </div>
    );
}