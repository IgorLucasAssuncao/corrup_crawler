import { type RecentDocument } from "../types/SearchResult";

type Props = {
documents: RecentDocument[];
};

const styles = {
section: {
    marginTop: "48px",
    paddingTop: "32px",
    borderTop: "2px solid #1e293b",
} as React.CSSProperties,

header: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    marginBottom: "24px",
    paddingBottom: "12px",
    borderBottom: "1px solid #e5e7eb",
} as React.CSSProperties,

title: {
    fontSize: "20px",
    fontWeight: 700,
    color: "#1e293b",
    textTransform: "uppercase",
    letterSpacing: "0.05em",
    fontFamily: "Georgia, 'Times New Roman', serif",
} as React.CSSProperties,

subtitle: {
    fontSize: "13px",
    color: "#64748b",
    fontStyle: "italic",
} as React.CSSProperties,

grid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fill, minmax(340px, 1fr))",
    gap: "1px",
    backgroundColor: "#e5e7eb",
    border: "1px solid #e5e7eb",
} as React.CSSProperties,

card: {
    display: "flex",
    flexDirection: "column",
    padding: "20px 22px",
    backgroundColor: "#ffffff",
    textDecoration: "none",
    color: "inherit",
    transition: "background-color 0.2s ease",
    minHeight: "200px",
} as React.CSSProperties,

domain: {
    fontSize: "11px",
    fontWeight: 700,
    color: "#0f4c81",
    textTransform: "uppercase",
    letterSpacing: "0.08em",
    marginBottom: "10px",
    paddingBottom: "8px",
    borderBottom: "2px solid #0f4c81",
    alignSelf: "flex-start",
} as React.CSSProperties,

cardTitle: {
    fontSize: "16px",
    fontWeight: 600,
    color: "#0f172a",
    lineHeight: 1.4,
    marginBottom: "12px",
    fontFamily: "Georgia, 'Times New Roman', serif",
} as React.CSSProperties,

preview: {
    fontSize: "13px",
    color: "#475569",
    lineHeight: 1.6,
    flex: 1,
    display: "-webkit-box",
    WebkitLineClamp: 3,
    WebkitBoxOrient: "vertical",
    overflow: "hidden",
    marginBottom: "14px",
} as React.CSSProperties,

footer: {
    display: "flex",
    alignItems: "center",
    gap: "8px",
    paddingTop: "12px",
    borderTop: "1px solid #f1f5f9",
    fontSize: "12px",
    color: "#64748b",
} as React.CSSProperties,

dateIcon: {
    width: "12px",
    height: "12px",
    opacity: 0.6,
} as React.CSSProperties,
};

export function RecentNewsGrid({ documents }: Props) {
return (
    <section style={styles.section}>
        <div style={styles.header}>
            <h2 style={styles.title}>Últimas Publicações</h2>
            <span style={styles.subtitle}>
                {documents.length} {documents.length === 1 ? "registro" : "registros"}
            </span>
        </div>

        <div style={styles.grid}>
            {documents.map((doc) => {
                const date = new Date(doc.collectedAt).toLocaleDateString("pt-BR", {
                    day: "2-digit",
                    month: "long",
                    year: "numeric",
                });

                return (
                    <a
                        key={doc.id}
                        href={doc.url}
                        target="_blank"
                        rel="noreferrer"
                        style={styles.card}
                        onMouseEnter={(e) =>
                            (e.currentTarget.style.backgroundColor = "#f8fafc")
                        }
                        onMouseLeave={(e) =>
                            (e.currentTarget.style.backgroundColor = "#ffffff")
                        }
                    >
                        <span style={styles.domain}>{doc.domain}</span>
                        <h3 style={styles.cardTitle}>{doc.title || "Sem título"}</h3>
                        <p style={styles.preview}>{doc.preview}</p>
                        <div style={styles.footer}>
                            <svg
                                style={styles.dateIcon}
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2"
                            >
                                <rect x="3" y="4" width="18" height="18" rx="2" />
                                <line x1="16" y1="2" x2="16" y2="6" />
                                <line x1="8" y1="2" x2="8" y2="6" />
                                <line x1="3" y1="10" x2="21" y2="10" />
                            </svg>
                            <time>{date}</time>
                        </div>
                    </a>
                );
            })}
        </div>
    </section>
);
}