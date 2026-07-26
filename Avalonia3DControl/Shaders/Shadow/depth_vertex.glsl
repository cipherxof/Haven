#version 300 es
precision highp float;

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 model;
uniform mat4 lightSpaceMatrix;

out vec4 Color;
out vec2 TexCoord;

void main()
{
    Color = aColor;
    TexCoord = aTexCoord;
    gl_Position = lightSpaceMatrix * model * vec4(aPosition, 1.0);
}
