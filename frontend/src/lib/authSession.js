const AUTH_STORAGE_KEY = "cnip.auth";

export function loadStoredSession() {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw);
    const accessToken = parsed?.accessToken;
    const email = typeof parsed?.email === "string" ? parsed.email : "";
    if (typeof accessToken === "string" && accessToken.length > 0 && email) {
      return { accessToken, email };
    }
  } catch {
    return null;
  }
  return null;
}

export function persistSession(accessToken, email) {
  localStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify({ accessToken, email, savedAt: Date.now() }),
  );
}

export function clearStoredSession() {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}
