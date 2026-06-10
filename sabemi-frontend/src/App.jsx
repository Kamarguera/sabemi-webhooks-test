import { useState, useEffect } from 'react'

const API = 'http://localhost:5261'


export default function App() {
  const [eventos, setEventos] = useState([])
  const [filtroStatus, setFiltroStatus] = useState('')
  const [filtroContrato, setFiltroContrato] = useState('')

  const buscar = () => {
    const params = new URLSearchParams()
    if (filtroStatus)   params.append('status', filtroStatus)
    if (filtroContrato) params.append('idContrato', filtroContrato)
    fetch(`${API}/webhooks/pagamentos?${params}`)
      .then(r => r.json())
      .then(setEventos)
  }

  useEffect(() => { buscar(); const t = setInterval(buscar, 5000); return () => clearInterval(t) }, [filtroStatus, filtroContrato])

  return (
    <div style={{ fontFamily: 'sans-serif', padding: 24 }}>
      <h1>Painel de Pagamentos — Sabemi</h1>

      <div style={{ marginBottom: 16, display: 'flex', gap: 12 }}>
        <select value={filtroStatus} onChange={e => setFiltroStatus(e.target.value)}>
          <option value="">Todos os status</option>
          <option value="Sucesso">Sucesso</option>
          <option value="Erro">Erro</option>
        </select>
        <input
          placeholder="Filtrar por ID Contrato"
          value={filtroContrato}
          onChange={e => setFiltroContrato(e.target.value)}
        />
        <button onClick={buscar}>Atualizar</button>
      </div>

      <table border="1" cellPadding="8" style={{ borderCollapse: 'collapse', width: '100%' }}>
        <thead style={{ background: '#222', color: '#fff' }}>
          <tr>
            <th>ID Transação</th><th>ID Contrato</th><th>Valor</th>
            <th>Data Pagamento</th><th>Status</th><th>Recebido em</th><th>Processado</th>
          </tr>
        </thead>
        <tbody>
          {eventos.map(e => (
            <tr key={e.id} style={{ background: (e.erro || e.status === 'Erro') ? '#ffe0e0' : e.status === 'Sucesso' ? '#e0ffe0' : '#fff' }}>
              <td>{e.idTransacao}</td>
              <td>{e.idContrato}</td>
              <td>R$ {Number(e.valor).toFixed(2)}</td>
              <td>{new Date(e.dataPagamento).toLocaleDateString('pt-BR')}</td>
              <td>
                {(e.erro || e.status === 'Erro')
                  ? <span style={{ color: 'red' }}>⚠️ {e.status}{e.erro ? ` — ${e.erro}` : ''}</span>
                  : <span style={{ color: 'green' }}>{e.status}</span>}
              </td>
              <td>{new Date(e.recebidoEm).toLocaleString('pt-BR')}</td>
              <td>{e.processado ? '✅' : '⏳'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {eventos.length === 0 && <p>Nenhum evento encontrado.</p>}
    </div>
  )
}