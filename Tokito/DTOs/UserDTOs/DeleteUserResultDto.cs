namespace Tokito.DTOs.UserDTOs;

public class DeleteUserResultDto
{
    public int UserId { get; set; }

    public bool WasPublisherAccount { get; set; }

    public int? PublisherId { get; set; }
}
