namespace Tokito.Models;

public class RegionCountry
{
    public string CountryCode { get; set; } = null!;

    public int RegionId { get; set; }

    public virtual Region Region { get; set; } = null!;
}
