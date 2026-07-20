#version 300 es
precision highp float;
in vec4 vertexColor;

uniform float materialAlpha;

out vec4 fragColor;

void main()
{
    // Use vertex color for RGB, but ignore alpha (only use materialAlpha)
    fragColor = vec4(vertexColor.rgb, materialAlpha);
}