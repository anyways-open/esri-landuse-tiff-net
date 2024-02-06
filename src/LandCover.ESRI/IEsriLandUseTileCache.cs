namespace LandCover.ESRI;

/// <summary>
/// Abstract definition of the ESRI land use tile cache.
/// </summary>
public interface IEsriLandUseTileCache
{
    /// <summary>
    /// Gets a tile.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    bool TryGetTile(string fileName, int x, int y, out byte[] data);

    /// <summary>
    /// Sets the tile.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="data"></param>
    void SetTile(string fileName, int x, int y, byte[] data);
}
