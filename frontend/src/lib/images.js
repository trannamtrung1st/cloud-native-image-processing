export function mapApiImage(item) {
  return {
    id: item.id ?? item.Id,
    name: item.name,
    uploadedAt:
      item.createdAtUtc != null
        ? new Date(item.createdAtUtc).toLocaleString()
        : "—",
    operation: item.operation?.toLowerCase() || "none",
    description: item.description || "No description provided.",
    status: item.status || "Ready",
    previewUrl: item.previewUrl ?? item.PreviewUrl ?? null,
  };
}

export function isProcessingImage(item) {
  return (item.status || "").toLowerCase() === "processing";
}
