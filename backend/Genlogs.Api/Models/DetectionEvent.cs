namespace Genlogs.Api.Models;

public class DetectionEvent
{
    public int DetectionId { get; set; }

    public int LaneId { get; set; }
    public Lane Lane { get; set; } = null!;

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateOnly CapturedAt { get; set; }
}
