using System;
using System.Collections.Generic;

namespace Tokito.Models;

public partial class Region
{
    public int RegionId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string CurrencySymbol { get; set; } = null!;
    public bool IsSupported { get; set; } = true;
    public virtual ICollection<RegionCountry> Countries { get; set; } = new List<RegionCountry>();
    public virtual ICollection<RegionalPrice> RegionalPrices { get; set; } = new List<RegionalPrice>();
}
