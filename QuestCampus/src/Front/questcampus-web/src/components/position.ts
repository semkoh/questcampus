export interface Position {
  latitude: number;
  longitude: number;
  accuracy?: number;
}

/** Le mode démo permet de choisir une position simulée (présentation en salle). */
export const DEMO_MODE = import.meta.env.VITE_DEMO_MODE === 'true';

/** Lieux de la quête de démonstration (seed). Utilisés uniquement si VITE_DEMO_MODE=true. */
export const DEMO_LOCATIONS: { label: string; position: Position }[] = [
  { label: 'Parvis de l’Hôtel de Ville', position: { latitude: 48.85646, longitude: 2.35242 } },
  { label: 'Tour Saint-Jacques', position: { latitude: 48.85797, longitude: 2.34888 } },
  { label: 'Fontaine Stravinsky', position: { latitude: 48.85928, longitude: 2.35139 } },
  { label: 'Centre Pompidou', position: { latitude: 48.86061, longitude: 2.35225 } },
  { label: 'Trop loin : rue de Rivoli (≈ 150 m)', position: { latitude: 48.85837, longitude: 2.34700 } },
];

/** Position GPS du navigateur (haute précision, pas de cache). */
export function getCurrentPosition(): Promise<Position> {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new Error('La géolocalisation n’est pas disponible sur cet appareil.'));
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (p) => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude, accuracy: p.coords.accuracy }),
      (error) => reject(new Error(error.code === error.PERMISSION_DENIED
        ? 'Autorisez l’accès à votre position pour valider un checkpoint.'
        : 'Position introuvable. Réessayez à l’extérieur.')),
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
    );
  });
}
