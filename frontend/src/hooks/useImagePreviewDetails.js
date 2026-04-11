import { useEffect, useState } from "react";
import { mapApiImage } from "../lib/images.js";
import { parseProblemMessage, readResponseJson } from "../lib/http.js";

export function useImagePreviewDetails(previewImage, accessToken, authFetch) {
  const [details, setDetails] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!previewImage?.id || !accessToken) {
      setDetails(null);
      setError("");
      setLoading(false);
      return;
    }

    let cancelled = false;
    setDetails(null);
    setError("");
    setLoading(true);

    (async () => {
      try {
        const response = await authFetch(`/api/images/${previewImage.id}`);
        const { json: body } = await readResponseJson(response);

        if (cancelled) {
          return;
        }

        if (!response.ok) {
          setError(
            parseProblemMessage(body) || `Could not load details (${response.status}).`,
          );
          setDetails(null);
          return;
        }

        setDetails(mapApiImage(body));
      } catch {
        if (!cancelled) {
          setError("Could not load image details.");
          setDetails(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [previewImage?.id, accessToken, authFetch]);

  return { details, loading, error };
}
