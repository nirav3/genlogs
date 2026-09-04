namespace Genlogs.Api.Models;

public class Vehicle
{
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;

    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;

    public ICollection<DetectionEvent> DetectionEvents { get; set; } = new List<DetectionEvent>();
}
