using Unity.Entities;

public struct Health : IComponentData
{
    public float MaxHealth;
    public float CurrentHealth;
    public bool IsDead => CurrentHealth <= 0;
}
