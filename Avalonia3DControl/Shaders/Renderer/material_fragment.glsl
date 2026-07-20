#version 300 es
precision highp float;
in vec4 vertexColor;
in vec3 worldPos;
in vec3 normal;
uniform vec3 materialAmbient;
uniform vec3 materialDiffuse;
uniform vec3 materialSpecular;
uniform float materialShininess;
uniform float materialAlpha;

out vec4 fragColor;

void main() {
    vec3 dFdxPos = dFdx(worldPos);
    vec3 dFdyPos = dFdy(worldPos);
    vec3 norm = normalize(cross(dFdxPos, dFdyPos));
    vec3 lightDir1 = normalize(vec3(1.0, 1.0, 1.0));
    vec3 lightDir2 = normalize(vec3(-0.5, 0.5, -0.5));
    vec3 lightColor = vec3(0.9, 0.9, 0.9);
    vec3 ambientLight = vec3(0.7, 0.7, 0.7);

    vec3 ambient = ambientLight * materialAmbient;

    float diff1 = max(dot(norm, lightDir1), 0.0);
    float diff2 = max(dot(norm, lightDir2), 0.0);
    vec3 diffuse = (diff1 + diff2 * 0.5) * lightColor * materialDiffuse;

    // Specular reflection calculation
    vec3 viewDir = normalize(vec3(0.0, 0.0, 1.0));
    vec3 reflectDir1 = reflect(-lightDir1, norm);
    vec3 reflectDir2 = reflect(-lightDir2, norm);
    float spec1 = pow(max(dot(viewDir, reflectDir1), 0.0), max(materialShininess, 1.0));
    float spec2 = pow(max(dot(viewDir, reflectDir2), 0.0), max(materialShininess, 1.0));
    vec3 specular = (spec1 + spec2 * 0.5) * lightColor * materialSpecular;

    vec3 result = ambient + diffuse + specular;
    // Use material alpha only, ignore vertex color alpha
    fragColor = vec4(result, materialAlpha);
}