import { useQuery } from "@tanstack/react-query";
import { api } from "../services/api";
import { type RecentDocument } from "../types/SearchResult";

export function useRecentDocuments(count = 12) {
    return useQuery<RecentDocument[]>({
        queryKey: ["recent-documents", count],
        queryFn: async () => {
            const response = await api.get("/documents/recent", {
                params: { count },
            });
            return response.data;
        },
        staleTime: 5 * 60 * 1000,
    });
}