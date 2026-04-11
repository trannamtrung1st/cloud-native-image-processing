import { apiUrl } from "../config/api.js";
import { readResponseJson } from "../lib/http.js";

export async function loginWithEmailPassword(email, password) {
  const response = await fetch(
    apiUrl("/api/auth/login?useCookies=false&useSessionCookies=false"),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    },
  );
  const { json } = await readResponseJson(response);
  return { response, json };
}

export async function registerAccount(email, password) {
  const response = await fetch(apiUrl("/api/auth/register"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  const { json } = await readResponseJson(response);
  return { response, json };
}
