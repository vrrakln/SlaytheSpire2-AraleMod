shader_type canvas_item;
uniform float threshold : hint_range(0.0,1.0) = 0.0;
float triangle(vec2 p, vec2 a, vec2 b, vec2 c) {
    vec2 e0=b-a; vec2 e1=c-b; vec2 e2=a-c;
    vec2 v0=p-a; vec2 v1=p-b; vec2 v2=p-c;
    float s=-sign(e0.x*e2.y-e0.y*e2.x);
    return max(max(s*(v0.x*e0.y-v0.y*e0.x)/length(e0),s*(v1.x*e1.y-v1.y*e1.x)/length(e1)),s*(v2.x*e2.y-v2.y*e2.x)/length(e2));
}
void fragment() {
    vec2 p=(UV-vec2(0.5))*vec2(1.777778,1.0);
    float scale=mix(0.001,3.5,threshold*threshold);
    vec2 q=p/scale;
    float d=length(q)-0.30;
    d=min(d,triangle(q,vec2(-0.29,-0.10),vec2(-0.30,-0.45),vec2(-0.04,-0.26)));
    d=min(d,triangle(q,vec2(0.29,-0.10),vec2(0.04,-0.26),vec2(0.30,-0.45)));
    float edge=0.009/scale;
    float alpha=1.0-smoothstep(-edge,edge,d);
    float rim=smoothstep(-0.025/scale,-0.010/scale,d);
    vec3 glow=mix(vec3(1.0,0.35,0.68),vec3(0.36,0.88,1.0),UV.x);
    glow=mix(glow,vec3(1.0,0.87,0.30),0.6);
    COLOR=vec4(mix(vec3(0.035,0.025,0.07),glow,rim),alpha);
    if(threshold<=0.0) COLOR.a=0.0;
    if(threshold>=1.0) COLOR=vec4(0.035,0.025,0.07,1.0);
}
