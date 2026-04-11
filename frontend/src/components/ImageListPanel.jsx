import { AuthenticatedImage } from "./AuthenticatedImage.jsx";
import { isProcessingImage } from "../lib/images.js";

export function ImageListPanel({
  images,
  isLoadingImages,
  currentPage,
  pageSize,
  totalCount,
  totalPages,
  accessToken,
  onPageSizeChange,
  onRefresh,
  onPrevPage,
  onNextPage,
  onOpenPreview,
  onDelete,
}) {
  return (
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
            onChange={(event) => onPageSizeChange(Number(event.target.value) || 10)}
            className="rounded-md border border-slate-300 px-2 py-1 text-xs"
          >
            <option value={5}>5</option>
            <option value={10}>10</option>
            <option value={20}>20</option>
          </select>
          <button
            type="button"
            onClick={onRefresh}
            disabled={isLoadingImages}
            className="rounded-md border border-slate-300 bg-white px-2 py-1 text-xs hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
            aria-label="Refresh image list"
          >
            {isLoadingImages ? "Refreshing…" : "Refresh"}
          </button>
          <button
            type="button"
            onClick={onPrevPage}
            disabled={currentPage <= 1 || isLoadingImages}
            className="rounded-md border border-slate-300 px-2 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-50"
          >
            Prev
          </button>
          <button
            type="button"
            onClick={onNextPage}
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
                onClick={() => onOpenPreview(item)}
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
              type="button"
              onClick={() => onDelete(item.id)}
              className="mt-3 rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-sm text-rose-600 hover:bg-rose-50 sm:mt-0"
            >
              Delete
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}
