import { useState } from 'react'
import type { FormEvent } from 'react'
import { createShortLink, LinkApiError } from './api/links'
import type { CreateShortUrlResult } from './api/links'
import { ShortLinkResult } from './features/create-link/ShortLinkResult'

function App() {
  const [url, setUrl] = useState('')
  const [result, setResult] = useState<CreateShortUrlResult | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<LinkApiError | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isSubmitting) return

    setIsSubmitting(true)
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
        <ShortLinkResult key={result.shortUrl} shortUrl={result.shortUrl} />
      )}

      <section className="mt-12 border-t border-border/40 pt-6" aria-labelledby="about-heading">
        <h2 id="about-heading" className="text-lg font-semibold">About AyShort</h2>
        <p className="mt-2 max-w-xl leading-relaxed text-muted">
          AyShort is a portfolio project that makes long web addresses easier to share.
          It brings together this web page, a service that saves short links and sends
          visitors to the original page, and tests that check those steps.
        </p>
        <a
          className="mt-2 inline-flex min-h-11 items-center gap-2 rounded-sm text-sm font-medium text-primary underline underline-offset-4 hover:text-primary-hover focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus"
          href="https://github.com/LaouadAyoub/AyShort#readme"
        >
          <svg className="size-5 shrink-0" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.043-1.61-4.043-1.61-.546-1.387-1.333-1.756-1.333-1.756-1.09-.745.083-.729.083-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.418-1.305.762-1.605-2.665-.3-5.466-1.334-5.466-5.931 0-1.31.469-2.38 1.236-3.22-.124-.303-.536-1.524.117-3.176 0 0 1.008-.322 3.301 1.23a11.5 11.5 0 0 1 3.003-.403c1.02.005 2.047.138 3.006.403 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.873.118 3.176.77.84 1.235 1.911 1.235 3.22 0 4.609-2.804 5.628-5.475 5.922.43.372.823 1.102.823 2.222 0 1.606-.015 2.898-.015 3.293 0 .322.216.694.825.576C20.565 22.092 24 17.6 24 12.297c0-6.627-5.373-12-12-12" />
          </svg>
          View source and documentation
        </a>
      </section>
    </main>
  )
}

export default App
