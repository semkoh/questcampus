// Contrats JSON exposés par le BFF (mêmes noms que les réponses des API, en camelCase).

export interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface QuestSummary {
  id: string;
  title: string;
  description: string;
  isPublished: boolean;
  checkpointCount: number;
  totalPoints: number;
  isOwner: boolean;
}

export interface Checkpoint {
  id: string;
  order: number;
  title: string;
  riddle: string;
  points: number;
  latitude: number | null;
  longitude: number | null;
  answer: string | null;
  isValidatedByMyTeam: boolean;
}

export interface MyTeam {
  id: string;
  name: string;
  inviteCode: string;
  memberCount: number;
}

export interface QuestDetail {
  id: string;
  title: string;
  description: string;
  isPublished: boolean;
  isOwner: boolean;
  totalPoints: number;
  checkpoints: Checkpoint[];
  myTeam: MyTeam | null;
}

export interface Team {
  id: string;
  questId: string;
  questTitle: string;
  name: string;
  inviteCode: string;
  memberCount: number;
}

export interface Dashboard {
  user: User;
  quests: QuestSummary[];
  myTeams: Team[];
  stats: { availableQuests: number; myTeamCount: number };
}

export interface AttemptResult {
  isValidated: boolean;
  alreadyValidated: boolean;
  isAnswerCorrect: boolean;
  isWithinRadius: boolean;
  distanceMeters: number;
  pointsAwarded: number;
  message: string;
}

export interface LeaderboardEntry {
  rank: number;
  teamId: string;
  teamName: string;
  memberCount: number;
  points: number;
  validatedCheckpoints: number;
  lastValidationAtUtc: string | null;
}

export interface Leaderboard {
  questId: string;
  questTitle: string;
  maxPoints: number;
  generatedAtUtc: string;
  entries: LeaderboardEntry[];
}

export const ROLE_ORGANIZER = 'Organisateur';
export const isOrganizer = (user: User | null) => !!user?.roles.includes(ROLE_ORGANIZER);
