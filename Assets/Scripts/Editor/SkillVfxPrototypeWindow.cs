using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class SkillVfxPrototypeWindow : EditorWindow
{
    private static readonly SkillId[] Skills = Enum.GetValues(typeof(SkillId)).Cast<SkillId>().Where(x => x != SkillId.None).ToArray();
    private static readonly string[] Labels = Skills.Select(Label).ToArray();
    private static string Label(SkillId id) => id switch
    {
        SkillId.Sword_Slash => "斬撃  (Sword_Slash)",
        SkillId.Sword_QuickSlash => "速斬  (Sword_QuickSlash)",
        SkillId.Sword_StrongSlash => "強斬撃  (Sword_StrongSlash)",
        SkillId.Shield_Slash => "盾撃  (Shield_Slash)",
        SkillId.Shield_ShoulderGuard => "肩代わり  (Shield_ShoulderGuard)",
        SkillId.Shield_IronWall => "鉄壁  (Shield_IronWall)",
        SkillId.Shield_Taunt => "挑発  (Shield_Taunt)",
        SkillId.Wand_Bolt => "魔弾  (Wand_Bolt)",
        SkillId.Wand_ArcaneBlast => "極大魔弾  (Wand_ArcaneBlast)",
        SkillId.Wand_AreaBlast => "範囲魔法  (Wand_AreaBlast)",
        SkillId.Wand_GodsHand => "神の手  (Wand_GodsHand)",
        SkillId.Grimoire_Bolt => "通常攻撃（呪弾）  (Grimoire_Bolt)",
        SkillId.Grimoire_StrDebuff => "STRデバフ  (Grimoire_StrDebuff)",
        SkillId.StatDebuff_INT => "INTデバフ  (StatDebuff_INT)",
        SkillId.StatDebuff_FAI => "FAIデバフ  (StatDebuff_FAI)",
        SkillId.StatDebuff_AGI => "AGIデバフ  (StatDebuff_AGI)",
        SkillId.Grimoire_Bind => "金縛り  (Grimoire_Bind)",
        SkillId.Grimoire_Poison => "毒  (Grimoire_Poison)",
        SkillId.Grimoire_Stealth => "不可視  (Grimoire_Stealth)",
        SkillId.Bible_Smite => "通常攻撃（聖書の聖撃）  (Bible_Smite)",
        SkillId.Bible_StrBuff => "STRバフ  (Bible_StrBuff)",
        SkillId.Bible_FaiBuff => "FAIバフ  (Bible_FaiBuff)",
        SkillId.Bible_IntBuff => "INTバフ  (Bible_IntBuff)",
        SkillId.Bible_AgiBuff => "AGIバフ  (Bible_AgiBuff)",
        SkillId.Bible_Invulnerable => "無敵  (Bible_Invulnerable)",
        SkillId.Bible_Gotsume => "ゴツメ  (Bible_Gotsume)",
        SkillId.Rosary_Strike => "通常攻撃（ロザリオの打撃）  (Rosary_Strike)",
        SkillId.Rosary_DistantHeal => "遠隔癒し  (Rosary_DistantHeal)",
        SkillId.Rosary_CloseHeal => "大回復  (Rosary_CloseHeal)",
        SkillId.Rosary_Regeneration => "継続回復  (Rosary_Regeneration)",
        SkillId.Rosary_HealingArea => "回復エリア  (Rosary_HealingArea)",
        SkillId.Rosary_SacrificeThunder => "神の雷  (Rosary_SacrificeThunder)",
        _ => id.ToString()
    };
    private Camera _camera;
    private Transform _caster, _target;
    private readonly SkillVfxEffect[] _effects = new SkillVfxEffect[4];
    private readonly RenderTexture[] _views = new RenderTexture[3];
    private int _skill, _count = 1;
    private float _time = .2f, _speed = 1;
    private bool _playing, _cast, _atlas;
    private double _lastTime;
    private Vector2 _scroll;
    private string _message;
    private SkillBase Definition => CombatSkillFactory.Create(Skills[_skill]);
    private float Duration => _cast ? Mathf.Max(.1f, Definition.CastTimeSeconds) :
        SkillVfxArt.Duration(Skills[_skill]) +
        SkillVfxArt.PreviewLead(Skills[_skill]) + .15f;

    [MenuItem("Tools/Combat/Skill VFX Studio")]
    [MenuItem("Tools/Combat/Skill VFX Comparison")]
    public static void Open() => GetWindow<SkillVfxPrototypeWindow>("スキルVFX");
    private void OnEnable()
    {
        _lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Advance;
        EditorApplication.playModeStateChanged += OnPlayMode;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += OnSceneClosing;
        FindTargets();
    }
    private void FindTargets()
    {
        _camera = Camera.main;
        var caster = GameObject.Find("Caster"); var target = GameObject.Find("Target");
        if (caster != null) _caster = caster.transform;
        if (target != null) _target = target.transform;
        if (_target == null)
        {
            var characters = UnityEngine.Object.FindObjectsByType<Character>();
            if (characters.Length > 0) _target = characters[0].transform;
            if (characters.Length > 1) _caster = characters[1].transform;
        }
    }
    private void Advance()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_playing) { _time = (_time + (float)(now - _lastTime) * _speed) % Duration; Repaint(); }
        _lastTime = now;
    }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("全32種の画像併用エフェクト。時刻を固定して前後の形を比較できます。見た目の再生専用で、効果判定・カメラ設定・シーンは変更しません。", MessageType.Info);
        _camera = (Camera)EditorGUILayout.ObjectField("カメラ", _camera, typeof(Camera), true);
        _caster = (Transform)EditorGUILayout.ObjectField("術者", _caster, typeof(Transform), true);
        _target = (Transform)EditorGUILayout.ObjectField("対象／地点", _target, typeof(Transform), true);
        if (GUILayout.Button("現在のシーンから取得")) FindTargets();
        int previous = _skill;
        _skill = EditorGUILayout.Popup("スキル", _skill, Labels);
        if (_skill != previous) _time = .2f;
        _cast = EditorGUILayout.Toggle("詠唱段階（実際の詠唱時間）", _cast);
        if (_cast && Definition.CastTimeSeconds <= 0) EditorGUILayout.HelpBox("このスキルの詠唱時間は0秒です。実戦では詠唱段階を再生しません。", MessageType.Info);
        _time = EditorGUILayout.Slider("時刻（秒）", _time, 0, Duration);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(_playing ? "停止" : "繰り返し再生")) _playing = !_playing;
            if (GUILayout.Button("発生")) { _time = .025f; _playing = false; }
            if (GUILayout.Button("主動作")) { _time = _cast ? Duration * .8f : SkillVfxEffect.IsPersistent(Skills[_skill]) ? .65f : SkillVfxArt.PreviewLead(Skills[_skill]) + .2f; _playing = false; }
            if (GUILayout.Button("消失")) { _time = Mathf.Max(0, Duration - .25f); _playing = false; }
        }
        _speed = EditorGUILayout.Slider("速度", _speed, .1f, 1);
        _count = EditorGUILayout.IntSlider("同時表示", _count, 1, 4);
        if (_camera == null || _target == null || SkillVfxAtlas.Shared == null) return;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        bool horizontal = position.width >= 1000;
        if (horizontal) EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 3; i++)
        {
            if (horizontal) EditorGUILayout.BeginVertical();
            float time = Mathf.Clamp(_time + (i - 1) * .15f, 0, Duration);
            if (_views[i] == null) _views[i] = new RenderTexture(960, 540, 24) { hideFlags = HideFlags.HideAndDontSave };
            if (Event.current.type == EventType.Repaint) RenderFrame(time, _views[i]);
            EditorGUILayout.LabelField($"{(i == 0 ? "0.15秒前" : i == 1 ? "選択時刻" : "0.15秒後")}  {time:0.00}s", EditorStyles.boldLabel);
            float width = Mathf.Min(480, horizontal ? (position.width - 60) / 3 : position.width - 32);
            Rect rect = GUILayoutUtility.GetRect(width, width * 9 / 16, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _views[i], ScaleMode.ScaleToFit, false);
            if (horizontal) EditorGUILayout.EndVertical();
        }
        if (horizontal) EditorGUILayout.EndHorizontal();
        _atlas = EditorGUILayout.Foldout(_atlas, "共通素材アトラス（実際のアルファ）", true);
        if (_atlas)
        {
            Texture texture = SkillVfxAtlas.Shared.Material.mainTexture;
            Rect rect = GUILayoutUtility.GetRect(420, 420, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(.3f, .35f, .3f));
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            EditorGUILayout.LabelField($"{texture.width}×{texture.height} / {SkillVfxAtlas.Shared.Regions.Length}部品 / 1共有マテリアル");
        }
        if (GUILayout.Button("選択時刻をPNG保存")) SaveFrame();
        if (!string.IsNullOrEmpty(_message)) EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }
    public void RenderFrame(float time, RenderTexture destination)
    {
        if (_camera == null || _target == null) FindTargets();
        if (_camera == null || _target == null) return;
        Vector3 point = Feet(_target), caster = _caster != null ? Feet(_caster) : point - Vector3.right * 2;
        var definition = Definition;
        for (int i = 0; i < _count; i++)
        {
            if (_effects[i] == null)
            {
                var root = new GameObject("VFX studio preview") { hideFlags = HideFlags.HideAndDontSave };
                _effects[i] = root.AddComponent<SkillVfxEffect>();
            }
            Vector3 offset = i == 0 ? Vector3.zero : _camera.transform.right * (i % 2 == 0 ? -1 : 1) * .8f + Vector3.forward * .3f * i;
            var fx = _effects[i];
            fx.Prepare(Skills[_skill], caster + offset, point + offset, point + offset,
                _cast ? SkillVfxEffect.Phase.Cast : SkillVfxEffect.Phase.Preview, definition.CastTimeSeconds, definition.AreaRadius);
            fx.SetPreviewCamera(_camera);
            fx.RenderAt(time);
        }
        RenderTexture previous = _camera.targetTexture, active = RenderTexture.active;
        try { _camera.targetTexture = destination; _camera.Render(); }
        finally
        {
            _camera.targetTexture = previous; RenderTexture.active = active;
            foreach (var fx in _effects) if (fx != null) fx.gameObject.SetActive(false);
        }
    }
    private static Vector3 Feet(Transform t)
    {
        if (t.GetComponent<Character>() != null) return SkillVfxEffect.FootPosition(t);
        var renderer = t.GetComponentInChildren<Renderer>();
        return renderer != null ? new Vector3(t.position.x, renderer.bounds.min.y, t.position.z) : t.position;
    }
    private void SaveFrame()
    {
        const string folder = "Captures/VfxArt";
        Directory.CreateDirectory(folder);
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderFrame(_time, _views[1]); RenderTexture.active = _views[1];
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            string path = $"{folder}/{Skills[_skill]}-{(_cast ? "cast" : "impact")}-{_time:0.00}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); _message = Path.GetFullPath(path);
        }
        finally { RenderTexture.active = previous; DestroyImmediate(texture); }
    }
    private void OnPlayMode(PlayModeStateChange state) => Cleanup();
    private void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene) => Cleanup();
    private void OnDisable()
    {
        EditorApplication.update -= Advance; EditorApplication.playModeStateChanged -= OnPlayMode;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= OnSceneClosing;
        Cleanup();
    }
    private void Cleanup()
    {
        foreach (var fx in _effects) if (fx != null) DestroyImmediate(fx.gameObject);
        for (int i = 0; i < _views.Length; i++)
            if (_views[i] != null) { _views[i].Release(); DestroyImmediate(_views[i]); _views[i] = null; }
    }
}
