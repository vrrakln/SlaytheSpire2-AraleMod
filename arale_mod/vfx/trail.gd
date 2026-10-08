extends Node2D
## Child of the game's NCardTrailVfx. Records global positions; root FadeOut controls alpha.
@export var history_seconds: float = 0.45
var points: Array[Vector2] = []
var ages: Array[float] = []
func _process(delta: float) -> void:
    for i in range(ages.size()): ages[i] += delta
    while ages.size() > 0 and ages[0] > history_seconds:
        ages.pop_front()
        points.pop_front()
    var p = get_parent().global_position
    if points.is_empty() or points[-1].distance_to(p) > 3:
        points.append(p)
        ages.append(0.0)
    queue_redraw()
func reset() -> void:
    points.clear()
    ages.clear()
func _draw() -> void:
    for i in range(1,points.size()):
        var strength = 1.0 - ages[i]/history_seconds
        var a = to_local(points[i-1])
        var b = to_local(points[i])
        draw_line(a,b,Color(1.0,0.78,0.18,strength*0.16),20.0*strength+1,true)
        draw_line(a,b,Color(1.0,0.88,0.35,strength*0.75),6.0*strength+0.5,true)
        draw_line(a,b,Color(1.0,0.98,0.82,strength*0.9),2.0*strength+0.5,true)
        if i % 4 == 0:
            var offset = Vector2(0,sin(i*1.8)*14)
            var c = Color("ff70bf") if i%8==0 else Color("70e8ff")
            c.a = strength*0.8
            draw_line(b+offset-Vector2(4,0)*strength,b+offset+Vector2(4,0)*strength,c,1.5,true)
            draw_line(b+offset-Vector2(0,4)*strength,b+offset+Vector2(0,4)*strength,c,1.5,true)
