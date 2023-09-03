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
                  
                  // Star Nest by Pablo Roman Andrioli
                  
                  // This content is under the MIT License.
                  
                  const int iterations = 17;
                  const float formuparam = 0.53;
                  
                  const int volsteps = 20;
                  const float stepsize = 0.1;
                  
                  const float zoom  = 0.800;
                  const float tile  = 0.850;
                  const float speed =0.010 ;
                  
                  const float brightness =0.0015;
                  const float darkmatter =0.300;
                  const float distfading =0.730;
                  const float saturation =0.850;
                  
                  
                  half4 main( in vec2 fragCoord )
                  {
                  	//get coords and direction
                  	vec2 uv=fragCoord.xy/iResolution.xy-.5;
                  	uv.y*=iResolution.y/iResolution.x;
                  	vec3 dir=vec3(uv*zoom,1.);
                  	float time=iTime*speed+.25;
                  
                  	//mouse rotation
                  	float a1=.5+iMouse.x/iResolution.x*2.;
                  	float a2=.8+iMouse.y/iResolution.y*2.;
                  	mat2 rot1=mat2(cos(a1),sin(a1),-sin(a1),cos(a1));
                  	mat2 rot2=mat2(cos(a2),sin(a2),-sin(a2),cos(a2));
                  	dir.xz*=rot1;
                  	dir.xy*=rot2;
                  	vec3 from=vec3(1.,.5,0.5);
                  	from+=vec3(time*2.,time,-2.);
                  	from.xz*=rot1;
                  	from.xy*=rot2;
                  	
                  	//volumetric rendering
                  	float s=0.1,fade=1.;
                  	vec3 v=vec3(0.);
                  	for (int r=0; r<volsteps; r++) {
                  		vec3 p=from+s*dir*.5;
                  		p = abs(vec3(tile)-mod(p,vec3(tile*2.))); // tiling fold
                  		float pa,a=pa=0.;
                  		for (int i=0; i<iterations; i++) { 
                  			p=abs(p)/dot(p,p)-formuparam; // the magic formula
                  			a+=abs(length(p)-pa); // absolute sum of average change
                  			pa=length(p);
                  		}
                  		float dm=max(0.,darkmatter-a*a*.001); //dark matter
                  		a*=a*a; // add contrast
                  		if (r>6) fade*=1.-dm; // dark matter, don't render near
                  		//v+=vec3(dm,dm*.5,0.);
                  		v+=fade;
                  		v+=vec3(s,s*s,s*s*s*s)*a*brightness*fade; // coloring based on distance
                  		fade*=distfading; // distance fading
                  		s+=stepsize;
                  	}
                  	v=mix(vec3(length(v)),v,saturation); //color adjust
                  	return vec4(v*.01,1.);	
                  	
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
