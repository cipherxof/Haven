#version 300 es
precision highp float;
in vec4 Color;
in vec2 TexCoord;
in vec3 WorldPos;
in vec4 LightSpacePos;
in float ViewDepth;
in vec3 ShadowedColor;

uniform bool hasTexture;
uniform sampler2D texture0;
uniform float materialAlpha;

uniform bool  uUseVertexAlpha;
uniform float uAlphaTestRef;
uniform bool  uForceOpaqueAlpha;

uniform bool uShadowsEnabled;
uniform bool uReceiveShadow;
uniform sampler2D shadowMap;
uniform vec3 uShadowLightDirection;
uniform float uShadowStrength;
uniform bool uHasShadowedLighting;
uniform vec2 uShadowTexelSize;

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
uniform float uContrast;
uniform float uTextureIsSrgb;

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


float SampleDirectionalShadow()
{
    if (!uShadowsEnabled || !uReceiveShadow || LightSpacePos.w <= 0.0) {
        return 0.0;
    }

    vec3 projected = LightSpacePos.xyz / LightSpacePos.w;
    projected = projected * 0.5 + 0.5;
    if (projected.z <= 0.0 || projected.z >= 1.0 ||
        projected.x <= 0.0 || projected.x >= 1.0 ||
        projected.y <= 0.0 || projected.y >= 1.0) {
        return 0.0;
    }

    // Derivative normal gives a stable slope-scaled bias without adding another
    // vertex attribute, which avoids large detached shadow-acne bands.
    vec3 geometricNormal = normalize(cross(dFdx(WorldPos), dFdy(WorldPos)));
    float facing = abs(dot(geometricNormal, normalize(-uShadowLightDirection)));
    float receiverBias = mix(0.00042, 0.00008, clamp(facing, 0.0, 1.0));
    float receiverDepth = projected.z - receiverBias;
    vec2 halfTexel = uShadowTexelSize * 0.5;
    float shadow = 0.0;
    shadow += receiverDepth > texture(shadowMap, projected.xy + vec2(-halfTexel.x, -halfTexel.y)).r ? 1.0 : 0.0;
    shadow += receiverDepth > texture(shadowMap, projected.xy + vec2( halfTexel.x, -halfTexel.y)).r ? 1.0 : 0.0;
    shadow += receiverDepth > texture(shadowMap, projected.xy + vec2(-halfTexel.x,  halfTexel.y)).r ? 1.0 : 0.0;
    shadow += receiverDepth > texture(shadowMap, projected.xy + vec2( halfTexel.x,  halfTexel.y)).r ? 1.0 : 0.0;
    return shadow * 0.25;
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
    vec4 textureColor;

    if (hasTexture) {
        textureColor = texture(texture0, TexCoord);
    } else {
        float scale = 8.0;
        vec2 scaledCoord = TexCoord * scale;
        vec2 grid = floor(scaledCoord);
        float checker = mod(grid.x + grid.y, 2.0);
        vec3 checkerColor = mix(vec3(0.8, 0.8, 0.8), vec3(0.2, 0.2, 0.2), checker);
        textureColor = vec4(checkerColor, 1.0);
    }

    float coverage = textureColor.a;
    if (uUseVertexAlpha) {
        coverage *= Color.a;
    }
    if (coverage < uAlphaTestRef) {
        discard;
    }

    float shadow = SampleDirectionalShadow();
    float shadowBlend = shadow * clamp(uShadowStrength, 0.0, 1.0);
    vec3 lightingInShadow = uHasShadowedLighting ? ShadowedColor : Color.rgb;

    float outAlpha = uForceOpaqueAlpha ? 1.0 : coverage;
    vec3 lighting = mix(Color.rgb, lightingInShadow, shadowBlend);
    // uTextureIsSrgb=0 (default): sample the texel raw and apply gamma once at
    // output. >0: linearise the texel here instead.
    vec3 albedo = textureColor.rgb;
    if (uTextureIsSrgb > 0.0 && uOutputGamma > 0.0)
    {
        albedo = pow(max(albedo, vec3(0.0)), vec3(uOutputGamma));
    }
    // albedo * linear HDR lighting; exposure in linear space, then display curve.
    vec3 color = albedo * lighting;
    if (uFogEnabled)
    {
        color = mix(color, uFogColor.rgb, Mgs4FogAmount(ViewDepth));
    }
    vec3 outColor = ApplyKonamiColorFilter(color);
    if (uOutputGamma > 0.0)
    {
        outColor = pow(max(outColor * uExposureScale, vec3(0.0)), vec3(1.0 / uOutputGamma));
        // Contrast slider: pivot around mid-grey after the display curve.
        outColor = clamp((outColor - vec3(0.5)) * uContrast + vec3(0.5), vec3(0.0), vec3(1.0));
    }
    fragColor = vec4(outColor, outAlpha * materialAlpha);
}
