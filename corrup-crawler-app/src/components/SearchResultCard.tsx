import { type SearchResult } from "../types/SearchResult";

type Props = {
    result: SearchResult;
    highlightTerms: string[];
};

function Highlight({ text, terms }: { text: string; terms: string[] }) {
    if (terms.length === 0) return <>{text}</>;

    const pattern = terms
        .map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))
        .join("|");
    const regex = new RegExp(`(${pattern})`, "gi");
    const parts = text.split(regex);

    return (
        <>
            {parts.map((part, i) =>
                regex.test(part) ? (
                    <mark key={i} className="highlight">{part}</mark>
                ) : (
                    <span key={i}>{part}</span>
                )
            )}
        </>
    );
}

export function SearchResultCard({ result, highlightTerms }: Props) {
    const date = new Date(result.collectedAt).toLocaleDateString("pt-BR");

    return (
        <div className="result-card">
            <a
                href={result.url}
                target="_blank"
                rel="noreferrer"
                className="result-title"
            >
                {result.title}
            </a>

            <p className="result-url">{result.domain}</p>

            <p className="result-preview">
                <Highlight text={result.preview} terms={highlightTerms} />
            </p>

            <div className="result-footer">
                <span>Score: {result.score.toFixed(2)}</span>
                <span style={{ marginLeft: "16px" }}>Collected: {date}</span>
            </div>
        </div>
    );
}