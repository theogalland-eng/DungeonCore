using UnityEngine;

/// <summary>
/// Interface pour tous les entités qui peuvent recevoir des dégâts
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
    bool IsAlive();
    float GetCurrentHealth();
    string GetName();
}
