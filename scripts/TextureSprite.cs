using Godot;

/// <summary>
/// Sprite2D that takes its texture as a resource <em>path</em> instead of a Texture2D.
/// </summary>
/// <remarks>
/// Works around the Godot MCP bridge, which cannot write object-typed properties:
/// handle_set_property in addons/godot_mcp/command_handler.gd coerces only
/// bool/int/float/String/Vector2/Vector3/Color, so a texture assignment is silently
/// dropped (it still reports success). A string export does survive that path, so the
/// assignment lands in the .tscn properly instead of being hardcoded in a script.
/// </remarks>
[Tool]
[GlobalClass]
public partial class TextureSprite : Sprite2D
{
    private string _texturePath = "";

    [Export(PropertyHint.File, "*.png,*.jpg,*.webp")]
    public string TexturePath
    {
        get => _texturePath;
        set
        {
            _texturePath = value;
            ApplyTexture();
        }
    }

    public override void _Ready()
    {
        ApplyTexture();
    }

    private void ApplyTexture()
    {
        if (string.IsNullOrEmpty(_texturePath))
            return;

        if (!ResourceLoader.Exists(_texturePath))
        {
            GD.PushWarning($"TextureSprite: no such resource: {_texturePath}");
            return;
        }

        Texture = GD.Load<Texture2D>(_texturePath);
    }
}
