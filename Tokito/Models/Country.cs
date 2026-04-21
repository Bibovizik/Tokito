using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Tokito.Models;

public partial class Country
{
    public string Name { get; set; } = null!;
    [Key]
    public string Code { get; set; } = null!;

    public int RegionId { get; set; }

    public virtual ICollection<Platform> Platforms { get; set; } = new List<Platform>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Publisher> Publishers { get; set; } = new List<Publisher>();

    public virtual Region Region { get; set; } = null!;
}
