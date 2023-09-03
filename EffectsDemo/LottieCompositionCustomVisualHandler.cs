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
                  uniform float Time;
                  uniform float3 Resolution;
                  
                  float random (vec2 uv) {
                      return fract(sin(dot(uv.xy, vec2(12.9898,78.233))) * 43758.5453123);
                  }
                  
                  // Based on Morgan McGuire @morgan3d
                  // https://www.shadertoy.com/view/4dS3Wd
                  
                  float noise (vec2 uv) {
                      vec2 i = floor(uv);
                      // Four corners in 2D of a tile
                      vec2 f = fract(uv);
                      float a = random(i);
                      float b = random(i + vec2(1.0, 0.0));
                      float c = random(i + vec2(0.0, 1.0));
                      float d = random(i + vec2(1.0, 1.0));
                      vec2 u = (f * f * (3.0 - 2.0 * f));
                      return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) +
                          (d - b) * u.x * u.y;
                  }
                  
                  float fbm (vec2 uv) {
                      float v = 0.0;
                      float a = 0.5;
                      vec2 shift = vec2(100.0);
                      // Rotate to reduce axial bias
                      float2x2 rot = float2x2(cos(0.5), sin(0.5), -sin(0.5), cos(0.50));
                      for (int i = 0; i < 5; ++i) {
                          v += a * noise(uv);
                          uv = rot * uv * 2.0 + shift;
                          a *= 0.5;
                      }
                      return v;
                  }
                  
                  half4 main(vec2 fragCoord) {
                      float2 uv = fragCoord.xy/Resolution.xy*3.;
                      // uv += uv * abs(sin(Time*0.1)*3.0);
                      vec3 color = vec3(0.0);vec2 q = vec2(0.);
                      q.x = fbm( uv + 0.00*Time);
                      q.y = fbm( uv + vec2(1.0));vec2 r = vec2(0.);
                      r.x = fbm( uv + 1.0*q + vec2(1.7,9.2)+ 0.15*Time );
                      r.y = fbm( uv + 1.0*q + vec2(8.3,2.8)+ 0.126*Time);
                      float f = fbm(uv+r);
                      
                      color = mix(vec3(0.101961,0.619608,0.666667), vec3(0.666667,0.666667,0.498039),
                          clamp((f*f)*4.0,0.0,1.0));
                      
                      color = mix(color,
                      vec3(0,0,0.164706), clamp(length(q),0.0,1.0));
                      color = mix(color,vec3(0.666667,1,1),clamp(length(r.x),0.0,1.0));
                      return half4((f*f*f+.6*f*f+.5*f)*color,1.);
                  }
                  """;

        _time = 0f;
        
        _effect = SKRuntimeEffect.CreateShader(src, out var errorText);
        _uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["Resolution"] = new [] { 512f, 512f, 0f },
            ["Time"] = _time,
            //["iMouse"] = new [] { 0f, 0f, -1f, -1f },
            //["iImageResolution"] = new [] { 512f, 512f, 0f },
        };
        //_children = new SKRuntimeEffectChildren(_effect) { ["iImage1"] = imageShader };
        _children = new SKRuntimeEffectChildren(_effect);

        _shader = _effect.ToShader(_uniforms, _children);
        _paint = new SKPaint { Shader = _shader };
    }

    private void UpdatePaint()
    {
        if (_uniforms is { } && _effect is { } && _paint is { })
        {
            _time += 1f / 60f;
            _uniforms["Time"] = _time;
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
