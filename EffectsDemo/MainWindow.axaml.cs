using Avalonia.Controls;
using Avalonia.Rendering;

namespace EffectsDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps;
        //RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps | RendererDebugOverlays.RenderTimeGraph | RendererDebugOverlays.LayoutTimeGraph;
    }
}
