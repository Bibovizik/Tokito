namespace Tokito.DTOs.Common
{
    public class PagedResultDto<T>
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();
    }
}
