import { useEffect, useState } from "react";

const DEFAULT_PREVIEW_URL =
  "https://static.vecteezy.com/system/resources/thumbnails/006/242/192/small/mountain-winter-caucasus-landscape-with-white-glaciers-and-rocky-peak-photo.jpg";
const PREVIEW_WIDTH = 1280;
const PREVIEW_HEIGHT = 800;

const MOCK_USER = {
  name: "Trung Tran",
  email: "trung@example.com",
  provider: "Google (Azure AD B2C)",
};

const INITIAL_IMAGES = [
  {
    id: "img-101",
    name: "mountain.jpg",
    previewUrl: DEFAULT_PREVIEW_URL,
    uploadedAt: "2026-04-08 10:30",
    operation: "grayscale",
    description: "A snow-capped mountain under cloudy skies.",
    status: "Processed",
  },
  {
    id: "img-102",
    name: "city-night.png",
    previewUrl: `https://picsum.photos/seed/city-night/${PREVIEW_WIDTH}/${PREVIEW_HEIGHT}`,
    uploadedAt: "2026-04-09 21:12",
    operation: "none",
    description: "A downtown skyline with bright neon lights.",
    status: "Uploaded",
  },
];

const getPreviewUrl = (seed) =>
  seed.toLowerCase().includes("mountain")
    ? DEFAULT_PREVIEW_URL
    : `https://picsum.photos/seed/${encodeURIComponent(seed)}/${PREVIEW_WIDTH}/${PREVIEW_HEIGHT}`;

function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [images, setImages] = useState(INITIAL_IMAGES);
  const [selectedOperation, setSelectedOperation] = useState("none");
  const [selectedImageName, setSelectedImageName] = useState("");
  const [selectedDescription, setSelectedDescription] = useState("");
  const [useAIDescription, setUseAIDescription] = useState(true);
  const [notifyByEmail, setNotifyByEmail] = useState(true);
  const [lastNotification, setLastNotification] = useState("");
  const [previewImage, setPreviewImage] = useState(null);

  const handleLogin = () => setIsLoggedIn(true);

  const handleLogout = () => setIsLoggedIn(false);

  const handleUpload = (event) => {
    event.preventDefault();
    if (!selectedImageName.trim()) return;

    const uploadedImage = {
      id: `img-${Date.now()}`,
      name: selectedImageName.trim(),
      previewUrl: getPreviewUrl(selectedImageName.trim()),
      uploadedAt: new Date().toLocaleString(),
      operation: selectedOperation,
      description: useAIDescription
        ? "AI description pending... (mocked Azure Computer Vision + worker pipeline)"
        : selectedDescription.trim() || "No description provided.",
      status: "Queued",
    };

    setImages((current) => [uploadedImage, ...current]);
    if (notifyByEmail) {
      setLastNotification(
        `Email notification sent for ${uploadedImage.name}.`
      );
    } else {
      setLastNotification("");
    }
    setSelectedImageName("");
    setSelectedDescription("");
  };

  const handleDeleteImage = (id) => {
    setImages((current) => current.filter((item) => item.id !== id));
  };

  const handleOpenPreview = (item) => {
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

  if (!isLoggedIn) {
    return (
      <main className="mx-auto flex min-h-screen max-w-5xl items-center justify-center p-6">
        <section className="w-full max-w-xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
          <p className="mb-2 text-sm font-medium text-blue-700">Cloud Native Image Processing</p>
          <h1 className="text-3xl font-semibold text-slate-900">
            Sign in to manage your image library
          </h1>
          <p className="mt-3 text-slate-600">
            Prototype flow: Azure AD B2C sign-in with Google, image upload, grayscale
            processing, AI-generated description, and email notifications.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <button
              onClick={handleLogin}
              className="rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
            >
              Login with Azure AD B2C
            </button>
            <button
              onClick={handleLogin}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 font-medium text-slate-700 hover:bg-slate-50"
            >
              Continue with Google
            </button>
          </div>
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
            {MOCK_USER.name} ({MOCK_USER.email}) via {MOCK_USER.provider}
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
          Upload with optional grayscale processing and email notification.
        </p>
        <form onSubmit={handleUpload} className="mt-4 grid gap-3 sm:grid-cols-2">
          <input
            type="text"
            placeholder="Image file name (e.g. beach.png)"
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
            className="rounded-lg bg-slate-900 px-4 py-2 font-medium text-white hover:bg-slate-800"
          >
            Upload
          </button>
        </form>
        {lastNotification && (
          <p className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            {lastNotification}
          </p>
        )}
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-lg font-semibold text-slate-900">My Images</h2>
        <p className="mt-1 text-sm text-slate-600">
          List, view details, and delete uploaded items.
        </p>
        <div className="mt-4 space-y-3">
          {images.map((item) => (
            <article
              key={item.id}
              className="rounded-xl border border-slate-200 bg-slate-50 p-4 sm:flex sm:items-start sm:justify-between sm:gap-4"
            >
              <div className="flex items-start gap-3">
                <button
                  type="button"
                  onClick={() => handleOpenPreview(item)}
                  className="cursor-pointer overflow-hidden rounded-md border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500"
                  aria-label={`Open preview for ${item.name}`}
                >
                  <img
                    src={item.previewUrl}
                    alt={`${item.name} preview`}
                    className="h-16 w-24 object-cover"
                  />
                </button>
                <div className="space-y-1">
                  <p className="font-medium text-slate-900">{item.name}</p>
                  <p className="text-xs text-slate-500">Uploaded: {item.uploadedAt}</p>
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
            <img
              src={previewImage.previewUrl}
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
