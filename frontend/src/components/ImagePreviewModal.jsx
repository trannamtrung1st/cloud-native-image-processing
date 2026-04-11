import { useEffect } from "react";
import { AuthenticatedImage } from "./AuthenticatedImage.jsx";

export function ImagePreviewModal({
  previewImage,
  accessToken,
  previewDetails,
  previewDetailsLoading,
  previewDetailsError,
  onClose,
}) {
  useEffect(() => {
    const onKeyDown = (event) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    if (previewImage) {
      window.addEventListener("keydown", onKeyDown);
    }

    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [previewImage, onClose]);

  if (!previewImage) {
    return null;
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/75 p-4"
      onClick={onClose}
      role="button"
      tabIndex={0}
    >
      <div
        className="relative flex max-h-[90vh] w-full max-w-5xl flex-col gap-4 overflow-hidden rounded-xl bg-white p-3 pt-12 shadow-xl md:flex-row md:items-start md:gap-6 md:p-5 md:pt-14"
        onClick={(event) => event.stopPropagation()}
      >
        <button
          type="button"
          onClick={onClose}
          className="absolute right-3 top-3 z-10 rounded-md bg-slate-900 px-2 py-1 text-sm font-medium text-white hover:bg-slate-700"
        >
          Close
        </button>
        <div className="min-h-0 min-w-0 flex-1 overflow-auto">
          <AuthenticatedImage
            imageId={previewImage.id}
            accessToken={accessToken}
            alt={`${previewImage.name} full preview`}
            className="max-h-[min(80vh,720px)] w-full rounded-md object-contain"
          />
        </div>
        <aside className="w-full shrink-0 border-t border-slate-200 pt-4 md:w-72 md:border-l md:border-t-0 md:pl-6 md:pt-0">
          <h3 className="text-sm font-semibold text-slate-900">Details</h3>
          {previewDetailsLoading && (
            <div className="mt-3 space-y-2" aria-busy="true">
              <div className="h-4 animate-pulse rounded bg-slate-200" />
              <div className="h-4 w-4/5 animate-pulse rounded bg-slate-200" />
              <div className="h-4 w-2/3 animate-pulse rounded bg-slate-200" />
            </div>
          )}
          {!previewDetailsLoading && previewDetailsError && (
            <p className="mt-3 text-sm text-rose-600">{previewDetailsError}</p>
          )}
          {!previewDetailsLoading && !previewDetailsError && previewDetails && (
            <dl className="mt-3 space-y-2 text-sm">
              <div>
                <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  Name
                </dt>
                <dd className="mt-0.5 text-slate-900">{previewDetails.name}</dd>
              </div>
              <div>
                <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  Status
                </dt>
                <dd className="mt-0.5 text-slate-900">{previewDetails.status}</dd>
              </div>
              <div>
                <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  Operation
                </dt>
                <dd className="mt-0.5 capitalize text-slate-900">{previewDetails.operation}</dd>
              </div>
              <div>
                <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  Uploaded
                </dt>
                <dd className="mt-0.5 text-slate-900">{previewDetails.uploadedAt}</dd>
              </div>
              <div>
                <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  Description
                </dt>
                <dd className="mt-0.5 text-slate-800">{previewDetails.description}</dd>
              </div>
              {previewDetails.previewUrl ? (
                <div>
                  <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                    Preview URL
                  </dt>
                  <dd className="mt-0.5 break-all text-blue-700">
                    <a
                      href={previewDetails.previewUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="underline hover:text-blue-900"
                    >
                      {previewDetails.previewUrl}
                    </a>
                  </dd>
                </div>
              ) : null}
            </dl>
          )}
        </aside>
      </div>
    </div>
  );
}
