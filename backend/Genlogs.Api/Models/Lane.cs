namespace Genlogs.Api.Models;

public class Lane
{
    public int LaneId { get; set; }
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;

    // requirements.md 4.2.3 / design.md: a real seeded row, not an in-code fallback branch.
    public bool IsDefaultFallback { get; set; }

    public ICollection<DetectionEvent> DetectionEvents { get; set; } = new List<DetectionEvent>();
}
