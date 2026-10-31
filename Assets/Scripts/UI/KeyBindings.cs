using UnityEngine;

// Переназначение клавиш. Хранится отдельно от сохранений игры (PlayerPrefs "controls.*")
public enum BindableAction
{
    MoveForward,
    MoveBack,
    MoveLeft,
    MoveRight,
    Jump,
    Sprint,
    Crouch,
    Fire,
    Aim,
    Reload,
    Slot1,
    Slot2,
    Slot3,
    Slot4,
    Slot5,
    Slot6,
    Slot7
}

public static class KeyBindings
{
    private const string Prefix = "controls.";

    public static readonly BindableAction[] AllActions = new BindableAction[]
    {
        BindableAction.MoveForward, BindableAction.MoveBack,
        BindableAction.MoveLeft, BindableAction.MoveRight,
        BindableAction.Jump, BindableAction.Sprint,
        BindableAction.Crouch, BindableAction.Fire,
        BindableAction.Aim, BindableAction.Reload,
        BindableAction.Slot1, BindableAction.Slot2,
        BindableAction.Slot3, BindableAction.Slot4,
        BindableAction.Slot5, BindableAction.Slot6,
        BindableAction.Slot7
    };

    public static KeyCode GetDefault(BindableAction action)
    {
        switch (action)
        {
            case BindableAction.MoveForward: return KeyCode.W;
            case BindableAction.MoveBack: return KeyCode.S;
            case BindableAction.MoveLeft: return KeyCode.A;
            case BindableAction.MoveRight: return KeyCode.D;
            case BindableAction.Jump: return KeyCode.Space;
            case BindableAction.Sprint: return KeyCode.LeftShift;
            case BindableAction.Crouch: return KeyCode.LeftControl;
            case BindableAction.Fire: return KeyCode.Mouse0;
            case BindableAction.Aim: return KeyCode.Mouse1;
            case BindableAction.Reload: return KeyCode.R;
            case BindableAction.Slot1: return KeyCode.Alpha1;
            case BindableAction.Slot2: return KeyCode.Alpha2;
            case BindableAction.Slot3: return KeyCode.Alpha3;
            case BindableAction.Slot4: return KeyCode.Alpha4;
            case BindableAction.Slot5: return KeyCode.Alpha5;
            case BindableAction.Slot6: return KeyCode.Alpha6;
            case BindableAction.Slot7: return KeyCode.Alpha7;
        }

        return KeyCode.None;
    }

    public static KeyCode Get(BindableAction action)
    {
        int fallback = (int)GetDefault(action);
        return (KeyCode)PlayerPrefs.GetInt(Prefix + action, fallback);
    }

    public static void Set(BindableAction action, KeyCode key)
    {
        PlayerPrefs.SetInt(Prefix + action, (int)key);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        for (int i = 0; i < AllActions.Length; i++)
        {
            PlayerPrefs.DeleteKey(Prefix + AllActions[i]);
        }

        PlayerPrefs.Save();
    }

    public static bool GetKey(BindableAction action) { return Input.GetKey(Get(action)); }
    public static bool GetKeyDown(BindableAction action) { return Input.GetKeyDown(Get(action)); }

    public static float Horizontal
    {
        get
        {
            float value = 0f;
            if (GetKey(BindableAction.MoveRight)) value += 1f;
            if (GetKey(BindableAction.MoveLeft)) value -= 1f;
            return value;
        }
    }

    public static float Vertical
    {
        get
        {
            float value = 0f;
            if (GetKey(BindableAction.MoveForward)) value += 1f;
            if (GetKey(BindableAction.MoveBack)) value -= 1f;
            return value;
        }
    }

    // Подпись клавиши для интерфейса
    public static string DisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "ЛКМ";
            case KeyCode.Mouse1: return "ПКМ";
            case KeyCode.Mouse2: return "СКМ";
            case KeyCode.Space: return "Пробел";
            case KeyCode.LeftShift: return "Shift";
            case KeyCode.RightShift: return "Правый Shift";
            case KeyCode.LeftControl: return "Ctrl";
            case KeyCode.RightControl: return "Правый Ctrl";
            case KeyCode.LeftAlt: return "Alt";
            case KeyCode.RightAlt: return "Правый Alt";
            case KeyCode.Escape: return "Esc";
            case KeyCode.UpArrow: return "Стрелка вверх";
            case KeyCode.DownArrow: return "Стрелка вниз";
            case KeyCode.LeftArrow: return "Стрелка влево";
            case KeyCode.RightArrow: return "Стрелка вправо";
            case KeyCode.Backspace: return "Backspace";
            case KeyCode.Return: return "Enter";
            case KeyCode.Tab: return "Tab";
            case KeyCode.None: return "—";
        }

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
        {
            return ((int)key - (int)KeyCode.Alpha0).ToString();
        }

        if (key >= KeyCode.Mouse3 && key <= KeyCode.Mouse6)
        {
            return "Мышь " + ((int)key - (int)KeyCode.Mouse3 + 4);
        }

        return key.ToString();
    }

    public static string ActionName(BindableAction action)
    {
        switch (action)
        {
            case BindableAction.MoveForward: return "Вперёд";
            case BindableAction.MoveBack: return "Назад";
            case BindableAction.MoveLeft: return "Влево";
            case BindableAction.MoveRight: return "Вправо";
            case BindableAction.Jump: return "Прыжок";
            case BindableAction.Sprint: return "Бег";
            case BindableAction.Crouch: return "Присесть";
            case BindableAction.Fire: return "Огонь";
            case BindableAction.Aim: return "Прицел";
            case BindableAction.Reload: return "Перезарядка";
            case BindableAction.Slot1: return "Оружие 1";
            case BindableAction.Slot2: return "Оружие 2";
            case BindableAction.Slot3: return "Оружие 3";
            case BindableAction.Slot4: return "Оружие 4";
            case BindableAction.Slot5: return "Оружие 5";
            case BindableAction.Slot6: return "Оружие 6";
            case BindableAction.Slot7: return "Оружие 7";
        }

        return action.ToString();
    }
}
