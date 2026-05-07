using PropertyChanged;

namespace Edi.Core.Gallery
{
    [AddINotifyPropertyChangedInterface]
    public class GamesConfig
    {
        // Root folder to scan. Any immediate subfolder containing
        // Definitions.csv or Definitions_auto.csv becomes a game entry.
        public string GalleryRootPath { get; set; } = "";

        // Cache of discovered games (name → path). Populated by Rescan().
        // When non-empty, GetAll() returns this directly instead of walking disk.
        public Dictionary<string, string> Games { get; set; } = new();

        // Returns the cached game list, or scans GalleryRootPath if cache is empty.
        public Dictionary<string, string> GetAll()
        {
            if (Games.Count > 0)
                return Games;

            return Scan();
        }

        // Walks GalleryRootPath, writes results into Games (triggers auto-save via PropertyChanged).
        public Dictionary<string, string> Rescan()
        {
            Games = Scan();
            return Games;
        }

        private Dictionary<string, string> Scan()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(GalleryRootPath) && Directory.Exists(GalleryRootPath))
            {
                foreach (var dir in Directory.EnumerateDirectories(GalleryRootPath))
                {
                    var hasDefs = File.Exists(Path.Combine(dir, "Definitions.csv"))
                               || File.Exists(Path.Combine(dir, "Definitions_auto.csv"));
                    if (hasDefs)
                        result[Path.GetFileName(dir)] = dir;
                }
            }

            return result;
        }
    }
}
