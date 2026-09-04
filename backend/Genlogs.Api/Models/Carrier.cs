namespace Genlogs.Api.Models;

public class Carrier
{
    public int CarrierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UsdotNumber { get; set; } = string.Empty;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
