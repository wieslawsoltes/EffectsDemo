using Avalonia;
using Avalonia.Media;

namespace EffectsDemo;

internal record struct DrawPayload(
    HandlerCommand HandlerCommand,
    object? Animation = null,
    Size? Size = default,
    Stretch? Stretch = null,
    StretchDirection? StretchDirection = null);
