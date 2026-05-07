using PropertyChanged;

namespace Edi.Core.Gallery
{
    [AddINotifyPropertyChangedInterface]
    public class GamesConfig
    {
        // Root folder to scan. Any immediate subfolder containing
        // Definitions.csv or Definitions_auto.csv becomes a game entry.
        public string GalleryRootPath { get; set; } = "";

        public Dictionary<string, string> GetAll()
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
