using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Tokito.Models;
public partial class RegionalPrice
{
    public const string BasePriceSource = "BasePriceUah";
    public const string AutoExchangeRateSource = "AutoExchangeRate";
    public const string ManualOverrideSource = "ManualOverride";

    public int PriceId { get; set; }

    public int GameId { get; set; }

    public int RegionId { get; set; }

    public decimal Amount { get; set; }

    public string PriceSource { get; set; } = BasePriceSource;

    public decimal? ExchangeRateToUahSnapshot { get; set; }

    public DateOnly? ExchangeDate { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual Region Region { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
