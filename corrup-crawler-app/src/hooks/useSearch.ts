import { useQuery } from "@tanstack/react-query";
import { api } from "../services/api";
import { type SearchFilters, type SearchResponse } from "../types/SearchResult";

export function useSearch(term: string, page: number = 1, filters: SearchFilters) {
    return useQuery<SearchResponse>({
        queryKey: ["search", term, page, filters],

        queryFn: async () => {
            const params: Record<string, string | number> = {
                q: term,
                page,
                pageSize: 10,
                sortBy: filters.sortBy,
            };

            if (filters.startDate) params.startDate = filters.startDate;
            if (filters.endDate) params.endDate = filters.endDate;

            const response = await api.get("/search", { params });
            return response.data;
        },

        enabled: !!term.trim(),
        placeholderData: (prev) => prev,
    });
}