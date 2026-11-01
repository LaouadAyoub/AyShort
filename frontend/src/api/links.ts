export type CreateShortUrlResult = {
  code: string
  shortUrl: string
}

export async function createShortLink(url: string): Promise<CreateShortUrlResult> {
  const response = await fetch('/api/links', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url }),
  })

  if (!response.ok) {
    throw new Error(response.status >= 500
      ? 'The service is unavailable. Please try again.'
      : 'Could not create the short link. Check your URL and try again.')
  }

  const result: unknown = await response.json()
  if (
    typeof result !== 'object' || result === null ||
    !('code' in result) || typeof result.code !== 'string' ||
    !('shortUrl' in result) || typeof result.shortUrl !== 'string'
  ) {
    throw new Error('The server returned an unexpected response. Please try again.')
  }

  return { code: result.code, shortUrl: result.shortUrl }
}
