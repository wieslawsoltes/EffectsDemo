using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;

namespace EffectsDemo;

internal class LottieCompositionCustomVisualHandler : CompositionCustomVisualHandler
{
    private bool _running;
    private Stretch? _stretch;
    private StretchDirection? _stretchDirection;
    private Size? _size;
    private readonly object _sync = new();
    private SKPaint? _paint;
    private SKShader? _shader;
    private SKRuntimeEffectUniforms? _uniforms;
    private SKRuntimeEffect? _effect;
    private SKRuntimeEffectChildren? _children;
    private float _time;

    public override void OnMessage(object message)
    {
        if (message is not LottiePayload msg)
        {
            return;
        }

        switch (msg)
        {
            case
            {
                LottieCommand: LottieCommand.Start,
                Animation: { } an,
                Size: { } size,
                Stretch: { } st,
                StretchDirection: { } sd
            }:
            {
                _running = true;
                _size = size;
                _stretch = st;
                _stretchDirection = sd;
                RegisterForNextAnimationFrameUpdate();
                break;
            }
            case
            {
                LottieCommand: LottieCommand.Update,
                Size: { } size,
                Stretch: { } st,
                StretchDirection: { } sd
            }:
            {
                _size = size;
                _stretch = st;
                _stretchDirection = sd;
                RegisterForNextAnimationFrameUpdate();
                break;
            }
            case
            {
                LottieCommand: LottieCommand.Stop
            }:
            {
                _running = false;
                break;
            }
            case
            {
                LottieCommand: LottieCommand.Dispose
            }:
            {
                DisposeImpl();
                break;
            }
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        if (!_running)
            return;

        Invalidate();
        RegisterForNextAnimationFrameUpdate();
    }

    private void DisposeImpl()
    {
        lock (_sync)
        {
            // TODO:
        }
    }

    private void CreatePaint()
    {
        using var image = SKImage.FromEncodedData(AssetLoader.Open(new Uri("avares://EffectsDemo/Assets/mandrill.png")));
        using var imageShader = image.ToShader();

        var src = """
                  uniform float3 iResolution;      // Viewport resolution (pixels)
                  uniform float  iTime;            // Shader playback time (s)
                  uniform float4 iMouse;           // Mouse drag pos=.xy Click pos=.zw (pixels)
                  uniform float3 iImageResolution; // iImage1 resolution (pixels)
                  uniform shader iImage1;          // An input image.
                  
                  // Source: @kamoshika_vrc https://twitter.com/kamoshika_vrc/status/1495081980278751234
                  
                  const float PI2 = 6.28318530718;
                  float F(vec2 c){
                    return fract(sin(dot(c, vec2(12.9898, 78.233))) * 43758.5453);
                  }
                  
                  half4 main(float2 FC) {
                    vec4 o;
                    float t = iTime;
                    vec2 r = iResolution.xy * vec2(1, -1);
                    vec3 R=normalize(vec3((FC.xy*2.-r)/r.y,1));
                    for(float i=0; i<100; ++i) {
                      float I=floor(t/.1)+i;
                      float d=(I*.1-t)/R.z;
                      vec2 p=d*R.xy+vec2(sin(t+F(I.xx)*PI2)*.3+F(I.xx*.9),t+F(I.xx*.8));
                      if (F(I/100+ceil(p))<.2) {
                        o+=smoothstep(.1,0.,length(fract(p)-.5))*exp(-d*d*.04);
                      }
                    }
                    return o;
                  }
                  
                  """;

        _time = 0f;
        
        _effect = SKRuntimeEffect.CreateShader(src, out var errorText);
        _uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["iResolution"] = new [] { 512f, 512f, 0f },
            ["iTime"] = _time,
            ["iMouse"] = new [] { 0f, 0f, -1f, -1f },
            ["iImageResolution"] = new [] { 512f, 512f, 0f },
        };
        _children = new SKRuntimeEffectChildren(_effect) { ["iImage1"] = imageShader };

        _shader = _effect.ToShader(_uniforms, _children);
        _paint = new SKPaint { Shader = _shader };
    }

    private void UpdatePaint()
    {
        if (_uniforms is { } && _effect is { } && _paint is { })
        {
            _time += 1f / 60f;
            _uniforms["iTime"] = _time;
            _shader?.Dispose();
            _shader = _effect.ToShader(_uniforms, _children);
            _paint.Shader = _shader;
        }
    }
    
    private void Draw(SKCanvas canvas)
    {
        canvas.Save();

        if (_paint is null)
        {
            CreatePaint();
        }
        else
        {
            UpdatePaint();
        }
        
        // TODO:
        canvas.DrawRect(0, 0, 512, 512, new SKPaint { Color = SKColors.Black });
        canvas.DrawRect(0, 0, 512, 512, _paint);

        canvas.Restore();
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        lock (_sync)
        {
            
            if (_stretch is not { } st 
                || _stretchDirection is not { } sd)
            {
                return;
            }

            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            var rb = GetRenderBounds();

            var size = _size ?? rb.Size;

            var viewPort = new Rect(rb.Size);
            var sourceSize = new Size(size.Width, size.Height);
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
                var canvas = lease?.SkCanvas;
                if (canvas is null)
                {
                    return;
                }
                Draw(canvas);
            }
        }
    }
}
