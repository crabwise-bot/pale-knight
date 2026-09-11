using Godot;

namespace PaleKnight;

/// <summary>Anything the player's nail (or a hazard) can damage: enemies and the boss.</summary>
public interface IDamageable
{
    bool IsDead { get; }
    void TakeHit(int damage, Vector2 fromPosition, Vector2 hitDirection);
}
