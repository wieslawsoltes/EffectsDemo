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
                  // Created by inigo quilez - iq/2013
                  // License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
                  
                  // See here for a tutorial on how to make this:
                  //
                  // http://www.iquilezles.org/www/articles/warp/warp.htm
                  
                  //====================================================================
                  
                  /* 
                  
                  SKSL PORT by kekekeks
                  
                  SKSL doesn't seem to support out/inout variables for anything but main
                  So I've made `func` to return float4 and encoded the original returnn value as the first element of the vector
                  
                  */
                  
                  uniform float iTime;
                  uniform float3 iResolution;
                  
                  const float2x2 m = float2x2( 0.80,  0.60, -0.60,  0.80 );
                  
                  float noise( in float2 p )
                  {
                      return sin(p.x)*sin(p.y);
                  }
                  
                  float fbm4( float2 p )
                  {
                      float f = 0.0;
                      f += 0.5000*noise( p ); p = m*p*2.02;
                      f += 0.2500*noise( p ); p = m*p*2.03;
                      f += 0.1250*noise( p ); p = m*p*2.01;
                      f += 0.0625*noise( p );
                      return f/0.9375;
                  }
                  
                  float fbm6( float2 p )
                  {
                      float f = 0.0;
                      f += 0.500000*(0.5+0.5*noise( p )); p = m*p*2.02;
                      f += 0.250000*(0.5+0.5*noise( p )); p = m*p*2.03;
                      f += 0.125000*(0.5+0.5*noise( p )); p = m*p*2.01;
                      f += 0.062500*(0.5+0.5*noise( p )); p = m*p*2.04;
                      f += 0.031250*(0.5+0.5*noise( p )); p = m*p*2.01;
                      f += 0.015625*(0.5+0.5*noise( p ));
                      return f/0.96875;
                  }
                  
                  float2 fbm4_2( float2 p )
                  {
                      return float2(fbm4(p), fbm4(p+float2(7.8)));
                  }
                  
                  float2 fbm6_2( float2 p )
                  {
                      return float2(fbm6(p+float2(16.8)), fbm6(p+float2(11.5)));
                  }
                  
                  //====================================================================
                  
                  float4 func( float2 q)
                  {
                      q += 0.03*sin( float2(0.27,0.23)*iTime + length(q)*float2(4.1,4.3));
                      float2 o = fbm4_2( 0.9*q );
                      o += 0.04*sin( float2(0.12,0.14)*iTime + length(o));
                      float2 n = fbm6_2( 3.0*o );
                      float f = 0.5 + 0.5*fbm4( 1.8*q + 6.0*n );
                      float rv = mix( f, f*f*f*3.5, f*abs(n.x) );
                      return float4(rv, o.y, n.x, n.y );
                  }
                  
                  
                  
                  half4 main(float2 fragCoord) 
                  {
                      //float2 p = sk_TransformedCoords2D[0]
                      float2 p = (2.0*fragCoord-iResolution.xy)/iResolution.y;
                      float e = 2.0/iResolution.y;
                  
                      float4 on = func(p);
                      float f = on.x;
                      float3 col = float3(0.0);
                      col = mix( float3(0.2,0.1,0.4), float3(0.3,0.05,0.05), f );
                      col = mix( col, float3(0.9,0.9,0.9), dot(on.zw,on.zw) );
                      col = mix( col, float3(0.4,0.3,0.3), 0.2 + 0.5*on.y*on.y );
                      col = mix( col, float3(0.0,0.2,0.4), 0.5*smoothstep(1.2,1.3,abs(on.z)+abs(on.w)) );
                      col = clamp( col*f*2.0, 0.0, 1.0 );
                  
                  
                      float3 nor = normalize( float3( func(p+float2(e,0.0)).x-f,
                      2.0*e,
                      func(p+float2(0.0,e)).x-f ));
                  
                      float3 lig = normalize( float3( 0.9, 0.2, -0.4 ) );
                      float dif = clamp( 0.3+0.7*dot( nor, lig ), 0.0, 1.0 );
                      float3 lin = float3(0.70,0.90,0.95)*(nor.y*0.5+0.5) + float3(0.15,0.10,0.05)*dif;
                      col *= 1.2*lin;
                      col = 1.0 - col;
                      col = 1.1*col*col;
                  
                      return half4(float4( col, 1.));
                  }
                  """;

        _time = 0f;
        
        _effect = SKRuntimeEffect.CreateShader(src, out var errorText);
        _uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["iResolution"] = new [] { 512f, 512f, 0f },
            ["iTime"] = _time,
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
