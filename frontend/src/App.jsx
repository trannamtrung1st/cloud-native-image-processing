import { useEffect, useRef, useState } from "react";

/** Dev: local API (default :5000 for dotnet run). Override with VITE_API_BASE_URL. Production: build-time (Docker often :8080). */
const API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL ||
  (import.meta.env.DEV ? "http://localhost:5000" : "")
).replace(/\/$/, "");

const apiUrl = (path) => {
  const p = path.startsWith("/") ? path : `/${path}`;
  return API_BASE_URL ? `${API_BASE_URL}${p}` : p;
};

const mapApiImage = (item) => ({
  id: item.id,
  name: item.name,
  uploadedAt: new Date(item.createdAtUtc).toLocaleString(),
  operation: item.operation?.toLowerCase() || "none",
  description: item.description || "No description provided.",
  status: item.status || "Queued",
});

const isProcessingImage = (item) => (item.status || "").toLowerCase() === "processing";

/** Loads image via authenticated GET /api/images/{id}/preview (img tags cannot send Bearer tokens). */
function AuthenticatedImage({ imageId, accessToken, alt, className }) {
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

const AUTH_STORAGE_KEY = "cnip.auth";

function loadStoredSession() {
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

function persistSession(accessToken, email) {
  localStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify({ accessToken, email, savedAt: Date.now() }),
  );
}

function clearStoredSession() {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}

function parseProblemMessage(body) {
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

function App() {
  const [sessionReady, setSessionReady] = useState(false);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [loginEmail, setLoginEmail] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [currentUserEmail, setCurrentUserEmail] = useState("");
  const [accessToken, setAccessToken] = useState("");
  const [images, setImages] = useState([]);
  const [selectedOperation, setSelectedOperation] = useState("none");
  const [selectedImageName, setSelectedImageName] = useState("");
  const [selectedFile, setSelectedFile] = useState(null);
  const fileInputRef = useRef(null);
  const [selectedDescription, setSelectedDescription] = useState("");
  const [useAIDescription, setUseAIDescription] = useState(true);
  const [notifyByEmail, setNotifyByEmail] = useState(true);
  const [lastNotification, setLastNotification] = useState("");
  const [previewImage, setPreviewImage] = useState(null);
  const [isLoadingImages, setIsLoadingImages] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  const authFetch = async (path, options = {}) => {
    const isFormData = options.body instanceof FormData;
    const baseHeaders = { ...(options.headers || {}) };
    if (isFormData) {
      delete baseHeaders["Content-Type"];
      delete baseHeaders["content-type"];
    }
    const response = await fetch(apiUrl(path), {
      ...options,
      headers: {
        ...baseHeaders,
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
    });

    return response;
  };

  const handleLogin = async () => {
    if (!loginEmail.trim() || !loginPassword) {
      setErrorMessage("Email and password are required.");
      return false;
    }

    try {
      setErrorMessage("");
      const response = await fetch(
        apiUrl("/api/auth/login?useCookies=false&useSessionCookies=false"),
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            email: loginEmail.trim(),
            password: loginPassword,
          }),
        },
      );

      const bodyText = await response.text();
      let body = null;
      if (bodyText) {
        try {
          body = JSON.parse(bodyText);
        } catch {
          body = null;
        }
      }

      if (!response.ok) {
        if (response.status === 401) {
          setErrorMessage("Incorrect email or password");
          return false;
        }
        const msg = parseProblemMessage(body) || "Sign-in failed.";
        setErrorMessage(msg);
        return false;
      }

      const token = body?.accessToken ?? body?.access_token;
      if (!token || typeof token !== "string") {
        setErrorMessage("Sign-in succeeded but no access token was returned.");
        return false;
      }

      const email = loginEmail.trim();
      setAccessToken(token);
      setCurrentUserEmail(email);
      setIsLoggedIn(true);
      persistSession(token, email);
      return true;
    } catch (error) {
      setErrorMessage(error.message || "Failed to login.");
      return false;
    }
  };

  const handleRegister = async () => {
    if (!loginEmail.trim() || !loginPassword) {
      setErrorMessage("Email and password are required.");
      return;
    }

    try {
      setErrorMessage("");
      const response = await fetch(apiUrl("/api/auth/register"), {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          email: loginEmail.trim(),
          password: loginPassword,
        }),
      });

      const bodyText = await response.text();
      let body = null;
      if (bodyText) {
        try {
          body = JSON.parse(bodyText);
        } catch {
          body = null;
        }
      }

      if (!response.ok) {
        const msg = parseProblemMessage(body) || "Registration failed.";
        throw new Error(msg);
      }

      await handleLogin();
    } catch (error) {
      setErrorMessage(error.message || "Failed to register.");
    }
  };

  const handleLogout = () => {
    clearStoredSession();
    setIsLoggedIn(false);
    setAccessToken("");
    setCurrentUserEmail("");
  };

  const loadImages = async (pageToLoad = currentPage, pageSizeToLoad = pageSize) => {
    setIsLoadingImages(true);
    setErrorMessage("");
    try {
      const query = new URLSearchParams({
        page: String(pageToLoad),
        pageSize: String(pageSizeToLoad),
      });
      const response = await authFetch(`/api/images?${query.toString()}`);
      if (response.status === 401) {
        handleLogout();
        setErrorMessage("Session expired. Please sign in again.");
        return;
      }
      if (!response.ok) {
        const bodyText = await response.text();
        let body = null;
        if (bodyText) {
          try {
            body = JSON.parse(bodyText);
          } catch {
            body = null;
          }
        }
        throw new Error(parseProblemMessage(body) || "Failed to load images.");
      }

      const data = await response.json();
      const payload = Array.isArray(data)
        ? {
            items: data,
            page: pageToLoad,
            pageSize: pageSizeToLoad,
            totalCount: data.length,
            totalPages: data.length > 0 ? 1 : 0,
          }
        : data;

      const itemList = payload.items ?? payload.Items ?? [];
      const nextTotalPages = payload.totalPages ?? payload.TotalPages ?? 0;
      const nextTotalCount = payload.totalCount ?? payload.TotalCount ?? 0;
      if (nextTotalPages > 0 && pageToLoad > nextTotalPages) {
        setCurrentPage(nextTotalPages);
        return;
      }

      setImages(itemList.map(mapApiImage));
      setTotalCount(nextTotalCount);
      setTotalPages(nextTotalPages);
      if (nextTotalPages === 0 && currentPage !== 1) {
        setCurrentPage(1);
      }
    } catch (error) {
      setErrorMessage(error.message || "Unexpected error while loading images.");
    } finally {
      setIsLoadingImages(false);
    }
  };

  const handleUpload = async (event) => {
    event.preventDefault();
    if (!selectedFile) {
      setErrorMessage("Choose an image file to upload.");
      return;
    }
    if (!selectedImageName.trim()) {
      setErrorMessage("Enter a name for the image.");
      return;
    }

    const formData = new FormData();
    formData.append("file", selectedFile);
    formData.append("name", selectedImageName.trim());
    const desc = selectedDescription.trim();
    if (desc) {
      formData.append("description", desc);
    }
    formData.append("useAiDescription", useAIDescription ? "true" : "false");
    formData.append("operation", selectedOperation);

    try {
      setErrorMessage("");
      setIsUploading(true);
      const response = await authFetch("/api/images", {
        method: "POST",
        body: formData,
      });

      if (response.status === 401) {
        handleLogout();
        setErrorMessage("Session expired. Please sign in again.");
        return;
      }
      if (!response.ok) {
        const bodyText = await response.text();
        let body = null;
        if (bodyText) {
          try {
            body = JSON.parse(bodyText);
          } catch {
            body = null;
          }
        }
        throw new Error(parseProblemMessage(body) || "Failed to upload image.");
      }

      await response.json();
      setCurrentPage(1);
      await loadImages(1, pageSize);

      if (notifyByEmail) {
        setLastNotification(`Email notification sent for ${selectedImageName.trim()}.`);
      } else {
        setLastNotification("");
      }

      setSelectedImageName("");
      setSelectedDescription("");
      setSelectedFile(null);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    } catch (error) {
      setErrorMessage(error.message || "Unexpected error while uploading image.");
    } finally {
      setIsUploading(false);
    }
  };

  const handleDeleteImage = async (id) => {
    try {
      setErrorMessage("");
      const response = await authFetch(`/api/images/${id}`, {
        method: "DELETE",
      });

      if (response.status === 401) {
        handleLogout();
        setErrorMessage("Session expired. Please sign in again.");
        return;
      }
      if (!response.ok) {
        throw new Error("Failed to delete image.");
      }

      if (previewImage?.id === id) {
        setPreviewImage(null);
      }
      await loadImages(currentPage, pageSize);
    } catch (error) {
      setErrorMessage(error.message || "Unexpected error while deleting image.");
    }
  };

  const handleOpenPreview = (item) => {
    if (isProcessingImage(item)) {
      return;
    }
    setPreviewImage(item);
  };

  const handleClosePreview = () => {
    setPreviewImage(null);
  };

  useEffect(() => {
    const onKeyDown = (event) => {
      if (event.key === "Escape") {
        handleClosePreview();
      }
    };

    if (previewImage) {
      window.addEventListener("keydown", onKeyDown);
    }

    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [previewImage]);

  useEffect(() => {
    const session = loadStoredSession();
    if (session) {
      setAccessToken(session.accessToken);
      setCurrentUserEmail(session.email);
      setLoginEmail(session.email);
      setIsLoggedIn(true);
    }
    setSessionReady(true);
  }, []);

  useEffect(() => {
    if (isLoggedIn && accessToken) {
      loadImages(currentPage, pageSize);
    }
  }, [isLoggedIn, accessToken, currentPage, pageSize]);

  if (!sessionReady) {
    return (
      <main className="flex min-h-screen items-center justify-center p-6">
        <p className="text-sm text-slate-500">Loading…</p>
      </main>
    );
  }

  if (!isLoggedIn) {
    return (
      <main className="mx-auto flex min-h-screen max-w-5xl items-center justify-center p-6">
        <section className="w-full max-w-xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
          <p className="mb-2 text-sm font-medium text-blue-700">Cloud Native Image Processing</p>
          <h1 className="text-3xl font-semibold text-slate-900">
            Sign in to manage your image library
          </h1>
          <p className="mt-3 text-slate-600">
            Sign in with ASP.NET Core Identity (local API). Create an account or log in, then manage
            your image library.
          </p>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              handleLogin();
            }}
            className="mt-6 space-y-3"
          >
            <input
              type="email"
              placeholder="Email"
              value={loginEmail}
              onChange={(event) => setLoginEmail(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
              required
            />
            <input
              type="password"
              placeholder="Password"
              value={loginPassword}
              onChange={(event) => setLoginPassword(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
              required
            />
            {errorMessage && (
              <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
                {errorMessage}
              </p>
            )}
            <div className="flex flex-wrap items-center gap-3">
              <button
                type="submit"
                className="rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
              >
                Login with Email
              </button>
              <button
                type="button"
                onClick={handleRegister}
                className="rounded-lg border border-slate-300 bg-white px-4 py-2 font-medium text-slate-700 hover:bg-slate-50"
              >
                Register with Email
              </button>
            </div>
          </form>
        </section>
      </main>
    );
  }

  return (
    <main className="mx-auto min-h-screen max-w-6xl p-6">
      <header className="mb-6 flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Image Library Dashboard</h1>
          <p className="text-sm text-slate-600">
            Signed in as {currentUserEmail} (email / password)
          </p>
        </div>
        <button
          onClick={handleLogout}
          className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          Logout
        </button>
      </header>

      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">Upload Image</h2>
        <p className="mt-1 text-sm text-slate-600">
          Choose or drop an image file. The API stores it in blob storage (multipart{" "}
          <code className="rounded bg-slate-100 px-1 text-xs">POST /api/images</code>) with your processing and AI options.
        </p>
        <form onSubmit={handleUpload} className="mt-4 grid gap-3 sm:grid-cols-2">
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*"
            className="sr-only"
            aria-label="Choose image file"
            onChange={(event) => {
              const file = event.target.files?.[0] ?? null;
              setSelectedFile(file);
              if (file) {
                setSelectedImageName((prev) => (prev.trim() ? prev : file.name));
              }
            }}
          />
          <div className="sm:col-span-2">
            <p className="mb-1 text-sm font-medium text-slate-700">Image file</p>
            <div
              role="button"
              tabIndex={0}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  fileInputRef.current?.click();
                }
              }}
              onDragOver={(e) => {
                e.preventDefault();
                e.stopPropagation();
              }}
              onDrop={(e) => {
                e.preventDefault();
                e.stopPropagation();
                const file = e.dataTransfer.files?.[0];
                if (file && file.type.startsWith("image/")) {
                  setSelectedFile(file);
                  setSelectedImageName((prev) => (prev.trim() ? prev : file.name));
                } else if (file) {
                  setErrorMessage("Please drop an image file (e.g. PNG or JPEG).");
                }
              }}
              className="flex min-h-[120px] cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-300 bg-slate-50/80 px-4 py-6 text-center transition hover:border-blue-400 hover:bg-slate-50"
              onClick={() => fileInputRef.current?.click()}
            >
              <span className="text-sm font-medium text-slate-800">Drop image here or click to browse</span>
              <span className="mt-1 text-xs text-slate-500">JPEG, PNG, WebP, GIF, …</span>
              <button
                type="button"
                className="mt-3 rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-slate-800 shadow-sm ring-1 ring-slate-200 hover:bg-slate-50"
                onClick={(e) => {
                  e.stopPropagation();
                  fileInputRef.current?.click();
                }}
              >
                Choose file
              </button>
            </div>
            {selectedFile && (
              <p className="mt-2 text-sm text-slate-600">
                Selected: <span className="font-medium text-slate-900">{selectedFile.name}</span>
                <span className="text-slate-500"> ({(selectedFile.size / 1024).toFixed(1)} KB)</span>
              </p>
            )}
          </div>
          <input
            type="text"
            placeholder="Display name (e.g. beach.png)"
            value={selectedImageName}
            onChange={(event) => setSelectedImageName(event.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
          />
          <select
            value={selectedOperation}
            onChange={(event) => setSelectedOperation(event.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
          >
            <option value="none">No processing</option>
            <option value="grayscale">Grayscale</option>
          </select>
          <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
            <input
              type="checkbox"
              checked={useAIDescription}
              onChange={(event) => setUseAIDescription(event.target.checked)}
            />
            AI generate description
          </label>
          <input
            type="text"
            placeholder="Manual description"
            value={selectedDescription}
            onChange={(event) => setSelectedDescription(event.target.value)}
            disabled={useAIDescription}
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:bg-slate-100 focus:border-blue-500 focus:outline-none sm:col-span-2"
          />
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              checked={notifyByEmail}
              onChange={(event) => setNotifyByEmail(event.target.checked)}
            />
            Send completion email
          </label>
          <button
            type="submit"
            disabled={isUploading}
            className="rounded-lg bg-slate-900 px-4 py-2 font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isUploading ? "Uploading…" : "Upload to API"}
          </button>
        </form>
        {lastNotification && (
          <p className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            {lastNotification}
          </p>
        )}
        {errorMessage && (
          <p className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
            {errorMessage}
          </p>
        )}
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">My Images</h2>
        <p className="mt-1 text-sm text-slate-600">
          List, view details, and delete uploaded items.
        </p>
        <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-slate-500">
            {totalCount > 0
              ? `Page ${currentPage} of ${Math.max(totalPages, 1)} (${totalCount} images)`
              : "No images yet."}
          </p>
          <div className="flex items-center gap-2">
            <label className="text-xs text-slate-600" htmlFor="page-size-select">
              Page size
            </label>
            <select
              id="page-size-select"
              value={pageSize}
              onChange={(event) => {
                const nextSize = Number(event.target.value) || 10;
                setPageSize(nextSize);
                setCurrentPage(1);
              }}
              className="rounded-md border border-slate-300 px-2 py-1 text-xs"
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
            </select>
            <button
              type="button"
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              disabled={currentPage <= 1 || isLoadingImages}
              className="rounded-md border border-slate-300 px-2 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-50"
            >
              Prev
            </button>
            <button
              type="button"
              onClick={() => setCurrentPage((p) => (totalPages > 0 ? Math.min(totalPages, p + 1) : p))}
              disabled={isLoadingImages || totalPages === 0 || currentPage >= totalPages}
              className="rounded-md border border-slate-300 px-2 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-50"
            >
              Next
            </button>
          </div>
        </div>
        <div className="mt-4 space-y-3">
          {isLoadingImages && (
            <p className="text-sm text-slate-500">Loading images from backend...</p>
          )}
          {!isLoadingImages && images.length === 0 && (
            <p className="text-sm text-slate-500">No images yet. Add one above.</p>
          )}
          {images.map((item) => (
            <article
              key={item.id}
              className="rounded-xl border border-slate-200 bg-slate-50 p-4 sm:flex sm:items-start sm:justify-between sm:gap-4"
            >
              <div className="flex items-start gap-3">
                <button
                  type="button"
                  onClick={() => handleOpenPreview(item)}
                  disabled={isProcessingImage(item)}
                  className="cursor-pointer overflow-hidden rounded-md border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-70"
                  aria-label={
                    isProcessingImage(item)
                      ? `${item.name} is processing`
                      : `Open preview for ${item.name}`
                  }
                >
                  {isProcessingImage(item) ? (
                    <div className="flex h-16 w-24 items-center justify-center bg-slate-200 text-[11px] font-medium text-slate-600">
                      Processing
                    </div>
                  ) : (
                    <AuthenticatedImage
                      imageId={item.id}
                      accessToken={accessToken}
                      alt={`${item.name} preview`}
                      className="h-16 w-24 object-cover"
                    />
                  )}
                </button>
                <div className="space-y-1">
                  <p className="font-medium text-slate-900">{item.name}</p>
                  <p className="text-xs text-slate-500">Uploaded: {item.uploadedAt}</p>
                  <p className="text-xs text-slate-500">Status: {item.status}</p>
                  <p className="pt-1 text-sm text-slate-700">{item.description}</p>
                </div>
              </div>
              <button
                onClick={() => handleDeleteImage(item.id)}
                className="mt-3 rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-sm text-rose-600 hover:bg-rose-50 sm:mt-0"
              >
                Delete
              </button>
            </article>
          ))}
        </div>
      </section>

      {previewImage && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/75 p-4"
          onClick={handleClosePreview}
          role="button"
          tabIndex={0}
        >
          <div
            className="relative w-full max-w-4xl rounded-xl bg-white p-3 shadow-xl"
            onClick={(event) => event.stopPropagation()}
          >
            <button
              type="button"
              onClick={handleClosePreview}
              className="absolute right-3 top-3 rounded-md bg-slate-900 px-2 py-1 text-sm font-medium text-white hover:bg-slate-700"
            >
              Close
            </button>
            <AuthenticatedImage
              imageId={previewImage.id}
              accessToken={accessToken}
              alt={`${previewImage.name} full preview`}
              className="max-h-[80vh] w-full rounded-md object-contain"
            />
            <p className="mt-2 text-sm text-slate-600">{previewImage.name}</p>
          </div>
        </div>
      )}
    </main>
  );
}

export default App;
