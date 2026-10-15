import type { Response } from 'express';

/** Réponse d'erreur au format ProblemDetails (RFC 9457), identique à celui des deux API. */
export function sendProblem(res: Response, status: number, title: string, detail?: string): void {
  res.status(status).type('application/problem+json').json(detail ? { status, title, detail } : { status, title });
}
