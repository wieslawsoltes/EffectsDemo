using System.IO;
using System.Numerics;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace EffectsDemo;

public partial class ShaderAnimationControl : UserControl
{
   private record struct DrawPayload(
        HandlerCommand HandlerCommand,
        Uri? ShaderCode = default,
        Size? ShaderSize = default,
        Size? Size = default,
        Stretch? Stretch = default,
        StretchDirection? StretchDirection = default);

    private enum HandlerCommand
    {
        Start,
        Stop,
        Update,
        Dispose
    }

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both);

    public static readonly StyledProperty<Uri> ShaderUriProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, Uri>("ShaderUri");

    public static readonly StyledProperty<double> ShaderWidthProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>("ShaderWidth", defaultValue: 512);

    public static readonly StyledProperty<double> ShaderHeightProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>("ShaderHeight", defaultValue: 512);

    public static readonly StyledProperty<bool> IsShaderFillCanvasProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, bool>("IsShaderFillCanvas");

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public Uri ShaderUri
    {
        get => GetValue(ShaderUriProperty);
        set => SetValue(ShaderUriProperty, value);
    }

    public double ShaderWidth
    {
        get => GetValue(ShaderWidthProperty);
        set => SetValue(ShaderWidthProperty, value);
    }

    public double ShaderHeight
    {
        get => GetValue(ShaderHeightProperty);
        set => SetValue(ShaderHeightProperty, value);
    }

    public bool IsShaderFillCanvas
    {
        get => GetValue(IsShaderFillCanvasProperty);
        set => SetValue(IsShaderFillCanvasProperty, value);
    }

    private CompositionCustomVisual? _customVisual;

    protected override void OnLoaded(RoutedEventArgs routedEventArgs)
    {
        base.OnLoaded(routedEventArgs);

        var elemVisual = ElementComposition.GetElementVisual(this);
        var compositor = elemVisual?.Compositor;
        if (compositor is null)
        {
            return;
        }

        _customVisual = compositor.CreateCustomVisual(new DrawCompositionCustomVisualHandler());
        ElementComposition.SetElementChildVisual(this, _customVisual);

        LayoutUpdated += OnLayoutUpdated;

        _customVisual.Size = new Vector2((float)Bounds.Size.Width, (float)Bounds.Size.Height);

        _customVisual.SendHandlerMessage(
            new DrawPayload(
                HandlerCommand.Update,
                null,
                GetFinalShaderSize(),
                Bounds.Size,
                Stretch,
                StretchDirection));

        Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;

        Stop();
        DisposeImpl();
    }


    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_customVisual == null)
        {
            return;
        }

        _customVisual.Size = new Vector2((float)Bounds.Size.Width, (float)Bounds.Size.Height);
        _customVisual.SendHandlerMessage(
            new DrawPayload(
                HandlerCommand.Update,
                null,
                GetFinalShaderSize(),
                Bounds.Size,
                Stretch,
                StretchDirection));
    }

    private Size GetFinalShaderSize()
    {
        return IsShaderFillCanvas ? Bounds.Size : new Size(ShaderWidth, ShaderHeight);
    }

    private void Start()
    {
        _customVisual?.SendHandlerMessage(
            new DrawPayload(
                HandlerCommand.Start,
                ShaderUri,
                new Size(ShaderWidth, ShaderHeight),
                Bounds.Size,
                Stretch,
                StretchDirection));
    }


    private void Stop()
    {
        _customVisual?.SendHandlerMessage(new DrawPayload(HandlerCommand.Stop));
    }

    private void DisposeImpl()
    {
        _customVisual?.SendHandlerMessage(new DrawPayload(HandlerCommand.Dispose));
    }

    private class DrawCompositionCustomVisualHandler : CompositionCustomVisualHandler
    {
        private bool _running;
        private Stretch? _stretch;
        private StretchDirection? _stretchDirection;
        private Size? _boundsSize;
        private Size? _shaderSize;
        private string? _shaderCode;
        private readonly object _sync = new(); 
        private SKRuntimeEffectUniforms? _uniforms;
        private SKRuntimeEffect? _effect;
        private bool _isDisposed;

        public override void OnMessage(object message)
        {
            if (message is not DrawPayload msg)
            {
                return;
            }

            switch (msg)
            {
                case
                {
                    HandlerCommand: HandlerCommand.Start,
                    ShaderCode: { } uri,
                    ShaderSize: { } shaderSize,
                    Size: { } size,
                    Stretch: { } st,
                    StretchDirection: { } sd
                }:
                {
                    using var stream = AssetLoader.Open(uri);
                    using var txt = new StreamReader(stream);
                    _shaderCode = txt.ReadToEnd();

                    
                    _effect = SKRuntimeEffect.CreateShader(_shaderCode, out var errorText);
                    if (_effect == null)
                    {
                        Console.WriteLine($"Shader compilation error: {errorText}");
                    }
                    
                    _shaderSize = shaderSize;
                    _running = true;
                    _boundsSize = size;
                    _stretch = st;
                    _stretchDirection = sd;
                    RegisterForNextAnimationFrameUpdate();
                    break;
                }
                case
                {
                    HandlerCommand: HandlerCommand.Update,
                    ShaderSize: { } shaderSize,
                    Size: { } size,
                    Stretch: { } st,
                    StretchDirection: { } sd
                }:
                {
                    _shaderSize = shaderSize;
                    _boundsSize = size;
                    _stretch = st;
                    _stretchDirection = sd;
                    RegisterForNextAnimationFrameUpdate();
                    break;
                }
                case
                {
                    HandlerCommand: HandlerCommand.Stop
                }:
                {
                    _running = false;
                    break;
                }
                case
                {
                    HandlerCommand: HandlerCommand.Dispose
                }:
                {
                    DisposeImpl();
                    break;
                }
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            if (!_running || _isDisposed)
                return;

            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }

        private void DisposeImpl()
        {
            Debugger.Break();
            lock (_sync)
            {
                if (!_isDisposed)
                {
                    _isDisposed = true; 
                    _effect?.Dispose();
                    _uniforms?.Reset();
                    _running = false;
                }
            }
        }


        private void Draw(SKCanvas canvas)
        {
            if (_isDisposed || _effect is null)
                return;

            canvas.Save();

            var targetWidth = (float)(_shaderSize?.Width ?? 512);
            var targetHeight = (float)(_shaderSize?.Height ?? 512);

            _uniforms ??= new SKRuntimeEffectUniforms(_effect);

            _uniforms["iTime"] = (float)CompositionNow.TotalSeconds;
            _uniforms["iResolution"] = new[]
                { targetWidth, targetHeight, 0f };

            using (var paint = new SKPaint())
            using (var shader = _effect.ToShader(_uniforms))
            {
                paint.Shader = shader;
                canvas.DrawRect(SKRect.Create(targetWidth, targetHeight), paint);
            }


            canvas.Restore();
        }

        public override void OnRender(ImmediateDrawingContext context)
        {
            lock (_sync)
            {
                if (_stretch is not { } st
                    || _stretchDirection is not { } sd
                    || _isDisposed)
                {
                    return;
                }

                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature is null)
                {
                    return;
                }

                var rb = GetRenderBounds();

                var size = _boundsSize ?? rb.Size;

                var viewPort = new Rect(rb.Size);
                var sourceSize = _shaderSize!.Value;
                if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                {
                    return;
                }

                var scale = st.CalculateScaling(rb.Size, sourceSize, sd);
                var scaledSize = sourceSize * scale;
                var destRect = viewPort
                    .CenterRect(new Rect(scaledSize))
                    .Intersect(viewPort);
                var sourceRect = new Rect(sourceSize)
                    .CenterRect(new Rect(destRect.Size / scale));

                var bounds = SKRect.Create(new SKPoint(), new SKSize((float)size.Width, (float)size.Height));
                var scaleMatrix = Matrix.CreateScale(
                    destRect.Width / sourceRect.Width,
                    destRect.Height / sourceRect.Height);
                var translateMatrix = Matrix.CreateTranslation(
                    -sourceRect.X + destRect.X - bounds.Top,
                    -sourceRect.Y + destRect.Y - bounds.Left);

                using (context.PushClip(destRect))
                using (context.PushPostTransform(translateMatrix * scaleMatrix))
                {
                    using var lease = leaseFeature.Lease();
                    var canvas = lease.SkCanvas;
                    Draw(canvas);
                }
            }
        }
    }
}
