extends Node2D
## One-shot procedural effect; no external textures or combat behavior changes.
@export_range(0,4) var variant: int = 0
@export var duration: float = 0.65
@export var auto_play: bool = true
var elapsed: float = 0.0
const GOLD = Color("ffe45e")
const PINK = Color("ff62b9")
const CYAN = Color("65e7ff")
func _process(delta: float) -> void:
    if auto_play:
        elapsed += delta
        if elapsed >= duration:
            queue_free()
            return
    queue_redraw()
func star(center: Vector2, radius: float, color: Color, turn: float = 0.0) -> void:
    var pts = PackedVector2Array()
    for k in range(10):
        var a = k * PI / 5.0 - PI / 2.0 + turn
        pts.append(center + Vector2.from_angle(a) * radius * (1.0 if k % 2 == 0 else 0.43))
    draw_colored_polygon(pts, color)
func _draw() -> void:
    var t = clampf(elapsed / duration, 0.0, 1.0)
    if t <= 0.0 or t >= 1.0: return
    var fade = pow(1.0-t,0.8) * minf(t*18.0,1.0)
    var gold = Color(GOLD,fade)
    var pink = Color(PINK,fade)
    var cyan = Color(CYAN,fade)
    var radius = 15.0 + 140.0 * (1.0-pow(1.0-t,2.0))
    if variant == 0: # Golden star punch
        star(Vector2.ZERO, 55.0*(1.0-t)+12.0, gold,t*0.6)
        draw_arc(Vector2.ZERO,radius,0,TAU,80,Color(GOLD,fade*0.6),3,true)
    elif variant == 1: # Voice rings
        for k in range(3):
            var r = maxf(1.0,radius-k*24.0)
            draw_arc(Vector2.ZERO,r,-0.9,0.9,48,[gold,pink,cyan][k],5.0*(1.0-t)+1,true)
            draw_arc(Vector2.ZERO,r,PI-0.9,PI+0.9,48,[gold,pink,cyan][k],5.0*(1.0-t)+1,true)
    elif variant == 2: # Twin-tail arcs
        for k in range(2):
            draw_arc(Vector2.ZERO,radius*(0.75+k*0.15),-2.6+t+k*PI,0.15+t+k*PI,70,[pink,cyan][k],11.0*(1.0-t)+1,true)
        star(Vector2.ZERO,24.0*(1.0-t),gold)
    elif variant == 3: # Dream star shower
        for k in range(7):
            var pos = Vector2.from_angle(k*TAU/7+0.2)*radius*0.72
            star(pos,18.0*(1-t)+3,[gold,pink,cyan][k%3],t+k)
    else: # Cat-ear resonance
        draw_arc(Vector2.ZERO,radius*0.62,0,TAU,90,gold,5.0*(1-t)+1,true)
        for side in [-1,1]:
            var tip = Vector2(side*radius*0.48,-radius*0.83)
            draw_polyline(PackedVector2Array([Vector2(side*radius*0.62,-radius*0.2),tip,Vector2(side*radius*0.17,-radius*0.60)]),[pink,cyan][int(side==1)],5.0*(1-t)+1,true)
        star(Vector2.ZERO,35.0*(1-t)+5,gold,t*0.4)
    for k in range(10):
        var a = k*TAU/10 + variant*0.31
        var dir = Vector2.from_angle(a)
        draw_line(dir*(radius+6),dir*(radius+14+17*(1-t)),Color([GOLD,PINK,CYAN][k%3],fade*0.75),2,true)
