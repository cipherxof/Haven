#version 300 es
precision highp float;
in vec3 aPosition;
in vec4 aColor;
in vec2 aTexCoord;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform bool uPointMode;
uniform float uPointSize;

out vec4 Color;
out vec2 TexCoord;

void main()
{
    Color = aColor;
    TexCoord = aTexCoord;
    gl_Position = projection * view * model * vec4(aPosition, 1.0);
    if (uPointMode) { gl_PointSize = uPointSize; }
}
