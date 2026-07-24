#version 300 es
precision highp float;
in vec4 vertexColor;
in float viewDepth;

uniform float materialAlpha;

out vec4 fragColor;
uniform bool uColorFilterEnabled;
uniform float uFilterMono;
uniform vec3 uFilterScale;
uniform float uFilterBrightness;
uniform float uFilterContrast;
uniform vec3 uFilterMinimum;
uniform vec3 uFilterMaximum;
uniform float uFilterNoise;
uniform bool uFogEnabled;
uniform vec4 uFogParam;
uniform vec4 uFogColor;
uniform float uOutputGamma;
uniform float uExposureScale;

vec3 ApplyKonamiColorFilter(vec3 color)
{
    if (!uColorFilterEnabled) return color;
    float luma = dot(color, vec3(0.299, 0.587, 0.114));
    color = mix(color, vec3(luma), clamp(uFilterMono, 0.0, 1.0));
    color *= max(uFilterScale, vec3(0.0));
    color += vec3(uFilterBrightness);
    color = (color - vec3(0.5)) * max(uFilterContrast, 0.0) + vec3(0.5);
    return clamp(color, min(uFilterMinimum, uFilterMaximum), max(uFilterMinimum, uFilterMaximum));
}


float Mgs4FogAmount(float linearViewDepth)
{
    float factor = linearViewDepth * uFogParam.x + uFogParam.y;
    float lowerLimit = min(uFogParam.z, uFogParam.w);
    float upperLimit = max(uFogParam.z, uFogParam.w);
    return clamp(factor, lowerLimit, upperLimit);
}


void main()
{
    // Use vertex color for RGB, but ignore alpha (only use materialAlpha)
    vec3 color = vertexColor.rgb;
    if (uFogEnabled)
    {
        color = mix(color, uFogColor.rgb, Mgs4FogAmount(viewDepth));
    }
    // MGS4 output transform (see material_fragment.glsl for the evidence:
    // the preshader emits LINEAR values; the display curve lives downstream).
    vec3 outColor = ApplyKonamiColorFilter(color);
    if (uOutputGamma > 0.0)
    {
        outColor = pow(max(outColor * uExposureScale, vec3(0.0)), vec3(1.0 / uOutputGamma));
    }
    fragColor = vec4(outColor, materialAlpha);
}