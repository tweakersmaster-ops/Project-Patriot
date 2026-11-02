using UnityEngine;

// Простой инвентарь ключевых предметов игрока.
// Статический класс, чтобы доступ был откуда угодно: PlayerInventory.hasPaddockKey
public static class PlayerInventory
{
    public static bool hasPaddockKey;

    public static void Reset()
    {
        hasPaddockKey = false;
    }
}
