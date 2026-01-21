import { useState, useEffect, useRef } from 'react'

const API_URL = 'http://localhost:5000'

interface DataModification {
  name: string
  description: string
}

interface LogMessage {
  type: 'log'
  timestamp: string
  level: string
  category: string
  message: string
}

interface CompleteEvent {
  type: 'complete'
  success: boolean
}

interface ErrorEvent {
  type: 'error'
  errorMessage: string
}

type LogEvent = LogMessage | CompleteEvent | ErrorEvent

interface ModificationState {
  logLines: string[]
  success: boolean | null
  error?: string
  loading: boolean
}

const styles = `
  * { box-sizing: border-box; }
  body { font-family: system-ui, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }
  .container { max-width: 900px; margin: 0 auto; }
  h1 { color: #333; margin-bottom: 20px; }
  .card { background: white; border-radius: 8px; padding: 20px; margin-bottom: 16px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
  .card h3 { margin: 0 0 8px 0; color: #333; }
  .card p { margin: 0 0 16px 0; color: #666; font-size: 14px; }
  .buttons { display: flex; gap: 8px; }
  button { padding: 8px 16px; border: none; border-radius: 4px; cursor: pointer; font-size: 14px; }
  button:disabled { opacity: 0.5; cursor: not-allowed; }
  .btn-blue { background: #3b82f6; color: white; }
  .btn-blue:hover:not(:disabled) { background: #2563eb; }
  .btn-green { background: #22c55e; color: white; }
  .btn-green:hover:not(:disabled) { background: #16a34a; }
  .btn-red { background: #ef4444; color: white; }
  .btn-red:hover:not(:disabled) { background: #dc2626; }
  .log-panel { margin-top: 16px; }
  .log-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
  .log-title { font-weight: 600; color: #333; }
  .log-output { background: #1e1e1e; color: #d4d4d4; padding: 12px; border-radius: 4px; font-family: monospace; font-size: 12px; max-height: 300px; overflow-y: auto; white-space: pre-wrap; }
  .log-info { color: #4ec9b0; }
  .log-warning { color: #dcdcaa; }
  .log-error { color: #f44747; }
  .status { margin-top: 8px; padding: 8px; border-radius: 4px; font-size: 14px; }
  .status-success { background: #dcfce7; color: #166534; }
  .status-error { background: #fee2e2; color: #991b1b; }
  .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; }
  .modal { background: white; padding: 24px; border-radius: 8px; max-width: 400px; width: 100%; }
  .modal h2 { margin: 0 0 16px 0; color: #dc2626; }
  .modal input { width: 100%; padding: 8px; border: 1px solid #ddd; border-radius: 4px; margin: 8px 0 16px 0; }
  .modal-buttons { display: flex; gap: 8px; }
  .modal-buttons button { flex: 1; }
`

