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
                  
                  float noise(vec3 p) //Thx to Las^Mercury
                  {
                      vec3 i = floor(p);
                      vec4 a = dot(i, vec3(1., 57., 21.)) + vec4(0., 57., 21., 78.);
                      vec3 f = cos((p-i)*acos(-1.))*(-.5)+.5;
                      a = mix(sin(cos(a)*a),sin(cos(1.+a)*(1.+a)), f.x);
                      a.xy = mix(a.xz, a.yw, f.y);
                      return mix(a.x, a.y, f.z);
                  }
                  
                  float sphere(vec3 p, vec4 spr)
                  {
                      return length(spr.xyz-p) - spr.w;
                  }
                  
                  float flame(vec3 p)
                  {
                      float d = sphere(p*vec3(1.,.3,1.), vec4(.0,-1.,.0,1.));
                      return d + (noise(p+vec3(.0,Time*0.1,.0)) + noise(p*3.)*.5)*.25*(p.y) ;
                  }
                  
                  float scene(vec3 p)
                  {
                      return min(100.-length(p) , abs(flame(p)) );
                  }
                  
                  vec4 raymarch(vec3 org, vec3 dir)
                  {
                      float d = 0.0, glow = 0.0, eps = 0.02;
                      vec3  p = org;
                      bool glowed = false;
                      
                      for(int i=0; i<64; i++)
                      {
                          d = scene(p) + eps;
                          p += d * dir;
                          if( d>eps )
                          {
                              if(flame(p) < .0)
                                  glowed=true;
                              if(glowed)
                                  glow = float(i)/64.;
                          }
                      }
                      return vec4(p,glow);
                  }
                  
                  half4 main(vec2 fragCoord)
                  {
                      vec2 uv = 2.0 * fragCoord.xy / Resolution.xy;
                      uv.x *= Resolution.x/Resolution.y;
                      
                      vec3 org = vec3(0., -4.5, 4.);
                      vec3 dir = normalize(vec3(uv.x*1.6, -uv.y, -1.5));
                      
                      vec4 p = raymarch(org, dir);
                      float glow = p.w;
                      
                      half4 col = mix(half4(1.,.5,.1,1.), half4(0.1,.5,1.,1.), p.y*.02+.4);
                      
                      return half4(mix(half4(0.), col, pow(glow*2.,4.)));
                  
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
