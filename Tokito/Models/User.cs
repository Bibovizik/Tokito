using Microsoft.AspNetCore.Identity;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.General;
using System;
using System.Collections.Generic;

namespace Tokito.Models;

public partial class User : IdentityUser<int>
{
    public string UserNickname { get; set; } = string.Empty;
    public DateOnly? RegistrationDate { get; set; }

    public byte AccountStatus { get; set; }

    public string? CountryId { get; set; }
    public string CountryCode { get; set; } = null!;
    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual ICollection<GameReview> GameRatings { get; set; } = new List<GameReview>();
}
