import { useState } from 'react'

type ShortLinkResultProps = {
  shortUrl: string
}

export function ShortLinkResult({ shortUrl }: ShortLinkResultProps) {
  const [copyStatus, setCopyStatus] = useState<'idle' | 'copying' | 'copied' | 'error'>('idle')

  async function handleCopy() {
    if (copyStatus === 'copying') return

    setCopyStatus('copying')
    try {
      if (!navigator.clipboard?.writeText) {
        throw new Error('Clipboard unavailable')
      }
      await navigator.clipboard.writeText(shortUrl)
      setCopyStatus('copied')
    } catch {
      setCopyStatus('error')
    }
  }

  return (
    <section className="mt-8 rounded-lg border border-border bg-surface p-6" aria-labelledby="result-heading">
      <h2 id="result-heading" className="font-semibold">Your short link</h2>
      <a className="mt-2 block break-all text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus" href={shortUrl}>
        {shortUrl}
      </a>
      <button
        className="mt-4 inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm font-medium text-primary hover:bg-canvas focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus disabled:cursor-wait disabled:opacity-60"
        type="button"
        onClick={handleCopy}
        disabled={copyStatus === 'copying'}
      >
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
          {copyStatus === 'copied' ? (
            <path d="m5 12 4 4L19 6" />
          ) : (
            <>
              <rect x="8" y="8" width="12" height="12" rx="2" />
              <path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2" />
            </>
          )}
        </svg>
        {copyStatus === 'copying' ? 'Copying…' : copyStatus === 'copied' ? 'Copied!' : 'Copy link'}
      </button>
      <p className="mt-2 text-sm" role="status">
        {copyStatus === 'copied' && 'Short link copied to clipboard.'}
        {copyStatus === 'error' && 'Could not copy the link. Select the URL above and copy it manually, or try again.'}
      </p>
    </section>
  )
}
