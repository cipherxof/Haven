#version 300 es
precision highp float;

in vec4 Color;
in vec2 TexCoord;

uniform bool hasTexture;
uniform sampler2D texture0;
uniform bool uUseVertexAlpha;
uniform float uAlphaTestRef;

layout(location = 0) out vec4 FragColor;

void main()
{
    if (hasTexture && uAlphaTestRef > 0.0) {
        float coverage = texture(texture0, TexCoord).a;
        if (uUseVertexAlpha) {
            coverage *= Color.a;
        }
        if (coverage < uAlphaTestRef) {
            discard;
        }
    }

    // The colour attachment only exists to keep the GLES framebuffer complete.
    // The lighting pass samples the depth attachment, not this value.
    FragColor = vec4(0.0);
}
