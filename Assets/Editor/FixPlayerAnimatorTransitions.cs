using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class FixPlayerAnimatorTransitions
{
    private const string ControllerPath = "Assets/Animations/Player/PlayerAnimator.controller";

    [MenuItem("Tools/Fix Player Animator Transitions")]
    public static void Fix()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[FixAnimator] Controller not found at: {ControllerPath}");
            return;
        }

        int fixedCount = 0;

        foreach (var layer in controller.layers)
        {
            foreach (var state in layer.stateMachine.states)
            {
                foreach (var transition in state.state.transitions)
                {
                    bool changed = false;

                    if (state.state.name == "Idle_Down" && transition.destinationState?.name == "Run_Down")
                    {
                        changed = ApplyTransitionSettings(transition, "isMoving", true);
                    }
                    else if (state.state.name == "Run_Down" && transition.destinationState?.name == "Idle_Down")
                    {
                        changed = ApplyTransitionSettings(transition, "isMoving", false);
                    }

                    if (changed) fixedCount++;
                }
            }
        }

        if (fixedCount > 0)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FixAnimator] Fixed {fixedCount} transition(s) in {ControllerPath}");
        }
        else
        {
            Debug.LogWarning("[FixAnimator] No matching transitions found. Check state names in the controller.");
        }

        Verify(controller);
    }

    private static bool ApplyTransitionSettings(AnimatorStateTransition transition, string paramName, bool value)
    {
        transition.hasExitTime = false;
        transition.duration = 0.1f;

        // Remove existing conditions for this parameter
        var conditions = new List<AnimatorCondition>(transition.conditions);
        conditions.RemoveAll(c => c.parameter == paramName);

        // Add the correct condition
        conditions.Add(new AnimatorCondition
        {
            parameter = paramName,
            mode = value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            threshold = 0f
        });

        transition.conditions = conditions.ToArray();

        string directionLabel = value ? "true" : "false";
        Debug.Log($"[FixAnimator] Set transition '{transition.destinationState?.name}': hasExitTime=false, {paramName}={directionLabel}, duration=0.1");
        return true;
    }

    private static void Verify(AnimatorController controller)
    {
        bool idleToRun = false;
        bool runToIdle = false;

        foreach (var layer in controller.layers)
        {
            foreach (var state in layer.stateMachine.states)
            {
                foreach (var t in state.state.transitions)
                {
                    if (state.state.name == "Idle_Down" && t.destinationState?.name == "Run_Down")
                    {
                        bool ok = !t.hasExitTime && HasCondition(t, "isMoving", true);
                        Debug.Log($"[FixAnimator] Verify Idle_Down→Run_Down: hasExitTime={t.hasExitTime}, conditionOK={HasCondition(t, "isMoving", true)} — {(ok ? "OK" : "FAIL")}");
                        idleToRun = ok;
                    }
                    else if (state.state.name == "Run_Down" && t.destinationState?.name == "Idle_Down")
                    {
                        bool ok = !t.hasExitTime && HasCondition(t, "isMoving", false);
                        Debug.Log($"[FixAnimator] Verify Run_Down→Idle_Down: hasExitTime={t.hasExitTime}, conditionOK={HasCondition(t, "isMoving", false)} — {(ok ? "OK" : "FAIL")}");
                        runToIdle = ok;
                    }
                }
            }
        }

        if (idleToRun && runToIdle)
            Debug.Log("[FixAnimator] All transitions verified successfully.");
        else
            Debug.LogWarning("[FixAnimator] Verification incomplete — some transitions may be missing or misnamed.");
    }

    private static bool HasCondition(AnimatorStateTransition t, string param, bool value)
    {
        foreach (var c in t.conditions)
        {
            if (c.parameter == param)
            {
                bool match = value
                    ? c.mode == AnimatorConditionMode.If
                    : c.mode == AnimatorConditionMode.IfNot;
                if (match) return true;
            }
        }
        return false;
    }
}
