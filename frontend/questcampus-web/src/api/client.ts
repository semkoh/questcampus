import type {
  AttemptResult, Checkpoint, Dashboard, Leaderboard, QuestDetail, QuestSummary, Team, User,
} from './types';

/** Erreur HTTP lisible : on remonte le "detail" des ProblemDetails renvoyés par les API. */
export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
  }
}

async function toError(response: Response): Promise<ApiError> {
  let message = `Erreur ${response.status}`;
  try {
    const problem = await response.json();
    const validation = problem.errors ? Object.values(problem.errors as Record<string, string[]>).flat() : [];
    message = validation.length > 0 ? validation.join(' ') : problem.detail ?? problem.title ?? message;
  } catch {
    if (response.status === 429) message = 'Trop de tentatives : patientez une minute.';
  }
  return new ApiError(response.status, message);
}

let refreshing: Promise<boolean> | null = null;

/** Demande un nouveau jeton au BFF (cookie de rafraîchissement). Une seule requête à la fois. */
function refreshSession(): Promise<boolean> {
  refreshing ??= fetch('/bff/auth/refresh', { method: 'POST', credentials: 'same-origin' })
    .then((r) => r.ok)
    .catch(() => false)
    .finally(() => { refreshing = null; });
  return refreshing;
}

/**
 * Appel au BFF. Les jetons voyagent uniquement dans des cookies HttpOnly (jamais lus en JS).
 * Sur 401, on tente une fois de rafraîchir la session puis on rejoue la requête.
 */
async function request<T>(method: string, url: string, body?: unknown, retry = true): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (response.status === 401 && retry && !url.startsWith('/bff/auth/')) {
    if (await refreshSession()) return request<T>(method, url, body, false);
  }
  if (!response.ok) throw await toError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  // Authentification (façade BFF -> service Auth)
  login: (email: string, password: string) => request<User>('POST', '/bff/auth/login', { email, password }),
  register: (email: string, password: string, displayName: string) =>
    request<User>('POST', '/bff/auth/register', { email, password, displayName }),
  logout: () => request<void>('POST', '/bff/auth/logout'),
  me: async (): Promise<User | null> => {
    try {
      return await request<User>('GET', '/bff/auth/me');
    } catch {
      if (await refreshSession()) {
        try { return await request<User>('GET', '/bff/auth/me', undefined, false); } catch { return null; }
      }
      return null;
    }
  },

  // Agrégation BFF (profil + quêtes + équipes en un appel)
  dashboard: () => request<Dashboard>('GET', '/bff/dashboard'),

  // API Métier relayée par le reverse proxy du BFF
  quest: (id: string) => request<QuestDetail>('GET', `/api/quests/${id}`),
  createQuest: (title: string, description: string) =>
    request<QuestSummary>('POST', '/api/quests', { title, description }),
  addCheckpoint: (questId: string, input: {
    title: string; riddle: string; latitude: number; longitude: number; answer: string; points: number;
  }) => request<Checkpoint>('POST', `/api/quests/${questId}/checkpoints`, input),
  publishQuest: (questId: string) => request<void>('POST', `/api/quests/${questId}/publish`),
  createTeam: (questId: string, name: string) => request<Team>('POST', `/api/quests/${questId}/teams`, { name }),
  joinTeam: (inviteCode: string) => request<Team>('POST', '/api/teams/join', { inviteCode }),
  leaderboard: (questId: string) => request<Leaderboard>('GET', `/api/quests/${questId}/leaderboard`),

  // Orchestration BFF : validation + diffusion temps réel du classement
  submitAttempt: (questId: string, checkpointId: string, answer: string, latitude: number, longitude: number) =>
    request<AttemptResult>('POST', `/bff/quests/${questId}/checkpoints/${checkpointId}/attempts`,
      { answer, latitude, longitude }),
};
