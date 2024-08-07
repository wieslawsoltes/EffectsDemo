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

internal class DrawCompositionCustomVisualHandler : CompositionCustomVisualHandler
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
        if (message is not DrawPayload msg)
        {
            return;
        }

        switch (msg)
        {
            case
            {
                HandlerCommand: HandlerCommand.Start,
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
                HandlerCommand: HandlerCommand.Update,
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

        // https://www.shadertoy.com/view/tdlXDM#
        var src = """
                  uniform vec3      iResolution;           // viewport resolution (in pixels)
                  uniform float     iTime;                 // shader playback time (in seconds)
                  uniform float     iTimeDelta;            // render time (in seconds)
                  uniform float     iFrameRate;            // shader frame rate
                  uniform int       iFrame;                // shader playback frame
                  uniform float     iChannelTime[4];       // channel playback time (in seconds)
                  uniform vec3      iChannelResolution[4]; // channel resolution (in pixels)
                  uniform vec4      iMouse;                // mouse pixel coords. xy: current (if MLB down), zw: click
                  //uniform samplerXX iChannel0..3;          // input channel. XX = 2D/Cube
                  uniform vec4      iDate;                 // (year, month, day, time in seconds)
                  uniform float     iSampleRate;           // sound sample rate (i.e., 44100)
                   
                  // Created by greenbird10
                  // License Creative Commons Attribution-NonCommercial-ShareAlike 3.0
                  
                  float hash(vec2 p) {
                  	return 0.5*(
                      sin(dot(p, vec2(271.319, 413.975)) + 1217.13*p.x*p.y)
                      ) + 0.5;
                  }
                  
                  float noise(vec2 p) {
                    vec2 w = fract(p);
                    w = w * w * (3.0 - 2.0*w);
                    p = floor(p);
                    return mix(
                      mix(hash(p+vec2(0,0)), hash(p+vec2(1,0)), w.x),
                      mix(hash(p+vec2(0,1)), hash(p+vec2(1,1)), w.x), w.y);
                  }
                  
                  // wave octave inspiration
                  // Alexander Alekseev - Seascape
                  // https://www.shadertoy.com/view/Ms2SD1
                  float map_octave(vec2 uv) {
                    uv = (uv + noise(uv)) / 2.5;
                    uv = vec2(uv.x*0.6-uv.y*0.8, uv.x*0.8+uv.y*0.6);
                    vec2 uvsin = 1.0 - abs(sin(uv));
                    vec2 uvcos = abs(cos(uv));
                    uv = mix(uvsin, uvcos, uvsin);
                    float val = 1.0 - pow(uv.x * uv.y, 0.65);
                    return val;
                  }
                  
                  float map(vec3 p) {
                    vec2 uv = p.xz + iTime/2.;
                    float amp = 0.6, freq = 2.0, val = 0.0;
                    for(int i = 0; i < 3; ++i) {
                      val += map_octave(uv) * amp;
                      amp *= 0.3;
                      uv *= freq;
                      // uv = vec2(uv.x*0.6-uv.y*0.8, uv.x*0.8+uv.y*0.6);
                    }
                    uv = p.xz - 1000. - iTime/2.;
                    amp = 0.6, freq = 2.0;
                    for(int i = 0; i < 3; ++i) {
                      val += map_octave(uv) * amp;
                      amp *= 0.3;
                      uv *= freq;
                      // uv = vec2(uv.x*0.6-uv.y*0.8, uv.x*0.8+uv.y*0.6);
                    }
                    return val + 3.0 - p.y;
                  }
                  
                  vec3 getNormal(vec3 p) {
                    float eps = 1./iResolution.x;
                    vec3 px = p + vec3(eps, 0, 0);
                    vec3 pz = p + vec3(0, 0, eps);
                    return normalize(vec3(map(px),eps,map(pz)));
                  }
                  
                  // raymarch inspiration
                  // Alexander Alekseev - Seascape
                  // https://www.shadertoy.com/view/Ms2SD1
                  float raymarch(vec3 ro, vec3 rd, out vec3 outP, out float outT) {
                      float l = 0., r = 26.;
                      const int steps = 16;
                      float dist = 1000000.;
                      for(int i = 0; i < steps; ++i) {
                          float mid = (r+l)/2.;
                          float mapmid = map(ro + rd*mid);
                          dist = min(dist, abs(mapmid));
                          if(mapmid > 0.) {
                          	l = mid;
                          }
                          else {
                          	r = mid;
                          }
                          if(r - l < 1./iResolution.x) break;
                      }
                      outP = ro + rd*l;
                      outT = l;
                      return dist;
                  }
                  
                  float fbm(vec2 n) {
                  	float total = 0.0, amplitude = 1.0;
                  	for (int i = 0; i < 5; i++) {
                  		total += noise(n) * amplitude; 
                  		n += n;
                  		amplitude *= 0.4; 
                  	}
                  	return total;
                  }
                  
                  float lightShafts(vec2 st) {
                      float angle = -0.2;
                      vec2 _st = st;
                      float t = iTime / 16.;
                      st = vec2(st.x * cos(angle) - st.y * sin(angle), 
                                st.x * sin(angle) + st.y * cos(angle));
                      float val = fbm(vec2(st.x*2. + 200. + t, st.y/4.));
                      val += fbm(vec2(st.x*2. + 200. - t, st.y/4.));
                      val = val / 3.;
                      float mask = pow(clamp(1.0 - abs(_st.y-0.15), 0., 1.)*0.49 + 0.5, 2.0);
                      mask *= clamp(1.0 - abs(_st.x+0.2), 0., 1.) * 0.49 + 0.5;
                  	return pow(val*mask, 2.0);
                  }
                  
                  vec2 bubble(vec2 uv, float scale) {
                      if(uv.y > 0.2) return vec2(0.);
                      float t = iTime/4.;
                      vec2 st = uv * scale;
                      vec2 _st = floor(st);
                      vec2 bias = vec2(0., 4. * sin(_st.x*128. + t));
                      float mask = smoothstep(0.1, 0.2, -cos(_st.x*128. + t));
                      st += bias;
                      vec2 _st_ = floor(st);
                      st = fract(st);
                      float size = noise(_st_)*0.07+0.01;
                      vec2 pos = vec2(noise(vec2(t, _st_.y*64.1)) * 0.8 + 0.1, 0.5);
                      if(length(st.xy - pos) < size) {
                          return (st + pos) * vec2(.1, .2) * mask;
                      }
                      return vec2(0.);
                  }
                  
                  //void mainImage(out vec4 fragColor, in vec2 fragCoord) {
                  half4 main(vec2 fragCoord) { 
                      vec3 ro = vec3(0.,0.,2.);
                      vec3 lightPos = vec3(8, 3, -3);
                      vec3 lightDir = normalize(lightPos - ro);
                  
                      // adjust uv
                      vec2 uv = fragCoord;
                      uv = (-iResolution.xy + 2.0*uv) / iResolution.y;
                      uv.y *= 0.5;
                      uv.x *= 0.45;
                      uv += bubble(uv, 12.) + bubble(uv, 24.); // add bubbles
                  
                      vec3 rd = normalize(vec3(uv, -1.));
                      vec3 hitPos;
                      float hitT;
                      vec3 seaColor = vec3(11,82,142)/255.;
                      vec3 color;
                      
                      // waves
                      float dist = raymarch(ro, rd, hitPos, hitT);
                      float diffuse = dot(getNormal(hitPos), rd) * 0.5 + 0.5;
                      color = mix(seaColor, vec3(15,120,152)/255., diffuse);
                      color += pow(diffuse, 12.0);
                  	// refraction
                      vec3 ref = normalize(refract(hitPos-lightPos, getNormal(hitPos), 0.05));
                      float refraction = clamp(dot(ref, rd), 0., 1.0);
                      color += vec3(245,250,220)/255. * 0.6 * pow(refraction, 1.5);
                  
                      vec3 col = vec3(0.);
                      col = mix(color, seaColor, pow(clamp(0., 1., dist), 0.2)); // glow edge
                      col += vec3(225,230,200)/255. * lightShafts(uv); // light shafts
                  
                      // tone map
                      col = (col*col + sin(col))/vec3(1.8, 1.8, 1.9);
                      
                      // vignette
                      // inigo quilez - Stop Motion Fox 
                      // https://www.shadertoy.com/view/3dXGWB
                      vec2 q = fragCoord / iResolution.xy;
                      col *= 0.7+0.3*pow(16.0*q.x*q.y*(1.0-q.x)*(1.0-q.y),0.2);
                  
                      //fragColor = vec4(col,1.0);
                      return vec4(col,1.0);
                  }
                  """;

        _time = 0f;

        _effect = SKRuntimeEffect.CreateShader(src, out var errorText);
        _uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["iResolution"] = new [] { 512f, 512f, 0f },
            ["iTime"] = _time,
            ["iMouse"] = new [] { 0f, 0f, -1f, -1f },
        };
        
        /*
         uniform vec3      iResolution;           // viewport resolution (in pixels)
uniform float     iTime;                 // shader playback time (in seconds)
uniform float     iTimeDelta;            // render time (in seconds)
uniform float     iFrameRate;            // shader frame rate
uniform int       iFrame;                // shader playback frame
uniform float     iChannelTime[4];       // channel playback time (in seconds)
uniform vec3      iChannelResolution[4]; // channel resolution (in pixels)
uniform vec4      iMouse;                // mouse pixel coords. xy: current (if MLB down), zw: click
//uniform samplerXX iChannel0..3;          // input channel. XX = 2D/Cube
uniform vec4      iDate;                 // (year, month, day, time in seconds)
uniform float     iSampleRate;           // sound sample rate (i.e., 44100)
         */
        
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
