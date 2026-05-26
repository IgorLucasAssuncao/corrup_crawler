import { type SearchResult } from "../types/SearchResult";

type Props = {
    result: SearchResult;
};

export function SearchResultCard({ result }: Props) {
    const preview =
        result.texto.length > 250
            ? result.texto.substring(0, 250) + "..."
            : result.texto;

    return (
        <div className="result-card">
            <a
                href={result.url}
                target="_blank"
                rel="noreferrer"
                className="result-title"
            >
                {result.titulo}
            </a>

            <p className="result-preview">
                {preview}
            </p>

            <div className="result-footer">
                Relevância: {result.pontuacaoRelevancia}
            </div>
        </div>
    );
}