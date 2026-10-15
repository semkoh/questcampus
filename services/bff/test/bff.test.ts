import { SignJWT } from 'jose';
import request from 'supertest';
import { describe, expect, it } from 'vitest';
import { createApp } from '../src/app.js';
import { DownstreamClient, DownstreamResponse, ServiceUnavailableError } from '../src/clients/downstream.js';
import { loadConfig } from '../src/config.js';
import { verifyAccessToken } from '../src/auth/tokens.js';

const config = loadConfig({ JWT_SIGNING_KEY: 'test-signing-key-with-more-than-32-characters' });

async function token(overrides: { audience?: string; expiresIn?: string; key?: string; roles?: string[] } = {}) {
  return new SignJWT({ email: 'alice@test.fr', name: 'Alice', role: overrides.roles ?? ['Joueur'] })
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject('b1000000-0000-0000-0000-000000000001')
    .setIssuer(config.jwtIssuer)
    .setAudience(overrides.audience ?? config.jwtAudience)
    .setIssuedAt()
    .setExpirationTime(overrides.expiresIn ?? '15m')
    .sign(new TextEncoder().encode(overrides.key ?? config.jwtSigningKey));
}

/** Faux service interne : répond selon une table "MÉTHODE chemin" -> réponse. */
class FakeService extends DownstreamClient {
  calls: string[] = [];
  constructor(private readonly routes: Record<string, DownstreamResponse | Error>) {
    super('http://fake');
  }
  override async send(method: string, path: string): Promise<DownstreamResponse> {
    this.calls.push(`${method} ${path}`);
    const answer = this.routes[`${method} ${path}`];
    if (answer instanceof Error) throw answer;
    return answer ?? new DownstreamResponse(404, '');
  }
}

const ok = (body: unknown) => new DownstreamResponse(200, JSON.stringify(body));

describe('validation du JWT', () => {
  it('accepte un jeton valide et lit identité et rôles', async () => {
    const user = await verifyAccessToken(await token(), config);
    expect(user).toEqual({ id: 'b1000000-0000-0000-0000-000000000001', email: 'alice@test.fr', displayName: 'Alice', roles: ['Joueur'] });
  });

  it('refuse un jeton expiré, mal signé ou destiné à une autre application', async () => {
    await expect(verifyAccessToken(await token({ expiresIn: '-1h' }), config)).rejects.toThrow();
    await expect(verifyAccessToken(await token({ key: 'another-key-another-key-another-key!!' }), config)).rejects.toThrow();
    await expect(verifyAccessToken(await token({ audience: 'autre' }), config)).rejects.toThrow();
  });
});

describe('BFF', () => {
  it('refuse les routes protégées sans cookie de session', async () => {
    const app = createApp({ config, auth: new FakeService({}), quests: new FakeService({}) });
    expect((await request(app).get('/bff/auth/me')).status).toBe(401);
    expect((await request(app).get('/bff/dashboard')).status).toBe(401);
    expect((await request(app).get('/api/quests')).status).toBe(401);
  });

  it('pose des cookies HttpOnly SameSite=Strict à la connexion sans renvoyer les jetons', async () => {
    const auth = new FakeService({
      'POST /api/auth/login': ok({ accessToken: 'AT', refreshToken: 'RT', refreshTokenExpiresAtUtc: '2030-01-01T00:00:00Z', user: { id: '1' } }),
    });
    const res = await request(createApp({ config, auth, quests: new FakeService({}) }))
      .post('/bff/auth/login').send({ email: 'a@b.fr', password: 'x' });
    expect(res.status).toBe(200);
    expect(res.body).toEqual({ id: '1' });
    const cookies = ([] as string[]).concat(res.headers['set-cookie'] ?? []);
    expect(cookies.some((c) => c.startsWith('qc_access=AT') && /HttpOnly/i.test(c) && /SameSite=Strict/i.test(c))).toBe(true);
    expect(cookies.some((c) => c.startsWith('qc_refresh=RT') && c.includes('Path=/bff/auth'))).toBe(true);
  });

  it('agrège profil, quêtes et équipes en un seul appel', async () => {
    const auth = new FakeService({ 'GET /api/auth/me': ok({ displayName: 'Alice (Auth)' }) });
    const quests = new FakeService({ 'GET /api/quests': ok([{ id: 'q1' }, { id: 'q2' }]), 'GET /api/teams/mine': ok([{ id: 't1' }]) });
    const res = await request(createApp({ config, auth, quests })).get('/bff/dashboard').set('Cookie', `qc_access=${await token()}`);
    expect(res.status).toBe(200);
    expect(res.body.user.displayName).toBe('Alice (Auth)');
    expect(res.body.stats).toEqual({ availableQuests: 2, myTeamCount: 1 });
  });

  it("passe en mode dégradé si le service Auth est indisponible", async () => {
    const auth = new FakeService({ 'GET /api/auth/me': new ServiceUnavailableError('down') });
    const quests = new FakeService({ 'GET /api/quests': ok([]), 'GET /api/teams/mine': ok([]) });
    const res = await request(createApp({ config, auth, quests })).get('/bff/dashboard').set('Cookie', `qc_access=${await token()}`);
    expect(res.status).toBe(200);
    expect(res.body.user.displayName).toBe('Alice'); // lu dans le JWT
  });

  it("renvoie 503 si l'API Métier est injoignable", async () => {
    const quests = new FakeService({ 'GET /api/quests': new ServiceUnavailableError('down'), 'GET /api/teams/mine': ok([]) });
    const res = await request(createApp({ config, auth: new FakeService({}), quests })).get('/bff/dashboard').set('Cookie', `qc_access=${await token()}`);
    expect(res.status).toBe(503);
  });

  it('diffuse le classement uniquement quand un checkpoint est validé', async () => {
    const q = '00000000-0000-0000-0000-000000000001';
    const c = '00000000-0000-0000-0000-000000000002';
    const broadcasts: unknown[] = [];
    const quests = new FakeService({
      [`POST /api/quests/${q}/checkpoints/${c}/attempts`]: ok({ isValidated: true }),
      [`GET /api/quests/${q}/leaderboard`]: ok({ entries: [] }),
    });
    const app = createApp({ config, auth: new FakeService({}), quests, broadcast: (_id, board) => broadcasts.push(board) });
    const res = await request(app).post(`/bff/quests/${q}/checkpoints/${c}/attempts`)
      .set('Cookie', `qc_access=${await token()}`).send({ answer: 'x', latitude: 1, longitude: 2 });
    expect(res.status).toBe(200);
    expect(broadcasts).toEqual([{ entries: [] }]);
  });

  it('limite le nombre de tentatives par minute (anti-force brute)', async () => {
    const q = '00000000-0000-0000-0000-000000000001';
    const c = '00000000-0000-0000-0000-000000000002';
    const quests = new FakeService({ [`POST /api/quests/${q}/checkpoints/${c}/attempts`]: ok({ isValidated: false }) });
    const app = createApp({ config: { ...config, attemptsPerMinute: 3 }, auth: new FakeService({}), quests });
    const cookie = `qc_access=${await token()}`;
    const statuses: number[] = [];
    for (let i = 0; i < 4; i++) {
      statuses.push((await request(app).post(`/bff/quests/${q}/checkpoints/${c}/attempts`).set('Cookie', cookie).send({})).status);
    }
    expect(statuses).toEqual([200, 200, 200, 429]);
  });
});
