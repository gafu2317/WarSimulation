#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

// Play-mode-only capture rig. It never saves scene or gameplay configuration changes.
public sealed class SkillVfxAudit : MonoBehaviour
{
    public string Status { get; private set; } = "Idle";
    public int Completed { get; private set; }
    private Camera _camera;
    private RenderTexture _rt;
    private Texture2D _texture;
    private readonly List<string> _results = new();
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private string _folder;
    private CombatSkillActionResult _lastAction;

    public void BeginPreview()
    {
        _folder="Captures/Vfx/EffectTest";
        PrepareCapture();
        StartCoroutine(Preview());
    }

    public void BeginCombat()
    {
        _folder="Captures/Vfx/Combat";
        PrepareCapture();
        StartCoroutine(Combat());
    }

    private void PrepareCapture()
    {
        Time.timeScale=1;
        Application.runInBackground=true;
        _camera=Camera.main;
        _rt=new RenderTexture(960,540,24);
        _texture=new Texture2D(960,540,TextureFormat.RGB24,false);
        Directory.CreateDirectory(_folder);
        _results.Clear(); _results.Add("skill,result,frames,elapsed_seconds,remaining_vfx"); Completed=0;
    }

    private IEnumerator Preview()
    {
        var player=FindAnyObjectByType<SkillVfxViewer>().GetComponent<SkillVfxPlayer>();
        Vector3 self=GameObject.Find("Caster").transform.position; self.y=0;
        Vector3 target=GameObject.Find("Target").transform.position; target.y=0;
        foreach(SkillId id in Enum.GetValues(typeof(SkillId)))
        {
            if(id==SkillId.None) continue;
            player.ClearAll();
            player.TryPlay(id,self,target,target,out string message);
            yield return CaptureSequence(id,SkillVfxEffect.IsPersistent(id)?5.4f:2f,player,message);
        }
        File.WriteAllLines(_folder+"/results.csv",_results);
        Status="Preview complete";
    }

    private IEnumerator Combat()
    {
        var characters=FindObjectsByType<Character>();
        Character actor=null,ally=null,enemy=null;
        foreach(var c in characters)
        {
            var brain=c.GetComponent<CombatAiBrain>(); if(brain!=null) brain.enabled=false;
            c.SkillCaster.ClearCast(); c.StopMoving();
            if(c.Team==CombatTeam.Ally) { if(actor==null) actor=c; else if(ally==null) ally=c; }
            else if(enemy==null) enemy=c;
        }
        if(actor==null || ally==null || enemy==null) { Status="Missing combat participants"; yield break; }
        // Keep the real battle camera. Place the fixture on the existing navigable battlefield.
        Vector3 center=enemy.transform.position;
        Place(actor,center+Vector3.left*2.5f); Place(ally,center+Vector3.right*2.5f);
        var player=GameObject.Find("SkillVfxRuntime").GetComponent<SkillVfxPlayer>();
        CombatSkillActionEvents.Completed+=OnActionCompleted;
        foreach(SkillId id in Enum.GetValues(typeof(SkillId)))
        {
            if(id==SkillId.None) continue;
            _lastAction=null;
            foreach(var c in characters)
            {
                c.StatusEffects.ClearAll(); c.SkillCaster.ClearCast();
                c.Health.Initialize(500); c.Health.TakeDamage(100);
            }
            foreach(var effect in FindObjectsByType<ShieldShoulderGuardEffect>()) effect.CancelImmediate();
            foreach(var effect in FindObjectsByType<BibleGotsumeEffect>()) effect.CancelImmediate();
            foreach(var effect in FindObjectsByType<BibleCarryRushEffect>()) effect.CancelImmediate();
            foreach(var zone in FindObjectsByType<RosaryHealingAreaZone>()) zone.CancelImmediate();
            player.ClearAll();
            yield return null;
            SkillBase skill=CombatSkillFactory.Create(id);
            Character recipient=skill.TargetKind is SkillTargetKind.Ally or SkillTargetKind.AllyOrSelf ? ally :
                skill.TargetKind==SkillTargetKind.Self ? actor : enemy;
            SkillExecutionContext context=skill.TargetKind is SkillTargetKind.Point or SkillTargetKind.Area
                ? SkillExecutionContext.ForPoint(recipient.transform.position,new[]{recipient})
                : skill.TargetKind==SkillTargetKind.RecognizedEnemies ? SkillExecutionContext.ForTargets(new[]{enemy})
                : SkillExecutionContext.ForTarget(recipient);
            actor.SkillCooldowns.ResetCooldown(skill);
            bool started=actor.SkillCaster.TryStartCast(skill,context);
            yield return CaptureSequence(id,skill.CastTimeSeconds+(SkillVfxEffect.IsPersistent(id)?5.5f:2f),player,started?"cast started":"CAST FAILED");
        }
        File.WriteAllLines(_folder+"/results.csv",_results);
        CombatSkillActionEvents.Completed-=OnActionCompleted;
        Status="Combat complete";
    }

