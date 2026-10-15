import type { Response } from 'express';

/** Réponse brute d'un service interne, retransmise telle quelle au front si besoin. */
export class DownstreamResponse {
  constructor(public readonly status: number, public readonly body: string) {}

  get ok(): boolean {
    return this.status >= 200 && this.status < 300;
  }

  json<T = unknown>(): T {
    return JSON.parse(this.body) as T;
  }

  /** Retransmet statut et corps (ProblemDetails compris) au client. */
  forward(res: Response): void {
    if (!this.body) {
      res.sendStatus(this.status);
      return;
    }
    res.status(this.status).type(this.ok ? 'application/json' : 'application/problem+json').send(this.body);
  }
}

/** Erreur réseau ou délai dépassé vers un service interne (=> 503 pour le front). */
export class ServiceUnavailableError extends Error {}

/** Client HTTP vers un service interne : ajoute le JWT de l'utilisateur et capture la réponse. */
export class DownstreamClient {
  constructor(private readonly baseUrl: string, private readonly timeoutMs = 10_000) {}

  async send(method: string, path: string, accessToken?: string, body?: unknown): Promise<DownstreamResponse> {
    const headers: Record<string, string> = {};
    if (accessToken) headers.authorization = `Bearer ${accessToken}`;
    if (body !== undefined) headers['content-type'] = 'application/json';
    try {
      const response = await fetch(new URL(path, this.baseUrl), {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(this.timeoutMs),
      });
      return new DownstreamResponse(response.status, await response.text());
    } catch (error) {
      throw new ServiceUnavailableError(`${this.baseUrl} injoignable : ${(error as Error).message}`);
    }
  }

  get(path: string, accessToken?: string) {
    return this.send('GET', path, accessToken);
  }

  post(path: string, accessToken?: string, body?: unknown) {
    return this.send('POST', path, accessToken, body);
  }
}
