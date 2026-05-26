import {  useState, type FormEvent } from "react";

type Props = {
    onSearch: (term: string) => void;
};

export function SearchBar({ onSearch }: Props) {
    const [term, setTerm] = useState("");

    function handleSubmit(e: FormEvent) {
        e.preventDefault();

        if (!term.trim()) {
            return;
        }

        onSearch(term);
    }

    return (
        <form className="search-form" onSubmit={handleSubmit}>
            <input
                type="text"
                placeholder="Pesquisar..."
                value={term}
                onChange={(e) => setTerm(e.target.value)}
                className="search-input"
            />

            <button type="submit" className="search-button">
                Buscar
            </button>
        </form>
    );
}