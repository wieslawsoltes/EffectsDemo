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
                  
                  // Source: @zozuar https://twitter.com/zozuar/status/1492217553103503363
                  
                  vec3 hsv(float h, float s, float v){
                      vec4 t = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                      vec3 p = abs(fract(vec3(h) + t.xyz) * 6.0 - vec3(t.w));
                      return v * mix(vec3(t.x), clamp(p - vec3(t.x), 0.0, 1.0), s);
                  }
                  
                  vec4 main(vec2 FC) {
                    float e=0,R=0,t=iTime,s;
                    vec2 r = iResolution.xy;
                    vec3 q=vec3(0,0,-1), p, d=vec3((FC.xy-.5*r)/r.y,.7);
                    vec4 o=vec4(0);
                    for(float i=0;i<100;++i) {
                      o.rgb+=hsv(.1,e*.4,e/1e2)+.005;
                      p=q+=d*max(e,.02)*R*.3;
                      float py = (p.x == 0 && p.y == 0) ? 1 : p.y;
                      p=vec3(log(R=length(p))-t,e=asin(-p.z/R)-1.,atan(p.x,py)+t/3.);
                      s=1;
                      for(int z=1; z<=9; ++z) {
                        e+=cos(dot(sin(p*s),cos(p.zxy*s)))/s;
                        s+=s;
                      }
                      i>50.?d/=-d:d;
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
