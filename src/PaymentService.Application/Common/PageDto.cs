namespace PaymentService.Application.Common;

/// <summary>
/// Una página de resultados, con la misma forma que los listados paginados de security-service y
/// notification-service (la web ya tiene ese modelo, PageResponse). Page empieza en 0.
/// </summary>
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int Size, long TotalElements, int TotalPages)
{
    public static PageDto<T> Of(IReadOnlyList<T> items, int page, int size, long totalElements) =>
        new(items, page, size, totalElements, (int)((totalElements + size - 1) / size));
}
