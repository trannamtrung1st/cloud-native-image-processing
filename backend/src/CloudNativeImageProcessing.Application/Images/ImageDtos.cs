namespace CloudNativeImageProcessing.Application.Images;

public sealed record ImageDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    string Operation,
    DateTimeOffset CreatedAtUtc,
    string? PreviewUrl);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
