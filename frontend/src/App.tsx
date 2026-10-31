function App() {
  return (
    <main className="mx-auto max-w-3xl px-6 py-16 sm:py-24">
      <h1 className="text-5xl font-bold tracking-tight sm:text-7xl">AyShort</h1>
      <p className="mt-4 max-w-xl text-lg leading-relaxed text-muted">
        Turn a long URL into a short link that is easy to share.
      </p>

      <form className="mt-10 flex flex-col gap-4">
        <label className="font-medium" htmlFor="url">Long URL</label>
        <input
          className="min-h-12 w-full rounded-lg border border-border bg-surface px-4 py-3 text-ink placeholder:text-muted focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus"
          id="url"
          name="url"
          type="url"
          placeholder="https://example.com/your-long-link"
        />
        <button
          className="min-h-12 w-full cursor-pointer rounded-lg bg-primary px-6 py-3 font-semibold text-white hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-focus sm:w-auto sm:self-start"
          type="submit"
        >
          Shorten
        </button>
      </form>
    </main>
  )
}

export default App
