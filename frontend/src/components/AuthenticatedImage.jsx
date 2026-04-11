import { useEffect, useState } from "react";
import { apiUrl } from "../config/api.js";

/** Loads image via authenticated GET /api/images/{id}/preview (img tags cannot send Bearer tokens). */
export function AuthenticatedImage({ imageId, accessToken, alt, className }) {
  const [src, setSrc] = useState(null);

  useEffect(() => {
    if (!accessToken || !imageId) {
      return;
    }

    let cancelled = false;
    let objectUrl;

    (async () => {
      try {
        const res = await fetch(apiUrl(`/api/images/${imageId}/preview`), {
          headers: { Authorization: `Bearer ${accessToken}` },
        });
        if (!res.ok || cancelled) {
          return;
        }
        const blob = await res.blob();
        objectUrl = URL.createObjectURL(blob);
        if (cancelled) {
          URL.revokeObjectURL(objectUrl);
          return;
        }
        setSrc(objectUrl);
      } catch {
        // network / CORS
      }
    })();

    return () => {
      cancelled = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
      setSrc(null);
    };
  }, [imageId, accessToken]);

  if (!src) {
    return (
      <div
        className={`animate-pulse bg-slate-200 ${className ?? ""}`}
        aria-hidden
      />
    );
  }

  return <img src={src} alt={alt} className={className} />;
}
