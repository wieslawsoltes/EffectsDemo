using Avalonia;
using Avalonia.Media;

namespace EffectsDemo;

internal record struct LottiePayload(
    LottieCommand LottieCommand,
    object? Animation = null,
    Size? Size = default,
    Stretch? Stretch = null,
    StretchDirection? StretchDirection = null);
