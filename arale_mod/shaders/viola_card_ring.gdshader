shader_type canvas_item;
render_mode unshaded;

// Allow ornament overlap at the frame; protect the actual portrait and text.
uniform bool protect_energy = false;
varying vec2 card_position;

void vertex() {
    card_position = VERTEX;
}

void fragment() {
    bool portrait = abs(card_position.x) < 126.0 && card_position.y > -167.0 && card_position.y < 24.0;
    bool description = abs(card_position.x) < 131.0 && card_position.y > 42.0 && card_position.y < 186.0;
    bool energy = protect_energy && card_position.x > -173.0 && card_position.x < -95.0 && card_position.y > -235.0 && card_position.y < -157.0;
    if (portrait || description || energy) {
        discard;
    }
}
