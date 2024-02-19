using BitMiracle.LibTiff.Classic;
using GeoAPI.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems.Transformations;

namespace LandCover.ESRI;

/// <summary>
/// The ESRI land use tiff.
/// </summary>
public sealed class EsriLandUseTiff : IDisposable
{
    private readonly string _fileName;
    private Tiff? _tiff;
    private (double longitude, double latitude) _origin;
    private (double x, double y) _pixelSize;
    private (int x, int y) _resolution;
    private ICoordinateTransformation? _transformTo;
    private int _tileSize;
    private static readonly CoordinateTransformationFactory CoordinateTransformFactory = new();
    private const byte ResidentialValue = 7;

    /// <summary>
    /// Creates a new ESRI land use tiff.
    /// </summary>
    /// <param name="fileName">The path to the tiff file.</param>
    public EsriLandUseTiff(string fileName)
    {
        _fileName = fileName;
    }

    /// <summary>
    /// Returns the pixel value covering the given longitude/latitude or null if the tiff does not cover the pixel.
    /// </summary>
    /// <param name="longitude">The longitude.</param>
    /// <param name="latitude">The latitude.</param>
    /// <param name="cache">The cache, if any.</param>
    /// <returns>The pixel value, or null</returns>
    public byte? TryReadPixel(double longitude, double latitude, IEsriLandUseTileCache? cache = null)
    {
        var pixel = this.ToPixel(longitude, latitude);
        if (pixel == null) return null;

        return this.ReadPixel(pixel.Value.x, pixel.Value.y, cache);
    }

    private (int x, int y)? ToPixel(double longitude, double latitude)
    {
        this.ReadTiff();

        if (_transformTo == null) throw new Exception("Transform to cannot be null after reading tiff");
        var projected = _transformTo.MathTransform.Transform(new[] { longitude, latitude });
        var projX = projected[0];
        var projY = projected[1];
        var xCoord = (int)System.Math.Round((projX - _origin.longitude) / _pixelSize.x);
        var yCoord = (int)System.Math.Round((projY - _origin.latitude) / _pixelSize.y);

        if (xCoord < 0) return null;
        if (yCoord < 0) return null;
        if (xCoord >= _resolution.x) return null;
        if (yCoord >= _resolution.y) return null;

        return (xCoord, yCoord);
    }

    /// <summary>
    /// Reads the tiff to memory if not loaded already.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void ReadTiff()
    {
        if (_tiff != null) return;

        lock (this)
        {
            if (_tiff != null) return;

            var tiff = Tiff.Open(_fileName, "r");

            var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            _resolution = (width, height);

            _tileSize = tiff.GetField(TiffTag.TILEWIDTH)[0].ToInt();

            var modelPixelScaleTag = tiff.GetField((TiffTag)33550);
            var modelTiePointTag = tiff.GetField((TiffTag)33922);

            byte[] modelPixelScale = modelPixelScaleTag[1].GetBytes();
            double pixelSizeX = BitConverter.ToDouble(modelPixelScale, 0);
            double pixelSizeY = BitConverter.ToDouble(modelPixelScale, 8) * -1;
            _pixelSize = (pixelSizeX, pixelSizeY);

            byte[] modelTransformation = modelTiePointTag[1].GetBytes();
            double originLon = BitConverter.ToDouble(modelTransformation, 24);
            double originLat = BitConverter.ToDouble(modelTransformation, 32);
            _origin = (originLon, originLat);

            var geoKeyDirectoryTag = tiff.GetField(TiffTag.GEOTIFF_GEOKEYDIRECTORYTAG);
            var geoKeyDirectoryTagBytes = geoKeyDirectoryTag[1].ToByteArray();
            ushort? crsId = null;
            for (var i = 0; i < geoKeyDirectoryTagBytes.Length; i += 2)
            {
                if (i % 8 != 0) continue;
                var value = BitConverter.ToUInt16(geoKeyDirectoryTagBytes, i);
                if (value == 3072) // https://docs.ogc.org/is/19-008r4/19-008r4.html#_requirements_class_projectedcrsgeokey
                {
                    crsId = BitConverter.ToUInt16(geoKeyDirectoryTagBytes, i + 6);
                }
            }

            if (crsId == null) throw new Exception("Could not read crs");
            var crs = SRIDReader.GetCSbyID(crsId.Value);
            if (crs == null) throw new Exception("Count not load crs");

            _transformTo = CoordinateTransformFactory.CreateFromCoordinateSystems(SRIDReader.GetWgs84(), crs);
            
            _tiff = tiff;
        }
    }

    private byte? ReadPixel(int x, int y, IEsriLandUseTileCache? cache = null)
    {
        if (_tiff == null) throw new Exception("Read tiff first");

        var tileXCoord = (x / _tileSize) * _tileSize;
        var tileYCoord = (y / _tileSize) * _tileSize;

        byte[]? buffer = null;
        if (cache != null)
        {
            if (!cache.TryGetTile(_fileName, tileXCoord, tileYCoord, out buffer))
            {
                buffer = null;
            }
        }

        if (buffer == null)
        {
            buffer = new byte[_tileSize * _tileSize];
            _tiff.ReadTile(buffer, 0, x, y, 0, 0);

            cache?.SetTile(_fileName, tileXCoord, tileYCoord, buffer);
        }
        var indexRev = ((y - tileYCoord) * _tileSize) + (x - tileXCoord);

        return buffer[indexRev];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _tiff?.Dispose();
    }
}
