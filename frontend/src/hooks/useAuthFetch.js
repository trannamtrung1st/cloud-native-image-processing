import { useCallback } from "react";
import { apiUrl } from "../config/api.js";

export function useAuthFetch(accessToken) {
  return useCallback(
    async (path, options = {}) => {
      const isFormData = options.body instanceof FormData;
      const baseHeaders = { ...(options.headers || {}) };
      if (isFormData) {
        delete baseHeaders["Content-Type"];
        delete baseHeaders["content-type"];
      }
      return fetch(apiUrl(path), {
        ...options,
        headers: {
          ...baseHeaders,
          ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        },
      });
    },
    [accessToken],
  );
}
