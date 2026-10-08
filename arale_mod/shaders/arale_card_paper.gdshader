shader_type canvas_item;
// Only recolor the pale inset paper; preserve the original frame and vertex tint.
uniform vec4 panel_rect = vec4(32.0, 512.0, 534.0, 260.0);
uniform bool colorless = false;
uniform vec3 paper_tint = vec3(0.8353, 0.8078, 0.8588);
varying vec4 modulate_color;
void vertex() { modulate_color = COLOR; }
void fragment() {
    vec4 col = texture(TEXTURE, UV);
    if (colorless) {
        // Match the native colorless HSV shader, including its YIQ inverse.
        mat3 rgb_to_yiq = mat3(vec3(0.2989, 0.5959, 0.2115),
            vec3(0.5870, -0.2774, -0.5229), vec3(0.1140, -0.3216, 0.3114));
        col.rgb = rgb_to_yiq * col.rgb;
        col.rgb *= mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, -0.0), vec3(0.0, 0.0, 1.0));
        col.rgb = mat3(vec3(1.0, 0.0, 0.0), vec3(0.0), vec3(0.0)) * col.rgb;
        col.rgb = mix(vec3(0.0), col.rgb, 1.2);
        col.rgb = inverse(rgb_to_yiq) * col.rgb;
    }
    vec2 p = UV * vec2(598.0, 844.0);
    vec2 half_size = panel_rect.zw * 0.5;
    vec2 q = abs(p - (panel_rect.xy + half_size)) - (half_size - vec2(24.0));
    float d = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - 24.0;
    float panel = 1.0 - smoothstep(-4.0, 0.0, d);
    float lo = min(col.r, min(col.g, col.b));
    float hi = max(col.r, max(col.g, col.b));
    float paper = smoothstep(0.76, 0.92, lo) * (1.0 - smoothstep(0.08, 0.20, hi - lo)) * panel;
    col.rgb = mix(col.rgb, min(col.rgb, vec3(1.0)) * paper_tint, paper);
    COLOR = col * modulate_color;
}

