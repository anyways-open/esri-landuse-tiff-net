using System.Reflection;
using System.Text;
using GeoAPI.CoordinateSystems;
using ProjNet.CoordinateSystems;

namespace LandCover.ESRI;

internal static class SRIDReader
{
    private static readonly Lazy<CoordinateSystemFactory> CoordinateSystemFactory =
        new Lazy<CoordinateSystemFactory>(() => new CoordinateSystemFactory());
    private static readonly Dictionary<int, ICoordinateSystem> _coordinateSystems = new();

    public struct WktString
    {
        /// <summary>
        /// Well-known ID
        /// </summary>
        public int WktId;
        /// <summary>
        /// Well-known Text
        /// </summary>
        public string Wkt;
    }

    private static IEnumerable<WktString> GetSrids()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LandCover.ESRI.SRID.csv")
                           ?? throw new Exception("cannot read embedded SRIDs");
        using var sr = new StreamReader(stream, Encoding.UTF8);
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            int split = line.IndexOf(';');
            if (split <= -1) continue;

            var wkt = new WktString
            {
                WktId = int.Parse(line[..split]),
                Wkt = line[(split + 1)..]
            };
            yield return wkt;
        }
    }

    public static ICoordinateSystem GetWgs84()
    {
        return CoordinateSystemFactory.Value.CreateFromWkt(
            "GEOGCS[\"GCS_WGS_1984\",\r\n     DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137,298.257223563]],\r\n     PRIMEM[\"Greenwich\",0],\r\n     UNIT[\"Degree\",0.0174532925199433]\r\n]\r\n");
    }

    public static ICoordinateSystem GetCSbyID(int id)
    {
        if (_coordinateSystems.TryGetValue(id, out var crs)) return crs;

        foreach (var wkt in GetSrids())
        {
            if (wkt.WktId != id) continue;

            crs = CoordinateSystemFactory.Value.CreateFromWkt(wkt.Wkt);
            _coordinateSystems.Add(id, crs);
            return crs;
        }

        throw new Exception($"SRID with id {id} not found");
    }
}
