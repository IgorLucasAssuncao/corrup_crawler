import { type RecentDocument } from "../types/SearchResult";

type Props = {
    documents: RecentDocument[];
};

export function RecentNewsGrid({ documents }: Props) {
    return (
        <div>
            <h2 className="recentes-titulo">Recent news</h2>
            <div className="recentes-grid">
                {documents.map((doc) => {
                    const date = new Date(doc.collectedAt).toLocaleDateString("pt-BR");
                    return (
                        <a
                            key={doc.id}
                            href={doc.url}
                            target="_blank"
                            rel="noreferrer"
                            className="recente-card"
                        >
                            <span className="recente-dominio">{doc.domain}</span>
                            <p className="recente-titulo">{doc.title}</p>
                            <p className="recente-preview">{doc.preview}</p>
                            <span className="recente-data">{date}</span>
                        </a>
                    );
                })}
            </div>
        </div>
    );
}