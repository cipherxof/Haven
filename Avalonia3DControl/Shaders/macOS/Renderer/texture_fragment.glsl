#version 330 core
in vec4 Color;
in vec2 TexCoord;

uniform bool hasTexture;
uniform sampler2D texture0;
uniform float materialAlpha;

uniform bool  uUseVertexAlpha;   // multiply coverage by vertex alpha (layer blend mask)
uniform float uAlphaTestRef;     // discard coverage < ref (0 = off, 0.5 = cutout)
uniform bool  uForceOpaqueAlpha; // opaque/cutout packets output alpha 1.0

out vec4 fragColor;

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

    float outAlpha = uForceOpaqueAlpha ? 1.0 : coverage;
    fragColor = vec4(textureColor.rgb * Color.rgb, outAlpha * materialAlpha);
}