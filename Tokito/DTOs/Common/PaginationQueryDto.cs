using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.Common
{
    public class PaginationQueryDto
    {
        private const int DefaultPage = 1;
        private const int DefaultPageSize = 20;

        [Range(1, int.MaxValue)]
        public int? Page { get; set; }

        [Range(1, 100)]
        public int? PageSize { get; set; }

        public bool IsSpecified => Page.HasValue || PageSize.HasValue;

        public int ResolvedPage => Page ?? DefaultPage;

        public int ResolvedPageSize => PageSize ?? DefaultPageSize;
    }
}
