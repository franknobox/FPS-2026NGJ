using System.Collections;
using System.Collections.Generic;
using NGJ.Combat;
using SlugWhitebox;
using UnityEngine;
using UnityEngine.AI;

namespace Wasteland
{
    public sealed class WastelandRunDirector : SlugLevelFlow
    {
        public int seed = 20261115;
        public Vector3 playerStart = new Vector3(0, .15f, -31);
        public WastelandArea[] areas;
        public Material spawnMaterial;
        [Min(.2f)] public float spawnWarning = .8f;
        public int ActiveArea { get; private set; } = -1;
        public int CompletedAreas { get; private set; }
        public int Wave { get; private set; }
        public bool Spawning { get; private set; }
        SlugGame owner;
        int runIndex, variantIndex;
        System.Random random;
        readonly List<GameObject> cues = new List<GameObject>();
        public override Vector3 StartPosition => playerStart;
        public override string Title => "循声虫族 / 荒野前哨";
        public override int AreaCount => areas.Length;
        public override string ProgressText => ActiveArea >= 0 ? areas[ActiveArea].displayName + " · 第 " + Wave + "/2 波" :
            CompletedAreas == areas.Length ? "区域全部肃清 · 前往 EXIT" : "缓冲区 · 前往下一处入口";

        public override void ResetRun(SlugGame game)
        {
            StopAllCoroutines();
            foreach (var cue in cues) if (cue) Destroy(cue);
            cues.Clear();
            owner = game;
            random = new System.Random(seed + runIndex++ * 7919);
            CompletedAreas = Wave = variantIndex = 0;
            ActiveArea = -1; Spawning = false;
            foreach (var area in areas) { area.entrance.SetClosed(false); area.exit.SetClosed(true); }
            game.Message("前往 01 入口 · 1–4 / 滚轮切换武器 · 清场后开放通路", 5);
        }

        public override void Tick(SlugGame game)
        {
            if (ActiveArea < 0)
            {
                if(game.progression && game.progression.IntermissionPending)return;
                if (CompletedAreas < areas.Length && areas[CompletedAreas].Contains(game.PlayerPosition, 3)) Activate(CompletedAreas);
                else if (CompletedAreas == areas.Length && Vector3.Distance(game.PlayerPosition, game.exit.position) < 4)
                    game.EndRun("荒野前哨回收完成 / 按 R 再来一局");
                return;
            }
            if (Spawning || game.Remaining > 0) return;
            if (Wave < 2) StartCoroutine(SpawnWave(true));
            else
            {
                var area = areas[ActiveArea];
                area.entrance.SetClosed(false); area.exit.SetClosed(false);
                CompletedAreas++;
                ActiveArea = -1;
                game.Message("区域肃清 · 拾取声腺后进入缓冲区", 4);
                game.progression?.AreaCleared(CompletedAreas<areas.Length);
            }
        }

        void Activate(int index)
        {
            ActiveArea = index; Wave = 0;
            owner.sector = index;
            areas[index].entrance.SetClosed(true);
            owner.Message(areas[index].displayName + " · 入口封锁", 3);
            StartCoroutine(SpawnWave(false));
        }

        IEnumerator SpawnWave(bool delay)
        {
            Spawning = true;
            if (delay) { owner.Message("下一波即将抵达", 2); yield return new WaitForSeconds(2); }
            Wave++;
            var area = areas[ActiveArea];
            int count = Wave == 1 ? area.firstWave : area.secondWave;
            var used = new HashSet<Transform>();
            for (int i = 0; i < count; i++)
            {
                int kind = KindForSpawn(ActiveArea, Wave, i, count);
                Transform[] candidates = kind == 0 ? area.airSpawns : area.groundSpawns;
                Transform marker = null;
                for (int attempt = 0; attempt < candidates.Length * 3; attempt++)
                {
                    var proposed = candidates[random.Next(candidates.Length)];
                    if (!used.Contains(proposed) && Vector3.Distance(proposed.position, owner.PlayerPosition) >= 9) { marker = proposed; break; }
                }
                if (!marker)
                {
                    float farthest = -1;
                    foreach (var candidate in candidates)
                    {
                        float distance = Vector3.Distance(candidate.position, owner.PlayerPosition);
                        if (!used.Contains(candidate) && distance > farthest) { marker = candidate; farthest = distance; }
                    }
                }
                if (!marker) throw new System.InvalidOperationException("No valid reserved spawn in " + area.displayName);
                used.Add(marker);
                Vector3 position = marker.position;
                var cue = CreateCue(position); cues.Add(cue);
                yield return new WaitForSeconds(spawnWarning);
                if (cue) Destroy(cue); cues.Remove(cue);
                var prefab = owner.enemyPrefabs[kind];
                if (kind == 1 && owner.miYinVariants != null && owner.miYinVariants.Length > 0)
                    prefab = owner.miYinVariants[variantIndex++ % owner.miYinVariants.Length];
                if (kind != 0)
                {
                    if (!NavMesh.SamplePosition(position, out var hit, 1, NavMesh.AllAreas))
                        throw new System.InvalidOperationException("Spawn lost navigation: " + marker.name);
                    position = hit.position;
                }
                var actor = Instantiate(prefab, position, Quaternion.identity);
                actor.hasMovementBounds = true;
                actor.movementBounds = area.bounds;
                var adapter = actor.GetComponent<SlugEnemy>() ?? actor.gameObject.AddComponent<SlugEnemy>();
                adapter.Initialize(owner, kind);
                owner.enemies.Add(adapter);
            }
            Spawning = false;
        }

        // 先以眼虫教学，再加入少量精英；Boss 只在终区末波出现一次。
        public static int KindForSpawn(int area, int wave, int index, int count)
        {
            if(area <= 0)return 1;
            bool boss = area >= 2 && wave == 2;
            if(boss && index == count-1)return 2;
            int elites = area == 1 && wave == 1 ? 1 : 2;
            return index >= count-(boss?1:0)-elites ? 0 : 1;
        }

        GameObject CreateCue(Vector3 point)
        {
            var go = new GameObject("敌人出现预警");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = spawnMaterial;
            line.loop = true; line.positionCount = 32;
            line.startWidth = line.endWidth = .065f;
            line.startColor = line.endColor = new Color(1, .2f, .35f);
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32;
                line.SetPosition(i, new Vector3(point.x + Mathf.Cos(a) * .9f, .045f, point.z + Mathf.Sin(a) * .9f));
            }
            return go;
        }
        public override void StopRun() { StopAllCoroutines(); foreach (var cue in cues) if (cue) Destroy(cue); cues.Clear(); Spawning=false; }
        void OnDisable() { StopRun(); }
    }
}
