using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatSkillTargetingTests
{
    [Test]
    public void GetEnemiesInRadius_ReturnsOnlyLivingEnemiesInRange()
    {
        TargetingFixture fixture = TargetingFixture.Create();
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            IReadOnlyList<Character> targets = CombatSkillTargeting.GetEnemiesInRadius(
                fixture.Owner,
                fixture.Owner.transform.position,
                radius: 3f);

            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(targets, Has.Member(fixture.NearEnemy));
            Assert.That(targets, Has.No.Member(fixture.FarEnemy));
            Assert.That(targets, Has.No.Member(fixture.Ally));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void GetAlliesInRadius_OptionallyIncludesSelf()
    {
        TargetingFixture fixture = TargetingFixture.Create();
        try
        {
            IReadOnlyList<Character> withoutSelf = CombatSkillTargeting.GetAlliesInRadius(
                fixture.Owner,
                fixture.Owner.transform.position,
                radius: 3f,
                includeSelf: false);
            IReadOnlyList<Character> withSelf = CombatSkillTargeting.GetAlliesInRadius(
                fixture.Owner,
                fixture.Owner.transform.position,
                radius: 3f,
                includeSelf: true);

            Assert.That(withoutSelf, Has.Count.EqualTo(1));
            Assert.That(withoutSelf, Has.Member(fixture.Ally));
            Assert.That(withoutSelf, Has.No.Member(fixture.Owner));
            Assert.That(withSelf, Has.Count.EqualTo(2));
            Assert.That(withSelf, Has.Member(fixture.Owner));
            Assert.That(withSelf, Has.Member(fixture.Ally));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CreateEnemyAreaContext_PopulatesTargetPointAndResolvedTargets()
    {
        TargetingFixture fixture = TargetingFixture.Create();
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            Vector3 point = fixture.Owner.transform.position;
            SkillExecutionContext context = CombatSkillTargeting.CreateEnemyAreaContext(
                fixture.Owner,
                point,
                radius: 3f);

            Assert.That(context.HasTargetPoint, Is.True);
            Assert.That(context.TargetPoint, Is.EqualTo(point));
            Assert.That(context.ResolvedTargets, Has.Count.EqualTo(1));
            Assert.That(context.ResolvedTargets, Has.Member(fixture.NearEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CreateRecognizedEnemiesContext_PopulatesRecognizedEnemyTargets()
    {
        TargetingFixture fixture = TargetingFixture.Create();
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            SkillExecutionContext context = CombatSkillTargeting.CreateRecognizedEnemiesContext(fixture.Owner);

            Assert.That(context.ResolvedTargets, Has.Count.EqualTo(2));
            Assert.That(context.ResolvedTargets, Has.Member(fixture.NearEnemy));
            Assert.That(context.ResolvedTargets, Has.Member(fixture.FarEnemy));
            Assert.That(context.PrimaryTarget, Is.EqualTo(fixture.NearEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CreateRecognizedEnemiesContext_IncludesRememberedHiddenEnemyTargets()
    {
        TargetingFixture fixture = TargetingFixture.Create();
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            fixture.Owner.Vision.UpdateVision();
            fixture.FarEnemy.StatusEffects.ApplyStealth(5f);

            SkillExecutionContext context = CombatSkillTargeting.CreateRecognizedEnemiesContext(fixture.Owner);

            Assert.That(context.ResolvedTargets, Has.Count.EqualTo(2));
            Assert.That(context.ResolvedTargets, Has.Member(fixture.NearEnemy));
            Assert.That(context.ResolvedTargets, Has.Member(fixture.FarEnemy));
            Assert.That(context.PrimaryTarget, Is.EqualTo(fixture.NearEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void GetEnemiesInRadius_UsesHorizontalRadiusForElevatedTargets()
    {
        TargetingFixture fixture = TargetingFixture.Create();
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.System);
            fixture.Owner.Vision.Initialize();
            fixture.NearEnemyGo.transform.position = new Vector3(0f, 2.5f, 2.5f);

            IReadOnlyList<Character> targets = CombatSkillTargeting.GetEnemiesInRadius(
                fixture.Owner,
                fixture.Owner.transform.position,
                radius: 3f);

            Assert.That(targets, Has.Member(fixture.NearEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    private sealed class TargetingFixture
    {
        public GameObject SystemGo;
        public GameObject OwnerGo;
        public GameObject AllyGo;
        public GameObject NearEnemyGo;
        public GameObject FarEnemyGo;
        public CombatCharacterSystem System;
        public Character Owner;
        public Character Ally;
        public Character NearEnemy;
        public Character FarEnemy;

        public static TargetingFixture Create()
        {
            var fixture = new TargetingFixture
            {
                SystemGo = new GameObject("CombatCharacterSystem"),
                OwnerGo = new GameObject("Owner"),
                AllyGo = new GameObject("Ally"),
                NearEnemyGo = new GameObject("NearEnemy"),
                FarEnemyGo = new GameObject("FarEnemy"),
            };

            fixture.System = fixture.SystemGo.AddComponent<CombatCharacterSystem>();

            fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
            fixture.Owner.SetTeam(CombatTeam.Ally);
            fixture.Owner.Health.Initialize(30);

            fixture.Ally = fixture.AllyGo.AddComponent<Character>();
            fixture.Ally.SetTeam(CombatTeam.Ally);
            fixture.Ally.Health.Initialize(30);
            fixture.AllyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.right * 2f;

            fixture.NearEnemy = fixture.NearEnemyGo.AddComponent<Character>();
            fixture.NearEnemy.SetTeam(CombatTeam.Enemy);
            fixture.NearEnemy.Health.Initialize(30);
            fixture.NearEnemyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.forward * 2f;

            fixture.FarEnemy = fixture.FarEnemyGo.AddComponent<Character>();
            fixture.FarEnemy.SetTeam(CombatTeam.Enemy);
            fixture.FarEnemy.Health.Initialize(30);
            fixture.FarEnemyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.forward * 6f;

            fixture.System.AllyCharacters.Add(fixture.Owner);
            fixture.System.AllyCharacters.Add(fixture.Ally);
            fixture.System.EnemyCharacters.Add(fixture.NearEnemy);
            fixture.System.EnemyCharacters.Add(fixture.FarEnemy);
            fixture.System.AssignTeamsFromLists();

            return fixture;
        }

        public void Destroy()
        {
            if (FarEnemyGo != null) Object.DestroyImmediate(FarEnemyGo);
            if (NearEnemyGo != null) Object.DestroyImmediate(NearEnemyGo);
            if (AllyGo != null) Object.DestroyImmediate(AllyGo);
            if (OwnerGo != null) Object.DestroyImmediate(OwnerGo);
            if (SystemGo != null) Object.DestroyImmediate(SystemGo);
        }
    }
}
