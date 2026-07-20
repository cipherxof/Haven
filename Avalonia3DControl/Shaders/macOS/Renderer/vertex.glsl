#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform bool uPointMode;
uniform float uPointSize;

out vec4 vertexColor;

void main()
{
    gl_Position = projection * view * model * vec4(aPosition, 1.0);
    if (uPointMode) { gl_PointSize = uPointSize; }
    vertexColor = aColor;
}