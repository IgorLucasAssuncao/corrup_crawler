export interface SearchResult {
    id: string;
    url: string;
    domain: string;
    title: string;
    preview: string;
    score: number;
    collectedAt: string;
    matchedTerms: string[];
}

export interface SearchResponse {
    total: number;
    page: number;
    totalPages: number;
    results: SearchResult[];
}

export type SortOrder = "Relevance" | "Latest";

export interface SearchFilters {
    startDate: string;
    endDate: string;
    sortBy: SortOrder;
}

export interface RecentDocument {
    id: string;
    url: string;
    domain: string;
    title: string;
    preview: string;
    collectedAt: string;
}