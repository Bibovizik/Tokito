namespace Tokito.DTOs.UserDTOs
{
    public class ChangeCountryResultDto
    {
        public string PreviousCountryCode { get; set; } = null!;
        public string CountryCode { get; set; } = null!;
        public string PreviousCurrencyCode { get; set; } = null!;
        public string CurrencyCode { get; set; } = null!;
        public decimal PreviousBalance { get; set; }
        public decimal ConvertedBalance { get; set; }
        public DateTime ChangedAt { get; set; }
        public bool BalanceWasConverted { get; set; }
    }
}
