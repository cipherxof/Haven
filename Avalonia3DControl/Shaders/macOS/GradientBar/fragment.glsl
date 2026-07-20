#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform int gradientType;
uniform int isSymmetric;
uniform float minValue;
uniform float maxValue;
vec3 getClassicColor(float t) {
    if (t < 0.33) {
        float ratio = t / 0.33;
        return mix(vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0), ratio);
    } else if (t < 0.66) {
        float ratio = (t - 0.33) / 0.33;
        return mix(vec3(0.0, 1.0, 0.0), vec3(1.0, 1.0, 0.0), ratio);
    } else {
        float ratio = (t - 0.66) / 0.34;
        return mix(vec3(1.0, 1.0, 0.0), vec3(1.0, 0.0, 0.0), ratio);
    }
}
vec3 getThermalColor(float t) {
    if (t < 0.33) {
        return mix(vec3(0.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0), t * 3.0);
    } else if (t < 0.67) {
        return mix(vec3(1.0, 0.0, 0.0), vec3(1.0, 1.0, 0.0), (t - 0.33) * 3.0);
    } else {
        return mix(vec3(1.0, 1.0, 0.0), vec3(1.0, 1.0, 1.0), (t - 0.67) * 3.0);
    }
}
vec3 getRainbowColor(float t) {
    float h = t * 5.0;
    float f = fract(h);
    if (h < 1.0) return mix(vec3(1.0, 0.0, 0.0), vec3(1.0, 0.5, 0.0), f);
    else if (h < 2.0) return mix(vec3(1.0, 0.5, 0.0), vec3(1.0, 1.0, 0.0), f);
    else if (h < 3.0) return mix(vec3(1.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0), f);
    else if (h < 4.0) return mix(vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0), f);
    else return mix(vec3(0.0, 0.0, 1.0), vec3(0.5, 0.0, 1.0), f);
}
vec3 getMonochromeColor(float t) {
    return mix(vec3(0.0, 0.0, 0.5), vec3(0.5, 0.5, 1.0), t);
}
vec3 getOceanColor(float t) {
    if (t < 0.33) {
        return mix(vec3(0.0, 0.0, 0.5), vec3(0.0, 0.5, 0.5), t * 3.0);
    } else if (t < 0.67) {
        return mix(vec3(0.0, 0.5, 0.5), vec3(0.0, 1.0, 0.0), (t - 0.33) * 3.0);
    } else {
        return mix(vec3(0.0, 1.0, 0.0), vec3(1.0, 1.0, 1.0), (t - 0.67) * 3.0);
    }
}
vec3 getFireColor(float t) {
    if (t < 0.25) {
        return mix(vec3(0.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0), t * 4.0);
    } else if (t < 0.5) {
        return mix(vec3(1.0, 0.0, 0.0), vec3(1.0, 0.5, 0.0), (t - 0.25) * 4.0);
    } else if (t < 0.75) {
        return mix(vec3(1.0, 0.5, 0.0), vec3(1.0, 1.0, 0.0), (t - 0.5) * 4.0);
    } else {
        return mix(vec3(1.0, 1.0, 0.0), vec3(1.0, 1.0, 1.0), (t - 0.75) * 4.0);
    }
}
void main() {
    float t = 1.0 - TexCoord.y;
    if (isSymmetric == 1) {
        t = abs(2.0 * t - 1.0);
    }
    vec3 color;
    if (gradientType == 0) color = getClassicColor(t);
    else if (gradientType == 1) color = getThermalColor(t);
    else if (gradientType == 2) color = getRainbowColor(t);
    else if (gradientType == 3) color = getMonochromeColor(t);
    else if (gradientType == 4) color = getOceanColor(t);
    else if (gradientType == 5) color = getFireColor(t);
    else color = getClassicColor(t);
    FragColor = vec4(color, 1.0);
}