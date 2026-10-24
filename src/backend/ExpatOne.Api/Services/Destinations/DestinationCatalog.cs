namespace ExpatOne.Api.Services.Destinations;

public record DestinationPlace(string City, double Latitude, double Longitude, string Landmark);

// Presentation metadata only. The countries table controls which destinations exist/are active.
public sealed class DestinationCatalog(IConfiguration configuration)
{
    private static readonly IReadOnlyDictionary<string, DestinationPlace> Defaults =
        new Dictionary<string, DestinationPlace>(StringComparer.OrdinalIgnoreCase)
        {
            ["MY"] = new("Kuala Lumpur", 3.1390, 101.6869, "Petronas Twin Towers"),
            ["SG"] = new("Singapore", 1.3521, 103.8198, "Marina Bay Sands"),
            ["IN"] = new("Agra", 27.1767, 78.0081, "Taj Mahal"),
            ["AE"] = new("Dubai", 25.2048, 55.2708, "Burj Khalifa"),
            ["BH"] = new("Manama", 26.2235, 50.5876, "Bahrain World Trade Center"),
            ["CN"] = new("Beijing", 39.9042, 116.4074, "Temple of Heaven"),
            ["ID"] = new("Jakarta", -6.2088, 106.8456, "National Monument"),
            ["KW"] = new("Kuwait City", 29.3759, 47.9774, "Kuwait Towers"),
            ["OM"] = new("Muscat", 23.5880, 58.3829, "Sultan Qaboos Grand Mosque"),
            ["QA"] = new("Doha", 25.2854, 51.5310, "Museum of Islamic Art"),
            ["SA"] = new("Riyadh", 24.7136, 46.6753, "Kingdom Centre")
        };

    public DestinationPlace? Find(string code)
    {
        var section = configuration.GetSection($"Weather:Destinations:{code}");
        if (section.Exists())
        {
            var city = section["City"];
            var lat = section.GetValue<double?>("Latitude");
            var lon = section.GetValue<double?>("Longitude");
            if (!string.IsNullOrWhiteSpace(city) && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
                return new(city, lat.Value, lon.Value, section["Landmark"] ?? city);
            return null;
        }
        return Defaults.GetValueOrDefault(code);
    }

    public string? ImageUrl(string code) => configuration[$"Weather:Destinations:{code}:ImageUrl"];
}
