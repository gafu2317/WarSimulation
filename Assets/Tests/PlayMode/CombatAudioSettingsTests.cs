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
    public IEnumerator AudioTable_AssignsPreviewsClearsAndLoopsBgm()
    {
        var canvasObject = new GameObject("AudioTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
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
            Assert.That(slots.Length, Is.EqualTo(System.Enum.GetValues(typeof(SkillId)).Length));
            var source = view.GetComponentsInChildren<CombatAudioDrag>().Single(x => x.Clip == clip);
            var pointer = new PointerEventData(null);
            source.OnBeginDrag(pointer);
            slots[0].OnDrop(pointer);
            source.OnEndDrag(pointer);
            Assert.That(settings.BattleBgm, Is.SameAs(clip));
            player.StartBattle();
            var bgm = player.GetComponents<AudioSource>().Single(x => x.loop);
            Assert.That(bgm.clip, Is.SameAs(clip));
            Assert.That(bgm.isPlaying, Is.True);
            player.StopBattle();
            Assert.That(bgm.isPlaying, Is.False);
            slots[1].Assign(clip);
            Assert.That(settings.GetClip(SkillId.Sword_Slash), Is.SameAs(clip));
            var action = new CombatSkillActionInfo(1, null, CombatSkillFactory.Create(SkillId.Sword_Slash), default, 0);
            CombatSkillActionEvents.Execute(action, () => { });
            Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying), Is.True);
            player.StopBattle();
            var row = slots[1].transform.parent;
            row.GetComponentsInChildren<Button>().Last().onClick.Invoke();
            Assert.That(settings.GetClip(SkillId.Sword_Slash), Is.Null);
            player.Preview(clip, false);
            Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying && x.clip == clip), Is.True);
            player.StopPreview();
            Assert.That(player.GetComponents<AudioSource>().Any(x => x.isPlaying), Is.False);
            Canvas.ForceUpdateCanvases();
            var grid = view.GetComponentInChildren<GridLayoutGroup>();
            Assert.That(grid.constraintCount, Is.EqualTo(3));
            Assert.That(grid.cellSize.x, Is.GreaterThan(240));
        }
        finally
        {
            settings.SetClip(SkillId.None, previousBgm);
            settings.SetClip(SkillId.Sword_Slash, previousSe);
            settings.Library.Remove(clip);
            settings.Save();
            player.StopBattle();
            Object.Destroy(canvasObject);
            Object.Destroy(flowObject);
            Object.Destroy(listener);
            Object.Destroy(clip);
            typeof(CombatAudioPlayer).GetProperty("Settings").SetValue(player, originalSettings);
            Object.Destroy(settings);
        }
        yield return null;
    }
}