export default function App() {
  const [modifications, setModifications] = useState<DataModification[]>([])
  const [dryRunState, setDryRunState] = useState<Record<string, ModificationState>>({})
  const [runState, setRunState] = useState<Record<string, ModificationState>>({})
  const [showConfirm, setShowConfirm] = useState<string | null>(null)
  const [confirmInput, setConfirmInput] = useState('')
  const abortControllers = useRef<Record<string, AbortController>>({})

  useEffect(() => {
    fetch(`${API_URL}/api/dataModifications/`)
      .then(r => r.json())
      .then(data => setModifications(data.dataModifications))
  }, [])

  const runModification = async (name: string, isDryRun: boolean) => {
    const key = `${name}-${isDryRun ? 'dry' : 'run'}`
    abortControllers.current[key]?.abort()
    abortControllers.current[key] = new AbortController()

    const setState = isDryRun ? setDryRunState : setRunState
    setState(prev => ({ ...prev, [name]: { logLines: [], success: null, loading: true } }))

    try {
      const endpoint = isDryRun ? 'dryRun' : 'run'
      const body = isDryRun
        ? { dataModificationName: name }
        : { dataModificationName: name, confirmationName: name }

      const res = await fetch(`${API_URL}/api/dataModifications/${endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        signal: abortControllers.current[key].signal
      })

      const reader = res.body?.getReader()
      if (!reader) return

      const decoder = new TextDecoder()
      let buffer = ''

      const processLine = (line: string) => {
        if (!line.startsWith('data: ')) return
        try {
          const event: LogEvent = JSON.parse(line.slice(6))
          setState(prev => {
            const current = prev[name] || { logLines: [], success: null, loading: true }
            if (event.type === 'log') {
              const logLine = `[${event.timestamp}] [${event.level}] ${event.message}`
              return { ...prev, [name]: { ...current, logLines: [...current.logLines, logLine] } }
            } else if (event.type === 'complete') {
              return { ...prev, [name]: { ...current, success: event.success, loading: false } }
            } else if (event.type === 'error') {
              return { ...prev, [name]: { ...current, success: false, error: event.errorMessage, loading: false } }
            }
            return prev
          })
        } catch (e) {
          console.error('Failed to parse SSE event:', line, e)
        }
      }

      while (true) {
        const { done, value } = await reader.read()

        if (value) {
          buffer += decoder.decode(value, { stream: true })

          // Process complete messages (ending with \n\n)
          let idx
          while ((idx = buffer.indexOf('\n\n')) !== -1) {
            const line = buffer.substring(0, idx)
            buffer = buffer.substring(idx + 2)
            processLine(line)
          }
        }

        if (done) {
          // Process any remaining
          if (buffer.trim()) {
            processLine(buffer)
          }
          setState(prev => {
            const current = prev[name]
            if (current?.loading) {
              return { ...prev, [name]: { ...current, success: true, loading: false } }
            }
            return prev
          })
          break
        }
      }
    } catch (e: any) {
      if (e.name === 'AbortError') {
        setState(prev => {
          const current = prev[name] || { logLines: [], success: null, loading: true }
          return { ...prev, [name]: { ...current, success: false, error: 'Cancelled', loading: false } }
        })
      } else {
        setState(prev => ({ ...prev, [name]: { logLines: [], success: false, error: e.message, loading: false } }))
      }
    }
  }

  const handleDryRun = (name: string) => runModification(name, true)

  const handleRun = (name: string) => {
    if (confirmInput !== name) {
      alert('Name does not match')
      return
    }
    setShowConfirm(null)
    setConfirmInput('')
    runModification(name, false)
  }

  const canRun = (name: string) => dryRunState[name]?.success === true

  return (
    <>
      <style>{styles}</style>
      <div className="container">
        <h1>Data Modifications</h1>

        {modifications.map(mod => (
          <div key={mod.name} className="card">
            <h3>{mod.name}</h3>
            <p>{mod.description}</p>

            <div className="buttons">
              <button
                className="btn-blue"
                onClick={() => handleDryRun(mod.name)}
                disabled={dryRunState[mod.name]?.loading}
              >
                {dryRunState[mod.name]?.loading ? 'Running...' : 'Dry Run'}
              </button>
              <button
                className="btn-green"
                onClick={() => setShowConfirm(mod.name)}
                disabled={!canRun(mod.name) || runState[mod.name]?.loading}
              >
                {runState[mod.name]?.loading ? 'Running...' : 'Execute'}
              </button>
              {(dryRunState[mod.name]?.loading || runState[mod.name]?.loading) && (
                <button
                  className="btn-red"
                  onClick={() => {
                    abortControllers.current[`${mod.name}-dry`]?.abort()
                    abortControllers.current[`${mod.name}-run`]?.abort()
                  }}
                >
                  Cancel
                </button>
              )}
            </div>

            {dryRunState[mod.name] && (
              <div className="log-panel">
                <div className="log-header">
                  <span className="log-title">Dry Run Output</span>
                </div>
                <div className="log-output">
                  {dryRunState[mod.name].logLines.map((line, i) => (
                    <div key={i} className={line.includes('[Error]') ? 'log-error' : line.includes('[Warning]') ? 'log-warning' : 'log-info'}>
                      {line}
                    </div>
                  ))}
                </div>
                {dryRunState[mod.name].success === true && (
                  <div className="status status-success">Dry run completed successfully - transaction rolled back</div>
                )}
                {dryRunState[mod.name].success === false && (
                  <div className="status status-error">Error: {dryRunState[mod.name].error}</div>
                )}
              </div>
            )}

            {runState[mod.name] && (
              <div className="log-panel">
                <div className="log-header">
                  <span className="log-title">Execution Output</span>
                </div>
                <div className="log-output">
                  {runState[mod.name].logLines.map((line, i) => (
                    <div key={i} className={line.includes('[Error]') ? 'log-error' : line.includes('[Warning]') ? 'log-warning' : 'log-info'}>
                      {line}
                    </div>
                  ))}
                </div>
                {runState[mod.name].success === true && (
                  <div className="status status-success">Modification executed successfully!</div>
                )}
                {runState[mod.name].success === false && (
                  <div className="status status-error">Error: {runState[mod.name].error}</div>
                )}
              </div>
            )}
          </div>
        ))}
      </div>

      {showConfirm && (
        <div className="modal-overlay" onClick={() => setShowConfirm(null)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h2>Confirm Execution</h2>
            <p>Type <strong>{showConfirm}</strong> to confirm:</p>
            <input
              value={confirmInput}
              onChange={e => setConfirmInput(e.target.value)}
              placeholder={showConfirm}
              autoFocus
            />
            <div className="modal-buttons">
              <button onClick={() => { setShowConfirm(null); setConfirmInput('') }}>Cancel</button>
              <button
                className="btn-red"
                onClick={() => handleRun(showConfirm)}
                disabled={confirmInput !== showConfirm}
              >
                Execute
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
