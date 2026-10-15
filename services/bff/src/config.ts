/** Configuration lue dans les variables d'environnement (voir docker-compose.yml). */
export interface Config {
  port: number;
  authApiUrl: string;
  questsApiUrl: string;
  jwtSigningKey: string;
  jwtIssuer: string;
  jwtAudience: string;
  attemptsPerMinute: number;
}

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  const config: Config = {
    port: Number(env.PORT ?? 5000),
    authApiUrl: env.AUTH_API_URL ?? 'http://localhost:8001',
    questsApiUrl: env.QUESTS_API_URL ?? 'http://localhost:8002',
    jwtSigningKey: env.JWT_SIGNING_KEY ?? 'dev-only-signing-key-change-me-0123456789abcdef',
    jwtIssuer: env.JWT_ISSUER ?? 'questcampus-auth',
    jwtAudience: env.JWT_AUDIENCE ?? 'questcampus',
    attemptsPerMinute: Number(env.ATTEMPTS_PER_MINUTE ?? 10),
  };
  if (config.jwtSigningKey.length < 32) {
    throw new Error('JWT_SIGNING_KEY doit contenir au moins 32 caractères.');
  }
  return config;
}
