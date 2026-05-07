using PropertyChanged;

namespace Edi.Core.Gallery
{
    [AddINotifyPropertyChangedInterface]
    public class GamesConfig
    {
        // Optional: path to a root folder. Any immediate subfolder containing
        // Definitions.csv or Definitions_auto.csv is treated as a game.
        public string GamesRootPath { get; set; } = "";

        // Explicit name→path overrides / additions (loaded from config file)
        public Dictionary<string, string> Games { get; set; } = new();

        // Returns discovered games merged with manual Games entries.
        // Manual entries take precedence (same name wins).
        public Dictionary<string, string> GetAll()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(GamesRootPath) && Directory.Exists(GamesRootPath))
            {
                foreach (var dir in Directory.EnumerateDirectories(GamesRootPath))
                {
                    var hasDefs = File.Exists(Path.Combine(dir, "Definitions.csv"))
                               || File.Exists(Path.Combine(dir, "Definitions_auto.csv"));
                    if (hasDefs)
                        result[Path.GetFileName(dir)] = dir;
                }
            }

            // Manual entries override auto-discovered ones
            foreach (var kv in Games)
                result[kv.Key] = kv.Value;

            return result;
        }
    }
}
