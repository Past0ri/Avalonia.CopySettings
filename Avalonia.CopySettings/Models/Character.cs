using Avalonia.Media.Imaging;

namespace Avalonia.CopySettings.Models
{
    public class Character
    {
        public string? CharacterName { get; set; }

        public string? CharacterId { get; set; }

        public string? CharacterFilePath { get; set; }

        public Bitmap? CharacterPotrait { get; set; }
    }
}