using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

public class CreateAllAnimations
{
    private const string ControllerPath  = "Assets/Animations/Player/PlayerAnimator.controller";
    private const string ClipsOutputPath = "Assets/Animations/Player/";
    private const string IdlePath        = "Assets/Sprites/Characters/Adventurer/Sprites/IDLE";
    private const string RunPath         = "Assets/Sprites/Characters/Adventurer/Sprites/RUN";
    private const string AttackPath      = "Assets/Sprites/Characters/Adventurer/Sprites/ATTACK";

    private static readonly string[] Directions = { "down", "left", "right", "up" };

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Create All Animations")]
    public static void CreateAll()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[CreateAnimations] Controller not found: {ControllerPath}");
            return;
        }

        EnsureFolder("Assets/Animations/Player");

        EnsureParameter(controller, "DirectionX",  AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "DirectionY",  AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "isMoving",    AnimatorControllerParameterType.Bool);

        var sm     = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();

        for (int i = 0; i < Directions.Length; i++)
        {
            string dir = Directions[i];
            string cap = Capitalize(dir);
            float  col = -450f + i * 300f;

            states[$"Idle_{cap}"]   = GetOrCreateState(sm, $"Idle_{cap}",
                CreateOrUpdateClip($"Idle_{cap}",   IdlePath,   dir, 8f,  loop: true),
                new Vector3(col, 0f));

            states[$"Run_{cap}"]    = GetOrCreateState(sm, $"Run_{cap}",
                CreateOrUpdateClip($"Run_{cap}",    RunPath,    dir, 10f, loop: true),
                new Vector3(col, 130f));

            states[$"Attack_{cap}"] = GetOrCreateState(sm, $"Attack_{cap}",
                CreateOrUpdateClip($"Attack_{cap}", AttackPath, dir, 12f, loop: false),
                new Vector3(col, 260f));
        }

        sm.defaultState = states["Idle_Down"];

        SetupTransitions(sm, states);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateAnimations] Done — 12 clips created and all transitions configured.");
    }

    // ── Clip creation ─────────────────────────────────────────────────────────

    private static AnimationClip CreateOrUpdateClip(string clipName, string spritesFolder, string dirFilter, float fps, bool loop)
    {
        string assetPath = $"{ClipsOutputPath}{clipName}.anim";
        var sprites      = LoadSprites(spritesFolder, dirFilter);

        if (sprites.Count == 0)
            Debug.LogWarning($"[CreateAnimations] No '{dirFilter}' sprites found in '{spritesFolder}' for clip '{clipName}'");

        bool isNew   = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) == null;
        var  clip    = isNew ? new AnimationClip() : AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        clip.name    = clipName;
        clip.frameRate = fps;

        var settings   = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (sprites.Count > 0)
        {
            float dur  = 1f / fps;
            var   keys = sprites
                .Select((s, i) => new ObjectReferenceKeyframe { time = i * dur, value = s })
                .ToArray();

            var binding = new EditorCurveBinding
            {
                type         = typeof(SpriteRenderer),
                path         = "",
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }

        if (isNew)
            AssetDatabase.CreateAsset(clip, assetPath);
        else
            EditorUtility.SetDirty(clip);

        Debug.Log($"[CreateAnimations] Clip '{clipName}': {sprites.Count} frames @ {fps} fps, loop={loop}");
        return clip;
    }

    private static List<Sprite> LoadSprites(string folderPath, string filter)
    {
        var result    = new List<Sprite>();
        var seenPaths = new HashSet<string>();
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!seenPaths.Add(path)) continue;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite s && s.name.ToLower().Contains(filter))
                    result.Add(s);
            }
        }

        return result.OrderBy(s => s.name).ToList();
    }

    // ── State helpers ─────────────────────────────────────────────────────────

    private static AnimatorState GetOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
    {
        foreach (var cs in sm.states)
        {
            if (cs.state.name != name) continue;
            cs.state.motion = clip;
            return cs.state;
        }
        var state = sm.AddState(name, pos);
        state.motion = clip;
        return state;
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private static void SetupTransitions(AnimatorStateMachine sm, Dictionary<string, AnimatorState> states)
    {
        // Clear old AnyState transitions to avoid duplicates
        foreach (var t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);

        // Clear existing Run→Idle and Attack→Idle transitions
        foreach (string dir in Directions)
        {
            string cap = Capitalize(dir);
            ClearTransitions(states[$"Run_{cap}"]);
            ClearTransitions(states[$"Attack_{cap}"]);
        }

        // ── AnyState → Run (horizontal priority over vertical) ────────────────
        AnyTo(sm, states["Run_Right"], 0.1f,
            BoolCond("isMoving", true),
            FloatCond("DirectionX", AnimatorConditionMode.Greater, 0.5f));

        AnyTo(sm, states["Run_Left"], 0.1f,
            BoolCond("isMoving", true),
            FloatCond("DirectionX", AnimatorConditionMode.Less, -0.5f));

        AnyTo(sm, states["Run_Up"], 0.1f,
            BoolCond("isMoving", true),
            FloatCond("DirectionY",  AnimatorConditionMode.Greater, 0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Greater, -0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Less,    0.5f));

        AnyTo(sm, states["Run_Down"], 0.1f,
            BoolCond("isMoving", true),
            FloatCond("DirectionY",  AnimatorConditionMode.Less,    -0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Greater, -0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Less,    0.5f));

        // ── Run → matching Idle ───────────────────────────────────────────────
        foreach (string dir in Directions)
        {
            string cap = Capitalize(dir);
            var t = states[$"Run_{cap}"].AddTransition(states[$"Idle_{cap}"]);
            t.hasExitTime = false;
            t.duration    = 0.1f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "isMoving");
        }

        // ── AnyState → Attack (same direction priority) ───────────────────────
        AnyTo(sm, states["Attack_Right"], 0.05f,
            BoolCond("IsAttacking", true),
            FloatCond("DirectionX", AnimatorConditionMode.Greater, 0.5f));

        AnyTo(sm, states["Attack_Left"], 0.05f,
            BoolCond("IsAttacking", true),
            FloatCond("DirectionX", AnimatorConditionMode.Less, -0.5f));

        AnyTo(sm, states["Attack_Up"], 0.05f,
            BoolCond("IsAttacking", true),
            FloatCond("DirectionY",  AnimatorConditionMode.Greater, 0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Greater, -0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Less,    0.5f));

        AnyTo(sm, states["Attack_Down"], 0.05f,
            BoolCond("IsAttacking", true),
            FloatCond("DirectionY",  AnimatorConditionMode.Less,    -0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Greater, -0.5f),
            FloatCond("DirectionX",  AnimatorConditionMode.Less,    0.5f));

        // ── Attack → matching Idle (plays full clip, then returns) ────────────
        foreach (string dir in Directions)
        {
            string cap = Capitalize(dir);
            var t = states[$"Attack_{cap}"].AddTransition(states[$"Idle_{cap}"]);
            t.hasExitTime = true;
            t.exitTime    = 1f;
            t.duration    = 0.1f;
        }
    }

    // ── Transition helpers ────────────────────────────────────────────────────

    private struct Cond { public string param; public AnimatorConditionMode mode; public float threshold; }

    private static Cond BoolCond(string p, bool v) => new Cond
    {
        param     = p,
        mode      = v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
        threshold = 0f
    };

    private static Cond FloatCond(string p, AnimatorConditionMode m, float v) => new Cond
    {
        param = p, mode = m, threshold = v
    };

    private static void AnyTo(AnimatorStateMachine sm, AnimatorState dest, float duration, params Cond[] conds)
    {
        var t = sm.AddAnyStateTransition(dest);
        t.hasExitTime          = false;
        t.duration             = duration;
        t.canTransitionToSelf  = false;
        foreach (var c in conds)
            t.AddCondition(c.mode, c.threshold, c.param);
    }

    private static void ClearTransitions(AnimatorState state)
    {
        foreach (var t in state.transitions.ToArray())
            state.RemoveTransition(t);
    }

    // ── Parameter & folder helpers ────────────────────────────────────────────

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in ctrl.parameters)
            if (p.name == name) return;
        ctrl.AddParameter(name, type);
        Debug.Log($"[CreateAnimations] Added parameter '{name}' ({type})");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int    slash  = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string folder = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static string Capitalize(string s) => char.ToUpper(s[0]) + s.Substring(1);
}
