using System;
using System.Collections.Generic;

namespace Tokito.Models;

public partial class Publisher
{
    public int PublisherId { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly? FoundationDate { get; set; }
    public string? Website { get; set; }
    public string? CountryId { get; set; }

    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public virtual Country? Country { get; set; }
    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
