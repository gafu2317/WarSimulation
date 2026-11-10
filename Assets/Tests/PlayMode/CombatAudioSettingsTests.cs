using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatAudioSettingsTests
{
    [UnityTest]
    public IEnumerator DefensiveSounds_PlayOnDefenseInsteadOfCast()
    {
        var player = CombatAudioPlayer.Instance;
        var original = player.Settings;
        var settings = Object.Instantiate(original);
        typeof(CombatAudioPlayer).GetProperty("Settings").SetValue(player, settings);
        var flowObject = new GameObject("DefenseAudioFlow", typeof(CombatBattleFlow));
        typeof(CombatBattleFlow).GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(flowObject.GetComponent<CombatBattleFlow>(), CombatBattleState.Running);
        var clip = AudioClip.Create("DefenseTest", 44100, 1, 44100, false);
        var victim = new GameObject("DefenseAudioVictim", typeof(Character));
        try
        {
            foreach (var skill in new[] { SkillId.Bible_Gotsume, SkillId.Shield_ShoulderGuard, SkillId.Shield_IronWall })
            {
                settings.SetClip(skill, clip);
                player.StopBattle();
                var action = new CombatSkillActionInfo(1, null, CombatSkillFactory.Create(skill), default, 0);
                CombatSkillActionEvents.Execute(action, () => { });
                Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying), Is.False, skill.ToString());
                var source = new CombatEffectSource(null, skill, "Defense", skill == SkillId.Bible_Gotsume);
                if (skill == SkillId.Bible_Gotsume)
                    CombatDamageEvents.RaiseDamageApplied(victim.GetComponent<Character>(), 1, source);
                else
                    CombatDamageEvents.RaiseDamagePrevented(victim.GetComponent<Character>(), 1, default, source);
                Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying), Is.True, skill.ToString());
            }
        }
        finally
        {
            player.StopBattle();
            typeof(CombatAudioPlayer).GetProperty("Settings").SetValue(player, original);
            Object.Destroy(settings);
            Object.Destroy(clip);
            Object.Destroy(victim);
            Object.Destroy(flowObject);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator AudioTable_AssignsPreviewsClearsAndLoopsBgm()
    {
        var canvasObject = new GameObject("AudioTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var eventSystemObject = new GameObject("AudioTestEvents", typeof(EventSystem));
        var player = CombatAudioPlayer.Instance;
        var originalSettings = player.Settings;
        var settings = Object.Instantiate(originalSettings);
        typeof(CombatAudioPlayer).GetProperty("Settings").SetValue(player, settings);
        var listener = new GameObject("AudioTestListener", typeof(AudioListener));
        var flowObject = new GameObject("AudioTestFlow");
        var flow = flowObject.AddComponent<CombatBattleFlow>();
        typeof(CombatBattleFlow).GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(flow, CombatBattleState.Running);
        var previousBgm = settings.BattleBgm;
        var previousSe = settings.GetClip(SkillId.Sword_Slash);
        var clip = AudioClip.Create("AudioTest", 44100 * 3, 1, 44100, false);
        settings.Library.Add(clip);
        try
        {
            CombatAudioSettingsView.Open(canvasObject.transform);
            yield return null;
            var view = canvasObject.GetComponentInChildren<CombatAudioSettingsView>();
            var slots = view.GetComponentsInChildren<CombatAudioDrag>().Where(x => x.Assign != null).ToArray();
            Assert.That(slots.Length, Is.EqualTo(System.Enum.GetValues(typeof(SkillId)).Length - 1));
            var source = view.GetComponentsInChildren<CombatAudioDrag>().Single(x => x.Clip == clip);
            var libraryPlay = source.GetComponent<Button>();
            libraryPlay.onClick.Invoke();
            Assert.That(player.IsPreviewPlaying, Is.True);
            libraryPlay.onClick.Invoke();
            Assert.That(player.IsPreviewPlaying, Is.False);
            var pointer = new PointerEventData(null);
            source.OnBeginDrag(pointer);
            var bgmTab = view.GetComponentsInChildren<Button>().Single(x => x.gameObject.name == "BGMTab");
            var seTab = view.GetComponentsInChildren<Button>().Single(x => x.gameObject.name == "SETab");
            bgmTab.onClick.Invoke();
            Assert.That(player.IsPreviewPlaying, Is.False);
            var bgmSlots = view.GetComponentsInChildren<CombatAudioDrag>().Where(x => x.Assign != null).ToArray();
            Assert.That(bgmSlots.Length, Is.EqualTo(4));
            bgmSlots[1].OnDrop(pointer);
            source.OnEndDrag(pointer);
            Assert.That(settings.BattleBgm, Is.SameAs(clip));
            player.StartBattle();
            var bgm = player.GetComponents<AudioSource>().Single(x => x.loop);
            Assert.That(bgm.clip, Is.SameAs(clip));
            Assert.That(bgm.isPlaying, Is.True);
            player.StopBattle();
            Assert.That(bgm.isPlaying, Is.False);
            bgmSlots[0].Assign(clip);
            Assert.That(settings.GetBgm(CombatAudioSettings.BgmScene.Formation), Is.SameAs(clip));
            var formationRow = bgmSlots[0].transform.parent;
            formationRow.GetComponentsInChildren<Slider>().Single().value = .4f;
            formationRow.GetComponentsInChildren<Button>()[1].onClick.Invoke();
            Assert.That(player.GetComponents<AudioSource>().Single(x => x.isPlaying).volume,
                Is.EqualTo(settings.BgmVolume * .4f).Within(.001f));
            seTab.onClick.Invoke();
            Assert.That(player.IsPreviewPlaying, Is.False);
            Canvas.ForceUpdateCanvases();
            var position = RectTransformUtility.WorldToScreenPoint(null, slots[0].transform.position);
            Assert.That(view.GetComponent<CombatBrowserAudioImport>().AssignAt(clip, position), Is.True);
            Assert.That(settings.GetClip(SkillId.Sword_Slash), Is.SameAs(clip));
            var action = new CombatSkillActionInfo(1, null, CombatSkillFactory.Create(SkillId.Sword_Slash), default, 0);
            CombatSkillActionEvents.Execute(action, () => { });
            Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying), Is.True);
            player.StopBattle();
            var row = slots[0].transform.parent;
            row.GetComponentsInChildren<Button>().Last().onClick.Invoke();
            Assert.That(settings.GetClip(SkillId.Sword_Slash), Is.Null);
            settings.SetVolume(SkillId.Sword_Slash, .25f);
            settings.SetClipVolume(clip, .5f);
            settings.SetClip(SkillId.Sword_Slash, clip);
            Assert.That(settings.GetVolume(SkillId.Sword_Slash), Is.EqualTo(.25f));
            player.Preview(clip, false, SkillId.Sword_Slash);
            var previewSource = player.GetComponents<AudioSource>().Single(x => x.isPlaying);
            Assert.That(previewSource.volume, Is.EqualTo(settings.SeVolume * .25f * .5f).Within(.001f));
            settings.SetVolume(SkillId.Sword_Slash, .5f);
            player.RefreshVolumes();
            Assert.That(previewSource.volume, Is.EqualTo(settings.SeVolume * .5f * .5f).Within(.001f));
            Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying && x.clip == clip), Is.True);
            player.StopPreview();
            Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying), Is.False);
            Canvas.ForceUpdateCanvases();
            foreach (var rowRect in view.GetComponentsInChildren<HorizontalLayoutGroup>())
            {
                float expectedHeight = rowRect.GetComponent<LayoutElement>().preferredHeight;
                Assert.That(((RectTransform)rowRect.transform).rect.height, Is.EqualTo(expectedHeight).Within(0.1f));
            }
            Assert.That(view.GetComponent<Image>().color.a, Is.EqualTo(1));
            var grid = view.GetComponentInChildren<GridLayoutGroup>();
            Assert.That(grid.constraintCount, Is.EqualTo(3));
            Assert.That(grid.cellSize.x * 3 + grid.spacing.x * 2,
                Is.EqualTo(((RectTransform)grid.transform.parent).rect.width).Within(.1f));
            var libraryGrid = view.GetComponentsInChildren<GridLayoutGroup>().Single(x => x.constraintCount == 4);
            Assert.That(libraryGrid.transform.parent.GetComponent<LayoutElement>().flexibleHeight, Is.EqualTo(1));
            Assert.That(grid.transform.parent.GetComponent<LayoutElement>().flexibleHeight, Is.Zero);
        }
        finally
        {
            settings.SetClip(SkillId.None, previousBgm);
            settings.SetClip(SkillId.Sword_Slash, previousSe);
            settings.Library.Remove(clip);
            settings.Save();
            player.StopBattle();
            Object.Destroy(canvasObject);
            Object.Destroy(eventSystemObject);
            Object.Destroy(flowObject);
            Object.Destroy(listener);
            Object.Destroy(clip);
            typeof(CombatAudioPlayer).GetProperty("Settings").SetValue(player, originalSettings);
            Object.Destroy(settings);
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator ClosingAudioUiAndBattleAfterPlayerDestruction_DoesNotRecreateAudioHost()
    {
        var canvas = new GameObject("AudioShutdownCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var flow = new GameObject("AudioShutdownFlow", typeof(CombatBattleFlow));
        CombatAudioSettingsView.Open(canvas.transform);
        Object.Destroy(CombatAudioPlayer.Instance.gameObject);
        yield return null;
        Object.Destroy(canvas);
        Object.Destroy(flow);
        yield return null;
        Assert.That(CombatAudioPlayer.Existing, Is.Null);
        Assert.That(Object.FindObjectsByType<CombatAudioPlayer>().Length, Is.Zero);
    }

}
