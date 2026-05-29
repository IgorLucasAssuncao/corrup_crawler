type Props = {
    paginaAtual: number;
    totalPaginas: number;
    onMudar: (pagina: number) => void;
};

export function Paginations({ paginaAtual, totalPaginas, onMudar }: Props) {
    if (totalPaginas <= 1) return null;

    // Gera o range de páginas visíveis (até 5, centrado na página atual)
    const range: number[] = [];
    const inicio = Math.max(1, paginaAtual - 2);
    const fim = Math.min(totalPaginas, paginaAtual + 2);
    for (let i = inicio; i <= fim; i++) range.push(i);

    return (
        <div className="pagination">
            <button
                className="pagination-btn"
                disabled={paginaAtual === 1}
                onClick={() => onMudar(paginaAtual - 1)}
            >
                ← Anterior
            </button>

            {inicio > 1 && (
                <>
                    <button className="pagination-btn" onClick={() => onMudar(1)}>1</button>
                    {inicio > 2 && <span className="pagination-ellipsis">…</span>}
                </>
            )}

            {range.map((p) => (
                <button
                    key={p}
                    className={`pagination-btn ${p === paginaAtual ? "pagination-btn--active" : ""}`}
                    onClick={() => onMudar(p)}
                >
                    {p}
                </button>
            ))}

            {fim < totalPaginas && (
                <>
                    {fim < totalPaginas - 1 && <span className="pagination-ellipsis">…</span>}
                    <button className="pagination-btn" onClick={() => onMudar(totalPaginas)}>
                        {totalPaginas}
                    </button>
                </>
            )}

            <button
                className="pagination-btn"
                disabled={paginaAtual === totalPaginas}
                onClick={() => onMudar(paginaAtual + 1)}
            >
                Próxima →
            </button>
        </div>
    );
}