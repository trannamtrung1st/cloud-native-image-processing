export function parseProblemMessage(body) {
  if (!body || typeof body !== "object") {
    return null;
  }
  if (typeof body.detail === "string" && body.detail.trim()) {
    return body.detail;
  }
  if (typeof body.message === "string" && body.message.trim()) {
    return body.message;
  }
  if (typeof body.title === "string" && body.title.trim()) {
    return body.title;
  }
  const { errors } = body;
  if (errors && typeof errors === "object") {
    const lines = Object.entries(errors).flatMap(([key, msgs]) => {
      const list = Array.isArray(msgs) ? msgs : [msgs];
      return list.map((m) => (key && key !== "" ? `${key}: ${m}` : String(m)));
    });
    if (lines.length) {
      return lines.join(" ");
    }
  }
  return null;
}

/** Reads response body as text and attempts JSON parse (RFC 7807 / Identity errors). */
export async function readResponseJson(response) {
  const text = await response.text();
  if (!text) {
    return { text: "", json: null };
  }
  try {
    return { text, json: JSON.parse(text) };
  } catch {
    return { text, json: null };
  }
}
