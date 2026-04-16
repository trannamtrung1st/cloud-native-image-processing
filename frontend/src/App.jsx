import { useCallback, useEffect, useRef, useState } from "react";
import { loginWithEmailPassword, registerAccount } from "./api/authApi.js";
import { LoginView } from "./components/LoginView.jsx";
import { ImageUploadSection } from "./components/ImageUploadSection.jsx";
import { ImageListPanel } from "./components/ImageListPanel.jsx";
import { ImagePreviewModal } from "./components/ImagePreviewModal.jsx";
import { useAuthFetch } from "./hooks/useAuthFetch.js";
import { useImagePreviewDetails } from "./hooks/useImagePreviewDetails.js";
import {
  clearStoredSession,
  loadStoredSession,
  persistSession,
} from "./lib/authSession.js";
import { isProcessingImage, mapApiImage } from "./lib/images.js";
import { parseProblemMessage, readResponseJson } from "./lib/http.js";
import { IMAGES_REFRESH_INTERVAL_MS } from "./config/api.js";

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
  const isRefreshingImagesRef = useRef(false);

  const authFetch = useAuthFetch(accessToken);
  const {
    details: previewDetails,
    loading: previewDetailsLoading,
    error: previewDetailsError,
  } = useImagePreviewDetails(previewImage, accessToken, authFetch);

  const handleLogout = useCallback(() => {
    clearStoredSession();
    setIsLoggedIn(false);
    setAccessToken("");
    setCurrentUserEmail("");
  }, []);

  const loadImages = useCallback(
    async (
      pageToLoad = currentPage,
      pageSizeToLoad = pageSize,
      options = {},
    ) => {
      const { silent = false } = options;
      if (isRefreshingImagesRef.current) {
        return;
      }

      isRefreshingImagesRef.current = true;
      if (!silent) {
        setIsLoadingImages(true);
        setErrorMessage("");
      }
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
          const { json: body } = await readResponseJson(response);
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
        if (!silent) {
          setErrorMessage(error.message || "Unexpected error while loading images.");
        }
      } finally {
        isRefreshingImagesRef.current = false;
        if (!silent) {
          setIsLoadingImages(false);
        }
      }
    },
    [authFetch, currentPage, pageSize, handleLogout],
  );

  const handleLogin = async () => {
    if (!loginEmail.trim() || !loginPassword) {
      setErrorMessage("Email and password are required.");
      return false;
    }

    try {
      setErrorMessage("");
      const { response, json: body } = await loginWithEmailPassword(
        loginEmail.trim(),
        loginPassword,
      );

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
      const { response, json: body } = await registerAccount(
        loginEmail.trim(),
        loginPassword,
      );

      if (!response.ok) {
        const msg = parseProblemMessage(body) || "Registration failed.";
        throw new Error(msg);
      }

      await handleLogin();
    } catch (error) {
      setErrorMessage(error.message || "Failed to register.");
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
        const { json: body } = await readResponseJson(response);
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
      const safeId = encodeURIComponent(String(id));
      const response = await authFetch(`/api/images/${safeId}`, {
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

  const handleClosePreview = useCallback(() => {
    setPreviewImage(null);
  }, []);

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
  }, [isLoggedIn, accessToken, currentPage, pageSize, loadImages]);

  useEffect(() => {
    if (!isLoggedIn || !accessToken) {
      return undefined;
    }

    const intervalId = setInterval(() => {
      if (document.visibilityState !== "visible") {
        return;
      }

      loadImages(currentPage, pageSize, { silent: true });
    }, IMAGES_REFRESH_INTERVAL_MS);

    return () => clearInterval(intervalId);
  }, [
    isLoggedIn,
    accessToken,
    currentPage,
    pageSize,
    loadImages,
    IMAGES_REFRESH_INTERVAL_MS,
  ]);

  if (!sessionReady) {
    return (
      <main className="flex min-h-screen items-center justify-center p-6">
        <p className="text-sm text-slate-500">Loading…</p>
      </main>
    );
  }

  if (!isLoggedIn) {
    return (
      <LoginView
        loginEmail={loginEmail}
        loginPassword={loginPassword}
        errorMessage={errorMessage}
        onEmailChange={setLoginEmail}
        onPasswordChange={setLoginPassword}
        onSubmit={handleLogin}
        onRegister={handleRegister}
      />
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
          type="button"
          onClick={handleLogout}
          className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          Logout
        </button>
      </header>

      <ImageUploadSection
        fileInputRef={fileInputRef}
        selectedFile={selectedFile}
        selectedImageName={selectedImageName}
        selectedDescription={selectedDescription}
        selectedOperation={selectedOperation}
        useAIDescription={useAIDescription}
        notifyByEmail={notifyByEmail}
        isUploading={isUploading}
        lastNotification={lastNotification}
        errorMessage={errorMessage}
        onImageNameChange={setSelectedImageName}
        onDescriptionChange={setSelectedDescription}
        onOperationChange={setSelectedOperation}
        onUseAiChange={setUseAIDescription}
        onNotifyChange={setNotifyByEmail}
        onFileInputChange={(event) => {
          const file = event.target.files?.[0] ?? null;
          setSelectedFile(file);
          if (file) {
            setSelectedImageName((prev) => (prev.trim() ? prev : file.name));
          }
        }}
        onDropZoneClick={() => fileInputRef.current?.click()}
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
        onDragOver={(e) => {
          e.preventDefault();
          e.stopPropagation();
        }}
        onDropZoneKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            fileInputRef.current?.click();
          }
        }}
        onChooseFileClick={(e) => {
          e.stopPropagation();
          fileInputRef.current?.click();
        }}
        onSubmit={handleUpload}
      />

      <ImageListPanel
        images={images}
        isLoadingImages={isLoadingImages}
        currentPage={currentPage}
        pageSize={pageSize}
        totalCount={totalCount}
        totalPages={totalPages}
        accessToken={accessToken}
        onPageSizeChange={(nextSize) => {
          setPageSize(nextSize);
          setCurrentPage(1);
        }}
        onRefresh={() => loadImages(currentPage, pageSize)}
        onPrevPage={() => setCurrentPage((p) => Math.max(1, p - 1))}
        onNextPage={() =>
          setCurrentPage((p) => (totalPages > 0 ? Math.min(totalPages, p + 1) : p))
        }
        onOpenPreview={(item) => {
          if (!isProcessingImage(item)) {
            setPreviewImage(item);
          }
        }}
        onDelete={handleDeleteImage}
      />

      <ImagePreviewModal
        previewImage={previewImage}
        accessToken={accessToken}
        previewDetails={previewDetails}
        previewDetailsLoading={previewDetailsLoading}
        previewDetailsError={previewDetailsError}
        onClose={handleClosePreview}
      />
    </main>
  );
}

export default App;
