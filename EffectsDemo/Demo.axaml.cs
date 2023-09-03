using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace EffectsDemo;

public partial class Demo : UserControl
{
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<Demo, Stretch>(nameof(Stretch), Stretch.Uniform);

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<Demo, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both);

    public Stretch Stretch
    {
        get { return GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    public StretchDirection StretchDirection
    {
        get { return GetValue(StretchDirectionProperty); }
        set { SetValue(StretchDirectionProperty, value); }
    }
    
    private CompositionCustomVisual? _customVisual;

    public Demo()
    {
        InitializeComponent();
    }
    
    protected override void OnLoaded(RoutedEventArgs routedEventArgs)
    {
        base.OnLoaded(routedEventArgs);

        var elemVisual = ElementComposition.GetElementVisual(this);
        var compositor = elemVisual?.Compositor;
        if (compositor is null)
        {
            return;
        }
        
        _customVisual = compositor.CreateCustomVisual(new LottieCompositionCustomVisualHandler());
        ElementComposition.SetElementChildVisual(this, _customVisual);

        LayoutUpdated += OnLayoutUpdated;

        _customVisual.Size = new Vector2((float)Bounds.Size.Width, (float)Bounds.Size.Height);
        _customVisual.SendHandlerMessage(
            new LottiePayload(
                LottieCommand.Update,
                null, 
                Bounds.Size,
                Stretch, 
                StretchDirection));
        
        Start();
    }
    
    protected override void OnUnloaded(RoutedEventArgs routedEventArgs)
    {
        base.OnUnloaded(routedEventArgs);

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
            new LottiePayload(
                LottieCommand.Update, 
                null, 
                Bounds.Size,
                Stretch, 
                StretchDirection));
    }

    private void Start()
    {
        _customVisual?.SendHandlerMessage(
            new LottiePayload(
                LottieCommand.Start,
                new object(), // TODO:
                Bounds.Size,
                Stretch, 
                StretchDirection));
    }

    private void Stop()
    {
        _customVisual?.SendHandlerMessage(new LottiePayload(LottieCommand.Stop));
    }

    private void DisposeImpl()
    {
        _customVisual?.SendHandlerMessage(new LottiePayload(LottieCommand.Dispose));
    }
}
