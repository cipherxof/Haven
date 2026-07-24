#version 300 es
precision highp float;
in vec4 vertexColor;
in vec3 worldPos;
in vec4 lightSpacePos;
in float viewDepth;
uniform vec3 materialAmbient;
uniform vec3 materialDiffuse;
uniform vec3 materialSpecular;
uniform float materialShininess;
uniform float materialAlpha;
uniform bool uShadowsEnabled;
uniform bool uReceiveShadow;
uniform sampler2D shadowMap;
uniform vec3 uShadowLightDirection;
uniform float uShadowStrength;
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
    if (!uShadowsEnabled || !uReceiveShadow || lightSpacePos.w <= 0.0) return 0.0;
    vec3 projected = lightSpacePos.xyz / lightSpacePos.w;
    projected = projected * 0.5 + 0.5;
    if (projected.z <= 0.0 || projected.z >= 1.0 ||
        projected.x <= 0.0 || projected.x >= 1.0 ||
        projected.y <= 0.0 || projected.y >= 1.0) return 0.0;

    // Four fixed taps are a good editor compromise: stable penumbra without the
    // previous 3x3 cost on every visible fragment.
    float receiverDepth = projected.z - 0.00022;
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


void main() {
    vec3 norm = normalize(cross(dFdx(worldPos), dFdy(worldPos)));
    vec3 lightDir1 = normalize(vec3(1.0, 1.0, 1.0));
    vec3 lightDir2 = normalize(vec3(-0.5, 0.5, -0.5));
    vec3 lightColor = vec3(0.9, 0.9, 0.9);
    vec3 ambientLight = vec3(0.7, 0.7, 0.7);

    vec3 ambient = ambientLight * materialAmbient;
    float diff1 = max(dot(norm, lightDir1), 0.0);
    float diff2 = max(dot(norm, lightDir2), 0.0);
    vec3 diffuse = (diff1 + diff2 * 0.5) * lightColor * materialDiffuse;

    vec3 viewDir = normalize(vec3(0.0, 0.0, 1.0));
    vec3 reflectDir1 = reflect(-lightDir1, norm);
    vec3 reflectDir2 = reflect(-lightDir2, norm);
    float spec1 = pow(max(dot(viewDir, reflectDir1), 0.0), max(materialShininess, 1.0));
    float spec2 = pow(max(dot(viewDir, reflectDir2), 0.0), max(materialShininess, 1.0));
    vec3 specular = (spec1 + spec2 * 0.5) * lightColor * materialSpecular;

    float shadow = SampleDirectionalShadow();
    float visibility = max(1.0 - shadow * clamp(uShadowStrength, 0.0, 1.0), 0.28);
    vec3 result = ambient + (diffuse + specular) * visibility;
    if (uFogEnabled)
    {
        result = mix(result, uFogColor.rgb, Mgs4FogAmount(viewDepth));
    }
    // Output transform: the per-vertex bake stays linear, so the display
    // encoding is applied here. uOutputGamma <= 0 disables the transform.
    vec3 outColor = ApplyKonamiColorFilter(result);
    if (uOutputGamma > 0.0)
    {
        outColor = pow(max(outColor * uExposureScale, vec3(0.0)), vec3(1.0 / uOutputGamma));
    }
    fragColor = vec4(outColor, materialAlpha);
}
