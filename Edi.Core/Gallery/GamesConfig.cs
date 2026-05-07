using PropertyChanged;

namespace Edi.Core.Gallery
{
    [AddINotifyPropertyChangedInterface]
    public class GamesConfig
    {
        // Key = display name shown in the UI, Value = path to that game's gallery folder
        public Dictionary<string, string> Games { get; set; } = new();
    }
}
