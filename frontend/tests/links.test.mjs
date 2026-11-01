import assert from 'node:assert/strict'
import { afterEach, mock, test } from 'node:test'
import { createShortLink, LinkApiError } from '../src/api/links.ts'

afterEach(() => mock.restoreAll())

function respond(response) {
  mock.method(globalThis, 'fetch', async () => response)
}

test('sends the expected request and returns a created link', async () => {
  const created = { code: 'abc1234', shortUrl: 'http://localhost:5142/abc1234' }
  mock.method(globalThis, 'fetch', async (path, options) => {
    assert.equal(path, '/api/links')
    assert.equal(options.method, 'POST')
    assert.equal(options.headers['Content-Type'], 'application/json')
    assert.deepEqual(JSON.parse(options.body), { url: 'https://example.com/' })
    return Response.json(created, { status: 201 })
  })
  assert.deepEqual(await createShortLink('https://example.com/'), created)
})

test('preserves the API validation detail and status', async () => {
  respond(Response.json({ detail: 'Only http/https are allowed.' }, { status: 400 }))
  await assert.rejects(createShortLink('ftp://example.com'), error => {
    assert.ok(error instanceof LinkApiError)
    assert.equal(error.status, 400)
    assert.equal(error.message, 'Only http/https are allowed.')
    return true
  })
})

test('preserves a conflict message', async () => {
  respond(Response.json({ detail: 'Short code already exists.' }, { status: 409 }))
  await assert.rejects(createShortLink('https://example.com'), /Short code already exists/)
})

test('uses a friendly fallback for non-JSON validation errors', async () => {
  respond(new Response('<html>Bad request</html>', { status: 400 }))
  await assert.rejects(createShortLink('https://example.com'), /Check your URL and try again/)
})

test('uses a friendly fallback for invalid validation details', async () => {
  respond(Response.json({ detail: { unexpected: true } }, { status: 400 }))
  await assert.rejects(createShortLink('https://example.com'), /Check your URL and try again/)
})

test('does not expose internal server error details', async () => {
  respond(Response.json({ detail: 'Database password and internal stack trace' }, { status: 500 }))
  await assert.rejects(createShortLink('https://example.com'), error => {
    assert.equal(error.status, 500)
    assert.equal(error.message, 'The service is unavailable. Please try again.')
    return true
  })
})

test('translates network failures into an actionable message', async () => {
  mock.method(globalThis, 'fetch', async () => { throw new TypeError('Failed to fetch') })
  await assert.rejects(createShortLink('https://example.com'), /Check your connection and try again/)
})

test('rejects a successful response containing malformed JSON', async () => {
  respond(new Response('{', { status: 201 }))
  await assert.rejects(createShortLink('https://example.com'), /unexpected response/)
})

test('rejects a successful response with the wrong shape', async () => {
  respond(Response.json({ code: 123, shortUrl: null }, { status: 201 }))
  await assert.rejects(createShortLink('https://example.com'), /unexpected response/)
})

test('allows a new attempt after a network failure', async () => {
  const created = { code: 'retry12', shortUrl: 'http://localhost:5142/retry12' }
  let attempts = 0
  mock.method(globalThis, 'fetch', async () => {
    attempts++
    if (attempts === 1) throw new TypeError('Failed to fetch')
    return Response.json(created, { status: 201 })
  })
  await assert.rejects(createShortLink('https://example.com'), LinkApiError)
  assert.deepEqual(await createShortLink('https://example.com'), created)
  assert.equal(attempts, 2)
})
