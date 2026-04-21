using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Tokito.Models;
public partial class RegionalPrice
{
    public int PriceId { get; set; }

    public int GameId { get; set; }

    public int PlatformId { get; set; }

    public int RegionId { get; set; }

    public decimal Amount { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual Platform Platform { get; set; } = null!;

    public virtual Region Region { get; set; } = null!;
}
