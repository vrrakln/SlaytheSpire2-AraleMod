shader_type canvas_item;
varying vec4 modulate_color;
void vertex() { modulate_color = COLOR; }

void fragment() {
    vec4 col = texture(TEXTURE, UV);
    // Preserve the native colorless treatment on the rest of this exact frame.
    float gray = dot(col.rgb, vec3(0.2989, 0.5870, 0.1140)) * 1.2;
    col.rgb = vec3(gray);
    // Rounded inset of the existing description panel, in the 598 x 844 texture.
    vec2 p = UV * vec2(598.0, 844.0);
    vec2 q = abs(p - vec2(299.0, 644.0)) - vec2(245.0, 109.0);
    float d = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - 23.0;
    float panel = 1.0 - smoothstep(-1.0, 1.0, d);
    // Retain dark details and highlights; recolor only the pale paper.
    float paper = smoothstep(0.82, 1.02, gray) * panel;
    col.rgb = mix(col.rgb, min(gray, 1.0) * vec3(0.8353, 0.8078, 0.8588), paper);
    COLOR = col * modulate_color;
}

