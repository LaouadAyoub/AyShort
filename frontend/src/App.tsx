import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { createShortLink, LinkApiError } from './api/links'
import type { CreateShortUrlResult } from './api/links'

function App() {
  const [url, setUrl] = useState('')
  const [result, setResult] = useState<CreateShortUrlResult | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<LinkApiError | null>(null)
  const [copyStatus, setCopyStatus] = useState<'idle' | 'copying' | 'copied' | 'error'>('idle')
  const copyAttempt = useRef(0)

  async function handleCopy() {
    if (!result || copyStatus === 'copying') return

    const attempt = ++copyAttempt.current
    setCopyStatus('copying')
    try {
      if (!navigator.clipboard?.writeText) {
        throw new Error('Clipboard unavailable')
      }
      await navigator.clipboard.writeText(result.shortUrl)
      if (attempt === copyAttempt.current) setCopyStatus('copied')
    } catch {
      if (attempt === copyAttempt.current) setCopyStatus('error')
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isSubmitting) return

    setIsSubmitting(true)
    // Ignore a pending copy's feedback once its result is removed.
    copyAttempt.current += 1
    setCopyStatus('idle')
    setResult(null)
    setError(null)
    try {
      const createdLink = await createShortLink(url)
      setResult(createdLink)
    } catch (error) {
      setError(error instanceof LinkApiError ? error : new LinkApiError('Something went wrong. Please try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="mx-auto max-w-3xl px-6 py-16 sm:py-24">
      <h1 className="text-5xl font-bold tracking-tight sm:text-7xl">AyShort</h1>
      <p className="mt-4 max-w-xl text-lg leading-relaxed text-muted">
        Turn a long URL into a short link that is easy to share.
      </p>

      <form className="mt-10 flex flex-col gap-4" onSubmit={handleSubmit}>
        <label className="font-medium" htmlFor="url">Long URL</label>
        <input
          className="min-h-12 w-full rounded-lg border border-border bg-surface px-4 py-3 text-ink placeholder:text-muted focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus"
          id="url"
          name="url"
          type="url"
          required
          disabled={isSubmitting}
          aria-invalid={error?.status === 400 || undefined}
          aria-describedby={error ? 'link-error' : undefined}
          value={url}
          onChange={(event) => setUrl(event.target.value)}
          placeholder="https://example.com/your-long-link"
        />
        <button
          className="min-h-12 w-full cursor-pointer rounded-lg bg-primary px-6 py-3 font-semibold text-white hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus disabled:cursor-wait disabled:opacity-70 sm:w-auto sm:self-start"
          type="submit"
          disabled={isSubmitting}
        >
          {isSubmitting ? 'Shortening…' : 'Shorten'}
        </button>
        {isSubmitting && <p role="status">Creating your short link…</p>}
        {error && <p id="link-error" role="alert">{error.message}</p>}
      </form>
      {result && (
        <section className="mt-8 rounded-lg border border-border bg-surface p-6" aria-labelledby="result-heading">
          <h2 id="result-heading" className="font-semibold">Your short link</h2>
          <a className="mt-2 block break-all text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus" href={result.shortUrl}>
            {result.shortUrl}
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
      )}
    </main>
  )
}

export default App
