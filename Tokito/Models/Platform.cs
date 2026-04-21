using System;
using System.Collections.Generic;

namespace Tokito.Models;

public partial class Platform
{
    public int PlatformId { get; set; }

    public string Name { get; set; } = null!;

    public string? Type { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public string? CountryId { get; set; }

    //public virtual Country? Country { get; set; }
}
