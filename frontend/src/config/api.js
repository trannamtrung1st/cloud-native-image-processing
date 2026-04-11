/** Dev: local API (default :5000 for dotnet run). Override with VITE_API_BASE_URL. Production + same-host ingress: leave unset so requests use relative /api. */
export const API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL ||
  (import.meta.env.DEV ? "http://localhost:5000" : "")
).replace(/\/$/, "");

export function apiUrl(path) {
  const p = path.startsWith("/") ? path : `/${path}`;
  return API_BASE_URL ? `${API_BASE_URL}${p}` : p;
}
