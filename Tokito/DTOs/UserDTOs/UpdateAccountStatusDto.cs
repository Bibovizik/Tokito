using System.ComponentModel.DataAnnotations;
using Tokito.Models;

namespace Tokito.DTOs.UserDTOs;

public class UpdateAccountStatusDto
{
    [Range(UserAccountStatusValues.Active, UserAccountStatusValues.Blocked)]
    public byte AccountStatus { get; set; }
}
