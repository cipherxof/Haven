#version 330 core
in vec3 aPosition;
in vec4 aColor;
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform bool uPointMode;
uniform float uPointSize;
out vec4 vertexColor;
out vec3 worldPos;
out vec3 normal;

void main() {
    vec4 worldPosition = model * vec4(aPosition, 1.0);
    worldPos = worldPosition.xyz;
    
    // Calculate face normal based on vertex position
    vec3 localNormal;
    if (abs(aPosition.x) > abs(aPosition.y) && abs(aPosition.x) > abs(aPosition.z)) {
        // X face
        localNormal = vec3(sign(aPosition.x), 0.0, 0.0);
    } else if (abs(aPosition.y) > abs(aPosition.z)) {
        // Y face
        localNormal = vec3(0.0, sign(aPosition.y), 0.0);
    } else {
        // Z face
        localNormal = vec3(0.0, 0.0, sign(aPosition.z));
    }
    
    normal = normalize(mat3(model) * localNormal);
    gl_Position = projection * view * worldPosition;
    if (uPointMode) { gl_PointSize = uPointSize; }
    vertexColor = aColor;
}