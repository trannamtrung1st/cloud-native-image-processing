export function ImageUploadSection({
  fileInputRef,
  selectedFile,
  selectedImageName,
  selectedDescription,
  selectedOperation,
  useAIDescription,
  notifyByEmail,
  isUploading,
  lastNotification,
  errorMessage,
  onImageNameChange,
  onDescriptionChange,
  onOperationChange,
  onUseAiChange,
  onNotifyChange,
  onFileInputChange,
  onDropZoneClick,
  onDrop,
  onDragOver,
  onDropZoneKeyDown,
  onChooseFileClick,
  onSubmit,
}) {
  return (
    <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">Upload Image</h2>
      <p className="mt-1 text-sm text-slate-600">
        Choose or drop an image file. The API stores it in blob storage (multipart{" "}
        <code className="rounded bg-slate-100 px-1 text-xs">POST /api/images</code>) with your processing and AI options.
      </p>
      <form onSubmit={onSubmit} className="mt-4 grid gap-3 sm:grid-cols-2">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          className="sr-only"
          aria-label="Choose image file"
          onChange={onFileInputChange}
        />
        <div className="sm:col-span-2">
          <p className="mb-1 text-sm font-medium text-slate-700">Image file</p>
          <div
            role="button"
            tabIndex={0}
            onKeyDown={onDropZoneKeyDown}
            onDragOver={onDragOver}
            onDrop={onDrop}
            className="flex min-h-[120px] cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-300 bg-slate-50/80 px-4 py-6 text-center transition hover:border-blue-400 hover:bg-slate-50"
            onClick={onDropZoneClick}
          >
            <span className="text-sm font-medium text-slate-800">Drop image here or click to browse</span>
            <span className="mt-1 text-xs text-slate-500">JPEG, PNG, WebP, GIF, …</span>
            <button
              type="button"
              className="mt-3 rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-slate-800 shadow-sm ring-1 ring-slate-200 hover:bg-slate-50"
              onClick={onChooseFileClick}
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
          onChange={(event) => onImageNameChange(event.target.value)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        />
        <select
          value={selectedOperation}
          onChange={(event) => onOperationChange(event.target.value)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        >
          <option value="none">No processing</option>
          <option value="grayscale">Grayscale</option>
        </select>
        <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
          <input
            type="checkbox"
            checked={useAIDescription}
            onChange={(event) => onUseAiChange(event.target.checked)}
          />
          AI generate description
        </label>
        <input
          type="text"
          placeholder="Manual description"
          value={selectedDescription}
          onChange={(event) => onDescriptionChange(event.target.value)}
          disabled={useAIDescription}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:bg-slate-100 focus:border-blue-500 focus:outline-none sm:col-span-2"
        />
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={notifyByEmail}
            onChange={(event) => onNotifyChange(event.target.checked)}
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
  );
}
