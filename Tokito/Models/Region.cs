using System;
using System.Collections.Generic;

namespace Tokito.Models;

public partial class Region
{
    public int RegionId { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string CurrencySymbol { get; set; } = null!;
    public bool IsSupported { get; set; } = true;
    public string CountryCode { get; set; } = null!;
    public virtual ICollection<RegionalPrice> RegionalPrices { get; set; } = new List<RegionalPrice>();
}
