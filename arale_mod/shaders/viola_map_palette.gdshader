shader_type canvas_item;

uniform vec4 paper_color : source_color;
varying vec4 map_ink;

void vertex() {
    map_ink = COLOR;
}

void fragment() {
    vec4 portrait = texture(TEXTURE, UV);
    float value = dot(portrait.rgb, vec3(0.299, 0.587, 0.114));
    // Dark hair becomes map ink, pale skin becomes map paper. No black multiply.
    float paper = smoothstep(0.07, 0.82, value);
    COLOR = vec4(mix(map_ink.rgb, paper_color.rgb, paper), portrait.a * map_ink.a);
}
