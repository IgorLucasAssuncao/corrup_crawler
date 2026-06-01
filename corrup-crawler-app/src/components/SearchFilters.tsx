import { type SearchFilters, type SortOrder } from "../types/SearchResult";

type Props = {
    filters: SearchFilters;
    onChange: (filters: SearchFilters) => void;
};

export function SearchFilters({ filters, onChange }: Props) {
    function setField<K extends keyof SearchFilters>(field: K, value: SearchFilters[K]) {
        onChange({ ...filters, [field]: value });
    }

    function clearFilters() {
        onChange({ startDate: "", endDate: "", sortBy: "Relevance" });
    }

    const hasActiveFilters =
        filters.startDate || filters.endDate || filters.sortBy !== "Relevance";

    return (
        <div className="filters-bar">
            <div className="filter-group">
                <label className="filter-label" htmlFor="sortBy">Sort by</label>
                <select
                    id="sortBy"
                    className="filter-select"
                    value={filters.sortBy}
                    onChange={(e) => setField("sortBy", e.target.value as SortOrder)}
                >
                    <option value="Relevance">Relevance</option>
                    <option value="Latest">Latest</option>
                </select>
            </div>

            <div className="filter-group">
                <label className="filter-label" htmlFor="startDate">From</label>
                <input
                    id="startDate"
                    type="date"
                    className="filter-input"
                    value={filters.startDate}
                    max={filters.endDate || undefined}
                    onChange={(e) => setField("startDate", e.target.value)}
                />
            </div>

            <div className="filter-group">
                <label className="filter-label" htmlFor="endDate">To</label>
                <input
                    id="endDate"
                    type="date"
                    className="filter-input"
                    value={filters.endDate}
                    min={filters.startDate || undefined}
                    onChange={(e) => setField("endDate", e.target.value)}
                />
            </div>

            {hasActiveFilters && (
                <button className="filter-clear" onClick={clearFilters}>
                    Clear filters
                </button>
            )}
        </div>
    );
}