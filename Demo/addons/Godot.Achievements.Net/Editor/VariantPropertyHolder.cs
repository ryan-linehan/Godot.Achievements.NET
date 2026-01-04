#if TOOLS
using System;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Helper Resource to hold a single Variant property for the property editor.
/// </summary>
[Tool]
public partial class VariantPropertyHolder : Resource
{
    private Variant _value = String.Empty;

    [Export]
    public Variant Value
    {
        get => _value;
        set
        {
            _value = value;
            EmitChanged();
        }
    }
}
#endif
