import { useQuery } from "@tanstack/react-query";
import { api } from "../services/api";
import { type SearchResult } from "../types/SearchResult";

export function useSearch(term: string) {
    return useQuery<SearchResult[]>({
        queryKey: ["search", term],

        queryFn: async () => {
            if (!term) {
                return [];
            }

            const response = await api.get("/search", {
                params: {
                    q: term
                }
            });

            return response.data;
        },

        enabled: !!term
    });
}