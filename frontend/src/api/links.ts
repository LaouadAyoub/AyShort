export type CreateShortUrlResult = {
  code: string
  shortUrl: string
}

export class LinkApiError extends Error {
  status: number | undefined

  constructor(message: string, status?: number) {
    super(message)
    this.name = 'LinkApiError'
    this.status = status
  }
}

export async function createShortLink(url: string): Promise<CreateShortUrlResult> {
  let response: Response
  try {
    response = await fetch('/api/links', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url }),
    })
  } catch {
    throw new LinkApiError('Could not reach the service. Check your connection and try again.')
  }

  if (!response.ok) {
    let message = response.status >= 500
      ? 'The service is unavailable. Please try again.'
      : 'Could not create the short link. Please try again.'

    // Only expected validation/conflict responses may supply user-facing details.
    if (response.status === 400 || response.status === 409) {
      message = response.status === 400
        ? 'Check your URL and try again.'
        : 'That short link already exists. Please try again.'
      try {
        const problem: unknown = await response.json()
        if (typeof problem === 'object' && problem !== null &&
          'detail' in problem && typeof problem.detail === 'string' && problem.detail.trim()) {
          message = problem.detail
        }
      } catch {
        // Keep the helpful fallback if the server returns an empty or non-JSON body.
      }
    }
    throw new LinkApiError(message, response.status)
  }

  let result: unknown
  try {
    result = await response.json()
  } catch {
    throw new LinkApiError('The server returned an unexpected response. Please try again.')
  }
  if (
    typeof result !== 'object' || result === null ||
    !('code' in result) || typeof result.code !== 'string' ||
    !('shortUrl' in result) || typeof result.shortUrl !== 'string'
  ) {
    throw new LinkApiError('The server returned an unexpected response. Please try again.')
  }

  return { code: result.code, shortUrl: result.shortUrl }
}
