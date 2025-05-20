#version 330 core

in vec3 FragPos;
in vec3 Normal;
in vec4 VertexColor;

out vec4 color;

uniform vec3 lightDir;
uniform vec3 baseColor;
uniform float ambientStrength;
uniform vec3 lightColor;
uniform int useVertexColor;

void main()
{
    vec3 norm = normalize(Normal);
    vec3 L    = normalize(-lightDir);

    float diffHL = dot(norm, L) * 0.5 + 0.5;
    diffHL = clamp(diffHL, 0.0, 1.0);

    vec3 ambient = ambientStrength * baseColor;

    vec3 effectiveColor = useVertexColor == 1 ? VertexColor.rgb : baseColor;

    vec3 diffuse = diffHL * lightColor * effectiveColor;

    vec3 result = ambient + diffuse;
    color = vec4(result, 1.0);
}

