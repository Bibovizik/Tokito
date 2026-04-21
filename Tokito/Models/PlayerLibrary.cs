namespace Tokito.Models;

public class PlayerLibrary
{
    public int PlayerId { get; set; }
    public int GameId { get; set; }
    public int PlatformId { get; set; }

    public DateTime AcquisitionDate { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Game Game { get; set; } = null!;
    public virtual Platform Platform { get; set; } = null!;
}