    private static void Place(Character c,Vector3 position)
    {
        var agent=c.GetComponent<NavMeshAgent>();
        if(NavMesh.SamplePosition(position,out var hit,6,NavMesh.AllAreas) && agent!=null && agent.isOnNavMesh) agent.Warp(hit.position);
    }

    private IEnumerator CaptureSequence(SkillId id,float duration,SkillVfxPlayer player,string result)
    {
        string folder=_folder+"/"+id; Directory.CreateDirectory(folder);
        float start=Time.time,next=0; int frame=0;
        while(Time.time-start<duration)
        {
            Status=$"{Completed+1}/29 {id}: {Time.time-start:0.0}s";
            if(Time.time-start>=next)
            {
                Capture(folder+"/"+frame.ToString("D4")+".png"); frame++; next+=.1f;
            }
            yield return null;
        }
        if(_folder.EndsWith("Combat")) result+=_lastAction!=null ? " / "+_lastAction.Outcome+" / effects="+_lastAction.Effects.Count : " / NO COMPLETION";
        _results.Add($"{id},{result},{frame},{(Time.time-start).ToString("0.000",Culture)},{player.ActiveCount}");
        Completed++;
        File.WriteAllLines(_folder+"/results.csv",_results);
    }

    private void OnActionCompleted(CombatSkillActionResult result) => _lastAction=result;

    private void Capture(string path)
    {
        RenderTexture oldTarget=_camera.targetTexture,oldActive=RenderTexture.active;
        _camera.targetTexture=_rt; _camera.Render(); RenderTexture.active=_rt;
        _texture.ReadPixels(new Rect(0,0,960,540),0,0); _texture.Apply();
        File.WriteAllBytes(path,_texture.EncodeToPNG());
        _camera.targetTexture=oldTarget; RenderTexture.active=oldActive;
    }

    public void BeginPerformance() => StartCoroutine(Performance());
    private IEnumerator Performance()
    {
        var host=new GameObject("VFX performance fixture"); var player=host.AddComponent<SkillVfxPlayer>();
        var rows=new List<string>{"mode,frames,median_frame_ms,p95_frame_ms,median_vfx_cpu_ms,p95_vfx_cpu_ms,median_draw_calls,peak_active,pool,drops"};
        using var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"SkillVfx.Update",1);
        int[] loads={0,4,32,32};
        var ids=new[]{SkillId.Wand_AreaBlast,SkillId.Wand_ArcaneBlast,SkillId.Grimoire_Bind,SkillId.Rosary_CloseHeal};
        for(int mode=0;mode<loads.Length;mode++)
        {
            int load=loads[mode]; player.ClearAll();
            var frames=new List<float>(); var times=new List<float>(); var batches=new List<float>();
            float start=Time.realtimeSinceStartup,next=0;
            while(Time.realtimeSinceStartup-start<8)
            {
                float elapsed=Time.realtimeSinceStartup-start;
                Status=$"Performance {mode}: {elapsed:0.0}s";
                if(load>0 && elapsed>=next)
                {
                    player.ClearAll();
                    for(int i=0;i<load;i++)
                    {
                        Vector3 p=new((i%8-4)*.6f,0,(i/8)*.6f);
                        if(CombatSceneContext.Instance!=null) p+=new Vector3(24,0,50);
                        player.TryPlay(ids[i%4],p-Vector3.right*2,p,p,out _);
                    }
                    next+=1.2f;
                }
                if(elapsed>2)
                {
                    frames.Add(Time.unscaledDeltaTime*1000);
                    times.Add(cpu.Valid?cpu.LastValue/1000000f:-1);
                    batches.Add(UnityStats.drawCalls);
                }
                yield return null;
            }
            frames.Sort();times.Sort();batches.Sort();
            rows.Add(string.Join(",",new[]{mode.ToString(),frames.Count.ToString(),Percentile(frames,.5f),Percentile(frames,.95f),
                Percentile(times,.5f),Percentile(times,.95f),Percentile(batches,.5f),player.PeakActiveCount.ToString(),player.PooledCount.ToString(),player.DroppedCount.ToString()}));
        }
        player.ClearAll(); rows.Add("remaining_active,"+player.ActiveCount);
        File.WriteAllLines("Captures/Vfx/performance-"+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+".csv",rows); Destroy(host); Status="Performance complete";
    }
    private static string Percentile(List<float> values,float p) => values[Mathf.Min(values.Count-1,(int)(values.Count*p))].ToString("0.000",Culture);
    private void OnDestroy()
    {
        CombatSkillActionEvents.Completed-=OnActionCompleted;
        if(_rt!=null) { _rt.Release(); DestroyImmediate(_rt); }
        if(_texture!=null) DestroyImmediate(_texture);
    }
}
#endif
