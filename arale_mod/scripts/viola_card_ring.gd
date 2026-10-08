extends Control
## Visual-only native card overlay. No card art, input, or combat state writes.
@export var bleak: bool = false
@export var animate: bool = true
var elapsed: float = 0.0
const LEFT: float = -148.0
const TOP: float = -207.0
const WIDTH: float = 296.0
const HEIGHT: float = 414.0
const VIOLET = preload("res://arale_mod/images/card_overlays/viola/violet.png")
const WILTED = preload("res://arale_mod/images/card_overlays/viola/wilted_violet.png")
const PETAL = preload("res://arale_mod/images/card_overlays/viola/petal.png")
const ASH = preload("res://arale_mod/images/card_overlays/viola/ash_petal.png")
const FLAME = preload("res://arale_mod/images/card_overlays/viola/purple_flame.png")

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	focus_mode = Control.FOCUS_NONE
	queue_redraw()

func _process(delta: float) -> void:
	if animate and is_visible_in_tree():
		elapsed = fmod(elapsed + delta, 6.0)
		queue_redraw()

func _sprite(tex: Texture2D, pos: Vector2, span: float, angle: float, alpha: float = 1.0) -> void:
	var vertices := PackedVector2Array()
	for corner in [Vector2(-1,-1),Vector2(1,-1),Vector2(1,1),Vector2(-1,1)]:
		vertices.append(pos + (corner * span * 0.5).rotated(angle))
	draw_polygon(vertices, PackedColorArray([Color(1,1,1,alpha)]), PackedVector2Array([Vector2.ZERO,Vector2.RIGHT,Vector2.ONE,Vector2.DOWN]), tex)

func _flame(pos: Vector2, width: float, height: float, phase: float, alpha: float = 0.85) -> void:
	var frame: float = fposmod(elapsed * 32.0 + phase * 10.0, 64.0)
	var index: int = int(frame)
	var next: int = (index + 1) % 64
	var rect := Rect2(pos - Vector2(width * 0.5, height * 0.925), Vector2(width, height))
	# Interpolate adjacent atlas frames at the render rate; 2-second seamless cycle.
	draw_texture_rect_region(FLAME, rect, Rect2((index % 8)*128,(index / 8)*160,128,160), Color(1,1,1,alpha*(1.0-fract(frame))))
	draw_texture_rect_region(FLAME, rect, Rect2((next % 8)*128,(next / 8)*160,128,160), Color(1,1,1,alpha*fract(frame)))

func fract(value: float) -> float:
	return value - floor(value)

func _border(u: float) -> Vector2:
	var d: float = fposmod(u,1.0) * (WIDTH + HEIGHT) * 2.0
	if d < WIDTH: return Vector2(LEFT+d,TOP)
	d -= WIDTH
	if d < HEIGHT: return Vector2(LEFT+WIDTH,TOP+d)
	d -= HEIGHT
	if d < WIDTH: return Vector2(LEFT+WIDTH-d,TOP+HEIGHT)
	return Vector2(LEFT,TOP+HEIGHT-(d-WIDTH))

func _draw() -> void:
	var t: float = elapsed * TAU / 6.0
	if bleak:
		for side in [-1.0,1.0]:
			for i in range(9):
				_flame(Vector2(side*148.0,TOP+HEIGHT-8-i*HEIGHT/9.0),45.0,48.0+12.0*sin(t*2.0+i*1.8),i+side)
		for i in range(8):
			_flame(Vector2(LEFT+12+i*(WIDTH-24)/7.0,TOP+HEIGHT+3),39.0,45.0+9.0*sin(t*2.0+i),i)
		for i in range(6):
			_flame(Vector2(LEFT+20+i*(WIDTH-40)/5.0,TOP+17),29.0,34.0+5.0*sin(t*2.0+i),i,0.65)
		_sprite(WILTED,Vector2(LEFT-2,TOP+HEIGHT-2),39,-0.5)
		_sprite(WILTED,Vector2(LEFT+WIDTH+2,TOP+HEIGHT-2),36,0.5)
		for side in [-1.0,1.0]:
			for i in range(8):
				var u: float = fposmod(elapsed/6.0+i/8.0,1.0)
				var pos := Vector2(side*(149.0+sin(u*TAU+i)*3.0),TOP+HEIGHT-u*(HEIGHT+38))
				_sprite(ASH,pos,14.0+i%3,u*5+side,sin(u*PI)*0.85)
				draw_circle(pos+Vector2(side*7,-10),1.4,Color(0.88,0.68,1.0,sin(u*PI)*0.7))
	else:
		for i in range(7):
			_flame(_border(i/7.0) + Vector2(0, 12 if _border(i/7.0).y < TOP+2 else 0),27,32+6*sin(t+i),i,0.55)
		for i in range(13):
			var u: float = i/13.0+elapsed/6.0
			var pos: Vector2 = _border(u)+Vector2(sin(i+t)*4.0,0)
			_sprite(PETAL,pos,13.0+i%3,u*TAU*2.0,0.65+0.2*sin(t+i))
		_sprite(VIOLET,Vector2(LEFT-2,TOP+HEIGHT-2),40,-0.3+sin(t)*0.08)
		_sprite(VIOLET,Vector2(LEFT+WIDTH+2,TOP+87),33,1.0+sin(t+0.5)*0.12)
		_sprite(VIOLET,Vector2(LEFT+WIDTH+2,TOP+HEIGHT-2),28,-0.8)
		for i in range(12):
			var u: float = fposmod(i/12.0+elapsed/6.0,1.0)
			draw_circle(_border(u),1.3,Color(0.89,0.74,1.0,0.4+0.2*sin(u*TAU)))

