using System;
using System.Collections.Generic;

namespace Tokito.Models;

public partial class Region
{
    public int RegionId { get; set; }

    public string Name { get; set; } = null!;

    public string CurrencyCode { get; set; } = null!;

    public virtual ICollection<Country> Countries { get; set; } = new List<Country>();

    public virtual ICollection<RegionalPrice> RegionalPrices { get; set; } = new List<RegionalPrice>();
}
