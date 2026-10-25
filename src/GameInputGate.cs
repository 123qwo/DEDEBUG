using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DEDEBUG;

internal static class GameInputGate
{
    private static bool Blocked => DedebugPlugin.InputCaptured;
    public static bool GetKey(KeyCode key) => !Blocked && Input.GetKey(key);
    public static bool GetKey(string key) => !Blocked && Input.GetKey(key);
    public static bool GetKeyDown(KeyCode key) => !Blocked && Input.GetKeyDown(key);
    public static bool GetKeyDown(string key) => !Blocked && Input.GetKeyDown(key);
    public static bool GetKeyUp(KeyCode key) => !Blocked && Input.GetKeyUp(key);
    public static bool GetKeyUp(string key) => !Blocked && Input.GetKeyUp(key);
    public static bool GetMouseButton(int button) => !Blocked && Input.GetMouseButton(button);
    public static bool GetMouseButtonDown(int button) => !Blocked && Input.GetMouseButtonDown(button);
    public static bool GetMouseButtonUp(int button) => !Blocked && Input.GetMouseButtonUp(button);
    public static bool GetButton(string button) => !Blocked && Input.GetButton(button);
    public static bool GetButtonDown(string button) => !Blocked && Input.GetButtonDown(button);
    public static bool GetButtonUp(string button) => !Blocked && Input.GetButtonUp(button);
    public static float GetAxis(string axis) => Blocked ? 0 : Input.GetAxis(axis);
    public static float GetAxisRaw(string axis) => Blocked ? 0 : Input.GetAxisRaw(axis);
    public static Touch GetTouch(int finger) => Blocked ? default : Input.GetTouch(finger);
    public static int touchCount => Blocked ? 0 : Input.touchCount;
    public static Vector2 mouseScrollDelta => Blocked ? Vector2.zero : Input.mouseScrollDelta;
    public static Vector3 mousePosition => Blocked ? Vector3.zero : Input.mousePosition;
    public static bool anyKey => !Blocked && Input.anyKey;
    public static bool anyKeyDown => !Blocked && Input.anyKeyDown;
    public static string inputString => Blocked ? "" : Input.inputString;
    internal static MethodInfo Replacement(MethodInfo original)
    {
        if (original.DeclaringType != typeof(Input) || original.Name.StartsWith("set_", StringComparison.Ordinal)) return null;
        var result = typeof(GameInputGate).GetMethod(original.Name, BindingFlags.Public | BindingFlags.Static,
            null, original.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), null);
        if (result == null || result.ReturnType != original.ReturnType) throw new MissingMethodException("Input wrapper missing: " + original);
        return result;
    }
}

