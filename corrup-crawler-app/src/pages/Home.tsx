import { useState } from "react";
import { SearchBar } from "../components/SearchBar";
import { SearchResultCard } from "../components/SearchResultCard";
import { Loading } from "../components/Loading";
import { useSearch } from "../hooks/useSearch";

export function Home() {
    const [term, setTerm] = useState("");

    const { data, isLoading, isError } = useSearch(term);

    return (
        <div className="container">
            <h1 className="main-title">
                Vigia <span className="brasilTitle">Brasil</span> 🕵️
            </h1>

            <SearchBar onSearch={setTerm} />

            {isLoading && <Loading />}

            {isError && (
                <div className="error">
                    Erro ao buscar resultados.
                </div>
            )}

            {data && (
                <div className="results-count">
                    Resultados encontrados: {data.length}
                </div>
            )}

            <div className="results-container">
                {data?.map((result) => (
                    <SearchResultCard
                        key={result.id}
                        result={result}
                    />
                ))}
            </div>

            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
            <div className="result-card">
                <a
                    target="_blank"
                    rel="noreferrer"
                    className="result-title"
                    href="https://g1.globo.com/rj/rio-de-janeiro/noticia/2026/05/26/claudio-castro-trocou-cupula-da-rioprevidencia-antes-de-fundo-investir-no-master-diz-pf.ghtml"
                >
                    Cláudio Castro trocou cúpula da Rioprevidência antes de fundo investir R$ 3,7 bilhões no Master, diz PF
                </a>

                <p className="result-preview">
                    Relatório da PF enviado ao STF aponta que mudanças em cargos estratégicos do Rioprevidência antecederam aportes bilionários em produtos ligados ao Banco Master. Defesa do ex-governador afirma que ele acompanhou buscas ‘com serenidade’.
                </p>

                <div className="result-footer">
                    Relevância: 10
                </div>
            </div>
        </div>
    );
}