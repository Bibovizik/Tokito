using Tokito.DTOs.GameDTOs;

namespace Tokito.Services.Statuses.GameStatuses
{
    public class DeleteGameResult
    {
        public DeleteGameStatus Status { get; init; }
        public DeleteGameResultDto? Value { get; init; }
        public bool IsSuccess => Status == DeleteGameStatus.Success;
        public string? Message { get; init; }

        public static DeleteGameResult Success(DeleteGameResultDto deletion) =>
            new()
            {
                Status = DeleteGameStatus.Success,
                Value = deletion,
                Message = $"Deleted game with id: {deletion.GameId}, by Admin/Publisher {deletion.DeleteInitializerId}"
            };

        public static DeleteGameResult Failure(DeleteGameStatus status, string message) =>
            new()
            {
                Status = status,
                Message = message
            };
    }
}
